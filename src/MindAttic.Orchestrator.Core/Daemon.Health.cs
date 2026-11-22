using System.Diagnostics;
using System.Globalization;
using MindAttic.Orchestrator.Settings;
using Serilog.Events;

namespace MindAttic.Orchestrator;

/// <summary>
/// The health probes on a timer (M.ORCH-T2037): the daemon launches each probe CLI (<c>orchestrator probe machine</c>,
/// <c>links</c>, <c>azure</c>) as its own hidden process with <c>--once</c> on its own interval
/// (<see cref="OrchestratorSettings.HealthProbeMachineSeconds"/>, <see cref="OrchestratorSettings.HealthProbeLinksSeconds"/>,
/// <see cref="OrchestratorSettings.HealthProbeAzureSeconds"/>; 0 turns one off), so the dashboard gets fresh reports over the app
/// bus without anyone running them. The probe publishes its own report; the daemon only starts it, never waits on it on the tick,
/// and logs when one couldn't run (Nagios 3, another exit code, no start, or past its timeout and killed) and when it runs again.
/// A warn or critical exit is the probe's verdict, not a failure, and is left to the report. One daemon runs them
/// (<see cref="HealthProbeLeaseName"/>).
/// </summary>
public sealed partial class Daemon
{
    /// <summary>The lease the daemon that runs the health probes holds, so a second daemon doesn't run them twice.</summary>
    public const string HealthProbeLeaseName = "health-probes.lock";

    /// <summary>The seconds past its own timeout a probe may run before it is killed as stuck.</summary>
    internal const double ProbeGraceSeconds = 60;

    /// <summary>The probes the daemon runs and how often (seconds; 0 or less is off).</summary>
    internal static IReadOnlyList<(string Name, double Seconds)> HealthProbes(OrchestratorSettings s) =>
    [
        ("machine", s.HealthProbeMachineSeconds),
        ("links", s.HealthProbeLinksSeconds),
        ("azure", s.HealthProbeAzureSeconds),
    ];

    /// <summary>The arguments of one daemon run of <paramref name="probe"/>: one check, timed out at its interval (1 s to a day).</summary>
    internal static string ProbeArguments(string probe, double seconds) =>
        $"probe {probe} --once --interval {Math.Clamp(Math.Round(seconds), 1, 86_400).ToString(CultureInfo.InvariantCulture)}";

    /// <summary>What a probe's exit code says went wrong, or null when it ran (0 ok, 1 warn, 2 critical are its verdict).</summary>
    internal static string? ProbeFailure(int exitCode, string? stderr)
    {
        if (exitCode is >= 0 and <= 2) return null;
        var said = stderr?.Trim();
        var why = exitCode == 3 ? "couldn't run" : $"exited {exitCode}";
        return string.IsNullOrEmpty(said) ? why : $"{why}: {said}";
    }

    private sealed class ProbeRun
    {
        public DateTime DueUtc;
        public Process? Running;
        public DateTime StartedUtc;
        public double TimeoutSeconds;
        public Task<string>? Stderr;
        public string? Failing;
    }

    private readonly Dictionary<string, ProbeRun> probeRuns = new(StringComparer.OrdinalIgnoreCase);
    private bool probeExeMissingSaid;

    /// <summary>Every tick: finishes the probe runs that ended (logging a failure or a recovery), kills one past its timeout,
    /// and starts each probe that is due and not still running.</summary>
    private void RunHealthProbes(OrchestratorSettings s, DateTime now)
    {
        string? exe = null;
        foreach (var (name, seconds) in HealthProbes(s))
        {
            if (!probeRuns.TryGetValue(name, out var run)) probeRuns[name] = run = new ProbeRun();
            if (run.Running is { } p)
            {
                if (!Exited(p))
                {
                    if ((now - run.StartedUtc).TotalSeconds <= run.TimeoutSeconds + ProbeGraceSeconds) continue;
                    try { p.Kill(entireProcessTree: true); } catch { /* already gone */ }
                    Finish(name, run, $"still running after {(now - run.StartedUtc).TotalSeconds:0} s, so it was ended");
                }
                else
                {
                    string? stderr = null;
                    try { if (run.Stderr is { } e && e.Wait(TimeSpan.FromSeconds(2))) stderr = e.Result; } catch { /* no output */ }
                    int code;
                    try { code = p.ExitCode; } catch (InvalidOperationException) { code = -1; }
                    Finish(name, run, ProbeFailure(code, stderr));
                }
            }
            if (!(seconds > 0) || now < run.DueUtc) continue;
            run.DueUtc = now + OrchestratorSettings.SecondsSpan(seconds, 1);
            exe ??= Scheduling.BranchQueue.OrchestratorExe();
            if (exe is null)
            {
                if (!probeExeMissingSaid)
                    Say(LogEventLevel.Warning, "health probes: no orchestrator CLI to run them (not installed in C:\\Apps\\MindAttic.Orchestrator\\Cli); the dashboard gets no fresh reports");
                probeExeMissingSaid = true;
                return;
            }
            probeExeMissingSaid = false;
            Start(name, run, exe, seconds, now);
        }
    }

    private void Start(string name, ProbeRun run, string exe, double seconds, DateTime now)
    {
        var info = new ProcessStartInfo(exe, ProbeArguments(name, seconds))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(exe) ?? Environment.CurrentDirectory,
        };
        try
        {
            var p = Process.Start(info) ?? throw new InvalidOperationException("the process didn't start");
            // Both pipes drained so a chatty probe never blocks on a full one; only stderr says why one couldn't run.
            _ = p.StandardOutput.ReadToEndAsync();
            run.Stderr = p.StandardError.ReadToEndAsync();
            run.Running = p;
            run.StartedUtc = now;
            run.TimeoutSeconds = Math.Clamp(seconds, 1, 86_400);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Fail(name, run, $"couldn't start it: {ex.Message}");
        }
    }

    private void Finish(string name, ProbeRun run, string? failure)
    {
        run.Running?.Dispose();
        run.Running = null;
        run.Stderr = null;
        if (failure is not null) { Fail(name, run, failure); return; }
        if (run.Failing is null) return;
        run.Failing = null;
        Say(LogEventLevel.Information, $"health probe {name}: runs again");
    }

    /// <summary>Logs a probe's failure when it starts failing or fails a new way; the same failure run after run is said once.</summary>
    private void Fail(string name, ProbeRun run, string failure)
    {
        if (failure == run.Failing) return;
        run.Failing = failure;
        Say(LogEventLevel.Warning, $"health probe {name}: {failure} (trying again next interval)");
    }

    private static bool Exited(Process p)
    {
        try { return p.HasExited; }
        catch (InvalidOperationException) { return true; }
    }
}
