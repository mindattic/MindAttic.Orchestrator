using MindAttic.Tickets.Log;

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

public static class Providers
{
    public const string Claude = "Claude";
    public const string Codex = "Codex";
    public const string Gemini = "Gemini";

    /// <summary>The Claude CLI's process name: <c>claude</c>, or <c>claude.exe.old.…</c> once its auto-updater has renamed the
    /// running binary to put a new one in place (the CLI keeps running under the old name until it restarts).</summary>
    public static bool IsClaudeProcess(string processName) =>
        processName.Equals("claude", StringComparison.OrdinalIgnoreCase) || processName.StartsWith("claude.exe.", StringComparison.OrdinalIgnoreCase);
}

/// <summary>A limit a provider reports about itself (Codex logs its own used percent per window).</summary>
public sealed record ProviderLimit(string Provider, string Window, double UsedPercent, DateTime ResetsUtc, DateTime SeenUtc);

/// <summary>One provider's uncached usage today and over the last 7 days, plus any limits it reports.</summary>
public sealed record ProviderUsage(string Provider, TokenUsage Today, TokenUsage Week, IReadOnlyList<ProviderLimit> Limits);

public enum LimitKind { Session, Weekly }

/// <summary>A "You've hit your session/weekly limit · resets …" message Claude Code wrote to a transcript.</summary>
public readonly record struct LimitHit(DateTime AtUtc, LimitKind Kind, DateTime ResetUtc);

/// <summary>One Claude Code session as seen through its transcript files.</summary>
public sealed record TranscriptSession(string SessionId, string Cwd, DateTime LastWriteUtc, TokenUsage UsageToday, string? Model)
{
    /// <summary>Every response in the reader's history window, oldest first.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<UsageEvent> Events { get; init; } = [];

    /// <summary>The ticket this session was dispatched for (from its opening prompt or its worktree), if any.</summary>
    public string? TicketId { get; init; }
    public string? TicketType { get; init; }

    /// <summary>Its subagents that were handed a ticket of their own (an /allocate handout, M.ORCH-T252): their responses
    /// are in <see cref="Events"/> (the CLI's limits count them) but their cost is their ticket's, not <see cref="TicketId"/>'s.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<TicketedSubagent> TicketedSubagents { get; init; } = [];

    /// <summary>Tickets it (or one of its subagents) filed in the turn it's still in: an agent that files a ticket and then fixes
    /// it is working it, though no assignment says so (M.ORCH-T255).</summary>
    public IReadOnlyList<FiledTicket> Filing { get; init; } = [];

    /// <summary>Its latest tool call in plain words ("Editing Daemon.cs") and when it was made; null before its first.</summary>
    public string? Doing { get; init; }
    public DateTime? DoingAtUtc { get; init; }

    /// <summary>Set when the session keeps repeating the same tool call: likely stuck in a loop.</summary>
    public Activity.Loop? Loop { get; init; }

    /// <summary>Which agent wrote it: Claude (Claude Code transcripts), Codex or Gemini (their session logs).</summary>
    public string Provider { get; init; } = Providers.Claude;

    /// <summary>Rate-limit walls this session ran into, in the same window as <see cref="Events"/>.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<LimitHit> Hits { get; init; } = [];

    public TokenUsage UsageSince(DateTime utc) =>
        Events.Where(e => e.AtUtc >= utc).Aggregate(default(TokenUsage), (sum, e) => sum + e.Usage);
}

/// <summary>A ticket a transcript filed with <c>tickets ticket new</c> in its current turn: the subagent that did (null for the
/// CLI itself), and when that transcript was last written.</summary>
public sealed record FiledTicket(string TicketId, string? AgentId, DateTime LastWriteUtc);

/// <summary>A session's subagent that works a ticket of its own, with every response it made in the reader's window.</summary>
public sealed record TicketedSubagent(string AgentId, string TicketId, string? TicketType, string Cwd, IReadOnlyList<UsageEvent> Events);

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

    public Budget.Pace? Pace { get; init; }

    /// <summary>This 5-hour window's utilization against its pace, for the dashboard's graph; null without a real limit.</summary>
    public Budget.Utilization? Utilization { get; init; }

    /// <summary>Usage per provider (Claude, Codex, Gemini). Budgets and pacing are Claude's own; the others' are shown, not paced.</summary>
    public IReadOnlyList<ProviderUsage> ProviderUsage { get; init; } = [];

    /// <summary>What dispatched tickets have cost, learned from their sessions; feeds estimates.</summary>
    public Budget.TicketCosts Costs { get; init; } = new([]);

    /// <summary>Uncached usage by day, repo and provider, a year back (the transcripts only reach 7 days).</summary>
    public IReadOnlyList<Budget.DayUsage> History { get; init; } = [];

    /// <summary>The Claude account the budget counts (<see cref="Budget.DayUsage.Account"/>'s key); null when no sign-in is known,
    /// and the budget counts every subscription session.</summary>
    public string? Account { get; init; }

    /// <summary>Each per-CLI Claude login's own budget and pace (M.ORCH-T559), apart from the global login's
    /// <see cref="Budget"/> and <see cref="Pace"/>; empty when no CLI runs on a login of its own.</summary>
    public IReadOnlyList<Budget.AccountPace> Accounts { get; init; } = [];

    /// <summary>Repos that share duplicated code (from the last audit): editing them at once risks conflicting fixes.</summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> LinkedRepos { get; init; } = new Dictionary<string, IReadOnlySet<string>>();

    /// <summary>Ticket → the tickets it requires (prerequisite links in the ticket database).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Requires { get; init; } = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>Tickets approved for work in the ticket database's queue. Fixes need no approval; any other open ticket
    /// waits outside the queue until it's here, so no agent works a feature you didn't ask for (M.ORCH-T384).</summary>
    public IReadOnlySet<string> Approved { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ticket → its manual priority in the ticket database's queue; higher runs sooner, missing means 0.</summary>
    public IReadOnlyDictionary<string, long> Priorities { get; init; } = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="ticket"/> may be worked: a fix, or one approved in the database's queue.</summary>
    public bool MayWork(Ticket ticket) =>
        ticket.Type.Equals("fix", StringComparison.OrdinalIgnoreCase) || Approved.Contains(ticket.Id);

    /// <summary>Every live Claude session and autopilot background agent, with its running subagents nested under it.</summary>
    public IReadOnlyList<Activity.AgentBoardRow> Agents { get; init; } = [];

    /// <summary>Who's billed (Claude Team or the Vault's API key, masked) and what each open Claude CLI bills (M.ORCH-T436).</summary>
    public Billing.BillingStatus? Billing { get; init; }

    public TokenUsage UsageToday => Repos.Aggregate(default(TokenUsage), (sum, r) => sum + r.UsageToday);
    public int Working => Repos.Count(r => r.State == RepoState.Working);
    public int Waiting => Repos.Count(r => r.State == RepoState.Waiting);
    public int LiveHosts => Repos.Sum(r => r.Hosts.Count);
    public int OpenTickets => Repos.Sum(r => r.OpenTickets.Count);
}
