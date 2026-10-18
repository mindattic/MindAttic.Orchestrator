using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using MindAttic.Workflow.Tickets;
using NUnit.Framework;
using Serilog.Events;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T176: an hour and half an hour before the reset, a window on course to end under 90% with nothing any CLI
/// could work raises one Needs-you alert per mark, saying what would go unused, what waits for approval and where /autopilot
/// is on; it never starts or approves anything.</summary>
public sealed class UnderTargetTests
{
    private static readonly DateTime End = new(2026, 10, 17, 18, 0, 0, DateTimeKind.Utc);
    private static readonly IReadOnlyDictionary<int, IReadOnlyList<string>> NoAutopilot = new Dictionary<int, IReadOnlyList<string>>();

    private static Ticket T(string id, string type = "fix") => new(id, "2026-10-10", type, "open", "work", "");

    private static RepoStatus Repo(string name, RepoState state, params Ticket[] open) =>
        new(name, $@"D:\W\{name}", state, [], [], open, null, default);

    private static Snapshot Snap(DateTime now, double projected, params RepoStatus[] repos) => new(now, repos, [])
    {
        Utilization = new Utilization(End.AddHours(-5), End, now, 99, [], 40, 5, projected, TimeSpan.Zero),
        Budget = new BudgetStatus(null, 10_000_000, false, 40, default, default, null, null, 0, null, 90),
    };

    private static (string? Line, SchedulePlan Plan) Check(UnderTarget u, Snapshot snap, OrchestratorSettings? s = null,
        IReadOnlyDictionary<int, IReadOnlyList<string>>? clis = null)
    {
        var plan = Scheduler.Plan(snap, s ?? new OrchestratorSettings(), []);
        return (u.Check(snap, plan, clis ?? NoAutopilot, snap.TakenUtc), plan);
    }

    [Test]
    public void Warns_an_hour_out_and_again_at_30_minutes_once_each_with_what_goes_unused_and_what_waits()
    {
        var u = new UnderTarget();
        var repos = new[] { Repo("A", RepoState.Dormant, T("A-T1", "feature"), T("A-T2", "chore")), Repo("B", RepoState.Waiting) };
        var clis = new Dictionary<int, IReadOnlyList<string>> { [42] = ["B"] };

        Assert.That(Check(u, Snap(End.AddMinutes(-61), 60, repos), clis: clis).Line, Is.Null, "not before the hour");
        var hour = Check(u, Snap(End.AddMinutes(-59), 60, repos), clis: clis).Line;
        Assert.That(hour, Does.Contain("on course to end at 60%, below 90%: 30% of the window (about 3M tokens) would go unused"));
        Assert.That(hour, Does.Contain("2 wait for your approval (A-T1, A-T2: orchestrator approve <ID>)"));
        Assert.That(hour, Does.Contain("/autopilot is on in CLI 42 for B"));
        Assert.That(Check(u, Snap(End.AddMinutes(-45), 60, repos), clis: clis).Line, Is.Null, "the hour's mark is said once");
        Assert.That(Check(u, Snap(End.AddMinutes(-30), 62, repos), clis: clis).Line, Does.Contain("on course to end at 62%"));
        Assert.That(Check(u, Snap(End.AddMinutes(-10), 62, repos), clis: clis).Line, Is.Null, "the half hour's mark is said once");

        // The next window gets its own warnings.
        var next = Snap(End.AddHours(5).AddMinutes(-50), 50, repos);
        next = next with { Utilization = next.Utilization! with { EndUtc = End.AddHours(5) } };
        Assert.That(Check(u, next).Line, Does.Contain("/autopilot is off in every CLI"));
    }

    [Test]
    public void Says_nothing_when_on_course_for_the_target_or_when_a_CLI_has_work()
    {
        var at = End.AddMinutes(-40);
        Assert.That(Check(new UnderTarget(), Snap(at, 92, Repo("A", RepoState.Dormant))).Line, Is.Null, "on course for 90%");

        // A fix in a repo whose CLI is open is work for it.
        Assert.That(Check(new UnderTarget(), Snap(at, 50, Repo("A", RepoState.Waiting, T("A-T1")))).Line, Is.Null);
        // So is one in a repo /autopilot works (a wildcard too), CLI open there or not.
        Assert.That(Check(new UnderTarget(), Snap(at, 50, Repo("GTFO.X", RepoState.Dormant, T("GTFO.X-T1"))),
            clis: new Dictionary<int, IReadOnlyList<string>> { [7] = ["GTFO.*"] }).Line, Is.Null);
        // A fix in a repo no CLI has open isn't anyone's: the warning still comes.
        Assert.That(Check(new UnderTarget(), Snap(at, 50, Repo("A", RepoState.Dormant, T("A-T1")))).Line,
            Does.Contain("No ticket waits for your approval, and no CLI has one it could work"));
        // An approved ticket is work; one blocked by another open ticket isn't.
        var s = new OrchestratorSettings { Approved = ["A-T2"], BlockedBy = new() { ["A-T2"] = ["C-T1"] } };
        var blocked = Snap(at, 50, Repo("A", RepoState.Waiting, T("A-T2", "feature")), Repo("C", RepoState.Dormant, T("C-T1", "feature")));
        Assert.That(Check(new UnderTarget(), blocked, s).Line, Does.Contain("1 waits for your approval (C-T1"));
        Assert.That(Check(new UnderTarget(), blocked, new OrchestratorSettings { Approved = ["A-T2"] }).Line, Is.Null);
    }

    /// <summary>M.ORCH-T373: a ticket estimated past what's left before the reset is one autopilot won't start, so it doesn't
    /// keep the warning quiet; the warning names it as one a person in an open CLI can still pick up, or split.</summary>
    [Test]
    public void A_ticket_too_big_for_what_is_left_does_not_quiet_it_and_is_named()
    {
        var at = End.AddMinutes(-40);
        SessionCost Cost(string ticket, long fresh) => new(Guid.NewGuid().ToString(), ticket, "fix", "X", fresh, at.AddDays(-1));
        Snapshot WithCost(long fresh) => Snap(at, 50, Repo("A", RepoState.Waiting, T("A-T1"))) with
        {
            Pace = new Pace(PaceMode.Open, 4, 1000, 100, 100, "5-hour", End, "On pace.") { RoomTokens = 2_000 },
            Costs = new TicketCosts([Cost("X-T1", fresh), Cost("X-T2", fresh), Cost("X-T3", fresh)]),
        };

        Assert.That(Check(new UnderTarget(), WithCost(500)).Line, Is.Null, "one that fits is work for the open CLI");
        var (line, plan) = Check(new UnderTarget(), WithCost(9_000));
        Assert.That(plan.TooBig.Single().Id, Is.EqualTo("A-T1"));
        Assert.That(line, Does.Contain("No ticket waits for your approval, and no CLI has one autopilot would start"));
        Assert.That(line, Does.Contain("1 (A-T1) is estimated past what's left before the reset, so autopilot won't start it: " +
                                       "a person in an open CLI still can, or split it"));
    }

    [Test]
    public void The_daemon_raises_it_as_a_Needs_you_alert_and_logs_it()
    {
        var dir = Path.Combine(Path.GetTempPath(), "orch-under-" + Guid.NewGuid().ToString("N"));
        var said = new List<(LogEventLevel Level, string Line)>();
        var alerts = new List<(string Title, string Message, bool Urgent)>();
        try
        {
            var daemon = new Daemon(new JsonStore(dir), (level, line) => said.Add((level, line)))
            {
                AlertSink = (title, message, urgent) => alerts.Add((title, message, urgent)),
            };
            var snap = Snap(End.AddMinutes(-55), 40, Repo("A", RepoState.Dormant, T("A-T1", "feature")));
            var plan = Scheduler.Plan(snap, new OrchestratorSettings(), []);
            daemon.ReportUnderTarget(snap, plan, NoAutopilot);
            daemon.ReportUnderTarget(snap, plan, NoAutopilot);

            Assert.That(alerts, Has.Count.EqualTo(1));
            Assert.That(alerts[0].Title, Is.EqualTo("Needs you"));
            Assert.That(alerts[0].Message, Does.Contain("50% of the window").And.Contain("Nothing is started or approved for you"));
            Assert.That(said, Has.One.Matches<(LogEventLevel Level, string Line)>(x => x.Level == LogEventLevel.Warning && x.Line.StartsWith("under target: ")));
            Assert.That(plan.AwaitingApproval.Single().Id, Is.EqualTo("A-T1"), "still waiting: nothing was approved");
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch (IOException) { } }
    }
}
