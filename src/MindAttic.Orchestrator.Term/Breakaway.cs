using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MindAttic.Orchestrator.Term;

/// <summary>The console a process started by <see cref="Breakaway"/> gets.</summary>
public enum BreakawayConsole
{
    /// <summary>The caller's console when it has one, else a new one for a console program (as <c>Process.Start</c>).</summary>
    Inherit,

    /// <summary>A console program gets a console with no window (<c>CREATE_NO_WINDOW</c>, as <c>CreateNoWindow</c>).</summary>
    NoWindow,

    /// <summary>No console at all and its own process group (<c>DETACHED_PROCESS | CREATE_NEW_PROCESS_GROUP</c>): a detached TermHost.</summary>
    Detached,
}

/// <summary>What <see cref="Breakaway.Start(string, string?, BreakawayConsole, IEnumerable{KeyValuePair{string, string?}}?)"/> started:
/// its pid, and whether it is outside the caller's jobs (false: a job forbade breakaway, so it was started inside, as before).</summary>
public readonly record struct BreakawayStart(int Pid, bool BrokeAway);

/// <summary>
/// The one way the orchestrator starts a process that is meant to outlive its caller (M.ORCH-T1691): the dashboard, the daemon,
/// the gateway, a detached TermHost, a TermViewer, the Launcher's terminal, Tickets Desktop. A hosted CLI runs inside a
/// kill-on-close <see cref="JobObject"/> (M.ORCH-T1674), so whatever it starts the ordinary way (<c>Process.Start</c>, PowerShell's
/// <c>Start-Process</c>) ends when that CLI's host does. These are started with <c>CREATE_BREAKAWAY_FROM_JOB</c> instead, which
/// that job allows, so a deploy run from a CLI leaves a dashboard and a daemon that stay up.
/// <para>When a job the caller is in forbids breakaway (Windows answers access denied), the process is started the ordinary
/// way instead, inside the job: better up and owned than not started. Outside any job the flag does nothing.</para>
/// Handles are not inherited, so the started process holds none of the caller's pipes (a script capturing the caller's output
/// doesn't wait on it). Builds, tests and a CLI's own tool children never come through here: they stay contained.
/// </summary>
public static class Breakaway
{
    internal const uint DETACHED_PROCESS = 0x00000008, CREATE_NEW_PROCESS_GROUP = 0x00000200, CREATE_UNICODE_ENVIRONMENT = 0x00000400,
        CREATE_BREAKAWAY_FROM_JOB = 0x01000000, CREATE_NO_WINDOW = 0x08000000;
    internal const int ERROR_ACCESS_DENIED = 5;

    /// <summary>One <c>CreateProcess</c> attempt with these creation flags: the pid, or the Win32 error it failed with.</summary>
    internal delegate (int Pid, int Error) Creator(uint flags);

    /// <summary>Starts <paramref name="commandLine"/> outside the caller's jobs, in <paramref name="workingDirectory"/> (null: the
    /// caller's), with <paramref name="environment"/> as its whole environment (null: the caller's). Throws
    /// <see cref="Win32Exception"/> when it can't be started at all.</summary>
    public static BreakawayStart Start(string commandLine, string? workingDirectory = null, BreakawayConsole console = BreakawayConsole.Inherit,
        IEnumerable<KeyValuePair<string, string?>>? environment = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandLine);
        var block = environment is null ? null : EnvironmentBlock(environment);
        var directory = string.IsNullOrEmpty(workingDirectory) ? null : workingDirectory;
        return Start(console, flags =>
        {
            var si = new STARTUPINFO { cb = Marshal.SizeOf<STARTUPINFO>() };
            // CreateProcess may write to the command line, so it gets its own buffer each attempt.
            if (!CreateProcessW(null, (commandLine + "\0").ToCharArray(), IntPtr.Zero, IntPtr.Zero, false, flags, block, directory, ref si, out var pi))
                return (0, Marshal.GetLastWin32Error());
            CloseHandle(pi.hThread);
            CloseHandle(pi.hProcess);
            return (pi.dwProcessId, 0);
        });
    }

    /// <summary>Starts what <paramref name="psi"/> describes outside the caller's jobs: its file and arguments, working directory,
    /// environment and <c>CreateNoWindow</c>. For a plain start only: a shell start (<c>UseShellExecute</c>, a verb), another
    /// user's or redirected streams throw <see cref="NotSupportedException"/>, since <c>CreateProcess</c> can't break those away
    /// the same way and a caller reading a process's output is waiting on it, not letting it outlive anything.</summary>
    public static BreakawayStart Start(ProcessStartInfo psi)
    {
        ArgumentNullException.ThrowIfNull(psi);
        if (psi.UseShellExecute || !string.IsNullOrEmpty(psi.Verb) || !string.IsNullOrEmpty(psi.UserName))
            throw new NotSupportedException("A breakaway start is a plain CreateProcess: no shell start, verb or other user.");
        if (psi.RedirectStandardInput || psi.RedirectStandardOutput || psi.RedirectStandardError)
            throw new NotSupportedException("A breakaway start can't redirect the process's streams.");
        return Start(CommandLine(psi), psi.WorkingDirectory, psi.CreateNoWindow ? BreakawayConsole.NoWindow : BreakawayConsole.Inherit, psi.Environment);
    }

    /// <summary>The breakaway attempt, then (only when a job refused it) the ordinary one; any other failure throws.</summary>
    internal static BreakawayStart Start(BreakawayConsole console, Creator create)
    {
        var (pid, error) = create(CreationFlags(console, breakaway: true));
        if (error == 0) return new(pid, BrokeAway: true);
        if (error != ERROR_ACCESS_DENIED) throw new Win32Exception(error);
        (pid, error) = create(CreationFlags(console, breakaway: false));
        if (error == 0) return new(pid, BrokeAway: false);
        throw new Win32Exception(error);
    }

    /// <summary>The <c>CreateProcess</c> creation flags for <paramref name="console"/>, with or without breakaway.</summary>
    internal static uint CreationFlags(BreakawayConsole console, bool breakaway) =>
        CREATE_UNICODE_ENVIRONMENT
        | (breakaway ? CREATE_BREAKAWAY_FROM_JOB : 0)
        | console switch
        {
            BreakawayConsole.NoWindow => CREATE_NO_WINDOW,
            BreakawayConsole.Detached => DETACHED_PROCESS | CREATE_NEW_PROCESS_GROUP,
            _ => 0u,
        };

    /// <summary>The command line <c>Process.Start</c> would build for <paramref name="psi"/>: the file quoted, then its
    /// <c>ArgumentList</c> each quoted, or its <c>Arguments</c> as written.</summary>
    internal static string CommandLine(ProcessStartInfo psi)
    {
        if (string.IsNullOrWhiteSpace(psi.FileName)) throw new InvalidOperationException("No file to start.");
        if (psi.ArgumentList.Count > 0) return CommandLine(psi.FileName, psi.ArgumentList);
        // Always in quotes, as Process.Start writes it: a path with a space is one word, and a bare name is still searched for.
        var file = psi.FileName.Contains('"') ? psi.FileName : '"' + psi.FileName + '"';
        return string.IsNullOrEmpty(psi.Arguments) ? file : file + " " + psi.Arguments;
    }

    /// <summary><paramref name="file"/> and <paramref name="arguments"/> as one command line, each quoted for <c>CommandLineToArgvW</c>.</summary>
    public static string CommandLine(string file, IEnumerable<string> arguments) =>
        string.Join(' ', new[] { file }.Concat(arguments).Select(Quote));

    /// <summary>Windows' argv quoting: what <c>CommandLineToArgvW</c> reads back as <paramref name="arg"/>.</summary>
    public static string Quote(string arg)
    {
        if (arg.Length > 0 && arg.IndexOfAny([' ', '\t', '\n', '\v', '"']) < 0) return arg;
        var quoted = new StringBuilder("\"");
        var slashes = 0;
        foreach (var c in arg)
        {
            if (c == '\\') { slashes++; continue; }
            // Backslashes before a quote are doubled, and the quote escaped; anywhere else they stand as they are.
            quoted.Append('\\', c == '"' ? slashes * 2 + 1 : slashes).Append(c);
            slashes = 0;
        }
        // Before the closing quote they're doubled too, or the last one would escape it.
        return quoted.Append('\\', slashes * 2).Append('"').ToString();
    }

    /// <summary>A Unicode environment block: <c>NAME=value\0</c> for each variable, sorted by name as Windows expects, then a
    /// closing <c>\0</c>. A null value leaves the variable out; a name given twice (in any case) keeps its last value.</summary>
    internal static char[] EnvironmentBlock(IEnumerable<KeyValuePair<string, string?>> environment)
    {
        var variables = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in environment)
        {
            if (string.IsNullOrEmpty(name)) continue;
            if (value is null) variables.Remove(name);
            else variables[name] = value;
        }
        var block = new StringBuilder();
        foreach (var (name, value) in variables) block.Append(name).Append('=').Append(value).Append('\0');
        // An empty block still ends in two: one for the (missing) last variable, one for the block.
        if (variables.Count == 0) block.Append('\0');
        return block.Append('\0').ToString().ToCharArray();
    }

    /// <summary>The running process <paramref name="pid"/>, for a caller that watches what it started; null when it has already
    /// gone (or can't be opened).</summary>
    public static Process? TryOpen(int pid)
    {
        try { return Process.GetProcessById(pid); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception) { return null; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFO
    {
        public int cb;
        public IntPtr lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessW(string? lpApplicationName, char[] lpCommandLine, IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles, uint dwCreationFlags, char[]? lpEnvironment,
        string? lpCurrentDirectory, ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);
}
