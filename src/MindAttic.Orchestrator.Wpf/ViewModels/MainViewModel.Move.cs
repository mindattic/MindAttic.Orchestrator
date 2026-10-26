using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// Move to account (M.ORCH-T607): <c>orchestrator move</c> sends a CLI's Launcher tab the restart-resume request with another
/// login's folder (<see cref="AccountMove"/>), so the CLI moves to that account at its next idle point, keeping its session. The
/// agent board's "Move to" menu is gone with the board (M.ORCH-T837); the dashboard still locks what a move in flight holds.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>Takes the moves in flight as the refresh read them (the daemon ends each one as it lands, fails or times out, and
    /// hands its tickets back, M.ORCH-T637), and says which ended without landing.</summary>
    private void ReconcileMoves(Snapshot next)
    {
        foreach (var m in PendingMoves.Sync(next.Moves, next.TakenUtc))
        {
            if ((full ?? next).Agents.Any(a => MoveLocks.Landed(m, a))) continue;
            Toasts.Insert(0, new ToastViewModel($"{m.Repo} hasn't moved to {m.ToName}",
                $"Its CLI didn't show up on {m.ToName}, so it and its tickets are unlocked again and stay with its old login" +
                (m.Tickets.Count > 0 ? $" ({string.Join(", ", m.Tickets)} are handed back to it)." : "."),
                Palette.Over, null));
        }
    }

    /// <summary>Whether a CLI moving to another login holds <paramref name="ticketId"/> (M.ORCH-T637): its dashboard actions
    /// (Start, Release, Use as prompt, Remove worktree) are refused until the move lands, saying so.</summary>
    internal bool Locked(string ticketId)
    {
        if (PendingMoves.ForTicket(ticketId) is not { } m) return false;
        QueueMessage = StatusMessage = $"{ticketId} is held by the CLI in {m.Repo}, which is moving to {m.ToName}: try again once it lands.";
        return true;
    }

    /// <summary>Re-reads every moving flag this tab shows, when any move begins, lands or times out.</summary>
    private void OnMovesChanged()
    {
        // Every ticket a move holds, wherever it shows (M.ORCH-T637): the repo's open tickets, the queue and finished work.
        foreach (var r in tiles.Values)
        {
            foreach (var t in r.Tickets) t.RaiseMoving();
            foreach (var h in r.Hosts) h.RaiseMoving();
        }
        foreach (var q in AllQueueRows) q.RaiseMoving();
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }
}
