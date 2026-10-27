using System.Diagnostics;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using MindAttic.Tickets.Log;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// Dispatcher against real temp git repos (M.ORCH-T136): Dispatch opens every ticket in the repo's own checkout on main and
/// never makes a worktree or a branch (M.ORCH-T1122); Git is killed past its timeout and never waits on a credential prompt;
/// Dispatch and Release change assignments through UpdateAssignments; RemoveWorktree keeps an old worktree that's dirty, and
/// its branch. No Windows Terminal tab is opened (seam: OpenTab).
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
    public void Dispatch_opens_in_the_repos_own_checkout_on_main_and_records_the_assignment_through_UpdateAssignments()
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

        var result = dispatcher.Dispatch(Item("A-T1") with { Assignment = stale, StaleSinceUtc = Now }, "Claude", submit: true);

        Assert.That(result.Ok, Is.True, result.Message);
        Assert.That(result.Message, Does.Contain($"in A's checkout ({repo}), on main"));
        Assert.That(result.Message, Does.Contain("submits by itself"));
        Assert.That(result.CameUp!.Result, Is.True);
        Assert.That(opened, Has.Count.EqualTo(1));
        Assert.That(opened[0].Title, Is.EqualTo("A A-T1"), "titled for its ticket, which tells it apart from others in the same checkout");
        Assert.That(opened[0].Directory, Is.EqualTo(repo));
        Assert.That(opened[0].Submit, Is.True);
        Assert.That(opened[0].Prompt, Is.EqualTo(Scheduler.PromptFor(Item("A-T1"))));
        Assert.That(opened[0].Prompt, Does.Contain("own checkout on main, never a separate branch or worktree"));
        Assert.That(Directory.Exists(wtRoot), Is.False, "no worktree is made");
        Assert.That(Dispatcher.Git(repo, "branch", "--list", "orch/*").Output.Trim(), Is.Empty, "nor an orch/ branch");

        var saved = store.LoadAssignments();
        Assert.That(saved.Select(a => a.TicketId), Is.EquivalentTo(new[] { "B-T9", "C-T5", "A-T1" }));
        var mine = saved.Single(a => a.TicketId == "A-T1");
        Assert.That(mine.Directory, Is.EqualTo(repo), "the stale entry is replaced, not duplicated");
        Assert.That((mine.Worktree, mine.IsClaim, mine.InOwnWorktree(repo)), Is.EqualTo((true, true, false)), "dispatched, in the checkout");
        Assert.That(mine.Provider, Is.EqualTo("Claude"));
        Assert.That(result.Assignment, Is.EqualTo(mine));
    }

    [Test]
    public void Dispatch_in_the_repo_itself_opens_there_and_leaves_the_prompt_to_you()
    {
        var dispatcher = Make((_, _, _, _, _) => (null, Task.FromResult(true)));

        var result = dispatcher.Dispatch(Item("A-T1"), "Codex");

        Assert.That(result.Ok, Is.True, result.Message);
        Assert.That(result.Message, Does.Contain("press Enter in the tab"));
        var mine = store.LoadAssignments().Single();
        Assert.That((mine.Directory, mine.Provider), Is.EqualTo((repo, "Codex")));
        Assert.That(Directory.Exists(wtRoot), Is.False, "no worktree");
    }

    [Test]
    public void A_ticket_claimed_elsewhere_since_the_plan_opens_no_tab_and_keeps_that_claim()
    {
        var claim = Assign("A-T1", "A", @"D:\WT\A\A-T1", true); // an /allocate CLI's `next --claim`
        store.SaveAssignments([claim]);
        var opened = 0;
        var dispatcher = Make((_, _, _, _, _) => { opened++; return (null, Task.FromResult(true)); });

        var result = dispatcher.Dispatch(Item("A-T1"), "Claude"); // the plan saw it unassigned

        Assert.That((result.Ok, opened), Is.EqualTo((false, 0)));
        Assert.That(result.Message, Does.Contain("claimed elsewhere"));
        Assert.That(store.LoadAssignments(), Is.EqualTo(new[] { claim }));
    }

    [Test]
    public void A_tab_that_fails_to_open_records_nothing()
    {
        store.SaveAssignments([Assign("B-T9", "B", @"C:\elsewhere", false)]);
        var dispatcher = Make((_, _, _, _, _) => ("Launcher not found", null));

        var result = dispatcher.Dispatch(Item("A-T1"), "Claude");

        Assert.That(result.Ok, Is.False);
        Assert.That(result.Message, Is.EqualTo("Launcher not found"));
        Assert.That(result.Assignment, Is.Null);
        Assert.That(store.LoadAssignments().Select(a => a.TicketId), Is.EqualTo(new[] { "B-T9" }));
    }

    [Test]
    public void Release_drops_only_that_ticket_and_keeps_its_worktree()
    {
        var tree = Path.Combine(wtRoot, "A", "A-T1");
        Assert.That(LegacyWorktree.Add(repo, tree, "orch/a-t1").Ok, Is.True);
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
        Assert.That(LegacyWorktree.Add(repo, tree, "orch/a-t1").Ok, Is.True);
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
        Assert.That(LegacyWorktree.Add(repo, tree, "orch/a-t1").Ok, Is.True);
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
    public void An_assignment_without_a_worktree_of_its_own_is_just_released()
    {
        foreach (var dispatched in new[] { false, true }) // a tab's of old, and a claim in the checkout (M.ORCH-T1122)
        {
            var a = Assign("A-T1", "A", repo, dispatched);
            store.SaveAssignments([a]);

            var result = Make().RemoveWorktree(a, repo);

            Assert.That(result.Ok, Is.True);
            Assert.That(result.Message, Is.EqualTo("A-T1 released."));
            Assert.That(Directory.Exists(repo), Is.True, "the repo's own checkout is never removed");
            Assert.That(store.LoadAssignments(), Is.Empty);
        }
    }

    [Test]
    public void A_Claude_tab_with_no_login_of_its_own_waits_unless_API_billing_is_on()
    {
        // M.ORCH-T747/T749: never the global login (a member's seat); the API key only when the owner has API billing on.
        var empty = new AccountDirs(Path.Combine(dir, "accounts"), Path.Combine(dir, ".claude"), Path.Combine(dir, ".claude.json"));
        Dispatcher Real(bool billing) =>
            new(new SessionControl(new OrchestratorOptions { LauncherExe = Path.Combine(dir, "no-launcher.exe") }), store)
            {
                ApiBilling = () => billing,
                Logins = new LoginPicker(empty, () => []),
            };

        var off = Real(false).Dispatch(Item("A-T1"), "Claude");
        Assert.That(off.Ok, Is.False);
        Assert.That(off.Message, Does.Contain("API billing is off"));
        Assert.That(store.LoadAssignments(), Is.Empty, "it waits: nothing is claimed");

        var on = Real(true).Dispatch(Item("A-T1"), "Claude");
        Assert.That(on.Message, Does.Not.Contain("API billing is off"), "past the gate; the missing launcher is what stops it here");
        Assert.That(on.Message, Does.Contain("Launcher not found"));

        var codex = Real(false).Dispatch(Item("A-T1"), "Codex");
        Assert.That(codex.Message, Does.Not.Contain("API billing is off"), "Codex has no Claude login to wait for");
    }

    [Test]
    public void An_orgs_ticket_starts_only_on_its_own_members_logins()
    {
        // M.ORCH-T753: repo A is Sales's; Amy's login is the default org's.
        var orgs = new OrgRouting(new OrgDirectory([new Organization("sales", "Sales", 1)], [new OrgMember("sales", "Jordan", "jordan@example.com")]),
            new Dictionary<string, string> { ["A"] = "sales" });
        var opened = 0;
        Dispatcher Org(OrgRouting routing) => new(new SessionControl(new OrchestratorOptions { LauncherExe = Path.Combine(dir, "no-launcher.exe") }), store)
        {
            OpenTab = (_, _, _, _, _) => { opened++; return (null, Task.FromResult(true)); },
            Orgs = () => routing,
        };

        var refused = Org(orgs).Dispatch(Item("A-T1"), "Claude", login: new Dispatcher.TabLogin(@"D:\acc\amy", "amy@example.com"));
        Assert.That(refused.Ok, Is.False);
        Assert.That(refused.Message, Does.Contain("Sales's").And.Contain("amy@example.com"));
        Assert.That((opened, store.LoadAssignments().Count), Is.EqualTo((0, 0)), "nothing opened or claimed");

        Assert.That(Org(orgs).Dispatch(Item("A-T1"), "Claude", login: new Dispatcher.TabLogin(@"D:\acc\jordan", "jordan@example.com")).Ok, Is.True);
        store.UpdateAssignments(all => all.Clear());
        Assert.That(Org(OrgRouting.Single).Dispatch(Item("A-T1"), "Claude", login: new Dispatcher.TabLogin(@"D:\acc\amy", "amy@example.com")).Ok,
            Is.True, "one org: any login, as before");
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
