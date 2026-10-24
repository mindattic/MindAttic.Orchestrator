using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Xml.Linq;
using MindAttic.Components.Wpf.Controls;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// The dashboard is aligned at every window size (M.ORCH-T192): the stat tiles are equal cells spanning the row, the chart
/// shares their edges, the toolbar and the body views share the same gutters, and the card grids fill the width with equal
/// columns and gaps.
/// </summary>
[Apartment(ApartmentState.STA)]
public sealed class DashboardAlignmentTests
{
    private static readonly XNamespace P = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>Every row of the dashboard starts and ends this far from the window's edges.</summary>
    private const double Side = 24;

    private static MaTileGrid Lay(MaTileGrid grid, int children, double width, Func<int, double>? height = null)
    {
        for (var i = 0; i < children; i++) grid.Children.Add(new Border { Height = height?.Invoke(i) ?? 74 });
        grid.Measure(new Size(width, double.PositiveInfinity));
        grid.Arrange(new Rect(0, 0, width, grid.DesiredSize.Height));
        grid.UpdateLayout();
        return grid;
    }

    private static Rect Slot(UIElement child) => LayoutInformation.GetLayoutSlot((FrameworkElement)child);

    [TestCase(1432)] // the default 1480px window
    [TestCase(932)]  // the 980px minimum
    [TestCase(1200)]
    [TestCase(1900)]
    [TestCase(2500)]
    public void The_stat_tiles_share_one_size_and_together_span_the_full_row(double width)
    {
        var grid = Lay(new MaTileGrid { MinItemWidth = 160, Spacing = 10, RowSpacing = 10, Balanced = true, UniformRows = true }, 8, width, i => i == 4 ? 80 : 74);
        var slots = grid.Children.Cast<UIElement>().Select(Slot).ToList();

        Assert.That(slots.Select(s => Math.Round(s.Width, 3)).Distinct().Count(), Is.EqualTo(1), "every tile is the same width");
        Assert.That(slots.Select(s => s.Height).Distinct().Count(), Is.EqualTo(1), "every tile is the same height");
        foreach (var row in slots.GroupBy(s => s.Y))
        {
            Assert.That(row.Min(s => s.Left), Is.EqualTo(0), "each row starts on the left edge");
            Assert.That(row.Max(s => s.Right), Is.EqualTo(width).Within(0.001), "each row ends on the right edge");
        }
        Assert.That(slots.GroupBy(s => s.Y).Select(r => r.Count()).Distinct().Count(), Is.EqualTo(1), "rows are balanced: 8 is 1x8, 2x4, never 7+1");
    }

    [Test]
    public void The_tiles_reflow_in_step_when_the_window_resizes()
    {
        var grid = Lay(new MaTileGrid { MinItemWidth = 160, Spacing = 10, Balanced = true }, 8, 1432);
        Assert.That(grid.Children.Cast<UIElement>().Select(Slot).Select(s => s.Y).Distinct().Count(), Is.EqualTo(1), "one row at the default size");

        grid.Measure(new Size(932, double.PositiveInfinity));
        grid.Arrange(new Rect(0, 0, 932, grid.DesiredSize.Height));
        var narrow = grid.Children.Cast<UIElement>().Select(Slot).ToList();
        Assert.That(narrow.Select(s => s.Y).Distinct().Count(), Is.EqualTo(2), "two rows of four at the minimum size");
        Assert.That(narrow.Max(s => s.Right), Is.EqualTo(932).Within(0.001));
        Assert.That(narrow.Where(s => s.Y > 0).Select(s => s.Left), Is.EqualTo(narrow.Where(s => s.Y == 0).Select(s => s.Left)), "the second row lines up under the first");
    }

    [TestCase(1432, 3)]
    [TestCase(932, 2)]
    [TestCase(400, 1)]
    public void The_agent_cards_fill_the_width_with_equal_columns_and_gaps(double width, int columns)
    {
        var grid = Lay(new MaTileGrid { MinItemWidth = 420, Spacing = 12, RowSpacing = 12 }, 5, width, i => 100 + 30 * i);
        var slots = grid.Children.Cast<UIElement>().Select(Slot).ToList();

        Assert.That(slots.Select(s => s.Left).Distinct().Count(), Is.EqualTo(columns));
        var firstRow = slots.Take(columns).ToList();
        Assert.That(firstRow[0].Left, Is.EqualTo(0));
        Assert.That(firstRow[^1].Right, Is.EqualTo(width).Within(0.001), "no ragged empty strip on the right");
        for (var c = 1; c < columns; c++) Assert.That(firstRow[c].Left - firstRow[c - 1].Right, Is.EqualTo(12).Within(0.001), "equal gaps");
        Assert.That(slots.Select(s => Math.Round(s.Width, 0)).Distinct().Count(), Is.EqualTo(1), "equal columns");
        Assert.That(slots.Skip(columns).Select(s => s.Left), Is.EqualTo(slots.Take(slots.Count - columns).Select(s => s.Left)), "later rows stay in the same columns");
    }

    [Test]
    public void Hidden_children_take_no_cell()
    {
        var grid = new MaTileGrid { MinItemWidth = 100, Spacing = 10, Balanced = true };
        grid.Children.Add(new Border { Height = 10 });
        grid.Children.Add(new Border { Height = 10, Visibility = Visibility.Collapsed });
        grid.Children.Add(new Border { Height = 10 });
        grid.Measure(new Size(410, double.PositiveInfinity));
        grid.Arrange(new Rect(0, 0, 410, grid.DesiredSize.Height));

        Assert.That(Slot(grid.Children[2]).Right, Is.EqualTo(410).Within(0.001));
        Assert.That(Slot(grid.Children[0]).Width, Is.EqualTo(200).Within(0.001));
    }

    [Test]
    public void Every_row_of_the_dashboard_shares_the_same_left_and_right_gutter()
    {
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        var doc = XDocument.Load(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        XElement Named(string name) => doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);
        static double[] Sides(string? thickness)
        {
            var parts = (thickness ?? "0").Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
            return parts.Length switch { 1 => [parts[0], parts[0]], 2 => [parts[0], parts[0]], _ => [parts[0], parts[2]] };
        }

        Assert.That(Sides((string?)Named("Header").Attribute("Margin")), Is.EqualTo(new[] { Side, Side }), "the header owns the gutter");
        Assert.That(Sides((string?)Named("Toolbar").Attribute("Margin")), Is.EqualTo(new[] { Side, Side }), "the tab row and the zoom");
        Assert.That(Sides((string?)Named("ToastRow").Attribute("Margin")), Is.EqualTo(new[] { Side, Side }));

        // Nothing inside the header adds a side margin of its own, so the tiles and the chart share both edges.
        var tiles = Named("StatTiles");
        var chart = Named("UtilizationCard");
        Assert.That(Sides((string?)tiles.Attribute("Margin")), Is.EqualTo(new[] { 0d, 0d }));
        Assert.That(Sides((string?)chart.Attribute("Margin")), Is.EqualTo(new[] { 0d, 0d }), "the chart lines up with the tiles' left and right edges");
        Assert.That(chart.Attribute("Width"), Is.Null);
        foreach (var tile in tiles.Elements(P + "Button"))
        {
            Assert.That(tile.Attribute("Width") ?? tile.Attribute("MinWidth") ?? tile.Attribute("Margin"), Is.Null, "the grid sizes every tile");
            Assert.That(tile.Descendants().Where(d => d.Name.LocalName == "Setter" && ((string?)d.Attribute("Property")) is "Margin" or "Width" or "MinWidth"), Is.Empty);
            Assert.That(tile.Descendants(P + "StackPanel").Select(s => s.Attribute("Width")).Where(a => a != null), Is.Empty, "no fixed-width tile content");
        }

        // Every body view uses the library's one gutter style (M.ORCH-T667), which keeps the right edge beside the scroll bar too.
        _ = System.IO.Packaging.PackUriHelper.UriSchemePack; // registers pack:// outside an Application
        var theme = (ResourceDictionary)Application.LoadComponent(new Uri("/MindAttic.Components.Wpf;component/Theme.xaml", UriKind.Relative));
        var gutter = (Thickness)((Style)theme["MaBodyScroll"]).Setters.OfType<Setter>().Single(x => x.Property == MaScrollArea.GutterProperty).Value;
        Assert.That(new[] { gutter.Left, gutter.Right }, Is.EqualTo(new[] { Side, Side }));
        foreach (var body in new[] { "AgentsBody", "MiscBody", "ApiKeyBody" })
            Assert.That((string?)Named(body).Attribute("Style"), Is.EqualTo("{StaticResource MaBodyScroll}"), body);
        var beside = MaScrollArea.PaddingFor(gutter, Visibility.Visible);
        Assert.That(beside.Left, Is.EqualTo(Side));
        Assert.That(beside.Right + MaScrollArea.ScrollBarWidth, Is.EqualTo(Side), "the scroll bar sits inside the gutter");

        // The agent cards are laid out by the library's MaTileList and carry no fixed width or margin of their own.
        var agents = Named("AgentCards");
        Assert.That((string?)agents.Attribute("Style"), Is.EqualTo("{StaticResource MaTileList}"));
        var card = agents.Descendants(P + "DataTemplate").First().Elements().First();
        Assert.That(card.Attribute("Width") ?? card.Attribute("Margin"), Is.Null);
    }

    [Test]
    public void The_clock_refresh_settings_and_launcher_sit_right_of_the_current_work_heading_under_the_chart()
    {
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        var doc = XDocument.Load(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        XElement Named(string name) => doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);
        static string? Binding(XElement e, string attribute) => (string?)e.Attribute(attribute);

        // Below the chart, on the view tabs' row and right of them (M.ORCH-T667): the toolbar's right-hand column.
        var toolbar = Named("Toolbar");
        var controls = Named("HeaderControls");
        Assert.That(controls.Ancestors().Contains(toolbar), Is.True, "on the toolbar's row, under the chart");
        Assert.That((string?)controls.Parent!.Attribute("Grid.Column"), Is.EqualTo("1"), "right of the current work's heading");
        Assert.That(Named("Header").Descendants().Contains(controls), Is.False, "no longer above the tiles");
        // No view pills (M.ORCH-T763): the person's current work is named on the left.
        Assert.That(toolbar.Descendants(P + "ListBox").Any(l => (string?)l.Attribute("ItemsSource") == "{Binding ViewChoices}"), Is.False);
        Assert.That((string?)Named("WorkHeading").Attribute("Grid.Column"), Is.Null, "the heading on the left");

        var clock = controls.Elements(P + "TextBlock").Single(t => Binding(t, "Text") == "{Binding Clock.Text}");
        Assert.That((string?)clock.Attribute("AutomationProperties.Name"), Is.EqualTo("Current time"));
        Assert.That(controls.Elements(P + "TextBlock").Any(t => Binding(t, "Text") == "{Binding RefreshInfo}"), Is.True);
        var refresh = controls.Elements(P + "Button").Single(b => (string?)b.Attribute("Content") == "Refresh");
        Assert.That(Binding(refresh, "Command"), Is.EqualTo("{Binding RefreshCommand}"));
        Assert.That((string?)refresh.Attribute("ToolTip"), Is.EqualTo("F5"));
        var settings = controls.Elements(P + "Button").Single(b => (string?)b.Attribute("Content") == "Settings");
        Assert.That(Binding(settings, "Command"), Is.EqualTo("{Binding ToggleSettingsCommand}"));
        Assert.That(doc.Descendants(P + "KeyBinding").Any(k => (string?)k.Attribute("Key") == "F5" && Binding(k, "Command") == "{Binding RefreshCommand}"), Is.True, "F5 still refreshes");

        Assert.That(doc.Descendants().Count(e => Binding(e, "Text") is "{Binding Clock.Text}" or "{Binding RefreshInfo}"
                                                 || (string?)e.Attribute("Content") is "Refresh" or "Settings"), Is.EqualTo(4), "one of each");
    }
}
