using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// The wheel goes to whatever is under the cursor (M.ORCH-T1165): over a CLI terminal it scrolls that terminal's own scrollback,
/// with no click needed first and without moving keyboard focus; off a terminal it scrolls the WPF page (AgentsBody) as always,
/// even while a terminal holds Win32 focus.
/// <para>Windows delivers WM_MOUSEWHEEL to whichever window holds Win32 focus (or, with "scroll inactive windows", the one under
/// the cursor), not reliably the one under it. A terminal is a native child window (an <see cref="HwndHost"/>), and once the
/// cursor is over it WPF stops tracking the mouse, so a window-level PreviewMouseWheel (52de43e8's approach) never saw a wheel
/// turned over an unfocused terminal, and a wheel turned over the page while a terminal held focus scrolled that terminal.</para>
/// <para>So the wheel is routed at the message pump instead, before anything dispatches it: <see cref="ComponentDispatcher"/>'s
/// thread filter sees every message the UI thread pumps, the terminals' renderer windows' included (they live on it). Only
/// WM_MOUSEWHEEL is looked at; it is reposted to the terminal under the cursor when it was headed elsewhere, or to the dashboard's
/// own window when a terminal got it but the cursor is over the page. Nothing polls, and keyboard focus is never touched.</para>
/// </summary>
internal static partial class TerminalMouse
{
    private const int WmMouseWheel = 0x020A;

    public static void Attach(Window window, FrameworkElement terminals)
    {
        ThreadMessageEventHandler filter = (ref MSG msg, ref bool handled) =>
        {
            if (handled || msg.message != WmMouseWheel) return;
            try { handled = Route(window, terminals, msg); }
            catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Routing the wheel failed; contained"); }
        };
        ComponentDispatcher.ThreadFilterMessage += filter;
        window.Closed += (_, _) => ComponentDispatcher.ThreadFilterMessage -= filter;
    }

    /// <summary>Reposts a wheel message headed for the wrong window to the right one; true when it did (the original is then
    /// dropped). The right one is the terminal under the cursor, or else the dashboard's window when the cursor is over it.</summary>
    private static bool Route(Window window, FrameworkElement terminals, MSG msg)
    {
        if (!terminals.IsVisible) return false;
        var main = new WindowInteropHelper(window).Handle;
        if (main == IntPtr.Zero) return false;
        // lParam is the cursor in screen coordinates, as two signed 16-bit halves.
        var lParam = (long)msg.lParam;
        var under = WindowFromPoint(new Point32 { X = (short)(lParam & 0xFFFF), Y = (short)((lParam >> 16) & 0xFFFF) });
        if (under == IntPtr.Zero || (under != main && !IsChild(main, under))) return false;

        var hovered = IntPtr.Zero;
        var fromTerminal = false;
        foreach (var host in Renderers(terminals))
        {
            var handle = host.Handle;
            if (handle == IntPtr.Zero) continue;
            if (hovered == IntPtr.Zero && Within(handle, under)) hovered = handle;
            if (Within(handle, msg.hwnd)) fromTerminal = true;
        }

        if (hovered != IntPtr.Zero)
            return !Within(hovered, msg.hwnd) && PostMessage(hovered, WmMouseWheel, msg.wParam, msg.lParam);
        // Off every terminal: a wheel a terminal got by focus goes back to the page; anything else is left as it was.
        return fromTerminal && PostMessage(main, WmMouseWheel, msg.wParam, msg.lParam);
    }

    private static bool Within(IntPtr root, IntPtr window) => window == root || (window != IntPtr.Zero && IsChild(root, window));

    /// <summary>The terminal renderers' windows under <paramref name="parent"/> that are shown.</summary>
    private static IEnumerable<HwndHost> Renderers(DependencyObject parent)
    {
        if (parent is HwndHost host) { if (host.IsVisible) yield return host; yield break; }
        if (parent is UIElement { IsVisible: false }) yield break;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            foreach (var inner in Renderers(VisualTreeHelper.GetChild(parent, i))) yield return inner;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point32
    {
        public int X;
        public int Y;
    }

    [LibraryImport("user32.dll")]
    private static partial IntPtr WindowFromPoint(Point32 point);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsChild(IntPtr parent, IntPtr window);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessage(IntPtr hwnd, uint msg, nint wParam, nint lParam);
}
