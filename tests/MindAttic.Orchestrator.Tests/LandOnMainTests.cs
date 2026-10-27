using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// Allocate.LandOnMain against real temp git repos with a bare origin (M.ORCH-T134): whether a worktree agent's ticket
/// landed closed on origin/main, what happens to your checkout and the worktree, and what the note says; plus ClosedIn.
/// </summary>
public sealed class LandOnMainTests
{
    private const string Header = "## Tickets\n\n| ID | Date | Type | Status | Summary | Ref |\n|---|---|---|---|---|---|\n";
    private static readonly DateTime Now = new(2026, 10, 16, 6, 0, 0, DateTimeKind.Utc);
    private string dir = "";
    private string origin = "";
    private string repo = "";
    private string tree = "";
    private Assignment a = null!;

    [SetUp]
    public void SetUp()
    {
        // With the ticket database's env var set, LandOnMain would read the ticket's status from that database instead.
        Assume.That(Environment.GetEnvironmentVariable(MindAttic.Tickets.Data.TicketsDb.EnvVar), Is.Null.Or.Empty);
        dir = Path.Combine(Path.GetTempPath(), "orch-land-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        origin = Path.Combine(dir, "origin.git");
        repo = Path.Combine(dir, "A");
        Git(dir, "init", "-q", "--bare", "-b", "main", origin);
        Git(dir, "clone", "-q", origin, repo);
        Git(repo, "config", "user.email", "t@t");
        Git(repo, "config", "user.name", "t");
        Git(repo, "symbolic-ref", "HEAD", "refs/heads/main");
        Directory.CreateDirectory(Path.Combine(repo, "docs"));
        WriteStories(repo, "open");
        Git(repo, "add", "-A");
        Git(repo, "commit", "-qm", "init");
        Git(repo, "push", "-q", "-u", "origin", "main");
        Git(repo, "remote", "set-head", "origin", "main");

        tree = Path.Combine(dir, "wt", "A-T1");
        Assert.That(LegacyWorktree.Add(repo, tree, Dispatcher.BranchFor("A-T1")).Ok, Is.True);
        a = new Assignment("A-T1", "A", tree, "Claude", Now, true);
    }

    [TearDown]
    public void TearDown()
    {
        // M.ORCH-T470: a delete that fails is a folder of git repos left in %TEMP% every run, so it's retried, then reported.
        if (dir.Length > 0 && !TempDirs.Delete(dir)) Assert.Fail($"Couldn't delete the test's git repos at {dir}");
    }

    [Test]
    public void A_closed_ticket_whose_branch_is_on_origin_main_fast_forwards_the_checkout_and_removes_the_worktree()
    {
        CloseInWorktreeAndCommit();
        Git(tree, "push", "-q", "origin", "HEAD:main");

        var (closed, note) = Allocate.LandOnMain(a, repo);

        Assert.That(closed, Is.True, note);
        Assert.That(note, Does.Contain("your checkout is up to date with it"));
        Assert.That(File.ReadAllText(Path.Combine(repo, "docs", "USER_STORIES.md")), Does.Contain("| A-T1 | 2026-10-01 | fix | closed |"),
            "your checkout on main fast-forwarded to origin/main");
        Assert.That(Head(repo), Is.EqualTo(Head(origin, "main")));
        Assert.That(Directory.Exists(tree), Is.False, "the worktree is removed");
        Assert.That(Dispatcher.Git(repo, "worktree", "list").Output, Does.Not.Contain("A-T1"));
    }

    [Test]
    public void A_closed_ticket_whose_branch_is_not_on_main_is_not_done_and_names_the_branch()
    {
        // Someone closed the row on main, but the agent's own commit never got there.
        WriteStories(repo, "closed");
        Git(repo, "commit", "-qam", "close A-T1 from elsewhere");
        Git(repo, "push", "-q", "origin", "main");
        File.WriteAllText(Path.Combine(tree, "work.txt"), "the fix");
        Git(tree, "add", "-A");
        Git(tree, "commit", "-qm", "the fix, never pushed");

        var (closed, note) = Allocate.LandOnMain(a, repo);

        Assert.That(closed, Is.False, note);
        Assert.That(note, Does.Contain("closed its ticket but its branch orch/a-t1 isn't on main"));
        Assert.That(note, Does.Contain(tree), "it says where the work is");
        Assert.That(Directory.Exists(tree), Is.True, "the unlanded work is kept");
        Assert.That(File.Exists(Path.Combine(tree, "work.txt")), Is.True);
    }

    [Test]
    public void A_ticket_still_open_keeps_its_work_and_says_where_it_is()
    {
        File.WriteAllText(Path.Combine(tree, "work.txt"), "half done");
        Git(tree, "add", "-A");
        Git(tree, "commit", "-qm", "progress");
        Git(tree, "push", "-q", "origin", "HEAD:main"); // even landed on main, an open ticket isn't finished

        var (closed, note) = Allocate.LandOnMain(a, repo);

        Assert.That(closed, Is.False, note);
        Assert.That(note, Is.EqualTo($"; its work is in {tree} (branch orch/a-t1)"));
        Assert.That(Directory.Exists(tree), Is.True);
        Assert.That(Dispatcher.Git(repo, "worktree", "list").Output, Does.Contain("A-T1"));
    }

    [Test]
    public void A_dirty_worktree_of_a_landed_closed_ticket_is_kept()
    {
        CloseInWorktreeAndCommit();
        Git(tree, "push", "-q", "origin", "HEAD:main");
        File.WriteAllText(Path.Combine(tree, "docs", "USER_STORIES.md"), "uncommitted edit\n");

        var (closed, note) = Allocate.LandOnMain(a, repo);

        Assert.That(closed, Is.True, note);
        Assert.That(note, Does.Contain($"its worktree {tree} kept (something there is uncommitted)"));
        Assert.That(File.ReadAllText(Path.Combine(tree, "docs", "USER_STORIES.md")), Is.EqualTo("uncommitted edit\n"),
            "the uncommitted change survives");
    }

    [Test]
    public void A_checkout_not_on_main_is_not_touched()
    {
        Git(repo, "checkout", "-q", "-b", "mine");
        File.WriteAllText(Path.Combine(repo, "mine.txt"), "my own work");
        Git(repo, "add", "-A");
        Git(repo, "commit", "-qm", "mine");
        var mineBefore = Head(repo);
        var mainBefore = Dispatcher.Git(repo, "rev-parse", "main").Output.Trim();
        CloseInWorktreeAndCommit();
        Git(tree, "push", "-q", "origin", "HEAD:main");

        var (closed, note) = Allocate.LandOnMain(a, repo);

        Assert.That(closed, Is.True, note);
        Assert.That(note, Does.Not.Contain("up to date").And.Not.Contain("pull"), "your checkout isn't on main: nothing said about it");
        Assert.That(Dispatcher.Git(repo, "rev-parse", "--abbrev-ref", "HEAD").Output.Trim(), Is.EqualTo("mine"));
        Assert.That(Head(repo), Is.EqualTo(mineBefore), "your branch isn't moved");
        Assert.That(Dispatcher.Git(repo, "rev-parse", "main").Output.Trim(), Is.EqualTo(mainBefore), "nor is your local main");
        Assert.That(Directory.Exists(tree), Is.False, "the finished worktree is still tidied");
    }

    [Test]
    public void ClosedIn_reads_closed_and_wont_do_as_done_open_as_not_and_matches_the_whole_id()
    {
        const string table = Header +
            "| A-T1 | 2026-10-01 | fix | closed | x | the test |\n" +
            "| A-T2 | 2026-10-01 | fix | wont-do | y | — |\n" +
            "| A-T3 | 2026-10-01 | fix | open | z | — |\n";
        Assert.That(Allocate.ClosedIn(table, "A-T1"), Is.True, "closed");
        Assert.That(Allocate.ClosedIn(table, "A-T2"), Is.True, "wont-do");
        Assert.That(Allocate.ClosedIn(table, "A-T3"), Is.False, "open");
        Assert.That(Allocate.ClosedIn(table, "A-T4"), Is.False, "not in the table");
        Assert.That(Allocate.ClosedIn(table.Replace("\n", "\r\n"), "A-T1"), Is.True, "CRLF line endings");

        Assert.That(Allocate.ClosedIn(Header + "| M.ORCH-T10 | 2026-10-01 | fix | closed | x | t |\n", "M.ORCH-T1"), Is.False,
            "M.ORCH-T10 closed doesn't close M.ORCH-T1");
        var both = Header + "| M.ORCH-T1 | 2026-10-01 | fix | closed | x | t |\n| M.ORCH-T10 | 2026-10-01 | fix | open | y | — |\n";
        Assert.That(Allocate.ClosedIn(both, "M.ORCH-T1"), Is.True);
        Assert.That(Allocate.ClosedIn(both, "M.ORCH-T10"), Is.False, "M.ORCH-T1 closed doesn't close M.ORCH-T10");
    }

    private void CloseInWorktreeAndCommit()
    {
        WriteStories(tree, "closed");
        Git(tree, "commit", "-qam", "close A-T1");
    }

    private static void WriteStories(string root, string status) =>
        File.WriteAllText(Path.Combine(root, "docs", "USER_STORIES.md"),
            Header + $"| A-T1 | 2026-10-01 | fix | {status} | x | {(status == "open" ? "—" : "the test")} |\n");

    private static string Head(string where, string rev = "HEAD") => Dispatcher.Git(where, "rev-parse", rev).Output.Trim();

    private static void Git(string where, params string[] args)
    {
        var (code, output) = Dispatcher.Git(where, args);
        Assert.That(code, Is.Zero, $"git {string.Join(" ", args)}: {output}");
    }
}
