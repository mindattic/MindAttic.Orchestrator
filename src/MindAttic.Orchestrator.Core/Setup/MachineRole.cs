using System.Text.Json;
using System.Text.Json.Serialization;

namespace MindAttic.Orchestrator.Setup;

/// <summary>What this machine does for the Orchestrator (M.ORCH-T1473).</summary>
public enum MachineRoleKind
{
    /// <summary>Everything on this one machine, as before: daemon, CLIs, hooks, data synced from the checkout.</summary>
    Standalone,

    /// <summary>The always-on work machine (a VM, an HVD): runs the daemon, every CLI and the gateway, and is the one writer of the
    /// orchestrator's and the ticket database's committed copies.</summary>
    Host,

    /// <summary>The owner's own PC: runs no daemon, no pace hooks and no gateway, never writes the checkout's databases (so a
    /// <c>git pull</c> is never blocked), and shows the host's CLIs through its gateway.</summary>
    Client,
}

/// <summary>
/// This machine's role (M.ORCH-T1473), kept per machine in <see cref="FilePath"/> and never in <c>orchestrator.db</c>, which git
/// carries between machines. <see cref="EnvVar"/> overrides the file's role. A missing or unreadable file is
/// <see cref="MachineRoleKind.Standalone"/>, today's behaviour.
/// </summary>
public sealed record MachineRole
{
    /// <summary>The environment variable whose role, when set, overrides the file's.</summary>
    public const string EnvVar = "MINDATTIC_ORCHESTRATOR_ROLE";

    /// <summary>The name a client gives the host's gateway on its Team Wall.</summary>
    public const string HostRemoteName = "host";

    /// <summary>Host, client or standalone; standalone when nothing says otherwise.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MachineRoleKind Role { get; init; } = MachineRoleKind.Standalone;

    /// <summary>A client's host: its machine name or address, as given to <c>orchestrator machine client</c>.</summary>
    public string? Host { get; init; }

    /// <summary>A client's host gateway, <c>https://host:port</c>.</summary>
    public string? GatewayUrl { get; init; }

    /// <summary>A member client's org member (hub phase 1, M.ORCH-T1640): the email the host invited (<c>orchestrator machine
    /// connect --member</c>). Null on the owner's own client, and on every other role.</summary>
    public string? Member { get; init; }

    /// <summary>When the role was last set (UTC).</summary>
    public DateTime? ChangedUtc { get; init; }

    /// <summary>Whether this machine is a client, watching a host.</summary>
    public bool IsClient => Role == MachineRoleKind.Client;
    /// <summary>Whether this machine is the host, running the daemon, the CLIs and the gateway.</summary>
    public bool IsHost => Role == MachineRoleKind.Host;

    /// <summary>This machine's own orchestrator folder, <c>%LOCALAPPDATA%\MindAttic\MindAttic.Orchestrator</c>, outside git.</summary>
    public static string Directory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MindAttic", "MindAttic.Orchestrator");

    /// <summary>The role file, <c>machine.json</c> in <see cref="Directory"/>.</summary>
    public static string FilePath => Path.Combine(Directory, "machine.json");

    /// <summary>A client's own copy of the orchestrator database, off the git checkout.</summary>
    public static string ClientOrchestratorDb => Path.Combine(Directory, "client", "orchestrator.db");

    /// <summary>A client's own copy of the ticket database, off the git checkout (MindAttic.Tickets reads it from
    /// <c>MINDATTIC_TICKETS_DB</c>).</summary>
    public static string ClientTicketsDb => Path.Combine(Directory, "client", "Tickets.db");

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private sealed record Cached(MachineRole Role, long Ticks);
    private static volatile Cached? cached;

    /// <summary>This machine's role, re-read at most every few seconds, so a change reaches a running dashboard quickly.</summary>
    public static MachineRole Current
    {
        get
        {
            var now = Environment.TickCount64;
            if (cached is { } c && now - c.Ticks < 5000) return c.Role;
            var role = Load(FilePath, Environment.GetEnvironmentVariable(EnvVar));
            cached = new Cached(role, now);
            return role;
        }
    }

    /// <summary>Forgets the cached role, so the next <see cref="Current"/> reads the file (after <see cref="Save()"/>).</summary>
    public static void Refresh() => cached = null;

    internal static MachineRole Load(string path, string? overrideRole)
    {
        var role = new MachineRole();
        try
        {
            if (File.Exists(path)) role = JsonSerializer.Deserialize<MachineRole>(File.ReadAllText(path), Json) ?? role;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        return Parse(overrideRole) is { } forced ? role with { Role = forced } : role;
    }

    /// <summary><c>host</c> (or server, vm), <c>client</c> (or pc, remote) or <c>standalone</c> (or local, both), any case; null
    /// for anything else.</summary>
    public static MachineRoleKind? Parse(string? text) =>
        text?.Trim().ToLowerInvariant() switch
        {
            "host" or "server" or "vm" => MachineRoleKind.Host,
            "client" or "pc" or "remote" => MachineRoleKind.Client,
            "standalone" or "local" or "both" => MachineRoleKind.Standalone,
            _ => null,
        };

    /// <summary>Writes this role to <see cref="FilePath"/>, stamped with the time, and drops the cached role.</summary>
    public void Save()
    {
        Save(FilePath);
        Refresh();
    }

    internal void Save(string path)
    {
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Unique per process (bughunt, M.ORCH-T1538), as every other temp-file writer in this codebase already is (e.g.
        // ExportedState.Write): a fixed name is a latent race if two processes ever call Save concurrently.
        var temp = $"{path}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this with { ChangedUtc = DateTime.UtcNow }, Json));
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>The role in words, with what this machine runs.</summary>
    public override string ToString() => Role switch
    {
        MachineRoleKind.Host => "host (runs the daemon, every CLI and the gateway)",
        MachineRoleKind.Client when !string.IsNullOrWhiteSpace(Member) => $"member client of {Host ?? GatewayUrl ?? "?"} as {Member} (runs nothing; watches the host's org)",
        MachineRoleKind.Client => $"client of {Host ?? GatewayUrl ?? "?"} (runs nothing; watches the host)",
        _ => "standalone (everything on this machine)",
    };
}
