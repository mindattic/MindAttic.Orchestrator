using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Status;
using MindAttic.Orchestrator.Wpf.ViewModels;
using MindAttic.Tickets.Log;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The owner inbox (M.ORCH-T1689): what it lists from the snapshot, the plan and the state rows, and that a new item is
/// alerted once and never again.</summary>
public sealed class OwnerInboxTests
{
    private string dir = "";
    private JsonStore store = null!;
    private static readonly DateTime Now = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
    private const string Repo = @"D:\W\A";
    private static readonly OrchestratorSettings Settings = new() { StaleAfterHours = 2 };

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "owner-inbox-" + Guid.NewGuid().ToString("N"));
        store = new JsonStore(Path.Combine(dir, "store"));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    private static Ticket T(string id, string type = "feature") => new(id, "2026-11-10", type, "open", $"summary of {id}", "");

    private static Snapshot Snap(params Ticket[] open) =>
        new(Now, [new RepoStatus("A", Repo, RepoState.Dormant, [], [], open, null, default)], []);

    private static OwnerInboxInputs Inputs(Snapshot snapshot, IReadOnlyList<Assignment>? assignments = null, OrchestratorSettings? settings = null) =>
        new(snapshot, Scheduler.Plan(snapshot, settings ?? Settings, assignments ?? []), Now) { Settings = settings ?? Settings };

    private static OwnerInboxItem Item(string kind, string id) => new(kind, id, $"title of {id}", "A", Now, $"do {id}") { TicketId = kind == OwnerInbox.SignIn ? null : id };

    // ── The items ────────────────────────────────────────────────────────

    [Test]
    public void An_unapproved_non_fix_ticket_waits_for_approval_and_a_fix_or_an_approved_one_does_not()
    {
        var snap = Snap(T("A-T1"), T("A-T2", "fix"), T("A-T3")) with { Approved = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "A-T3" } };
        var items = OwnerInbox.Build(Inputs(snap));
        Assert.That(items.Select(i => i.Key), Is.EqualTo(new[] { "approval|A-T1" }));
        var item = items.Single();
        Assert.Multiple(() =>
        {
            Assert.That(item.Kind, Is.EqualTo(OwnerInbox.Approval));
            Assert.That(item.TicketId, Is.EqualTo("A-T1"));
            Assert.That(item.Repo, Is.EqualTo("A"));
            Assert.That(item.Title, Is.EqualTo("summary of A-T1"));
            Assert.That(item.Action, Is.EqualTo("tickets ticket approve A-T1"));
            Assert.That(item.SinceUtc, Is.Not.Null);
        });
    }

    [Test]
    public void A_ticket_items_assignee_comes_from_the_snapshot_for_the_delegate_picker()
    {
        var snap = Snap(T("A-T1"), T("A-T2")) with
        {
            Assignees = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["A-T1"] = "amy@example.com" },
        };
        var items = OwnerInbox.Build(Inputs(snap));
        Assert.Multiple(() =>
        {
            Assert.That(items.Single(i => i.Id == "A-T1").Assignee, Is.EqualTo("amy@example.com"));
            Assert.That(items.Single(i => i.Id == "A-T2").Assignee, Is.Null, "unassigned stays null, not empty");
        });
    }

    [Test]
    public void A_ticket_waiting_on_the_owner_is_listed_once_with_the_command_that_clears_it()
    {
        // Unapproved too: it is the owner's by its label first, not also an approval.
        var snap = Snap(T("A-T1"), T("A-T2", "fix")) with
        {
            OwnerWaiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "A-T1", "A-T2", "A-T9" },
            Names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["A-T2"] = "Crash on start" },
        };
        var items = OwnerInbox.Build(Inputs(snap));
        Assert.That(items.Select(i => i.Key), Is.EqualTo(new[] { "waiting-on-owner|A-T1", "waiting-on-owner|A-T2" }), "A-T9 isn't open: not listed");
        Assert.That(items[0].Action, Is.EqualTo("tickets ticket label remove A-T1 waiting-on-owner"));
        Assert.That(items[1].Title, Is.EqualTo("Crash on start"), "its friendly name over its summary");
    }

    [Test]
    public void A_stale_claim_is_the_owners_only_once_nobody_picked_it_up_for_another_stale_period()
    {
        var snap = Snap(T("A-T1", "fix"));
        // Quiet 3h with a 2h stale period: stale, back in the queue, but an /allocate CLI may still take it.
        Assignment claim(double hoursAgo) => new("A-T1", "A", Repo, Providers.Claude, Now.AddHours(-hoursAgo), true);
        var recent = Inputs(snap, [claim(3)]);
        Assert.That(recent.Plan.Stale, Has.Count.EqualTo(1));
        Assert.That(OwnerInbox.Build(recent), Is.Empty);

        var old = OwnerInbox.Build(Inputs(snap, [claim(5)]));
        Assert.That(old.Select(i => i.Key), Is.EqualTo(new[] { "stale|A-T1" }));
        Assert.That(old[0].Action, Is.EqualTo("orchestrator release A-T1"));
    }

    [Test]
    public void An_orphaned_ticket_is_listed_when_given_up_on_or_after_waiting_a_stale_period()
    {
        var snap = Snap(T("A-T1", "fix"), T("A-T2", "fix"), T("A-T3", "fix"));
        var inputs = Inputs(snap) with
        {
            Orphans =
            [
                new OrphanRecord("A-T1", "A", Repo, Now.AddMinutes(-10)) { State = OrphanRecord.NeedsYou, Why = "it went dark twice" },
                new OrphanRecord("A-T2", "A", Repo, Now.AddMinutes(-10)) { State = OrphanRecord.Orphaned, Why = "allocate is paused" },
                new OrphanRecord("A-T3", "A", Repo, Now.AddHours(-3)) { State = OrphanRecord.Orphaned, Why = "allocate is paused" },
                new OrphanRecord("A-T4", "A", Repo, Now.AddHours(-3)) { State = OrphanRecord.NeedsYou, Why = "closed since" },
                new OrphanRecord("A-T5", "A", Repo, Now.AddHours(-3)) { State = OrphanRecord.HandedBack, Why = "handed back" },
            ],
        };
        var items = OwnerInbox.Build(inputs);
        Assert.That(items.Select(i => i.Key), Is.EquivalentTo(new[] { "orphaned|A-T1", "orphaned|A-T3" }));
        Assert.That(items.Single(i => i.Id == "A-T1").Title, Does.Contain("it went dark twice"));
        Assert.That(items.Single(i => i.Id == "A-T1").Action, Is.EqualTo("orchestrator release A-T1"));
    }

    [Test]
    public void A_login_to_sign_in_again_and_a_live_consent_request_are_listed_without_a_ticket()
    {
        var inputs = Inputs(Snap()) with
        {
            SignInAgain = ["amy@example.com"],
            Consents =
            [
                new RemoteConsent { Id = "c1", Session = "s1", Viewer = "ryan@example.com", AskedUtc = Now.AddSeconds(-5), DeadlineUtc = Now.AddSeconds(30) },
                new RemoteConsent { Id = "c2", Session = "s2", Viewer = "ryan@example.com", AskedUtc = Now.AddMinutes(-5), DeadlineUtc = Now.AddSeconds(-1) },
            ],
        };
        var items = OwnerInbox.Build(inputs);
        Assert.That(items.Select(i => i.Key), Is.EqualTo(new[] { "sign-in|amy@example.com", "consent|c1" }), "a consent request past its deadline is gone");
        Assert.Multiple(() =>
        {
            Assert.That(items[0].Action, Is.EqualTo("orchestrator login --account amy@example.com"));
            Assert.That(items[0].TicketId, Is.Null);
            Assert.That(items[1].Action, Is.EqualTo("orchestrator machine consent c1 allow|deny"));
            Assert.That(items[1].TicketId, Is.Null);
        });
    }

    [Test]
    public void Providers_are_a_list_a_new_kind_joins_and_one_that_throws_adds_nothing()
    {
        var inputs = Inputs(Snap(T("A-T1")));
        OwnerInbox.Provider milestone = _ => [new OwnerInboxItem("milestone", "M1", "Accept milestone M1", null, Now, "orchestrator milestone accept M1")];
        OwnerInbox.Provider broken = _ => throw new InvalidOperationException("boom");
        OwnerInbox.Provider again = _ => [Item(OwnerInbox.Stale, "A-T1")];
        var items = OwnerInbox.Build(inputs, [.. OwnerInbox.Providers, broken, milestone, again]);
        Assert.That(items.Select(i => i.Key), Is.EqualTo(new[] { "approval|A-T1", "milestone|M1" }), "a ticket is listed once, by the first provider that names it");
    }

    [Test]
    public void Read_loads_the_orphaned_tickets_row_and_never_throws_on_an_empty_store()
    {
        store.Save(OrphanWatch.StoreName, new List<OrphanRecord> { new("A-T1", "A", Repo, Now) { State = OrphanRecord.NeedsYou, Why = "needs you" } });
        var snap = Snap(T("A-T1", "fix"));
        var inputs = OwnerInbox.Read(store, snap, Scheduler.Plan(snap, Settings, []), Settings, Now);
        Assert.That(inputs.Orphans.Select(o => o.TicketId), Is.EqualTo(new[] { "A-T1" }));
        Assert.That(inputs.SignInAgain, Is.Empty);
        Assert.That(OwnerInbox.Build(inputs).Select(i => i.Key), Is.EqualTo(new[] { "orphaned|A-T1" }));
    }

    // ── The alert, once per item ─────────────────────────────────────────

    [Test]
    public void The_first_look_notes_what_already_waits_and_alerts_nothing()
    {
        var alerts = new OwnerInboxAlerts(store);
        Assert.That(alerts.Fresh([Item(OwnerInbox.Approval, "A-T1"), Item(OwnerInbox.Approval, "A-T2")], Now), Is.Empty);
        Assert.That(alerts.Fresh([Item(OwnerInbox.Approval, "A-T1"), Item(OwnerInbox.Approval, "A-T2")], Now.AddMinutes(1)), Is.Empty);
        Assert.That(store.Load<OwnerInboxAlerts.Alerted>(OwnerInboxAlerts.StateName).Keys.Keys, Is.EquivalentTo(new[] { "approval|A-T1", "approval|A-T2" }));
    }

    [Test]
    public void A_new_item_is_alerted_once_and_never_again_even_after_a_restart_or_after_it_leaves_and_returns()
    {
        var alerts = new OwnerInboxAlerts(store);
        alerts.Fresh([], Now); // the first look
        var one = Item(OwnerInbox.Approval, "A-T1");
        var two = Item(OwnerInbox.Stale, "A-T2");

        Assert.That(alerts.Fresh([one], Now.AddMinutes(1)).Select(i => i.Key), Is.EqualTo(new[] { "approval|A-T1" }));
        Assert.That(alerts.Fresh([one], Now.AddMinutes(2)), Is.Empty, "the same item: not again");
        Assert.That(alerts.Fresh([one, two], Now.AddMinutes(3)).Select(i => i.Key), Is.EqualTo(new[] { "stale|A-T2" }), "only the new one");

        // A restart, or another daemon: the row remembers.
        var restarted = new OwnerInboxAlerts(store);
        Assert.That(restarted.Fresh([one, two], Now.AddMinutes(4)), Is.Empty);

        // It leaves the inbox and comes back (a label removed and put back, a ticket database unreadable for a tick).
        Assert.That(restarted.Fresh([two], Now.AddMinutes(5)), Is.Empty);
        Assert.That(restarted.Fresh([one, two], Now.AddMinutes(6)), Is.Empty, "the same item is never alerted twice");

        // The same ticket under another kind is another item.
        Assert.That(restarted.Fresh([Item(OwnerInbox.Orphaned, "A-T1"), two], Now.AddMinutes(7)).Select(i => i.Key), Is.EqualTo(new[] { "orphaned|A-T1" }));
    }

    [Test]
    public void An_item_gone_longer_than_it_is_remembered_is_forgotten()
    {
        var alerts = new OwnerInboxAlerts(store);
        alerts.Fresh([], Now);
        var one = Item(OwnerInbox.Approval, "A-T1");
        Assert.That(alerts.Fresh([one], Now.AddMinutes(1)), Has.Count.EqualTo(1));
        var later = Now + OwnerInboxAlerts.Remembered + TimeSpan.FromDays(1);
        Assert.That(alerts.Fresh([], later), Is.Empty);
        Assert.That(store.Load<OwnerInboxAlerts.Alerted>(OwnerInboxAlerts.StateName).Keys, Is.Empty, "the row doesn't grow for good");
        // One still waiting is kept however old it is.
        Assert.That(alerts.Fresh([one], later.AddMinutes(1)), Has.Count.EqualTo(1));
        Assert.That(alerts.Fresh([one], later + OwnerInboxAlerts.Remembered + TimeSpan.FromDays(2)), Is.Empty);
    }

    [Test]
    public void The_text_names_one_item_with_its_command_and_counts_past_three()
    {
        var one = OwnerInboxAlerts.Text([Item(OwnerInbox.Approval, "A-T1")]);
        Assert.That(one, Is.EqualTo("Orchestrator inbox: Approve A-T1 (title of A-T1). do A-T1"));

        var many = OwnerInboxAlerts.Text([.. Enumerable.Range(1, 5).Select(n => Item(OwnerInbox.Approval, $"A-T{n}"))]);
        Assert.That(many, Does.StartWith("Orchestrator inbox: 5 new items wait on you: Approve A-T1 (title of A-T1); "));
        Assert.That(many, Does.Contain("A-T3").And.Not.Contain("A-T4"));
        Assert.That(many, Does.EndWith("(+2 more). orchestrator inbox lists them."));
    }

    [Test]
    public void The_daemon_texts_nothing_with_inbox_alerts_off_or_nobody_to_text_and_still_notes_the_item()
    {
        var daemon = new Daemon(store);
        var empty = Snap();
        var plan = Scheduler.Plan(empty, Settings, []);
        Assert.That(daemon.ReportInbox(empty, plan, Settings, Now), Is.Null, "the first look");

        var snap = Snap(T("A-T1"));
        var off = Settings with { InboxAlerts = false };
        Assert.That(daemon.ReportInbox(snap, Scheduler.Plan(snap, off, []), off, Now + Daemon.InboxEvery), Is.Null);
        Assert.That(store.Load<OwnerInboxAlerts.Alerted>(OwnerInboxAlerts.StateName).Keys.Keys, Does.Contain("approval|A-T1"), "seen, so turning alerts on later texts no backlog");

        var nobody = Settings with { PsstAlertTo = "" };
        var more = Snap(T("A-T1"), T("A-T2"));
        Assert.That(daemon.ReportInbox(more, Scheduler.Plan(more, nobody, []), nobody, Now + Daemon.InboxEvery + Daemon.InboxEvery), Is.Null);
        Assert.That(new OwnerInboxAlerts(store).Fresh(OwnerInbox.Build(Inputs(more)), Now.AddHours(1)), Is.Empty);
    }

    // ── The dashboard's button ───────────────────────────────────────────

    [Test]
    public void The_dashboard_badge_and_tooltip_follow_the_count()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MindAttic.Orchestrator.Wpf.ViewModels.MainViewModel.BadgeOf(0), Is.Empty);
            Assert.That(MindAttic.Orchestrator.Wpf.ViewModels.MainViewModel.BadgeOf(7), Is.EqualTo("7"));
            Assert.That(MindAttic.Orchestrator.Wpf.ViewModels.MainViewModel.BadgeOf(120), Is.EqualTo("99+"));
            Assert.That(MindAttic.Orchestrator.Wpf.ViewModels.MainViewModel.InboxLine(0), Is.EqualTo("Inbox: nothing is waiting on you"));
            Assert.That(MindAttic.Orchestrator.Wpf.ViewModels.MainViewModel.InboxLine(1), Is.EqualTo("Inbox: 1 item is waiting on you"));
            Assert.That(MindAttic.Orchestrator.Wpf.ViewModels.MainViewModel.InboxLine(4), Is.EqualTo("Inbox: 4 items are waiting on you"));
        });
        var row = new MindAttic.Orchestrator.Wpf.ViewModels.InboxRowViewModel(new OwnerInboxItem(OwnerInbox.SignIn, "amy@example.com", "must sign in again", null, null, "orchestrator login"), Now);
        Assert.That(row.IsTicket, Is.False);
        Assert.That(row.Where, Is.Empty);
        Assert.That(row.Kind, Is.EqualTo("Sign in"));
    }

    [Test]
    public void Only_its_own_kind_of_row_can_approve_clear_release_or_work_it_now_but_any_ticket_can_be_delegated()
    {
        Assert.Multiple(() =>
        {
            var approval = Row(OwnerInbox.Approval, "A-T1");
            Assert.That(approval, Has.Property(nameof(InboxRowViewModel.CanApprove)).True
                .And.Property(nameof(InboxRowViewModel.CanWorkNow)).True
                .And.Property(nameof(InboxRowViewModel.CanDelegate)).True
                .And.Property(nameof(InboxRowViewModel.CanClearWaiting)).False
                .And.Property(nameof(InboxRowViewModel.CanRelease)).False);

            var waiting = Row(OwnerInbox.WaitingOnOwner, "A-T2");
            Assert.That(waiting, Has.Property(nameof(InboxRowViewModel.CanClearWaiting)).True
                .And.Property(nameof(InboxRowViewModel.CanWorkNow)).True
                .And.Property(nameof(InboxRowViewModel.CanApprove)).False);

            var stale = Row(OwnerInbox.Stale, "A-T3");
            Assert.That(stale, Has.Property(nameof(InboxRowViewModel.CanRelease)).True
                .And.Property(nameof(InboxRowViewModel.CanWorkNow)).False
                .And.Property(nameof(InboxRowViewModel.CanDelegate)).True);

            var orphaned = Row(OwnerInbox.Orphaned, "A-T4");
            Assert.That(orphaned, Has.Property(nameof(InboxRowViewModel.CanRelease)).True);

            var signIn = new InboxRowViewModel(Item(OwnerInbox.SignIn, "amy@example.com"), Now);
            Assert.That(signIn, Has.Property(nameof(InboxRowViewModel.CanDelegate)).False
                .And.Property(nameof(InboxRowViewModel.CanApprove)).False
                .And.Property(nameof(InboxRowViewModel.CanRelease)).False);
        });
        return;

        static InboxRowViewModel Row(string kind, string id) => new(Item(kind, id), Now);
    }

    [Test]
    public void The_delegate_picker_starts_on_whoever_already_has_it_else_unassigned()
    {
        var nobody = new PersonOptionViewModel(null);
        var amy = new PersonOptionViewModel("amy@example.com");
        var people = new List<PersonOptionViewModel> { nobody, amy };

        var assigned = new InboxRowViewModel(Item(OwnerInbox.Approval, "A-T1") with { Assignee = "amy@example.com" }, Now, people);
        Assert.That(assigned.Assignee, Is.SameAs(amy));

        var unassigned = new InboxRowViewModel(Item(OwnerInbox.Approval, "A-T2"), Now, people);
        Assert.That(unassigned.Assignee, Is.SameAs(nobody));
    }
}
