namespace MindAttic.Orchestrator.Budget;

/// <summary>What one session spent on one ticket (uncached tokens), as far as its transcript showed. <paramref name="OwnShare"/>:
/// a CLI's cost worked out without its ticketed subagents' tokens; one stored before M.ORCH-T252 has it false (M.ORCH-T344).
/// <paramref name="Model"/>: the model family it ran on ("sonnet", "opus", ...; M.ORCH-T563), null when not known.</summary>
public sealed record SessionCost(string SessionId, string TicketId, string? Type, string Repo, long Fresh, DateTime LastUtc, bool OwnShare = false,
    string? Model = null);

/// <summary>A ticket's cost: every session that worked on it, added up. <paramref name="Model"/>: the model that spent the most
/// on it (an Opus review of a Sonnet ticket's diff leaves it a Sonnet ticket), null when none is known.</summary>
public sealed record TicketCost(string TicketId, string? Type, string Repo, long Fresh, DateTime LastUtc, int Sessions, string? Model = null);

/// <summary>What the completed tickets of one model cost: how many, in all, and per ticket (M.ORCH-T563).</summary>
public sealed record ModelCost(string Model, int Tickets, long Fresh)
{
    public long PerTicket => Tickets == 0 ? 0 : Fresh / Tickets;
}

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
            g.Sum(s => s.Fresh), g.Max(s => s.LastUtc), g.Count(),
            g.Where(s => s.Model is not null).GroupBy(s => s.Model!, StringComparer.Ordinal)
                .OrderByDescending(m => m.Sum(s => s.Fresh)).ThenBy(m => m.Key, StringComparer.Ordinal).Select(m => m.Key).FirstOrDefault()))];

    /// <summary>Cost per completed ticket by model, for <paramref name="tickets"/> (those finished in the period asked about),
    /// most tickets first; a ticket with no model known isn't counted.</summary>
    public static IReadOnlyList<ModelCost> ByModel(IEnumerable<TicketCost> tickets) =>
        [.. tickets.Where(t => t.Model is not null).GroupBy(t => t.Model!, StringComparer.Ordinal)
            .Select(g => new ModelCost(g.Key, g.Count(), g.Sum(t => t.Fresh)))
            .OrderByDescending(m => m.Tickets).ThenBy(m => m.Model, StringComparer.Ordinal)];

    /// <summary>Folds what the transcripts show now into the stored history.</summary>
    public static List<SessionCost> Merge(IEnumerable<SessionCost> stored, IEnumerable<SessionCost> observed, DateTime nowUtc,
        IEnumerable<string>? unticketed = null)
    {
        var byId = new Dictionary<string, SessionCost>(StringComparer.Ordinal);
        foreach (var s in stored) byId[s.SessionId] = s;
        foreach (var id in unticketed ?? []) byId.Remove(id);
        var seen = observed.ToList();
        // What a session's ticketed subagents are charged now: a ticketed CLI's cost stored before M.ORCH-T252 (not marked as
        // its own share) was its whole session, theirs included. Its own share plus theirs covering that total means the stored
        // one counted them twice, so its own share replaces it (M.ORCH-T344).
        var subagents = seen.Where(s => s.SessionId.Contains('/')).GroupBy(s => s.SessionId[..s.SessionId.IndexOf('/')], StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.Fresh), StringComparer.Ordinal);
        foreach (var s in seen)
            if (!byId.TryGetValue(s.SessionId, out var had) || s.Fresh > had.Fresh ||
                !had.OwnShare && s.OwnShare && subagents.TryGetValue(s.SessionId, out var theirs) && s.Fresh + theirs >= had.Fresh)
                byId[s.SessionId] = s with { Type = s.Type ?? had?.Type, Model = s.Model ?? had?.Model };
            // One stored before costs kept their model (M.ORCH-T563) learns it from its transcript while that's still read.
            else if (had.Model is null && s.Model is not null)
                byId[s.SessionId] = had with { Model = s.Model };
        return [.. byId.Values.Where(s => s.LastUtc >= nowUtc - Keep).OrderBy(s => s.LastUtc).ThenBy(s => s.SessionId, StringComparer.Ordinal)];
    }

    /// <summary>The sessions that were dispatched on a ticket, with what they've spent so far. A subagent handed its own
    /// ticket (an /allocate handout) is charged to that ticket alone, keyed <c>&lt;session&gt;/&lt;agent&gt;</c>; the CLI that
    /// started it is charged only for the rest, and only when it was itself dispatched on a ticket (M.ORCH-T252).</summary>
    public static IEnumerable<SessionCost> Observe(IEnumerable<TranscriptSession> sessions, Func<string?, string> repoOf)
    {
        foreach (var s in sessions)
        {
            foreach (var sub in s.TicketedSubagents.Where(a => a.Events.Count > 0))
                yield return new SessionCost(SubagentKey(s.SessionId, sub.AgentId), sub.TicketId, sub.TicketType, repoOf(sub.Cwd),
                    sub.Events.Sum(e => e.Usage.Fresh), sub.Events[^1].AtUtc, Model: Scheduling.ModelChoice.Family(sub.Model));
            if (s.TicketId is null) continue;
            var theirs = s.TicketedSubagents.SelectMany(a => a.Events).ToHashSet();
            var own = s.Events.Where(e => !theirs.Contains(e)).ToList();
            if (own.Count > 0)
                yield return new SessionCost(s.SessionId, s.TicketId, s.TicketType, repoOf(s.Cwd), own.Sum(e => e.Usage.Fresh), own[^1].AtUtc, OwnShare: true,
                    Model: Scheduling.ModelChoice.Family(s.Model));
        }
    }

    /// <summary>The cost key of a session's subagent: its own, so it never merges into its parent's.</summary>
    public static string SubagentKey(string sessionId, string agentId) => $"{sessionId}/{agentId}";

    /// <summary>
    /// The ids of sessions the transcripts show weren't dispatched on a ticket. An entry stored for one was charged when the
    /// reader stamped a subagent's ticket on its whole session (M.ORCH-T252); <see cref="Merge"/> drops it. Only a session read
    /// within the window counts: one older than it is only scanned for whose it is, never for its ticket.
    /// </summary>
    public static IEnumerable<string> Unticketed(IEnumerable<TranscriptSession> sessions) =>
        sessions.Where(s => s.TicketId is null && s.SessionId.Length > 0 && s.Events.Count > 0).Select(s => s.SessionId);

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
