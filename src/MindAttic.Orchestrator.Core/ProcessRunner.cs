using System.Diagnostics;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator;

/// <summary>How a child process ended: its exit code, its stdout and stderr, and whether it was killed for running past its timeout.</summary>
public sealed record ProcessResult(int ExitCode, string Output, string Error, bool TimedOut)
{
    /// <summary>The killed.</summary>
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

/// <summary>
/// The real runner: no window, output captured, the process tree killed at the timeout without waiting on its pipes.
/// <para>Every run is inside its own kill-on-close <see cref="JobObject"/> (M.ORCH-T1675), so the whole tree, however its
/// processes were started, ends at the timeout and when this process dies mid-run (an abandoned run: Windows closes the job
/// with it). A job Windows refuses is done without: <c>Kill(entireProcessTree: true)</c> stays as the fallback.</para>
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    /// <summary>The default: a command's leftovers are ended at its timeout or when this process dies, and left alone once the
    /// command itself has exited (git's background maintenance, a helper it started on purpose).</summary>
    public static ProcessRunner Default { get; } = new();

    /// <summary>For builds and tests (M.ORCH-T1675): as <see cref="Default"/>, and whatever the command left running once it
    /// exited (MSBuild nodes, testhost, VBCSCompiler) is ended too, so nothing holds its files or its output pipes.</summary>
    public static ProcessRunner Contained { get; } = new() { EndLeftovers = true };

    /// <summary>Whether processes the command leaves running after it exits are ended with it.</summary>
    public bool EndLeftovers { get; init; }

    /// <summary>How long the pipes are waited on once the process exited: a held pipe won't come free by waiting out the rest of a
    /// long timeout (a test run's, say), and a finished pipe drains at once (bughunt 2026-10-27).</summary>
    public static readonly TimeSpan PipeGrace = TimeSpan.FromSeconds(30);

    /// <summary>Runs.</summary>
    public ProcessResult Run(string file, IReadOnlyList<string> args, TimeSpan timeout)
    {
        var psi = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        // Made before the process, held to the end of the run: if this process dies first, the job closes and the tree ends.
        var job = JobObject.TryCreate(out _);
        try { return Run(psi, timeout, job); }
        finally
        {
            // Closing ends what's left in the job; releasing lets it run on.
            if (EndLeftovers) job?.Dispose();
            else job?.Release();
        }
    }

    private ProcessResult Run(ProcessStartInfo psi, TimeSpan timeout, JobObject? job)
    {
        using var p = Process.Start(psi)!;
        // Joined as soon as it exists: Process can't start one inside a job, so a child it started in these first moments would
        // be outside it. One that has already exited can't join, and has nothing left to own.
        try { job?.TryAssign(p.Handle, out _); }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        var output = p.StandardOutput.ReadToEndAsync();
        var error = p.StandardError.ReadToEndAsync();
        var started = Stopwatch.StartNew();
        if (!p.WaitForExit(timeout))
        {
            // The job first: every process in it, whoever its parent is or was. Then the tree by parent pid, the fallback for a
            // run without a job.
            job?.Terminate();
            // AggregateException: part of the tree couldn't be ended (protected, or exiting mid-kill); still killed as far as it goes.
            try { p.Kill(entireProcessTree: true); } catch (Exception ex) when (ex is InvalidOperationException or AggregateException) { } // exited meanwhile
            // Not waiting on the pipes: something outside the tree may still hold them, and the caller is the daemon's loop.
            return ProcessResult.Killed;
        }
        // A build's or test's leftovers end here, so the pipes they inherited reach EOF and the output is whole.
        if (EndLeftovers) job?.Terminate();
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
