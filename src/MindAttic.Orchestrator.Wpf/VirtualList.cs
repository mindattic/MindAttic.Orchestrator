using System.Windows;
using System.Windows.Controls;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// A list that scrolls its own items and builds only the ones on screen (M.ORCH-T655): a plain <see cref="ItemsControl"/> in a
/// scroll view builds every row however long the list is, and builds them all again whenever the window is handed another
/// tab's or org's collection. Here the scroll view is the list's own, its panel a recycling <see cref="VirtualizingStackPanel"/>
/// scrolled by pixel, so a 761-ticket queue costs a screenful. <see cref="ScrollStyle"/> is the scroll view's style (the
/// window's BodyScroll, so it keeps the theme's scroll bars and the body's gutters).
/// </summary>
public sealed class VirtualList : ItemsControl
{
    public static readonly DependencyProperty ScrollStyleProperty = DependencyProperty.Register(
        nameof(ScrollStyle), typeof(Style), typeof(VirtualList), new FrameworkPropertyMetadata(null));

    public Style? ScrollStyle
    {
        get => (Style?)GetValue(ScrollStyleProperty);
        set => SetValue(ScrollStyleProperty, value);
    }

    /// <summary>The list's own scroll view around its items: set on each list, since the theme's ItemsControl style would
    /// otherwise give it the plain ItemsControl's template, with no scroll view, so every row was built.</summary>
    private static readonly ControlTemplate Scrolling;

    static VirtualList()
    {
        var scroll = new FrameworkElementFactory(typeof(ScrollViewer));
        scroll.SetValue(StyleProperty, new TemplateBindingExtension(ScrollStyleProperty));
        scroll.SetValue(ScrollViewer.CanContentScrollProperty, true);
        scroll.SetValue(FocusableProperty, false);
        scroll.AppendChild(new FrameworkElementFactory(typeof(ItemsPresenter)));
        var template = new ControlTemplate(typeof(VirtualList)) { VisualTree = scroll };
        template.Seal();
        Scrolling = template;
        ItemsPanelProperty.OverrideMetadata(typeof(VirtualList),
            new FrameworkPropertyMetadata(new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel)))));
        FocusableProperty.OverrideMetadata(typeof(VirtualList), new FrameworkPropertyMetadata(false));
    }

    public VirtualList()
    {
        Template = Scrolling;
        VirtualizingPanel.SetIsVirtualizing(this, true);
        VirtualizingPanel.SetVirtualizationMode(this, VirtualizationMode.Recycling);
        VirtualizingPanel.SetScrollUnit(this, ScrollUnit.Pixel);
        ScrollViewer.SetCanContentScroll(this, true);
    }
}
