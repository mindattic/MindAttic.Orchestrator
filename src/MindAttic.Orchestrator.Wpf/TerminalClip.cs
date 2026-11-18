using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// Keeps the Terminals panel's native terminal windows inside the scroll view they sit in (M.ORCH-T810). A terminal is a child
/// window (an <see cref="HwndHost"/>) drawn over WPF, and WPF never clips one to a scroll view: scrolled part way out of the
/// person's page it would paint over the toolbar and the tabs above. After each layout pass (a scroll is one) the one selected
/// terminal window is given a window region of just the part of it inside the scroll view's viewport, none when it's fully
/// inside, and an empty one when it's scrolled out of sight.
/// <para><c>host</c> (<c>TerminalPanesHost</c>) holds every CLI's terminal ever opened, across every login, for the life of
/// the tab (M.ORCH-T810's own reattach design keeps each one parented); only one is ever visible at a time
/// (<c>TerminalPaneViewModel.IsSelected</c>). Walking every one of them on every <c>LayoutUpdated</c> (M.ORCH-T1756: it used to,
/// via a recursive <c>VisualTreeHelper</c> descent of the whole panel) cost scaled with the total number of CLIs ever opened
/// rather than with what's actually on screen; <paramref name="selected"/> (the shown login's one selected pane) is all that
/// still needs a region each pass.</para>
/// </summary>
internal static partial class TerminalClip
{
    /// <summary>The region last given each terminal window (null: none, it's whole), so a layout pass that moved nothing
    /// sets nothing.</summary>
    private static readonly ConditionalWeakTable<HwndHost, StrongBox<Int32Rect?>> Applied = new();

    /// <summary><paramref name="selected"/> is read fresh on every layout pass: the shown login's currently selected pane's
    /// <c>Control</c> (its <see cref="HwndHost"/> found inside it), or null while nothing is selected.</summary>
    public static void Attach(FrameworkElement host, Func<FrameworkElement?> selected) => host.LayoutUpdated += (_, _) => Clip(host, selected);

    private static void Clip(FrameworkElement host, Func<FrameworkElement?> selected)
    {
        if (!host.IsVisible || PresentationSource.FromVisual(host) is null) return;
        if (selected() is not { } shown) return;
        if (Descendants<HwndHost>(shown).FirstOrDefault() is not { } terminal) return;
        if (terminal.Handle == IntPtr.Zero || !terminal.IsVisible || terminal.ActualWidth <= 0 || terminal.ActualHeight <= 0) return;
        if (Viewport(host) is not { } viewport) return;
        var bounds = ScreenRect(terminal, new Rect(terminal.RenderSize));
        Apply(terminal, Region(bounds, viewport));
    }

    /// <summary>The part of a window at <paramref name="bounds"/> (screen pixels) inside <paramref name="viewport"/>, in the
    /// window's own pixels: null when it's wholly inside (no region), an empty rect when none of it is.</summary>
    internal static Int32Rect? Region(Rect bounds, Rect viewport)
    {
        var inside = Rect.Intersect(bounds, viewport);
        if (inside.IsEmpty || inside.Width < 1 || inside.Height < 1) return Int32Rect.Empty;
        if (inside.Left <= bounds.Left + 0.5 && inside.Top <= bounds.Top + 0.5 && inside.Right >= bounds.Right - 0.5 && inside.Bottom >= bounds.Bottom - 0.5)
            return null;
        return new Int32Rect((int)Math.Round(inside.X - bounds.X), (int)Math.Round(inside.Y - bounds.Y),
            (int)Math.Round(inside.Width), (int)Math.Round(inside.Height));
    }

    private static void Apply(HwndHost terminal, Int32Rect? region)
    {
        var last = Applied.GetValue(terminal, _ => new StrongBox<Int32Rect?>(null));
        if (Nullable.Equals(last.Value, region)) return;
        var handle = IntPtr.Zero;
        if (region is { } r)
        {
            handle = r.IsEmpty ? CreateRectRgn(0, 0, 0, 0) : CreateRectRgn(r.X, r.Y, r.X + r.Width, r.Y + r.Height);
            // CreateRectRgn failing (GDI handle exhaustion) must not fall through to SetWindowRgn with a null handle: that
            // call succeeds too, but it means "no region" (fully unclipped), the opposite of the wanted clip, and the code
            // below would then record it as applied and never retry. Leave the window's actual clip and `last` alone instead,
            // so the next layout pass tries again.
            if (handle == IntPtr.Zero) return;
        }
        // On success the window owns the region; on failure it's still ours to free.
        if (SetWindowRgn(terminal.Handle, handle, true) == 0 && handle != IntPtr.Zero) DeleteObject(handle);
        else last.Value = region;
    }

    /// <summary>The visible content area of the scroll view <paramref name="host"/> scrolls in, in screen pixels.</summary>
    private static Rect? Viewport(FrameworkElement host)
    {
        DependencyObject? at = host;
        while (at is not null and not ScrollViewer) at = VisualTreeHelper.GetParent(at);
        if (at is not ScrollViewer scroll) return null;
        var content = scroll.Template?.FindName("PART_ScrollContentPresenter", scroll) as FrameworkElement ?? scroll;
        return ScreenRect(content, new Rect(content.RenderSize));
    }

    private static Rect ScreenRect(Visual visual, Rect local) =>
        new(visual.PointToScreen(local.TopLeft), visual.PointToScreen(local.BottomRight));

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found) { yield return found; continue; }
            foreach (var deeper in Descendants<T>(child)) yield return deeper;
        }
    }

    [LibraryImport("gdi32.dll")]
    private static partial IntPtr CreateRectRgn(int left, int top, int right, int bottom);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(IntPtr handle);

    [LibraryImport("user32.dll")]
    private static partial int SetWindowRgn(IntPtr window, IntPtr region, [MarshalAs(UnmanagedType.Bool)] bool redraw);
}
