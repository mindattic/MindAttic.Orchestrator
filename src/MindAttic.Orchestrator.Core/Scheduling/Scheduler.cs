using System.Text.RegularExpressions;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using MindAttic.Tickets.Log;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>One open ticket in the cross-repo queue.</summary>
public sealed record QueueItem(string Repo, string RepoPath, Ticket Ticket, int Priority, Assignment? Assignment, bool RepoBusy)
{
    /// <summary>The ticket's ID (<see cref="Ticket"/>'s).</summary>
    public string Id => Ticket.Id;

    /// <summary>Set when the ticket is assigned but nothing has happened where it was dispatched for
    /// <see cref="OrchestratorSettings.StaleAfterHours"/>: the last activity there. A stale ticket is back in the queue.</summary>
    public DateTime? StaleSinceUtc { get; init; }

    /// <summary>Whether it's assigned but gone quiet (<see cref="StaleSinceUtc"/> set), so it's back in the queue.</summary>
    public bool IsStale => StaleSinceUtc is not null;

    /// <summary>What it's likely to cost, from what similar tickets cost; null with no history.</summary>
    public CostEstimate? Estimate { get; init; }

    /// <summary>Estimated to cost more than is left before the reset; see <see cref="SchedulePlan.TooBig"/>.</summary>
    public bool TooBig { get; init; }

    /// <summary>Tokens reserved for it in the ticket database (<see cref="Snapshot.Reservations"/>); 0 when none.</summary>
    public long Reserved { get; init; }

    /// <summary>Open tickets it waits for; it isn't proposed until they close.</summary>
    public IReadOnlyList<string> BlockedBy { get; init; } = [];

    /// <summary>Queued tickets that wait for this one (it's their prerequisite), so it's pulled ahead of them (M.ORCH-T863).</summary>
    public IReadOnlyList<string> RequiredBy { get; init; } = [];

    /// <summary>Opened more than <see cref="OrchestratorSettings.GroomAfterDays"/> ago.</summary>
    public bool Old { get; init; }

    /// <summary>Approved for work in the ticket database's queue (<see cref="Snapshot.Approved"/>, M.ORCH-T761).</summary>
    public bool Approved { get; init; }

    /// <summary>Whether it's planned as one task (level:task, M.ORCH-T1906): true gets the task budget in its prompt, false
    /// (an unplanned leaf) a cheap plan pass first; null (not known, as in a hand-built item) neither.</summary>
    public bool? IsTask { get; init; }

    /// <summary>The role its <c>role:</c> label names (M.ORCH-T1746); null when none, which <see cref="Roles.Default"/> fills in.</summary>
    public string? Role { get; init; }

    /// <summary>One work unit in uncached tokens (<see cref="WorkUnits.Budget"/>), named in the prompt; 0 means the default.</summary>
    public long UnitBudget { get; init; }

    /// <summary>Whether it may be worked: a fix, or approved.</summary>
    public bool MayWork => Approved || Ticket.Type.Equals("fix", StringComparison.OrdinalIgnoreCase);

    /// <summary>The move holding it (M.ORCH-T637): its CLI is on its way to another login, and it stays that CLI's, never
    /// proposed, handed out, claimed or started elsewhere, until the move lands or fails. Null when it isn't moving.</summary>
    public Sessions.MoveLock? Moving { get; init; }

    /// <summary>Whether someone has it: an assignment that isn't stale, or a move holding it.</summary>
    public bool IsAssigned => (Assignment is not null && !IsStale) || Moving is not null;

    /// <summary>The user it's assigned to (M.ORCH-T668), by their login's email (<see cref="Snapshot.Assignees"/>, the ticket database's);
    /// null for an unassigned ticket. A pin: it's dispatched only on that login, and waits for them while they're out of room.</summary>
    public string? Assignee { get; init; }

    /// <summary>Whether it's assigned to a user (<see cref="Assignee"/>): see <see cref="TicketPins"/>.</summary>
    public bool IsPinned => Assignee is not null;

    /// <summary>Its graph weight in the ticket database (<see cref="Snapshot.GraphWeights"/>, M.TIX-T316): after explicit priority,
    /// the heavier runs sooner (M.ORCH-T1457).</summary>
    public long GraphWeight { get; init; }

    /// <summary>Its friendly name in the ticket database (<see cref="Snapshot.Names"/>, M.TIX-T313); null while it has none.</summary>
    public string? Name { get; init; }

    /// <summary>The active milestone it's on (<see cref="Snapshot.Milestones"/>, M.TIX-T335), null when on none: after explicit
    /// priority and fixes, milestone work runs before the rest (M.ORCH-T1683).</summary>
    public TicketMilestone? Milestone { get; init; }

    /// <summary>Whether it's on an active milestone (<see cref="Milestone"/>).</summary>
    public bool OnMilestone => Milestone is not null;
}

/// <summary>An open ticket a live agent is working though no assignment says so (M.ORCH-T255): the session (and subagent,
/// null for the CLI itself) that has it, and how it's known. <paramref name="Filed"/>: it's held because that transcript filed it
/// this turn, the one hold the calling CLI itself is exempt from (M.ORCH-T328).</summary>
public sealed record WorkedTicket(string TicketId, string SessionId, string? AgentId, string How, bool Filed = false);

/// <summary>A ticket the scheduler thinks should start now, and why. It starts in its repo's own checkout on main, never a
/// worktree (M.ORCH-T1122).</summary>
public sealed record Proposal(QueueItem Item, string Reason);

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
    /// <summary>How many sessions may run at once now: the pace's session cap within <see cref="Slots"/>, or <see cref="Slots"/> when unpaced.</summary>
    public int Cap { get; init; } = Slots;
    /// <summary>The pace the plan was made under; null when pacing is off.</summary>
    public Pace? Pace { get; init; }

    /// <summary>The pace whose subagent allowance <c>orchestrator next</c> counts against (M.ORCH-T2246): <see cref="Pace"/>, or,
    /// unpaced, the login's own allowance (<see cref="AccountPacer"/>, M.ORCH-T660) that <see cref="Pace"/> leaves out. Before,
    /// an unpaced login (a solo Ultra account under its weekly line) reached Allocate with no pace at all and was handed the
    /// session setting less every ticket being worked anywhere in the workspace, which another busy repo drove below zero.
    /// Derived, never stored (M.ORCH-T2267): a stored copy went stale when a plan was remade <c>with { Pace = ... }</c>, so
    /// <c>next</c> counted against the pace the plan was first made under instead of the one it carries.</summary>
    public Pace? AllowancePace => Pace ?? UnpacedAllowance;

    /// <summary>The login's own pace while it is unpaced (pacing off or <see cref="Pace.IsPaced"/> false), when it carries a
    /// subagent allowance; <see cref="AllowancePace"/> falls back to it when <see cref="Pace"/> is null (M.ORCH-T2246).</summary>
    public Pace? UnpacedAllowance { get; init; }

    /// <summary>The window is at its target: nothing new starts. <see cref="Pace"/> is only kept when pacing is on, but the
    /// stop doesn't depend on pacing (the pace gate refuses every call either way).</summary>
    public bool Stop { get; init; }

    /// <summary>Whether nothing may start: the stop, read from the plan or its pace.</summary>
    public bool Stopped => Stop || Pace is { Stop: true };

    /// <summary>Open tickets whose session went quiet without closing them; they're back in <see cref="Queue"/>.</summary>
    public IReadOnlyList<QueueItem> Stale { get; init; } = [];

    /// <summary>Repos whose Tickets.db couldn't be read (M.ORCH-T727): their open tickets are unknown, not none (M.ORCH-T740).</summary>
    public IReadOnlySet<string> UnknownRepos { get; init; } = new HashSet<string>();

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
/// never starts anything itself: <see cref="Dispatcher"/> does on a click, and <see cref="Allocate"/> feeds the CLIs that ask for it.
/// </summary>
public static partial class Scheduler
{
    private const int Century = 36_525;

    /// <summary>Whether <paramref name="id"/> is part of <paramref name="ancestor"/> at any depth through <paramref name="partOf"/>
    /// (id → its part-of parents), a visited set against part-of cycles (M.ORCH-T2286).</summary>
    internal static bool IsPartOf(IReadOnlyDictionary<string, IReadOnlyList<string>> partOf, string id, string ancestor)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { id };
        var pending = new Stack<string>(partOf.GetValueOrDefault(id) ?? []);
        while (pending.TryPop(out var at))
        {
            if (at.Equals(ancestor, StringComparison.OrdinalIgnoreCase)) return true;
            if (!seen.Add(at)) continue;
            foreach (var up in partOf.GetValueOrDefault(at) ?? []) pending.Push(up);
        }
        return false;
    }

    /// <summary>Orders every repo's open tickets into one queue and decides which should start now within the slots and pace; starts nothing.</summary>
    public static SchedulePlan Plan(Snapshot snapshot, OrchestratorSettings settings, IReadOnlyList<Assignment> assignments)
    {
        var assigned = assignments.ToDictionary(a => a.TicketId, StringComparer.Ordinal);
        var openIds = snapshot.Repos.SelectMany(r => r.OpenTickets).Select(t => t.Id).ToHashSet(StringComparer.Ordinal);

        // A moving CLI's tickets stay its own until the move lands or fails (M.ORCH-T637): never stale meanwhile.
        Sessions.MoveLock? MovingOf(string id) => Sessions.MoveLocks.ForTicket(snapshot.Moves, id);

        // A session that ended without closing its ticket would hold it out of the queue forever.
        DateTime? StaleSince(Assignment? a)
        {
            if (a is null || settings.StaleAfterHours <= 0 || MovingOf(a.TicketId) is not null) return null;
            var last = LastActivityUtc(a, snapshot);
            // Clamped to a century: a huge setting threw out of every plan, so out of every daemon tick (M.ORCH-T429).
            return snapshot.TakenUtc - last > TimeSpan.FromHours(Math.Min(settings.StaleAfterHours, Century * 24)) ? last : null;
        }

        // Approval and priority from the ticket database's queue alone, read through the MindAttic.Tickets engine, where
        // tickets ticket approve/priority and the Tickets Desktop write them too (M.ORCH-T761).
        bool IsApproved(string id) => snapshot.Approved.Contains(id);
        // An epic (label 'epic', M.ORCH-T1146) is never work itself: it's started by approving it, and closes when its children are done.
        bool IsEpic(string id) => snapshot.Epics.Contains(id);
        // A container (an open ticket with open part-of children, M.ORCH-T1904) is held out exactly like an epic: its children are the work.
        bool IsContainer(string id) => snapshot.IsContainer(id);

        // A block set by hand is a 'requires' link in the ticket database (tickets ticket block, M.ORCH-T762), or a 'part-of'
        // link to an unapproved epic (M.ORCH-T1210: nothing in an unapproved epic is picked up just because only 'part-of'
        // names it, not 'requires' too). One on an epic waits only until the epic is approved (started), not until it closes
        // (M.ORCH-T1146). Any other part-of parent is a container whose children are the work, never their blocker (M.ORCH-T1904),
        // though what it requires blocks them (M.ORCH-T1909).
        // A part-of ancestor's 'requires' blockers block t too (M.ORCH-T1909): a story split into tasks hands its prerequisites
        // down, so its tasks wait for them as the story would have. Any depth, a visited set against part-of cycles.
        IEnumerable<string> AncestorRequires(string id)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal) { id };
            var pending = new Stack<string>(snapshot.PartOf.GetValueOrDefault(id) ?? []);
            while (pending.TryPop(out var parent))
            {
                if (!seen.Add(parent)) continue;
                // A requires link to the ancestor's own part-of descendant is the split's own work (tickets ticket split
                // adds one per task), not a prerequisite handed down: counting it deadlocked a split's tasks on their own
                // parent or on each other (M.ORCH-T2286).
                foreach (var req in snapshot.Requires.GetValueOrDefault(parent) ?? [])
                    if (!IsPartOf(snapshot.PartOf, req, parent)) yield return req;
                foreach (var up in snapshot.PartOf.GetValueOrDefault(parent) ?? []) pending.Push(up);
            }
        }
        IReadOnlyList<string> Blockers(Ticket t) =>
            [.. BlockedByPhrase().Matches(t.Summary).Select(m => m.Groups["id"].Value.ToUpperInvariant())
                .Concat(snapshot.Requires.GetValueOrDefault(t.Id) ?? [])
                .Concat(AncestorRequires(t.Id))
                .Where(id => !(IsEpic(id) && IsApproved(id)))
                .Concat((snapshot.PartOf.GetValueOrDefault(t.Id) ?? []).Where(id => IsEpic(id) && !IsApproved(id)))
                .Where(id => id != t.Id && openIds.Contains(id))
                .Distinct(StringComparer.Ordinal)];

        var groomBefore = settings.GroomAfterDays > 0
            ? DateOnly.FromDateTime(snapshot.TakenUtc.ToLocalTime().AddDays(-Math.Min(settings.GroomAfterDays, Century)))
            : DateOnly.MinValue;
        // The canonical rule lives on Snapshot itself (M.ORCH-T1210): fix or approved, never an epic, never part-of one that
        // isn't approved yet — one place, so this and every other caller can never drift apart on what "may work" means.
        bool MayWork(Ticket t) => snapshot.MayWork(t);
        // Clamped to what a queue item holds.
        int Priority(string id) => (int)Math.Clamp(snapshot.Priorities.GetValueOrDefault(id), int.MinValue, int.MaxValue);
        // Who each ticket is assigned to (M.ORCH-T668), from the ticket database (M.ORCH-T762), whatever case its id was saved in.
        var assignees = TicketPins.Assignees(snapshot);
        // Milestone-first (M.ORCH-T1683, roadmap Phase 9), only while an active milestone has open tickets: fixes, then the
        // tickets on an active milestone (the earlier milestone first), then everything else. Ordering only: nothing is held
        // back or reserved for it. With no active milestone every ticket is in one tier and the order is what it was.
        var milestoneFirst = snapshot.Milestones is { Count: > 0 };
        int Tier(QueueItem q) => !milestoneFirst || q.Ticket.Type.Equals("fix", StringComparison.OrdinalIgnoreCase) ? 0 : q.OnMilestone ? 1 : 2;
        bool IsOld(Ticket t) => DateOnly.TryParse(t.Date, System.Globalization.CultureInfo.InvariantCulture, out var opened) && opened < groomBefore;
        // Only fixes and tickets you approved are work; the rest wait for your yes outside the queue (one already
        // being worked stays, so its agent is still tracked). One whose assignment went stale waits for it too (M.ORCH-T394).
        var awaiting = snapshot.Repos
            // An epic or container waits here for its yes, and once approved (started) it's neither waiting nor work (M.ORCH-T1146, M.ORCH-T1904).
            .SelectMany(r => r.OpenTickets.Where(t => !MayWork(t) && !(IsContainer(t.Id) && IsApproved(t.Id)) && (assigned.GetValueOrDefault(t.Id) is not { } a || StaleSince(a) is not null))
                .Select(t => new QueueItem(r.Name, r.Path, t, Priority(t.Id), null, false) { Old = IsOld(t), Approved = IsApproved(t.Id), Assignee = assignees.GetValueOrDefault(t.Id) }))
            .OrderBy(q => q.Repo, StringComparer.OrdinalIgnoreCase).ThenBy(q => q.Ticket.Number ?? int.MaxValue)
            .ToList();
        var queue = snapshot.Repos
            // A host branch ticket waiting on its pushed branch is open but not work (M.ORCH-T1661): never handed out again meanwhile.
            .SelectMany(r => r.OpenTickets.Where(t => !IsContainer(t.Id) && !snapshot.AwaitingBranch.Contains(t.Id) && (MayWork(t) || assigned.ContainsKey(t.Id))).Select(t => new QueueItem(
                r.Name, r.Path, t,
                Priority(t.Id),
                assigned.GetValueOrDefault(t.Id),
                r.State != RepoState.Dormant)
                {
                    StaleSinceUtc = StaleSince(assigned.GetValueOrDefault(t.Id)),
                    Estimate = snapshot.Costs.Estimate(r.Name, t.Type, t.Id),
                    Reserved = snapshot.Reservations.GetValueOrDefault(t.Id),
                    BlockedBy = Blockers(t),
                    Old = IsOld(t),
                    Approved = IsApproved(t.Id),
                    Moving = MovingOf(t.Id),
                    Assignee = assignees.GetValueOrDefault(t.Id),
                    GraphWeight = snapshot.GraphWeights?.GetValueOrDefault(t.Id) ?? 0,
                    Name = snapshot.Names?.GetValueOrDefault(t.Id),
                    Milestone = snapshot.Milestones?.GetValueOrDefault(t.Id),
                    IsTask = snapshot.IsTask(t.Id),
                    Role = snapshot.Roles?.GetValueOrDefault(t.Id),
                    UnitBudget = WorkUnits.Budget(settings),
                }))
            .OrderBy(q => q.IsAssigned)
            // A ticket the last-resort cool-off aborted mid-run goes back at the front for the next round (M.ORCH-T786).
            .ThenByDescending(q => snapshot.Requeued.Contains(q.Id))
            .ThenByDescending(q => q.Reserved > 0)
            // An explicit priority (tickets ticket bump/priority, or the daemon lifting a failing-test fix above the rest,
            // M.ORCH-T562) still outranks recency (M.ORCH-T863).
            .ThenByDescending(q => q.Priority)
            // Then milestone-first (M.ORCH-T1683): below an explicit priority, above weight and recency, so newer unrelated work
            // doesn't pass a milestone ticket. Within the milestone tier the graph weight below is its critical path.
            .ThenBy(Tier)
            .ThenBy(q => Tier(q) == 1 ? q.Milestone!.Rank : 0)
            // Then the Tickets graph weight, heaviest first (GraphWeight.Order's rule, M.TIX-T316, M.ORCH-T1457): what most other
            // open work leans on goes first; today's order breaks ties.
            .ThenByDescending(q => q.GraphWeight)
            .ThenBy(q => q, Newest)
            .ToList();
        // Then each ticket's open prerequisites ('requires' links) are pulled ahead of it, transitively (M.ORCH-T863).
        queue = PrerequisitesFirst(queue);

        // A ticket that's no longer open was finished (or closed by hand): its assignment is done.
        // So is one whose work is pushed to its work branch on the host, waiting on its test (M.ORCH-T1661): its agent is done.
        var finished = assignments.Where(a => (!openIds.Contains(a.TicketId) || snapshot.AwaitingBranch.Contains(a.TicketId)) && !snapshot.UnknownRepos.Contains(a.Repo)).ToList();

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
                { Level: BudgetLevel.Warn } b => $"Holding: budget at {b.WorstPercent:0}% (warns at {b.ActiveWarnPercent}%).",
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
            // An unapproved ticket is in the queue only while it's being worked (approved then, or taken back since): once its
            // assignment goes stale it waits for your yes like any other, not proposed again (M.ORCH-T383).
            if (item.IsAssigned || item.RepoPath.Length == 0 || item.BlockedBy.Count > 0 ||
                workedIds.Contains(item.Id) || !item.MayWork) continue;
            var reserved = item.Reserved > 0;
            // Too big is said of every one it's true of, held, past the slots or behind its repo's next one or not: the
            // under-target warning counts an unmarked one as work a CLI could take (M.ORCH-T395).
            if (!reserved && roomForRest is { } room && item.Estimate is { } est && est.Fresh > room)
            {
                tooBig.Add(item);
                continue;
            }
            if (reposTaken.Contains(item.Repo)) continue;
            // A reserved ticket gets its slice even while pacing coasts or caps, never past a limit.
            if (reserved ? overLimit || proposals.Count >= free || pace?.RoomTokens < item.Reserved
                         : hold is not null || proposals.Count >= slots) continue;
            if (OrchestratorSettings.ForRepo(settings.RepoBudgetPercent, item.Repo, 0) is > 0 and var pct && limit5h > 0 &&
                RepoSpent(item.Repo) >= limit5h * pct / 100) continue;
            if (Active(item.Repo) >= OrchestratorSettings.ForRepo(settings.RepoMaxSessions, item.Repo, settings.MaxSessionsPerRepo)) continue;

            reposTaken.Add(item.Repo);
            var why = reserved ? $"reserved {Formatting.Tokens(item.Reserved)}"
                : item.RequiredBy.Count > 0 ? $"required by {string.Join(", ", item.RequiredBy)}"
                : item.Priority != 0 ? $"priority {item.Priority:+#;-#}"
                : milestoneFirst && Tier(item) == 1 ? $"on active milestone {item.Milestone!.Id}" : $"newest {item.Ticket.Type}";
            if (item.IsStale) why += $"; its last session went quiet {Formatting.Ago(item.StaleSinceUtc, snapshot.TakenUtc)}";
            if (item.Estimate is { } cost) why += $"; {cost}";
            var clash = (snapshot.LinkedRepos.GetValueOrDefault(item.Repo) ?? (IEnumerable<string>)[])
                .Where(l => !l.Equals(item.Repo, StringComparison.OrdinalIgnoreCase) && (busy.Contains(l) || reposTaken.Contains(l)))
                .Order(StringComparer.OrdinalIgnoreCase).ToList();
            if (clash.Count > 0)
                why += $"; shares duplicated code with {string.Join(", ", clash)}, which {(clash.Count == 1 ? "has" : "have")} a session: watch for conflicting edits";
            // In the repo's own checkout on main whatever else runs there (M.ORCH-T1122): agents there touch only their own files.
            proposals.Add(new Proposal(item, item.RepoBusy ? $"{why}; repo has a session, so it shares the checkout (touching only its own files)" : why));
        }

        if (tooBig.Count > 0)
        {
            var big = tooBig.Select(q => q.Id).ToHashSet(StringComparer.Ordinal);
            queue = [.. queue.Select(q => big.Contains(q.Id) ? q with { TooBig = true } : q)];
            tooBig = [.. queue.Where(q => q.TooBig)];
        }

        return new SchedulePlan(queue, proposals, settings.MaxConcurrentSessions, occupied, hold, finished)
        {
            Cap = cap, UnknownRepos = snapshot.UnknownRepos, Pace = pace,
            UnpacedAllowance = pace is null && snapshot.Pace is { SubagentAllowance: not null } own ? own : null, Stop = snapshot.Pace is { Stop: true }, Stale = [.. queue.Where(q => q.IsStale)], TooBig = tooBig, RoomForRest = roomForRest,
            // Waiting for approval too: an old feature nobody approved is the likeliest to close as wont-do.
            Groom = [.. queue.Concat(awaiting).Where(q => q.Old && q.Assignment is null)],
            AwaitingApproval = awaiting, Worked = worked,
            OverRepoCap = queue.Select(q => q.Repo).Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(r => OrchestratorSettings.ForRepo(settings.RepoBudgetPercent, r, 0) is > 0 and var pct && limit5h > 0 && RepoSpent(r) >= limit5h * pct / 100)
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
        };
    }

    /// <summary>
    /// Newest first (M.ORCH-T863: "recently created tickets always have highest priority"): the later creation date first,
    /// and on the same date the higher ticket number, then the repo's name so the order is stable.
    /// </summary>
    internal static readonly IComparer<QueueItem> Newest = Comparer<QueueItem>.Create((a, b) =>
    {
        var byDate = string.CompareOrdinal(b.Ticket.Date, a.Ticket.Date);
        if (byDate != 0) return byDate;
        var byNumber = (b.Ticket.Number ?? int.MinValue).CompareTo(a.Ticket.Number ?? int.MinValue);
        return byNumber != 0 ? byNumber : StringComparer.OrdinalIgnoreCase.Compare(a.Repo, b.Repo);
    });

    /// <summary>
    /// Pulls each unassigned ticket's open prerequisites (<see cref="QueueItem.BlockedBy"/>: its 'requires' links and "blocked by"
    /// phrases) ahead of it, transitively, keeping the prerequisites in <paramref name="ordered"/>'s own order among themselves
    /// (M.ORCH-T863). A prerequisite outside the list (awaiting approval) or already assigned isn't moved; a cycle is broken
    /// where it closes, so each ticket appears once. Assigned tickets keep their place at the end. Each moved prerequisite's
    /// <see cref="QueueItem.RequiredBy"/> names the queued tickets that wait for it.
    /// </summary>
    internal static List<QueueItem> PrerequisitesFirst(IReadOnlyList<QueueItem> ordered)
    {
        var rank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var byId = new Dictionary<string, QueueItem>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
            if (!ordered[i].IsAssigned && byId.TryAdd(ordered[i].Id, ordered[i])) rank[ordered[i].Id] = i;
        var requiredBy = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var q in ordered.Where(q => !q.IsAssigned))
            foreach (var id in q.BlockedBy.Where(byId.ContainsKey))
                (requiredBy.TryGetValue(id, out var list) ? list : requiredBy[id] = []).Add(q.Id);

        var result = new List<QueueItem>(ordered.Count);
        var placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Place(QueueItem q)
        {
            // Already placed, or on the path that led here: a cycle, broken by placing this one after what it waits for.
            if (placed.Contains(q.Id) || !visiting.Add(q.Id)) return;
            foreach (var id in q.BlockedBy.Where(byId.ContainsKey).OrderBy(id => rank[id])) Place(byId[id]);
            visiting.Remove(q.Id);
            placed.Add(q.Id);
            result.Add(requiredBy.TryGetValue(q.Id, out var deps) ? q with { RequiredBy = deps } : q);
        }
        foreach (var q in ordered)
            if (!q.IsAssigned) Place(q);
        result.AddRange(ordered.Where(q => q.IsAssigned));
        return result;
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
            // Every ticket it was handed (M.ORCH-T2276): a coordinator may give one subagent several.
            foreach (var sub in s.TicketedSubagents.Where(t => t.Events.Count > 0 && Live(t.Events[^1].AtUtc)))
                foreach (var id in sub.Tickets)
                    worked.Add(new(id, s.SessionId, sub.AgentId, $"its subagent {sub.AgentId} was handed it"));
        }
        return [.. worked.Where(w => open.Contains(w.TicketId)).DistinctBy(w => (w.TicketId.ToUpperInvariant(), w.SessionId, w.AgentId))];
    }

    [GeneratedRegex(@"blocked by (?<id>(?:[A-Z]\.)?[A-Z][A-Z0-9]*-T\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex BlockedByPhrase();

    /// <summary>
    /// The last sign of life of an assignment: a transcript working it, or (as "now") a Launcher tab still running it. In the
    /// repo's shared checkout (M.ORCH-T1122) that's judged by its ticket (a session or subagent started on it, a tab titled
    /// for it), in an old worktree of its own by its folder (<see cref="TicketActivity"/>). Never earlier than the dispatch.
    /// </summary>
    internal static DateTime LastActivityUtc(Assignment a, Snapshot snapshot)
    {
        var last = a.StartedUtc;
        var byFolder = TicketActivity.ByFolder(a, snapshot);
        foreach (var repo in snapshot.Repos)
        {
            if (repo.Hosts.Any(h => TicketActivity.Hosts(h, a, byFolder))) return snapshot.TakenUtc;
            foreach (var s in repo.Sessions)
            {
                if (s.LastWriteUtc > last && TicketActivity.Works(s.Cwd, s.TicketId, a, byFolder)) last = s.LastWriteUtc;
                // Checked unconditionally (M.ORCH-T1164), the same as TicketActivity.Active: a legacy worktree
                // assignment with a quiet parent session but an actively-writing subagent must count as live too.
                foreach (var t in s.TicketedSubagents)
                    if (t.Events.Count > 0 && t.Events[^1].AtUtc > last && t.Tickets.Any(id => TicketActivity.Works(t.Cwd, id, a, byFolder))) last = t.Events[^1].AtUtc;
            }
        }
        return last;
    }

    /// <summary>The opening prompt for a ticket: what to do, where (the repo's own checkout on main, M.ORCH-T1122), and how to land
    /// and mark it done, the same as /allocate's (M.ORCH-T2139: build only, commit only its own paths, merge, push, close).</summary>
    public static string PromptFor(QueueItem item) => Allocate.PromptFor(item, item.RepoPath);
}
