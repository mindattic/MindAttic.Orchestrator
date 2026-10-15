namespace MindAttic.Orchestrator.Budget;

/// <summary>What one session spent on one ticket (uncached tokens), as far as its transcript showed.</summary>
public sealed record SessionCost(string SessionId, string TicketId, string? Type, string Repo, long Fresh, DateTime LastUtc);

/// <summary>A ticket's cost: every session that worked on it, added up.</summary>
public sealed record TicketCost(string TicketId, string? Type, string Repo, long Fresh, DateTime LastUtc, int Sessions);

/// <summary>A guess at what a ticket will cost, and what it's based on.</summary>
public sealed record CostEstimate(long Fresh, int Basis, string Of)
{
    public override string ToString() => $"~{Formatting.Tokens(Fresh)} (median of {Basis} {Of})";
}

/// <summary>
/// Learns what tickets cost from the sessions dispatched on them. Each session's total is kept by
/// session (the largest seen, since transcripts age out of the 7-day window), so a ticket's cost
/// survives long after its transcripts do. <see cref="Estimate"/> turns that history into a guess for
/// a new ticket: same repo and type when there are enough, then same type, then everything.
/// </summary>
public sealed record TicketCosts(IReadOnlyList<SessionCost> Sessions)
{
    public const int MinBasis = 3;
    public static readonly TimeSpan Keep = TimeSpan.FromDays(180);

    public IReadOnlyList<TicketCost> Tickets { get; } = [.. Sessions
        .GroupBy(s => s.TicketId, StringComparer.Ordinal)
        .Select(g => new TicketCost(g.Key, g.Select(s => s.Type).FirstOrDefault(t => t is not null), g.First().Repo,
            g.Sum(s => s.Fresh), g.Max(s => s.LastUtc), g.Count()))];

    /// <summary>Folds what the transcripts show now into the stored history.</summary>
    public static List<SessionCost> Merge(IEnumerable<SessionCost> stored, IEnumerable<SessionCost> observed, DateTime nowUtc)
    {
        var byId = new Dictionary<string, SessionCost>(StringComparer.Ordinal);
        foreach (var s in stored) byId[s.SessionId] = s;
        foreach (var s in observed)
            if (!byId.TryGetValue(s.SessionId, out var had) || s.Fresh > had.Fresh)
                byId[s.SessionId] = s with { Type = s.Type ?? had?.Type };
        return [.. byId.Values.Where(s => s.LastUtc >= nowUtc - Keep).OrderBy(s => s.LastUtc)];
    }

    /// <summary>The sessions that were dispatched on a ticket, with what they've spent so far.</summary>
    public static IEnumerable<SessionCost> Observe(IEnumerable<TranscriptSession> sessions, Func<string?, string> repoOf) =>
        sessions
            .Where(s => s.TicketId is not null && s.Events.Count > 0)
            .Select(s => new SessionCost(s.SessionId, s.TicketId!, s.TicketType, repoOf(s.Cwd),
                s.Events.Sum(e => e.Usage.Fresh), s.Events[^1].AtUtc));

    public CostEstimate? Estimate(string repo, string type, string? excludeTicket = null)
    {
        var done = Tickets.Where(t => t.TicketId != excludeTicket).ToList();
        foreach (var (pool, of) in new[]
        {
            (done.Where(t => t.Repo == repo && t.Type == type).ToList(), $"{type} tickets in {repo}"),
            (done.Where(t => t.Type == type).ToList(), $"{type} tickets"),
            (done, "tickets"),
        })
        {
            if (pool.Count < MinBasis && of != "tickets") continue;
            if (pool.Count == 0) return null;
            var sorted = pool.Select(t => t.Fresh).Order().ToList();
            return new CostEstimate(sorted[sorted.Count / 2], sorted.Count, of);
        }
        return null;
    }
}
