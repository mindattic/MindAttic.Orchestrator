using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using MindAttic.Workflow.Tickets;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>One open ticket in the cross-repo queue.</summary>
public sealed record QueueItem(string Repo, string RepoPath, Ticket Ticket, int Priority, Assignment? Assignment, bool RepoBusy)
{
    public string Id => Ticket.Id;
    public bool IsAssigned => Assignment is not null;
}

/// <summary>A ticket the scheduler thinks should start now, and how.</summary>
public sealed record Proposal(QueueItem Item, bool UseWorktree, string Reason);

/// <summary>What the scheduler concluded this round.</summary>
public sealed record SchedulePlan(
    IReadOnlyList<QueueItem> Queue,
    IReadOnlyList<Proposal> Proposals,
    int Slots,
    int Occupied,
    string? Hold,
    IReadOnlyList<Assignment> Finished);

/// <summary>
/// Turns every repo's open tickets into one ordered queue and decides what should start now. It
/// never starts anything itself: <see cref="Dispatcher"/> does, on a click or (opt-in) automatically.
/// </summary>
public static class Scheduler
{
    private static readonly Dictionary<string, int> TypeRank = new(StringComparer.Ordinal)
    {
        ["fix"] = 3, ["feature"] = 2, ["chore"] = 1, ["docs"] = 0,
    };

    public static SchedulePlan Plan(Snapshot snapshot, OrchestratorSettings settings, IReadOnlyList<Assignment> assignments)
    {
        var assigned = assignments.ToDictionary(a => a.TicketId, StringComparer.Ordinal);
        var openIds = snapshot.Repos.SelectMany(r => r.OpenTickets).Select(t => t.Id).ToHashSet(StringComparer.Ordinal);

        var queue = snapshot.Repos
            .SelectMany(r => r.OpenTickets.Select(t => new QueueItem(
                r.Name, r.Path, t,
                settings.Priorities.GetValueOrDefault(t.Id),
                assigned.GetValueOrDefault(t.Id),
                r.State != RepoState.Dormant)))
            .OrderBy(q => q.IsAssigned)
            .ThenByDescending(q => q.Priority)
            .ThenByDescending(q => TypeRank.GetValueOrDefault(q.Ticket.Type))
            .ThenBy(q => q.Ticket.Date, StringComparer.Ordinal)
            .ThenBy(q => q.Ticket.Number ?? int.MaxValue)
            .ThenBy(q => q.Repo, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // A ticket that's no longer open was finished (or closed by hand): its assignment is done.
        var finished = assignments.Where(a => !openIds.Contains(a.TicketId)).ToList();

        // Every repo with an agent working or waiting holds a slot, as does every live assignment.
        var occupied = snapshot.Repos.Count(r => r.State != RepoState.Dormant);
        var slots = Math.Max(0, settings.MaxConcurrentSessions - occupied);

        string? hold = snapshot.Budget switch
        {
            { Level: BudgetLevel.Over } b => $"Holding: budget at {b.WorstPercent:0}%.",
            { Level: BudgetLevel.Warn } b => $"Holding: budget at {b.WorstPercent:0}% (warns at {b.WarnPercent}%).",
            _ => null,
        };
        if (hold is null && slots == 0)
            hold = $"All {settings.MaxConcurrentSessions} session slots are in use.";

        var proposals = new List<Proposal>();
        if (hold is null)
        {
            var reposTaken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in queue)
            {
                if (proposals.Count >= slots) break;
                if (item.IsAssigned || item.RepoPath.Length == 0 || !reposTaken.Add(item.Repo)) continue;
                var why = item.Priority != 0 ? $"priority {item.Priority:+#;-#}" : $"oldest {item.Ticket.Type}";
                proposals.Add(new Proposal(item, item.RepoBusy,
                    item.RepoBusy ? $"{why}; repo has a session, so it gets its own worktree" : why));
            }
        }

        return new SchedulePlan(queue, proposals, settings.MaxConcurrentSessions, occupied, hold, finished);
    }

    /// <summary>The opening prompt for a ticket: what to do, and how to mark it done.</summary>
    public static string PromptFor(QueueItem item) =>
        $"Work on ticket {item.Id} ({item.Ticket.Type}): {item.Ticket.Summary} " +
        $"When it's done and committed, close it with: workflow ticket close {item.Id} --repo {item.Repo}";
}
