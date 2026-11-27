using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using MindAttic.Orchestrator.Wpf.ViewModels;

namespace MindAttic.Orchestrator.Wpf.Views;

/// <summary>The Roadmap page (M.ORCH-T2384, M.ORCH-T2386), in the main area in place of the terminal; its data context is the
/// global login's tab. Its connector lines join each row's selected block to its children in the row below: one orthogonal
/// elbow per edge, drawn on the <c>Connectors</c> canvas over the rows whenever the layout moves.</summary>
public partial class RoadmapPage : Grid
{
    // The last edges drawn, so a layout pass that moved nothing doesn't redraw (and lay out again).
    private string drawn = "";

    public RoadmapPage()
    {
        InitializeComponent();
        LayoutUpdated += (_, _) => DrawConnectors();
        SizeChanged += (_, _) => DrawConnectors();
    }

    /// <summary>Draws one path from the bottom centre of each row's selected block to the top centre of every block in the row
    /// below it: an elbow down, across and down, halfway through the gap. The selected child's path takes the accent.</summary>
    private void DrawConnectors()
    {
        if (!IsVisible || Content.ActualWidth <= 0) return;
        var blocks = Blocks(Rows).Where(b => b.Button.IsVisible).ToLookup(b => b.Model.Row);
        var edges = new List<(Point From, Point To, bool Selected)>();
        foreach (var row in blocks)
        {
            if (row.Key == 0) continue;
            var parent = blocks[row.Key - 1].FirstOrDefault(b => b.Model.IsSelected).Button;
            if (parent is null) continue;
            var from = parent.TranslatePoint(new Point(parent.ActualWidth / 2, parent.ActualHeight), Content);
            foreach (var (button, model) in row)
                edges.Add((from, button.TranslatePoint(new Point(button.ActualWidth / 2, 0), Content), model.IsSelected));
        }
        var key = new StringBuilder();
        foreach (var e in edges) key.Append(CultureInfo.InvariantCulture, $"{e.From.X:0},{e.From.Y:0},{e.To.X:0},{e.To.Y:0},{e.Selected};");
        if (key.ToString() == drawn) return;
        drawn = key.ToString();
        Connectors.Children.Clear();
        foreach (var (from, to, selected) in edges.OrderBy(e => e.Selected))
        {
            var midY = to.Y - Math.Max(4, (to.Y - from.Y) / 2);
            var figure = new PathFigure { StartPoint = from };
            figure.Segments.Add(new PolyLineSegment([new Point(from.X, midY), new Point(to.X, midY), to], isStroked: true));
            var path = new Path { Data = new PathGeometry([figure]), StrokeThickness = selected ? 2 : 1, SnapsToDevicePixels = true };
            path.SetResourceReference(Shape.StrokeProperty, selected ? "MaAccent" : "MaChromeBorder");
            Connectors.Children.Add(path);
        }
    }

    /// <summary>A ticket block's right-click (M.ORCH-T2388): assign it to someone of the org (the one change the Roadmap makes),
    /// copy its id, or open it in Tickets Desktop.</summary>
    private void Block_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not Button { DataContext: RoadmapBlockViewModel { Node: { } node } block } button || DataContext is not MainViewModel model) return;
        e.Handled = true;
        var menu = new ContextMenu { PlacementTarget = button, Placement = e.CursorLeft < 0 ? PlacementMode.Bottom : PlacementMode.MousePoint };
        menu.SetResourceReference(StyleProperty, "MaDropdownMenu");
        AutomationProperties.SetName(menu, $"{node.Id} actions");
        var assign = Item("Assign to…");
        foreach (var person in model.People)
        {
            var who = Item(person.Name);
            who.IsChecked = string.Equals(person.Email, node.Assignee, StringComparison.OrdinalIgnoreCase);
            var email = person.Email;
            who.Click += (_, _) => model.Roadmap.Assign(block, email);
            assign.Items.Add(who);
        }
        menu.Items.Add(assign);
        var copy = Item("Copy id");
        copy.Click += (_, _) =>
        {
            try { Clipboard.SetText(node.Id); }
            catch (System.Runtime.InteropServices.ExternalException) { }
        };
        menu.Items.Add(copy);
        var open = Item("Open in Tickets");
        open.Command = model.OpenInTicketsCommand;
        open.CommandParameter = node.Id;
        menu.Items.Add(open);
        menu.IsOpen = true;
    }

    private static MenuItem Item(string header)
    {
        var item = new MenuItem { Header = header };
        item.SetResourceReference(StyleProperty, "MaDropdownMenuItem");
        AutomationProperties.SetName(item, header);
        return item;
    }

    /// <summary>Every block button under <paramref name="root"/>, with its block.</summary>
    private static IEnumerable<(Button Button, RoadmapBlockViewModel Model)> Blocks(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is Button { DataContext: RoadmapBlockViewModel model } button) { yield return (button, model); continue; }
            foreach (var found in Blocks(child)) yield return found;
        }
    }
}
