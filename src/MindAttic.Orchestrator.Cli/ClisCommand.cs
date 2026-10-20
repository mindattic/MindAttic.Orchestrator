using System.ComponentModel;
using System.Text.Json;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Billing;
using MindAttic.Orchestrator.Sessions;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary><c>orchestrator clis</c> (M.ORCH-T474): every open Claude CLI and whether /allocate is on in it, from the roster the
/// daemon keeps; built here when the daemon isn't keeping it fresh.</summary>
internal sealed class ClisCommand : Command<ClisCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--json")]
        [Description("Write the roster as JSON.")]
        public bool Json { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var cycler = new ContextCycler();
        var now = DateTime.UtcNow;
        var roster = CliRoster.Read(Ctx.Store().Db) is { } r && now - r.WrittenUtc <= CliRoster.FreshFor
            ? r
            : new CliRosterFile(now, CliRoster.Build(cycler, new BillingSwitch(cycler.Directory), new HostSessionScanner(OrchestratorOptions.Load()).Scan(), now));
        if (settings.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(roster, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        if (roster.Clis.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No Claude CLI is open.[/]");
            return 0;
        }
        var table = new Table().Border(TableBorder.Rounded).AddColumns("Repo", "Pid", "Autopilot", "Bills", "Where", "State", "Last turn");
        foreach (var c in roster.Clis.OrderByDescending(c => c.Autopilot).ThenBy(c => c.Repo, StringComparer.OrdinalIgnoreCase))
            table.AddRow(
                Markup.Escape(c.Repo),
                c.Pid.ToString(),
                c.Autopilot ? $"[green]on[/] {Markup.Escape(string.Join(", ", c.AutopilotRepos))}" : "[grey]off[/]",
                c.Bills is null ? "" : Markup.Escape(BillingModes.Name(c.Bills)),
                c.InLauncher ? "Launcher tab" : "terminal",
                c.Idle ? "[yellow]waiting[/]" : "[green]working[/]",
                Format.Ago(c.LastTurnUtc, roster.WrittenUtc));
        AnsiConsole.Write(table);
        var on = roster.Clis.Count(c => c.Autopilot);
        AnsiConsole.MarkupLine($"{roster.Clis.Count} open CLI(s), /allocate on in {on}.");
        return 0;
    }
}
