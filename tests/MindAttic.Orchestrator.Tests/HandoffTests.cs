using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>/handoff (M.ORCH-T481): the mark, what still keeps it from being quiet, and the daemon's watch over it.</summary>
public sealed class HandoffTests
{
    private static readonly DateTime Now = new(2026, 10, 19, 20, 0, 0, DateTimeKind.Utc);
    private readonly TempDirs temp = new();
    private string dir = "";
    private ContextCycler cycler = null!;
    private JsonStore store = null!;

    [SetUp]
    public void SetUp()
    {
        dir = temp.New();
        cycler = new ContextCycler(Path.Combine(dir, "session-context")) { ProjectsDir = Path.Combine(dir, "projects") };
        Directory.CreateDirectory(cycler.Directory);
        store = new JsonStore(Path.Combine(dir, "store"));
    }

    [TearDown]
    public void TearDown() => temp.DeleteAll();

    private static CliRow Row(int pid, bool idle, bool launcher = false, DateTime? turn = null) =>
        new(pid, "s" + pid, "Repo" + pid, @"D:\W\Repo" + pid, null, launcher, false, [], idle, turn ?? Now.AddMinutes(-1));

    private HandoffWatch Watch(List<string>? returned = null, List<string>? typed = null) => new(cycler, store)
    {
        Return = (a, _) => { returned?.Add(a.TicketId); store.UpdateAssignments(all => all.RemoveAll(x => x.TicketId == a.TicketId)); return "its worktree removed"; },
        Submit = (pipe, text) => { typed?.Add(pipe + ":" + text); return Task.FromResult(true); },
        HostOf = (pid, hosts) => hosts.FirstOrDefault(h => h.ProcessId == pid),
    };

    private void Winding(DateTime? started = null) =>
        Handoff.Write(cycler.Directory, new HandoffMark(Handoff.Winding, Handoff.Stamp(started ?? Now.AddMinutes(-10)), 1, "acct", null, null, []));

    [Test]
    public void Reading_a_mark_file_never_creates_the_database()
    {
        // M.ORCH-T541: Read -> ReadIn saved or deleted the row, which created orchestrator.db where none was.
        var db = OrchestratorDb.StatePath(dir);
        Assert.That(File.Exists(db), Is.False);
        File.WriteAllText(Path.Combine(cycler.Directory, Handoff.FileName), "{\"State\":\"off\"}");
        Assert.That(Handoff.Read(cycler.Directory), Is.Null);
        File.WriteAllText(Path.Combine(cycler.Directory, Handoff.FileName),
            System.Text.Json.JsonSerializer.Serialize(new HandoffMark(Handoff.Winding, Handoff.Stamp(Now), 1, "acct", null, null, [])));
        Assert.That(Handoff.Read(cycler.Directory)?.State, Is.EqualTo(Handoff.Winding), "the file is read as it is");
        Assert.That(File.Exists(db), Is.False, "a read never creates the database");
    }

    [Test]
    public void Start_writes_a_winding_mark_once_and_off_clears_it_with_every_clis_saved_mark()
    {
        Assert.That((Handoff.Read(cycler.Directory), Handoff.Holding(cycler.Directory)), Is.EqualTo(((HandoffMark?)null, false)));
        File.WriteAllText(Path.Combine(cycler.Directory, Handoff.SavedPrefix + "99"), "Old"); // left from an earlier handoff
        var mark = Handoff.Start(cycler, 7, "acct-1", Now);
        Assert.That((mark.State, mark.By, mark.Account, mark.StartedUtc), Is.EqualTo((Handoff.Winding, (int?)7, "acct-1", Now)));
        Assert.That(Handoff.Holding(cycler.Directory), Is.True);
        Assert.That(Handoff.Saved(cycler.Directory), Is.Empty, "a new handoff starts with nobody saved");

        Assert.That(Handoff.Start(cycler, 8, "acct-2", Now.AddMinutes(5)).By, Is.EqualTo(7), "one under way is kept, not restarted");

        File.WriteAllText(Path.Combine(cycler.Directory, Handoff.SavedPrefix + "7"), "Prose");
        Assert.That(Handoff.Saved(cycler.Directory), Is.EqualTo(new Dictionary<int, string> { [7] = "Prose" }));
        Handoff.Clear(cycler.Directory);
        Assert.That((Handoff.Read(cycler.Directory), Handoff.Saved(cycler.Directory).Count), Is.EqualTo(((HandoffMark?)null, 0)));
    }

    [Test]
    public void The_handoff_marks_outlive_the_weekly_prune_while_the_switch_waits()
    {
        Handoff.Start(cycler, 7, "acct", Now);
        foreach (var f in new[] { Handoff.FileName, Handoff.SavedPrefix + "7", Handoff.RestoredPrefix + "0", "old.state.json" })
        {
            var path = Path.Combine(cycler.Directory, f);
            if (!File.Exists(path)) File.WriteAllText(path, "{}");
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-9));
        }
        cycler.Prune(DateTime.UtcNow);
        Assert.That(Directory.GetFiles(cycler.Directory).Select(Path.GetFileName),
            Is.EquivalentTo(new[] { Handoff.FileName, Handoff.SavedPrefix + "7", Handoff.RestoredPrefix + "0" }));
    }

    [Test]
    public void Working_is_clis_mid_turn_subagents_and_claims_no_subagent_is_on()
    {
        var roster = new[] { Row(1, idle: false), Row(2, idle: true), Row(3, idle: false, turn: null) with { LastTurnUtc = null } };
        var claims = new[]
        {
            new Assignment("A-T1", "A", @"D:\WT\A\A-T1", Providers.Claude, Now, true),
            new Assignment("A-T2", "A", @"D:\WT\A\A-T2", Providers.Claude, Now, true),
            new Assignment("A-T3", "A", @"D:\W\A", Providers.Claude, Now, false), // a tab's, not a claim
        };
        var work = Handoff.Working(roster, [("agent-1", 1)], claims, new HashSet<string>(["A-T2"], StringComparer.OrdinalIgnoreCase));
        Assert.That(work.Busy.Select(c => c.Pid), Is.EqualTo(new[] { 1 }), "idle, or never had a turn, isn't working");
        Assert.That(work.Claims.Select(a => a.TicketId), Is.EqualTo(new[] { "A-T1" }), "A-T2 has a subagent on it, so it's that subagent's work");
        Assert.That(work.Any, Is.True);
        Assert.That(Handoff.Working([Row(2, idle: true)], [], [], new HashSet<string>()).Any, Is.False);

        Assert.That(Handoff.Unstarted(claims[0], new HashSet<string>(), Now.Add(Handoff.ClaimGrace)), Is.True);
        Assert.That(Handoff.Unstarted(claims[0], new HashSet<string>(), Now.AddMinutes(1)), Is.False, "its Agent call may still be held by the pace gate");
        Assert.That(Handoff.Unstarted(claims[2], new HashSet<string>(), Now.AddHours(1)), Is.False, "a tab's assignment isn't a claim");

        var lines = Handoff.Describe(Handoff.Start(cycler, 1, null, Now), work, roster, new Dictionary<int, string> { [2] = "Repo2" }, Now.AddMinutes(1));
        Assert.That(lines, Has.Some.Contains("working: the CLI in Repo1 (pid 1), mid-turn with 1 subagent"));
        Assert.That(lines, Has.Some.Contains("claimed: A-T1 in A"));
        Assert.That(lines, Has.Some.Contains("saved: the CLI in Repo2 (pid 2)"));
        Assert.That(lines, Has.Some.Contains("not saved: the CLI in Repo1 (pid 1)"));
        Assert.That(Handoff.Describe(null, work, roster, new Dictionary<int, string>(), Now).Single(), Does.StartWith("No handoff"));
    }

    [Test]
    public void The_daemon_returns_unstarted_claims_asks_idle_tabs_to_save_and_says_quiet_once_after_two_quiet_ticks()
    {
        Winding();
        store.UpdateAssignments(all =>
        {
            all.Add(new Assignment("A-T1", "A", @"D:\WT\A\A-T1", Providers.Claude, Now.AddMinutes(-20), true)); // never started
            all.Add(new Assignment("A-T2", "A", @"D:\WT\A\A-T2", Providers.Claude, Now.AddMinutes(-20), true)); // a subagent is on it
        });
        var returned = new List<string>();
        var typed = new List<string>();
        var watch = Watch(returned, typed);
        var worked = new[] { new WorkedTicket("A-T2", "s1", "agent-1", "its subagent agent-1 was handed it") };
        var repos = new Dictionary<string, string> { ["A"] = @"D:\W\A" };
        var hosts = new[] { new HostSession("claude", 2, "pipe-2", null) };
        IReadOnlyList<CliRow> roster = [Row(1, idle: false), Row(2, idle: true, launcher: true), Row(3, idle: true)];

        var (lines, quiet) = watch.Tick(roster, hosts, worked, repos, Now);
        Assert.That(quiet, Is.False);
        Assert.That(returned, Is.EqualTo(new[] { "A-T1" }));
        Assert.That(lines, Has.Some.Contains("A-T1 was claimed but no subagent started on it: it's back in the queue"));
        Assert.That(typed, Is.EqualTo(new[] { "pipe-2:/handoff" }), "only the idle Launcher tab can be typed into; the terminal one can't");
        Assert.That(store.LoadAssignments().Select(a => a.TicketId), Is.EqualTo(new[] { "A-T2" }));

        // The busy CLI ends its turn and saves; the tab saved too. Its subagent's claim closed and is gone.
        File.WriteAllText(Path.Combine(cycler.Directory, Handoff.SavedPrefix + "2"), "Repo2");
        store.UpdateAssignments(all => all.Clear());
        roster = [Row(1, idle: true), Row(2, idle: true, launcher: true), Row(3, idle: true)];
        Assert.That(watch.Tick(roster, hosts, [], repos, Now.AddMinutes(1)).WentQuiet, Is.False, "one quiet tick could be between two turns");
        Assert.That(typed, Has.Count.EqualTo(1), "a tab is asked once");
        var (said, wentQuiet) = watch.Tick(roster, hosts, [], repos, Now.AddMinutes(2));
        Assert.That(wentQuiet, Is.True);
        Assert.That(said, Has.Some.Contains(Handoff.QuietLine));
        var mark = Handoff.Read(cycler.Directory)!;
        Assert.That((mark.State, mark.QuietUtc), Is.EqualTo((Handoff.Quiet, (DateTime?)Now.AddMinutes(2))));
        Assert.That(watch.Tick(roster, hosts, [], repos, Now.AddMinutes(3)).WentQuiet, Is.False, "said once");
    }

    [Test]
    public void A_claim_whose_worktree_has_work_is_left_said_once_and_doesnt_keep_the_handoff_from_going_quiet()
    {
        // M.ORCH-T509: it was checked and said again every tick, and counted as working for good, so the handoff never went quiet.
        Winding();
        store.UpdateAssignments(all => all.Add(new Assignment("A-T1", "A", @"D:\WT\A\A-T1", Providers.Claude, Now.AddMinutes(-20), true)));
        var tried = 0;
        var watch = new HandoffWatch(cycler, store) { Return = (_, _) => { tried++; return null; } };
        var repos = new Dictionary<string, string> { ["A"] = @"D:\W\A" };

        var said = new List<string>();
        var quiet = false;
        for (var i = 0; i < 3 && !quiet; i++)
        {
            var (lines, wentQuiet) = watch.Tick([], [], [], repos, Now.AddMinutes(i));
            said.AddRange(lines);
            quiet = wentQuiet;
        }
        Assert.That(tried, Is.EqualTo(1));
        Assert.That(said.Count(l => l.Contains("has work in it, so it's left as it is")), Is.EqualTo(1));
        Assert.That(quiet, Is.True);
        Assert.That(store.LoadAssignments().Select(a => a.TicketId), Is.EqualTo(new[] { "A-T1" }), "its worktree and claim stay");

        Winding(Now);
        store.UpdateAssignments(all => all.Add(new Assignment("B-T1", "B", @"D:\WT\B\B-T1", Providers.Claude, Now.AddMinutes(-20), true)));
        Assert.That(watch.Tick([], [], [], repos, Now.AddMinutes(5)).Lines, Has.Some.Contains("its repo B isn't in the workspace"));
    }

    [Test]
    public void A_handoff_cancelled_during_the_tick_that_goes_quiet_stays_cancelled()
    {
        // M.ORCH-T510: the tick read the mark at its start and wrote it back quiet at its end, over a /handoff off in between.
        Winding();
        var watch = new HandoffWatch(cycler, store)
        {
            Submit = (_, _) => { Handoff.Clear(cycler.Directory); return Task.FromResult(false); },
            HostOf = (pid, hosts) => hosts.FirstOrDefault(h => h.ProcessId == pid),
        };
        var repos = new Dictionary<string, string>();
        Assert.That(watch.Tick([], [], [], repos, Now).WentQuiet, Is.False);
        // The second quiet tick: /handoff off lands while it asks an idle tab to save.
        var (_, wentQuiet) = watch.Tick([Row(2, idle: true, launcher: true)], [new HostSession("claude", 2, "pipe-2", null)], [], repos, Now.AddMinutes(1));
        Assert.That(wentQuiet, Is.False);
        Assert.That(Handoff.Read(cycler.Directory), Is.Null);
    }

    [Test]
    public void Nothing_goes_quiet_while_a_subagent_runs_and_a_resumed_handoff_is_tidied_a_day_on()
    {
        Winding();
        var watch = Watch();
        // A subagent mark with no CLI recorded counts by its age alone.
        Directory.CreateDirectory(cycler.SubagentDirectory);
        File.WriteAllText(Path.Combine(cycler.SubagentDirectory, "agent-9"), "");
        for (var i = 0; i < 3; i++)
            Assert.That(watch.Tick([], [], [], new Dictionary<string, string>(), DateTime.UtcNow).WentQuiet, Is.False);
        Assert.That(Handoff.Read(cycler.Directory)!.State, Is.EqualTo(Handoff.Winding));

        var resumed = new HandoffMark(Handoff.Resumed, Handoff.Stamp(Now.AddDays(-2)), 1, "acct", Handoff.Stamp(Now.AddDays(-2)), Handoff.Stamp(Now.AddHours(-2)), []);
        Handoff.Write(cycler.Directory, resumed);
        Assert.That(Handoff.Holding(cycler.Directory), Is.False, "resumed: new work starts again");
        watch.Tick([], [], [], new Dictionary<string, string>(), Now);
        Assert.That(Handoff.Read(cycler.Directory), Is.Not.Null, "kept for CLIs still to start");
        watch.Tick([], [], [], new Dictionary<string, string>(), Now.AddDays(1));
        Assert.That(Handoff.Read(cycler.Directory), Is.Null);
    }
}
