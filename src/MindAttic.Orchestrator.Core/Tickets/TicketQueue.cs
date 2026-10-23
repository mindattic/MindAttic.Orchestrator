using MindAttic.Orchestrator.Settings;
using MindAttic.Tickets;
using MindAttic.Tickets.Data;
using MindAttic.Tickets.Log;

namespace MindAttic.Orchestrator.Tickets;

/// <summary>
/// Where a ticket's approval and priority are written (M.ORCH-T441): the ticket database's queue (<c>ticket_queue</c>) when
/// the database holds the ticket, as <c>tickets ticket approve</c> and the Tickets Desktop write it (M.ORCH-T384); otherwise
/// settings.json's <see cref="OrchestratorSettings.StoryApproved"/> and <see cref="OrchestratorSettings.StoryPriorities"/>, since
/// <c>ticket_queue</c> only takes tickets in the database and a repo still on its own <c>docs/USER_STORIES.md</c> would have
/// none of its features, chores or docs approved, ever. The scheduler reads both.
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

    /// <summary>Approves <paramref name="id"/> (or takes the approval back): in the database's queue when it holds the ticket,
    /// else in settings.json.</summary>
    public static void SetApproval(JsonStore store, TicketsDb? db, string id, bool approved)
    {
        if (db?.Ticket(id) is not null)
        {
            if (approved) db.Approve(id); else db.Unapprove(id);
            return;
        }
        store.Update<OrchestratorSettings>(JsonStore.SettingsName, s =>
        {
            s.StoryApproved.RemoveAll(x => x.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (approved) s.StoryApproved.Add(id);
        });
    }

    /// <summary><paramref name="id"/>'s priority moved by <paramref name="by"/>, where its approval lives; the new priority.</summary>
    public static long Bump(JsonStore store, TicketsDb? db, string id, long by)
    {
        if (db?.Ticket(id) is not null)
        {
            var next = db.QueueState(id).Priority + by;
            db.SetPriority(id, next);
            return next;
        }
        long moved = 0;
        store.Update<OrchestratorSettings>(JsonStore.SettingsName, s =>
        {
            var same = s.StoryPriorities.Where(kv => kv.Key.Equals(id, StringComparison.OrdinalIgnoreCase)).ToList();
            moved = same.Sum(kv => kv.Value) + by;
            foreach (var kv in same) s.StoryPriorities.Remove(kv.Key);
            if (moved != 0) s.StoryPriorities[id] = moved;
        });
        return moved;
    }

    /// <summary>
    /// Moves settings.json's approvals and priorities of tickets the database now holds (their repo was brought into it) into
    /// its queue, without overriding what the database already says, and drops them from settings.json, so that
    /// <c>tickets ticket approve --undo</c> or the Tickets Desktop can take them back. Ids it still doesn't hold stay. The
    /// number of entries moved; 0 when there was nothing to move or no migrated database.
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
        // Which ids the database holds is asked first: each is a query that can wait on its lock, and a settings change saved
        // (the Settings panel, orchestrator reserve) between this load and its save would be lost.
        var moved = approved.Concat(prioritized.Select(kv => kv.Key)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        store.Update<OrchestratorSettings>(JsonStore.SettingsName, s =>
        {
            s.StoryApproved.RemoveAll(moved.Contains);
            foreach (var k in s.StoryPriorities.Keys.Where(moved.Contains).ToList()) s.StoryPriorities.Remove(k);
        });
        return approved.Count + prioritized.Count;
    }
}
