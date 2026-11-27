using MindAttic.Tickets.Data;

namespace MindAttic.Orchestrator.Tickets;

/// <summary>
/// Every ticket's friendly name (<c>tickets ticket names</c>, M.TIX-T313), from the process's <see cref="TicketCache"/> and kept
/// while the cache's snapshot holds (M.ORCH-T2400), so a sidebar card's "ID · name" (M.ORCH-T1450, <see cref="TicketsDb.Caption"/>)
/// costs one version scalar on a refresh with nothing new. A ticket with no name yet, or a workspace with no migrated database,
/// gives nothing: the caller falls back to the ticket's summary (<see cref="TicketsDb.Label"/>), never a blank card. Not
/// thread-safe: one read at a time, same as <see cref="ClosedTicketSource"/>.
/// </summary>
public sealed class TicketNameSource
{
    private (TicketCacheSnapshot Snapshot, IReadOnlyDictionary<string, string> Names)? cache;

    private static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every named ticket in <paramref name="workspaceRoot"/>'s database, by id (any case); empty with no database,
    /// or while a read catches it mid-write (the last good read stands; read again next time).</summary>
    public IReadOnlyDictionary<string, string> Names(string? workspaceRoot)
    {
        if (!File.Exists(TicketsDb.DefaultPath(workspaceRoot))) return Empty;
        var tickets = TicketCache.For(workspaceRoot);
        try { tickets.Refresh(); }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Data.Common.DbException)
        {
            return cache?.Names ?? Empty;
        }
        var snapshot = tickets.Snapshot;
        if (snapshot.Version == 0) return Empty;
        if (cache is { } hit && ReferenceEquals(hit.Snapshot, snapshot)) return hit.Names;
        var names = snapshot.Rows.Values.Where(r => !string.IsNullOrWhiteSpace(r.Name))
            .ToDictionary(r => r.Id, r => r.Name!, StringComparer.OrdinalIgnoreCase);
        cache = (snapshot, names);
        return names;
    }
}
