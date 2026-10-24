using MindAttic.Orchestrator.Settings;
using MindAttic.Tickets.Data;
using MindAttic.Tickets.Log;

namespace MindAttic.Orchestrator.Tickets;

/// <summary>
/// Moves the approvals and priorities settings.json kept into the ticket database's queue (<c>ticket_queue</c>), once, and
/// drops them from settings.json (M.ORCH-T384). Approval used to live in two stores that never agreed: the scheduler read
/// settings.json while <c>tickets ticket approve</c> and the Tickets Desktop wrote the database, so a ticket approved there
/// waited for approval forever. The database is the one store now, written through the MindAttic.Tickets engine (which keeps it
/// on SQL Server when configured, <c>data/Tickets.db</c> its copy), and the orchestrator's own state records that the move was
/// made (<see cref="DoneState"/>; M.ORCH-T761: the marker used to be written into <c>data/Tickets.db</c>'s <c>meta</c> table
/// directly, around the engine, and is now only read from there).
/// </summary>
public static class QueueImport
{
    /// <summary>What one import did: approvals and priorities written to the database.</summary>
    public sealed record Result(int Approved, int Prioritized);

    /// <summary>The <c>meta</c> key that recorded, in the ticket database, that the move was made (M.ORCH-T389): only read now,
    /// so a move recorded there before M.ORCH-T761 still counts.</summary>
    public const string DoneKey = "orchestrator.settings_queue_imported";

    /// <summary>The orchestrator state value recording that the move was made (M.ORCH-T761).</summary>
    public const string DoneState = "settings_queue_imported";

    /// <summary>What <see cref="DoneState"/> keeps: when the move was made.</summary>
    public sealed record Done(DateTime AtUtc);

    /// <summary>Imports <paramref name="store"/>'s legacy approvals and priorities into the database under
    /// <paramref name="workspaceRoot"/>, records in the orchestrator's state that it did, and clears them from settings.json;
    /// null when there was nothing to import, no migrated database to take it (the settings are left as they are then), or the move
    /// was already made. Once it was, the fields are only dropped, never imported again: a process still running an older
    /// build saves them back into settings.json, and importing them again would re-approve a ticket un-approved in the
    /// database since (M.ORCH-T389).</summary>
    public static Result? Run(JsonStore store, string? workspaceRoot)
    {
        var settings = store.LoadSettings();
        if (settings.Approved is null && settings.Priorities is null) return null;
        using var db = TicketsDb.OpenIfMigrated(workspaceRoot);
        if (db is null) return null;
        Result? result = null;
        if (!IsDone(store, db.Path))
        {
            result = Import(db, settings.Approved ?? [], settings.Priorities ?? []);
            store.Save(DoneState, new Done(DateTime.UtcNow));
        }
        // Only once the database holds them; a save that fails leaves them to be dropped next time.
        store.Update<OrchestratorSettings>(JsonStore.SettingsName, s => { s.Approved = null; s.Priorities = null; });
        return result;
    }

    /// <summary>Whether the move was made: recorded in the orchestrator's state, or, before M.ORCH-T761, in the <c>meta</c> table of
    /// the ticket database's file at <paramref name="dbPath"/>.</summary>
    public static bool IsDone(JsonStore store, string dbPath) => store.Has(DoneState) || LegacyDone(dbPath);

    // The MindAttic.Tickets library has no API for its meta table, so the old marker is read from the file, read-only: nothing is
    // ever written to it here (the engine owns that file, and on SQL Server it's only a copy).
    private static bool LegacyDone(string dbPath)
    {
        if (!File.Exists(dbPath)) return false;
        try
        {
            using var c = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Mode=ReadOnly;Pooling=False");
            c.Open();
            using (var wait = c.CreateCommand())
            {
                wait.CommandText = "PRAGMA busy_timeout = 15000;";
                wait.ExecuteNonQuery();
            }
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM meta WHERE key = $k";
            cmd.Parameters.AddWithValue("$k", DoneKey);
            return cmd.ExecuteScalar() is long n && n > 0;
        }
        catch (Microsoft.Data.Sqlite.SqliteException) { return false; }
    }

    /// <summary>
    /// Writes <paramref name="approved"/> and <paramref name="priorities"/> into <paramref name="db"/>'s queue without overriding
    /// what the database already says: only a proposed open ticket is approved (a rejected one stays rejected, a closed one
    /// needs none), and only a ticket still at priority 0 takes the settings' priority. Ids the database doesn't know are dropped.
    /// </summary>
    public static Result Import(TicketsDb db, IEnumerable<string> approved, IReadOnlyDictionary<string, int> priorities)
    {
        int approvals = 0, prioritized = 0;
        foreach (var id in approved.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (db.Ticket(id)?.Ticket is not { Status: TicketRules.Open } t || db.QueueState(t.Id).Approval != TicketsDb.Proposed) continue;
            db.Approve(t.Id);
            approvals++;
        }
        foreach (var (id, priority) in priorities)
        {
            if (priority == 0 || db.Ticket(id)?.Ticket is not { } t || db.QueueState(t.Id).Priority != 0) continue;
            db.SetPriority(t.Id, priority);
            prioritized++;
        }
        return new Result(approvals, prioritized);
    }
}
