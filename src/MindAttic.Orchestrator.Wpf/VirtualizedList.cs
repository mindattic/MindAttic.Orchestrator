using System.Windows;
using System.Windows.Controls;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// Makes a library-styled list (an <see cref="ItemsControl"/> in the library's MaList / MaRowList) scroll its own items and build
/// only the ones on screen (M.ORCH-T655, M.ORCH-T756): a plain ItemsControl in a scroll view builds every row however long the
/// list is, and builds them all again whenever the window is handed another tab's or org's collection. Here the scroll view is the
/// list's own (the library's MaBodyScroll, so it keeps the theme's scroll bars and the body's gutters), its panel a recycling
/// <see cref="VirtualizingStackPanel"/> scrolled by pixel, so a 761-ticket queue costs a screenful.
/// MindAttic.Components.Wpf has no virtualizing list yet, so this is done in code on the XAML's library-styled list rather than by a
/// control of the dashboard's own in the XAML.
/// </summary>
public static class VirtualizedList
{
    /// <summary>Gives <paramref name="list"/> its own scroll view and a virtualizing, recycling panel at most
    /// <paramref name="maxWidth"/> wide.</summary>
    public static void Apply(ItemsControl list, double maxWidth)
    {
        var scroll = new FrameworkElementFactory(typeof(ScrollViewer));
        scroll.SetResourceReference(FrameworkElement.StyleProperty, "MaBodyScroll");
        scroll.SetValue(ScrollViewer.CanContentScrollProperty, true);
        scroll.SetValue(UIElement.FocusableProperty, false);
        scroll.AppendChild(new FrameworkElementFactory(typeof(ItemsPresenter)));
        // Set on the list itself: as a style's setter, the theme's ItemsControl style would replace it with one that has no scroll
        // view, and every row was built on each org switch.
        list.Template = new ControlTemplate(typeof(ItemsControl)) { VisualTree = scroll };

        var panel = new FrameworkElementFactory(typeof(VirtualizingStackPanel));
        panel.SetValue(FrameworkElement.MaxWidthProperty, maxWidth);
        panel.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        list.ItemsPanel = new ItemsPanelTemplate(panel);

        list.Focusable = false;
        VirtualizingPanel.SetIsVirtualizing(list, true);
        VirtualizingPanel.SetVirtualizationMode(list, VirtualizationMode.Recycling);
        VirtualizingPanel.SetScrollUnit(list, ScrollUnit.Pixel);
        ScrollViewer.SetCanContentScroll(list, true);
    }
}
