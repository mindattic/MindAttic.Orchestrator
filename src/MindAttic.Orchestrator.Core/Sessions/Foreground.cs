using System.Runtime.InteropServices;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// Keeps unattended work from taking the screen: whether you're in a game or other fullscreen app
/// (Windows' own "busy / fullscreen Direct3D / presentation" state, or a borderless window covering its
/// whole monitor), and giving focus back to whatever had it after a tab opens.
/// </summary>
public static partial class Foreground
{
    /// <summary>Why now is a bad time to open a window, or null when it's fine.</summary>
    public static string? Busy()
    {
        if (SHQueryUserNotificationState(out var state) == 0 && state is 2 or 3 or 4)
            return state == 3 ? "a fullscreen game is running" : state == 4 ? "presentation mode is on" : "a fullscreen app is in front";
        return CoversItsMonitor(GetForegroundWindow()) ? "a fullscreen window is in front" : null;
    }

    /// <summary>The window in front now, to hand focus back to after opening one.</summary>
    public static IntPtr Current() => GetForegroundWindow();

    /// <summary>Gives focus back to <paramref name="window"/> once a new window has had a moment to take it.</summary>
    public static async Task RestoreAsync(IntPtr window, TimeSpan after)
    {
        if (window == IntPtr.Zero) return;
        await Task.Delay(after).ConfigureAwait(false);
        if (GetForegroundWindow() != window) SetForegroundWindow(window);
    }

    private static bool CoversItsMonitor(IntPtr window)
    {
        if (window == IntPtr.Zero || window == GetShellWindow() || window == GetDesktopWindow()) return false;
        if (!GetWindowRect(window, out var w)) return false;
        var monitor = MonitorFromWindow(window, 2 /* nearest */);
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfoW(monitor, ref info)) return false;
        var m = info.rcMonitor;
        return w.Left <= m.Left && w.Top <= m.Top && w.Right >= m.Right && w.Bottom >= m.Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }

    [LibraryImport("shell32.dll")]
    private static partial int SHQueryUserNotificationState(out int state);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr window);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetShellWindow();

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetDesktopWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(IntPtr window, out RECT rect);

    [LibraryImport("user32.dll")]
    private static partial IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfoW(IntPtr monitor, ref MONITORINFO info);
}
