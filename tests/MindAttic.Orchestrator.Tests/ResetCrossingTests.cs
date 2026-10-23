using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The moment a window resets is a fresh window everywhere: crossed on a fixed clock, 10 seconds either side (M.ORCH-T387).</summary>
public sealed class ResetCrossingTests
{
    // Sunday 2026-10-18 7:00 AM Central: the week's reset (seven_day resets_at 1792324800); the 5-hour window resets at 6:10.
    private static readonly DateTime Week = DateTimeOffset.FromUnixTimeSeconds(1792324800).UtcDateTime;
    private static readonly DateTime Five = Week.AddMinutes(-50);
    private static readonly TimeZoneInfo Central = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
    private static readonly OrchestratorSettings Settings = new();

    private static UsageEvent At(DateTime utc, long fresh) => new(utc, new TokenUsage(0, 0, 1_000_000, fresh));

    private static BudgetStatus Budget(IReadOnlyList<UsageEvent> events, DateTime now, ReportedUsage? reported, LimitCalibration? learned = null, long? reportedLimit = null) =>
        BudgetCalculator.Compute(events, Settings, now, now.Date, Central, learned, reported, reportedLimit);

    [Test]
    public void The_week_starts_over_at_claudes_reset_with_only_the_new_weeks_tokens()
    {
        List<UsageEvent> events = [At(Week.AddDays(-3), 8_000_000), At(Week.AddHours(-1), 1_000_000), At(Week.AddSeconds(5), 100)];
        // The last reading is from before the reset, and an older weekly hit (Thursday 4 PM) would put the week elsewhere.
        var reported = new ReportedUsage(new ReportedWindow(30, Week.AddHours(4)), new ReportedWindow(80, Week), Week.AddMinutes(-5));
        var learned = new LimitCalibration([new LimitObservation(LimitKind.Weekly, Week.AddDays(-11), Week.AddDays(-9.625), 50_000_000)]);

        var before = Budget(events, Week.AddSeconds(-10), reported, learned);
        Assert.That((before.WeekStartUtc, before.WeekResetUtc, before.WeeklyPercent), Is.EqualTo((Week.AddDays(-7), (DateTime?)Week, (double?)80)));

        var after = Budget(events, Week.AddSeconds(10), reported, learned);
        Assert.That((after.WeekStartUtc, after.WeekResetUtc), Is.EqualTo((Week, (DateTime?)Week.AddDays(7))), "Claude's reset, rolled a week on");
        Assert.That(after.Week.Fresh, Is.EqualTo(100), "only what was spent since the reset");
        Assert.That(after.WeeklyPercent, Is.LessThan(1), "never the old week's 80%");
        Assert.That(after.AtLimitUntilUtc, Is.Null);
    }

    [Test]
    public void The_5_hour_window_starts_over_at_its_reset_with_its_full_room()
    {
        List<UsageEvent> events = [At(Five.AddHours(-4), 9_000), At(Five.AddMinutes(-3), 500), At(Five.AddSeconds(5), 50)];
        var reported = new ReportedUsage(new ReportedWindow(95, Five), new ReportedWindow(80, Week), Five.AddMinutes(-2));

        var before = Budget(events, Five.AddSeconds(-10), reported, reportedLimit: 10_000);
        var paceBefore = Pacer.Compute(before, 1, 1_000, Settings, Five.AddSeconds(-10));
        Assert.That(paceBefore.Stop, Is.False, "95% of the old window: nothing held yet (M.ORCH-T741)");

        var now = Five.AddSeconds(10);
        var after = Budget(events, now, reported, reportedLimit: 10_000);
        Assert.That(after.CurrentBlock!.StartUtc, Is.EqualTo(Five), "the transcripts' block (it started 2:10) doesn't run on past Claude's reset");
        Assert.That(after.CurrentBlock.Usage.Fresh, Is.EqualTo(50));
        Assert.That((after.BlockLimitSource, after.BlockLimit), Is.EqualTo((LimitSource.Reported, 10_000L)), "the size from the reports still holds");
        var pace = Pacer.Compute(after, 1, 1_000, Settings, now);
        Assert.That(pace.Stop, Is.False);
        Assert.That(pace.RoomTokens, Is.EqualTo(10_000 * Settings.PaceTargetPercent / 100.0 - 50));
    }

    [Test]
    public void Another_accounts_weekly_hits_neither_date_nor_size_this_accounts_week()
    {
        var mine = new LimitObservation(LimitKind.Weekly, Week.AddDays(-8), Week.AddDays(-7), 70_000_000);
        var theirs = new LimitObservation(LimitKind.Weekly, Week.AddDays(-2), Week.AddDays(-1.5), 20_000_000);
        var learned = new LimitCalibration([mine, theirs]) { Mine = t => t < Week.AddDays(-3) };
        Assert.That(learned.LearnedWeeklyReset(Central), Is.EqualTo(new WeeklyReset(DayOfWeek.Sunday, new TimeOnly(7, 0))));
        Assert.That(learned.LearnedWeekly, Is.EqualTo(70_000_000));

        // No reading at all: the week is the one this account's own hit taught, starting over at 7:00.
        var after = Budget([At(Week.AddHours(-1), 1_000), At(Week.AddSeconds(5), 100)], Week.AddSeconds(10), null, learned);
        Assert.That((after.WeekStartUtc, after.Week.Fresh), Is.EqualTo((Week, 100L)));
        var before = Budget([At(Week.AddHours(-1), 1_000), At(Week.AddSeconds(5), 100)], Week.AddSeconds(-10), null, learned);
        Assert.That(before.WeekResetUtc, Is.EqualTo(Week));
    }

    [Test]
    public void A_reading_from_before_the_reset_is_due_a_fresh_one_and_a_snapshot_from_before_it_isnt_shared()
    {
        var reading = new ReportedUsage(new ReportedWindow(30, Week.AddHours(4)), new ReportedWindow(80, Week), Week.AddMinutes(-3));
        Assert.That(StatusAggregator.ResetSinceReading(reading, Week.AddSeconds(-10)), Is.Null);
        Assert.That(StatusAggregator.ResetSinceReading(reading, Week.AddSeconds(10)), Is.EqualTo(Week));
        Assert.That(StatusAggregator.ResetSinceReading(reading with { AtUtc = Week.AddSeconds(5) }, Week.AddSeconds(10)), Is.Null, "read since: nothing to ask");

        var pace = new Pace(PaceMode.Coast, 0, 0, 0, null, "5-hour", Five, "") { Stop = true };
        var snapshot = new Snapshot(Five.AddSeconds(-12), [], []) { Pace = pace };
        Assert.That(StatusAggregator.ResetSince(snapshot, Five.AddSeconds(-10)), Is.False);
        Assert.That(StatusAggregator.ResetSince(snapshot, Five.AddSeconds(10)), Is.True, "its stop is the old window's");
    }
}
