using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MindAttic.Orchestrator.Term;

/// <summary>
/// The <c>term-session</c> kind of the Orchestrator's status bus (M.ORCH-T925, docs/app-bus.md): a TermHost announces itself as it
/// starts, changes and ends, by writing one envelope line to the daemon's inbox pipe, which the daemon relays to every
/// subscriber. This library doesn't reference Core, so it writes the envelope itself, the same line Core's
/// <c>StatusEnvelope</c> and the Python hooks write: <c>{"kind":"term-session","v":1,"at":..,"pid":..,"payload":{..}}</c>.
/// Best effort: no daemon, or every inbox listener busy, and the message is dropped at once. The registry files
/// (<see cref="TermSessions"/>) are the truth.
/// </summary>
public static class TermBus
{
    /// <summary>The bus kind.</summary>
    public const string Kind = "term-session";

    /// <summary>The payload version (<see cref="TermSessionEvent"/>).</summary>
    public const int Version = 1;

    /// <summary>How long a send waits for the inbox, as the statusline does (M.ORCH-T892).</summary>
    public const int ConnectTimeoutMs = 50;

    /// <summary>The daemon's inbox, Core's <c>StatusStream.InboxPipeName</c>: <c>mindattic-orchestrator-status-in-&lt;user&gt;</c>,
    /// every character of the user name that isn't a letter or digit a dash.</summary>
    public static string InboxPipeName { get; } = "mindattic-orchestrator-status-in-" +
        new string(Environment.UserName.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());

    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>The envelope line (no newline) announcing <paramref name="message"/>, from process <paramref name="pid"/> at
    /// <paramref name="atUtc"/>.</summary>
    public static string Envelope(TermSessionEvent message, DateTime atUtc, int pid) =>
        JsonSerializer.Serialize(new Line(Kind, Version, atUtc, pid, message), Json);

    /// <summary>The payload of an envelope line of this kind; null when the line is another kind, a newer version or no
    /// envelope at all. For a reader without Core's StatusBus (Core's reads the same payload with
    /// <c>bus.On&lt;TermSessionEvent&gt;(TermBus.Kind, ..., TermBus.Version)</c>).</summary>
    public static TermSessionEvent? Parse(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("kind", out var kind) || kind.GetString() != Kind) return null;
            if (root.TryGetProperty("v", out var v) && v.TryGetInt32(out var version) && version > Version) return null;
            return root.TryGetProperty("payload", out var payload) ? payload.Deserialize<TermSessionEvent>(Json) : null;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) { return null; }
    }

    /// <summary>Writes <paramref name="message"/> to the daemon's inbox; whether the first try got there. Never throws, and never
    /// waits longer than <paramref name="timeoutMs"/> for the pipe: a miss is tried again off the caller's thread
    /// (<see cref="TrySendLineRetrying"/>).</summary>
    public static bool TrySend(TermSessionEvent message, string? pipeName = null, int timeoutMs = ConnectTimeoutMs) =>
        TrySendLineRetrying(Envelope(message, DateTime.UtcNow, Environment.ProcessId), Kind + ":" + message.Id, pipeName, timeoutMs);

    /// <summary>The waits before each retry of a send that missed (M.ORCH-T1001): the bus keeps no copy of these kinds, so one
    /// sent while the daemon restarted, or while every inbox listener was busy, was lost for good.</summary>
    public static readonly IReadOnlyList<TimeSpan> RetryBackoff =
        [TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(600), TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(3)];

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> sends = new(StringComparer.Ordinal);

    /// <summary><see cref="TrySendLine"/>, and when that misses, the same line again after each of <see cref="RetryBackoff"/>'s
    /// waits, on the thread pool, so the caller is never held up. A retry stops once a newer line under the same
    /// <paramref name="key"/> (a session's id, say) was sent, so an old announcement never lands after a newer one. Returns
    /// whether the first try got there. Never throws.</summary>
    public static bool TrySendLineRetrying(string envelope, string key, string? pipeName = null, int timeoutMs = ConnectTimeoutMs)
    {
        var mine = sends.AddOrUpdate(key, 1, (_, n) => n + 1);
        if (TrySendLine(envelope, pipeName, timeoutMs)) return true;
        var retry = Task.Run(async () =>
        {
            foreach (var wait in RetryBackoff)
            {
                await Task.Delay(wait).ConfigureAwait(false);
                if (sends.TryGetValue(key, out var newest) && newest != mine) return;
                if (TrySendLine(envelope, pipeName, timeoutMs)) return;
            }
        });
        lock (retries) retries.Add(retry);
        _ = retry.ContinueWith(t => { lock (retries) retries.Remove(t); }, TaskScheduler.Default);
        return false;
    }

    private static readonly HashSet<Task> retries = [];

    /// <summary>How long a process about to exit waits at most for its sends' retries (<see cref="WaitForRetries"/>): a little over
    /// all of <see cref="RetryBackoff"/>'s waits.</summary>
    public static readonly TimeSpan RetryDrain = TimeSpan.FromSeconds(6);

    /// <summary>Waits, at most <paramref name="max"/> (<see cref="RetryDrain"/> by default), for the retries of the sends that
    /// missed (M.ORCH-T1017): they run on the thread pool, so a process that exits right after its last announcement (a TermHost's
    /// <c>ended</c>, the gateway's watcher reset) would otherwise kill them and the announcement would be lost. Returns at once
    /// when none is pending. Never throws.</summary>
    public static void WaitForRetries(TimeSpan? max = null)
    {
        try
        {
            Task[] pending;
            lock (retries) pending = [.. retries];
            if (pending.Length > 0) Task.WaitAll(pending, max ?? RetryDrain);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { }
    }

    /// <summary>The envelope line of any kind (M.ORCH-T935: <see cref="TermWatchersBus"/> writes <c>term-watchers</c> this way).</summary>
    public static string Envelope<T>(string kind, int version, T payload, DateTime atUtc, int pid) =>
        JsonSerializer.Serialize(new Line<T>(kind, version, atUtc, pid, payload), Json);

    /// <summary>The payload of an envelope line of <paramref name="kind"/> at most <paramref name="version"/>; null otherwise.</summary>
    public static T? ParsePayload<T>(string line, string kind, int version) where T : class
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("kind", out var k) || k.GetString() != kind) return null;
            if (root.TryGetProperty("v", out var v) && v.TryGetInt32(out var found) && found > version) return null;
            return root.TryGetProperty("payload", out var payload) ? payload.Deserialize<T>(Json) : null;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) { return null; }
    }

    /// <summary>Writes one envelope line to the daemon's inbox; whether it got there. Never throws.</summary>
    public static bool TrySendLine(string envelope, string? pipeName = null, int timeoutMs = ConnectTimeoutMs)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName ?? InboxPipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            pipe.Connect(timeoutMs);
            pipe.Write(Encoding.UTF8.GetBytes(envelope + "\n"));
            pipe.Flush();
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return false; }
    }

    private sealed record Line<T>(
        [property: JsonPropertyName("kind")] string Kind,
        [property: JsonPropertyName("v")] int V,
        [property: JsonPropertyName("at")] DateTime At,
        [property: JsonPropertyName("pid")] int Pid,
        [property: JsonPropertyName("payload")] T Payload);

    private sealed record Line(
        [property: JsonPropertyName("kind")] string Kind,
        [property: JsonPropertyName("v")] int V,
        [property: JsonPropertyName("at")] DateTime At,
        [property: JsonPropertyName("pid")] int Pid,
        [property: JsonPropertyName("payload")] TermSessionEvent Payload);
}
