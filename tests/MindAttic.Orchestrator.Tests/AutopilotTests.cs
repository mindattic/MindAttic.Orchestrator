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
        order = [];
    }

    [TearDown]
    public void TearDown() => Directory.Delete(dir, recursive: true);

    private Autopilot Pilot(DispatchLease? lease = null) => new(store, sessions, lease)
    {
        Order = () => order,
        Alive = clis.Contains,
        IsRunning = agents.Contains,
        LogDirectory = Path.Combine(dir, "logs"),
        Start = (d, prompt, log) =>
        {
            var pid = nextPid++;
            agents.Add(pid);
            started.Add((d, prompt, log, pid));
            return pid;
        },
    };

    /// <summary>An open Claude CLI (process <paramref name="pid"/>) in <paramref name="cwd"/>.</summary>
    private void Cli(int pid, string cwd)
    {
        clis.Add(pid);
        File.WriteAllText(Path.Combine(sessions.Directory, "s" + pid + ".state.json"),
            $$"""{"state":"idle","at":"{{Now:yyyy-MM-ddTHH:mm:ssZ}}","pid":{{pid}},"cwd":"{{cwd.Replace(@"\", @"\\")}}"}""");
    }

    private static Ticket T(string id) => new(id, "2026-10-01", "fix", "open", $"summary of {id}", "");

    private static Snapshot Snap(params (string Name, Ticket[] Open)[] repos) =>
        new(Now, [.. repos.Select(r => new RepoStatus(r.Name, $@"D:\W\{r.Name}", RepoState.Dormant, [], [], r.Open, null, default))], []);

    private SchedulePlan Plan(Snapshot snap, int cap = 6) => Scheduler.Plan(snap, new() { MaxConcurrentSessions = cap }, store.LoadAssignments());

    [Test]
    public void Each_repo_with_an_open_cli_gets_one_background_agent_on_its_next_story_and_nothing_is_typed()
    {
        Cli(100, @"D:\W\A");
        Cli(200, @"D:\W\B\src");
        Cli(300, @"D:\W"); // the workspace root: no repo of its own
        var snap = Snap(("A", [T("A-T1"), T("A-T2")]), ("B", [T("B-T1")]), ("C", [T("C-T1")]));
        var pilot = Pilot();

        var said = pilot.Tick(Plan(snap), snap, new(), Now);
        Assert.That(started.Select(s => (s.Dir, s.Prompt[..19])), Is.EquivalentTo(new[] { (@"D:\W\A", "Work on ticket A-T1"), (@"D:\W\B", "Work on ticket B-T1") }),
            "C has no CLI open, and A gets one agent, not two");
        Assert.That(started[0].Prompt, Does.Contain("/commit").And.Contains("workflow ticket close"));
        Assert.That(started[0].Log, Does.StartWith(Path.Combine(dir, "logs")).And.Contains("T1-"));
        Assert.That(said, Has.Count.EqualTo(2).And.All.Contains("in the background"));
        var a = store.LoadAssignments().Single(x => x.TicketId == "A-T1");
        Assert.That((a.Pilot, a.Log, a.Worktree), Is.EqualTo(((int?)started.Single(s => s.Dir.EndsWith('A')).Pid, started.Single(s => s.Dir.EndsWith('A')).Log, false)));
        Assert.That(pilot.Working.Select(w => w.TicketId), Is.EquivalentTo(new[] { "A-T1", "B-T1" }));

        pilot.Tick(Plan(snap), snap, new(), Now.AddMinutes(1));
        Assert.That(started, Has.Count.EqualTo(2), "still working: no second agent in A");
    }

    [Test]
    public void A_finished_agent_makes_way_for_the_next_story_and_one_that_left_its_ticket_open_gives_it_back()
    {
        Cli(100, @"D:\W\A");
        Cli(200, @"D:\W\B");
        var snap = Snap(("A", [T("A-T1"), T("A-T2")]), ("B", [T("B-T1")]));
        var pilot = Pilot();
        pilot.Tick(Plan(snap), snap, new(), Now);
        agents.Clear(); // both agents exit: A closed A-T1, B didn't close B-T1

        var after = Snap(("A", [T("A-T2")]), ("B", [T("B-T1")]));
        var said = pilot.Tick(Plan(after), after, new(), Now.AddMinutes(30));
        Assert.That(said, Has.Some.Contains("finished A-T1"));
        Assert.That(said, Has.Some.Contains("needs you: B-T1's agent in B ended without closing it"));
        pilot.Tick(Plan(after), after, new(), Now.AddMinutes(31));
        Assert.That(started.Skip(2).Select(s => s.Prompt[..19]), Is.EqualTo(new[] { "Work on ticket A-T2" }),
            "A moves on; B-T1 is back in the queue for you, not retried in a loop");
        Assert.That(store.LoadAssignments().Any(x => x.TicketId == "B-T1"), Is.False);
    }

    [Test]
    public void No_more_agents_at_once_than_the_pace_allows()
    {
        foreach (var (pid, name) in new[] { (1, "A"), (2, "B"), (3, "C") }) Cli(pid, $@"D:\W\{name}");
        var snap = Snap(("A", [T("A-T1")]), ("B", [T("B-T1")]), ("C", [T("C-T1")]));

        var said = Pilot().Tick(Plan(snap, cap: 2), snap, new(), Now);
        Assert.That(started, Has.Count.EqualTo(2));
        Assert.That(said, Has.Some.Contains("all the pace allows"));
    }

    [Test]
    public void Repo_flag_works_named_repos_even_with_no_cli_open_and_off_starts_nothing()
    {
        var snap = Snap(("A", [T("A-T1")]), ("B", [T("B-T1")]));
        order = ["b", "Nope"];
        var said = Pilot().Tick(Plan(snap), snap, new(), Now);
        Assert.That(started.Single().Dir, Is.EqualTo(@"D:\W\B"));
        Assert.That(said, Has.Some.Contains("no repo named Nope"));

        order = null;
        Cli(100, @"D:\W\A");
        var off = Pilot();
        off.Tick(Plan(snap), snap, new(), Now);
        Assert.That((off.On, started.Count), Is.EqualTo((false, 1)));
    }

    [Test]
    public void A_repo_with_a_story_going_in_a_tab_waits_and_the_pace_or_switch_holds_everything()
    {
        Cli(100, @"D:\W\A");
        var snap = Snap(("A", [T("A-T1"), T("A-T2")]));
        store.SaveAssignments([new Assignment("A-T1", "A", @"D:\W\A", "Claude", Now, false)]);
        Assert.That(Pilot().Tick(Plan(snap), snap, new(), Now).Single(), Does.Contain("A-T1 going in a session"));
        Assert.That(started, Is.Empty, "never two agents in one checkout");

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
    public void A_restart_adopts_agents_still_working_instead_of_starting_them_again()
    {
        Cli(100, @"D:\W\A");
        var snap = Snap(("A", [T("A-T1"), T("A-T2")]));
        Pilot().Tick(Plan(snap), snap, new(), Now);

        var restarted = Pilot(); // a new daemon: same store, the agent's process still running
        Assert.That(restarted.Tick(Plan(snap), snap, new(), Now.AddMinutes(5)).Single(), Does.Contain("A is working on A-T1"));
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
