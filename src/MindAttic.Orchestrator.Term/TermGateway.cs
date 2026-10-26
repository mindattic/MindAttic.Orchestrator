using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MindAttic.Orchestrator.Term;

/// <summary>
/// What the team gateway (M.ORCH-T933, <c>MindAttic.Orchestrator.Gateway</c>) writes while it runs, so the daemon that starts it
/// and a local client that wants its certificate pin can find it without asking it: <c>%LOCALAPPDATA%\MindAttic\MindAttic.Orchestrator\gateway\gateway.json</c>.
/// Never holds a secret: the owner key and the token signing key are files of their own beside it.
/// </summary>
public sealed record TermGatewayStatus
{
    [JsonPropertyName("pid")] public int Pid { get; init; }

    /// <summary>When the gateway process started (UTC), to tell it from a reused pid (<see cref="TermSessions.HostStartOf"/>).</summary>
    [JsonPropertyName("processStartUtc")] public DateTime ProcessStartUtc { get; init; }

    /// <summary>The address and port Kestrel listens on.</summary>
    [JsonPropertyName("bind")] public string Bind { get; init; } = "";
    [JsonPropertyName("port")] public int Port { get; init; }

    /// <summary>The SHA-256 of the TLS certificate it serves (hex, no separators): what a client pins.</summary>
    [JsonPropertyName("certSha256")] public string CertSha256 { get; init; } = "";

    /// <summary>The command line it was started with, minus the executable: the daemon restarts it when its settings change this.</summary>
    [JsonPropertyName("config")] public string Config { get; init; } = "";

    [JsonPropertyName("version")] public string Version { get; init; } = "";

    /// <summary>Whether the daemon started it (<see cref="TermGateway.ManagedVariable"/>): the daemon only ever stops one it manages,
    /// never one started by hand.</summary>
    [JsonPropertyName("managed")] public bool Managed { get; init; }
}

/// <summary>The gateway's files and its status (M.ORCH-T933). Nothing here throws on an I/O failure.</summary>
public static class TermGateway
{
    /// <summary>The executable's name.</summary>
    public const string ExeName = "MindAttic.Orchestrator.Gateway.exe";

    /// <summary>Overrides where the daemon looks for the gateway (a dev build).</summary>
    public const string PathVariable = "MINDATTIC_GATEWAY";

    /// <summary>The password of a PFX certificate (<c>--cert-path</c>), read from the environment only: never a setting, an
    /// argument or a log line.</summary>
    public const string CertPasswordVariable = "MINDATTIC_GATEWAY_CERT_PASSWORD";

    /// <summary>Set to 1 in the environment of a gateway the daemon starts.</summary>
    public const string ManagedVariable = "MINDATTIC_GATEWAY_MANAGED";

    /// <summary>The default port.</summary>
    public const int DefaultPort = 7443;

    /// <summary><c>%LOCALAPPDATA%\MindAttic\MindAttic.Orchestrator\gateway</c>: the status, the keys, the dev certificate and the log.</summary>
    public static string DefaultDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MindAttic", "MindAttic.Orchestrator", "gateway");

    public static string StatusPath(string? directory = null) => Path.Combine(directory ?? DefaultDirectory, "gateway.json");

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    /// <summary>Writes the status atomically; false when it couldn't be.</summary>
    public static bool WriteStatus(TermGatewayStatus status, string? directory = null)
    {
        var dir = directory ?? DefaultDirectory;
        var path = StatusPath(dir);
        var temp = path + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(temp, JsonSerializer.Serialize(status, Json), new UTF8Encoding(false));
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            try { File.Delete(temp); } catch (Exception) { }
            return false;
        }
    }

    /// <summary>The status as written; null when there is none or it can't be read.</summary>
    public static TermGatewayStatus? ReadStatus(string? directory = null)
    {
        try
        {
            var path = StatusPath(directory);
            return File.Exists(path) ? JsonSerializer.Deserialize<TermGatewayStatus>(File.ReadAllText(path), Json) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    /// <summary>The running gateway's status: null when none was written or its process is gone (or the pid was reused).</summary>
    public static TermGatewayStatus? Running(string? directory = null) =>
        ReadStatus(directory) is { Pid: > 0 } s && TermSessions.HostStartOf(s.Pid) is { } started
            && (started - s.ProcessStartUtc).Duration() <= TermSessions.StartTolerance ? s : null;

    /// <summary>Deletes the status, as the gateway exits.</summary>
    public static void DeleteStatus(string? directory = null)
    {
        try { File.Delete(StatusPath(directory)); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
