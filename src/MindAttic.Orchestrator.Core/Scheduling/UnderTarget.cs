using MindAttic.Orchestrator.Budget;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// The under-target warning (M.ORCH-T176): an hour before each 5-hour reset, and again at 30 minutes, when the window is
/// on course to end below <see cref="Percent"/> and no CLI has a ticket it would work, says how much would go unused, how
/// many tickets wait for your approval, which are too big for autopilot to start before the reset (M.ORCH-T373) and where
/// /autopilot is on. It only says so: it never starts or approves anything.
/// Each mark is said at most once per window.
/// </summary>
public sealed class UnderTarget
{
    /// <summary>Where a window should end; one on course to end below it is worth a word before the reset.</summary>
    public const double Percent = 90;

    /// <summary>How long before the reset each warning is due: the first an hour out, the second at 30 minutes.</summary>
    public static readonly TimeSpan[] Marks = [TimeSpan.FromHours(1), TimeSpan.FromMinutes(30)];

    private readonly HashSet<(DateTime EndUtc, TimeSpan Mark)> said = [];

    /// <summary>The Needs-you message due now, or null. <paramref name="autopilotClis"/> is the CLIs /autopilot is on in
    /// (by process id) and the repos each works (<see cref="Sessions.ContextCycler.AutopilotClis"/>).</summary>
    public string? Check(Snapshot snapshot, SchedulePlan plan, IReadOnlyDictionary<int, IReadOnlyList<string>> autopilotClis, DateTime nowUtc)
    {
        if (snapshot.Utilization is not { } u) return null;
        var left = u.EndUtc - nowUtc;
        if (left <= TimeSpan.Zero) return null;
        // The tightest mark it's inside: 45 minutes out is the hour's mark, 20 minutes out the half hour's.
        var due = Marks.Where(m => left <= m).OrderBy(m => m).FirstOrDefault();
        if (due == default || said.Contains((u.EndUtc, due))) return null;
        if (plan.Stopped || u.ProjectedEndPercent >= Percent) return null;
        if (Workable(snapshot, plan, autopilotClis).Count > 0) return null;

        said.Add((u.EndUtc, due));
        said.RemoveWhere(x => x.EndUtc < u.EndUtc);
        return Message(snapshot, plan, autopilotClis, u, left);
    }

    /// <summary>The tickets some CLI would work now: open fixes and approved tickets, not claimed, blocked or already being
    /// worked, in a repo that has a CLI open or that /autopilot works, and not estimated past what's left before the reset
    /// (M.ORCH-T373). A too-big one doesn't keep the warning quiet: autopilot won't start it, so the window goes unused unless
    /// a person picks it up, which is what the warning is for; the message names them instead (<see cref="TooBig"/>).</summary>
    internal static IReadOnlyList<QueueItem> Workable(Snapshot snapshot, SchedulePlan plan, IReadOnlyDictionary<int, IReadOnlyList<string>> autopilotClis) =>
        [.. Candidates(snapshot, plan, autopilotClis).Where(q => !q.TooBig)];

    /// <summary>The tickets a CLI could take but autopilot won't start, being estimated past what's left before the reset:
    /// a person in an open CLI still can, or can split them.</summary>
    internal static IReadOnlyList<QueueItem> TooBig(Snapshot snapshot, SchedulePlan plan, IReadOnlyDictionary<int, IReadOnlyList<string>> autopilotClis) =>
        [.. Candidates(snapshot, plan, autopilotClis).Where(q => q.TooBig)];

    private static IEnumerable<QueueItem> Candidates(Snapshot snapshot, SchedulePlan plan, IReadOnlyDictionary<int, IReadOnlyList<string>> autopilotClis)
    {
        var withCli = snapshot.Repos.Where(r => r.Hosts.Count > 0 || r.State != RepoState.Dormant).Select(r => r.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var autopiloted = autopilotClis.Values.SelectMany(r => r).ToList();
        var worked = plan.Worked.Select(w => w.TicketId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return plan.Queue.Where(q => !q.IsAssigned && q.BlockedBy.Count == 0 && q.RepoPath.Length > 0 && !worked.Contains(q.Id) &&
                                     (withCli.Contains(q.Repo) || Autopilot.Named(autopiloted, q.Repo)));
    }

    private static string Message(Snapshot snapshot, SchedulePlan plan, IReadOnlyDictionary<int, IReadOnlyList<string>> autopilotClis,
        Utilization u, TimeSpan left)
    {
        var shortBy = Percent - Math.Max(0, u.ProjectedEndPercent);
        var tokens = snapshot.Budget is { BlockLimitIsPeak: false, BlockLimit: > 0 } b ? (long)(b.BlockLimit * shortBy / 100) : 0;
        var unused = $"{shortBy:0}% of the window" + (tokens > 0 ? $" (about {Formatting.Tokens(tokens)} tokens)" : "");
        var waiting = plan.AwaitingApproval.Count;
        var big = TooBig(snapshot, plan, autopilotClis);
        var none = big.Count == 0 ? "it could work" : "autopilot would start";
        var approval = waiting == 0
            ? $"No ticket waits for your approval, and no CLI has one {none}"
            : $"No CLI has a ticket {none}; {waiting} wait{(waiting == 1 ? "s" : "")} for your approval " +
              $"({Ids(plan.AwaitingApproval)}: orchestrator approve <ID>)";
        if (big.Count > 0)
            approval += $". {big.Count} ({Ids(big)}) {(big.Count == 1 ? "is" : "are")} estimated past what's left before the reset, " +
                        $"so autopilot won't start {(big.Count == 1 ? "it" : "them")}: a person in an open CLI still can, or split {(big.Count == 1 ? "it" : "them")}";
        var autopilot = autopilotClis.Count == 0
            ? "/autopilot is off in every CLI"
            : "/autopilot is on in " + string.Join("; ", autopilotClis.OrderBy(c => c.Key).Select(c =>
                $"CLI {c.Key} for {string.Join(", ", c.Value.Select(r => r == Autopilot.AllRepos ? "every repo" : r))}"));
        return $"The 5-hour window resets at {u.EndUtc.ToLocalTime():h:mm tt} (in {Math.Ceiling(left.TotalMinutes):0} min) and is on course to end at " +
               $"{u.ProjectedEndPercent:0}%, below {Percent:0}%: {unused} would go unused. {approval}. {autopilot}. Nothing is started or approved for you.";
    }

    private static string Ids(IReadOnlyList<QueueItem> items) =>
        string.Join(", ", items.Take(5).Select(q => q.Id)) + (items.Count > 5 ? ", ..." : "");
}
