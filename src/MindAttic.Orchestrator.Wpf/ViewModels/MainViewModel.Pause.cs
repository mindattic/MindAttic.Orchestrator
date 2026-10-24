using System.Windows.Input;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Scheduling;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// The global Pause/Resume toggle in the window's upper-right corner (M.ORCH-T808), beside the emergency key and the sun/moon:
/// pausing ends every Claude CLI on the machine outright, every login's, so RAM is freed and API calls stop at once, puts their
/// tickets back first in the queue, and holds every claim and dispatch org-wide until it's resumed (<see cref="GlobalPause"/>).
/// Like the API key (M.ORCH-T773), pausing asks first; resuming doesn't, it's always the safe direction.
/// </summary>
public sealed partial class MainViewModel
{
    private bool paused, switchingPause;
    private ICommand? togglePauseCommand;

    /// <summary>Whether the global pause is on, as of the last refresh or toggle (one value for every login's tab).</summary>
    public bool IsPaused => Root.paused;

    /// <summary>The corner button's glyph: Segoe's Pause (E769) while work runs, Play (E768) while paused.</summary>
    public string PauseGlyph => IsPaused ? "" : "";

    /// <summary>Its tooltip and accessible name.</summary>
    public string PauseToggleText => IsPaused
        ? "Paused: nothing claims or dispatches. Resume lets /allocate and dispatch start work again"
        : "Pause everything: end every CLI on every login now";

    /// <summary>MaText while work runs; MaWarn while paused, so it's never left on unnoticed.</summary>
    public System.Windows.Media.Brush PauseBrush => IsPaused ? Palette.Waiting : Palette.Text;

    /// <summary>Pauses (after a confirm) or resumes (at once).</summary>
    public ICommand TogglePauseCommand => togglePauseCommand ??= new RelayCommand(async () =>
    {
        if (Root.switchingPause) return;
        if (IsPaused) { await SetPausedAsync(false); return; }
        var confirmed = MindAttic.Components.Wpf.Controls.MaMessageBox.Show(
            System.Windows.Application.Current?.MainWindow,
            "This ends every Claude CLI on this machine right now, on every login, yours included, with their subagents: RAM is "
            + "freed and API calls stop at once. Each ticket they held goes back first in the queue; uncommitted work stays in its "
            + "worktree. Nothing claims or dispatches until you resume.",
            "Pause every CLI?",
            System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning, System.Windows.MessageBoxResult.No)
            == System.Windows.MessageBoxResult.Yes;
        if (!confirmed) return;
        await SetPausedAsync(true);
    }, () => !Root.switchingPause);

    private async Task SetPausedAsync(bool pause)
    {
        Root.switchingPause = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            if (pause)
            {
                var by = Root.workspace?.GlobalEmail ?? Environment.UserName;
                var result = await Task.Run(() => GlobalPause.Pause(store, by, DateTime.UtcNow));
                Root.paused = true;
                var body = $"Ended {result.Killed.Count} CLI{(result.Killed.Count == 1 ? "" : "s")}; " +
                           (result.Requeued.Count == 0 ? "no tickets were held." : $"put {string.Join(", ", result.Requeued)} back in the queue.") +
                           (result.Failed.Count > 0 ? " " + string.Join("; ", result.Failed) + "." : "");
                Toasts.Insert(0, new ToastViewModel("Paused", body, result.Failed.Count > 0 ? Palette.Over : Palette.Waiting, null));
                OrchestratorLog.For("Dashboard").Warning("global pause: {Body}", body);
            }
            else
            {
                await Task.Run(() => GlobalPause.Resume(store));
                Root.paused = false;
                OrchestratorLog.For("Dashboard").Information("global pause lifted");
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Toasts.Insert(0, new ToastViewModel(pause ? "Not paused" : "Not resumed", ex.Message, Palette.Over, null));
            OrchestratorLog.For("Dashboard").Error(ex, "global pause toggle failed");
        }
        finally
        {
            NoteLocalChange();
            Root.switchingPause = false;
            CommandManager.InvalidateRequerySuggested();
            RaisePause();
        }
        await RefreshAsync();
    }

    /// <summary>Reads the pause once a refresh (the global login's tab, applied first); every tab shows the same.</summary>
    private void ApplyPause()
    {
        if (root is null && !switchingPause) paused = GlobalPause.Active(store);
        RaisePause();
    }

    private void RaisePause()
    {
        Raise(nameof(IsPaused)); Raise(nameof(PauseGlyph)); Raise(nameof(PauseToggleText)); Raise(nameof(PauseBrush));
    }
}
