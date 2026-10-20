using System.ComponentModel;
using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Billing;
using MindAttic.Orchestrator.Sessions;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary><c>orchestrator handoff [on|off|status]</c> (M.ORCH-T481): /handoff from outside a CLI. status (the default) lists
/// what's still working; on starts winding every CLI down for an account switch; off cancels it.</summary>
internal sealed class HandoffCommand : Command<HandoffCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[ACTION]")]
        [Description("status (default): what's still working; on: wind every CLI down for an account switch; off: cancel it.")]
        public string Action { get; init; } = "status";
    }

    /// <summary>The session-context folder; null is the user's own. Tests point it at a temp folder.</summary>
    internal static string? ContextDirectory { get; set; }

    public override int Execute(CommandContext context, Settings settings)
    {
        var cycler = new ContextCycler(ContextDirectory);
        var now = DateTime.UtcNow;
        switch (settings.Action.Trim().ToLowerInvariant())
        {
            case "on" or "start":
                var was = Handoff.Read(cycler.Directory);
                var mark = Handoff.Start(cycler, null, ContextDirectory is null ? new MindAttic.Orchestrator.Budget.ClaudeAccount().Read(now)?.Account : null, now);
                AnsiConsole.MarkupLine(Markup.Escape(was?.Holds == true
                    ? "A handoff is already under way."
                    : $"Handing off: nothing new starts; what's working finishes, lands and closes, then each CLI quicksaves. {mark.Autopilot?.Count ?? 0} CLI(s)' /allocate recorded for after the switch."));
                break;
            case "off" or "cancel":
                var had = Handoff.Read(cycler.Directory) is not null;
                Handoff.Clear(cycler.Directory);
                AnsiConsole.MarkupLine(had ? "Handoff cancelled: new work starts again." : "[grey]No handoff to cancel.[/]");
                return 0;
            case "status" or "":
                break;
            default:
                AnsiConsole.MarkupLine($"[red]No handoff action \"{Markup.Escape(settings.Action)}\"; use on, off or status.[/]");
                return 1;
        }
        var current = Handoff.Read(cycler.Directory);
        var roster = Roster(cycler, now);
        var store = Ctx.Store();
        // What a live session or subagent is working: a claim it's on isn't waiting for one.
        var worked = current?.State == Handoff.Winding ? Worked(store) : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var work = Handoff.Working(roster, cycler.AliveSubagents(now), store.LoadAssignments(), worked);
        foreach (var line in Handoff.Describe(current, work, roster, Handoff.Saved(cycler.Directory), now))
            AnsiConsole.MarkupLine(Markup.Escape(line));
        return 0;
    }

    private static IReadOnlyList<CliRow> Roster(ContextCycler cycler, DateTime now) =>
        CliRoster.Read(Ctx.Store().Db) is { } r && now - r.WrittenUtc <= CliRoster.FreshFor ? r.Clis
            : ContextDirectory is not null ? []
            : CliRoster.Build(cycler, new BillingSwitch(cycler.Directory), new HostSessionScanner(OrchestratorOptions.Load()).Scan(), now);

    private static HashSet<string> Worked(MindAttic.Orchestrator.Settings.JsonStore store)
    {
        if (ContextDirectory is not null) return new(StringComparer.OrdinalIgnoreCase);
        var (_, _, _, plan) = Ctx.Load();
        return plan.Worked.Select(w => w.TicketId).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
