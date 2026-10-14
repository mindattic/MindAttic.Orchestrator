using MindAttic.Workflow;
using MindAttic.Workflow.Tickets;

namespace MindAttic.Orchestrator.Tickets;

/// <summary>
/// Open tickets per repo, straight from each repo's <c>docs/USER_STORIES.md</c> through
/// MindAttic.Workflow's parser. A file is only re-parsed when it changes on disk.
/// </summary>
public sealed class OpenTicketSource
{
    private readonly Dictionary<string, (DateTime WriteUtc, IReadOnlyList<Ticket> Open)> cache =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<Ticket> OpenFor(Repo repo, ICollection<string> warnings)
    {
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
}
