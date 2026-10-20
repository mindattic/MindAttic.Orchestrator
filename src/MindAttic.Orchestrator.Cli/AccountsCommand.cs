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
        var (db, account, usagePath) = Override?.Invoke() ?? (new OrchestratorDb(OrchestratorDb.DefaultPath(OrchestratorOptions.Load().WorkspaceRoot)), new ClaudeAccount(), null);
        var now = DateTime.UtcNow;
        var signIn = account.Read(now);
        if (signIn is not null)
        {
            var reading = ReportedUsage.Load(now, usagePath);
            try { AccountRoster.Observe(db, signIn, account.Profile, signIn.Owns(reading) ? reading : null, now); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException) { }
        }
        var rows = AccountRoster.Load(db, signIn?.Account);
        if (settings.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        if (rows.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No Claude account has been signed in to yet.[/]");
            return 0;
        }
        var table = new Table().Border(TableBorder.Rounded)
            .AddColumns("Account", "Plan", "Week used", "Week left", "Week resets", "5h used", "5h resets", "Last read");
        foreach (var r in rows)
        {
            var left = r.WeekLeftAt(now);
            table.AddRow(
                Markup.Escape(r.Label) + (r.Account == signIn?.Account ? " [green](signed in)[/]" : ""),
                Markup.Escape(r.Plan ?? ""),
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
        return 0;
    }
}
