using MindAttic.Orchestrator;
using MindAttic.Orchestrator.Setup;

// MindAttic.Orchestrator.Daemon.exe (M.ORCH-T1942): the always-on loop, headless. Takes the daemon mutex and runs Core's Daemon
// until the process is stopped; exits at once (0) when this machine is a client of its host or another daemon holds the mutex.
// Arguments are ignored, so a forwarded "--daemon" works unchanged.

// Whoever started us (a scheduled job, a forwarder), run with the PATH a fresh logon gives: the daemon needs tickets, and every
// TermHost and CLI started from here inherits it (M.ORCH-T1314).
ProcessPath.Refresh();
OrchestratorLog.Start("Daemon");
var log = OrchestratorLog.For("Daemon");
AppDomain.CurrentDomain.UnhandledException += (_, e) => log.Fatal(e.ExceptionObject as Exception, "Daemon crashed");
TaskScheduler.UnobservedTaskException += (_, e) => log.Error(e.Exception, "Unobserved task exception");
// Every CLI, git, dotnet and hook started from here inherits TEMP, so it goes under the temp root first (M.ORCH-T2219).
TempRootStartup.Enforce("Daemon");

try
{
    using var host = DaemonHost.Start();
    if (host is null) return 0;
    log.Information("Headless daemon started (pid {Pid}, {Exe:l})", Environment.ProcessId, Environment.ProcessPath);
    // The loop restarts itself when it fails, so this waits for good unless Daemon.Run ever returns.
    host.Loop.Join();
    return 0;
}
finally
{
    OrchestratorLog.Stop();
}
