namespace MindAttic.Orchestrator.Activity;

/// <summary>How fresh one CLI's activity line is (M.ORCH-T965).</summary>
public enum ActivityFreshness
{
    /// <summary>Its turn, a subagent, a background job or a claim is running.</summary>
    Working,

    /// <summary>Blocked on a permission prompt or a question (<see cref="AgentBoardRow.Asking"/>): never aged out.</summary>
    Asking,

    /// <summary>Its turn ended, recently.</summary>
    Idle,

    /// <summary>Idle, and nothing seen from it for the ActivityStaleMinutes Vault setting or longer: drawn dimmed.</summary>
    Stale,
}

/// <summary>One CLI's activity line: what it's doing (or last did), how fresh that is, and how long ago it was seen.</summary>
public sealed record CliActivityLine(string Text, ActivityFreshness Freshness, string Age, DateTime? LastSeenUtc)
{
    /// <summary>The line's state in words, so it's never only a colour.</summary>
    public string StateWord => Freshness switch
    {
        ActivityFreshness.Working => "Working",
        ActivityFreshness.Asking => "Waiting on you",
        ActivityFreshness.Idle => "Idle",
        _ => "Stale",
    };
}

/// <summary>
/// The activity feed's dashboard consumer (M.ORCH-T965, a follow-up of the docs/spikes/activity-feed.md evaluation, M.ORCH-T937):
/// turns the agent board row the dashboard already reads into one compact line for the Team Wall's per-CLI strip and its
/// phone-width list. Pure: nothing is read or polled here. A session idle for longer than the ActivityStaleMinutes setting ages to
/// <see cref="ActivityFreshness.Stale"/>, so a wall full of forgotten CLIs doesn't read as busy.
/// </summary>
public static class CliActivityLines
{
    /// <summary>The longest a line's text is, so a long tool description never pushes the strip's age off.</summary>
    public const int MaxText = 120;

    /// <summary>The line for <paramref name="row"/> at <paramref name="nowUtc"/>; null when no row is known (a CLI whose session the
    /// dashboard hasn't matched yet, or a teammate's, whose gateway doesn't send activity until the bus kind of M.ORCH-T962).</summary>
    public static CliActivityLine? Describe(AgentBoardRow? row, DateTime nowUtc, TimeSpan staleAfter)
    {
        if (row is null) return null;
        DateTime? seen = row.DoingAtUtc is { } at && at > row.LastActivityUtc ? at : row.LastActivityUtc;
        if (seen == default(DateTime)) seen = null;
        var age = seen is { } s && s <= nowUtc ? nowUtc - s : TimeSpan.Zero;
        var doing = row.Doing is { Length: > 0 } d ? Cut(d) : null;

        if (row.Asking)
            return new(doing is null ? "Waiting on you" : $"Waiting on you · {doing}", ActivityFreshness.Asking, Formatting.Span(age), seen);
        if (row.IsBusy)
            return new(doing ?? Cut(row.StateDetail is { Length: > 0 } detail ? detail : "Working"), ActivityFreshness.Working, Formatting.Span(age), seen);
        var stale = staleAfter > TimeSpan.Zero && age >= staleAfter;
        var text = stale
            ? doing is null ? $"Quiet for {Formatting.Span(age)}" : $"Quiet for {Formatting.Span(age)} · last: {doing}"
            : doing is null ? "Idle" : $"Idle · last: {doing}";
        return new(text, stale ? ActivityFreshness.Stale : ActivityFreshness.Idle, Formatting.Span(age), seen);
    }

    private static string Cut(string text)
    {
        var line = text.ReplaceLineEndings(" ").Trim();
        return line.Length <= MaxText ? line : line[..(MaxText - 1)] + "…";
    }
}
