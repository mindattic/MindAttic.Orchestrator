namespace MindAttic.Orchestrator.Budget;

/// <summary>Uncached tokens one repo spent with one provider on one local day, and whose they were.</summary>
/// <param name="Account">For Claude, the account signed in when they were spent (<see cref="ClaudeAccount.Current"/>'s key), or
/// <see cref="UsageHistory.BilledElsewhere"/> and how for a session billed to an API key, a gateway or a cloud provider; null
/// when not known (another provider, no sign-in recorded yet, or a day kept from before accounts were).</param>
/// <param name="BeforeUtc">On the day a week reset fell, the reset when these are the tokens spent before it (the last week's),
/// kept apart from the rest of that day's (the new week's); null for any other day's, or the reset day's after it.</param>
public sealed record DayUsage(DateOnly Day, string Repo, string Provider, long Fresh, string? Account = null, DateTime? BeforeUtc = null);

/// <summary>
/// Usage by day, repo and provider, kept for a year so reports and the weekly burndown reach past the
/// 7 days of transcripts. Days still inside the transcript window are recomputed every refresh; older
/// days are kept as they were last seen.
/// </summary>
public static class UsageHistory
{
    public static readonly TimeSpan Keep = TimeSpan.FromDays(366);

    /// <summary>What <see cref="DayUsage.Account"/> starts with for a session billed elsewhere than the subscription; never an account's key.</summary>
    public const string BilledElsewhere = "billed:";

    public static bool IsBilledElsewhere(string? account) => account?.StartsWith(BilledElsewhere, StringComparison.Ordinal) == true;

    /// <param name="accountOf">Whose each event's tokens were (see <see cref="DayUsage.Account"/>); null tags none.</param>
    /// <param name="weekStartUtc">The week's reset: its day's tokens spent before it are kept apart (<see cref="DayUsage.BeforeUtc"/>),
    /// so the burndown's first bar counts from the reset as the week's total does, not from that day's midnight (M.ORCH-T388).</param>
    public static IEnumerable<DayUsage> Observe(IEnumerable<TranscriptSession> sessions, Func<string?, string> repoOf, DateTime sinceUtc,
        Func<TranscriptSession, UsageEvent, string?>? accountOf = null, DateTime? weekStartUtc = null)
    {
        var resetDay = weekStartUtc is { } w ? DateOnly.FromDateTime(w.ToLocalTime()) : (DateOnly?)null;
        return sessions
            .SelectMany(s => s.Events.Where(e => e.AtUtc >= sinceUtc).Select(e =>
            {
                var day = DateOnly.FromDateTime(e.AtUtc.ToLocalTime());
                return (Day: day, Repo: repoOf(s.Cwd), s.Provider, e.Usage.Fresh, Account: accountOf?.Invoke(s, e),
                    Before: day == resetDay && e.AtUtc < weekStartUtc ? weekStartUtc : null);
            }))
            .GroupBy(x => (x.Day, x.Repo, x.Provider, x.Account, x.Before))
            .Select(g => new DayUsage(g.Key.Day, g.Key.Repo, g.Key.Provider, g.Sum(x => x.Fresh), g.Key.Account, g.Key.Before));
    }

    /// <summary>Replaces every day on or after <paramref name="windowStart"/> with what the transcripts show now; keeps the rest.</summary>
    public static List<DayUsage> Merge(IEnumerable<DayUsage> stored, IEnumerable<DayUsage> observed, DateOnly windowStart, DateOnly today) =>
        [.. stored.Where(d => d.Day < windowStart && d.Day > today.AddDays(-(int)Keep.TotalDays))
            .Concat(observed.Where(d => d.Day >= windowStart))
            .OrderBy(d => d.Day).ThenBy(d => d.Repo, StringComparer.OrdinalIgnoreCase).ThenBy(d => d.Provider, StringComparer.Ordinal)
            .ThenBy(d => d.Account, StringComparer.Ordinal).ThenBy(d => d.BeforeUtc)];
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
    /// <remarks>The bars count what <paramref name="budget"/>'s week does: only <paramref name="account"/>'s tokens when the
    /// signed-in account is known (the budget counts only its spans), and never a session billed elsewhere. They used to count
    /// every Claude token, so after a /login, or with an API-key CLI running, they disagreed with the week's total (M.ORCH-T372).
    /// The first bar counts from the reset, not the whole day it fell on: the day's tokens spent before it (<see cref="DayUsage.BeforeUtc"/>)
    /// were the last week's, which the total leaves out too (M.ORCH-T388).</remarks>
    public static Burndown Compute(BudgetStatus budget, IEnumerable<DayUsage> history, DateTime nowUtc, string? account = null)
    {
        var start = DateOnly.FromDateTime(budget.WeekStartUtc.ToLocalTime());
        var today = DateOnly.FromDateTime(nowUtc.ToLocalTime());
        var byDay = history.Where(d => d.Provider == Providers.Claude && d.Day >= start && !(d.BeforeUtc <= budget.WeekStartUtc) &&
                (account is null ? !UsageHistory.IsBilledElsewhere(d.Account) : d.Account == account))
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
