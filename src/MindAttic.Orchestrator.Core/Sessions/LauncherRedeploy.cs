using System.Diagnostics;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// Redeploys MindAttic.Launcher (M.ORCH-T1511): runs its own <c>run.ps1 -Force</c>, the same rebuild its in-app Redeploy uses
/// (MindAttic.Launcher/run.ps1's <c>-WaitPid</c>: "Redeploy means 'run fresh code'"), so a Launcher tab closed and reopened on
/// the dashboard runs the newest build instead of whatever already sat at <see cref="MindAttic.Orchestrator.OrchestratorOptions.LauncherExe"/>.
/// </summary>
public static class LauncherRedeploy
{
    /// <summary>How long a rebuild may run before it's given up on as hung.</summary>
    public static TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(3);

    /// <summary>Runs <paramref name="workspaceRoot"/>'s MindAttic.Launcher\run.ps1 -Force; null on success, else why not (its
    /// script missing, a non-zero exit with the tail of its output, or a timeout). Blocks the caller: run off the UI thread.
    /// Never throws.</summary>
    public static string? Run(string workspaceRoot)
    {
        var script = Path.Combine(workspaceRoot, "MindAttic.Launcher", "run.ps1");
        if (!File.Exists(script)) return $"MindAttic.Launcher's run.ps1 not found at {script}.";
        var psi = new ProcessStartInfo("powershell.exe")
        {
            WorkingDirectory = Path.GetDirectoryName(script),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script, "-Force" }) psi.ArgumentList.Add(a);
        try
        {
            using var process = Process.Start(psi);
            if (process is null) return "Couldn't start MindAttic.Launcher's run.ps1.";
            var output = new List<string>();
            var sync = new object();
            process.OutputDataReceived += (_, e) => { if (e.Data is { } line) lock (sync) output.Add(line); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is { } line) lock (sync) output.Add(line); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            if (!process.WaitForExit((int)Timeout.TotalMilliseconds))
            {
                try { process.Kill(true); } catch (Exception ex) when (ex is not OutOfMemoryException) { }
                return "Redeploying MindAttic.Launcher timed out.";
            }
            if (process.ExitCode == 0) return null;
            string tail;
            lock (sync) tail = string.Join(" ", output.Where(l => l.Length > 0).TakeLast(3));
            return $"Redeploying MindAttic.Launcher failed (exit {process.ExitCode})" + (tail.Length > 0 ? $": {tail}" : ".");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return $"Redeploying MindAttic.Launcher failed: {ex.Message}"; }
    }
}
