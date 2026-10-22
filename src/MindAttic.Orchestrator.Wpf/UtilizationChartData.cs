using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using MindAttic.Components.Wpf.Controls;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Wpf.ViewModels;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// The 5-hour window as the library's <see cref="MaLineChart"/> draws it (M.ORCH-T531; the chart itself was the dashboard's
/// own UtilizationChart, M.ORCH-T191). The target is a level line in MaAccent labelled 'NN% target'; the pace line reaches
/// it exactly at the reset (straight on the linear profile, the front-loaded curve otherwise, M.ORCH-T234); the window's
/// own line is MaBad on each stretch that ended above the pace and MaGood below; and, dotted, where the recent rate ends up
/// by the reset, in MaBad past 100%, MaWarn past the target, else MaGood. Between windows it's the last one as it ended,
/// labelled 'ended NN%' with no projection, or with none on record just the empty axis (M.ORCH-T502). The caption under
/// the chart says the same in words, so nothing is told by colour alone. With nothing running on the login it's a flat line
/// at its current % across the window against the target, labelled 'idle NN%' (M.ORCH-T629).
/// </summary>
public static class UtilizationChartData
{
    /// <summary>The brushes the chart's data is drawn in.</summary>
    public sealed record ChartBrushes(Brush Accent, Brush Good, Brush Warn, Brush Bad);

    /// <summary>The theme's MaAccent, MaGood, MaWarn and MaBad (the library theme's own outside the app, in tests).</summary>
    public static ChartBrushes ThemeBrushes() => new(Palette.Accent, Palette.Working, Palette.Waiting, Palette.Over);

    /// <summary>What the chart draws for <paramref name="u"/>.</summary>
    public static MaLineChartData From(Utilization u, ChartBrushes brushes)
    {
        // No window running and none on record: just the empty axis; the caption says the next starts with the next prompt.
        if (u.Ended && u.Actual.Count == 0) return new MaLineChartData(u.StartUtc, u.EndUtc, []);

        var target = MaChartReferenceLine.Level(u.StartUtc, u.EndUtc, u.TargetPercent, brushes.Accent,
            label: string.Create(CultureInfo.CurrentCulture, $"{u.TargetPercent:0}% target"));
        // Nothing running on the login (M.ORCH-T629): a flat line at its current % across the window, against the target.
        if (u.Idle)
            return new MaLineChartData(u.StartUtc, u.EndUtc,
                [.. u.Actual.Select(p => new MaChartPoint(p.AtUtc, p.Percent, p.Percent > u.TargetPercent ? brushes.Bad : brushes.Good))],
                $"idle {Formatting.FiveHourPercent(u.NowPercent)}", ReferenceLines: [target]);

        var pace = MaChartReferenceLine.Curve(u.StartUtc, u.EndUtc, u.PaceAt, steps: u.Curved ? 60 : 1);

        // The window so far, each stretch coloured by whether it ended above the pace line, ending at now.
        var points = u.Actual.Select(p => new MaChartPoint(p.AtUtc, p.Percent, p.Percent > u.PaceAt(p.AtUtc) ? brushes.Bad : brushes.Good)).ToList();
        var now = new MaChartPoint(u.NowUtc, u.NowPercent, u.NowPercent > u.PaceAt(u.NowUtc) ? brushes.Bad : brushes.Good);
        if (points.Count == 0 || u.NowUtc > points[^1].AtUtc) points.Add(now);
        else if (u.NowUtc == points[^1].AtUtc) points[^1] = now;

        // The last window, between windows: where it ended, and no projection, since nothing more will be spent in it.
        if (u.Ended)
            return new MaLineChartData(u.StartUtc, u.EndUtc, points, $"ended {Formatting.FiveHourPercent(u.NowPercent)}",
                ReferenceLines: [target, pace]);

        var lands = u.ProjectedEndPercent > 100 ? brushes.Bad : u.ProjectedEndPercent > u.TargetPercent ? brushes.Warn : brushes.Good;
        return new MaLineChartData(u.StartUtc, u.EndUtc, points, $"now {Formatting.FiveHourPercent(u.NowPercent)}",
            new MaChartProjection(u.EndUtc, u.ProjectedEndPercent, lands, $"{Formatting.FiveHourPercent(u.ProjectedEndPercent)} at reset"),
            [target, pace]);
    }

    /// <summary>Binds the view model's <see cref="Utilization"/> to <see cref="MaLineChart.Data"/>.</summary>
    public static IValueConverter Converter { get; } = new UtilizationConverter();

    private sealed class UtilizationConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is Utilization u ? From(u, ThemeBrushes()) : null;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
