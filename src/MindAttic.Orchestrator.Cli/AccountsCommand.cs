using System.ComponentModel;
using System.Text.Json;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>
/// <c>orchestrator accounts pick</c> (M.ORCH-T605): the Auto login (<see cref="LoginPicker"/>) a new Claude CLI should start
/// on, for the Launcher to set as its <c>CLAUDE_CONFIG_DIR</c>. Prints exactly one line, the login's full folder, or an empty
/// line for the global login (also when Auto is off); exits 0 either way. The pick is held for the CLI about to start, so the
/// next pick goes elsewhere. <c>--json</c> writes <c>{"configDir", "email", "reason"}</c> instead.
/// </summary>
internal sealed class AccountsPickCommand : Command<AccountsPickCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--json")]
        [Description("Write {configDir, email, reason} as JSON instead of the bare folder.")]
        public bool Json { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        LoginPick pick;
        try
        {
            pick = Ctx.Settings.AutoLogin
                ? new LoginPicker(AccountsCommand.LoginsOverride?.Invoke(), StartCommand.LiveOverride) { Allowances = () => WeeklyAllowances.Active(Ctx.Store(), DateTime.UtcNow), Cutoffs = () => UsageCutoffs.Load(Ctx.Store()) }.Pick(DateTime.UtcNow, claim: true, Ctx.Settings.WeeklyStopPercent)
                : new LoginPick(null, "Auto login is off (settings --auto-login): no per-CLI login");
        }
        // The contract is one line and exit 0: anything unreadable means no per-CLI login.
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            pick = new LoginPick(null, $"couldn't pick ({ex.Message}): no per-CLI login");
        }
        if (pick.Account is { } account)
            try { (AccountsCommand.LoginsOverride?.Invoke() ?? new AccountDirs()).Prepare(account); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { pick = new LoginPick(null, $"couldn't prepare {account.Email}'s login ({ex.Message}): no per-CLI login"); }
        // Plain Console, not AnsiConsole: the Launcher reads this line as is, with no markup or wrapping.
        Console.WriteLine(settings.Json
            ? JsonSerializer.Serialize(new { configDir = pick.ConfigDir, email = pick.Account?.Email, reason = pick.Reason })
            : pick.ConfigDir);
        return 0;
    }
}

/// <summary>
/// <c>orchestrator accounts alias &lt;email&gt; &lt;name&gt;</c> (M.ORCH-T614): names a Claude login, as the Accounts view's Rename
/// does. The name is kept in the login's own folder (<see cref="LoginLook.FileName"/> in its <c>~/.claude-accounts</c> folder,
/// and in <c>~/.claude</c> while the global login is signed in as it), stamped with the email, so the Launcher and the
/// account's status line read it without the Orchestrator. Without a name it says the login's name; <c>--reset</c> goes back
/// to the email's part before the @.
/// </summary>
internal sealed class AccountsAliasCommand : Command<AccountsAliasCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<EMAIL>")]
        [Description("The login's email.")]
        public string Email { get; init; } = "";

        [CommandArgument(1, "[NAME]")]
        [Description("Its name (quote one with spaces).")]
        public string? Name { get; init; }

        [CommandOption("--reset")]
        [Description("Go back to the email's part before the @.")]
        public bool Reset { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var dirs = AccountsCommand.LoginsOverride?.Invoke() ?? new AccountDirs();
        if (AccountDirs.Normalize(settings.Email) is not { } email)
        {
            AnsiConsole.MarkupLine($"[red]\"{Markup.Escape(settings.Email)}\" isn't an email address.[/]");
            return 1;
        }
        var own = dirs.For(email);
        var isGlobal = LoginLook.GlobalEmail(dirs) is { } g && string.Equals(g, email, StringComparison.OrdinalIgnoreCase);
        var folder = Directory.Exists(own.Directory) ? own.Directory : isGlobal ? dirs.ClaudeDir : null;
        if (folder is null)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(email)} has no login here: neither a per-CLI one (orchestrator login --account {Markup.Escape(email)}) nor the global one.[/]");
            return 1;
        }
        if (settings.Name is null && !settings.Reset)
        {
            Console.WriteLine(LoginLook.For(folder, email).Alias);
            return 0;
        }
        IReadOnlyList<string> written;
        // The same call as the dashboard's Rename (M.ORCH-T698): the login's files and the member's name in the members table.
        try { written = LoginLook.Rename(dirs, folder, email, settings.Reset ? null : settings.Name, null, Ctx.Store().Db); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Data.Common.DbException)
        {
            AnsiConsole.MarkupLine($"[red]Couldn't name {Markup.Escape(email)}: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }
        var name = LoginLook.For(folder, email).Alias;
        AnsiConsole.MarkupLine($"{Markup.Escape(email)} is now called [bold]{Markup.Escape(name)}[/] [grey]({Markup.Escape(string.Join(", ", written.Select(f => Path.Combine(f, LoginLook.FileName))))})[/].");
        return 0;
    }
}

/// <summary><c>orchestrator accounts</c> (M.ORCH-T490): every Claude account signed in to on this machine, with how much of
/// its week and 5-hour window it has used and when each resets, from the accounts table in the repo's <c>data/orchestrator.db</c>. It brings the signed-in account's
/// row up to date first, from Claude's last reading, so the roster is there without the daemon.</summary>
internal sealed class AccountsCommand : Command<AccountsCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--json")]
        [Description("Write the roster as JSON.")]
        public bool Json { get; init; }
    }

    /// <summary>The database, sign-in and usage file it reads; tests point them elsewhere (never the real ones).</summary>
    internal static Func<(OrchestratorDb Db, ClaudeAccount Account, string? UsagePath)>? Override { get; set; }

    public override int Execute(CommandContext context, Settings settings)
    {
        var (rows, current, now) = Load();
        if (settings.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        Write(rows, current, now);
        WriteOwnLogins(LoginsOverride?.Invoke() ?? new AccountDirs(), now);
        return 0;
    }

    /// <summary>Where per-CLI logins' folders are; tests point it elsewhere (never the real ~/.claude-accounts).</summary>
    internal static Func<AccountDirs>? LoginsOverride { get; set; }

    /// <summary>
    /// The per-CLI logins (M.ORCH-T559): each account a CLI can run on with <c>orchestrator start &lt;repo&gt; --account</c>, with
    /// its own meter (Claude's 5-hour and weekly percent for it) and the pace the daemon last wrote for its CLIs.
    /// </summary>
    internal static void WriteOwnLogins(AccountDirs dirs, DateTime now)
    {
        var all = dirs.All();
        if (all.Count == 0) return;
        var table = new Table().Border(TableBorder.Rounded).Title("Per-CLI logins")
            .AddColumns("Account", "Signed in as", "5h used", "5h resets", "Week used", "Pace", "Subagents", "Paced");
        foreach (var dir in all)
        {
            // The same card the dashboard's Accounts view shows (M.ORCH-T603).
            var card = LoginBoard.Own(dir, now);
            table.AddRow(
                (card.Alias is { } alias ? $"{Markup.Escape(alias)} [grey]{Markup.Escape(dir.Email)}[/]" : Markup.Escape(dir.Email)),
                card.SignedIn ? Markup.Escape(card.SignedInAs ?? "(signed in)") : "[red]not signed in[/] [grey](orchestrator login --account)[/]",
                AccountRoster.Percent(card.FiveHourPercent),
                card.FiveHourResetUtc is { } r ? Formatting.ResetTime(r, now) : "",
                AccountRoster.Percent(card.WeekPercent) + $" [grey](stops at {card.WeeklyStopPercent}%)[/]",
                Markup.Escape(card.Pace?.Pace ?? "—"),
                card.Pace?.Subagents ?? "—",
                card.Pace?.AtUtc is { } a ? Formatting.Ago(a, now) : "[grey]not yet[/]");
        }
        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("[grey]orchestrator start <repo> --account <email> runs a CLI on one of these; orchestrator login --account <email> adds one; orchestrator accounts alias <email> <name> names one.[/]");
    }

    /// <summary>The roster with the signed-in account's row brought up to date from Claude's last reading, and who's signed in.</summary>
    internal static (IReadOnlyList<AccountRow> Rows, string? Current, DateTime NowUtc) Load()
    {
        var (db, account, usagePath) = Override?.Invoke() ?? (new OrchestratorDb(OrchestratorDb.LivePath), new ClaudeAccount(), null);
        var now = DateTime.UtcNow;
        var signIn = account.Read(now);
        if (signIn is not null)
        {
            var reading = ReportedUsage.Load(now, usagePath);
            try { AccountRoster.Observe(db, signIn, account.Profile, signIn.Owns(reading) ? reading : null, now); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Data.Common.DbException) { }
        }
        var current = signIn is { IsApiKey: false } ? signIn.Account : null;
        return (AccountRoster.Load(db, current), current, now);
    }

    /// <summary>Each account's weekly stop line (M.ORCH-T609): 100 minus the weekly reserve setting; the default's when the
    /// settings can't be read.</summary>
    internal static int WeeklyStop()
    {
        try { return Ctx.Settings.WeeklyStopPercent; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return OrchestratorSettings.WeeklyStopFor(OrchestratorSettings.DefaultWeeklyReservePercent);
        }
    }

    /// <summary>The roster table <c>accounts</c> and a bare <c>login</c> print (M.ORCH-T492): one row per account, the
    /// signed-in one marked, the one with the most headroom suggested.</summary>
    internal static void Write(IReadOnlyList<AccountRow> rows, string? current, DateTime now)
    {
        if (rows.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No Claude account has been signed in to yet.[/]");
            return;
        }
        var stop = WeeklyStop();
        var suggested = AccountSwitch.MostHeadroom(rows, now, stop)?.Account;
        var table = new Table().Border(TableBorder.Rounded)
            .AddColumns("Account", "5h used", "5h resets", "Week usage", "Week resets", "Last Checked");
        foreach (var r in rows)
        {
            var c = AccountSwitch.Columns(r, now, stop);
            var left = r.WeekLeftAt(now);
            table.AddRow(
                Markup.Escape(r.Label) + (r.Account == current ? " [green](signed in)[/]" : "") + (r.Account == suggested ? " [aqua](most headroom)[/]" : ""),
                c.FiveHourUsed,
                c.FiveHourResets,
                left is null ? c.WeekUsage : $"[{(left < 10 ? "red" : left < 30 ? "yellow" : "green")}]{c.WeekUsage}[/]",
                c.WeekResets,
                r.ReadUtc is null ? "[grey]not read yet[/]" : c.LastRead);
        }
        AnsiConsole.Write(table);
        if (rows.Any(r => r.Email is null))
            AnsiConsole.MarkupLine("[grey]An account's email shows once it's signed in to again (/login).[/]");
    }
}
