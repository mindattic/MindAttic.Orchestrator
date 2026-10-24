using MindAttic.Orchestrator.Settings;
using MindAttic.Tickets;
using MindAttic.Tickets.Data;
using MindAttic.Tickets.Log;

namespace MindAttic.Orchestrator.Tickets;

/// <summary>
/// A ticket's approval and priority, kept in one place: the ticket database's queue (<c>ticket_queue</c>), written and read
/// through the MindAttic.Tickets engine (<see cref="TicketsDb"/>, which keeps them on SQL Server when this machine has it
/// configured, <c>data/Tickets.db</c> its copy), exactly where <c>tickets ticket approve</c>, <c>tickets ticket priority</c> and the
/// Tickets Desktop write them (M.ORCH-T761). The orchestrator used to keep a second set in its settings for tickets the database
/// didn't hold (<see cref="OrchestratorSettings.StoryApproved"/>, <see cref="OrchestratorSettings.StoryPriorities"/>, M.ORCH-T441),
/// so approving in one app didn't show in the other; those are only moved into the database now (<see cref="MoveIntoDatabase"/>),
/// never written or read. A ticket the database doesn't hold can't be approved or prioritized until its repo is brought into it
/// (<c>tickets db migrate</c>). The Orchestrator writes none of it itself any more (M.ORCH-T762): approving, prioritizing,
/// assigning, reserving and blocking are MindAttic.Tickets' (its CLI and Tickets Desktop); this only moves the settings it kept
/// before into the database (<see cref="MoveIntoDatabase"/>, <see cref="MoveWorkIntoDatabase"/>), and the scheduler reads it there.
/// </summary>
public static class TicketQueue
{
    /// <summary>A ticket found by id: in the database, or in the story file of a repo the database doesn't hold.</summary>
    public sealed record Found(string Repo, Ticket Ticket, bool InDatabase);

    /// <summary><paramref name="id"/>'s ticket: the database's when it holds it, else from the <c>docs/USER_STORIES.md</c> of a
    /// workspace repo the database doesn't hold (every repo when there's no database); null when neither has it.</summary>
    public static Found? Find(TicketsDb? db, string? workspaceRoot, string id)
    {
        if (db?.Ticket(id) is { } held) return new(held.Repo, held.Ticket, true);
        if (string.IsNullOrEmpty(workspaceRoot)) return null;
        var inDb = db?.Repos().Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        foreach (var repo in new Workspace(workspaceRoot).Repos())
        {
            if (inDb.Contains(repo.Name) || !File.Exists(repo.StoriesPath)) continue;
            try
            {
                if (StoryFile.Load(repo.StoriesPath).Tickets.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) is { } t)
                    return new(repo.Name, t, false);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException) { }
        }
        return null;
    }

    /// <summary>
    /// The one-time move of the ticket state the orchestrator kept in its settings before MindAttic.Tickets owned it (M.ORCH-T762):
    /// assignments (<see cref="OrchestratorSettings.Assignees"/>), reservations (<see cref="OrchestratorSettings.Reservations"/>) and
    /// blocks set by hand (<see cref="OrchestratorSettings.BlockedBy"/>), into the ticket database through the engine, without
    /// overriding what it already says (an assignee or reservation of its own wins). An entry leaves the settings only once the
    /// database holds its ticket and carries it (or one of its own); a block the database refuses (a prerequisite it doesn't hold,
    /// or a cycle) is dropped, as the scheduler no longer reads it. The number of entries moved; 0 with nothing to move.
    /// </summary>
    public static int MoveWorkIntoDatabase(JsonStore store, string? workspaceRoot)
    {
        var settings = store.LoadSettings();
        if (settings.Assignees.Count == 0 && settings.Reservations.Count == 0 && settings.BlockedBy.Count == 0) return 0;
        using var db = TicketsDb.OpenIfMigrated(workspaceRoot);
        if (db is null) return 0;
        bool Held(string id) => db.Ticket(id) is not null;
        var assigned = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (id, email) in settings.Assignees)
        {
            if (!Held(id)) continue;
            if (db.Work(id).Assignee is null && TicketsDb.NormalizeEmail(email) is { } e) db.Assign(id, e);
            assigned.Add(id);
        }
        var reserved = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (id, tokens) in settings.Reservations)
        {
            if (!Held(id)) continue;
            if (db.Work(id).Reserved == 0 && tokens is > 0 and <= TicketsDb.MaxReserved) db.Reserve(id, tokens);
            reserved.Add(id);
        }
        var blocked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (id, by) in settings.BlockedBy)
        {
            if (!Held(id)) continue;
            try { db.Block(id, by.Where(Held)); }
            catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException) { }
            blocked.Add(id);
        }
        if (assigned.Count + reserved.Count + blocked.Count == 0) return 0;
        store.Update<OrchestratorSettings>(JsonStore.SettingsName, s =>
        {
            foreach (var k in assigned) s.Assignees.Remove(k);
            foreach (var k in reserved) s.Reservations.Remove(k);
            foreach (var k in blocked) s.BlockedBy.Remove(k);
        });
        return assigned.Count + reserved.Count + blocked.Count;
    }

    /// <summary>
    /// The one-time move of the approvals and priorities the orchestrator's settings kept for tickets the database didn't hold
    /// (M.ORCH-T441) into the database's queue, through the engine (M.ORCH-T761), without overriding what the database already
    /// says. An entry leaves the settings only once the database is read back and verified to carry it (approved, or a decision
    /// of its own: rejected or closed; the priority set, or one of its own), so none is lost; ids it doesn't hold yet stay until
    /// their repo is brought into it. The number of entries moved; 0 when there was nothing to move or no migrated database.
    /// </summary>
    public static int MoveIntoDatabase(JsonStore store, string? workspaceRoot)
    {
        var settings = store.LoadSettings();
        if (settings.StoryApproved.Count == 0 && settings.StoryPriorities.Count == 0) return 0;
        using var db = TicketsDb.OpenIfMigrated(workspaceRoot);
        if (db is null) return 0;
        bool Held(string id) => db.Ticket(id) is not null;
        var approved = settings.StoryApproved.Where(Held).ToList();
        var prioritized = settings.StoryPriorities.Where(kv => Held(kv.Key)).ToList();
        if (approved.Count == 0 && prioritized.Count == 0) return 0;
        foreach (var id in approved)
            if (db.Ticket(id)?.Ticket is { Status: TicketRules.Open } t && db.QueueState(t.Id).Approval == TicketsDb.Proposed) db.Approve(t.Id);
        foreach (var (id, priority) in prioritized)
            if (priority != 0 && db.Ticket(id)?.Ticket is { } t && db.QueueState(t.Id).Priority == 0) db.SetPriority(t.Id, priority);
        // Verified: read back from the database before anything leaves the settings.
        var approvedHeld = approved.Where(id => db.Ticket(id)?.Ticket is { } t &&
            (t.Status != TicketRules.Open || db.QueueState(t.Id).Approval != TicketsDb.Proposed)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var prioritizedHeld = prioritized.Where(kv => kv.Value == 0 || db.Ticket(kv.Key)?.Ticket is { } t && db.QueueState(t.Id).Priority != 0)
            .Select(kv => kv.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // Which ids the database holds is asked first: each is a query that can wait on its lock, and a settings change saved
        // (the Settings panel, say) between this load and its save would be lost.
        store.Update<OrchestratorSettings>(JsonStore.SettingsName, s =>
        {
            s.StoryApproved.RemoveAll(approvedHeld.Contains);
            foreach (var k in s.StoryPriorities.Keys.Where(prioritizedHeld.Contains).ToList()) s.StoryPriorities.Remove(k);
        });
        return approvedHeld.Count + prioritizedHeld.Count;
    }
}
