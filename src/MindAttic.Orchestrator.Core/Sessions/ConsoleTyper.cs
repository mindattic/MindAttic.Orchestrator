using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// Types into the console of an agent that isn't in a Launcher tab: attaches to the process's console,
/// writes key events into its input buffer (as the Launcher's ConsoleInputInjector does inside its own
/// tab), and detaches. Only a process without a console of its own can attach, so this is for the
/// daemon (a GUI-subsystem exe); attaching is process-wide, so calls are serialized.
/// </summary>
public static partial class ConsoleTyper
{
    private static readonly Lock Gate = new();

    /// <summary>Types <paramref name="text"/> and presses Enter in <paramref name="pid"/>'s console. False when
    /// the process is gone, isn't the Claude CLI, or has no console to type into (a desktop-app session).</summary>
    public static bool Submit(int pid, string text)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            if (!p.ProcessName.Equals("claude", StringComparison.OrdinalIgnoreCase)) return false;
        }
        catch (ArgumentException) { return false; }

        lock (Gate)
        {
            if (!AttachConsole((uint)pid)) return false;
            try
            {
                var input = CreateFileW("CONIN$", 0xC0000000 /* read | write */, 3 /* share read | write */, IntPtr.Zero, 3 /* open existing */, 0, IntPtr.Zero);
                if (input == new IntPtr(-1)) return false;
                try
                {
                    var records = Records(text.TrimEnd('\r', '\n') + "\n");
                    return WriteConsoleInputW(input, records, (uint)records.Length, out var written) && written == records.Length;
                }
                finally { CloseHandle(input); }
            }
            finally { FreeConsole(); }
        }
    }

    /// <summary>Whether <paramref name="pid"/> is a live Claude CLI with a console this process can attach to.</summary>
    public static bool CanType(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            if (!p.ProcessName.Equals("claude", StringComparison.OrdinalIgnoreCase)) return false;
        }
        catch (ArgumentException) { return false; }
        lock (Gate)
        {
            if (!AttachConsole((uint)pid)) return false;
            FreeConsole();
            return true;
        }
    }

    internal static INPUT_RECORD[] Records(string text)
    {
        var records = new List<INPUT_RECORD>(text.Length * 2);
        foreach (var ch in text)
        {
            if (ch == '\r') continue;
            var (vkey, c) = ch == '\n' ? ((ushort)0x0D, '\r') : ((ushort)0, ch);
            records.Add(Key(true, vkey, c));
            records.Add(Key(false, vkey, c));
        }
        return [.. records];
    }

    private static INPUT_RECORD Key(bool down, ushort vkey, char ch) => new()
    {
        EventType = 0x0001,
        KeyEvent = new KEY_EVENT_RECORD { bKeyDown = down ? 1 : 0, wRepeatCount = 1, wVirtualKeyCode = vkey, UnicodeChar = ch },
    };

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEY_EVENT_RECORD
    {
        public int bKeyDown;
        public ushort wRepeatCount;
        public ushort wVirtualKeyCode;
        public ushort wVirtualScanCode;
        public char UnicodeChar;
        public uint dwControlKeyState;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct INPUT_RECORD
    {
        [FieldOffset(0)] public ushort EventType;
        [FieldOffset(4)] public KEY_EVENT_RECORD KeyEvent;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(uint processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool FreeConsole();

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "WriteConsoleInputW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteConsoleInputW(IntPtr input, [In] INPUT_RECORD[] buffer, uint length, out uint written);
}
