using MindAttic.Orchestrator.Budget;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T785: the cool-off for one login split across many CLIs. Past the pace line its new subagents start slower,
/// never stopped; under it, with one CLI, or at the hard cutoff, the pace is left as it is.</summary>
public sealed class CoolOffTests
{
    private static readonly DateTime End = new(2026, 10, 24, 20, 0, 0, DateTimeKind.Utc);

    private static Pace Paced(int allowance, int running) =>
        new Pace(PaceMode.Open, 6, 10_000, 0, 1_000, "5-hour", End, "On pace.") { SubagentAllowance = allowance, Subagents = running };

    /// <summary>Two hours into the window, on course for <paramref name="projected"/>% against the 98% line.</summary>
    private static Utilization Trend(double projected) =>
        new(End.AddHours(-5), End, End.AddHours(-3), 98, [], 50, 10, projected, TimeSpan.Zero);

    [Test]
    public void Past_the_line_on_many_clis_new_subagents_start_slower()
    {
        var pace = Paced(8, 6);
        var slowed = Pacer.CoolOff(pace, Trend(118), 3);
        Assert.That(slowed.SubagentAllowance, Is.EqualTo(4), "6 running less 2 for 20 points over: the excess drains as they finish");
        Assert.That((slowed.CoolOffPercent, slowed.CoolOffClis, slowed.TargetPercent), Is.EqualTo(((double?)118, 3, 98.0)));
        Assert.That(slowed.Stop, Is.False, "never a stop: the hard cutoff stays the pace's own");
        Assert.That(slowed.SubagentNote, Does.StartWith("Subagents: 4 at once").And.Contain("Cooling off").And.Contain("near 118%, past its 98% pace line"));
        Assert.That(slowed.Note, Does.Contain("Cooling off: 3 CLIs on this login"));
        Assert.That(Pacer.CoolOff(pace, Trend(99), 2).SubagentAllowance, Is.EqualTo(5), "just over: one fewer than running");
    }

    [Test]
    public void It_never_stops_new_subagents_or_raises_the_allowance()
    {
        Assert.That(Pacer.CoolOff(Paced(8, 1), Trend(300), 4).SubagentAllowance, Is.EqualTo(1), "far over: still one at a time");
        Assert.That(Pacer.CoolOff(Paced(8, 0), Trend(120), 2).SubagentAllowance, Is.EqualTo(1), "none running: one may start");
        Assert.That(Pacer.CoolOff(Paced(2, 6), Trend(105), 2).SubagentAllowance, Is.EqualTo(2), "never above what the pace allowed");
    }

    [Test]
    public void It_stays_off_under_the_line_with_one_stream_and_at_the_cutoff()
    {
        var pace = Paced(8, 6);
        Assert.That(Pacer.CoolOff(pace, Trend(97), 3), Is.SameAs(pace), "under the line");
        Assert.That(Pacer.CoolOff(pace, Trend(98), 3), Is.SameAs(pace), "on the line is not past it");
        var one = Paced(8, 1);
        Assert.That(Pacer.CoolOff(one, Trend(130), 1), Is.SameAs(one), "one CLI and one subagent: truly a single stream");
        var stopped = pace with { Stop = true };
        Assert.That(Pacer.CoolOff(stopped, Trend(130), 3), Is.SameAs(stopped), "the hard cutoff decides at the stop");
        var none = pace with { SubagentAllowance = 0 };
        Assert.That(Pacer.CoolOff(none, Trend(130), 3), Is.SameAs(none), "the pace says none new");
        var unpaced = new Pace(PaceMode.Unpaced, 4, 0, 0, null, null, null, "off") { SubagentAllowance = 4 };
        Assert.That(Pacer.CoolOff(unpaced, Trend(130), 3), Is.SameAs(unpaced), "unpaced");
        Assert.That(Pacer.CoolOff(pace, null, 3), Is.SameAs(pace), "no window");
        Assert.That(Pacer.CoolOff(pace, Trend(130) with { Ended = true }, 3), Is.SameAs(pace), "the window has ended");
        Assert.That(Pacer.CoolOff(pace, Trend(130) with { Idle = true }, 3), Is.SameAs(pace), "nothing running on the login");
    }

    [Test]
    public void Many_subagents_under_one_cli_cool_off_too()
    {
        // M.ORCH-T1767: found live, 11 subagents under one CLI ran a window to 100% with the per-call hold maxed out,
        // because this only ever looked at distinct CLI count (1) and never considered it. clis=1 here throughout.
        var pace = Paced(16, 11);
        var slowed = Pacer.CoolOff(pace, Trend(118), 1);
        Assert.That(slowed.SubagentAllowance, Is.EqualTo(9), "11 running less 2 for 20 points over: the excess drains as they finish");
        Assert.That((slowed.CoolOffPercent, slowed.CoolOffClis, slowed.TargetPercent), Is.EqualTo(((double?)118, 1, 98.0)));
        Assert.That(slowed.Note, Does.Contain("Cooling off: 11 subagents on this login"), "named by subagent count, not a bogus CLI count of 1");
        var single = Paced(16, 1);
        Assert.That(Pacer.CoolOff(single, Trend(130), 1), Is.SameAs(single), "one CLI, one subagent: genuinely one stream, stays off");
    }

    [Test]
    public void It_lifts_as_soon_as_the_trend_bends_back_under()
    {
        var pace = Paced(8, 6);
        Assert.That(Pacer.CoolOff(pace, Trend(106), 3).SubagentAllowance, Is.EqualTo(5), "8 points over: one fewer than the 6 running");
        var back = Pacer.CoolOff(pace, Trend(96), 3);
        Assert.That((back.SubagentAllowance, back.CoolOffPercent), Is.EqualTo(((int?)8, (double?)null)), "back under: the pace's own allowance");
    }
}
