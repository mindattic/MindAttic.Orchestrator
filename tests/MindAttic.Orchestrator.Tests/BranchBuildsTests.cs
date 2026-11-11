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
        Assert.That(BranchBuilds.TreeOf(@"D:\wt", "A", "work/x/y z"), Is.EqualTo(Path.Combine(@"D:\wt", "A", "branch-x-y-z")));
    }
}
