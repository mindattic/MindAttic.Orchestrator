using System.Text.Json;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using MindAttic.Tickets.Log;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// Allocate's holds and parking (M.ORCH-T135): an agent halted by the window's stop isn't parked, a parked story is
/// never retried, a lease held elsewhere starts and reaps nothing, which hold <see cref="Allocate.Paused"/> names first,
/// what the statusline's waiting file holds, a repo's session cap is told once, and a worktree failure skips only that repo.
/// </summary>
public sealed class AllocateHoldsTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 6, 0, 0, DateTimeKind.Utc);
    private string dir = "";
    private JsonStore store = null!;
    private ContextCycler sessions = null!;
    private readonly List<(string Dir, string Prompt, int Pid)> started = [];
    private readonly HashSet<int> agents = [];
    private readonly HashSet<string> landed = new(StringComparer.Ordinal);
    private readonly List<string> landCalls = [];
    private readonly List<string> worktreeCalls = [];
    private Func<string, bool> worktreeFails = _ => false;
    private IReadOnlyList<string>? order = [];
    private int nextPid = 7000;

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "orch-holds-" + Guid.NewGuid().ToString("N"));
        store = new JsonStore(Path.Combine(dir, "store"));
        sessions = new ContextCycler(Path.Combine(dir, "ctx"));
        Directory.CreateDirectory(sessions.Directory);
        started.Clear();
        agents.Clear();
        landed.Clear();
        landCalls.Clear();
        worktreeCalls.Clear();
        worktreeFails = _ => false;
        nextPid = 7000;
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(dir, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    [Test]
    public void An_agent_halted_by_the_windows_stop_is_not_parked_and_starts_again_after_the_reset()
    {
        order = ["A"];
        var snap = Snap(("A", [T("A-T1")]));
        var pilot = Pilot();
        pilot.Tick(Paced(snap, 1), snap, Settings(), Now);
        Assert.That(started.Select(s => s.Prompt[..19]), Is.EqualTo(new[] { "Work on ticket A-T1" }));

        agents.Clear(); // the pace gate stopped it at the target: it exited with its ticket still open
        var stopped = Plan(snap) with { Pace = new Pace(PaceMode.Over, 0, 0, 0, null, "5-hour", Now.AddHours(1), "at the 5-hour target") { Stop = true } };
        var said = pilot.Tick(stopped, snap, Settings(), Now.AddMinutes(30));
        Assert.That(said, Has.Some.Contains("A-T1's agent in A stopped at the window's target; it starts again after the reset"));
        Assert.That(said.Select(Allocate.NeedsYou), Has.All.Null, "a stop isn't a failure: nothing needs you");
        Assert.That(pilot.Paused, Does.StartWith("pace:"));
        Assert.That(store.LoadAssignments(), Is.Empty, "reaped, so it's back in the queue");
        Assert.That(started, Has.Count.EqualTo(1), "nothing starts while the window is stopped");

        pilot.Tick(Paced(snap, 1), snap, Settings(), Now.AddHours(1).AddMinutes(1));
        Assert.That(started.Select(s => s.Prompt[..19]), Is.EqualTo(new[] { "Work on ticket A-T1", "Work on ticket A-T1" }),
            "after the reset it starts again, not parked");
    }

    [Test]
    public void A_parked_story_is_never_retried_while_the_others_carry_on()
    {
        order = ["A"];
        var snap = Snap(("A", [T("A-T1"), T("A-T2")]));
        var pilot = Pilot();
        pilot.Tick(Paced(snap, 1), snap, Settings(), Now);
        agents.Clear(); // A-T2's agent (the newest first, M.ORCH-T863) exits without closing it, with the window open

        var said = pilot.Tick(Paced(snap, 1), snap, Settings(), Now.AddMinutes(30));
        Assert.That(said.Select(Allocate.NeedsYou).OfType<string>().Single(), Does.StartWith("A-T2's agent in A ended without finishing it"));
        Assert.That(started.Select(s => s.Prompt[..19]), Is.EqualTo(new[] { "Work on ticket A-T2", "Work on ticket A-T1" }), "A moves on to A-T1");

        agents.Clear(); // A-T1 finishes
        landed.Add("A-T1");
        var after = Snap(("A", [T("A-T2")]));
        for (var i = 1; i <= 4; i++)
        {
            var later = pilot.Tick(Paced(after, 10), after, Settings(), Now.AddHours(i));
            if (i == 1) Assert.That(later, Has.Some.Contains("A's open stories are blocked, parked or too big"));
            else Assert.That(later, Has.None.Contains("parked"), "said once, not every tick");
        }
        Assert.That(started, Has.Count.EqualTo(2), "A-T2 stays parked however much room there is");
        Assert.That(started.Count(s => s.Prompt.StartsWith("Work on ticket A-T2", StringComparison.Ordinal)), Is.EqualTo(1));
    }

    [Test]
    public void With_the_lease_held_elsewhere_nothing_is_started_reaped_or_saved()
    {
        order = ["A"];
        var snap = Snap(("A", [T("A-T1"), T("A-T2")]));
        var gone = new Assignment("A-T1", "A", @"D:\WT\A\A-T1", "Claude", Now.AddHours(-1), true) { Pilot = 4242, Log = "a-t1.log" };
        store.SaveAssignments([gone]); // an agent that has exited: reaping it is the lease holder's job
        var leases = Path.Combine(dir, "lease");
        using var daemon = new DispatchLease(leases);
        Assert.That(daemon.TryAcquire(), Is.True);
        using var mine = new DispatchLease(leases);
        var waiting = Path.Combine(dir, "waiting.json");
        var pilot = Pilot(mine, waiting);

        var said = pilot.Tick(Paced(snap, 10), snap, Settings(), Now);

        Assert.That(pilot.HeldElsewhere, Is.True);
        Assert.That(said, Is.Empty);
        Assert.That(started, Is.Empty, "the daemon starts them");
        Assert.That(landCalls, Is.Empty, "and reaps them");
        Assert.That(store.LoadAssignments().Select(a => a.TicketId), Is.EqualTo(new[] { "A-T1" }), "its assignment is left for the holder");
        Assert.That(File.Exists(waiting), Is.False, "the holder writes the statusline's file");
        Assert.That(mine.IsHeld, Is.False);

        daemon.Dispose();
        pilot.Tick(Paced(snap, 10), snap, Settings(), Now.AddMinutes(1));
        Assert.That((pilot.HeldElsewhere, landCalls.Count, started.Count), Is.EqualTo((false, 1, 1)),
            "once free, it reaps A-T1 (parked: it ended unfinished) and starts A-T2");
    }

    [Test]
    public void With_allocate_off_here_the_lease_still_decides_who_reaps_and_sweeps()
    {
        // CLIs run /allocate now, so the order is unset in the daemon and the dashboard alike: only the lease holder may
        // reap and run the hourly sweep's git (the dashboard ran it on its UI thread beside the daemon).
        order = null;
        var snap = Snap(("A", [T("A-T1")]));
        var leases = Path.Combine(dir, "lease");
        using var daemon = new DispatchLease(leases);
        Assert.That(daemon.TryAcquire(), Is.True);
        using var mine = new DispatchLease(leases);
        var pilot = Pilot(mine);

        pilot.Tick(Paced(snap, 10), snap, Settings(), Now);
        Assert.That((pilot.On, pilot.HeldElsewhere), Is.EqualTo((false, true)));

        daemon.Dispose();
        pilot.Tick(Paced(snap, 10), snap, Settings(), Now.AddMinutes(1));
        Assert.That((pilot.HeldElsewhere, mine.IsHeld), Is.EqualTo((false, true)), "free: this process takes it and does the work");
    }

    [Test]
    public void Paused_names_switched_off_then_machine_health_then_quiet_hours_then_the_pace()
    {
        order = ["A"];
        var snap = Snap(("A", [T("A-T1")]));
        var local = TimeOnly.FromDateTime(Now.ToLocalTime());
        var quiet = $"{local.AddMinutes(-30):HH\\:mm}-{local.AddMinutes(30):HH\\:mm}";
        var coast = Plan(snap) with { Pace = new Pace(PaceMode.Coast, 1, 0, 0, null, null, null, "no room for another session") };
        var pilot = Pilot();
        pilot.HealthHold = "handles low";

        pilot.Tick(coast, snap, Settings() with { Allocate = false, QuietHours = [quiet] }, Now);
        Assert.That(pilot.Paused, Is.EqualTo("allocate is switched off"));

        pilot.Tick(coast, snap, Settings() with { QuietHours = [quiet] }, Now);
        Assert.That(pilot.Paused, Is.EqualTo("machine health: handles low"));

        pilot.HealthHold = null;
        pilot.Tick(coast, snap, Settings() with { QuietHours = [quiet] }, Now);
        Assert.That(pilot.Paused, Is.EqualTo($"quiet hours ({quiet})"));

        pilot.Tick(coast, snap, Settings(), Now);
        Assert.That(pilot.Paused, Is.EqualTo("pace: no room for another session"));
        Assert.That(started, Is.Empty, "every one of them holds new agents");

        pilot.Tick(Paced(snap, 1), snap, Settings(), Now);
        Assert.That((pilot.Paused, started.Count), Is.EqualTo(((string?)null, 1)), "with none of them, it starts");
    }

    [Test]
    public void The_waiting_file_holds_the_repos_waiting_on_the_pace_and_those_left_alone_and_why()
    {
        order = ["A", "B", "C"];
        var snap = Snap(("A", [T("A-T1")]), ("B", [T("B-T1")]), ("C", [T("C-T1")]));
        var waiting = Path.Combine(dir, "waiting.json");
        var pilot = Pilot(waiting: waiting);
        var coast = Plan(snap) with { Pace = new Pace(PaceMode.Coast, 1, 0, 0, null, null, null, "no room") };

        pilot.Tick(coast, snap, Settings() with { AllocateSkip = ["c"] }, Now);

        using (var json = JsonDocument.Parse(File.ReadAllText(waiting)))
        {
            var root = json.RootElement;
            Assert.That(root.GetProperty("repos").EnumerateArray().Select(e => e.GetString()), Is.EqualTo(new[] { "A", "B" }));
            Assert.That(root.GetProperty("left_alone").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()),
                Is.EqualTo(new Dictionary<string, string?> { ["C"] = "you set it to be skipped (settings --allocate-skip)" }));
            Assert.That(root.GetProperty("at").GetString(), Is.EqualTo("2026-10-15T06:00:00Z"));
        }
        Assert.That(Directory.GetFiles(dir, "waiting.json.*.tmp"), Is.Empty, "written whole, then moved into place");

        File.Delete(waiting);
        pilot.Tick(coast, snap, Settings(), Now.AddMinutes(1), dryRun: true);
        Assert.That(File.Exists(waiting), Is.False, "a dry run writes nothing");

        pilot.Tick(Plan(snap), snap, Settings() with { Allocate = false }, Now.AddMinutes(2));
        using var off = JsonDocument.Parse(File.ReadAllText(waiting));
        Assert.That(off.RootElement.GetProperty("repos").GetArrayLength(), Is.Zero, "switched off isn't waiting on the pace");
    }

    [Test]
    public void A_repos_session_cap_is_told_once_and_other_repos_still_start()
    {
        order = ["A", "B"];
        var snap = Snap(("A", [T("A-T1"), T("A-T2"), T("A-T3")]), ("B", [T("B-T1")]));
        var capped = Settings() with { RepoMaxSessions = new() { ["A"] = 1 } };
        var pilot = Pilot();
        const string cap = "allocate: A has 1 agents working, the cap you set for it (settings --repo-sessions)";

        var first = pilot.Tick(Paced(snap, 10), snap, capped, Now);
        Assert.That(started.Select(s => s.Dir), Is.EquivalentTo(new[] { @"D:\WT\A\A-T3", @"D:\WT\B\B-T1" }), "A's newest (M.ORCH-T863)");
        Assert.That(first.Count(l => l == cap), Is.EqualTo(1), "A-T2 and A-T1 both hit the cap; it's said once");

        var second = pilot.Tick(Paced(snap, 10), snap, capped, Now.AddMinutes(20));
        Assert.That(second, Has.None.EqualTo(cap), "nor again on the next tick while it's unchanged");
        Assert.That(started, Has.Count.EqualTo(2));
    }

    [Test]
    public void A_worktree_failure_skips_only_that_repo_and_only_for_the_tick()
    {
        order = ["A", "B"];
        var snap = Snap(("A", [T("A-T1"), T("A-T2")]), ("B", [T("B-T1")]));
        worktreeFails = repo => repo == @"D:\W\A";
        var pilot = Pilot();

        var said = pilot.Tick(Paced(snap, 10), snap, Settings(), Now);
        Assert.That(started.Select(s => s.Dir), Is.EqualTo(new[] { @"D:\WT\B\B-T1" }), "B starts despite A's failure");
        Assert.That(worktreeCalls.Count(p => p.StartsWith(@"D:\WT\A\", StringComparison.Ordinal)), Is.EqualTo(1), "A-T1 isn't tried once A-T2 (the newest) failed");
        Assert.That(said, Has.Some.EqualTo("allocate: couldn't make a worktree for A-T2: disk full"));
        Assert.That(said, Has.None.Contains("A has no open stories").And.None.Contains("A's open stories"), "a failure isn't reported as nothing to do");
        Assert.That(store.LoadAssignments().Select(a => a.TicketId), Is.EqualTo(new[] { "B-T1" }), "nothing is claimed for A");

        worktreeFails = _ => false;
        pilot.Tick(Paced(snap, 10), snap, Settings(), Now.AddMinutes(20));
        Assert.That(started.Select(s => s.Dir), Is.EquivalentTo(new[] { @"D:\WT\B\B-T1", @"D:\WT\A\A-T1", @"D:\WT\A\A-T2" }),
            "the next tick tries A again");
    }

    private Allocate Pilot(DispatchLease? lease = null, string? waiting = null) => new(store, sessions, lease)
    {
        WaitingPath = waiting,
        Order = () => order,
        Alive = _ => false,
        IsRunning = a => agents.Contains(a.Pilot!.Value),
        StartTimeOf = _ => null,
        LogDirectory = Path.Combine(dir, "logs"),
        MainlineOf = _ => "main",
        MakeWorktree = (repo, path, branch) =>
        {
            worktreeCalls.Add(path);
            return worktreeFails(repo) ? (false, "", "disk full") : (true, path, "");
        },
        Land = (a, repo) => { landCalls.Add(a.TicketId); return (landed.Contains(a.TicketId), ""); },
        Start = (d, prompt, log) =>
        {
            var pid = nextPid++;
            agents.Add(pid);
            started.Add((d, prompt, pid));
            return pid;
        },
    };

    private static OrchestratorSettings Settings() => new() { WorktreeRoot = @"D:\WT" };

    private static Ticket T(string id) => new(id, "2026-10-01", "fix", "open", $"summary of {id}", "");

    private static Snapshot Snap(params (string Name, Ticket[] Open)[] repos) =>
        new(Now, [.. repos.Select(r => new RepoStatus(r.Name, $@"D:\W\{r.Name}", RepoState.Dormant, [], [], r.Open, null, default))], []);

    private SchedulePlan Plan(Snapshot snap) => Scheduler.Plan(snap, new() { MaxConcurrentSessions = 6 }, store.LoadAssignments());

    /// <summary>A plan whose pace has room for <paramref name="spare"/> more sessions.</summary>
    private SchedulePlan Paced(Snapshot snap, int spare) =>
        Plan(snap) with { Pace = new Pace(PaceMode.Open, 6, 10_000, 0, 1_000, "5-hour", Now.AddHours(3), "On pace.") { SpareSessions = spare } };
}
