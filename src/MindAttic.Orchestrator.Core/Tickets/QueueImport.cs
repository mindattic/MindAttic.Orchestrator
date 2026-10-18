using MindAttic.Orchestrator.Settings;
using MindAttic.Workflow.Data;
using MindAttic.Workflow.Tickets;

namespace MindAttic.Orchestrator.Tickets;

/// <summary>
/// Moves the approvals and priorities settings.json kept into the Workflow database's queue (<c>ticket_queue</c>), once, and
/// drops them from settings.json (M.ORCH-T384). Approval used to live in two stores that never agreed: the scheduler read
/// settings.json while <c>workflow ticket approve</c> and the Workflow Desktop wrote the database, so a ticket approved there
/// waited for approval forever. The database is the one store now.
/// </summary>
public static class QueueImport
{
    /// <summary>What one import did: approvals and priorities written to the database.</summary>
    public sealed record Result(int Approved, int Prioritized);

    /// <summary>Imports <paramref name="store"/>'s legacy approvals and priorities into the database under
    /// <paramref name="workspaceRoot"/> and clears them from settings.json; null when there was nothing to import or no
    /// migrated database to take it (the settings are left as they are then).</summary>
    public static Result? Run(JsonStore store, string? workspaceRoot)
    {
        var settings = store.LoadSettings();
        if (settings.Approved is null && settings.Priorities is null) return null;
        using var db = WorkflowDb.OpenIfMigrated(workspaceRoot);
        if (db is null) return null;
        var result = Import(db, settings.Approved ?? [], settings.Priorities ?? []);
        // Only once the database holds them; a save that fails leaves them to be imported again (each write is idempotent).
        var current = store.LoadSettingsToChange();
        store.SaveSettings(current with { Approved = null, Priorities = null });
        return result;
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
