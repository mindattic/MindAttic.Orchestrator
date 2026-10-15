using System.Text.Json;
using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Activity;

/// <summary>A subagent (the Agent tool) running right now, seen through its own transcript.</summary>
public sealed record SubagentActivity(string AgentId, string ParentSessionId, string Type, DateTime StartedUtc, DateTime LastWriteUtc, TokenUsage Usage);

/// <summary>
/// Finds subagents that are running right now: alive per the context gate's SubagentStart/Stop marks
/// (<see cref="ContextCycler.AliveSubagentIds"/>), with type and token cost read from each one's own
/// transcript. Claude Code writes a subagent's turns to <c>{projectsDir}/.../{parentSessionId}/subagents/
/// agent-{agentId}.jsonl</c>, next to its parent's own <c>{parentSessionId}.jsonl</c>; each line carries
/// an <c>attributionAgent</c> (the subagent type, e.g. "Explore") and, on assistant lines, the same
/// <c>usage</c> block a top-level transcript line carries.
/// </summary>
public static class SubagentActivityReader
{
    public static IReadOnlyList<SubagentActivity> Running(ContextCycler cycler, string projectsDir, DateTime nowUtc)
    {
        var alive = cycler.AliveSubagentIds(nowUtc);
        if (alive.Count == 0 || !Directory.Exists(projectsDir)) return [];

        var byAgentId = new Dictionary<string, string>(StringComparer.Ordinal);
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        foreach (var file in Directory.EnumerateFiles(projectsDir, "agent-*.jsonl", options))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (name.Length > "agent-".Length) byAgentId[name["agent-".Length..]] = file;
        }

        var result = new List<SubagentActivity>(alive.Count);
        foreach (var id in alive)
            if (byAgentId.TryGetValue(id, out var file) && Summarize(file, id) is { } s)
                result.Add(s);
        return result;
    }

    private static SubagentActivity? Summarize(string file, string agentId)
    {
        DateTime? start = null, end = null;
        var type = "subagent";
        var usage = default(TokenUsage);
        var counted = new HashSet<string>(StringComparer.Ordinal);
        var parentSessionId = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(file))) ?? "";

        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            while (reader.ReadLine() is { } line)
            {
                JsonDocument doc;
                try { doc = JsonDocument.Parse(line); } catch (JsonException) { continue; }
                using (doc)
                {
                    var root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Object) continue;

                    if (root.TryGetProperty("timestamp", out var ts) && ts.ValueKind == JsonValueKind.String &&
                        DateTime.TryParse(ts.GetString(), null,
                            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var at))
                    {
                        start ??= at;
                        end = at;
                    }

                    if (root.TryGetProperty("attributionAgent", out var a) && a.ValueKind == JsonValueKind.String && a.GetString() is { Length: > 0 } t)
                        type = t;

                    if (root.TryGetProperty("type", out var ty) && ty.GetString() == "assistant" &&
                        root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.Object &&
                        m.TryGetProperty("usage", out var u) && u.ValueKind == JsonValueKind.Object &&
                        (!m.TryGetProperty("id", out var mid) || counted.Add(mid.GetString() ?? "")))
                    {
                        usage += new TokenUsage(Num(u, "input_tokens"), Num(u, "cache_creation_input_tokens"), Num(u, "cache_read_input_tokens"), Num(u, "output_tokens"));
                    }
                }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        return start is null ? null : new SubagentActivity(agentId, parentSessionId, type, start.Value, end ?? start.Value, usage);
    }

    private static long Num(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : 0;
}
