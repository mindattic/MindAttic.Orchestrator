using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using MindAttic.Tickets.Log;
using NUnit.Framework;
using Serilog.Events;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T176: an hour and half an hour before the reset, a window on course to end under 90% with nothing any CLI
/// could work raises one Needs-you alert per mark, saying what would go unused, what waits for approval and where /allocate
/// is on; it never starts or approves anything.</summary>
public sealed class UnderTargetTests
{
    private static readonly DateTime End = new(2026, 10, 17, 18, 0, 0, DateTimeKind.Utc);
    private static readonly IReadOnlyDictionary<int, IReadOnlyList<string>> NoAllocate = new Dictionary<int, IReadOnlyList<string>>();

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
        return (u.Check(snap, plan, clis ?? NoAllocate, snap.TakenUtc), plan);
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
        Assert.That(hour, Does.Contain("2 wait for your approval (A-T1, A-T2: tickets ticket approve <ID>)"));
        Assert.That(hour, Does.Contain("/allocate is on in CLI 42 for B"));
        Assert.That(Check(u, Snap(End.AddMinutes(-45), 60, repos), clis: clis).Line, Is.Null, "the hour's mark is said once");
        Assert.That(Check(u, Snap(End.AddMinutes(-30), 62, repos), clis: clis).Line, Does.Contain("on course to end at 62%"));
        Assert.That(Check(u, Snap(End.AddMinutes(-10), 62, repos), clis: clis).Line, Is.Null, "the half hour's mark is said once");

        // The next window gets its own warnings.
        var next = Snap(End.AddHours(5).AddMinutes(-50), 50, repos);
        next = next with { Utilization = next.Utilization! with { EndUtc = End.AddHours(5) } };
        Assert.That(Check(u, next).Line, Does.Contain("/allocate is off in every CLI"));
    }

    [Test]
    public void Says_nothing_when_on_course_for_the_target_or_when_a_CLI_has_work()
    {
        var at = End.AddMinutes(-40);
        Assert.That(Check(new UnderTarget(), Snap(at, 92, Repo("A", RepoState.Dormant))).Line, Is.Null, "on course for 90%");

        // A fix in a repo whose CLI is open is work for it.
        Assert.That(Check(new UnderTarget(), Snap(at, 50, Repo("A", RepoState.Waiting, T("A-T1")))).Line, Is.Null);
        // So is one in a repo /allocate works (a wildcard too), CLI open there or not.
        Assert.That(Check(new UnderTarget(), Snap(at, 50, Repo("GTFO.X", RepoState.Dormant, T("GTFO.X-T1"))),
            clis: new Dictionary<int, IReadOnlyList<string>> { [7] = ["GTFO.*"] }).Line, Is.Null);
        // A fix in a repo no CLI has open isn't anyone's: the warning still comes.
        Assert.That(Check(new UnderTarget(), Snap(at, 50, Repo("A", RepoState.Dormant, T("A-T1")))).Line,
            Does.Contain("No ticket waits for your approval, and no CLI has one it could work"));
        // An approved ticket is work; one blocked by another open ticket isn't.
        // Blocked in the ticket database: a 'requires' link (tickets ticket block, M.ORCH-T762).
        var free = Snap(at, 50, Repo("A", RepoState.Waiting, T("A-T2", "feature")), Repo("C", RepoState.Dormant, T("C-T1", "feature")))
            with { Approved = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "A-T2" } };
        var blocked = free with { Requires = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase) { ["A-T2"] = ["C-T1"] } };
        Assert.That(Check(new UnderTarget(), blocked).Line, Does.Contain("1 waits for your approval (C-T1"));
        Assert.That(Check(new UnderTarget(), free).Line, Is.Null);
    }

    [Test]
    public void The_reset_time_is_written_the_same_on_any_windows_language()
    {
        // M.ORCH-T405: a culture with no AM/PM designators printed "resets at 6:00  (in 40 min)".
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            var bare = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.GetCultureInfo("en-US").Clone();
            bare.DateTimeFormat.AMDesignator = bare.DateTimeFormat.PMDesignator = "";
            System.Globalization.CultureInfo.CurrentCulture = bare;
            var line = Check(new UnderTarget(), Snap(End.AddMinutes(-40), 50, Repo("A", RepoState.Dormant))).Line;
            Assert.That(line, Does.Match(@"resets at \d{1,2}:\d\d [AP]M \(in 40 min\)"));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = culture; }
    }

    [Test]
    public void An_unapproved_ticket_whose_assignment_went_stale_does_not_quiet_it()
    {
        // M.ORCH-T393: dispatched, then approve --undo; once stale nothing will start it, so it's no CLI's work.
        var at = End.AddMinutes(-40);
        var snap = Snap(at, 50, Repo("A", RepoState.Waiting, T("A-T1", "feature")));
        var quiet = new Assignment("A-T1", "A", @"D:\Projects\MindAttic.worktrees\A\A-T1", "Claude", at.AddHours(-20), Worktree: true);
        var plan = Scheduler.Plan(snap, new OrchestratorSettings(), [quiet]);

        Assert.That(plan.Stale.Single().Id, Is.EqualTo("A-T1"), "still in the queue, stale");
        Assert.That(new UnderTarget().Check(snap, plan, NoAllocate, at), Does.Contain("would go unused"));
    }

    /// <summary>M.ORCH-T373: a ticket estimated past what's left before the reset is one allocate won't start, so it doesn't
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
        Assert.That(line, Does.Contain("No ticket waits for your approval, and no CLI has one allocate would start"));
        Assert.That(line, Does.Contain("1 (A-T1) is estimated past what's left before the reset, so allocate won't start it: " +
                                       "a person in an open CLI still can, or split it"));
    }

    [Test]
    public void A_ticket_too_big_for_what_is_left_is_marked_so_under_a_hold_too()
    {
        // M.ORCH-T395: the plan stopped short of the size check while holding, so it went unmarked and quieted the warning.
        var at = End.AddMinutes(-40);
        SessionCost Cost(string ticket) => new(Guid.NewGuid().ToString(), ticket, "fix", "X", 9_000, at.AddDays(-1));
        var snap = Snap(at, 50, Repo("A", RepoState.Waiting, T("A-T1"))) with
        {
            Pace = new Pace(PaceMode.Coast, 4, 1000, 100, 100, "5-hour", End, "Coasting.") { RoomTokens = 2_000 },
            Costs = new TicketCosts([Cost("X-T1"), Cost("X-T2"), Cost("X-T3")]),
        };

        var (line, plan) = Check(new UnderTarget(), snap);

        Assert.That(plan.TooBig.Single().Id, Is.EqualTo("A-T1"));
        Assert.That(line, Does.Contain("1 (A-T1) is estimated past what's left before the reset"));
    }

    [Test]
    public void No_workable_tickets_left_for_allocate_under_target_is_alerted_once_per_window_with_what_waits_for_approval()
    {
        // M.ORCH-T529: every tick, not only at the hour marks, and never widening a CLI's repos.
        var u = new UnderTarget();
        var clis = new Dictionary<int, IReadOnlyList<string>> { [42] = ["A"] };
        var repos = new[] { Repo("A", RepoState.Waiting, T("A-T1", "feature")), Repo("B", RepoState.Dormant, T("B-T1"), T("B-T2", "feature")) };
        SchedulePlan Plan(Snapshot s) => Scheduler.Plan(s, new OrchestratorSettings(), []);

        var early = Snap(End.AddHours(-4), 65, repos);
        var line = u.CheckNoWork(early, Plan(early), clis);
        Assert.That(line, Does.StartWith("On course for 65%: no workable tickets left; approve some?"));
        Assert.That(line, Does.Contain("1 waits for your approval there (A-T1: tickets ticket approve <ID>)").And.Contain("1 more in repos no /allocate works"));
        Assert.That(line, Does.Not.Contain("B-T1"), "B's fix isn't /allocate's: its repos aren't widened");
        Assert.That(UnderTarget.NoWorkLine(early, Plan(early), clis), Is.EqualTo(line), "the dashboard says the same");

        var later = Snap(End.AddHours(-2), 60, repos);
        Assert.That(u.CheckNoWork(later, Plan(later), clis), Is.Null, "once per window");
        var next = Snap(End.AddHours(1), 50, repos);
        next = next with { Utilization = next.Utilization! with { EndUtc = End.AddHours(5) } };
        Assert.That(u.CheckNoWork(next, Plan(next), clis), Is.Not.Null, "the next window gets its own");

        Assert.That(new UnderTarget().CheckNoWork(Snap(End.AddHours(-4), 99.5, repos), Plan(early), clis), Is.Null, "on course for the target");
        var work = Snap(End.AddHours(-4), 65, Repo("A", RepoState.Waiting, T("A-T2")));
        Assert.That(new UnderTarget().CheckNoWork(work, Plan(work), clis), Is.Null, "a fix /allocate can start is work");
        Assert.That(new UnderTarget().CheckNoWork(early, Plan(early), NoAllocate), Is.Null, "no /allocate CLI to say it of");
    }

    [Test]
    public void The_allowance_shown_to_CLIs_is_raised_while_the_window_is_on_course_to_end_under_target()
    {
        var pace = new Pace(PaceMode.Open, 6, 10_000, 0, 1_000, "5-hour", End, "On pace.") { SubagentAllowance = 3, Subagents = 2 };
        var under = new Utilization(End.AddHours(-5), End, End.AddHours(-3), 98, [], 20, 5, 65, TimeSpan.Zero);

        var raised = Pacer.UnderTarget(pace, under, 16);
        Assert.That(raised.SubagentAllowance, Is.EqualTo(6), "2 running + 4 for 33 points short");
        Assert.That(raised.SubagentNote, Does.StartWith("Subagents: up to 6 may run at once").And.Contain("near 65%, under its 98% target, so start more now"));
        Assert.That(Pacer.UnderTarget(pace, under, 4).SubagentAllowance, Is.EqualTo(4), "never past the ceiling");
        Assert.That(Pacer.UnderTarget(pace, under with { ProjectedEndPercent = 98.5 }, 16), Is.SameAs(pace), "on course: left as it is");
        var none = pace with { SubagentAllowance = 0 };
        Assert.That(Pacer.UnderTarget(none, under, 16), Is.SameAs(none), "the pace says none new");
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
            daemon.ReportUnderTarget(snap, plan, NoAllocate);
            daemon.ReportUnderTarget(snap, plan, NoAllocate);

            Assert.That(alerts, Has.Count.EqualTo(1));
            Assert.That(alerts[0].Title, Is.EqualTo("Needs you"));
            Assert.That(alerts[0].Message, Does.Contain("50% of the window").And.Contain("Nothing is started or approved for you"));
            Assert.That(said, Has.One.Matches<(LogEventLevel Level, string Line)>(x => x.Level == LogEventLevel.Warning && x.Line.StartsWith("under target: ")));
            Assert.That(plan.AwaitingApproval.Single().Id, Is.EqualTo("A-T1"), "still waiting: nothing was approved");
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch (IOException) { } }
    }
}
