using System.Windows;
using System.Windows.Threading;
using MindAttic.Components.Wpf.Controls;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Wpf.ViewModels;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// "A crash always needs to dump everything into restorable states" (M.ORCH-T862). The dashboard's every terminal tab, the
/// Claude session its CLI is in, and the person and tab on screen are written to <see cref="SessionRestoreStore"/>'s file
/// continuously: whenever a tab opens, closes or moves, a session is learned, or the selection changes (a hard crash or a kill
/// can run no code, so the file must already be current). The crash handlers and ProcessExit try one last write; a clean exit
/// writes it marked clean. The next start offers to reopen an unclean one's tabs, each Claude CLI with <c>claude --resume</c>.
/// Every step here is contained: nothing in it may ever take the dashboard down.
/// </summary>
internal static class SessionRestore
{
    private static readonly Serilog.ILogger Log = OrchestratorLog.For("Restore");
    private static readonly SessionRestoreStore Store = new();
    private static MainViewModel? model;
    private static Dispatcher? dispatcher;
    private static RestoreState? last;
    private static bool pending, frozen, exited;

    /// <summary>The last run's state, read at <see cref="Begin"/> before it was rotated aside; null when none.</summary>
    internal static RestoreState? Previous { get; private set; }

    /// <summary>At start, before any window: reads the last run's file, then rotates it to <c>session.1.json</c> (kept, never
    /// deleted on read), so this run writes its own.</summary>
    internal static void Begin()
    {
        try
        {
            Previous = Store.Load();
            var rotated = Store.Rotate();
            if (Previous is { } p)
                Log.Information("Last run's session-restore file ({Reason:l}, {Clean:l}, {Count} tab(s), saved {Saved:u}) kept as {Path:l}",
                    p.Reason ?? "?", p.Clean ? "clean exit" : "not a clean exit", p.Tabs.Count, p.SavedUtc, rotated ?? "(not rotated)");
        }
        catch (Exception ex) { Log.Error(ex, "Reading the last session-restore file failed; contained"); }
    }

    /// <summary>Starts writing <paramref name="root"/>'s tabs whenever they change.</summary>
    internal static void Track(MainViewModel root)
    {
        try
        {
            model = root;
            dispatcher = Dispatcher.CurrentDispatcher;
            var all = root.AllTerminals;
            all.Panes.CollectionChanged += (_, _) => Changed();
            all.LoginsChanged += Changed;
            all.SessionsLearned += Changed;
            all.Changed += Changed; // a tab's TermHost session became known (M.ORCH-T926)
            all.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TerminalsViewModel.SelectedPane)) Changed(); };
            root.SelectedTabChanged += (_, _) => Changed();
        }
        catch (Exception ex) { Log.Error(ex, "Tracking the terminals for session restore failed; contained"); }
    }

    /// <summary>Something worth saving changed: written once the burst settles (one write for many tabs opening at once).</summary>
    internal static void Changed()
    {
        if (frozen || pending || dispatcher is null) return;
        pending = true;
        try
        {
            dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                pending = false;
                Write("change", clean: false);
            });
        }
        catch (Exception ex) { pending = false; Log.Error(ex, "Queuing a session-restore write failed; contained"); }
    }

    /// <summary>The window is closing: its tabs as they are now are the ones kept; their teardown after this isn't written.</summary>
    internal static void Freeze()
    {
        if (frozen) return;
        Write("closing", clean: false);
        frozen = true;
    }

    /// <summary>The dashboard is exiting on its own: the last state written again, marked clean, so the next start offers nothing.
    /// An upgrade restarting it (<paramref name="clean"/> false) is left to offer its tabs back.</summary>
    internal static void Exit(bool clean, string reason)
    {
        if (exited) return;
        Freeze();
        WriteLast(reason, clean);
        exited = true;
    }

    /// <summary>A best-effort last write from a crash handler or ProcessExit, from any thread: the tabs read afresh when this is
    /// the UI thread, else the last state written. Never after a clean exit's.</summary>
    internal static void Crash(string reason)
    {
        if (exited) return;
        try
        {
            if (!frozen && dispatcher is { } d && d.CheckAccess()) Write(reason, clean: false);
            else WriteLast(reason, clean: false);
        }
        catch (Exception) { }
    }

    private static void Write(string reason, bool clean)
    {
        if (frozen || model is null) return;
        try
        {
            var state = model.CaptureRestore(reason, clean);
            last = state;
            Store.Save(state);
        }
        catch (Exception ex) { Log.Error(ex, "Writing the session-restore file failed; contained"); }
    }

    private static void WriteLast(string reason, bool clean)
    {
        try
        {
            if (last is null) return;
            Store.Save(last with { Reason = reason, Clean = clean, SavedUtc = DateTime.UtcNow });
        }
        catch (Exception) { }
    }

    /// <summary>
    /// At start, when the last run didn't exit cleanly and had tabs open: asks (listing them) whether to reopen them, and does.
    /// Returns true when it reopened any, so the caller reselects the person and tab after the first refresh
    /// (<see cref="MainViewModel.ReselectRestored"/>).
    /// </summary>
    internal static bool Offer(Window owner, MainViewModel root)
    {
        try
        {
            if (Previous is null) return false;
            // Tabs whose TermHost still lives are reattached by the dashboard, never offered for --resume (M.ORCH-T928).
            var state = SessionRestoreStore.Unhosted(Previous);
            if (!SessionRestoreStore.ShouldOffer(state)) return false;
            var lines = SessionRestoreStore.Describe(state);
            var when = state.SavedUtc.ToLocalTime().ToString("g");
            var answer = MaMessageBox.Show(owner,
                $"The dashboard didn't close cleanly last time ({state.Reason ?? "unknown"}, last saved {when}). These terminal tabs were open:\n\n" +
                string.Join("\n", lines.Take(20)) + (lines.Count > 20 ? $"\n...and {lines.Count - 20} more" : "") +
                "\n\nYes: Restore. Reopen them, each Claude CLI resuming its session (claude --resume) on its login.\nNo: Dismiss. Start without them (the file is kept in " +
                Store.Directory + ").",
                "Restore terminal tabs?", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
            if (answer != MessageBoxResult.Yes)
            {
                Log.Information("Restore of {Count} tab(s) dismissed", state.Tabs.Count);
                return false;
            }
            var results = root.RestoreTabs(state);
            foreach (var line in results) Log.Information("Restore: {Line:l}", line);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Restoring the last session's tabs failed; contained");
            return false;
        }
    }
}
