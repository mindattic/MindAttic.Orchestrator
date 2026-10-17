using MindAttic.Orchestrator.Budget;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class ReportedLimitTests
{
    private static readonly DateTime Day = new(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Reset = Day.AddHours(14);   // window 9:00-14:00

    private static UsageEvent E(double hour, long output) =>
        new(Day.AddHours(hour), new TokenUsage(0, 0, 1_000_000, output));

    private static ReportedUsage Report(double percent, double hour, DateTime? reset = null) =>
        new(new ReportedWindow(percent, reset ?? Reset), null, Day.AddHours(hour));

    private static UsageSample S(double hour, double percent, long fresh, DateTime? reset = null) =>
        new(Day.AddHours(hour), percent, reset ?? Reset, fresh);

    [Test]
    public void Record_keeps_one_sample_per_report_time()
    {
        var samples = new List<UsageSample>();

        Assert.That(ReportedLimit.Record(samples, Report(10, 10), [], Day.AddHours(10)), Is.True);
        Assert.That(ReportedLimit.Record(samples, Report(12, 10), [], Day.AddHours(11)), Is.False, "same report time, even re-read later");
        Assert.That(ReportedLimit.Record(samples, Report(12, 10.5), [], Day.AddHours(11)), Is.True);
        Assert.That(samples.Select(s => s.AtUtc), Is.EqualTo(new[] { Day.AddHours(10), Day.AddHours(10.5) }));
        Assert.That(samples[0].Percent, Is.EqualTo(10), "the first sample isn't overwritten");
    }

    [Test]
    public void Record_skips_a_report_made_after_its_window_reset()
    {
        // An idle tab restamps its last response (97% of the 9:00-14:00 window) at 14:30: the next window's tokens aren't its.
        var samples = new List<UsageSample>();
        Assert.That(ReportedLimit.Record(samples, Report(97, 14.5), [E(14.2, 5_000)], Day.AddHours(14.5)), Is.False);
        Assert.That(ReportedLimit.Record(samples, Report(97, 14), [], Day.AddHours(14)), Is.False, "at the reset it's already the next window");
        Assert.That(samples, Is.Empty);
    }

    [Test]
    public void Record_ignores_a_missing_report_or_five_hour_window()
    {
        var samples = new List<UsageSample>();

        Assert.That(ReportedLimit.Record(samples, null, [], Day), Is.False);
        Assert.That(ReportedLimit.Record(samples, new ReportedUsage(null, new ReportedWindow(50, Reset), Day), [], Day), Is.False);
        Assert.That(samples, Is.Empty);
    }

    [Test]
    public void Record_counts_only_fresh_tokens_inside_the_window_up_to_the_report()
    {
        var samples = new List<UsageSample>();
        UsageEvent[] events = [E(8.5, 1), E(9, 10), E(9.5, 100), E(10, 1_000), E(10.5, 10_000)];

        ReportedLimit.Record(samples, Report(20, 10), events, Day.AddHours(10));

        Assert.That(samples.Single().WindowFresh, Is.EqualTo(1_110), "9:00 through 10:00 inclusive; not 8:30 before the window or 10:30 after the report");
        Assert.That(samples.Single().ResetUtc, Is.EqualTo(Reset));
    }

    [Test]
    public void Record_trims_samples_older_than_a_week()
    {
        var samples = new List<UsageSample> { S(-24 * 8, 5, 0), S(-24 * 7, 5, 0), S(-24 * 6, 5, 0) };

        ReportedLimit.Record(samples, Report(10, 0), [], Day);

        Assert.That(samples.Select(s => s.AtUtc), Is.EqualTo(new[] { Day.AddDays(-7), Day.AddDays(-6), Day }),
            "8 days old goes; exactly 7 days stays");
    }

    [Test]
    public void Estimate_is_null_with_no_samples_or_a_single_report()
    {
        Assert.That(ReportedLimit.Estimate([]), Is.Null);
        Assert.That(ReportedLimit.Estimate([S(10, 40, 5_000)]), Is.Null);
    }

    [Test]
    public void Estimate_needs_the_percent_to_move_at_least_MinDelta()
    {
        Assert.That(ReportedLimit.Estimate([S(10, 10, 0), S(11, 10 + ReportedLimit.MinDelta - 1, 1_000)]), Is.Null);
        Assert.That(ReportedLimit.Estimate([S(10, 10, 0), S(11, 10 + ReportedLimit.MinDelta, 1_000)]), Is.Not.Null);
    }

    [Test]
    public void Estimate_widens_the_delta_by_one_so_the_limit_errs_small()
    {
        // 1,000 tokens moved it 4 points: taken as 5, so 1,000 / 5% = 20,000 (not 25,000).
        Assert.That(ReportedLimit.Estimate([S(10, 10, 0), S(11, 14, 1_000)]), Is.EqualTo(20_000));
        // 9,000 tokens moved it 8 points: taken as 9 -> 100,000.
        Assert.That(ReportedLimit.Estimate([S(10, 2, 1_000), S(11, 10, 10_000)]), Is.EqualTo(100_000));
    }

    [Test]
    public void Estimate_uses_the_first_and_last_report_of_a_window_by_time_not_list_order()
    {
        var samples = new[] { S(12, 30, 9_000), S(10, 10, 1_000), S(11, 15, 4_000) };

        // 8,000 tokens over 20 points, taken as 21.
        Assert.That(ReportedLimit.Estimate(samples), Is.EqualTo((long)(8_000 / (21 / 100.0))));
    }

    [Test]
    public void Estimate_takes_the_newest_window_that_moved()
    {
        var older = Reset.AddHours(-5);
        var samples = new[]
        {
            S(5, 10, 0, older), S(8, 50, 41_000, older),   // older window: 41,000 over 40 (+1) -> 100,000
            S(10, 10, 0), S(13, 14, 1_000),                // newer window: 1,000 over 4 (+1) -> 20,000
        };

        Assert.That(ReportedLimit.Estimate(samples), Is.EqualTo(20_000));
    }

    [Test]
    public void Estimate_falls_back_to_an_older_window_when_the_newest_hasnt_moved_enough()
    {
        var older = Reset.AddHours(-5);
        var samples = new[]
        {
            S(5, 10, 0, older), S(8, 50, 41_000, older),
            S(10, 10, 0), S(13, 12, 50_000),               // newest moved only 2 points
        };

        Assert.That(ReportedLimit.Estimate(samples), Is.EqualTo(100_000));
    }

    [Test]
    public void Estimate_ignores_a_window_whose_local_spend_didnt_grow()
    {
        var older = Reset.AddHours(-5);

        // The percent moved but local transcripts recorded nothing (another account, claude.ai, another machine).
        Assert.That(ReportedLimit.Estimate([S(10, 10, 5_000), S(13, 30, 5_000)]), Is.Null);
        Assert.That(ReportedLimit.Estimate([S(10, 10, 5_000), S(13, 30, 4_000)]), Is.Null, "a shrinking count never sizes a limit");
        Assert.That(ReportedLimit.Estimate([S(5, 10, 0, older), S(8, 50, 41_000, older), S(10, 10, 5_000), S(13, 30, 5_000)]),
            Is.EqualTo(100_000), "skipped for the older window that did spend");
    }
    // Every open session's statusline rewrites claude-usage.json each minute with its own last response's percent,
    // so an idle one stamps a stale, lower percent with the current time (M.ORCH-T179).
    [Test]
    public void Record_skips_a_report_below_one_already_recorded_for_its_window()
    {
        var samples = new List<UsageSample> { S(10, 35, 3_500) };

        Assert.That(ReportedLimit.Record(samples, Report(12, 10.1), [], Day.AddHours(10.1)), Is.False, "stale, from an idle session");
        Assert.That(ReportedLimit.Record(samples, Report(12, 10.2, Reset.AddHours(5)), [], Day.AddHours(10.2)), Is.True, "another window isn't compared");
        Assert.That(ReportedLimit.Record(samples, Report(35, 10.3), [], Day.AddHours(10.3)), Is.True, "level is fine");
    }

    [Test]
    public void Estimate_ignores_stale_dips_recorded_earlier()
    {
        // 10% to 30% over 2,000 tokens, then a stale 12% last: the dip isn't the window's end.
        var samples = new List<UsageSample> { S(10, 10, 1_000), S(11, 30, 3_000), S(11.5, 12, 3_100) };

        Assert.That(ReportedLimit.Estimate(samples), Is.EqualTo((long)(2_000 / (21 / 100.0))));
    }

    [Test]
    public void Highest_raises_a_stale_report_to_the_highest_recorded_for_its_window()
    {
        var samples = new List<UsageSample> { S(10, 30, 0), S(10.5, 35, 0), S(10.6, 35, 0), S(10, 80, 0, Reset.AddHours(-5)) };

        var raised = ReportedLimit.Highest(samples, Report(12, 11));

        Assert.That(raised!.FiveHour!.Percent, Is.EqualTo(35));
        Assert.That(raised.AtUtc, Is.EqualTo(Day.AddHours(10.6)), "local spend is added from the latest sample at that percent");
        Assert.That(raised.FiveHour.ResetUtc, Is.EqualTo(Reset));
        Assert.That(raised.WeekAt, Is.EqualTo(Day.AddHours(11)), "the week still comes from the current report, and is sized as of its time");
        Assert.That(ReportedLimit.Highest(samples, Report(40, 11)), Is.EqualTo(Report(40, 11)), "a higher report stands");
        Assert.That(ReportedLimit.Highest(samples, null), Is.Null);
    }

    [Test]
    public void Highest_with_a_report_from_a_window_that_reset_uses_the_running_windows_highest_sample()
    {
        // An idle session last answered in the 4:00-9:00 window and keeps stamping it; the running window was at 36% (M.ORCH-T179).
        var samples = new List<UsageSample> { S(10, 30, 0), S(10.5, 36, 0), S(10.6, 34, 0), S(8, 90, 0, Reset.AddHours(-5)) };
        var now = Day.AddHours(11);

        var raised = ReportedLimit.Highest(samples, Report(26, 11, Reset.AddHours(-5)), now);

        Assert.That(raised!.FiveHour, Is.EqualTo(new ReportedWindow(36, Reset)));
        Assert.That(raised.AtUtc, Is.EqualTo(Day.AddHours(10.5)));
        Assert.That(raised.WeekAt, Is.EqualTo(Day.AddHours(11)), "the week is the current report's");
        Assert.That(ReportedLimit.Highest(samples, null, now)!.FiveHour!.Percent, Is.EqualTo(36), "no report at all");
        Assert.That(ReportedLimit.Highest([], Report(26, 11, Reset.AddHours(-5)), now), Is.EqualTo(Report(26, 11, Reset.AddHours(-5))),
            "nothing recorded for the running window: left as it was");
        Assert.That(ReportedLimit.Highest(samples, Report(26, 11), now)!.FiveHour!.Percent, Is.EqualTo(36), "a stale report in the running window still rises");
    }
}
