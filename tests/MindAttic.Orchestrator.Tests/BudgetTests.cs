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
    public void Blocks_start_on_the_hour_and_last_five_hours()
    {
        var blocks = BudgetCalculator.Blocks([E(9.5, 10), E(13.9, 20), E(14.1, 30), E(20, 40)]);

        Assert.That(blocks, Has.Count.EqualTo(3));
        Assert.That(blocks[0].StartUtc, Is.EqualTo(Day.AddHours(9)));
        Assert.That(blocks[0].Usage.Output, Is.EqualTo(30), "9:30 and 13:54 share the 9:00-14:00 block");
        Assert.That(blocks[1].StartUtc, Is.EqualTo(Day.AddHours(14)));
        Assert.That(blocks[2].Usage.Output, Is.EqualTo(40));
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
