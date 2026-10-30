using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using MindAttic.Components.Wpf.Controls;

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
    public static void Apply(ItemsControl list, double maxWidth) =>
        Apply(list, maxWidth, HorizontalAlignment.Left, typeof(VirtualizingStackPanel), VirtualizationMode.Recycling);

    internal static void Apply(ItemsControl list, double maxWidth, HorizontalAlignment alignment, Type panelType, VirtualizationMode mode)
    {
        var scroll = new FrameworkElementFactory(typeof(ScrollViewer));
        scroll.SetResourceReference(FrameworkElement.StyleProperty, "MaBodyScroll");
        scroll.SetValue(ScrollViewer.CanContentScrollProperty, true);
        scroll.SetValue(UIElement.FocusableProperty, false);
        scroll.AppendChild(new FrameworkElementFactory(typeof(ItemsPresenter)));
        // Set on the list itself: as a style's setter, the theme's ItemsControl style would replace it with one that has no scroll
        // view, and every row was built on each org switch.
        list.Template = new ControlTemplate(typeof(ItemsControl)) { VisualTree = scroll };

        var panel = new FrameworkElementFactory(panelType);
        panel.SetValue(FrameworkElement.MaxWidthProperty, maxWidth);
        panel.SetValue(FrameworkElement.HorizontalAlignmentProperty, alignment);
        list.ItemsPanel = new ItemsPanelTemplate(panel);

        list.Focusable = false;
        VirtualizingPanel.SetIsVirtualizing(list, true);
        VirtualizingPanel.SetVirtualizationMode(list, mode);
        VirtualizingPanel.SetScrollUnit(list, ScrollUnit.Pixel);
        ScrollViewer.SetCanContentScroll(list, true);
    }

    /// <summary>Virtualizes a whole body view (M.ORCH-T754): see <see cref="VirtualizedBody"/>.</summary>
    public static VirtualizedBody ApplyBody(ScrollViewer body) =>
        new(body, body.Content as Panel ?? throw new InvalidOperationException($"{body.Name}'s content is not a panel"));

    /// <summary>Virtualizes <paramref name="content"/>, a panel nested inside <paramref name="body"/> rather than its direct
    /// content (M.ORCH-T1302): what comes before it in <paramref name="body"/>'s own content (e.g. the live terminal, a native
    /// HwndHost) stays a plain, always-realized sibling, immune to the virtualizing panel's own realize/derealize cycle, while
    /// still scrolling with everything else in the same view.</summary>
    public static VirtualizedBody ApplyBody(ScrollViewer body, Panel content) => new(body, content);
}

/// <summary>
/// A body view (an MaBodyScroll whose content is a vertical panel of headings, notes and library-styled lists) made one virtualized
/// list (M.ORCH-T754), so the Agents, Accounts, Repos and Audit views, and each person's Tickets view, build only what's on screen:
/// a 9-person and a 2-person org switch equally fast. The panel's children become the list's items in their order: every element
/// that isn't a list stays as it is (one item each, its bindings and visibility its own), and every library list (MaList, MaRowList,
/// MaTileList) is replaced by its items, one row each, or for a tile list with equal columns (<see cref="MaList.MinItemWidthProperty"/>)
/// one row per line of tiles, as many as the library's <see cref="MaTileGrid"/> fits the width, each row an MaTileGrid of its own, so
/// the tiles keep their columns, gaps and edges. The list in the XAML stays the source of its items, template, style and spacing; it is
/// just never on screen itself. A tile list that flows at its tiles' own widths (no MinItemWidth) is one row: not virtualized.
/// The body keeps its place, name and visibility, but scrolls no more: the list inside it does, in the same MaBodyScroll gutters.
/// MindAttic.Components.Wpf has no virtualizing list yet, so this is done in code on the XAML's library-styled lists.
/// </summary>
public sealed class VirtualizedBody
{
    private static readonly ConditionalWeakTable<ItemsControl, VirtualizedBody> Bodies = new();
    private static readonly DataTemplate RowTemplate = MakeRowTemplate();

    private readonly ItemsControl host = new();
    private readonly List<object> parts = [];
    private readonly HashSet<INotifyCollectionChanged> watched = [];
    private double width = double.NaN;
    private bool pending;

    internal VirtualizedBody(ScrollViewer body, Panel content)
    {
        var isBodysOwnContent = ReferenceEquals(content, body.Content);
        var children = content.Children.Cast<UIElement>().ToList();
        content.Children.Clear();
        if (isBodysOwnContent) body.Content = null;
        foreach (var child in children)
        {
            if (child is ItemsControl list && list.GetType() == typeof(ItemsControl)) parts.Add(Watch(new Section(list)));
            else parts.Add(child);
        }

        // The body no longer scrolls, nor pads: the list inside it does both, with the same MaBodyScroll.
        body.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        MaScrollArea.SetGutter(body, new Thickness(0));
        VirtualizedList.Apply(host, content.MaxWidth, content.HorizontalAlignment, typeof(BodyPanel), VirtualizationMode.Standard);
        host.ItemTemplate = RowTemplate;
        host.Margin = content.Margin;
        AutomationProperties.SetName(host, AutomationProperties.GetName(body));
        Bodies.Add(host, this);
        if (isBodysOwnContent) body.Content = host;
        else
        {
            // content (now emptied) sits inside some outer, never-virtualized wrapper (M.ORCH-T1302): host takes its place there.
            var outer = (Panel)content.Parent!;
            var index = outer.Children.IndexOf(content);
            outer.Children.RemoveAt(index);
            outer.Children.Insert(index, host);
        }
        Schedule();
    }

    /// <summary>The items of <paramref name="groups"/> each lead their own tiles, laid out as <paramref name="tiles"/> is (its
    /// template, style, columns and gaps) right under it: <paramref name="tilesOf"/> gives a group's tiles, or null while the group
    /// is hidden. <paramref name="tiles"/> is only that pattern: it lists nothing itself.</summary>
    public VirtualizedBody Nest(ItemsControl groups, ItemsControl tiles, Func<object, IEnumerable?> tilesOf)
    {
        var section = parts.OfType<Section>().Single(s => s.List == groups);
        section.Tiles = parts.OfType<Section>().Single(s => s.List == tiles);
        parts.Remove(section.Tiles);
        section.TilesOf = tilesOf;
        Schedule();
        return this;
    }

    private Section Watch(Section section)
    {
        // Out of the window's tree, the list reads its items from the body's data context, as it did in the XAML.
        section.List.SetBinding(FrameworkElement.DataContextProperty, new Binding(nameof(FrameworkElement.DataContext)) { Source = host });
        ((INotifyCollectionChanged)section.List.Items).CollectionChanged += (_, _) => Schedule();
        DependencyPropertyDescriptor.FromProperty(UIElement.VisibilityProperty, typeof(ItemsControl)).AddValueChanged(section.List, (_, _) => Schedule());
        return section;
    }

    private void Schedule()
    {
        if (pending) return;
        pending = true;
        host.Dispatcher.BeginInvoke(DispatcherPriority.DataBind, Rebuild);
    }

    private void OnWidth(double next)
    {
        if (next == width) return;
        width = next;
        Schedule();
    }

    private void Rebuild()
    {
        pending = false;
        var rows = new List<object>();
        var nested = new List<INotifyCollectionChanged>();
        foreach (var part in parts)
        {
            if (part is not Section section) { rows.Add(part); continue; }
            if (section.List.Visibility != Visibility.Visible) continue;
            var items = section.List.Items.Cast<object>().ToList();
            if (section.Tiles is not { } tiles) { AddRows(rows, section, items); continue; }
            foreach (var group in items)
            {
                AddRows(rows, section, [group]);
                if (section.TilesOf!(group) is not { } of) continue;
                if (of is INotifyCollectionChanged changes) nested.Add(changes);
                AddRows(rows, tiles, [.. of.Cast<object>()]);
            }
            foreach (var group in items.OfType<INotifyPropertyChanged>()) nested.Add(new GroupWatch(group));
        }
        WatchNested(nested);
        Sync(rows);
    }

    /// <summary>A section's items as rows: one item a row, or for a tile list a line of tiles a row; the gap between two rows, the
    /// list's own margin above the first and below the last.</summary>
    private void AddRows(List<object> rows, Section section, List<object> items)
    {
        var list = section.List;
        var perRow = !section.IsTiles ? 1
            : !section.HasColumns ? Math.Max(1, items.Count)
            : double.IsNaN(width) ? 1
            : MaTileGrid.ColumnsFor(width, int.MaxValue, MaList.GetMinItemWidth(list), MaList.GetSpacing(list), balanced: false);
        var gap = section.IsTiles ? MaList.GetRowSpacing(list) : MaList.GetSpacing(list);
        for (var start = 0; start < items.Count; start += perRow)
        {
            var first = start == 0;
            var last = start + perRow >= items.Count;
            var margin = new Thickness(0, first ? list.Margin.Top : 0, 0, last ? list.Margin.Bottom : gap);
            rows.Add(new Row(list, [.. items.Skip(start).Take(perRow)], margin));
        }
    }

    /// <summary>Brings the list's items to <paramref name="rows"/>, keeping every row that's still there (and its built row).</summary>
    private void Sync(List<object> rows)
    {
        var items = host.Items;
        var wanted = new HashSet<object>(rows);
        for (var i = items.Count - 1; i >= 0; i--)
            if (!wanted.Contains(items[i])) items.RemoveAt(i);
        for (var i = 0; i < rows.Count; i++)
        {
            if (i < items.Count && Equals(items[i], rows[i])) continue;
            var at = -1;
            for (var j = i + 1; j < items.Count; j++) if (Equals(items[j], rows[i])) { at = j; break; }
            if (at >= 0)
            {
                var kept = items[at];
                items.RemoveAt(at);
                items.Insert(i, kept);
            }
            else items.Insert(i, rows[i]);
        }
        while (items.Count > rows.Count) items.RemoveAt(items.Count - 1);
    }

    /// <summary>A nested list's own collection and its groups' changes (a group shown or hidden) rebuild the rows too.</summary>
    private void WatchNested(List<INotifyCollectionChanged> now)
    {
        foreach (var source in now)
        {
            if (watched.Add(source)) source.CollectionChanged += OnNestedChanged;
        }
    }

    private void OnNestedChanged(object? sender, NotifyCollectionChangedEventArgs e) => Schedule();

    private static DataTemplate MakeRowTemplate()
    {
        // A row: the source list's own style, template and spacing over the row's items, so it lays them out as the list did.
        var row = new FrameworkElementFactory(typeof(ItemsControl));
        row.SetBinding(FrameworkElement.StyleProperty, new Binding("List.Style"));
        row.SetBinding(ItemsControl.ItemTemplateProperty, new Binding("List.ItemTemplate"));
        row.SetBinding(ItemsControl.ItemTemplateSelectorProperty, new Binding("List.ItemTemplateSelector"));
        row.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(Row.Items)));
        row.SetBinding(FrameworkElement.MarginProperty, new Binding(nameof(Row.Margin)));
        foreach (var property in new[] { MaList.SpacingProperty, MaList.RowSpacingProperty, MaList.MinItemWidthProperty, MaList.UniformRowsProperty, AutomationProperties.NameProperty })
            row.SetBinding(property, new Binding { Path = new PropertyPath("List.(0)", property) });
        return new DataTemplate(typeof(Row)) { VisualTree = row };
    }

    /// <summary>A library list in the body: the source of its rows.</summary>
    private sealed class Section(ItemsControl list)
    {
        public ItemsControl List { get; } = list;
        public bool IsTiles { get; } = list.ItemsPanel?.LoadContent() is MaTileGrid;
        public bool HasColumns => MaList.GetMinItemWidth(List) is var w && !double.IsNaN(w) && w > 0;
        public Section? Tiles { get; set; }
        public Func<object, IEnumerable?>? TilesOf { get; set; }
    }

    /// <summary>One row of the body's list: some of <see cref="List"/>'s items (one, or a line of tiles).</summary>
    public sealed class Row(ItemsControl list, object[] items, Thickness margin)
    {
        public ItemsControl List { get; } = list;
        public object[] Items { get; } = items;
        public Thickness Margin { get; } = margin;

        public override bool Equals(object? obj) => obj is Row r && r.List == List && r.Margin == Margin
            && r.Items.Length == Items.Length && r.Items.Zip(Items).All(p => ReferenceEquals(p.First, p.Second));

        public override int GetHashCode() => HashCode.Combine(List, Items.Length, Items.Length > 0 ? RuntimeHelpers.GetHashCode(Items[0]) : 0);
    }

    /// <summary>A group's property changes (shown or hidden, its count) as a change of the body's rows.</summary>
    private sealed class GroupWatch : INotifyCollectionChanged
    {
        private readonly INotifyPropertyChanged group;
        public GroupWatch(INotifyPropertyChanged group) => this.group = group;
        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add { if (value is not null) group.PropertyChanged += Forward(value); }
            remove { }
        }
        private static PropertyChangedEventHandler Forward(NotifyCollectionChangedEventHandler to) =>
            (s, _) => to(s, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        public override bool Equals(object? obj) => obj is GroupWatch g && ReferenceEquals(g.group, group);
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(group);
    }

    /// <summary>The body list's panel: tells the body the width its rows get (what the scroll view offers, at most the panel's
    /// MaxWidth), which sets how many tiles a row holds. The width offered, not the panel's own, which a left-aligned panel takes
    /// from the rows on screen.</summary>
    private sealed class BodyPanel : VirtualizingStackPanel
    {
        protected override Size MeasureOverride(Size constraint)
        {
            if (!double.IsInfinity(constraint.Width) && ItemsControl.GetItemsOwner(this) is { } owner && Bodies.TryGetValue(owner, out var body))
                body.OnWidth(Math.Min(constraint.Width, MaxWidth));
            return base.MeasureOverride(constraint);
        }
    }
}
