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
    private readonly List<(string Dir, string Prompt, string Log, int Pid)> started = [];
    private readonly HashSet<int> clis = [];
    private readonly HashSet<int> agents = [];
    private IReadOnlyList<string>? order = [];
    private readonly HashSet<string> landed = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> landedFrom = new(StringComparer.Ordinal);
    private int nextPid = 5000;
    private static readonly DateTime Now = new(2026, 10, 15, 6, 0, 0, DateTimeKind.Utc);

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "orch-autopilot-" + Guid.NewGuid().ToString("N"));
        store = new JsonStore(Path.Combine(dir, "store"));
        sessions = new ContextCycler(Path.Combine(dir, "ctx"));
        Directory.CreateDirectory(sessions.Directory);
        started.Clear();
        clis.Clear();
        agents.Clear();
        landedFrom.Clear();
        order = [];
    }

    [Test]
    public void The_last_open_story_of_a_repo_is_landed_from_the_repos_own_checkout_not_its_worktree()
    {
        Cli(100, @"D:\W\A");
        var snap = Snap(("A", [T("A-T1")]));
        var pilot = Pilot();
        pilot.Tick(Paced(snap, 1), snap, new() { WorktreeRoot = @"D:\WT" }, Now);
        agents.Clear();
        landed.Add("A-T1");

        var after = Snap(("A", [])); // it closed the repo's last open story, so the queue has nothing for A
        var said = pilot.Tick(Paced(after, 1), after, new() { WorktreeRoot = @"D:\WT" }, Now.AddMinutes(30));
        Assert.That(landedFrom["A-T1"], Is.EqualTo(@"D:\W\A"), "the worktree has no ticket database beside it, so a finished story was parked");
        Assert.That(said, Has.Some.Contains("finished A-T1"));
    }

    [Test]
    public void Autopilot_all_works_every_repo_with_open_stories_whether_or_not_a_cli_is_open()
    {
        order = [Autopilot.AllRepos];
        Cli(100, @"D:\W\A", busy: true); // A's own CLI is being worked by hand
        var snap = Snap(("A", [T("A-T1")]), ("B", [T("B-T1")]), ("C", []));
        var pilot = Pilot();

        var said = pilot.Tick(Paced(snap, 10), snap, new() { WorktreeRoot = @"D:\WT" }, Now);

        Assert.That(started.Select(s => s.Dir), Is.EqualTo(new[] { @"D:\WT\B\B-T1" }), "B has no CLI open and still gets an agent; A is yours; C has nothing open");
        Assert.That(pilot.Repos, Is.EqualTo(new[] { "B" }));
        Assert.That(said, Has.None.Contains("no repo named"));
    }

    [Test]
    public void Stories_start_in_queue_order_across_repos_not_one_repo_filled_first()
    {
        // Found live: /autopilot all put all five spare sessions into Automata while a +70 story elsewhere waited.
        order = [Autopilot.AllRepos];
        var snap = Snap(("Automata", [T("AUT-T1"), T("AUT-T2"), T("AUT-T3")]), ("Launcher", [T("M.LNCH-T21")]));
        var plan = Scheduler.Plan(snap, new() { Priorities = new() { ["M.LNCH-T21"] = 70 } }, store.LoadAssignments());
        plan = plan with { Pace = new Pace(PaceMode.Open, 6, 10_000, 0, 1_000, "5-hour", Now.AddHours(3), "On pace.") { SpareSessions = 2 } };

        Pilot().Tick(plan, snap, new() { WorktreeRoot = @"D:\WT", Priorities = new() { ["M.LNCH-T21"] = 70 } }, Now);

        Assert.That(started.Select(s => s.Dir).First(), Is.EqualTo(@"D:\WT\Launcher\M.LNCH-T21"), "the prioritized story goes first");
        Assert.That(started, Has.Count.EqualTo(2));
    }

    [Test]
    public void Past_the_wind_down_line_only_a_story_with_an_estimate_that_fits_starts()
    {
        Cli(100, @"D:\W\A");
        var snap = Snap(("A", [T("A-T1")]));
        var winding = Paced(snap, 5);
        winding = winding with { Pace = winding.Pace! with { WindDown = true } };

        Pilot().Tick(winding, snap, new(), Now);

        Assert.That(started, Is.Empty, "no estimate yet: it might not finish before the stop");
    }

    [Test]
    public void An_agent_is_told_to_land_on_its_repos_own_main_line()
    {
        // Found live: Automata lands on master; told to push HEAD:main, an agent created a stray main branch on GitHub.
        Cli(100, @"D:\W\A");
        var snap = Snap(("A", [T("A-T1")]));
        new Autopilot(store, sessions)
        {
            Order = () => order, Alive = clis.Contains, IsRunning = _ => false, StartTimeOf = _ => null, LogDirectory = Path.Combine(dir, "logs"),
            MakeWorktree = (repo, path, branch) => (true, path, ""), MainlineOf = _ => "master",
            Start = (d, prompt, log) => { started.Add((d, prompt, log, 1)); return 1; },
        }.Tick(Paced(snap, 1), snap, new() { WorktreeRoot = Path.Combine(dir, "wt") }, Now);
        Assert.That(started.Single().Prompt, Does.Contain("git rebase origin/master").And.Contain("git push origin HEAD:master").And.Not.Contain("HEAD:main "));
        Assert.That(started.Single().Prompt, Does.Contain("never in the background"), "a -p run ends with its last turn and cuts off background subagents");
    }

    [Test]
    public void Landing_checks_the_repos_real_main_line_against_a_real_git_remote()
    {
        static (int, string) Git(string cwd, params string[] args) => Dispatcher.Git(cwd, args);
        var origin = Path.Combine(dir, "origin.git");
        var repo = Path.Combine(dir, "repo");
        Directory.CreateDirectory(repo);
        Git(dir, "init", "-q", "--bare", "-b", "master", origin);
        Git(repo, "init", "-q", "-b", "master");
        Git(repo, "config", "user.email", "t@t");
        Git(repo, "config", "user.name", "t");
        Directory.CreateDirectory(Path.Combine(repo, "docs"));
        File.WriteAllText(Path.Combine(repo, "docs", "USER_STORIES.md"), "| ID | Date | Type | Status | Summary | Ref |\n| X-T1 | 2026-10-15 | fix | open | s | |\n| X-T2 | 2026-10-15 | fix | closed | s | done |\n");
        Git(repo, "add", "-A");
        Git(repo, "commit", "-q", "-m", "start");
        Git(repo, "remote", "add", "origin", origin);
        Git(repo, "push", "-q", "-u", "origin", "master");
        Git(repo, "remote", "set-head", "origin", "master");
        Assert.That(Dispatcher.Mainline(repo), Is.EqualTo("master"));

        // Your checkout is on an unfinished branch of your own: the agent's branch must not start from it.
        Git(repo, "checkout", "-q", "-b", "mine");
        File.WriteAllText(Path.Combine(repo, "mine.txt"), "half done");
        Git(repo, "add", "-A");
        Git(repo, "commit", "-q", "-m", "my unfinished work");
        var tree = Path.Combine(dir, "wt", "X-T1");
        Assert.That(Dispatcher.EnsureWorktree(repo, tree, Dispatcher.BranchFor("X-T1")).Ok, Is.True);
        Assert.That(File.Exists(Path.Combine(tree, "mine.txt")), Is.False, "the agent starts from origin/master, not your branch");
        Git(repo, "checkout", "-q", "master");
        File.WriteAllText(Path.Combine(tree, "docs", "USER_STORIES.md"), "| ID | Date | Type | Status | Summary | Ref |\n| X-T1 | 2026-10-15 | fix | closed | s | done |\n| X-T2 | 2026-10-15 | fix | closed | s | done |\n");
        Git(tree, "commit", "-q", "-am", "X-T1 done");
        var a = new Assignment("X-T1", "X", tree, "Claude", Now, true);

        Assert.That(Autopilot.LandOnMain(a, repo).Closed, Is.False, "closed on its branch but not pushed: not done");
        Git(tree, "push", "-q", "origin", "HEAD:master");
        var (closed, note) = Autopilot.LandOnMain(a, repo);
        Assert.That(closed, Is.True, note);
        Assert.That(note, Does.Contain("up to date"), "your checkout on master is fast-forwarded");
        Assert.That(Directory.Exists(tree), Is.False, "the worktree is removed");

        // A worktree that was never on an orch/<id> branch, whose ticket is closed and whose work is on master, is landed
        // (found live: 35 such worktrees were reported unlanded because orch/<id> didn't exist).
        var other = Path.Combine(dir, "wt", "X-T2");
        Git(repo, "worktree", "add", "-q", "-b", "a-name-the-agent-chose", other, "origin/master");
        var (otherLanded, otherNote) = Autopilot.LandOnMain(new Assignment("X-T2", "X", other, "Claude", Now, true), repo);
        Assert.That(otherLanded, Is.True, otherNote);
        Assert.That(Directory.Exists(other), Is.False);
    }

    [Test]
    public void A_closed_ticket_s_leftover_worktree_is_swept_and_a_closed_tab_assignment_is_dropped()
    {
        Directory.CreateDirectory(Path.Combine(dir, "wt", "A", "A-T7"));
        File.WriteAllText(Path.Combine(dir, "wt", "A", "A-T7", ".git"), "gitdir: x");
        Directory.CreateDirectory(Path.Combine(dir, "wt", "A", "A-T8")); // open: still work in progress
        File.WriteAllText(Path.Combine(dir, "wt", "A", "A-T8", ".git"), "gitdir: x");
        store.SaveAssignments([new Assignment("A-T5", "A", @"D:\W\A", "Claude", Now.AddDays(-1), false)]); // a tab's, its ticket since closed
        landed.Add("A-T7");
        Cli(100, @"D:\W\A");
        var snap = Snap(("A", [T("A-T8")]));

        var said = Pilot().Tick(Paced(snap, 0), snap, new() { WorktreeRoot = Path.Combine(dir, "wt") }, Now);

        Assert.That(said, Has.Some.Contains("tidied the finished worktree of A-T7"));
        Assert.That(landedFrom.Keys, Is.EquivalentTo(new[] { "A-T7" }), "an open ticket's worktree is left alone");
        Assert.That(store.LoadAssignments(), Is.Empty, "found live: closed tab assignments were listed as done forever");
    }

    [Test]
    public void A_repo_whose_stories_all_wait_for_approval_says_so()
    {
        Cli(100, @"D:\W\A");
        var snap = Snap(("A", [new Ticket("A-T1", "2026-10-01", "chore", "open", "tests", "")]));
        var said = Pilot().Tick(Paced(snap, 3), snap, new(), Now);
        Assert.That(said, Has.Some.Contains("A's 1 open story waits for your approval"), "not 'no open stories left'");
    }

    [Test]
    public void A_cli_running_autopilot_is_handed_its_repos_workable_tickets_each_with_a_worktree_and_prompt_once()
    {
        // M.ORCH-T164: /autopilot works tickets through the CLI's own subagents; `orchestrator next --claim` hands them out.
        var snap = Snap(("A", [T("A-T1"), T("A-T2"), new Ticket("A-T3", "2026-10-01", "feature", "open", "not approved", "")]), ("B", [T("B-T1")]));
        var settings = new OrchestratorSettings { WorktreeRoot = @"D:\WT" };
        var made = new List<string>();
        (bool, string, string) Make(string repo, string path, string branch) { made.Add(path); return (true, path, ""); }
        SchedulePlan Planned() => Paced(snap, 5) with { Pace = Paced(snap, 5).Pace! with { SubagentAllowance = 4, Subagents = 2 } };

        var (handed, why) = Autopilot.Next(Planned(), settings, store, ["A"], null, claim: true, Now, Make, _ => "master");
        Assert.That(handed.Select(h => h.Item.Id), Is.EqualTo(new[] { "A-T1", "A-T2" }), "A's fixes only (A-T3 awaits approval, B isn't named), up to the 2 the allowance has room for");
        Assert.That(why, Is.Null);
        Assert.That(made, Is.EqualTo(new[] { @"D:\WT\A\A-T1", @"D:\WT\A\A-T2" }));
        Assert.That(handed[0].Prompt, Does.Contain("Work on ticket A-T1").And.Contain("cd there first").And.Contain("HEAD:master").And.Contain("workflow ticket close A-T1"));
        Assert.That(store.LoadAssignments().Select(a => a.TicketId), Is.EquivalentTo(new[] { "A-T1", "A-T2" }), "claimed, so not handed out twice");

        var (again, whyAgain) = Autopilot.Next(Planned(), settings, store, ["A"], null, claim: true, Now, Make, _ => "main");
        Assert.That(again, Is.Empty);
        Assert.That(whyAgain, Does.Contain("2 already being worked"));

        var (waiting, whyWaiting) = Autopilot.Next(Scheduler.Plan(Snap(("C", [new Ticket("C-T1", "2026-10-01", "chore", "open", "x", "")])), new(), []),
            settings, store, ["C"], null, claim: false, Now, Make, _ => "main");
        Assert.That((waiting.Count, whyWaiting), Is.EqualTo((0, "none left that may be worked: 1 wait for your approval (orchestrator approve)")));

        var stopped = Planned() with { Pace = Planned().Pace! with { Stop = true } };
        Assert.That(Autopilot.Next(stopped, settings, store, ["*"], null, claim: false, Now, Make, _ => "main").Why, Does.Contain("at its target"));
        Assert.That((Autopilot.Named(["GTFO.*"], "gtfo.Api"), Autopilot.Named(["GTFO.*"], "GTFO"), Autopilot.Named(["GTFO.*"], "MindAttic.GTFO.x"),
            Autopilot.Named(["*"], "anything"), Autopilot.Named(["A", "B"], "b")), Is.EqualTo((true, false, false, true, true)),
            "-repo=GTFO.* names every repo starting GTFO. (M.ORCH-T165)");
    }

    [Test]
    public void The_daemon_no_longer_starts_headless_agents_of_its_own()
    {
        var snap = Snap(("A", [T("A-T1")]));
        var said = new Autopilot(store, sessions) { Start = (_, _, _) => throw new AssertionException("no headless agent may start") }
            .Tick(Paced(snap, 5), snap, new(), Now);
        Assert.That(said, Is.Empty);
    }

    [Test]
    public void Only_a_line_that_is_a_needs_you_line_raises_the_alert()
    {
        Assert.That(Autopilot.NeedsYou("autopilot: needs you: A-T1's agent in A ended"), Is.EqualTo("A-T1's agent in A ended"));
        Assert.That(Autopilot.NeedsYou("autopilot: started A-T2 in A in the background: Toast when a session needs you: (log x)"), Is.Null,
            "a ticket summary can say \"needs you\"");
    }

    [Test]
    public void A_cmd_that_reuses_a_finished_agents_pid_is_not_the_agent()
    {
        var start = new DateTime(2026, 10, 15, 6, 0, 0, DateTimeKind.Utc);
        Assert.That(BackgroundAgent.SameStart(start.AddMilliseconds(400), start), Is.True);
        Assert.That(BackgroundAgent.SameStart(start.AddMinutes(20), start), Is.False);
        Assert.That(BackgroundAgent.IsRunning(Environment.ProcessId, DateTime.UtcNow), Is.False, "not a cmd, whatever its start");
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(dir, recursive: true);
    }

    private Autopilot Pilot(DispatchLease? lease = null, string? waiting = null) => new(store, sessions, lease)
    {
        WaitingPath = waiting,
        Order = () => order,
        Alive = clis.Contains,
        IsRunning = a => agents.Contains(a.Pilot!.Value),
        StartTimeOf = _ => null,
        LogDirectory = Path.Combine(dir, "logs"),
        MakeWorktree = (repo, path, branch) => (true, path, ""),
        Land = (a, repo) => { landedFrom[a.TicketId] = repo; return (landed.Contains(a.TicketId), ""); },
        Start = (d, prompt, log) =>
        {
            var pid = nextPid++;
            agents.Add(pid);
            started.Add((d, prompt, log, pid));
            return pid;
        },
    };

    /// <summary>An open Claude CLI (process <paramref name="pid"/>) in <paramref name="cwd"/>, idle since an hour ago
    /// unless it's <paramref name="busy"/> or its turn ended at <paramref name="turn"/>.</summary>
    private void Cli(int pid, string cwd, bool busy = false, DateTime? turn = null)
    {
        clis.Add(pid);
        // /autopilot typed in it: on for that CLI's own repo (a CLI at the workspace root has none).
        var rel = Path.GetRelativePath(@"D:\W", cwd);
        if (order is not null && rel != "." && rel.Split('\\')[0] is var repo && !order.Contains(repo)) order = [.. order, repo];
        File.WriteAllText(Path.Combine(sessions.Directory, "s" + pid + ".state.json"),
            $$"""{"state":"{{(busy ? "busy" : "idle")}}","at":"{{turn ?? Now.AddHours(-1):yyyy-MM-ddTHH:mm:ssZ}}","pid":{{pid}},"cwd":"{{cwd.Replace(@"\", @"\\")}}"}""");
    }

    private static Ticket T(string id) => new(id, "2026-10-01", "fix", "open", $"summary of {id}", "");

    private static Snapshot Snap(params (string Name, Ticket[] Open)[] repos) =>
        new(Now, [.. repos.Select(r => new RepoStatus(r.Name, $@"D:\W\{r.Name}", RepoState.Dormant, [], [], r.Open, null, default))], []);

    private SchedulePlan Plan(Snapshot snap, int cap = 6) => Scheduler.Plan(snap, new() { MaxConcurrentSessions = cap }, store.LoadAssignments());

    /// <summary>A plan whose pace has room for <paramref name="spare"/> more sessions.</summary>
    private SchedulePlan Paced(Snapshot snap, int spare) =>
        Plan(snap) with { Pace = new Pace(PaceMode.Open, 6, 10_000, 0, 1_000, "5-hour", Now.AddHours(3), "On pace.") { SpareSessions = spare } };

    [Test]
    public void Repos_with_an_open_cli_get_parallel_background_agents_each_in_its_own_worktree_and_nothing_is_typed()
    {
        Cli(100, @"D:\W\A");
        Cli(200, @"D:\W\B\src");
        Cli(300, @"D:\W"); // the workspace root: no repo of its own
        var snap = Snap(("A", [T("A-T1"), T("A-T2"), T("A-T3")]), ("B", [T("B-T1")]), ("C", [T("C-T1")]));
        var pilot = Pilot();

        var said = pilot.Tick(Paced(snap, 10), snap, new() { WorktreeRoot = @"D:\WT" }, Now);
        Assert.That(started.Select(s => (s.Dir, s.Prompt[..19])), Is.EquivalentTo(new[]
        {
            (@"D:\WT\A\A-T1", "Work on ticket A-T1"), (@"D:\WT\A\A-T2", "Work on ticket A-T2"), (@"D:\WT\A\A-T3", "Work on ticket A-T3"),
            (@"D:\WT\B\B-T1", "Work on ticket B-T1"),
        }), "with room, all of A's stories run at once, each in its worktree; C has no CLI open");
        Assert.That(started[0].Prompt, Does.Contain("own git worktree").And.Contains("branch orch/a-t1").And.Contains("/commit")
            .And.Contains("git push origin HEAD:main").And.Contains("workflow ticket close A-T1 --ref"));
        Assert.That(started[0].Log, Does.StartWith(Path.Combine(dir, "logs")).And.Contains("T1-"));
        Assert.That(said, Has.Some.Contains("in the background"));
        var a = store.LoadAssignments().Single(x => x.TicketId == "A-T1");
        Assert.That((a.Directory, a.Worktree, a.Pilot), Is.EqualTo((@"D:\WT\A\A-T1", true, (int?)started[0].Pid)));
        Assert.That(pilot.Working, Has.Count.EqualTo(4));

        pilot.Tick(Paced(snap, 10), snap, new() { WorktreeRoot = @"D:\WT" }, Now.AddMinutes(1));
        Assert.That(started, Has.Count.EqualTo(4), "nothing left that isn't already going");
    }

    [Test]
    public void A_finished_agent_makes_way_for_the_next_story_and_one_that_left_its_ticket_open_gives_it_back()
    {
        Cli(100, @"D:\W\A");
        var snap = Snap(("A", [T("A-T1"), T("A-T2"), T("A-T3")]));
        var pilot = Pilot();
        pilot.Tick(Paced(snap, 2), snap, new(), Now); // room for two: A-T1 and A-T2
        agents.Clear(); // both exit: A-T1 landed closed on main, A-T2 didn't close
        landed.Add("A-T1");

        var after = Snap(("A", [T("A-T2"), T("A-T3")]));
        var said = pilot.Tick(Paced(after, 2), after, new(), Now.AddMinutes(30));
        Assert.That(said, Has.Some.Contains("finished A-T1"));
        Assert.That(said, Has.Some.Contains("needs you: A-T2's agent in A ended without finishing it (closed and landed)"));
        pilot.Tick(Paced(after, 2), after, new(), Now.AddMinutes(31));
        Assert.That(started.Skip(2).Select(s => s.Prompt[..19]), Is.EqualTo(new[] { "Work on ticket A-T3" }),
            "A moves on to A-T3; A-T2 is back in the queue for you, not retried in a loop");
        Assert.That(store.LoadAssignments().Any(x => x.TicketId == "A-T2"), Is.False);
        Assert.That(Autopilot.ClosedIn("| A-T1 | 2026-10-01 | fix | closed | x | t |\n| A-T2 | 2026-10-01 | fix | open | y | — |", "A-T1"), Is.True);
        Assert.That(Autopilot.ClosedIn("| A-T1 | 2026-10-01 | fix | closed | x | t |\n| A-T2 | 2026-10-01 | fix | open | y | — |", "A-T2"), Is.False);
        Assert.That(Autopilot.ClosedIn("| A-T10 | 2026-10-01 | fix | closed | x | t |", "A-T1"), Is.False, "the whole ID, not a prefix");
    }

    [Test]
    public void It_starts_as_many_as_the_pace_has_room_for_right_now_counting_fresh_agents_as_drawing()
    {
        foreach (var (pid, name) in new[] { (1, "A"), (2, "B"), (3, "C") }) Cli(pid, $@"D:\W\{name}");
        var snap = Snap(("A", [T("A-T1"), T("A-T2")]), ("B", [T("B-T1")]), ("C", [T("C-T1")]));
        var pilot = Pilot();

        var said = pilot.Tick(Paced(snap, 2), snap, new() { MaxConcurrentSessions = 1 }, Now);
        Assert.That(started, Has.Count.EqualTo(2), "room for two: the session setting doesn't cap a paced run");
        Assert.That(said, Has.Some.Contains("the pace has no room for another"));

        pilot.Tick(Paced(snap, 3), snap, new(), Now.AddMinutes(2));
        Assert.That(started, Has.Count.EqualTo(3), "room for three, but the two started minutes ago aren't in the measured draw yet: one more");

        pilot.Tick(Paced(snap, -1), snap, new(), Now.AddMinutes(30));
        Assert.That(started, Has.Count.EqualTo(3), "over pace: nothing new; the running ones drain");

        Assert.That(Pacer.SpareSessions(10_000, 6_000, 1_000), Is.EqualTo(4));
        Assert.That(Pacer.SpareSessions(10_000, 12_500, 1_000), Is.EqualTo(-3));
        Assert.That(Pacer.SpareSessions(0, 0, 1_000), Is.EqualTo(0), "at the target");
        Assert.That(Pacer.SpareSessions(10_000, 0, null), Is.EqualTo(1), "no history: one, to measure it");
    }

    [Test]
    public void Repo_flag_works_named_repos_even_with_no_cli_open_and_off_starts_nothing()
    {
        var snap = Snap(("A", [T("A-T1")]), ("B", [T("B-T1")]));
        order = ["b", "Nope"];
        var said = Pilot().Tick(Plan(snap), snap, new(), Now);
        Assert.That(started.Single().Dir, Does.EndWith(@"\B\B-T1"));
        Assert.That(said, Has.Some.Contains("no repo named Nope"));

        order = null;
        Cli(100, @"D:\W\A");
        var off = Pilot();
        off.Tick(Plan(snap), snap, new(), Now);
        Assert.That((off.On, started.Count), Is.EqualTo((false, 1)));
    }

    [Test]
    public void Repos_being_worked_by_hand_or_set_to_be_skipped_get_no_new_agents()
    {
        var snap = Snap(("A", [T("A-T1")]), ("B", [T("B-T1")]), ("C", [T("C-T1")]), ("D", [T("D-T1")]));
        order = ["A", "B", "C", "D"];
        Cli(100, @"D:\W\A\src", busy: true);                    // mid-turn
        Cli(200, @"D:\W\B", turn: Now.AddMinutes(-5));          // between your prompts
        Cli(300, @"D:\W\C", busy: true);
        agents.Add(300);                                        // one of autopilot's own agents, not you
        store.SaveAssignments([new Assignment("X-T1", "C", @"D:\WT\C\X-T1", "Claude", Now, true) { Pilot = 300, Log = "x.log" }]);
        var waiting = Path.Combine(dir, "waiting.json");
        var pilot = Pilot(waiting: waiting);

        var said = pilot.Tick(Paced(snap, 10), snap, new() { AutopilotSkip = ["d"] }, Now);
        Assert.That(started.Select(s => s.Prompt[..19]), Is.EqualTo(new[] { "Work on ticket C-T1" }), "only C: its busy CLI is autopilot's own agent");
        Assert.That(said, Has.Some.EqualTo("autopilot: leaving A alone: its CLI is being worked by hand"));
        Assert.That(said, Has.Some.EqualTo("autopilot: leaving B alone: its CLI is being worked by hand"));
        Assert.That(said, Has.Some.Contains("leaving D alone: you set it to be skipped"));
        Assert.That(pilot.Repos, Is.EqualTo(new[] { "C" }));
        Assert.That(pilot.LeftAlone.Keys, Is.EquivalentTo(new[] { "A", "B", "D" }), "the dashboard says which and why");
        Assert.That(File.ReadAllText(waiting), Does.Contain("\"left_alone\":{").And.Contain("\"D\":\"you set it to be skipped"), "and so can the statusline");

        pilot.Tick(Paced(snap, 10), snap, new() { AutopilotSkip = ["d"], AutopilotHandMinutes = 10 }, Now.AddMinutes(1));
        Assert.That(started, Has.Count.EqualTo(1), "with a 10-minute window B's pause of 6 minutes is still yours");
        pilot.Tick(Paced(snap, 10), snap, new() { AutopilotSkip = ["d"], AutopilotHandMinutes = 10 }, Now.AddMinutes(6));
        Assert.That(started.Select(s => s.Prompt[..19]), Is.EquivalentTo(new[] { "Work on ticket C-T1", "Work on ticket B-T1" }),
            "B's CLI has been idle past the window, so autopilot takes it; A is still mid-turn");
        Assert.That(pilot.LeftAlone.Keys, Is.EquivalentTo(new[] { "A", "D" }));
    }

    [Test]
    public void A_story_going_in_a_tab_is_left_to_it_and_the_pace_or_switch_holds_everything()
    {
        Cli(100, @"D:\W\A");
        var snap = Snap(("A", [T("A-T1"), T("A-T2")]));
        store.SaveAssignments([new Assignment("A-T1", "A", @"D:\W\A", "Claude", Now, false)]);
        Pilot().Tick(Plan(snap), snap, new(), Now);
        Assert.That(started.Single().Prompt, Does.StartWith("Work on ticket A-T2"), "A-T1 is the tab's; the agent takes the next, in its own worktree");
        started.Clear();
        agents.Clear();

        store.SaveAssignments([]);
        var coast = Plan(snap) with { Pace = new Pace(PaceMode.Coast, 1, 0, 0, null, null, null, "burning faster than the window allows") };
        var pilot = Pilot();
        pilot.Tick(coast, snap, new(), Now);
        Assert.That(pilot.Paused, Does.Contain("burning faster"));
        pilot.Tick(Plan(snap), snap, new() { Autopilot = false }, Now);
        Assert.That(pilot.Paused, Does.Contain("switched off"));
        Assert.That(started, Is.Empty);
    }

    [Test]
    public void Repos_waiting_on_the_pace_are_saved_for_the_statusline_and_cleared_once_one_starts()
    {
        Cli(100, @"D:\W\A");
        Cli(101, @"D:\W\B");
        var snap = Snap(("A", [T("A-T1")]), ("B", [T("B-T1")]));
        var path = Path.Combine(dir, "waiting.json");
        var withFile = Pilot(waiting: path);
        string Saved() => File.ReadAllText(path);

        var coast = Plan(snap) with { Pace = new Pace(PaceMode.Coast, 1, 0, 0, null, null, null, "burning faster than the window allows") };
        withFile.Tick(coast, snap, new(), Now);
        Assert.That(Saved(), Does.Contain("\"A\"").And.Contain("\"B\""), "both repos wait on the pace");

        withFile.Tick(Paced(snap, 1), snap, new(), Now.AddMinutes(1));
        Assert.That(started, Has.Count.EqualTo(1));
        var startedRepo = started.Single().Prompt.Contains("A-T1") ? "A" : "B";
        var other = startedRepo == "A" ? "B" : "A";
        Assert.That(Saved(), Does.Contain($"\"{other}\"").And.Not.Contain($"\"{startedRepo}\""), "the repo with an agent working no longer waits");

        withFile.Tick(Paced(snap, 10), snap, new() { Autopilot = false }, Now.AddMinutes(2));
        Assert.That(Saved(), Does.Contain("\"repos\":[]"), "switched off isn't waiting on the pace");
    }

    [Test]
    public void A_restart_adopts_agents_still_working_instead_of_starting_them_again()
    {
        Cli(100, @"D:\W\A");
        var snap = Snap(("A", [T("A-T1"), T("A-T2")]));
        OrchestratorSettings one = new() { RepoMaxSessions = new() { ["A"] = 1 } };
        Pilot().Tick(Plan(snap), snap, one, Now);

        var restarted = Pilot(); // a new daemon: same store, the agents' processes still running
        restarted.Tick(Plan(snap), snap, one, Now.AddMinutes(5));
        Assert.That(started, Has.Count.EqualTo(1));
        Assert.That(restarted.Working.Single().TicketId, Is.EqualTo("A-T1"));
    }

    [Test]
    public void Only_one_process_starts_agents_and_the_next_takes_over_when_it_exits()
    {
        Cli(100, @"D:\W\A");
        var snap = Snap(("A", [T("A-T1")]));
        var leases = Path.Combine(dir, "lease");
        using var daemonLease = new DispatchLease(leases);
        using var dashboardLease = new DispatchLease(leases);
        var daemon = Pilot(daemonLease);
        var dashboard = Pilot(dashboardLease);

        daemon.Tick(Plan(snap), snap, new(), Now);
        dashboard.Tick(Plan(snap), snap, new(), Now);
        Assert.That(dashboard.HeldElsewhere, Is.True);
        Assert.That(started, Has.Count.EqualTo(1), "started once, not by both");

        daemonLease.Dispose();
        dashboard.Tick(Plan(snap), snap, new(), Now);
        Assert.That(dashboard.HeldElsewhere, Is.False, "the lease is free again");
    }

    [Test]
    public void The_agent_runs_the_launchers_claude_command_headless_with_its_prompt_from_a_file()
    {
        var settings = Path.Combine(dir, "launcher.json");
        File.WriteAllText(settings, """{"agentProviders":[{"key":"Codex","runCommand":"codex x"},{"key":"Claude","runCommand":"claude --dangerously-skip-permissions --model claude-sonnet-5"}]}""");
        Assert.That(BackgroundAgent.ClaudeCommand(settings), Is.EqualTo("claude --dangerously-skip-permissions --model claude-sonnet-5"));
        Assert.That(BackgroundAgent.ClaudeCommand(Path.Combine(dir, "missing.json")), Is.EqualTo(BackgroundAgent.FallbackCommand));
        Assert.That(BackgroundAgent.CommandLine(@"C:\l\A-T1.prompt.txt", @"C:\l\A-T1.log", "claude --x"),
            Is.EqualTo("""claude --x -p < "C:\l\A-T1.prompt.txt" > "C:\l\A-T1.log" 2>&1"""));
    }

    [Test]
    public void A_worktree_agent_that_pushed_its_closed_ticket_to_main_is_landed_and_tidied_up()
    {
        var origin = Path.Combine(dir, "origin.git");
        var repo = Path.Combine(dir, "A");
        Git(dir, "init", "-q", "--bare", "-b", "main", origin);
        Git(dir, "clone", "-q", origin, repo);
        Directory.CreateDirectory(Path.Combine(repo, "docs"));
        File.WriteAllText(Path.Combine(repo, "docs", "USER_STORIES.md"), "## Tickets\n\n| A-T1 | 2026-10-01 | fix | open | x | — |\n");
        Git(repo, "add", ".");
        Git(repo, "-c", "user.email=t@t", "-c", "user.name=t", "commit", "-qm", "init");
        Git(repo, "push", "-q", "origin", "HEAD:main");

        var tree = Path.Combine(dir, "wt", "A-T1");
        Assert.That(Dispatcher.EnsureWorktree(repo, tree, "orch/a-t1").Ok);
        var a = new Assignment("A-T1", "A", tree, "Claude", Now, true) { Pilot = 1, Log = "x.log" };
        Assert.That(Autopilot.LandOnMain(a, repo).Closed, Is.False, "not pushed yet");

        // What the agent does at the end: close its row in the worktree, commit, push to main.
        File.WriteAllText(Path.Combine(tree, "docs", "USER_STORIES.md"), "## Tickets\n\n| A-T1 | 2026-10-01 | fix | closed | x | the test |\n");
        Git(tree, "-c", "user.email=t@t", "-c", "user.name=t", "commit", "-qam", "close A-T1");
        Git(tree, "push", "-q", "origin", "HEAD:main");

        var (closed, note) = Autopilot.LandOnMain(a, repo);
        Assert.That(closed, Is.True, note);
        Assert.That(File.ReadAllText(Path.Combine(repo, "docs", "USER_STORIES.md")), Does.Contain("| closed |"), "your checkout fast-forwarded to it");
        Assert.That(Directory.Exists(tree), Is.False, "its worktree removed");
    }

    private static void Git(string where, params string[] args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        psi.ArgumentList.Add("-C"); psi.ArgumentList.Add(where);
        foreach (var x in args) psi.ArgumentList.Add(x);
        using var p = System.Diagnostics.Process.Start(psi)!;
        var err = p.StandardError.ReadToEndAsync();
        p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        Assert.That(p.ExitCode, Is.Zero, $"git {string.Join(" ", args)}: {err.Result}");
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
    public async Task Enter_goes_to_the_tab_on_its_own_after_the_text_so_it_isnt_read_as_part_of_a_paste()
    {
        // Found live: text and Enter in one write left a long story prompt sitting unsent in the box.
        var pipe = "orch-test-" + Guid.NewGuid().ToString("N");
        var got = new List<(string Text, DateTime At)>();
        var server = Task.Run(async () =>
        {
            for (var i = 0; i < 2; i++)
            {
                using var s = new System.IO.Pipes.NamedPipeServerStream(pipe, System.IO.Pipes.PipeDirection.In, 1,
                    System.IO.Pipes.PipeTransmissionMode.Byte, System.IO.Pipes.PipeOptions.Asynchronous);
                await s.WaitForConnectionAsync();
                using var r = new StreamReader(s);
                got.Add((await r.ReadToEndAsync(), DateTime.UtcNow));
            }
        });

        Assert.That(await SessionControl.SubmitAsync(pipe, "Work on ticket X-T1 (fix): a long prompt\n"), Is.True);
        await server.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.That(got.Select(g => g.Text), Is.EqualTo(new[] { "Work on ticket X-T1 (fix): a long prompt", "\n" }));
        Assert.That(got[1].At - got[0].At, Is.GreaterThanOrEqualTo(SessionControl.EnterAfter - TimeSpan.FromMilliseconds(100)));
    }

    [Test]
    public void A_cli_the_updater_renamed_still_counts_as_claude()
    {
        Assert.That(ConsoleTyper.IsClaude("claude"), Is.True);
        Assert.That(ConsoleTyper.IsClaude("claude.exe.old.1791946834302.10372"), Is.True, "found live: the updater renames the running binary");
        Assert.That(ConsoleTyper.IsClaude("claudia"), Is.False);
    }
}
