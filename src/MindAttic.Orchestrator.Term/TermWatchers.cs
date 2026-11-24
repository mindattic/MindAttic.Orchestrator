using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MindAttic.Orchestrator.Term;

/// <summary>One remote viewer attached through the team gateway (M.ORCH-T935).</summary>
public sealed record TermWatcher
{
    /// <summary>This attachment's id, unique in the gateway's run.</summary>
    [JsonPropertyName("connection")] public string Connection { get; init; } = "";

    /// <summary>The id of the hosted session the viewer is attached to.</summary>
    [JsonPropertyName("sessionId")] public string SessionId { get; init; } = "";

    /// <summary>Who is watching: the subject of the token they attached with.</summary>
    [JsonPropertyName("viewer")] public string Viewer { get; init; } = "";

    /// <summary><c>watch</c> (read-only) or <c>interact</c>.</summary>
    [JsonPropertyName("mode")] public string Mode { get; init; } = "watch";

    /// <summary>The viewer's remote address; null when it isn't known.</summary>
    [JsonPropertyName("from")] public string? From { get; init; }

    /// <summary>When the viewer attached (UTC).</summary>
    [JsonPropertyName("sinceUtc")] public DateTime SinceUtc { get; init; }
}

/// <summary>What happened: <see cref="TermWatchersEvent.Event"/>'s values.</summary>
public static class TermWatchersEvents
{
    /// <summary>A remote viewer attached to a session.</summary>
    public const string Attached = "attached";
    /// <summary>A remote viewer detached from a session.</summary>
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
    /// <summary>What happened: one of <see cref="TermWatchersEvents"/>.</summary>
    [JsonPropertyName("event")] public string Event { get; init; } = "";

    /// <summary>Who attached or detached; null for <see cref="TermWatchersEvents.Reset"/>.</summary>
    [JsonPropertyName("watcher")] public TermWatcher? Watcher { get; init; }

    /// <summary>Everyone attached right now, after this event.</summary>
    [JsonPropertyName("watchers")] public List<TermWatcher> Watchers { get; init; } = [];

    /// <summary>The pid of the gateway that sent it, so a restarted gateway's list replaces the old one.</summary>
    [JsonPropertyName("gatewayPid")] public int GatewayPid { get; init; }
}

/// <summary>
/// The <c>term-watchers</c> kind, and <c>watchers.json</c> beside the gateway's status: the gateway rewrites the file on each
/// attach and detach and deletes it as it stops. The bus is the fast path, the file the truth (read only while the gateway
/// runs, <see cref="TermGateway.Running"/>). Neither ever holds a token or a byte of terminal output. Nothing here throws.
/// </summary>
public static class TermWatchersBus
{
    /// <summary>The bus message kind these events travel under.</summary>
    public const string Kind = "term-watchers";
    /// <summary>The payload version of <see cref="TermWatchersEvent"/> on the bus.</summary>
    public const int Version = 1;
    /// <summary>The watchers file's name, beside the gateway's status.</summary>
    public const string FileName = "watchers.json";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    /// <summary>The watchers file's full path in <paramref name="directory"/>, or in <see cref="TermGateway.DefaultDirectory"/> when null.</summary>
    public static string PathIn(string? directory = null) => Path.Combine(directory ?? TermGateway.DefaultDirectory, FileName);

    /// <summary>The bus envelope line for <paramref name="message"/>, stamped with <paramref name="atUtc"/> and the sender's <paramref name="pid"/>.</summary>
    public static string Envelope(TermWatchersEvent message, DateTime atUtc, int pid) => TermBus.Envelope(Kind, Version, message, atUtc, pid);

    /// <summary>The event in a bus envelope line; null when the line isn't a <c>term-watchers</c> v1 message.</summary>
    public static TermWatchersEvent? Parse(string line) => TermBus.ParsePayload<TermWatchersEvent>(line, Kind, Version);

    /// <summary>Sends <paramref name="message"/> to the daemon's inbox (50 ms at most, a miss tried again off this thread,
    /// M.ORCH-T1001); whether the first try got there.</summary>
    public static bool TrySend(TermWatchersEvent message, string? pipeName = null) =>
        TermBus.TrySendLineRetrying(Envelope(message, DateTime.UtcNow, Environment.ProcessId), Kind, pipeName);

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

    /// <summary>Deletes the watchers file, as the gateway stops; a failure is ignored.</summary>
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
