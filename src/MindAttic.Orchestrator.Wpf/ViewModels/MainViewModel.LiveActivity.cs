using System.Collections.ObjectModel;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Activity;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>A live CLI session on this tab's login with no ticket attributed yet (M.ORCH-T1519): <see cref="TicketAttribution"/>
/// found nothing in its prompt or worktree, but it's genuinely running. Shown in the sidebar's ticket area as a card with the
/// same look as a real ticket card, except its title is the session's current "Doing" text instead of a ticket summary and
/// it has no ticket id to open, so a busy member never reads as having "no active or pending tickets" just because nothing's
/// been attributed to a ticket yet (the companion M.ORCH-T1520 covers making that attribution a hard requirement instead).</summary>
public sealed class LiveActivityRowViewModel(AgentBoardRow row) : ObservableObject
{
    private AgentBoardRow row = row;

    /// <summary>Its live session, the sync key: one card per running session, not per repo (two ticket-less sessions on the
    /// same repo each get their own).</summary>
    public string SessionId => row.SessionId;
    public string Repo => row.Repo;

    /// <summary>What it's doing right now (its latest tool call in plain words, <see cref="AgentBoardRow.Doing"/>), falling
    /// back to its state's own detail ("Working", or the pace gate's note) while no tool call has been seen yet.</summary>
    public string Summary => row.Doing is { Length: > 0 } doing ? doing : row.StateDetail;

    /// <summary>The card's accessible name: no "Open in Tickets" (there's nothing to open), unlike <see cref="UserTicketRowViewModel.OpenName"/>.</summary>
    public string OpenName => $"{Repo}: {Summary}. No ticket attributed yet";

    public void Update(AgentBoardRow next)
    {
        row = next;
        Raise(string.Empty);
    }
}

public sealed partial class MainViewModel
{
    /// <summary>This tab's live, ticket-less activity (M.ORCH-T1519): one row per running session whose ticket attribution
    /// came back empty, shown beside the real ticket cards in the sidebar.</summary>
    public ObservableCollection<LiveActivityRowViewModel> LiveActivity { get; } = [];

    /// <summary>Something is running with no ticket attributed: the sidebar's "No active or pending tickets" empty state
    /// only shows once this is empty too (see <see cref="HasOpenTickets"/>).</summary>
    public bool HasLiveActivity => LiveActivity.Count > 0;

    /// <summary>Brings <see cref="LiveActivity"/> up to date with <paramref name="next"/>'s agent board: every session
    /// that's actually running (Working or HeldByPace - one merely Waiting for you has already stopped) and still has no
    /// <see cref="AgentBoardRow.TicketId"/>.</summary>
    private void SyncLiveActivity(Snapshot next)
    {
        var untracked = next.Agents.Where(a => a.TicketId is null && a.State is AgentState.Working or AgentState.HeldByPace);
        LiveActivity.SyncBy([.. untracked], a => a.SessionId, r => r.SessionId,
            a => new LiveActivityRowViewModel(a), (r, a) => r.Update(a));
        Raise(nameof(HasLiveActivity));
        Raise(nameof(HasOpenTickets));
    }
}
