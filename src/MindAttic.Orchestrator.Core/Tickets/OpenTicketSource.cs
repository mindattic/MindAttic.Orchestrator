using MindAttic.Tickets;
using MindAttic.Tickets.Data;
using MindAttic.Tickets.Log;
using DbRead = (string Path, System.DateTime WriteUtc, System.Linq.ILookup<string, MindAttic.Tickets.Log.Ticket> Open,
    System.Collections.Generic.HashSet<string> Repos, System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<string>> Requires,
    System.Collections.Generic.IReadOnlySet<string> Approved, System.Collections.Generic.IReadOnlySet<string> Epics,
    System.Collections.Generic.IReadOnlyDictionary<string, long> Priorities, MindAttic.Orchestrator.Tickets.OpenTicketSource.WorkState Work,
    System.Collections.Generic.IReadOnlyDictionary<string, System.Collections.Generic.IReadOnlyList<string>> PartOf,
    System.Collections.Generic.IReadOnlySet<string> OwnerWaiting, System.Collections.Generic.IReadOnlyDictionary<string, long> GraphWeights,
    System.Collections.Generic.IReadOnlyDictionary<string, string> Names,
    System.Collections.Generic.IReadOnlyDictionary<string, MindAttic.Orchestrator.TicketMilestone> Milestones,
    System.Collections.Generic.IReadOnlyList<MindAttic.Orchestrator.Tickets.MilestoneScope> MilestoneScopes,
    System.Collections.Generic.IReadOnlySet<string> Containers, System.Collections.Generic.IReadOnlyDictionary<string, string> Levels,
    System.Collections.Generic.IReadOnlyDictionary<string, string> Roles);

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

    /// <summary>The label that parks a ticket as waiting on the owner (M.ORCH-T1425): <c>orchestrator release --waiting-on-owner</c>
    /// adds it; nothing hands the ticket out or counts it as work until the owner removes it.</summary>
    public const string WaitingOnOwnerLabel = "waiting-on-owner";

    /// <summary>The prefix of a ticket's level label: <c>level:epic|feature|story|task</c> (M.ORCH-T1904).</summary>
    public const string LevelLabelPrefix = "level:";

    /// <summary>The prefix of a ticket's role label: <c>role:&lt;name&gt;</c>, the role whose agent works it (M.ORCH-T1746;
    /// <see cref="Scheduling.Roles"/>).</summary>
    public const string RoleLabelPrefix = "role:";

    private DateTime dbReadUtc;

    private DbRead? fromDb;

    // The store's change version as of fromDb, and when fromDb was last read in full (M.ORCH-T1938): a look that finds the
    // version unmoved keeps fromDb (a scalar query, not the whole tickets, links and labels tables) up to TicketDbVersion.MaxAge.
    private long? dbVersion;
    private DateTime dbFullReadUtc;

    /// <summary>The store's change version for a database path (<see cref="TicketDbVersion.Of"/>, M.ORCH-T1938); a test's stands
    /// in for a server.</summary>
    internal Func<string, long?> VersionOf { get; init; } = TicketDbVersion.Of;

    /// <summary>
    /// How long a refresh waits on a ticket-database read before going on with the last good read (M.ORCH-T1937): a read on a
    /// pegged Azure SQL ran 1358s inside the daemon's refresh, and rl-status.json (written later in that same refresh) and the
    /// tick's allocate and alerts waited on it the whole time. The read runs on in the background and lands on a later refresh;
    /// none other starts while it runs, so a slow database isn't handed a pile of the same query.
    /// </summary>
    public TimeSpan ReadWait { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Runs at the start of each background database read; a test's blocks it to stand in for a slow database.</summary>
    internal Action? BeforeRead { get; init; }

    // The database read in flight (M.ORCH-T1937): its database path, its task, and when a refresh stops waiting on it.
    private (string Path, Task<(DbRead? Read, long? Version, bool Kept)> Read, DateTime WaitUntilUtc)? reading;

    /// <summary>Each active and accepting milestone's scope (M.ORCH-T1685): what waits for its owner's approval and how much it
    /// has grown since activation; empty with none, or with a database that predates the scope lock.</summary>
    public IReadOnlyList<MilestoneScope> MilestoneScopes => fromDb?.MilestoneScopes ?? [];

    /// <summary>Open ticket → its graph weight (<see cref="TicketsDb.GraphWeights"/>, M.TIX-T316): dispatch's order after explicit
    /// priority (M.ORCH-T1457); missing means 0.</summary>
    public IReadOnlyDictionary<string, long> GraphWeights => fromDb?.GraphWeights ?? new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Open ticket → the first active milestone it's on (<see cref="TicketsDb.ActiveMilestoneTickets"/>, M.TIX-T335):
    /// what the queue puts ahead of other work that isn't a fix (M.ORCH-T1683); empty with no active milestone.</summary>
    public IReadOnlyDictionary<string, TicketMilestone> Milestones => fromDb?.Milestones ?? NoMilestones;

    private static readonly IReadOnlyDictionary<string, TicketMilestone> NoMilestones = new Dictionary<string, TicketMilestone>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Read from <paramref name="db"/> for the tickets in <paramref name="open"/>. A read that fails (a database from
    /// before milestones, or one locked) gives <paramref name="last"/>, the read before it, or none: never a failed ticket read.</summary>
    internal static IReadOnlyDictionary<string, TicketMilestone> MilestonesOf(TicketsDb db, IReadOnlySet<string> open,
        IReadOnlyDictionary<string, TicketMilestone>? last = null)
    {
        try
        {
            var on = db.ActiveMilestoneTickets();
            if (on.Count == 0) return NoMilestones;
            var rank = db.Milestones(MilestoneStates.Active).Select((m, i) => (m.Id, Rank: i))
                .ToDictionary(m => m.Id, m => m.Rank, StringComparer.OrdinalIgnoreCase);
            return on.Where(t => open.Contains(t.Key))
                .Select(t => (t.Key, First: t.Value.Where(rank.ContainsKey).OrderBy(id => rank[id]).FirstOrDefault()))
                .Where(t => t.First is not null)
                .ToDictionary(t => t.Key, t => new TicketMilestone(t.First!, rank[t.First!]), StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Data.Common.DbException)
        {
            return last ?? NoMilestones;
        }
    }

    /// <summary>Ticket → its friendly name (<see cref="TicketsDb.Names"/>, M.TIX-T313).</summary>
    public IReadOnlyDictionary<string, string> Names => fromDb?.Names ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Tickets labelled <see cref="WaitingOnOwnerLabel"/>: held back from work until the owner acts (M.ORCH-T1425).</summary>
    public IReadOnlySet<string> OwnerWaiting => fromDb?.OwnerWaiting ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

    /// <summary>Open tickets that some open ticket is <c>part-of</c> (M.ORCH-T1904): containers, never work themselves.</summary>
    public IReadOnlySet<string> Containers => fromDb?.Containers ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ticket → its level from its <c>level:</c> label (<see cref="LevelLabelPrefix"/>, M.ORCH-T1904).</summary>
    public IReadOnlyDictionary<string, string> Levels => fromDb?.Levels ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ticket → its level from <paramref name="labels"/> (the database's labels, open or closed tickets alike; M.ORCH-T1908).</summary>
    public static Dictionary<string, string> LevelsOf(IReadOnlyDictionary<string, IReadOnlyList<string>> labels) =>
        PrefixedOf(labels, LevelLabelPrefix);

    /// <summary>Ticket → its role from its <c>role:</c> label (<see cref="RoleLabelPrefix"/>, M.ORCH-T1746); missing means none named.</summary>
    public IReadOnlyDictionary<string, string> Roles => fromDb?.Roles ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ticket → what follows <paramref name="prefix"/> in its first label that starts with it, trimmed and lower-cased.</summary>
    public static Dictionary<string, string> PrefixedOf(IReadOnlyDictionary<string, IReadOnlyList<string>> labels, string prefix) =>
        labels.Select(l => (l.Key, Label: l.Value.FirstOrDefault(v => v.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))))
            .Where(l => l.Label is not null && l.Label.Length > prefix.Length)
            .ToDictionary(l => l.Key, l => l.Label![prefix.Length..].Trim().ToLowerInvariant(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Ticket → its manual priority in the database's queue (higher runs sooner); missing means 0.</summary>
    public IReadOnlyDictionary<string, long> Priorities => fromDb?.Priorities ?? new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ticket → the member it's assigned to, by login email (the database's <c>ticket_work</c>, M.ORCH-T762).</summary>
    public IReadOnlyDictionary<string, string> Assignees => fromDb?.Work.Assignees ?? WorkState.Empty.Assignees;

    /// <summary>Ticket → the tokens reserved for it in the database (M.ORCH-T762).</summary>
    public IReadOnlyDictionary<string, long> Reservations => fromDb?.Work.Reservations ?? WorkState.Empty.Reservations;

    /// <summary>Who each ticket is assigned to and what's reserved for it, as the ticket database's <c>ticket_work</c> holds them.</summary>
    public sealed record WorkState(IReadOnlyDictionary<string, string> Assignees, IReadOnlyDictionary<string, long> Reservations)
    {
        /// <summary>The empty.</summary>
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

    /// <summary>Opens the for.</summary>
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
            // Read off the caller's thread, waited on for ReadWait at most per read (M.ORCH-T1937): past it the last good read
            // stands, as it does for a read that fails, and the read lands on a later refresh.
            var prior = fromDb is { } p && p.Path == path ? p : (DbRead?)null;
            if (reading is not { } r || r.Path != path)
            {
                var before = BeforeRead;
                var versionOf = VersionOf;
                var (priorVersion, priorFullUtc) = (dbVersion, dbFullReadUtc);
                reading = r = (path, Task.Run(() =>
                {
                    before?.Invoke();
                    // The change version first (M.ORCH-T1938), read before the rows it stands for: unmoved, the last read stands.
                    var version = versionOf(path);
                    if (prior is { } kept && TicketDbVersion.Unchanged(priorVersion, version, priorFullUtc, DateTime.UtcNow))
                    {
                        kept.WriteUtc = written;
                        return ((DbRead?)kept, version, true);
                    }
                    return (Read(root, path, written, prior), version, false);
                }), DateTime.UtcNow + ReadWait);
            }
            var left = r.WaitUntilUtc - DateTime.UtcNow;
            try { r.Read.Wait(left > TimeSpan.Zero ? left : TimeSpan.Zero); }
            catch (AggregateException) { } // thrown again below, by GetResult, as the read's own exception
            if (!r.Read.IsCompleted)
            {
                var slow = prior is null
                    ? $"ticket database: a read is still running after {ReadWait.TotalSeconds:0}s, with no earlier read to stand on (M.ORCH-T1937)"
                    : $"ticket database: a read is still running after {ReadWait.TotalSeconds:0}s; using the read from {dbReadUtc.ToLocalTime():T} until it lands (M.ORCH-T1937)";
                if (!warnings.Contains(slow)) warnings.Add(slow);
                if (prior is null)
                {
                    unknown.Add(repo.Name); // unknown, not none, as for a first read that fails (M.ORCH-T1538)
                    return null;
                }
                return fromDb!.Value.Repos.Contains(repo.Name) ? [.. fromDb.Value.Open[repo.Name]] : null;
            }
            reading = null;
            try
            {
                var (got, version, kept) = r.Read.GetAwaiter().GetResult();
                if (got is not { } read) return null;
                fromDb = read;
                dbReadUtc = DateTime.UtcNow;
                dbVersion = version;
                if (!kept) dbFullReadUtc = dbReadUtc;
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
                    // Bughunt, M.ORCH-T1538: this returned [] (empty list), which OpenFor's `is { } db` null-check treats as
                    // a legitimate, zero-open-tickets answer - the exact "unknown, not none" it was just marked as above.
                    // null instead falls through to the USER_STORIES.md fallback, the same path a repo absent from the
                    // database's own repo list already takes (the ternary this method ends with).
                    return null;
                }
            }
        }
        return fromDb!.Value.Repos.Contains(repo.Name) ? [.. fromDb.Value.Open[repo.Name]] : null;
    }

    /// <summary>One read of the ticket database at <paramref name="path"/> (written at <paramref name="written"/>), on a background
    /// thread (M.ORCH-T1937): null when the workspace isn't migrated. <paramref name="prior"/> is the last good read of it, whose
    /// milestones stand when theirs can't be read.</summary>
    private static DbRead? Read(string? root, string path, DateTime written, DbRead? prior)
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
        var tickets = db.Tickets();
        var names = db.Names();
        // One graph from the rows already read (M.ORCH-T1938): GraphWeights() and MilestoneScope.Read each read the whole
        // tickets, links and names tables again for their own TicketsDb.Graph(), three of each per refresh.
        var graph = TicketGraph.Build(tickets, links, names);
        var open = tickets.Where(t => t.Ticket.Status == TicketRules.Open).ToList();
        var openIds = open.Select(t => t.Ticket.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var labels = db.AllLabels();
        // An open ticket some open ticket is part-of is a container (M.ORCH-T1904): its children are the work.
        var containers = partOf.Where(p => openIds.Contains(p.Key)).SelectMany(p => p.Value).Where(openIds.Contains)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return (path, written,
            open.ToLookup(t => t.Repo, t => t.Ticket, StringComparer.OrdinalIgnoreCase),
            db.Repos().Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase), requires,
            queue.Where(q => q.Value.Approval == TicketsDb.Approved).Select(q => q.Key).ToHashSet(StringComparer.OrdinalIgnoreCase),
            labels.Where(l => l.Value.Contains(EpicLabel, StringComparer.OrdinalIgnoreCase)).Select(l => l.Key).ToHashSet(StringComparer.OrdinalIgnoreCase),
            queue.Where(q => q.Value.Priority != 0).ToDictionary(q => q.Key, q => q.Value.Priority, StringComparer.OrdinalIgnoreCase),
            WorkState.Of(db), partOf,
            labels.Where(l => l.Value.Contains(WaitingOnOwnerLabel, StringComparer.OrdinalIgnoreCase)).Select(l => l.Key).ToHashSet(StringComparer.OrdinalIgnoreCase),
            GraphWeight.Of(graph).Where(w => w.Value.Value != 0).ToDictionary(w => w.Key, w => (long)w.Value.Value, StringComparer.OrdinalIgnoreCase),
            names.ToDictionary(n => n.Key, n => n.Value, StringComparer.OrdinalIgnoreCase),
            MilestonesOf(db, openIds, prior?.Milestones),
            MilestoneScope.ReadOr(db, prior?.MilestoneScopes, graph),
            containers, LevelsOf(labels), PrefixedOf(labels, RoleLabelPrefix));
    }
}
