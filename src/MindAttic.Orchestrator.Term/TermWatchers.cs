using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MindAttic.Orchestrator.Term;

/// <summary>One remote viewer attached through the team gateway (M.ORCH-T935).</summary>
public sealed record TermWatcher
{
    /// <summary>This attachment's id, unique in the gateway's run.</summary>
    [JsonPropertyName("connection")] public string Connection { get; init; } = "";

    [JsonPropertyName("sessionId")] public string SessionId { get; init; } = "";

    /// <summary>Who: the token's subject.</summary>
    [JsonPropertyName("viewer")] public string Viewer { get; init; } = "";

    /// <summary><c>watch</c> (read-only) or <c>interact</c>.</summary>
    [JsonPropertyName("mode")] public string Mode { get; init; } = "watch";

    /// <summary>The viewer's address.</summary>
    [JsonPropertyName("from")] public string? From { get; init; }

    [JsonPropertyName("sinceUtc")] public DateTime SinceUtc { get; init; }
}

/// <summary>What happened: <see cref="TermWatchersEvent.Event"/>'s values.</summary>
public static class TermWatchersEvents
{
    public const string Attached = "attached";
    public const string Detached = "detached";

    /// <summary>The gateway started or stopped: <see cref="TermWatchersEvent.Watchers"/> is the whole truth (empty).</summary>
    public const string Reset = "reset";
}

/// <summary>
/// A <c>term-watchers</c> (v1) bus message (M.ORCH-T935, docs/app-bus.md): a remote viewer attached to or detached from a hosted
/// session through the team gateway. <see cref="Watchers"/> is every remote viewer attached now, to any session, so a reader
/// replaces what it knew with it.
/// </summary>
public sealed record TermWatchersEvent
{
    [JsonPropertyName("event")] public string Event { get; init; } = "";

    /// <summary>Who attached or detached; null for <see cref="TermWatchersEvents.Reset"/>.</summary>
    [JsonPropertyName("watcher")] public TermWatcher? Watcher { get; init; }

    [JsonPropertyName("watchers")] public List<TermWatcher> Watchers { get; init; } = [];

    [JsonPropertyName("gatewayPid")] public int GatewayPid { get; init; }
}

/// <summary>
/// The <c>term-watchers</c> kind, and <c>watchers.json</c> beside the gateway's status: the gateway rewrites the file on each
/// attach and detach and deletes it as it stops. The bus is the fast path, the file the truth (read only while the gateway
/// runs, <see cref="TermGateway.Running"/>). Neither ever holds a token or a byte of terminal output. Nothing here throws.
/// </summary>
public static class TermWatchersBus
{
    public const string Kind = "term-watchers";
    public const int Version = 1;
    public const string FileName = "watchers.json";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public static string PathIn(string? directory = null) => Path.Combine(directory ?? TermGateway.DefaultDirectory, FileName);

    public static string Envelope(TermWatchersEvent message, DateTime atUtc, int pid) => TermBus.Envelope(Kind, Version, message, atUtc, pid);

    public static TermWatchersEvent? Parse(string line) => TermBus.ParsePayload<TermWatchersEvent>(line, Kind, Version);

    /// <summary>Sends <paramref name="message"/> to the daemon's inbox (50 ms at most); whether it got there.</summary>
    public static bool TrySend(TermWatchersEvent message, string? pipeName = null) =>
        TermBus.TrySendLine(Envelope(message, DateTime.UtcNow, Environment.ProcessId), pipeName);

    /// <summary>Writes the current watchers atomically; false when it couldn't.</summary>
    public static bool Write(IReadOnlyList<TermWatcher> watchers, string? directory = null)
    {
        var dir = directory ?? TermGateway.DefaultDirectory;
        var path = PathIn(dir);
        var temp = path + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(temp, JsonSerializer.Serialize(watchers, Json), new UTF8Encoding(false));
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            try { File.Delete(temp); } catch (Exception) { }
            return false;
        }
    }

    public static void Delete(string? directory = null)
    {
        try { File.Delete(PathIn(directory)); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>The remote viewers attached now: none unless the gateway runs.</summary>
    public static IReadOnlyList<TermWatcher> Current(string? directory = null)
    {
        try
        {
            if (TermGateway.Running(directory) is null) return [];
            var path = PathIn(directory);
            return File.Exists(path) ? JsonSerializer.Deserialize<List<TermWatcher>>(File.ReadAllText(path), Json) ?? [] : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return []; }
    }
}
