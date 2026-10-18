using System.Text.RegularExpressions;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using MindAttic.Workflow.Tickets;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>One open ticket in the cross-repo queue.</summary>
public sealed record QueueItem(string Repo, string RepoPath, Ticket Ticket, int Priority, Assignment? Assignment, bool RepoBusy)
{
    public string Id => Ticket.Id;

    /// <summary>Set when the ticket is assigned but nothing has happened where it was dispatched for
    /// <see cref="OrchestratorSettings.StaleAfterHours"/>: the last activity there. A stale ticket is back in the queue.</summary>
    public DateTime? StaleSinceUtc { get; init; }

    public bool IsStale => StaleSinceUtc is not null;

    /// <summary>What it's likely to cost, from what similar tickets cost; null with no history.</summary>
    public CostEstimate? Estimate { get; init; }

    /// <summary>Estimated to cost more than is left before the reset; see <see cref="SchedulePlan.TooBig"/>.</summary>
    public bool TooBig { get; init; }

    /// <summary>Tokens reserved for it (<see cref="OrchestratorSettings.Reservations"/>); 0 when none.</summary>
    public long Reserved { get; init; }

    /// <summary>Open tickets it waits for; it isn't proposed until they close.</summary>
    public IReadOnlyList<string> BlockedBy { get; init; } = [];

    /// <summary>Opened more than <see cref="OrchestratorSettings.GroomAfterDays"/> ago.</summary>
    public bool Old { get; init; }
    public bool IsAssigned => Assignment is not null && !IsStale;

    /// <summary>Whether starting it takes a worktree: its repo has a session, or it went stale in a worktree, where its
    /// partial work is. Every way of starting it (the proposal, the dashboard's Start, orchestrator dispatch) asks this.</summary>
    public bool NeedsWorktree => RepoBusy || (IsStale && Assignment!.Worktree);
}

/// <summary>An open ticket a live agent is working though no assignment says so (M.ORCH-T255): the session (and subagent,
/// null for the CLI itself) that has it, and how it's known. <paramref name="Filed"/>: it's held because that transcript filed it
/// this turn, the one hold the calling CLI itself is exempt from (M.ORCH-T328).</summary>
public sealed record WorkedTicket(string TicketId, string SessionId, string? AgentId, string How, bool Filed = false);

/// <summary>A ticket the scheduler thinks should start now, and how.</summary>
public sealed record Proposal(QueueItem Item, bool UseWorktree, string Reason);

/// <summary>What the scheduler concluded this round. <see cref="Slots"/> is the configured maximum;
/// <see cref="Cap"/> is what pacing allows right now (the same when unpaced).</summary>
public sealed record SchedulePlan(
    IReadOnlyList<QueueItem> Queue,
    IReadOnlyList<Proposal> Proposals,
    int Slots,
    int Occupied,
    string? Hold,
    IReadOnlyList<Assignment> Finished)
{
    public int Cap { get; init; } = Slots;
    public Pace? Pace { get; init; }

    /// <summary>The window is at its target: nothing new starts. <see cref="Pace"/> is only kept when pacing is on, but the
    /// stop doesn't depend on pacing (the pace gate refuses every call either way).</summary>
    public bool Stop { get; init; }

    /// <summary>Whether nothing may start: the stop, read from the plan or its pace.</summary>
    public bool Stopped => Stop || Pace is { Stop: true };

    /// <summary>Open tickets whose session went quiet without closing them; they're back in <see cref="Queue"/>.</summary>
    public IReadOnlyList<QueueItem> Stale { get; init; } = [];

    /// <summary>Tickets that would have been proposed but are estimated to cost more than is left before
    /// the reset: better split, or started after it, than run out of room halfway.</summary>
    public IReadOnlyList<QueueItem> TooBig { get; init; } = [];

    /// <summary>What's left before the reset once reservations are taken out: what <see cref="TooBig"/> is measured against.</summary>
    public double? RoomForRest { get; init; }

    /// <summary>Old open tickets nobody is on: re-prioritize them, or close them as wont-do.</summary>
    public IReadOnlyList<QueueItem> Groom { get; init; } = [];

    /// <summary>Open tickets that aren't fixes and that you haven't approved: never proposed or started until you do.</summary>
    public IReadOnlyList<QueueItem> AwaitingApproval { get; init; } = [];

    /// <summary>Tickets a live agent is working without an assignment (one it filed this turn and is fixing, or one its prompt
    /// or worktree names): never proposed or handed out, or two agents fix the same thing (M.ORCH-T255).</summary>
    public IReadOnlyList<WorkedTicket> Worked { get; init; } = [];

    /// <summary>Repos that have spent their cap of the 5-hour limit this block (settings --repo-cap): nothing more starts
    /// there, proposed or handed out (M.ORCH-T369).</summary>
    public IReadOnlySet<string> OverRepoCap { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Turns every repo's open tickets into one ordered queue and decides what should start now. It
/// never starts anything itself: <see cref="Dispatcher"/> does on a click, and <see cref="Autopilot"/> feeds the CLIs that ask for it.
/// </summary>
public static partial class Scheduler
{
    private static readonly Dictionary<string, int> TypeRank = new(StringComparer.Ordinal)
    {
        ["fix"] = 3, ["feature"] = 2, ["chore"] = 1, ["docs"] = 0,
    };

    public static SchedulePlan Plan(Snapshot snapshot, OrchestratorSettings settings, IReadOnlyList<Assignment> assignments)
    {
        var assigned = assignments.ToDictionary(a => a.TicketId, StringComparer.Ordinal);
        var openIds = snapshot.Repos.SelectMany(r => r.OpenTickets).Select(t => t.Id).ToHashSet(StringComparer.Ordinal);

        // A session that ended without closing its ticket would hold it out of the queue forever.
        DateTime? StaleSince(Assignment? a)
        {
            if (a is null || settings.StaleAfterHours <= 0) return null;
            var last = LastActivityUtc(a, snapshot);
            return snapshot.TakenUtc - last > TimeSpan.FromHours(settings.StaleAfterHours) ? last : null;
        }

        IReadOnlyList<string> Blockers(Ticket t) =>
            [.. BlockedByPhrase().Matches(t.Summary).Select(m => m.Groups["id"].Value.ToUpperInvariant())
                .Concat(settings.BlockedBy.GetValueOrDefault(t.Id) ?? [])
                .Concat(snapshot.Requires.GetValueOrDefault(t.Id) ?? [])
                .Where(id => id != t.Id && openIds.Contains(id))
                .Distinct(StringComparer.Ordinal)];

        var groomBefore = settings.GroomAfterDays > 0
            ? DateOnly.FromDateTime(snapshot.TakenUtc.ToLocalTime().AddDays(-settings.GroomAfterDays))
            : DateOnly.MinValue;
        bool IsOld(Ticket t) => DateOnly.TryParse(t.Date, System.Globalization.CultureInfo.InvariantCulture, out var opened) && opened < groomBefore;
        // Only fixes and tickets you approved are work; the rest wait for your yes outside the queue (one already
        // being worked stays, so its agent is still tracked).
        var awaiting = snapshot.Repos
            .SelectMany(r => r.OpenTickets.Where(t => !settings.MayWork(t) && !assigned.ContainsKey(t.Id))
                .Select(t => new QueueItem(r.Name, r.Path, t, settings.Priorities.GetValueOrDefault(t.Id), null, false) { Old = IsOld(t) }))
            .OrderBy(q => q.Repo, StringComparer.OrdinalIgnoreCase).ThenBy(q => q.Ticket.Number ?? int.MaxValue)
            .ToList();
        var queue = snapshot.Repos
            .SelectMany(r => r.OpenTickets.Where(t => settings.MayWork(t) || assigned.ContainsKey(t.Id)).Select(t => new QueueItem(
                r.Name, r.Path, t,
                settings.Priorities.GetValueOrDefault(t.Id),
                assigned.GetValueOrDefault(t.Id),
                r.State != RepoState.Dormant)
                {
                    StaleSinceUtc = StaleSince(assigned.GetValueOrDefault(t.Id)),
                    Estimate = snapshot.Costs.Estimate(r.Name, t.Type, t.Id),
                    Reserved = settings.Reservations.GetValueOrDefault(t.Id),
                    BlockedBy = Blockers(t),
                    Old = IsOld(t),
                }))
            .OrderBy(q => q.IsAssigned)
            .ThenByDescending(q => q.Reserved > 0)
            .ThenByDescending(q => q.Priority)
            // Smallest effort first: a known estimate before an unknown one, cheapest first.
            .ThenBy(q => q.Estimate is null)
            .ThenBy(q => q.Estimate?.Fresh ?? 0)
            .ThenByDescending(q => TypeRank.GetValueOrDefault(q.Ticket.Type))
            .ThenBy(q => q.Ticket.Date, StringComparer.Ordinal)
            .ThenBy(q => q.Ticket.Number ?? int.MaxValue)
            .ThenBy(q => q.Repo, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // A ticket that's no longer open was finished (or closed by hand): its assignment is done.
        var finished = assignments.Where(a => !openIds.Contains(a.TicketId)).ToList();

        // Every repo with an agent working or waiting holds a slot, as does every live assignment.
        var occupied = snapshot.Repos.Count(r => r.State != RepoState.Dormant);
        // A tab opened in the last few minutes holds its slot before its transcript shows it working,
        // so a run of starts can't outpace the cap (or keep filling slots with tabs that never came up).
        var dormant = snapshot.Repos.Where(r => r.State == RepoState.Dormant).Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        occupied += assignments.Count(a => dormant.Contains(a.Repo) && snapshot.TakenUtc - a.StartedUtc < TimeSpan.FromMinutes(10));

        // Paced, the cap follows the burn rate and the warn line is only a warning; unpaced, the warn
        // line holds everything, as it always did. Over a limit holds either way.
        var pace = snapshot.Pace is { IsPaced: true } p ? p : null;
        var cap = pace is null ? settings.MaxConcurrentSessions : Math.Min(pace.SessionCap, settings.MaxConcurrentSessions);
        var slots = Math.Max(0, cap - occupied);
        var overLimit = pace?.Mode == PaceMode.Over || snapshot.Budget is { Level: BudgetLevel.Over };

        string? hold = pace switch
        {
            { Mode: PaceMode.Over or PaceMode.Coast } => $"Holding. {pace.Note}",
            not null when snapshot.Budget is { Level: BudgetLevel.Over } b => $"Holding: budget at {b.WorstPercent:0}%.",
            not null => null,
            _ => snapshot.Budget switch
            {
                { Level: BudgetLevel.Over } b => $"Holding: budget at {b.WorstPercent:0}%.",
                { Level: BudgetLevel.Warn } b => $"Holding: budget at {b.WorstPercent:0}% (warns at {b.WarnPercent}%).",
                _ => null,
            },
        };

        // Another provider's own limit, when that's who gets dispatched.
        if (hold is null && settings.DefaultProvider != Providers.Claude && settings.ProviderCapPercent > 0 &&
            snapshot.ProviderUsage.FirstOrDefault(u => u.Provider == settings.DefaultProvider)?.Limits
                .FirstOrDefault(l => l.Window == "5h" && l.UsedPercent >= settings.ProviderCapPercent) is { } providerLimit)
            hold = $"Holding: {settings.DefaultProvider} is at {providerLimit.UsedPercent:0}% of its own 5-hour limit (cap {settings.ProviderCapPercent}%).";

        // Reservations come out of the room before anything else is measured against it.
        var reservedTotal = queue.Where(q => q.Reserved > 0 && !q.IsAssigned).Sum(q => q.Reserved);
        double? roomForRest = pace?.RoomTokens is { } room0 ? room0 - reservedTotal : null;
        if (hold is null && reservedTotal > 0 && roomForRest <= 0)
            hold = $"Holding: what's left before the reset is reserved ({Formatting.Tokens(reservedTotal)}).";
        if (hold is null && slots == 0)
            hold = cap < settings.MaxConcurrentSessions
                ? $"{pace!.Note} {occupied} open, so nothing new starts."
                : $"All {settings.MaxConcurrentSessions} session slots are in use.";

        var blockStart = snapshot.Budget?.CurrentBlock?.StartUtc;
        var limit5h = snapshot.Budget is { BlockLimitIsPeak: false } bb ? bb.BlockLimit : 0;
        long RepoSpent(string repo) => blockStart is not { } from ? 0
            : snapshot.Repos.Where(x => x.Name == repo).SelectMany(x => x.Sessions)
                .Where(s => s.Provider == Providers.Claude).Sum(s => s.UsageSince(from).Fresh);
        int Active(string repo) => Math.Max(
            snapshot.Repos.Any(x => x.Name == repo && x.State != RepoState.Dormant) ? 1 : 0,
            queue.Count(q => q.Repo == repo && q.IsAssigned));
        var busy = snapshot.Repos.Where(x => x.State != RepoState.Dormant).Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var worked = WorkedBy(snapshot);
        var workedIds = worked.Select(w => w.TicketId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var proposals = new List<Proposal>();
        var tooBig = new List<QueueItem>();
        var free = Math.Max(0, settings.MaxConcurrentSessions - occupied);
        var reposTaken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in queue)
        {
            if (item.IsAssigned || item.RepoPath.Length == 0 || reposTaken.Contains(item.Repo) || item.BlockedBy.Count > 0 ||
                workedIds.Contains(item.Id)) continue;
            var reserved = item.Reserved > 0;
            // A reserved ticket gets its slice even while pacing coasts or caps, never past a limit.
            if (reserved ? overLimit || proposals.Count >= free || pace?.RoomTokens < item.Reserved
                         : hold is not null || proposals.Count >= slots) continue;
            if (!reserved && roomForRest is { } room && item.Estimate is { } est && est.Fresh > room)
            {
                tooBig.Add(item);
                continue;
            }
            if (OrchestratorSettings.ForRepo(settings.RepoBudgetPercent, item.Repo, 0) is > 0 and var pct && limit5h > 0 &&
                RepoSpent(item.Repo) >= limit5h * pct / 100) continue;
            if (Active(item.Repo) >= OrchestratorSettings.ForRepo(settings.RepoMaxSessions, item.Repo, settings.MaxSessionsPerRepo)) continue;

            reposTaken.Add(item.Repo);
            var why = reserved ? $"reserved {Formatting.Tokens(item.Reserved)}"
                : item.Priority != 0 ? $"priority {item.Priority:+#;-#}" : $"oldest {item.Ticket.Type}";
            // A stale ticket that had a worktree goes back to it: its partial work is there.
            var worktree = item.NeedsWorktree;
            if (item.IsStale) why += $"; its last session went quiet {Formatting.Ago(item.StaleSinceUtc, snapshot.TakenUtc)}";
            if (item.Estimate is { } cost) why += $"; {cost}";
            var clash = (snapshot.LinkedRepos.GetValueOrDefault(item.Repo) ?? (IEnumerable<string>)[])
                .Where(l => !l.Equals(item.Repo, StringComparison.OrdinalIgnoreCase) && (busy.Contains(l) || reposTaken.Contains(l)))
                .Order(StringComparer.OrdinalIgnoreCase).ToList();
            if (clash.Count > 0)
                why += $"; shares duplicated code with {string.Join(", ", clash)}, which {(clash.Count == 1 ? "has" : "have")} a session: watch for conflicting edits";
            proposals.Add(new Proposal(item, worktree, item.RepoBusy ? $"{why}; repo has a session, so it gets its own worktree" : why));
        }

        if (tooBig.Count > 0)
        {
            var big = tooBig.Select(q => q.Id).ToHashSet(StringComparer.Ordinal);
            queue = [.. queue.Select(q => big.Contains(q.Id) ? q with { TooBig = true } : q)];
            tooBig = [.. queue.Where(q => q.TooBig)];
        }

        return new SchedulePlan(queue, proposals, settings.MaxConcurrentSessions, occupied, hold, finished)
        {
            Cap = cap, Pace = pace, Stop = snapshot.Pace is { Stop: true }, Stale = [.. queue.Where(q => q.IsStale)], TooBig = tooBig, RoomForRest = roomForRest,
            // Waiting for approval too: an old feature nobody approved is the likeliest to close as wont-do.
            Groom = [.. queue.Concat(awaiting).Where(q => q.Old && q.Assignment is null)],
            AwaitingApproval = awaiting, Worked = worked,
            OverRepoCap = queue.Select(q => q.Repo).Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(r => OrchestratorSettings.ForRepo(settings.RepoBudgetPercent, r, 0) is > 0 and var pct && limit5h > 0 && RepoSpent(r) >= limit5h * pct / 100)
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
        };
    }

    /// <summary>How recently a transcript must have been written for its agent to count as still working what it has.</summary>
    internal static readonly TimeSpan WorkingWithin = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Open tickets live agents are working, assigned or not (M.ORCH-T255): one a session's transcript filed in the turn it's
    /// still in (an agent that hits a bug files it and fixes it), and one a session or its subagent was started on (its
    /// prompt or worktree names it). A transcript quiet for <see cref="WorkingWithin"/> no longer holds anything.
    /// </summary>
    internal static IReadOnlyList<WorkedTicket> WorkedBy(Snapshot snapshot)
    {
        var open = snapshot.Repos.SelectMany(r => r.OpenTickets).Select(t => t.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool Live(DateTime lastUtc) => snapshot.TakenUtc - lastUtc < WorkingWithin;
        var worked = new List<WorkedTicket>();
        foreach (var s in snapshot.Repos.SelectMany(r => r.Sessions).Where(s => s.Provider == Providers.Claude))
        {
            // Its own ticket first: a session that was started on a ticket and filed it too is working it, not just its filer.
            if (s.TicketId is { } own && Live(s.LastWriteUtc)) worked.Add(new(own, s.SessionId, null, "started on it"));
            foreach (var f in s.Filing.Where(f => Live(f.LastWriteUtc)))
                worked.Add(new(f.TicketId, s.SessionId, f.AgentId, f.AgentId is null ? "filed it this turn" : $"its subagent {f.AgentId} filed it", Filed: true));
            foreach (var sub in s.TicketedSubagents.Where(t => t.Events.Count > 0 && Live(t.Events[^1].AtUtc)))
                worked.Add(new(sub.TicketId, s.SessionId, sub.AgentId, $"its subagent {sub.AgentId} was handed it"));
        }
        return [.. worked.Where(w => open.Contains(w.TicketId)).DistinctBy(w => (w.TicketId.ToUpperInvariant(), w.SessionId, w.AgentId))];
    }

    [GeneratedRegex(@"blocked by (?<id>(?:[A-Z]\.)?[A-Z][A-Z0-9]*-T\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex BlockedByPhrase();

    /// <summary>
    /// The last sign of life where an assignment was dispatched: a transcript written under its
    /// directory, or (as "now") a Launcher tab still open there. Never earlier than the dispatch.
    /// </summary>
    internal static DateTime LastActivityUtc(Assignment a, Snapshot snapshot)
    {
        var last = a.StartedUtc;
        foreach (var repo in snapshot.Repos)
        {
            if (repo.Hosts.Any(h => IsUnder(h.WorkingDirectory, a.Directory))) return snapshot.TakenUtc;
            foreach (var s in repo.Sessions)
                if (s.LastWriteUtc > last && IsUnder(s.Cwd, a.Directory)) last = s.LastWriteUtc;
        }
        return last;
    }

    private static bool IsUnder(string? path, string directory)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(directory)) return false;
        var p = Path.TrimEndingDirectorySeparator(path.Replace('/', '\\'));
        var d = Path.TrimEndingDirectorySeparator(directory.Replace('/', '\\'));
        return p.Equals(d, StringComparison.OrdinalIgnoreCase)
            || p.StartsWith(d + "\\", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The opening prompt for a ticket: what to do, and how to mark it done.</summary>
    public static string PromptFor(QueueItem item) =>
        $"Work on ticket {item.Id} ({item.Ticket.Type}): {item.Ticket.Summary} " +
        $"When it's done: run /commit (it commits only what you changed), then mark it finished with: workflow ticket close {item.Id} --repo {item.Repo} --ref \"<the test or commit that proves it>\"";
}
