using System.Diagnostics;
using System.Text;

namespace MindAttic.Orchestrator.Gateway;

/// <summary>
/// Runs the tickets CLI on the host for a client (M.ORCH-T1479): the owner's commands, or a member's allowlisted ones, as an
/// argument array, never a command line, in the workspace folder the client typed it in (relative, so it can't leave the
/// workspace), with a time limit. Also the orchestrator CLI's <c>branches</c> listing and report for the owner
/// (<see cref="RunBranchesAsync"/>) and <c>start</c> for a member (<see cref="RunStartAsync"/>).
/// </summary>
internal static class TicketsRunner
{
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    /// <summary>The workspace on this machine: <c>MINDATTIC_WORKSPACE</c>, else the default every MindAttic app is built for.</summary>
    public static string WorkspaceRoot =>
        Environment.GetEnvironmentVariable("MINDATTIC_WORKSPACE") is { Length: > 0 } w ? w : @"D:\Projects\MindAttic";

    /// <summary>The tickets executable: the first <c>tickets.exe</c> on PATH, else the .NET tools folder's.</summary>
    public static string? Exe()
    {
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim().Trim('"'), "tickets.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException) { }
        }
        var tools = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "tools", "tickets.exe");
        return File.Exists(tools) ? tools : null;
    }

    /// <summary>The folder under the workspace <paramref name="relative"/> names, or the workspace root when it's empty, rooted,
    /// climbs out, or doesn't exist here.</summary>
    public static string Folder(string? relative)
    {
        var root = Path.GetFullPath(WorkspaceRoot);
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) return root;
        try
        {
            var full = Path.GetFullPath(Path.Combine(root, relative));
            var inside = full.Equals(root, StringComparison.OrdinalIgnoreCase)
                || full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            return inside && Directory.Exists(full) ? full : root;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return root; }
    }

    /// <summary>The orchestrator CLI here (M.ORCH-T1635): <c>MINDATTIC_ORCHESTRATOR</c>, else the installed one, else the first
    /// <c>orchestrator.exe</c> on PATH.</summary>
    public static string? OrchestratorExe()
    {
        if (Environment.GetEnvironmentVariable("MINDATTIC_ORCHESTRATOR") is { Length: > 0 } set && File.Exists(set)) return set;
        const string installed = @"C:\Apps\MindAttic.Orchestrator\Cli\orchestrator.exe";
        if (File.Exists(installed)) return installed;
        return OnPath("orchestrator.exe");
    }

    private static string? OnPath(string name)
    {
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim().Trim('"'), name);
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException) { }
        }
        return null;
    }

    /// <summary>
    /// Whether <paramref name="args"/> is an <c>orchestrator branches</c> command the owner's client may run here (M.ORCH-T1635):
    /// the host's work-branch queue (<c>branches</c> with options only, such as <c>--ready --json</c>) or a local test run's
    /// report (<c>branches report ...</c>). Nothing else of the orchestrator CLI is reachable through the gateway.
    /// </summary>
    public static bool BranchesAllowed(IReadOnlyList<string> args) =>
        args.Count > 0 && args[0] == "branches"
        && (args.Count == 1 || args[1] == "report" || args.Skip(1).All(a => a is "--ready" or "--returned" or "--json"));

    public static async Task<(int ExitCode, string Output)> RunAsync(IReadOnlyList<string> args, string? relativeDirectory, CancellationToken ct)
    {
        if (Exe() is not { } exe) return (127, "The tickets CLI isn't installed on the host (MindAttic.Tickets' run.ps1 installs it).");
        return await RunExeAsync(exe, "tickets", args, relativeDirectory, ct).ConfigureAwait(false);
    }

    /// <summary>Runs an allowed <c>orchestrator branches</c> command here (<see cref="BranchesAllowed"/>, M.ORCH-T1635).</summary>
    public static async Task<(int ExitCode, string Output)> RunBranchesAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        if (!BranchesAllowed(args)) return (2, "Only orchestrator branches (its listing, or report) runs on the host through the gateway.");
        if (OrchestratorExe() is not { } exe) return (127, "The orchestrator CLI isn't installed on the host (MindAttic.Orchestrator's run.ps1 installs it).");
        return await RunExeAsync(exe, "orchestrator", args, null, ct).ConfigureAwait(false);
    }

    /// <summary>Starts a CLI here for a member's client (hub phase 2, M.ORCH-T1641): <c>orchestrator start &lt;repo&gt; --account
    /// &lt;login&gt;</c>, nothing else of the orchestrator CLI. The caller checked the repo's shape and that the login is theirs.</summary>
    public static async Task<(int ExitCode, string Output)> RunStartAsync(string repo, string login, CancellationToken ct)
    {
        if (OrchestratorExe() is not { } exe) return (127, "The orchestrator CLI isn't installed on the host (MindAttic.Orchestrator's run.ps1 installs it).");
        return await RunExeAsync(exe, "orchestrator", ["start", repo, "--account", login], null, ct).ConfigureAwait(false);
    }

    private static async Task<(int ExitCode, string Output)> RunExeAsync(string exe, string name, IReadOnlyList<string> args, string? relativeDirectory, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true, UseShellExecute = false, CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = Folder(relativeDirectory),
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.Environment["NO_COLOR"] = "1";
        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"{name} didn't start");
        p.StandardInput.Close();
        // The limit covers reading the output too: a child of tickets still holding its stdout kept the reads (and the
        // request) waiting past it.
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(Timeout);
        // Read into buffers rather than ReadToEndAsync so what was read survives giving up on the reads (M.ORCH-T1966).
        using var reads = CancellationTokenSource.CreateLinkedTokenSource(limit.Token);
        var outText = new StringBuilder();
        var errText = new StringBuilder();
        var stdout = DrainAsync(p.StandardOutput, outText, reads.Token);
        var stderr = DrainAsync(p.StandardError, errText, reads.Token);
        try
        {
            await p.WaitForExitAsync(limit.Token).ConfigureAwait(false);
            // Once it has exited, a grandchild still holding stdout mustn't turn a finished command into a timeout
            // (M.ORCH-T1966): give the reads a moment, then answer with the real exit code and whatever was read.
            try { await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None).ConfigureAwait(false); }
            catch (TimeoutException) { reads.Cancel(); }
            return (p.ExitCode, Snapshot(outText) + Snapshot(errText));
        }
        catch (OperationCanceledException)
        {
            try { p.Kill(entireProcessTree: true); } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
            if (ct.IsCancellationRequested) return (130, $"The request was cancelled before {name} finished."); // the client went away: not a timeout
            return (124, $"{name} timed out on the host after {Timeout.TotalMinutes:0} minutes.");
        }
    }

    private static async Task DrainAsync(StreamReader reader, StringBuilder into, CancellationToken ct)
    {
        var buffer = new char[4096];
        try
        {
            int n;
            while ((n = await reader.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false)) > 0)
                lock (into) into.Append(buffer, 0, n);
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException) { }
    }

    private static string Snapshot(StringBuilder text)
    {
        lock (text) return text.ToString();
    }
}
