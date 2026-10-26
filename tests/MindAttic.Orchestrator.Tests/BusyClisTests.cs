using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T873: a redeploy refuses while any Claude CLI is still working, and never takes what it can't read for idle.</summary>
public sealed class BusyClisTests
{
    private static readonly DateTime Now = new(2026, 10, 25, 22, 0, 0, DateTimeKind.Utc);
    private static readonly AccountDirs Accounts = new(root: @"C:\nowhere\.claude-accounts", claudeDir: @"C:\nowhere\.claude");

    private static BusyClis.Proc Proc(int pid, int? parent = null) => new(pid, Now.AddHours(-1), parent);

    private static BusyClis.State State(string session, int pid, string turn, DateTime? asking = null, string cwd = @"D:\Projects\Repo") =>
        new(session, pid, Now.AddMinutes(-1), turn, Now.AddMinutes(-1), asking, cwd, null);

    private static BusyReport Judge(BusyClis.Proc[] procs, BusyClis.State[] states, BusyClis.Subagent[]? subagents = null,
        Assignment[]? claims = null, int? self = null, string[]? problems = null) =>
        BusyClis.Judge(procs, states, subagents ?? [], claims ?? [], self, Accounts, problems ?? [], Now);

    [Test]
    public void Idle_clis_with_nothing_running_let_the_redeploy_go_ahead()
    {
        var report = Judge([Proc(1), Proc(2)], [State("a", 1, "idle"), State("b", 2, "idle")]);
        Assert.That(report.AllIdle, Is.True);
        Assert.That(report.Clis.Select(c => c.Activity), Is.All.EqualTo(CliActivity.Idle));
    }

    [Test]
    public void A_cli_mid_turn_blocks_it()
    {
        var report = Judge([Proc(1)], [State("a", 1, "busy")]);
        Assert.That(report.AllIdle, Is.False);
        Assert.That(report.Blocking.Single().Activity, Is.EqualTo(CliActivity.Working));
        Assert.That(report.Blocking.Single().Repo, Is.EqualTo("Repo"));
    }

    [Test]
    public void An_idle_cli_with_a_subagent_running_or_a_claim_in_flight_or_a_prompt_waiting_is_working()
    {
        var claim = new Assignment("M.X-T1", "Repo", @"D:\wt", "Claude", Now, Worktree: true) { Cli = 2 };
        var report = Judge([Proc(1), Proc(2), Proc(3)],
            [State("a", 1, "idle"), State("b", 2, "idle"), State("c", 3, "idle", asking: Now.AddMinutes(-2))],
            subagents: [new BusyClis.Subagent("agent-x", 1, "global login")], claims: [claim]);
        Assert.That(report.Blocking.Select(c => c.Pid), Is.EquivalentTo(new int?[] { 1, 2, 3 }));
        Assert.That(report.Clis.Single(c => c.Pid == 2).Why.Single(), Does.Contain("M.X-T1"));
    }

    [Test]
    public void The_cli_running_the_deploy_is_left_out_even_though_it_is_busy()
    {
        var report = Judge([Proc(1)], [State("a", 1, "busy")], subagents: [new BusyClis.Subagent("agent-x", 1, "global login")], self: 1);
        Assert.That(report.AllIdle, Is.True);
        Assert.That(report.Clis.Single().Activity, Is.EqualTo(CliActivity.Self));
    }

    [Test]
    public void A_cli_with_no_state_or_an_unreadable_state_or_a_failed_read_is_unknown_never_idle()
    {
        var unreadable = new BusyClis.State("broken", null, Now.AddMinutes(-1), null, null, null, null, null, Unreadable: true);
        var report = Judge([Proc(1), Proc(2)], [State("a", 2, "idle"), unreadable], problems: ["the /allocate claims couldn't be read: held"]);
        Assert.That(report.AllIdle, Is.False);
        Assert.That(report.Clis.Single(c => c.Pid == 1).Activity, Is.EqualTo(CliActivity.Unknown));
        Assert.That(report.Blocking.Count(c => c.Pid is null), Is.EqualTo(2), "the unreadable state file and the failed claims read");
    }

    [Test]
    public void A_state_written_before_its_process_started_belongs_to_an_earlier_process_with_the_same_id()
    {
        var old = new BusyClis.State("old", 1, Now.AddHours(-3), "idle", null, null, null, null);
        var report = Judge([Proc(1)], [old]);
        Assert.That(report.Clis.Single().Activity, Is.EqualTo(CliActivity.Unknown));
    }

    [Test]
    public void A_claude_started_by_another_claude_is_part_of_that_cli_not_a_cli_of_its_own()
    {
        var report = Judge([Proc(1), Proc(5, parent: 1)], [State("a", 1, "idle")]);
        Assert.That(report.AllIdle, Is.True);
        Assert.That(report.Clis, Has.Count.EqualTo(1));
    }

    [Test]
    public void A_running_subagent_whose_mark_names_no_cli_is_unknown()
    {
        var report = Judge([Proc(1)], [State("a", 1, "idle")], subagents: [new BusyClis.Subagent("agent-y", null, "someone@example.com")]);
        Assert.That(report.AllIdle, Is.False);
        Assert.That(report.Blocking.Single().Activity, Is.EqualTo(CliActivity.Unknown));
    }

    [Test]
    public void A_busy_cli_inside_a_live_termhost_is_hosted_and_never_blocks()
    {
        var report = Judge([Proc(1) with { HostPid = 900 }, Proc(2)], [State("a", 1, "busy"), State("b", 2, "idle")]);
        Assert.That(report.AllIdle, Is.True);
        Assert.That(report.Hosted.Single().Pid, Is.EqualTo(1));
        Assert.That(report.Hosted.Single().Why.Single(), Does.Contain("unaffected by deploy"));
    }

    [Test]
    public void An_unhosted_busy_cli_still_blocks_beside_a_hosted_one()
    {
        var report = Judge([Proc(1) with { HostPid = 900 }, Proc(2)], [State("a", 1, "busy"), State("b", 2, "busy")]);
        Assert.That(report.Blocking.Select(c => c.Pid), Is.EqualTo(new int?[] { 2 }));
        Assert.That(report.Hosted, Has.Count.EqualTo(1));
    }

    [Test]
    public void HostAncestor_finds_the_termhost_above_a_cli_by_walking_parent_pids()
    {
        var tree = new Dictionary<int, (int Parent, string Exe)>
        {
            [900] = (1, "TermHost.exe"), [901] = (900, "conhost.exe"), [902] = (901, "claude.exe"), [50] = (1, "cmd.exe"), [51] = (50, "claude.exe"),
        };
        var hosts = new HashSet<int> { 900 };
        Assert.That(BusyClis.HostAncestor(902, tree, hosts), Is.EqualTo(900));
        Assert.That(BusyClis.HostAncestor(51, tree, hosts), Is.Null);
        Assert.That(BusyClis.HostAncestor(902, tree, new HashSet<int>()), Is.Null);
    }
}
