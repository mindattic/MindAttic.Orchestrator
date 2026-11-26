using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T1501 (first slice): the remote host builds each origin/work/* branch on its own and queues the clean ones
/// for a local test run.</summary>
public sealed class BranchBuildsTests
{
    private readonly TempDirs temps = new();
    private string dir = "";
    private JsonStore store = null!;
    private Dictionary<string, string> heads = [];
    private readonly List<string> built = [];
    private static readonly DateTime Now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    [SetUp]
    public void SetUp()
    {
        dir = temps.New();
        store = new JsonStore(Path.Combine(dir, "store"));
        heads = new() { ["work/M.ORCH-T1"] = "aaaaaaa1", ["work/M.ORCH-T2"] = "bbbbbbb1" };
        built.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        temps.DeleteAll();
    }

    private BranchBuilds Make(Func<string, BranchBuilds.BuildResult>? build = null) => new(store)
    {
        Git = (path, args) => args[0] switch
        {
            "for-each-ref" => (0, string.Join("\n", heads.Select(h => $"{h.Key} {h.Value}")) + "\n"),
            "ls-tree" => (0, "A.slnx\nsrc\n"),
            "worktree" => (0, ""),
            _ => (0, ""),
        },
        RunBuild = s => { built.Add(s); return build?.Invoke(s) ?? new BranchBuilds.BuildResult(true); },
        Spawn = w => Task.FromResult(w()),
        CheckEvery = TimeSpan.FromMinutes(1),
    };

    private IReadOnlyList<BranchBuilds.Line> Tick(BranchBuilds b, DateTime at, bool mayStart = true) =>
        b.Tick([("A", Path.Combine(dir, "A"))], Path.Combine(dir, "wt"), at, mayStart);

    [Test]
    public void Each_work_branch_is_built_in_its_own_worktree_and_the_clean_ones_are_ready_to_test()
    {
        var b = Make(s => s.Contains("T2") ? new BranchBuilds.BuildResult(false, "error CS0103") : new BranchBuilds.BuildResult(true));
        Tick(b, Now);
        Assert.That(built, Is.EquivalentTo(new[]
        {
            Path.Combine(dir, "wt", "A", "branch-M.ORCH-T1", "A.slnx"),
            Path.Combine(dir, "wt", "A", "branch-M.ORCH-T2", "A.slnx"),
        }));
        var lines = Tick(b, Now.AddSeconds(1)); // the finished builds are recorded
        Assert.That(lines.Where(l => l.Warn).Select(l => l.Text), Has.Some.Contain("fails to build"));
        var ready = BranchBuilds.ReadyToTest(BranchBuilds.Load(store));
        Assert.That(ready.Select(r => (r.Repo, r.Branch, r.Head)), Is.EqualTo(new[] { ("A", "work/M.ORCH-T1", "aaaaaaa1") }));
    }

    [Test]
    public void A_branch_is_built_again_only_once_its_head_moves()
    {
        var b = Make();
        Tick(b, Now);
        Tick(b, Now.AddMinutes(2));
        Assert.That(built, Has.Count.EqualTo(2), "nothing moved: nothing built again");
        heads["work/M.ORCH-T1"] = "aaaaaaa2";
        Tick(b, Now.AddMinutes(4));
        Assert.That(built, Has.Count.EqualTo(3));
        Assert.That(BranchBuilds.ReadyToTest(BranchBuilds.Load(store)).Any(r => r.Head == "aaaaaaa2"), Is.False, "being built: not ready yet");
        Tick(b, Now.AddMinutes(4).AddSeconds(1));
        Assert.That(BranchBuilds.ReadyToTest(BranchBuilds.Load(store)).Select(r => r.Head), Does.Contain("aaaaaaa2"));
    }

    [Test]
    public void No_more_than_MaxRunning_build_at_once_and_none_while_held_or_without_the_lease()
    {
        var pending = new List<TaskCompletionSource<BranchBuilds.BuildResult>>();
        var b = new BranchBuilds(store)
        {
            Git = (path, args) => args[0] == "for-each-ref" ? (0, string.Join("\n", heads.Select(h => $"{h.Key} {h.Value}"))) : (0, "A.slnx"),
            RunBuild = _ => new BranchBuilds.BuildResult(true),
            Spawn = _ => { var t = new TaskCompletionSource<BranchBuilds.BuildResult>(); pending.Add(t); return t.Task; },
            MaxRunning = 1,
        };
        Tick(b, Now, mayStart: false);
        Assert.That(pending, Is.Empty, "no lease: nothing starts");
        Tick(b, Now);
        Assert.That(pending, Has.Count.EqualTo(1));
        pending[0].SetResult(new BranchBuilds.BuildResult(true));
        Tick(b, Now.AddMinutes(2));
        Assert.That(pending, Has.Count.EqualTo(2), "the second starts once the first is done");
    }

    [Test]
    public void A_branch_gone_from_origin_is_forgotten()
    {
        var b = Make();
        Tick(b, Now);
        Tick(b, Now.AddSeconds(1));
        heads.Remove("work/M.ORCH-T2");
        var lines = Tick(b, Now.AddMinutes(2));
        Assert.That(lines.Select(l => l.Text), Has.Some.Contain("gone from origin"));
        Assert.That(BranchBuilds.Load(store)["A"].Keys, Is.EqualTo(new[] { "work/M.ORCH-T1" }));
    }

    [Test]
    public void Only_branches_under_work_count_and_their_folder_names_are_safe()
    {
        heads["feature/x"] = "ccccccc1";
        var b = Make();
        Assert.That(b.Branches(dir)!.Keys, Does.Not.Contain("feature/x"));
        Assert.That(Path.GetFileName(BranchBuilds.TreeOf(@"D:\wt", "A", "work/x/y z")), Does.Match("^branch-x-y-z-[0-9a-f]{8}$"));
        Assert.That(BranchBuilds.TreeOf(@"D:\wt", "A", "work/x/y"), Is.Not.EqualTo(BranchBuilds.TreeOf(@"D:\wt", "A", "work/x-y"))); // M.ORCH-T2300
    }

    // M.ORCH-T1637: a failing local run sends the branch back to the work unit that produced it, which keeps iterating on the
    // same branch, while every other branch goes on.

    private void BuildBoth()
    {
        var b = Make();
        Tick(b, Now);
        Tick(b, Now.AddSeconds(1));
    }

    [Test]
    public void A_failing_local_run_sends_the_branch_back_to_its_ticket_without_holding_the_others()
    {
        BuildBoth();
        var report = BranchBuilds.RecordTest(store, "A", "work/M.ORCH-T1", "aaaaaaa1", ok: false, "Foo_test failed", Now);
        Assert.That(report.Outcome, Is.EqualTo(BranchBuilds.ReportOutcome.SentBack));
        Assert.That(report.Ticket, Is.EqualTo("M.ORCH-T1"));

        var state = BranchBuilds.Load(store);
        var tests = BranchBuilds.LoadTests(store);
        Assert.That(BranchBuilds.ReadyToTest(state, tests).Select(r => r.Branch), Is.EqualTo(new[] { "work/M.ORCH-T2" }), "the other branch is still queued");
        var back = BranchBuilds.ReturnedToWork(state, tests).Single();
        Assert.That((back.Branch, back.Ticket, back.Note, back.Rounds), Is.EqualTo(("work/M.ORCH-T1", "M.ORCH-T1", "Foo_test failed", 1)));
        Assert.That(BranchBuilds.Brief(back), Does.Contain("same branch").And.Contain("origin/work/M.ORCH-T1").And.Contain("Foo_test failed"));
        Assert.That(BranchBuilds.BranchFor(state, "A", "M.ORCH-T1"), Is.EqualTo("work/M.ORCH-T1"), "its next round goes on the same branch");
    }

    [Test]
    public void A_new_head_on_a_returned_branch_is_built_and_queued_again()
    {
        BuildBoth();
        BranchBuilds.RecordTest(store, "A", "work/M.ORCH-T1", "aaaaaaa1", ok: false, "x", Now);
        heads["work/M.ORCH-T1"] = "aaaaaaa2";
        var b = Make();
        Tick(b, Now.AddMinutes(2));
        Tick(b, Now.AddMinutes(2).AddSeconds(1));
        var state = BranchBuilds.Load(store);
        var tests = BranchBuilds.LoadTests(store);
        Assert.That(BranchBuilds.ReturnedToWork(state, tests), Is.Empty, "the work unit moved on");
        Assert.That(BranchBuilds.ReadyToTest(state, tests).Select(r => r.Head), Does.Contain("aaaaaaa2"));

        var second = BranchBuilds.RecordTest(store, "A", "work/M.ORCH-T1", "aaaaaaa2", ok: false, "y", Now.AddMinutes(3));
        Assert.That(BranchBuilds.LoadTests(store)["A"]["work/M.ORCH-T1"].Rounds, Is.EqualTo(2), second.Text);
        BranchBuilds.RecordTest(store, "A", "work/M.ORCH-T1", "aaaaaaa2", ok: false, "y", Now.AddMinutes(4));
        Assert.That(BranchBuilds.LoadTests(store)["A"]["work/M.ORCH-T1"].Rounds, Is.EqualTo(2), "the same result twice counts once");
    }

    [Test]
    public void A_result_for_an_old_head_is_dropped_and_a_pass_signs_off()
    {
        BuildBoth();
        Assert.That(BranchBuilds.RecordTest(store, "A", "work/M.ORCH-T1", "aaaaaaa0", ok: false, null, Now).Outcome, Is.EqualTo(BranchBuilds.ReportOutcome.Stale));
        Assert.That(BranchBuilds.RecordTest(store, "A", "work/nope", "aaaaaaa1", ok: true, null, Now).Outcome, Is.EqualTo(BranchBuilds.ReportOutcome.Unknown));
        Assert.That(BranchBuilds.RecordTest(store, "A", "work/M.ORCH-T2", "bbbbbbb1", ok: true, null, Now).Outcome, Is.EqualTo(BranchBuilds.ReportOutcome.Passed));
        var state = BranchBuilds.Load(store);
        var tests = BranchBuilds.LoadTests(store);
        Assert.That(BranchBuilds.SignedOff(state, tests).Select(r => r.Branch), Is.EqualTo(new[] { "work/M.ORCH-T2" }));
        Assert.That(BranchBuilds.ReadyToTest(state, tests).Select(r => r.Branch), Is.EqualTo(new[] { "work/M.ORCH-T1" }));
    }

    [Test]
    public void An_unreadable_record_of_local_runs_is_not_overwritten_by_the_next_report()
    {
        BuildBoth();
        BranchBuilds.RecordTest(store, "A", "work/M.ORCH-T2", "bbbbbbb1", ok: true, null, Now);
        StoreFaults.Garble(store, BranchBuilds.TestsName, "{ \"A\": { trunc");

        var report = BranchBuilds.RecordTest(store, "A", "work/M.ORCH-T1", "aaaaaaa1", ok: false, "x", Now);

        Assert.That(report.Outcome, Is.EqualTo(BranchBuilds.ReportOutcome.Unreadable));
        Assert.That(store.TryLoad<Dictionary<string, Dictionary<string, BranchBuilds.TestState>>>(BranchBuilds.TestsName, out _), Is.False,
            "the stored record is left as it was, not replaced by one holding only this report");
    }

    [Test]
    public void A_finished_merge_round_is_kept_while_the_merge_record_is_unreadable_and_settled_once_it_is()
    {
        BuildBoth();
        BranchBuilds.RecordTest(store, "A", "work/M.ORCH-T2", "bbbbbbb1", ok: true, null, Now);
        var round = new TaskCompletionSource<IReadOnlyList<BranchMerges.Outcome>>();
        var settled = new List<string>();
        var merges = new BranchMerges(store)
        {
            Spawn = _ => round.Task,
            Settle = o => { settled.Add(o.Branch); return null; },
            CheckEvery = TimeSpan.FromMinutes(1),
        };
        (string, string)[] repos = [("A", Path.Combine(dir, "A"))];
        var trees = Path.Combine(dir, "wt");

        merges.Tick(repos, trees, Now);
        Assert.That(merges.Running, Is.True);
        round.SetResult([new BranchMerges.Outcome("A", "work/M.ORCH-T2", "bbbbbbb1", "ccccccc1", null)]);
        StoreFaults.Garble(store, BranchMerges.StateName, "{ trunc");

        Assert.That(merges.Tick(repos, trees, Now.AddSeconds(1)), Is.Empty);
        Assert.That(settled, Is.Empty, "nothing settled while its record can't be read");
        Assert.That(merges.Running, Is.True, "the round's results are kept for the next tick");

        StoreFaults.Garble(store, BranchMerges.StateName, "{}");
        merges.Tick(repos, trees, Now.AddSeconds(2));
        Assert.That(settled, Is.EqualTo(new[] { "work/M.ORCH-T2" }), "the merged ticket is settled, not later handed back as gone");
        Assert.That(merges.Running, Is.False);
        Assert.That(BranchMerges.Load(store)["A"]["work/M.ORCH-T2"].Merged, Is.EqualTo("ccccccc1"));
    }

    [Test]
    public void A_work_branch_names_its_ticket()
    {
        Assert.That(BranchBuilds.WorkUnitOf("work/M.ORCH-T1637"), Is.EqualTo("M.ORCH-T1637"));
        Assert.That(BranchBuilds.WorkUnitOf("work/M.ORCH-T1637-retry"), Is.EqualTo("M.ORCH-T1637"));
        Assert.That(BranchBuilds.WorkUnitOf("work/spike"), Is.Null);
    }
}
