using System.Diagnostics;
using System.Windows.Input;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Activity;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// The dashboard's Save button (M.ORCH-T1769): a left click force-quicksaves the shown CLI alone, through the same
/// input-injection path the "Say" button / on-screen keyboard already use (<see cref="TerminalPaneViewModel.WriteInput"/>),
/// with no closing and no confirm - it's a single, easily-undone action like any other keystroke sent to the CLI. A right
/// click opens a menu (<see cref="MainWindow"/>'s own <c>OnSaveButtonContextMenu</c>) for Quick Save All and Quick Load
/// All, each confirmed first and reporting how long the confirmed run actually took once it's done.
/// <para>
/// This replaces Save all &amp; close (M.ORCH-T1724), whose one bar icon force-quicksaved every open CLI and then closed
/// and exited the whole app (a "pause everything, resume at the next start" action the dashboard no longer offers): Quick
/// Save All keeps that same force-quicksave-every-open-CLI mechanic (<see cref="QuicksaveOneAsync"/>, sequenced within a
/// repo since its handoff is one slot shared by every CLI checked out there, parallel across repos) but never closes a
/// tab or exits, and Quick Load All is its new counterpart - typing bare <c>/quickload</c> (this repo's latest handoff)
/// into every open CLI the same way.
/// </para>
/// </summary>
public sealed partial class MainViewModel
{
    private bool quicksavingAll, quickloadingAll;

    /// <summary>How long one pane's forced <c>/quicksave</c> is given to land in its repo's handoff file before it's given
    /// up on (not an error - just reported, and the rest of a Quick Save All run isn't held up by one stuck CLI).</summary>
    private static readonly TimeSpan QuickAllTimeout = TimeSpan.FromSeconds(90);

    private static readonly TimeSpan QuickAllPoll = TimeSpan.FromSeconds(2);

    private void InitSave()
    {
        QuicksaveShownCommand = new RelayCommand(async () => await QuicksaveShownAsync(),
            () => AllTerminals.SelectedPane is { Ended: false, Request.IsMenu: false, Request.Unlisted: false });
        QuickSaveAllCommand = new RelayCommand(async () => await QuickSaveAllAsync(), () => !quicksavingAll);
        QuickLoadAllCommand = new RelayCommand(async () => await QuickLoadAllAsync(), () => !quickloadingAll);
    }

    /// <summary>Left-click: quicksaves the shown CLI alone.</summary>
    public ICommand QuicksaveShownCommand { get; private set; } = null!;

    /// <summary>Right-click menu, "Quick Save All" (<see cref="MainWindow"/> confirms first and reports how long it took).</summary>
    public ICommand QuickSaveAllCommand { get; private set; } = null!;

    /// <summary>Right-click menu, "Quick Load All" (<see cref="MainWindow"/> confirms first and reports how long it took).</summary>
    public ICommand QuickLoadAllCommand { get; private set; } = null!;

    /// <summary>Open, real CLI panes - the same set Save all &amp; close worked (M.ORCH-T1724): not the Launcher's own
    /// menu tab, not an ended one, not an unlisted one.</summary>
    private List<TerminalPaneViewModel> OpenPanes() => AllTerminals.Panes.Where(p => !p.Ended && !p.Request.IsMenu && !p.Request.Unlisted).ToList();

    private async Task QuicksaveShownAsync()
    {
        if (AllTerminals.SelectedPane is not { Ended: false } pane) return;
        var code = await QuicksaveOneAsync(pane);
        Toasts.Insert(0, code is { Length: > 0 }
            ? new ToastViewModel("Quicksave", $"{pane.Name} saved ({code}).", Palette.Working, null)
            : new ToastViewModel("Quicksave", $"{pane.Name} didn't quicksave in time (busy, hung, or not a quicksave-capable provider).", Palette.Working, null));
    }

    /// <summary>"Quick Save All" (right-click menu, confirmed first by <see cref="MainWindow"/>): force-quicksaves every
    /// open CLI, same grouping Save all &amp; close used, but closes nothing and never exits. Returns how long it took and
    /// how many of how many landed, for the completion modal.</summary>
    internal async Task<(TimeSpan Elapsed, int Acted, int Total)> QuickSaveAllAsync()
    {
        if (quicksavingAll) return (TimeSpan.Zero, 0, 0);
        quicksavingAll = true;
        var clock = Stopwatch.StartNew();
        try
        {
            var panes = OpenPanes();
            if (panes.Count == 0) return (clock.Elapsed, 0, 0);
            var codes = new string?[panes.Count];
            var byRepo = panes.Select((p, i) => (Pane: p, Index: i)).GroupBy(x => x.Pane.Request.Directory, StringComparer.OrdinalIgnoreCase);
            await Task.WhenAll(byRepo.Select(async group =>
            {
                foreach (var (pane, index) in group) codes[index] = await QuicksaveOneAsync(pane);
            }));
            return (clock.Elapsed, codes.Count(c => c is { Length: > 0 }), panes.Count);
        }
        finally { quicksavingAll = false; }
    }

    /// <summary>"Quick Load All" (right-click menu, confirmed first by <see cref="MainWindow"/>): types bare
    /// <c>/quickload</c> (this repo's latest handoff) into every open CLI, same grouping as <see cref="QuickSaveAllAsync"/>.
    /// Returns how long it took and how many of how many were sent, for the completion modal.</summary>
    internal async Task<(TimeSpan Elapsed, int Acted, int Total)> QuickLoadAllAsync()
    {
        if (quickloadingAll) return (TimeSpan.Zero, 0, 0);
        quickloadingAll = true;
        var clock = Stopwatch.StartNew();
        try
        {
            var panes = OpenPanes();
            if (panes.Count == 0) return (clock.Elapsed, 0, 0);
            var sent = new bool[panes.Count];
            var byRepo = panes.Select((p, i) => (Pane: p, Index: i)).GroupBy(x => x.Pane.Request.Directory, StringComparer.OrdinalIgnoreCase);
            await Task.WhenAll(byRepo.Select(async group =>
            {
                foreach (var (pane, index) in group) sent[index] = await QuickloadOneAsync(pane);
            }));
            return (clock.Elapsed, sent.Count(s => s), panes.Count);
        }
        finally { quickloadingAll = false; }
    }

    /// <summary>Types <c>/quicksave</c> into <paramref name="pane"/> and waits for its repo's handoff file to show a save
    /// written after this moment, up to <see cref="QuickAllTimeout"/>; null (not an error - just reported) when it never
    /// typed, or the CLI never took the turn in time (busy, hung, or not a quicksave-capable provider).</summary>
    private async Task<string?> QuicksaveOneAsync(TerminalPaneViewModel pane)
    {
        var baselineUtc = DateTime.UtcNow;
        if (!pane.WriteInput(TerminalPaste.Payload("/quicksave", pane.Bracketed))) return null;
        pane.WriteInput("\r");
        var path = QuicksaveCapture.SessionPath(pane.Request.Directory);
        var deadlineUtc = baselineUtc + QuickAllTimeout;
        while (DateTime.UtcNow < deadlineUtc)
        {
            await Task.Delay(QuickAllPoll);
            if (QuicksaveCapture.TryReadCode(path, baselineUtc) is { Length: > 0 } code) return code;
        }
        return null;
    }

    /// <summary>Types bare <c>/quickload</c> into <paramref name="pane"/> (restores this repo's latest handoff); true once
    /// typed. There's no file-based landing signal for a load the way <see cref="QuicksaveOneAsync"/> has for a save
    /// (the handoff file a repo's own <c>/quicksave</c> writes says nothing about a later <c>/quickload</c> reading it), so
    /// this only reports that it was sent, after a short settle so every pane isn't typed into in the very same instant.</summary>
    private async Task<bool> QuickloadOneAsync(TerminalPaneViewModel pane)
    {
        if (!pane.WriteInput(TerminalPaste.Payload("/quickload", pane.Bracketed))) return false;
        pane.WriteInput("\r");
        await Task.Delay(QuickAllPoll);
        return true;
    }
}
