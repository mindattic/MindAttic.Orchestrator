using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// Lets the wheel scroll a CLI's own scrollback while the cursor sits over it, with no click needed first (M.ORCH-T1165).
/// Windows delivers WM_MOUSEWHEEL to whichever window holds Win32 focus, not the one under the cursor (unlike the other mouse
/// messages), so this handler only ever sees a wheel turn that Win32 routed to the dashboard's own window rather than straight
/// to a focused terminal's renderer; that is exactly the case where hovering an unfocused terminal and scrolling went nowhere.
/// Posted on to the renderer's own window underneath the cursor (<see cref="TerminalKeyboard"/>'s same trick for keys), the
/// wheel reaches the terminal regardless of focus. Off a terminal, nothing is touched, so the page's own ScrollViewer
/// (AgentsBody) scrolls as it always did.
/// </summary>
internal static partial class TerminalMouse
{
    private const uint WmMouseWheel = 0x020A;
    private const uint MkControl = 0x0008;

    public static void Attach(Window window, FrameworkElement terminals) =>
        window.PreviewMouseWheel += (_, e) => OnPreviewMouseWheel(terminals, e);

    private static void OnPreviewMouseWheel(FrameworkElement terminals, MouseWheelEventArgs e)
    {
        if (e.Handled || !terminals.IsVisible) return;
        if (RendererUnder(terminals, e.OriginalSource as DependencyObject) is not { } host || host.Handle == IntPtr.Zero) return;
        var cursor = host.PointToScreen(Mouse.GetPosition(host));
        var keys = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ? MkControl : 0;
        var wParam = (nint)(((uint)(ushort)e.Delta << 16) | keys);
        var lParam = (nint)((((int)cursor.Y & 0xFFFF) << 16) | ((int)cursor.X & 0xFFFF));
        if (PostMessage(host.Handle, WmMouseWheel, wParam, lParam)) e.Handled = true;
    }

    /// <summary>The terminal renderer window under <paramref name="source"/>, if it belongs to a pane inside
    /// <paramref name="terminals"/>; null off any terminal.</summary>
    private static HwndHost? RendererUnder(FrameworkElement terminals, DependencyObject? source)
    {
        for (var at = source; at is not null; at = VisualTreeHelper.GetParent(at))
        {
            if (at is HwndHost host && ViewModels.TerminalPaneViewModel.Of(host) is not null) return host;
            if (ReferenceEquals(at, terminals)) break;
        }
        return null;
    }

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessage(IntPtr hwnd, uint msg, nint wParam, nint lParam);
}
