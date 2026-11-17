using System.Runtime.InteropServices;
using System.Text;

namespace MindAttic.Orchestrator.TermHost;

/// <summary>ConPTY and process P/Invoke, in the shapes EasyWindowsTerminalControl's TermPTY and ProcessFactory use. The
/// pseudoconsole calls go to the bundled <c>conpty.dll</c> (Microsoft.Windows.Console.ConPTY), not kernel32.</summary>
internal static class Native
{
    public const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000, DETACHED_PROCESS = 0x00000008,
        CREATE_NEW_PROCESS_GROUP = 0x00000200, CREATE_BREAKAWAY_FROM_JOB = 0x01000000, CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    public const uint INFINITE = 0xFFFFFFFF;
    public const int ERROR_ACCESS_DENIED = 5;
    public static readonly IntPtr PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;

    [StructLayout(LayoutKind.Sequential)]
    public struct COORD { public short X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct STARTUPINFO
    {
        public int cb;
        public IntPtr lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct STARTUPINFOEX { public STARTUPINFO StartupInfo; public IntPtr lpAttributeList; }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, IntPtr lpPipeAttributes, int nSize);

    [DllImport("conpty.dll", SetLastError = true)]
    public static extern int CreatePseudoConsole(COORD size, IntPtr hInput, IntPtr hOutput, uint dwFlags, out IntPtr phPC);

    [DllImport("conpty.dll", SetLastError = true)]
    public static extern int ResizePseudoConsole(IntPtr hPC, COORD size);

    [DllImport("conpty.dll", SetLastError = true)]
    public static extern void ClosePseudoConsole(IntPtr hPC);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool UpdateProcThreadAttribute(IntPtr lpAttributeList, uint dwFlags, IntPtr attribute, IntPtr lpValue,
        IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool CreateProcessW(string? lpApplicationName, char[] lpCommandLine, IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string? lpCurrentDirectory,
        ref STARTUPINFOEX lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    /// <summary>Windows' argv quoting: what <c>CommandLineToArgvW</c> reads back as <paramref name="arg"/> (PaneRequest's).</summary>
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

    private static readonly char[] CmdSpecial = [' ', '\t', '\v', '&', '|', '<', '>', '^', '(', ')', '%', '!', ',', ';', '='];

    /// <summary>
    /// <see cref="Quote"/> for an argument that cmd.exe parses first, on <c>cmd.exe /d /c</c> and again in a script's <c>%*</c>
    /// (M.ORCH-T996). An argument with white space or a cmd metacharacter (<c>&amp; | &lt; &gt; ^ ( ) % !</c>, and the
    /// delimiters <c>, ; =</c>) is always quoted, so both parses take it as literal text. A <c>%</c> can't be made literal inside
    /// quotes, so it goes outside them, caret-escaped (<c>"a"^%"b"</c>): no variable is expanded. A quote or line break can't
    /// be passed through cmd safely at all, so an argument holding one is refused.
    /// </summary>
    public static string CmdQuote(string arg)
    {
        if (arg.IndexOfAny(['"', '\r', '\n', '\0']) >= 0)
            throw new ArgumentException($"An argument run through cmd.exe can't hold a quote or a line break: {arg}", nameof(arg));
        if (arg.Length > 0 && arg.IndexOfAny(CmdSpecial) < 0) return arg;
        var quoted = new StringBuilder("\"");
        var slashes = 0;
        foreach (var c in arg)
        {
            if (c == '\\') { slashes++; continue; }
            // Backslashes before a closing quote are doubled, as before the final one.
            if (c == '%') quoted.Append('\\', slashes * 2).Append("\"^%\"");
            else quoted.Append('\\', slashes).Append(c);
            slashes = 0;
        }
        return quoted.Append('\\', slashes * 2).Append('"').ToString();
    }

    /// <summary>Starts <paramref name="commandLine"/> detached: no console, its own process group, and outside the caller's job
    /// (<c>CREATE_BREAKAWAY_FROM_JOB</c>), retried without breakaway when the job forbids it. Returns the pid.</summary>
    public static int SpawnDetached(string commandLine, string? cwd)
    {
        foreach (var breakaway in new[] { true, false })
        {
            var si = new STARTUPINFOEX();
            si.StartupInfo.cb = Marshal.SizeOf<STARTUPINFO>();
            var flags = DETACHED_PROCESS | CREATE_NEW_PROCESS_GROUP | CREATE_UNICODE_ENVIRONMENT | (breakaway ? CREATE_BREAKAWAY_FROM_JOB : 0);
            if (CreateProcessW(null, (commandLine + "\0").ToCharArray(), IntPtr.Zero, IntPtr.Zero, false, flags, IntPtr.Zero, cwd, ref si, out var pi))
            {
                CloseHandle(pi.hThread);
                CloseHandle(pi.hProcess);
                return pi.dwProcessId;
            }
            var error = Marshal.GetLastWin32Error();
            if (!(breakaway && error == ERROR_ACCESS_DENIED)) throw new System.ComponentModel.Win32Exception(error, "Starting the detached host failed");
        }
        throw new InvalidOperationException("unreachable");
    }
}
