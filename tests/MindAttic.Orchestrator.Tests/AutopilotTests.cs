using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using MindAttic.Workflow.Tickets;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class AutopilotTests
{
    private string dir = "";
    private JsonStore store = null!;
    private ContextCycler sessions = null!;
    private readonly List<(int Pid, string Text)> typed = [];
    private readonly HashSet<int> running = [];
    private IReadOnlyList<string>? order = [];
    private static readonly DateTime Now = new(2026, 10, 15, 6, 0, 0, DateTimeKind.Utc);

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "orch-autopilot-" + Guid.NewGuid().ToString("N"));
        store = new JsonStore(Path.Combine(dir, "store"));
        sessions = new ContextCycler(Path.Combine(dir, "ctx"));
        Directory.CreateDirectory(sessions.Directory);
        typed.Clear();
        running.Clear();
        order = [];
    }

    [TearDown]
    public void TearDown() => Directory.Delete(dir, recursive: true);

    private Autopilot Pilot(DispatchLease? lease = null) => new(store, sessions, lease)
    {
        Order = () => order,
        Alive = running.Contains,
        Typer = (pid, text) => { typed.Add((pid, text)); return true; },
        SendToHost = (_, _) => throw new AssertionException("no Launcher tab in these tests"),
        IsAncestor = (_, _) => false,
    };

    /// <summary>A running CLI (process <paramref name="pid"/>) in <paramref name="cwd"/> whose last turn ended <paramref name="idleFor"/> ago.</summary>
    private void Cli(int pid, string cwd, TimeSpan idleFor, bool idle = true)
    {
        running.Add(pid);
        var at = (Now - idleFor).ToString("yyyy-MM-ddTHH:mm:ssZ");
        File.WriteAllText(Path.Combine(sessions.Directory, "s" + pid + ".state.json"),
            $$"""{"state":"{{(idle ? "idle" : "busy")}}","at":"{{at}}","pid":{{pid}},"cwd":"{{cwd.Replace(@"\", @"\\")}}"}""");
    }

    private static Ticket T(string id) => new(id, "2026-10-01", "fix", "open", $"summary of {id}", "");

    private static Snapshot Snap(params (string Name, Ticket[] Open)[] repos) =>
        new(Now, [.. repos.Select(r => new RepoStatus(r.Name, $@"D:\W\{r.Name}", RepoState.Dormant, [], [], r.Open, null, default))], []);

    private SchedulePlan Plan(Snapshot snap) => Scheduler.Plan(snap, new(), store.LoadAssignments());

    [Test]
    public void One_autopilot_puts_every_open_repo_cli_to_work_on_its_own_repo_only()
    {
        Cli(100, @"D:\W\A", TimeSpan.FromMinutes(1));
        Cli(200, @"D:\W\B\src", TimeSpan.FromMinutes(1));
        Cli(300, @"D:\W", TimeSpan.FromMinutes(1)); // the workspace root: no repo of its own
        var snap = Snap(("A", [T("A-T1"), T("A-T2")]), ("B", [T("B-T1")]), ("C", [T("C-T1")]));
        var pilot = Pilot();

        var said = pilot.Tick(Plan(snap), snap, new(), Now);
        Assert.That(typed.OrderBy(t => t.Pid).Select(t => (t.Pid, t.Text[..19])),
            Is.EqualTo(new[] { (100, "Work on ticket A-T1"), (200, "Work on ticket B-T1") }));
        Assert.That(typed[0].Text, Does.Contain("/commit").And.Contains("workflow ticket close A-T1"));
        Assert.That(said, Has.Count.EqualTo(2));
        Assert.That(pilot.Repos, Is.EqualTo(new[] { "A", "B" }));
        Assert.That(store.LoadAssignments().Select(a => (a.TicketId, a.Pilot)), Is.EquivalentTo(new[] { ("A-T1", (int?)100), ("B-T1", (int?)200) }));
    }

    [Test]
    public void A_cli_gets_nothing_more_until_its_story_is_closed()
    {
        Cli(100, @"D:\W\A", TimeSpan.FromMinutes(1));
        var snap = Snap(("A", [T("A-T1"), T("A-T2")]));
        var pilot = Pilot();
        pilot.Tick(Plan(snap), snap, new(), Now);

        Cli(100, @"D:\W\A", TimeSpan.FromMinutes(-5)); // its turn on A-T1 ended without closing it
        Assert.That(pilot.Tick(Plan(snap), snap, new(), Now.AddMinutes(10)).Single(), Does.Contain("still on A-T1"));
        Assert.That(typed, Has.Count.EqualTo(1), "it waits for you rather than piling on A-T2");

        var after = Snap(("A", [T("A-T2")]));
        pilot.Tick(Plan(after), after, new(), Now.AddMinutes(10));
        Assert.That(typed.Select(t => t.Text[..19]), Is.EqualTo(new[] { "Work on ticket A-T1", "Work on ticket A-T2" }));
    }

    [Test]
    public void Repo_flag_limits_it_to_those_repos_clis_and_off_stops_it()
    {
        Cli(100, @"D:\W\A", TimeSpan.FromMinutes(1));
        Cli(200, @"D:\W\B", TimeSpan.FromMinutes(1));
        var snap = Snap(("A", [T("A-T1")]), ("B", [T("B-T1")]));

        order = ["b", "Nope"];
        var said = Pilot().Tick(Plan(snap), snap, new(), Now);
        Assert.That(typed.Single().Pid, Is.EqualTo(200));
        Assert.That(said, Has.Some.Contains("no repo named Nope"));

        order = null;
        var off = Pilot();
        off.Tick(Plan(snap), snap, new(), Now.AddMinutes(1));
        Assert.That((off.On, typed.Count), Is.EqualTo((false, 1)));
    }

    [Test]
    public void A_cli_is_left_alone_while_busy_just_finished_closed_or_about_to_be_cycled()
    {
        var snap = Snap(("A", [T("A-T1")]));
        Cli(100, @"D:\W\A", TimeSpan.FromMinutes(1), idle: false);
        Pilot().Tick(Plan(snap), snap, new(), Now);

        Cli(100, @"D:\W\A", TimeSpan.FromSeconds(5));
        Pilot().Tick(Plan(snap), snap, new(), Now);

        Cli(100, @"D:\W\A", TimeSpan.FromMinutes(1));
        running.Remove(100);
        Pilot().Tick(Plan(snap), snap, new(), Now);

        running.Add(100);
        File.WriteAllText(Path.Combine(sessions.Directory, "s100.json"), """{"used_percentage":85}""");
        Pilot().Tick(Plan(snap), snap, new(), Now);
        Assert.That(typed, Is.Empty, "busy, a turn that just ended, a closed CLI, and one past the cycle point are all skipped");
    }

    [Test]
    public void A_repo_with_a_story_going_in_another_session_waits()
    {
        Cli(100, @"D:\W\A", TimeSpan.FromMinutes(1));
        var snap = Snap(("A", [T("A-T1"), T("A-T2")]));
        store.SaveAssignments([new Assignment("A-T1", "A", @"D:\W\A", "Claude", Now, false)]);

        Assert.That(Pilot().Tick(Plan(snap), snap, new(), Now).Single(), Does.Contain("A-T1 going in another session"));
        Assert.That(typed, Is.Empty, "never two agents in one checkout");
    }

    [Test]
    public void It_holds_when_the_pace_cant_afford_another_story_or_its_switched_off()
    {
        Cli(100, @"D:\W\A", TimeSpan.FromMinutes(1));
        var snap = Snap(("A", [T("A-T1")]));
        var coast = Plan(snap) with { Pace = new Pace(PaceMode.Coast, 1, 0, 0, null, null, null, "burning faster than the window allows") };

        var pilot = Pilot();
        pilot.Tick(coast, snap, new(), Now);
        Assert.That(pilot.Paused, Does.Contain("burning faster"));
        pilot.Tick(Plan(snap), snap, new() { Autopilot = false }, Now);
        Assert.That(pilot.Paused, Does.Contain("switched off"));
        Assert.That(typed, Is.Empty);

        pilot.Tick(Plan(snap), snap, new(), Now);
        Assert.That(pilot.Paused, Is.Null);
        Assert.That(typed, Has.Count.EqualTo(1));
    }

    [Test]
    public void Only_one_process_feeds_and_the_next_takes_over_when_it_exits()
    {
        Cli(100, @"D:\W\A", TimeSpan.FromMinutes(1));
        var snap = Snap(("A", [T("A-T1")]));
        var leases = Path.Combine(dir, "lease");
        using var daemonLease = new DispatchLease(leases);
        using var dashboardLease = new DispatchLease(leases);
        var daemon = Pilot(daemonLease);
        var dashboard = Pilot(dashboardLease);

        daemon.Tick(Plan(snap), snap, new(), Now);
        dashboard.Tick(Plan(snap), snap, new(), Now);
        Assert.That(dashboard.HeldElsewhere, Is.True);
        Assert.That(typed, Has.Count.EqualTo(1), "typed once, not by both");

        daemonLease.Dispose();
        dashboard.Tick(Plan(snap), snap, new(), Now);
        Assert.That(dashboard.HeldElsewhere, Is.False, "the lease is free again");
    }

    [Test]
    public void A_cli_in_a_subfolder_belongs_to_its_repo_and_one_elsewhere_to_none()
    {
        var snap = Snap(("A", []), ("AB", []));
        Assert.That(Autopilot.RepoFor(@"D:\W\A\src", snap)?.Name, Is.EqualTo("A"));
        Assert.That(Autopilot.RepoFor(@"d:\w\ab", snap)?.Name, Is.EqualTo("AB"));
        Assert.That(Autopilot.RepoFor(@"D:\W", snap), Is.Null);
        Assert.That(Autopilot.RepoFor(@"D:\Projects\MindAttic.worktrees\A\A-T1", snap), Is.Null);
    }

    [Test]
    public void A_cli_the_updater_renamed_still_counts_as_claude()
    {
        Assert.That(ConsoleTyper.IsClaude("claude"), Is.True);
        Assert.That(ConsoleTyper.IsClaude("claude.exe.old.1791946834302.10372"), Is.True, "found live: the updater renames the running binary");
        Assert.That(ConsoleTyper.IsClaude("claudia"), Is.False);
    }
}
