using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// <c>/autopilot</c>, typed in any CLI, works the repos that have an open Claude CLI (or, with
/// <c>-repo=a,b</c>, those repos whether or not one is open): each repo's next open story (queue order:
/// priority, then smallest effort first) goes to a headless background Claude in that repo, so no tab is
/// opened and nothing is typed into a CLI you may be using. One agent per repo at a time, and no more in
/// total than the pace's session cap, so more run in parallel while there's room; a new one only starts
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

        var running = Working.Count;
        var room = plan.Pace?.RoomTokens;
        foreach (var repo in repos)
        {
            if (running >= plan.Cap)
            {
                Tell("cap", $"autopilot: {running} agents working, which is all the pace allows right now ({plan.Cap})");
                break;
            }
            // One story at a time per checkout: never two agents (or an agent and a dispatched tab) in one folder.
            if (plan.Queue.FirstOrDefault(q => q.IsAssigned && Same(q.Repo, repo.Name) && !q.Assignment!.Worktree) is { } open)
            {
                Tell(repo.Name, open.Assignment!.Log is not null
                    ? $"autopilot: {repo.Name} is working on {open.Id}"
                    : $"autopilot: {repo.Name} already has {open.Id} going in a session, so it waits");
                continue;
            }
            var next = plan.Queue.FirstOrDefault(q => Same(q.Repo, repo.Name) && !q.IsAssigned && q.BlockedBy.Count == 0 && !parked.Contains(q.Id) &&
                !(room is { } left && q.Estimate is { } e && e.Fresh > left));
            if (next is null)
            {
                Tell(repo.Name, plan.Queue.Any(q => Same(q.Repo, repo.Name) && !q.IsAssigned)
                    ? $"autopilot: {repo.Name}'s open stories are blocked or too big for what's left before the reset"
                    : $"autopilot: {repo.Name} has no open stories left");
                continue;
            }
            if (dryRun)
            {
                said.Add($"autopilot would start a background agent on {next.Id} in {repo.Name}: {next.Ticket.Summary}");
                running++;
                continue;
            }

            var log = Path.Combine(LogDirectory, $"{next.Id}-{nowUtc.ToLocalTime():yyyyMMdd-HHmm}.log");
            if (Start(repo.Path, Scheduler.PromptFor(next), log) is not { } pid)
            {
                Tell(repo.Name, $"autopilot: couldn't start an agent in {repo.Name}");
                continue;
            }
            running++;
            told.Remove(repo.Name);
            var all = store.LoadAssignments();
            all.RemoveAll(a => a.TicketId == next.Id);
            all.Add(new Assignment(next.Id, next.Repo, next.RepoPath, Providers.Claude, nowUtc, false) { Pilot = pid, Log = log });
            store.SaveAssignments(all);
            said.Add($"autopilot: started {next.Id} in {repo.Name} in the background: {next.Ticket.Summary} (log {log})");
        }
        Working = [.. store.LoadAssignments().Where(a => a.Log is not null && a.Pilot is { } p && IsRunning(p))];
        return said;
    }

    /// <summary>Agents that exited: their ticket closed (done), or it's still open and goes back in the queue.</summary>
    private void Reap(SchedulePlan plan, List<string> said)
    {
        var all = store.LoadAssignments();
        var gone = all.Where(a => a.Log is not null && !(a.Pilot is { } p && IsRunning(p))).ToList();
        if (gone.Count == 0) return;
        foreach (var a in gone)
        {
            all.Remove(a);
            var open = plan.Queue.Any(q => q.Id == a.TicketId);
            if (open) parked.Add(a.TicketId);
            said.Add(open
                ? $"autopilot: needs you: {a.TicketId}'s agent in {a.Repo} ended without closing it, so autopilot leaves it for you (it's back in the queue); see {a.Log}"
                : $"autopilot: finished {a.TicketId} in {a.Repo}");
        }
        store.SaveAssignments(all);
    }

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
