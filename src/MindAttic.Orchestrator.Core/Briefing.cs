using MindAttic.Orchestrator.Scheduling;

namespace MindAttic.Orchestrator;

/// <summary>"While you were away": one paragraph on what happened since the dashboard was last open and what's waiting now.</summary>
public static class Briefing
{
    /// <summary>Away for less than this isn't worth a briefing.</summary>
    public static readonly TimeSpan MinAway = TimeSpan.FromHours(1);

    public static string? Compose(Snapshot snapshot, SchedulePlan plan, DateTime sinceUtc)
    {
        var away = snapshot.TakenUtc - sinceUtc;
        if (away < MinAway) return null;
        var parts = new List<string>();

        var spent = snapshot.Repos.SelectMany(r => r.Sessions).Sum(s => s.UsageSince(sinceUtc).Fresh);
        var busiest = snapshot.Repos.Select(r => (r.Name, Fresh: r.Sessions.Sum(s => s.UsageSince(sinceUtc).Fresh)))
            .Where(x => x.Fresh > 0).OrderByDescending(x => x.Fresh).Take(3).ToList();
        parts.Add(spent == 0 ? "Nothing ran."
            : $"{Formatting.Tokens(spent)} uncached went by, mostly in {string.Join(", ", busiest.Select(b => $"{b.Name} ({Formatting.Tokens(b.Fresh)})"))}.");

        var walls = snapshot.Budget?.Calibration?.Observations.Count(o => o.HitUtc >= sinceUtc) ?? 0;
        if (walls > 0) parts.Add($"You hit a usage limit {walls} time{(walls == 1 ? "" : "s")}.");

        var finished = snapshot.Costs.Tickets.Where(t => t.LastUtc >= sinceUtc).Select(t => t.TicketId).ToList();
        if (plan.Finished.Count > 0) parts.Add($"{string.Join(", ", plan.Finished.Select(a => a.TicketId))} closed.");
        else if (finished.Count > 0) parts.Add($"Dispatched work moved on {string.Join(", ", finished.Take(5))}.");

        var waiting = snapshot.Repos.Where(r => r.State == RepoState.Waiting).Select(r => r.Name).ToList();
        if (waiting.Count > 0) parts.Add($"Waiting on you: {string.Join(", ", waiting.Take(5))}{(waiting.Count > 5 ? $" and {waiting.Count - 5} more" : "")}.");
        if (plan.Stale.Count > 0) parts.Add($"{string.Join(", ", plan.Stale.Select(q => q.Id))} went stale and {(plan.Stale.Count == 1 ? "is" : "are")} back in the queue.");
        if (Activity.Watchdog.Stuck(snapshot) is { Count: > 0 } stuck) parts.Add($"A session in {string.Join(", ", stuck.Select(x => x.Repo).Distinct())} looks stuck.");
        if (snapshot.Pace is { IsPaced: true } pace) parts.Add($"Now: {pace.Note}");

        return $"Since {sinceUtc.ToLocalTime():ddd h:mm tt} ({Formatting.Span(away)} away): " + string.Join(" ", parts);
    }
}
