using MindAttic.Orchestrator.Sessions;
using NUnit.Framework;
using static MindAttic.Orchestrator.Sessions.ProcessReaper;

namespace MindAttic.Orchestrator.Tests;

public sealed class ProcessReaperTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    private static ProcessInfo P(int pid, string name, int parentPid, long handles = 100, DateTime? startUtc = null, string? cmd = null) =>
        new(pid, name, parentPid, handles, startUtc ?? Now, cmd ?? $"{name} ...");

    /// <summary>What a scan picks, given the pids earlier scans saw under a live Claude CLI.</summary>
    private static IReadOnlyList<Reaped> Pick(IReadOnlyList<ProcessInfo> processes, params int[] seenUnderAgent) =>
        Select(processes, Now, processes.Where(p => seenUnderAgent.Contains(p.Pid)).Select(p => (p.Pid, p.StartUtc)).ToHashSet());

    [Test]
    public void A_tool_process_whose_parent_chain_is_gone_is_reaped_as_orphaned()
    {
        // find.exe's parent (a bash shell) already exited: pid 2 isn't in the snapshot at all.
        var processes = new[] { P(3, "find.exe", parentPid: 2, cmd: "find / -name *.log") };

        var reaped = Pick(processes, 3);

        Assert.That(reaped, Has.Count.EqualTo(1));
        Assert.That(reaped[0].Process.Pid, Is.EqualTo(3));
        Assert.That(reaped[0].Reason, Does.Contain("exited"));
    }

    [Test]
    public void An_orphan_never_seen_under_an_agent_is_yours_and_left_alone()
    {
        // A node server started from a terminal you've since closed: orphaned, but no agent ever ran it (M.ORCH-T118).
        var processes = new[] { P(3, "node.exe", parentPid: 2, startUtc: Now.AddHours(-2)), P(4, "python.exe", parentPid: 2, startUtc: Now.AddHours(-2)) };
        Assert.That(Pick(processes), Is.Empty);
    }

    [Test]
    public void An_agents_tool_seen_on_one_scan_is_reaped_on_the_scan_after_its_agent_exits()
    {
        var seen = new HashSet<(int, DateTime)>();
        var claude = P(1, "claude.exe", parentPid: 0, startUtc: Now.AddHours(-1));
        var bash = P(2, "bash.exe", parentPid: 1, startUtc: Now.AddMinutes(-5));
        var find = P(3, "find.exe", parentPid: 2, startUtc: Now.AddMinutes(-4));
        var mine = P(9, "node.exe", parentPid: 8, startUtc: Now.AddHours(-3));
        var killed = new List<int>();

        ReapOnce(Now, () => [claude, bash, find, mine], _ => null, p => killed.Add(p.Pid), seen);
        Assert.That(killed, Is.Empty, "attached and young");

        ReapOnce(Now.AddMinutes(1), () => [find, mine], _ => null, p => killed.Add(p.Pid), seen); // the agent and its shell exited
        Assert.That(killed, Is.EqualTo(new[] { 3 }), "the find it left behind goes; your own node stays");
        Assert.That(seen, Is.EquivalentTo(new[] { (3, find.StartUtc) }), "what's gone is forgotten");
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

        Assert.That(Pick(processes), Is.Empty);
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
        Assert.That(Pick(processes, 3).Select(r => r.Process.Pid), Is.EqualTo(new[] { 3 }));

        var unknown = new[]
        {
            P(2, "claude.exe", parentPid: 0, startUtc: Unknown),
            P(3, "find.exe", parentPid: 2, startUtc: Now.AddMinutes(-10)),
            P(4, "git.exe", parentPid: 2, startUtc: Unknown),
        };
        Assert.That(Pick(unknown, 3, 4), Is.Empty, "an unknown start is neither younger nor old");
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

        var reaped = Pick(processes);

        Assert.That(reaped, Has.Count.EqualTo(1));
        Assert.That(reaped[0].Process.Pid, Is.EqualTo(3));
        Assert.That(reaped[0].Reason, Does.Contain("handles"));
    }

    [Test]
    public void A_search_under_a_live_claude_cli_running_too_long_is_reaped_but_a_long_test_run_is_not()
    {
        var old = Now - MaxAge - TimeSpan.FromMinutes(1);
        var processes = new[]
        {
            P(1, "claude.exe", parentPid: 0, startUtc: Now.AddHours(-2)),
            P(2, "rg.exe", parentPid: 1, handles: 50, startUtc: old),
            P(3, "python.exe", parentPid: 1, handles: 50, startUtc: old), // found live: an agent's 30-minute test run was killed
            P(4, "git.exe", parentPid: 1, handles: 50, startUtc: old),
        };

        var reaped = Pick(processes);

        Assert.That(reaped.Select(r => r.Process.Pid), Is.EqualTo(new[] { 2 }));
        Assert.That(reaped[0].Reason, Does.Contain("running"));
    }

    [Test]
    public void An_msbuild_or_test_run_an_agent_left_behind_is_reaped_once_its_cli_exits()
    {
        // M.ORCH-T1586: a dotnet build/test an agent ran (MSBuild node-reuse server, vstest worker, or the
        // driver itself) outlives its CLI the same way an orphaned find does, so it's just another target.
        var seen = new HashSet<(int, DateTime)>();
        var claude = P(1, "claude.exe", parentPid: 0, startUtc: Now.AddHours(-1));
        var bash = P(2, "bash.exe", parentPid: 1, startUtc: Now.AddMinutes(-20));
        var msbuild = P(3, "dotnet.exe", parentPid: 2, startUtc: Now.AddMinutes(-19), cmd: "dotnet exec MSBuild.dll");
        var killed = new List<int>();

        ReapOnce(Now, () => [claude, bash, msbuild], _ => null, p => killed.Add(p.Pid), seen);
        Assert.That(killed, Is.Empty, "attached: the build server stays while its CLI is alive");

        ReapOnce(Now.AddMinutes(1), () => [msbuild], _ => null, p => killed.Add(p.Pid), seen); // the CLI and its shell exited
        Assert.That(killed, Is.EqualTo(new[] { 3 }), "the idle build server it left behind goes");
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

        Assert.That(Pick(processes), Is.Empty);
    }

    [Test]
    public void A_process_with_no_claude_exe_ancestor_is_never_reaped_on_any_scan()
    {
        // M.ORCH-T386: Adobe Creative Cloud's node.exe was killed at 2026-10-15 08:51Z by the first reaper (1e77272), which
        // reaped any target whose parent had exited. Since c1b494f a process is reaped only if it, or an ancestor, ran under
        // a live claude.exe. Creative Cloud's node never does, whether its launcher exited or not, however big or old it gets,
        // with agents and their tools coming and going beside it, and even when it takes an exited agent tool's pid.
        var seen = new HashSet<(int, DateTime)>();
        var claude = P(1, "claude.exe", parentPid: 0, startUtc: Now.AddHours(-1));
        var bash = P(2, "bash.exe", parentPid: 1, startUtc: Now.AddMinutes(-5));
        var git = P(3, "git.exe", parentPid: 2, startUtc: Now.AddMinutes(-4));
        var adobeService = P(20, "Adobe Desktop Service.exe", parentPid: 0, startUtc: Now.AddDays(-1));
        var ccNode = P(21, "node.exe", parentPid: 20, handles: MaxHandleCount + 1, startUtc: Now.AddDays(-1));
        var ccOrphan = P(22, "node.exe", parentPid: 19, handles: MaxHandleCount + 1, startUtc: Now.AddDays(-1)); // its launcher exited
        var killed = new List<int>();

        ReapOnce(Now, () => [claude, bash, git, adobeService, ccNode, ccOrphan], _ => null, p => killed.Add(p.Pid), seen);
        ReapOnce(Now.AddMinutes(1), () => [adobeService, ccNode, ccOrphan], _ => null, p => killed.Add(p.Pid), seen);
        // Windows hands the exited agent git's id to a new Creative Cloud node whose own parent has gone.
        var reusedId = P(3, "node.exe", parentPid: 19, handles: MaxHandleCount + 1, startUtc: Now.AddMinutes(1).AddSeconds(30));
        ReapOnce(Now.AddMinutes(2), () => [adobeService, ccNode, ccOrphan, reusedId], _ => null, p => killed.Add(p.Pid), seen);
        ReapOnce(Now + MaxAge + TimeSpan.FromHours(1), () => [adobeService, ccNode, ccOrphan, reusedId], _ => null, p => killed.Add(p.Pid), seen);

        Assert.That(killed, Is.Empty);
    }

    [Test]
    public void A_renamed_claude_exe_from_the_updater_still_counts_as_the_live_cli()
    {
        var processes = new[]
        {
            P(1, "claude.exe.old.5", parentPid: 0, startUtc: Now.AddHours(-1)),
            P(2, "node.exe", parentPid: 1, handles: 50, startUtc: Now.AddMinutes(-2)),
        };

        Assert.That(Pick(processes), Is.Empty);
    }

    [Test]
    public void A_non_target_process_is_never_reaped_even_when_orphaned_and_huge()
    {
        var processes = new[] { P(3, "notepad.exe", parentPid: 2, handles: MaxHandleCount + 1, startUtc: Now.AddYears(-1)) };

        Assert.That(Pick(processes), Is.Empty);
    }

    // ---- the seen set kept across a daemon restart (M.ORCH-T1678) ----

    private static string ScratchDir() => Path.Combine(Path.GetTempPath(), "reaper-seen-" + Guid.NewGuid().ToString("N"));

    private static void Delete(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    [Test]
    public void An_orphan_seen_by_the_daemon_before_a_restart_is_reaped_on_the_first_scan_after_it()
    {
        var dir = ScratchDir();
        try
        {
            var claude = P(1, "claude.exe", parentPid: 0, startUtc: Now.AddHours(-1));
            var bash = P(2, "bash.exe", parentPid: 1, startUtc: Now.AddMinutes(-5));
            var find = P(3, "find.exe", parentPid: 2, startUtc: Now.AddMinutes(-4));
            var mine = P(9, "node.exe", parentPid: 8, startUtc: Now.AddHours(-3));
            var killed = new List<int>();

            // The first daemon: sees the agent's tools, keeps them, and stops.
            var before = new SeenStore(new MindAttic.Orchestrator.Settings.JsonStore(dir), "PC");
            var seen = new HashSet<(int Pid, DateTime Start)>();
            before.Load(seen);
            ReapOnce(Now, () => [claude, bash, find, mine], _ => null, p => killed.Add(p.Pid), seen);
            Assert.That(before.SaveIfDue(seen, Now), Is.True);
            Assert.That(killed, Is.Empty);

            // The agent and its shell exit while no daemon runs; a new daemon's first scan knows the find was the agent's.
            var after = new SeenStore(new MindAttic.Orchestrator.Settings.JsonStore(dir), "PC");
            var restarted = new HashSet<(int Pid, DateTime Start)>();
            Assert.That(after.Load(restarted), Is.EqualTo(2), "the shell and the find");
            ReapOnce(Now.AddMinutes(30), () => [find, mine], _ => null, p => killed.Add(p.Pid), restarted);

            Assert.That(killed, Is.EqualTo(new[] { 3 }), "the find the dead agent left goes; your own node, never seen under one, stays");
            Assert.That(restarted, Is.EquivalentTo(new[] { (3, find.StartUtc) }), "the shell that's gone is forgotten");
        }
        finally { Delete(dir); }
    }

    [Test]
    public void A_kept_pid_that_another_process_has_taken_since_is_never_reaped()
    {
        var dir = ScratchDir();
        try
        {
            var store = new MindAttic.Orchestrator.Settings.JsonStore(dir);
            var find = P(3, "find.exe", parentPid: 2, startUtc: Now.AddMinutes(-4));
            var before = new SeenStore(store, "PC");
            before.Load(new HashSet<(int Pid, DateTime Start)>());
            Assert.That(before.SaveIfDue(new HashSet<(int Pid, DateTime Start)> { (find.Pid, find.StartUtc) }, Now), Is.True);

            // After the restart pid 3 is your own orphaned node, started later (or a tick apart): same id, not the same process.
            var restarted = new HashSet<(int Pid, DateTime Start)>();
            new SeenStore(store, "PC").Load(restarted);
            var killed = new List<int>();
            var reused = P(3, "node.exe", parentPid: 2, startUtc: Now.AddMinutes(10));
            var nearly = P(3, "find.exe", parentPid: 2, startUtc: find.StartUtc.AddTicks(1));
            ReapOnce(Now.AddMinutes(30), () => [reused], _ => null, p => killed.Add(p.Pid), restarted);
            Assert.That(killed, Is.Empty);
            Assert.That(restarted, Is.Empty, "the kept entry is dropped: its process is gone");

            new SeenStore(store, "PC").Load(restarted);
            ReapOnce(Now.AddMinutes(30), () => [nearly], _ => null, p => killed.Add(p.Pid), restarted);
            Assert.That(killed, Is.Empty, "the start must be the one recorded, to the tick");
        }
        finally { Delete(dir); }
    }

    [Test]
    public void The_kept_set_round_trips_pid_and_start_exactly_and_skips_what_has_no_identity()
    {
        var start = new DateTime(2026, 11, 16, 8, 30, 15, DateTimeKind.Utc).AddTicks(1234567);
        var state = SeenStore.ToState([(7, start), (7, start.AddTicks(1)), (5, start), (9, Unknown), (0, start), (-4, start)], "PC");

        Assert.That(state.Machine, Is.EqualTo("PC"));
        Assert.That(state.Seen, Is.EqualTo(new[] { new SeenProcess(5, start.Ticks), new SeenProcess(7, start.Ticks), new SeenProcess(7, start.Ticks + 1) }),
            "no unknown start, no pid 0, in a steady order");
        Assert.That(SeenStore.FromState(state, "PC"), Is.EquivalentTo(new[] { (5, start), (7, start), (7, start.AddTicks(1)) }));
        Assert.That(SeenStore.FromState(state, "pc"), Has.Count.EqualTo(3), "a machine's name in another case is still it");

        Assert.That(SeenStore.FromState(state, "OTHER-PC"), Is.Empty, "another machine's pids mean nothing here");
        Assert.That(SeenStore.FromState(null, "PC"), Is.Empty);
        Assert.That(SeenStore.FromState(new SeenState { Seen = [new SeenProcess(5, start.Ticks)] }, "PC"), Is.Empty, "a row that names no machine");
        var bad = new SeenState { Machine = "PC", Seen = [new SeenProcess(0, start.Ticks), new SeenProcess(5, 0), new SeenProcess(6, -1), new SeenProcess(7, long.MaxValue)] };
        Assert.That(SeenStore.FromState(bad, "PC"), Is.Empty, "no pid, no start, or a start that isn't a time");
    }

    [Test]
    public void The_kept_set_is_capped()
    {
        var many = Enumerable.Range(1, MaxSeen + 10).Select(i => (i, Now));
        var state = SeenStore.ToState(many, "PC");

        Assert.That(state.Seen, Has.Count.EqualTo(MaxSeen));
        Assert.That(SeenStore.FromState(state, "PC"), Has.Count.EqualTo(MaxSeen));
    }

    [Test]
    public void The_seen_set_is_written_only_when_it_changed_and_at_most_about_once_a_minute()
    {
        var dir = ScratchDir();
        try
        {
            var store = new MindAttic.Orchestrator.Settings.JsonStore(dir);
            var kept = new SeenStore(store, "PC");
            var seen = new HashSet<(int Pid, DateTime Start)> { (3, Now) };

            Assert.That(kept.SaveIfDue(seen, Now), Is.False, "never before the row was read: that would replace it unread");
            Assert.That(store.Has(SeenStore.StateName), Is.False);
            Assert.That(kept.Load(new HashSet<(int Pid, DateTime Start)>()), Is.Zero, "nothing kept yet");
            Assert.That(kept.Loaded, Is.True);

            Assert.That(kept.SaveIfDue(seen, Now), Is.True);
            Assert.That(kept.SaveIfDue(seen, Now.AddMinutes(5)), Is.False, "unchanged");
            seen.Add((4, Now));
            Assert.That(kept.SaveIfDue(seen, Now.AddSeconds(20)), Is.False, "changed, but written 20 s ago");
            Assert.That(kept.SaveIfDue(seen, Now.AddSeconds(60)), Is.True);
            seen.Clear();
            Assert.That(kept.SaveIfDue(seen, Now.AddMinutes(3)), Is.True, "emptied: the row is emptied too, so nothing stale is read back");

            var restarted = new HashSet<(int Pid, DateTime Start)>();
            Assert.That(new SeenStore(store, "PC").Load(restarted), Is.Zero);
        }
        finally { Delete(dir); }
    }

    [Test]
    public void Loading_adds_to_what_this_run_already_saw_and_happens_once()
    {
        var dir = ScratchDir();
        try
        {
            var store = new MindAttic.Orchestrator.Settings.JsonStore(dir);
            var first = new SeenStore(store, "PC");
            first.Load(new HashSet<(int Pid, DateTime Start)>());
            first.SaveIfDue(new HashSet<(int Pid, DateTime Start)> { (3, Now), (4, Now) }, Now);

            var kept = new SeenStore(store, "PC");
            var seen = new HashSet<(int Pid, DateTime Start)> { (4, Now), (8, Now) };
            Assert.That(kept.Load(seen), Is.EqualTo(1), "pid 4 was there already");
            Assert.That(seen, Is.EquivalentTo(new[] { (3, Now), (4, Now), (8, Now) }));

            seen.Clear();
            Assert.That(kept.Load(seen), Is.Zero, "read once a run: a later scan's forgetting isn't undone");
            Assert.That(seen, Is.Empty);

            // Another machine's daemon reading this file (it travels with the checkout) takes none of it.
            var elsewhere = new HashSet<(int Pid, DateTime Start)>();
            Assert.That(new SeenStore(store, "OTHER-PC").Load(elsewhere), Is.Zero);
            Assert.That(elsewhere, Is.Empty);
        }
        finally { Delete(dir); }
    }

    [Test]
    public void The_seen_row_is_volatile_so_it_stays_on_this_machine_and_out_of_the_dump()
    {
        Assert.That(MindAttic.Orchestrator.Settings.OrchestratorDb.Volatile, Does.Contain(SeenStore.StateName));
    }

    [Test]
    public void Only_the_processes_about_to_be_killed_have_their_command_line_fetched()
    {
        // The scan lists every process without its command line (the costly part of the WMI query, M.ORCH-T117);
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

        var reaped = ReapOnce(Now, () => processes, pid => { asked.Add(pid); return "find / -name x"; }, p => killed.Add(p.Pid), new HashSet<(int, DateTime)> { (4, Now) });

        Assert.That(asked, Is.EqualTo(new[] { 4 }), "only the orphaned find");
        Assert.That(killed, Is.EqualTo(new[] { 4 }));
        Assert.That(reaped.Single().Process.CommandLine, Is.EqualTo("find / -name x"));
    }
}
