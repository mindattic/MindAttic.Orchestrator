using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// One workspace snapshot as an org sees it (M.ORCH-T655), as <see cref="LoginScope"/> splits it per login: only its
/// members' sessions, Launcher tabs and agents, and only their logins' pace (<see cref="Snapshot.Accounts"/>); the global
/// login's meter only while the global login is the org's. Tickets stay the workspace's, but a ticket claimed by another
/// org's CLI isn't in this org's queue. With one org (<see cref="OrgDirectory.HasOrgs"/> false) everything is unchanged.
/// </summary>
public static class OrgScope
{
    public static Snapshot For(Snapshot all, OrgDirectory orgs, string orgId, TimeSpan workingWindow, TimeSpan waitingWindow)
    {
        if (!orgs.HasOrgs) return all;
        bool Mine(string? email) => orgs.OrgOfClaude(email) == orgId;
        var scoped = LoginScope.Keep(all, Mine, workingWindow, waitingWindow);
        scoped = scoped with { Accounts = [.. all.Accounts.Where(a => Mine(a.Email))] };
        // The global login's meter and pace are the snapshot's own: another org's, when the global login isn't this org's.
        return Mine(null) ? scoped : scoped with { Budget = null, Pace = null, Utilization = null, Account = null };
    }

    /// <summary>The login a claim's CLI runs on (its session's per-CLI login; null the global login or not known).</summary>
    public static string? LoginOf(Snapshot all, Assignment assignment) => LoginScope.LoginOf(all, assignment.CliSession);

    /// <summary>Whether <paramref name="orgId"/>'s queue lists <paramref name="item"/>: every unclaimed ticket, and a claimed one
    /// only when the CLI holding it is one of the org's.</summary>
    public static bool Lists(Snapshot all, OrgDirectory orgs, string orgId, QueueItem item) =>
        !orgs.HasOrgs || item.Assignment is not { } a || orgs.OrgOfClaude(LoginOf(all, a)) == orgId;

    /// <summary>What the org switcher says of an org: its people and logins ("3 people · 4 logins · 1 Codex").</summary>
    public static string Summary(OrgDirectory orgs, string orgId, int tabs)
    {
        var members = orgs.MembersOf(orgId);
        var people = members.Select(m => m.Person).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var others = members.Where(m => !m.IsClaude).GroupBy(m => m.Provider).Select(g => $"{g.Count()} {g.Key}");
        var logins = Math.Max(tabs, members.Count(m => m.IsClaude));
        return string.Join(" · ", new[]
        {
            people > 0 ? $"{people} {(people == 1 ? "person" : "people")}" : null,
            $"{logins} login{(logins == 1 ? "" : "s")}",
        }.OfType<string>().Concat(others));
    }
}
