using System.ComponentModel;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Term;
using Spectre.Console;
using Spectre.Console.Cli;

/// <summary>
/// Resolves a hosted TermHost session from the <c>dock</c>/<c>undock</c> commands' optional target (M.ORCH-T1448): a session
/// id (or the start of one), else the calling CLI's own session from <c>MINDATTIC_TERM_ID</c> (set by the host in its child's
/// environment, <c>TermHost/Program.cs</c>) -- what lets a CLI's own <c>/dock</c>/<c>/undock</c> skill name no target and mean
/// itself.
/// </summary>
internal static class DockTarget
{
    public static (TermSessionRecord? Session, string? Error) Resolve(string? target)
    {
        var id = string.IsNullOrWhiteSpace(target) ? Environment.GetEnvironmentVariable("MINDATTIC_TERM_ID") : target.Trim();
        if (string.IsNullOrEmpty(id))
            return (null, "No session id given, and this CLI has no MINDATTIC_TERM_ID of its own: it isn't running under a TermHost. Relaunch it through the Launcher (which starts CLIs hosted) to get a dockable session, or name the session id (orchestrator broadcast lists them).");
        var live = TermSessions.Enumerate();
        var matches = live.Where(s => s.Id.StartsWith(id, StringComparison.OrdinalIgnoreCase)).ToList();
        return matches.Count switch
        {
            0 => (null, $"No hosted session {id} is running (orchestrator broadcast lists them)."),
            1 => (matches[0], null),
            _ => (null, $"{id} matches {matches.Count} sessions: name more of it."),
        };
    }
}

/// <summary><c>orchestrator dock [SESSION]</c> (M.ORCH-T1448, the <c>/dock</c> skill): re-announces a hosted session as just
/// started, the same message its TermHost sent when it registered, so any running dashboard reattaches it right away instead
/// of waiting for its next automatic check (~45s) -- the proven path a dashboard crash or restart already reattaches
/// through, never a kill-and-relaunch of the CLI. Best effort, like every <see cref="TermBus"/> send: with no dashboard
/// running (or its daemon not reachable), nothing happens and nothing is lost -- the next real resync still finds it.</summary>
internal sealed class DockCommand : Command<DockCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[SESSION]")]
        [Description("A hosted session's id (or the start of one), from orchestrator broadcast; without it, this CLI's own session (MINDATTIC_TERM_ID).")]
        public string? Session { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var (session, error) = DockTarget.Resolve(settings.Session);
        if (session is null) { AnsiConsole.MarkupLine($"[red]{Markup.Escape(error!)}[/]"); return 1; }
        if (!TermPaneFile.SetUndocked(session, undocked: false))
        { AnsiConsole.MarkupLine($"[red]Couldn't clear {Markup.Escape(session.Id)}'s undocked mark in its tab file.[/]"); return 1; }
        TermBus.TrySend(new TermSessionEvent(session, TermSessionEvents.Started));
        AnsiConsole.MarkupLine($"[green]Asked any running dashboard to dock {Markup.Escape(session.Title ?? session.Repo ?? session.Id)}.[/] [grey]Best effort: if nothing's running, the next automatic check (or another orchestrator dock) still finds it.[/]");
        return 0;
    }
}

/// <summary><c>orchestrator undock [SESSION]</c> (M.ORCH-T1448, the <c>/undock</c> skill): asks any running dashboard to drop
/// this session's tab without touching the CLI under it (its TermHost keeps running headless). The opposite send of
/// <see cref="DockCommand"/>'s; <c>orchestrator dock</c> brings it back.</summary>
internal sealed class UndockCommand : Command<UndockCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[SESSION]")]
        [Description("A hosted session's id (or the start of one), from orchestrator broadcast; without it, this CLI's own session (MINDATTIC_TERM_ID).")]
        public string? Session { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var (session, error) = DockTarget.Resolve(settings.Session);
        if (session is null) { AnsiConsole.MarkupLine($"[red]{Markup.Escape(error!)}[/]"); return 1; }
        // The mark is what keeps a dashboard's start and resync from re-adding it; the bus send only makes it immediate.
        if (!TermPaneFile.SetUndocked(session, undocked: true))
        { AnsiConsole.MarkupLine($"[red]Couldn't mark {Markup.Escape(session.Id)} undocked in its tab file.[/]"); return 1; }
        TermBus.TrySend(new TermSessionEvent(session, TermSessionEvents.Undock));
        AnsiConsole.MarkupLine($"[green]Asked any running dashboard to undock {Markup.Escape(session.Title ?? session.Repo ?? session.Id)}.[/] [grey]Its CLI keeps running; orchestrator dock (or the dashboard's own Dock now) brings the tab back.[/]");
        return 0;
    }
}
