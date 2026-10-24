using System.Text.Json;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using MindAttic.Tickets.Log;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>An idle /allocate CLI is sent back for work within a tick of next having a ticket and room for it (M.ORCH-T523).</summary>
public sealed class AllocateWakerTests
{
    private string dir = "";
    private ContextCycler cycler = null!;
    private JsonStore store = null!;
    private readonly List<(string Pipe, string Text)> typed = [];
    private static readonly DateTime Now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly HostSession Tab = new("Claude", 900, "mindattic-host-claude-900", @"D:\W\A");
    private static readonly OrchestratorSettings Settings = new() { WorktreeRoot = @"D:\WT" };
    private const int Cli = 42;

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "pilot-waker-" + Guid.NewGuid().ToString("N"));
        cycler = new ContextCycler(Path.Combine(dir, "session-context"));
        store = new JsonStore(Path.Combine(dir, "store"));
        Directory.CreateDirectory(cycler.Directory);
        typed.Clear();
    }

    [TearDown]
    public void TearDown() => Directory.Delete(dir, recursive: true);

    private AllocateWaker Waker(bool inTab = true) => new(cycler, store)
    {
        IsAlive = _ => true,
        Pilots = () => new Dictionary<int, IReadOnlyList<string>> { [Cli] = ["A"] },
        HostOf = (pid, hosts) => inTab && pid == Cli ? hosts.FirstOrDefault() : null,
        Submit = (pipe, text) => { lock (typed) typed.Add((pipe, text)); return Task.FromResult(true); },
    };

    private void State(string state, DateTime at, bool limitStop = false, string? goal = null)
    {
        var body = new Dictionary<string, object?> { ["state"] = state, ["at"] = at.ToString("yyyy-MM-ddTHH:mm:ssZ"), ["pid"] = Cli, ["cwd"] = @"D:\W\A" };
        if (limitStop) body["limit_stop"] = new Dictionary<string, object> { ["until"] = new DateTimeOffset(Now.AddHours(1)).ToUnixTimeSeconds(), ["window"] = "5-hour" };
        if (goal is not null) body["goal"] = goal;
        File.WriteAllText(Path.Combine(cycler.Directory, "s1.state.json"), JsonSerializer.Serialize(body));
    }

    private static Ticket T(string id) => new(id, "2026-10-01", "fix", "open", $"summary of {id}", "");

    /// <summary>A plan whose pace allows <paramref name="allowance"/> subagents, <paramref name="running"/> of them running.</summary>
    private static SchedulePlan Plan(int allowance, int running, params Ticket[] open) =>
        Scheduler.Plan(new Snapshot(Now, [new RepoStatus("A", @"D:\W\A", RepoState.Dormant, [], [], open, null, default)], []), Settings, []) with
        {
            Pace = new Pace(PaceMode.Open, 6, 10_000, 0, 1_000, "5-hour", Now.AddHours(3), "On pace.") { SubagentAllowance = allowance, Subagents = running },
        };

    [Test]
    public void An_idle_allocate_cli_let_stop_with_no_room_is_typed_back_to_work_once_room_opens()
    {
        // Found live 2026-10-20: every /allocate CLI stopped the night before on "the pace has no room", and none was sent back
        // when the window reset; MindAttic.Tickets' sat idle with M.TIX-T116 workable until the user typed to it.
        State("idle", Now.AddHours(-6));
        var waker = Waker();

        Assert.That(waker.Tick(Plan(4, 4, T("A-T1")), Settings, [Tab], type: true, Now), Is.Empty, "no room yet: next would hand out nothing");
        Assert.That(typed, Is.Empty);

        var w = waker.Tick(Plan(4, 0, T("A-T1")), Settings, [Tab], type: true, Now.AddMinutes(1)).Single();
        Assert.That(w.Typed, Is.True);
        Assert.That(w.Tickets, Is.EqualTo(new[] { "A-T1" }));
        Assert.That(typed.Single().Pipe, Is.EqualTo(Tab.PipeName));
        Assert.That(typed.Single().Text, Does.Contain("orchestrator next --repos 'A' --claim").And.Contain("A-T1").And.Not.Contain("\n").And.Not.Contain("\""),
            "one line, nothing wt or the prompt box would split");
        Assert.That(store.LoadAssignments(), Is.Empty, "a dry run: nothing is claimed for it");

        Assert.That(waker.Tick(Plan(4, 0, T("A-T1")), Settings, [Tab], true, Now.AddMinutes(2)), Is.Empty, "not again inside the cooldown");
        State("idle", Now.AddMinutes(5)); // it stopped again
        Assert.That(waker.Tick(Plan(4, 0, T("A-T1")), Settings, [Tab], true, Now.AddMinutes(1) + AllocateWaker.Cooldown), Has.Count.EqualTo(1),
            "once the cooldown is past it's asked again");
    }

    [Test]
    public void A_cli_on_its_own_login_is_judged_by_that_accounts_pace_not_the_global_logins()
    {
        // Found live 2026-10-22 (M.ORCH-T640): the nudge said "room to work again" three times while that CLI's own
        // `orchestrator next --claim`, on its login's pace, answered "the pace has no room for another subagent".
        State("idle", Now.AddHours(-6));
        var root = Path.Combine(dir, "accounts");
        var login = Path.Combine(root, "me@example.com");
        var globalPlan = Plan(4, 0, T("A-T1")); // the global login has room
        var accountPace = globalPlan.Pace! with { SubagentAllowance = 2, Subagents = 2 }; // this account's has none
        var snapshot = new Snapshot(Now, [new RepoStatus("A", @"D:\W\A", RepoState.Dormant, [], [], [T("A-T1")], null, default)], [])
        {
            Accounts = [new AccountPace("me@example.com", login, null, default!, accountPace, null, 1)],
        };
        var onOwn = new AllocateWaker(cycler, store)
        {
            IsAlive = _ => true,
            Pilots = () => new Dictionary<int, IReadOnlyList<string>> { [Cli] = ["A"] },
            HostOf = (pid, hosts) => hosts.FirstOrDefault(),
            Submit = (pipe, text) => { lock (typed) typed.Add((pipe, text)); return Task.FromResult(true); },
            Dirs = new AccountDirs(root),
            OwnLogins = () => new Dictionary<string, string> { ["s1"] = login },
        };

        Assert.That(onOwn.Tick(globalPlan, Settings, [Tab], type: true, Now, snapshot), Is.Empty, "its own account has no room: next --claim would hand out nothing");
        Assert.That(typed, Is.Empty);
        Assert.That(onOwn.Tick(globalPlan, Settings, [Tab], type: true, Now), Has.Count.EqualTo(1), "without a snapshot it's the plan it was given");
    }

    [Test]
    public void A_busy_just_stopped_window_held_or_goal_cli_or_one_with_nothing_workable_is_left_alone()
    {
        var plan = Plan(4, 0, T("A-T1"));
        State("busy", Now.AddHours(-1));
        Assert.That(Waker().Tick(plan, Settings, [Tab], true, Now), Is.Empty, "mid-turn");
        State("idle", Now.AddSeconds(-30));
        Assert.That(Waker().Tick(plan, Settings, [Tab], true, Now), Is.Empty, "just stopped: keep-going had its say");
        State("idle", Now.AddHours(-1), limitStop: true);
        Assert.That(Waker().Tick(plan, Settings, [Tab], true, Now), Is.Empty, "waiting on a window: LimitWaker's to wake");
        State("idle", Now.AddHours(-1), goal: "tests pass");
        Assert.That(Waker().Tick(plan, Settings, [Tab], true, Now), Is.Empty, "a /goal runs its own turns");

        State("idle", Now.AddHours(-1));
        Assert.That(Waker().Tick(Plan(4, 0), Settings, [Tab], true, Now), Is.Empty, "nothing workable");
        Assert.That(Waker().Tick(plan with { Stop = true }, Settings, [Tab], true, Now), Is.Empty, "at the window's stop");
        Assert.That(Waker().Tick(plan, Settings with { KeepGoing = false }, [Tab], true, Now), Is.Empty, "keep-going off");
        Assert.That(typed, Is.Empty);
    }

    /// <summary>A window on course for <paramref name="projected"/>% of 98%, with A-T1 and A-T2 claimed for a CLI's subagents
    /// <paramref name="claimedAgo"/> ago (worktrees under D:\WT\A), and the pace allowing <paramref name="allowance"/> subagents.</summary>
    private static (Snapshot Snap, SchedulePlan Plan) Claimed(double projected, int allowance, int running, TimeSpan claimedAgo,
        IReadOnlyList<TranscriptSession>? sessions = null)
    {
        var snap = new Snapshot(Now, [new RepoStatus("A", @"D:\W\A", RepoState.Dormant, [], sessions ?? [], [T("A-T1"), T("A-T2")], null, default)], [])
        {
            Utilization = new Utilization(Now.AddHours(-2), Now.AddHours(3), Now, 98, [], 30, 7, projected, TimeSpan.Zero),
        };
        var claims = new[] { "A-T1", "A-T2" }.Select(id => new Assignment(id, "A", $@"D:\WT\A\{id}", Providers.Claude, Now - claimedAgo, true)).ToList();
        var plan = Scheduler.Plan(snap, Settings, claims) with
        {
            Pace = new Pace(PaceMode.Open, 6, 10_000, 0, 1_000, "5-hour", Now.AddHours(3), "On pace.") { SubagentAllowance = allowance, Subagents = running },
        };
        return (snap, plan);
    }

    [Test]
    public void An_idle_claim_under_target_with_room_gets_one_wake_to_start_it_now()
    {
        // Found live 2026-10-20 (M.ORCH-T529): the pace projected ~65% of 98% while an /allocate CLI held M.COMP-T9 and
        // M.ORCH-T499 claimed and unstarted to avoid merge conflicts, until the user asked for more subagents.
        State("busy", Now.AddMinutes(-1));
        var waker = Waker();
        var (snap, plan) = Claimed(65, 8, 2, TimeSpan.FromMinutes(10));

        var w = waker.TickClaims(snap, plan, Settings, [Tab], type: true, Now).Single();
        Assert.That(w.Typed, Is.True);
        Assert.That(w.Tickets, Is.EqualTo(new[] { "A-T1", "A-T2" }));
        Assert.That(typed.Single().Text, Does.Contain("near 65%, under its 98% target").And.Contain(@"A-T1 (worktree D:\WT\A\A-T1)")
            .And.Contain("Overlapping files are no reason to wait").And.Not.Contain("\n").And.Not.Contain("\""));

        Assert.That(waker.TickClaims(snap, plan, Settings, [Tab], true, Now.AddMinutes(1)), Is.Empty, "one wake, not one every tick");
        Assert.That(typed, Has.Count.EqualTo(1));
    }

    [Test]
    public void No_wake_for_claims_when_on_pace_at_the_allowance_freshly_claimed_or_with_a_subagent_on_them()
    {
        State("idle", Now.AddHours(-1));
        Assert.That(Waker().TickClaims(Claimed(99, 8, 2, TimeSpan.FromMinutes(10)).Snap, Claimed(99, 8, 2, TimeSpan.FromMinutes(10)).Plan, Settings, [Tab], true, Now),
            Is.Empty, "on course for the target");
        var full = Claimed(65, 4, 4, TimeSpan.FromMinutes(10));
        Assert.That(Waker().TickClaims(full.Snap, full.Plan, Settings, [Tab], true, Now), Is.Empty, "the allowance is in use");
        var fresh = Claimed(65, 8, 2, TimeSpan.FromMinutes(2));
        Assert.That(Waker().TickClaims(fresh.Snap, fresh.Plan, Settings, [Tab], true, Now), Is.Empty, "claimed under 5 minutes ago");
        var stopped = Claimed(65, 8, 2, TimeSpan.FromMinutes(10));
        Assert.That(Waker().TickClaims(stopped.Snap, stopped.Plan with { Stop = true }, Settings, [Tab], true, Now), Is.Empty, "at the window's stop");

        // A subagent working in A-T1's worktree: only A-T2 is idle.
        var sub = new TranscriptSession("s1", @"D:\W\A", Now.AddMinutes(-1), default, null)
        {
            TicketedSubagents = [new TicketedSubagent("ag1", "X-T9", "fix", @"D:\WT\A\A-T1\src", [new UsageEvent(Now.AddMinutes(-1), default)])],
        };
        var one = Claimed(65, 8, 2, TimeSpan.FromMinutes(10), [sub]);
        Assert.That(Waker().TickClaims(one.Snap, one.Plan, Settings, [Tab], true, Now).Single().Tickets, Is.EqualTo(new[] { "A-T2" }));
    }

    [Test]
    public void A_cli_it_cant_type_into_is_said_once_per_idle_stretch()
    {
        State("idle", Now.AddHours(-1));
        var waker = Waker(inTab: false);
        var w = waker.Tick(Plan(4, 0, T("A-T1")), Settings, [Tab], true, Now).Single();
        Assert.That(w.Typed, Is.False);
        Assert.That(w.Line, Does.Contain("isn't in a Launcher tab"));
        Assert.That(waker.Tick(Plan(4, 0, T("A-T1")), Settings, [Tab], true, Now.AddMinutes(1)), Is.Empty, "said once, not every tick");
        Assert.That(typed, Is.Empty);
    }
}
