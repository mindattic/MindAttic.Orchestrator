using System.ComponentModel;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Setup;

/// <summary>
/// <c>orchestrator spawn [--cwd &lt;dir&gt;] [--no-window] [--] &lt;program&gt; [arguments...]</c> (M.ORCH-T1691): starts a program that
/// is meant to outlive the caller, outside the caller's job (<see cref="Breakaway"/>), and returns at once. For scripts:
/// PowerShell's <c>Start-Process</c> can't ask for <c>CREATE_BREAKAWAY_FROM_JOB</c>, so a dashboard that <c>run.ps1</c> started
/// from a hosted CLI ended when that CLI's host did. Not for builds, tests or anything the caller waits on.
/// <para>Exit code 0: started (its pid is printed). 1: it couldn't be started. 2: the command line is wrong. A CLI from before
/// this verb answers 1 without starting anything (an unknown command), so a script falls back to <c>Start-Process</c>.</para>
/// Handled before the command app, like <c>--write-settings-manifest</c>: the program's own arguments are passed on as given,
/// never read as this CLI's options, and nothing else of the CLI starts (no log, no store).
/// </summary>
public static class SpawnVerb
{
    /// <summary>The verb: the CLI's first argument.</summary>
    public const string Verb = "spawn";

    /// <summary>The usage line printed when the command line is wrong.</summary>
    public const string Usage = "Usage: orchestrator spawn [--cwd <dir>] [--no-window] [--] <program> [arguments...]";

    /// <summary>What to start: the program, its arguments, where, and whether a console program gets no window.</summary>
    public sealed record Request(string Program, IReadOnlyList<string> Arguments, string? WorkingDirectory, bool NoWindow);

    /// <summary>Reads the arguments after the verb: this verb's own options up to the program (or <c>--</c>), then the program
    /// and everything after it, untouched. Null (and <paramref name="error"/>) when there is no program or an option is wrong.</summary>
    public static Request? Parse(IReadOnlyList<string> args, out string? error)
    {
        error = null;
        string? cwd = null;
        var noWindow = false;
        var at = 0;
        for (; at < args.Count; at++)
        {
            var a = args[at];
            if (a == "--") { at++; break; }
            if (string.Equals(a, "--no-window", StringComparison.OrdinalIgnoreCase)) { noWindow = true; continue; }
            if (string.Equals(a, "--cwd", StringComparison.OrdinalIgnoreCase))
            {
                if (++at >= args.Count || string.IsNullOrWhiteSpace(args[at])) { error = "--cwd needs a folder."; return null; }
                cwd = args[at];
                continue;
            }
            if (a.StartsWith('-')) { error = $"Unknown option {a} (put -- before a program whose name starts with a dash)."; return null; }
            break;
        }
        if (at >= args.Count || string.IsNullOrWhiteSpace(args[at])) { error = "No program to start."; return null; }
        return new Request(args[at], [.. args.Skip(at + 1)], cwd, noWindow);
    }

    /// <summary>Runs the verb when <paramref name="args"/> start with it; false (nothing done) for any other command line.
    /// <paramref name="start"/> is the test seam for the start itself.</summary>
    public static bool TryRun(IReadOnlyList<string> args, TextWriter output, TextWriter error, out int exitCode,
        Func<Request, BreakawayStart>? start = null)
    {
        exitCode = 0;
        if (args is null || args.Count == 0 || !string.Equals(args[0], Verb, StringComparison.Ordinal)) return false;
        if (Parse([.. args.Skip(1)], out var why) is not { } request)
        {
            error.WriteLine(why);
            error.WriteLine(Usage);
            exitCode = 2;
            return true;
        }
        if (request.WorkingDirectory is { } dir && !Directory.Exists(dir))
        {
            error.WriteLine($"Folder not found: {dir}");
            exitCode = 2;
            return true;
        }
        try
        {
            var started = (start ?? Start)(request);
            output.WriteLine(started.BrokeAway
                ? $"Started {request.Program} (pid {started.Pid})."
                : $"Started {request.Program} (pid {started.Pid}) inside this process's job, which doesn't allow breaking away: it ends with it.");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or ArgumentException)
        {
            error.WriteLine($"Couldn't start {request.Program}: {ex.Message}");
            exitCode = 1;
        }
        return true;
    }

    private static BreakawayStart Start(Request request) =>
        Breakaway.Start(Breakaway.CommandLine(request.Program, request.Arguments), request.WorkingDirectory,
            request.NoWindow ? BreakawayConsole.NoWindow : BreakawayConsole.Inherit);
}
