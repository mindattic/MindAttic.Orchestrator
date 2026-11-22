using System.ComponentModel;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Setup;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary><c>orchestrator quickload-all</c> (M.ORCH-T2070, the shared <c>/quickload all</c>): asks the running dashboard to run
/// Quick Load All (the newest save state or autosave restored: closed tabs reopened, every CLI typed <c>/quickload</c> on its
/// repo's handoff, /crossrepo and /allocate back where they were) down its single-instance pipe, never becoming the dashboard
/// itself, then waits for its <see cref="QuickLoadAllReport"/> and prints what it resumed. With no dashboard running there are
/// no hosted CLIs to load into, and it says so.</summary>
internal sealed class QuickLoadAllCommand : AsyncCommand<QuickLoadAllCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--wait <SECONDS>")]
        [Description("How long to wait for the dashboard's report (default 600: a reopened CLI is given a minute to come up, and every CLI in a repo is resumed in turn).")]
        public int Wait { get; init; } = 600;
    }

    private static readonly TimeSpan Poll = TimeSpan.FromSeconds(1);

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var nonce = QuickLoadAllReport.NewNonce();
        if (!SingleInstance.TryHandOff(SingleInstance.DashboardName, [DashboardRequest.QuickLoadAllArg, nonce]))
        {
            AnsiConsole.MarkupLine("[red]No MindAttic.Orchestrator dashboard is running (or it didn't answer), so there are no hosted CLIs to quick load.[/] [grey]Start the dashboard, or run /quickload in each CLI yourself.[/]");
            return 1;
        }
        AnsiConsole.MarkupLine("[grey]Asked the dashboard to run Quick Load All; waiting for it to finish...[/]");
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(Math.Max(1, settings.Wait));
        while (DateTime.UtcNow < deadline)
        {
            if (QuickLoadAllReport.Read(nonce) is { } report)
            {
                QuickLoadAllReport.Delete(nonce);
                Print(report);
                return 0;
            }
            await Task.Delay(Poll);
        }
        AnsiConsole.MarkupLine($"[yellow]The dashboard took the request but hadn't reported back after {settings.Wait}s; it keeps going there (its toast says what it did).[/]");
        return 2;
    }

    private static void Print(QuickLoadAllReport report)
    {
        AnsiConsole.MarkupLine(report.Total == 0
            ? "[yellow]Quick Load All: no CLI tabs to quick load.[/]"
            : $"[green]Quick Load All: {report.Acted} of {report.Total} CLI(s) resumed in {report.ElapsedSeconds:0.0}s.[/]");
        foreach (var line in report.Resumed) AnsiConsole.MarkupLine($"  {Markup.Escape(line)}");
        if (report.Note is { Length: > 0 }) AnsiConsole.MarkupLine($"[grey]{Markup.Escape(report.Note)}[/]");
    }
}
