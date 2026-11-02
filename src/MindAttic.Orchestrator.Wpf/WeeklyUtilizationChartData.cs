using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using MindAttic.Components.Wpf.Controls;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Wpf.ViewModels;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// The weekly window as the library's <see cref="MaLineChart"/> draws it (M.ORCH-T1403): the same drawing rules as
/// <see cref="UtilizationChartData"/>'s 5-hour chart (target line, pace line to the reset, the week's own line coloured by
/// whether each stretch is above or below pace, and the dotted projection), over <see cref="WeeklyUtilizationSeries"/>'s
/// week-long <see cref="Utilization"/> instead of the 5-hour one. <see cref="Formatting.FiveHourPercent"/> is reused for the
/// percent text here too: the same "a whole percent hides the last stretch's movement near the stop line" reasoning holds
/// for the week as much as the 5-hour window, so a second formatter would only duplicate it.
/// </summary>
public static class WeeklyUtilizationChartData
{
    /// <summary>What the chart draws for <paramref name="u"/>.</summary>
    public static MaLineChartData From(Utilization u, UtilizationChartData.ChartBrushes brushes)
    {
        if (u.Ended && u.Actual.Count == 0) return new MaLineChartData(u.StartUtc, u.EndUtc, []);

        var target = MaChartReferenceLine.Level(u.StartUtc, u.EndUtc, u.TargetPercent, brushes.Accent,
            label: string.Create(CultureInfo.CurrentCulture, $"{u.TargetPercent:0}% target"));

        var pace = MaChartReferenceLine.Curve(u.StartUtc, u.EndUtc, u.PaceAt, steps: u.Curved ? 60 : 1);
        var points = u.Actual.Select(p => new MaChartPoint(p.AtUtc, p.Percent, p.Percent > u.PaceAt(p.AtUtc) ? brushes.Bad : brushes.Good)).ToList();
        var now = new MaChartPoint(u.NowUtc, u.NowPercent, u.NowPercent > u.PaceAt(u.NowUtc) ? brushes.Bad : brushes.Good);
        if (points.Count == 0 || u.NowUtc > points[^1].AtUtc) points.Add(now);
        else if (u.NowUtc == points[^1].AtUtc) points[^1] = now;

        if (u.Ended)
            return new MaLineChartData(u.StartUtc, u.EndUtc, points, $"ended {Formatting.FiveHourPercent(u.NowPercent)}",
                ReferenceLines: [target, pace]);

        var lands = u.ProjectedEndPercent > 100 ? brushes.Bad : u.ProjectedEndPercent > u.TargetPercent ? brushes.Warn : brushes.Good;
        return new MaLineChartData(u.StartUtc, u.EndUtc, points, $"now {Formatting.FiveHourPercent(u.NowPercent)}",
            new MaChartProjection(u.EndUtc, u.ProjectedEndPercent, lands, $"{Formatting.FiveHourPercent(u.ProjectedEndPercent)} at reset"),
            [target, pace]);
    }

    /// <summary>Binds the view model's <see cref="MainViewModel.WeeklyUtilization"/> to <see cref="MaLineChart.Data"/>.</summary>
    public static IValueConverter Converter { get; } = new WeeklyUtilizationConverter();

    private sealed class WeeklyUtilizationConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is Utilization u ? From(u, UtilizationChartData.ThemeBrushes()) : null;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
