using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// The terminal control keeps a 10px scroll bar column ("scrollbar") to the right of its renderer that the dashboard never shows
/// anything in, so the CLI's black area ended 10px short of the CLI tab bar above it (M.ORCH-T1325). It is collapsed: the wheel over
/// the CLI still scrolls its scrollback (TerminalMouse), which never went through that bar.
/// </summary>
internal static class TerminalScrollbar
{
    /// <summary>Collapses <paramref name="terminal"/>'s own scroll bar once it's built, and again if its template is rebuilt.</summary>
    public static void Hide(FrameworkElement terminal)
    {
        if (!Collapse(terminal)) terminal.Loaded += (_, _) => Collapse(terminal);
        // LayoutUpdated fires for every layout pass of the whole window, for every pane (M.ORCH-T1735): the bars found are kept,
        // so a pass only looks at them, and the tree is walked again only once one has left it (a rebuilt template).
        List<ScrollBar>? bars = null;
        terminal.LayoutUpdated += (_, _) =>
        {
            if (bars is { Count: > 0 } && bars.TrueForAll(b => VisualTreeHelper.GetParent(b) is not null))
            {
                foreach (var bar in bars)
                    if (bar.Visibility != Visibility.Collapsed) bar.Visibility = Visibility.Collapsed;
                return;
            }
            bars = [.. Descendants(terminal).OfType<ScrollBar>().Where(Ours)];
            foreach (var bar in bars) bar.Visibility = Visibility.Collapsed;
        };
    }

    private static bool Ours(ScrollBar bar) => bar is { Name: "scrollbar", Orientation: System.Windows.Controls.Orientation.Vertical };

    /// <summary>Collapses every vertical ScrollBar named "scrollbar" under <paramref name="root"/>; true when one was found.</summary>
    internal static bool Collapse(DependencyObject root)
    {
        var found = false;
        foreach (var bar in Descendants(root))
        {
            if (bar is ScrollBar { Name: "scrollbar", Orientation: System.Windows.Controls.Orientation.Vertical } scrollBar)
            {
                found = true;
                if (scrollBar.Visibility != Visibility.Collapsed) scrollBar.Visibility = Visibility.Collapsed;
            }
        }
        return found;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var deeper in Descendants(child)) yield return deeper;
        }
    }
}
