using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class LimitCalibrationTests
{
    private static DateTime Utc(int month, int day, double hour) =>
        new DateTime(2026, month, day, 0, 0, 0, DateTimeKind.Utc).AddHours(hour);

    private static UsageEvent E(DateTime at, long output) => new(at, new TokenUsage(0, 0, 0, output));

    private static LimitObservation Obs(DateTime hit, long used, LimitKind kind = LimitKind.Session) =>
        new(kind, hit, hit.AddHours(1), used);

    [TestCase("You've hit your session limit · resets 6:40pm (America/Chicago)", "2026-10-13T21:22:25Z", "2026-10-13T23:40:00Z",
        TestName = "Same-day reset in the message's zone")]
    [TestCase("You've hit your session limit · resets 4:40am (America/Chicago)", "2026-10-14T04:00:00Z", "2026-10-14T09:40:00Z",
        TestName = "A time already past today is tomorrow's")]
    [TestCase("You've hit your session limit · resets 2pm (America/Chicago)", "2026-10-13T19:00:02Z", "2026-10-13T19:00:00Z",
        TestName = "A session reset the message was written just after is the one passed, not tomorrow's")]
    [TestCase("You've hit your weekly limit · resets 2pm (America/Chicago)", "2026-10-13T19:00:02Z", "2026-10-14T19:00:00Z",
        TestName = "A weekly reset with no date just passed is tomorrow's")]
    [TestCase("You've hit your session limit · resets 2pm (America/Chicago)", "2026-10-13T15:00:00Z", "2026-10-13T19:00:00Z",
        TestName = "Hour without minutes")]
    [TestCase("You've hit your weekly limit · resets Sep 24, 4pm (America/Chicago)", "2026-09-20T22:49:00Z", "2026-09-24T21:00:00Z",
        TestName = "Weekly reset with a date")]
    public void Parses_the_reset_Claude_Code_reports(string text, string at, string reset)
    {
        var atUtc = DateTime.Parse(at, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
        Assert.That(LimitHitParser.TryParse(text, atUtc, out var hit), Is.True);
        Assert.That(hit.ResetUtc, Is.EqualTo(DateTime.Parse(reset, null, System.Globalization.DateTimeStyles.AdjustToUniversal)));
        Assert.That(hit.Kind, Is.EqualTo(text.Contains("weekly") ? LimitKind.Weekly : LimitKind.Session));
    }

    [Test]
    public void Other_api_errors_are_not_hits() =>
        Assert.That(LimitHitParser.TryParse("API Error: This request would exceed your account's rate limit.", DateTime.UtcNow, out _), Is.False);

    [Test]
    public void One_observation_per_wall_counting_the_window_up_to_the_first_hit()
    {
        var reset = Utc(10, 13, 20);   // window 15:00-20:00
        var events = new[] { E(Utc(10, 13, 14.9), 1000), E(Utc(10, 13, 15.5), 300), E(Utc(10, 13, 17), 200), E(Utc(10, 13, 18.5), 999) };
        var hits = new[]
        {
            new LimitHit(Utc(10, 13, 18), LimitKind.Session, reset),
            new LimitHit(Utc(10, 13, 18.2), LimitKind.Session, reset), // another session hitting the same wall
        };

        var o = LimitCalibration.Observe(hits, events, Utc(10, 1, 0)).Single();

        Assert.That(o.HitUtc, Is.EqualTo(Utc(10, 13, 18)));
        Assert.That(o.FreshUsed, Is.EqualTo(500), "only usage inside the window and before the hit");
        Assert.That(o.Covered, Is.True);
    }

    [Test]
    public void A_window_older_than_the_transcripts_is_kept_but_not_learned_from()
    {
        var o = LimitCalibration.Observe([new LimitHit(Utc(10, 13, 18), LimitKind.Session, Utc(10, 13, 20))], [], Utc(10, 13, 16)).Single();

        Assert.That(o.Covered, Is.False);
        Assert.That(new LimitCalibration([o]).Learned5h, Is.Null);
    }

    [Test]
    public void Learns_from_the_most_recent_hits_so_a_raised_limit_takes_over()
    {
        var old = Enumerable.Range(0, 6).Select(i => Obs(Utc(9, 1 + i, 12), 3_000_000));
        var recent = Enumerable.Range(0, 3).Select(i => Obs(Utc(10, 11 + i, 12), 12_000_000 + i));

        var c = new LimitCalibration([.. old, .. recent]);

        Assert.That(c.Learned5h, Is.EqualTo(12_000_000), "3 of the last 5 hits are at the new level");
        Assert.That(c.Basis(LimitKind.Session), Is.EqualTo(LimitCalibration.RecentHits));
        Assert.That(c.LearnedWeekly, Is.Null);
    }

    [Test]
    public void Merge_keeps_the_first_sighting_and_forgets_after_sixty_days()
    {
        var now = Utc(10, 14, 0);
        var stored = new[] { Obs(Utc(10, 13, 18), 500), Obs(Utc(7, 1, 0), 1) };
        var later = stored[0] with { HitUtc = Utc(10, 13, 18.5), FreshUsed = 600 };

        var merged = LimitCalibration.Merge(stored, [later, Obs(Utc(10, 13, 22), 700)], now);

        Assert.That(merged.Select(o => o.FreshUsed), Is.EqualTo(new[] { 500L, 700L }));
    }

    [Test]
    public void A_set_budget_wins_over_a_learned_limit()
    {
        var learned = new LimitCalibration([Obs(Utc(10, 12, 12), 5000)]);
        var now = Utc(10, 13, 10.5);

        var auto = BudgetCalculator.Compute([E(Utc(10, 13, 10), 1000)], new OrchestratorSettings(), now, Utc(10, 13, 0), learned: learned);
        var manual = BudgetCalculator.Compute([E(Utc(10, 13, 10), 1000)], new OrchestratorSettings { BlockBudget = 2000 }, now, Utc(10, 13, 0), learned: learned);

        Assert.That((auto.BlockLimitSource, auto.BlockLimit, auto.BlockPercent), Is.EqualTo((LimitSource.Learned, 5000L, 20.0)));
        Assert.That((manual.BlockLimitSource, manual.BlockLimit), Is.EqualTo((LimitSource.Manual, 2000L)));
    }

    [Test]
    public void Spending_past_a_learned_limit_without_a_wall_retires_it()
    {
        // The weekly limit learned in September was passed this week with no wall: limits went up.
        var learned = new LimitCalibration([Obs(Utc(9, 25, 5), 67_000_000, LimitKind.Weekly), Obs(Utc(10, 12, 12), 11_000_000)]);
        var now = Utc(10, 14, 14);

        var s = BudgetCalculator.Compute([E(Utc(10, 12, 0), 170_000_000), E(Utc(10, 14, 13.5), 1000)],
            new OrchestratorSettings(), now, Utc(10, 14, 0), learned: learned);

        Assert.That(s.WeeklyLimitOutdated, Is.True);
        Assert.That(s.WeeklyLimit, Is.Null);
        Assert.That(s.Level, Is.EqualTo(BudgetLevel.Ok), "a stale limit must not report a wall that isn't there");
        Assert.That(s.BlockLimitSource, Is.EqualTo(LimitSource.Learned), "the 5-hour limit wasn't contradicted");
    }

    [Test]
    public void Behind_a_reported_wall_the_block_ends_at_its_reset_and_is_over()
    {
        var reset = Utc(10, 13, 23 + 2.0 / 3); // 23:40
        var learned = new LimitCalibration([new LimitObservation(LimitKind.Session, Utc(10, 13, 22.75), reset, 9000)]);

        var s = BudgetCalculator.Compute([E(Utc(10, 13, 19), 9000)], new OrchestratorSettings(), Utc(10, 13, 23), Utc(10, 13, 0), learned: learned);

        Assert.That(s.CurrentBlock!.EndUtc, Is.EqualTo(reset), "the reported reset beats the estimated block");
        Assert.That(s.AtLimitUntilUtc, Is.EqualTo(reset));
        Assert.That(s.Level, Is.EqualTo(BudgetLevel.Over));
    }

    [Test]
    public void The_weekly_reset_is_learned_from_a_weekly_hit()
    {
        var learned = new LimitCalibration([new LimitObservation(LimitKind.Weekly, Utc(10, 7, 22), Utc(10, 8, 16), 80_000_000)]);

        var s = BudgetCalculator.Compute([E(Utc(10, 13, 10), 1)], new OrchestratorSettings(), Utc(10, 13, 12), Utc(10, 13, 0),
            TimeZoneInfo.Utc, learned);

        Assert.That(s.WeekResetUtc, Is.EqualTo(Utc(10, 15, 16)), "Thursdays at 16:00, like the hit said");
        Assert.That(s.WeeklyLimitSource, Is.EqualTo(LimitSource.Learned));
    }
}
