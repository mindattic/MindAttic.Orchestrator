using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using MindAttic.Tickets.Data;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// A branch the owner assigned to a user (the Roadmap's Assign to…, M.ORCH-T2395) is worked by that user's login, one CLI per
/// task (M.ORCH-T2397). Every <see cref="OrchestratorSettings.AssignedDispatchCheckSeconds"/>, each open, approved, unclaimed
/// leaf assigned to someone and under a container labelled <see cref="GateLabel"/> (the mark a branch assign leaves) is started
/// in a new tab in its repo's checkout on main on its assignee's login (<see cref="Dispatcher"/>), at most
/// <see cref="OrchestratorSettings.AssignedDispatchMaxPerLogin"/> on one login at once and no more than its pace has room for.
/// A user not signed in, locked, or out of room keeps their tasks waiting, said once each.
/// </summary>
public sealed class AssignedDispatch(JsonStore store)
{
    /// <summary>The label a branch assign puts on its container: its tasks go to the assignee's login, and once none is open its
    /// suite runs before it closes.</summary>
    public const string GateLabel = "gate:tests";

    /// <summary>Opens a new tab on a ticket in its repo's checkout with the prompt given (seam); null opens none.</summary>
    internal Func<QueueItem, string, Dispatcher.Result>? OpenTab { get; init; }

    /// <summary>Whether a ticket is still open right now (seam; <see cref="Allocate.StillOpen"/>).</summary>
    internal Func<string, string, bool> StillOpen { get; init; } = Allocate.StillOpen;

    /// <summary>The main line a repo lands on (seam).</summary>
    internal Func<string, string> MainlineOf { get; init; } = Dispatcher.Mainline;

    /// <summary>Which of the ids given carry <see cref="GateLabel"/> (seam); null reads the ticket database.</summary>
    internal Func<IReadOnlyCollection<string>, IReadOnlySet<string>>? Gated { get; init; }

    private DateTime checkedUtc = DateTime.MinValue;
    private readonly Dictionary<string, string> said = new(StringComparer.OrdinalIgnoreCase);

    /// <returns>What's worth a line in the log: each tab opened, and once each, why a task still waits.</returns>
    public IReadOnlyList<string> Tick(SchedulePlan plan, Snapshot snapshot, OrchestratorSettings settings, string workspaceRoot, DateTime nowUtc)
    {
        var lines = new List<string>();
        if (!settings.AssignedDispatchEnabled || OpenTab is not { } open) return lines;
        if (nowUtc - checkedUtc < OrchestratorSettings.SecondsSpan(settings.AssignedDispatchCheckSeconds, 5)) return lines;
        checkedUtc = nowUtc;
        if (QuietHours.Active(settings.QuietHours, nowUtc.ToLocalTime()) is not null || plan.Stopped) return lines;

        var worked = plan.Worked.Select(w => w.TicketId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var givenUp = OrphanWatch.Load(store).Where(r => r.Open).Select(r => r.TicketId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidates = plan.Queue.Where(q => q.Assignee is not null && !q.IsAssigned && q.MayWork && q.BlockedBy.Count == 0 &&
            q.RepoPath.Length > 0 && !worked.Contains(q.Id) && !givenUp.Contains(q.Id) && q.Moving is null).ToList();
        if (candidates.Count == 0) return lines;
        var ancestors = candidates.ToDictionary(q => q.Id, q => snapshot.PartOfAncestors(q.Id).ToList(), StringComparer.OrdinalIgnoreCase);
        var gated = (Gated ?? (ids => GatedIn(workspaceRoot, ids)))(ancestors.Values.SelectMany(a => a).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        var max = (int)Math.Clamp(double.IsFinite(settings.AssignedDispatchMaxPerLogin) ? settings.AssignedDispatchMaxPerLogin : 10, 1, 64);
        var assignees = TicketPins.Assignees(snapshot);
        var assignments = store.LoadAssignments();

        foreach (var login in candidates.Where(q => ancestors[q.Id].Any(gated.Contains)).GroupBy(q => q.Assignee!, StringComparer.OrdinalIgnoreCase))
        {
            var who = login.Key;
            var pace = TicketPins.Same(who, snapshot.GlobalEmail) ? snapshot.Pace
                : snapshot.Accounts.FirstOrDefault(a => TicketPins.Same(a.Email, who))?.Pace;
            if (pace is { Stop: true })
            {
                Say(who, $"assigned dispatch: {who}'s tasks wait: their pace is at its stop");
                continue;
            }
            // Its own task CLIs on the login: each claim on it of a ticket pinned to this user.
            var running = assignments.Count(a => TicketPins.Same(a.Account, who) && TicketPins.Same(assignees.GetValueOrDefault(a.TicketId), who));
            var room = max - running;
            if (pace is { IsPaced: true } paced) room = Math.Min(room, paced.SpareSessions);
            if (room <= 0)
            {
                Say(who, $"assigned dispatch: {who}'s tasks wait: {running} task CLI{(running == 1 ? "" : "s")} on their login, and no room for another now");
                continue;
            }
            foreach (var q in login)
            {
                if (room <= 0) break;
                if (!StillOpen(q.RepoPath, q.Id)) continue;
                var result = open(q, Allocate.PromptFor(q, q.RepoPath, MainlineOf(q.RepoPath),
                    branch: Allocate.BranchWorkFor(settings, Setup.MachineTopology.Current, store, q)));
                if (!result.Ok)
                {
                    // Not signed in, locked, or claimed elsewhere: the rest of this user's wait for the same reason.
                    Say(who, $"assigned dispatch: {q.Id} waits: {result.Message}");
                    break;
                }
                room--;
                said.Remove(who);
                lines.Add($"assigned dispatch: {q.Id} in {q.Repo} started in a new tab on {who}'s login: {result.Message}");
            }
        }
        return lines;

        void Say(string who, string line)
        {
            if (said.GetValueOrDefault(who) == line) return;
            said[who] = line;
            lines.Add(line);
        }
    }

    /// <summary>The ids among <paramref name="ids"/> labelled <see cref="GateLabel"/> in the workspace's ticket database; none when
    /// it can't be read.</summary>
    private static IReadOnlySet<string> GatedIn(string workspaceRoot, IReadOnlyCollection<string> ids)
    {
        var gated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (ids.Count == 0) return gated;
        try
        {
            using var db = TicketsDb.OpenIfMigrated(workspaceRoot);
            if (db is null) return gated;
            foreach (var id in ids)
                if (db.Labels(id).Contains(GateLabel, StringComparer.OrdinalIgnoreCase)) gated.Add(id);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Data.Common.DbException) { }
        return gated;
    }
}
