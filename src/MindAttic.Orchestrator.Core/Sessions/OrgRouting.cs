using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// Which org a ticket is dispatched in (M.ORCH-T753, after the dashboard-only scoping of M.ORCH-T655): a repo's tickets are its
/// org's (<see cref="OrchestratorSettings.RepoOrgs"/>, the default org for a repo not named), and go only to that org's members'
/// logins, each paced on its own login as always. A ticket assigned to a user (M.ORCH-T668) is that user's, whatever its repo:
/// the pin already sends it only to their login. With one org (<see cref="Active"/> false) every check here says yes, so a
/// single-org setup dispatches exactly as before.
/// </summary>
/// <param name="GlobalEmail">The global <c>~/.claude</c> login's email, when known: a member row may name the global login by it
/// or as "global".</param>
public sealed record OrgRouting(OrgDirectory Orgs, IReadOnlyDictionary<string, string> RepoOrgs, string? GlobalEmail = null)
{
    /// <summary>One org, nothing mapped: everything is the default org's.</summary>
    public static OrgRouting Single { get; } = new(OrgDirectory.Single, new Dictionary<string, string>());

    /// <summary>Whether there's more than one org, so dispatch is scoped to orgs at all.</summary>
    public bool Active => Orgs.HasOrgs;

    /// <summary>The orgs from the store (the default one alone when it can't be read) and the repos' orgs from <paramref name="settings"/>.</summary>
    public static OrgRouting Load(JsonStore store, OrchestratorSettings settings, string? globalEmail = null)
    {
        OrgDirectory orgs;
        try { orgs = store.Db.TryLoadOrgs(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { orgs = OrgDirectory.Single; }
        return new(orgs, settings.RepoOrgs, globalEmail);
    }

    /// <summary>The org <paramref name="repo"/>'s tickets belong to: the one it's mapped to (any case) while that org exists, else the default.</summary>
    public string OrgOfRepo(string repo)
    {
        var id = RepoOrgs.TryGetValue(repo, out var exact) ? exact
            : RepoOrgs.FirstOrDefault(kv => kv.Key.Equals(repo, StringComparison.OrdinalIgnoreCase)).Value;
        return id is not null && Orgs.Find(id.Trim()) is { } org ? org.Id : Organization.DefaultId;
    }

    /// <summary>The org of the Claude login <paramref name="email"/>; null (or the global login's own email) is the global login,
    /// named in a member row as "global" or by its email.</summary>
    public string OrgOfLogin(string? email)
    {
        var global = email is null || (GlobalEmail is not null && TicketPins.Same(email, GlobalEmail));
        if (!global) return Orgs.OrgOfClaude(email);
        var byGlobal = Orgs.Members.FirstOrDefault(m => m.IsClaude && m.Login.Length == 0);
        if (byGlobal is not null) return byGlobal.OrgId;
        return GlobalEmail is null ? Organization.DefaultId : Orgs.OrgOfClaude(GlobalEmail);
    }

    /// <summary>The org <paramref name="item"/> is dispatched in: its assignee's when it's pinned to a user, else its repo's.</summary>
    public string OrgOf(QueueItem item) => item.Assignee is { } who ? OrgOfLogin(who) : OrgOfRepo(item.Repo);

    /// <summary>Whether a CLI on <paramref name="login"/> may be handed <paramref name="item"/>: always with one org; else only a
    /// ticket of the login's own org (a pinned ticket is left to <see cref="TicketPins.MayTake"/>).</summary>
    public bool MayTake(QueueItem item, string? login) => !Active || item.IsPinned || OrgOf(item) == OrgOfLogin(login);

    /// <summary>The org's name, for what's said ("Sales"), else its id.</summary>
    public string NameOf(string orgId) => Orgs.Find(orgId)?.Name ?? orgId;
}
