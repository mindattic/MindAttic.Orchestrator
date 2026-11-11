using System.Collections.ObjectModel;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Activity;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>A live CLI session on this tab's login with no ticket attributed yet (M.ORCH-T1519): <see cref="TicketAttribution"/>
/// found nothing in its prompt or worktree, but it's genuinely running. Shown in the sidebar's ticket area as a card with the
/// same look as a real ticket card, except its title is the session's current "Doing" text instead of a ticket summary and
/// it has no ticket id to open, so a busy member never reads as having "no active or pending tickets" just because nothing's
/// been attributed to a ticket yet (the companion M.ORCH-T1520 covers making that attribution a hard requirement instead).</summary>
public sealed class LiveActivityRowViewModel(AgentBoardRow row, MainViewModel? owner = null) : ObservableObject
{
    private AgentBoardRow row = row;

    /// <summary>The Task List colour (M.ORCH-T1594): its CLI's own, else its person's (<see cref="MainViewModel.TaskAccent"/>).</summary>
    public System.Windows.Media.Brush Accent => owner?.TaskAccent(null, row.SessionId) ?? Palette.Quiet;

    /// <summary>Reads <see cref="Accent"/> again (a Change Color).</summary>
    internal void RaiseAccent() => Raise(nameof(Accent));

    /// <summary>Its live session, the sync key: one card per running session, not per repo (two ticket-less sessions on the
    /// same repo each get their own).</summary>
    public string SessionId => row.SessionId;
    public string Repo => row.Repo;

    /// <summary>What it's doing right now (its latest tool call in plain words, <see cref="AgentBoardRow.Doing"/>), falling
    /// back to its state's own detail ("Working", or the pace gate's note) while no tool call has been seen yet.</summary>
    public string Summary => row.Doing is { Length: > 0 } doing ? doing : row.StateDetail;

    /// <summary>The card's accessible name: no "Open in Tickets" (there's nothing to open), unlike <see cref="UserTicketRowViewModel.OpenName"/>.</summary>
    public string OpenName => $"{Repo}: {Summary}. No ticket attributed yet";

    /// <summary>Flagged (M.ORCH-T1520): running this long with no ticket attributed, past the grace period early
    /// reading/exploring gets before the GOLDEN RULE's "every request is a ticket" is worth calling out. A UI-only nudge,
    /// not a block: the pace gate it would otherwise share already gates every tool call for every login with no staged
    /// rollout, so a bug there would stop everyone at once - too much blast radius for a first cut at enforcement.</summary>
    public bool IsOverdue => DateTime.UtcNow - row.StartedUtc >= MainViewModel.TicketlessFlagAfter;

    public string OverdueText => $"No ticket after {Formatting.Span(DateTime.UtcNow - row.StartedUtc)} - every request needs one";

    public void Update(AgentBoardRow next)
    {
        row = next;
        Raise(string.Empty);
    }
}

public sealed partial class MainViewModel
{
    /// <summary>How long a live session may run with no ticket attributed before its card flags it (M.ORCH-T1520): long
    /// enough that reading or exploring to find the right ticket isn't itself flagged (the GOLDEN RULE's own allowance for
    /// that), short enough that a session that's actually gone on to edit, commit or deploy without one doesn't sit unnoticed.</summary>
    internal static readonly TimeSpan TicketlessFlagAfter = TimeSpan.FromMinutes(15);

    /// <summary>Sessions already toasted for running past <see cref="TicketlessFlagAfter"/> with no ticket (M.ORCH-T1520):
    /// said once per session, not on every refresh tick.</summary>
    private readonly HashSet<string> knownTicketless = new(StringComparer.Ordinal);

    /// <summary>This tab's live, ticket-less activity (M.ORCH-T1519): one row per running session whose ticket attribution
    /// came back empty, shown beside the real ticket cards in the sidebar.</summary>
    public ObservableCollection<LiveActivityRowViewModel> LiveActivity { get; } = [];

    /// <summary>Something is running with no ticket attributed: the sidebar's "No active or pending tickets" empty state
    /// only shows once this is empty too (see <see cref="HasOpenTickets"/>).</summary>
    public bool HasLiveActivity => LiveActivity.Count > 0;

    /// <summary>Brings <see cref="LiveActivity"/> up to date with <paramref name="next"/>'s agent board: every session
    /// that's actually running (Working or HeldByPace - one merely Waiting for you has already stopped) and still has no
    /// <see cref="AgentBoardRow.TicketId"/>; a session still running past <see cref="TicketlessFlagAfter"/> with none gets
    /// one toast (M.ORCH-T1520), same shape as the "Looks stuck" toast (<see cref="MainViewModel.Phases"/>).</summary>
    private void SyncLiveActivity(Snapshot next)
    {
        var untracked = next.Agents.Where(a => a.TicketId is null && a.State is AgentState.Working or AgentState.HeldByPace).ToList();
        LiveActivity.SyncBy(untracked, a => a.SessionId, r => r.SessionId,
            a => new LiveActivityRowViewModel(a, this), (r, a) => r.Update(a));
        Raise(nameof(HasLiveActivity));
        Raise(nameof(HasOpenTickets));

        foreach (var row in LiveActivity.Where(r => r.IsOverdue && knownTicketless.Add(r.SessionId)))
            Toasts.Insert(0, new ToastViewModel("Working with no ticket", $"{row.Repo}'s CLI has been running {Formatting.Span(TicketlessFlagAfter)}+ with no ticket attributed.", Palette.Waiting, null));
        knownTicketless.IntersectWith(untracked.Select(a => a.SessionId));
    }
}
