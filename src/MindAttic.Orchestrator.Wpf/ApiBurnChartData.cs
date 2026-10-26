using System.Globalization;
using System.Windows.Data;
using MindAttic.Components.Wpf.Controls;
using MindAttic.Orchestrator.Billing;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// The API key person's money as a burndown on the library's <see cref="MaLineChart"/> (M.ORCH-T906), in place of the 5-hour
/// chart a Claude login has: a downhill line from its starting credit (100%) to what's left now, each point the share of the
/// credit left after that hour's spend, and, dotted, where the recent rate takes it, the way the 5-hour chart projects "at
/// reset": to $0 where it runs out within the chart's span, else to what's left at its end. The chart's axis is a percent, so
/// the labels carry the dollars. With no starting credit set there's nothing to burn down: just the empty axis, and the caption
/// says how to set one.
/// </summary>
public static class ApiBurnChartData
{
    /// <summary>How far past now the chart reaches when the credit doesn't run out sooner.</summary>
    public static readonly TimeSpan Ahead = TimeSpan.FromDays(3);

    /// <summary>What the chart draws for <paramref name="spend"/> at <paramref name="nowUtc"/>.</summary>
    public static MaLineChartData From(ApiSpend spend, DateTime nowUtc, UtilizationChartData.ChartBrushes brushes)
    {
        var start = spend.SinceUtc ?? (spend.Burn.Count > 0 ? spend.Burn[0].AtUtc : nowUtc.AddDays(-1));
        if (start >= nowUtc) start = nowUtc.AddHours(-1);
        if (spend.CreditUsd is not { } credit || credit <= 0) return new MaLineChartData(start, nowUtc + Ahead, []);

        double Left(double spent) => Math.Clamp((credit - spent) / credit * 100, 0, 100);
        var runsOut = spend.RunsOutUtc(nowUtc);
        var end = runsOut is { } r && r < nowUtc + Ahead ? r : nowUtc + Ahead;
        if (end <= nowUtc) end = nowUtc.AddHours(1);

        var leftNow = Left(spend.SpentUsd);
        var lineBrush = leftNow <= 0 ? brushes.Bad : leftNow < 25 ? brushes.Warn : brushes.Good;
        var points = new List<MaChartPoint> { new(start, 100, lineBrush) };
        points.AddRange(spend.Burn.Where(p => p.AtUtc > start && p.AtUtc <= nowUtc).Select(p => new MaChartPoint(p.AtUtc, Left(p.SpentUsd), lineBrush)));
        if (points[^1].AtUtc < nowUtc) points.Add(new MaChartPoint(nowUtc, leftNow, lineBrush));

        var label = $"{ApiSpend.Usd(Math.Max(0, credit - spend.SpentUsd))} left";
        if (leftNow <= 0 || spend.PerHourUsd <= 0) return new MaLineChartData(start, end, points, label);
        var projectedLeftUsd = runsOut is { } at && at <= end ? 0 : Math.Max(0, credit - spend.SpentUsd - spend.PerHourUsd * (end - nowUtc).TotalHours);
        var projected = projectedLeftUsd / credit * 100;
        var lands = projected <= 0 ? brushes.Bad : projected < 25 ? brushes.Warn : brushes.Good;
        var endLabel = projectedLeftUsd <= 0 && runsOut is { } o
            ? $"runs out {o.ToLocalTime().ToString("ddd h:mm tt", CultureInfo.CurrentCulture)}"
            : $"{ApiSpend.Usd(projectedLeftUsd)} by {end.ToLocalTime().ToString("ddd", CultureInfo.CurrentCulture)}";
        return new MaLineChartData(start, end, points, label, new MaChartProjection(end, projected, lands, endLabel));
    }

    /// <summary>Binds the view model's <see cref="ApiSpend"/> to <see cref="MaLineChart.Data"/>.</summary>
    public static IValueConverter Converter { get; } = new SpendConverter();

    private sealed class SpendConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            try { return value is ApiSpend s ? From(s, DateTime.UtcNow, UtilizationChartData.ThemeBrushes()) : null; }
            catch (Exception ex) when (ex is not OutOfMemoryException) { return null; } // a chart never takes the dashboard down
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
