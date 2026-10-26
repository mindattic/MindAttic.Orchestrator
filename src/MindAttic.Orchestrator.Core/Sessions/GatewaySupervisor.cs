using System.Diagnostics;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Term;
using Serilog.Events;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// Keeps the team gateway (M.ORCH-T933) in step with <see cref="OrchestratorSettings.Gateway"/>, once a daemon tick. On: it
/// starts the gateway as a per-user process when none runs, and restarts it when its settings changed or it exited (backing
/// off, doubling from a minute to half an hour, while it keeps failing). Off: it stops the gateway it started. It only ever
/// stops a gateway the daemon started (<see cref="TermGatewayStatus.Managed"/>), never one run by hand, and never touches a
/// TermHost: stopping or losing the gateway only drops remote viewers.
/// </summary>
public sealed class GatewaySupervisor(Action<LogEventLevel, string> say)
{
    public const string ProcessName = "MindAttic.Orchestrator.Gateway";
    internal static readonly TimeSpan FirstRetry = TimeSpan.FromMinutes(1), MaxRetry = TimeSpan.FromMinutes(30), Healthy = TimeSpan.FromMinutes(10);

    /// <summary>Where the gateway's status is read; a test's own folder.</summary>
    internal string? StatusDirectory { get; init; }

    /// <summary>Finds the gateway's exe; a test's stands in.</summary>
    internal Func<string?> FindExe { get; init; } = DefaultFindExe;

    /// <summary>Starts it and returns its pid; a test's records instead.</summary>
    internal Func<string, IReadOnlyList<string>, int?> StartProcess { get; init; } = DefaultStart;

    /// <summary>Stops the process with this pid; a test's records instead.</summary>
    internal Action<int> StopProcess { get; init; } = DefaultStop;

    /// <summary>The exe the process with this pid runs from, or null; a test's stands in.</summary>
    internal Func<int, string?> RunningExe { get; init; } = DefaultRunningExe;

    private DateTime lastStartUtc = DateTime.MinValue;
    private int failures;
    private string? lastSaid;

    public void Tick(GatewaySettings settings, DateTime nowUtc)
    {
        var running = TermGateway.Running(StatusDirectory);
        if (!settings.Enabled)
        {
            if (running is { Managed: true })
            {
                StopProcess(running.Pid);
                Say(LogEventLevel.Information, $"stopped the team gateway (pid {running.Pid}): it's turned off");
            }
            failures = 0;
            return;
        }

        var arguments = settings.Arguments();
        var config = string.Join(' ', arguments);
        if (running is not null)
        {
            if (!running.Managed) { SayOnce(LogEventLevel.Information, $"a team gateway started by hand runs (pid {running.Pid}); leaving it be"); return; }
            var newer = running.Config == config ? NewerInstall(running) : null;
            if (running.Config == config && newer is null)
            {
                if (nowUtc - lastStartUtc >= Healthy) failures = 0;
                return;
            }
            // A redeploy (M.ORCH-T1028): an old gateway (say, one whose masker leaks) must not keep serving remote viewers, and
            // its version folder can only be cleaned up once it exits. Remote viewers reconnect to the new one.
            StopProcess(running.Pid);
            Say(LogEventLevel.Information, newer is null
                ? $"stopped the team gateway (pid {running.Pid}) to restart it with its new settings"
                : $"stopped the team gateway (pid {running.Pid}, {Describe(running)}) to restart it on the newer install {newer}");
            lastStartUtc = DateTime.MinValue;
        }

        var wait = TimeSpan.FromTicks(Math.Min(MaxRetry.Ticks, FirstRetry.Ticks << Math.Min(failures, 10)));
        if (lastStartUtc != DateTime.MinValue && nowUtc - lastStartUtc < wait) return;
        if (FindExe() is not { } exe)
        {
            SayOnce(LogEventLevel.Warning, $"the team gateway is turned on but isn't installed: no {TermGateway.ExeName} found (set {TermGateway.PathVariable} for a dev build)");
            return;
        }
        if (lastStartUtc != DateTime.MinValue) failures++;
        lastStartUtc = nowUtc;
        var pid = StartProcess(exe, arguments);
        Say(pid is null ? LogEventLevel.Warning : LogEventLevel.Information, pid is null
            ? $"couldn't start the team gateway ({exe}); trying again later"
            : $"started the team gateway (pid {pid}) on https://{settings.Bind}:{settings.Port}");
    }

    /// <summary>
    /// The exe the gateway would start from now, when it isn't the one <paramref name="running"/> runs from (M.ORCH-T1028); null
    /// when it is, or when that can't be told. The running exe is the one its status names, else its process's main module (a
    /// gateway that predates <see cref="TermGatewayStatus.Exe"/>); when neither is known, its version is compared with that of the
    /// newest install's folder (<see cref="GatewayInstalls"/>).
    /// </summary>
    internal string? NewerInstall(TermGatewayStatus running)
    {
        if (FindExe() is not { } newest) return null;
        var current = !string.IsNullOrWhiteSpace(running.Exe) ? running.Exe : RunningExe(running.Pid);
        if (!string.IsNullOrWhiteSpace(current))
            return string.Equals(Normalize(current), Normalize(newest), StringComparison.OrdinalIgnoreCase) ? null : newest;
        var folder = Path.GetFileName(Path.GetDirectoryName(newest) ?? "");
        return TermHostInstalls.Parse(folder, "", GatewayInstalls.ExeName) is { } installed
               && (!Version.TryParse(running.Version, out var version) || version < installed.Version) ? newest : null;
    }

    private static string Describe(TermGatewayStatus running) =>
        string.IsNullOrWhiteSpace(running.Exe) ? $"version {(running.Version is { Length: > 0 } v ? v : "unknown")}" : running.Exe;

    private static string Normalize(string path)
    {
        try { return Path.GetFullPath(path).TrimEnd('\\'); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return path.TrimEnd('\\'); }
    }

    private void Say(LogEventLevel level, string line)
    {
        lastSaid = null;
        say(level, line);
    }

    private void SayOnce(LogEventLevel level, string line)
    {
        if (line == lastSaid) return;
        lastSaid = line;
        say(level, line);
    }

    /// <summary><see cref="TermGateway.PathVariable"/>, then the newest <c>Gateway\&lt;version&gt;\</c> under the Orchestrator's install folder
    /// (<see cref="GatewayInstalls"/>, M.ORCH-T967), then a plain <c>Gateway\</c> there, then beside this app.</summary>
    internal static string? DefaultFindExe()
    {
        var candidates = new List<string?> { Environment.GetEnvironmentVariable(TermGateway.PathVariable) };
        try
        {
            var installDir = new Setup.Installer().InstallDir;
            candidates.Add(GatewayInstalls.ResolveExe(installDir));
            candidates.Add(Path.Combine(installDir, GatewayInstalls.FolderName, TermGateway.ExeName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { }
        candidates.Add(Path.Combine(AppContext.BaseDirectory, TermGateway.ExeName));
        return candidates.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p));
    }

    private static int? DefaultStart(string exe, IReadOnlyList<string> arguments)
    {
        try
        {
            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(exe) ?? AppContext.BaseDirectory,
            };
            foreach (var a in arguments) psi.ArgumentList.Add(a);
            psi.Environment[TermGateway.ManagedVariable] = "1";
            using var p = Process.Start(psi);
            return p?.Id;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException) { return null; }
    }

    /// <summary>The gateway's main-module path; null when the pid isn't the gateway any more or it can't be read.</summary>
    private static string? DefaultRunningExe(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return string.Equals(p.ProcessName, ProcessName, StringComparison.OrdinalIgnoreCase) ? p.MainModule?.FileName : null;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Kills the gateway only: the pid must still be a process of the gateway's name.</summary>
    private static void DefaultStop(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            if (!string.Equals(p.ProcessName, ProcessName, StringComparison.OrdinalIgnoreCase)) return;
            p.Kill();
            // So a restart right after finds its single-instance mutex free.
            p.WaitForExit(5000);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
    }
}
