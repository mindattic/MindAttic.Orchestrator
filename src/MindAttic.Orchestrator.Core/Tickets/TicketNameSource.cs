using MindAttic.Tickets.Data;

namespace MindAttic.Orchestrator.Tickets;

/// <summary>
/// Every ticket's friendly name (<c>tickets ticket names</c>, M.TIX-T313), read through the engine and cached by the ticket
/// database's write time, so a sidebar card's "ID · name" (M.ORCH-T1450, <see cref="TicketsDb.Caption"/>) doesn't open a
/// connection on every refresh. A ticket with no name yet, or a workspace with no migrated database, gives nothing: the
/// caller falls back to the ticket's summary (<see cref="TicketsDb.Label"/>), never a blank card. Not thread-safe: one read
/// at a time, same as <see cref="ClosedTicketSource"/>.
/// </summary>
public sealed class TicketNameSource
{
    private (string Path, DateTime WriteUtc, IReadOnlyDictionary<string, string> Names)? cache;

    private static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every named ticket in <paramref name="workspaceRoot"/>'s database, by id (any case); empty with no database,
    /// or while a read catches it mid-write (the last good read stands; read again next time).</summary>
    public IReadOnlyDictionary<string, string> Names(string? workspaceRoot)
    {
        var path = TicketsDb.DefaultPath(workspaceRoot);
        if (!File.Exists(path)) return Empty;
        DateTime written;
        try { written = File.GetLastWriteTimeUtc(path); }
        catch (IOException) { return cache?.Names ?? Empty; }
        if (cache is { } hit && hit.Path == path && hit.WriteUtc == written) return hit.Names;
        try
        {
            using var db = TicketsDb.OpenIfMigrated(workspaceRoot);
            if (db is null) return Empty;
            var names = db.Names();
            cache = (path, written, names);
            return names;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Data.Common.DbException)
        {
            return cache?.Names ?? Empty;
        }
    }
}
