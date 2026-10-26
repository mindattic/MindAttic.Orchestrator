using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using MindAttic.Tickets.Log;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// Tickets per user (M.ORCH-T668): a ticket assigned to a user (in the ticket database, where Tickets assigns it, M.ORCH-T762) is
/// pinned to their login: next --claim and Start dispatch it only there (and the claim or tab records it), it waits for its user
/// instead of being handed on, and once the pin is dropped (Tickets' redelegate) it's handed on like an unassigned ticket.
/// </summary>
public sealed class TicketPinsTests
{
    private const string Ryan = "ryan@example.com", Jordan = "jordan@example.com";
    private static readonly DateTime Now = new(2026, 10, 23, 15, 0, 0, DateTimeKind.Utc);
    private string dir = "";
    private JsonStore store = null!;

    /// <summary>The ticket database's assignments as the snapshot carries them (ticket id → login email).</summary>
    private Dictionary<string, string> assignees = new(StringComparer.OrdinalIgnoreCase);

    [SetUp]
    public void SetUp()
    {
        assignees = new(StringComparer.OrdinalIgnoreCase);
        dir = Path.Combine(Path.GetTempPath(), "orch-pins-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        store = new JsonStore(Path.Combine(dir, "store"));
        store.SaveSettings(new OrchestratorSettings { WorktreeRoot = Path.Combine(dir, "wt") });
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(dir, true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    [Test]
    public void The_ticket_databases_assignment_pins_a_ticket_to_its_users_login()
    {
        assignees[" a-t1 "] = "  Ryan@Example.com ";
        Assert.That(TicketPins.Assignees(Snap()), Is.EqualTo(new Dictionary<string, string> { ["a-t1"] = Ryan }), "the login as its folder names it");
        Assert.That(Plan().Queue.Single(q => q.Id == "A-T1").Assignee, Is.EqualTo(Ryan), "the queue says who has it, whatever case its id is in");

        assignees.Clear();
        assignees["A-T1"] = Jordan;
        Assert.That(Plan().Queue.Single(q => q.Id == "A-T1").Assignee, Is.EqualTo(Jordan), "reassigned in Tickets: the next plan follows");

        assignees["A-T1"] = "not an email";
        Assert.That(Plan().Queue.Single(q => q.Id == "A-T1").IsPinned, Is.False, "an assignee that isn't a login pins nothing");
        assignees.Clear();
        Assert.That(Plan().Queue.Single(q => q.Id == "A-T1").IsPinned, Is.False);
        Assert.That(store.LoadSettings().Assignees, Is.Empty, "the orchestrator's settings hold no assignment");
    }

    [Test]
    public void Next_claim_hands_a_pinned_ticket_only_to_a_cli_on_its_users_login_and_records_it()
    {
        assignees["A-T1"] = Ryan;
        var settings = store.LoadSettings();

        var (jordans, why) = Allocate.Next(Plan(), settings, store, ["A"], null, claim: true, Now, Make, _ => "main", login: Jordan);
        Assert.That(jordans.Select(h => h.Item.Id), Is.EqualTo(new[] { "A-T2" }), "Jordan's CLI gets the unassigned one, not Ryan's");
        Assert.That(why, Is.Null);

        var (unknown, whyUnknown) = Allocate.Next(Plan(), settings, store, ["A"], null, claim: false, Now, Make, _ => "main");
        Assert.That(unknown, Is.Empty, "a CLI whose login isn't known takes no pinned ticket (A-T2 is claimed)");
        Assert.That(whyUnknown, Does.Contain("already being worked"));

        var (ryans, _) = Allocate.Next(Plan(), settings, store, ["A"], null, claim: true, Now, Make, _ => "main", login: "RYAN@example.com");
        Assert.That(ryans.Select(h => h.Item.Id), Is.EqualTo(new[] { "A-T1" }));
        var claims = store.LoadAssignments().ToDictionary(a => a.TicketId);
        Assert.That((claims["A-T1"].Account, claims["A-T2"].Account), Is.EqualTo(((string?)Ryan, (string?)Jordan)),
            "each claim records the login it was handed to, and the column keeps it");
    }

    [Test]
    public void Start_opens_a_pinned_ticket_only_on_its_users_login()
    {
        assignees["A-T1"] = Ryan;
        var item = Plan().Queue.Single(q => q.Id == "A-T1");
        var opened = 0;
        var dispatcher = Dispatcher(() => opened++, who => who == Ryan ? new Dispatcher.TabLogin(@"C:\accounts\ryan", Ryan) : null);

        var elsewhere = dispatcher.Dispatch(item, Providers.Claude, useWorktree: false, login: new(@"C:\accounts\jordan", Jordan));
        Assert.That(elsewhere.Ok, Is.False);
        Assert.That(elsewhere.Message, Does.Contain("assigned to ryan@example.com").And.Contain("only on ryan@example.com's login"));
        Assert.That(Dispatcher(() => opened++, _ => null).Dispatch(item, "Codex", false).Ok, Is.False, "not on another provider either");
        Assert.That(opened, Is.Zero);

        var started = dispatcher.Dispatch(item, Providers.Claude, useWorktree: false);
        Assert.That(started.Ok, Is.True, started.Message);
        Assert.That(opened, Is.EqualTo(1));
        Assert.That(store.LoadAssignments().Single().Account, Is.EqualTo(Ryan), "nothing chosen: it opens on Ryan's login, and says so");

        var unpinned = Plan().Queue.Single(q => q.Id == "A-T2");
        Assert.That(dispatcher.Dispatch(unpinned, Providers.Claude, false, login: new(null, Jordan)).Ok, Is.True);
        Assert.That(store.LoadAssignments().Single(a => a.TicketId == "A-T2").Account, Is.EqualTo(Jordan), "any tab records its login");
    }

    [Test]
    public void A_pinned_ticket_whose_user_is_out_of_room_waits_for_them_instead_of_being_redelegated()
    {
        assignees["A-T1"] = Ryan;
        var item = Plan().Queue.Single(q => q.Id == "A-T1");

        // Ryan's week is past its stop line (95%): the card says it waits, and why.
        var why = TicketPins.OutOfRoom(signedIn: true, fiveHourUsedPercent: 40, weekUsedPercent: 97.4, fiveHourStopPercent: 98, weeklyStopPercent: 95);
        Assert.That(TicketPins.WaitingText("Ryan", why!), Is.EqualTo("Waiting for Ryan: week at 97%"));
        Assert.That(TicketPins.OutOfRoom(true, 98.2, 50, 98, 95), Is.EqualTo("5-hour window at 98%"));
        Assert.That(TicketPins.OutOfRoom(false, null, null, 98, 95), Is.EqualTo("not signed in on this machine"));
        Assert.That(TicketPins.OutOfRoom(true, 60, 80, 98, 95), Is.Null, "room left: nothing to wait for");

        // The pacing's redelegation (M.ORCH-T666) leaves it alone; an unassigned ticket it may move.
        Assert.That(TicketPins.Redelegable(item), Is.False);
        Assert.That(TicketPins.Redelegable(Plan().Queue.Single(q => q.Id == "A-T2")), Is.True);

        // Jordan, with room, isn't handed it: it waits for Ryan.
        var (handed, whyNot) = Allocate.Next(Plan(only: "A-T1"), store.LoadSettings(), store, ["A"], null, claim: true, Now, Make, _ => "main", login: Jordan);
        Assert.That(handed, Is.Empty);
        Assert.That(whyNot, Does.Contain("1 assigned to other users wait for them").And.Contain(Ryan));
        Assert.That(store.LoadAssignments(), Is.Empty, "nothing claimed");
    }

    [Test]
    public void Once_the_pin_is_dropped_in_Tickets_a_ticket_is_handed_on_like_an_unassigned_one()
    {
        assignees["A-T1"] = Ryan;
        assignees.Remove("A-T1"); // tickets ticket assign A-T1 --redelegate, or Tickets Desktop's Redelegate now

        var item = Plan(only: "A-T1").Queue.Single();
        Assert.That((item.IsPinned, TicketPins.Redelegable(item)), Is.EqualTo((false, true)), "the pacing may move it again");
        var (handed, _) = Allocate.Next(Plan(only: "A-T1"), store.LoadSettings(), store, ["A"], null, claim: true, Now, Make, _ => "main", login: Jordan);
        Assert.That(handed.Select(h => h.Item.Id), Is.EqualTo(new[] { "A-T1" }), "another user with room takes it");
        Assert.That(store.LoadAssignments().Single().Account, Is.EqualTo(Jordan));
    }

    [Test]
    public void Each_persons_tickets_view_sits_below_their_open_clis_and_assigns_in_Tickets_Desktop()
    {
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        var window = File.ReadAllText(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        Assert.That(window.IndexOf("x:Name=\"UserTickets\"", StringComparison.Ordinal),
            Is.GreaterThan(window.IndexOf("x:Name=\"TerminalFrame\"", StringComparison.Ordinal)), "below the person's CLIs");
        Assert.That(window, Does.Contain("Style=\"{StaticResource MaRowList}\" ItemsSource=\"{Binding AssignedTickets}\"")
            .And.Contain("ItemsSource=\"{Binding UnassignedTickets}\"").And.Contain("x:Name=\"AssignInTickets\""));
        // M.ORCH-T762: assigning, reassigning and redelegating are Tickets Desktop's; the dashboard only links there.
        Assert.That(window, Does.Not.Contain("AssigneePicker").And.Not.Contain("RedelegateCommand").And.Not.Contain("UpCommand"));
    }

    private Snapshot Snap(string? only = null)
    {
        Ticket[] open = [T("A-T1"), T("A-T2")];
        return new Snapshot(Now, [new RepoStatus("A", dir, RepoState.Dormant, [], [], [.. open.Where(t => only is null || t.Id == only)], null, default)], [])
        {
            Assignees = new Dictionary<string, string>(assignees, StringComparer.OrdinalIgnoreCase),
        };
    }

    private SchedulePlan Plan(string? only = null)
    {
        var plan = Scheduler.Plan(Snap(only), store.LoadSettings(), store.LoadAssignments());
        return plan with { Pace = new Pace(PaceMode.Open, 6, 10_000, 0, 1_000, "5-hour", Now.AddHours(3), "On pace.") { SpareSessions = 5, SubagentAllowance = 8 } };
    }

    private static Ticket T(string id) => new(id, "2026-10-01", "fix", "open", $"summary of {id}", "");

    private static (bool, string, string) Make(string repo, string path, string branch) => (true, path, "");

    private Dispatcher Dispatcher(Action opened, Func<string, Dispatcher.TabLogin?> pinned) =>
        new(new SessionControl(new OrchestratorOptions { LauncherExe = Path.Combine(dir, "no-launcher.exe") }), store)
        {
            OpenTab = (_, _, _, _, _) => { opened(); return (null, Task.FromResult(true)); },
            PinnedLogin = pinned,
        };
}
