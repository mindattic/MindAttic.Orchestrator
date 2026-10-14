using System.Diagnostics;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using MindAttic.Workflow.Tickets;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class SchedulerTests
{
    private static Ticket T(string id, string type = "feature", string date = "2026-10-01") =>
        new(id, date, type, "open", $"summary of {id}", "");

    private static RepoStatus Repo(string name, RepoState state, params Ticket[] open) =>
        new(name, $@"D:\W\{name}", state, [], [], open, null, default);

    private static Snapshot Snap(BudgetStatus? budget = null, params RepoStatus[] repos) =>
        new(DateTime.UtcNow, repos, []) { Budget = budget };

    [Test]
    public void Queue_orders_by_priority_then_type_then_age()
    {
        var snap = Snap(null,
            Repo("A", RepoState.Dormant, T("A-T1", "docs", "2026-01-01"), T("A-T2", "fix", "2026-09-01")),
            Repo("B", RepoState.Dormant, T("B-T1", "feature", "2026-02-01"), T("B-T2", "fix", "2026-08-01")));
        var settings = new OrchestratorSettings { Priorities = new() { ["A-T1"] = 5 } };

        var plan = Scheduler.Plan(snap, settings, []);

        Assert.That(plan.Queue.Select(q => q.Id), Is.EqualTo(new[] { "A-T1", "B-T2", "A-T2", "B-T1" }));
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
        Assert.That(plan.Proposals.Select(p => p.Item.Id), Is.EqualTo(new[] { "B-T1", "C-T1" }));
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
        Assert.That(Scheduler.PromptFor(item), Does.Contain("workflow ticket close RP-T3 --repo Repo"));
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
    public void Auto_dispatch_is_off_unless_asked_and_paced()
    {
        var store = new JsonStore(Path.Combine(Path.GetTempPath(), "orch-store-" + Guid.NewGuid().ToString("N")));
        var auto = new AutoDispatch(new Dispatcher(new MindAttic.Orchestrator.Sessions.SessionControl(
            new OrchestratorOptions { LauncherExe = @"Z:\nope.exe" }), store));
        var plan = Scheduler.Plan(Snap(null, Repo("A", RepoState.Dormant, T("A-T1"))), new OrchestratorSettings(), []);
        var now = DateTime.UtcNow;

        Assert.That(auto.Tick(plan, new OrchestratorSettings(), now), Is.Null, "off by default");
        var on = new OrchestratorSettings { AutoDispatch = true };
        Assert.That(auto.Tick(plan, on, now)?.Ok, Is.False, "missing Launcher reports, doesn't throw");
        Assert.That(auto.Tick(plan, on, now.AddSeconds(10)), Is.Null, "paced");
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
