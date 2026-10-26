using System.IO.Pipes;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Gateway;

/// <summary>What a remote viewer may do with a session. Ordered: each level includes the ones below it.</summary>
internal enum GatewayAccess
{
    /// <summary>Not listed, not attachable.</summary>
    None = 0,

    /// <summary>Read-only: it sees the stream; its Input and Resize frames are dropped and its Hello never sets the size.</summary>
    Watch = 1,

    /// <summary>Types into the session and owns its size, as a local viewer does.</summary>
    Interact = 2,
}

/// <summary>
/// Who may watch or interact with which session (M.ORCH-T933), filled by M.ORCH-T934's per-member grants (<see cref="AclGrants"/>).
/// </summary>
internal interface IGatewayGrants
{
    /// <summary>The most <paramref name="viewer"/> (a token's subject) may do with <paramref name="session"/>.</summary>
    GatewayAccess AccessTo(string viewer, TermSessionRecord session);
}

/// <summary>Only the machine owner's own identity gets in, with full access; everyone else gets nothing (M.ORCH-T933's grants,
/// kept for tests and as what <see cref="AclGrants"/> answers without a snapshot).</summary>
internal sealed class OwnerOnlyGrants(string owner) : IGatewayGrants
{
    public GatewayAccess AccessTo(string viewer, TermSessionRecord session) =>
        string.Equals(viewer, owner, StringComparison.OrdinalIgnoreCase) ? GatewayAccess.Interact : GatewayAccess.None;

    /// <summary>The access a request gets: the least of the grant, the token's scope and what the request asked for
    /// (<c>?mode=watch</c> asks for read-only).</summary>
    public static GatewayAccess Effective(GatewayAccess granted, string tokenScope, string? requestedMode)
    {
        var access = (GatewayAccess)Math.Min((int)granted, (int)GatewayScopes.ToAccess(tokenScope));
        if (string.Equals(requestedMode, GatewayScopes.Watch, StringComparison.OrdinalIgnoreCase)) access = (GatewayAccess)Math.Min((int)access, (int)GatewayAccess.Watch);
        return access;
    }
}

/// <summary>
/// Who may watch or interact with which session, from <c>orchestrator.db</c> (M.ORCH-T934): its <c>term_grants</c>, <c>members</c>
/// and <c>org_members</c>, which reach the gateway as the snapshot the daemon and <c>orchestrator gateway grant|revoke</c> write
/// (<see cref="TermGatewayAudit.AclPath"/>), read again whenever the file changes. The rule (<see cref="Evaluate"/>):
/// <list type="bullet">
/// <item>The owner (the gateway's <c>--owner</c>) always has <see cref="GatewayAccess.Interact"/>.</item>
/// <item>Anyone who isn't an active member has <see cref="GatewayAccess.None"/>, whatever a grant says.</item>
/// <item>A member in the same org as the session's login may <see cref="GatewayAccess.Watch"/> by default.</item>
/// <item>A grant, to the person or for one session, raises that to its level while it lasts; an interact grant always has an
/// expiry, and one without is ignored.</item>
/// </list>
/// With no snapshot (the daemon hasn't written one yet, or it can't be read) only the owner gets in, as before.
/// </summary>
internal sealed class AclGrants(string owner, string directory, Func<DateTime>? clock = null) : IGatewayGrants
{
    private static readonly TimeSpan Recheck = TimeSpan.FromSeconds(1);
    private readonly Func<DateTime> now = clock ?? (() => DateTime.UtcNow);
    private readonly System.Threading.Lock gate = new();
    private TermGatewayAclSnapshot? snapshot;
    private DateTime stamp = DateTime.MinValue, checkedUtc = DateTime.MinValue;

    public GatewayAccess AccessTo(string viewer, TermSessionRecord session) => Evaluate(Current(), owner, viewer, session, now());

    private TermGatewayAclSnapshot? Current()
    {
        lock (gate)
        {
            var at = DateTime.UtcNow;
            if (at - checkedUtc < Recheck) return snapshot;
            checkedUtc = at;
            DateTime written;
            try { written = File.GetLastWriteTimeUtc(TermGatewayAudit.AclPath(directory)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return snapshot; }
            if (written == stamp) return snapshot;
            stamp = written;
            // Unreadable or gone: nobody but the owner, never the last list kept past a change.
            snapshot = TermGatewayAudit.ReadAcl(directory);
            return snapshot;
        }
    }

    /// <summary>The most <paramref name="viewer"/> may do with <paramref name="session"/> of <paramref name="owner"/>'s under
    /// <paramref name="acl"/> at <paramref name="nowUtc"/> (the rule is in the class summary).</summary>
    internal static GatewayAccess Evaluate(TermGatewayAclSnapshot? acl, string owner, string viewer, TermSessionRecord session, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(viewer)) return GatewayAccess.None;
        if (string.Equals(viewer.Trim(), owner.Trim(), StringComparison.OrdinalIgnoreCase)) return GatewayAccess.Interact;
        if (acl is null) return GatewayAccess.None;
        var v = Normalize(viewer);
        var member = acl.Members.FirstOrDefault(m => Normalize(m.Email) == v);
        if (member is null) return GatewayAccess.None;

        var access = SharesOrg(acl, member, session) ? GatewayAccess.Watch : GatewayAccess.None;
        foreach (var g in acl.Grants)
        {
            if (!string.Equals(g.Owner?.Trim(), owner.Trim(), StringComparison.OrdinalIgnoreCase) || Normalize(g.Viewer) != v) continue;
            if (!string.IsNullOrEmpty(g.Session) && !string.Equals(g.Session, session.Id, StringComparison.OrdinalIgnoreCase)) continue;
            var level = string.Equals(g.Level, GatewayScopes.Interact, StringComparison.OrdinalIgnoreCase) ? GatewayAccess.Interact
                : string.Equals(g.Level, GatewayScopes.Watch, StringComparison.OrdinalIgnoreCase) ? GatewayAccess.Watch : GatewayAccess.None;
            if (g.ExpiresUtc is { } expires ? expires <= nowUtc : level == GatewayAccess.Interact) continue;
            if (level > access) access = level;
        }
        return access;
    }

    /// <summary>Whether the member is in the org of the session's login (a login no <c>org_members</c> row names, the global one
    /// included, is the default org's; so is a member none of whose logins or person is placed).</summary>
    internal static bool SharesOrg(TermGatewayAclSnapshot acl, TermAclMember member, TermSessionRecord session)
    {
        var login = Normalize(session.Login);
        var sessionOrg = acl.Logins.FirstOrDefault(l => Normalize(l.Login) == login)?.Org ?? DefaultOrg;
        var email = Normalize(member.Email);
        var orgs = acl.Logins
            .Where(l => (l.Login.Length > 0 && Normalize(l.Login) == email)
                        || (!string.IsNullOrWhiteSpace(member.Name) && string.Equals(l.Person.Trim(), member.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            .Select(l => l.Org)
            .ToHashSet(StringComparer.Ordinal);
        if (orgs.Count == 0) orgs.Add(DefaultOrg);
        return orgs.Contains(sessionOrg);
    }

    /// <summary>The org every login belongs to until another names it (<c>Organization.DefaultId</c> in Core).</summary>
    internal const string DefaultOrg = "default";

    private static string Normalize(string? email) => email?.Trim().ToLowerInvariant() ?? "";
}

/// <summary>The hosted sessions the gateway can offer: the registry (<see cref="TermSessions"/>) by default, a test's own otherwise.</summary>
internal interface ISessionSource
{
    IReadOnlyList<TermSessionRecord> Enumerate();
    TermSessionRecord? Get(string id);
}

internal sealed class RegistrySessions : ISessionSource
{
    public IReadOnlyList<TermSessionRecord> Enumerate() => TermSessions.Enumerate();
    public TermSessionRecord? Get(string id) => TermSessions.Get(id);
}

/// <summary>Opens a session's local pipe: <see cref="TermSessionRecord.Pipe"/> on this machine by default, an in-memory stream in tests.</summary>
internal interface IPipeConnector
{
    Task<Stream> ConnectAsync(TermSessionRecord session, CancellationToken ct);
}

internal sealed class NamedPipeConnector : IPipeConnector
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public async Task<Stream> ConnectAsync(TermSessionRecord session, CancellationToken ct)
    {
        var name = string.IsNullOrWhiteSpace(session.Pipe) ? TermProtocol.PipeName(session.Id) : session.Pipe;
        var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(Timeout);
            await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
            return pipe;
        }
        catch
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
