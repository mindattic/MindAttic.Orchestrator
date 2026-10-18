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

    /// <summary>Claude Code's own config, where the signed-in account is kept (<c>oauthAccount.accountUuid</c>).</summary>
    public string ConfigPath { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude.json");

    public string StatePath { get; init; } = Path.Combine(Setup.AgentGate.ClaudeDir, "session-context", FileName);

    private DateTime configStamp;
    private DateTime? switched;

    /// <summary>When the sign-in last moved to another account (the first time one is seen counts, so an install made
    /// while already switched starts clean); null when no account is known (signed out, an API key).</summary>
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
            if (seen?["account"]?.GetValue<string>() == account && seen["since"]?.GetValue<double>() is { } since)
                return switched = DateTime.UnixEpoch.AddSeconds(since);
            Write(account, nowUtc);
            return switched = nowUtc;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
        {
            return switched;
        }
    }

    /// <summary>The signed-in account's id from Claude Code's config, or null.</summary>
    internal static string? Current(string configPath)
    {
        if (!File.Exists(configPath)) return null;
        using var stream = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var doc = JsonDocument.Parse(stream);
        return doc.RootElement.TryGetProperty("oauthAccount", out var o) && o.ValueKind == JsonValueKind.Object &&
               o.TryGetProperty("accountUuid", out var id) && id.ValueKind == JsonValueKind.String && id.GetString() is { Length: > 0 } s
            ? s : null;
    }

    private void Write(string account, DateTime nowUtc)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
        var json = new JsonObject { ["account"] = account, ["since"] = (nowUtc - DateTime.UnixEpoch).TotalSeconds }.ToJsonString();
        var tmp = $"{StatePath}.{Environment.ProcessId}.tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, StatePath, overwrite: true);
    }
}
