using MindAttic.Orchestrator.Budget;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class UtilizationSeriesTests
{
    private static readonly DateTime Start = new(2026, 10, 16, 14, 10, 0, DateTimeKind.Utc);
    private static readonly DateTime End = Start.AddHours(5);

    private static BudgetStatus Budget(double percent, double perHour = 0, bool peak = false) =>
        new(new UsageBlock(Start, End, default, Start), 1_000_000, peak, percent, default, default, null, null, 0, null, 90)
        { RecentFreshPerHour = perHour };

    private static UsageSample S(double hours, double percent, DateTime? reset = null) => new(Start.AddHours(hours), percent, reset ?? End, 0);

    [Test]
    public void Line_starts_at_zero_only_rises_and_ends_at_the_budget_now()
    {
        var u = UtilizationSeries.Build([S(1, 10), S(1.5, 30), S(1.6, 12), S(2, 35), S(1, 80, End.AddHours(-5))], Budget(37), 99, Start.AddHours(2.5))!;

        Assert.That(u.Actual.Select(p => p.Percent), Is.EqualTo(new[] { 0, 10, 30, 35, 37.0 }), "the stale 12% and the other window's 80% are left out");
        Assert.That(u.NowPercent, Is.EqualTo(37));
    }

    [Test]
    public void Behind_a_wall_samples_whose_reset_is_off_by_seconds_still_draw_the_line()
    {
        // The block ends at the limit message's whole-minute reset; Claude's resets_at carries seconds.
        var u = UtilizationSeries.Build([S(1, 10, End.AddSeconds(37)), S(2, 35, End.AddSeconds(37))], Budget(37), 99, Start.AddHours(2.5))!;
        Assert.That(u.Actual.Select(p => p.Percent), Is.EqualTo(new[] { 0, 10, 35, 37.0 }));
    }

    [Test]
    public void Pace_runs_straight_to_the_target_and_over_is_time_spent_above_it()
    {
        // Pace is 19.8% after 1h; 30% at 1h is over, 35% at 2h is under (39.6%).
        var u = UtilizationSeries.Build([S(1, 30), S(2, 35)], Budget(35), 99, Start.AddHours(2))!;

        Assert.That(u.PaceAt(Start.AddHours(2.5)), Is.EqualTo(49.5).Within(1e-9));
        Assert.That(u.OverPace, Is.EqualTo(TimeSpan.FromHours(1)));
        Assert.That(u.OverNow, Is.False);
    }

    [Test]
    public void Projection_carries_the_recent_rate_to_the_reset()
    {
        // 100k/h of a 1M limit is 10%/h; 3h left from 40% lands at 70%, short of the limit.
        var u = UtilizationSeries.Build([], Budget(40, 100_000), 99, Start.AddHours(2))!;
        Assert.That(u.ProjectedEndPercent, Is.EqualTo(70).Within(1e-9));
        Assert.That(u.HitsLimitUtc, Is.Null);

        // 30%/h from 40% reaches 100% two hours on, before the reset.
        var fast = UtilizationSeries.Build([], Budget(40, 300_000), 99, Start.AddHours(2))!;
        Assert.That(fast.ProjectedEndPercent, Is.EqualTo(130).Within(1e-9));
        Assert.That(fast.HitsLimitUtc, Is.EqualTo(Start.AddHours(4)));
    }

    [Test]
    public void Nothing_to_draw_without_a_real_limit()
    {
        Assert.That(UtilizationSeries.Build([], Budget(40, peak: true), 99, Start.AddHours(1)), Is.Null);
    }
}
