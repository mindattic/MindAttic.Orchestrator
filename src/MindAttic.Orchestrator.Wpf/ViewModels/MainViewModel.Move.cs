using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// Move to account (M.ORCH-T607): <c>orchestrator move</c> sends a CLI's Launcher tab the restart-resume request with another
/// login's folder (<see cref="AccountMove"/>), so the CLI moves to that account at its next idle point, keeping its session. Each CLI tab's right-click menu
/// has "Move to" (M.ORCH-T883, which the agent board's button became when the board went, M.ORCH-T837).
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>The move (seam: tests replace it, never sending to a real tab).</summary>
    internal Func<AccountMove> AccountMover { get; set; } = () => AccountMove.Default();

    /// <summary>Session ids with a move in flight from this tab right now (between the guard check below and its own
    /// `finally`), so a second "Move to" on an unrelated CLI pane while one move is in flight is never blocked by it: the
    /// old single `moving` bool here guarded every session on the tab, not just the one actually moving.</summary>
    private readonly HashSet<string> movingSessions = [];

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
        if (PendingMoves.For(sessionId) is not null || !movingSessions.Add(sessionId))
        {
            StatusMessage = $"The CLI in {repo} is already moving to another account; try again once it lands.";
            return;
        }
        // The tickets it and its subagents hold (M.ORCH-T637): its claims, and the one it was started on.
        var row = full?.Agents.FirstOrDefault(a => a.SessionId == sessionId);
        List<string> holding = [.. (row?.Claims ?? []).Concat(row?.TicketId is { Length: > 0 } own ? [own] : []).Distinct(StringComparer.OrdinalIgnoreCase)];
        // This tab's own pane for the CLI, if one's open (M.ORCH-T1883): by its SessionId, else (just started, not learned
        // yet) its Launcher tab's host pid, the same two keys TerminalPaneViewModel.IsMoving itself watches below.
        var pane = AllTerminals.Panes.FirstOrDefault(p => string.Equals(p.SessionId, sessionId, StringComparison.Ordinal))
            ?? (row?.Host?.ProcessId is { } byHost ? AllTerminals.Panes.FirstOrDefault(p => p.HostPid == byHost) : null);
        var fromLogin = pane?.Login;
        // Locked at once (M.ORCH-T638): the CLI only moves at its next idle point, so without this it looks like nothing happened.
        PendingMoves.Begin(new MoveLock
        {
            SessionId = sessionId, Pid = cliPid ?? 0, HostPid = row?.Host?.ProcessId, HostPipe = row?.Host?.PipeName, Repo = repo,
            FromEmail = fromLogin, ToEmail = target.Email, ToName = NameOf(target), StartedUtc = DateTime.UtcNow, Tickets = holding,
        });
        // The pane disappears from its own login's tree/tab strip and shows at the target's at once (M.ORCH-T1883), rather
        // than waiting for the real transfer (about a minute) to land: moved for real into SyncTerminals' own reckoning of
        // whose page it's on (OwnerOf, keyed by Login, the same field a landed move changes for real once the next refresh's
        // Update sees its CLI there), while it renders as a faded/dashed placeholder with a spinner the whole time
        // (TerminalPaneViewModel.IsMoving, driven by the PendingMoves lock just begun above) - put back below if it doesn't land.
        if (pane is not null && !string.Equals(fromLogin, target.Email, StringComparison.OrdinalIgnoreCase))
        {
            pane.Login = target.Email;
            SyncTerminals();
        }
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
        finally { movingSessions.Remove(sessionId); }
        StatusMessage = result.Message;
        AccountsMessage = result.Message;
        if (!result.Ok)
        {
            PendingMoves.End(sessionId);
            // Didn't land: the placeholder goes back to a real row under its old login (M.ORCH-T1883), not left stranded
            // under the target's; a no-op when there was no pane to move in the first place, or it already moved on its own.
            if (pane is not null && string.Equals(pane.Login, target.Email, StringComparison.OrdinalIgnoreCase))
            {
                pane.Login = fromLogin;
                SyncTerminals();
            }
            Toasts.Insert(0, new ToastViewModel($"{repo} not moved to {target.Label}", result.Message, Palette.Over, null));
        }
    }

    /// <summary>Says why a dragged CLI wasn't moved here (M.ORCH-T1849), in the status bar and the Accounts view.</summary>
    internal void SayMove(string message) => StatusMessage = AccountsMessage = message;

    /// <summary>The target's name (M.ORCH-T614), else its email or "Global login".</summary>
    private static string NameOf(MoveTarget target)
    {
        try { return LoginLook.ForConfigDir(target.IsGlobal ? null : target.ConfigDir).Alias; }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or ArgumentException) { return target.Label; }
    }

    /// <summary>The agent whose Launcher tab has host <paramref name="hostPid"/> (M.ORCH-T883), null while it isn't known.</summary>
    internal MindAttic.Orchestrator.Activity.AgentBoardRow? AgentOfHost(int hostPid) => full?.Agents.FirstOrDefault(a => a.Host?.ProcessId == hostPid);

    /// <summary>Takes the moves in flight as the refresh read them (the daemon ends each one as it lands, fails or times out, and
    /// hands its tickets back, M.ORCH-T637), and says which ended without landing.</summary>
    private void ReconcileMoves(Snapshot next)
    {
        foreach (var m in PendingMoves.Sync(next.Moves, next.TakenUtc))
        {
            if ((full ?? next).Agents.Any(a => MoveLocks.Landed(m, a))) continue;
            // A backstop for M.ORCH-T1883's own revert in MoveToAccountAsync, above, for a move whose placeholder this
            // dashboard shows but whose own await never got the chance to put it back (a restart mid-move, or a lock this
            // dashboard didn't begin itself, e.g. `orchestrator move`): still shown under the target it never reached, so
            // it goes back to where it actually came from, by SessionId alone (the one key a pane's own identity never loses).
            if (AllTerminals.Panes.FirstOrDefault(p => string.Equals(p.SessionId, m.SessionId, StringComparison.Ordinal)) is { } pane
                && string.Equals(pane.Login, m.ToEmail, StringComparison.OrdinalIgnoreCase))
            {
                pane.Login = m.FromEmail;
                SyncTerminals();
            }
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
        // Every open CLI's own placeholder (M.ORCH-T1883): AllTerminals.Panes is the one shared list every tab's own
        // Terminals.Panes is drawn from (SyncTerminals), so raising it from the global tab alone is enough.
        if (IsGlobalTab) foreach (var p in AllTerminals.Panes) p.RaiseMoving();
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }
}
