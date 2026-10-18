using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class UsageHistoryTests
{
    [Test]
    public void Days_in_the_transcript_window_are_recomputed_and_older_ones_kept()
    {
        var today = new DateOnly(2026, 10, 14);
        var stored = new[]
        {
            new DayUsage(today.AddDays(-30), "A", "Claude", 100),
            new DayUsage(today.AddDays(-1), "A", "Claude", 5),
        };
        var observed = new[] { new DayUsage(today.AddDays(-1), "A", "Claude", 50), new DayUsage(today, "B", "Codex", 7) };

        var merged = UsageHistory.Merge(stored, observed, today.AddDays(-7), today);

        Assert.That(merged.Select(d => (d.Day, d.Fresh)), Is.EqualTo(new[] { (today.AddDays(-30), 100L), (today.AddDays(-1), 50L), (today, 7L) }));
    }

    [Test]
    public void The_week_is_projected_against_its_limit_and_reset()
    {
        var now = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
        // The week started 2 days ago and resets in 5; 60k used of 100k is 30k/day: out in ~1.3 days.
        var settings = new OrchestratorSettings { WeeklyBudget = 100_000, WeeklyReset = "Mon 12:00" };
        var budget = BudgetCalculator.Compute([new UsageEvent(now.AddDays(-1), new TokenUsage(0, 0, 0, 60_000))], settings, now, now.Date, TimeZoneInfo.Utc);
        var history = new[] { new DayUsage(DateOnly.FromDateTime(now.AddDays(-1).ToLocalTime()), "A", "Claude", 60_000) };

        var w = Burndown.Compute(budget, history, now);

        Assert.That(w.PerHour * 24, Is.EqualTo(30_000).Within(1));
        Assert.That(w.RunsOut, Is.True);
        Assert.That(w.ExhaustedUtc, Is.EqualTo(now.AddHours(40_000 / (30_000 / 24.0))).Within(TimeSpan.FromMinutes(1)));
        Assert.That(w.Days.Sum(d => d.Fresh), Is.EqualTo(60_000));
    }

    [Test]
    public void The_bars_count_the_signed_in_account_and_never_a_session_billed_elsewhere()
    {
        var now = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
        var settings = new OrchestratorSettings { WeeklyBudget = 100_000, WeeklyReset = "Mon 12:00" };
        var budget = BudgetCalculator.Compute([], settings, now, now.Date, TimeZoneInfo.Utc);
        var day = DateOnly.FromDateTime(now.ToLocalTime());
        var history = new[]
        {
            new DayUsage(day, "A", "Claude", 10, "a"), new DayUsage(day, "A", "Claude", 200, "b"),
            new DayUsage(day, "A", "Claude", 3_000, UsageHistory.BilledElsewhere + "api-key"), new DayUsage(day, "A", "Claude", 40_000),
        };

        Assert.That(Burndown.Compute(budget, history, now, "b").Days.Sum(d => d.Fresh), Is.EqualTo(200));
        Assert.That(Burndown.Compute(budget, history, now).Days.Sum(d => d.Fresh), Is.EqualTo(40_210), "no account known: every subscription token");
    }

    [Test]
    public void The_first_bar_counts_from_the_reset_not_from_the_midnight_of_the_day_it_fell_on()
    {
        // M.ORCH-T388: after a 7:00 AM reset the first bar counted midnight to 7:00 too, which the week's total does not.
        DateTime Local(int day, int hour) => new DateTime(2026, 10, day, hour, 0, 0, DateTimeKind.Local).ToUniversalTime();
        var now = Local(14, 12);
        var settings = new OrchestratorSettings { WeeklyBudget = 100_000, WeeklyReset = "Mon 07:00" };
        UsageEvent Spent(DateTime at, long fresh) => new(at, new TokenUsage(0, 0, 0, fresh));
        var events = new[] { Spent(Local(12, 5), 500), Spent(Local(12, 9), 70), Spent(Local(13, 10), 30) };
        var budget = BudgetCalculator.Compute(events, settings, now, Local(14, 0), TimeZoneInfo.Local);
        var session = new TranscriptSession("s", @"D:\W\A", now, default, null) { Events = events };

        var history = UsageHistory.Observe([session], _ => "A", Local(7, 0), weekStartUtc: budget.WeekStartUtc).ToList();
        var w = Burndown.Compute(budget, history, now);

        Assert.That(budget.WeekStartUtc, Is.EqualTo(Local(12, 7)));
        Assert.That(w.Days.Select(d => d.Fresh), Is.EqualTo(new[] { 70L, 30L, 0L }), "Monday from 7:00 on");
        Assert.That(w.Days.Sum(d => d.Fresh), Is.EqualTo(w.Used));
        Assert.That(history.Where(d => d.Day == new DateOnly(2026, 10, 12)).Select(d => (d.Fresh, d.BeforeUtc)),
            Is.EquivalentTo(new[] { (500L, (DateTime?)Local(12, 7)), (70L, (DateTime?)null) }), "the day's whole spend still kept, for reports");
    }
}
