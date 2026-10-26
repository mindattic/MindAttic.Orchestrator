using System.Runtime.InteropServices;

namespace TermHostStub;

/// <summary>ConPTY and process P/Invoke, in the shapes EasyWindowsTerminalControl's TermPTY and ProcessFactory use.</summary>
internal static class Native
{
    public const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000, DETACHED_PROCESS = 0x00000008,
        CREATE_NEW_PROCESS_GROUP = 0x00000200, CREATE_BREAKAWAY_FROM_JOB = 0x01000000, CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    public static readonly IntPtr PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = (IntPtr)0x00020016;

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

    /// <summary>Windows' argv quoting: what CommandLineToArgvW reads back as <paramref name="arg"/>.</summary>
    public static string Quote(string arg)
    {
        if (arg.Length > 0 && arg.IndexOfAny([' ', '\t', '"']) < 0) return arg;
        var sb = new System.Text.StringBuilder("\"");
        for (var i = 0; i < arg.Length; i++)
        {
            var slashes = 0;
            while (i < arg.Length && arg[i] == '\\') { slashes++; i++; }
            if (i == arg.Length) sb.Append('\\', slashes * 2);
            else if (arg[i] == '"') sb.Append('\\', slashes * 2 + 1).Append('"');
            else sb.Append('\\', slashes).Append(arg[i]);
        }
        return sb.Append('"').ToString();
    }

    /// <summary>Starts <paramref name="commandLine"/> detached from this console, in a new process group; retries without
    /// breakaway when the job forbids it. Returns the pid.</summary>
    public static int SpawnDetached(string commandLine, string? cwd)
    {
        foreach (var breakaway in new[] { true, false })
        {
            var si = new STARTUPINFOEX();
            si.StartupInfo.cb = Marshal.SizeOf<STARTUPINFO>();
            var flags = DETACHED_PROCESS | CREATE_NEW_PROCESS_GROUP | (breakaway ? CREATE_BREAKAWAY_FROM_JOB : 0);
            if (CreateProcessW(null, (commandLine + "\0").ToCharArray(), IntPtr.Zero, IntPtr.Zero, false, flags, IntPtr.Zero, cwd, ref si, out var pi))
            {
                CloseHandle(pi.hThread);
                CloseHandle(pi.hProcess);
                return pi.dwProcessId;
            }
            var error = Marshal.GetLastWin32Error();
            if (!(breakaway && error == 5)) throw new System.ComponentModel.Win32Exception(error);
        }
        throw new InvalidOperationException("unreachable");
    }
}
