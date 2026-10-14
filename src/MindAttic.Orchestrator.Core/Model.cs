using MindAttic.Workflow.Tickets;

namespace MindAttic.Orchestrator;

public enum RepoState
{
    /// <summary>No session and no recent activity.</summary>
    Dormant,
    /// <summary>A session is open but hasn't written for a while: it's waiting on you.</summary>
    Waiting,
    /// <summary>An agent wrote to its transcript within the working window.</summary>
    Working,
}

/// <summary>A Launcher-hosted agent tab, found by its <c>mindattic-host-{provider}-{pid}</c> pipe.</summary>
public sealed record HostSession(string Provider, int ProcessId, string PipeName, string? WorkingDirectory);

/// <summary>Token counts as the API reports them.</summary>
public readonly record struct TokenUsage(long Input, long CacheCreation, long CacheRead, long Output)
{
    public long Total => Input + CacheCreation + CacheRead + Output;

    /// <summary>Tokens that weren't served from cache: the part that drives rate limits hardest.</summary>
    public long Fresh => Input + CacheCreation + Output;

    public static TokenUsage operator +(TokenUsage a, TokenUsage b) =>
        new(a.Input + b.Input, a.CacheCreation + b.CacheCreation, a.CacheRead + b.CacheRead, a.Output + b.Output);
}

/// <summary>One API response's token usage.</summary>
public readonly record struct UsageEvent(DateTime AtUtc, TokenUsage Usage);

/// <summary>One Claude Code session as seen through its transcript files.</summary>
public sealed record TranscriptSession(string SessionId, string Cwd, DateTime LastWriteUtc, TokenUsage UsageToday, string? Model)
{
    /// <summary>Every response in the reader's history window, oldest first.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<UsageEvent> Events { get; init; } = [];

    public TokenUsage UsageSince(DateTime utc) =>
        Events.Where(e => e.AtUtc >= utc).Aggregate(default(TokenUsage), (sum, e) => sum + e.Usage);
}

public sealed record RepoStatus(
    string Name,
    string Path,
    RepoState State,
    IReadOnlyList<HostSession> Hosts,
    IReadOnlyList<TranscriptSession> Sessions,
    IReadOnlyList<Ticket> OpenTickets,
    DateTime? LastActivityUtc,
    TokenUsage UsageToday);

public sealed record Snapshot(DateTime TakenUtc, IReadOnlyList<RepoStatus> Repos, IReadOnlyList<string> Warnings)
{
    public Budget.BudgetStatus? Budget { get; init; }

    public TokenUsage UsageToday => Repos.Aggregate(default(TokenUsage), (sum, r) => sum + r.UsageToday);
    public int Working => Repos.Count(r => r.State == RepoState.Working);
    public int Waiting => Repos.Count(r => r.State == RepoState.Waiting);
    public int LiveHosts => Repos.Sum(r => r.Hosts.Count);
    public int OpenTickets => Repos.Sum(r => r.OpenTickets.Count);
}
