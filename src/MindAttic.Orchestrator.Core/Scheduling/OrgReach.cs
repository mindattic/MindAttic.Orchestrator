using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// An org's tickets reach its members when none of them runs /allocate (M.ORCH-T758, after M.ORCH-T753). Org dispatch hands an
/// /allocate CLI only its login's org's tickets, so a ticket in a repo an /allocate CLI works, but of an org none of the
/// /allocate CLIs' logins is in, waited for one of its members to turn /allocate on. Each tick, for each such org (one ticket
/// per org per tick, and none sooner than <see cref="Spacing"/> after its last), the next workable one is started in a new
/// Launcher tab in its repo's checkout on main (<see cref="Dispatcher"/>: only on a login of its org with room under its
/// cutoffs, the API key only while API billing is on), as an orphaned ticket is. Only where you turned /allocate on for its
/// repo, never while allocate is paused, quiet hours run or the window is at its stop; with one org it does nothing.
/// </summary>
public sealed class OrgReach(JsonStore store)
{
    /// <summary>The least time between two tabs opened for the same org.</summary>
    public static readonly TimeSpan Spacing = TimeSpan.FromMinutes(5);

    /// <summary>Opens a new tab on a ticket in its repo's checkout with the prompt given (seam); null opens none.</summary>
    internal Func<QueueItem, string, Dispatcher.Result>? OpenTab { get; init; }

    /// <summary>Whether a ticket is still open right now (seam; <see cref="Allocate.StillOpen"/>).</summary>
    internal Func<string, string, bool> StillOpen { get; init; } = Allocate.StillOpen;

    /// <summary>The main line a repo lands on (seam).</summary>
    internal Func<string, string> MainlineOf { get; init; } = Dispatcher.Mainline;

    private readonly Dictionary<string, DateTime> openedUtc = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> said = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="pilots">The live /allocate CLIs and their repos.</param>
    /// <param name="cliLogins">Each /allocate CLI's login (email; null for the global login).</param>
    /// <returns>What's worth a line in the log: each tab opened, and once each, why an org's ticket still waits.</returns>
    public IReadOnlyList<string> Tick(SchedulePlan plan, OrchestratorSettings settings, OrgRouting orgs,
        IReadOnlyDictionary<int, IReadOnlyList<string>> pilots, IReadOnlyDictionary<int, string?> cliLogins, DateTime nowUtc)
    {
        var lines = new List<string>();
        if (!orgs.Active || pilots.Count == 0 || OpenTab is not { } open) return lines;
        if (!settings.Allocate || QuietHours.Active(settings.QuietHours, nowUtc.ToLocalTime()) is not null || plan.Stopped) return lines;

        var withCli = pilots.Keys.Select(p => orgs.OrgOfLogin(cliLogins.GetValueOrDefault(p))).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var named = pilots.Values.SelectMany(r => r).ToList();
        var worked = plan.Worked.Select(w => w.TicketId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var givenUp = OrphanWatch.Load(store).Where(r => r.Open).Select(r => r.TicketId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // In the queue's own order (priority, then prerequisites first), as orchestrator next hands them out.
        var waiting = plan.Queue.Where(q => !q.IsAssigned && !q.IsPinned && q.MayWork && q.BlockedBy.Count == 0 && !q.TooBig &&
            q.RepoPath.Length > 0 && !worked.Contains(q.Id) && !givenUp.Contains(q.Id) && q.Moving is null &&
            !settings.AllocateSkip.Contains(q.Repo, StringComparer.OrdinalIgnoreCase) && !plan.OverRepoCap.Contains(q.Repo) &&
            Allocate.Named(named, q.Repo) && !withCli.Contains(orgs.OrgOf(q)));

        var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var q in waiting)
        {
            var org = orgs.OrgOf(q);
            if (!done.Add(org)) continue; // one per org per tick
            if (openedUtc.TryGetValue(org, out var last) && nowUtc - last < Spacing) continue;
            // With Auto login off a new tab opens on the global login, which is only right when that's a login of the org.
            if (!settings.AutoLogin && orgs.OrgOfLogin(null) != org)
            {
                Say(org, $"org-reach: {orgs.NameOf(org)}'s tickets wait: none of its members runs /allocate, and Auto login is off, so a tab would open on the global login, which isn't its");
                continue;
            }
            if (!StillOpen(q.RepoPath, q.Id)) continue;
            var result = open(q, Allocate.PromptFor(q, q.RepoPath, MainlineOf(q.RepoPath)));
            if (!result.Ok)
            {
                Say(org, $"org-reach: {orgs.NameOf(org)}'s {q.Id} waits: {result.Message}");
                continue;
            }
            openedUtc[org] = nowUtc;
            said.Remove(org);
            lines.Add($"org-reach: none of {orgs.NameOf(org)}'s members runs /allocate, so {q.Id} in {q.Repo} started in a new tab on one of their logins: {result.Message}");
        }
        return lines;

        void Say(string org, string line)
        {
            if (said.GetValueOrDefault(org) == line) return;
            said[org] = line;
            lines.Add(line);
        }
    }
}
