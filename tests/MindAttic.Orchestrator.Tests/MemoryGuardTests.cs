using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The memory guard (M.ORCH-T1494): low RAM holds new work like the 5-hour and weekly stops, and doesn't flap.</summary>
public sealed class MemoryGuardTests
{
    private static readonly DateTime T0 = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);
    private static readonly OrchestratorSettings S = new();

    private static MemoryReading Free(double gb, double commit = 50, bool counted = true) => new(gb, commit, counted);

    [TestCase(8.0, 50.0, MemoryLevel.Ok)]
    [TestCase(3.9, 50.0, MemoryLevel.Tight)]
    [TestCase(1.9, 50.0, MemoryLevel.Critical)]
    [TestCase(0.9, 50.0, MemoryLevel.Emergency)]
    [TestCase(8.0, 80.0, MemoryLevel.Tight)]
    [TestCase(8.0, 88.0, MemoryLevel.Critical)]
    [TestCase(8.0, 94.0, MemoryLevel.Emergency)]
    public void Each_line_has_its_level(double free, double commit, MemoryLevel level) =>
        Assert.That(MemoryGuard.Judge(Free(free, commit), S, margin: false).Level, Is.EqualTo(level));

    [Test]
    public void Below_the_start_line_of_ram_used_the_guard_is_dormant_and_caps_nothing()
    {
        // M.ORCH-T1591: full speed until near the edge; the start line is a Vault setting.
        var calm = new MemoryReading(0.5, 99, true, UsedPercent: 60);
        Assert.That(MemoryGuard.Judge(calm, S, margin: false).Level, Is.EqualTo(MemoryLevel.Ok));
        var pace = Allowing(8, running: 3);
        Assert.That(Pacer.CapByMemory(pace, new MemoryState(MemoryLevel.Ok, new MemoryReading(2, 50, true, UsedPercent: 60), ""), S).SubagentAllowance, Is.EqualTo(8));
        Assert.That(MemoryGuard.Judge(calm with { UsedPercent = 95 }, S, margin: false).Level, Is.EqualTo(MemoryLevel.Emergency), "past the line it's awake");
        Assert.That(MemoryGuard.Judge(calm, S with { MemoryGuardStartPercent = 50 }, margin: false).Level, Is.EqualTo(MemoryLevel.Emergency), "the line is a setting");
    }

    [Test]
    public void A_system_managed_pagefiles_commit_is_not_counted()
    {
        // Its limit grows with the pagefile, so 106% of RAM committed with 6 GB free is fine (seen live 2026-11-06).
        Assert.That(MemoryGuard.Judge(Free(6.3, 106, counted: false), S, margin: false).Level, Is.EqualTo(MemoryLevel.Ok));
    }

    [Test]
    public void It_goes_up_at_once_and_comes_down_a_step_at_a_time_once_clear_for_a_minute()
    {
        var g = new MemoryGuard();
        Assert.That(g.Step(MemoryLevel.Ok, Free(0.5), S, T0).Level, Is.EqualTo(MemoryLevel.Emergency), "straight to the worst");
        Assert.That(g.Step(MemoryLevel.Emergency, Free(8), S, T0.AddSeconds(5)).Level, Is.EqualTo(MemoryLevel.Emergency), "clear, but not for a minute yet");
        Assert.That(g.Step(MemoryLevel.Emergency, Free(8), S, T0.AddSeconds(66)).Level, Is.EqualTo(MemoryLevel.Critical), "one step down");
        Assert.That(g.Step(MemoryLevel.Critical, Free(8), S, T0.AddSeconds(100)).Level, Is.EqualTo(MemoryLevel.Critical), "each step waits its own minute");
        Assert.That(g.Step(MemoryLevel.Critical, Free(8), S, T0.AddSeconds(127)).Level, Is.EqualTo(MemoryLevel.Tight));
        Assert.That(g.Step(MemoryLevel.Tight, Free(8), S, T0.AddSeconds(188)).Level, Is.EqualTo(MemoryLevel.Ok));
    }

    [Test]
    public void New_work_waits_for_the_margin_not_just_the_line()
    {
        var g = new MemoryGuard();
        // Over the 4 GB line, under 4 + 2: still tight, however long it stays there.
        for (var i = 0; i < 10; i++)
            Assert.That(g.Step(MemoryLevel.Tight, Free(5), S, T0.AddMinutes(i)).Level, Is.EqualTo(MemoryLevel.Tight));
        Assert.That(g.Step(MemoryLevel.Tight, Free(6), S, T0.AddMinutes(20)).Level, Is.EqualTo(MemoryLevel.Tight), "the clear minute starts now");
        Assert.That(g.Step(MemoryLevel.Tight, Free(6), S, T0.AddMinutes(21)).Level, Is.EqualTo(MemoryLevel.Ok));
    }

    [Test]
    public void A_dip_back_under_restarts_the_clear_minute()
    {
        var g = new MemoryGuard();
        g.Step(MemoryLevel.Tight, Free(7), S, T0);
        g.Step(MemoryLevel.Tight, Free(5), S, T0.AddSeconds(40));
        Assert.That(g.Step(MemoryLevel.Tight, Free(7), S, T0.AddSeconds(70)).Level, Is.EqualTo(MemoryLevel.Tight));
        Assert.That(g.Step(MemoryLevel.Tight, Free(7), S, T0.AddSeconds(131)).Level, Is.EqualTo(MemoryLevel.Ok));
    }

    [Test]
    public void Observe_reads_at_most_every_five_seconds_and_nothing_while_off()
    {
        var reads = 0;
        var g = new MemoryGuard { Read = () => { reads++; return Free(8); } };
        g.Observe(S, T0);
        g.Observe(S, T0.AddSeconds(2));
        Assert.That(reads, Is.EqualTo(1));
        g.Observe(S, T0.AddSeconds(6));
        Assert.That(reads, Is.EqualTo(2));
        Assert.That(g.Observe(S with { MemoryGuard = false }, T0.AddSeconds(20)), Is.Null);
        Assert.That(new MemoryGuard { Read = () => null }.Observe(S, T0), Is.Null, "memory that can't be read holds nothing");
    }

    // ── Pacer.CapByMemory ───────────────────────────────────────────────────

    private static Pace Allowing(int? allowance, int running) =>
        new(PaceMode.Open, 4, 1_000, 0, null, "5-hour", null, "On pace.") { SubagentAllowance = allowance, Subagents = running };

    private static MemoryState At(MemoryLevel level, double free) => new(level, Free(free), "");

    [TestCase(MemoryLevel.Tight, 3.0)]
    [TestCase(MemoryLevel.Critical, 1.5)]
    [TestCase(MemoryLevel.Emergency, 0.5)]
    public void Short_of_RAM_none_new_start_and_none_running_are_cut(MemoryLevel level, double free)
    {
        var p = Pacer.CapByMemory(Allowing(8, running: 3), At(level, free), S);
        Assert.That(p.SubagentAllowance, Is.EqualTo(3));
        Assert.That(p.MemoryHold, Is.True);
        Assert.That(p.SubagentNote, Does.Contain("none new").Or.Contain("RAM emergency"));
    }

    [TestCase(7.0, 3, 6)]   // (7 - 4) / 1 = 3 more fit
    [TestCase(5.5, 3, 4)]   // 1 more fits
    [TestCase(4.5, 3, 3)]   // none more fit
    [TestCase(30.0, 3, 8)]  // RAM to spare: the allowance stands
    public void With_RAM_to_spare_no_more_start_than_free_RAM_holds(double free, int running, int expected) =>
        Assert.That(Pacer.CapByMemory(Allowing(8, running), At(MemoryLevel.Ok, free), S).SubagentAllowance, Is.EqualTo(expected));

    [Test]
    public void An_unpaced_allowance_is_only_set_when_RAM_binds()
    {
        Assert.That(Pacer.CapByMemory(Allowing(null, 2), At(MemoryLevel.Ok, 64), S).SubagentAllowance, Is.Null);
        Assert.That(Pacer.CapByMemory(Allowing(null, 2), At(MemoryLevel.Tight, 3), S).SubagentAllowance, Is.EqualTo(2));
    }

    [Test]
    public void Off_the_pace_is_left_as_it_is()
    {
        var pace = Allowing(8, 3);
        Assert.That(Pacer.CapByMemory(pace, At(MemoryLevel.Emergency, 0.5), S with { MemoryGuard = false }), Is.SameAs(pace));
        Assert.That(Pacer.CapByMemory(pace, null, S), Is.SameAs(pace));
    }
}
