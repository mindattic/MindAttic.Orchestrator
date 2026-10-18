namespace MindAttic.Orchestrator.Budget;

/// <summary>
/// When a pace reaches a limit, guarded against the overflow a tiny rate gives: hours left divided by a few tokens an hour
/// is past what a <see cref="TimeSpan"/> or <see cref="DateTime"/> can hold, and adding it threw, crashing the weekly
/// burndown on a week that had used under about 500 tokens (M.ORCH-T448). Such a moment is no projection (null).
/// </summary>
public static class Projection
{
    /// <summary><paramref name="nowUtc"/> plus <paramref name="hours"/>, or null when that isn't a finite moment a <see cref="DateTime"/> can hold.</summary>
    public static DateTime? After(DateTime nowUtc, double hours) =>
        double.IsFinite(hours) && hours >= 0 && hours < (DateTime.MaxValue - nowUtc).TotalHours - 1
            ? nowUtc + TimeSpan.FromHours(hours)
            : null;

    /// <summary><paramref name="nowUtc"/> plus <paramref name="hours"/> when that is before <paramref name="endUtc"/>, else null.</summary>
    public static DateTime? Before(DateTime nowUtc, double hours, DateTime endUtc) =>
        double.IsFinite(hours) && hours >= 0 && hours < (endUtc - nowUtc).TotalHours ? After(nowUtc, hours) : null;
}
