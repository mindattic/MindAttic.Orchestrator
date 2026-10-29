using System.Globalization;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>One <c>term_grants</c> row (M.ORCH-T934): <see cref="Viewer"/> may <see cref="Level"/> the sessions of
/// <see cref="Owner"/> (every one when <see cref="Session"/> is empty) until <see cref="ExpiresUtc"/>.</summary>
public sealed record TermGrant(string Owner, string Viewer, string Session, string Level, DateTime? ExpiresUtc, string? CreatedBy, DateTime CreatedUtc)
{
    public bool IsLive(DateTime nowUtc) => ExpiresUtc is not { } e || e > nowUtc;
}

/// <summary>
/// The team gateway's viewing ACL and audit log in <c>orchestrator.db</c> (M.ORCH-T934): SQL Server once configured, else the
/// SQLite file, as <see cref="OrchestratorDb.OpenData"/> picks.
/// <list type="bullet">
/// <item><c>term_grants</c>: explicit watch and interact grants. By default members of the same org (<c>members</c>,
/// <c>org_members</c>) may watch each other's sessions; interact always needs a grant here, with an expiry.</item>
/// <item><c>term_audit</c>: every attach, detach, refusal and batch of remote input (counts and sizes, never content).</item>
/// </list>
/// The gateway references only the Term library, so the ACL reaches it as a snapshot file (<see cref="Publish"/>) and its audit
/// records come back through a spool file (<see cref="ImportAudit"/>); the daemon does both each tick while the gateway is on,
/// and the <c>orchestrator gateway</c> commands at once.
/// </summary>
public static class TermGrants
{
    public const string Watch = TermGatewayTokens.Watch, Interact = TermGatewayTokens.Interact;

    /// <summary>How long an interact grant lasts when no <c>--for</c> is given.</summary>
    public static readonly TimeSpan DefaultInteract = TimeSpan.FromHours(1);

    /// <summary>The longest a grant may be given for.</summary>
    public static readonly TimeSpan MaxFor = TimeSpan.FromDays(30);

    /// <summary>The gateway owner's identity: its setting, else the Windows user name (as the gateway's own default).</summary>
    public static string OwnerOf(GatewaySettings settings) => string.IsNullOrWhiteSpace(settings.Owner) ? Environment.UserName : settings.Owner.Trim();

    /// <summary>
    /// Grants <paramref name="viewer"/> (a member's email) <paramref name="level"/> on <paramref name="owner"/>'s sessions, or on
    /// <paramref name="session"/> alone, replacing a grant they already had for the same scope. An interact grant must expire.
    /// </summary>
    /// <exception cref="ArgumentException">A bad level, a missing or past expiry, or one too far off.</exception>
    public static TermGrant Grant(OrchestratorDb db, string owner, string viewer, string level, string? session, DateTime? expiresUtc,
        string? createdBy, DateTime nowUtc)
    {
        var lv = level.Trim().ToLowerInvariant();
        if (lv is not (Watch or Interact)) throw new ArgumentException("The level is watch or interact.", nameof(level));
        if (AccountDirs.Normalize(viewer) is not { } who) throw new ArgumentException($"\"{viewer}\" isn't an email.", nameof(viewer));
        if (lv == Interact && expiresUtc is null) throw new ArgumentException("An interact grant needs an expiry.", nameof(expiresUtc));
        if (expiresUtc is { } e && (e <= nowUtc || e - nowUtc > MaxFor + TimeSpan.FromMinutes(1)))
            throw new ArgumentException($"A grant lasts from a minute to {MaxFor.TotalDays:0} days.", nameof(expiresUtc));
        // Session ids and the owner are matched case-insensitively everywhere (the gateway, broadcast.json, Revoke's SQL), so
        // they're stored in lower case: SQLite's TEXT compares are case-sensitive without it.
        var grant = new TermGrant(owner.Trim().ToLowerInvariant(), who, session?.Trim().ToLowerInvariant() ?? "", lv, expiresUtc is { } x ? Utc(x) : null, createdBy, Utc(nowUtc));
        db.WriteData((connection, tx) => Sql.Upsert(connection, tx, "term_grants", ["owner", "viewer", "session"],
            ("owner", grant.Owner), ("viewer", grant.Viewer), ("session", grant.Session), ("level", grant.Level),
            ("expires_utc", grant.ExpiresUtc is { } ex ? Iso(ex) : null), ("created_by", grant.CreatedBy), ("created_utc", Iso(grant.CreatedUtc))));
        return grant;
    }

    /// <summary>Removes <paramref name="viewer"/>'s grants on <paramref name="owner"/>'s sessions: the one for
    /// <paramref name="session"/> (empty: the person-wide one), or every one when <paramref name="session"/> is null. How many went.</summary>
    public static int Revoke(OrchestratorDb db, string owner, string viewer, string? session)
    {
        var who = AccountDirs.Normalize(viewer) ?? viewer.Trim().ToLowerInvariant();
        var ownerKey = owner.Trim().ToLowerInvariant();
        var removed = 0;
        db.WriteDataIf((connection, tx) =>
        {
            removed = session is null
                ? Sql.Run(connection, tx, "DELETE FROM term_grants WHERE LOWER(owner) = @o AND viewer = @v;", ("@o", ownerKey), ("@v", who))
                // In any case, as the gateway matches it (and grants stored before ids were lower-cased).
                : Sql.Run(connection, tx, "DELETE FROM term_grants WHERE LOWER(owner) = @o AND viewer = @v AND LOWER(session) = @s;",
                    ("@o", ownerKey), ("@v", who), ("@s", session.Trim().ToLowerInvariant()));
            return removed > 0;
        });
        return removed;
    }

    /// <summary>Every grant (of <paramref name="owner"/>'s, when given), live ones only unless <paramref name="includeExpired"/>.</summary>
    public static IReadOnlyList<TermGrant> List(OrchestratorDb db, DateTime nowUtc, string? owner = null, bool includeExpired = false)
    {
        using var connection = db.OpenData();
        var all = Sql.Read(connection, null, "SELECT owner, viewer, session, level, expires_utc, created_by, created_utc FROM term_grants ORDER BY viewer, session;",
            r => new TermGrant(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), ParseUtc(Sql.OptionalText(r, 4)),
                Sql.OptionalText(r, 5), ParseUtc(r.GetString(6)) ?? DateTime.MinValue));
        return [.. all.Where(g => (owner is null || string.Equals(g.Owner, owner.Trim(), StringComparison.OrdinalIgnoreCase)) && (includeExpired || g.IsLive(nowUtc)))];
    }

    /// <summary>What the gateway decides from: the active members, the Claude logins placed in orgs, and the live grants.</summary>
    public static TermGatewayAclSnapshot Snapshot(OrchestratorDb db, DateTime nowUtc)
    {
        var members = MemberRoster.Load(db)
            .Where(m => m.Status == "active")
            .Select(m => new TermAclMember(m.Email, m.Name))
            .OrderBy(m => m.Email, StringComparer.Ordinal)
            .ToList();
        var logins = db.LoadOrgs().Members
            .Where(m => m.IsClaude)
            .Select(m => new TermAclLogin(m.Login, m.OrgId, m.Person))
            .OrderBy(l => l.Login, StringComparer.Ordinal)
            .ToList();
        var grants = List(db, nowUtc)
            .Select(g => new TermAclGrant(g.Owner, g.Viewer, g.Session, g.Level, g.ExpiresUtc))
            .ToList();
        return new TermGatewayAclSnapshot { Members = members, Logins = logins, Grants = grants };
    }

    /// <summary>Writes the snapshot into the gateway's data folder when it changed; true when it wrote. An expired grant drops
    /// out at the next publish after it lapses, and the gateway checks expiry itself meanwhile.</summary>
    public static bool Publish(OrchestratorDb db, DateTime nowUtc, string? directory = null) =>
        TermGatewayAudit.WriteAcl(Snapshot(db, nowUtc), directory);

    /// <summary>Imports the gateway's spooled audit records into <c>term_audit</c> (each id once) and clears the spool. How many
    /// were new.</summary>
    public static int ImportAudit(OrchestratorDb db, string? directory = null)
    {
        var (records, commit) = TermGatewayAudit.Drain(directory);
        var added = 0;
        if (records.Count > 0)
            db.WriteDataIf((connection, tx) =>
            {
                foreach (var r in records)
                {
                    var before = Convert.ToInt64(Sql.Scalar(connection, tx, "SELECT COUNT(*) FROM term_audit WHERE id = @id;", ("@id", r.Id)), CultureInfo.InvariantCulture);
                    if (before > 0) continue;
                    Sql.Run(connection, tx,
                        "INSERT INTO term_audit (id, at_utc, event, viewer, owner, session, level, remote, frames, bytes, detail) " +
                        "VALUES (@id, @at, @event, @viewer, @owner, @session, @level, @remote, @frames, @bytes, @detail);",
                        ("@id", Clip(r.Id, 64)), ("@at", Iso(r.AtUtc)), ("@event", Clip(r.Event, 16)), ("@viewer", Clip(r.Viewer, 256)),
                        ("@owner", r.Owner is null ? null : Clip(r.Owner, 256)), ("@session", Clip(r.Session, 64)), ("@level", Clip(r.Level, 16)),
                        ("@remote", r.Remote is null ? null : Clip(r.Remote, 64)), ("@frames", r.Frames), ("@bytes", r.Bytes), ("@detail", r.Detail));
                    added++;
                }
                return false; // term_audit isn't in the dump
            });
        commit();
        return added;
    }

    /// <summary>The audit records from <paramref name="sinceUtc"/> on (all when null), newest last, at most <paramref name="limit"/>
    /// (the newest ones).</summary>
    public static IReadOnlyList<TermGatewayAuditRecord> Audit(OrchestratorDb db, DateTime? sinceUtc, int limit = 200)
    {
        using var connection = db.OpenData();
        var n = Math.Clamp(limit, 1, 100_000);
        var where = sinceUtc is null ? "" : " WHERE at_utc >= @since";
        var columns = "id, at_utc, event, viewer, owner, session, level, remote, frames, bytes, detail";
        var sql = Sql.IsServer(connection)
            ? $"SELECT TOP ({n}) {columns} FROM term_audit{where} ORDER BY at_utc DESC;"
            : $"SELECT {columns} FROM term_audit{where} ORDER BY at_utc DESC LIMIT {n};";
        (string, object?)[] parameters = sinceUtc is { } s ? [("@since", Iso(s))] : [];
        var rows = Sql.Read(connection, null, sql, r => new TermGatewayAuditRecord
        {
            Id = r.GetString(0), AtUtc = ParseUtc(r.GetString(1)) ?? DateTime.MinValue, Event = r.GetString(2), Viewer = r.GetString(3),
            Owner = Sql.OptionalText(r, 4), Session = r.GetString(5), Level = r.GetString(6), Remote = Sql.OptionalText(r, 7),
            Frames = Sql.Long(r, 8), Bytes = Sql.Long(r, 9), Detail = Sql.OptionalText(r, 10),
        }, parameters);
        rows.Reverse();
        return rows;
    }

    /// <summary>A length of time as <c>--for</c> and <c>--since</c> take it: <c>90m</c>, <c>2h</c>, <c>1d</c>, <c>1h30m</c>, or bare
    /// minutes. Null when it isn't one, or isn't positive.</summary>
    public static TimeSpan? ParseDuration(string? text)
    {
        var t = text?.Trim().ToLowerInvariant() ?? "";
        if (t.Length == 0) return null;
        try { return Parse(t); }
        catch (OverflowException) { return null; }
    }

    private static TimeSpan? Parse(string t)
    {
        if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var bare)) return bare > 0 ? TimeSpan.FromMinutes(bare) : null;
        var total = TimeSpan.Zero;
        var number = "";
        foreach (var c in t)
        {
            if (char.IsAsciiDigit(c) || c == '.') { number += c; continue; }
            if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return null;
            total += c switch
            {
                'w' => TimeSpan.FromDays(7 * value),
                'd' => TimeSpan.FromDays(value),
                'h' => TimeSpan.FromHours(value),
                'm' => TimeSpan.FromMinutes(value),
                's' => TimeSpan.FromSeconds(value),
                _ => TimeSpan.MinValue,
            };
            if (total < TimeSpan.Zero) return null;
            number = "";
        }
        return number.Length == 0 && total > TimeSpan.Zero ? total : null;
    }

    private static DateTime Utc(DateTime t) => t.Kind == DateTimeKind.Utc ? t : t.Kind == DateTimeKind.Local ? t.ToUniversalTime() : DateTime.SpecifyKind(t, DateTimeKind.Utc);

    /// <summary>ISO-8601 UTC with a fixed width, so the text sorts as the time does.</summary>
    internal static string Iso(DateTime t) => Utc(t).ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);

    internal static DateTime? ParseUtc(string? text) =>
        DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t) ? t : null;

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..max];
}
