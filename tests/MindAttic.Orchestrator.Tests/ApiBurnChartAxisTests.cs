using System.Windows.Media;
using MindAttic.Components.Wpf.Controls;
using MindAttic.Orchestrator.Billing;
using MindAttic.Orchestrator.Wpf;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// M.ORCH-T1327: the API key page's credit burndown spans days (a day back, three ahead), and MaLineChart used to label every
/// whole hour on it, ~96 labels on one axis drawn on top of each other. Since MindAttic.Components.Wpf 26.0.0 (M.COMP-T161)
/// the chart labels only the hours that fit; these pin that for the burndown's own span.
/// </summary>
public sealed class ApiBurnChartAxisTests
{
    private static readonly UtilizationChartData.ChartBrushes Theme = new(Brushes.SteelBlue, Brushes.Green, Brushes.Orange, Brushes.Red);
    private static readonly DateTime Now = new(2026, 10, 31, 23, 40, 0, DateTimeKind.Utc);
    private const double LabelWidth = 52; // "12:00 PM" in Segoe UI 12.5

    /// <summary>A full day's hourly series: $1 an hour since midnight a day ago.</summary>
    private static ApiSpend Day(double? credit)
    {
        var since = Now.Date.AddDays(-1);
        var burn = Enumerable.Range(1, (int)(Now - since).TotalHours).Select(h => new ApiSpendPoint(since.AddHours(h), h)).ToList();
        return new ApiSpend(credit, since, burn[^1].SpentUsd, 23, 1, burn);
    }

    [TestCase(100.0, 1344)]
    [TestCase(100.0, 700)]
    [TestCase(null, 1344)]
    [TestCase(null, 500)]
    public void A_full_days_burndown_labels_only_as_many_hours_as_fit(double? credit, double plotWidth)
    {
        var d = ApiBurnChartData.From(Day(credit), Now, Theme);
        var hours = MaLineChart.HourTicks(d.StartUtc, d.EndUtc, TimeZoneInfo.Local).Count();
        var ticks = MaLineChart.LabelTicks(d.StartUtc, d.EndUtc, TimeZoneInfo.Local, plotWidth, LabelWidth);

        Assert.That(hours, Is.GreaterThan(24), "the burndown spans more than a day");
        Assert.That(ticks, Is.Not.Empty);
        Assert.That(ticks.Count, Is.LessThan(hours), "not every hour is labelled");
        Assert.That(ticks.Count, Is.LessThanOrEqualTo(plotWidth / (LabelWidth + MaLineChart.LabelGap)), "no more labels than the width holds");
        var pxPerHour = plotWidth / (d.EndUtc - d.StartUtc).TotalHours;
        for (var i = 1; i < ticks.Count; i++)
            Assert.That((ticks[i] - ticks[i - 1]).TotalHours * pxPerHour, Is.GreaterThanOrEqualTo(LabelWidth + MaLineChart.LabelGap),
                $"labels at {ticks[i - 1]:u} and {ticks[i]:u} overlap");
    }

    [Test]
    public void The_five_hour_chart_still_labels_every_hour()
    {
        var start = new DateTime(2026, 10, 31, 22, 48, 0, DateTimeKind.Utc);
        Assert.That(MaLineChart.LabelTicks(start, start.AddHours(5), TimeZoneInfo.Local, 1344, LabelWidth),
            Is.EqualTo(MaLineChart.HourTicks(start, start.AddHours(5), TimeZoneInfo.Local).ToList()));
    }
}
