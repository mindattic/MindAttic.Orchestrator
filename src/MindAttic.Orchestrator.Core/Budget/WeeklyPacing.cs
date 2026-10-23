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
/// Where weekly-aware pacing (<see cref="OrchestratorSettings.WeeklyAwarePacing"/>, M.ORCH-T666) puts this 5-hour window's ceiling.
/// <see cref="CeilingPercent"/> is a percent of the 5-hour limit; null while the week isn't taken in (under its line, no reading,
/// or no ratio learned), when the 5-hour target alone applies.
/// </summary>
/// <param name="WeekPercent">The week's percent by Claude's meter; null with no reading.</param>
/// <param name="Ratio">Week points per 5-hour point (<see cref="WeeklyRatio"/>); null until learned.</param>
/// <param name="WindowsLeft">5-hour windows to the weekly reset, this one included.</param>
/// <param name="TargetPercent">The 5-hour pace target the ceiling is held to at most.</param>
public sealed record WeeklyCap(double? WeekPercent, DateTime? WeekResetUtc, double? Ratio, double? CeilingPercent, int WindowsLeft, double TargetPercent)
{
    /// <summary>The week is at or past <see cref="OrchestratorSettings.WeeklyAwareLinePercent"/>: it's taken in.</summary>
    public bool Watching => WeekPercent >= OrchestratorSettings.WeeklyAwareLinePercent;

    /// <summary>The week holds this window under its 5-hour target.</summary>
    public bool Capped => CeilingPercent is { } c && c < TargetPercent;

    /// <summary>The week's own room to its stop line in 5-hour points (what's left of the week, over the ratio); null while the
    /// week isn't taken in.</summary>
    public double? WeekRoom5HourPoints => Watching && Ratio is > 0 and var r && WeekPercent is { } w
        ? Math.Max(0, OrchestratorSettings.WeeklyAwareStopPercent - w) / r : null;

    /// <summary>Past <see cref="OrchestratorSettings.WeeklyAwareWindDownPercent"/>: only what can finish starts.</summary>
    public bool WindDown => WeekPercent >= OrchestratorSettings.WeeklyAwareWindDownPercent;

    /// <summary>What every pace note says of it, so it's plain the toggle is on and which limit governs.</summary>
    public string Note
    {
        get
        {
            var at = WeekPercent is { } w ? Pct(w) : null;
            if (at is null) return "Weekly-aware: no weekly reading yet, so only the 5-hour target applies.";
            if (!Watching) return $"Weekly-aware: the week is at {at}, under its {OrchestratorSettings.WeeklyAwareLinePercent}% line, so only the 5-hour target applies.";
            if (Ratio is null) return $"Weekly-aware: the week is at {at}, but how much of it a 5-hour point costs isn't learned yet, so only the 5-hour target applies.";
            var spread = $"the week at {at}, its room to {OrchestratorSettings.WeeklyAwareStopPercent}% spread over {WindowsLeft} " +
                $"5-hour window{(WindowsLeft == 1 ? "" : "s")}" + (WeekResetUtc is { } r ? $" to its reset {Formatting.ResetTime(r, DateTime.UtcNow)}" : "");
            return Capped
                ? $"Weekly-aware: capped by the week: {Pct(CeilingPercent!.Value)} of this window ({spread})."
                : $"Weekly-aware: the week has room for this window's {Pct(TargetPercent)} target ({spread}).";
        }
    }

    /// <summary>The dashboard's short form: "capped by the week: 31% of this window"; empty when not capped.</summary>
    public string Short => Capped ? $"Capped by the week: {Pct(CeilingPercent!.Value)} of this window" : "";

    internal static string Pct(double p) => p.ToString("0", CultureInfo.InvariantCulture) + "%";
}

/// <summary>Works out <see cref="WeeklyCap"/> for one login from its budget (M.ORCH-T666).</summary>
public static class WeeklyPacing
{
    /// <summary>
    /// The window's ceiling: the smaller of the 5-hour target and the week's room to
    /// <see cref="OrchestratorSettings.WeeklyAwareStopPercent"/> as of this window's start, spread over the 5-hour windows left
    /// before the weekly reset (this one included), in 5-hour points (over <paramref name="ratio"/>); never past what would take
    /// the week itself to the stop. Worked out from the window's start (the week then is the week now less what this window
    /// cost it), so it holds still while the window spends instead of moving up with every point spent. Null when the toggle is
    /// off: nothing changes.
    /// </summary>
    public static WeeklyCap? Cap(BudgetStatus b, double? ratio, OrchestratorSettings s, DateTime nowUtc)
    {
        if (!s.WeeklyAwarePacing) return null;
        double target = Math.Clamp(s.PaceTargetPercent, 1, 100);
        var week = b.WeeklyPercent;
        var reset = b.WeekResetUtc;
        var start = b.CurrentBlock is { } block && block.IsActive(nowUtc) ? block.StartUtc : nowUtc;
        var windows = reset is { } r ? Math.Max(1, (int)Math.Ceiling((r - start).TotalHours / BudgetCalculator.BlockLength.TotalHours - 1e-9)) : 1;
        if (week is not { } w || w < OrchestratorSettings.WeeklyAwareLinePercent || ratio is not > 0 || b.BlockLimitIsPeak || reset is null)
            return new WeeklyCap(week, reset, ratio is > 0 && !b.BlockLimitIsPeak && reset is not null ? ratio : null, null, windows, target);
        var used = b.CurrentBlock is { } current && current.IsActive(nowUtc) ? b.BlockPercent : 0;
        var stop = (double)OrchestratorSettings.WeeklyAwareStopPercent;
        var weekAtStart = Math.Max(0, w - ratio.Value * used);
        var perWindow = Math.Max(0, stop - weekAtStart) / windows / ratio.Value;
        var toStop = used + Math.Max(0, stop - w) / ratio.Value;
        var ceiling = Math.Max(0, Math.Min(target, Math.Min(perWindow, toStop)));
        return new WeeklyCap(w, reset, ratio, ceiling, windows, target);
    }
}
