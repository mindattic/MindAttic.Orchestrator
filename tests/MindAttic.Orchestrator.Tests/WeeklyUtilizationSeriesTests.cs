using MindAttic.Orchestrator.Budget;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class WeeklyUtilizationSeriesTests
{
    private static readonly DateTime Reset = new(2026, 11, 9, 10, 59, 0, DateTimeKind.Utc);
    private static readonly DateTime Start = Reset - WeeklyUtilizationSeries.WindowLength;

    private static BudgetStatus Budget(double? weeklyPercent) =>
        new(null, 0, false, 0, default, default, null, weeklyPercent, 0, null, 90) { WeekResetUtc = Reset };

    private static UsageSample S(double days, double weekPercent, DateTime? weekReset = null) =>
        new(Start.AddDays(days), 0, Start.AddDays(days).AddHours(5), 0) { WeekPercent = weekPercent, WeekResetUtc = weekReset ?? Reset };

    [Test]
    public void Line_starts_at_zero_only_rises_and_ends_at_the_budgets_own_now()
    {
        var u = WeeklyUtilizationSeries.Build([S(1, 10), S(2, 30), S(2.1, 12), S(3, 35), S(1, 80, Reset.AddDays(-7))], Budget(37), 99, Start.AddDays(3.5));

        Assert.That(u.Actual.Select(p => p.Percent), Is.EqualTo(new[] { 0, 10, 30, 35, 37.0 }), "the stale 12% and the other week's 80% are left out");
        Assert.That(u.NowPercent, Is.EqualTo(37));
        Assert.That((u.StartUtc, u.EndUtc), Is.EqualTo((Start, Reset)));
    }

    [Test]
    public void No_rate_learned_yet_draws_flat_with_no_projection()
    {
        var u = WeeklyUtilizationSeries.Build([S(1, 20)], Budget(20), 99, Start.AddDays(1.5));
        Assert.That(u.PercentPerHour, Is.EqualTo(0));
        Assert.That(u.ProjectedEndPercent, Is.EqualTo(20));
    }

    [Test]
    public void Projection_carries_weekly_burns_rate_to_the_reset()
    {
        // WeeklyBurn.PerHour needs readings spanning at least one 5-hour block within its trailing 24h.
        var samples = new[] { S(2, 20), S(2 + 10.0 / 24, 25) };
        var u = WeeklyUtilizationSeries.Build(samples, Budget(25), 99, Start.AddDays(2) + TimeSpan.FromHours(10));

        var rate = WeeklyBurn.PerHour(samples, Reset, Start.AddDays(2) + TimeSpan.FromHours(10));
        Assert.That(rate, Is.Not.Null);
        Assert.That(u.PercentPerHour, Is.EqualTo(rate!.Value).Within(1e-9));
        var hoursLeft = (Reset - (Start.AddDays(2) + TimeSpan.FromHours(10))).TotalHours;
        Assert.That(u.ProjectedEndPercent, Is.EqualTo(25 + rate.Value * hoursLeft).Within(1e-6));
    }

    [Test]
    public void No_weekly_reset_known_yet_and_nothing_on_record_is_an_empty_axis()
    {
        var u = WeeklyUtilizationSeries.Build([], new BudgetStatus(null, 0, false, 0, default, default, null, null, 0, null, 90), 99, Start);
        Assert.That(u.Ended, Is.True);
        Assert.That(u.HasLastWindow, Is.False);
    }

    [Test]
    public void Between_weeks_the_last_week_is_drawn_as_it_ended()
    {
        // A week that reset before Start, with its own readings during it (not Start's own week, which S() dates from).
        var pastReset = Start;
        var weekStart = pastReset - WeeklyUtilizationSeries.WindowLength;
        var samples = new[]
        {
            new UsageSample(weekStart.AddDays(1), 0, weekStart.AddDays(1).AddHours(5), 0) { WeekPercent = 20, WeekResetUtc = pastReset },
            new UsageSample(weekStart.AddDays(3), 0, weekStart.AddDays(3).AddHours(5), 0) { WeekPercent = 60, WeekResetUtc = pastReset },
        };
        var u = WeeklyUtilizationSeries.Build(samples, new BudgetStatus(null, 0, false, 0, default, default, null, null, 0, null, 90), 99, pastReset.AddMinutes(40));

        Assert.That(u.Ended, Is.True);
        Assert.That(u.HasLastWindow, Is.True);
        Assert.That(u.Actual.Select(p => p.Percent), Is.EqualTo(new[] { 0, 20, 60.0 }));
        Assert.That(u.ProjectedEndPercent, Is.EqualTo(60));
    }
}
