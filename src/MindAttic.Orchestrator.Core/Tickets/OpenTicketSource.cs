using MindAttic.Workflow;
using MindAttic.Workflow.Data;
using MindAttic.Workflow.Tickets;

namespace MindAttic.Orchestrator.Tickets;

/// <summary>
/// Open tickets per repo, and the prerequisites between tickets, from the Workflow database once the workspace
/// is migrated, with every ticket's approval and priority from its queue (re-read only when the database changes on disk); a repo not in it falls back to its
/// <c>docs/USER_STORIES.md</c>, re-parsed only when that file changes.
/// </summary>
public sealed class OpenTicketSource
{
    private readonly Dictionary<string, (DateTime WriteUtc, IReadOnlyList<Ticket> Open)> cache =
        new(StringComparer.OrdinalIgnoreCase);

    private (string Path, DateTime WriteUtc, ILookup<string, Ticket> Open, HashSet<string> Repos, IReadOnlyDictionary<string, IReadOnlyList<string>> Requires,
        IReadOnlySet<string> Approved, IReadOnlyDictionary<string, long> Priorities)? fromDb;

    /// <summary>Ticket → the tickets it requires (prerequisite links in the database).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Requires => fromDb?.Requires ?? new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>Tickets approved for work in the database's queue (<c>workflow ticket approve</c>, <c>orchestrator approve</c>,
    /// the Workflow Desktop): the one store approval lives in (M.ORCH-T384).</summary>
    public IReadOnlySet<string> Approved => fromDb?.Approved ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ticket → its manual priority in the database's queue (higher runs sooner); missing means 0.</summary>
    public IReadOnlyDictionary<string, long> Priorities => fromDb?.Priorities ?? new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<Ticket> OpenFor(Repo repo, ICollection<string> warnings)
    {
        if (FromDatabase(repo, warnings) is { } db) return db;

        var path = repo.StoriesPath;
        if (!File.Exists(path)) return [];
        var written = File.GetLastWriteTimeUtc(path);
        if (cache.TryGetValue(path, out var hit) && hit.WriteUtc == written) return hit.Open;

        IReadOnlyList<Ticket> open;
        try
        {
            open = [.. StoryFile.Load(path).Tickets.Where(t => t.Status == TicketRules.Open)];
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            warnings.Add($"{repo.Name}: {ex.Message}");
            // Caught mid-write (or held by an editor or git): its last good read stands, not "no open tickets", which
            // would read every claimed ticket as finished and let autopilot reap live assignments. Read again next time.
            // With no good read yet, nothing is cached either: cached under this write time it would stand until the file changed.
            return hit.Open ?? [];
        }
        cache[path] = (written, open);
        return open;
    }

    private IReadOnlyList<Ticket>? FromDatabase(Repo repo, ICollection<string> warnings)
    {
        var root = Path.GetDirectoryName(repo.Path);
        var path = WorkflowDb.DefaultPath(root);
        if (!File.Exists(path)) return null;
        var written = File.GetLastWriteTimeUtc(path);
        if (fromDb is not { } c || c.Path != path || c.WriteUtc != written)
        {
            try
            {
                using var db = WorkflowDb.OpenIfMigrated(root);
                if (db is null) return null;
                var requires = db.AllLinks().Where(l => l.Kind == "requires")
                    .GroupBy(l => l.From, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)[.. g.Select(l => l.To)], StringComparer.OrdinalIgnoreCase);
                var queue = db.QueueStates();
                fromDb = (path, written,
                    db.Tickets().Where(t => t.Ticket.Status == TicketRules.Open).ToLookup(t => t.Repo, t => t.Ticket, StringComparer.OrdinalIgnoreCase),
                    db.Repos().Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase), requires,
                    queue.Where(q => q.Value.Approval == WorkflowDb.Approved).Select(q => q.Key).ToHashSet(StringComparer.OrdinalIgnoreCase),
                    queue.Where(q => q.Value.Priority != 0).ToDictionary(q => q.Key, q => q.Value.Priority, StringComparer.OrdinalIgnoreCase));
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
            {
                warnings.Add($"workflow database: {ex.Message}");
                // A read that fails mid-write (the database locked) keeps the last good read rather than fall back to
                // USER_STORIES.md, which can lag it: a ticket missing there reads as finished, and autopilot would reap
                // its live assignment. The read is tried again next time, since the cached time still differs.
                if (fromDb is not { } last || last.Path != path) return null;
            }
        }
        return fromDb!.Value.Repos.Contains(repo.Name) ? [.. fromDb.Value.Open[repo.Name]] : null;
    }
}
