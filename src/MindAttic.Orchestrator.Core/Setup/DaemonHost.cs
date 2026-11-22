using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Setup;

/// <summary>
/// One running daemon (M.ORCH-T1942): holds <see cref="Installer.DaemonMutex"/> for this process's life and runs <see cref="Daemon"/>
/// on a background thread, starting the loop again when it dies. Shared by the headless MindAttic.Orchestrator.Daemon.exe and the
/// dashboard exe's legacy <c>--daemon</c> mode, so both behave the same.
/// </summary>
public sealed class DaemonHost : IDisposable
{
    private readonly Mutex mutex;
    private readonly Thread loop;

    private DaemonHost(Mutex mutex)
    {
        this.mutex = mutex;
        loop = new Thread(RunLoop) { IsBackground = true, Name = "Daemon" };
    }

    /// <summary>The thread the daemon loop runs on; it ends only if <see cref="Daemon.Run"/> ever returns.</summary>
    public Thread Loop => loop;

    /// <summary>Takes the daemon mutex and starts the loop, or returns null (logged) when this machine is a client of its host or
    /// another daemon already holds the mutex. Call after <see cref="OrchestratorLog.Start"/>.</summary>
    public static DaemonHost? Start()
    {
        var log = OrchestratorLog.For("Daemon");
        // A client's daemon is the host's (M.ORCH-T1473): Scheduler's logon job still starts this, and it exits at once.
        if (!MachineTopology.Current.RunsDaemon)
        {
            log.Information("This machine is a client of {Host:l}; the host runs the daemon, so this one exits", MachineRole.Current.Host ?? "its host");
            return null;
        }
        var mutex = new Mutex(initiallyOwned: true, Installer.DaemonMutex, out var first);
        if (!first)
        {
            mutex.Dispose();
            log.Information("A daemon is already running; this one exits");
            return null;
        }
        var host = new DaemonHost(mutex);
        host.loop.Start();
        ThreadPool.QueueUserWorkItem(_ => PruneExtractions());
        return host;
    }

    /// <summary>This process holds the daemon mutex for good, so a loop that dies must start again here (M.ORCH-T1615): left dead,
    /// no daemon would run (no pacing, wakes or fallback viewers) until someone restarted it by hand.</summary>
    private static void RunLoop()
    {
        for (var failures = 0; ; failures++)
        {
            try
            {
                new Daemon(new JsonStore()).Run(TimeSpan.FromSeconds(15), CancellationToken.None);
                return;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Both numbers are Vault settings (M.ORCH-T1619); defaults when settings can't be read.
                var cs = new JsonStore().TryLoadSettings(out var loaded) ? loaded : new OrchestratorSettings();
                var cap = Math.Max(1, cs.DaemonRestartMaxSeconds);
                var pause = TimeSpan.FromSeconds(Math.Min(cap, Math.Max(1, cs.DaemonRestartSeconds) * Math.Pow(2, Math.Min(failures, 10))));
                OrchestratorLog.For("Daemon").Error(ex, "The daemon loop died; starting it again in {Seconds:0}s", pause.TotalSeconds);
                Thread.Sleep(pause);
            }
        }
    }

    /// <summary>Deletes old builds' unpacked single-file folders of this exe, the dashboard exe and its old-named copy (M.ORCH-T999),
    /// which unpacks under its own name each time it forwards a launch. Never throws.</summary>
    private static void PruneExtractions()
    {
        try
        {
            var (folders, bytes) = (0, 0L);
            var apps = new[] { Environment.ProcessPath, Installer.ExeName, Installer.LegacyExeName }
                .Where(n => !string.IsNullOrEmpty(n))
                .Select(n => Path.GetFileNameWithoutExtension(n!))
                .Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var app in apps)
            {
                var (f, b) = ExtractionPruner.Prune(ExtractionPruner.BaseDir(), app);
                folders += f;
                bytes += b;
            }
            if (folders > 0)
                OrchestratorLog.For("Daemon").Information("Deleted {Folders} old builds' unpacked files ({MB:N0} MB) from {Dir:l}",
                    folders, bytes / 1048576.0, ExtractionPruner.BaseDir());
        }
        catch (Exception ex) { OrchestratorLog.For("Daemon").Error(ex, "Pruning old builds' files failed"); }
    }

    /// <summary>Releases the daemon mutex (from another thread it is released when this process exits).</summary>
    public void Dispose()
    {
        try { mutex.ReleaseMutex(); }
        catch (ApplicationException) { }
        mutex.Dispose();
    }
}
