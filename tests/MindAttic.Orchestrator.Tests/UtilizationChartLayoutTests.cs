using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using MindAttic.Components.Wpf.Controls;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Wpf;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The 5-hour utilization chart spans the dashboard's full width under the stat tiles, and is tall (M.ORCH-T191).</summary>
[Apartment(ApartmentState.STA)]
public sealed class UtilizationChartLayoutTests
{
    private static readonly UtilizationChartData.ChartBrushes Theme = new(Brushes.SteelBlue, Brushes.Green, Brushes.Orange, Brushes.Red);
    private static readonly DateTime Start = new(2026, 10, 16, 14, 0, 0, DateTimeKind.Utc);

    private static Utilization Window(double now, double projected, double exponent = 1, params (double Hours, double Percent)[] line) =>
        new(Start, Start.AddHours(5), Start.AddHours(3), 80, [.. line.Select(p => new UtilizationPoint(Start.AddHours(p.Hours), p.Percent))],
            now, 10, projected, TimeSpan.Zero, exponent);

    // M.ORCH-T531: the library's MaLineChart draws the window; this is what it's given.
    [Test]
    public void The_target_is_an_accent_level_and_the_pace_line_reaches_it_at_the_reset()
    {
        var d = UtilizationChartData.From(Window(50, 70, line: [(0, 0), (1, 10), (3, 50)]), Theme);

        Assert.That(d.StartUtc, Is.EqualTo(Start));
        Assert.That(d.EndUtc, Is.EqualTo(Start.AddHours(5)));
        var target = d.ReferenceLines![0];
        Assert.That(target.Label, Is.EqualTo("80% target"));
        Assert.That(target.Brush, Is.SameAs(Brushes.SteelBlue));
        Assert.That(target.Path.Select(p => p.Percent), Is.EqualTo(new[] { 80d, 80d }));
        var pace = d.ReferenceLines[1];
        Assert.That(pace.Dash, Is.EqualTo(MaLineDash.Dashed));
        Assert.That(pace.Path, Has.Count.EqualTo(2), "a straight line on the linear profile");
        Assert.That(pace.Path[0].Percent, Is.Zero);
        Assert.That(pace.Path[^1].Percent, Is.EqualTo(80));

        var curved = UtilizationChartData.From(Window(50, 70, exponent: 0.5, line: [(0, 0), (3, 50)]), Theme).ReferenceLines![1];
        Assert.That(curved.Path, Has.Count.EqualTo(61), "the front-loaded curve, sampled");
        Assert.That(curved.Path[30].Percent, Is.EqualTo(80 * Math.Sqrt(0.5)).Within(1e-9));
    }

    [Test]
    public void Each_stretch_is_bad_above_the_pace_and_good_below_and_the_line_ends_at_now()
    {
        // Pace: 16% an hour. 1h at 30 is over it; 2h at 30 and 3h at 40 are under.
        var d = UtilizationChartData.From(Window(40, 70, line: [(0, 0), (1, 30), (2, 30), (3, 40)]), Theme);

        Assert.That(d.Points.Skip(1).Select(p => p.Brush), Is.EqualTo(new[] { Brushes.Red, Brushes.Green, Brushes.Green }));
        Assert.That(d.Points[^1].AtUtc, Is.EqualTo(Start.AddHours(3)));
        Assert.That(d.EndLabel, Is.EqualTo("now 40%"));
    }

    [TestCase(120, "Red")]
    [TestCase(90, "Orange")]
    [TestCase(70, "Green")]
    public void The_projection_is_coloured_by_where_it_lands(double projected, string colour)
    {
        var d = UtilizationChartData.From(Window(40, projected, line: [(0, 0), (3, 40)]), Theme);

        Assert.That(d.Projection!.AtUtc, Is.EqualTo(Start.AddHours(5)));
        Assert.That(d.Projection.Percent, Is.EqualTo(projected));
        Assert.That(d.Projection.Label, Is.EqualTo($"{Formatting.FiveHourPercent(projected)} at reset"));
        Assert.That(d.Projection.Brush, Is.SameAs(typeof(Brushes).GetProperty(colour)!.GetValue(null)));
    }

    // M.ORCH-T635: 98.3 over a 98% target read as a bare "98% at reset", hiding the overshoot.
    [Test]
    public void The_projection_label_shows_a_decimal_so_an_overshoot_of_the_target_is_visible()
    {
        var d = UtilizationChartData.From(Window(40, 98.3, line: [(0, 0), (3, 40)]), Theme);

        Assert.That(d.Projection!.Label, Is.EqualTo("98.3% at reset"));
    }

    // M.ORCH-T502 kept: between windows the last one as it ended, with no projection; with none on record, the empty axis.
    [Test]
    public void An_ended_window_is_drawn_to_where_it_ended_without_a_projection()
    {
        var ended = Window(55, 55, line: [(0, 0), (2, 30), (3, 55)]) with { Ended = true };
        var d = UtilizationChartData.From(ended, Theme);

        Assert.That(d.EndLabel, Is.EqualTo("ended 55%"));
        Assert.That(d.Projection, Is.Null);
        Assert.That(d.Points, Has.Count.EqualTo(3));
        Assert.That(d.ReferenceLines, Has.Count.EqualTo(2));
    }

    [Test]
    public void With_no_window_on_record_it_is_just_the_empty_axis()
    {
        var now = Start;
        var idle = new Utilization(now, now.AddHours(5), now, 80, [], 0, 0, 0, TimeSpan.Zero, Ended: true);
        var d = UtilizationChartData.From(idle, Theme);

        Assert.That(d.Points, Is.Empty);
        Assert.That(d.ReferenceLines ?? [], Is.Empty);
        Assert.That(d.Projection, Is.Null);
        Assert.That(d.EndLabel, Is.Null);
        Assert.That(UtilizationChartData.Converter.Convert(null, typeof(MaLineChartData), null, System.Globalization.CultureInfo.InvariantCulture), Is.Null);
    }

    [Test]
    public void The_chart_draws_its_pace_line_target_and_projection_at_full_width()
    {
        var start = new DateTime(2026, 10, 16, 14, 0, 0, DateTimeKind.Utc);
        var now = start.AddHours(3);
        var actual = new List<UtilizationPoint> { new(start, 0), new(start.AddHours(1), 30), new(start.AddHours(2), 45), new(now, 70) };
        var chart = new MaLineChart { Data = UtilizationChartData.From(new Utilization(start, start.AddHours(5), now, 99, actual, 70, 20, 110, TimeSpan.FromHours(2)), Theme) };
        var host = new Border { Background = Brushes.Black, Child = chart, Width = 1400, Height = 260 };
        host.Measure(new Size(1400, 260));
        host.Arrange(new Rect(0, 0, 1400, 260));
        host.UpdateLayout();

        var bitmap = new RenderTargetBitmap(1400, 260, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(host);
        var pixels = new byte[1400 * 260 * 4];
        bitmap.CopyPixels(pixels, 1400 * 4, 0);
        Assert.That(chart.ActualWidth, Is.EqualTo(1400));
        Assert.That(pixels.Where((_, i) => i % 4 != 3).Count(b => b > 40), Is.GreaterThan(5000), "the lines and labels drew");

        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(TestContext.CurrentContext.WorkDirectory, "utilization-chart.png"));
        png.Save(file);
    }

    [Test]
    public void The_utilization_chart_has_its_own_full_width_row_under_terminals_and_is_tall()
    {
        // M.ORCH-T826: moved out of the pinned header (no longer under the stat tiles) to the scrolling body, under Terminals
        // and above Tickets, so Current work and Terminals are the first things on the page.
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation", x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var doc = XDocument.Load(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        var card = doc.Descendants(p + "Border").Single(b => (string?)b.Attribute(x + "Name") == "UtilizationCard");
        var chart = card.Descendants().Single(e => e.Name.LocalName == "MaLineChart");

        Assert.That(card.Ancestors(p + "WrapPanel"), Is.Empty, "the chart isn't one more tile beside the stats");
        Assert.That(card.Ancestors().Any(e => (string?)e.Attribute(x + "Name") == "Header"), Is.False, "no longer pinned above the toolbar");
        Assert.That(card.Ancestors().Any(e => (string?)e.Attribute(x + "Name") == "AgentsBody"), Is.True, "in the scrolling body with current work and Terminals");
        var names = doc.Descendants().Select(e => (string?)e.Attribute(x + "Name")).ToList();
        Assert.That(names.IndexOf("UtilizationCard"), Is.GreaterThan(names.IndexOf("TerminalsPanel")), "under Terminals");
        Assert.That(names.IndexOf("UtilizationCard"), Is.LessThan(names.IndexOf("UserTickets")), "above Tickets");
        Assert.That(card.Attribute("Width"), Is.Null, "no fixed width: it stretches across the window");
        Assert.That(chart.Attribute("Width"), Is.Null);
        Assert.That((string?)chart.Attribute("Data"), Is.EqualTo("{Binding Utilization, Converter={x:Static local:UtilizationChartData.Converter}}"));
        Assert.That(double.Parse((string)chart.Attribute("Height")!, System.Globalization.CultureInfo.InvariantCulture), Is.GreaterThanOrEqualTo(240));
        // It collapses only on an empty caption, which is never so once a snapshot is in, idle or not (M.ORCH-T502).
        Assert.That((string?)card.Attribute("Visibility"), Does.StartWith("{Binding UtilizationText,"));
    }

    // M.ORCH-T464: the hour marks stepped the local clock across a clock change.
    private static readonly TimeZoneInfo Central = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");

    private static string[] Marks(DateTime startUtc, DateTime endUtc, TimeZoneInfo zone) =>
        [.. MaLineChart.HourTicks(startUtc, endUtc, zone).Select(t => TimeZoneInfo.ConvertTimeFromUtc(t, zone).ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture) + "@" + t.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture))];

    [Test]
    public void Hour_marks_fall_on_every_whole_local_hour_between_start_and_reset()
    {
        // 14:20Z to 19:20Z on an ordinary day in Chicago (CDT, UTC-5): 10:00 to 14:00 local, never the ends.
        Assert.That(Marks(new DateTime(2026, 10, 16, 14, 20, 0, DateTimeKind.Utc), new DateTime(2026, 10, 16, 19, 20, 0, DateTimeKind.Utc), Central),
            Is.EqualTo(new[] { "10:00@15:00", "11:00@16:00", "12:00@17:00", "13:00@18:00", "14:00@19:00" }));
        // A window starting on the hour doesn't mark its own start.
        Assert.That(Marks(new DateTime(2026, 10, 16, 15, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 16, 17, 0, 0, DateTimeKind.Utc), Central),
            Is.EqualTo(new[] { "11:00@16:00" }));
        // A half-hour zone marks its own hours, at :30 UTC.
        var india = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
        Assert.That(Marks(new DateTime(2026, 10, 16, 4, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 16, 6, 0, 0, DateTimeKind.Utc), india),
            Is.EqualTo(new[] { "10:00@04:30", "11:00@05:30" }));
    }

    [Test]
    public void Across_the_spring_forward_no_two_hour_marks_share_a_place()
    {
        // 2027-03-14 in Chicago: 2:00 AM CST becomes 3:00 AM CDT at 08:00Z. The window runs 05:30Z to 10:30Z.
        Assert.That(Marks(new DateTime(2027, 3, 14, 5, 30, 0, DateTimeKind.Utc), new DateTime(2027, 3, 14, 10, 30, 0, DateTimeKind.Utc), Central),
            Is.EqualTo(new[] { "00:00@06:00", "01:00@07:00", "03:00@08:00", "04:00@09:00", "05:00@10:00" }));
    }

    [Test]
    public void Across_the_fall_back_both_one_oclocks_are_marked()
    {
        // 2026-11-01 in Chicago: 1:00 AM CDT (06:00Z), then 1:00 AM CST again at 07:00Z.
        Assert.That(Marks(new DateTime(2026, 11, 1, 4, 30, 0, DateTimeKind.Utc), new DateTime(2026, 11, 1, 9, 30, 0, DateTimeKind.Utc), Central),
            Is.EqualTo(new[] { "00:00@05:00", "01:00@06:00", "01:00@07:00", "02:00@08:00", "03:00@09:00" }));
    }

    // M.ORCH-T194: near the reset, 'now NN%' ran into 'NN% at reset'.
    private static readonly Rect Plot = new(44, 10, 1344, 226);
    private static readonly Size NowSize = new(62, 17), ResetSize = new(92, 17);

    [Test]
    public void Far_from_the_reset_now_keeps_its_usual_spot_above_right_of_the_dot()
    {
        var (now, reset) = MaLineChart.PlaceEndLabels(new Point(800, 120), NowSize, new Point(Plot.Right, 60), ResetSize, Plot);
        Assert.That(now.Left, Is.EqualTo(806));
        Assert.That(now.Top + now.Height / 2, Is.EqualTo(102));
        Assert.That(reset, Is.Not.Null);
    }

    [TestCase(1370, 80, 70)]   // now just before the reset, projection a little higher
    [TestCase(1380, 60, 60)]   // flat: both at the same height
    [TestCase(1340, 75, 65)]
    [TestCase(1387, 12, 12)]   // pinned to the top edge
    [TestCase(1300, 120, 118)]
    public void Near_the_reset_the_now_label_and_the_projection_label_do_not_overlap(double nowX, double nowY, double endY)
    {
        var (now, reset) = MaLineChart.PlaceEndLabels(new Point(nowX, nowY), NowSize, new Point(Plot.Right, endY), ResetSize, Plot);
        if (reset is { } r) Assert.That(now.IntersectsWith(r), Is.False, $"now {now} overlaps reset {r}");
        Assert.That(now.Left, Is.GreaterThanOrEqualTo(Plot.Left));
        Assert.That(now.Right, Is.LessThanOrEqualTo(Plot.Right + 12), "now stays inside the chart");
    }
}
