using System.Diagnostics;
using System.IO;
using System.Windows;
using MindAttic.Orchestrator.Setup;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// The dashboard's window without a tray (M.ORCH-T501): closing it quits, and it's pinned to Start or the taskbar instead.
/// A later launch brings it to the front (<see cref="SingleInstance"/>, M.ORCH-T497), and a copy an older build left
/// hidden in the tray is asked to exit, so none pile up out of sight under the taskbar.
/// </summary>
public static class DashboardWindow
{
    /// <summary>Asks every other dashboard of <paramref name="exe"/> to exit the way an upgrade does
    /// (<see cref="Installer.DashboardExitEvent"/>). Called once this one holds the <see cref="SingleInstance"/> mutex, so any
    /// other is from a build before it, possibly hidden in the tray with no window to close. Only dashboards wait on that
    /// event, so the daemon is left alone. Returns how many were asked.</summary>
    public static int RetireHiddenCopies(string exe)
    {
        var asked = 0;
        foreach (var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exe)))
        {
            try
            {
                if (p.Id == Environment.ProcessId || p.MainModule?.FileName is not { } path
                    || !string.Equals(Path.GetFullPath(path), Path.GetFullPath(exe), StringComparison.OrdinalIgnoreCase)) continue;
                if (!EventWaitHandle.TryOpenExisting(Installer.DashboardExitEvent(p.Id), out var exit)) continue;
                using (exit) exit.Set();
                asked++;
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or UnauthorizedAccessException) { }
            finally { p.Dispose(); }
        }
        return asked;
    }

    /// <summary>The state each window had before it was minimized: brought back, a maximized one comes back maximized (M.ORCH-T336).</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Window, System.Runtime.CompilerServices.StrongBox<WindowState>> before = [];

    private static System.Runtime.CompilerServices.StrongBox<WindowState> Unminimized(Window window) =>
        before.GetValue(window, w =>
        {
            var box = new System.Runtime.CompilerServices.StrongBox<WindowState>(w.WindowState == WindowState.Maximized ? WindowState.Maximized : WindowState.Normal);
            w.StateChanged += (_, _) => { if (w.WindowState != WindowState.Minimized) box.Value = w.WindowState; };
            return box;
        });

    /// <summary>Remembers <paramref name="window"/>'s state from here on, so <see cref="Restore"/> knows it.</summary>
    public static void Track(Window window) => Unminimized(window);

    /// <summary>Shows <paramref name="window"/>: visible, not minimized, in front. Activate alone can lose to Windows'
    /// foreground lock and only flash the taskbar button when it's behind other windows, so it's raised above them for a
    /// moment first (M.ORCH-T185); it doesn't stay on top.</summary>
    public static void Restore(Window window) => Restore(window, w => w.Activate(), w => w.Focus());

    /// <summary><see cref="Restore(Window)"/> with the focus-taking calls passed in, so tests can check them on an
    /// off-screen window without stealing the user's keyboard focus (M.ORCH-T530).</summary>
    internal static void Restore(Window window, Action<Window> activate, Action<Window> focus)
    {
        window.Show();
        if (window.WindowState == WindowState.Minimized) window.WindowState = Unminimized(window).Value;
        var topmost = window.Topmost;
        window.Topmost = true;
        activate(window);
        window.Topmost = topmost;
        focus(window);
    }
}
