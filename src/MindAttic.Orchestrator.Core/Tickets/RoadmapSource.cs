using MindAttic.Tickets.Data;
using MindAttic.Tickets.Log;

namespace MindAttic.Orchestrator.Tickets;

/// <summary>
/// Reads the Roadmap (M.ORCH-T2382) from the ticket database, read-only and with the fewest columns it can: tickets, their
/// <c>part-of</c> links, levels, names, priorities, descriptions (M.ORCH-T2394) and who has them, plus each repo's active milestone and its checks. Call it
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

    /// <summary>The Roadmap for the workspace at <paramref name="workspaceRoot"/>; empty when it has no ticket database. Every
    /// ticket's row (name, level, status, priority, description, who has it) comes from the process's <see cref="TicketCache"/>,
    /// which reads only what changed; the links, active milestones and their checks are one query each, and closed epics'
    /// close times one status-history read only while one isn't known yet (M.ORCH-T2400).</summary>
    public RoadmapTree Read(string? workspaceRoot, int closedEpicDays, double workingCredit, DateTime nowUtc)
    {
        var version = VersionOf(TicketsDb.DefaultPath(workspaceRoot));
        if (last is { } kept && kept.Root == workspaceRoot && kept.ClosedEpicDays == closedEpicDays && kept.Credit == workingCredit
            && TicketDbVersion.Unchanged(kept.Version, version, kept.ReadUtc, DateTime.UtcNow))
            return kept.Tree;
        var readUtc = DateTime.UtcNow;
        using var db = TicketsDb.OpenIfMigrated(workspaceRoot);
        if (db is null) return RoadmapTree.Empty;
        var cache = TicketCache.For(workspaceRoot);
        cache.Refresh(db);
        var rows = cache.Snapshot.Rows.Values.ToList();
        var tickets = rows.Select(r => (r.Repo, new Ticket(r.Id, "", "", r.Status, r.Summary, ""))).ToList();
        var parents = db.AllLinks()
            .Where(l => l.Kind == "part-of" && !l.To.StartsWith(MilestonePrefix, StringComparison.OrdinalIgnoreCase))
            .GroupBy(l => l.From, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)[.. g.Select(l => l.To)], StringComparer.OrdinalIgnoreCase);
        var levels = rows.Where(r => !string.IsNullOrWhiteSpace(r.Level))
            .ToDictionary(r => r.Id, r => r.Level!.Trim().ToLowerInvariant(), StringComparer.OrdinalIgnoreCase);
        var names = rows.Where(r => !string.IsNullOrWhiteSpace(r.Name)).ToDictionary(r => r.Id, r => r.Name!, StringComparer.OrdinalIgnoreCase);
        var priorities = rows.Where(r => r.Priority != 0).ToDictionary(r => r.Id, r => r.Priority, StringComparer.OrdinalIgnoreCase);
        var work = rows.Where(r => r.Assignee is not null || r.Login is not null)
            .ToDictionary(r => r.Id, r => new TicketWork(r.Assignee, 0, r.Login, null, null), StringComparer.OrdinalIgnoreCase);
        var descriptions = rows.Where(r => r.Description is not null).ToDictionary(r => r.Id, r => r.Description, StringComparer.OrdinalIgnoreCase);
        var cutoff = nowUtc.AddDays(-closedEpicDays);
        var closedEpics = rows.Where(r => r.Status == TicketRules.Closed && levels.TryGetValue(r.Id, out var l) && l == OpenTicketSource.EpicLabel).ToList();
        ReadClosedAt(closedEpics, db);
        var recentEpics = closedEpics.Where(r => closedAt.TryGetValue(r.Id, out var c) && c.At is { } at && at >= cutoff)
            .Select(r => r.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tree = RoadmapTree.Build(tickets, parents, levels, names, priorities, work, recentEpics, Milestones(db),
            workingCredit, id => descriptions.GetValueOrDefault(id));
        last = (workspaceRoot, version, readUtc, closedEpicDays, workingCredit, tree);
        return tree;
    }

    /// <summary>Each closed epic's close time not yet known at its status, from one read of every ticket's status history.</summary>
    private void ReadClosedAt(IReadOnlyList<TicketRow> closedEpics, TicketsDb db)
    {
        var unknown = closedEpics.Where(r => !closedAt.TryGetValue(r.Id, out var known) || known.Status != r.Status).ToList();
        if (unknown.Count == 0) return;
        var history = db.StatusHistory().ToLookup(e => e.TicketId, StringComparer.OrdinalIgnoreCase);
        foreach (var r in unknown) closedAt[r.Id] = (r.Status, ClosedTicketSource.ClosedAt([.. history[r.Id]]));
    }

    /// <summary>Repo → its first active milestone (in milestone order) and that milestone's checks passed of all; none when the
    /// database has no milestones to read.</summary>
    private static IReadOnlyDictionary<string, (string Id, string Name, long Order, int Passed, int Total)> Milestones(TicketsDb db)
    {
        var map = new Dictionary<string, (string Id, string Name, long Order, int Passed, int Total)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var allChecks = db.MilestoneChecksAll();
            foreach (var m in db.Milestones(MilestoneStates.Active).OrderBy(m => m.Order))
            {
                var repos = m.Repos.Where(r => !map.ContainsKey(r)).ToList();
                if (repos.Count == 0) continue;
                IReadOnlyList<MilestoneCheck> checks = allChecks.TryGetValue(m.Id, out var found) ? found : [];
                foreach (var r in repos) map[r] = (m.Id, m.Name, m.Order, checks.Count(c => c.Passed), checks.Count);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Data.Common.DbException) { }
        return map;
    }
}
