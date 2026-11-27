using System.Globalization;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Tickets.Data;
using MindAttic.Tickets.Log;

namespace MindAttic.Orchestrator.Tickets;

/// <summary>A closed or won't-do ticket assigned to someone (M.ORCH-T849), with when it closed (null when its history doesn't say).</summary>
public sealed record ClosedTicket(string Repo, Ticket Ticket, string Assignee, DateTime? ClosedUtc)
{
    /// <summary>The ticket's ID (<see cref="Ticket"/>'s).</summary>
    public string Id => Ticket.Id;

    /// <summary>As a queue item, so a person's Queue lists it with the same row as an open one.</summary>
    public QueueItem AsItem() => new(Repo, "", Ticket, 0, null, false) { Assignee = Assignee };
}

/// <summary>
/// The recently closed tickets each person had assigned (M.ORCH-T849), for the Closed and Won't do tabs of their Queue: the
/// ticket database's closed and won't-do tickets with an assignee, closed within <see cref="Window"/>. When a ticket closed is
/// its history's last status change, read once per ticket and kept (a ticket's close time doesn't change unless it's reopened,
/// which its status shows), and at most <see cref="MaxHistoryReads"/> histories are read per call, newest tickets first, so a
/// read on refresh stays cheap however many tickets were ever closed. Not thread-safe: one read at a time.
/// </summary>
public sealed class ClosedTicketSource
{
    /// <summary>How far back the Closed tab reaches.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromDays(7);

    /// <summary>The most ticket histories one read looks up; the rest wait for the next read.</summary>
    public const int MaxHistoryReads = 100;

    private readonly Dictionary<string, (string Status, DateTime? At)> closedAt = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The closed tickets assigned to anyone in the workspace at <paramref name="workspaceRoot"/>, closed after
    /// <paramref name="nowUtc"/> less <see cref="Window"/>, newest first; none when the workspace has no ticket database.</summary>
    public IReadOnlyList<ClosedTicket> Read(string? workspaceRoot, DateTime nowUtc)
    {
        // The database's change version where the last read left it (M.ORCH-T1938): nothing changed, so the candidates and
        // assignees stand and only the window moves; the database is opened only for a history still to read.
        // The candidates come from the process's ticket cache (M.ORCH-T2401): its snapshot unchanged, they stand.
        var cache = TicketCache.For(workspaceRoot);
        var version = VersionOf(TicketsDb.DefaultPath(workspaceRoot));
        if (last is { } kept && kept.Root == workspaceRoot && ReferenceEquals(kept.Snapshot, cache.Snapshot)
            && TicketDbVersion.Unchanged(kept.Version, version, kept.ReadUtc, DateTime.UtcNow))
        {
            TicketsDb? lazy = null;
            try { return Recent(kept.Candidates, kept.Assignees, HistoryOf(() => lazy ??= TicketsDb.OpenIfMigrated(workspaceRoot)), nowUtc); }
            finally { lazy?.Dispose(); }
        }
        last = null;
        var readUtc = DateTime.UtcNow;
        using var db = TicketsDb.OpenIfMigrated(workspaceRoot);
        if (db is null) return [];
        cache.Refresh(db);
        var snapshot = cache.Snapshot;
        var assigned = snapshot.Rows.Values.Where(r => TicketsDb.NormalizeEmail(r.Assignee) is not null).ToList();
        var assignees = assigned.ToDictionary(r => r.Id, r => TicketsDb.NormalizeEmail(r.Assignee)!, StringComparer.OrdinalIgnoreCase);
        // Open tickets aren't filtered out here (M.ORCH-T1163): Recent needs to see a ticket go through Open itself,
        // so a reopen-then-reclose can't reuse the stale close time its cache keyed only on the "closed" status string.
        // The cache has no filing date: a ticket's created event stands in for one with no close event (Recent).
        List<(string Repo, Ticket Ticket)> candidates = [.. assigned
            .Select(r => (r.Repo, new Ticket(r.Id, "", "", r.Status, r.Summary, "")))
            .OrderByDescending(t => t.Item2.Number ?? 0)];
        last = (workspaceRoot, snapshot, version, readUtc, candidates, assignees);
        return assignees.Count == 0 ? [] : Recent(candidates, assignees, HistoryOf(() => db), nowUtc);
    }

    /// <summary>A ticket's status history from one read of every ticket's (M.ORCH-T2400), made the first time one is asked for,
    /// not a history read per ticket.</summary>
    private static Func<string, IReadOnlyList<TicketEvent>> HistoryOf(Func<TicketsDb?> db)
    {
        ILookup<string, TicketEvent>? all = null;
        return id => [.. (all ??= (db()?.StatusHistory() ?? []).ToLookup(e => e.TicketId, StringComparer.OrdinalIgnoreCase))[id]];
    }

    /// <summary>The store's change version for a database path (<see cref="TicketDbVersion.Of"/>); a test's stands in for a server.</summary>
    internal Func<string, long?> VersionOf { get; init; } = TicketDbVersion.Of;

    // The last full read (M.ORCH-T1938): kept while the database's change version stays where it was.
    private (string? Root, TicketCacheSnapshot Snapshot, long? Version, DateTime ReadUtc, IReadOnlyList<(string Repo, Ticket Ticket)> Candidates,
        IReadOnlyDictionary<string, string> Assignees)? last;

    /// <summary>The candidates closed within the window (<see cref="Read"/>, with the database's parts given).</summary>
    internal IReadOnlyList<ClosedTicket> Recent(IReadOnlyList<(string Repo, Ticket Ticket)> candidates, IReadOnlyDictionary<string, string> assignees,
        Func<string, IReadOnlyList<TicketEvent>> history, DateTime nowUtc)
    {
        var cutoff = nowUtc - Window;
        var reads = 0;
        var list = new List<ClosedTicket>();
        foreach (var (repo, ticket) in candidates)
        {
            if (ticket.Status == TicketRules.Open)
            {
                // Drops a stale close time once a ticket is seen open again (M.ORCH-T1163), so a later reclose
                // re-reads its history instead of matching the cache entry from before it was reopened.
                closedAt.Remove(ticket.Id);
                continue;
            }
            // Opened inside the window, it closed inside it too: no history needed to know it belongs.
            var opened = OpenedUtc(ticket);
            if (!closedAt.TryGetValue(ticket.Id, out var known) || known.Status != ticket.Status)
            {
                if (reads >= MaxHistoryReads)
                {
                    if (opened >= cutoff) list.Add(new(repo, ticket, assignees[ticket.Id], null));
                    continue;
                }
                reads++;
                var events = history(ticket.Id);
                known = (ticket.Status, ClosedAt(events) ?? (ticket.Date.Length == 0 ? CreatedAt(events) : null));
                closedAt[ticket.Id] = known;
            }
            var when = known.At ?? opened;
            if (when is { } w && w < cutoff) continue;
            list.Add(new(repo, ticket, assignees[ticket.Id], known.At));
        }
        return [.. list.OrderByDescending(c => c.ClosedUtc ?? OpenedUtc(c.Ticket) ?? DateTime.MinValue)];
    }

    /// <summary>When the ticket's status last changed to closed or won't-do, from its history; null when it never says.</summary>
    internal static DateTime? ClosedAt(IReadOnlyList<TicketEvent> events)
    {
        for (var i = events.Count - 1; i >= 0; i--)
        {
            var e = events[i];
            if (e.Field == "status" && e.New is TicketRules.Closed or TicketRules.WontDo) return ParseUtc(e.At);
        }
        return null;
    }

    /// <summary>When the ticket was filed, from its history's created event; null when it doesn't say.</summary>
    private static DateTime? CreatedAt(IReadOnlyList<TicketEvent> events) =>
        events.FirstOrDefault(e => e.Field == "created") is { } created ? ParseUtc(created.At) : null;

    private static DateTime? OpenedUtc(Ticket ticket) => ParseUtc(ticket.Date);

    private static DateTime? ParseUtc(string? text) =>
        DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var at) ? at : null;
}
