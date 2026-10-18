using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class WeeklyResetTests
{
    private static readonly TimeZoneInfo Central = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");

    [TestCase("Mon 9:00", DayOfWeek.Monday, 9, 0)]
    [TestCase("monday 09:00", DayOfWeek.Monday, 9, 0)]
    [TestCase("Tue 5pm", DayOfWeek.Tuesday, 17, 0)]
    [TestCase("Fri 5:30 PM", DayOfWeek.Friday, 17, 30)]
    [TestCase("Sunday 23:15", DayOfWeek.Sunday, 23, 15)]
    [TestCase("Th 7", DayOfWeek.Thursday, 7, 0)]
    public void Parses_day_and_time(string text, DayOfWeek day, int hour, int minute)
    {
        Assert.That(WeeklyReset.TryParse(text, out var r), Is.True);
        Assert.That(r, Is.EqualTo(new WeeklyReset(day, new TimeOnly(hour, minute))));
    }

    [TestCase("")]
    [TestCase("none")]
    [TestCase(null)]
    public void Blank_means_rolling(string? text)
    {
        Assert.That(WeeklyReset.TryParse(text, out var r), Is.True);
        Assert.That(r, Is.Null);
    }

    [TestCase("9:00")]
    [TestCase("Someday 9:00")]
    [TestCase("Mon 25:00")]
    [TestCase("T 9:00")] // ambiguous: Tuesday or Thursday
    public void Rejects_garbage(string text) => Assert.That(WeeklyReset.TryParse(text, out _), Is.False);

    [Test]
    public void Round_trips_through_its_stored_form()
    {
        var r = new WeeklyReset(DayOfWeek.Wednesday, new TimeOnly(18, 5));
        Assert.That(WeeklyReset.TryParse(r.ToString(), out var back), Is.True);
        Assert.That(back, Is.EqualTo(r));
        Assert.That(WeeklyReset.TryParse(r.Display, out var fromDisplay), Is.True);
        Assert.That(fromDisplay, Is.EqualTo(r));
    }

    [Test]
    public void Window_is_the_last_reset_and_the_next()
    {
        var reset = new WeeklyReset(DayOfWeek.Monday, new TimeOnly(9, 0));
        // Wednesday 2026-10-14 12:00 Central (CDT, UTC-5) = 17:00 UTC.
        var (start, next) = reset.Window(new DateTime(2026, 10, 14, 17, 0, 0, DateTimeKind.Utc), Central);

        Assert.That(start, Is.EqualTo(new DateTime(2026, 10, 12, 14, 0, 0, DateTimeKind.Utc)), "Mon 9:00 CDT");
        Assert.That(next, Is.EqualTo(new DateTime(2026, 10, 19, 14, 0, 0, DateTimeKind.Utc)));
    }

    [Test]
    public void Exactly_at_the_reset_starts_the_new_week()
    {
        var reset = new WeeklyReset(DayOfWeek.Monday, new TimeOnly(9, 0));
        var atReset = new DateTime(2026, 10, 12, 14, 0, 0, DateTimeKind.Utc);

        Assert.That(reset.Window(atReset, Central).StartUtc, Is.EqualTo(atReset));
        Assert.That(reset.Window(atReset.AddSeconds(-1), Central).StartUtc, Is.EqualTo(atReset.AddDays(-7)));
    }

    [Test]
    public void Keeps_local_hour_across_a_daylight_saving_change()
    {
        // US DST ends Sunday 2026-11-01, so the week starting Mon 2026-10-26 (CDT) ends Mon 2026-11-02 (CST).
        var reset = new WeeklyReset(DayOfWeek.Monday, new TimeOnly(9, 0));
        var (start, next) = reset.Window(new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc), Central);

        Assert.That(start, Is.EqualTo(new DateTime(2026, 10, 26, 14, 0, 0, DateTimeKind.Utc)));
        Assert.That(next, Is.EqualTo(new DateTime(2026, 11, 2, 15, 0, 0, DateTimeKind.Utc)), "9:00 CST is 15:00 UTC");
    }

    [Test]
    public void A_reset_in_the_repeated_fall_back_hour_is_its_first_pass()
    {
        // 2026-11-01 01:30 happens at 06:30Z (CDT) and again at 07:30Z (CST): at 06:45Z the week started at 06:30Z (M.ORCH-T430).
        var reset = new WeeklyReset(DayOfWeek.Sunday, new TimeOnly(1, 30));
        var now = new DateTime(2026, 11, 1, 6, 45, 0, DateTimeKind.Utc);
        var (start, _) = reset.Window(now, Central);

        Assert.That(start, Is.EqualTo(new DateTime(2026, 11, 1, 6, 30, 0, DateTimeKind.Utc)));
        Assert.That(start, Is.LessThanOrEqualTo(now));
    }

    [Test]
    public void Weekly_usage_restarts_at_the_reset_instead_of_rolling()
    {
        var now = new DateTime(2026, 10, 14, 17, 0, 0, DateTimeKind.Utc); // Wed noon CDT
        UsageEvent At(DateTime utc, long output) => new(utc, new TokenUsage(0, 0, 0, output));
        var events = new[]
        {
            At(new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc), 7000), // Sunday: last week
            At(new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc), 1000), // Tuesday: this week
        };

        var anchored = BudgetCalculator.Compute(events,
            new OrchestratorSettings { WeeklyBudget = 10_000, WeeklyReset = "Monday 09:00" }, now, now.Date, Central);
        var rolling = BudgetCalculator.Compute(events, new OrchestratorSettings { WeeklyBudget = 10_000 }, now, now.Date, Central);

        Assert.That(anchored.Week.Output, Is.EqualTo(1000));
        Assert.That(anchored.WeeklyPercent, Is.EqualTo(10).Within(0.01));
        Assert.That(anchored.WeekResetUtc, Is.EqualTo(new DateTime(2026, 10, 19, 14, 0, 0, DateTimeKind.Utc)));
        Assert.That(rolling.Week.Output, Is.EqualTo(8000));
        Assert.That(rolling.WeekIsRolling, Is.True);
    }

    [Test]
    public void A_bad_stored_value_falls_back_to_rolling()
    {
        var now = new DateTime(2026, 10, 14, 17, 0, 0, DateTimeKind.Utc);
        var s = BudgetCalculator.Compute([], new OrchestratorSettings { WeeklyReset = "whenever" }, now, now.Date, Central);

        Assert.That(s.WeekIsRolling, Is.True);
        Assert.That(s.WeekStartUtc, Is.EqualTo(now.AddDays(-7)));
    }
}
