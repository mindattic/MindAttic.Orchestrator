using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using MindAttic.Tickets.Log;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>A ticket whose CLI or tab exited mid-task is seen at once and redispatched in its worktree (M.ORCH-T748).</summary>
public sealed class OrphanWatchTests
{
    private string dir = "";
    private JsonStore store = null!;
    private static readonly DateTime Now = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
    private const string Repo = @"D:\W\A", Root = @"D:\WT", Tree = @"D:\WT\A\A-T1";
    private const int DeadCli = 42, Pilot = 7;
    private static readonly OrchestratorSettings Settings = new() { WorktreeRoot = Root };
    private readonly List<(QueueItem Item, string Prompt)> opened = [];
    private bool openOk = true;

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "orphans-" + Guid.NewGuid().ToString("N"));
        store = new JsonStore(Path.Combine(dir, "store"));
        opened.Clear();
        openOk = true;
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    private static Ticket T(string id) => new(id, "2026-10-01", "fix", "open", $"summary of {id}", "");

    private static Assignment Claim(DateTime started, int cli = DeadCli, string directory = Tree) =>
        new("A-T1", "A", directory, Providers.Claude, started, true) { Cli = cli, CliSession = "dead-session" };

    private OrphanWatch Watch(params int[] alive) => new(store)
    {
        CliRunning = pid => alive.Contains(pid),
        Inspect = (_, _) => (2, ["src/Half.cs", "docs/notes.md"]),
        StillOpen = (_, _) => true,
        MainlineOf = _ => "main",
        OpenTab = (item, prompt) =>
        {
            opened.Add((item, prompt));
            return new Dispatcher.Result(openOk, openOk ? "Opened Claude on A-T1." : "no login of its own has room and API billing is off", null);
        },
    };

    private static Snapshot Snap(DateTime at, IReadOnlyList<HostSession>? hosts = null, IReadOnlyList<TranscriptSession>? sessions = null, params Ticket[] open) =>
        new(at, [new RepoStatus("A", Repo, RepoState.Dormant, hosts ?? [], sessions ?? [], open.Length == 0 ? [T("A-T1")] : open, null, default)], []);

    private (SchedulePlan Plan, Snapshot Snapshot) Planned(DateTime at, Snapshot? snapshot = null, OrchestratorSettings? settings = null)
    {
        var snap = snapshot ?? Snap(at);
        return (Scheduler.Plan(snap, settings ?? Settings, store.LoadAssignments()), snap);
    }

    private static Dictionary<int, IReadOnlyList<string>> Pilots(bool any) =>
        any ? new() { [Pilot] = ["A"] } : [];

    [Test]
    public void A_claim_whose_cli_exited_is_orphaned_at_once_and_handed_back_to_the_repos_allocate_cli()
    {
        store.SaveAssignments([Claim(Now.AddHours(-1))]);
        var (plan, snap) = Planned(Now);

        var events = Watch().Tick(plan, snap, Settings, Pilots(true), Now);

        Assert.That(events.Single(e => e.Alert is not null).Alert!.Value.Title, Is.EqualTo("Orphaned ticket"), "said at once, not at the next SessionStart");
        Assert.That(events.Any(e => e.Line.Contains("handed A-T1 back to the /allocate CLI 7")), Is.True);
        Assert.That(store.LoadAssignments(), Is.Empty, "the dead claim is dropped, so the /allocate CLI's next --claim takes it again");
        var rec = OrphanWatch.Load(store).Single();
        Assert.That((rec.State, rec.Deaths, rec.Redispatches, rec.Ahead), Is.EqualTo((OrphanRecord.HandedBack, 1, 1, 2)));
        Assert.That(rec.Uncommitted, Is.EqualTo(new[] { "src/Half.cs", "docs/notes.md" }));
        Assert.That(opened, Is.Empty, "nothing is typed or opened: the claim machinery does it");

        // Its next claim carries the work forward, in the same worktree.
        var (again, _) = Planned(Now.AddMinutes(1));
        var (handed, _) = Allocate.Next(again, Settings, store, ["A"], 1, claim: true, Now.AddMinutes(1),
            (_, path, _) => (true, path, ""), _ => "main", stillOpen: (_, _) => true);
        Assert.That(handed.Single().Worktree, Is.EqualTo(Tree));
        Assert.That(handed.Single().Prompt, Does.StartWith("A previous agent on A-T1 died mid-task").And.Contain("redispatch 1")
            .And.Contain("src/Half.cs").And.Contain("2 commits not on the main line").And.Contain("never discard").And.Contain("Work on ticket A-T1"));
        Assert.That(handed.Single().Prompt, Does.Not.Contain("\"").And.Not.Contain("\n"));
    }

    [Test]
    public void With_no_allocate_cli_it_starts_again_in_a_new_tab_in_its_worktree_and_waits_when_no_login_has_room()
    {
        store.SaveAssignments([Claim(Now.AddHours(-1))]);
        var watch = Watch();
        openOk = false;
        var (plan, snap) = Planned(Now);
        var first = watch.Tick(plan, snap, Settings, Pilots(false), Now);
        Assert.That(first.Any(e => e.Line.Contains("waits: no login of its own has room")), Is.True);
        Assert.That(OrphanWatch.Load(store).Single().State, Is.EqualTo(OrphanRecord.Orphaned), "it waits: never the global login");
        Assert.That(store.LoadAssignments(), Has.Count.EqualTo(1), "the claim stays until it's redispatched");
        Assert.That(watch.Tick(plan, snap, Settings, Pilots(false), Now.AddMinutes(1)), Is.Empty, "the same wait isn't said every tick");

        openOk = true;
        watch.Tick(plan, snap, Settings, Pilots(false), Now.AddMinutes(2));
        var (item, prompt) = opened.Last();
        Assert.That(item.Id, Is.EqualTo("A-T1"));
        Assert.That(prompt, Does.StartWith("A previous agent on A-T1 died mid-task").And.Contain(Tree).And.Contain("git push origin HEAD:main"));
        var rec = OrphanWatch.Load(store).Single();
        Assert.That((rec.State, rec.Redispatches), Is.EqualTo((OrphanRecord.Reopened, 1)));
        Assert.That(rec.Open, Is.False, "running again: no longer listed as orphaned");
    }

    [Test]
    public void A_live_cli_recent_work_in_the_worktree_or_a_fresh_claim_is_not_orphaned()
    {
        store.SaveAssignments([Claim(Now.AddHours(-1))]);
        var (plan, snap) = Planned(Now);
        Assert.That(Watch(DeadCli).Tick(plan, snap, Settings, Pilots(true), Now), Is.Empty, "its CLI still runs");

        var working = new TranscriptSession("other", Repo, Now.AddMinutes(-2), default, null)
        {
            TicketedSubagents = [new TicketedSubagent("agent", "A-T1", "fix", Tree, [new UsageEvent(Now.AddMinutes(-2), default)])],
        };
        var busy = Snap(Now, sessions: [working]);
        Assert.That(Watch().Tick(Scheduler.Plan(busy, Settings, store.LoadAssignments()), busy, Settings, Pilots(true), Now), Is.Empty,
            "something wrote from its worktree two minutes ago");

        store.SaveAssignments([Claim(Now.AddMinutes(-3))]);
        (plan, snap) = Planned(Now);
        Assert.That(Watch().Tick(plan, snap, Settings, Pilots(true), Now), Is.Empty, "claimed three minutes ago: its subagent may not be up yet");
        Assert.That(OrphanWatch.Load(store), Is.Empty);
    }

    [Test]
    public void At_the_windows_target_or_with_allocate_paused_it_waits_with_its_claim_kept()
    {
        store.SaveAssignments([Claim(Now.AddHours(-1))]);
        var (plan, snap) = Planned(Now);
        var events = Watch().Tick(plan with { Stop = true }, snap, Settings, Pilots(true), Now);
        Assert.That(events.Any(e => e.Line.Contains("window is at its target")), Is.True);
        Assert.That(store.LoadAssignments(), Has.Count.EqualTo(1));

        var paused = Settings with { Allocate = false };
        Watch().Tick(plan, snap, paused, Pilots(true), Now.AddMinutes(1));
        Assert.That(OrphanWatch.Load(store).Single().Why, Is.EqualTo("allocate is paused"));
        Assert.That(store.LoadAssignments(), Has.Count.EqualTo(1));
    }

    [Test]
    public void A_ticket_that_keeps_dying_is_redispatched_only_up_to_the_cap_then_left_for_you_and_not_handed_out()
    {
        var once = Settings with { OrphanRedispatches = 1 };
        store.SaveAssignments([Claim(Now.AddHours(-2))]);
        var (plan, snap) = Planned(Now, settings: once);
        Watch().Tick(plan, snap, once, Pilots(true), Now);
        Assert.That(OrphanWatch.Load(store).Single().State, Is.EqualTo(OrphanRecord.HandedBack));

        // Claimed again by another CLI, which dies too.
        store.SaveAssignments([Claim(Now.AddHours(-1), cli: 43)]);
        (plan, snap) = Planned(Now, settings: once);
        var events = Watch().Tick(plan, snap, once, Pilots(true), Now);
        var alert = events.Single(e => e.Alert is { Title: "Needs you" }).Alert!.Value;
        Assert.That(alert.Urgent, Is.True);
        Assert.That(alert.Text, Does.Contain("went dark twice").And.Contain("isn't redispatched again"));
        var rec = OrphanWatch.Load(store).Single();
        Assert.That((rec.State, rec.Deaths, rec.Redispatches), Is.EqualTo((OrphanRecord.NeedsYou, 2, 1)));
        Assert.That(store.LoadAssignments(), Has.Count.EqualTo(1), "no loop: it stays put");
        Assert.That(Watch().Tick(plan, snap, once, Pilots(true), Now.AddMinutes(1)), Is.Empty, "said once");

        // Even once its claim goes stale, next --claim doesn't hand it out again.
        var stale = Planned(Now.AddDays(1), Snap(Now.AddDays(1)), once).Plan;
        Assert.That(stale.Stale, Has.Count.EqualTo(1));
        var (handed, _) = Allocate.Next(stale, once, store, ["A"], 1, claim: false, Now.AddDays(1), stillOpen: (_, _) => true);
        Assert.That(handed, Is.Empty);
    }

    [Test]
    public void Work_left_where_a_redispatch_wouldnt_start_is_never_left_behind()
    {
        store.SaveAssignments([Claim(Now.AddHours(-1), directory: @"D:\Elsewhere\A-T1")]);
        var (plan, snap) = Planned(Now);
        var events = Watch().Tick(plan, snap, Settings, Pilots(true), Now);
        Assert.That(events.Any(e => e.Alert is { Title: "Needs you" } a && a.Text.Contains(@"D:\Elsewhere\A-T1")), Is.True);
        Assert.That(store.LoadAssignments(), Has.Count.EqualTo(1), "not handed back: its uncommitted work would stay behind");
        Assert.That(OrphanWatch.Load(store).Single().State, Is.EqualTo(OrphanRecord.NeedsYou));
    }

    [Test]
    public void A_closed_tickets_record_is_dropped_and_release_forgets_one()
    {
        store.SaveAssignments([Claim(Now.AddHours(-1))]);
        var (plan, snap) = Planned(Now);
        Watch().Tick(plan, snap, Settings with { OrphanRedispatches = 0 }, Pilots(true), Now);
        Assert.That(OrphanWatch.Load(store).Single().Why, Does.Contain("--orphan-redispatches"));
        Assert.That(store.LoadAssignments(), Has.Count.EqualTo(1), "0 only shows it");

        var closed = Snap(Now.AddMinutes(5), open: [T("A-T2")]);
        var events = Watch().Tick(Scheduler.Plan(closed, Settings, store.LoadAssignments()), closed, Settings, Pilots(true), Now.AddMinutes(5));
        Assert.That(events.Any(e => e.Line.Contains("A-T1 is closed now")), Is.True);
        Assert.That(OrphanWatch.Load(store), Is.Empty);

        Watch().Tick(plan, snap, Settings with { OrphanRedispatches = 0 }, Pilots(true), Now);
        Assert.That(OrphanWatch.Forget(store, "a-t1"), Is.True);
        Assert.That(OrphanWatch.Load(store), Is.Empty);
    }
}
