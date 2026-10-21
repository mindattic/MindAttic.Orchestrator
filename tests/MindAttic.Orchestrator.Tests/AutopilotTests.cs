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
        // Found live: /allocate all put all five spare sessions into Automata while a +70 story elsewhere waited.
        order = [Autopilot.AllRepos];
        var snap = Snap(("Automata", [T("AUT-T1"), T("AUT-T2"), T("AUT-T3")]), ("Launcher", [T("M.LNCH-T21")]));
        snap = snap with { Priorities = new Dictionary<string, long> { ["M.LNCH-T21"] = 70 } };
        var plan = Scheduler.Plan(snap, new(), store.LoadAssignments());
        plan = plan with { Pace = new Pace(PaceMode.Open, 6, 10_000, 0, 1_000, "5-hour", Now.AddHours(3), "On pace.") { SpareSessions = 2 } };

        Pilot().Tick(plan, snap, new() { WorktreeRoot = @"D:\WT" }, Now);

        Assert.That(started.Select(s => s.Dir).First(), Is.EqualTo(@"D:\WT\Launcher\M.LNCH-T21"), "the prioritized story goes first");
        Assert.That(started, Has.Count.EqualTo(2));
    }

    [Test]
    public void Past_the_wind_down_line_no_story_starts_until_the_reset_estimate_or_not()
    {
        // M.ORCH-T254: stories whose estimates each fit what was left still started past 90%, five at once near the reset.
        Cli(100, @"D:\W\A");
        var snap = Snap(("A", [T("A-T1"), T("A-T2")]));
        var winding = Paced(snap, 5);
        winding = winding with
        {
            Queue = [.. winding.Queue.Select(q => q.Id == "A-T1" ? q with { Estimate = new CostEstimate(1_000, 3, "fixes") } : q)],
            Pace = winding.Pace! with { WindDown = true, RoomTokens = 1_000_000 },
        };

        var pilot = Pilot();
        var said = pilot.Tick(winding, snap, new(), Now);

        Assert.That(started, Is.Empty, "neither the story whose estimate fits nor the one with none starts before the reset");
        Assert.That(pilot.Paused, Does.Contain("past 90%").And.Contain("resets"));
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
    public void A_closed_tickets_worktree_with_a_tab_open_in_it_or_a_session_lately_written_from_it_is_not_swept()
    {
        // M.ORCH-T329: a tab's or claim's assignment is reaped as soon as its ticket closes, so the sweep saw nobody there.
        var wt = Path.Combine(dir, "wt", "A");
        foreach (var id in new[] { "A-T7", "A-T8", "A-T9" })
        {
            Directory.CreateDirectory(Path.Combine(wt, id, "src"));
            File.WriteAllText(Path.Combine(wt, id, ".git"), "gitdir: x");
            landed.Add(id);
        }
        Cli(100, @"D:\W\A");
        var snap = Snap(("A", []));
        snap = snap with
        {
            Repos = [snap.Repos[0] with
            {
                Hosts = [new HostSession("Claude", 7, "pipe", Path.Combine(wt, "A-T7"))],
                Sessions =
                [
                    new TranscriptSession("recent", Path.Combine(wt, "A-T8", "src"), Now.AddMinutes(-5), default, null),
                    new TranscriptSession("long-gone", Path.Combine(wt, "A-T9"), Now.AddHours(-2), default, null),
                ],
            }],
        };

        Pilot().Tick(Paced(snap, 0), snap, new() { WorktreeRoot = Path.Combine(dir, "wt") }, Now);

        Assert.That(landedFrom.Keys, Is.EquivalentTo(new[] { "A-T9" }), "only the one nobody has been in for a while is tidied");
    }

    [Test]
    public void The_sweep_leaves_a_worktree_whose_name_is_not_a_ticket_id_alone()
    {
        // M.ORCH-T547: any clean folder whose name the db didn't know read as a closed ticket and was removed, e.g. a bughunt
        // worktree a subagent was working in (its cwd is its CLI's, so no activity is seen there).
        var wt = Path.Combine(dir, "wt", "A");
        foreach (var name in new[] { "bugfix-20261020-c", "scratch", "USS-T5", "M.COMP-T4", "A-T7" })
        {
            Directory.CreateDirectory(Path.Combine(wt, name));
            File.WriteAllText(Path.Combine(wt, name, ".git"), "gitdir: x");
            landed.Add(name.ToUpperInvariant());
        }
        Cli(100, @"D:\W\A");
        var snap = Snap(("A", []));

        Pilot().Tick(Paced(snap, 0), snap, new() { WorktreeRoot = Path.Combine(dir, "wt") }, Now);

        Assert.That(landedFrom.Keys, Is.EquivalentTo(new[] { "USS-T5", "M.COMP-T4", "A-T7" }),
            "ticket-shaped names (an old prefix too, M.ORCH-T368) are swept; anything else isn't a ticket's worktree");
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
        // M.ORCH-T164: /allocate works tickets through the CLI's own subagents; `orchestrator next --claim` hands them out.
        var snap = Snap(("A", [T("A-T1"), T("A-T2"), new Ticket("A-T3", "2026-10-01", "feature", "open", "not approved", "")]), ("B", [T("B-T1")]));
        var settings = new OrchestratorSettings { WorktreeRoot = @"D:\WT" };
        var made = new List<string>();
        (bool, string, string) Make(string repo, string path, string branch) { made.Add(path); return (true, path, ""); }
        SchedulePlan Planned() => Paced(snap, 5) with { Pace = Paced(snap, 5).Pace! with { SubagentAllowance = 4, Subagents = 2 } };

        var before = Planned(); // what a second CLI read before this one's claim landed
        var (handed, why) = Autopilot.Next(Planned(), settings, store, ["A"], null, claim: true, Now, Make, _ => "master");
        Assert.That(handed.Select(h => h.Item.Id), Is.EqualTo(new[] { "A-T1", "A-T2" }), "A's fixes only (A-T3 awaits approval, B isn't named), up to the 2 the allowance has room for");
        Assert.That(why, Is.Null);
        Assert.That(made, Is.EqualTo(new[] { @"D:\WT\A\A-T1", @"D:\WT\A\A-T2" }));
        Assert.That(handed[0].Prompt, Does.Contain("Work on ticket A-T1").And.Contain("cd there first").And.Contain("HEAD:master").And.Contain("workflow ticket close A-T1"));
        Assert.That(store.LoadAssignments().Select(a => a.TicketId), Is.EquivalentTo(new[] { "A-T1", "A-T2" }), "claimed, so not handed out twice");

        // A second CLI whose plan was read before the first one's claim landed hands nothing out twice.
        var (raced, whyRaced) = Autopilot.Next(before, settings, store, ["A"], null, claim: true, Now.AddSeconds(1), Make, _ => "master");
        Assert.That(raced, Is.Empty, "both were claimed by the first CLI between this plan and its claim");
        Assert.That(whyRaced, Is.EqualTo("another CLI claimed them first; nothing else is ready"));
        Assert.That(store.LoadAssignments().Select(a => (a.TicketId, a.StartedUtc)), Is.EquivalentTo(new[] { ("A-T1", Now), ("A-T2", Now) }),
            "the first CLI's claims stand");

        var (again, whyAgain) = Autopilot.Next(Planned(), settings, store, ["A"], null, claim: true, Now, Make, _ => "main");
        Assert.That(again, Is.Empty);
        Assert.That(whyAgain, Does.Contain("2 already being worked"));

        var (waiting, whyWaiting) = Autopilot.Next(Scheduler.Plan(Snap(("C", [new Ticket("C-T1", "2026-10-01", "chore", "open", "x", "")])), new(), []),
            settings, store, ["C"], null, claim: false, Now, Make, _ => "main");
        Assert.That((waiting.Count, whyWaiting), Is.EqualTo((0, "none left that may be worked: 1 wait for your approval (orchestrator approve)")));

        var stopped = Planned() with { Pace = Planned().Pace! with { Stop = true } };
        Assert.That(Autopilot.Next(stopped, settings, store, ["*"], null, claim: false, Now, Make, _ => "main").Why, Does.Contain("at its target"));
        // M.ORCH-T481: while a /handoff winds every CLI down, nothing is handed out, unthrottled or not, and nothing is claimed.
        var handingOff = Autopilot.Next(Planned(), settings, store, ["*"], null, claim: true, Now, Make, _ => "main", unthrottled: true, handingOff: true);
        Assert.That((handingOff.Handed.Count, handingOff.Why), Is.EqualTo((0, MindAttic.Orchestrator.Sessions.Handoff.HoldReason)));
        Assert.That(handingOff.Why, Does.StartWith("handing off"));
        Assert.That((Autopilot.Named(["GTFO.*"], "gtfo.Api"), Autopilot.Named(["GTFO.*"], "GTFO"), Autopilot.Named(["GTFO.*"], "MindAttic.GTFO.x"),
            Autopilot.Named(["*"], "anything"), Autopilot.Named(["A", "B"], "b")), Is.EqualTo((true, false, false, true, true)),
            "-repo=GTFO.* names every repo starting GTFO. (M.ORCH-T165)");
    }

    /// <summary>M.ORCH-T495: one repo's /allocate claimed all 8 places at once (8 subagents on one repo) while other CLIs got none.</summary>
    [Test]
    public void A_cli_running_autopilot_claims_its_fair_share_of_the_allowance_not_every_place()
    {
        var snap = Snap(("A", [.. Enumerable.Range(1, 10).Select(i => T($"A-T{i}"))]), ("B", [T("B-T1"), T("B-T2"), T("B-T3"), T("B-T4"), T("B-T5")]), ("C", []));
        var settings = new OrchestratorSettings { WorktreeRoot = @"D:\WT" };
        (bool, string, string) Make(string repo, string path, string branch) => (true, path, "");
        SchedulePlan Planned(int running = 0, Dictionary<int, int>? byCli = null) => Paced(snap, 5) with
        {
            Pace = Paced(snap, 5).Pace! with { SubagentAllowance = 8, Subagents = running, SubagentsByCli = byCli ?? [] },
        };
        Dictionary<int, IReadOnlyList<string>> pilots = new() { [1] = ["A"], [2] = ["B"] };

        var (handed, _) = Autopilot.Next(Planned(), settings, store, ["A"], null, claim: false, Now, Make, _ => "main", cli: 1, pilots: pilots);
        Assert.That(handed, Has.Count.EqualTo(4), "CLI 2's /allocate has B's tickets to work: half the 8 is kept for it");

        var (b, _) = Autopilot.Next(Planned(4, new() { [1] = 4 }), settings, store, ["B"], null, claim: false, Now, Make, _ => "main", cli: 2, pilots: pilots);
        Assert.That(b, Has.Count.EqualTo(4), "and CLI 2 gets it");

        var (full, why) = Autopilot.Next(Planned(4, new() { [1] = 4 }), settings, store, ["A"], null, claim: false, Now, Make, _ => "main", cli: 1, pilots: pilots);
        Assert.That(full, Is.Empty);
        Assert.That(why, Does.Contain("fair share"));

        // A CLI whose repo has nothing workable lends its share: CLI 1 may have all 8.
        var idle = new Dictionary<int, IReadOnlyList<string>> { [1] = ["A"], [2] = ["C"] };
        Assert.That(Autopilot.Next(Planned(), settings, store, ["A"], null, claim: false, Now, Make, _ => "main", cli: 1, pilots: idle).Handed, Has.Count.EqualTo(8));
        // An explicit --count isn't held to a share.
        Assert.That(Autopilot.Next(Planned(), settings, store, ["A"], 6, claim: false, Now, Make, _ => "main", cli: 1, pilots: pilots).Handed, Has.Count.EqualTo(6));
    }

    /// <summary>M.ORCH-T558: the first CLI to turn /allocate on claimed every place before the CLIs opened just after it could.</summary>
    [Test]
    public void A_cli_whose_autopilot_just_turned_on_warms_up_one_more_subagent_each_minute()
    {
        var snap = Snap(("A", [.. Enumerable.Range(1, 12).Select(i => T($"A-T{i}"))]), ("B", [T("B-T1"), T("B-T2")]));
        var settings = new OrchestratorSettings { WorktreeRoot = @"D:\WT" };
        (bool, string, string) Make(string repo, string path, string branch) => (true, path, "");
        SchedulePlan Planned(int running = 0, Dictionary<int, int>? byCli = null) => Paced(snap, 5) with
        {
            Pace = Paced(snap, 5).Pace! with { SubagentAllowance = 12, Subagents = running, SubagentsByCli = byCli ?? [] },
        };
        Dictionary<int, IReadOnlyList<string>> alone = new() { [1] = ["A"] };
        var since = new Dictionary<int, DateTime> { [1] = Now };

        var (first, _) = Autopilot.Next(Planned(), settings, store, ["A"], null, claim: false, Now, Make, _ => "main", cli: 1, pilots: alone, onSince: since);
        Assert.That(first, Has.Count.EqualTo(1), "minute 0: one, not all 12");
        var (held, why) = Autopilot.Next(Planned(1, new() { [1] = 1 }), settings, store, ["A"], null, claim: false, Now.AddSeconds(30), Make, _ => "main",
            cli: 1, pilots: alone, onSince: since);
        Assert.That(held, Is.Empty);
        Assert.That(why, Does.Contain("warming up"));
        Assert.That(Autopilot.Next(Planned(1, new() { [1] = 1 }), settings, store, ["A"], null, claim: false, Now.AddMinutes(2.5), Make, _ => "main",
            cli: 1, pilots: alone, onSince: since).Handed, Has.Count.EqualTo(2), "minute 2: up to 3");
        Assert.That(Autopilot.Next(Planned(), settings, store, ["A"], null, claim: false, Now.AddMinutes(30), Make, _ => "main",
            cli: 1, pilots: alone, onSince: since).Handed, Has.Count.EqualTo(12), "warmed up and alone: every place");

        // A CLI opened just after it turns /allocate on: it warms up from its own minute, and its share is kept for it.
        Dictionary<int, IReadOnlyList<string>> both = new() { [1] = ["A"], [2] = ["B"] };
        since[2] = Now.AddMinutes(3);
        Assert.That(Autopilot.Next(Planned(3, new() { [1] = 3 }), settings, store, ["A"], null, claim: false, Now.AddMinutes(10), Make, _ => "main",
            cli: 1, pilots: both, onSince: since).Handed, Has.Count.EqualTo(3), "CLI 1 gets up to its share of 6, the rest kept for CLI 2");
        Assert.That(Autopilot.Next(Planned(6, new() { [1] = 6 }), settings, store, ["B"], null, claim: false, Now.AddMinutes(3), Make, _ => "main",
            cli: 2, pilots: both, onSince: since).Handed, Has.Count.EqualTo(1), "CLI 2's own minute 0: one");
        Assert.That(Autopilot.Next(Planned(), settings, store, ["A"], null, claim: false, Now, Make, _ => "main", cli: 1, pilots: alone).Handed,
            Has.Count.EqualTo(12), "not known to be on: no warm-up");
    }

    [Test]
    public void A_ticket_closed_after_the_queue_was_read_is_not_claimed_or_given_a_worktree()
    {
        // M.ORCH-T489: `next --claim` handed out M.ORCH-T488, and made its worktree, a second after `workflow ticket close`
        // had closed it: the plan's ticket list was older than the close. The claim checks the database at that moment.
        var repo = Path.Combine(dir, "A");
        Directory.CreateDirectory(Path.Combine(repo, "docs"));
        File.WriteAllText(Path.Combine(repo, "docs", "USER_STORIES.md"),
            "---\ncode: A\n---\n\n# A\n\n## Tickets\n\n| ID | Date | Type | Status | Summary | Ref |\n|---|---|---|---|---|---|\n" +
            "| A-T1 | 2026-10-01 | fix | open | x | — |\n| A-T2 | 2026-10-01 | fix | open | y | — |\n");
        try
        {
            using (var db = MindAttic.Workflow.Data.WorkflowDb.Open(MindAttic.Workflow.Data.WorkflowDb.DefaultPath(dir)))
                db.Import([new MindAttic.Workflow.Repo("A", repo)]);
            var snap = new Snapshot(Now, [new RepoStatus("A", repo, RepoState.Dormant, [], [], [T("A-T1"), T("A-T2")], null, default)], []);
            var plan = Paced(snap, 5); // read while both were open
            using (var db = MindAttic.Workflow.Data.WorkflowDb.Open(MindAttic.Workflow.Data.WorkflowDb.DefaultPath(dir)))
                db.Close("A-T1", reference: "done");
            Assert.That((Autopilot.StillOpen(repo, "A-T1"), Autopilot.StillOpen(repo, "a-t2")), Is.EqualTo((false, true)));

            var made = new List<string>();
            (bool, string, string) Make(string r, string path, string branch) { made.Add(path); return (true, path, ""); }
            var settings = new OrchestratorSettings { WorktreeRoot = Path.Combine(dir, "wt") };
            var (handed, why) = Autopilot.Next(plan, settings, store, ["A"], 1, claim: true, Now, Make, _ => "main", pilotRunning: _ => false);
            Assert.That(handed.Select(h => h.Item.Id), Is.EqualTo(new[] { "A-T2" }), "the closed one is passed over for the next");
            Assert.That(made, Is.EqualTo(new[] { Path.Combine(dir, "wt", "A", "A-T2") }), "no worktree for the closed one");
            Assert.That(store.LoadAssignments().Select(a => a.TicketId), Is.EqualTo(new[] { "A-T2" }));

            using (var db = MindAttic.Workflow.Data.WorkflowDb.Open(MindAttic.Workflow.Data.WorkflowDb.DefaultPath(dir)))
                db.Close("A-T2", reference: "done");
            var (none, whyNone) = Autopilot.Next(plan with { }, settings, new JsonStore(Path.Combine(dir, "store2")), ["A"], 1, claim: true, Now, Make, _ => "main",
                pilotRunning: _ => false);
            Assert.That((none.Count, whyNone), Is.EqualTo((0, "they were closed since the queue was read; nothing else is ready")));
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); }
    }

    [Test]
    public void Pausing_autopilot_quiet_hours_and_a_skipped_repo_apply_to_the_clis_that_run_it()
    {
        var snap = Snap(("A", [T("A-T1")]), ("B", [T("B-T1")]));
        (bool, string, string) Make(string repo, string path, string branch) => (true, path, "");
        (IReadOnlyList<Autopilot.Handout> Handed, string? Why) Next(OrchestratorSettings s) =>
            Autopilot.Next(Paced(snap, 5), s with { WorktreeRoot = @"D:\WT" }, store, ["*"], null, claim: false, Now, Make, _ => "main");

        Assert.That(Next(new() { Autopilot = false }).Why, Does.StartWith("autopilot is paused"), "Settings' Pause, settings --autopilot false");
        var local = Now.ToLocalTime();
        var quiet = $"{local.AddHours(-1):HH:mm}-{local.AddHours(1):HH:mm}";
        Assert.That(Next(new() { QuietHours = [quiet] }).Why, Does.StartWith("quiet hours"));
        Assert.That(Next(new() { AutopilotSkip = ["a"] }).Handed.Select(h => h.Item.Id), Is.EqualTo(new[] { "B-T1" }), "a skipped repo isn't handed out under *");
        Assert.That(Next(new() { AutopilotSkip = ["a", "B"] }).Why, Does.StartWith("A, B set to be skipped").Or.StartWith("B, A set to be skipped"),
            "says why, not 'no open fixes left'");
        Assert.That(store.LoadAssignments(), Is.Empty);
    }

    [Test]
    public void Open_tickets_held_back_are_said_so_not_reported_as_none_left()
    {
        var snap = Snap(("A", [new Ticket("A-T1", "2026-10-01", "fix", "open", "crash, blocked by A-T2", ""),
                               new Ticket("A-T2", "2026-10-01", "feature", "open", "the API (not approved)", "")]));
        var (handed, why) = Autopilot.Next(Paced(snap, 5), new OrchestratorSettings { WorktreeRoot = @"D:\WT" }, store, ["A"], null, claim: false, Now,
            (repo, path, branch) => (true, path, ""), _ => "main");
        Assert.That(handed, Is.Empty);
        Assert.That(why, Does.Contain("1 wait on other open tickets").And.Not.Contain("no open fixes"));
    }

    [Test]
    public void With_pacing_off_the_windows_stop_still_hands_nothing_out()
    {
        // Unpaced, the plan keeps no pace; the stop (which doesn't depend on pacing) still has to reach `next --claim`.
        var snap = Snap(("A", [T("A-T1")])) with { Pace = new Pace(PaceMode.Unpaced, 6, 0, 0, null, "5-hour", Now.AddHours(1), "Pacing is off.") { Stop = true } };
        var plan = Scheduler.Plan(snap, new OrchestratorSettings { Pacing = false }, []);
        Assert.That((plan.Pace, plan.Stopped), Is.EqualTo(((Pace?)null, true)));

        var (handed, why) = Autopilot.Next(plan, new OrchestratorSettings { WorktreeRoot = @"D:\WT" }, store, ["A"], null, claim: true, Now,
            (repo, path, branch) => (true, path, ""), _ => "main");
        Assert.That((handed.Count, why), Is.EqualTo((0, "the 5-hour window is at its target: nothing starts before it resets")));
        Assert.That(store.LoadAssignments(), Is.Empty);
    }

    [Test]
    public void A_ticket_another_cli_claimed_first_is_passed_over_for_the_next_one()
    {
        var snap = Snap(("A", [T("A-T1"), T("A-T2")]));
        var settings = new OrchestratorSettings { WorktreeRoot = @"D:\WT" };
        (bool, string, string) Make(string repo, string path, string branch) => (true, path, "");
        var before = Paced(snap, 5);
        store.UpdateAssignments(all => all.Add(new Assignment("A-T1", "A", @"D:\WT\A\A-T1", "Claude", Now, true))); // the other CLI

        var (handed, why) = Autopilot.Next(before, settings, store, ["A"], 1, claim: true, Now.AddSeconds(1), Make, _ => "main");

        Assert.That((handed.Select(h => h.Item.Id).Single(), why), Is.EqualTo(("A-T2", (string?)null)), "its one place goes to the next free ticket");
    }

    [Test]
    public void A_ticket_another_agent_is_working_is_not_handed_out()
    {
        // M.ORCH-T255: `next --claim` handed out M.ORCH-T250, T251 and T253 while the agent that filed each was fixing it, so
        // three subagents redid the work and hit rebase conflicts.
        var cli = new TranscriptSession("cli", @"D:\W\A", Now.AddMinutes(-1), default, null)
        {
            Filing = [new FiledTicket("A-T1", "agent-x", Now.AddMinutes(-2)), new FiledTicket("A-T2", null, Now.AddMinutes(-1)),
                new FiledTicket("A-T6", "agent-y", Now.AddHours(-1))],
        };
        var other = new TranscriptSession("other", @"D:\W\A", Now.AddMinutes(-5), default, null)
        {
            TicketId = "A-T3", Filing = [new FiledTicket("A-T4", null, Now.AddMinutes(-5))],
        };
        var snap = Snap(("A", [T("A-T1"), T("A-T2"), T("A-T3"), T("A-T4"), T("A-T5"), T("A-T6")])) is var s0
            ? s0 with { Repos = [s0.Repos[0] with { Sessions = [cli, other] }] } : null!;
        var settings = new OrchestratorSettings { WorktreeRoot = @"D:\WT" };
        (bool, string, string) Make(string repo, string path, string branch) => (true, path, "");
        var plan = Paced(snap, 5);

        Assert.That(plan.Worked.Select(w => (w.TicketId, w.SessionId, w.AgentId)), Is.EquivalentTo(new[]
        {
            ("A-T1", "cli", (string?)"agent-x"), ("A-T2", "cli", null), ("A-T3", "other", null), ("A-T4", "other", null),
        }), "A-T6's filer has been quiet an hour: it's the queue's again");
        Assert.That(plan.Proposals.Select(p => p.Item.Id), Has.None.AnyOf("A-T1", "A-T2", "A-T3", "A-T4"));

        var (handed, _) = Autopilot.Next(plan, settings, store, ["A"], 10, claim: false, Now, Make, _ => "main", self: "cli");
        Assert.That(handed.Select(h => h.Item.Id), Is.EquivalentTo(new[] { "A-T2", "A-T5", "A-T6" }),
            "not what the calling CLI's subagent filed and is fixing, nor another session's; what the calling CLI filed itself is its to hand out");

        var onlyWorked = Paced(snap with { Repos = [snap.Repos[0] with { OpenTickets = [T("A-T1"), T("A-T3")] }] }, 5);
        var (none, why) = Autopilot.Next(onlyWorked, settings, store, ["A"], 10, claim: true, Now, Make, _ => "main", self: "cli");
        Assert.That((none.Count, why), Is.EqualTo((0, "2 already being worked; nothing else is ready")));
        Assert.That(store.LoadAssignments().Select(a => a.TicketId), Has.None.AnyOf("A-T1", "A-T3"), "never claimed");
    }

    [Test]
    public void Next_hands_a_repo_no_more_than_the_sessions_you_capped_it_at()
    {
        // M.ORCH-T358: settings --repo-sessions was enforced only by the retired Tick; next --claim handed out all four.
        var snap = Snap(("A", [T("A-T1"), T("A-T2"), T("A-T3"), T("A-T4")]), ("B", [T("B-T1")]));
        var settings = new OrchestratorSettings { WorktreeRoot = @"D:\WT", RepoMaxSessions = new() { ["A"] = 2 } };
        (bool, string, string) Make(string repo, string path, string branch) => (true, path, "");

        var (handed, _) = Autopilot.Next(Paced(snap, 5), settings, store, ["*"], 10, claim: true, Now, Make, _ => "main");
        Assert.That(handed.Select(h => h.Item.Id), Is.EquivalentTo(new[] { "A-T1", "A-T2", "B-T1" }));

        var (more, why) = Autopilot.Next(Paced(snap, 5), settings, store, ["A"], 10, claim: true, Now, Make, _ => "main");
        Assert.That((more.Count, why), Is.EqualTo((0, "A is at the cap you set (settings --repo-sessions or --repo-cap); nothing else is ready")), "its two are being worked");
    }

    [Test]
    public void Next_hands_nothing_more_to_a_repo_past_its_share_of_the_5_hour_limit()
    {
        // M.ORCH-T369: settings --repo-cap held only the dashboard's proposals; next --claim went on handing A out.
        var blockStart = Now.AddHours(-2);
        var spent = new TranscriptSession("s", @"D:\W\A", Now.AddMinutes(-5), default, null)
        {
            Events = [new UsageEvent(Now.AddMinutes(-30), new TokenUsage(0, 0, 0, 3_000_000))],
        };
        var snap = Snap(("A", [T("A-T1")]), ("B", [T("B-T1")])) is var s0
            ? s0 with
            {
                Repos = [s0.Repos[0] with { Sessions = [spent] }, s0.Repos[1]],
                Budget = new BudgetStatus(new UsageBlock(blockStart, blockStart.AddHours(5), default, Now), 10_000_000, false, 30, default, default, null, null, 0, null, 90),
            }
            : null!;
        var settings = new OrchestratorSettings { WorktreeRoot = @"D:\WT", RepoBudgetPercent = new() { ["A"] = 20 } };
        (bool, string, string) Make(string repo, string path, string branch) => (true, path, "");
        var plan = Scheduler.Plan(snap, settings, store.LoadAssignments()) with { Pace = new Pace(PaceMode.Open, 6, 10_000, 0, 1_000, "5-hour", Now.AddHours(3), "On pace.") { SpareSessions = 5 } };

        Assert.That(plan.OverRepoCap, Is.EquivalentTo(new[] { "A" }), "3M of a 10M limit is past A's 20%");
        var (handed, _) = Autopilot.Next(plan, settings, store, ["*"], 10, claim: false, Now, Make, _ => "main");
        Assert.That(handed.Select(h => h.Item.Id), Is.EqualTo(new[] { "B-T1" }));
    }

    [Test]
    public void The_ticket_the_calling_cli_was_started_on_is_not_handed_to_its_own_subagents()
    {
        // M.ORCH-T328: the calling CLI was exempt from every hold of its own, so the ticket its first prompt named went to a
        // subagent of its own while it worked it too. Only what it filed itself is its to hand out.
        var cli = new TranscriptSession("cli", @"D:\W\A", Now.AddMinutes(-1), default, null)
        {
            TicketId = "A-T1", Filing = [new FiledTicket("A-T2", null, Now.AddMinutes(-1))],
        };
        var snap = Snap(("A", [T("A-T1"), T("A-T2")])) is var s0 ? s0 with { Repos = [s0.Repos[0] with { Sessions = [cli] }] } : null!;
        (bool, string, string) Make(string repo, string path, string branch) => (true, path, "");

        var (handed, _) = Autopilot.Next(Paced(snap, 5), new() { WorktreeRoot = @"D:\WT" }, store, ["A"], 10, claim: false, Now, Make, _ => "main", self: "cli");

        Assert.That(handed.Select(h => h.Item.Id), Is.EqualTo(new[] { "A-T2" }));
    }

    [Test]
    public void A_stale_ticket_whose_background_agent_is_still_running_is_not_handed_out()
    {
        // M.ORCH-T255: a daemon background agent quiet past StaleAfterHours is still on its ticket while its process runs.
        store.UpdateAssignments(all => all.Add(new Assignment("A-T1", "A", @"D:\WT\A\A-T1", "Claude", Now.AddHours(-30), true) { Pilot = 4242 }));
        var plan = Paced(Snap(("A", [T("A-T1")])), 5);
        Assert.That(plan.Queue.Single().IsStale, Is.True);
        (bool, string, string) Make(string repo, string path, string branch) => (true, path, "");

        var (held, why) = Autopilot.Next(plan, new() { WorktreeRoot = @"D:\WT" }, store, ["A"], null, claim: true, Now, Make, _ => "main",
            pilotRunning: a => a.Pilot == 4242);
        Assert.That((held.Count, why), Is.EqualTo((0, "1 already being worked; nothing else is ready")));

        var (handed, _) = Autopilot.Next(plan, new() { WorktreeRoot = @"D:\WT" }, store, ["A"], null, claim: true, Now, Make, _ => "main",
            pilotRunning: _ => false);
        Assert.That(handed.Select(h => h.Item.Id), Is.EqualTo(new[] { "A-T1" }), "once its agent is gone, the stale ticket is back in the queue");
    }

    [Test]
    public void An_unthrottled_cli_is_handed_tickets_past_the_subagent_allowance_room_and_wind_down_but_not_past_the_stop()
    {
        // M.ORCH-T195: /unthrottle reaches `orchestrator next --claim` as it reaches the pace gate's holds.
        var snap = Snap(("A", [T("A-T1"), T("A-T2"), T("A-T3")]));
        var settings = new OrchestratorSettings { WorktreeRoot = @"D:\WT", MaxConcurrentSessions = 2 };
        (bool, string, string) Make(string repo, string path, string branch) => (true, path, "");
        var plan = Paced(snap, 5);
        // A-T1 costs more than is left, and past the wind-down line nothing new starts anyway.
        plan = plan with
        {
            Queue = [.. plan.Queue.Select(q => q.Id == "A-T1" ? q with { Estimate = new CostEstimate(5_000_000, 3, "fixes") } : q)],
            Pace = plan.Pace! with { SubagentAllowance = 2, Subagents = 2, RoomTokens = 1_000, WindDown = true },
        };

        var (held, why) = Autopilot.Next(plan, settings, store, ["A"], null, claim: false, Now, Make, _ => "main");
        Assert.That(held, Is.Empty, "a throttled CLI gets nothing past the wind-down line");
        Assert.That(why, Does.Contain("past 90%").And.Contain("no new ticket starts before it resets"));

        var (handed, whyNot) = Autopilot.Next(plan, settings, store, ["A"], null, claim: false, Now, Make, _ => "main", unthrottled: true);
        Assert.That((handed.Count, whyNot), Is.EqualTo((2, (string?)null)), "MaxConcurrentSessions of them, whatever the allowance, room or wind-down");
        Assert.That(Autopilot.Next(plan, settings, store, ["A"], 3, claim: false, Now, Make, _ => "main", unthrottled: true).Handed, Has.Count.EqualTo(3),
            "--count is honoured as asked");

        var stopped = plan with { Pace = plan.Pace! with { Stop = true } };
        Assert.That(Autopilot.Next(stopped, settings, store, ["A"], null, claim: false, Now, Make, _ => "main", unthrottled: true).Why,
            Does.Contain("at its target"), "the window's hard stop still applies");
    }

    [Test]
    public void With_pacing_off_next_subtracts_the_tickets_already_running_or_claimed_from_the_session_setting()
    {
        // M.ORCH-T549: unpaced, `next --claim` handed out MaxConcurrentSessions every call with nothing subtracted, so a CLI
        // calling it as each subagent finished stacked 4+4+... claims. Two claimed and one a live session works: 4 - 3 = 1.
        store.SaveAssignments([new Assignment("A-T1", "A", @"D:\WT\A\A-T1", "Claude", Now, true), new Assignment("A-T2", "A", @"D:\WT\A\A-T2", "Claude", Now, true)]);
        var worker = new TranscriptSession("w", @"D:\WT\A\A-T3", Now.AddMinutes(-1), default, null) { TicketId = "A-T3" };
        var snap = Snap(("A", [T("A-T1"), T("A-T2"), T("A-T3"), T("A-T4"), T("A-T5"), T("A-T6")])) is var s0
            ? s0 with { Repos = [s0.Repos[0] with { Sessions = [worker] }] } : null!;
        var settings = new OrchestratorSettings { WorktreeRoot = @"D:\WT", MaxConcurrentSessions = 4, Pacing = false };
        (bool, string, string) Make(string repo, string path, string branch) => (true, path, "");
        var plan = Scheduler.Plan(snap, settings, store.LoadAssignments());
        Assert.That(plan.Pace, Is.Null);

        var (handed, _) = Autopilot.Next(plan, settings, store, ["A"], null, claim: true, Now, Make, _ => "main", stillOpen: (_, _) => true, pilotRunning: _ => false);
        Assert.That(handed.Select(h => h.Item.Id), Is.EqualTo(new[] { "A-T4" }), "one place left of the four");

        var (none, why) = Autopilot.Next(Scheduler.Plan(snap, settings, store.LoadAssignments()), settings, store, ["A"], null, claim: true, Now, Make, _ => "main",
            stillOpen: (_, _) => true, pilotRunning: _ => false);
        Assert.That((none.Count, why), Is.EqualTo((0, "the pace has no room for another subagent right now; run it again in a few minutes")), "the next call: all four are taken");
        Assert.That(Autopilot.Next(plan, settings, store, ["A"], 2, claim: false, Now, Make, _ => "main", pilotRunning: _ => false).Handed, Has.Count.EqualTo(2),
            "an explicit --count is still honoured as asked");
    }

    [Test]
    public void Past_the_wind_down_line_a_cli_is_handed_no_ticket_even_one_whose_estimate_fits_or_with_a_count()
    {
        // M.ORCH-T254: `next --claim` handed out five tickets at 96.6% (past the 90% wind-down) as the reset neared and the
        // subagent allowance opened back up; each one's estimate fit what was left, but nothing new should start then.
        var snap = Snap(("A", [T("A-T1"), T("A-T2"), T("A-T3")]));
        var settings = new OrchestratorSettings { WorktreeRoot = @"D:\WT", MaxConcurrentSessions = 6 };
        var made = new List<string>();
        (bool, string, string) Make(string repo, string path, string branch) { made.Add(path); return (true, path, ""); }
        var plan = Paced(snap, 5);
        plan = plan with
        {
            Queue = [.. plan.Queue.Select(q => q with { Estimate = new CostEstimate(1_000, 3, "fixes") })],
            Pace = plan.Pace! with { SubagentAllowance = 6, Subagents = 0, RoomTokens = 1_000_000, WindDown = true },
        };

        var (handed, why) = Autopilot.Next(plan, settings, store, ["A"], null, claim: true, Now, Make, _ => "main");
        Assert.That(handed, Is.Empty, "every estimate fits and the allowance has room, but the window is winding down");
        Assert.That(why, Does.Contain("past 90%"));
        Assert.That(Autopilot.Next(plan, settings, store, ["A"], 5, claim: true, Now, Make, _ => "main").Handed, Is.Empty, "--count doesn't get past it");
        Assert.That((made.Count, store.LoadAssignments().Count), Is.EqualTo((0, 0)), "nothing claimed, no worktree made");

        var reset = plan with { Pace = plan.Pace! with { WindDown = false } };
        Assert.That(Autopilot.Next(reset, settings, store, ["A"], null, claim: false, Now, Make, _ => "main").Handed, Has.Count.EqualTo(3),
            "once the window resets they go out again");
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
        // /allocate typed in it: on for that CLI's own repo (a CLI at the workspace root has none).
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
    public void Next_judges_a_ticket_too_big_against_what_isnt_reserved_as_the_queue_does()
    {
        var snap = Snap(("A", [T("A-T1")]), ("B", [T("B-T1")])) with
        {
            Pace = new Pace(PaceMode.Open, 6, 10_000, 0, 1_000, "5-hour", Now.AddHours(3), "On pace.") { SpareSessions = 5, RoomTokens = 3_000 },
        };
        var settings = new OrchestratorSettings { WorktreeRoot = @"D:\WT", Reservations = new() { ["B-T1"] = 2_000 } };
        var plan = Scheduler.Plan(snap, settings, []);
        plan = plan with { Queue = [.. plan.Queue.Select(q => q.Id == "A-T1" ? q with { Estimate = new CostEstimate(1_500, 3, "fixes") } : q)] };
        (bool, string, string) Make(string repo, string path, string branch) => (true, path, "");

        var (handed, why) = Autopilot.Next(plan, settings, store, ["A"], null, claim: false, Now, Make, _ => "main");

        Assert.That(handed, Is.Empty, "1.5k fits the 3k left but not the 1k that B-T1's reservation leaves");
        Assert.That(why, Does.Contain("estimated past what's left"));
    }

    [Test]
    public void The_prompt_closes_the_ticket_only_after_the_push_lands_with_the_landed_hash()
    {
        // M.ORCH-T178: push and close chained together let a rejected push still close the ticket on a pre-rebase hash.
        var item = new QueueItem("A", @"D:\W\A", T("A-T1"), 0, null, false);
        var prompt = Autopilot.PromptFor(item, @"D:\WT\A\A-T1", "orch/a-t1", "master");

        var push = prompt.IndexOf("git push origin HEAD:master", StringComparison.Ordinal);
        var retry = prompt.IndexOf("If the push is rejected because master moved, fetch, rebase and push again", StringComparison.Ordinal);
        var onlyOnce = prompt.IndexOf("Only once the push has succeeded, as its own step", StringComparison.Ordinal);
        var close = prompt.IndexOf("workflow ticket close A-T1 --ref", StringComparison.Ordinal);
        Assert.That(new[] { push, retry, onlyOnce, close }, Has.All.GreaterThanOrEqualTo(0), prompt);
        Assert.That(push < retry && retry < onlyOnce && onlyOnce < close, "push, retry on a rejection, then close as its own step");
        Assert.That(prompt, Does.Contain("the commit hash as it landed on master"), "the ref is the landed hash, not the pre-rebase one");
        Assert.That(prompt, Does.Contain("If you can't finish or land it, leave the ticket open"));
    }

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

    [Test]
    public void A_landed_clean_worktree_whose_ticket_the_database_doesnt_have_is_tidied()
    {
        // M.ORCH-T368: worktrees of an old prefix (USS- before U.SPC-) were reported unlanded after every start, and kept.
        var origin = Path.Combine(dir, "origin.git");
        var repo = Path.Combine(dir, "A");
        Git(dir, "init", "-q", "--bare", "-b", "main", origin);
        Git(dir, "clone", "-q", origin, repo);
        Directory.CreateDirectory(Path.Combine(repo, "docs"));
        File.WriteAllText(Path.Combine(repo, "docs", "USER_STORIES.md"),
            "---\ncode: A\n---\n\n# A\n\n## Tickets\n\n| ID | Date | Type | Status | Summary | Ref |\n|---|---|---|---|---|---|\n| A-T1 | 2026-10-01 | fix | open | x | — |\n");
        Git(repo, "add", ".");
        Git(repo, "-c", "user.email=t@t", "-c", "user.name=t", "commit", "-qm", "init");
        Git(repo, "push", "-q", "origin", "HEAD:main");
        try
        {
            using (var db = MindAttic.Workflow.Data.WorkflowDb.Open(MindAttic.Workflow.Data.WorkflowDb.DefaultPath(dir)))
                db.Import([new MindAttic.Workflow.Repo("A", repo)]);
            var tree = Path.Combine(dir, "wt", "OLD-T9");
            Assert.That(Dispatcher.EnsureWorktree(repo, tree, "orch/old-t9").Ok);

            var (closed, note) = Autopilot.LandOnMain(new Assignment("OLD-T9", "A", tree, "Claude", Now, true), repo);

            Assert.That(closed, Is.True, note);
            Assert.That(Directory.Exists(tree), Is.False, "tidied");
            var open = Path.Combine(dir, "wt", "A-T1");
            Assert.That(Dispatcher.EnsureWorktree(repo, open, "orch/a-t1").Ok);
            Assert.That(Autopilot.LandOnMain(new Assignment("A-T1", "A", open, "Claude", Now, true), repo).Closed, Is.False, "a known open ticket isn't");
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); }
    }

    [Test]
    public void A_repo_the_database_doesnt_hold_has_its_tickets_read_from_its_user_stories()
    {
        // M.ORCH-T551: once the Workflow db existed, every ticket of a repo still on USER_STORIES.md read as closed, so Reap
        // called an open ticket finished, removed its worktree and dropped its assignment, then handed it out again.
        const string head = "---\ncode: {0}\n---\n\n# {0}\n\n## Tickets\n\n| ID | Date | Type | Status | Summary | Ref |\n|---|---|---|---|---|---|\n";
        string Repo(string name, string status)
        {
            var origin = Path.Combine(dir, name + ".git");
            var repo = Path.Combine(dir, name);
            Git(dir, "init", "-q", "--bare", "-b", "main", origin);
            Git(dir, "clone", "-q", origin, repo);
            Directory.CreateDirectory(Path.Combine(repo, "docs"));
            File.WriteAllText(Path.Combine(repo, "docs", "USER_STORIES.md"), string.Format(head, name) + $"| {name}-T1 | 2026-10-01 | fix | {status} | x | — |\n");
            Git(repo, "add", ".");
            Git(repo, "-c", "user.email=t@t", "-c", "user.name=t", "commit", "-qm", "init");
            Git(repo, "push", "-q", "origin", "HEAD:main");
            return repo;
        }
        var inDb = Repo("B", "open");
        var a = Repo("A", "open");
        try
        {
            using (var db = MindAttic.Workflow.Data.WorkflowDb.Open(MindAttic.Workflow.Data.WorkflowDb.DefaultPath(dir)))
                db.Import([new MindAttic.Workflow.Repo("B", inDb)]); // A isn't migrated yet
            var tree = Path.Combine(dir, "wt", "A-T1");
            Assert.That(Dispatcher.EnsureWorktree(a, tree, "orch/a-t1").Ok);
            var assignment = new Assignment("A-T1", "A", tree, "Claude", Now, true);

            var (closed, note) = Autopilot.LandOnMain(assignment, a);
            Assert.That(closed, Is.False, note);
            Assert.That(Directory.Exists(tree), Is.True, "an open ticket's worktree is kept");

            File.WriteAllText(Path.Combine(tree, "docs", "USER_STORIES.md"), string.Format(head, "A") + "| A-T1 | 2026-10-01 | fix | closed | x | the test |\n");
            Git(tree, "-c", "user.email=t@t", "-c", "user.name=t", "commit", "-qam", "close A-T1");
            Git(tree, "push", "-q", "origin", "HEAD:main");
            Assert.That(Autopilot.LandOnMain(assignment, a).Closed, Is.True, "closed in its USER_STORIES.md on main: finished");
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); }
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
        Assert.That(Providers.IsClaudeProcess("claude"), Is.True);
        Assert.That(Providers.IsClaudeProcess("claude.exe.old.1791946834302.10372"), Is.True, "found live: the updater renames the running binary");
        Assert.That(Providers.IsClaudeProcess("claudia"), Is.False);
    }
}
