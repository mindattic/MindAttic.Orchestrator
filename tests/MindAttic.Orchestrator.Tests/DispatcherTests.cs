using System.Diagnostics;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using MindAttic.Workflow.Tickets;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// Dispatcher against real temp git repos (M.ORCH-T136): EnsureWorktree reuses, checks out or creates the ticket's
/// branch and says what git said when it can't; Git is killed past its timeout and never waits on a credential prompt;
/// Dispatch and Release change assignments through UpdateAssignments; RemoveWorktree keeps a dirty tree and the branch.
/// No Windows Terminal tab is opened (seam: OpenTab).
/// </summary>
public sealed class DispatcherTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 6, 0, 0, DateTimeKind.Utc);
    private string dir = "";
    private string repo = "";
    private string wtRoot = "";
    private JsonStore store = null!;

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "orch-dispatch-" + Guid.NewGuid().ToString("N"));
        repo = Path.Combine(dir, "A");
        wtRoot = Path.Combine(dir, "wt");
        Directory.CreateDirectory(repo);
        Git(repo, "init", "-q", "-b", "main");
        Git(repo, "config", "user.email", "t@t");
        Git(repo, "config", "user.name", "t");
        File.WriteAllText(Path.Combine(repo, "file.txt"), "main\n");
        Git(repo, "add", "-A");
        Git(repo, "commit", "-qm", "init");
        store = new JsonStore(Path.Combine(dir, "store"));
        store.SaveSettings(new OrchestratorSettings { WorktreeRoot = wtRoot });
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(dir, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    // ---- EnsureWorktree ----

    [Test]
    public void A_worktree_folder_deleted_by_hand_is_made_again_not_refused_for_good()
    {
        var tree = Path.Combine(wtRoot, "A", "A-T5");
        Assert.That(Dispatcher.EnsureWorktree(repo, tree, "orch/a-t5").Ok, Is.True);
        Directory.Delete(tree, recursive: true); // Explorer: git still has it registered, its branch checked out there

        var (ok, path, error) = Dispatcher.EnsureWorktree(repo, tree, "orch/a-t5");

        Assert.That(ok, Is.True, error);
        Assert.That(Directory.Exists(Path.Combine(path, ".git")) || File.Exists(Path.Combine(path, ".git")), Is.True);
    }

    [Test]
    public void An_existing_worktree_is_reused_as_it_is()
    {
        var tree = Path.Combine(wtRoot, "A", "A-T1");
        Assert.That(Dispatcher.EnsureWorktree(repo, tree, "orch/a-t1").Ok, Is.True);
        File.WriteAllText(Path.Combine(tree, "work.txt"), "in progress");
        var listed = Dispatcher.Git(repo, "worktree", "list", "--porcelain").Output;

        var (ok, path, error) = Dispatcher.EnsureWorktree(repo, tree, "orch/a-t1");

        Assert.That(ok, Is.True, error);
        Assert.That(path, Is.EqualTo(tree));
        Assert.That(File.ReadAllText(Path.Combine(tree, "work.txt")), Is.EqualTo("in progress"), "the agent's work is untouched");
        Assert.That(Dispatcher.Git(repo, "worktree", "list", "--porcelain").Output, Is.EqualTo(listed), "no second worktree");
    }

    [Test]
    public void A_clean_landed_worktree_handed_out_again_is_moved_up_to_the_main_line()
    {
        // M.ORCH-T367: its claim went stale (or the ticket reopened) while its worktree stayed, days behind main.
        var tree = Path.Combine(wtRoot, "A", "A-T5");
        Assert.That(Dispatcher.EnsureWorktree(repo, tree, "orch/a-t5").Ok, Is.True);
        File.WriteAllText(Path.Combine(repo, "file.txt"), "main moved on\n");
        Git(repo, "commit", "-qam", "later work on main");

        var (ok, _, error) = Dispatcher.EnsureWorktree(repo, tree, "orch/a-t5");

        Assert.That(ok, Is.True, error);
        Assert.That(Head(tree), Is.EqualTo(Head(repo)), "fast-forwarded to the main line");
        Assert.That(File.ReadAllText(Path.Combine(tree, "file.txt")), Is.EqualTo("main moved on\n"));
    }

    [Test]
    public void An_existing_branch_is_checked_out_not_recreated()
    {
        Git(repo, "switch", "-q", "-c", "orch/a-t2");
        File.WriteAllText(Path.Combine(repo, "file.txt"), "earlier work\n");
        Git(repo, "commit", "-qam", "earlier work");
        var branchTip = Head(repo);
        Git(repo, "switch", "-q", "main");
        var tree = Path.Combine(wtRoot, "A", "A-T2");

        var (ok, _, error) = Dispatcher.EnsureWorktree(repo, tree, "orch/a-t2");

        Assert.That(ok, Is.True, error);
        Assert.That(Head(tree), Is.EqualTo(branchTip), "the branch's earlier work is what's checked out");
        Assert.That(Dispatcher.Git(tree, "branch", "--show-current").Output.Trim(), Is.EqualTo("orch/a-t2"));
        Assert.That(File.ReadAllText(Path.Combine(tree, "file.txt")), Is.EqualTo("earlier work\n"));
    }

    [Test]
    public void A_landed_branch_claimed_again_starts_from_the_main_line_not_where_it_landed()
    {
        // M.ORCH-T360: the ticket's work landed, the main line moved on, the ticket was reopened and claimed again.
        Git(repo, "branch", "orch/a-t4");
        File.WriteAllText(Path.Combine(repo, "file.txt"), "main moved on\n");
        Git(repo, "commit", "-qam", "later work on main");
        var tree = Path.Combine(wtRoot, "A", "A-T4");

        var (ok, _, error) = Dispatcher.EnsureWorktree(repo, tree, "orch/a-t4");

        Assert.That(ok, Is.True, error);
        Assert.That(Head(tree), Is.EqualTo(Head(repo)), "from the main line as it is now");
        Assert.That(Dispatcher.Git(tree, "branch", "--show-current").Output.Trim(), Is.EqualTo("orch/a-t4"));
    }

    [Test]
    public void A_new_branch_is_created_from_HEAD_when_there_is_no_origin()
    {
        var tree = Path.Combine(wtRoot, "A", "A-T3");

        var (ok, _, error) = Dispatcher.EnsureWorktree(repo, tree, "orch/a-t3");

        Assert.That(ok, Is.True, error);
        Assert.That(Dispatcher.Git(repo, "rev-parse", "--verify", "--quiet", "refs/heads/orch/a-t3").Code, Is.Zero, "the branch exists");
        Assert.That(Head(tree), Is.EqualTo(Head(repo)), "it starts where your checkout's HEAD is");
        Assert.That(Dispatcher.Git(tree, "branch", "--show-current").Output.Trim(), Is.EqualTo("orch/a-t3"));
    }

    [Test]
    public void A_failed_worktree_add_reports_what_git_said()
    {
        // A folder that's there but isn't a worktree: git won't add one over it.
        var tree = Path.Combine(wtRoot, "A", "A-T4");
        Directory.CreateDirectory(tree);
        File.WriteAllText(Path.Combine(tree, "stray.txt"), "not a worktree");

        var (ok, path, error) = Dispatcher.EnsureWorktree(repo, tree, "orch/a-t4");

        Assert.That(ok, Is.False);
        Assert.That(path, Is.EqualTo(tree));
        Assert.That(error, Does.StartWith("git worktree add failed: "));
        Assert.That(error, Does.Contain("already exists"), "git's own words are passed on");
    }

    // ---- Git ----

    [Test]
    public void A_git_call_past_its_timeout_is_killed_and_says_so()
    {
        Git(repo, "config", "alias.hang", "!sleep 60");
        var clock = Stopwatch.StartNew();

        var (code, output) = Dispatcher.Git(TimeSpan.FromSeconds(2), repo, "hang");

        Assert.That(code, Is.EqualTo(-1));
        Assert.That(output, Is.EqualTo("git hang took over 2s; stopped it"));
        Assert.That(clock.Elapsed, Is.LessThan(TimeSpan.FromSeconds(30)), "control comes back at the timeout, not when git finishes");
    }

    [Test]
    public void A_helper_outliving_git_with_its_pipes_does_not_hold_the_call_past_the_timeout()
    {
        // git exits at once, but what its alias left running in the background still holds its output.
        Git(repo, "config", "alias.linger", "!sleep 8 &");
        var clock = Stopwatch.StartNew();

        var (code, output) = Dispatcher.Git(TimeSpan.FromSeconds(2), repo, "linger");

        Assert.That(clock.Elapsed, Is.LessThan(TimeSpan.FromSeconds(6)));
        Assert.That((code, output), Is.EqualTo((-1, "git linger took over 2s; stopped it")));
    }

    [Test]
    public void Git_never_waits_on_a_credential_prompt()
    {
        Git(repo, "config", "alias.prompt", "!echo prompt=$GIT_TERMINAL_PROMPT");

        var (code, output) = Dispatcher.Git(repo, "prompt");

        Assert.That(code, Is.Zero, output);
        Assert.That(output.Trim(), Is.EqualTo("prompt=0"));
    }

    [Test]
    public void The_default_git_timeout_is_two_minutes() =>
        Assert.That(Dispatcher.GitTimeout, Is.EqualTo(TimeSpan.FromMinutes(2)));

    // ---- Dispatch and Release ----

    [Test]
    public void Dispatch_in_a_worktree_records_the_assignment_through_UpdateAssignments()
    {
        var stale = Assign("A-T1", "A", @"C:\stale", false);
        store.SaveAssignments([Assign("B-T9", "B", @"C:\elsewhere", false), stale]);
        var opened = new List<(string Title, string Directory, string Provider, string Prompt, bool Submit)>();
        var dispatcher = Make((title, directory, provider, prompt, submit) =>
        {
            opened.Add((title, directory, provider, prompt, submit));
            // Another process records one while the tab is opening: Dispatch's save mustn't drop it.
            store.UpdateAssignments(all => all.Add(Assign("C-T5", "C", @"C:\other", false)));
            return (null, Task.FromResult(true));
        });

        var result = dispatcher.Dispatch(Item("A-T1") with { Assignment = stale, StaleSinceUtc = Now }, "Claude", useWorktree: true, submit: true);

        var tree = Path.Combine(wtRoot, "A", "A-T1");
        Assert.That(result.Ok, Is.True, result.Message);
        Assert.That(result.Message, Does.Contain($"worktree {tree} (branch orch/a-t1)"));
        Assert.That(result.Message, Does.Contain("submits by itself"));
        Assert.That(result.CameUp!.Result, Is.True);
        Assert.That(opened, Has.Count.EqualTo(1));
        Assert.That(opened[0].Title, Is.EqualTo("A A-T1"));
        Assert.That(opened[0].Directory, Is.EqualTo(tree));
        Assert.That(opened[0].Submit, Is.True);
        Assert.That(opened[0].Prompt, Is.EqualTo(Scheduler.PromptFor(Item("A-T1"))));
        Assert.That(Directory.Exists(tree), Is.True);

        var saved = store.LoadAssignments();
        Assert.That(saved.Select(a => a.TicketId), Is.EquivalentTo(new[] { "B-T9", "C-T5", "A-T1" }));
        var mine = saved.Single(a => a.TicketId == "A-T1");
        Assert.That(mine.Directory, Is.EqualTo(tree), "the stale entry is replaced, not duplicated");
        Assert.That(mine.Worktree, Is.True);
        Assert.That(mine.Provider, Is.EqualTo("Claude"));
        Assert.That(result.Assignment, Is.EqualTo(mine));
    }

    [Test]
    public void Dispatch_in_the_repo_itself_opens_there_and_leaves_the_prompt_to_you()
    {
        var dispatcher = Make((_, _, _, _, _) => (null, Task.FromResult(true)));

        var result = dispatcher.Dispatch(Item("A-T1"), "Codex", useWorktree: false);

        Assert.That(result.Ok, Is.True, result.Message);
        Assert.That(result.Message, Does.Contain("press Enter in the tab"));
        var mine = store.LoadAssignments().Single();
        Assert.That((mine.Directory, mine.Worktree, mine.Provider), Is.EqualTo((repo, false, "Codex")));
        Assert.That(Directory.Exists(wtRoot), Is.False, "no worktree");
    }

    [Test]
    public void A_ticket_claimed_elsewhere_since_the_plan_opens_no_tab_and_keeps_that_claim()
    {
        var claim = Assign("A-T1", "A", @"D:\WT\A\A-T1", true); // an /allocate CLI's `next --claim`
        store.SaveAssignments([claim]);
        var opened = 0;
        var dispatcher = Make((_, _, _, _, _) => { opened++; return (null, Task.FromResult(true)); });

        var result = dispatcher.Dispatch(Item("A-T1"), "Claude", useWorktree: false); // the plan saw it unassigned

        Assert.That((result.Ok, opened), Is.EqualTo((false, 0)));
        Assert.That(result.Message, Does.Contain("claimed elsewhere"));
        Assert.That(store.LoadAssignments(), Is.EqualTo(new[] { claim }));
    }

    [Test]
    public void A_tab_that_fails_to_open_records_nothing()
    {
        store.SaveAssignments([Assign("B-T9", "B", @"C:\elsewhere", false)]);
        var dispatcher = Make((_, _, _, _, _) => ("Launcher not found", null));

        var result = dispatcher.Dispatch(Item("A-T1"), "Claude", useWorktree: false);

        Assert.That(result.Ok, Is.False);
        Assert.That(result.Message, Is.EqualTo("Launcher not found"));
        Assert.That(result.Assignment, Is.Null);
        Assert.That(store.LoadAssignments().Select(a => a.TicketId), Is.EqualTo(new[] { "B-T9" }));
    }

    [Test]
    public void A_worktree_that_cannot_be_made_opens_no_tab()
    {
        var tree = Path.Combine(wtRoot, "A", "A-T1");
        Directory.CreateDirectory(tree);
        File.WriteAllText(Path.Combine(tree, "stray.txt"), "x");
        var opened = 0;
        var dispatcher = Make((_, _, _, _, _) => { opened++; return (null, Task.FromResult(true)); });

        var result = dispatcher.Dispatch(Item("A-T1"), "Claude", useWorktree: true);

        Assert.That(result.Ok, Is.False);
        Assert.That(result.Message, Does.StartWith("git worktree add failed: "));
        Assert.That(opened, Is.Zero);
        Assert.That(store.LoadAssignments(), Is.Empty);
    }

    [Test]
    public void Release_drops_only_that_ticket_and_keeps_its_worktree()
    {
        var tree = Path.Combine(wtRoot, "A", "A-T1");
        Assert.That(Dispatcher.EnsureWorktree(repo, tree, "orch/a-t1").Ok, Is.True);
        store.SaveAssignments([Assign("A-T1", "A", tree, true), Assign("B-T9", "B", @"C:\elsewhere", false)]);

        Make().Release("A-T1");

        Assert.That(store.LoadAssignments().Select(a => a.TicketId), Is.EqualTo(new[] { "B-T9" }));
        Assert.That(Directory.Exists(tree), Is.True);
    }

    // ---- RemoveWorktree ----

    [Test]
    public void A_dirty_worktree_is_refused_and_its_assignment_kept()
    {
        var tree = Path.Combine(wtRoot, "A", "A-T1");
        Assert.That(Dispatcher.EnsureWorktree(repo, tree, "orch/a-t1").Ok, Is.True);
        File.WriteAllText(Path.Combine(tree, "file.txt"), "uncommitted\n");
        var a = Assign("A-T1", "A", tree, true);
        store.SaveAssignments([a]);

        var result = Make().RemoveWorktree(a, repo);

        Assert.That(result.Ok, Is.False);
        Assert.That(result.Message, Does.StartWith("git kept the worktree: "));
        Assert.That(result.Assignment, Is.EqualTo(a));
        Assert.That(File.ReadAllText(Path.Combine(tree, "file.txt")), Is.EqualTo("uncommitted\n"), "nothing is lost");
        Assert.That(store.LoadAssignments().Select(x => x.TicketId), Is.EqualTo(new[] { "A-T1" }));
    }

    [Test]
    public void A_clean_worktree_is_removed_and_its_branch_kept()
    {
        var tree = Path.Combine(wtRoot, "A", "A-T1");
        Assert.That(Dispatcher.EnsureWorktree(repo, tree, "orch/a-t1").Ok, Is.True);
        File.WriteAllText(Path.Combine(tree, "file.txt"), "committed work\n");
        Git(tree, "commit", "-qam", "work");
        var tip = Head(tree);
        var a = Assign("A-T1", "A", tree, true);
        store.SaveAssignments([a, Assign("B-T9", "B", @"C:\elsewhere", false)]);

        var result = Make().RemoveWorktree(a, repo);

        Assert.That(result.Ok, Is.True, result.Message);
        Assert.That(result.Message, Is.EqualTo($"Removed {tree}; branch orch/a-t1 is kept."));
        Assert.That(Directory.Exists(tree), Is.False);
        Assert.That(Head(repo, "refs/heads/orch/a-t1"), Is.EqualTo(tip), "the branch and its commit survive");
        Assert.That(store.LoadAssignments().Select(x => x.TicketId), Is.EqualTo(new[] { "B-T9" }));
    }

    [Test]
    public void An_assignment_without_a_worktree_is_just_released()
    {
        var a = Assign("A-T1", "A", repo, false);
        store.SaveAssignments([a]);

        var result = Make().RemoveWorktree(a, repo);

        Assert.That(result.Ok, Is.True);
        Assert.That(result.Message, Is.EqualTo("A-T1 released."));
        Assert.That(Directory.Exists(repo), Is.True);
        Assert.That(store.LoadAssignments(), Is.Empty);
    }

    private Dispatcher Make(Func<string, string, string, string, bool, (string?, Task<bool>?)>? open = null) =>
        new(new SessionControl(new OrchestratorOptions { LauncherExe = Path.Combine(dir, "no-launcher.exe") }), store)
        {
            OpenTab = open ?? ((_, _, _, _, _) => throw new AssertionException("no tab should open")),
        };

    private QueueItem Item(string id) => new("A", repo, new Ticket(id, "2026-10-01", "chore", "open", "tests", ""), 0, null, false);

    private static Assignment Assign(string id, string repoName, string directory, bool worktree) =>
        new(id, repoName, directory, "Claude", Now, worktree);

    private static string Head(string where, string rev = "HEAD") => Dispatcher.Git(where, "rev-parse", rev).Output.Trim();

    private static void Git(string where, params string[] args)
    {
        var (code, output) = Dispatcher.Git(where, args);
        Assert.That(code, Is.Zero, $"git {string.Join(" ", args)}: {output}");
    }
}
