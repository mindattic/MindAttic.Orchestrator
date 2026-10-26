using System.Text.Json;
using System.Text.Json.Serialization;

namespace MindAttic.Orchestrator.Status;

/// <summary>
/// One line on the status bus (M.ORCH-T872): <c>{"kind":"usage","v":1,"at":"2026-10-25T12:00:00Z","pid":1234,"payload":{...}}</c>.
/// <list type="bullet">
/// <item><c>kind</c> names what the payload is, and so which subscriber's handler reads it (<see cref="StatusBus.On{T}"/>). A kind a
/// subscriber doesn't know is skipped, so a new kind (M.ORCH-T879's ticket events) never breaks an older dashboard.</item>
/// <item><c>v</c> is that kind's payload version: a compatible change (a field added) keeps it; a breaking one bumps it, and a
/// subscriber skips a version above the one it reads rather than misread it.</item>
/// <item><c>at</c> is when the writer sent it and <c>pid</c> which process did.</item>
/// <item><c>payload</c> is the kind's own record, as JSON (property names read case-insensitively, doubles at full precision).</item>
/// </list>
/// </summary>
public sealed record StatusEnvelope
{
    [JsonPropertyName("kind")] public string Kind { get; init; } = "";
    [JsonPropertyName("v")] public int V { get; init; } = 1;
    [JsonPropertyName("at")] public DateTime AtUtc { get; init; }
    [JsonPropertyName("pid")] public int Pid { get; init; }
    [JsonPropertyName("payload")] public JsonElement Payload { get; init; }

    /// <summary>An envelope of <paramref name="kind"/> carrying <paramref name="payload"/>, from this process now.</summary>
    public static StatusEnvelope Of<T>(string kind, T payload, int v = 1) => new()
    {
        Kind = kind, V = v, AtUtc = DateTime.UtcNow, Pid = Environment.ProcessId,
        Payload = JsonSerializer.SerializeToElement(payload, StatusStream.PayloadJson),
    };

    /// <summary>The payload read as <typeparamref name="T"/>; null when there's none. Throws <see cref="JsonException"/> on one that
    /// isn't a <typeparamref name="T"/>.</summary>
    public T? PayloadAs<T>() where T : class =>
        Payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null ? null : Payload.Deserialize<T>(StatusStream.PayloadJson);
}

/// <summary>
/// A subscriber's dispatch table (M.ORCH-T872): each kind it reads, with the newest payload version it understands and the
/// handler that takes it. Kinds it doesn't list, versions past its own and payloads that won't read are skipped (the last two
/// logged once per kind), and a handler that throws is logged: nothing a writer sends can reach the subscriber's caller.
/// Adding a kind is one <see cref="On{T}"/> call; a second app subscribing (M.ORCH-T879: Tickets.Desktop beside the dashboard)
/// builds its own bus and calls <see cref="StatusStreamClient.RunAsync"/> with it.
/// </summary>
public sealed class StatusBus
{
    private static readonly Serilog.ILogger log = OrchestratorLog.For("StatusStream");
    private readonly Dictionary<string, (int Version, Action<StatusEnvelope> Handle)> handlers = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> warned = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Reads <paramref name="kind"/>'s payloads as <typeparamref name="T"/>, up to version <paramref name="version"/>.</summary>
    public StatusBus On<T>(string kind, Action<StatusEnvelope, T> handler, int version = 1) where T : class
    {
        handlers[kind] = (version, e =>
        {
            if (e.PayloadAs<T>() is { } payload) handler(e, payload);
        });
        return this;
    }

    /// <summary>Whether a handler reads <paramref name="kind"/>.</summary>
    public bool Handles(string kind) => handlers.ContainsKey(kind);

    /// <summary>Hands <paramref name="envelope"/> to its kind's handler. Never throws.</summary>
    public void Dispatch(StatusEnvelope envelope)
    {
        try
        {
            if (!handlers.TryGetValue(envelope.Kind, out var h)) return;
            if (envelope.V > h.Version)
            {
                if (warned.Add(envelope.Kind))
                    log.Warning("Skipping status messages of kind {Kind:l} at version {Version}: this build reads up to {Known}", envelope.Kind, envelope.V, h.Version);
                return;
            }
            h.Handle(envelope);
        }
        catch (JsonException ex)
        {
            if (warned.Add(envelope.Kind)) log.Warning("An unreadable {Kind:l} status payload was skipped: {Message:l}", envelope.Kind, ex.Message);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.Error(ex, "Applying a {Kind:l} status message failed", envelope.Kind);
        }
    }
}
