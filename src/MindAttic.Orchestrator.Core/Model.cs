using MindAttic.Tickets.Log;

namespace MindAttic.Orchestrator;

/// <summary>What a repo's agents are doing right now, from its transcripts and hosted tabs.</summary>
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
public sealed record HostSession(string Provider, int ProcessId, string PipeName, string? WorkingDirectory)
{
    /// <summary>The Claude login the tab's CLI bills (M.ORCH-T606), as <see cref="Activity.AgentBoardRow.Login"/> words it; null when not known.</summary>
    public string? Login { get; init; }

    /// <summary>The email behind <see cref="Login"/>'s name (M.ORCH-T614), for its tooltip; null when not known.</summary>
    public string? LoginEmail { get; init; }

    /// <summary>The per-CLI login the tab's CLI runs on (its email), null on the global login or when not known: the
    /// dashboard tab it shows on (M.ORCH-T613).</summary>
    public string? OwnLogin { get; init; }

    /// <summary>The tab's title as the Launcher was given it (<c>host --title</c>), which its Windows Terminal tab shows
    /// behind a status glyph (M.ORCH-T619); null when the command line had none.</summary>
    public string? Title { get; init; }
}

/// <summary>Token counts as the API reports them.</summary>
public readonly record struct TokenUsage(long Input, long CacheCreation, long CacheRead, long Output)
{
    /// <summary>Every token counted: input, both cache kinds and output.</summary>
    public long Total => Input + CacheCreation + CacheRead + Output;

    /// <summary>Tokens that weren't served from cache: the part that drives rate limits hardest.</summary>
    public long Fresh => Input + CacheCreation + Output;

    /// <summary>The field-by-field sum of two usages.</summary>
    public static TokenUsage operator +(TokenUsage a, TokenUsage b) =>
        new(a.Input + b.Input, a.CacheCreation + b.CacheCreation, a.CacheRead + b.CacheRead, a.Output + b.Output);
}

/// <summary>One API response's token usage.</summary>
public readonly record struct UsageEvent(DateTime AtUtc, TokenUsage Usage);

/// <summary>The agent providers whose sessions are read, as the names that tag <see cref="TranscriptSession.Provider"/>.</summary>
public static class Providers
{
    /// <summary>Claude Code: the provider budgets and pacing are about.</summary>
    public const string Claude = "Claude";
    /// <summary>OpenAI's Codex CLI.</summary>
    public const string Codex = "Codex";
    /// <summary>Google's Gemini CLI.</summary>
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

/// <summary>Which of Claude's two limits a <see cref="LimitHit"/> ran into: the rolling 5-hour session, or the weekly one.</summary>
public enum LimitKind
{
    /// <summary>The rolling 5-hour window's limit.</summary>
    Session,
    /// <summary>The weekly limit.</summary>
    Weekly,
}

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
    /// <summary>The type (fix, feature, chore...) of <see cref="TicketId"/>'s ticket, if any.</summary>
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
    /// <summary>When <see cref="Doing"/>'s tool call was made.</summary>
    public DateTime? DoingAtUtc { get; init; }

    /// <summary>Set when the session keeps repeating the same tool call: likely stuck in a loop.</summary>
    public Activity.Loop? Loop { get; init; }

    /// <summary>Which agent wrote it: Claude (Claude Code transcripts), Codex or Gemini (their session logs).</summary>
    public string Provider { get; init; } = Providers.Claude;

    /// <summary>Rate-limit walls this session ran into, in the same window as <see cref="Events"/>.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<LimitHit> Hits { get; init; } = [];

    /// <summary>The usage of every response at or after <paramref name="utc"/>, from <see cref="Events"/>.</summary>
    public TokenUsage UsageSince(DateTime utc) =>
        Events.Where(e => e.AtUtc >= utc).Aggregate(default(TokenUsage), (sum, e) => sum + e.Usage);
}

/// <summary>A ticket a transcript filed with <c>tickets ticket new</c> in its current turn: the subagent that did (null for the
/// CLI itself), and when that transcript was last written.</summary>
public sealed record FiledTicket(string TicketId, string? AgentId, DateTime LastWriteUtc);

/// <summary>A session's subagent that works a ticket of its own, with every response it made in the reader's window, and the
/// model it ran on as its transcript names it (M.ORCH-T563), null when it names none.</summary>
public sealed record TicketedSubagent(string AgentId, string TicketId, string? TicketType, string Cwd, IReadOnlyList<UsageEvent> Events, string? Model = null);

/// <summary>One repo as the dashboard shows it: its state, hosted tabs, transcript sessions, open tickets and today's usage.
/// <c>Name</c> and <c>Path</c> identify the repo; <c>Hosts</c> are its Launcher tabs, <c>Sessions</c> its transcript sessions,
/// <c>OpenTickets</c> the tickets still open in it, <c>LastActivityUtc</c> its latest write (null when none) and <c>UsageToday</c>
/// the tokens used today.</summary>
public sealed record RepoStatus(
    string Name,
    string Path,
    RepoState State,
    IReadOnlyList<HostSession> Hosts,
    IReadOnlyList<TranscriptSession> Sessions,
    IReadOnlyList<Ticket> OpenTickets,
    DateTime? LastActivityUtc,
    TokenUsage UsageToday);

/// <summary>One look at every repo and the budget around them: what the daemon publishes, and the dashboard and
/// scheduler read. <c>TakenUtc</c> is when it was taken, <c>Repos</c> each repo's status and <c>Warnings</c> anything that
/// went wrong building it.</summary>
public sealed record Snapshot(DateTime TakenUtc, IReadOnlyList<RepoStatus> Repos, IReadOnlyList<string> Warnings)
{
    /// <summary>The Claude budget: the limit learned, what's used and what's left; null before one is known.</summary>
    public Budget.BudgetStatus? Budget { get; init; }

    /// <summary>Where the budget's use stands against its pace line; null when there's no budget to pace.</summary>
    public Budget.Pace? Pace { get; init; }

    /// <summary>This 5-hour window's utilization against its pace, for the dashboard's graph; null without a real limit.</summary>
    public Budget.Utilization? Utilization { get; init; }

    /// <summary>The current week's utilization against its pace, for the dashboard's weekly graph (M.ORCH-T1403); null without
    /// a weekly reset known yet.</summary>
    public Budget.Utilization? WeeklyUtilization { get; init; }

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

    /// <summary>Session → the per-CLI login it runs on (its email, M.ORCH-T613); a session not here is on the global login.
    /// What splits the snapshot into one dashboard tab per login (<see cref="Sessions.LoginScope"/>).</summary>
    public IReadOnlyDictionary<string, string> OwnLogins { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>The email signed in to the global <c>~/.claude</c> login; null when not known.</summary>
    public string? GlobalEmail { get; init; }

    /// <summary>Repos that share duplicated code (from the last audit): editing them at once risks conflicting fixes.</summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> LinkedRepos { get; init; } = new Dictionary<string, IReadOnlySet<string>>();

    /// <summary>Ticket → the tickets it requires (prerequisite links in the ticket database).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Requires { get; init; } = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>Repos whose open tickets could not be read (the ticket database locked on a first read): their claims
    /// are not finished and nothing is proposed from them (M.ORCH-T727).</summary>
    public IReadOnlySet<string> UnknownRepos { get; init; } = new HashSet<string>();

    /// <summary>Tickets approved for work in the ticket database's queue. Fixes need no approval; any other open ticket
    /// waits outside the queue until it's here, so no agent works a feature you didn't ask for (M.ORCH-T384).</summary>
    public IReadOnlySet<string> Approved { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Tickets labelled <c>epic</c> in the ticket database (M.ORCH-T1146): never queued or dispatched as work, and a
    /// 'requires' link to one is satisfied once it's <see cref="Approved"/> (started), not only once it closes.</summary>
    public IReadOnlySet<string> Epics { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Tickets parked as waiting on the owner (label <c>waiting-on-owner</c>, M.ORCH-T1425): never handed out as work until the label is removed.</summary>
    public IReadOnlySet<string> OwnerWaiting { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ticket → the tickets it's <c>part-of</c> (prerequisite-free grouping links in the ticket database). An epic
    /// among them gates it exactly as an explicit 'requires' link to that epic would (M.ORCH-T1210): nobody has to remember
    /// to also write a 'requires' link for an epic's children to be held back while the epic itself isn't approved.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> PartOf { get; init; } = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>Ticket → its manual priority in the ticket database's queue; higher runs sooner, missing means 0.</summary>
    public IReadOnlyDictionary<string, long> Priorities { get; init; } = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Open ticket → its graph weight in the ticket database (<c>TicketsDb.GraphWeights</c>, M.TIX-T316): how much other
    /// open work leans on it. Dispatch orders by it after explicit priority (M.ORCH-T1457); missing means 0.</summary>
    public IReadOnlyDictionary<string, long>? GraphWeights { get; init; }

    /// <summary>Ticket → its friendly name in the ticket database (<c>TicketsDb.Names</c>, M.TIX-T313); missing means none.</summary>
    public IReadOnlyDictionary<string, string>? Names { get; init; }

    /// <summary>Ticket → the member it's assigned to, by login email (the ticket database's <c>ticket_work</c>, written by
    /// <c>tickets ticket assign</c> and Tickets Desktop, read through the engine, M.ORCH-T762); missing means unassigned.</summary>
    public IReadOnlyDictionary<string, string> Assignees { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ticket → the uncached tokens reserved for it in the ticket database (<c>tickets ticket reserve</c>, M.ORCH-T762).</summary>
    public IReadOnlyDictionary<string, long> Reservations { get; init; } = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Tickets the orchestrator's last-resort cool-off aborted and put back (M.ORCH-T786, <see cref="Scheduling.AgentAbort"/>):
    /// first in the queue for the next round.</summary>
    public IReadOnlySet<string> Requeued { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="ticket"/> may be worked: a fix, or one approved in the database's queue; never an epic
    /// (M.ORCH-T1146), and never one that's part-of an epic that isn't itself approved yet (M.ORCH-T1210) — a fix is no
    /// exception to that part: nothing in an unapproved epic has started, so there's nothing in it to fix yet either.</summary>
    public bool MayWork(Ticket ticket) => !Epics.Contains(ticket.Id) && !OwnerWaiting.Contains(ticket.Id) &&
        (PartOf.GetValueOrDefault(ticket.Id) ?? []).All(parent => !Epics.Contains(parent) || Approved.Contains(parent)) &&
        (ticket.Type.Equals("fix", StringComparison.OrdinalIgnoreCase) || Approved.Contains(ticket.Id));

    /// <summary>Every live Claude session and allocate background agent, with its running subagents nested under it.</summary>
    public IReadOnlyList<Activity.AgentBoardRow> Agents { get; init; } = [];

    /// <summary>The moves in flight (M.ORCH-T637): each moving CLI and the tickets it holds, locked until it lands.</summary>
    public IReadOnlyList<Sessions.MoveLock> Moves { get; init; } = [];

    /// <summary>Who's billed (Claude Team or the Vault's API key, masked) and what each open Claude CLI bills (M.ORCH-T436).</summary>
    public Billing.BillingStatus? Billing { get; init; }

    /// <summary>The API key person's money (M.ORCH-T906): its credit, what its CLIs spent and the burndown; null when unread.</summary>
    public Billing.ApiSpend? ApiSpend { get; init; }

    /// <summary>Today's token usage across every repo.</summary>
    public TokenUsage UsageToday => Repos.Aggregate(default(TokenUsage), (sum, r) => sum + r.UsageToday);
    /// <summary>How many repos are <see cref="RepoState.Working"/>.</summary>
    public int Working => Repos.Count(r => r.State == RepoState.Working);
    /// <summary>How many repos are <see cref="RepoState.Waiting"/> on you.</summary>
    public int Waiting => Repos.Count(r => r.State == RepoState.Waiting);
    /// <summary>How many Launcher-hosted tabs are open across every repo.</summary>
    public int LiveHosts => Repos.Sum(r => r.Hosts.Count);
    /// <summary>How many tickets are open across every repo.</summary>
    public int OpenTickets => Repos.Sum(r => r.OpenTickets.Count);
}
