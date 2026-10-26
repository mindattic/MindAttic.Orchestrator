using System.Windows;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.TermViewer;

/// <summary>
/// <c>MindAttic.Orchestrator.TermViewer --session &lt;id&gt;</c> (M.ORCH-T960): one window on one hosted CLI, for while the dashboard
/// is down. It exits 0 without a window when the dashboard is up (attaching would take the session from its tab), when another
/// viewer already holds the session (<see cref="TermViewerSlot"/>), or when the session is gone. It exits 0 too when the person
/// closes it or it folds back into the dashboard, so the daemon reopens only a viewer that crashed.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        OrchestratorLog.Start("TermViewer");
        var log = OrchestratorLog.For("TermViewer");
        try
        {
            // The team wall's pop-out of a teammate's CLI, through their gateway (M.ORCH-T936).
            if (RemoteViewer.RemoteArg(args) is { } remote)
                return RemoteViewer.Run(remote, SessionArg(args), args.Contains("--interact", StringComparer.OrdinalIgnoreCase), log);
            if (SessionArg(args) is not { } id)
            {
                log.Warning("Started without --session <id>; nothing to show");
                return 2;
            }
            // --stay (for trying it out beside a running dashboard): shown even then, and never folds back.
            // --watch: a read-only Hello (M.ORCH-T931); it only ever shows the session. The dashboard's "Pop out" menu starts it
            // with --stay (and --watch for watch only), beside the tab, on a protocol 2 host.
            var stay = args.Contains("--stay", StringComparer.OrdinalIgnoreCase);
            var watchOnly = args.Contains("--watch", StringComparer.OrdinalIgnoreCase);
            if (!stay && DashboardPresence.IsUp())
            {
                log.Information("The dashboard is up; it shows {Id:l}, so this viewer exits", id);
                return 0;
            }
            // Only an interactive viewer holds or counts for the session's slot (M.ORCH-T977): a --watch pop-out holding it would
            // stop the daemon opening the interactive fallback window when the dashboard goes down.
            var first = true;
            using var slot = watchOnly ? null : new Mutex(initiallyOwned: true, TermViewerSlot.MutexName(id), out first);
            if (!first && !stay)
            {
                log.Information("Another viewer already shows {Id:l}; this one exits", id);
                return 0;
            }
            try
            {
                // A deliberate pop-out (--stay) may share the session with another viewer, so it never exits for a held slot.
                if (TermSessions.Get(id) is not { } session)
                {
                    log.Information("No live hosted session {Id:l}; nothing to show", id);
                    return 0;
                }
                var pane = TermPaneFile.Read(id) ?? TermPaneFile.FromRecord(session);
                var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
                // A fault in one handler is logged and contained: left unhandled it would close the window (never the CLI).
                app.DispatcherUnhandledException += (_, e) =>
                {
                    log.Error(e.Exception, "TermViewer hit an unhandled exception on the UI thread; contained");
                    e.Handled = true;
                };
                AppDomain.CurrentDomain.UnhandledException += (_, e) => log.Fatal(e.ExceptionObject as Exception, "TermViewer crashed");
                TaskScheduler.UnobservedTaskException += (_, e) => { log.Error(e.Exception, "Unobserved task exception"); e.SetObserved(); };
                var window = new ViewerWindow(session, pane, stay, watchOnly);
                log.Information("Showing {Id:l} ({Title:l}) while the dashboard is down", id, session.Title ?? "");
                return app.Run(window);
            }
            finally
            {
                try { if (first) slot?.ReleaseMutex(); }
                catch (ApplicationException) { }
            }
        }
        catch (Exception ex)
        {
            log.Fatal(ex, "TermViewer failed");
            return 1;
        }
        finally
        {
            OrchestratorLog.Stop();
        }
    }

    /// <summary>The id after <c>--session</c>, or null.</summary>
    internal static string? SessionArg(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count - 1; i++)
            if (string.Equals(args[i], "--session", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(args[i + 1]))
                return args[i + 1].Trim();
        return null;
    }
}
