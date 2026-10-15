using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// <c>/autopilot</c>, typed in any CLI, puts every open Claude CLI that sits in a repo to work (or, with
/// <c>-repo=a,b</c>, only the CLIs in those repos): each is fed its own repo's next open story (queue order:
/// priority, then smallest effort first) whenever it has been idle for <see cref="Settle"/> and its last story
/// is closed. A CLI never gets another repo's story, and no tabs are opened, so nothing takes the screen. A new
/// story only starts while the 5-hour pace can afford it, and the pace gate holds the running ones, so the
/// window is spent to its target without running into the limit. <c>/autopilot off</c> stops it. With a
/// <see cref="DispatchLease"/>, only the process holding it feeds.
/// </summary>
public sealed class Autopilot(JsonStore store, ContextCycler sessions, DispatchLease? lease = null)
{
    /// <summary>How long a CLI has to have been idle before it's fed, so a reply you're typing isn't typed over.</summary>
    public static readonly TimeSpan Settle = TimeSpan.FromSeconds(30);

    internal Func<HostSession, string, bool> SendToHost { get; init; } =
        (host, text) => SessionControl.SubmitAsync(host.PipeName, text).GetAwaiter().GetResult();
    internal Func<int, string, bool> Typer { get; init; } = ConsoleTyper.Submit;
    internal Func<int, int, bool> IsAncestor { get; init; } = ProcessTree.IsAncestor;
    internal Func<IReadOnlyList<string>?> Order { get; init; } = sessions.AutopilotOrder;
    internal Func<int, bool> Alive { get; init; } = ContextCycler.IsClaude;

    private readonly Dictionary<int, DateTime> fedUtc = [];
    private readonly Dictionary<string, string> told = [];

    /// <summary>Another orchestrator process (usually the logon daemon) is feeding the CLIs.</summary>
    public bool HeldElsewhere { get; private set; }

    /// <summary>Why no CLI is being fed right now (switched off, quiet hours, the pace), if that's so.</summary>
    public string? Paused { get; private set; }

    /// <summary>Whether <c>/autopilot</c> is on (until <c>/autopilot off</c>).</summary>
    public bool On { get; private set; }

    /// <summary>The open CLIs it's working through, by repo.</summary>
    public IReadOnlyList<string> Repos { get; private set; } = [];

    /// <summary>Feeds every CLI that's ready; returns what it did, and once each, why a CLI is left alone.
    /// <paramref name="dryRun"/> only says what it would type, and into which CLI, ignoring the idle wait.</summary>
    public IReadOnlyList<string> Tick(SchedulePlan plan, Snapshot snapshot, OrchestratorSettings settings, DateTime nowUtc, bool dryRun = false)
    {
        var said = new List<string>();
        void Tell(string key, string why) { if (told.GetValueOrDefault(key) != why) said.Add(why); told[key] = why; }

        var order = Order();
        On = order is not null;
        // A CLI's newest session is the live one (/clear starts another under the same process).
        var clis = order is null ? [] : sessions.Load()
            .Where(s => s.Pid is { } p && Alive(p))
            .GroupBy(s => s.Pid!.Value).Select(g => g.MaxBy(s => s.TurnUtc)!)
            .Select(s => (Session: s, Repo: RepoFor(s.Cwd, snapshot)))
            .Where(c => c.Repo is not null && (order.Count == 0 || order.Contains(c.Repo.Name, StringComparer.OrdinalIgnoreCase)))
            .Select(c => (c.Session, Repo: c.Repo!))
            .ToList();
        Repos = [.. clis.Select(c => c.Repo.Name).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)];
        if (order is { Count: > 0 } && order.Where(n => !snapshot.Repos.Any(r => Same(r.Name, n))).ToList() is { Count: > 0 } unknown)
            Tell("unknown", $"autopilot: no repo named {string.Join(", ", unknown)} in the workspace");

        Paused = !settings.Autopilot ? "autopilot is switched off"
            : QuietHours.Active(settings.QuietHours, nowUtc.ToLocalTime()) is { } quiet ? $"quiet hours ({quiet})"
            : PaceHold(plan, snapshot);
        HeldElsewhere = clis.Count > 0 && lease is not null && !lease.TryAcquire();
        if (clis.Count == 0 || Paused is not null || HeldElsewhere) return said;

        var room = plan.Pace?.RoomTokens;
        foreach (var (s, repo) in clis)
        {
            var pid = s.Pid!.Value;
            var key = pid.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!dryRun && (!Ready(s, settings, nowUtc) || fedUtc.GetValueOrDefault(pid) >= s.TurnUtc)) continue;

            // One story at a time per checkout: the last one has to be closed (by the agent, or by you) first.
            if (plan.Queue.FirstOrDefault(q => q.IsAssigned && Same(q.Repo, repo.Name) && !q.Assignment!.Worktree) is { } open)
            {
                Tell(key, open.Assignment!.Pilot == pid
                    ? $"autopilot: the {repo.Name} CLI is still on {open.Id}; it's waiting for you"
                    : $"autopilot: {repo.Name} already has {open.Id} going in another session, so its CLI waits");
                continue;
            }
            var next = plan.Queue.FirstOrDefault(q => Same(q.Repo, repo.Name) && !q.IsAssigned && q.BlockedBy.Count == 0 &&
                !(room is { } left && q.Estimate is { } e && e.Fresh > left));
            if (next is null)
            {
                Tell(key, plan.Queue.Any(q => Same(q.Repo, repo.Name) && !q.IsAssigned)
                    ? $"autopilot: {repo.Name}'s open stories are blocked or too big for what's left before the reset"
                    : $"autopilot: {repo.Name} has no open stories left; its CLI is idle");
                continue;
            }

            var prompt = SessionControl.SafePrompt(Scheduler.PromptFor(next));
            if (dryRun)
            {
                said.Add($"autopilot would start {next.Id} in the {repo.Name} CLI: {next.Ticket.Summary}");
                continue;
            }
            var host = snapshot.Repos.SelectMany(r => r.Hosts).FirstOrDefault(h => h.Provider == Providers.Claude && IsAncestor(h.ProcessId, pid));
            if (!(host is not null ? SendToHost(host, prompt) : Typer(pid, prompt)))
            {
                Tell(key, $"autopilot: couldn't type into the {repo.Name} CLI (process {pid})");
                continue;
            }
            fedUtc[pid] = nowUtc;
            told.Remove(key);
            var all = store.LoadAssignments();
            all.RemoveAll(a => a.TicketId == next.Id);
            all.Add(new Assignment(next.Id, next.Repo, next.RepoPath, Providers.Claude, nowUtc, false) { Pilot = pid });
            store.SaveAssignments(all);
            said.Add($"autopilot: started {next.Id} in the {repo.Name} CLI: {next.Ticket.Summary}");
        }
        return said;
    }

    /// <summary>Idle long enough, and not about to be (or being) cycled for context.</summary>
    internal static bool Ready(SessionContext s, OrchestratorSettings settings, DateTime nowUtc) =>
        s.Idle && nowUtc - s.TurnUtc >= Settle && !(settings.ContextCycle && s.Percent >= settings.ContextCyclePercent);

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
