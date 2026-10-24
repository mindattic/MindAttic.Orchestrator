using System.Text.Json;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class BudgetTests
{
    private static readonly DateTime Day = new(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc);

    private static UsageEvent E(double hour, long output) =>
        new(Day.AddHours(hour), new TokenUsage(0, 0, 1_000_000, output));

    [Test]
    public void Blocks_start_on_the_ten_minutes_before_the_first_message_and_last_five_hours()
    {
        // 9:33, 14:27, 14:33, 20:00
        var blocks = BudgetCalculator.Blocks([E(9.55, 10), E(14.45, 20), E(14.55, 30), E(20, 40)]);

        Assert.That(blocks, Has.Count.EqualTo(3));
        Assert.That(blocks[0].StartUtc, Is.EqualTo(Day.AddHours(9.5)), "Claude Code's resets land on 10-minute marks, not the hour");
        Assert.That(blocks[0].EndUtc, Is.EqualTo(Day.AddHours(14.5)));
        Assert.That(blocks[0].Usage.Output, Is.EqualTo(30), "9:33 and 14:27 share the 9:30-14:30 block");
        Assert.That(blocks[1].StartUtc, Is.EqualTo(Day.AddHours(14.5)));
        Assert.That(blocks[2].Usage.Output, Is.EqualTo(40));
    }

    [Test]
    public void Claudes_own_percent_sizes_the_limit_and_fixes_the_reset()
    {
        // Claude said 24% at 11:00 with 2,400 spent since the block it resets at 15:20 began; 600 more since.
        var reported = new ReportedUsage(new ReportedWindow(24, Day.AddHours(15 + 1.0 / 3)), new ReportedWindow(50, Day.AddDays(2)), Day.AddHours(11));
        var events = new[] { E(-30, 9_000), E(10.5, 2400), E(11.5, 600) };

        var s = BudgetCalculator.Compute(events, new OrchestratorSettings(), Day.AddHours(12), Day, TimeZoneInfo.Utc, reported: reported);

        Assert.That((s.BlockLimitSource, s.BlockLimit), Is.EqualTo((LimitSource.Reported, 9_600L)), "2,400 over 25%: a whole-percent report could be up to a point low, so the limit errs small");
        Assert.That(s.BlockPercent, Is.EqualTo(24).Within(0.01), "Claude's own 24%, not padded or added to with what's been spent since (M.ORCH-T788)");
        Assert.That(s.CurrentBlock!.EndUtc, Is.EqualTo(Day.AddHours(15 + 1.0 / 3)), "the reset Claude gave, not the estimate");
        Assert.That((s.WeeklyLimitSource, s.WeeklyLimit), Is.EqualTo((LimitSource.Reported, 22_352L)), "11,400 this week over 51%");
        Assert.That(s.WeekResetUtc, Is.EqualTo(Day.AddDays(2)));

        var manual = BudgetCalculator.Compute(events, new OrchestratorSettings { BlockBudget = 5_000 }, Day.AddHours(12), Day, TimeZoneInfo.Utc, reported: reported);
        Assert.That(manual.BlockLimitSource, Is.EqualTo(LimitSource.Manual), "a budget you set still wins");
    }

    [Test]
    public void After_an_account_switch_Claudes_percent_is_what_counts_not_the_old_accounts_tokens()
    {
        // 9,000 local tokens this window came from the old account; the new one says 3% at 11:00, 200 spent since.
        var reported = new ReportedUsage(new ReportedWindow(3, Day.AddHours(15)), null, Day.AddHours(11));
        var events = new[] { E(10.2, 9000), E(11.5, 200) };

        var s = BudgetCalculator.Compute(events, new OrchestratorSettings(), Day.AddHours(12), Day, TimeZoneInfo.Utc, reported: reported, reportedLimit: 10_000);

        Assert.That(s.BlockLimitSource, Is.EqualTo(LimitSource.Reported));
        Assert.That(s.BlockPercent, Is.EqualTo(3).Within(0.01), "Claude's own 3%: not the 92% the local tokens say, and not padded by what's spent since (M.ORCH-T788)");
    }

    [Test]
    public void The_limit_is_sized_from_how_far_the_percent_moved_so_other_spending_cancels_out()
    {
        var samples = new List<UsageSample>();
        var reset = Day.AddHours(15);
        UsageEvent[] events = [E(10.1, 50_000), E(10.6, 900)]; // the 50,000 predates both reports (another account)
        Assert.That(ReportedLimit.Record(samples, new(new(10, reset), null, Day.AddHours(10.5)), events, Day.AddHours(10.5)), Is.True);
        Assert.That(ReportedLimit.Record(samples, new(new(10, reset), null, Day.AddHours(10.5)), events, Day.AddHours(10.6)), Is.False, "one sample per report");
        Assert.That(ReportedLimit.Estimate(samples), Is.Null, "one report can't show movement");

        ReportedLimit.Record(samples, new(new(13, reset), null, Day.AddHours(10.7)), events, Day.AddHours(10.7));
        Assert.That(ReportedLimit.Estimate(samples), Is.Null, "3 points is too coarse to size by");

        ReportedLimit.Record(samples, new(new(14, reset), null, Day.AddHours(10.8)), [.. events, E(10.75, 100)], Day.AddHours(10.8));
        Assert.That(ReportedLimit.Estimate(samples), Is.EqualTo(20_000), "1,000 spent while it moved 4 points, taken as 5: errs small, ignores the 50,000");
    }

    [Test]
    public void A_report_too_coarse_or_from_a_window_already_reset_is_not_used()
    {
        var events = new[] { E(10.5, 100) };
        var coarse = new ReportedUsage(new ReportedWindow(2, Day.AddHours(15)), null, Day.AddHours(11));
        var ended = new ReportedUsage(new ReportedWindow(60, Day.AddHours(11.5)), null, Day.AddHours(11));

        Assert.That(BudgetCalculator.Compute(events, new OrchestratorSettings(), Day.AddHours(12), Day, reported: coarse).BlockLimitIsPeak, Is.True, "2% is ±25%");
        Assert.That(BudgetCalculator.Compute(events, new OrchestratorSettings(), Day.AddHours(12), Day, reported: ended).BlockLimitIsPeak, Is.True);
    }

    [Test]
    public void The_report_the_statusline_saves_is_read_and_goes_stale()
    {
        var path = Path.Combine(Path.GetTempPath(), "usage-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, """{"rate_limits":{"five_hour":{"used_percentage":24,"resets_at":1792041600},"seven_day":{"used_percentage":72,"resets_at":1792533600}},"at":"2026-10-15T01:04:10Z"}""");
            var at = new DateTime(2026, 10, 15, 1, 4, 10, DateTimeKind.Utc);

            var r = ReportedUsage.Load(at.AddMinutes(5), path)!;
            Assert.That(r.FiveHour, Is.EqualTo(new ReportedWindow(24, DateTimeOffset.FromUnixTimeSeconds(1792041600).UtcDateTime)));
            Assert.That(r.Week!.Percent, Is.EqualTo(72));
            Assert.That(ReportedUsage.Load(at.AddHours(2), path), Is.Not.Null,
                "with only background agents running nothing redraws a statusline: the last report carries on, plus the transcripts since");
            Assert.That(ReportedUsage.Load(at.AddHours(6), path), Is.Null, "older than a whole window: its window has reset");
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void Without_a_budget_the_current_block_is_measured_against_the_peak_block()
    {
        var events = new[] { E(1, 1000), E(10, 250) };
        var s = BudgetCalculator.Compute(events, new OrchestratorSettings(), Day.AddHours(11), Day);

        Assert.That(s.BlockLimitIsPeak, Is.True);
        Assert.That(s.BlockLimit, Is.EqualTo(1000));
        Assert.That(s.BlockPercent, Is.EqualTo(25).Within(0.01));
        Assert.That(s.Today.CacheRead, Is.EqualTo(2_000_000), "cache reads are reported");
        Assert.That(s.CurrentBlock!.Usage.Fresh, Is.EqualTo(250), "but don't count toward the budget");
    }

    [Test]
    public void Beating_your_peak_is_context_not_an_alarm()
    {
        var s = BudgetCalculator.Compute([E(1, 100), E(10, 300)], new OrchestratorSettings(), Day.AddHours(11), Day);

        Assert.That(s.BlockPercent, Is.EqualTo(300).Within(0.01));
        Assert.That(s.Level, Is.EqualTo(BudgetLevel.Ok));
        Assert.That(s.ProjectedLimitUtc, Is.Null);
    }

    [Test]
    public void Peak_mode_withdraws_only_a_status_file_it_wrote()
    {
        var path = Path.Combine(Path.GetTempPath(), "rl-" + Guid.NewGuid().ToString("N") + ".json");
        var peak = BudgetCalculator.Compute([E(10, 1)], new OrchestratorSettings(), Day.AddHours(11), Day);
        try
        {
            File.WriteAllText(path, """{"percent_used": 5, "source": "someone else"}""");
            new RateLimitStatusWriter(path).Write(peak);
            Assert.That(File.Exists(path), Is.True, "someone else's file is left alone");

            var budgeted = BudgetCalculator.Compute([E(10, 1)], new OrchestratorSettings { BlockBudget = 10 }, Day.AddHours(11), Day);
            var writer = new RateLimitStatusWriter(path);
            writer.Write(budgeted);
            writer.Write(peak);
            Assert.That(File.Exists(path), Is.False);
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void A_set_budget_drives_level_and_projection()
    {
        var settings = new OrchestratorSettings { BlockBudget = 1000, WarnPercent = 80 };
        var s = BudgetCalculator.Compute([E(10, 850)], settings, Day.AddHours(11), Day);

        Assert.That(s.Level, Is.EqualTo(BudgetLevel.Warn));
        Assert.That(s.BlockRemaining, Is.EqualTo(150));
        Assert.That(s.FreshPerHour, Is.EqualTo(850).Within(0.01));
        Assert.That(s.ProjectedLimitUtc, Is.EqualTo(Day.AddHours(11) + TimeSpan.FromHours(150.0 / 850)).Within(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public void No_active_block_means_zero_percent()
    {
        var s = BudgetCalculator.Compute([E(1, 500)], new OrchestratorSettings { BlockBudget = 1000 }, Day.AddHours(12), Day);

        Assert.That(s.CurrentBlock, Is.Null);
        Assert.That(s.BlockPercent, Is.Zero);
        Assert.That(s.Level, Is.EqualTo(BudgetLevel.Ok));
    }

    [Test]
    public void Weekly_budget_counts_the_last_seven_days()
    {
        var settings = new OrchestratorSettings { WeeklyBudget = 10_000 };
        var s = BudgetCalculator.Compute([E(-24 * 8, 9000), E(-24, 9000)], settings, Day, Day);

        Assert.That(s.Week.Output, Is.EqualTo(9000));
        Assert.That(s.WeeklyPercent, Is.EqualTo(90).Within(0.01));
    }

    [Test]
    public void Writes_the_file_rl_check_reads()
    {
        var path = Path.Combine(Path.GetTempPath(), "rl-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var s = BudgetCalculator.Compute([E(10, 400)], new OrchestratorSettings { BlockBudget = 1000 }, Day.AddHours(11), Day);
            new RateLimitStatusWriter(path).Write(s);

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            Assert.That(root.GetProperty("percent_used").GetDouble(), Is.EqualTo(40));
            Assert.That(root.GetProperty("tokens_remaining").GetInt64(), Is.EqualTo(600));
            Assert.That(root.GetProperty("timestamp").GetString(), Does.EndWith("Z"));
            Assert.That(root.GetProperty("reset_time").GetString(), Is.Not.Empty);
        }
        finally { File.Delete(path); }
    }
}
