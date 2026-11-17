using System.Data.Common;
using System.Globalization;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Activity;

/// <summary>The kinds of thing the orchestrator creates on disk and records in the <c>resources</c> table (M.ORCH-T1676).</summary>
public static class ResourceKind
{
    /// <summary>A ticket's own scratch dir (<see cref="TicketScratch"/>), handed to its agent as <c>ORCH_SCRATCH</c>.</summary>
    public const string Scratch = "scratch";

    /// <summary>A git worktree (the main-tests, branch build, merge and work-&lt;ticket&gt; ones under the worktree root).</summary>
    public const string Worktree = "worktree";

    /// <summary>A temp file or folder (a review's diff, a token check's copy, ...).</summary>
    public const string TempFile = "temp";
}

/// <summary>
/// One <c>resources</c> row (M.ORCH-T1676): something the orchestrator created on <see cref="Machine"/> at <see cref="Path"/>,
/// owned by <see cref="Owner"/> (<see cref="ResourceLedger.TicketOwner"/>, <see cref="ResourceLedger.SessionOwner"/>,
/// <see cref="ResourceLedger.ProcessOwner"/> or <see cref="ResourceLedger.DaemonOwner"/>), its lease running to
/// <see cref="ExpiresUtc"/> (null: no expiry, kept until released), and <see cref="ReleasedUtc"/> once its owner let go of it.
/// </summary>
public sealed record Resource(string Machine, string Kind, string Path, string Owner, string? Ticket, DateTime CreatedUtc, DateTime? ExpiresUtc, DateTime? ReleasedUtc)
{
    /// <summary>Whether its owner has let go of it.</summary>
    public bool IsReleased => ReleasedUtc is not null;

    /// <summary>Whether its lease has run out by <paramref name="nowUtc"/>.</summary>
    public bool IsExpired(DateTime nowUtc) => ExpiresUtc is { } e && e <= nowUtc;

    /// <summary>Whether a sweeper may reclaim it: released, or its lease run out. (Whether its owner is dead is the sweeper's
    /// own call: the ledger records only who owns it.)</summary>
    public bool IsDue(DateTime nowUtc) => IsReleased || IsExpired(nowUtc);
}

/// <summary>
/// The resource ledger (roadmap Phase 4, M.ORCH-T1676): the <c>resources</c> table in <c>orchestrator.db</c> (SQL Server once
/// configured, else the SQLite file, as <see cref="OrchestratorDb.OpenData"/> picks) records every scratch dir, worktree and
/// temp file the orchestrator creates, with its owner and lease expiry. The contract the sweepers build on (prune on daemon
/// start, the disk floor, ...): <see cref="Register"/> <em>before</em> the thing is created (so a crash between the two leaves
/// a row pointing at nothing, never an untracked folder), <see cref="Release"/> or <see cref="ReleaseOwnedBy"/> when its owner
/// ends, and <see cref="Forget"/> once the thing itself is deleted. <see cref="Due"/> lists what may be reclaimed. Rows are
/// per machine (<see cref="Machine"/>): paths mean nothing on another one. The table isn't in the dump: it churns and is
/// machine-local.
/// </summary>
public sealed class ResourceLedger(OrchestratorDb db, string? machine = null)
{
    /// <summary>The table's name.</summary>
    public const string Table = "resources";

    /// <summary>The longest path the SQL Server key holds (<c>nvarchar(320)</c>, so the key fits 900 bytes).</summary>
    public const int MaxPathLength = 320;

    /// <summary>This machine's name, the rows this ledger reads and writes.</summary>
    public string Machine { get; } = machine ?? Environment.MachineName;

    /// <summary>The owner name for something a ticket's agent uses.</summary>
    public static string TicketOwner(string ticketId) => "ticket:" + ticketId;

    /// <summary>The owner name for something a Claude session uses.</summary>
    public static string SessionOwner(string sessionId) => "session:" + sessionId;

    /// <summary>The owner name for something a process uses (its pid).</summary>
    public static string ProcessOwner(int pid) => "pid:" + pid.ToString(CultureInfo.InvariantCulture);

    /// <summary>The owner name for something the daemon itself uses.</summary>
    public const string DaemonOwner = "daemon";

    /// <summary>
    /// Records <paramref name="path"/> as <paramref name="owner"/>'s, its lease running to <paramref name="expiresUtc"/> (null:
    /// none). Call it before creating the thing. Registering a path already recorded renews it: the new owner, ticket and
    /// expiry, and it's no longer released (its creation time stays).
    /// </summary>
    public Resource Register(string kind, string path, string owner, DateTime nowUtc, DateTime? expiresUtc = null, string? ticket = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        var full = Normalize(path);
        Resource? kept = null;
        db.WriteData((connection, tx) =>
        {
            // Already recorded (maybe spelled in another case): that row is renewed, not a second one added beside it.
            var existing = Find(connection, tx, kind, full);
            var created = existing?.CreatedUtc ?? nowUtc;
            var key = existing?.Path ?? full;
            kept = new Resource(Machine, kind, key, owner, ticket, created, expiresUtc, null);
            Sql.Upsert(connection, tx, Table, ["machine", "kind", "path"],
                ("machine", Machine), ("kind", kind), ("path", key), ("owner", owner), ("ticket", ticket),
                ("created_utc", Text(created)), ("expires_utc", expiresUtc is { } e ? Text(e) : null), ("released_utc", null));
        }, dump: false);
        return kept!;
    }

    /// <summary>Marks <paramref name="path"/> released (its owner is done with it); false when it isn't recorded or already was.</summary>
    public bool Release(string kind, string path, DateTime nowUtc)
    {
        var changed = 0;
        db.WriteData((connection, tx) => changed = Sql.Run(connection, tx,
            $"UPDATE {Table} SET released_utc = @at WHERE machine = @m AND kind = @k AND LOWER(path) = @p AND released_utc IS NULL;",
            ("@at", Text(nowUtc)), ("@m", Machine), ("@k", kind), ("@p", Normalize(path).ToLowerInvariant())), dump: false);
        return changed > 0;
    }

    /// <summary>Marks everything <paramref name="owner"/> holds released (its unit ended); how many were.</summary>
    public int ReleaseOwnedBy(string owner, DateTime nowUtc)
    {
        var changed = 0;
        db.WriteData((connection, tx) => changed = Sql.Run(connection, tx,
            $"UPDATE {Table} SET released_utc = @at WHERE machine = @m AND owner = @o AND released_utc IS NULL;",
            ("@at", Text(nowUtc)), ("@m", Machine), ("@o", owner)), dump: false);
        return changed;
    }

    /// <summary>Drops <paramref name="path"/>'s row, once the thing itself is deleted (or was never made); false when none.</summary>
    public bool Forget(string kind, string path)
    {
        var changed = 0;
        db.WriteData((connection, tx) => changed = Sql.Run(connection, tx,
            $"DELETE FROM {Table} WHERE machine = @m AND kind = @k AND LOWER(path) = @p;",
            ("@m", Machine), ("@k", kind), ("@p", Normalize(path).ToLowerInvariant())), dump: false);
        return changed > 0;
    }

    /// <summary><paramref name="path"/>'s row on this machine, or null.</summary>
    public Resource? Find(string kind, string path)
    {
        using var connection = db.OpenData();
        return Find(connection, null, kind, Normalize(path));
    }

    /// <summary>Every row on this machine, by kind then path.</summary>
    public IReadOnlyList<Resource> All()
    {
        using var connection = db.OpenData();
        return Sql.Read(connection, null, $"SELECT {Columns} FROM {Table} WHERE machine = @m ORDER BY kind, path;", Row, ("@m", Machine));
    }

    /// <summary>Everything <paramref name="owner"/> holds on this machine, released or not.</summary>
    public IReadOnlyList<Resource> OwnedBy(string owner) => [.. All().Where(r => r.Owner == owner)];

    /// <summary>What a sweeper may reclaim on this machine by <paramref name="nowUtc"/>: released, or its lease run out
    /// (<see cref="Resource.IsDue"/>).</summary>
    public IReadOnlyList<Resource> Due(DateTime nowUtc) => [.. All().Where(r => r.IsDue(nowUtc))];

    private Resource? Find(DbConnection connection, DbTransaction? tx, string kind, string full) =>
        Sql.Read(connection, tx, $"SELECT {Columns} FROM {Table} WHERE machine = @m AND kind = @k AND LOWER(path) = @p;", Row,
            ("@m", Machine), ("@k", kind), ("@p", full.ToLowerInvariant())).FirstOrDefault();

    private const string Columns = "machine, kind, path, owner, ticket, created_utc, expires_utc, released_utc";

    private static Resource Row(DbDataReader r) => new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), Sql.OptionalText(r, 4),
        Parse(r.GetString(5)), Sql.OptionalText(r, 6) is { } e ? Parse(e) : null, Sql.OptionalText(r, 7) is { } d ? Parse(d) : null);

    /// <summary>A path as the ledger keys it: full, without a trailing separator. Windows paths compare case-insensitively.</summary>
    public static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = System.IO.Path.GetFullPath(path);
        var root = System.IO.Path.GetPathRoot(full) ?? "";
        full = full.Length > root.Length ? full.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar) : full;
        if (full.Length > MaxPathLength) throw new ArgumentException($"A path the ledger records is at most {MaxPathLength} characters: {full}", nameof(path));
        return full;
    }

    private static string Text(DateTime utc) => utc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    private static DateTime Parse(string text) =>
        DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
}

/// <summary>
/// Each claimed ticket's own scratch dir (roadmap Phase 4, M.ORCH-T1676): <c>&lt;WorktreeRoot&gt;\.scratch\&lt;ticket&gt;</c> (on the
/// worktree root's drive, not %TEMP%), registered in the <see cref="ResourceLedger"/> as the ticket's
/// (<see cref="ResourceLedger.TicketOwner"/>) before it's made, its lease <see cref="OrchestratorSettings.ScratchLeaseHours"/>,
/// and handed to the agent as <see cref="EnvVar"/> in its claim prompt (<see cref="Sentence"/>). Released when the claim ends.
/// The <c>.scratch</c> folder is never a repo's name, so the old-worktree sweep (which walks <c>WorktreeRoot\&lt;repo&gt;</c>)
/// leaves it alone.
/// </summary>
public static class TicketScratch
{
    /// <summary>The name the agent knows its scratch dir by.</summary>
    public const string EnvVar = "ORCH_SCRATCH";

    /// <summary>The folder under the worktree root that holds every ticket's scratch dir.</summary>
    public const string FolderName = ".scratch";

    /// <summary><paramref name="ticketId"/>'s scratch dir under <paramref name="worktreeRoot"/>.</summary>
    public static string PathFor(string worktreeRoot, string ticketId) =>
        System.IO.Path.Combine(worktreeRoot, FolderName, string.Concat(ticketId.Select(c => System.IO.Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)));

    /// <summary>
    /// Registers <paramref name="ticketId"/>'s scratch dir in <paramref name="ledger"/> (renewing its lease when it's already
    /// there), then makes it. Best effort: null when either fails, so a claim never fails over its scratch dir.
    /// </summary>
    public static string? Prepare(ResourceLedger ledger, OrchestratorSettings settings, string ticketId, DateTime nowUtc)
    {
        try
        {
            var dir = ResourceLedger.Normalize(PathFor(settings.WorktreeRoot, ticketId));
            ledger.Register(ResourceKind.Scratch, dir, ResourceLedger.TicketOwner(ticketId), nowUtc,
                settings.ScratchLeaseHours > 0 ? nowUtc + OrchestratorSettings.SecondsSpan(settings.ScratchLeaseHours * 3600) : null, ticketId);
            Directory.CreateDirectory(dir);
            return dir;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DbException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>What a claim's prompt says about its scratch dir.</summary>
    public static string Sentence(string dir) =>
        $"Your own scratch dir for this ticket is {dir} ({EnvVar}): put any temp files, logs, test output or throwaway builds there, " +
        "never in the repo or %TEMP%; the orchestrator owns it and reclaims it once the ticket is done. ";
}
