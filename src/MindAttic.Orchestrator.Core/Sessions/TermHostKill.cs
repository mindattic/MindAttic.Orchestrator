using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>Ends a hosted CLI (M.ORCH-T926): the dashboard tab's X and the standalone TermViewer's "End CLI" (M.ORCH-T960).</summary>
public static class TermHostKill
{
    /// <summary>Kills <paramref name="session"/>'s host and everything under it (its ConPTY and CLI), when its pid is still that
    /// host (not reused), and deletes the session's tab file. Never throws.</summary>
    public static void Kill(TermSessionRecord session)
    {
        try
        {
            if (TermSessions.HostStartOf(session.HostPid) is not { } started || (started - session.HostStartUtc).Duration() > TimeSpan.FromSeconds(1)) return;
            using var process = System.Diagnostics.Process.GetProcessById(session.HostPid);
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException or AggregateException) { }
        finally
        {
            TermPaneFile.Delete(session.Id);
        }
    }
}
