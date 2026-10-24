namespace MindAttic.Orchestrator.Budget;

/// <summary>One reading of the 5-hour window: how much of it was used by then.</summary>
public sealed record UtilizationPoint(DateTime AtUtc, double Percent);

/// <summary>
/// The current 5-hour window drawn against its pace: the pace line runs from 0 at the window's start to
/// <see cref="TargetPercent"/> at its reset, straight on the linear profile and bowed up (target × elapsed^<see cref="Exponent"/>)
/// on the front-loaded one (M.ORCH-T234), so a reading above it is spending faster than the window can sustain
/// (over) and one below it slower (under). <see cref="ProjectedEndPercent"/> is where the recent rate lands by the reset.
/// <see cref="Ended"/> while no window is running (M.ORCH-T502): then it's the last window as it ended (its line, pace and
/// last reading, <see cref="NowPercent"/> at <see cref="NowUtc"/>), or with no <see cref="Actual"/> points an empty axis
/// when there's no window on record. <see cref="Idle"/> while nothing runs on the login (M.ORCH-T629): a real running
/// window keeps its own climbing <see cref="Actual"/>, against the target only; without one, a flat line at the roster's
/// last reading across the window (<see cref="UtilizationSeries.IdleWindow"/>, M.ORCH-T787).
/// </summary>
public sealed record Utilization(DateTime StartUtc, DateTime EndUtc, DateTime NowUtc, double TargetPercent,
    IReadOnlyList<UtilizationPoint> Actual, double NowPercent, double PercentPerHour, double ProjectedEndPercent, TimeSpan OverPace,
    double Exponent = 1, bool Ended = false, bool Idle = false, bool Unstarted = false)
{
    /// <summary>Ended with a window on record to draw (not just an empty axis).</summary>
    public bool HasLastWindow => Ended && Actual.Count > 0;

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
        : PercentPerHour > 0 ? Projection.Before(NowUtc, (100 - NowPercent) / PercentPerHour, EndUtc) : null;
}

public static class UtilizationSeries
{
    /// <summary>The most points kept for the line; readings closer together than this are thinned.</summary>
    private static readonly TimeSpan MinSpacing = TimeSpan.FromMinutes(1);

    /// <summary>
    /// The current window's line from Claude's own reports (<paramref name="samples"/> of that window, only ever rising:
    /// a lower one is a stale report from an idle session) and the budget's figure now. Null without a real 5-hour limit while a window
    /// runs; with none running, the last window as it ended (or an empty axis), never null.
    /// </summary>
    /// <param name="exponent">The pace curve's exponent (<see cref="Settings.OrchestratorSettings.PaceExponent"/>): 1 draws the straight line.</param>
    public static Utilization? Build(IReadOnlyList<UsageSample> samples, BudgetStatus b, double targetPercent, DateTime nowUtc, double exponent = 1)
    {
        // Between windows the graph stays up with the last one as it ended (M.ORCH-T502): Claude's own percents, so no
        // token limit is needed to draw it.
        if (b.CurrentBlock is not { } block) return LastWindow(samples, targetPercent, nowUtc, exponent);
        if (b.BlockLimitIsPeak || b.BlockLimit <= 0) return null;
        var points = Line(samples, block.StartUtc, block.EndUtc, nowUtc);
        var now = Math.Max(points[^1].Percent, b.BlockPercent);
        if (nowUtc > points[^1].AtUtc) points.Add(new(nowUtc, now));

        // The rate the projection carries forward is never under what the last few minutes show (M.ORCH-T783): a fresh
        // burst across a login's CLIs and subagents moves ControlFreshPerHour within minutes, while the 30-minute
        // RecentFreshPerHour it's steadied against still carries the quieter stretch before it and can read "under target"
        // for a long stretch of a burst already running hot. The pace hook's guidance, the subagent allowance raise and
        // this same chart all read this one number, so understating it here let them all say "start more" while the
        // window was already on course past its target.
        var rate = Math.Max(b.RecentFreshPerHour, b.ControlFreshPerHour);
        var series = new Utilization(block.StartUtc, block.EndUtc, nowUtc, targetPercent, points, now,
            rate / b.BlockLimit * 100, 0, TimeSpan.Zero, exponent);
        var over = OverPace(series, points);
        var hoursLeft = Math.Max(0, (block.EndUtc - nowUtc).TotalHours);
        return series with { ProjectedEndPercent = now + series.PercentPerHour * hoursLeft, OverPace = over };
    }

    /// <summary>The window from <paramref name="startUtc"/> to <paramref name="endUtc"/> from 0 at its start through its readings
    /// up to <paramref name="nowUtc"/>, only ever rising. Behind a wall the block ends at the limit message's reset (whole minutes,
    /// local clock), not Claude's resets_at to the second: the same window when within 10 minutes, as the pace ledger takes it.</summary>
    private static List<UtilizationPoint> Line(IReadOnlyList<UsageSample> samples, DateTime startUtc, DateTime endUtc, DateTime nowUtc)
    {
        var points = new List<UtilizationPoint> { new(startUtc, 0) };
        foreach (var s in samples.Where(s => Math.Abs((s.ResetUtc - endUtc).TotalMinutes) <= 10 && s.AtUtc <= nowUtc &&
                                             s.AtUtc >= startUtc).OrderBy(s => s.AtUtc))
        {
            if (s.Percent < points[^1].Percent) continue;
            if (s.AtUtc - points[^1].AtUtc < MinSpacing && points.Count > 1) points[^1] = new(s.AtUtc, s.Percent);
            else points.Add(new(s.AtUtc, s.Percent));
        }
        return points;
    }

    /// <summary>
    /// No window running: the most recent one that has reset, drawn as it ended (its line to the last reading, the pace,
    /// <see cref="Utilization.NowPercent"/> its last reading). With none on record, an empty 5-hour axis from now. Never null,
    /// so the card stays up between windows.
    /// </summary>
    private static Utilization LastWindow(IReadOnlyList<UsageSample> samples, double targetPercent, DateTime nowUtc, double exponent)
    {
        var last = samples.Where(s => s.ResetUtc <= nowUtc && s.AtUtc <= nowUtc).OrderByDescending(s => s.ResetUtc).FirstOrDefault();
        if (last is null)
            return new Utilization(nowUtc, nowUtc + BudgetCalculator.BlockLength, nowUtc, targetPercent, [], 0, 0, 0, TimeSpan.Zero, exponent, Ended: true);
        var (start, end) = (last.ResetUtc - BudgetCalculator.BlockLength, last.ResetUtc);
        var points = Line(samples, start, end, nowUtc);
        var ended = points[^1];
        var series = new Utilization(start, end, ended.AtUtc, targetPercent, points, ended.Percent, 0, ended.Percent, TimeSpan.Zero, exponent, Ended: true);
        return series with { OverPace = OverPace(series, points) };
    }

    /// <summary>
    /// Nothing running on the login (M.ORCH-T629): its window drawn as a flat line at <paramref name="percent"/> from the
    /// window's start (<paramref name="resetUtc"/> less 5 hours) to its reset, against <paramref name="targetPercent"/>. A
    /// window that has reset or was never read (<paramref name="resetUtc"/> null or past) is the next 5 hours from now at 0%,
    /// <see cref="Utilization.Unstarted"/>.
    /// </summary>
    public static Utilization IdleWindow(DateTime? resetUtc, double percent, double targetPercent, DateTime nowUtc)
    {
        var unstarted = resetUtc is not { } r || r <= nowUtc;
        var (start, end) = unstarted ? (nowUtc, nowUtc + BudgetCalculator.BlockLength) : (resetUtc!.Value - BudgetCalculator.BlockLength, resetUtc.Value);
        var level = unstarted ? 0 : Math.Max(0, percent);
        return new Utilization(start, end, nowUtc, targetPercent, [new(start, level), new(end, level)], level, 0, level, TimeSpan.Zero,
            Idle: true, Unstarted: unstarted);
    }

    private static TimeSpan OverPace(Utilization series, IReadOnlyList<UtilizationPoint> points)
    {
        var over = TimeSpan.Zero;
        for (var i = 1; i < points.Count; i++)
            if (points[i].Percent > series.PaceAt(points[i].AtUtc)) over += points[i].AtUtc - points[i - 1].AtUtc;
        return over;
    }
}
