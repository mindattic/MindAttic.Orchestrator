using MindAttic.Orchestrator.Budget;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class UtilizationSeriesTests
{
    private static readonly DateTime Start = new(2026, 10, 16, 14, 10, 0, DateTimeKind.Utc);
    private static readonly DateTime End = Start.AddHours(5);

    private static BudgetStatus Budget(double percent, double perHour = 0, bool peak = false, double controlPerHour = 0) =>
        new(new UsageBlock(Start, End, default, Start), 1_000_000, peak, percent, default, default, null, null, 0, null, 90)
        { RecentFreshPerHour = perHour, ControlFreshPerHour = controlPerHour };

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
    public void Projection_never_understates_a_burst_the_30_minute_rate_hasnt_caught_up_to()
    {
        // M.ORCH-T783: a login split across many CLIs can start burning hard enough that ControlFreshPerHour (10 min)
        // already shows it, while RecentFreshPerHour (30 min) is still carrying the quieter stretch before the burst and
        // reads low. The projection every reader shares (the pace hook's guidance, the subagent-allowance raise, and this
        // same chart) must follow whichever rate is worse, not the slower one, or it keeps saying "under target" while
        // the window is already on course past it.
        var laggy = UtilizationSeries.Build([], Budget(40, perHour: 100_000, controlPerHour: 300_000), 99, Start.AddHours(2))!;
        Assert.That(laggy.ProjectedEndPercent, Is.EqualTo(130).Within(1e-9), "the faster, more current rate wins over the slower one");

        // The other way round (a burst that's cooled off in the last 10 minutes but still shows in the 30-minute average)
        // doesn't get read down either: still the worse (higher) of the two.
        var cooling = UtilizationSeries.Build([], Budget(40, perHour: 300_000, controlPerHour: 100_000), 99, Start.AddHours(2))!;
        Assert.That(cooling.ProjectedEndPercent, Is.EqualTo(130).Within(1e-9));
    }

    private static BudgetStatus Idle() => new(null, 0, true, 0, default, default, null, null, 0, null, 90);

    [Test]
    public void Between_windows_the_last_window_is_drawn_as_it_ended()
    {
        // M.ORCH-T502: no window running, so the last one (its reset passed) is drawn to its last reading, without a projection.
        var u = UtilizationSeries.Build([S(1, 10, End.AddHours(-5)), S(1, 20), S(3, 50), S(4.5, 72)], Idle(), 99, End.AddMinutes(40))!;

        Assert.That(u.Ended, Is.True);
        Assert.That(u.HasLastWindow, Is.True);
        Assert.That((u.StartUtc, u.EndUtc), Is.EqualTo((Start, End)));
        Assert.That(u.Actual.Select(p => p.Percent), Is.EqualTo(new[] { 0, 20, 50, 72.0 }), "the earlier window's reading is left out");
        Assert.That((u.NowUtc, u.NowPercent, u.ProjectedEndPercent), Is.EqualTo((Start.AddHours(4.5), 72.0, 72.0)));
    }

    [Test]
    public void Between_windows_with_none_on_record_an_empty_axis()
    {
        var now = End.AddMinutes(40);
        var u = UtilizationSeries.Build([], Idle(), 99, now)!;
        Assert.That(u.Ended, Is.True);
        Assert.That(u.HasLastWindow, Is.False);
        Assert.That(u.Actual, Is.Empty);
        Assert.That((u.StartUtc, u.EndUtc), Is.EqualTo((now, now.AddHours(5))));
    }

    [Test]
    public void Drawn_flat_at_the_reported_percent_without_a_real_limit_yet()
    {
        // M.ORCH-T835: a window is running but no real 5-hour limit has been learned yet (peak-relative, or none at
        // all), so there's no token-per-hour rate to project - still draw the reported BlockPercent rather than
        // leave the chart blank (M.ORCH-T629: every tab always shows its chart).
        var u = UtilizationSeries.Build([], Budget(40, peak: true), 99, Start.AddHours(1));

        Assert.That(u, Is.Not.Null);
        Assert.That((u.NowPercent, u.ProjectedEndPercent, u.PercentPerHour), Is.EqualTo((40.0, 40.0, 0.0)));
        Assert.That(u.Ended, Is.False);
        Assert.That(u.Idle, Is.False);
    }

    [Test]
    public void Still_climbs_with_the_actual_reports_without_a_real_limit_yet()
    {
        // Claude's own reports still draw the climbing line even with no learned limit (BlockLimit <= 0): the fallback
        // only replaces the rate-based projection, not the real readings.
        var u = UtilizationSeries.Build([S(1, 10), S(1.5, 30)], new(new UsageBlock(Start, End, default, Start), 0, false, 30, default,
            default, null, null, 0, null, 90), 99, Start.AddHours(1.5));

        Assert.That(u, Is.Not.Null);
        Assert.That(u.Actual.Select(p => p.Percent), Is.EqualTo(new[] { 0, 10, 30.0 }));
        Assert.That(u.NowPercent, Is.EqualTo(30));
    }
}
