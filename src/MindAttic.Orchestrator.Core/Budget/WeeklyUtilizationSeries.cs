namespace MindAttic.Orchestrator.Budget;

/// <summary>
/// The current weekly window drawn the same way as the 5-hour <see cref="Utilization"/> (M.ORCH-T1403): reuses its record and
/// pace-curve math (<see cref="Utilization.PaceAt"/>, the target line, the projection), built from each
/// <see cref="UsageSample"/>'s <see cref="UsageSample.WeekPercent"/>/<see cref="UsageSample.WeekResetUtc"/> instead of its
/// 5-hour <c>Percent</c>/<c>ResetUtc</c>, over the week instead of the 5-hour block, and <see cref="WeeklyBurn.PerHour"/>'s
/// rate (already in week-points-per-hour, so no token conversion is needed) in place of the 5-hour control rate. Samples are
/// trimmed to 7 days (<see cref="ReportedLimit.Record"/>), exactly this window's length, so the chart's line spans the whole
/// week it has readings for.
/// </summary>
public static class WeeklyUtilizationSeries
{
    public static readonly TimeSpan WindowLength = TimeSpan.FromDays(7);

    /// <summary>The week against its pace, from <paramref name="samples"/>' weekly readings and <paramref name="b"/>'s own
    /// current weekly figure. With no weekly reset known yet, the last week on record (<see cref="LastWindow"/>) or an empty
    /// axis.</summary>
    public static Utilization Build(IReadOnlyList<UsageSample> samples, BudgetStatus b, double targetPercent, DateTime nowUtc)
    {
        if (b.WeekResetUtc is not { } reset || reset <= nowUtc) return LastWindow(samples, targetPercent, nowUtc);
        var start = reset - WindowLength;
        var points = Line(samples, start, reset, nowUtc);
        var now = Math.Max(points[^1].Percent, b.WeeklyPercent ?? points[^1].Percent);
        if (nowUtc > points[^1].AtUtc) points.Add(new UtilizationPoint(nowUtc, now));

        var rate = WeeklyBurn.PerHour(samples, reset, nowUtc);
        if (rate is not { } r || r <= 0)
        {
            // No rate learned yet: the line just holds where the week's own reporting puts it, same as the 5-hour chart's
            // fallback while no real limit is known.
            var flat = new Utilization(start, reset, nowUtc, targetPercent, points, now, 0, now, TimeSpan.Zero);
            return flat with { OverPace = OverPace(flat, points) };
        }
        var series = new Utilization(start, reset, nowUtc, targetPercent, points, now, r, 0, TimeSpan.Zero);
        var hoursLeft = Math.Max(0, (reset - nowUtc).TotalHours);
        return series with { ProjectedEndPercent = now + series.PercentPerHour * hoursLeft, OverPace = OverPace(series, points) };
    }

    /// <summary>The weekly window from <paramref name="startUtc"/> to <paramref name="endUtc"/> from 0 at its start through its
    /// readings up to <paramref name="nowUtc"/>, only ever rising (a lower later report is a stale restamp, never real).</summary>
    private static List<UtilizationPoint> Line(IReadOnlyList<UsageSample> samples, DateTime startUtc, DateTime endUtc, DateTime nowUtc)
    {
        var points = new List<UtilizationPoint> { new(startUtc, 0) };
        foreach (var s in samples.Where(s => s.WeekPercent is not null && s.WeekResetUtc is { } wr &&
                                             Math.Abs((wr - endUtc).TotalHours) <= 1 && s.AtUtc <= nowUtc && s.AtUtc >= startUtc)
                     .OrderBy(s => s.AtUtc))
        {
            var p = s.WeekPercent!.Value;
            if (p < points[^1].Percent) continue;
            points.Add(new UtilizationPoint(s.AtUtc, p));
        }
        return points;
    }

    /// <summary>No weekly reset known yet: the most recent week that has reset, drawn as it ended; with none on record, an
    /// empty weekly axis from now.</summary>
    private static Utilization LastWindow(IReadOnlyList<UsageSample> samples, double targetPercent, DateTime nowUtc)
    {
        var last = samples.Where(s => s.WeekPercent is not null && s.WeekResetUtc is { } r && r <= nowUtc && s.AtUtc <= nowUtc)
            .OrderByDescending(s => s.WeekResetUtc).FirstOrDefault();
        if (last is null)
            return new Utilization(nowUtc, nowUtc + WindowLength, nowUtc, targetPercent, [], 0, 0, 0, TimeSpan.Zero, Ended: true);
        var (start, end) = (last.WeekResetUtc!.Value - WindowLength, last.WeekResetUtc!.Value);
        var points = Line(samples, start, end, nowUtc);
        var ended = points[^1];
        var series = new Utilization(start, end, ended.AtUtc, targetPercent, points, ended.Percent, 0, ended.Percent, TimeSpan.Zero, Ended: true);
        return series with { OverPace = OverPace(series, points) };
    }

    private static TimeSpan OverPace(Utilization series, IReadOnlyList<UtilizationPoint> points)
    {
        var over = TimeSpan.Zero;
        for (var i = 1; i < points.Count; i++)
            if (points[i].Percent > series.PaceAt(points[i].AtUtc)) over += points[i].AtUtc - points[i - 1].AtUtc;
        return over;
    }
}
