using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T1885: a login with no 5-hour window (a solo Ultra account) ran 90% of its week in two days with nothing to
/// slow it. Its week is now paced along a straight line to the weekly pace target: above the line new subagents start slower
/// and tool calls are held; at or under it, and at the stop, the pace is left as it is.</summary>
public sealed class WeeklyPaceTests
{
    private static readonly DateTime Reset = new(2026, 11, 25, 17, 0, 0, DateTimeKind.Utc);
    private static readonly OrchestratorSettings Settings = new() { ThrottleAgents = true, MaxAgentDelaySeconds = 60 };

    private static Pace Paced(int allowance, int running) =>
        new Pace(PaceMode.Open, 6, 10_000, 0, 1_000, null, Reset, "On pace.") { SubagentAllowance = allowance, Subagents = running };

    /// <summary><paramref name="daysIn"/> days into the week, at <paramref name="used"/>% and burning <paramref name="perHour"/> points an hour, against a 95% line.</summary>
    private static Utilization Week(double daysIn, double used, double perHour) =>
        new(Reset.AddDays(-7), Reset, Reset.AddDays(-7 + daysIn), 95, [], used, perHour, used + perHour * (7 - daysIn) * 24, TimeSpan.Zero);

    [Test]
    public void Above_the_line_new_subagents_start_slower_and_tool_calls_are_held()
    {
        // Ultra's own case: 90% two days in, where the line to 95% is near 27%.
        var paced = Pacer.WeeklyPace(Paced(16, 4), Week(2, 90, 2), 0, 16, Settings);

        Assert.That(paced.SubagentAllowance, Is.EqualTo(1), "far over: one at a time");
        Assert.That(paced.AgentDelaySeconds, Is.EqualTo(60), "the burn is many times what's left to the target: the most hold");
        Assert.That(paced.WeeklyPaceLinePercent, Is.EqualTo(95 * 2 / 7.0).Within(1e-9));
        Assert.That(paced.WeeklyPaceUsedPercent, Is.EqualTo(90));
        Assert.That(paced.Stop, Is.False, "slowed, never stopped");
        Assert.That(paced.SubagentNote, Does.Contain("no 5-hour window"));
    }

    [Test]
    public void Just_over_the_line_it_slows_by_one()
    {
        var paced = Pacer.WeeklyPace(Paced(16, 4), Week(3.5, 50, 0.5), 0, 16, Settings);
        Assert.That(paced.SubagentAllowance, Is.EqualTo(3), "2.5 points over: one fewer than running");
        Assert.That(paced.AgentDelaySeconds, Is.EqualTo(0), "its burn fits what's left: no hold");
    }

    [Test]
    public void At_or_under_the_line_at_the_stop_or_with_no_week_the_pace_is_left_alone()
    {
        var pace = Paced(16, 4);
        Assert.That(Pacer.WeeklyPace(pace, Week(3.5, 40, 1), 0, 16, Settings), Is.SameAs(pace), "under the line");
        var stopped = pace with { Stop = true, SubagentAllowance = 0 };
        Assert.That(Pacer.WeeklyPace(stopped, Week(2, 90, 2), 0, 16, Settings), Is.SameAs(stopped), "the cutoff stands");
        var ended = Week(2, 90, 2) with { Ended = true };
        Assert.That(Pacer.WeeklyPace(pace, ended, 0, 16, Settings), Is.SameAs(pace), "no week running");
    }

    [Test]
    public void Only_a_login_with_no_five_hour_window_is_paced_by_its_week()
    {
        Assert.That(AccountPacer.HasNoFiveHourWindow("Ultra"), Is.True);
        Assert.That(AccountPacer.HasNoFiveHourWindow("Team · Max"), Is.False, "a Team seat keeps its cutoffs only");
        Assert.That(AccountPacer.HasNoFiveHourWindow(null), Is.False, "unknown plan: left as it was");
    }
}
