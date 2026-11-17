using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Setup;
using MindAttic.Tickets.Log;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T1633 (a M.ORCH-T1501 follow-up): with RemoteBranchBuilds on, /allocate on the remote host works each ticket on
/// its own origin/work/&lt;ticket&gt; branch; with it off, or on any machine but the host, the prompt is today's, byte for byte.</summary>
public sealed class BranchWorkTests
{
    private readonly TempDirs temps = new();
    private JsonStore store = null!;
    private static readonly DateTime Now = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
    private static readonly MachineTopology Host = new(TopologyKind.Host);

    /// <summary>Today's prompt for A-T1 in D:\W\A on main (no Sonnet review step): the text every machine but the host with
    /// branch builds on must keep printing.</summary>
    private const string MainPrompt =
        """Work on ticket A-T1 (fix): summary of A-T1 Work in A's own checkout (D:\W\A) on main, never a separate branch or worktree: run every command from that folder (cd there first). Other agents may be working A in this same checkout at the same time, so touch only the files this ticket needs and leave every change you didn't make as it is (never git add -A or git add ., git stash, git reset, git checkout -- or git clean). If the checkout isn't on main, don't switch it: say so and stop. Run any subagents in the foreground, never in the background. Do these steps in order: 1. When the work is done, build it to check it; do not run any tests (not even the ones your change touches), because once you land, the orchestrator runs A's full suite on main itself and files a fix ticket for any test that fails, so don't wait for a full run. 2. Right before committing, re-read each file you changed and confirm it still has the edit you made (this checkout is shared, so another agent's own concurrent edit, pull, or commit on the same path can overwrite or drop yours with no error shown): if any doesn't match, STOP without committing it — don't commit whatever is currently on disk and don't assume your edit is simply gone — and say so loudly, naming the path, so it can be re-checked instead of silently lost or bundled into the wrong commit. Otherwise commit this ticket's work alone, as one commit, naming every file you changed: git add <path> ... for new files, then plain git commit -m "<subject>" -- <path> <path> ... (the -- and the paths are required: other agents share this checkout and may have files staged, and a commit without paths would take theirs too) (not /commit, whose gate runs the test suite), with A-T1 at the end of the subject; no push yet, the steps below do that, so keep going. 3. Land it on A's main line, main: git fetch origin main, then git merge --no-edit origin/main (never git pull --rebase --autostash, git stash or git reset here: they sweep other agents' uncommitted work; a merge leaves it alone and git refuses it safely if incoming changes touch a file someone has dirty, so wait a moment and retry; resolve any conflicts in your own files, then rebuild), then git push origin HEAD:main. If the push is rejected because main moved, fetch and merge again (still no stash) and push again. 4. Only once the push has succeeded, as its own step, mark it finished: tickets ticket close A-T1 --ref <the commit hash as it landed on main>. You are done only when the push has succeeded and the ticket is closed. If you can't finish or land it, leave the ticket open and say why.""";

    [SetUp]
    public void SetUp() => store = new JsonStore(Path.Combine(temps.New(), "store"));

    [TearDown]
    public void TearDown()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        temps.DeleteAll();
    }

    private static Snapshot Snap() =>
        new(Now, [new RepoStatus("A", @"D:\W\A", RepoState.Dormant, [], [], [new Ticket("A-T1", "2026-10-01", "fix", "open", "summary of A-T1", "")], null, default)], []);

    private static OrchestratorSettings Settings(bool branches) => new() { WorktreeRoot = @"D:\WT", RemoteBranchBuilds = branches };

    private Allocate.Handout Claim(OrchestratorSettings settings, MachineTopology topology)
    {
        var (handed, why) = Allocate.Next(Scheduler.Plan(Snap(), settings, []), settings, store, ["A"], 1, claim: true, Now, _ => "main",
            pilotRunning: _ => false, stillOpen: (_, _) => true, topology: topology);
        Assert.That(why, Is.Null);
        return handed.Single();
    }

    [Test]
    public void The_main_line_prompt_is_unchanged_byte_for_byte()
    {
        var item = Scheduler.Plan(Snap(), Settings(false), []).Queue.Single();
        Assert.That(Allocate.PromptFor(item, @"D:\W\A"), Is.EqualTo(MainPrompt));
        Assert.That(Allocate.PromptFor(item, @"D:\W\A", "main", null, null), Is.EqualTo(MainPrompt));
    }

    [TestCase(TopologyKind.Host, false)]
    [TestCase(TopologyKind.Standalone, false)]
    [TestCase(TopologyKind.Client, false)]
    [TestCase(TopologyKind.Standalone, true)]
    [TestCase(TopologyKind.Client, true)]
    [TestCase(TopologyKind.MemberClient, true)]
    public void With_the_setting_off_or_off_the_host_a_claim_is_worked_in_the_checkout_on_main_as_today(TopologyKind kind, bool setting)
    {
        var topology = new MachineTopology(kind);
        Assert.That(Allocate.WorksOnBranches(Settings(setting), topology), Is.False);
        var h = Claim(Settings(setting), topology);
        Assert.That((h.Directory, h.Branch), Is.EqualTo((@"D:\W\A", (string?)null)));
        Assert.That(h.Prompt, Is.EqualTo(Allocate.PromptFor(h.Item, @"D:\W\A", "main", h.Model)), "the same prompt, byte for byte");
        if (h.Model is not { IsSonnet: true }) Assert.That(h.Prompt, Is.EqualTo(MainPrompt));
        Assert.That(h.Prompt, Does.Not.Contain("work/"));
        Assert.That(Allocate.BranchWorkFor(Settings(setting), topology, store, h.Item), Is.Null);
    }

    [Test]
    public void On_the_host_with_branch_builds_on_a_claim_is_worked_on_its_own_work_branch_never_main()
    {
        var h = Claim(Settings(true), Host);
        Assert.That(h.Branch, Is.EqualTo("work/A-T1"));
        Assert.That(h.Directory, Is.EqualTo(Path.Combine(@"D:\WT", "A", "work-A-T1")), "its own worktree, apart from the host's build and merge ones");
        Assert.That(h.Prompt, Does.StartWith("Work on ticket A-T1 (fix): summary of A-T1 ")
            .And.Contain("its own branch, work/A-T1, never on main")
            .And.Contain(@"worktree add -B work/A-T1 ""D:\WT\A\work-A-T1"" origin/work/A-T1")
            .And.Contain("git push origin HEAD:work/A-T1")
            .And.Contain("tickets ticket close A-T1 --ref <the commit hash as it landed on origin/work/A-T1>")
            .And.Not.Contain("HEAD:main")
            .And.Not.Contain("Work in A's own checkout")
            .And.Not.Contain("earlier round"));
        Assert.That(store.LoadAssignments().Single().Directory, Is.EqualTo(@"D:\W\A"), "the claim still names the repo's checkout");
    }

    [Test]
    public void A_ticket_whose_branch_came_back_carries_on_on_that_branch_with_what_failed()
    {
        store.Save(BranchBuilds.StateName, new Dictionary<string, Dictionary<string, BranchBuilds.BranchState>>
        {
            ["A"] = new() { ["work/A-T1-retry"] = new() { Head = "aaaaaaa1", Built = "aaaaaaa1", Ok = true } },
        });
        store.Save(BranchBuilds.TestsName, new Dictionary<string, Dictionary<string, BranchBuilds.TestState>>
        {
            ["A"] = new() { ["work/A-T1-retry"] = new() { Head = "aaaaaaa1", Ok = false, Note = "Foo_test failed", Rounds = 1, ReturnedTo = "A-T1" } },
        });
        var h = Claim(Settings(true), Host);
        Assert.That(h.Branch, Is.EqualTo("work/A-T1-retry"), "the branch it already has, not a new one");
        Assert.That(h.Prompt, Does.Contain("Foo_test failed").And.Contain("already has work from an earlier round")
            .And.Contain("git push origin HEAD:work/A-T1-retry"));
    }
}
