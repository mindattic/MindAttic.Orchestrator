using MindAttic.Orchestrator.Settings;
using MindAttic.Workflow.Data;
using MindAttic.Workflow.Tickets;

namespace MindAttic.Orchestrator.Tickets;

/// <summary>
/// Moves the approvals and priorities settings.json kept into the Workflow database's queue (<c>ticket_queue</c>), once, and
/// drops them from settings.json (M.ORCH-T384). Approval used to live in two stores that never agreed: the scheduler read
/// settings.json while <c>workflow ticket approve</c> and the Workflow Desktop wrote the database, so a ticket approved there
/// waited for approval forever. The database is the one store now, and records that the move was made (<see cref="DoneKey"/>).
/// </summary>
public static class QueueImport
{
    /// <summary>What one import did: approvals and priorities written to the database.</summary>
    public sealed record Result(int Approved, int Prioritized);

    /// <summary>The <c>meta</c> key recording, in the database itself, that the move was made (M.ORCH-T389).</summary>
    public const string DoneKey = "orchestrator.settings_queue_imported";

    /// <summary>Imports <paramref name="store"/>'s legacy approvals and priorities into the database under
    /// <paramref name="workspaceRoot"/>, records in the database that it did, and clears them from settings.json; null when
    /// there was nothing to import, no migrated database to take it (the settings are left as they are then), or the move
    /// was already made. Once it was, the fields are only dropped, never imported again: a process still running an older
    /// build saves them back into settings.json, and importing them again would re-approve a ticket un-approved in the
    /// database since (M.ORCH-T389).</summary>
    public static Result? Run(JsonStore store, string? workspaceRoot)
    {
        var settings = store.LoadSettings();
        if (settings.Approved is null && settings.Priorities is null) return null;
        using var db = WorkflowDb.OpenIfMigrated(workspaceRoot);
        if (db is null) return null;
        Result? result = null;
        if (!IsDone(db.Path))
        {
            result = Import(db, settings.Approved ?? [], settings.Priorities ?? []);
            MarkDone(db.Path);
        }
        // Only once the database holds them; a save that fails leaves them to be dropped next time.
        var current = store.LoadSettingsToChange();
        store.SaveSettings(current with { Approved = null, Priorities = null });
        return result;
    }

    /// <summary>Whether the database at <paramref name="dbPath"/> records the move as made.</summary>
    public static bool IsDone(string dbPath)
    {
        using var c = Connect(dbPath);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM meta WHERE key = $k";
        cmd.Parameters.AddWithValue("$k", DoneKey);
        return cmd.ExecuteScalar() is long n && n > 0;
    }

    private static void MarkDone(string dbPath)
    {
        using var c = Connect(dbPath);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO meta (key, value) VALUES ($k, $v)";
        cmd.Parameters.AddWithValue("$k", DoneKey);
        cmd.Parameters.AddWithValue("$v", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture));
        cmd.ExecuteNonQuery();
    }

    // The Workflow library has no API for its meta table; a connection of our own, set up as it sets up its own (no pooling,
    // so the file is closed on Dispose, and the same busy timeout).
    private static Microsoft.Data.Sqlite.SqliteConnection Connect(string dbPath)
    {
        var c = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Pooling=False");
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "PRAGMA busy_timeout = 15000;";
        cmd.ExecuteNonQuery();
        return c;
    }

    /// <summary>
    /// Writes <paramref name="approved"/> and <paramref name="priorities"/> into <paramref name="db"/>'s queue without overriding
    /// what the database already says: only a proposed open ticket is approved (a rejected one stays rejected, a closed one
    /// needs none), and only a ticket still at priority 0 takes the settings' priority. Ids the database doesn't know are dropped.
    /// </summary>
    public static Result Import(WorkflowDb db, IEnumerable<string> approved, IReadOnlyDictionary<string, int> priorities)
    {
        int approvals = 0, prioritized = 0;
        foreach (var id in approved.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (db.Ticket(id)?.Ticket is not { Status: TicketRules.Open } t || db.QueueState(t.Id).Approval != WorkflowDb.Proposed) continue;
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
