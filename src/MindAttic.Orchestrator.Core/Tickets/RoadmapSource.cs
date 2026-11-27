using MindAttic.Tickets.Data;
using MindAttic.Tickets.Log;

namespace MindAttic.Orchestrator.Tickets;

/// <summary>
/// Reads the Roadmap (M.ORCH-T2382) from the ticket database, read-only and with the fewest columns it can: tickets, their
/// <c>part-of</c> links, levels, names, priorities and who has them, plus each repo's active milestone and its checks. Call it
/// off the UI thread. A read while the store's change version hasn't moved (<see cref="TicketDbVersion"/>) gives the last tree.
/// </summary>
public sealed class RoadmapSource
{
    /// <summary>The prefix of a milestone's id: a <c>part-of</c> link to one is milestone membership, not a parent.</summary>
    public const string MilestonePrefix = "MS-";

    private (string? Root, long? Version, DateTime ReadUtc, int ClosedEpicDays, double Credit, RoadmapTree Tree)? last;

    // Closed epic → when it closed, by the status it had when its history was read.
    private readonly Dictionary<string, (string Status, DateTime? At)> closedAt = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The store's change version for a database path; a test's stands in for a server.</summary>
    internal Func<string, long?> VersionOf { get; init; } = TicketDbVersion.Of;

    /// <summary>The Roadmap for the workspace at <paramref name="workspaceRoot"/>; empty when it has no ticket database.</summary>
    public RoadmapTree Read(string? workspaceRoot, int closedEpicDays, double workingCredit, DateTime nowUtc)
    {
        var version = VersionOf(TicketsDb.DefaultPath(workspaceRoot));
        if (last is { } kept && kept.Root == workspaceRoot && kept.ClosedEpicDays == closedEpicDays && kept.Credit == workingCredit
            && TicketDbVersion.Unchanged(kept.Version, version, kept.ReadUtc, DateTime.UtcNow))
            return kept.Tree;
        var readUtc = DateTime.UtcNow;
        using var db = TicketsDb.OpenIfMigrated(workspaceRoot);
        if (db is null) return RoadmapTree.Empty;
        var tickets = db.Tickets();
        var parents = db.AllLinks()
            .Where(l => l.Kind == "part-of" && !l.To.StartsWith(MilestonePrefix, StringComparison.OrdinalIgnoreCase))
            .GroupBy(l => l.From, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)[.. g.Select(l => l.To)], StringComparer.OrdinalIgnoreCase);
        var levels = OpenTicketSource.LevelsOf(db.AllLabels());
        var priorities = db.QueueStates().Where(q => q.Value.Priority != 0)
            .ToDictionary(q => q.Key, q => q.Value.Priority, StringComparer.OrdinalIgnoreCase);
        var cutoff = nowUtc.AddDays(-closedEpicDays);
        var recentEpics = tickets.Where(t => t.Ticket.Status == TicketRules.Closed && levels.TryGetValue(t.Ticket.Id, out var l)
                && l == OpenTicketSource.EpicLabel)
            .Where(t => ClosedAt(t.Ticket, db) is { } at && at >= cutoff)
            .Select(t => t.Ticket.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tree = RoadmapTree.Build(tickets, parents, levels, db.Names(), priorities, db.WorkStates(), recentEpics, Milestones(db),
            workingCredit);
        last = (workspaceRoot, version, readUtc, closedEpicDays, workingCredit, tree);
        return tree;
    }

    private DateTime? ClosedAt(Ticket ticket, TicketsDb db)
    {
        if (closedAt.TryGetValue(ticket.Id, out var known) && known.Status == ticket.Status) return known.At;
        var at = ClosedTicketSource.ClosedAt(db.History(ticket.Id));
        closedAt[ticket.Id] = (ticket.Status, at);
        return at;
    }

    /// <summary>Repo → its first active milestone (in milestone order) and that milestone's checks passed of all; none when the
    /// database has no milestones to read.</summary>
    private static IReadOnlyDictionary<string, (string Id, string Name, long Order, int Passed, int Total)> Milestones(TicketsDb db)
    {
        var map = new Dictionary<string, (string Id, string Name, long Order, int Passed, int Total)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var m in db.Milestones(MilestoneStates.Active).OrderBy(m => m.Order))
            {
                var repos = m.Repos.Where(r => !map.ContainsKey(r)).ToList();
                if (repos.Count == 0) continue;
                var checks = db.MilestoneChecks(m.Id);
                foreach (var r in repos) map[r] = (m.Id, m.Name, m.Order, checks.Count(c => c.Passed), checks.Count);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Data.Common.DbException) { }
        return map;
    }
}
