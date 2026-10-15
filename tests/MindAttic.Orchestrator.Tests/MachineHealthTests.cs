using MindAttic.Orchestrator.Activity;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class MachineHealthTests
{
    private static HealthSample S(int topHandles = 5_000, long total = 300_000, double commit = 60, double freeGb = 8) =>
        new(total, commit, freeGb, [new HandleHog(42, "find", topHandles)]);

    [Test]
    public void A_healthy_machine_is_ok()
    {
        Assert.That(MachineHealth.Judge(S()), Is.EqualTo((HealthLevel.Ok, (string?)null)));
    }

    [Test]
    public void The_freeze_found_live_is_danger_and_names_the_process()
    {
        // Two orphaned `find /` runs held 372k and 288k handles while CPU sat idle.
        var (level, why) = MachineHealth.Judge(S(topHandles: 372_497, total: 1_100_000));
        Assert.That(level, Is.EqualTo(HealthLevel.Danger));
        Assert.That(why, Does.Contain("find (pid 42) holds 372,497 handles"));
    }

    [TestCase(120_000, 300_000, 60, 8, HealthLevel.Warn, "holds 120,000 handles")]
    [TestCase(5_000, 1_600_000, 60, 8, HealthLevel.Warn, "1,600,000 handles open")]
    [TestCase(5_000, 300_000, 90, 8, HealthLevel.Warn, "commit at 90%")]
    [TestCase(5_000, 300_000, 96, 8, HealthLevel.Danger, "commit at 96%")]
    [TestCase(5_000, 300_000, 60, 1.5, HealthLevel.Warn, "1.5 GB RAM free")]
    [TestCase(5_000, 300_000, 60, 0.5, HealthLevel.Danger, "0.5 GB RAM free")]
    public void Each_resource_has_a_warn_and_a_danger_line(int top, long total, double commit, double free, HealthLevel level, string because)
    {
        var (got, why) = MachineHealth.Judge(S(top, total, commit, free));
        Assert.That(got, Is.EqualTo(level));
        Assert.That(why, Does.Contain(because));
    }

    [Test]
    public void A_live_sample_reads_this_machine()
    {
        var s = MachineHealth.Sample();
        Assert.That(s.TotalHandles, Is.GreaterThan(1_000));
        Assert.That(s.CommitPercent, Is.InRange(0.1, 100));
        Assert.That(s.Top, Has.Count.EqualTo(3));
        Assert.That(s.Top.Select(t => t.Pid), Has.None.EqualTo(4), "the kernel's own handles aren't a leak");
    }
}
