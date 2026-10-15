using MindAttic.Orchestrator.Sessions;
using NUnit.Framework;
using static MindAttic.Orchestrator.Sessions.ProcessReaper;

namespace MindAttic.Orchestrator.Tests;

public sealed class ProcessReaperTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    private static ProcessInfo P(int pid, string name, int parentPid, long handles = 100, DateTime? startUtc = null, string? cmd = null) =>
        new(pid, name, parentPid, handles, startUtc ?? Now, cmd ?? $"{name} ...");

    [Test]
    public void A_tool_process_whose_parent_chain_is_gone_is_reaped_as_orphaned()
    {
        // find.exe's parent (a bash shell) already exited: pid 2 isn't in the snapshot at all.
        var processes = new[] { P(3, "find.exe", parentPid: 2, cmd: "find / -name *.log") };

        var reaped = Select(processes, Now);

        Assert.That(reaped, Has.Count.EqualTo(1));
        Assert.That(reaped[0].Process.Pid, Is.EqualTo(3));
        Assert.That(reaped[0].Reason, Does.Contain("exited"));
    }

    [Test]
    public void A_tool_process_under_a_live_claude_cli_is_left_alone_when_small_and_young()
    {
        var processes = new[]
        {
            P(1, "claude.exe", parentPid: 0, startUtc: Now.AddHours(-1)),
            P(2, "bash.exe", parentPid: 1, startUtc: Now.AddMinutes(-3)),
            P(3, "rg.exe", parentPid: 2, handles: 200, startUtc: Now.AddMinutes(-2)),
        };

        Assert.That(Select(processes, Now), Is.Empty);
    }

    [Test]
    public void A_parent_younger_than_its_child_took_a_dead_parents_id_so_the_child_is_orphaned()
    {
        // The agent's bash exited; Windows gave its id to a claude.exe started later. That CLI isn't this find's agent.
        var processes = new[]
        {
            P(2, "claude.exe", parentPid: 0, startUtc: Now.AddMinutes(-1)),
            P(3, "find.exe", parentPid: 2, startUtc: Now.AddMinutes(-10)),
        };
        Assert.That(Select(processes, Now).Select(r => r.Process.Pid), Is.EqualTo(new[] { 3 }));

        var unknown = new[]
        {
            P(2, "claude.exe", parentPid: 0, startUtc: Unknown),
            P(3, "find.exe", parentPid: 2, startUtc: Now.AddMinutes(-10)),
            P(4, "git.exe", parentPid: 2, startUtc: Unknown),
        };
        Assert.That(Select(unknown, Now), Is.Empty, "an unknown start is neither younger nor old");
    }

    [Test]
    public void A_tool_process_under_a_live_claude_cli_with_too_many_handles_is_reaped()
    {
        var processes = new[]
        {
            P(1, "claude.exe", parentPid: 0, startUtc: Now.AddHours(-1)),
            P(2, "bash.exe", parentPid: 1, startUtc: Now.AddMinutes(-2)),
            P(3, "find.exe", parentPid: 2, handles: MaxHandleCount + 1, startUtc: Now.AddMinutes(-1)),
        };

        var reaped = Select(processes, Now);

        Assert.That(reaped, Has.Count.EqualTo(1));
        Assert.That(reaped[0].Process.Pid, Is.EqualTo(3));
        Assert.That(reaped[0].Reason, Does.Contain("handles"));
    }

    [Test]
    public void A_tool_process_under_a_live_claude_cli_running_too_long_is_reaped()
    {
        var processes = new[]
        {
            P(1, "claude.exe", parentPid: 0, startUtc: Now.AddHours(-2)),
            P(2, "git.exe", parentPid: 1, handles: 50, startUtc: Now - MaxAge - TimeSpan.FromMinutes(1)),
        };

        var reaped = Select(processes, Now);

        Assert.That(reaped, Has.Count.EqualTo(1));
        Assert.That(reaped[0].Process.Pid, Is.EqualTo(2));
        Assert.That(reaped[0].Reason, Does.Contain("running"));
    }

    [Test]
    public void A_tool_process_with_an_intact_chain_reaching_no_agent_is_left_alone()
    {
        // The user's own terminal: explorer -> pwsh -> git, no Claude CLI anywhere, and nothing in the
        // chain has exited, so this is an ordinary dev process, not something an agent spawned.
        var processes = new[]
        {
            P(1, "explorer.exe", parentPid: 0, startUtc: Now.AddDays(-1)),
            P(2, "pwsh.exe", parentPid: 1, startUtc: Now.AddHours(-3)),
            P(3, "git.exe", parentPid: 2, handles: MaxHandleCount + 1, startUtc: Now - MaxAge - TimeSpan.FromHours(1)),
        };

        Assert.That(Select(processes, Now), Is.Empty);
    }

    [Test]
    public void A_renamed_claude_exe_from_the_updater_still_counts_as_the_live_cli()
    {
        var processes = new[]
        {
            P(1, "claude.exe.old.5", parentPid: 0, startUtc: Now.AddHours(-1)),
            P(2, "node.exe", parentPid: 1, handles: 50, startUtc: Now.AddMinutes(-2)),
        };

        Assert.That(Select(processes, Now), Is.Empty);
    }

    [Test]
    public void A_non_target_process_is_never_reaped_even_when_orphaned_and_huge()
    {
        var processes = new[] { P(3, "notepad.exe", parentPid: 2, handles: MaxHandleCount + 1, startUtc: Now.AddYears(-1)) };

        Assert.That(Select(processes, Now), Is.Empty);
    }

    [Test]
    public void Only_the_processes_about_to_be_killed_have_their_command_line_fetched()
    {
        // The scan lists every process without its command line (the costly part of the WMI query, MAO-T117);
        // a reap candidate's is looked up on its own, before it's killed, so the log still says what it was running.
        var processes = new[]
        {
            P(1, "claude.exe", parentPid: 0, startUtc: Now.AddHours(-1), cmd: null),
            P(2, "bash.exe", parentPid: 1, startUtc: Now.AddMinutes(-3), cmd: null),
            P(3, "rg.exe", parentPid: 2, startUtc: Now.AddMinutes(-2), cmd: null),
            P(4, "find.exe", parentPid: 99, cmd: null),
        }.Select(p => p with { CommandLine = null }).ToList();
        var asked = new List<int>();
        var killed = new List<int>();

        var reaped = ReapOnce(Now, () => processes, pid => { asked.Add(pid); return "find / -name x"; }, killed.Add);

        Assert.That(asked, Is.EqualTo(new[] { 4 }), "only the orphaned find");
        Assert.That(killed, Is.EqualTo(new[] { 4 }));
        Assert.That(reaped.Single().Process.CommandLine, Is.EqualTo("find / -name x"));
    }
}
