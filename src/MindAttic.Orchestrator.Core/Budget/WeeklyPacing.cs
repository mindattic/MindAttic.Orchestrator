using System.Globalization;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Budget;

/// <summary>
/// How much of the week one 5-hour point costs, per login (M.ORCH-T666): learned from Claude's readings in which the 5-hour
/// percent and the week's percent rose together, within one 5-hour window of one week. Claude reports whole percents and the
/// week moves a point for every several of the 5 hours, so a window counts only once both rose; one where the week didn't
/// move is left out, which errs the ratio high (the week is taken to cost more), never low.
/// </summary>
public static class WeeklyRatio
{
    /// <summary>The 5-hour points the readings must have risen by, all windows together, before a ratio is trusted.</summary>
    public const double Min5HourRise = ReportedLimit.MinDelta;

    /// <summary>The week's points they must have risen by.</summary>
    public const double MinWeekRise = 1;

    /// <summary>Week points per 5-hour point; null until enough readings rose together.</summary>
    public static double? Learn(IEnumerable<UsageSample> samples)
    {
        double five = 0, week = 0;
        foreach (var window in samples.Where(s => s.WeekPercent is not null && s.WeekResetUtc is not null)
                     .GroupBy(s => (s.ResetUtc, s.WeekResetUtc)))
        {
            var ordered = window.OrderBy(s => s.AtUtc).ToList();
            // Only rises: a stale lower reading recorded late would end the window low.
            var first = ordered[0];
            var last = ordered.Aggregate(first, (best, s) => s.Percent >= best.Percent && s.WeekPercent >= best.WeekPercent ? s : best);
            var d5 = last.Percent - first.Percent;
            var dw = last.WeekPercent!.Value - first.WeekPercent!.Value;
            if (d5 <= 0 || dw <= 0) continue;
            five += d5;
            week += dw;
        }
        return five >= Min5HourRise && week >= MinWeekRise ? week / five : null;
    }
}

/// <summary>
/// How fast a login's week is actually being spent (M.ORCH-T669): its week points per hour over the last <see cref="Trailing"/> of
/// Claude's readings of this week, so weekly-aware pacing holds a window only when the week, spent the way it is being spent, would
/// reach its stop before it resets. Null until the readings span <see cref="MinSpan"/>: then the week is judged as if every window
/// ran to its target, which errs toward holding, never toward running the week out early.
/// </summary>
public static class WeeklyBurn
{
    /// <summary>How far back the rate is read.</summary>
    public static readonly TimeSpan Trailing = TimeSpan.FromHours(24);

    /// <summary>The readings must span at least this long (one 5-hour window) to be a rate.</summary>
    public static readonly TimeSpan MinSpan = BudgetCalculator.BlockLength;

    /// <summary>Week points per hour of the week resetting at <paramref name="weekResetUtc"/>; null without enough readings.</summary>
    public static double? PerHour(IEnumerable<UsageSample> samples, DateTime? weekResetUtc, DateTime nowUtc)
    {
        if (weekResetUtc is not { } reset) return null;
        var mine = samples.Where(s => s.WeekPercent is not null && s.WeekResetUtc is { } r && Math.Abs((r - reset).TotalHours) < 1 &&
                s.AtUtc <= nowUtc && nowUtc - s.AtUtc <= Trailing)
            .OrderBy(s => s.AtUtc).ToList();
        if (mine.Count < 2 || mine[^1].AtUtc - mine[0].AtUtc < MinSpan) return null;
        // The highest reading since, not the last: a stale lower one recorded late would read as the week going backwards.
        var rose = mine.Max(s => s.WeekPercent!.Value) - mine[0].WeekPercent!.Value;
        return Math.Max(0, rose) / (mine[^1].AtUtc - mine[0].AtUtc).TotalHours;
    }
}

/// <summary>
/// Where weekly-aware pacing (<see cref="OrchestratorSettings.WeeklyAwarePacing"/>, M.ORCH-T666, tuned by M.ORCH-T669) puts this
/// 5-hour window's ceiling. <see cref="CeilingPercent"/> is a percent of the 5-hour limit: the 5-hour target while the week isn't
/// held, lower while it is; null while the week isn't taken in (under its line, no reading, or no ratio learned), when the 5-hour
/// target alone applies.
/// </summary>
/// <param name="WeekPercent">The week's percent by Claude's meter; null with no reading.</param>
/// <param name="Ratio">Week points per 5-hour point (<see cref="WeeklyRatio"/>); null until learned.</param>
/// <param name="WindowsLeft">5-hour windows to the weekly reset, this one included (a part one counts as one).</param>
/// <param name="TargetPercent">The 5-hour pace target the ceiling is held to at most.</param>
public sealed record WeeklyCap(double? WeekPercent, DateTime? WeekResetUtc, double? Ratio, double? CeilingPercent, int WindowsLeft, double TargetPercent)
{
    /// <summary>Where the week lands and stops (<see cref="OrchestratorSettings.WeeklyAwareTarget"/>), as the settings had it.</summary>
    public double StopPercent { get; init; } = OrchestratorSettings.WeeklyAwareStopPercent;

    /// <summary>The line the week is taken in from (<see cref="OrchestratorSettings.WeeklyAwareLine"/>).</summary>
    public double LinePercent { get; init; } = OrchestratorSettings.WeeklyAwareLinePercent;

    /// <summary>Where the week winds down without a plan (<see cref="OrchestratorSettings.WeeklyAwareWindDown"/>).</summary>
    public double WindDownPercent { get; init; } = OrchestratorSettings.WeeklyAwareWindDownPercent;

    /// <summary>The week is at or past <see cref="LinePercent"/>: it's taken in.</summary>
    public bool Watching => WeekPercent >= LinePercent;

    /// <summary>The week holds this window under its 5-hour target.</summary>
    public bool Capped => CeilingPercent is { } c && c < TargetPercent;

    /// <summary>Hours from this window's start to the weekly reset.</summary>
    public double HoursLeft { get; init; }

    /// <summary>How fast the week is being spent (<see cref="WeeklyBurn"/>), week points per hour; null while not measured.</summary>
    public double? WeekPerHour { get; init; }

    /// <summary>Where the week is on course to end at its reset, unheld: spent as it's being spent (<see cref="WeekPerHour"/>), or
    /// with every window at its target while that isn't measured; null while the week isn't taken in.</summary>
    public double? ProjectedEndPercent { get; init; }

    /// <summary>Where this window is planned to end, in percent of its 5-hour limit: its ceiling, or its target.</summary>
    public double PlannedEndPercent => CeilingPercent ?? TargetPercent;

    /// <summary>Where the week is planned to end at its reset: its stop line while held (it lands there as it resets), else where
    /// it's on course for; null while the week isn't taken in.</summary>
    public double? PlannedWeekEndPercent => CeilingPercent is null ? null
        : Capped ? StopPercent : ProjectedEndPercent;

    /// <summary>The week's own room to its stop line in 5-hour points (what's left of the week, over the ratio); null while the
    /// week isn't taken in.</summary>
    public double? WeekRoom5HourPoints => Watching && Ratio is > 0 and var r && WeekPercent is { } w
        ? Math.Max(0, StopPercent - w) / r : null;

    /// <summary>Past <see cref="WindDownPercent"/> with no plan for the week (no ratio learned): only
    /// what can finish starts. With a plan, the window's own ceiling winds down instead, so the week isn't wound down hours before
    /// it resets while it still has room (M.ORCH-T669).</summary>
    public bool WindDown => WeekPercent >= WindDownPercent && CeilingPercent is null;

    /// <summary>What every pace note says of it, so it's plain the toggle is on and which limit governs.</summary>
    public string Note
    {
        get
        {
            var at = WeekPercent is { } w ? Pct(w) : null;
            if (at is null) return "Weekly-aware: no weekly reading yet, so only the 5-hour target applies.";
            if (!Watching) return $"Weekly-aware: the week is at {at}, under its {Pct(LinePercent)} line, so only the 5-hour target applies.";
            if (Ratio is null) return $"Weekly-aware: the week is at {at}, but how much of it a 5-hour point costs isn't learned yet, so only the 5-hour target applies.";
            var to = WeekResetUtc is { } r ? $" at its reset {Formatting.ResetTime(r, DateTime.UtcNow)}" : " at its reset";
            var course = ProjectedEndPercent is { } p
                ? $"on course for {Pct(p)}{to} " + (WeekPerHour is not null ? "as it's being spent" : "with every window at its target")
                : "";
            return Capped
                ? $"Weekly-aware: capped by the week: {Pct(CeilingPercent!.Value)} of this window (the week at {at}, {course}; " +
                  $"its room to {Pct(StopPercent)} is spread over the {Hours(HoursLeft)} left, so it lands there as it resets)."
                : $"Weekly-aware: nothing held: the week at {at} is {course}, under its {Pct(StopPercent)} stop, so this window keeps its {Pct(TargetPercent)} target.";
        }
    }

    /// <summary>The dashboard's short form: "capped by the week: 31% of this window"; empty when not capped.</summary>
    public string Short => Capped ? $"Capped by the week: {Pct(CeilingPercent!.Value)} of this window" : "";

    /// <summary>The plan in a line, for the dashboard and the log (M.ORCH-T669): "planned: this window to 31%, the week to 98% at
    /// its reset"; empty while the week isn't taken in.</summary>
    public string Plan => CeilingPercent is null ? "" :
        $"planned: this window to {Pct(PlannedEndPercent)}" + (PlannedWeekEndPercent is { } w ? $", the week to {Pct(w)} at its reset" : "");

    internal static string Pct(double p) => p.ToString("0", CultureInfo.InvariantCulture) + "%";

    private static string Hours(double h) => h >= 48
        ? (h / 24).ToString("0.#", CultureInfo.InvariantCulture) + " days"
        : h.ToString("0.#", CultureInfo.InvariantCulture) + " hours";
}

/// <summary>Works out <see cref="WeeklyCap"/> for one login from its budget (M.ORCH-T666, M.ORCH-T669).</summary>
public static class WeeklyPacing
{
    /// <summary>
    /// The window's ceiling, measured per login against its own weekly reset. Under <see cref="OrchestratorSettings.WeeklyAwareLine"/>
    /// nothing is held. Past it, the week's room to its target (<see cref="OrchestratorSettings.WeeklyAwareTarget"/>) as of this window's
    /// start is weighed against what the week would spend to its reset unheld: as it's being spent (<paramref name="weekPerHour"/>),
    /// or, while that isn't measured, with every window at its 5-hour target (never more than that). With room for it nothing is
    /// held: the window keeps its target. Without, the hold grows only as far as needed: the room is spread over the hours left
    /// to the reset, this window getting its part of them, so the last window (cut short by the reset) gets all that's left and
    /// the pacer spends it to the reset itself (<see cref="Pacer.Compute"/>): the week lands at its stop as it resets, not hours
    /// early. Never past what would take the week itself to the stop. Worked out from the window's start (the week then is the
    /// week now less what this window cost it), so it holds still while the window spends. Null when the toggle is off.
    /// </summary>
    public static WeeklyCap? Cap(BudgetStatus b, double? ratio, OrchestratorSettings s, DateTime nowUtc, double? weekPerHour = null)
    {
        if (!s.WeeklyAwarePacing) return null;
        double target = Math.Clamp(s.PaceTargetPercent, 1, 100);
        var week = b.WeeklyPercent;
        var reset = b.WeekResetUtc;
        var start = b.CurrentBlock is { } block && block.IsActive(nowUtc) ? block.StartUtc : nowUtc;
        var length = BudgetCalculator.BlockLength.TotalHours;
        var hours = reset is { } r ? Math.Max(1.0 / 60, (r - start).TotalHours) : length;
        var windows = Math.Max(1, (int)Math.Ceiling(hours / length - 1e-9));
        var stop = (double)s.WeeklyAwareTarget;
        if (week is not { } w || w < s.WeeklyAwareLine || ratio is not > 0 || b.BlockLimitIsPeak || reset is null)
            return new WeeklyCap(week, reset, ratio is > 0 && !b.BlockLimitIsPeak && reset is not null ? ratio : null, null, windows, target)
                { HoursLeft = hours, WeekPerHour = weekPerHour, StopPercent = stop, LinePercent = s.WeeklyAwareLine, WindDownPercent = s.WeeklyAwareWindDown };
        var used = b.CurrentBlock is { } current && current.IsActive(nowUtc) ? b.BlockPercent : 0;
        var weekAtStart = Math.Max(0, w - ratio.Value * used);
        var room = Math.Max(0, stop - weekAtStart);
        // What the week would spend from this window's start to its reset, unheld: at its measured rate, never more than every
        // window at its target would.
        var fullPace = target * ratio.Value * hours / length;
        var need = weekPerHour is { } rate ? Math.Min(fullPace, Math.Max(0, rate) * hours) : fullPace;
        var toStop = used + Math.Max(0, stop - w) / ratio.Value;
        var share = room * Math.Min(length, hours) / hours / ratio.Value;
        var ceiling = Math.Min(target, need <= room ? toStop : Math.Min(share, toStop));
        return new WeeklyCap(w, reset, ratio, Math.Max(0, ceiling), windows, target)
            {
                HoursLeft = hours, WeekPerHour = weekPerHour, ProjectedEndPercent = weekAtStart + need,
                StopPercent = stop, LinePercent = s.WeeklyAwareLine, WindDownPercent = s.WeeklyAwareWindDown,
            };
    }
}
