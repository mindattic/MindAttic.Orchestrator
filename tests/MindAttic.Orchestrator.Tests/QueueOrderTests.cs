using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using MindAttic.Tickets.Log;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// M.ORCH-T863: "recently created tickets always have highest priority, unless they have a prereq, then those have higher
/// priority". Newest created first (same date: the higher number), each ticket's open prerequisites pulled ahead of it,
/// an explicit priority still above recency, and a ticket still not handed out before its prerequisite closes.
/// </summary>
public sealed class QueueOrderTests
{
    private static readonly DateTime Now = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

    private static Ticket T(string id, string date = "2026-10-01", string type = "fix") => new(id, date, type, "open", $"summary of {id}", "");

    private static Snapshot Snap(params (string Name, Ticket[] Open)[] repos) =>
        new(Now, [.. repos.Select(r => new RepoStatus(r.Name, $@"D:\W\{r.Name}", RepoState.Dormant, [], [], r.Open, null, default))], []);

    private static Dictionary<string, IReadOnlyList<string>> Requires(params (string Ticket, string[] Prereqs)[] links) =>
        links.ToDictionary(l => l.Ticket, l => (IReadOnlyList<string>)l.Prereqs, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<string> Order(Snapshot snap, IReadOnlyList<Assignment>? assignments = null) =>
        [.. Scheduler.Plan(snap, new OrchestratorSettings(), assignments ?? []).Queue.Select(q => q.Id)];

    [Test]
    public void The_newest_created_ticket_comes_first_and_on_the_same_date_the_higher_number()
    {
        var snap = Snap(("A", [T("A-T1", "2026-10-20"), T("A-T2", "2026-10-24"), T("A-T3", "2026-10-24")]),
            ("B", [T("B-T7", "2026-10-22"), T("B-T9", "2026-09-01")]));

        Assert.That(Order(snap), Is.EqualTo(new[] { "A-T3", "A-T2", "B-T7", "A-T1", "B-T9" }));
    }

    [Test]
    public void Type_and_estimate_no_longer_outrank_recency()
    {
        var snap = Snap(("A", [T("A-T1", "2026-10-01", "fix"), T("A-T2", "2026-10-10", "feature"), T("A-T3", "2026-10-05", "docs")])) with
        {
            Approved = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "A-T2", "A-T3" },
        };

        Assert.That(Order(snap), Is.EqualTo(new[] { "A-T2", "A-T3", "A-T1" }), "an approved feature filed later goes before an older fix");
    }

    [Test]
    public void A_tickets_open_prerequisites_are_pulled_ahead_of_it_and_it_waits_for_them()
    {
        // A-T9 is the newest, but needs A-T1 (the oldest); A-T5 sits between them on recency.
        var snap = Snap(("A", [T("A-T1", "2026-10-01"), T("A-T5", "2026-10-05"), T("A-T9", "2026-10-09")])) with
        {
            Requires = Requires(("A-T9", ["A-T1"])),
        };
        var plan = Scheduler.Plan(snap, new OrchestratorSettings(), []);

        Assert.That(plan.Queue.Select(q => q.Id), Is.EqualTo(new[] { "A-T1", "A-T9", "A-T5" }));
        Assert.That(plan.Queue[0].RequiredBy, Is.EqualTo(new[] { "A-T9" }));
        Assert.That(plan.Queue[1].BlockedBy, Is.EqualTo(new[] { "A-T1" }));
        Assert.That(plan.Proposals.Single().Item.Id, Is.EqualTo("A-T1"), "the prerequisite starts first");
        Assert.That(plan.Proposals.Single().Reason, Does.StartWith("required by A-T9"));

        var store = new JsonStore(Path.Combine(Path.GetTempPath(), "orch-order-" + Guid.NewGuid().ToString("N")));
        var (handed, _) = Allocate.Next(plan, new OrchestratorSettings { WorktreeRoot = @"D:\WT" }, store, ["A"], 10, claim: false, Now,
            pilotRunning: _ => false);
        Assert.That(handed.Select(h => h.Item.Id), Is.EqualTo(new[] { "A-T1", "A-T5" }), "A-T9 isn't handed out while A-T1 is open");
    }

    [Test]
    public void Prerequisites_are_pulled_transitively_keeping_their_own_newest_first_order()
    {
        // A-T9 needs A-T2 and A-T4; A-T4 needs A-T1. A-T6 is unrelated.
        var snap = Snap(("A", [T("A-T1", "2026-10-01"), T("A-T2", "2026-10-02"), T("A-T4", "2026-10-04"), T("A-T6", "2026-10-06"), T("A-T9", "2026-10-09")])) with
        {
            Requires = Requires(("A-T9", ["A-T2", "A-T4"]), ("A-T4", ["A-T1"])),
        };

        Assert.That(Order(snap), Is.EqualTo(new[] { "A-T1", "A-T4", "A-T2", "A-T9", "A-T6" }),
            "A-T4 (newer) before A-T2, and A-T4's own prerequisite A-T1 before it");
    }

    [Test]
    public void A_prerequisite_cycle_is_broken_and_every_ticket_appears_once()
    {
        var snap = Snap(("A", [T("A-T1", "2026-10-01"), T("A-T2", "2026-10-02"), T("A-T3", "2026-10-03"), T("A-T4", "2026-10-04")])) with
        {
            Requires = Requires(("A-T3", ["A-T2"]), ("A-T2", ["A-T1"]), ("A-T1", ["A-T3"])),
        };
        var plan = Scheduler.Plan(snap, new OrchestratorSettings(), []);

        Assert.That(plan.Queue.Select(q => q.Id), Is.EquivalentTo(new[] { "A-T1", "A-T2", "A-T3", "A-T4" }));
        Assert.That(plan.Queue.Select(q => q.Id), Is.Unique);
        Assert.That(plan.Queue[0].Id, Is.EqualTo("A-T4"), "the one outside the cycle, newest, is untouched");
        Assert.That(plan.Proposals.Select(p => p.Item.Id), Is.EqualTo(new[] { "A-T4" }), "each in the cycle waits on another: none is proposed");
    }

    [Test]
    public void A_ticket_that_requires_itself_or_a_closed_ticket_is_not_held()
    {
        var snap = Snap(("A", [T("A-T1", "2026-10-01"), T("A-T2", "2026-10-02")])) with
        {
            Requires = Requires(("A-T2", ["A-T2", "A-T7"])),
        };
        var plan = Scheduler.Plan(snap, new OrchestratorSettings(), []);

        Assert.That(plan.Queue.Select(q => q.Id), Is.EqualTo(new[] { "A-T2", "A-T1" }));
        Assert.That(plan.Queue[0].BlockedBy, Is.Empty);
    }

    [Test]
    public void An_explicit_priority_still_outranks_recency_and_still_pulls_its_prerequisites_ahead()
    {
        var snap = Snap(("A", [T("A-T1", "2026-10-01"), T("A-T2", "2026-10-02"), T("A-T3", "2026-10-03"), T("A-T8", "2026-10-08")])) with
        {
            Priorities = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { ["A-T2"] = 50 },
            Requires = Requires(("A-T2", ["A-T1"])),
        };

        Assert.That(Order(snap), Is.EqualTo(new[] { "A-T1", "A-T2", "A-T8", "A-T3" }),
            "the bumped A-T2 first, behind its prerequisite; then newest first");
    }

    [Test]
    public void A_prerequisite_awaiting_approval_or_assigned_is_not_moved_but_still_holds_its_dependent()
    {
        var snap = Snap(("A", [T("A-T1", "2026-10-01", "feature"), T("A-T2", "2026-10-02"), T("A-T3", "2026-10-03"), T("A-T5", "2026-10-05")])) with
        {
            Requires = Requires(("A-T5", ["A-T1", "A-T2"])),
        };
        var plan = Scheduler.Plan(snap, new OrchestratorSettings(), [new Assignment("A-T2", "A", @"D:\WT\A\A-T2", "Claude", Now, true)]);

        Assert.That(plan.Queue.Select(q => q.Id), Is.EqualTo(new[] { "A-T5", "A-T3", "A-T2" }),
            "the unapproved A-T1 waits outside the queue; the assigned A-T2 stays at the end");
        Assert.That(plan.Queue[0].BlockedBy, Is.EquivalentTo(new[] { "A-T1", "A-T2" }));
        Assert.That(plan.Proposals.Single().Item.Id, Is.EqualTo("A-T3"), "A-T5 still waits for both");
    }

    [Test]
    public void The_newest_comparer_breaks_ties_by_repo()
    {
        QueueItem Item(string repo, string id) => new(repo, $@"D:\W\{repo}", T(id), 0, null, false);
        var items = new[] { Item("B", "B-T3"), Item("A", "A-T3"), Item("C", "C-T4") }.Order(Scheduler.Newest).Select(q => q.Id);

        Assert.That(items, Is.EqualTo(new[] { "C-T4", "A-T3", "B-T3" }));
    }
}
