using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T1592: under the pace guards' start line on both the 5-hour window and the week, no pace guard holds anything
/// (a CLI was told it was paced at 6% of its 5-hour window and 1% of its week); from the line on they work as before.</summary>
public sealed class PaceGuardDormancyTests
{
    private static readonly DateTime End = new(2026, 11, 10, 20, 0, 0, DateTimeKind.Utc);
    private static readonly OrchestratorSettings Settings = new() { MaxSubagents = 8, MaxConcurrentSessions = 6 };

    private static Pace Throttled() =>
        new Pace(PaceMode.Coast, 0, 1_000, 5_000, 2_000, "5-hour", End, "Coasting.")
        { SubagentAllowance = 1, Subagents = 2, AgentDelaySeconds = 30, SpareSessions = -2, CoolOffPercent = 120, CoolOffClis = 3 };

    private static BudgetStatus Budget(double fiveHour, double? week) =>
        new(null, 1_000_000, false, fiveHour, default, default, null, week, 0, null, 80);

    [Test]
    public void Under_the_line_on_both_windows_runs_full_speed()
    {
        var pace = Pacer.Dormant(Throttled(), Budget(6, 1), Settings, 2);
        Assert.Multiple(() =>
        {
            Assert.That(pace.GuardsDormant, Is.True);
            Assert.That(pace.Mode, Is.EqualTo(PaceMode.Open));
            Assert.That(pace.SessionCap, Is.EqualTo(6));
            Assert.That(pace.SubagentAllowance, Is.EqualTo(8));
            Assert.That(pace.AgentDelaySeconds, Is.Zero);
            Assert.That(pace.CoolOffPercent, Is.Null);
            Assert.That(pace.SpareSessions, Is.EqualTo(4));
            Assert.That(pace.Advise, Is.False);
            Assert.That(pace.SubagentNote, Does.Contain("full speed"));
        });
    }

    [TestCase(90, 1)]
    [TestCase(6, 90)]
    [TestCase(95, 95)]
    public void At_the_line_on_either_window_the_guards_wake(double fiveHour, double week)
    {
        var pace = Pacer.Dormant(Throttled(), Budget(fiveHour, week), Settings, 2);
        Assert.That(pace.GuardsDormant, Is.False);
        Assert.That(pace.SubagentAllowance, Is.EqualTo(1));
    }

    [Test]
    public void The_line_is_the_setting()
    {
        var pace = Pacer.Dormant(Throttled(), Budget(50, 10), Settings with { PaceGuardStartPercent = 40 }, 2);
        Assert.That(pace.GuardsDormant, Is.False);
    }

    [Test]
    public void A_stop_or_an_unpaced_pace_is_left_as_it_is()
    {
        var stopped = Throttled() with { Stop = true };
        Assert.That(Pacer.Dormant(stopped, Budget(6, 1), Settings, 2), Is.SameAs(stopped));
        var unpaced = new Pace(PaceMode.Unpaced, 6, 0, 0, null, null, null, "Pacing is off.");
        Assert.That(Pacer.Dormant(unpaced, Budget(6, 1), Settings, 2), Is.SameAs(unpaced));
    }
}
