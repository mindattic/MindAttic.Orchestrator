using System.Diagnostics;
using System.Text;

namespace MindAttic.Orchestrator.Gateway;

/// <summary>
/// Runs the tickets CLI on the host for the owner's client (M.ORCH-T1479): an argument array, never a command line, in the
/// workspace folder the client typed it in (relative, so it can't leave the workspace), with a time limit.
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

    public static async Task<(int ExitCode, string Output)> RunAsync(IReadOnlyList<string> args, string? relativeDirectory, CancellationToken ct)
    {
        if (Exe() is not { } exe) return (127, "The tickets CLI isn't installed on the host (MindAttic.Tickets' run.ps1 installs it).");
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true, UseShellExecute = false, CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = Folder(relativeDirectory),
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.Environment["NO_COLOR"] = "1";
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("tickets didn't start");
        p.StandardInput.Close();
        var stdout = p.StandardOutput.ReadToEndAsync(ct);
        var stderr = p.StandardError.ReadToEndAsync(ct);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(Timeout);
        try { await p.WaitForExitAsync(limit.Token).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            try { p.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            return (124, $"tickets timed out on the host after {Timeout.TotalMinutes:0} minutes.");
        }
        return (p.ExitCode, await stdout.ConfigureAwait(false) + await stderr.ConfigureAwait(false));
    }
}
