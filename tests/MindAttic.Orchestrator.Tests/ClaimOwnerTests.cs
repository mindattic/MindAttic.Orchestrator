using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using MindAttic.Tickets.Log;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// M.ORCH-T532: a claim records the CLI that made it, so with two /allocate CLIs on one repo the idle-claim nudge, the fair
/// share and the agent board each give a CLI only its own claims (T529's nudge prompted the repo's owner for the other's).
/// </summary>
public sealed class ClaimOwnerTests
{
    private string dir = "";
    private JsonStore store = null!;
    private ContextCycler cycler = null!;
    private static readonly DateTime Now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly OrchestratorSettings Settings = new() { WorktreeRoot = @"D:\WT" };
    private const int First = 41, Second = 42;

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "claim-owner-" + Guid.NewGuid().ToString("N"));
        store = new JsonStore(Path.Combine(dir, "store"));
        cycler = new ContextCycler(Path.Combine(dir, "session-context"));
        Directory.CreateDirectory(cycler.Directory);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(dir, recursive: true);

    private static Ticket T(string id) => new(id, "2026-10-01", "fix", "open", $"summary of {id}", "");

    private static Assignment Claim(string id, int? cli, TimeSpan ago, string? session = null) =>
        new(id, "A", $@"D:\WT\A\{id}", Providers.Claude, Now - ago, true) { Cli = cli, CliSession = session };

    private static Snapshot Snap(params Ticket[] open) =>
        new(Now, [new RepoStatus("A", @"D:\W\A", RepoState.Dormant, [], [], open, null, default)], [])
        {
            Utilization = new Utilization(Now.AddHours(-2), Now.AddHours(3), Now, 98, [], 30, 7, 65, TimeSpan.Zero),
        };

    private static SchedulePlan Plan(Snapshot snap, IReadOnlyList<Assignment> claims, int allowance = 8, int running = 0,
        Dictionary<int, int>? byCli = null) =>
        Scheduler.Plan(snap, Settings, claims) with
        {
            Pace = new Pace(PaceMode.Open, 6, 10_000, 0, 1_000, "5-hour", Now.AddHours(3), "On pace.")
            {
                SubagentAllowance = allowance, Subagents = running, SubagentsByCli = byCli ?? [],
            },
        };

    [Test]
    public void Two_allocate_clis_on_one_repo_are_each_nudged_only_for_their_own_idle_claims()
    {
        var waker = new AllocateWaker(cycler, store)
        {
            IsAlive = _ => true,
            // Both on A; the first names it outright, so before T532 it was told of every claim on A.
            Pilots = () => new Dictionary<int, IReadOnlyList<string>> { [First] = ["A"], [Second] = ["A"] },
        };
        var snap = Snap(T("A-T1"), T("A-T2"), T("A-T3"));
        var plan = Plan(snap, [Claim("A-T1", First, TimeSpan.FromMinutes(10)), Claim("A-T2", Second, TimeSpan.FromMinutes(10)),
            Claim("A-T3", Second, TimeSpan.FromMinutes(10))], running: 2);

        var wakes = waker.TickClaims(snap, plan, Settings, Now);

        Assert.That(wakes.Single(w => w.Pid == First).Tickets, Is.EqualTo(new[] { "A-T1" }));
        Assert.That(wakes.Single(w => w.Pid == Second).Tickets, Is.EqualTo(new[] { "A-T3", "A-T2" }), "in queue order: newest first (M.ORCH-T863)");
        string Nudge(int pid) => File.ReadAllText(Path.Combine(cycler.Directory, $"nudge-{pid}-claims.txt"));
        Assert.That(Nudge(First), Does.Contain("A-T1").And.Not.Contain("A-T2").And.Not.Contain("A-T3"));
        Assert.That(Nudge(Second), Does.Contain("A-T2").And.Contain("A-T3").And.Not.Contain("A-T1"));
    }

    [Test]
    public void A_claim_whose_cli_is_unknown_or_gone_still_goes_to_the_cli_working_its_repo()
    {
        var pilots = new Dictionary<int, IReadOnlyList<string>> { [Second] = ["A"] };
        var snap = Snap(T("A-T1"), T("A-T2"));
        var plan = Plan(snap, [Claim("A-T1", null, TimeSpan.FromMinutes(10)), Claim("A-T2", First, TimeSpan.FromMinutes(10))]);

        var idle = AllocateWaker.IdleClaims(snap, plan, pilots, Now);

        Assert.That(idle.Select(i => (i.Cli, i.Item.Id)), Is.EqualTo(new[] { (Second, "A-T2"), (Second, "A-T1") }),
            "made before claims recorded it, or by a CLI no longer running /allocate: left to the one that works A");
    }

    [Test]
    public void Next_claim_records_the_claiming_cli_and_its_session()
    {
        var snap = Snap(T("A-T1"));
        var (handed, why) = Allocate.Next(Plan(snap, []), Settings, store, ["A"], 1, claim: true, Now,
            _ => "main", stillOpen: (_, _) => true, self: "sess-41", cli: First);

        Assert.That(why, Is.Null);
        Assert.That(handed.Single().Item.Id, Is.EqualTo("A-T1"));
        var a = store.LoadAssignments().Single();
        Assert.That((a.Cli, a.CliSession), Is.EqualTo(((int?)First, "sess-41")), "kept in orchestrator.db's assignments");
    }

    [Test]
    public void A_clis_unstarted_claims_count_against_its_own_fair_share_not_the_other_clis()
    {
        var snap = Snap([.. Enumerable.Range(1, 10).Select(i => T($"A-T{i}"))]);
        var pilots = new Dictionary<int, IReadOnlyList<string>> { [First] = ["A"], [Second] = ["A"] };
        // The second CLI claimed three with no subagent on them yet; nothing is running.
        var claims = new[] { Claim("A-T1", Second, TimeSpan.FromMinutes(1)), Claim("A-T2", Second, TimeSpan.FromMinutes(1)),
            Claim("A-T3", Second, TimeSpan.FromMinutes(1)) };
        var plan = Plan(snap, claims);

        Assert.That(Allocate.PendingClaims(plan), Is.EqualTo(new Dictionary<int, int> { [Second] = 3 }));
        var first = Allocate.Next(plan, Settings, store, ["A"], null, claim: false, Now, cli: First, pilots: pilots).Handed;
        Assert.That(first, Has.Count.EqualTo(4), "the first CLI's share of 8 between two is untouched by the second's claims");
        var second = Allocate.Next(plan, Settings, store, ["A"], null, claim: false, Now, cli: Second, pilots: pilots).Handed;
        Assert.That(second, Has.Count.EqualTo(1), "the second has 3 of its 4 claimed already");

        // Claims of a CLI not known count for nobody.
        var legacy = Plan(snap, [.. claims.Select(c => c with { Cli = null })]);
        Assert.That(Allocate.PendingClaims(legacy), Is.Empty);
    }

    [Test]
    public void A_closed_clis_unstarted_claims_hold_no_place_and_go_to_the_cli_working_their_repo()
    {
        // M.ORCH-T550: a claim counted as pending until StaleAfterHours, so a closed CLI's three unstarted claims kept three of
        // eight places from every CLI for up to 12 hours.
        var snap = Snap([.. Enumerable.Range(1, 10).Select(i => T($"A-T{i}"))]);
        var claims = new[] { Claim("A-T1", Second, TimeSpan.FromMinutes(10)), Claim("A-T2", Second, TimeSpan.FromMinutes(10)),
            Claim("A-T3", Second, TimeSpan.FromMinutes(10)), Claim("A-T4", null, TimeSpan.FromMinutes(10)) };
        var plan = Plan(snap, claims);
        var pilots = new Dictionary<int, IReadOnlyList<string>> { [First] = ["A"] }; // the second CLI has closed

        Assert.That(Allocate.PendingClaims(plan, c => c == First), Is.Empty, "its claimer isn't running; an unknown claimer counts for nobody, as before");
        Assert.That(Allocate.PendingClaims(plan, _ => true), Is.EqualTo(new Dictionary<int, int> { [Second] = 3 }), "still running: pending");
        var handed = Allocate.Next(plan, Settings, store, ["A"], null, claim: false, Now, cli: First, pilots: pilots, cliRunning: c => c == First).Handed;
        Assert.That(handed, Has.Count.EqualTo(6), "every free ticket: the dead claims don't take 3 of the 8 places");
        Assert.That(AllocateWaker.IdleClaims(snap, plan, pilots, Now).Select(i => (i.Cli, i.Item.Id)),
            Is.EqualTo(new[] { (First, "A-T4"), (First, "A-T3"), (First, "A-T2"), (First, "A-T1") }), "offered to the CLI that works A, newest first");
    }

    [Test]
    public void The_agent_board_lists_each_cli_only_its_own_claims()
    {
        TranscriptSession Session(string id) => new(id, @"D:\W\A", Now.AddMinutes(-1), default, "claude-sonnet-5");
        var repos = new[] { new RepoStatus("A", @"D:\W\A", RepoState.Working, [], [Session("s41"), Session("s42"), Session("s43")], [], Now, default) };
        var assignments = new[]
        {
            Claim("A-T1", First, TimeSpan.FromMinutes(1), "s41-before-clear"),
            Claim("A-T2", Second, TimeSpan.FromMinutes(1)),
            Claim("A-T3", null, TimeSpan.FromMinutes(1), "s43"),
            Claim("A-T4", null, TimeSpan.FromMinutes(1)),
        };
        var clis = new Dictionary<string, int> { ["s41"] = First, ["s42"] = Second };

        var rows = AgentBoard.Build(repos, null, assignments, [], TimeSpan.FromMinutes(10), Now, clis: clis).ToDictionary(r => r.SessionId);

        Assert.That(rows["s41"].Claims, Is.EqualTo(new[] { "A-T1" }), "by its CLI process, though claimed in its session before a /clear");
        Assert.That(rows["s42"].Claims, Is.EqualTo(new[] { "A-T2" }));
        Assert.That(rows["s43"].Claims, Is.EqualTo(new[] { "A-T3" }), "by the session the claim recorded");
    }

    [Test]
    public void Step_7_adds_the_claiming_cli_to_a_version_6_database_and_leaves_its_claims_unknown()
    {
        var path = Path.Combine(dir, "v6.db");
        var old = new JsonStore(Path.Combine(dir, "v6"), new OrchestratorDb(path));
        old.SaveAssignments([Claim("A-T1", null, TimeSpan.FromMinutes(1))]);
        // Back to how a version-6 database was: no claimer columns.
        using (var connection = old.Db.Open())
            OrchestratorDb.Execute(connection, LeaseTests.DropStep10 + "ALTER TABLE assignments DROP COLUMN cli; ALTER TABLE assignments DROP COLUMN cli_session; DROP TABLE session_states; " +
                "ALTER TABLE cli_marks DROP COLUMN since; PRAGMA user_version = 6;");

        var store6 = new JsonStore(Path.Combine(dir, "v6"), new OrchestratorDb(path));
        var kept = store6.LoadAssignments().Single();
        Assert.That(kept.TicketId, Is.EqualTo("A-T1"));
        Assert.That((kept.Cli, kept.CliSession), Is.EqualTo(((int?)null, (string?)null)), "an existing claim's CLI is unknown");
        Assert.That(OrchestratorDb.Version, Is.GreaterThanOrEqualTo(7));

        store6.UpdateAssignments(all => all.Add(Claim("A-T2", Second, TimeSpan.Zero, "s42")));
        Assert.That(store6.LoadAssignments().Single(a => a.TicketId == "A-T2") is { Cli: Second, CliSession: "s42" });
        Assert.That(File.ReadAllText(store6.Db.DumpPath), Does.Contain("cli_session"), "the dump shows who claimed it");
    }
}
