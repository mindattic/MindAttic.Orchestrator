namespace MindAttic.Orchestrator.Budget;

/// <summary>One reading of the 5-hour window: how much of it was used by then.</summary>
public sealed record UtilizationPoint(DateTime AtUtc, double Percent);

/// <summary>
/// The current 5-hour window drawn against its pace: the pace line runs from 0 at the window's start to
/// <see cref="TargetPercent"/> at its reset, straight on the linear profile and bowed up (target × elapsed^<see cref="Exponent"/>)
/// on the front-loaded one (M.ORCH-T234), so a reading above it is spending faster than the window can sustain
/// (over) and one below it slower (under). <see cref="ProjectedEndPercent"/> is where the recent rate lands by the reset.
/// </summary>
public sealed record Utilization(DateTime StartUtc, DateTime EndUtc, DateTime NowUtc, double TargetPercent,
    IReadOnlyList<UtilizationPoint> Actual, double NowPercent, double PercentPerHour, double ProjectedEndPercent, TimeSpan OverPace,
    double Exponent = 1)
{
    /// <summary>Where the pace line is at <paramref name="atUtc"/>.</summary>
    public double PaceAt(DateTime atUtc) =>
        EndUtc <= StartUtc ? TargetPercent : TargetPercent * Curve(Math.Clamp((atUtc - StartUtc) / (EndUtc - StartUtc), 0, 1));

    /// <summary>The share of the target the pace line is at <paramref name="elapsed"/> (0-1) of the way through the window.</summary>
    public double Curve(double elapsed) => Exponent >= 1 ? elapsed : Math.Pow(elapsed, Exponent);

    /// <summary>The pace line is a curve (the front-loaded profile), not a straight line.</summary>
    public bool Curved => Exponent < 1;

    public bool OverNow => NowPercent > PaceAt(NowUtc);

    /// <summary>When the recent rate reaches 100%, if that's before the reset.</summary>
    public DateTime? HitsLimitUtc => NowPercent >= 100 ? NowUtc
        : PercentPerHour > 0 && NowUtc + TimeSpan.FromHours((100 - NowPercent) / PercentPerHour) is var at && at < EndUtc ? at : null;
}

public static class UtilizationSeries
{
    /// <summary>The most points kept for the line; readings closer together than this are thinned.</summary>
    private static readonly TimeSpan MinSpacing = TimeSpan.FromMinutes(1);

    /// <summary>
    /// The current window's line from Claude's own reports (<paramref name="samples"/> of that window, only ever rising:
    /// a lower one is a stale report from an idle session) and the budget's figure now. Null without a real 5-hour limit.
    /// </summary>
    /// <param name="exponent">The pace curve's exponent (<see cref="Settings.OrchestratorSettings.PaceExponent"/>): 1 draws the straight line.</param>
    public static Utilization? Build(IReadOnlyList<UsageSample> samples, BudgetStatus b, double targetPercent, DateTime nowUtc, double exponent = 1)
    {
        if (b.BlockLimitIsPeak || b.BlockLimit <= 0 || b.CurrentBlock is not { } block) return null;
        var points = new List<UtilizationPoint> { new(block.StartUtc, 0) };
        // Behind a wall the block ends at the limit message's reset (whole minutes, local clock), not Claude's resets_at
        // to the second: the same window when within 10 minutes, as the pace ledger takes it.
        foreach (var s in samples.Where(s => Math.Abs((s.ResetUtc - block.EndUtc).TotalMinutes) <= 10 && s.AtUtc <= nowUtc &&
                                             s.AtUtc >= block.StartUtc).OrderBy(s => s.AtUtc))
        {
            if (s.Percent < points[^1].Percent) continue;
            if (s.AtUtc - points[^1].AtUtc < MinSpacing && points.Count > 1) points[^1] = new(s.AtUtc, s.Percent);
            else points.Add(new(s.AtUtc, s.Percent));
        }
        var now = Math.Max(points[^1].Percent, b.BlockPercent);
        if (nowUtc > points[^1].AtUtc) points.Add(new(nowUtc, now));

        var series = new Utilization(block.StartUtc, block.EndUtc, nowUtc, targetPercent, points, now,
            b.RecentFreshPerHour / b.BlockLimit * 100, 0, TimeSpan.Zero, exponent);
        var over = TimeSpan.Zero;
        for (var i = 1; i < points.Count; i++)
            if (points[i].Percent > series.PaceAt(points[i].AtUtc)) over += points[i].AtUtc - points[i - 1].AtUtc;
        var hoursLeft = Math.Max(0, (block.EndUtc - nowUtc).TotalHours);
        return series with { ProjectedEndPercent = now + series.PercentPerHour * hoursLeft, OverPace = over };
    }
}
