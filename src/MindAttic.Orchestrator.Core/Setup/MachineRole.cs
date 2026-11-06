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
    public const string EnvVar = "MINDATTIC_ORCHESTRATOR_ROLE";

    /// <summary>The name a client gives the host's gateway on its Team Wall.</summary>
    public const string HostRemoteName = "host";

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MachineRoleKind Role { get; init; } = MachineRoleKind.Standalone;

    /// <summary>A client's host: its machine name or address, as given to <c>orchestrator machine client</c>.</summary>
    public string? Host { get; init; }

    /// <summary>A client's host gateway, <c>https://host:port</c>.</summary>
    public string? GatewayUrl { get; init; }

    /// <summary>When the role was last set (UTC).</summary>
    public DateTime? ChangedUtc { get; init; }

    public bool IsClient => Role == MachineRoleKind.Client;
    public bool IsHost => Role == MachineRoleKind.Host;

    public static string Directory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MindAttic", "MindAttic.Orchestrator");

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

    /// <summary><c>host</c>, <c>client</c> or <c>standalone</c> (any case); null for anything else.</summary>
    public static MachineRoleKind? Parse(string? text) =>
        text?.Trim().ToLowerInvariant() switch
        {
            "host" or "server" or "vm" => MachineRoleKind.Host,
            "client" or "pc" or "remote" => MachineRoleKind.Client,
            "standalone" or "local" or "both" => MachineRoleKind.Standalone,
            _ => null,
        };

    public void Save()
    {
        Save(FilePath);
        Refresh();
    }

    internal void Save(string path)
    {
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this with { ChangedUtc = DateTime.UtcNow }, Json));
        File.Move(temp, path, overwrite: true);
    }

    public override string ToString() => Role switch
    {
        MachineRoleKind.Host => "host (runs the daemon, every CLI and the gateway)",
        MachineRoleKind.Client => $"client of {Host ?? GatewayUrl ?? "?"} (runs nothing; watches the host)",
        _ => "standalone (everything on this machine)",
    };
}
