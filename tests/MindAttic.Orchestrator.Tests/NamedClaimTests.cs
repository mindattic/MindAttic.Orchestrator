using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using MindAttic.Tickets.Log;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T1696: a CLI claims a ticket it starts by hand (orchestrator claim), so /allocate skips it.</summary>
public sealed class NamedClaimTests
{
    private string dir = "";
    private JsonStore store = null!;
    private static readonly DateTime Now = new(2026, 11, 17, 6, 0, 0, DateTimeKind.Utc);
    private static readonly OrchestratorSettings Settings = new() { WorktreeRoot = @"D:\WT" };
    private const int Mine = 41, Other = 42;

    [SetUp]
    public void SetUp() => store = new JsonStore(Path.Combine(dir = Path.Combine(Path.GetTempPath(), "named-claim-" + Guid.NewGuid().ToString("N")), "store"));

    [TearDown]
    public void TearDown()
    {
        // M.ORCH-T1711: the store may never have written anything (or something else already reclaimed %TEMP%), so the folder can be missing.
        // M.ORCH-T1713: an Exists check alone still races a folder vanishing mid-delete; TempDirs.Delete re-checks and retries.
        // M.ORCH-T1716: same failure on every test in this class (seen on Claim_is_refused_when_a_running_background_agent_holds_it), so the one TearDown covers them all.
        Assert.That(TempDirs.Delete(dir), Is.True, "couldn't delete " + dir);
    }

    private NamedClaim.Result Claim(int? cli, string? session = "s", Func<int, bool>? running = null) =>
        NamedClaim.Claim(store, "A-T1", "A", @"D:\W\A", cli, session, "me@x.com", running ?? (_ => true), _ => false, Now);

    private static SchedulePlan PlanOf(IReadOnlyList<Assignment> claims)
    {
        var open = new[] { new Ticket("A-T1", "2026-10-01", "fix", "open", "s", ""), new Ticket("A-T2", "2026-10-01", "fix", "open", "s", "") };
        var snap = new Snapshot(Now, [new RepoStatus("A", @"D:\W\A", RepoState.Dormant, [], [], open, null, default)], [])
        {
            Utilization = new Utilization(Now.AddHours(-2), Now.AddHours(3), Now, 98, [], 30, 7, 65, TimeSpan.Zero),
        };
        return Scheduler.Plan(snap, Settings, claims) with
        {
            Pace = new Pace(PaceMode.Open, 6, 10_000, 0, 1_000, "5-hour", Now.AddHours(3), "On pace.") { SubagentAllowance = 8 },
        };
    }

    [Test]
    public void Claim_records_a_real_assignment_for_the_cli_and_session()
    {
        var r = Claim(Mine, "sess-1");

        Assert.That(r.Claimed, Is.True);
        var a = store.LoadAssignments().Single();
        Assert.That((a.TicketId, a.Repo, a.Directory, a.Cli, a.CliSession), Is.EqualTo(("A-T1", "A", @"D:\W\A", (int?)Mine, "sess-1")));
        Assert.That(a.Account, Is.EqualTo("me@x.com"));
    }

    [Test]
    public void Allocate_skips_a_claimed_ticket_and_hands_the_next()
    {
        Claim(Mine);
        var (handed, _) = Allocate.Next(PlanOf(store.LoadAssignments()), Settings, store, ["A"], null, claim: false, Now, cli: Other);

        Assert.That(handed.Select(h => h.Item.Id), Is.EqualTo(new[] { "A-T2" }));
    }

    [Test]
    public void Claim_is_refused_while_another_live_cli_holds_it_and_keeps_that_claim()
    {
        Claim(Other, "theirs");
        var r = Claim(Mine, "mine");

        Assert.That(r.Claimed, Is.False);
        Assert.That(r.Message, Does.Contain("CLI 42").And.Contain("still running"));
        Assert.That(store.LoadAssignments().Single().Cli, Is.EqualTo(Other));
    }

    [Test]
    public void Claim_takes_over_a_claim_whose_cli_is_gone()
    {
        Claim(Other, "theirs");
        var r = Claim(Mine, "mine", running: c => c != Other);

        Assert.That(r.Claimed, Is.True);
        Assert.That(store.LoadAssignments().Single().Cli, Is.EqualTo(Mine));
    }

    [Test]
    public void Claim_again_by_the_same_cli_is_fine_and_stays_one_assignment()
    {
        Claim(Mine);

        Assert.That(Claim(Mine).Claimed, Is.True);
        Assert.That(store.LoadAssignments(), Has.Count.EqualTo(1));
    }

    [Test]
    public void Reclaiming_its_own_claim_keeps_what_the_claim_already_carries()
    {
        store.UpdateAssignments(all => all.Add(new Assignment("A-T1", "A", @"D:\W\A", Providers.Claude, Now.AddHours(-1), true) { Cli = Mine, Model = "opus", Log = "x.log" }));

        Assert.That(Claim(Mine, "sess").Claimed, Is.True);
        var a = store.LoadAssignments().Single();
        Assert.That((a.Model, a.Log, a.CliSession, a.StartedUtc), Is.EqualTo(("opus", "x.log", "sess", Now.AddHours(-1))));
    }

    [Test]
    public void Claim_is_refused_when_a_running_background_agent_holds_it()
    {
        store.UpdateAssignments(all => all.Add(new Assignment("A-T1", "A", @"D:\W\A", Providers.Claude, Now, true) { Pilot = 999, PilotStartedUtc = Now, Log = "x.log" }));
        var r = NamedClaim.Claim(store, "A-T1", "A", @"D:\W\A", Mine, "s", null, _ => true, _ => true, Now);

        Assert.That(r.Claimed, Is.False);
        Assert.That(r.Message, Does.Contain("background agent"));
    }

    [Test]
    public void Claim_is_refused_for_an_assignment_of_unknown_owner_until_released()
    {
        store.UpdateAssignments(all => all.Add(new Assignment("A-T1", "A", @"D:\W\A", Providers.Claude, Now, true)));

        Assert.That(Claim(Mine).Message, Does.Contain("orchestrator release A-T1"));
    }

    [Test]
    public void Released_ticket_is_handed_out_again()
    {
        Claim(Mine);
        store.UpdateAssignments(all => all.RemoveAll(a => a.TicketId == "A-T1"));
        var (handed, _) = Allocate.Next(PlanOf(store.LoadAssignments()), Settings, store, ["A"], null, claim: false, Now, cli: Other);

        Assert.That(handed.Select(h => h.Item.Id), Is.EquivalentTo(new[] { "A-T1", "A-T2" }));
    }
}
