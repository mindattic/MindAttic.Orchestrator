using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// An organization (M.ORCH-T655): a group of people (Accounting with 3, Sales with 8, ...), each owning one or more logins,
/// that the dashboard switches between above the person tabs. The <see cref="Default"/> org is always there and holds every
/// login no other org names, so with no org added the dashboard is the one-org setup it always was.
/// </summary>
public sealed record Organization(string Id, string Name, int Position = 0)
{
    public const string DefaultId = "default";

    /// <summary>The org every login belongs to until another names it; renamed by saving a row with its id.</summary>
    public static readonly Organization Default = new(DefaultId, "MindAttic", 0);

    public bool IsDefault => Id == DefaultId;

    /// <summary>An org's id from its name: lower case, words joined by '-' ("Sales East" is sales-east).</summary>
    public static string IdFor(string name)
    {
        var id = new string([.. name.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-')]);
        while (id.Contains("--", StringComparison.Ordinal)) id = id.Replace("--", "-", StringComparison.Ordinal);
        return id.Trim('-');
    }
}

/// <summary>
/// One login of a person in an org (M.ORCH-T655): its LLM provider (Claude today; Codex, Gemini and others later) and the
/// login itself, for Claude its email, or "" for the global <c>~/.claude</c> login. A person is the name their logins share;
/// a login is in one org at most (the key is its provider and login).
/// </summary>
public sealed record OrgMember(string OrgId, string Person, string Login, string Provider = Providers.Claude)
{
    /// <summary>A login as it's kept: a Claude login by its normalized email ("" for the global login, also written "global");
    /// another provider's trimmed and lower case.</summary>
    public static string NormalizeLogin(string provider, string? login)
    {
        var l = login?.Trim() ?? "";
        if (!string.Equals(provider, Providers.Claude, StringComparison.OrdinalIgnoreCase)) return l.ToLowerInvariant();
        return l.Length == 0 || l.Equals("global", StringComparison.OrdinalIgnoreCase) ? "" : AccountDirs.Normalize(l) ?? l.ToLowerInvariant();
    }

    /// <summary>A provider by its known name (Claude, Codex, Gemini) whatever its case; any other as given.</summary>
    public static string NormalizeProvider(string? provider) =>
        new[] { Providers.Claude, Providers.Codex, Providers.Gemini }.FirstOrDefault(p => p.Equals(provider?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? (string.IsNullOrWhiteSpace(provider) ? Providers.Claude : provider.Trim());

    public bool IsClaude => Provider == Providers.Claude;
}

/// <summary>
/// Every org and who's in each (M.ORCH-T655), as the store keeps them: the orgs (the default one always first) and their
/// members. A login no member row names is the default org's, so a login just signed in shows there until it's placed.
/// </summary>
public sealed class OrgDirectory
{
    private readonly Dictionary<(string Provider, string Login), OrgMember> byLogin;

    public OrgDirectory(IEnumerable<Organization> orgs, IEnumerable<OrgMember> members)
    {
        var list = orgs.Where(o => !o.IsDefault).ToList();
        var saved = orgs.FirstOrDefault(o => o.IsDefault);
        Orgs = [saved ?? Organization.Default, .. list.OrderBy(o => o.Position).ThenBy(o => o.Name, StringComparer.OrdinalIgnoreCase)];
        var ids = Orgs.Select(o => o.Id).ToHashSet(StringComparer.Ordinal);
        // A member of an org since removed is the default org's again.
        Members = [.. members.Select(m => ids.Contains(m.OrgId) ? m : m with { OrgId = Organization.DefaultId })];
        byLogin = Members.GroupBy(m => (m.Provider, m.Login)).ToDictionary(g => g.Key, g => g.Last());
    }

    /// <summary>One org, the default, with nobody placed: the setup before any org is added.</summary>
    public static OrgDirectory Single { get; } = new([], []);

    public IReadOnlyList<Organization> Orgs { get; }
    public IReadOnlyList<OrgMember> Members { get; }

    /// <summary>Whether there's more than the default org, so the dashboard shows its org switcher.</summary>
    public bool HasOrgs => Orgs.Count > 1;

    public Organization? Find(string? id) => Orgs.FirstOrDefault(o => o.Id == id);

    /// <summary>The org <paramref name="idOrName"/> names (M.ORCH-T755): its id, else its name, either in any case.</summary>
    public Organization? Named(string? idOrName)
    {
        var t = idOrName?.Trim() ?? "";
        if (t.Length == 0) return null;
        return Orgs.FirstOrDefault(o => o.Id.Equals(t, StringComparison.OrdinalIgnoreCase))
            ?? Orgs.FirstOrDefault(o => o.Name.Equals(t, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The org <paramref name="login"/> on <paramref name="provider"/> is in: the one naming it, else the default org.</summary>
    public string OrgOf(string provider, string? login) =>
        byLogin.TryGetValue((provider, OrgMember.NormalizeLogin(provider, login)), out var m) ? m.OrgId : Organization.DefaultId;

    /// <summary>The org of a Claude login: its email, or null for the global login.</summary>
    public string OrgOfClaude(string? email) => OrgOf(Providers.Claude, email);

    /// <summary>The org a person tab is in: the 'API key' tab, the workspace's Vault key, is always the default org's.</summary>
    public string OrgOfTab(LoginTab tab) => tab.IsApiKey ? Organization.DefaultId : OrgOfClaude(tab.Email);

    /// <summary>Whether <paramref name="tab"/> shows under <paramref name="orgId"/>: its person's org.</summary>
    public bool InOrg(string orgId, LoginTab tab) => OrgOfTab(tab) == orgId;

    public IReadOnlyList<OrgMember> MembersOf(string orgId) => [.. Members.Where(m => m.OrgId == orgId)];

    /// <summary>The people in <paramref name="orgId"/>, by name, each with their logins.</summary>
    public IReadOnlyList<(string Person, IReadOnlyList<OrgMember> Logins)> PeopleOf(string orgId) =>
        [.. MembersOf(orgId).GroupBy(m => m.Person, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => (g.First().Person, (IReadOnlyList<OrgMember>)[.. g]))];

    /// <summary>Every Claude login a member row names (not the global one): each has a person tab, signed in or not yet.</summary>
    public IEnumerable<string> ClaudeEmails => Members.Where(m => m.IsClaude && m.Login.Length > 0).Select(m => m.Login);
}

/// <summary>
/// The org chosen in the dashboard's switcher (M.ORCH-T655), kept in the store across restarts, and what
/// <c>orchestrator org</c> works on (M.ORCH-T755) unless its <c>--org</c> names another.
/// </summary>
public static class OrgSelection
{
    /// <summary>The store state the dashboard keeps its chosen org in.</summary>
    public const string ViewName = "dashboard-org";

    public sealed class View { public string? Org { get; init; } }

    /// <summary>The org last chosen in the dashboard, or null when none was (or it can't be read now).</summary>
    public static string? Remembered(JsonStore store)
    {
        try { return store.Load<View>(ViewName).Org; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { return null; }
    }

    public static void Remember(JsonStore store, string orgId) => store.Save(ViewName, new View { Org = orgId });

    /// <summary>The org a command works on: the one <paramref name="requested"/> names (id or name, any case), else the
    /// dashboard's chosen one while it's still there, else the default org. Null with <paramref name="error"/> when
    /// <paramref name="requested"/> names no org.</summary>
    public static Organization? Choose(OrgDirectory orgs, string? requested, string? remembered, out string? error)
    {
        error = null;
        if (!string.IsNullOrWhiteSpace(requested))
        {
            if (orgs.Named(requested) is { } named) return named;
            error = $"No org is \"{requested.Trim()}\": name one by its id or name ({string.Join(", ", orgs.Orgs.Select(o => o.Id == o.Name ? o.Id : $"{o.Name} ({o.Id})"))}); orchestrator orgs lists them.";
            return null;
        }
        return orgs.Find(remembered) ?? orgs.Orgs[0];
    }
}

/// <summary>
/// The orgs and their members in the store of the orchestrator's data (M.ORCH-T655): SQL Server once configured
/// (<see cref="OrchestratorDb.OpenData"/>), else <c>data/orchestrator.db</c>; the tables <c>organizations</c> and
/// <c>org_members</c>.
/// </summary>
public static class OrgStore
{
    public static OrgDirectory LoadOrgs(this OrchestratorDb db)
    {
        using var connection = db.OpenData();
        var orgs = Sql.Read(connection, null, "SELECT id, name, position FROM organizations;",
            r => new Organization(r.GetString(0), r.GetString(1), Sql.Int(r, 2)));
        var members = Sql.Read(connection, null, "SELECT org_id, person, login, provider FROM org_members;",
            r => new OrgMember(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3)));
        return new OrgDirectory(orgs, members);
    }

    /// <summary>The orgs, or the single default one when the store can't be read now.</summary>
    public static OrgDirectory TryLoadOrgs(this OrchestratorDb db)
    {
        try { return db.LoadOrgs(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Data.Common.DbException) { return OrgDirectory.Single; }
    }

    /// <summary>Adds <paramref name="org"/> or renames and moves the one with its id.</summary>
    public static void SaveOrg(this OrchestratorDb db, Organization org)
    {
        if (string.IsNullOrWhiteSpace(org.Id) || string.IsNullOrWhiteSpace(org.Name)) throw new ArgumentException("An org needs an id and a name.", nameof(org));
        db.WriteData((connection, tx) => Sql.Upsert(connection, tx, "organizations", ["id"],
            ("id", org.Id), ("name", org.Name.Trim()), ("position", org.Position), ("created_utc", DateTime.UtcNow.ToString("O"))));
        StoreChanges.Note(db, StoreChanges.Orgs);
    }

    /// <summary>Removes the org <paramref name="id"/> (never the default one); its members go back to the default org.
    /// False when there's no such org.</summary>
    public static bool RemoveOrg(this OrchestratorDb db, string id)
    {
        if (id == Organization.DefaultId) throw new ArgumentException("The default org can't be removed; rename it instead.", nameof(id));
        var removed = false;
        db.WriteData((connection, tx) =>
        {
            Sql.Run(connection, tx, "DELETE FROM org_members WHERE org_id = @id;", ("@id", id));
            removed = Sql.Run(connection, tx, "DELETE FROM organizations WHERE id = @id;", ("@id", id)) > 0;
        });
        if (removed) StoreChanges.Note(db, StoreChanges.Orgs);
        return removed;
    }

    /// <summary>Puts <paramref name="member"/>'s login in its org under its person (moving it from any other org).</summary>
    public static OrgMember SetMember(this OrchestratorDb db, OrgMember member)
    {
        var provider = OrgMember.NormalizeProvider(member.Provider);
        var m = member with { Provider = provider, Login = OrgMember.NormalizeLogin(provider, member.Login), Person = member.Person.Trim() };
        if (m.Person.Length == 0) throw new ArgumentException("A member needs a person's name.", nameof(member));
        db.WriteData((connection, tx) =>
        {
            if (m.OrgId != Organization.DefaultId && Sql.Scalar(connection, tx, "SELECT COUNT(*) FROM organizations WHERE id = @id;", ("@id", m.OrgId)) is var n && Convert.ToInt64(n, System.Globalization.CultureInfo.InvariantCulture) == 0)
                throw new ArgumentException($"There's no org \"{m.OrgId}\".", nameof(member));
            Sql.Upsert(connection, tx, "org_members", ["provider", "login"],
                ("org_id", m.OrgId), ("person", m.Person), ("login", m.Login), ("provider", m.Provider), ("added_utc", DateTime.UtcNow.ToString("O")));
        });
        StoreChanges.Note(db, StoreChanges.Orgs);
        return m;
    }

    /// <summary>Takes a login out of its org (back to the default one). False when no org named it.</summary>
    public static bool RemoveMember(this OrchestratorDb db, string provider, string login)
    {
        var p = OrgMember.NormalizeProvider(provider);
        var removed = false;
        db.WriteData((connection, tx) => removed = Sql.Run(connection, tx, "DELETE FROM org_members WHERE provider = @p AND login = @l;",
            ("@p", p), ("@l", OrgMember.NormalizeLogin(p, login))) > 0);
        if (removed) StoreChanges.Note(db, StoreChanges.Orgs);
        return removed;
    }
}
