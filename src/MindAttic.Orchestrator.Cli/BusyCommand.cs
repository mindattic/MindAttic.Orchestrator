using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using MindAttic.Orchestrator.Sessions;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary><c>orchestrator busy</c> (M.ORCH-T873): whether any Claude CLI on this machine, on any login, is still working: mid-turn,
/// running a subagent, holding an /allocate claim, or holding on a prompt. A CLI inside a live TermHost survives a redeploy, so it
/// is listed as hosted and never counts (M.ORCH-T928). Exit 0 when every one is idle; 1 when any is working
/// or can't be read (never taken for idle). deploy.ps1 runs it before a redeploy, which restarts the dashboard and every terminal
/// it hosts. The CLI running it is left out.</summary>
internal sealed class BusyCommand : Command<BusyCommand.Settings>
{
    /// <summary>The exit code while anyone is working or unknown.</summary>
    public const int BusyExit = 1;

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--json")]
        [Description("Write every CLI and its state as JSON.")]
        public bool Json { get; init; }

        [CommandOption("--wait")]
        [Description("Poll until every CLI is idle, then exit 0 (or exit 1 at --timeout).")]
        public bool Wait { get; init; }

        [CommandOption("--poll <SECONDS>")]
        [Description("With --wait: seconds between checks (default 30).")]
        public int Poll { get; init; } = 30;

        [CommandOption("--timeout <MINUTES>")]
        [Description("With --wait: give up after this many minutes (default: never).")]
        public int? Timeout { get; init; }

        [CommandOption("--self <PID>")]
        [Description("The CLI to leave out (default: MINDATTIC_CLI_PID, else the claude.exe this runs under).")]
        public int? Self { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        BusyReport report;
        if (settings.Wait)
        {
            using var cancel = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel(); };
            report = BusyClis.WaitForIdle(TimeSpan.FromSeconds(Math.Max(5, settings.Poll)),
                settings.Timeout is { } m ? TimeSpan.FromMinutes(Math.Max(0, m)) : null,
                r => AnsiConsole.MarkupLine($"[grey]{DateTime.Now:HH:mm:ss}[/] waiting on {r.Blocking.Count} CLI(s): " +
                    Markup.Escape(string.Join(" | ", BusyClis.Describe(r)))),
                cancel.Token, settings.Self);
        }
        else report = BusyClis.Scan(settings.Self);

        if (settings.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { report.AtUtc, report.AllIdle, report.Clis },
                new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } }));
            return report.AllIdle ? 0 : BusyExit;
        }

        if (report.Clis.Count > 0)
        {
            var table = new Table().Border(TableBorder.Rounded).AddColumns("Repo", "Login", "Pid", "State", "Doing");
            foreach (var c in report.Clis.OrderBy(c => c.Activity switch { CliActivity.Working => 0, CliActivity.Unknown => 1, CliActivity.Self => 2, CliActivity.Hosted => 3, _ => 4 })
                         .ThenBy(c => c.Repo, StringComparer.OrdinalIgnoreCase))
                table.AddRow(
                    Markup.Escape(c.Repo),
                    Markup.Escape(c.Login),
                    c.Pid?.ToString() ?? "",
                    c.Activity switch
                    {
                        CliActivity.Working => "[red]working[/]",
                        CliActivity.Unknown => "[yellow]unknown[/]",
                        CliActivity.Self => "[grey]this CLI (left out)[/]",
                        CliActivity.Hosted => "[blue]hosted, unaffected by deploy[/]",
                        _ => "[green]idle[/]",
                    },
                    Markup.Escape(c.Why.Count == 0 ? "done" : string.Join("; ", c.Why)));
            AnsiConsole.Write(table);
        }
        if (report.AllIdle)
        {
            AnsiConsole.MarkupLine("[green]No other unhosted Claude CLI is working: safe to redeploy.[/]");
            return 0;
        }
        AnsiConsole.MarkupLine($"[red]{report.Blocking.Count} CLI(s) unhosted and still working or unknown:[/]");
        foreach (var line in BusyClis.Describe(report)) AnsiConsole.WriteLine("  " + line);
        return BusyExit;
    }
}
