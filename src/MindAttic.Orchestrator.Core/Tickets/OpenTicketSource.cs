using MindAttic.Workflow;
using MindAttic.Workflow.Data;
using MindAttic.Workflow.Tickets;

namespace MindAttic.Orchestrator.Tickets;

/// <summary>
/// Open tickets per repo, and the prerequisites between tickets, from the Workflow database once the workspace
/// is migrated (re-read only when the database changes on disk); a repo not in it falls back to its
/// <c>docs/USER_STORIES.md</c>, re-parsed only when that file changes.
/// </summary>
public sealed class OpenTicketSource
{
    private readonly Dictionary<string, (DateTime WriteUtc, IReadOnlyList<Ticket> Open)> cache =
        new(StringComparer.OrdinalIgnoreCase);

    private (string Path, DateTime WriteUtc, ILookup<string, Ticket> Open, HashSet<string> Repos, IReadOnlyDictionary<string, IReadOnlyList<string>> Requires)? fromDb;

    /// <summary>Ticket → the tickets it requires (prerequisite links in the database).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Requires => fromDb?.Requires ?? new Dictionary<string, IReadOnlyList<string>>();

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
            open = [];
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
                fromDb = (path, written,
                    db.Tickets().Where(t => t.Ticket.Status == TicketRules.Open).ToLookup(t => t.Repo, t => t.Ticket, StringComparer.OrdinalIgnoreCase),
                    db.Repos().Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase), requires);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
            {
                warnings.Add($"workflow database: {ex.Message}");
                return null;
            }
        }
        return fromDb!.Value.Repos.Contains(repo.Name) ? [.. fromDb.Value.Open[repo.Name]] : null;
    }
}
