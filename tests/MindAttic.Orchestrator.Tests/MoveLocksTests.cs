using System.Text.Json;
using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Billing;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using MindAttic.Tickets.Log;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// M.ORCH-T637: Move to carries the CLI's subagents and locks its work until the move lands. The move stops its running
/// subagents, keeps their tickets claimed (worktrees and all) and locked from every other claim, and hands them back to the CLI
/// once it shows up on the new login; one that doesn't land in time is unlocked with its old owner keeping them. Every store and
/// login folder is a scratch one, and nothing is typed into a real tab.
/// </summary>
public sealed class MoveLocksTests
{
    private static readonly DateTime Now = new(2026, 10, 22, 16, 0, 0, DateTimeKind.Utc);
    private static readonly OrchestratorSettings Settings = new() { WorktreeRoot = @"D:\WT" };
    private static readonly HostSession Tab = new("Claude", 77, "mindattic-host-claude-77", @"D:\W\A");
    private const int OldPid = 4242, NewPid = 5151;
    private readonly TempDirs temps = new();
    private JsonStore store = null!;
    private AccountDirs dirs = null!;
    private readonly List<string> sent = [];
    private readonly List<HostSession> interrupted = [];
    private bool tabTakes = true;

    [SetUp]
    public void SetUp()
    {
        sent.Clear();
        interrupted.Clear();
        tabTakes = true;
        var dir = temps.New();
        store = new JsonStore(Path.Combine(dir, "store"));
        Directory.CreateDirectory(Path.Combine(dir, ".claude"));
        dirs = new AccountDirs(Path.Combine(dir, ".claude-accounts"), Path.Combine(dir, ".claude"), Path.Combine(dir, ".claude.json"));
        var b = dirs.For("b@x.com");
        Directory.CreateDirectory(b.Directory);
        File.WriteAllText(b.ConfigPath, JsonSerializer.Serialize(new { oauthAccount = new { accountUuid = "u-b", emailAddress = "b@x.com" } }));
    }

    [TearDown]
    public void TearDown() => temps.DeleteAll();

    private static CliBilling Cli => new(OldPid, "sess-1", @"D:\W\A", "subscription", Tab);

    private AccountMove Mover() => new(dirs)
    {
        Send = (_, m) => { sent.Add(m); return Task.FromResult(tabTakes); },
        Interrupt = h => { interrupted.Add(h); return Task.FromResult(true); },
        OnOwnLogin = () => new Dictionary<string, string>(),
        LiveOverride = (_, _) => [Cli],
        NameOf = _ => "Bea",
        Locks = new MoveLocks(store),
    };

    private static Assignment Claim(string id, int cli, string session = "sess-1") =>
        new(id, "A", $@"D:\WT\A\{id}", Providers.Claude, Now.AddMinutes(-20), true) { Cli = cli, CliSession = session, Model = "sonnet" };

    private static Ticket T(string id) => new(id, "2026-10-01", "fix", "open", $"summary of {id}", "");

    private static AgentBoardRow Row(string session, int pid, string? ownLogin, HostSession? host, DateTime started) =>
        new(session, "A", null, null, started, started, AgentState.Waiting, "", false, default, []) { OwnLogin = ownLogin, CliPid = pid, Host = host };

    [Test]
    public async Task A_move_locks_the_cli_and_its_claims_and_stops_its_subagents_at_once()
    {
        store.SaveAssignments([Claim("A-T1", OldPid), Claim("A-T2", 999, "sess-1"), Claim("A-T3", 999, "other")]);

        var result = await Mover().MoveAsync(Cli, "b@x.com", Now, holding: ["A-T9"], repo: "A");

        Assert.That(result.Ok, Is.True, result.Message);
        Assert.That(result.Message, Does.Contain("subagents were stopped"));
        var locked = new MoveLocks(store).All().Single();
        Assert.That(locked.Tickets, Is.EquivalentTo(new[] { "A-T1", "A-T2", "A-T9" }), "its claims by process or session, and what it holds; never another CLI's");
        Assert.That(locked.Text, Is.EqualTo("Moving to Bea…"));
        Assert.That(locked.Interrupted, Is.True);
        Assert.That(interrupted, Is.EqualTo(new[] { Tab }), "its turn is interrupted, so the restart doesn't wait for its subagents");
        Assert.That(store.LoadAssignments().Select(a => (a.TicketId, a.Cli)), Is.EquivalentTo(new[] { ("A-T1", (int?)OldPid), ("A-T2", 999), ("A-T3", 999) }),
            "their tickets stay claimed, as they were");

        var again = await Mover().MoveAsync(Cli, "b@x.com", Now.AddMinutes(1));
        Assert.That(again.Ok, Is.False);
        Assert.That(again.Message, Does.Contain("already moving to Bea"));
        Assert.That(sent, Has.Count.EqualTo(1), "Move to again is refused while it's on its way");
    }

    [Test]
    public async Task A_move_the_tab_refused_is_unlocked_at_once_and_a_cli_holding_nothing_is_not_interrupted()
    {
        tabTakes = false;
        Assert.That((await Mover().MoveAsync(Cli, "b@x.com", Now)).Ok, Is.False);
        Assert.That(new MoveLocks(store).All(), Is.Empty);

        tabTakes = true;
        Assert.That((await Mover().MoveAsync(Cli, "b@x.com", Now)).Ok, Is.True);
        Assert.That(interrupted, Is.Empty, "no tickets: nothing of it to stop");
    }

    [Test]
    public void Its_tickets_are_never_claimed_handed_out_or_nudged_elsewhere_while_it_moves()
    {
        var move = new MoveLock { SessionId = "sess-1", Pid = OldPid, Repo = "A", ToName = "Bea", StartedUtc = Now, Tickets = ["A-T1", "A-T2"] };
        new MoveLocks(store).TryBegin(move, out _);
        var snap = new Snapshot(Now, [new RepoStatus("A", @"D:\W\A", RepoState.Dormant, [], [], [T("A-T1"), T("A-T2"), T("A-T3")], null, default)], [])
        {
            Moves = new MoveLocks(store).All(),
        };
        // A-T1's claim long stale, A-T2 with none at all: both stay the moving CLI's.
        var plan = Scheduler.Plan(snap, Settings with { StaleAfterHours = 0 }, [Claim("A-T1", OldPid) with { StartedUtc = Now.AddDays(-3) }]);
        Assert.That(plan.Queue.Where(q => q.Moving is not null).Select(q => q.Id), Is.EquivalentTo(new[] { "A-T1", "A-T2" }));
        Assert.That(plan.Queue.Where(q => q.Moving is not null).All(q => q.IsAssigned), Is.True);

        var (handed, _) = Allocate.Next(plan, Settings, store, ["A"], 5, claim: false, Now, cli: 1, cliRunning: _ => true);
        Assert.That(handed.Select(h => h.Item.Id), Is.EqualTo(new[] { "A-T3" }), "another CLI's next gets only what's free");

        var (mine, why) = Allocate.Next(plan, Settings, store, ["A"], 5, claim: false, Now, cli: OldPid, cliRunning: _ => true);
        Assert.That(mine, Is.Empty);
        Assert.That(why, Does.Contain("moving to Bea"), "the moving CLI claims nothing until it lands");

        var pilots = new Dictionary<int, IReadOnlyList<string>> { [7] = ["A"] };
        Assert.That(AllocateWaker.IdleClaims(snap, plan, pilots, Now), Is.Empty, "its idle claims aren't put to the repo's other /allocate CLI");
    }

    [Test]
    public void Its_own_tab_lands_it_ahead_of_another_cli_of_its_repo_started_since()
    {
        // bughunt 2026-10-27: the first CLI of its repo started since the move on the new login was taken for it.
        var locks = new MoveLocks(store) { IsAlive = _ => true };
        locks.TryBegin(new MoveLock
        {
            SessionId = "sess-1", Pid = OldPid, HostPid = Tab.ProcessId, Repo = "A", ToEmail = "b@x.com", ToName = "Bea", StartedUtc = Now, Tickets = ["A-T1"],
        }, out _);
        var other = Row("sess-9", 9999, "b@x.com", new HostSession("Claude", 88, "mindattic-host-claude-88", @"D:\W\A"), Now.AddMinutes(1));

        var ends = locks.Reconcile([other, Row("sess-2", NewPid, "b@x.com", Tab, Now.AddMinutes(1))], Now.AddMinutes(2));

        Assert.That(ends.Single().Cli?.SessionId, Is.EqualTo("sess-2"));
    }

    [Test]
    public void Once_it_lands_on_the_new_login_its_tickets_are_handed_back_to_start_again_in_their_worktrees()
    {
        store.SaveAssignments([Claim("A-T1", OldPid), Claim("A-T2", 999, "other")]);
        var locks = new MoveLocks(store) { IsAlive = _ => false };
        locks.TryBegin(new MoveLock
        {
            SessionId = "sess-1", Pid = OldPid, HostPid = Tab.ProcessId, HostPipe = Tab.PipeName, Repo = "A", Cwd = @"D:\W\A",
            ToEmail = "b@x.com", ToName = "Bea", StartedUtc = Now, Tickets = ["A-T1"], Interrupted = true,
        }, out _);

        // Still on the old login: still moving.
        Assert.That(locks.Reconcile([Row("sess-1", OldPid, null, Tab, Now.AddHours(-1))], Now.AddMinutes(1)), Is.Empty);

        // The same tab, a new claude.exe and session, on b@x.com.
        var ends = locks.Reconcile([Row("sess-2", NewPid, "B@x.com", Tab, Now.AddHours(-1))], Now.AddMinutes(2));
        Assert.That(ends.Single().Landed, Is.True);
        Assert.That(locks.All(), Is.Empty);
        Assert.That(locks.Reconcile([Row("sess-2", NewPid, "b@x.com", Tab, Now)], Now.AddMinutes(3)), Is.Empty, "ended once");

        var nudged = new List<(int Pid, string Text)>();
        var result = new MoveHandback(store) { Nudge = (p, t) => { nudged.Add((p, t)); return true; } }.Deliver(ends[0]);

        Assert.That(result.Left, Is.True, result.Line);
        Assert.That(store.LoadAssignments().Single(a => a.TicketId == "A-T1") is { Cli: NewPid, CliSession: "sess-2" }, "the claim is the CLI's as it is now");
        Assert.That(store.LoadAssignments().Single(a => a.TicketId == "A-T2").Cli, Is.EqualTo(999), "another CLI's claim is left alone");
        Assert.That(nudged.Single().Pid, Is.EqualTo(NewPid), "left for the CLI's hook as it runs now, never typed into its tab (M.ORCH-T864)");
        Assert.That(nudged.Single().Text, Does.Contain("moved to Bea").And.Contain(@"A-T1 (worktree D:\WT\A\A-T1, model sonnet)")
            .And.Contain("same worktree").And.Not.Contain("A-T2").And.Not.Contain("\""));
    }

    [Test]
    public void A_move_that_never_lands_ends_with_its_old_owner_keeping_its_tickets()
    {
        store.SaveAssignments([Claim("A-T1", OldPid)]);
        var alive = true;
        var locks = new MoveLocks(store) { IsAlive = _ => alive };
        var move = new MoveLock { SessionId = "sess-1", Pid = OldPid, HostPipe = Tab.PipeName, Repo = "A", ToEmail = "b@x.com", ToName = "Bea", StartedUtc = Now, Tickets = ["A-T1"] };
        locks.TryBegin(move, out _);

        Assert.That(locks.Reconcile([], Now + MoveLocks.GoneAfter + TimeSpan.FromMinutes(1)), Is.Empty, "its CLI still runs: it may yet move at its next idle point");
        var ends = locks.Reconcile([], Now + MoveLocks.Timeout + TimeSpan.FromSeconds(1));
        Assert.That(ends.Single() is { Landed: false } end && end.Why.Contains("30 minutes"));

        var nudged = new List<string>();
        var result = new MoveHandback(store) { IsAlive = _ => true, Nudge = (_, t) => { nudged.Add(t); return true; } }.Deliver(ends[0]);
        Assert.That(store.LoadAssignments().Single().Cli, Is.EqualTo(OldPid), "the old owner keeps it");
        Assert.That(result.Line, Does.Contain("didn't move to Bea"));
        Assert.That(nudged.Single(), Does.Contain("didn't happen").And.Contain("A-T1"));

        // A CLI that ended and never came back fails sooner.
        alive = false;
        locks.TryBegin(move, out _);
        Assert.That(locks.Reconcile([], Now + MoveLocks.GoneAfter + TimeSpan.FromSeconds(1)).Single().Why, Does.Contain("ended"));
    }
}
