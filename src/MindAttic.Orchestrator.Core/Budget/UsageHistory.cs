namespace MindAttic.Orchestrator.Budget;

/// <summary>Uncached tokens one repo spent with one provider on one local day.</summary>
public sealed record DayUsage(DateOnly Day, string Repo, string Provider, long Fresh);

/// <summary>
/// Usage by day, repo and provider, kept for a year so reports and the weekly burndown reach past the
/// 7 days of transcripts. Days still inside the transcript window are recomputed every refresh; older
/// days are kept as they were last seen.
/// </summary>
public static class UsageHistory
{
    public static readonly TimeSpan Keep = TimeSpan.FromDays(366);

    public static IEnumerable<DayUsage> Observe(IEnumerable<TranscriptSession> sessions, Func<string?, string> repoOf, DateTime sinceUtc) =>
        sessions
            .SelectMany(s => s.Events.Where(e => e.AtUtc >= sinceUtc).Select(e => (Day: DateOnly.FromDateTime(e.AtUtc.ToLocalTime()), Repo: repoOf(s.Cwd), s.Provider, e.Usage.Fresh)))
            .GroupBy(x => (x.Day, x.Repo, x.Provider))
            .Select(g => new DayUsage(g.Key.Day, g.Key.Repo, g.Key.Provider, g.Sum(x => x.Fresh)));

    /// <summary>Replaces every day on or after <paramref name="windowStart"/> with what the transcripts show now; keeps the rest.</summary>
    public static List<DayUsage> Merge(IEnumerable<DayUsage> stored, IEnumerable<DayUsage> observed, DateOnly windowStart, DateOnly today) =>
        [.. stored.Where(d => d.Day < windowStart && d.Day > today.AddDays(-(int)Keep.TotalDays))
            .Concat(observed.Where(d => d.Day >= windowStart))
            .OrderBy(d => d.Day).ThenBy(d => d.Repo, StringComparer.OrdinalIgnoreCase).ThenBy(d => d.Provider, StringComparer.Ordinal)];
}

/// <summary>The week so far, day by day, and where it ends at the current pace.</summary>
public sealed record Burndown(
    IReadOnlyList<(DateOnly Day, long Fresh)> Days,
    long Used,
    long? Limit,
    DateTime? ResetUtc,
    double PerHour,
    DateTime? ExhaustedUtc)
{
    /// <summary>At this pace the weekly limit runs out before the week resets.</summary>
    public bool RunsOut => ExhaustedUtc is { } at && ResetUtc is { } reset && at < reset;

    /// <summary>Claude usage by day since the week started; the pace is the average over the week so far (at least a day of it).</summary>
    public static Burndown Compute(BudgetStatus budget, IEnumerable<DayUsage> history, DateTime nowUtc)
    {
        var start = DateOnly.FromDateTime(budget.WeekStartUtc.ToLocalTime());
        var today = DateOnly.FromDateTime(nowUtc.ToLocalTime());
        var byDay = history.Where(d => d.Provider == Providers.Claude && d.Day >= start)
            .GroupBy(d => d.Day).ToDictionary(g => g.Key, g => g.Sum(d => d.Fresh));
        var days = new List<(DateOnly, long)>();
        for (var d = start; d <= today; d = d.AddDays(1)) days.Add((d, byDay.GetValueOrDefault(d)));

        var used = budget.Week.Fresh;
        var hours = Math.Max(24, (nowUtc - budget.WeekStartUtc).TotalHours);
        var perHour = used / hours;
        DateTime? exhausted = budget.WeeklyLimit is > 0 and var limit && perHour > 0
            ? nowUtc + TimeSpan.FromHours(Math.Max(0, limit - used) / perHour)
            : null;
        return new Burndown(days, used, budget.WeeklyLimit, budget.WeekResetUtc, perHour, exhausted);
    }
}
