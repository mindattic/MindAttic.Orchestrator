using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// <c>/autopilot</c>, typed in any CLI, works the repos that have an open Claude CLI (or, with
/// <c>-repo=a,b</c>, those repos whether or not one is open): each repo's next open story (queue order:
/// priority, then smallest effort first) goes to a headless background Claude in its own git worktree of that
/// repo, so no tab is opened, nothing is typed into a CLI you may be using, and no checkout is edited under
/// anyone. Agents run in parallel up to the repo's session limit and, in total, the pace's session cap, so as
/// much runs at once as the window allows; a new one only starts
/// while the 5-hour pace can afford it, and the pace gate holds the running ones, so the window is spent
/// to its target without running into the limit. An agent that exits without closing its ticket gives the
/// ticket back and says so. <c>/autopilot off</c> starts nothing new. With a <see cref="DispatchLease"/>, only
/// the process holding it starts agents.
/// </summary>
public sealed class Autopilot(JsonStore store, ContextCycler sessions, DispatchLease? lease = null)
{
    internal Func<string, string, string, int?> Start { get; init; } = BackgroundAgent.Start;
    internal Func<int, bool> IsRunning { get; init; } = BackgroundAgent.IsRunning;
    internal Func<IReadOnlyList<string>?> Order { get; init; } = sessions.AutopilotOrder;
    internal Func<int, bool> Alive { get; init; } = ContextCycler.IsClaude;
    internal string LogDirectory { get; init; } = BackgroundAgent.LogDirectory;
    internal Func<string, string, string, (bool Ok, string Path, string Error)> MakeWorktree { get; init; } = Dispatcher.EnsureWorktree;

    private readonly Dictionary<string, string> told = [];

    /// <summary>Stories whose agent ended without closing them: not retried (it would likely fail the same way) until
    /// you look, or the orchestrator restarts.</summary>
    private readonly HashSet<string> parked = new(StringComparer.Ordinal);

    /// <summary>Another orchestrator process (usually the logon daemon) is starting the agents.</summary>
    public bool HeldElsewhere { get; private set; }

    /// <summary>Why no new agent starts right now (switched off, quiet hours, the pace), if that's so.</summary>
    public string? Paused { get; private set; }

    /// <summary>Whether <c>/autopilot</c> is on (until <c>/autopilot off</c>).</summary>
    public bool On { get; private set; }

    /// <summary>The repos it's working.</summary>
    public IReadOnlyList<string> Repos { get; private set; } = [];

    /// <summary>The background agents working now.</summary>
    public IReadOnlyList<Assignment> Working { get; private set; } = [];

    /// <summary>Whether <paramref name="pid"/> (a Claude CLI) runs inside one of the background agents.</summary>
    public bool IsAgent(int pid) => Working.Any(a => a.Pilot is { } p && (p == pid || ProcessTree.IsAncestor(p, pid)));

    /// <summary>Reaps finished agents and starts new ones; returns what happened, and once each, why a repo waits.
    /// <paramref name="dryRun"/> only says what it would start.</summary>
    public IReadOnlyList<string> Tick(SchedulePlan plan, Snapshot snapshot, OrchestratorSettings settings, DateTime nowUtc, bool dryRun = false)
    {
        var said = new List<string>();
        void Tell(string key, string why) { if (told.GetValueOrDefault(key) != why) said.Add(why); told[key] = why; }

        var order = Order();
        On = order is not null;
        HeldElsewhere = lease is not null && (On || store.LoadAssignments().Any(a => a.Log is not null)) && !lease.TryAcquire();
        if (!dryRun && !HeldElsewhere) Reap(plan, said);
        Working = [.. store.LoadAssignments().Where(a => a.Log is not null && a.Pilot is { } p && IsRunning(p))];

        // Repos with an open Claude CLI (agents' own sessions aside), or the ones named.
        var repos = order is null ? []
            : order.Count > 0 ? snapshot.Repos.Where(r => r.Path.Length > 0 && order.Contains(r.Name, StringComparer.OrdinalIgnoreCase)).ToList()
            : sessions.Load().Where(s => s.Pid is { } p && Alive(p) && !IsAgent(p))
                .Select(s => RepoFor(s.Cwd, snapshot)).OfType<RepoStatus>()
                .DistinctBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
        Repos = [.. repos.Select(r => r.Name).Order(StringComparer.OrdinalIgnoreCase)];
        if (order is { Count: > 0 } && order.Where(n => !snapshot.Repos.Any(r => Same(r.Name, n))).ToList() is { Count: > 0 } unknown)
            Tell("unknown", $"autopilot: no repo named {string.Join(", ", unknown)} in the workspace");

        Paused = !settings.Autopilot ? "autopilot is switched off"
            : QuietHours.Active(settings.QuietHours, nowUtc.ToLocalTime()) is { } quiet ? $"quiet hours ({quiet})"
            : PaceHold(plan, snapshot);
        if (repos.Count == 0 || Paused is not null || HeldElsewhere) return said;

        // Every agent gets its own worktree, so a repo runs as many stories at once as its session limit and the
        // pace allow, and nobody's checkout (yours included) is ever edited under them.
        var running = Working.Count;
        var room = plan.Pace?.RoomTokens;
        var taken = new HashSet<string>(StringComparer.Ordinal);
        // How many more the pace pays for, worked out fresh every tick. An agent started in the last control
        // window isn't in the measured draw yet, so it's counted as drawing already; unpaced, the session setting decides.
        var fresh = Working.Count(a => nowUtc - a.StartedUtc < BudgetCalculator.ControlWindow);
        var more = plan.Pace is { } pace ? pace.SpareSessions - fresh : settings.MaxConcurrentSessions - running;
        foreach (var repo in repos)
        {
            // Worktrees keep agents apart, so a repo is only capped if you've capped it yourself.
            var limit = settings.RepoMaxSessions.GetValueOrDefault(repo.Name, int.MaxValue);
            var inRepo = Working.Count(a => Same(a.Repo, repo.Name));
            while (true)
            {
                if (more <= 0)
                {
                    Tell("cap", $"autopilot: {running} agent{(running == 1 ? "" : "s")} working; the pace has no room for another right now");
                    return Done();
                }
                if (inRepo >= limit)
                {
                    Tell(repo.Name, $"autopilot: {repo.Name} has {inRepo} agents working, the cap you set for it (settings --repo-sessions)");
                    break;
                }
                var next = plan.Queue.FirstOrDefault(q => Same(q.Repo, repo.Name) && !q.IsAssigned && q.BlockedBy.Count == 0 &&
                    !parked.Contains(q.Id) && !taken.Contains(q.Id) && !(room is { } left && q.Estimate is { } e && e.Fresh > left));
                if (next is null)
                {
                    if (inRepo == 0)
                        Tell(repo.Name, plan.Queue.Any(q => Same(q.Repo, repo.Name) && !q.IsAssigned)
                            ? $"autopilot: {repo.Name}'s open stories are blocked, parked or too big for what's left before the reset"
                            : $"autopilot: {repo.Name} has no open stories left");
                    break;
                }
                taken.Add(next.Id);
                if (dryRun)
                {
                    said.Add($"autopilot would start a background agent on {next.Id} in {repo.Name}: {next.Ticket.Summary}");
                    running++;
                more--;
                    inRepo++;
                    continue;
                }

                var branch = Dispatcher.BranchFor(next.Id);
                var (ok, tree, error) = MakeWorktree(repo.Path, Path.Combine(settings.WorktreeRoot, repo.Name, next.Id), branch);
                if (!ok)
                {
                    Tell(repo.Name, $"autopilot: couldn't make a worktree for {next.Id}: {error}");
                    break;
                }
                var log = Path.Combine(LogDirectory, $"{next.Id}-{nowUtc.ToLocalTime():yyyyMMdd-HHmm}.log");
                if (Start(tree, PromptFor(next, tree, branch), log) is not { } pid)
                {
                    Tell(repo.Name, $"autopilot: couldn't start an agent in {repo.Name}");
                    break;
                }
                running++;
                more--;
                inRepo++;
                told.Remove(repo.Name);
                var all = store.LoadAssignments();
                all.RemoveAll(a => a.TicketId == next.Id);
                all.Add(new Assignment(next.Id, next.Repo, tree, Providers.Claude, nowUtc, true) { Pilot = pid, Log = log });
                store.SaveAssignments(all);
                said.Add($"autopilot: started {next.Id} in {repo.Name} in the background: {next.Ticket.Summary} (log {log})");
            }
        }
        return Done();

        IReadOnlyList<string> Done()
        {
            Working = [.. store.LoadAssignments().Where(a => a.Log is not null && a.Pilot is { } p && IsRunning(p))];
            return said;
        }
    }

    /// <summary>
    /// The story's prompt for an agent in its own worktree. It starts like <see cref="Scheduler.PromptFor"/> (so its
    /// session is tied to the ticket), lands the branch on main itself, then closes the ticket in the Workflow
    /// database (the same database from any folder, worktrees included).
    /// </summary>
    internal static string PromptFor(QueueItem item, string worktree, string branch) =>
        $"Work on ticket {item.Id} ({item.Ticket.Type}): {item.Ticket.Summary} " +
        $"You are in your own git worktree of {item.Repo} ({worktree}, branch {branch}) so other agents can work {item.Repo} at the same time; work, build and test only here. " +
        "When it's done: run /commit, but don't push (the next step does). Land it: git fetch origin, git rebase origin/main (resolve any conflicts and rerun the tests), git push origin HEAD:main. " +
        $"Then mark it finished: workflow ticket close {item.Id} --ref <the test or commit that proves it>. " +
        "If you can't finish or land it, leave the ticket open and say why.";

    /// <summary>
    /// Agents that exited. A worktree agent is done when its ticket is closed (in the Workflow database, or on origin/main
    /// before the migration) and its branch is on origin/main: then your checkout is fast-forwarded (git refuses if that
    /// would touch your own changes) and the worktree removed (git keeps it if anything there is uncommitted). Otherwise
    /// the ticket goes back in the queue, parked for you.
    /// </summary>
    private void Reap(SchedulePlan plan, List<string> said)
    {
        var all = store.LoadAssignments();
        var gone = all.Where(a => a.Log is not null && !(a.Pilot is { } p && IsRunning(p))).ToList();
        if (gone.Count == 0) return;
        foreach (var a in gone)
        {
            all.Remove(a);
            var repoPath = plan.Queue.FirstOrDefault(q => Same(q.Repo, a.Repo))?.RepoPath ?? a.Directory;
            var (closed, note) = a.Worktree ? Land(a, repoPath) : (!plan.Queue.Any(q => q.Id == a.TicketId), "");
            if (!closed) parked.Add(a.TicketId);
            said.Add(closed
                ? $"autopilot: finished {a.TicketId} in {a.Repo}{note}"
                : $"autopilot: needs you: {a.TicketId}'s agent in {a.Repo} ended without closing it, so autopilot leaves it for you (it's back in the queue); see {a.Log}{note}");
        }
        store.SaveAssignments(all);
    }

    /// <summary>Whether a worktree agent's ticket landed closed on origin/main, after tidying up; and what to add about it.</summary>
    internal Func<Assignment, string, (bool Closed, string Note)> Land { get; init; } = LandOnMain;

    internal static (bool Closed, string Note) LandOnMain(Assignment a, string repoPath)
    {
        Dispatcher.Git(repoPath, "fetch", "-q", "origin");
        bool closed;
        using (var db = MindAttic.Workflow.Data.WorkflowDb.OpenIfMigrated(Path.GetDirectoryName(repoPath)))
        {
            if (db is not null)
                closed = db.Ticket(a.TicketId)?.Ticket.Status is "closed" or "wont-do";
            else
            {
                var (code, stories) = Dispatcher.Git(repoPath, "show", "origin/main:docs/USER_STORIES.md");
                closed = code == 0 && ClosedIn(stories, a.TicketId);
            }
        }
        var note = "";
        // Closed but never landed: its commits are only on its branch, so it isn't done.
        if (closed && Dispatcher.Git(repoPath, "merge-base", "--is-ancestor", Dispatcher.BranchFor(a.TicketId), "origin/main").Code != 0)
            return (false, $"; it closed its ticket but its branch {Dispatcher.BranchFor(a.TicketId)} isn't on main; the work is in {a.Directory}");
        if (closed && Dispatcher.Git(repoPath, "rev-parse", "--abbrev-ref", "HEAD").Output.Trim() == "main")
            note += Dispatcher.Git(repoPath, "merge", "--ff-only", "-q", "origin/main").Code == 0
                ? "; your checkout is up to date with it" : "; pull main to get it (your checkout has changes in the way)";
        if (closed)
            note += Dispatcher.Git(repoPath, "worktree", "remove", a.Directory).Code == 0
                ? "" : $"; its worktree {a.Directory} kept (something there is uncommitted)";
        else note += $"; its work is in {a.Directory} (branch {Dispatcher.BranchFor(a.TicketId)})";
        return (closed, note);
    }

    /// <summary>Whether the ticket table in <paramref name="stories"/> has <paramref name="id"/> closed (or wont-do).</summary>
    internal static bool ClosedIn(string stories, string id) =>
        stories.Split('\n').Any(l => l.TrimStart().StartsWith($"| {id} |", StringComparison.Ordinal) &&
            l.Split('|').Select(c => c.Trim()).ElementAtOrDefault(4) is "closed" or "wont-do");

    /// <summary>Why the pace can't afford another story right now; null when it can.</summary>
    internal static string? PaceHold(SchedulePlan plan, Snapshot snapshot) =>
        plan.Pace is { Mode: PaceMode.Over or PaceMode.Coast } p ? $"pace: {p.Note}"
        : snapshot.Budget is { Level: BudgetLevel.Over } b ? $"budget at {b.WorstPercent:0}%"
        : null;

    /// <summary>The repo a CLI runs in: its root or a folder under it (never a worktree, which lives elsewhere).</summary>
    internal static RepoStatus? RepoFor(string? cwd, Snapshot snapshot)
    {
        if (string.IsNullOrEmpty(cwd)) return null;
        var dir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(cwd));
        return snapshot.Repos.Where(r => r.Path.Length > 0).FirstOrDefault(r =>
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(r.Path));
            return dir.Equals(root, StringComparison.OrdinalIgnoreCase) || dir.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase);
        });
    }

    private static bool Same(string a, string b) => a.Equals(b, StringComparison.OrdinalIgnoreCase);
}
