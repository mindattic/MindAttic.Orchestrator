using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Activity;

/// <summary>A subagent (the Agent tool) running right now, seen through its own transcript.</summary>
public sealed record SubagentActivity(string AgentId, string ParentSessionId, string Type, DateTime StartedUtc, DateTime LastWriteUtc, TokenUsage Usage)
{
    /// <summary>What it was asked to do: the first line of its prompt.</summary>
    public string? Task { get; init; }

    /// <summary>Its latest tool call in plain words ("Reading Daemon.cs").</summary>
    public string? Doing { get; init; }
}

/// <summary>
/// Where a subagent's own transcript is: Claude Code writes it to <c>{projectsDir}/{project}/{parentSessionId}/subagents/
/// agent-{agentId}.jsonl</c>, next to its parent's own <c>{parentSessionId}.jsonl</c>. Looked for only in those
/// session folders (a few hundred existence checks), never by walking every transcript under the projects folder,
/// and remembered once found (M.ORCH-T113).
/// </summary>
public static class SubagentTranscripts
{
    /// <summary>How long a subagent whose transcript isn't there yet waits before it's looked for again.</summary>
    internal static readonly TimeSpan RetryMissAfter = TimeSpan.FromSeconds(10);

    private static readonly ConcurrentDictionary<string, string> Found = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, DateTime> Missed = new(StringComparer.OrdinalIgnoreCase);

    public static string? Find(string projectsDir, string agentId) => Find(projectsDir, agentId, DateTime.UtcNow);

    /// <summary><see cref="Find(string, string)"/> as of <paramref name="now"/> (a seam for tests of the retry wait).</summary>
    internal static string? Find(string projectsDir, string agentId, DateTime now)
    {
        var key = projectsDir + "|" + agentId;
        if (Found.TryGetValue(key, out var known))
        {
            if (File.Exists(known)) return known;
            Found.TryRemove(key, out _);
        }
        if (Missed.TryGetValue(key, out var missedAt) && now - missedAt < RetryMissAfter) return null;

        var name = "agent-" + agentId + ".jsonl";
        try
        {
            if (Directory.Exists(projectsDir))
                foreach (var project in Directory.EnumerateDirectories(projectsDir))
                    foreach (var session in Directory.EnumerateDirectories(project))
                    {
                        var candidate = Path.Combine(session, "subagents", name);
                        if (!File.Exists(candidate)) continue;
                        if (Found.Count > 4096) Found.Clear(); // ids never repeat: don't keep every one ever seen
                        Found[key] = candidate;
                        Missed.TryRemove(key, out _);
                        return candidate;
                    }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }

        if (Missed.Count > 4096) Missed.Clear();
        Missed[key] = now;
        return null;
    }
}

/// <summary>
/// Finds subagents that are running right now: alive per the context gate's SubagentStart/Stop marks
/// (<see cref="ContextCycler.AliveSubagentIds"/>), with type and token cost read from each one's own
/// transcript (<see cref="SubagentTranscripts"/>). Each line carries an <c>attributionAgent</c> (the subagent
/// type, e.g. "Explore") and, on assistant lines, the same <c>usage</c> block a top-level transcript line
/// carries. A transcript is tailed: each poll parses only what was appended since the last one, since a
/// long-running subagent's file reaches tens of MB and was re-parsed whole every refresh (M.ORCH-T113).
/// Keep one instance; not thread-safe.
/// </summary>
public sealed class SubagentActivityReader(string projectsDir)
{
    private sealed class Tail
    {
        public long Offset;
        public DateTime? Start, End;
        public string Type = "subagent";
        public TokenUsage Usage;
        public string? Task, Doing;
        public readonly HashSet<string> Counted = new(StringComparer.Ordinal);
    }

    private readonly Dictionary<string, Tail> tails = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A one-off read: nothing is remembered between calls.</summary>
    public static IReadOnlyList<SubagentActivity> Running(ContextCycler cycler, string projectsDir, DateTime nowUtc) =>
        new SubagentActivityReader(projectsDir).Poll(cycler, nowUtc);

    public IReadOnlyList<SubagentActivity> Poll(ContextCycler cycler, DateTime nowUtc)
    {
        var alive = cycler.AliveSubagentIds(nowUtc);
        if (alive.Count == 0 || !Directory.Exists(projectsDir))
        {
            tails.Clear();
            return [];
        }

        var result = new List<SubagentActivity>(alive.Count);
        var reading = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in alive)
        {
            if (SubagentTranscripts.Find(projectsDir, id) is not { } file) continue;
            reading.Add(file);
            if (!tails.TryGetValue(file, out var tail)) tails[file] = tail = new Tail();
            ReadAppended(file, tail);
            if (tail.Start is { } start)
                result.Add(new SubagentActivity(id, Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(file))) ?? "",
                    tail.Type, start, tail.End ?? start, tail.Usage) { Task = tail.Task, Doing = tail.Doing });
        }
        foreach (var gone in tails.Keys.Where(k => !reading.Contains(k)).ToList()) tails.Remove(gone);
        return result;
    }

    /// <summary>Folds in the complete lines written since the last read; a line still being written waits for the next.</summary>
    private static void ReadAppended(string file, Tail tail)
    {
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length < tail.Offset) // truncated or replaced: start over
            {
                tail.Offset = 0;
                tail.Start = tail.End = null;
                tail.Type = "subagent";
                tail.Usage = default;
                tail.Task = tail.Doing = null;
                tail.Counted.Clear();
            }
            if (stream.Length == tail.Offset) return;

            stream.Seek(tail.Offset, SeekOrigin.Begin);
            var buffer = new byte[stream.Length - tail.Offset];
            var total = 0;
            while (total < buffer.Length)
            {
                var n = stream.Read(buffer, total, buffer.Length - total);
                if (n == 0) break;
                total += n;
            }
            var span = buffer.AsSpan(0, total);
            var end = span.LastIndexOf((byte)'\n');
            if (end < 0) return;

            var consumed = span[..(end + 1)];
            while (!consumed.IsEmpty)
            {
                var nl = consumed.IndexOf((byte)'\n');
                var line = consumed[..nl];
                consumed = consumed[(nl + 1)..];
                if (!line.IsEmpty) Apply(line, tail);
            }
            tail.Offset += end + 1;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void Apply(ReadOnlySpan<byte> line, Tail tail)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(Encoding.UTF8.GetString(line)); } catch (JsonException) { return; }
        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return;

            if (root.TryGetProperty("timestamp", out var ts) && ts.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(ts.GetString(), null,
                    System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var at))
            {
                tail.Start ??= at;
                tail.End = at;
            }

            if (root.TryGetProperty("attributionAgent", out var a) && a.ValueKind == JsonValueKind.String && a.GetString() is { Length: > 0 } t)
                tail.Type = t;

            // Its first user line is the prompt it was given; each tool_use block after is what it's doing now.
            if (root.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.Object && msg.TryGetProperty("content", out var content))
            {
                var role = root.TryGetProperty("type", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null;
                if (role == "user" && tail.Task is null)
                {
                    var text = content.ValueKind == JsonValueKind.String ? content.GetString()
                        : content.ValueKind == JsonValueKind.Array ? content.EnumerateArray()
                            .Where(b => b.ValueKind == JsonValueKind.Object && b.TryGetProperty("type", out var bt) && bt.GetString() == "text")
                            .Select(b => b.TryGetProperty("text", out var x) ? x.GetString() : null).FirstOrDefault(x => x is { Length: > 0 })
                        : null;
                    if (text is { Length: > 0 }) tail.Task = ToolCallText.FirstLine(text);
                }
                if (role == "assistant" && content.ValueKind == JsonValueKind.Array)
                    foreach (var block in content.EnumerateArray())
                        if (block.ValueKind == JsonValueKind.Object && block.TryGetProperty("type", out var bt) && bt.GetString() == "tool_use")
                            tail.Doing = ToolCallText.Describe(block.TryGetProperty("name", out var n) ? n.GetString() ?? "?" : "?",
                                block.TryGetProperty("input", out var i) ? i.GetRawText() : "");
            }

            if (root.TryGetProperty("type", out var ty) && ty.ValueKind == JsonValueKind.String && ty.GetString() == "assistant" &&
                root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.Object &&
                m.TryGetProperty("usage", out var u) && u.ValueKind == JsonValueKind.Object &&
                (!m.TryGetProperty("id", out var mid) || tail.Counted.Add(mid.ValueKind == JsonValueKind.String ? mid.GetString() ?? "" : mid.GetRawText())))
            {
                tail.Usage += new TokenUsage(Num(u, "input_tokens"), Num(u, "cache_creation_input_tokens"), Num(u, "cache_read_input_tokens"), Num(u, "output_tokens"));
            }
        }
    }

    private static long Num(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : 0;
}
