using System.Text.Json;
using System.Text.Json.Nodes;

namespace MindAttic.Orchestrator.Budget;

/// <summary>
/// Which Claude account this machine is signed in to, and when that last changed. Every limit the orchestrator
/// watches is one account's: after a <c>/login</c> to another, the old account's weekly 99% (in
/// <c>claude-usage.json</c>, the stop latch, the samples and the limit hits in the transcripts) kept every prompt refused
/// until the old account's reset, though the new one had room. Whatever was recorded before <see cref="SwitchedAtUtc"/>
/// is the old account's and is left out. The hooks keep the same record (<see cref="FileName"/>), so whichever sees the
/// switch first dates it for both.
/// </summary>
public sealed class ClaudeAccount
{
    public const string FileName = "account.json";

    /// <summary>Claude Code's own config, where the signed-in account is kept (<c>oauthAccount</c>, or an API key).</summary>
    public string ConfigPath { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude.json");

    public string StatePath { get; init; } = Path.Combine(Setup.AgentGate.ClaudeDir, "session-context", FileName);

    private DateTime configStamp;
    private DateTime? switched;

    /// <summary>When the sign-in last moved to another account (the first time one is seen counts, so an install made
    /// while already switched starts clean); null when signed out. A move to or from an API key is a switch too.</summary>
    public DateTime? SwitchedAtUtc(DateTime nowUtc)
    {
        try
        {
            // ~/.claude.json is rewritten often and can be large: read only when it changed.
            var stamp = File.Exists(ConfigPath) ? File.GetLastWriteTimeUtc(ConfigPath) : default;
            if (stamp == configStamp && switched is not null) return switched;
            configStamp = stamp;
            if (Current(ConfigPath) is not { } account) return switched = null;
            var seen = File.Exists(StatePath) ? JsonNode.Parse(File.ReadAllText(StatePath)) : null;
            if (seen?["account"]?.GetValue<string>() is { } was && seen["since"]?.GetValue<double>() is { } since)
            {
                var at = DateTime.UnixEpoch.AddSeconds(since);
                if (was == account) return switched = at;
                // A record from before the organization was part of the key is the same sign-in: keep its date.
                if (account.StartsWith(was + "/", StringComparison.Ordinal) && !was.Contains('/'))
                {
                    Write(account, at);
                    return switched = at;
                }
            }
            Write(account, nowUtc);
            return switched = nowUtc;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
        {
            return switched;
        }
    }

    /// <summary>What an API-key sign-in reads as: no Claude subscription's windows are its.</summary>
    public const string ApiKey = "api-key";

    /// <summary>
    /// The signed-in account from Claude Code's config, or null when signed out. A Claude login is its
    /// <c>oauthAccount.accountUuid</c> and <c>organizationUuid</c> (one person's Max plan and their Team seat, or a Console
    /// login, share the account id but not the limits); an API key (<c>primaryApiKey</c>, or an approved
    /// <c>ANTHROPIC_API_KEY</c> with no Claude login) is <see cref="ApiKey"/>. It used to read as signed out, so moving
    /// to an API key wasn't a switch and the Claude account's stop stayed latched. The pace hooks key the same way.
    /// </summary>
    internal static string? Current(string configPath)
    {
        if (!File.Exists(configPath)) return null;
        using var stream = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return null;
        if (root.TryGetProperty("oauthAccount", out var o) && o.ValueKind == JsonValueKind.Object && Text(o, "accountUuid") is { } id)
            return Text(o, "organizationUuid") is { } org ? $"{id}/{org}" : id;
        if (Text(root, "primaryApiKey") is not null) return ApiKey;
        return root.TryGetProperty("customApiKeyResponses", out var r) && r.ValueKind == JsonValueKind.Object &&
               r.TryGetProperty("approved", out var approved) && approved.ValueKind == JsonValueKind.Array && approved.GetArrayLength() > 0
            ? ApiKey : null;
    }

    private static string? Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s ? s : null;

    private void Write(string account, DateTime nowUtc)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
        var json = new JsonObject { ["account"] = account, ["since"] = (nowUtc - DateTime.UnixEpoch).TotalSeconds }.ToJsonString();
        var tmp = $"{StatePath}.{Environment.ProcessId}.tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, StatePath, overwrite: true);
    }
}
