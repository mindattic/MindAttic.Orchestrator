using System.Text.Json;
using System.Text.Json.Nodes;

namespace MindAttic.Orchestrator.Setup;

/// <summary>
/// The pace gate: a Claude Code PreToolUse hook in <c>~/.claude/settings.json</c> that holds each tool
/// call for the <c>pace_delay_seconds</c> the pacer writes into <c>rl-status.json</c>, so running agents
/// slow down while the burn won't last until the reset, instead of running into the wall. It re-reads the
/// file while it waits, so a hold lifted mid-wait ends at once, and ignores a file nobody refreshes.
/// </summary>
public static class AgentGate
{
    public const string ScriptName = "pace-gate.py";

    /// <summary>Claude Code's limit on the hook; above <see cref="MaxHoldSeconds"/> so a hold is never cut off as a failure.</summary>
    public const int HookTimeoutSeconds = 150;

    public const int MaxHoldSeconds = 120;

    public static string ClaudeDir { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");

    internal static string Script => $$"""
        #!/usr/bin/env python3
        # MindAttic.Orchestrator's pace gate (written by the orchestrator; edits are overwritten).
        # Holds each tool call for rl-status.json's pace_delay_seconds so agents slow down instead of
        # hitting the usage limit. Re-reads the file while waiting: a lifted hold ends at once.
        import json, time
        from datetime import datetime
        from pathlib import Path

        STATUS = Path.home() / ".claude" / "rl-status.json"
        STALE = {{(int)Budget.RateLimitStatusWriter.GateStale.TotalSeconds}}
        MAX_HOLD = {{MaxHoldSeconds}}

        def delay():
            try:
                s = json.loads(STATUS.read_text())
                if s.get("source") != "MindAttic.Orchestrator":
                    return 0
                at = datetime.fromisoformat(s["timestamp"].replace("Z", ""))
                if (datetime.utcnow() - at).total_seconds() > STALE:
                    return 0
                return float(s.get("pace_delay_seconds", 0))
            except Exception:
                return 0

        start = time.monotonic()
        while True:
            waited = time.monotonic() - start
            left = min(delay(), MAX_HOLD) - waited
            if left <= 0:
                break
            time.sleep(min(left, 5))
        """.Replace("\r\n", "\n");

    /// <summary>Writes the script and registers the hook when either is missing or out of date.
    /// Returns what it did, or null when everything was already in place.</summary>
    public static string? Ensure(string? claudeDir = null)
    {
        var dir = claudeDir ?? ClaudeDir;
        Directory.CreateDirectory(dir);
        string? did = null;

        var script = Path.Combine(dir, ScriptName);
        if (!File.Exists(script) || File.ReadAllText(script) != Script)
        {
            File.WriteAllText(script, Script);
            did = "wrote the pace gate";
        }

        var settingsPath = Path.Combine(dir, "settings.json");
        JsonObject? root;
        try { root = File.Exists(settingsPath) ? JsonNode.Parse(File.ReadAllText(settingsPath)) as JsonObject : new JsonObject(); }
        catch (JsonException) { root = null; }
        if (root is null) return did; // unreadable: leave someone else's file alone
        if (AddHook(root, Command(script)))
        {
            var temp = settingsPath + ".tmp";
            File.WriteAllText(temp, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, settingsPath, overwrite: true);
            did = did is null ? "registered the pace gate hook" : did + " and registered its hook";
        }
        return did;
    }

    internal static string Command(string script) => $"python \"{script.Replace('\\', '/')}\"";

    /// <summary>Adds the PreToolUse entry unless one already runs the gate; true when it changed <paramref name="root"/>.</summary>
    internal static bool AddHook(JsonObject root, string command)
    {
        var hooks = root["hooks"] as JsonObject ?? (JsonObject)(root["hooks"] = new JsonObject());
        var pre = hooks["PreToolUse"] as JsonArray ?? (JsonArray)(hooks["PreToolUse"] = new JsonArray());
        foreach (var hook in pre.OfType<JsonObject>().SelectMany(m => (m["hooks"] as JsonArray ?? []).OfType<JsonObject>()))
        {
            if (hook["command"]?.GetValue<string>() is { } c && c.Contains(ScriptName, StringComparison.OrdinalIgnoreCase))
            {
                if (c == command && hook["timeout"]?.GetValue<int>() == HookTimeoutSeconds) return false;
                hook["command"] = command;
                hook["timeout"] = HookTimeoutSeconds;
                return true;
            }
        }
        pre.Add(new JsonObject
        {
            ["matcher"] = "",
            ["hooks"] = new JsonArray(new JsonObject { ["type"] = "command", ["command"] = command, ["timeout"] = HookTimeoutSeconds }),
        });
        return true;
    }
}
