using System.Diagnostics;
using System.Windows.Input;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// The dashboard's Save button (M.ORCH-T1769) and its save states (M.ORCH-T1869), like an emulator's. Its left click is
/// Quick Save All (M.ORCH-T1803: the one Quicksave button that also pauses); its right-click menu force-quicksaves the shown
/// CLI alone (its handoff code kept on its tab, so the session-restore autosave carries it) or runs Quick Load All.
/// Quick Save All saves the whole dashboard as one package: it first stops new spending (/allocate off
/// in every CLI, each letting its running subagents land), force-quicksaves every open CLI, then writes every tab (login,
/// folder, Claude session, handoff code, /allocate and /crossrepo, subagents running) to a <see cref="SaveStateStore"/> slot.
/// Quick Load All restores the newer of that package and the session-restore autosave (so a crash or a Windows restart comes
/// back too): every tab not open is reopened with <c>claude --resume</c>, each CLI gets <c>/quickload</c> on its handoff (or a
/// "carry on"), then its /crossrepo and /allocate back, which picks its interrupted tickets up again; the tab that was on screen
/// is shown. One CLI failing never holds the rest up; each run reports what it did.
/// </summary>
public sealed partial class MainViewModel
{
    private bool quicksavingAll, quickloadingAll;

    /// <summary>How long one pane's forced <c>/quicksave</c> is given to land in its repo's handoff file before it's given
    /// up on (not an error - just reported, and the rest of a Quick Save All run isn't held up by one stuck CLI).</summary>
    private static readonly TimeSpan QuickAllTimeout = TimeSpan.FromSeconds(90);

    private static readonly TimeSpan QuickAllPoll = TimeSpan.FromSeconds(2);

    /// <summary>How long a reopened CLI is given to come up (its agent seen by the status read) before it's typed into anyway.</summary>
    private static readonly TimeSpan ReopenReadyTimeout = TimeSpan.FromSeconds(60);

    /// <summary>What a Quick Save All or Quick Load All did, for the completion modal.</summary>
    internal sealed record QuickAllResult(TimeSpan Elapsed, int Acted, int Total, string Note)
    {
        /// <summary>Quick Load All only: one line per CLI it resumed (its tab, its repo, its handoff code or "carry on"), for
        /// <c>/quickload all</c>'s report (M.ORCH-T2070).</summary>
        public IReadOnlyList<string> Resumed { get; init; } = [];
    }

    private void InitSave()
    {
        QuicksaveShownCommand = new RelayCommand(async () => await QuicksaveShownAsync(),
            () => AllTerminals.SelectedPane is { Ended: false, Request.IsMenu: false, Request.Unlisted: false });
        QuickSaveAllCommand = new RelayCommand(async () => await QuickSaveAllAsync(), () => !quicksavingAll);
        QuickLoadAllCommand = new RelayCommand(async () => await QuickLoadAllAsync(), () => !quickloadingAll);
    }

    /// <summary>Left-click: quicksaves the shown CLI alone.</summary>
    public ICommand QuicksaveShownCommand { get; private set; } = null!;

    /// <summary>Right-click menu, "Quick Save All" (<see cref="MainWindow"/> confirms first and reports what it did).</summary>
    public ICommand QuickSaveAllCommand { get; private set; } = null!;

    /// <summary>Right-click menu, "Quick Load All" (<see cref="MainWindow"/> confirms first and reports what it did).</summary>
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

    /// <summary>"Quick Save All" (right-click menu, confirmed first by <see cref="MainWindow"/>): the whole dashboard saved as
    /// one package (M.ORCH-T1869). Every CLI's /allocate and /crossrepo is read, /allocate is turned off everywhere so no new
    /// work starts (running subagents finish and land), every open CLI is force-quicksaved (sequenced within a repo, whose
    /// handoff is one slot, parallel across repos), and every tab is written to a save-state slot. Closes nothing.</summary>
    internal async Task<QuickAllResult> QuickSaveAllAsync()
    {
        if (!IsGlobalTab) return await Root.QuickSaveAllAsync();
        if (quicksavingAll) return new(TimeSpan.Zero, 0, 0, "A Quick Save All is already running.");
        quicksavingAll = true;
        var clock = Stopwatch.StartNew();
        try
        {
            var panes = OpenPanes();
            if (panes.Count == 0) return new(clock.Elapsed, 0, 0, "");

            // What each CLI was doing, read before /allocate goes off, so a load puts it back.
            var cycler = new ContextCycler();
            var allocating = Safe(() => cycler.AllocateClis(), new Dictionary<int, IReadOnlyList<string>>());
            var marks = new Dictionary<TerminalPaneViewModel, (IReadOnlyList<string>? Allocate, IReadOnlyList<string>? Crossrepo)>();
            foreach (var pane in panes)
                marks[pane] = pane.StatusRow?.CliPid is { } pid
                    ? (allocating.TryGetValue(pid, out var repos) ? repos : null, Safe(() => cycler.CrossRepoOf(pid), null))
                    : (null, null);

            foreach (var (pane, m) in marks) if (m.Allocate is not null) windMarks[pane] = m;

            // Stop spending: nothing new is claimed or started; subagents already working finish and land.
            var wound = Safe(() => CliMarks.WindDownEverywhere(cycler.Directory), 0);

            var codes = new string?[panes.Count];
            var byRepo = panes.Select((p, i) => (Pane: p, Index: i)).GroupBy(x => x.Pane.Request.Directory, StringComparer.OrdinalIgnoreCase);
            await Task.WhenAll(byRepo.Select(async group =>
            {
                foreach (var (pane, index) in group) codes[index] = await QuicksaveOneAsync(pane);
            }));

            // The package: every tab as the session-restore file has it (the same order as its panes), plus what was read above.
            var live = AllTerminals.Panes.Where(p => !p.Ended).ToList();
            var state = CaptureRestore(SaveStateStore.SaveAllReason, clean: false);
            var tabs = state.Tabs.Select((tab, i) => i < live.Count && marks.TryGetValue(live[i], out var m)
                ? tab with { Allocate = m.Allocate, Crossrepo = m.Crossrepo } : tab).ToList();
            var path = new SaveStateStore().Save(state with { Tabs = tabs });
            SessionRestore.Changed();

            var saved = codes.Count(c => c is { Length: > 0 });
            var note = (path is null ? "The save state couldn't be written (see the log)." : $"Saved {tabs.Count} tab(s) as one save state.")
                + (wound > 0 ? $" /allocate is off in {wound} CLI(s); their running subagents finish, nothing new starts. Quick Load All turns it back on." : "");
            OrchestratorLog.For("SaveState").Information("Quick Save All: {Saved} of {Total} quicksaved, {Wound} wound down, {Tabs} tab(s) saved to {Path:l}",
                saved, panes.Count, wound, tabs.Count, path ?? "(not written)");
            return new(clock.Elapsed, saved, panes.Count, note);
        }
        finally { quicksavingAll = false; }
    }

    /// <summary>"Quick Load All" (right-click menu, confirmed first by <see cref="MainWindow"/>): the newer of the last save
    /// state and the session-restore autosave, restored (M.ORCH-T1869). Every saved tab not open now is reopened (its CLI with
    /// <c>claude --resume</c> on its login), then each CLI gets <c>/quickload</c> on its handoff (else a "carry on"), then its
    /// /crossrepo and /allocate as they were; the tab that was on screen is shown again.
    /// <para>On start (M.ORCH-T2069) it runs on its own with <paramref name="alive"/>, the CLIs whose TermHost outlived the last
    /// run and were reattached: they never lost their place, so each gets only its /crossrepo and /allocate back (what Quick
    /// Save All turned off), never a /quickload over the work it's still doing; the tab on screen is left as reselected.</para></summary>
    internal async Task<QuickAllResult> QuickLoadAllAsync(IReadOnlySet<TerminalPaneViewModel>? alive = null)
    {
        if (!IsGlobalTab) return await Root.QuickLoadAllAsync(alive);
        if (quickloadingAll) return new(TimeSpan.Zero, 0, 0, "A Quick Load All is already running.");
        quickloadingAll = true;
        var clock = Stopwatch.StartNew();
        try
        {
            var state = SaveStateStore.Pick(new SaveStateStore().Latest(), SessionRestore.Previous);
            if (state is null) return new(clock.Elapsed, 0, 0, "There's no save state yet: Quick Save All makes one.");
            new SaveStateStore().MarkLoaded(state);
            var auto = alive is not null;

            var targets = new List<(TerminalPaneViewModel Pane, RestoreTab Tab, bool Reopened, bool Alive)>();
            TerminalPaneViewModel? shown = null;
            var reopened = 0;
            for (var i = 0; i < state.Tabs.Count; i++)
            {
                var tab = state.Tabs[i];
                var pane = OpenPaneOf(tab);
                var isNew = false;
                if (pane is null)
                {
                    var (opened, line) = ReopenTab(tab);
                    OrchestratorLog.For("SaveState").Information("Quick Load All: {Line:l}", line);
                    if (opened is null) continue;
                    pane = opened;
                    isNew = true;
                    reopened++;
                }
                if (state.SelectedTab == i) shown = pane;
                if (!tab.Request.IsMenu) targets.Add((pane, tab, isNew || auto && !alive!.Contains(pane), auto && alive!.Contains(pane)));
            }
            if (shown is not null && !auto) ShowPane(shown);

            var sent = new bool[targets.Count];
            var byRepo = targets.Select((t, i) => (Target: t, Index: i)).GroupBy(x => x.Target.Tab.Request.Directory, StringComparer.OrdinalIgnoreCase);
            await Task.WhenAll(byRepo.Select(async group =>
            {
                foreach (var (target, index) in group) sent[index] = await ResumeOneAsync(target.Pane, target.Tab, target.Reopened, target.Alive);
            }));
            var allocating = targets.Count(t => t.Tab.Allocate is { Count: > 0 });
            var note = $"From the {(state.Reason == SaveStateStore.SaveAllReason ? "save state" : "autosave")} of {state.SavedUtc.ToLocalTime():g}: "
                + $"{reopened} tab(s) reopened, {targets.Count - reopened} already open"
                + (allocating > 0 ? $", /allocate turned back on in {allocating}." : ".");
            var resumed = targets.Where((_, i) => sent[i]).Select(t =>
                $"{t.Pane.Name} in {System.IO.Path.GetFileName(t.Tab.Request.Directory.TrimEnd('\\', '/'))}: "
                + (t.Alive ? "kept its place (/crossrepo and /allocate only)"
                    : t.Tab.HandoffCode is { Length: > 0 } code ? $"/quickload {code}" : "carry on (no handoff code)")
                + (t.Reopened ? ", reopened" : "")).ToList();
            return new(clock.Elapsed, sent.Count(s => s), targets.Count, note) { Resumed = resumed };
        }
        finally { quickloadingAll = false; }
    }

    /// <summary>On start (M.ORCH-T2069, M.ORCH-T1803 part 2): when the newer of the last save state and the last run's
    /// autosave was saved after the state last restored, Quick Load All runs on its own - no prompt, nothing closed: every CLI
    /// reopened gets <c>/quickload</c> on its handoff (else a "carry on"), and every one its /crossrepo and /allocate back.
    /// <paramref name="alive"/> is the CLIs reattached from a TermHost that outlived the last run (see
    /// <see cref="QuickLoadAllAsync"/>). Null when there was nothing newer to restore.</summary>
    internal async Task<QuickAllResult?> RestoreAllOnStartAsync(IReadOnlySet<TerminalPaneViewModel> alive)
    {
        var store = new SaveStateStore();
        var state = SaveStateStore.Pick(store.Latest(), SessionRestore.Previous);
        if (!SaveStateStore.IsNewer(state, store.LastLoadedUtc())) return null;
        var result = await QuickLoadAllAsync(alive);
        OrchestratorLog.For("SaveState").Information("Restore all on start: {Acted} of {Total} CLI(s) resumed in {Seconds:0.0}s. {Note:l}",
            result.Acted, result.Total, result.Elapsed.TotalSeconds, result.Note);
        return result;
    }

    /// <summary>The open pane that is <paramref name="tab"/>: the same TermHost session, else the same Claude session.</summary>
    private TerminalPaneViewModel? OpenPaneOf(RestoreTab tab) => AllTerminals.Panes.FirstOrDefault(p => !p.Ended &&
        (tab.HostId is { Length: > 0 } host && string.Equals(p.TermId, host, StringComparison.OrdinalIgnoreCase)
         || tab.SessionId is { Length: > 0 } session && string.Equals(p.SessionId, session, StringComparison.OrdinalIgnoreCase)
         || tab.Request.IsMenu && p.Request.IsMenu && string.Equals(p.Login, tab.Login, StringComparison.OrdinalIgnoreCase)));

    /// <summary>One CLI back where it was: (once a reopened one is up) its handoff loaded, then /crossrepo and /allocate as
    /// saved, each typed as its own prompt (Claude queues one typed while it's busy). True once the first was typed.
    /// <paramref name="alive"/> (a CLI that outlived the last run, M.ORCH-T2069): no resume prompt, only the two modes.</summary>
    private async Task<bool> ResumeOneAsync(TerminalPaneViewModel pane, RestoreTab tab, bool reopened, bool alive = false)
    {
        try
        {
            if (reopened)
            {
                var deadline = DateTime.UtcNow + ReopenReadyTimeout;
                while (pane.StatusRow is null && !pane.Ended && DateTime.UtcNow < deadline) await Task.Delay(QuickAllPoll);
                await Task.Delay(QuickAllPoll);
            }
            if (pane.Ended) return false;
            if (alive)
            {
                var typed = false;
                foreach (var command in new[] { SaveStateStore.CrossrepoCommand(tab), SaveStateStore.AllocateCommand(tab) })
                    if (command is not null)
                    {
                        if (typed) await Task.Delay(QuickAllPoll);
                        typed |= Type(pane, command);
                    }
                return typed;
            }
            if (!Type(pane, SaveStateStore.ResumePrompt(tab))) return false;
            foreach (var command in new[] { SaveStateStore.CrossrepoCommand(tab), SaveStateStore.AllocateCommand(tab) })
                if (command is not null)
                {
                    await Task.Delay(QuickAllPoll);
                    Type(pane, command);
                }
            await Task.Delay(QuickAllPoll);
            return true;
        }
        catch (Exception ex)
        {
            OrchestratorLog.For("SaveState").Error(ex, "Resuming {Title:l} failed; contained", tab.Request.Title);
            return false;
        }
    }

    /// <summary>Types <paramref name="text"/> into <paramref name="pane"/> and presses Enter.</summary>
    private static bool Type(TerminalPaneViewModel pane, string text)
    {
        if (!pane.WriteInput(TerminalPaste.Payload(text, pane.Bracketed))) return false;
        pane.WriteInput("\r");
        return true;
    }

    private static T Safe<T>(Func<T> read, T fallback)
    {
        try { return read(); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return fallback; }
    }

    /// <summary>Types <c>/quicksave</c> into <paramref name="pane"/> and waits for its repo's handoff file to show a save
    /// written after this moment, up to <see cref="QuickAllTimeout"/>; null (not an error - just reported) when it never
    /// typed, or the CLI never took the turn in time (busy, hung, or not a quicksave-capable provider). The code is kept on
    /// the pane (M.ORCH-T1869), so the autosave and the next save state carry it.</summary>
    private async Task<string?> QuicksaveOneAsync(TerminalPaneViewModel pane)
    {
        var baselineUtc = DateTime.UtcNow;
        if (!Type(pane, "/quicksave")) return null;
        var path = QuicksaveCapture.SessionPath(pane.Request.Directory);
        var deadlineUtc = baselineUtc + QuickAllTimeout;
        while (DateTime.UtcNow < deadlineUtc)
        {
            await Task.Delay(QuickAllPoll);
            if (QuicksaveCapture.TryReadCode(path, baselineUtc) is { Length: > 0 } code)
            {
                pane.HandoffCode = code;
                SessionRestore.Changed();
                return code;
            }
        }
        return null;
    }
}
