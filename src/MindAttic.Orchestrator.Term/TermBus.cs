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

    /// <summary>Writes <paramref name="message"/> to the daemon's inbox; whether it got there. Never throws, and never waits
    /// longer than <paramref name="timeoutMs"/> for the pipe.</summary>
    public static bool TrySend(TermSessionEvent message, string? pipeName = null, int timeoutMs = ConnectTimeoutMs)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName ?? InboxPipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            pipe.Connect(timeoutMs);
            pipe.Write(Encoding.UTF8.GetBytes(Envelope(message, DateTime.UtcNow, Environment.ProcessId) + "\n"));
            pipe.Flush();
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return false; }
    }

    private sealed record Line(
        [property: JsonPropertyName("kind")] string Kind,
        [property: JsonPropertyName("v")] int V,
        [property: JsonPropertyName("at")] DateTime At,
        [property: JsonPropertyName("pid")] int Pid,
        [property: JsonPropertyName("payload")] TermSessionEvent Payload);
}
