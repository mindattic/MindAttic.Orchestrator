using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T1677: the ledger sweeper deletes only what its owner is done with (dead, lease run out, or proven
/// delivered), never what's still claimed, alive, outside its roots or behind a link, and nothing at all in a dry run.</summary>
public sealed class LedgerSweeperTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);

    private string root = "";

    [SetUp]
    public void SetUp() => root = Directory.CreateTempSubdirectory("orch-ledger-sweep-").FullName;

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(root, recursive: true); } catch (IOException) { }
    }

    private static Resource Row(string owner, DateTime? expires = null, DateTime? released = null, string kind = ResourceKind.Scratch,
        string path = @"D:\wt\.scratch\X-T1") =>
        new("PC", kind, path, owner, null, Now.AddHours(-3), expires, released);

    private static LedgerOwners Owners(TicketFacts? ticket = null, bool? session = null, ProcessFact? process = null) =>
        new(_ => ticket, SessionLiveness.From(_ => session), _ => process ?? new ProcessFact(null, null));

    private static TicketFacts Ticket(bool closed = false, bool claimed = false, bool landed = false, bool exists = true) =>
        new(exists, closed, claimed, landed);

    // ---- A ticket's scratch: delete only what is proven delivered ----

    [Test]
    public void A_closed_ticket_whose_commit_is_on_main_goes_at_once_lease_or_not()
    {
        var r = Row("ticket:X-T1", expires: Now.AddHours(40));
        Assert.That(LedgerSweeper.Judge(r, Now, Owners(Ticket(closed: true, landed: true))).Go, Is.True);
        // Even with an assignment still lingering on it: closed and landed is delivered.
        Assert.That(LedgerSweeper.Judge(r, Now, Owners(Ticket(closed: true, claimed: true, landed: true))).Go, Is.True);
    }

    [Test]
    public void A_closed_ticket_with_no_commit_on_main_waits_for_its_lease()
    {
        var closed = Owners(Ticket(closed: true));
        Assert.That(LedgerSweeper.Judge(Row("ticket:X-T1", expires: Now.AddHours(1)), Now, closed).Go, Is.False);
        Assert.That(LedgerSweeper.Judge(Row("ticket:X-T1", expires: Now.AddHours(1), released: Now.AddHours(-1)), Now, closed).Go, Is.False,
            "released, but not proven delivered: it waits");
        Assert.That(LedgerSweeper.Judge(Row("ticket:X-T1", expires: Now.AddMinutes(-1)), Now, closed).Go, Is.True);
    }

    [Test]
    public void A_ticket_still_claimed_keeps_its_scratch_even_past_its_lease()
    {
        var (go, why) = LedgerSweeper.Judge(Row("ticket:X-T1", expires: Now.AddDays(-3)), Now, Owners(Ticket(claimed: true)));
        Assert.That(go, Is.False);
        Assert.That(why, Does.Contain("still claimed"));
    }

    [Test]
    public void An_open_ticket_nobody_holds_waits_for_its_lease_unless_it_was_handed_back()
    {
        var open = Owners(Ticket());
        Assert.That(LedgerSweeper.Judge(Row("ticket:X-T1", expires: Now.AddHours(1)), Now, open).Go, Is.False);
        Assert.That(LedgerSweeper.Judge(Row("ticket:X-T1", expires: Now.AddHours(-1)), Now, open).Go, Is.True);
        var handedBack = LedgerSweeper.Judge(Row("ticket:X-T1", expires: Now.AddHours(1), released: Now.AddMinutes(-5)), Now, open);
        Assert.That(handedBack.Go, Is.True);
        Assert.That(handedBack.Why, Does.StartWith("handed back"));
    }

    [Test]
    public void A_ticket_whose_state_cant_be_read_is_kept()
    {
        Assert.That(LedgerSweeper.Judge(Row("ticket:X-T1", expires: Now.AddDays(-9), released: Now.AddDays(-9)), Now, Owners(ticket: null)).Go, Is.False);
    }

    [Test]
    public void A_lease_less_scratch_goes_only_when_released_or_delivered()
    {
        var closed = Owners(Ticket(closed: true));
        Assert.That(LedgerSweeper.Judge(Row("ticket:X-T1"), Now, closed).Go, Is.False);
        Assert.That(LedgerSweeper.Judge(Row("ticket:X-T1", released: Now.AddHours(-1)), Now, closed).Go, Is.True);
        Assert.That(LedgerSweeper.Judge(Row("ticket:X-T1", expires: Now.AddHours(-1)), Now, Owners(Ticket(exists: false))).Go, Is.True,
            "a ticket the database doesn't have holds nothing past its lease");
    }

    // ---- Sessions and processes: owner dead ----

    [Test]
    public void A_sessions_entry_goes_when_the_session_ended_and_never_while_it_runs()
    {
        var r = Row("session:11111111-1111-1111-1111-111111111111", expires: Now.AddDays(-1), kind: ResourceKind.TempFile);
        Assert.That(LedgerSweeper.Judge(r, Now, Owners(session: false)).Go, Is.True);
        Assert.That(LedgerSweeper.Judge(r, Now, Owners(session: true)).Go, Is.False, "a running session keeps it, whatever its lease");
        Assert.That(LedgerSweeper.Judge(r, Now, Owners(session: null)).Go, Is.True, "unknown, and its lease ran out");
        Assert.That(LedgerSweeper.Judge(r with { ExpiresUtc = Now.AddHours(1) }, Now, Owners(session: null)).Go, Is.False);
        Assert.That(LedgerSweeper.Judge(r with { ReleasedUtc = Now }, Now, Owners(session: true)).Go, Is.True, "its owner let go of it");
    }

    [Test]
    public void A_processs_entry_goes_when_the_pid_is_gone_or_is_another_process_now()
    {
        var started = Now.AddHours(-2);
        var r = Row(ResourceLedger.ProcessOwner(4321, started), kind: ResourceKind.TempFile);
        Assert.That(LedgerSweeper.Judge(r, Now, Owners(process: new ProcessFact(false, null))).Go, Is.True);
        Assert.That(LedgerSweeper.Judge(r, Now, Owners(process: new ProcessFact(true, started))).Go, Is.False);
        Assert.That(LedgerSweeper.Judge(r, Now, Owners(process: new ProcessFact(true, started.AddMinutes(30)))).Go, Is.True, "the pid was reused");
        Assert.That(LedgerSweeper.Judge(r, Now, Owners(process: new ProcessFact(true, null))).Go, Is.False, "it runs and can't be read: kept");
        Assert.That(LedgerSweeper.Judge(r, Now, Owners(process: new ProcessFact(null, null))).Go, Is.False, "can't tell, and no lease ran out");
        // A bare pid (no start time recorded) can't be told from a reused one: kept while any process has it.
        Assert.That(LedgerSweeper.Judge(Row(ResourceLedger.ProcessOwner(4321)), Now, Owners(process: new ProcessFact(true, Now))).Go, Is.False);
    }

    [Test]
    public void A_process_owner_round_trips_with_and_without_its_start_time()
    {
        var started = new DateTime(2026, 11, 16, 8, 0, 5, DateTimeKind.Utc);
        Assert.That(ResourceLedger.ParseProcessOwner(ResourceLedger.ProcessOwner(77, started)), Is.EqualTo((77, (DateTime?)started)));
        Assert.That(ResourceLedger.ParseProcessOwner(ResourceLedger.ProcessOwner(77)), Is.EqualTo((77, (DateTime?)null)));
        Assert.That(ResourceLedger.ParseProcessOwner("ticket:X-T1"), Is.Null);
        Assert.That(ResourceLedger.ParseProcessOwner("pid:abc"), Is.Null);
    }

    [Test]
    public void The_daemons_and_an_unknown_owners_entries_wait_for_their_lease()
    {
        Assert.That(LedgerSweeper.Judge(Row(ResourceLedger.DaemonOwner), Now, Owners()).Go, Is.False);
        Assert.That(LedgerSweeper.Judge(Row(ResourceLedger.DaemonOwner, expires: Now.AddMinutes(-1)), Now, Owners()).Go, Is.True);
        Assert.That(LedgerSweeper.Judge(Row("somebody"), Now, Owners()).Go, Is.False);
        Assert.That(LedgerSweeper.Judge(Row("somebody", expires: Now.AddMinutes(-1)), Now, Owners()).Go, Is.True);
    }

    // ---- Never deleted ----

    [Test]
    public void A_worktree_is_never_the_sweepers_to_delete()
    {
        var r = Row(ResourceLedger.DaemonOwner, expires: Now.AddDays(-30), released: Now.AddDays(-30), kind: ResourceKind.Worktree);
        Assert.That(LedgerSweeper.Judge(r, Now, Owners()).Go, Is.False);
    }

    [Test]
    public void Only_paths_strictly_under_the_scratch_root_or_the_temp_dir_may_go()
    {
        string[] roots = [@"D:\wt\.scratch", @"C:\Temp"];
        Assert.That(LedgerSweeper.Refuse(@"D:\wt\.scratch\X-T1", roots), Is.Null);
        Assert.That(LedgerSweeper.Refuse(@"C:\Temp\orchestrator-review-1-abc.txt", roots), Is.Null);
        Assert.That(LedgerSweeper.Refuse(@"D:\wt\.scratch", roots), Is.Not.Null, "a root itself");
        Assert.That(LedgerSweeper.Refuse(@"D:\wt\.scratchy\X-T1", roots), Is.Not.Null, "a sibling whose name starts the same");
        Assert.That(LedgerSweeper.Refuse(@"D:\wt\MindAttic.Orchestrator\main-tests", roots), Is.Not.Null);
        Assert.That(LedgerSweeper.Refuse(@"D:\Projects\MindAttic\MindAttic.Orchestrator", roots), Is.Not.Null, "a user's repo");
        Assert.That(LedgerSweeper.Refuse(@"D:\wt\.scratch\..\..\Users", roots), Is.Not.Null, "a path that climbs out");
        Assert.That(LedgerSweeper.Refuse(@"C:\Users\me\file.txt", [@"C:\"]), Is.Not.Null, "a drive root is never a root to delete under");
    }

    [Test]
    public void The_daemons_own_worktrees_are_told_from_tickets_ones()
    {
        foreach (var name in new[] { "main-tests", "main-bisect", "local-tests", "merge-main", "branch-orch-M.ORCH-T1" })
            Assert.That(LedgerSweeper.IsDaemonTree(name), Is.True, name);
        foreach (var name in new[] { "work-M.ORCH-T1", "M.ORCH-T1", ".scratch", "bughunt" })
            Assert.That(LedgerSweeper.IsDaemonTree(name), Is.False, name);
    }

    // ---- The sweep itself, on real folders ----

    private LedgerSweepOptions Options(bool dryRun, string? log = null) => new()
    {
        WorktreeRoot = Path.Combine(root, "wt"),
        TempDir = Path.Combine(root, "temp"),
        DryRun = dryRun,
        QuietMinutes = 0,
        LeftoverHours = 24,
        BuildOutputHours = 72,
        LogPath = log,
    };

    private static void Age(string path, DateTime utc)
    {
        if (File.Exists(path))
        {
            File.SetCreationTimeUtc(path, utc);
            File.SetLastWriteTimeUtc(path, utc);
            return;
        }
        foreach (var f in Directory.GetFiles(path, "*", SearchOption.AllDirectories)) Age(f, utc);
        foreach (var d in Directory.GetDirectories(path, "*", SearchOption.AllDirectories).Append(path))
        {
            Directory.SetCreationTimeUtc(d, utc);
            Directory.SetLastWriteTimeUtc(d, utc);
        }
    }

    private string Put(string relative, int bytes = 100)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
        return path;
    }

    [Test]
    public void Crash_leftovers_go_by_name_once_old_and_a_dry_run_deletes_nothing()
    {
        var now = DateTime.UtcNow;
        var oldReview = Put(@"temp\orchestrator-review-1-aaaa.txt");
        var newReview = Put(@"temp\orchestrator-review-2-bbbb.txt");
        var oldCheck = Path.GetDirectoryName(Put(@"temp\mindattic-token-check-cccc\.claude.json", 50))!;
        var notOurs = Put(@"temp\somebody-elses.txt");
        Age(oldReview, now.AddDays(-3));
        Age(oldCheck, now.AddDays(-3));
        var log = Path.Combine(root, "disk-cleanup.log");

        var dry = LedgerSweeper.Sweep(null, Owners(), Options(dryRun: true, log), now);
        Assert.That(dry.Going.Select(i => i.Path), Is.EquivalentTo(new[] { oldReview, oldCheck }));
        Assert.That(dry.Bytes, Is.EqualTo(150));
        Assert.That(File.Exists(oldReview) && Directory.Exists(oldCheck), Is.True, "a dry run deletes nothing");
        Assert.That(File.ReadAllText(log), Does.Contain("would delete").And.Contain(oldReview), "and writes what it would, with bytes");

        var real = LedgerSweeper.Sweep(null, Owners(), Options(dryRun: false, log), now);
        Assert.That(real.Bytes, Is.EqualTo(150));
        Assert.That(File.Exists(oldReview) || Directory.Exists(oldCheck), Is.False);
        Assert.That(File.Exists(newReview) && File.Exists(notOurs), Is.True, "a fresh leftover and anything else in the temp dir stay");
    }

    [Test]
    public void A_leftover_with_a_file_held_open_is_kept()
    {
        var now = DateTime.UtcNow;
        var review = Put(@"temp\orchestrator-review-9-open.txt");
        Age(review, now.AddDays(-3));
        using (File.Open(review, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var r = LedgerSweeper.Sweep(null, Owners(), Options(dryRun: false), now);
            Assert.That(r.Going, Is.Empty);
            Assert.That(r.Items.Single().Why, Does.Contain("is open"));
        }
        Assert.That(File.Exists(review), Is.True);
    }

    [Test]
    public void Stale_bin_and_obj_beside_a_project_go_from_a_daemon_worktree_and_nowhere_else()
    {
        var now = DateTime.UtcNow;
        var old = now.AddDays(-10);
        Put(@"wt\Repo\main-tests\src\App\App.csproj");
        var bin = Path.GetDirectoryName(Put(@"wt\Repo\main-tests\src\App\bin\Debug\App.dll", 300))!;
        var obj = Path.GetDirectoryName(Put(@"wt\Repo\main-tests\src\App\obj\project.assets.json", 200))!;
        var scripts = Put(@"wt\Repo\main-tests\tools\bin\run.ps1"); // a tracked bin folder: no project beside it
        var source = Put(@"wt\Repo\main-tests\src\App\Program.cs");
        Put(@"wt\Repo\work-X-T1\src\App\App.csproj");
        var ticketsBin = Put(@"wt\Repo\work-X-T1\src\App\bin\Debug\App.dll"); // a ticket's worktree: an agent's
        Put(@"wt\Repo\local-tests\src\App\App.csproj");
        var freshBin = Put(@"wt\Repo\local-tests\src\App\bin\Debug\App.dll"); // written just now: a run may be using it
        Age(Path.Combine(root, "wt", "Repo", "main-tests"), old);
        Age(Path.Combine(root, "wt", "Repo", "work-X-T1"), old);

        var dry = LedgerSweeper.Sweep(null, Owners(), Options(dryRun: true), now);
        Assert.That(dry.Going.Select(i => i.Path), Is.EqualTo(new[] { Path.Combine(root, "wt", "Repo", "main-tests") }));
        Assert.That(dry.Bytes, Is.EqualTo(500));
        Assert.That(Directory.Exists(Path.GetDirectoryName(bin)), Is.True);

        var busy = LedgerSweeper.Sweep(null, Owners(), Options(dryRun: false) with { Busy = _ => true }, now);
        Assert.That(busy.Going, Is.Empty, "a build or test running there keeps it");

        var real = LedgerSweeper.Sweep(null, Owners(), Options(dryRun: false), now);
        Assert.That(real.Bytes, Is.EqualTo(500));
        Assert.That(Directory.Exists(Path.Combine(root, "wt", "Repo", "main-tests", "src", "App", "bin")), Is.False);
        Assert.That(Directory.Exists(obj), Is.False);
        Assert.That(File.Exists(scripts) && File.Exists(source) && File.Exists(ticketsBin) && File.Exists(freshBin), Is.True);
    }

    [Test]
    public void A_dead_owners_ledger_entry_is_deleted_then_released_and_a_live_ones_is_left()
    {
        var now = DateTime.UtcNow;
        var ledger = new ResourceLedger(new OrchestratorDb(Path.Combine(root, "data", "orchestrator.db")), "PC");
        var delivered = Path.GetDirectoryName(Put(@"wt\.scratch\X-T1\build\out.bin", 400))!;
        delivered = Path.GetDirectoryName(delivered)!;
        var claimed = Path.GetDirectoryName(Put(@"wt\.scratch\X-T2\notes.txt", 10))!;
        var outside = Path.GetDirectoryName(Put(@"elsewhere\X-T3\keep.txt", 10))!;
        ledger.Register(ResourceKind.Scratch, delivered, ResourceLedger.TicketOwner("X-T1"), now.AddHours(-1), now.AddHours(47), "X-T1");
        ledger.Register(ResourceKind.Scratch, claimed, ResourceLedger.TicketOwner("X-T2"), now.AddHours(-1), now.AddHours(-0.5), "X-T2");
        ledger.Register(ResourceKind.Scratch, outside, ResourceLedger.TicketOwner("X-T1"), now.AddHours(-1), now.AddHours(47), "X-T1");
        var owners = new LedgerOwners(id => id == "X-T1" ? Ticket(closed: true, landed: true) : Ticket(claimed: true),
            SessionLiveness.From(_ => null), _ => new ProcessFact(null, null));

        var dry = LedgerSweeper.Sweep(ledger, owners, Options(dryRun: true), now);
        Assert.That(dry.Going.Select(i => i.Path), Is.EqualTo(new[] { delivered }));
        Assert.That(Directory.Exists(delivered), Is.True);
        Assert.That(ledger.Find(ResourceKind.Scratch, delivered)!.IsReleased, Is.False, "a dry run releases nothing");

        // Written a moment ago: with a quiet window it waits.
        var waiting = LedgerSweeper.Sweep(ledger, owners, Options(dryRun: false) with { QuietMinutes = 30 }, now);
        Assert.That(waiting.Going, Is.Empty);

        var real = LedgerSweeper.Sweep(ledger, owners, Options(dryRun: false), now);
        Assert.That(real.Bytes, Is.EqualTo(400));
        Assert.That(Directory.Exists(delivered), Is.False);
        Assert.That(ledger.Find(ResourceKind.Scratch, delivered)!.IsReleased, Is.True, "the row stays, released");
        Assert.That(Directory.Exists(claimed), Is.True, "still claimed, though its lease ran out");
        Assert.That(Directory.Exists(outside), Is.True, "recorded, but outside the scratch root and the temp dir: never deleted");
        Assert.That(real.Items.Single(i => i.Path == outside).Why, Does.Contain("never deleted"));

        // Released and gone, the row is history; past the history window it's dropped.
        LedgerSweeper.Sweep(ledger, owners, Options(dryRun: false) with { HistoryDays = 30 }, now.AddDays(31));
        Assert.That(ledger.Find(ResourceKind.Scratch, delivered), Is.Null);
    }
}
