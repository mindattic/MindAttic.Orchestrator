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

    /// <summary>Whether <paramref name="viewer"/> is an active org member (hub phase 1, M.ORCH-T1640): only then may a member key
    /// mint them a token, or a member token run a tickets command. Nobody is, without a member roster.</summary>
    bool IsActiveMember(string viewer) => false;

    /// <summary>The Claude logins that are <paramref name="viewer"/>'s (hub phase 1, M.ORCH-T1640): their own email, and every login
    /// <c>org_members</c> places under their name. A member may close only a ticket one of these works or is assigned.</summary>
    IReadOnlySet<string> LoginsOf(string viewer) => new HashSet<string>(StringComparer.Ordinal) { viewer.Trim().ToLowerInvariant() };

    /// <summary>The hub phase 2 policy (M.ORCH-T1641), or null while it's off: then nothing below differs from <see cref="AccessTo"/>.</summary>
    TermHubPolicy? Hub => null;

    /// <summary>The most <paramref name="viewer"/> may do with <paramref name="session"/>, which runs under Windows account
    /// <paramref name="sessionAccount"/>, and whether that rests on a grant its owner must consent to (hub phase 2). Without a hub
    /// policy it is <see cref="AccessTo"/>, with no consent.</summary>
    GatewayDecision Decide(string viewer, TermSessionRecord session, string? sessionAccount) => new(AccessTo(viewer, session));
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

    public bool IsActiveMember(string viewer) => FindMember(Current(), viewer) is not null;

    public IReadOnlySet<string> LoginsOf(string viewer) => Logins(Current(), viewer);

    public TermHubPolicy? Hub => Current()?.Hub is { IsOn: true } hub ? hub : null;

    public GatewayDecision Decide(string viewer, TermSessionRecord session, string? sessionAccount) =>
        Decide(Current(), owner, viewer, session, now(), sessionAccount);

    /// <summary>
    /// The hub phase 2 decision (M.ORCH-T1641; <see cref="TermHubPolicy"/>). Without a hub policy in <paramref name="acl"/> it is
    /// <see cref="Evaluate"/>, unchanged. With one:
    /// <list type="bullet">
    /// <item>The machine owner always interacts.</item>
    /// <item>The session's owner (the member whose login it runs on) interacts with it when it may be typed into
    /// (<see cref="TermHubPolicy.MayInteract"/>: trusted org, or it runs under their own Windows account), else watches it, whatever
    /// org it's in.</item>
    /// <item>Anyone else: the org default and the machine owner's grants, as before, except that an interact grant counts only on a
    /// session that may be typed into, and on a member's session it needs that member's consent (<see cref="GatewayDecision.ConsentKey"/>):
    /// the gateway serves watch until they say yes.</item>
    /// </list>
    /// </summary>
    internal static GatewayDecision Decide(TermGatewayAclSnapshot? acl, string owner, string viewer, TermSessionRecord session, DateTime nowUtc, string? sessionAccount)
    {
        if (acl?.Hub is not { IsOn: true } hub) return new(Evaluate(acl, owner, viewer, session, nowUtc));
        if (string.IsNullOrWhiteSpace(viewer)) return new(GatewayAccess.None);
        if (string.Equals(viewer.Trim(), owner.Trim(), StringComparison.OrdinalIgnoreCase)) return new(GatewayAccess.Interact);
        var member = FindMember(acl, viewer);
        if (member is null) return new(GatewayAccess.None);
        var v = Normalize(viewer);
        var sessionOwner = SessionOwner(acl, session);
        var typable = hub.MayInteract(sessionOwner, sessionAccount);
        if (sessionOwner == v) return new(typable ? GatewayAccess.Interact : GatewayAccess.Watch, null, sessionOwner);

        var access = SharesOrg(acl, member, session) ? GatewayAccess.Watch : GatewayAccess.None;
        string? consent = null;
        var interactWithoutConsent = false;
        foreach (var g in acl.Grants ?? [])
        {
            if (g is null) continue;
            if (!string.Equals(g.Owner?.Trim(), owner.Trim(), StringComparison.OrdinalIgnoreCase) || Normalize(g.Viewer) != v) continue;
            if (!string.IsNullOrEmpty(g.Session) && !string.Equals(g.Session, session.Id, StringComparison.OrdinalIgnoreCase)) continue;
            var level = string.Equals(g.Level, GatewayScopes.Interact, StringComparison.OrdinalIgnoreCase) ? GatewayAccess.Interact
                : string.Equals(g.Level, GatewayScopes.Watch, StringComparison.OrdinalIgnoreCase) ? GatewayAccess.Watch : GatewayAccess.None;
            if (g.ExpiresUtc is { } expires ? expires <= nowUtc : level == GatewayAccess.Interact) continue;
            if (level == GatewayAccess.Interact)
            {
                // Typing into a CLI that runs as the host's one user (or as someone else's account) is refused without isolation.
                if (!typable) level = GatewayAccess.Watch;
                // On a member's CLI, the machine owner's grant takes effect only with that member's consent.
                else if (sessionOwner is not null) consent ??= ConsentKey(g, v, sessionOwner);
                else interactWithoutConsent = true;
            }
            if (level > access) access = level;
        }
        if (access == GatewayAccess.Interact && !interactWithoutConsent && consent is not null) return new(GatewayAccess.Interact, consent, sessionOwner);
        return new(access, null, sessionOwner);
    }

    /// <summary>The member who owns <paramref name="session"/> under <paramref name="acl"/>: the active member whose logins (their
    /// email, or one placed under their name) include the session's login; null for the global login or one no member owns.</summary>
    internal static string? SessionOwner(TermGatewayAclSnapshot? acl, TermSessionRecord session)
    {
        if (acl is null) return null;
        var login = Normalize(session.Login);
        if (login.Length == 0) return null;
        foreach (var m in acl.Members ?? [])
            if (m is not null && !string.IsNullOrWhiteSpace(m.Email) && Logins(acl, m.Email).Contains(login)) return Normalize(m.Email);
        return null;
    }

    /// <summary>One grant's identity for consent: who granted it, to whom, on what, until when, on whose CLI. A new or changed
    /// grant is a new key, so it is asked about again.</summary>
    internal static string ConsentKey(TermAclGrant g, string viewer, string sessionOwner) =>
        string.Join('|', Normalize(g.Owner), viewer, (g.Session ?? "").Trim().ToLowerInvariant(), g.ExpiresUtc?.ToUniversalTime().Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "", sessionOwner);

    /// <summary>The active member <paramref name="viewer"/> is under <paramref name="acl"/>, or null.</summary>
    internal static TermAclMember? FindMember(TermGatewayAclSnapshot? acl, string? viewer)
    {
        if (acl is null || string.IsNullOrWhiteSpace(viewer)) return null;
        var v = Normalize(viewer);
        return (acl.Members ?? []).FirstOrDefault(m => m is not null && Normalize(m.Email) == v);
    }

    /// <summary>The logins that are <paramref name="viewer"/>'s under <paramref name="acl"/>: their email, and each login placed
    /// under their name (as <see cref="SharesOrg"/> reads a member's logins).</summary>
    internal static IReadOnlySet<string> Logins(TermGatewayAclSnapshot? acl, string viewer)
    {
        var mine = new HashSet<string>(StringComparer.Ordinal) { Normalize(viewer) };
        if (FindMember(acl, viewer) is { Name: { Length: > 0 } name })
            foreach (var l in acl!.Logins ?? [])
                if (l is not null && !string.IsNullOrEmpty(l.Login) && string.Equals(l.Person?.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase))
                    mine.Add(Normalize(l.Login));
        return mine;
    }

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
            // Gone or not a snapshot: nobody but the owner, never the last list kept past a change. Not readable just now (a
            // sharing violation as the daemon replaces it): the last good list, and the stamp stays, so the next look tries
            // again rather than waiting for the file to change (M.ORCH-T986).
            if (!TermGatewayAudit.TryReadAcl(directory, out var read)) return snapshot;
            stamp = written;
            snapshot = read;
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
        // A null list or entry (a hand-edited or damaged acl.json) is skipped, never a throw (M.ORCH-T984).
        var member = (acl.Members ?? []).FirstOrDefault(m => m is not null && Normalize(m.Email) == v);
        if (member is null) return GatewayAccess.None;

        var access = SharesOrg(acl, member, session) ? GatewayAccess.Watch : GatewayAccess.None;
        foreach (var g in acl.Grants ?? [])
        {
            if (g is null) continue;
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
        var logins = (acl.Logins ?? []).Where(l => l is not null).ToList();
        var sessionOrg = logins.FirstOrDefault(l => Normalize(l.Login) == login)?.Org ?? DefaultOrg;
        var email = Normalize(member.Email);
        var orgs = logins
            .Where(l => (!string.IsNullOrEmpty(l.Login) && Normalize(l.Login) == email)
                        || (!string.IsNullOrWhiteSpace(member.Name) && string.Equals(l.Person?.Trim(), member.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            .Select(l => l.Org ?? DefaultOrg)
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
