using MindAttic.Orchestrator.Budget;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T801: "reached" is decided from the full-precision planned and actual percents, not the tenth they're
/// rounded to for the stored row -- rounding first could call a window reached up to <see cref="PacePlan.Reach"/> / 2 early.</summary>
public sealed class PacePlanTests
{
    private static readonly DateTime End = new(2026, 10, 24, 18, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Reached_is_decided_on_full_precision_not_the_rounded_row()
    {
        var now = End.AddHours(-1);
        var block = new UsageBlock(End.AddHours(-5), End, default, now);
        // Plan (the ceiling) is 50.0%; actual is 49.46%, short of the 49.5% reach line by 0.04 at full precision.
        // Rounded to a tenth first, 49.46 rounds up to 49.5, which equals 50.0 - 0.5 exactly: the old code called that
        // reached a tick early.
        var budget = new BudgetStatus(block, 10_000_000, false, 49.46, default, default, null, null, 0, null, 90);
        var week = new WeeklyCap(80, End.AddDays(3), 2, 50.0, 3, 60);
        var rows = new List<PlannedWindow>();
        var closed = new List<string>();

        PacePlan.Observe(rows, "me", budget, week, now, closed);

        var row = rows.Single(r => r.Kind == PlannedWindow.FiveHour);
        Assert.That(row.PlannedPercent, Is.EqualTo(50.0));
        Assert.That(row.ActualPercent, Is.EqualTo(49.5), "still rounds to a tenth for the row");
        Assert.That(row.ReachedUtc, Is.Null, "49.46% is short of the 50% plan's reach line (49.5%) at full precision");
    }

    [Test]
    public void Reached_once_the_full_precision_actual_meets_the_reach_line()
    {
        var now = End.AddHours(-1);
        var block = new UsageBlock(End.AddHours(-5), End, default, now);
        var budget = new BudgetStatus(block, 10_000_000, false, 49.5, default, default, null, null, 0, null, 90);
        var week = new WeeklyCap(80, End.AddDays(3), 2, 50.0, 3, 60);
        var rows = new List<PlannedWindow>();
        var closed = new List<string>();

        PacePlan.Observe(rows, "me", budget, week, now, closed);

        var row = rows.Single(r => r.Kind == PlannedWindow.FiveHour);
        Assert.That(row.ReachedUtc, Is.EqualTo(now));
    }
}
