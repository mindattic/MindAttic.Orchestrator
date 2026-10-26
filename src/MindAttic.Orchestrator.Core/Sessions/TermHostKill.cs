using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>Ends a hosted CLI (M.ORCH-T926): the dashboard tab's X and the standalone TermViewer's "End CLI" (M.ORCH-T960).</summary>
public static class TermHostKill
{
    /// <summary>How long a killed host gets to be gone before its registry file is deleted, so a write it had in flight can't
    /// put the file back.</summary>
    private static readonly TimeSpan ExitWait = TimeSpan.FromSeconds(2);

    /// <summary>Kills <paramref name="session"/>'s host and everything under it (its ConPTY and CLI), when its pid is still that
    /// host (not reused), and deletes the session's tab file. A hard kill never runs the host's own unregistering, so after a
    /// successful one its registry file is deleted and <c>ended</c> announced on the bus from here (M.ORCH-T1033), off the
    /// caller's thread. Never throws.</summary>
    public static void Kill(TermSessionRecord session)
    {
        try
        {
            if (TermSessions.HostStartOf(session.HostPid) is not { } started || (started - session.HostStartUtc).Duration() > TimeSpan.FromSeconds(1)) return;
            using var process = System.Diagnostics.Process.GetProcessById(session.HostPid);
            process.Kill(entireProcessTree: true);
            _ = Task.Run(() => Unregister(session));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException or AggregateException) { }
        finally
        {
            TermPaneFile.Delete(session.Id);
        }
    }

    /// <summary>What the host's Registration.End would have done, once the killed host is gone: its registry file goes and
    /// <c>ended</c> is announced. Best effort; never throws.</summary>
    private static void Unregister(TermSessionRecord session)
    {
        try
        {
            try
            {
                using var process = System.Diagnostics.Process.GetProcessById(session.HostPid);
                process.WaitForExit(ExitWait);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { } // gone already
            TermSessions.Delete(session.Id);
            TermBus.TrySend(new TermSessionEvent(session, TermSessionEvents.Ended));
        }
        catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Unregistering killed host {Id:l} failed; contained", session.Id); }
    }
}
