using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// Dock and Undock (M.ORCH-Tnnn): relocating a Claude CLI's tab between this dashboard's embedded Terminals panel and its own
/// Windows Terminal window, keeping its conversation (<c>claude --resume</c>) rather than starting over. Mirrors the shape of
/// "Move to account" (<c>MainViewModel.Move.cs</c>), but a relocation never changes who a CLI runs as (only where its tab
/// lives), so it needs no ticket-claim handback and ends purely by watching for its old host pid to leave the live snapshot
/// (<see cref="ReconcileRelocates"/>), reused for both directions since only the dashboard can finish a Dock (embedding is
/// WPF-only) and keeping Undock on the same single path is simpler than splitting it into the daemon the way Move is.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>Undocks <paramref name="pane"/>: ends its CLI at its next idle point and reopens it, resumed, in its own
    /// Windows Terminal window once that's confirmed. Claude only; refused for the Launcher's own menu pane.</summary>
    public async Task UndockAsync(TerminalPaneViewModel pane)
    {
        if (pane.Ended || pane.Relocating) return;
        if (pane.Request.IsMenu || !string.Equals(pane.Request.Provider, "Claude", StringComparison.OrdinalIgnoreCase)) return;
        if (pane.SessionId is not { } session || pane.CliPid is not { } cliPid || pane.HostPid is not { } hostPid)
        {
            StatusMessage = "This tab's session isn't known yet; try again once it's finished starting.";
            return;
        }
        var move = new RelocateLock
        {
            SessionId = session, Pid = cliPid, HostPid = hostPid, Repo = pane.Request.Repo ?? pane.Name, Cwd = pane.Request.Directory,
            ConfigDir = pane.Request.ConfigDir, Email = pane.Request.Email, Initials = pane.Request.Initials, TabColor = pane.Request.TabColor,
            Direction = RelocateDirection.Undock, StartedUtc = DateTime.UtcNow,
        };
        if (!Root.relocateLocks.TryBegin(move, out _))
        {
            StatusMessage = $"{move.Repo} is already being relocated.";
            return;
        }
        pane.Relocating = true;
        var host = new HostSession("Claude", hostPid, "", null);
        var sent = await Task.Run(() => SessionControl.SendAsync(SessionControl.ControlPipeName(host), Relocate.Message(session, cliPid, move.Cwd, move.StartedUtc), submit: true));
        if (!sent)
        {
            Root.relocateLocks.End(session);
            pane.Relocating = false;
            StatusMessage = $"Couldn't reach {move.Repo}'s control pipe, so it wasn't undocked.";
            Toasts.Insert(0, new ToastViewModel("Not undocked", StatusMessage, Palette.Over, null));
            return;
        }
        StatusMessage = $"Undock requested: {move.Repo} reopens in its own window once it's idle.";
    }

    /// <summary>Every relocation whose host has disappeared from <paramref name="all"/> (confirmed gone: reopen it on the
    /// other surface) or has simply been stuck too long (give up, say so) — called right after <see cref="TerminalsViewModel.Update"/>
    /// so it sees the same fresh scan.</summary>
    private void ReconcileRelocates(Snapshot all)
    {
        var liveHostIds = all.Repos.SelectMany(r => r.Hosts).Select(h => h.ProcessId).ToHashSet();
        foreach (var end in Root.relocateLocks.Reconcile(liveHostIds, all.TakenUtc))
        {
            if (!end.Gone)
            {
                Toasts.Insert(0, new ToastViewModel($"{end.Lock.Repo} wasn't relocated",
                    "It didn't end within 30 minutes, so nothing was reopened; try again once it's idle.", Palette.Over, null));
                continue;
            }
            _ = CompleteRelocateAsync(end.Lock);
        }
    }

    private Task CompleteRelocateAsync(RelocateLock move)
    {
        if (move.Direction == RelocateDirection.Dock)
        {
            // Another reconcile tick may have already embedded this session (e.g. a second dashboard instance also polling).
            if (Root.AllTerminals.Panes.Any(p => p.SessionId == move.SessionId && !p.Ended)) return Task.CompletedTask;
            var error = Root.OpenTerminal(Relocate.ResumedPaneRequest(move, aggregator.Options, control));
            StatusMessage = error ?? $"{move.Repo} docked: reopened here, resumed.";
            if (error is not null) Toasts.Insert(0, new ToastViewModel($"{move.Repo} not docked", error, Palette.Over, null));
            return Task.CompletedTask;
        }
        var psi = control.Start(move.Repo, move.Cwd ?? aggregator.Options.WorkspaceRoot, "Claude", prompt: null, move.ConfigDir, move.SessionId);
        StatusMessage = psi ?? $"{move.Repo} undocked: reopened in its own window, resumed.";
        if (psi is not null) Toasts.Insert(0, new ToastViewModel($"{move.Repo} not undocked", psi, Palette.Over, null));
        return Task.CompletedTask;
    }
}
