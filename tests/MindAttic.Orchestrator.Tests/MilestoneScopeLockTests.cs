using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Status;
using MindAttic.Orchestrator.Tickets;
using MindAttic.Orchestrator.Wpf.ViewModels;
using MindAttic.Tickets.Data;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The milestone scope lock in the Orchestrator (M.ORCH-T1685): what waits to join a locked milestone is an inbox
/// item the owner approves or declines, and a milestone that has grown since activation is told once per figure.</summary>
public sealed class MilestoneScopeLockTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
    private static readonly OrchestratorSettings Settings = new();

    private readonly TempDirs temps = new();
    private string dir = "";

    [SetUp]
    public void SetUp() => dir = temps.New();

    [TearDown]
    public void TearDown()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        temps.DeleteAll();
    }

    private static MilestoneScope Scope(string id, int atActivation, int now, int waitingTickets = 0, params MilestoneWaiting[] waiting) =>
        new(id, $"name of {id}", MilestoneStates.Active, atActivation, Math.Max(0, now - atActivation), 0, now, waitingTickets, waiting);

    private static IReadOnlyList<OwnerInboxItem> Inbox(params MilestoneScope[] scopes)
    {
        var snapshot = new Snapshot(Now, [], []) { MilestoneScopes = scopes };
        return OwnerInbox.Build(new OwnerInboxInputs(snapshot, Scheduler.Plan(snapshot, Settings, []), Now) { Settings = Settings });
    }

    private TicketsDb Tickets()
    {
        var repo = Path.Combine(dir, "A");
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        Directory.CreateDirectory(Path.Combine(repo, "docs"));
        File.WriteAllText(Path.Combine(repo, "docs", "USER_STORIES.md"),
            "---\ncode: A\n---\n\n# A\n\n## Tickets\n\n| ID | Date | Type | Status | Summary | Ref |\n|---|---|---|---|---|---|\n" +
            "| A-T1 | 2026-10-01 | fix | open | one | — |\n| A-T2 | 2026-10-01 | fix | open | two | — |\n| A-T3 | 2026-10-01 | fix | open | three | — |\n");
        var db = TicketsDb.Open(TicketsDb.DefaultPath(dir));
        db.Import([new MindAttic.Tickets.Repo("A", repo)]);
        return db;
    }

    // ── The inbox items ──────────────────────────────────────────────────

    [Test]
    public void An_addition_waiting_to_join_a_milestone_is_an_inbox_item_with_the_command_that_answers_it()
    {
        var item = Inbox(Scope("MS-1", 2, 2, 1, new MilestoneWaiting("A-T5", Now.AddHours(-3), "amy"))).Single();
        Assert.Multiple(() =>
        {
            Assert.That(item.Kind, Is.EqualTo(OwnerInbox.MilestoneAddition));
            Assert.That(item.Key, Is.EqualTo("milestone-addition|MS-1/A-T5"));
            Assert.That(item.Title, Is.EqualTo("A-T5 is waiting to join milestone MS-1 (name of MS-1), proposed by amy"));
            Assert.That(item.SinceUtc, Is.EqualTo(Now.AddHours(-3)));
            Assert.That(item.Action, Is.EqualTo("tickets milestone approve|decline MS-1 A-T5 --by <WHO>"));
            Assert.That(item.MilestoneId, Is.EqualTo("MS-1"));
            Assert.That(item.MilestoneItem, Is.EqualTo("A-T5"));
            Assert.That(item.TicketId, Is.Null, "the same ticket may also wait for its own approval, so it is not that ticket's item");
            Assert.That(OwnerInbox.KindLabel(item.Kind), Is.EqualTo("Milestone addition"));
        });
    }

    [Test]
    public void Waiting_additions_are_listed_oldest_first_across_milestones_and_one_nobody_proposed_names_nobody()
    {
        var items = Inbox(
            Scope("MS-1", 1, 1, 1, new MilestoneWaiting("A-T5", Now.AddHours(-1), null)),
            Scope("MS-2", 1, 1, 2, new MilestoneWaiting("A-T6", Now.AddHours(-2), "ben"), new MilestoneWaiting("A-T5", null, "ben")));
        Assert.That(items.Select(i => i.Id), Is.EqualTo(new[] { "MS-2/A-T6", "MS-1/A-T5", "MS-2/A-T5" }), "the same item waits on two milestones as two items");
        Assert.That(items[1].Title, Is.EqualTo("A-T5 is waiting to join milestone MS-1 (name of MS-1)"));
    }

    [Test]
    public void A_milestone_that_has_grown_is_told_with_nothing_to_answer_and_one_that_has_not_is_not()
    {
        var items = Inbox(Scope("MS-1", 4, 7, 2), Scope("MS-2", 4, 4), Scope("MS-3", 4, 3), Scope("MS-4", 1, 2));
        Assert.That(items.Select(i => i.Key), Is.EqualTo(new[] { "milestone-growth|MS-1|+3/2", "milestone-growth|MS-4|+1/0" }));
        Assert.Multiple(() =>
        {
            Assert.That(items[0].Title, Is.EqualTo("Milestone MS-1 (name of MS-1) has grown by 3 tickets since it was activated, 2 more waiting for approval"));
            Assert.That(items[1].Title, Is.EqualTo("Milestone MS-4 (name of MS-4) has grown by 1 ticket since it was activated"));
            Assert.That(items[0].Action, Is.EqualTo("orchestrator milestone list"));
            Assert.That(items[0].MilestoneItem, Is.Null);
            Assert.That(items[0].TicketId, Is.Null);
            Assert.That(OwnerInbox.KindLabel(items[0].Kind), Is.EqualTo("Milestone growth"));
        });
    }

    [Test]
    public void A_snapshot_with_no_milestone_scopes_adds_nothing()
    {
        var snapshot = new Snapshot(Now, [], []);
        Assert.That(OwnerInbox.Build(new OwnerInboxInputs(snapshot, Scheduler.Plan(snapshot, Settings, []), Now)), Is.Empty);
    }

    [Test]
    public void Growth_is_alerted_once_per_figure_and_not_again_while_it_stands()
    {
        var store = new JsonStore(Path.Combine(dir, "store"));
        var alerts = new OwnerInboxAlerts(store);
        alerts.Fresh([], Now); // the first look
        Assert.That(alerts.Fresh(Inbox(Scope("MS-1", 4, 5)), Now.AddMinutes(1)).Select(i => i.Key), Is.EqualTo(new[] { "milestone-growth|MS-1|+1/0" }));
        Assert.That(alerts.Fresh(Inbox(Scope("MS-1", 4, 5)), Now.AddMinutes(2)), Is.Empty, "the same figure: not again");
        Assert.That(alerts.Fresh(Inbox(Scope("MS-1", 4, 6)), Now.AddMinutes(3)).Select(i => i.Key), Is.EqualTo(new[] { "milestone-growth|MS-1|+2/0" }), "a new figure is told");
        Assert.That(alerts.Fresh(Inbox(Scope("MS-1", 4, 6)), Now.AddMinutes(4)), Is.Empty);
    }

    [Test]
    public void The_alert_text_for_a_waiting_addition_names_it_and_its_command()
    {
        var text = OwnerInboxAlerts.Text(Inbox(Scope("MS-1", 2, 2, 1, new MilestoneWaiting("A-T5", Now, "amy"))));
        Assert.That(text, Is.EqualTo("Orchestrator inbox: Milestone addition: A-T5 is waiting to join milestone MS-1 (name of MS-1), proposed by amy. tickets milestone approve|decline MS-1 A-T5 --by <WHO>"));
    }

    // ── The scope, in words and from the store ───────────────────────────

    [Test]
    public void The_growth_reads_as_a_signed_count_and_what_waits_and_is_empty_when_nothing_changed()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Scope("MS-1", 4, 7, 2).GrownText, Is.EqualTo("+3, 2 waiting"));
            Assert.That(Scope("MS-1", 4, 7).GrownText, Is.EqualTo("+3"));
            Assert.That(Scope("MS-1", 4, 4).GrownText, Is.Empty);
            Assert.That(Scope("MS-1", 4, 3).GrownText, Is.EqualTo("-1"));
            Assert.That(Scope("MS-1", 4, 4, 1).GrownText, Is.EqualTo("1 waiting"));
            Assert.That(Scope("MS-1", 4, 4, 0, new MilestoneWaiting("A-T1", null, null)).GrownText, Is.EqualTo("1 waiting (no new tickets)"));
        });
    }

    [Test]
    public void A_store_timestamp_reads_as_utc_and_anything_else_as_unknown()
    {
        Assert.That(MilestoneScope.At("2026-11-16T09:30:00Z"), Is.EqualTo(new DateTime(2026, 11, 16, 9, 30, 0, DateTimeKind.Utc)));
        Assert.That(MilestoneScope.At("soon"), Is.Null);
        Assert.That(MilestoneScope.At(null), Is.Null);
    }

    [Test]
    public void Only_active_and_accepting_milestones_have_a_scope_and_a_planned_one_takes_members_at_once()
    {
        using var db = Tickets();
        var m = db.AddMilestone("First", null, ["A"]);
        db.AddToMilestone(m.Id, ["A-T1"]);
        Assert.That(MilestoneScope.Read(db), Is.Empty, "planned: not locked yet");

        db.ActivateMilestone(m.Id);
        var scope = MilestoneScope.Read(db).Single();
        Assert.Multiple(() =>
        {
            Assert.That(scope.Id, Is.EqualTo(m.Id));
            Assert.That(scope.Name, Is.EqualTo("First"));
            Assert.That(scope.AtActivation, Is.EqualTo(1));
            Assert.That(scope.Grown, Is.Zero);
            Assert.That(scope.Waiting, Is.Empty);
        });
    }

    [Test]
    public void An_addition_to_an_active_milestone_waits_until_the_inbox_action_approves_it_and_then_counts_as_growth()
    {
        string id;
        using (var db = Tickets())
        {
            id = db.AddMilestone("First", null, ["A"]).Id;
            db.AddToMilestone(id, ["A-T1"]);
            db.ActivateMilestone(id);
            db.AddToMilestone(id, ["A-T2"], by: "amy");
            var waiting = MilestoneScope.Read(db).Single();
            Assert.That(waiting.Waiting.Select(w => (w.Item, w.By)), Is.EqualTo(new[] { ("A-T2", (string?)"amy") }));
            Assert.That(waiting.Waiting[0].SinceUtc, Is.Not.Null);
            Assert.That(waiting.WaitingTickets, Is.EqualTo(1));
            Assert.That(waiting.Grown, Is.Zero, "it is no member until approved");
        }

        OwnerInboxActions.ApproveAddition(dir, id, "A-T2", "owner@example.com");

        using (var db = TicketsDb.OpenIfMigrated(dir)!)
        {
            var grown = MilestoneScope.Read(db).Single();
            Assert.That(grown.Waiting, Is.Empty);
            Assert.That((grown.AtActivation, grown.Now, grown.Grown), Is.EqualTo((1, 2, 1)));
            Assert.That(db.MilestoneHistory(id).Select(e => e.Field), Does.Contain("approved"));
        }
    }

    [Test]
    public void The_inbox_action_declines_a_waiting_addition_with_its_reason_and_it_never_joins()
    {
        string id;
        using (var db = Tickets())
        {
            id = db.AddMilestone("First", null, ["A"]).Id;
            db.AddToMilestone(id, ["A-T1"]);
            db.ActivateMilestone(id);
            db.AddToMilestone(id, ["A-T3"], by: "amy");
        }

        OwnerInboxActions.DeclineAddition(dir, id, "A-T3", "owner@example.com", "not this milestone");

        using (var db = TicketsDb.OpenIfMigrated(dir)!)
        {
            var scope = MilestoneScope.Read(db).Single();
            Assert.That(scope.Waiting, Is.Empty);
            Assert.That(scope.Grown, Is.Zero);
            Assert.That(db.MilestoneHistory(id).Last(e => e.Field == "declined").New, Is.EqualTo("A-T3 by owner@example.com: not this milestone"));
        }
    }

    [Test]
    public void Answering_an_addition_that_is_not_waiting_or_with_nobody_named_is_refused()
    {
        string id;
        using (var db = Tickets())
        {
            id = db.AddMilestone("First", null, ["A"]).Id;
            db.ActivateMilestone(id);
            db.AddToMilestone(id, ["A-T2"]);
        }
        Assert.Throws<KeyNotFoundException>(() => OwnerInboxActions.ApproveAddition(dir, id, "A-T3", "owner@example.com"));
        Assert.Throws<ArgumentException>(() => OwnerInboxActions.ApproveAddition(dir, id, "A-T2", " "));
        Assert.Throws<ArgumentException>(() => OwnerInboxActions.DeclineAddition(dir, id, "A-T2", ""));
    }

    [Test]
    public void The_addition_actions_throw_instead_of_silently_doing_nothing_with_no_migrated_database()
    {
        Assert.Throws<InvalidOperationException>(() => OwnerInboxActions.ApproveAddition(dir, "MS-1", "A-T1", "owner@example.com"));
        Assert.Throws<InvalidOperationException>(() => OwnerInboxActions.DeclineAddition(dir, "MS-1", "A-T1", "owner@example.com"));
    }

    // ── The dashboard's row ──────────────────────────────────────────────

    [Test]
    public void Only_a_waiting_addition_row_can_be_approved_or_declined_as_an_addition_and_growth_has_no_action()
    {
        var items = Inbox(Scope("MS-1", 4, 6, 1, new MilestoneWaiting("A-T5", Now.AddHours(-2), "amy")));
        var addition = new InboxRowViewModel(items.Single(i => i.Kind == OwnerInbox.MilestoneAddition), Now);
        var growth = new InboxRowViewModel(items.Single(i => i.Kind == OwnerInbox.MilestoneGrowth), Now);
        Assert.Multiple(() =>
        {
            Assert.That(addition.CanAnswerAddition, Is.True);
            Assert.That((addition.MilestoneId, addition.MilestoneItem), Is.EqualTo(("MS-1", "A-T5")));
            Assert.That(addition.Kind, Is.EqualTo("Milestone addition"));
            Assert.That(addition, Has.Property(nameof(InboxRowViewModel.CanApprove)).False
                .And.Property(nameof(InboxRowViewModel.CanDelegate)).False
                .And.Property(nameof(InboxRowViewModel.IsTicket)).False);
            Assert.That(growth, Has.Property(nameof(InboxRowViewModel.CanAnswerAddition)).False
                .And.Property(nameof(InboxRowViewModel.CanApprove)).False
                .And.Property(nameof(InboxRowViewModel.CanRelease)).False
                .And.Property(nameof(InboxRowViewModel.CanDelegate)).False);
            Assert.That(growth.Action, Is.EqualTo("orchestrator milestone list"));
        });
    }
}
