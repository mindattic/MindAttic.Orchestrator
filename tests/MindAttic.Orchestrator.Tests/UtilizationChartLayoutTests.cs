using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Wpf;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The 5-hour utilization chart spans the dashboard's full width under the stat tiles, and is tall (M.ORCH-T191).</summary>
[Apartment(ApartmentState.STA)]
public sealed class UtilizationChartLayoutTests
{
    [Test]
    public void The_chart_draws_its_pace_line_target_and_projection_at_full_width()
    {
        var start = new DateTime(2026, 10, 16, 14, 0, 0, DateTimeKind.Utc);
        var now = start.AddHours(3);
        var actual = new List<UtilizationPoint> { new(start, 0), new(start.AddHours(1), 30), new(start.AddHours(2), 45), new(now, 70) };
        var chart = new UtilizationChart { Data = new Utilization(start, start.AddHours(5), now, 99, actual, 70, 20, 110, TimeSpan.FromHours(2)) };
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
    public void The_utilization_chart_has_its_own_full_width_row_under_the_tiles_and_is_tall()
    {
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation", x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var doc = XDocument.Load(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        var card = doc.Descendants(p + "Border").Single(b => (string?)b.Attribute(x + "Name") == "UtilizationCard");
        var chart = card.Descendants().Single(e => e.Name.LocalName == "UtilizationChart");

        Assert.That(card.Ancestors(p + "WrapPanel"), Is.Empty, "the chart isn't one more tile beside the stats");
        var header = card.Parent!;
        Assert.That(header.Name, Is.EqualTo(p + "StackPanel"));
        var siblings = header.Elements().ToList();
        Assert.That(siblings.IndexOf(card), Is.GreaterThan(siblings.FindIndex(e => e.Name.LocalName == "AlignedGrid")), "it sits under the tiles");
        Assert.That(card.Attribute("Width"), Is.Null, "no fixed width: it stretches across the window");
        Assert.That(chart.Attribute("Width"), Is.Null);
        Assert.That(double.Parse((string)chart.Attribute("Height")!, System.Globalization.CultureInfo.InvariantCulture), Is.GreaterThanOrEqualTo(240));
    }

    // M.ORCH-T194: near the reset, 'now NN%' ran into 'NN% at reset'.
    private static readonly Rect Plot = new(44, 10, 1344, 226);
    private static readonly Size NowSize = new(62, 17), ResetSize = new(92, 17);

    [Test]
    public void Far_from_the_reset_now_keeps_its_usual_spot_above_right_of_the_dot()
    {
        var (now, reset) = UtilizationChart.PlaceEndLabels(new Point(800, 120), NowSize, new Point(Plot.Right, 60), ResetSize, Plot);
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
        var (now, reset) = UtilizationChart.PlaceEndLabels(new Point(nowX, nowY), NowSize, new Point(Plot.Right, endY), ResetSize, Plot);
        if (reset is { } r) Assert.That(now.IntersectsWith(r), Is.False, $"now {now} overlaps reset {r}");
        Assert.That(now.Left, Is.GreaterThanOrEqualTo(Plot.Left));
        Assert.That(now.Right, Is.LessThanOrEqualTo(Plot.Right + 12), "now stays inside the chart");
    }
}
