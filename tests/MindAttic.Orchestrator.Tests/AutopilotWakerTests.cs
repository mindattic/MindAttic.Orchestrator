using System.Text.Json;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using MindAttic.Workflow.Tickets;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>An idle /autopilot CLI is sent back for work within a tick of next having a ticket and room for it (M.ORCH-T523).</summary>
public sealed class AutopilotWakerTests
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

    private AutopilotWaker Waker(bool inTab = true, bool handingOff = false) => new(cycler, store)
    {
        IsAlive = _ => true,
        Pilots = () => new Dictionary<int, IReadOnlyList<string>> { [Cli] = ["A"] },
        HostOf = (pid, hosts) => inTab && pid == Cli ? hosts.FirstOrDefault() : null,
        Submit = (pipe, text) => { lock (typed) typed.Add((pipe, text)); return Task.FromResult(true); },
        HandingOff = () => handingOff,
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
    public void An_idle_autopilot_cli_let_stop_with_no_room_is_typed_back_to_work_once_room_opens()
    {
        // Found live 2026-10-20: every /autopilot CLI stopped the night before on "the pace has no room", and none was sent back
        // when the window reset; MindAttic.Workflow's sat idle with M.FLOW-T116 workable until the user typed to it.
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
        Assert.That(waker.Tick(Plan(4, 0, T("A-T1")), Settings, [Tab], true, Now.AddMinutes(1) + AutopilotWaker.Cooldown), Has.Count.EqualTo(1),
            "once the cooldown is past it's asked again");
    }

    [Test]
    public void A_busy_just_stopped_window_held_goal_or_handoff_cli_or_one_with_nothing_workable_is_left_alone()
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
        Assert.That(Waker().Tick(plan with { Pace = plan.Pace! with { WindDown = true } }, Settings, [Tab], true, Now), Is.Empty, "past the wind-down line");
        Assert.That(Waker().Tick(plan, Settings with { KeepGoing = false }, [Tab], true, Now), Is.Empty, "keep-going off");
        Assert.That(Waker(handingOff: true).Tick(plan, Settings, [Tab], true, Now), Is.Empty, "a /handoff holds every CLI");
        Assert.That(typed, Is.Empty);
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
