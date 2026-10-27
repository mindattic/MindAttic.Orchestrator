using System.Diagnostics;

namespace MindAttic.Orchestrator;

/// <summary>How a child process ended: its exit code, its stdout and stderr, and whether it was killed for running past its timeout.</summary>
public sealed record ProcessResult(int ExitCode, string Output, string Error, bool TimedOut)
{
    public static ProcessResult Killed { get; } = new(-1, "", "", TimedOut: true);

    /// <summary>The process itself exited (its <see cref="ExitCode"/> is real); with <see cref="TimedOut"/> it's only its output
    /// that was cut short, by a pipe something it started still held (bughunt 2026-10-27).</summary>
    public bool Exited { get; init; }
}

/// <summary>Runs a command to completion or its timeout; the daemon's seam for <c>tickets db sync</c> and <c>gh</c>.</summary>
public interface IProcessRunner
{
    /// <summary>Runs <paramref name="file"/> with <paramref name="args"/>; past <paramref name="timeout"/> the whole tree is killed and
    /// <see cref="ProcessResult.TimedOut"/> is set. A missing program throws <see cref="System.ComponentModel.Win32Exception"/>.</summary>
    ProcessResult Run(string file, IReadOnlyList<string> args, TimeSpan timeout);
}

/// <summary>The real runner: no window, output captured, the process tree killed at the timeout without waiting on its pipes.</summary>
public sealed class ProcessRunner : IProcessRunner
{
    public static ProcessRunner Default { get; } = new();

    /// <summary>How long the pipes are waited on once the process exited: a held pipe won't come free by waiting out the rest of a
    /// long timeout (a test run's, say), and a finished pipe drains at once (bughunt 2026-10-27).</summary>
    public static readonly TimeSpan PipeGrace = TimeSpan.FromSeconds(30);

    public ProcessResult Run(string file, IReadOnlyList<string> args, TimeSpan timeout)
    {
        var psi = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var output = p.StandardOutput.ReadToEndAsync();
        var error = p.StandardError.ReadToEndAsync();
        var started = Stopwatch.StartNew();
        if (!p.WaitForExit(timeout))
        {
            // AggregateException: part of the tree couldn't be ended (protected, or exiting mid-kill); still killed as far as it goes.
            try { p.Kill(entireProcessTree: true); } catch (Exception ex) when (ex is InvalidOperationException or AggregateException) { } // exited meanwhile
            // Not waiting on the pipes: something outside the tree may still hold them, and the caller is the daemon's loop.
            return ProcessResult.Killed;
        }
        // The readers end at the pipes' EOF, not at the process's exit: a grandchild that inherited them (and outlives it)
        // would hold the daemon's loop forever, so the wait for them still counts against the timeout. Output cut short
        // that way is reported as timed out, never as a success with nothing to say; marked Exited, since the process itself
        // finished. The wait is at most PipeGrace (bughunt 2026-10-27).
        var left = timeout - started.Elapsed;
        if (left > PipeGrace) left = PipeGrace;
        if (!Task.WaitAll([output, error], left > TimeSpan.Zero ? left : TimeSpan.Zero))
            return new ProcessResult(p.ExitCode, output.IsCompletedSuccessfully ? output.Result : "",
                error.IsCompletedSuccessfully ? error.Result : "", TimedOut: true) { Exited = true };
        return new ProcessResult(p.ExitCode, output.Result, error.Result, TimedOut: false) { Exited = true };
    }
}
