using System.Text.Json;
using System.Text.Json.Nodes;

namespace MindAttic.Orchestrator.Setup;

/// <summary>
/// Undoes <see cref="AgentGate.Ensure"/> and <see cref="AgentGate.EnsureOtherProviders"/> on uninstall (M.ORCH-T439): the
/// pace and context gate hooks they registered in <c>~/.claude/settings.json</c>, <c>~/.codex/hooks.json</c>,
/// <c>~/.gemini/settings.json</c> and Kimi's <c>config.toml</c>, then the scripts and slash commands they wrote. Uninstall
/// used to leave every hook in place, so each CLI kept running the gates (and saying the daemon was down) with no orchestrator
/// behind them. Only the installer's entries go: an entry is the installer's when its command runs one of the gate scripts,
/// the rule <see cref="AgentGate.AddHook"/> already uses to find its own. Every other hook and setting stays; a file that
/// can't be read is left alone.
/// </summary>
public static class AgentGateRemoval
{
    private static readonly string[] Scripts = [AgentGate.ScriptName, AgentGate.ContextScriptName];

    /// <summary>Returns what it removed, or null when nothing of the orchestrator's was there.</summary>
    public static string? Remove(string? claudeDir = null, string? codexDir = null, string? geminiDir = null, string? kimiConfigPath = null)
    {
        var claude = claudeDir ?? AgentGate.ClaudeDir;
        var did = new List<string>();
        foreach (var (name, path) in new[]
        {
            ("Claude", Path.Combine(claude, "settings.json")),
            ("Codex", Path.Combine(codexDir ?? AgentGate.CodexDir, "hooks.json")),
            ("Gemini", Path.Combine(geminiDir ?? AgentGate.GeminiDir, "settings.json")),
        })
            if (RemoveJsonHooks(path)) did.Add($"{name}: removed the gate hooks from {Path.GetFileName(path)}");
        if (RemoveKimiHooks(kimiConfigPath ?? AgentGate.KimiConfigPath)) did.Add("Kimi: removed the gate hooks from config.toml");

        // The scripts carry "edits are overwritten", so they're the orchestrator's; a command file only while it's still ours word for word.
        foreach (var script in Scripts)
            if (TryDelete(Path.Combine(claude, script))) did.Add("deleted " + script);
        foreach (var (file, body) in new[]
        {
            ("unthrottle.md", AgentGate.UnthrottleCommand), ("throttle.md", AgentGate.ThrottleCommand),
            ("autopilot.md", AgentGate.AutopilotCommand), ("crossrepo.md", AgentGate.CrossRepoCommand),
        })
        {
            var path = Path.Combine(claude, "commands", file);
            try { if (File.Exists(path) && File.ReadAllText(path) == body && TryDelete(path)) did.Add("deleted commands/" + file); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return did.Count > 0 ? string.Join(", ", did) : null;
    }

    private static bool IsOurs(JsonNode? hook) =>
        hook is JsonObject o && o["command"] is JsonValue v && v.TryGetValue<string>(out var command)
        && Scripts.Any(s => command.Contains(s, StringComparison.OrdinalIgnoreCase));

    /// <summary>Takes the gate hooks out of a Claude/Codex/Gemini style <c>{"hooks": {event: [{matcher, hooks: [...]}]}}</c>
    /// file; a matcher group, event or <c>hooks</c> object left empty by that goes too. True when it rewrote the file.</summary>
    internal static bool RemoveJsonHooks(string path)
    {
        if (!File.Exists(path)) return false;
        JsonObject? root;
        try
        {
            root = JsonNode.Parse(File.ReadAllText(path), documentOptions: new JsonDocumentOptions
            {
                AllowDuplicateProperties = false, CommentHandling = JsonCommentHandling.Skip,
            }) as JsonObject;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException) { return false; }
        if (root?["hooks"] is not JsonObject hooks) return false;

        var changed = false;
        foreach (var (hookEvent, node) in hooks.ToList())
        {
            if (node is not JsonArray groups) continue;
            var here = false;
            foreach (var group in groups.OfType<JsonObject>().ToList())
            {
                if (group["hooks"] is not JsonArray inner) continue;
                var ours = inner.Where(IsOurs).ToList();
                if (ours.Count == 0) continue;
                foreach (var hook in ours) inner.Remove(hook);
                if (inner.Count == 0) groups.Remove(group);
                here = true;
            }
            if (here && groups.Count == 0) hooks.Remove(hookEvent);
            changed |= here;
        }
        if (!changed) return false;
        if (hooks.Count == 0) root!.Remove("hooks");
        Write(path, root!.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return true;
    }

    /// <summary>Takes each <c>[[hooks]]</c> table whose command runs a gate script out of Kimi's <c>config.toml</c>, with the
    /// blank line the installer put before it; every other line is kept as it was. True when it rewrote the file.</summary>
    internal static bool RemoveKimiHooks(string path)
    {
        string text;
        try { if (!File.Exists(path)) return false; text = File.ReadAllText(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
        var newline = text.Contains("\r\n") ? "\r\n" : "\n";
        var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
        var changed = false;
        for (var i = 0; i < lines.Count; i++)
        {
            if (!lines[i].TrimStart().StartsWith("[[hooks]]", StringComparison.Ordinal)) continue;
            var end = i + 1;
            while (end < lines.Count && !lines[end].TrimStart().StartsWith('[')) end++;
            var ours = lines.Skip(i + 1).Take(end - i - 1).Any(l =>
                l.TrimStart().StartsWith("command", StringComparison.Ordinal) && Scripts.Any(s => l.Contains(s, StringComparison.OrdinalIgnoreCase)));
            if (!ours) continue;
            var start = i;
            while (start > 0 && lines[start - 1].Trim().Length == 0) start--;
            // The blank lines before it separate it from what came before; kept when another table follows.
            if (end < lines.Count && start > 0) start = i;
            lines.RemoveRange(start, end - start);
            i = start - 1;
            changed = true;
        }
        if (!changed) return false;
        var result = string.Join(newline, lines).TrimEnd('\r', '\n');
        Write(path, result.Length > 0 ? result + newline : "");
        return true;
    }

    private static bool TryDelete(string path)
    {
        try
        {
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    private static void Write(string path, string text)
    {
        var temp = $"{path}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temp, text);
            File.Move(temp, path, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
