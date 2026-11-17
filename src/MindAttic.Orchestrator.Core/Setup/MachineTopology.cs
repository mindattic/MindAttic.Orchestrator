using System.Net;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Setup;

/// <summary>
/// What part this machine plays in a multi-machine install (hub phase 0, M.ORCH-T1639). <see cref="Standalone"/>,
/// <see cref="Host"/> and <see cref="Client"/> are today's <see cref="MachineRoleKind"/> values; <see cref="MemberClient"/> (hub
/// phase 1, M.ORCH-T1640) is a client connected with a member key; <see cref="Spoke"/> is reserved for a later hub phase
/// (docs/research/hub-and-spoke.md) and nothing selects it yet.
/// </summary>
public enum TopologyKind
{
    /// <summary>Everything on this one machine: today's default install.</summary>
    Standalone,

    /// <summary>The always-on work machine that runs the daemon, every CLI and the gateway (docs/REMOTE_HOST.md).</summary>
    Host,

    /// <summary>The owner's own PC watching its host: runs no daemon, no hooks, no gateway (docs/REMOTE_HOST.md).</summary>
    Client,

    /// <summary>Hub phase 1 (M.ORCH-T1640): an org member's PC connected to a central host with their own member key. A client in
    /// every way (<see cref="MachineTopology.IsClient"/>); only a client whose <see cref="MachineRole.Member"/> is set selects it,
    /// never a <see cref="MachineRoleKind"/> on its own.</summary>
    MemberClient,

    /// <summary>Reserved (hub phase 3): a federated machine that joins a host through Relay. Nothing selects it yet.</summary>
    Spoke,
}

/// <summary>
/// The one read point for "what is this machine?" (hub phase 0, M.ORCH-T1639). Every role-dependent branch (daemon start, pace
/// hooks, the database the store opens, SQL Server, the ticket database and shim, the welcome window, the client's Team Wall)
/// asks this instead of <see cref="MachineRole"/> directly, so later hub phases change one place and a standalone install's
/// behaviour is pinned by tests (MachineTopologyTests). The answers are today's, unchanged; the seam sits at today's function
/// boundaries so M.RLY-T13's Local seams (IClaims, IPolicySource, ...) can slot in behind it.
/// </summary>
public sealed record MachineTopology(TopologyKind Kind)
{
    /// <summary>This machine's topology, from <see cref="MachineRole.Current"/> (re-read at most every few seconds).</summary>
    public static MachineTopology Current => From(MachineRole.Current);

    /// <summary>The topology <paramref name="role"/> means: <see cref="For"/> its kind, except that a client connected as an org
    /// member (<see cref="MachineRole.Member"/>, hub phase 1) is a <see cref="TopologyKind.MemberClient"/>.</summary>
    public static MachineTopology From(MachineRole role) =>
        role.IsClient && !string.IsNullOrWhiteSpace(role.Member) ? new(TopologyKind.MemberClient) : For(role.Role);

    public static MachineTopology For(MachineRoleKind role) => new(role switch
    {
        MachineRoleKind.Host => TopologyKind.Host,
        MachineRoleKind.Client => TopologyKind.Client,
        _ => TopologyKind.Standalone,
    });

    public bool IsStandalone => Kind == TopologyKind.Standalone;
    public bool IsHost => Kind == TopologyKind.Host;

    /// <summary>An org member's own PC connected to the central host with a member key (hub phase 1, M.ORCH-T1640).</summary>
    public bool IsMemberClient => Kind == TopologyKind.MemberClient;

    /// <summary>A machine that watches another one's work rather than doing it: the owner's client (and, reserved, a member's).</summary>
    public bool IsClient => Kind is TopologyKind.Client or TopologyKind.MemberClient;

    /// <summary>Whether this machine runs the daemon (and so starts it at logon and when the dashboard finds it down).</summary>
    public bool RunsDaemon => !IsClient;

    /// <summary>Whether this machine's Claude Code hooks pace work here (installed); a client's are taken out.</summary>
    public bool InstallsPaceHooks => RunsDaemon;

    /// <summary>Whether the first-run welcome window may show; a client's setup is its host's.</summary>
    public bool ShowsWelcome => !IsClient;

    /// <summary>Whether the dashboard is the host's Team Wall instead of the local one.</summary>
    public bool OpensHostTeamWall => IsClient;

    /// <summary>Whether the orchestrator's store is the checkout's committed <c>data/orchestrator.db</c> (which the daemon's git
    /// sync commits); a client keeps its own copy off the checkout so its pulls are never blocked.</summary>
    public bool UsesCheckoutDatabase => !IsClient;

    /// <summary>Whether the daemon's git sync of the orchestrator's and ticket databases runs here.</summary>
    public bool SyncsDatabasesToGit => RunsDaemon;

    /// <summary>Whether a configured SQL Server is the store of record here; a client reads its own SQLite copy.</summary>
    public bool UsesSqlServer => !IsClient;

    /// <summary>Whether the ticket tools use the checkout's <c>Tickets.db</c>; a client uses a mirror of the host's.</summary>
    public bool UsesLocalTicketsDb => !IsClient;

    /// <summary>Whether <c>tickets</c> on this machine may be shimmed to run on the host.</summary>
    public bool UsesTicketsShim => IsClient;

    /// <summary>Whether this machine's daemon starts work-branch builds: only where it runs a daemon and the Vault's
    /// <see cref="OrchestratorSettings.RemoteBranchBuilds"/> is on (off by default).</summary>
    public bool RunsBranchBuilds(bool remoteBranchBuildsSetting) => RunsDaemon && remoteBranchBuildsSetting;

    /// <summary>Whether this machine pulls its host's ready-to-test queue on a Scheduler job (M.ORCH-T1634): only a client, and only
    /// with the same <see cref="OrchestratorSettings.RemoteBranchBuilds"/> switch on here (off by default).</summary>
    public bool FetchesBranchQueue(bool remoteBranchBuildsSetting) => IsClient && remoteBranchBuildsSetting;

    /// <summary>Whether this machine's gateway listens beyond loopback: only where the daemon runs, the gateway is turned on and
    /// bound to a non-loopback address. With default settings this is false on every machine.</summary>
    public bool GatewayListensOnNetwork(GatewaySettings gateway) =>
        RunsDaemon && gateway.Enabled && !IsLoopback(gateway.Bind);

    private static bool IsLoopback(string? bind) =>
        string.IsNullOrWhiteSpace(bind)
        || bind.Trim().Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || (IPAddress.TryParse(bind.Trim(), out var address) && IPAddress.IsLoopback(address));

    public override string ToString() => Kind.ToString().ToLowerInvariant();
}
