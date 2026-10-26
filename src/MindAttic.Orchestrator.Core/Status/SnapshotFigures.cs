using System.Security.Cryptography;
using System.Text.Json;
using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Billing;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Status;

/// <summary>
/// A snapshot's own figures, pushed on the status stream as each one is built (M.ORCH-T872, kind <see cref="Kind"/>): the global
/// login's pace (projected %, rate, target, stop) and budget, the 5-hour graph, each per-CLI login's budget and pace, the agents
/// and CLIs with each one's state, billing, the moves in flight, the counts, and the warnings: every number the dashboard
/// shows from a snapshot. A dashboard lays them over the snapshot it shows (<see cref="Over"/>) without reading the 10 MB
/// published file; only when <see cref="Rest"/> moved (the repos' sessions, tickets and tabs, the queue, the history: what this
/// doesn't carry) does it read the file again. Doubles go at full precision; only the dashboard rounds, for display.
/// </summary>
public sealed record SnapshotFigures
{
    public const string Kind = "figures";
    public const int Version = 1;

    /// <summary>When the snapshot these came from was taken.</summary>
    public DateTime TakenUtc { get; init; }

    /// <summary>A digest of everything in the snapshot this doesn't carry (<see cref="RestOf"/>): the same digest means the
    /// published file has nothing new beyond these figures.</summary>
    public string Rest { get; init; } = "";

    public BudgetStatus? Budget { get; init; }
    public Pace? Pace { get; init; }
    public Utilization? Utilization { get; init; }
    public IReadOnlyList<ProviderUsage> ProviderUsage { get; init; } = [];
    public IReadOnlyList<AccountPace> Accounts { get; init; } = [];
    public IReadOnlyList<AgentBoardRow> Agents { get; init; } = [];
    public IReadOnlyList<MoveLock> Moves { get; init; } = [];
    public BillingStatus? Billing { get; init; }
    public string? Account { get; init; }
    public string? GlobalEmail { get; init; }
    public IReadOnlyDictionary<string, string> OwnLogins { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public FigureCounts Counts { get; init; } = new();

    /// <summary><paramref name="s"/>'s figures, with the digest of the rest.</summary>
    public static SnapshotFigures Of(Snapshot s) => new()
    {
        TakenUtc = s.TakenUtc, Rest = RestOf(s),
        Budget = s.Budget, Pace = s.Pace, Utilization = s.Utilization, ProviderUsage = s.ProviderUsage, Accounts = s.Accounts,
        Agents = s.Agents, Moves = s.Moves, Billing = s.Billing, Account = s.Account, GlobalEmail = s.GlobalEmail,
        OwnLogins = s.OwnLogins, Warnings = s.Warnings, Counts = FigureCounts.Of(s),
    };

    /// <summary><paramref name="shown"/> with these figures laid over it, taken when they were.</summary>
    public Snapshot Over(Snapshot shown) => shown with
    {
        TakenUtc = TakenUtc, Warnings = Warnings,
        Budget = Budget, Pace = Pace, Utilization = Utilization, ProviderUsage = ProviderUsage, Accounts = Accounts, Agents = Agents,
        Moves = Moves, Billing = Billing, Account = Account, GlobalEmail = GlobalEmail,
        OwnLogins = new Dictionary<string, string>(OwnLogins, StringComparer.Ordinal),
    };

    /// <summary>
    /// A digest of <paramref name="s"/> without the figures: its repos (sessions, tabs, open tickets, activity), the queue
    /// (approvals, priorities, assignees, reservations), links, costs and history. Hashed from the same JSON the published file
    /// holds, so anything there that changed changes it; a repo's activity moving does too, so the tiles never lag the file.
    /// </summary>
    internal static string RestOf(Snapshot s)
    {
        var rest = s with
        {
            TakenUtc = default, Warnings = [],
            Budget = null, Pace = null, Utilization = null, ProviderUsage = [], Accounts = [], Agents = [], Moves = [], Billing = null,
            Account = null, GlobalEmail = null, OwnLogins = new Dictionary<string, string>(),
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(rest, StatusStream.PayloadJson);
        return Convert.ToHexString(SHA256.HashData(json), 0, 16);
    }
}

/// <summary>The snapshot's counts (M.ORCH-T872), for a subscriber that shows them without the repos behind them.</summary>
public sealed record FigureCounts
{
    public int Repos { get; init; }
    public int Working { get; init; }
    public int Waiting { get; init; }

    /// <summary>The Launcher tabs open (sessions' slots).</summary>
    public int LiveHosts { get; init; }
    public int OpenTickets { get; init; }

    /// <summary>The live Claude sessions and allocate background agents on the agents board.</summary>
    public int Agents { get; init; }

    /// <summary>The subagents running under them.</summary>
    public int Subagents { get; init; }

    public static FigureCounts Of(Snapshot s) => new()
    {
        Repos = s.Repos.Count(r => !r.Name.StartsWith('(')), Working = s.Working, Waiting = s.Waiting, LiveHosts = s.LiveHosts,
        OpenTickets = s.OpenTickets, Agents = s.Agents.Count, Subagents = s.Agents.Sum(a => a.Subagents.Count),
    };
}
