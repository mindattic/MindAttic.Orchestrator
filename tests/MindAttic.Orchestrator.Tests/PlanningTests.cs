using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using MindAttic.Workflow.Tickets;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class PlanningTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    private static Ticket T(string id, string summary = "work", string date = "2026-10-10", string type = "fix") =>
        new(id, date, type, "open", summary, "");

    private static RepoStatus Repo(string name, RepoState state, params Ticket[] open) =>
        new(name, $@"D:\W\{name}", state, [], [], open, null, default);

    private static Snapshot Snap(params RepoStatus[] repos) => new(Now, repos, []);

    private static IEnumerable<string> Proposed(SchedulePlan p) => p.Proposals.Select(x => x.Item.Id);

    [Test]
    public void A_ticket_waits_for_the_tickets_it_is_blocked_by()
    {
        var snap = Snap(Repo("A", RepoState.Dormant, T("A-T1"), T("A-T2", "needs the API, blocked by A-T1")),
                        Repo("B", RepoState.Dormant, T("B-T1")));
        var settings = new OrchestratorSettings { BlockedBy = new() { ["B-T1"] = ["A-T1"] }, Priorities = new() { ["A-T2"] = 9, ["B-T1"] = 9 } };

        var plan = Scheduler.Plan(snap, settings, []);

        Assert.That(Proposed(plan), Is.EqualTo(new[] { "A-T1" }));
        Assert.That(plan.Queue.Single(q => q.Id == "A-T2").BlockedBy, Is.EqualTo(new[] { "A-T1" }), "from the summary");
        Assert.That(plan.Queue.Single(q => q.Id == "B-T1").BlockedBy, Is.EqualTo(new[] { "A-T1" }), "set by hand");

        var closed = Scheduler.Plan(Snap(Repo("A", RepoState.Dormant, T("A-T2", "blocked by A-T1"))), settings, []);
        Assert.That(Proposed(closed), Is.EqualTo(new[] { "A-T2" }), "a closed blocker doesn't block");
    }

    [Test]
    public void A_reserved_ticket_starts_while_coasting_and_the_rest_pace_against_what_is_left()
    {
        var snap = Snap(Repo("A", RepoState.Dormant, T("A-T1")), Repo("B", RepoState.Dormant, T("B-T1")));
        var coasting = new Snapshot(Now, snap.Repos, []) { Pace =new Pace(PaceMode.Coast, 0, 0, 5000, 5000, "5-hour", Now.AddHours(1), "Coasting.") { RoomTokens = 3_000 } };
        var settings = new OrchestratorSettings { Reservations = new() { ["B-T1"] = 2_000 } };

        var plan = Scheduler.Plan(coasting, settings, []);

        Assert.That(Proposed(plan), Is.EqualTo(new[] { "B-T1" }));
        Assert.That(plan.Proposals.Single().Reason, Does.StartWith("reserved 2"));
        Assert.That(plan.RoomForRest, Is.EqualTo(1_000), "what too big is measured against, and says is left");

        var over = new Snapshot(Now, snap.Repos, []) { Pace = new Pace(PaceMode.Over, 0, 0, 0, null, "5-hour", Now.AddHours(1), "Over.") { RoomTokens = 0 } };
        Assert.That(Scheduler.Plan(over, settings, []).Proposals, Is.Empty, "never past a limit");

        var open = new Snapshot(Now, snap.Repos, []) { Pace = new Pace(PaceMode.Open, 4, 9000, 100, 100, "5-hour", Now.AddHours(1), "On pace.") { RoomTokens = 1_500 } };
        var tight = Scheduler.Plan(open, settings with { Reservations = new() { ["B-T1"] = 1_500 } }, []);
        Assert.That(Proposed(tight), Is.EqualTo(new[] { "B-T1" }));
        Assert.That(tight.Hold, Does.Contain("reserved"), "everything left is spoken for");
    }

    [Test]
    public void A_repo_past_its_share_of_the_block_or_its_session_limit_gets_nothing_new()
    {
        var spender = new RepoStatus("A", @"D:\W\A", RepoState.Dormant, [],
            [new TranscriptSession("s", @"D:\W\A", Now.AddHours(-1), default, null) { Events = [new UsageEvent(Now.AddMinutes(-30), new TokenUsage(0, 0, 0, 4_000))] }],
            [T("A-T1")], Now.AddHours(-1), default);
        var budget = BudgetCalculator.Compute([new UsageEvent(Now.AddMinutes(-30), new TokenUsage(0, 0, 0, 4_000))],
            new OrchestratorSettings { BlockBudget = 10_000, WarnPercent = 100 }, Now, Now.Date);
        var snap = new Snapshot(Now, [spender, Repo("B", RepoState.Dormant, T("B-T1"))], []) { Budget = budget };

        var capped = Scheduler.Plan(snap, new OrchestratorSettings { RepoBudgetPercent = new() { ["A"] = 30 } }, []);
        Assert.That(Proposed(capped), Is.EqualTo(new[] { "B-T1" }), "A spent 40% of the block against a 30% cap");

        var busy = Snap(Repo("A", RepoState.Working, T("A-T1")));
        Assert.That(Scheduler.Plan(busy, new OrchestratorSettings(), []).Proposals, Has.Count.EqualTo(1), "the repo plus one worktree by default");
        Assert.That(Scheduler.Plan(busy, new OrchestratorSettings { RepoMaxSessions = new() { ["A"] = 1 } }, []).Proposals, Is.Empty);
    }

    [TestCase("22:00-07:00", 23, true)]
    [TestCase("22:00-07:00", 6, true)]
    [TestCase("22:00-07:00", 12, false)]
    [TestCase("12:00-13:00", 12, true)]
    [TestCase("nonsense", 12, false)]
    public void Quiet_hours_wrap_past_midnight(string window, int hour, bool quiet) =>
        Assert.That(QuietHours.Active([window], new DateTime(2026, 10, 14, hour, 30, 0)) is not null, Is.EqualTo(quiet));

    [Test]
    public void Old_untouched_tickets_are_listed_for_grooming()
    {
        var plan = Scheduler.Plan(Snap(Repo("A", RepoState.Dormant, T("A-T1", date: "2026-08-01"), T("A-T2", date: "2026-10-12"))), new OrchestratorSettings(), []);

        Assert.That(plan.Groom.Select(q => q.Id), Is.EqualTo(new[] { "A-T1" }));

        var unapproved = Scheduler.Plan(Snap(Repo("A", RepoState.Dormant, T("A-T3", date: "2026-08-01", type: "feature"))), new OrchestratorSettings(), []);
        Assert.That(unapproved.Groom.Select(q => q.Id), Is.EqualTo(new[] { "A-T3" }), "an old feature still waiting for approval");
    }

    [Test]
    public void Starting_work_in_a_repo_that_shares_code_with_a_busy_one_is_flagged()
    {
        var snap = new Snapshot(Now, [Repo("A", RepoState.Dormant, T("A-T1")), Repo("B", RepoState.Working)], [])
        {
            LinkedRepos = new Dictionary<string, IReadOnlySet<string>> { ["A"] = new HashSet<string> { "B" } },
        };

        var plan = Scheduler.Plan(snap, new OrchestratorSettings(), []);

        Assert.That(plan.Proposals.Single().Reason, Does.Contain("shares duplicated code with B"));
    }
}
