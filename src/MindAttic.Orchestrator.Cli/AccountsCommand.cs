using System.ComponentModel;
using System.Text.Json;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Budget;
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
        return 0;
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
            .AddColumns("Account", "Seat", "Week used", "Week left", "Week resets", "5h used", "5h resets", "Last read");
        foreach (var r in rows)
        {
            var left = r.WeekLeftAt(now);
            table.AddRow(
                Markup.Escape(r.Label) + (r.Account == current ? " [green](signed in)[/]" : "") + (r.Account == suggested ? " [aqua](most headroom)[/]" : ""),
                Markup.Escape(AccountSwitch.SeatOrPlan(r)),
                AccountRoster.Percent(r.WeekUsedAt(now)),
                left is null ? "—" : $"[{(left < 10 ? "red" : left < 30 ? "yellow" : "green")}]{AccountRoster.Percent(left)}[/]",
                r.WeekResetAt(now) is { } wr ? Formatting.ResetTime(wr, now) : "",
                AccountRoster.Percent(r.FiveHourUsedAt(now)),
                r.FiveHourResetAt(now) is { } fr ? Formatting.ResetTime(fr, now) : "",
                r.ReadUtc is { } read ? Formatting.Ago(read, now) : "[grey]not yet[/]");
        }
        AnsiConsole.Write(table);
        if (rows.Any(r => r.Email is null))
            AnsiConsole.MarkupLine("[grey]An account's email shows once it's signed in to again (/login).[/]");
    }
}
