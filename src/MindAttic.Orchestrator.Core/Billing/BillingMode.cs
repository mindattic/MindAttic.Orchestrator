using System.Text.Json;
using System.Text.Json.Nodes;

namespace MindAttic.Orchestrator.Billing;

/// <summary>The two things every CLI can bill (M.ORCH-T436): the Claude Team login, for everyday tokens, or the Claude API
/// key in the MindAttic Vault keyring, for emergencies only.</summary>
public static class BillingModes
{
    public const string Team = "team";
    public const string Api = "api";

    /// <summary>A typed mode ("team", "api", "claude team", "api key", "key"…) as <see cref="Team"/> or <see cref="Api"/>; null when it's neither.</summary>
    public static string? Parse(string? typed) => typed?.Trim().ToLowerInvariant().Replace('-', ' ').Replace('_', ' ') switch
    {
        "team" or "claude team" or "claude" or "login" or "subscription" => Team,
        "api" or "api key" or "key" or "apikey" => Api,
        _ => null,
    };

    /// <summary>The mode in words: "Claude Team" or "API key".</summary>
    public static string Name(string mode) => mode == Api ? "API key" : "Claude Team";

    /// <summary>The other mode.</summary>
    public static string Other(string mode) => mode == Api ? Team : Api;
}

/// <summary>
/// The billing mode in force: which of <see cref="BillingModes"/> (<paramref name="Mode"/>), when it was set and by what
/// (the dashboard, the CLI, a /login), the claude.ai login signed in then (so a later /login to another can be told), and
/// the CLIs the Launcher has been asked to restart for it, or couldn't be asked.
/// </summary>
public sealed record BillingState(string Mode, DateTime SinceUtc, string? Login, string By)
{
    /// <summary>Sessions whose Launcher tab took the restart-resume request for this mode.</summary>
    public IReadOnlyList<string> Requested { get; init; } = [];

    /// <summary>Sessions whose Launcher tab couldn't be asked (no control pipe: a Launcher from before M.LNCH-T38).</summary>
    public IReadOnlyList<string> Refused { get; init; } = [];

    /// <summary>When the switch to the API key approved the Vault key (null when it was approved already): a CLI started before
    /// then settled on the login at its start, whatever its environment holds, until it's restarted.</summary>
    public DateTime? KeyApprovedUtc { get; init; }
}

/// <summary>
/// <c>~/.claude/session-context/billing.json</c>: the billing mode, where the Launcher reads it to start each CLI on what it
/// says (M.LNCH-T38) and the context gate reads it to tell each CLI what it bills. The orchestrator writes it on a switch;
/// <c>since</c> is epoch seconds so the hooks' Python reads it as it does every other file there.
/// </summary>
public sealed class BillingModeFile(string? path = null)
{
    public const string FileName = "billing.json";

    public string Path { get; } = path ?? System.IO.Path.Combine(Setup.AgentGate.ClaudeDir, "session-context", FileName);

    /// <summary>The mode written; null when there's none (or it can't be read, or names neither mode).</summary>
    public BillingState? Read()
    {
        try
        {
            if (!File.Exists(Path)) return null;
            if (JsonNode.Parse(ReadShared(Path)) is not JsonObject o) return null;
            if (BillingModes.Parse(Text(o, "mode")) is not { } mode) return null;
            var since = o["since"] is JsonValue v && v.GetValueKind() == JsonValueKind.Number ? DateTime.UnixEpoch.AddSeconds(v.GetValue<double>()) : DateTime.MinValue;
            return new BillingState(mode, since, Text(o, "login"), Text(o, "by") ?? "unknown")
            {
                Requested = List(o, "requested"),
                Refused = List(o, "refused"),
                KeyApprovedUtc = o["approved_at"] is JsonValue a && a.GetValueKind() == JsonValueKind.Number ? DateTime.UnixEpoch.AddSeconds(a.GetValue<double>()) : null,
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    public void Write(BillingState state)
    {
        var o = new JsonObject
        {
            ["mode"] = state.Mode,
            ["since"] = Math.Round((state.SinceUtc - DateTime.UnixEpoch).TotalSeconds, 3),
            ["login"] = state.Login,
            ["by"] = state.By,
            ["requested"] = new JsonArray([.. state.Requested.Select(s => (JsonNode)JsonValue.Create(s))]),
            ["refused"] = new JsonArray([.. state.Refused.Select(s => (JsonNode)JsonValue.Create(s))]),
            ["approved_at"] = state.KeyApprovedUtc is { } at ? Math.Round((at - DateTime.UnixEpoch).TotalSeconds, 3) : null,
        };
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var tmp = $"{Path}.{Environment.ProcessId}.tmp";
        File.WriteAllText(tmp, o.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, Path, overwrite: true);
    }

    private static string? Text(JsonObject o, string name) =>
        o[name] is JsonValue v && v.TryGetValue<string>(out var s) && s.Length > 0 ? s : null;

    private static IReadOnlyList<string> List(JsonObject o, string name) =>
        o[name] is JsonArray a ? [.. a.OfType<JsonValue>().Select(v => v.TryGetValue<string>(out var s) ? s : null).OfType<string>()] : [];

    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
