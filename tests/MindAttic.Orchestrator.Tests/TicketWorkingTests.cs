using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Tickets;
using MindAttic.Tickets.Data;
using MindAttic.Tickets.Log;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T766: who works each ticket is recorded in the ticket database through the engine, so Tickets Desktop links it
/// back to the Orchestrator: set on a claim, kept in step with the assignments, cleared once nobody works it.</summary>
public sealed class TicketWorkingTests
{
    private static readonly DateTime Now = new(2026, 10, 23, 6, 0, 0, DateTimeKind.Utc);
    private string dir = "";
    private string repo = "";
    private JsonStore store = null!;

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "orch-working-" + Guid.NewGuid().ToString("N"));
        repo = Path.Combine(dir, "A");
        Directory.CreateDirectory(Path.Combine(repo, "docs"));
        File.WriteAllText(Path.Combine(repo, "docs", "USER_STORIES.md"),
            "---\ncode: A\n---\n\n# A\n\n## Tickets\n\n| ID | Date | Type | Status | Summary | Ref |\n|---|---|---|---|---|---|\n" +
            "| A-T1 | 2026-10-01 | fix | open | x | — |\n| A-T2 | 2026-10-01 | fix | open | y | — |\n");
        using (var db = Open()) db.Import([new MindAttic.Tickets.Repo("A", repo)]);
        store = new JsonStore(Path.Combine(dir, "store"));
    }

    [TearDown]
    public void TearDown()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(dir, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private TicketsDb Open() => TicketsDb.Open(TicketsDb.DefaultPath(dir));

    private static Ticket T(string id) => new(id, "2026-10-01", "fix", "open", $"summary of {id}", "");

    private Snapshot Snap() => new(Now, [new RepoStatus("A", repo, RepoState.Dormant, [], [], [T("A-T1"), T("A-T2")], null, default)], []);

    [Test]
    public void A_claim_records_its_login_and_session_and_the_sync_clears_it_once_released()
    {
        var plan = Scheduler.Plan(Snap(), new(), []);
        var settings = new OrchestratorSettings { WorktreeRoot = Path.Combine(dir, "wt") };
        (bool, string, string) Make(string r, string path, string branch) => (true, path, "");

        var (handed, _) = Allocate.Next(plan, settings, store, ["A"], 1, claim: true, Now, Make, _ => "main", pilotRunning: _ => false,
            self: "sess-1", login: "Ryan@Example.com");

        Assert.That(handed.Select(h => h.Item.Id), Is.EqualTo(new[] { "A-T1" }));
        using (var db = Open())
            Assert.That((db.Work("A-T1").Login, db.Work("A-T1").Session), Is.EqualTo(("ryan@example.com", "sess-1")), "recorded as it's claimed");

        // The daemon's sync with the claim still there changes nothing; released, it's cleared.
        var claimed = Scheduler.Plan(Snap(), new(), store.LoadAssignments());
        var (same, none) = TicketWorking.Sync(dir, claimed, store.LoadAssignments());
        Assert.That((same, none.Count), Is.EqualTo((0, 0)));
        store.SaveAssignments([]);
        var (changed, failed) = TicketWorking.Sync(dir, claimed, store.LoadAssignments());
        Assert.That((changed, failed.Count), Is.EqualTo((1, 0)));
        using (var db = Open()) Assert.That(db.Work("A-T1").IsWorking, Is.False, "released: nobody works it");
    }

    [Test]
    public void Wanted_takes_each_live_assignment_and_each_worked_session_but_not_a_stale_claim()
    {
        var claim = new Assignment("A-T1", "A", @"D:\WT\A\A-T1", "Claude", Now, true) { Account = "a@x.com", CliSession = "s-claim" };
        var tab = new Assignment("A-T2", "A", @"D:\WT\A\A-T2", "Claude", Now, true) { Account = "b@x.com" };
        var stale = new Assignment("A-T3", "A", @"D:\WT\A\A-T3", "Claude", Now, true) { Account = "c@x.com" };
        var plan = new SchedulePlan(
            [Item("A-T1", claim), Item("A-T2", tab), Item("A-T3", stale) with { StaleSinceUtc = Now }, Item("A-T4", null), Item("A-T5", null)],
            [], 1, 0, null, [])
        {
            Worked = [new WorkedTicket("A-T2", "s-tab", null, "prompt"), new WorkedTicket("A-T4", "s-b", null, "prompt"), new WorkedTicket("A-T4", "s-a", "agent", "worktree")],
        };

        var wanted = TicketWorking.Wanted(plan);

        Assert.That(wanted.Keys.Order(), Is.EqualTo(new[] { "A-T1", "A-T2", "A-T4" }), "the stale claim and the unworked ticket aren't anyone's");
        Assert.That(wanted["A-T1"], Is.EqualTo(new TicketWorking.Who("a@x.com", "s-claim")));
        Assert.That(wanted["A-T2"], Is.EqualTo(new TicketWorking.Who("b@x.com", "s-tab")), "a tab's session as the daemon sees it");
        Assert.That(wanted["A-T4"], Is.EqualTo(new TicketWorking.Who(null, "s-a")), "the same session every tick");

        // Read after the plan: a claim handed back since is nobody's, a stale one claimed again is the new claim's.
        var again = stale with { StartedUtc = Now.AddMinutes(5), Account = "d@x.com" };
        var now = TicketWorking.Wanted(plan, [tab, again]);
        Assert.That(now.Keys.Order(), Is.EqualTo(new[] { "A-T2", "A-T3", "A-T4" }));
        Assert.That(now["A-T3"].Login, Is.EqualTo("d@x.com"));
    }

    [Test]
    public void A_write_that_fails_is_said_and_the_rest_go_on_and_nothing_is_cleared_while_a_repo_is_unread()
    {
        using (var db = Open()) db.SetWorking("A-T2", "old@x.com", null);
        using (var db = Open())
        {
            var (changed, failed) = TicketWorking.Sync(db, new Dictionary<string, TicketWorking.Who>
            {
                ["NOPE-T1"] = new(null, "s"),
                ["A-T1"] = new("a@x.com", null),
            }, mayClear: false);
            Assert.That((changed, failed.Count), Is.EqualTo((1, 1)));
            Assert.That(failed[0], Does.Contain("NOPE-T1"));
            Assert.That(db.Work("A-T2").Login, Is.EqualTo("old@x.com"), "not cleared while it can't be known to be finished");
        }
        var plan = Scheduler.Plan(Snap(), new(), []) with { UnknownRepos = new HashSet<string> { "B" } };
        Assert.That(TicketWorking.Sync(dir, plan, []).Changed, Is.EqualTo(0), "a repo unread holds every clear");
        Assert.DoesNotThrow(() => TicketWorking.Set(Path.Combine(dir, "nowhere"), "A-T1", "a@x.com", null), "no database is nothing to do");
        Assert.DoesNotThrow(() => TicketWorking.Clear(dir, "NOPE-T9"), "an unknown ticket is nothing to do");
    }

    private QueueItem Item(string id, Assignment? a) => new("A", repo, T(id), 0, a, false);
}
