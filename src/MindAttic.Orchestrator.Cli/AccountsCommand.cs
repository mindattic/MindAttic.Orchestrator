using System.ComponentModel;
using System.Text.Json;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using Spectre.Console;
using Spectre.Console.Cli;

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
            var usage = ReportedUsage.Load(now, dir.UsagePath);
            var status = Status(dir.StatusPath);
            string? Text(string name) => status is { } s && s.TryGetProperty(name, out var v) ? v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.GetDouble().ToString("0.#", System.Globalization.CultureInfo.InvariantCulture),
                _ => null,
            } : null;
            var at = Text("timestamp") is { } t && DateTime.TryParse(t, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var stamp) ? stamp : (DateTime?)null;
            table.AddRow(
                Markup.Escape(dir.Email),
                dir.SignedIn ? Markup.Escape(dir.SignedInEmail ?? "(signed in)") : "[red]not signed in[/] [grey](orchestrator login --account)[/]",
                AccountRoster.Percent(ReportedUsage.Usable(usage?.FiveHour, now)?.Percent),
                ReportedUsage.Usable(usage?.FiveHour, now) is { } w ? Formatting.ResetTime(w.ResetUtc, now) : "",
                AccountRoster.Percent(usage?.Week is { } week && week.ResetUtc > now ? week.Percent : null),
                Markup.Escape(Text("pace") ?? (Text("stop") is not null ? "stop" : "—")),
                Text("subagent_allowance") is { } allowed ? $"{Text("subagents_running") ?? "0"} of {allowed}" : "—",
                at is { } a ? Formatting.Ago(a, now) : "[grey]not yet[/]");
        }
        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("[grey]orchestrator start <repo> --account <email> runs a CLI on one of these; orchestrator login --account <email> adds one.[/]");

        static JsonElement? Status(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                return doc.RootElement.Clone();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
        }
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
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException) { }
        }
        var current = signIn is { IsApiKey: false } ? signIn.Account : null;
        return (AccountRoster.Load(db, current), current, now);
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
        var suggested = AccountSwitch.MostHeadroom(rows, now)?.Account;
        var table = new Table().Border(TableBorder.Rounded)
            .AddColumns("Account", "Week usage", "Week resets", "5h used", "5h resets", "Last Checked");
        foreach (var r in rows)
        {
            var c = AccountSwitch.Columns(r, now);
            var left = r.WeekLeftAt(now);
            table.AddRow(
                Markup.Escape(r.Label) + (r.Account == current ? " [green](signed in)[/]" : "") + (r.Account == suggested ? " [aqua](most headroom)[/]" : ""),
                left is null ? c.WeekUsage : $"[{(left < 10 ? "red" : left < 30 ? "yellow" : "green")}]{c.WeekUsage}[/]",
                c.WeekResets,
                c.FiveHourUsed,
                c.FiveHourResets,
                r.ReadUtc is null ? "[grey]not read yet[/]" : c.LastRead);
        }
        AnsiConsole.Write(table);
        if (rows.Any(r => r.Email is null))
            AnsiConsole.MarkupLine("[grey]An account's email shows once it's signed in to again (/login).[/]");
    }
}
