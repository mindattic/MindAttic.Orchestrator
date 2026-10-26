using System.Diagnostics;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using MindAttic.Tickets.Log;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class SchedulerTests
{
    // Fixes need no approval, so they're what these scheduling tests use unless the type itself matters.
    private static Ticket T(string id, string type = "fix", string date = "2026-10-01") =>
        new(id, date, type, "open", $"summary of {id}", "");

    private static RepoStatus Repo(string name, RepoState state, params Ticket[] open) =>
        new(name, $@"D:\W\{name}", state, [], [], open, null, default);

    private static Snapshot Snap(BudgetStatus? budget = null, params RepoStatus[] repos) =>
        new(DateTime.UtcNow, repos, []) { Budget = budget };

    [Test]
    public void Queue_orders_by_priority_then_newest_first()
    {
        var snap = Snap(null,
            Repo("A", RepoState.Dormant, T("A-T1", "docs", "2026-01-01"), T("A-T2", "fix", "2026-09-01")),
            Repo("B", RepoState.Dormant, T("B-T1", "feature", "2026-02-01"), T("B-T2", "fix", "2026-08-01")));
        snap = snap with { Priorities = new Dictionary<string, long> { ["A-T1"] = 5 }, Approved = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "A-T1", "B-T1" } };

        var plan = Scheduler.Plan(snap, new OrchestratorSettings(), []);

        Assert.That(plan.Queue.Select(q => q.Id), Is.EqualTo(new[] { "A-T1", "A-T2", "B-T2", "B-T1" }), "the bumped one, then newest created first (M.ORCH-T863)");
    }

    [Test]
    public void Only_fixes_and_approved_tickets_are_work_and_the_rest_wait_for_your_yes()
    {
        var snap = Snap(null, Repo("A", RepoState.Dormant, T("A-T1", "feature"), T("A-T2", "fix"), T("A-T3", "chore"), T("A-T4", "docs")));

        var waiting = Scheduler.Plan(snap, new OrchestratorSettings(), []);
        Assert.That(waiting.Queue.Select(q => q.Id), Is.EqualTo(new[] { "A-T2" }), "a fix needs no approval");
        Assert.That(waiting.AwaitingApproval.Select(q => q.Id), Is.EqualTo(new[] { "A-T1", "A-T3", "A-T4" }));

        var approved = Scheduler.Plan(snap with { Approved = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "a-t3" } }, new OrchestratorSettings(), []);
        Assert.That(approved.Queue.Select(q => q.Id), Is.EquivalentTo(new[] { "A-T2", "A-T3" }), "approved, in any case");

        var running = Scheduler.Plan(snap, new OrchestratorSettings(),
            [new Assignment("A-T1", "A", @"D:\W\A", "Claude", DateTime.UtcNow, true) { Pilot = 1, Log = "x" }]);
        Assert.That(running.Queue.Select(q => q.Id), Does.Contain("A-T1"), "one already being worked stays tracked");
    }

    /// <summary>M.ORCH-T761: approval and priority come from the ticket database's queue alone; what the orchestrator's settings
    /// kept for tickets outside it (M.ORCH-T441) isn't read.</summary>
    [Test]
    public void Approval_and_priority_come_from_the_ticket_database_alone()
    {
        var snap = Snap(null, Repo("B", RepoState.Dormant, T("B-T1", "feature"), T("B-T2", "docs"), T("B-T3", "chore")))
            with
            {
                Approved = new HashSet<string>(["B-T3"], StringComparer.OrdinalIgnoreCase),
                Priorities = new Dictionary<string, long> { ["B-T3"] = 1 },
            };
        var settings = new OrchestratorSettings
        {
            StoryApproved = ["b-t1", "B-T3"],
            StoryPriorities = new() { ["B-T1"] = 5, ["B-T3"] = 9 },
        };

        var plan = Scheduler.Plan(snap, settings, []);

        Assert.That(plan.Queue.Select(q => (q.Id, q.Priority, q.Approved)), Is.EqualTo(new[] { ("B-T3", 1, true) }));
        Assert.That(plan.AwaitingApproval.Select(q => (q.Id, q.Priority)), Is.EqualTo(new[] { ("B-T1", 0), ("B-T2", 0) }));
    }

    [Test]
    public void Proposes_one_ticket_per_repo_up_to_the_free_slots()
    {
        var snap = Snap(null,
            Repo("A", RepoState.Working),
            Repo("B", RepoState.Dormant, T("B-T1", "fix"), T("B-T2", "fix")),
            Repo("C", RepoState.Dormant, T("C-T1")),
            Repo("D", RepoState.Dormant, T("D-T1")));

        var plan = Scheduler.Plan(snap, new OrchestratorSettings { MaxConcurrentSessions = 3 }, []);

        Assert.That(plan.Occupied, Is.EqualTo(1));
        Assert.That(plan.Proposals.Select(p => p.Item.Id), Is.EqualTo(new[] { "B-T2", "C-T1" }), "B's newest; then C, on the same date and number");
        Assert.That(plan.Proposals.All(p => !p.UseWorktree));
    }

    [Test]
    public void A_busy_repo_gets_a_worktree()
    {
        var plan = Scheduler.Plan(Snap(null, Repo("A", RepoState.Waiting, T("A-T1"))), new OrchestratorSettings(), []);

        Assert.That(plan.Proposals.Single().UseWorktree, Is.True);
    }

    [Test]
    public void Assigned_tickets_leave_the_proposals_and_closed_ones_finish()
    {
        var snap = Snap(null, Repo("A", RepoState.Dormant, T("A-T1"), T("A-T2")));
        var assignments = new[]
        {
            new Assignment("A-T1", "A", @"D:\W\A", "Claude", DateTime.UtcNow, false),
            new Assignment("A-T9", "A", @"D:\W\A", "Claude", DateTime.UtcNow, false),
        };

        var plan = Scheduler.Plan(snap, new OrchestratorSettings(), assignments);

        Assert.That(plan.Proposals.Single().Item.Id, Is.EqualTo("A-T2"));
        Assert.That(plan.Queue.Last().Id, Is.EqualTo("A-T1"), "assigned work sinks to the bottom");
        Assert.That(plan.Finished.Single().TicketId, Is.EqualTo("A-T9"));
    }

    [Test]
    public void Budget_pressure_holds_everything()
    {
        var hot = BudgetCalculator.Compute([new UsageEvent(DateTime.UtcNow.AddMinutes(-30), new TokenUsage(0, 0, 0, 900))],
            new OrchestratorSettings { BlockBudget = 1000 }, DateTime.UtcNow, DateTime.UtcNow.Date);

        var plan = Scheduler.Plan(Snap(hot, Repo("A", RepoState.Dormant, T("A-T1"))), new OrchestratorSettings(), []);

        Assert.That(plan.Proposals, Is.Empty);
        Assert.That(plan.Hold, Does.Contain("budget"));
    }

    [Test]
    public void Prompt_says_how_to_close_the_ticket()
    {
        var item = new QueueItem("Repo", @"D:\W\Repo", T("RP-T3", "fix"), 0, null, false);
        Assert.That(Scheduler.PromptFor(item), Does.Contain("tickets ticket close RP-T3 --repo Repo"));
    }

    [Test]
    public void Worktrees_are_created_on_their_own_branch_and_reused()
    {
        var root = Path.Combine(Path.GetTempPath(), "orch-wt-" + Guid.NewGuid().ToString("N"));
        var repo = Path.Combine(root, "repo");
        Directory.CreateDirectory(repo);
        try
        {
            Run(repo, "init", "-q");
            File.WriteAllText(Path.Combine(repo, "a.txt"), "a");
            Run(repo, "add", ".");
            Run(repo, "-c", "user.email=t@t", "-c", "user.name=t", "commit", "-qm", "init");

            var wt = Path.Combine(root, "wt", "repo", "X-T1");
            var first = Dispatcher.EnsureWorktree(repo, wt, "orch/x-t1");
            var again = Dispatcher.EnsureWorktree(repo, wt, "orch/x-t1");

            Assert.That(first.Ok, Is.True, first.Error);
            Assert.That(again.Ok, Is.True);
            Assert.That(File.Exists(Path.Combine(wt, "a.txt")));
            Assert.That(Run(wt, "branch", "--show-current").Trim(), Is.EqualTo("orch/x-t1"));
        }
        finally
        {
            foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void After_priority_the_newest_goes_first_whatever_its_estimate_and_every_story_ends_committed_and_closed()
    {
        // Fixes in A have cost ~100 each, in B ~1,000; B's older fix waits behind A's newer one (M.ORCH-T863: newest first, not cheapest).
        var done = new List<MindAttic.Orchestrator.Budget.SessionCost>();
        for (var i = 0; i < 3; i++)
        {
            done.Add(new("a" + i, "A-T9" + i, "fix", "A", 100, DateTime.UtcNow));
            done.Add(new("b" + i, "B-T9" + i, "fix", "B", 1000, DateTime.UtcNow));
        }
        var older = new Ticket("B-T1", "2026-01-01", "fix", "open", "old but big", "");
        var newer = new Ticket("A-T1", "2026-10-01", "fix", "open", "new but small", "");
        var snap = Snap(null, Repo("A", RepoState.Dormant, newer), Repo("B", RepoState.Dormant, older)) with { Costs = new(done) };

        var plan = Scheduler.Plan(snap, new OrchestratorSettings(), []);
        Assert.That(plan.Queue.Select(q => q.Id), Is.EqualTo(new[] { "A-T1", "B-T1" }));
        Assert.That(Scheduler.Plan(snap with { Priorities = new Dictionary<string, long> { ["B-T1"] = 1 } }, new OrchestratorSettings(), []).Queue[0].Id, Is.EqualTo("B-T1"),
            "priority still comes first");
        Assert.That(Scheduler.PromptFor(plan.Queue[0]), Does.Contain("/commit").And.Contains("tickets ticket close A-T1 --repo A"));
    }

    [Test]
    public void Auto_submit_presses_enter_only_in_the_tab_it_just_opened()
    {
        HostSession H(int pid, string? dir) => new("Claude", pid, "mindattic-host-claude-" + pid, dir);
        var hosts = new[] { H(1, @"D:\W\A"), H(2, @"D:\W\A\sub"), H(3, null), H(4, @"d:\w\a\"), H(5, @"D:\W\A") };
        var before = new HashSet<int> { 1 };

        Assert.That(MindAttic.Orchestrator.Sessions.SessionControl.NewHostAt(hosts, @"D:\W\A", before)?.ProcessId, Is.EqualTo(4),
            "skips the tab already open there, other folders and unreadable command lines");
        Assert.That(MindAttic.Orchestrator.Sessions.SessionControl.NewHostAt(hosts[..3], @"D:\W\A", before), Is.Null,
            "no new tab there yet: nothing to submit");
    }

    [Test]
    public void A_tab_opened_minutes_ago_holds_its_slot_before_it_shows_as_working()
    {
        var snap = Snap(null, Repo("A", RepoState.Dormant, T("A-T1")), Repo("B", RepoState.Dormant, T("B-T1")));
        var fresh = new Assignment("A-T1", "A", @"D:\W\A", "Claude", DateTime.UtcNow.AddMinutes(-2), false);
        var plan = Scheduler.Plan(snap, new OrchestratorSettings { MaxConcurrentSessions = 1 }, [fresh]);
        Assert.That((plan.Occupied, plan.Proposals.Count), Is.EqualTo((1, 0)), "the one slot is A's new tab, so B waits");
    }

    [Test]
    public void A_quote_or_line_break_in_a_prompt_never_reaches_windows_terminal()
    {
        var control = new MindAttic.Orchestrator.Sessions.SessionControl(new OrchestratorOptions());
        var args = control.StartArguments("A", @"D:\W\A", "Claude", "close it with --ref \"<the test>\"\nnext line").ToList();
        Assert.That(args[^1], Is.EqualTo("close it with --ref '<the test>' next line"));
        Assert.That(args.Any(a => a.Contains('"')), Is.False);
    }

    private const string Worktree = @"D:\Projects\MindAttic.worktrees\A\A-T1";

    private static Snapshot Activity(DateTime now, DateTime? transcriptAt = null, bool tabOpen = false) =>
        new(now, [new RepoStatus("A", @"D:\W\A", RepoState.Dormant,
            tabOpen ? [new HostSession("Claude", 1, "pipe", Worktree + @"\src")] : [],
            transcriptAt is { } at ? [new TranscriptSession("s", Worktree, at, default, null)] : [],
            [T("A-T1", date: "2026-10-02"), T("A-T2")], transcriptAt, default)], []); // A-T1 the newest (M.ORCH-T863)

    private static readonly Assignment Quiet =
        new("A-T1", "A", Worktree, "Claude", DateTime.UtcNow.AddHours(-20), Worktree: true);

    [Test]
    public void A_ticket_whose_session_went_quiet_goes_back_in_the_queue_and_back_to_its_worktree()
    {
        var now = DateTime.UtcNow;
        var plan = Scheduler.Plan(Activity(now, transcriptAt: now.AddHours(-13)), new OrchestratorSettings(), [Quiet]);

        var item = plan.Stale.Single();
        Assert.That(item.Id, Is.EqualTo("A-T1"));
        Assert.That(item.IsAssigned, Is.False);
        Assert.That(item.StaleSinceUtc, Is.EqualTo(now.AddHours(-13)), "the last sign of life, not the dispatch");
        var proposal = plan.Proposals.Single();
        Assert.That(proposal.Item.Id, Is.EqualTo("A-T1"), "it keeps its place in line rather than sinking");
        Assert.That(proposal.UseWorktree, Is.True, "its partial work is in the worktree");
        Assert.That(proposal.Reason, Does.Contain("went quiet"));
        Assert.That((item.RepoBusy, item.NeedsWorktree), Is.EqualTo((false, true)),
            "started any other way (orchestrator dispatch, a queue row that isn't the proposal) it goes back there too");
        Assert.That(plan.Queue.Single(q => q.Id == "A-T2").NeedsWorktree, Is.False, "a fresh ticket in a free repo starts in the repo");
    }

    [Test]
    public void An_unapproved_ticket_whose_session_went_quiet_isnt_proposed_or_handed_out_again()
    {
        // M.ORCH-T383: dispatched, then approve --undo (or never approved); once stale it waits for your yes.
        var now = DateTime.UtcNow;
        var snap = new Snapshot(now, [new RepoStatus("A", @"D:\W\A", RepoState.Dormant, [],
            [new TranscriptSession("s", Worktree, now.AddHours(-13), default, null)],
            [T("A-T1", "feature")], now.AddHours(-13), default)], []);
        var plan = Scheduler.Plan(snap, new OrchestratorSettings(), [Quiet]);

        Assert.That(plan.Stale.Single().Id, Is.EqualTo("A-T1"), "still said to be stale");
        Assert.That(plan.AwaitingApproval.Single().Id, Is.EqualTo("A-T1"), "and listed as waiting for your yes (M.ORCH-T394)");
        Assert.That(plan.Proposals, Is.Empty);
        var store = new MindAttic.Orchestrator.Settings.JsonStore(Path.Combine(Path.GetTempPath(), "orch-sched-" + Guid.NewGuid().ToString("N")));
        var (handed, _) = Allocate.Next(plan, new OrchestratorSettings(), store, ["A"], 5, claim: false, now, pilotRunning: _ => false);
        Assert.That(handed, Is.Empty);

        // Blocked as well, it's counted once, as waiting for your yes (M.ORCH-T401).
        var blocked = snap with
        {
            Repos = [snap.Repos[0] with { OpenTickets = [T("A-T1", "feature"), T("A-T2", "feature")] }],
            Requires = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase) { ["A-T1"] = ["A-T2"] },
        };
        var (none, why) = Allocate.Next(Scheduler.Plan(blocked, new OrchestratorSettings(), [Quiet]),
            new OrchestratorSettings(), store, ["A"], 5, claim: false, now, pilotRunning: _ => false);
        Assert.That((none, why), Is.EqualTo((Array.Empty<QueueItem>(), "none left that may be worked: 2 wait for your approval (tickets ticket approve)")));

        // While its session still works it, it's being worked, not waiting.
        var working = new Snapshot(now, [new RepoStatus("A", @"D:\W\A", RepoState.Dormant, [],
            [new TranscriptSession("s", Worktree, now.AddHours(-1), default, null)],
            [T("A-T1", "feature")], now.AddHours(-1), default)], []);
        Assert.That(Scheduler.Plan(working, new OrchestratorSettings(), [Quiet]).AwaitingApproval, Is.Empty);
    }

    [Test]
    public void Recent_work_or_an_open_tab_keeps_the_assignment()
    {
        var now = DateTime.UtcNow;

        var working = Scheduler.Plan(Activity(now, transcriptAt: now.AddHours(-2)), new OrchestratorSettings(), [Quiet]);
        var tabOpen = Scheduler.Plan(Activity(now, transcriptAt: now.AddHours(-30), tabOpen: true), new OrchestratorSettings(), [Quiet]);
        var off = Scheduler.Plan(Activity(now), new OrchestratorSettings { StaleAfterHours = 0 }, [Quiet]);

        Assert.That(working.Stale, Is.Empty);
        Assert.That(tabOpen.Stale, Is.Empty, "a tab left open is you coming back to it");
        Assert.That(off.Stale, Is.Empty);
        Assert.That(working.Proposals.Single().Item.Id, Is.EqualTo("A-T2"));

        // A huge setting threw out of every plan (M.ORCH-T429).
        var huge = new OrchestratorSettings { StaleAfterHours = int.MaxValue, GroomAfterDays = int.MaxValue };
        Assert.That(Scheduler.Plan(Activity(now, transcriptAt: now.AddHours(-30)), huge, [Quiet]).Stale, Is.Empty);
    }

    [Test]
    public void A_worktree_git_already_removed_counts_as_removed_and_releases_its_ticket()
    {
        // Found live: with the agent's tab still open in the worktree, git unregisters it and deletes
        // its files but Windows keeps the folder, so `git worktree remove` fails on an empty husk.
        var root = Path.Combine(Path.GetTempPath(), "orch-wtgone-" + Guid.NewGuid().ToString("N"));
        var repo = Path.Combine(root, "repo");
        Directory.CreateDirectory(repo);
        try
        {
            Run(repo, "init", "-q");
            File.WriteAllText(Path.Combine(repo, "a.txt"), "a");
            Run(repo, "add", ".");
            Run(repo, "-c", "user.email=t@t", "-c", "user.name=t", "commit", "-qm", "init");
            var wt = Path.Combine(root, "wt", "X-T1");
            Assert.That(Dispatcher.EnsureWorktree(repo, wt, "orch/x-t1").Ok);
            Run(repo, "worktree", "remove", wt);
            Directory.CreateDirectory(wt); // the folder Windows wouldn't let go of

            var store = new JsonStore(Path.Combine(root, "store"));
            var a = new Assignment("X-T1", "repo", wt, "Claude", DateTime.UtcNow, Worktree: true);
            store.SaveAssignments([a]);
            var r = new Dispatcher(new MindAttic.Orchestrator.Sessions.SessionControl(new OrchestratorOptions()), store).RemoveWorktree(a, repo);

            Assert.That(r.Ok, Is.True, r.Message);
            Assert.That(store.LoadAssignments(), Is.Empty, "released, so the queue stops offering it");
            Assert.That(Directory.Exists(wt), Is.False, "the empty folder goes too when nothing holds it");
        }
        finally
        {
            foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(root, recursive: true);
        }
    }

    private static string Run(string dir, params string[] args)
    {
        var psi = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        psi.ArgumentList.Add("-C"); psi.ArgumentList.Add(dir);
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        return output;
    }
}
