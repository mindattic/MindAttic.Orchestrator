using System.Windows.Input;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// Save all &amp; close (M.ORCH-T1724): one bar icon that force-quicksaves every open CLI, closes every one of them (freeing
/// their memory - Wind Down's <c>/allocate off</c> never does that, and it hangs forever on an idle CLI besides, since its
/// own save only fires from a turn-boundary hook an idle CLI never hits again), and exits. The next start reopens them: a
/// tab whose session is known reopens with Claude's own <c>claude --resume</c> (already true for every hosted tab across a
/// dashboard restart, M.ORCH-T862); the rare tab with no resumable session gets its captured handoff's <c>/quickload &lt;code&gt;</c>
/// typed in for it instead (<see cref="MainWindow"/>'s own startup, after <see cref="MainViewModel.RestoreTabs"/>).
/// </summary>
public sealed partial class MainViewModel
{
    private bool savingAllClose;

    /// <summary>How long one pane's forced <c>/quicksave</c> is given to land in its repo's handoff file before it's given
    /// up on (and closed anyway - running work is never held open for this).</summary>
    private static readonly TimeSpan SaveAllCloseTimeout = TimeSpan.FromSeconds(90);

    private static readonly TimeSpan SaveAllClosePoll = TimeSpan.FromSeconds(2);

    private void InitSaveAllClose() => SaveAllCloseCommand = new RelayCommand(async () => await SaveAllCloseAsync(), () => !savingAllClose);

    public ICommand SaveAllCloseCommand { get; private set; } = null!;

    private async Task SaveAllCloseAsync()
    {
        if (savingAllClose) return;
        savingAllClose = true;
        try
        {
            var panes = AllTerminals.Panes.Where(p => !p.Ended && !p.Request.IsMenu && !p.Request.Unlisted).ToList();
            if (panes.Count == 0)
            {
                Toasts.Insert(0, new ToastViewModel("Save all & close", "No open CLI tabs to save.", Palette.Working, null));
                return;
            }
            Toasts.Insert(0, new ToastViewModel("Save all & close",
                $"Quicksaving {panes.Count} CLI{(panes.Count == 1 ? "" : "s")}, then closing every one...", Palette.Working, null));
            var tabs = panes.Select(p => new RestoreTab
            {
                Request = p.Request,
                SessionId = p.SessionId,
                Login = p.Login,
                HostId = p.IsHosted ? p.TermId : null,
            }).ToList();
            var codes = new string?[panes.Count];
            var byRepo = panes.Select((p, i) => (Pane: p, Index: i)).GroupBy(x => x.Pane.Request.Directory, StringComparer.OrdinalIgnoreCase);
            // Sequenced within a repo (its handoff is one slot shared by every CLI checked out there, M.ORCH-T1724's note on
            // this ticket), parallel across repos.
            await Task.WhenAll(byRepo.Select(async group =>
            {
                foreach (var (pane, index) in group)
                    codes[index] = await QuicksaveOneAsync(pane);
            }));
            new SaveAllCloseStore().Save(new SaveAllCloseState
            {
                State = new RestoreState { SavedUtc = DateTime.UtcNow, Clean = true, Reason = "save-all-close", Pid = Environment.ProcessId, Tabs = tabs },
                Codes = codes,
            });
            var captured = codes.Count(c => c is { Length: > 0 });
            foreach (var pane in panes) AllTerminals.Close(pane);
            Toasts.Insert(0, new ToastViewModel("Save all & close",
                $"{captured} of {panes.Count} quicksave{(panes.Count == 1 ? "" : "s")} captured. Closing now.", Palette.Working, null));
            System.Windows.Application.Current.Shutdown();
        }
        finally { savingAllClose = false; }
    }

    /// <summary>Types <c>/quicksave</c> into <paramref name="pane"/> and waits for its repo's handoff file to show a save
    /// written after this moment, up to <see cref="SaveAllCloseTimeout"/>; null (not an error - just reported) when it never
    /// typed, or the CLI never took the turn in time (busy, hung, or not a quicksave-capable provider).</summary>
    private async Task<string?> QuicksaveOneAsync(TerminalPaneViewModel pane)
    {
        var baselineUtc = DateTime.UtcNow;
        if (!pane.WriteInput(TerminalPaste.Payload("/quicksave", pane.Bracketed))) return null;
        pane.WriteInput("\r");
        var path = QuicksaveCapture.SessionPath(pane.Request.Directory);
        var deadlineUtc = baselineUtc + SaveAllCloseTimeout;
        while (DateTime.UtcNow < deadlineUtc)
        {
            await Task.Delay(SaveAllClosePoll);
            if (QuicksaveCapture.TryReadCode(path, baselineUtc) is { Length: > 0 } code) return code;
        }
        return null;
    }
}
