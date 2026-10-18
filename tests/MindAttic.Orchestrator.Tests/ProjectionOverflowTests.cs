using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T448: a tiny rate projected a moment past what a TimeSpan or DateTime holds, and the burndown threw.</summary>
public sealed class ProjectionOverflowTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    [TestCase(0L)]
    [TestCase(1L)]
    [TestCase(100L)]
    [TestCase(499L)]
    public void A_week_that_used_a_few_tokens_has_no_projection_instead_of_throwing(long used)
    {
        var settings = new OrchestratorSettings { WeeklyBudget = 1_000_000_000, WeeklyReset = "Mon 12:00" };
        var events = used > 0 ? new[] { new UsageEvent(Now.AddDays(-1), new TokenUsage(0, 0, 0, used)) } : [];
        var budget = BudgetCalculator.Compute(events, settings, Now, Now.Date, TimeZoneInfo.Utc);

        var w = Burndown.Compute(budget, [], Now);

        Assert.That(w.ExhaustedUtc, Is.Null);
        Assert.That(w.RunsOut, Is.False);
    }

    [Test]
    public void A_week_with_a_real_pace_is_still_projected()
    {
        var settings = new OrchestratorSettings { WeeklyBudget = 1_000_000_000, WeeklyReset = "Mon 12:00" };
        var budget = BudgetCalculator.Compute([new UsageEvent(Now.AddDays(-1), new TokenUsage(0, 0, 0, 10_000_000))], settings, Now, Now.Date, TimeZoneInfo.Utc);

        Assert.That(Burndown.Compute(budget, [], Now).ExhaustedUtc, Is.Not.Null);
    }

    [Test]
    public void A_five_hour_window_that_used_a_token_has_no_projection_instead_of_throwing()
    {
        var settings = new OrchestratorSettings { BlockBudget = 100_000_000_000 };
        var events = new[] { new UsageEvent(Now.AddHours(-4), new TokenUsage(0, 0, 0, 1)) };

        BudgetStatus? b = null;
        Assert.DoesNotThrow(() => b = BudgetCalculator.Compute(events, settings, Now, Now.Date, TimeZoneInfo.Utc));
        Assert.That(b!.ProjectedLimitUtc, Is.Null);
    }

    [Test]
    public void The_utilization_line_at_a_tiny_rate_never_hits_the_limit_instead_of_throwing()
    {
        var u = new Utilization(Now.AddHours(-1), Now.AddHours(4), Now, 99, [], 0.0001, 1e-12, 0, TimeSpan.Zero);

        Assert.That(u.HitsLimitUtc, Is.Null);
    }

    [Test]
    public void Projection_guards_unrepresentable_moments()
    {
        Assert.That(Projection.After(Now, double.PositiveInfinity), Is.Null);
        Assert.That(Projection.After(Now, double.NaN), Is.Null);
        Assert.That(Projection.After(Now, 1e15), Is.Null);
        Assert.That(Projection.After(Now, 2), Is.EqualTo(Now.AddHours(2)));
        Assert.That(Projection.Before(Now, 2, Now.AddHours(1)), Is.Null);
        Assert.That(Projection.Before(Now, 1e15, Now.AddHours(1)), Is.Null);
        Assert.That(Projection.Before(Now, 0.5, Now.AddHours(1)), Is.EqualTo(Now.AddMinutes(30)));
    }
}
