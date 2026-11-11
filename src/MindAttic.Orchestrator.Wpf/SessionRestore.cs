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

    private static Window? window;

    /// <summary>
    /// The dashboard's window back where it was at the last run's end (M.ORCH-T1510), before it's shown: its normal bounds, then
    /// maximized when it was; skipped when they'd be off every screen now. Then its moves and resizes are kept the same way as
    /// the tabs, a second after each one settles.
    /// </summary>
    internal static void TrackWindow(Window main)
    {
        try
        {
            window = main;
            if (Previous?.Window is { } was
                && was.Fits(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight))
            {
                main.WindowStartupLocation = WindowStartupLocation.Manual;
                main.Left = was.Left;
                main.Top = was.Top;
                main.Width = was.Width;
                main.Height = was.Height;
                if (was.Maximized) main.WindowState = WindowState.Maximized;
            }
            var settle = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
            settle.Tick += (_, _) => { settle.Stop(); Changed(); };
            void Settle() { settle.Stop(); settle.Start(); }
            main.LocationChanged += (_, _) => Settle();
            main.SizeChanged += (_, _) => Settle();
            main.StateChanged += (_, _) => Settle();
        }
        catch (Exception ex) { Log.Error(ex, "Restoring the dashboard window's place failed; contained"); }
    }

    /// <summary>The window's bounds as they are now (its normal ones while maximized or minimized); null when unknown.</summary>
    private static RestoreWindow? WindowNow()
    {
        try
        {
            if (window is not { } w) return null;
            var normal = w.WindowState == WindowState.Normal ? new Rect(w.Left, w.Top, w.Width, w.Height) : w.RestoreBounds;
            if (normal.IsEmpty || !double.IsFinite(normal.Width)) return last?.Window;
            // Minimized keeps whether it was maximized before (what it comes back as), as the last write had it.
            var maximized = w.WindowState == WindowState.Maximized || (w.WindowState == WindowState.Minimized && last?.Window?.Maximized == true);
            return new RestoreWindow(normal.Left, normal.Top, normal.Width, normal.Height, maximized);
        }
        catch (Exception) { return last?.Window; }
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
        catch (Exception ex) { Log.Warning(ex, "The crash-time session-restore write failed; contained"); }
    }

    private static void Write(string reason, bool clean)
    {
        if (frozen || model is null) return;
        try
        {
            var state = model.CaptureRestore(reason, clean) with { Window = WindowNow() };
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
        catch (Exception ex) { Log.Warning(ex, "Rewriting the last session-restore state failed; contained"); }
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
