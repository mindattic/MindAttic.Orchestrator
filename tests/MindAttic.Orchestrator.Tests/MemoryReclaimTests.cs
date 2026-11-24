using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Sessions;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The memory guard's one kill of our own (M.ORCH-T2143): an idle headless CLI, never from a stale roster or a reused pid.</summary>
public sealed class MemoryReclaimTests
{
    private static readonly DateTime T0 = new(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);

    private static CliRow Cli(int pid, int idleMinutes, bool inLauncher = false, bool idle = true) =>
        new(pid, $"s{pid}", "Repo", null, null, inLauncher, false, [], idle, T0.AddMinutes(-idleMinutes));

    private static readonly CliRosterFile Roster = new(T0, [Cli(10, 5), Cli(20, 30), Cli(30, 60, inLauncher: true), Cli(40, 90, idle: false)]);

    private static (string, DateTime)? Claude(int pid) => ("claude", T0.AddHours(-2));

    [Test]
    public void A_fresh_roster_picks_the_longest_idle_headless_cli() =>
        Assert.That(MemoryReclaim.PickIdleHeadless(Roster, T0.AddSeconds(30), Claude)?.Pid, Is.EqualTo(20));

    [Test]
    public void A_stale_roster_kills_nothing()
    {
        Assert.That(MemoryReclaim.PickIdleHeadless(Roster, T0 + CliRoster.FreshFor + TimeSpan.FromSeconds(1), Claude), Is.Null);
        Assert.That(MemoryReclaim.PickIdleHeadless(null, T0, Claude), Is.Null);
    }

    [Test]
    public void A_pid_that_is_no_longer_that_cli_is_skipped()
    {
        (string, DateTime)? Reused(int pid) => pid == 20 ? ("notepad", T0.AddHours(-2)) : Claude(pid);
        Assert.That(MemoryReclaim.PickIdleHeadless(Roster, T0, Reused)?.Pid, Is.EqualTo(10), "another program on the pid");

        (string, DateTime)? Restarted(int pid) => pid == 20 ? ("claude", T0.AddSeconds(10)) : Claude(pid);
        Assert.That(MemoryReclaim.PickIdleHeadless(Roster, T0.AddSeconds(20), Restarted)?.Pid, Is.EqualTo(10), "a claude started after the roster was written");

        (string, DateTime)? Gone(int pid) => pid == 20 ? null : ("node", T0.AddHours(-2));
        Assert.That(MemoryReclaim.PickIdleHeadless(Roster, T0, Gone)?.Pid, Is.EqualTo(10), "gone, and node still counts as a CLI");

        Assert.That(MemoryReclaim.PickIdleHeadless(Roster, T0, _ => ("explorer", T0.AddHours(-2))), Is.Null);
    }
}
