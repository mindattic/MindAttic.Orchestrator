using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// Move to account (M.ORCH-T607): each CLI on the Accounts view and the agent board has a "Move to" menu of the other signed-in
/// logins. Choosing one sends its Launcher tab the restart-resume request with that login's folder (<see cref="AccountMove"/>),
/// so the CLI moves to that account at its next idle point, keeping its session; <c>orchestrator move</c> does the same.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>The move (seam: tests replace it, never sending to a real tab).</summary>
    internal Func<AccountMove> AccountMover { get; set; } = () => AccountMove.Default();

    private bool moving;

    /// <summary>The logins a CLI on <paramref name="ownLogin"/> (null: the global login) can move to, read off the UI thread
    /// (a login's <c>.claude.json</c> can run to megabytes).</summary>
    public async Task<IReadOnlyList<MoveTarget>> MoveTargetsAsync(string? ownLogin)
    {
        var mover = AccountMover;
        try { return await Task.Run(() => mover().TargetsFor(ownLogin)); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            StatusMessage = $"Couldn't read the logins: {ex.Message}";
            return [];
        }
    }

    /// <summary>Moves the CLI of <paramref name="sessionId"/> (or, failing that, claude.exe <paramref name="cliPid"/>) to
    /// <paramref name="target"/>, saying what it did in the status bar and the Accounts view, and a toast when it couldn't.</summary>
    public async Task MoveToAccountAsync(string repo, string sessionId, int? cliPid, MoveTarget target)
    {
        if (moving || PendingMoves.For(sessionId) is not null) return;
        moving = true;
        // The tickets it and its subagents hold (M.ORCH-T637): its claims, and the one it was started on.
        var row = full?.Agents.FirstOrDefault(a => a.SessionId == sessionId);
        List<string> holding = [.. (row?.Claims ?? []).Concat(row?.TicketId is { Length: > 0 } own ? [own] : []).Distinct(StringComparer.OrdinalIgnoreCase)];
        // Locked at once (M.ORCH-T638): the CLI only moves at its next idle point, so without this it looks like nothing happened.
        PendingMoves.Begin(new MoveLock
        {
            SessionId = sessionId, Pid = cliPid ?? 0, HostPid = row?.Host?.ProcessId, HostPipe = row?.Host?.PipeName, Repo = repo,
            ToEmail = target.Email, ToName = NameOf(target), StartedUtc = DateTime.UtcNow, Tickets = holding,
        });
        var hosts = full?.Repos.SelectMany(r => r.Hosts).ToList() ?? [];
        var mover = AccountMover;
        AccountMoveResult result;
        try
        {
            result = await Task.Run(async () =>
            {
                var move = mover();
                var now = DateTime.UtcNow;
                var live = move.Live(hosts, now);
                var cli = live.FirstOrDefault(c => c.SessionId == sessionId) ?? (cliPid is { } pid ? live.FirstOrDefault(c => c.Pid == pid) : null);
                return cli is null
                    ? new AccountMoveResult(false, $"The CLI in {repo} isn't running any more, so it wasn't moved.")
                    : await move.MoveAsync(cli, target.Email, now, holding, repo);
            });
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            result = new AccountMoveResult(false, $"Couldn't move the CLI in {repo}: {ex.Message}");
            OrchestratorLog.For("Dashboard").Error(ex, "move to account failed");
        }
        finally { moving = false; }
        StatusMessage = result.Message;
        AccountsMessage = result.Message;
        if (!result.Ok)
        {
            PendingMoves.End(sessionId);
            Toasts.Insert(0, new ToastViewModel($"{repo} not moved to {target.Label}", result.Message, Palette.Over, null));
        }
    }

    /// <summary>The target's name (M.ORCH-T614), else its email or "Global login".</summary>
    private static string NameOf(MoveTarget target)
    {
        try { return LoginLook.ForConfigDir(target.IsGlobal ? null : target.ConfigDir).Alias; }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or ArgumentException) { return target.Label; }
    }

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
        foreach (var a in Agents) a.RaiseMoving();
        foreach (var card in LoginCards) foreach (var c in card.Clis) c.RaiseMoving();
        foreach (var p in Terminals.Panes) p.RaiseMoving();
        // Every ticket a move holds, wherever it shows (M.ORCH-T637): the repo's open tickets, the queue and finished work.
        foreach (var r in tiles.Values)
        {
            foreach (var t in r.Tickets) t.RaiseMoving();
            foreach (var h in r.Hosts) h.RaiseMoving();
        }
        foreach (var q in Queue) q.RaiseMoving();
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }
}
