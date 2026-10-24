using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>BudgetCalculator's reported path: Claude's own 5-hour percent sizing and driving the block (M.ORCH-T128).</summary>
public sealed class BudgetReportedPathTests
{
    private static readonly DateTime Day = new(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc);

    private static UsageEvent E(double hour, long output) =>
        new(Day.AddHours(hour), new TokenUsage(0, 0, 1_000_000, output));

    private static ReportedUsage Report(double percent, double resetHour, double atHour) =>
        new(new ReportedWindow(percent, Day.AddHours(resetHour)), null, Day.AddHours(atHour));

    [Test]
    public void ImpliedLimit_is_null_without_a_window_a_report_time_or_any_spend()
    {
        var events = new[] { E(10.5, 2400) };
        var window = new ReportedWindow(24, Day.AddHours(15));

        Assert.That(BudgetCalculator.ImpliedLimit(events, null, BudgetCalculator.BlockLength, Day.AddHours(11)), Is.Null, "no window");
        Assert.That(BudgetCalculator.ImpliedLimit(events, window, BudgetCalculator.BlockLength, null), Is.Null, "no report time");
        Assert.That(BudgetCalculator.ImpliedLimit([], window, BudgetCalculator.BlockLength, Day.AddHours(11)), Is.Null, "nothing spent to size by");
        Assert.That(BudgetCalculator.ImpliedLimit([E(11.5, 500)], window, BudgetCalculator.BlockLength, Day.AddHours(11)), Is.Null,
            "spend after the report isn't part of what the percent measured");
        Assert.That(BudgetCalculator.ImpliedLimit([E(9.5, 500)], window, BudgetCalculator.BlockLength, Day.AddHours(11)), Is.Null,
            "spend before the window began isn't either");
    }

    [Test]
    public void ImpliedLimit_widens_the_whole_percent_by_one_so_the_limit_errs_small()
    {
        // 2,400 in the 10:00-15:00 window by the 11:00 report at 24%: taken as 25%, so 9,600 rather than 10,000.
        var events = new[] { E(9.5, 7000), E(10.5, 2400), E(11.5, 600) };
        var window = new ReportedWindow(24, Day.AddHours(15));

        Assert.That(BudgetCalculator.ImpliedLimit(events, window, BudgetCalculator.BlockLength, Day.AddHours(11)), Is.EqualTo(9_600));
    }

    [Test]
    public void A_fresh_reported_limit_gives_exactly_claudes_percent_not_local_spend()
    {
        // Reported at 11:00, now 11:01, well inside ReportFreshMinutes: 9,000 local tokens before the report, 500
        // at 11:00:30 (after the report but still before now), neither moves it off Claude's own 30% (M.ORCH-T788).
        var events = new[] { E(10.9, 9000), new UsageEvent(Day.AddHours(11).AddSeconds(30), new TokenUsage(0, 0, 1_000_000, 500)) };

        var s = BudgetCalculator.Compute(events, new OrchestratorSettings(), Day.AddHours(11).AddMinutes(1), Day, TimeZoneInfo.Utc,
            reported: Report(30, 15, 11), reportedLimit: 10_000);

        Assert.That((s.BlockLimitSource, s.BlockLimit), Is.EqualTo((LimitSource.Reported, 10_000L)));
        Assert.That(s.CurrentBlock!.Usage.Fresh, Is.EqualTo(3_000), "Claude's own 30% of 10,000, nothing added");
        Assert.That(s.BlockPercent, Is.EqualTo(30).Within(0.001));
        Assert.That(s.CurrentBlock.EndUtc, Is.EqualTo(Day.AddHours(15)), "the reset Claude gave");
    }

    [Test]
    public void A_stale_reported_limit_adds_back_local_spend_since_the_report()
    {
        // Reported an hour ago, well past ReportFreshMinutes (nothing's redrawn claude-usage.json since): the hard
        // cutoff can't stay blind to the 500 spent since, so it's added to Claude's own 30% (M.ORCH-T783, M.ORCH-T788).
        var events = new[] { E(10.2, 9000), E(11.5, 500) };

        var s = BudgetCalculator.Compute(events, new OrchestratorSettings(), Day.AddHours(12), Day, TimeZoneInfo.Utc,
            reported: Report(30, 15, 11), reportedLimit: 10_000);

        Assert.That((s.BlockLimitSource, s.BlockLimit), Is.EqualTo((LimitSource.Reported, 10_000L)));
        Assert.That(s.CurrentBlock!.Usage.Fresh, Is.EqualTo(3_500), "30% of 10,000 plus the 500 since, not padded by a point");
        Assert.That(s.BlockPercent, Is.EqualTo(35).Within(0.001));
        Assert.That(s.CurrentBlock.EndUtc, Is.EqualTo(Day.AddHours(15)), "the reset Claude gave");
    }

    [Test]
    public void The_percent_used_never_passes_100()
    {
        var s = BudgetCalculator.Compute([E(10.2, 100)], new OrchestratorSettings(), Day.AddHours(12), Day, TimeZoneInfo.Utc,
            reported: Report(101, 15, 11), reportedLimit: 10_000);

        Assert.That(s.CurrentBlock!.Usage.Fresh, Is.EqualTo(10_000), "101% (Claude's own, however it got above 100) still caps at 100%");
        Assert.That(s.BlockPercent, Is.EqualTo(100).Within(0.001));
    }

    [Test]
    public void A_report_older_than_an_hour_but_inside_its_window_still_drives_the_block()
    {
        // Reported at 9:00, now 11:30: two and a half hours old, its window runs to 13:00. Stale, so the 300 spent
        // since the report is added to Claude's own 20%.
        var events = new[] { E(8.5, 2000), E(10, 300) };

        var s = BudgetCalculator.Compute(events, new OrchestratorSettings(), Day.AddHours(11.5), Day, TimeZoneInfo.Utc,
            reported: Report(20, 13, 9));

        Assert.That((s.BlockLimitSource, s.BlockLimit), Is.EqualTo((LimitSource.Reported, 9_523L)), "2,000 over 21%");
        Assert.That(s.CurrentBlock!.EndUtc, Is.EqualTo(Day.AddHours(13)));
        Assert.That(s.CurrentBlock.Usage.Fresh, Is.EqualTo((long)(0.20 * 9_523) + 300), "Claude's own 20%, not padded, plus the 300 since");
    }

    [Test]
    public void A_report_past_its_reset_starts_a_new_window_still_sized_by_its_reports()
    {
        var learned = new LimitCalibration([new LimitObservation(LimitKind.Session, Day.AddDays(-2), Day.AddDays(-2).AddHours(1), 5_000)]);
        var events = new[] { E(10.5, 1000), E(11.75, 400) };

        var s = BudgetCalculator.Compute(events, new OrchestratorSettings(), Day.AddHours(12), Day, TimeZoneInfo.Utc,
            learned: learned, reported: Report(60, 11.5, 11), reportedLimit: 10_000);

        // The limit didn't change with the reset: the last window's reports size it, not an older wall's 5,000 (M.ORCH-T387).
        Assert.That((s.BlockLimitSource, s.BlockLimit), Is.EqualTo((LimitSource.Reported, 10_000L)));
        Assert.That((s.CurrentBlock!.StartUtc, s.CurrentBlock.Usage.Fresh), Is.EqualTo((Day.AddHours(11) + TimeSpan.FromMinutes(40), 400L)),
            "the new window from its first message: neither the stale percent nor the old window's 1,000");
        Assert.That(s.BlockPercent, Is.EqualTo(4).Within(0.001));

        s = BudgetCalculator.Compute(events, new OrchestratorSettings(), Day.AddHours(12), Day, TimeZoneInfo.Utc, learned: learned, reported: Report(60, 11.5, 11));
        Assert.That((s.BlockLimitSource, s.BlockLimit), Is.EqualTo((LimitSource.Learned, 5_000L)), "with no reports to size it, the learned limit");
    }

    [Test]
    public void A_learned_limit_this_window_disproved_never_sizes_the_reported_one()
    {
        // Learned 5,000 from an old wall; this window has spent 7,000 with no wall, and Claude says 70% at 11:00 with no
        // usage samples moved enough to size it yet: the window's own tokens over its percent size it, not the stale 5,000.
        var learned = new LimitCalibration([new LimitObservation(LimitKind.Session, Day.AddDays(-2), Day.AddDays(-2).AddHours(1), 5_000)]);
        var events = new[] { E(10.5, 7_000) };

        var s = BudgetCalculator.Compute(events, new OrchestratorSettings(), Day.AddHours(11.5), Day, TimeZoneInfo.Utc,
            learned: learned, reported: Report(70, 15, 11));

        Assert.That((s.BlockLimitSource, s.BlockLimit, s.BlockLimitOutdated), Is.EqualTo((LimitSource.Reported, 9_859L, true)), "7,000 over 71%");
    }

    [Test]
    public void A_report_under_MinPercent_is_not_used_to_size_the_limit()
    {
        var events = new[] { E(10.5, 400) };
        var under = Report(ReportedUsage.MinPercent - 1, 15, 11);

        var s = BudgetCalculator.Compute(events, new OrchestratorSettings(), Day.AddHours(12), Day, TimeZoneInfo.Utc, reported: under);
        Assert.That(s.BlockLimitSource, Is.EqualTo(LimitSource.Peak), "4% is too coarse to imply a limit");

        var atMin = BudgetCalculator.Compute(events, new OrchestratorSettings(), Day.AddHours(12), Day, TimeZoneInfo.Utc,
            reported: Report(ReportedUsage.MinPercent, 15, 11));
        Assert.That((atMin.BlockLimitSource, atMin.BlockLimit), Is.EqualTo((LimitSource.Reported, 6_666L)), "400 over 6% once it reaches 5%");

        var sized = BudgetCalculator.Compute(events, new OrchestratorSettings(), Day.AddHours(12), Day, TimeZoneInfo.Utc,
            reported: under, reportedLimit: 10_000);
        Assert.That(sized.BlockLimitSource, Is.EqualTo(LimitSource.Reported), "a low percent is still the truth about what's used");
        Assert.That(sized.CurrentBlock!.Usage.Fresh, Is.EqualTo(400), "Claude's own 4% of a limit sized elsewhere");
    }

    [Test]
    public void A_manual_limit_never_shows_less_than_claudes_reported_percent()
    {
        // 2,600 local tokens of a 10,000 budget is 26%, but Claude said 36% (M.ORCH-T179): the percent is at least 36.
        var events = new[] { E(10.2, 2600) };
        var manual = BudgetCalculator.Compute(events, new OrchestratorSettings { BlockBudget = 10_000 }, Day.AddHours(12), Day, TimeZoneInfo.Utc,
            reported: Report(36, 15, 11));
        var ahead = BudgetCalculator.Compute([E(10.2, 5000)], new OrchestratorSettings { BlockBudget = 10_000 }, Day.AddHours(12), Day, TimeZoneInfo.Utc,
            reported: Report(36, 15, 11));

        Assert.That(manual.BlockLimitSource, Is.EqualTo(LimitSource.Manual));
        Assert.That(manual.BlockPercent, Is.EqualTo(36).Within(0.001));
        Assert.That(manual.BlockRemaining, Is.EqualTo(6_400));
        Assert.That(ahead.BlockPercent, Is.EqualTo(50).Within(0.001), "a local count already past Claude's percent stands");
    }

    [Test]
    public void A_manual_limit_shows_claudes_exact_percent_even_when_the_implied_token_floor_isnt_round()
    {
        // M.ORCH-T801: 60% of a 2,731 budget is 1,638.6 tokens, which can only floor to 1,638; re-deriving the percent
        // from that floor (as M.ORCH-T794 found for the Reported source) would drift it to ~59.978%, not Claude's 60%.
        var events = new[] { E(10.2, 1_000) };
        var s = BudgetCalculator.Compute(events, new OrchestratorSettings { BlockBudget = 2_731 }, Day.AddHours(12), Day, TimeZoneInfo.Utc,
            reported: Report(60, 15, 11));

        Assert.That(s.BlockLimitSource, Is.EqualTo(LimitSource.Manual));
        Assert.That(s.CurrentBlock!.Usage.Fresh, Is.EqualTo(1_638), "the floor still bumps the shown token count up to match the reported percent");
        Assert.That(s.BlockPercent, Is.EqualTo(60), "taken straight from Claude's percent, not re-derived from the floored token count");
    }

    [Test]
    public void A_ledger_restarted_part_way_through_the_week_sizes_no_weekly_limit_and_the_percent_stands_alone()
    {
        // The week began 3 days ago; a /login 2 hours ago restarted the ledger, so its 43.5k is only what the new account spent
        // since, not the 77% Claude reports for the whole week. Sized from it, the week read 11.5k (M.ORCH-T485).
        var now = Day.AddHours(12);
        var switchedAt = Day.AddHours(10);
        var events = new[] { new UsageEvent(Day.AddHours(10.5), new TokenUsage(0, 0, 0, 43_500)) };
        var reported = new ReportedUsage(null, new ReportedWindow(77, Day.AddDays(4)), Day.AddHours(11.5));

        var s = BudgetCalculator.Compute(events, new OrchestratorSettings(), now, Day, TimeZoneInfo.Utc,
            reported: reported, ledgerFromUtc: switchedAt);

        Assert.That(s.WeeklyLimit, Is.Null, "no limit sized from part of the week");
        Assert.That(s.WeeklyLimitSource, Is.Null);
        Assert.That(s.WeeklyPercent, Is.EqualTo(77), "Claude's own percent still shows");
        Assert.That(s.WeeklyLedgerPartial, Is.True);
        Assert.That(BudgetCalculator.ImpliedLimit(events, reported.Week, TimeSpan.FromDays(7), reported.AtUtc, switchedAt), Is.Null);

        var whole = BudgetCalculator.Compute(events, new OrchestratorSettings(), now, Day, TimeZoneInfo.Utc,
            reported: reported, ledgerFromUtc: Day.AddDays(-4));
        Assert.That(whole.WeeklyLimitSource, Is.EqualTo(LimitSource.Reported), "a ledger spanning the week still sizes it");
        Assert.That(whole.WeeklyLedgerPartial, Is.False);
    }

    [Test]
    public void A_ledger_restarted_part_way_through_the_week_keeps_the_learned_weekly_limit()
    {
        var now = Day.AddHours(12);
        var events = new[] { new UsageEvent(Day.AddHours(10.5), new TokenUsage(0, 0, 0, 43_500)) };
        var reported = new ReportedUsage(null, new ReportedWindow(77, Day.AddDays(4)), Day.AddHours(11.5));
        var learned = new LimitCalibration([new LimitObservation(LimitKind.Weekly, Day.AddDays(-10), Day.AddDays(-9), 400_000_000)]);

        var s = BudgetCalculator.Compute(events, new OrchestratorSettings(), now, Day, TimeZoneInfo.Utc,
            learned: learned, reported: reported, ledgerFromUtc: Day.AddHours(10));

        Assert.That((s.WeeklyLimitSource, s.WeeklyLimit), Is.EqualTo(((LimitSource?)LimitSource.Learned, (long?)400_000_000)));
        Assert.That(s.WeeklyLedgerPartial, Is.False);
    }
}
