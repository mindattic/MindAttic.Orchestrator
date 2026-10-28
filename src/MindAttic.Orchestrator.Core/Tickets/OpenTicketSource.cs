using MindAttic.Tickets;
using MindAttic.Tickets.Data;
using MindAttic.Tickets.Log;

namespace MindAttic.Orchestrator.Tickets;

/// <summary>
/// Open tickets per repo, and the prerequisites between tickets, from the ticket database once the workspace
/// is migrated, with every ticket's approval and priority from its queue (re-read only when the database changes on disk); a repo not in it falls back to its
/// <c>docs/USER_STORIES.md</c>, re-parsed only when that file changes.
/// </summary>
public sealed class OpenTicketSource
{
    private readonly Dictionary<string, (DateTime WriteUtc, IReadOnlyList<Ticket> Open)> cache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>How long a database read stands even when its file didn't move: a database on SQL Server (or one whose writes
    /// are still in its WAL) changes no file time here, as <see cref="TicketActivityFeed.ForceEvery"/> (bughunt 2026-10-27).</summary>
    public static readonly TimeSpan DbMaxAge = TicketActivityFeed.ForceEvery;

    /// <summary>The label that makes a ticket an epic (M.ORCH-T1146).</summary>
    public const string EpicLabel = "epic";

    private DateTime dbReadUtc;

    private (string Path, DateTime WriteUtc, ILookup<string, Ticket> Open, HashSet<string> Repos, IReadOnlyDictionary<string, IReadOnlyList<string>> Requires,
        IReadOnlySet<string> Approved, IReadOnlySet<string> Epics, IReadOnlyDictionary<string, long> Priorities, WorkState Work,
        IReadOnlyDictionary<string, IReadOnlyList<string>> PartOf)? fromDb;

    /// <summary>Ticket → the tickets it requires (prerequisite links in the database: <c>tickets ticket block</c> writes them).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Requires => fromDb?.Requires ?? new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>Ticket → the tickets it's <c>part-of</c> (<c>tickets ticket link X part-of Y</c>, M.ORCH-T1210): an epic among
    /// them that isn't <see cref="Approved"/> blocks it exactly as an explicit 'requires' link would, so nothing in an
    /// unapproved epic is picked up just because the epic link itself was never also written as 'requires'.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> PartOf => fromDb?.PartOf ?? new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>Tickets approved for work in the database's queue (<c>tickets ticket approve</c>, the Tickets Desktop): the one
    /// store approval lives in (M.ORCH-T384).</summary>
    public IReadOnlySet<string> Approved => fromDb?.Approved ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Tickets labelled <c>epic</c> in the database (M.ORCH-T1146): never work themselves, and a 'requires' link to
    /// one waits only until it's approved.</summary>
    public IReadOnlySet<string> Epics => fromDb?.Epics ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ticket → its manual priority in the database's queue (higher runs sooner); missing means 0.</summary>
    public IReadOnlyDictionary<string, long> Priorities => fromDb?.Priorities ?? new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ticket → the member it's assigned to, by login email (the database's <c>ticket_work</c>, M.ORCH-T762).</summary>
    public IReadOnlyDictionary<string, string> Assignees => fromDb?.Work.Assignees ?? WorkState.Empty.Assignees;

    /// <summary>Ticket → the tokens reserved for it in the database (M.ORCH-T762).</summary>
    public IReadOnlyDictionary<string, long> Reservations => fromDb?.Work.Reservations ?? WorkState.Empty.Reservations;

    /// <summary>Who each ticket is assigned to and what's reserved for it, as the ticket database's <c>ticket_work</c> holds them.</summary>
    public sealed record WorkState(IReadOnlyDictionary<string, string> Assignees, IReadOnlyDictionary<string, long> Reservations)
    {
        public static readonly WorkState Empty = new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase));

        /// <summary>Read from <paramref name="db"/> through the engine; an assignee that isn't an email is left out.</summary>
        public static WorkState Of(TicketsDb db)
        {
            var work = db.WorkStates();
            return new(
                work.Where(w => TicketsDb.NormalizeEmail(w.Value.Assignee) is not null)
                    .ToDictionary(w => w.Key, w => TicketsDb.NormalizeEmail(w.Value.Assignee)!, StringComparer.OrdinalIgnoreCase),
                work.Where(w => w.Value.Reserved > 0).ToDictionary(w => w.Key, w => w.Value.Reserved, StringComparer.OrdinalIgnoreCase));
        }
    }

    private readonly HashSet<string> unknown = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Repos whose last <see cref="OpenFor"/> could not read the ticket database and had no earlier read to
    /// stand on: their open tickets are unknown, not empty (M.ORCH-T727).</summary>
    public IReadOnlySet<string> Unknown => unknown;

    public IReadOnlyList<Ticket> OpenFor(Repo repo, ICollection<string> warnings)
    {
        unknown.Remove(repo.Name);
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
            // would read every claimed ticket as finished and let allocate reap live assignments. Read again next time.
            // With no good read yet, nothing is cached either: cached under this write time it would stand until the file changed.
            // Nor is it "none": its open tickets are unknown, as the database read says (bughunt 2026-10-27).
            if (hit.Open is null) unknown.Add(repo.Name);
            return hit.Open ?? [];
        }
        cache[path] = (written, open);
        return open;
    }

    private IReadOnlyList<Ticket>? FromDatabase(Repo repo, ICollection<string> warnings)
    {
        var root = Path.GetDirectoryName(repo.Path);
        var path = TicketsDb.DefaultPath(root);
        if (!File.Exists(path)) return null;
        var written = File.GetLastWriteTimeUtc(path);
        if (fromDb is not { } c || c.Path != path || c.WriteUtc != written || DateTime.UtcNow - dbReadUtc >= DbMaxAge)
        {
            try
            {
                using var db = TicketsDb.OpenIfMigrated(root);
                if (db is null) return null;
                var links = db.AllLinks();
                var requires = links.Where(l => l.Kind == "requires")
                    .GroupBy(l => l.From, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)[.. g.Select(l => l.To)], StringComparer.OrdinalIgnoreCase);
                var partOf = links.Where(l => l.Kind == "part-of")
                    .GroupBy(l => l.From, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)[.. g.Select(l => l.To)], StringComparer.OrdinalIgnoreCase);
                var queue = db.QueueStates();
                fromDb = (path, written,
                    db.Tickets().Where(t => t.Ticket.Status == TicketRules.Open).ToLookup(t => t.Repo, t => t.Ticket, StringComparer.OrdinalIgnoreCase),
                    db.Repos().Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase), requires,
                    queue.Where(q => q.Value.Approval == TicketsDb.Approved).Select(q => q.Key).ToHashSet(StringComparer.OrdinalIgnoreCase),
                    db.AllLabels().Where(l => l.Value.Contains(EpicLabel, StringComparer.OrdinalIgnoreCase)).Select(l => l.Key).ToHashSet(StringComparer.OrdinalIgnoreCase),
                    queue.Where(q => q.Value.Priority != 0).ToDictionary(q => q.Key, q => q.Value.Priority, StringComparer.OrdinalIgnoreCase),
                    WorkState.Of(db), partOf);
                dbReadUtc = DateTime.UtcNow;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Data.Common.DbException)
            {
                warnings.Add($"ticket database: {ex.Message}");
                // A read that fails mid-write (the database locked) keeps the last good read rather than fall back to
                // USER_STORIES.md, which can lag it: a ticket missing there reads as finished, and allocate would reap
                // its live assignment. The read is tried again next time, since the cached time still differs.
                // With no good read at all (a daemon just restarted), the open tickets are unknown, not none: say so, so
                // nothing reads the claims in this repo as finished.
                if (fromDb is not { } last || last.Path != path)
                {
                    unknown.Add(repo.Name);
                    return [];
                }
            }
        }
        return fromDb!.Value.Repos.Contains(repo.Name) ? [.. fromDb.Value.Open[repo.Name]] : null;
    }
}
