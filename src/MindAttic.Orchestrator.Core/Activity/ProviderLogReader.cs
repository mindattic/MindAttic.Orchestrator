using System.Globalization;
using System.Text;
using System.Text.Json;

namespace MindAttic.Orchestrator.Activity;

/// <summary>
/// The other agents' session logs, read incrementally like <see cref="TranscriptReader"/>: Codex's
/// <c>~/.codex/sessions/yyyy/MM/dd/rollout-*.jsonl</c> (a <c>session_meta</c> line with the cwd, then a
/// <c>token_count</c> event per turn, carrying Codex's own limit percentages) and Gemini CLI's
/// <c>~/.gemini/tmp/&lt;project&gt;/chats/*.jsonl</c> (the cwd is in <c>.project_root</c>; each reply
/// carries its tokens). Kimi writes no local session log, so it isn't counted.
/// </summary>
public sealed class ProviderLogReader(string codexSessionsDir, string geminiTmpDir)
{
    public static ProviderLogReader Default()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new(Path.Combine(home, ".codex", "sessions"), Path.Combine(home, ".gemini", "tmp"));
    }

    private sealed class FileState
    {
        public long Offset;
        public string Provider = "";
        public string? Cwd;
        public string? SessionId;
        public DateTime LastWriteUtc;
        public long LastTotal;
        public readonly List<UsageEvent> Events = [];
        public readonly HashSet<string> Seen = new(StringComparer.Ordinal);
    }

    private readonly Dictionary<string, FileState> files = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string, string), ProviderLimit> limits = [];
    private DateTime countingSinceUtc;

    /// <summary>The latest limits each provider reported (Codex: its 5-hour and weekly used percent).</summary>
    public IReadOnlyList<ProviderLimit> Limits => [.. limits.Values];

    public IReadOnlyList<TranscriptSession> Poll(DateTime sinceUtc)
    {
        if (sinceUtc != countingSinceUtc) { files.Clear(); countingSinceUtc = sinceUtc; }

        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (path, provider, cwd) in Candidates(sinceUtc))
        {
            present.Add(path);
            if (!files.TryGetValue(path, out var state)) files[path] = state = new FileState { Provider = provider, Cwd = cwd };
            try
            {
                var info = new FileInfo(path);
                if (info.LastWriteTimeUtc < sinceUtc) continue;
                ReadAppended(path, state, sinceUtc);
                state.LastWriteUtc = info.LastWriteTimeUtc;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        foreach (var gone in files.Keys.Where(k => !present.Contains(k)).ToList()) files.Remove(gone);

        return [.. files.Values
            .Where(s => s.Cwd is not null && s.Events.Count > 0)
            .GroupBy(s => (s.Provider, s.SessionId ?? s.GetHashCode().ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .Select(g => new TranscriptSession(g.Key.Item2, g.First().Cwd!, g.Max(s => s.LastWriteUtc), default, null)
            {
                Provider = g.Key.Provider,
                Events = [.. g.SelectMany(s => s.Events).OrderBy(e => e.AtUtc)],
            })];
    }

    private IEnumerable<(string Path, string Provider, string? Cwd)> Candidates(DateTime sinceUtc)
    {
        // Codex files by date folder, so only the window's days are listed, not every session ever.
        if (Directory.Exists(codexSessionsDir))
            for (var day = sinceUtc.ToLocalTime().Date.AddDays(-1); day <= DateTime.Now.Date; day = day.AddDays(1))
            {
                // Gregorian whatever the Windows language: th-TH's Buddhist calendar would look under 2569 (and never find one).
                var dir = Path.Combine(codexSessionsDir, day.ToString("yyyy", CultureInfo.InvariantCulture), day.ToString("MM", CultureInfo.InvariantCulture),
                    day.ToString("dd", CultureInfo.InvariantCulture));
                if (!Directory.Exists(dir)) continue;
                foreach (var f in List(() => Directory.GetFiles(dir, "*.jsonl"))) yield return (f, Providers.Codex, null);
            }

        if (Directory.Exists(geminiTmpDir))
            foreach (var project in List(() => Directory.GetDirectories(geminiTmpDir)))
            {
                var chats = Path.Combine(project, "chats");
                if (!Directory.Exists(chats)) continue;
                var rootFile = Path.Combine(project, ".project_root");
                string? cwd = null;
                try { if (File.Exists(rootFile)) cwd = File.ReadAllText(rootFile).Trim(); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                foreach (var f in List(() => Directory.GetFiles(chats, "*.jsonl"))) yield return (f, Providers.Gemini, cwd);
            }
    }

    // A folder removed or locked between the Exists check and the listing is skipped, not a failed poll.
    private static string[] List(Func<string[]> list)
    {
        try { return list(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }
    }

    private void ReadAppended(string path, FileState state, DateTime sinceUtc)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length < state.Offset) { state.Offset = 0; state.Events.Clear(); state.Seen.Clear(); state.LastTotal = 0; }
        if (stream.Length == state.Offset) return;
        stream.Seek(state.Offset, SeekOrigin.Begin);
        var buffer = new byte[stream.Length - state.Offset];
        var total = 0;
        while (total < buffer.Length) { var n = stream.Read(buffer, total, buffer.Length - total); if (n == 0) break; total += n; }
        var end = buffer.AsSpan(0, total).LastIndexOf((byte)'\n');
        if (end < 0) return;
        foreach (var line in Encoding.UTF8.GetString(buffer, 0, end).Split('\n'))
            if (line.Length > 0) ApplyLine(line, state, sinceUtc, limits);
        state.Offset += end + 1;
    }

    private static void ApplyLine(string line, FileState state, DateTime sinceUtc, Dictionary<(string, string), ProviderLimit> limits)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(line); } catch (JsonException) { return; }
        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return;
            var at = Str(root, "timestamp") is { } ts && DateTime.TryParse(ts, null,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var p) ? p : DateTime.UtcNow;

            if (state.Provider == Providers.Codex) Codex(root, at, state, sinceUtc, limits);
            else Gemini(root, at, state, sinceUtc);
        }
    }

    private static void Codex(JsonElement root, DateTime at, FileState state, DateTime sinceUtc, Dictionary<(string, string), ProviderLimit> limits)
    {
        if (!root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object) return;
        var type = Str(root, "type");
        if (type == "session_meta")
        {
            state.Cwd ??= Str(payload, "cwd");
            state.SessionId ??= Str(payload, "id");
            return;
        }
        if (type != "event_msg" || Str(payload, "type") != "token_count") return;

        if (payload.TryGetProperty("rate_limits", out var rl) && rl.ValueKind == JsonValueKind.Object)
            foreach (var (name, window) in new[] { ("primary", "5h"), ("secondary", "week") })
                if (rl.TryGetProperty(name, out var w) && w.ValueKind == JsonValueKind.Object &&
                    w.TryGetProperty("used_percent", out var used) && used.ValueKind == JsonValueKind.Number &&
                    w.TryGetProperty("resets_at", out var resets) && resets.ValueKind == JsonValueKind.Number)
                {
                    var key = (Providers.Codex, window);
                    if (!limits.TryGetValue(key, out var had) || had.SeenUtc <= at)
                        limits[key] = new ProviderLimit(Providers.Codex, window, used.GetDouble(), DateTimeOffset.FromUnixTimeSeconds(resets.GetInt64()).UtcDateTime, at);
                }

        if (!payload.TryGetProperty("info", out var info) || info.ValueKind != JsonValueKind.Object) return;
        // Codex repeats a token_count without a new turn; the running total only grows on a real one.
        var runningTotal = info.TryGetProperty("total_token_usage", out var tot) ? Num(tot, "total_tokens") : 0;
        if (runningTotal <= state.LastTotal || !info.TryGetProperty("last_token_usage", out var last)) return;
        state.LastTotal = runningTotal;
        if (at < sinceUtc) return;
        var input = Num(last, "input_tokens");
        var cached = Num(last, "cached_input_tokens");
        // OpenAI counts cached input inside input_tokens (and reasoning inside output_tokens).
        state.Events.Add(new UsageEvent(at, new TokenUsage(Math.Max(0, input - cached), Num(last, "cache_write_input_tokens"), cached, Num(last, "output_tokens"))));
    }

    private static void Gemini(JsonElement root, DateTime at, FileState state, DateTime sinceUtc)
    {
        if (Str(root, "type") != "gemini" || !root.TryGetProperty("tokens", out var t) || t.ValueKind != JsonValueKind.Object) return;
        if (Str(root, "id") is { } id && !state.Seen.Add(id)) return;
        if (at < sinceUtc) return;
        var input = Num(t, "input");
        var cached = Num(t, "cached");
        state.Events.Add(new UsageEvent(at, new TokenUsage(Math.Max(0, input - cached), 0, cached, Num(t, "output") + Num(t, "thoughts"))));
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static long Num(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : 0;
}
