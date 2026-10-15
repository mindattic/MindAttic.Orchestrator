using System.Text;
using System.Text.Json;
using MindAttic.Orchestrator.Budget;

namespace MindAttic.Orchestrator.Activity;

/// <summary>
/// Reads Claude Code's transcripts (<c>~/.claude/projects/**/*.jsonl</c>) incrementally: each poll
/// only parses bytes appended since the last one. Every line carries the session's <c>cwd</c>, and
/// assistant lines carry the API's <c>usage</c> block, so this is both the activity feed and the
/// token ledger without any hook having to record anything. With a <c>cachePath</c>, what it has read
/// survives restarts, so a new process parses only what was written since the last one.
/// </summary>
public sealed class TranscriptReader(string projectsDir, string? cachePath = null)
{
    private const int HeadScanLimit = 256 * 1024;
    private const int CacheVersion = 2;
    private static readonly TimeSpan SaveEvery = TimeSpan.FromMinutes(1);

    /// <summary>A file written this recently is opened every poll whatever its directory entry says (a file held open can
    /// show a stale length and timestamp there); one quiet longer is skipped while its entry is unchanged.</summary>
    internal static readonly TimeSpan HotFor = TimeSpan.FromMinutes(10);

    /// <summary>How often every file in the window is opened anyway, so growth a stale directory entry hid is still found.</summary>
    internal static readonly TimeSpan FullCheckEvery = TimeSpan.FromMinutes(5);
    private static readonly byte[] UsageMarker = Encoding.UTF8.GetBytes("\"usage\"");
    private static readonly byte[] TicketMarker = Encoding.UTF8.GetBytes(TicketAttribution.PromptMarker);

    private readonly Dictionary<string, FileState> files = new(StringComparer.OrdinalIgnoreCase);
    private DateTime countingSinceUtc;
    private bool triedCache;
    private bool dirty;
    private DateTime savedUtc = DateTime.MinValue;
    private DateTime fullCheckUtc = DateTime.MinValue;
    private IReadOnlyList<TranscriptSession>? sessions;

    /// <summary>Bumped whenever a poll finds anything new; while it's unchanged, <see cref="Poll"/> returns the same sessions.</summary>
    public long Version { get; private set; }

    /// <summary>How many transcripts the last poll opened (for tests).</summary>
    internal int LastPollOpened { get; private set; }

    private sealed class FileState
    {
        public long Offset;
        public string? Cwd;
        public string? SessionId;
        public string? Model;
        public string? TicketId;
        public string? TicketType;
        public DateTime LastWriteUtc;
        /// <summary>The length and timestamp its directory entry showed when last read; while both are unchanged and the
        /// file has been quiet a while, it isn't opened again (MAO-T116).</summary>
        public long ListedLength = -1;
        public DateTime ListedWriteUtc;
        /// <summary>Loaded from the cache and not read since: growth found now happened while nobody was watching.</summary>
        public bool Restored;
        public readonly List<UsageEvent> Events = [];
        public readonly List<LimitHit> Hits = [];
        public readonly List<ToolCall> Tools = [];
        public readonly HashSet<string> CountedMessages = new(StringComparer.Ordinal);
    }

    /// <summary>
    /// Brings every transcript up to date and returns one entry per session, with every API response
    /// timestamped at or after <paramref name="sinceUtc"/> as a <see cref="UsageEvent"/>. Moving the
    /// boundary later just drops what fell out of the window; moving it earlier recounts from scratch.
    /// </summary>
    public IReadOnlyList<TranscriptSession> Poll(DateTime sinceUtc)
    {
        var changed = false;
        if (!triedCache)
        {
            triedCache = true;
            LoadCache(sinceUtc);
            changed = true;
        }
        if (sinceUtc != countingSinceUtc)
        {
            if (countingSinceUtc != default && sinceUtc > countingSinceUtc) Trim(sinceUtc);
            else files.Clear();
            countingSinceUtc = sinceUtc;
            dirty = changed = true;
        }
        if (!Directory.Exists(projectsDir))
        {
            if (sessions is not { Count: 0 }) Version++;
            return sessions = [];
        }

        var now = DateTime.UtcNow;
        LastPollOpened = 0;
        var fullCheck = now - fullCheckUtc >= FullCheckEvery;
        if (fullCheck) fullCheckUtc = now;
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var info in EnumerateTranscripts())
        {
            present.Add(info.FullName);
            if (!files.TryGetValue(info.FullName, out var state))
                files[info.FullName] = state = new FileState();

            try
            {
                if (info.LastWriteTimeUtc < sinceUtc)
                {
                    // Nothing in it can count toward today; only learn whose it is.
                    if (state.LastWriteUtc != info.LastWriteTimeUtc)
                    {
                        if (state.Cwd is null) ScanHead(info.FullName, state);
                        dirty = changed = true;
                    }
                    state.Offset = 0;
                    state.LastWriteUtc = info.LastWriteTimeUtc;
                }
                else
                {
                    // Opening every file in the window every poll was most of a refresh's cost (MAO-T116). One whose
                    // directory entry hasn't moved since it was last read, and that has been quiet a while, is skipped.
                    if (!fullCheck && !state.Restored && state.ListedLength == info.Length && state.ListedWriteUtc == info.LastWriteTimeUtc &&
                        now - state.LastWriteUtc >= HotFor)
                        continue;

                    // A file Claude Code holds open can show a stale timestamp in the directory listing,
                    // so recent files are checked by their real length rather than trusted by mtime.
                    var known = state.LastWriteUtc != default && !state.Restored;
                    LastPollOpened++;
                    var grew = ReadAppended(info.FullName, state, sinceUtc);
                    // Growth on a file we'd already read means "written just now"; the first read of a
                    // file (or of one restored from the cache) only tells us its history, so it keeps
                    // the directory's timestamp.
                    var seen = grew && known ? DateTime.UtcNow : DateTime.MinValue;
                    var lastWrite = new[] { state.LastWriteUtc, info.LastWriteTimeUtc, seen }.Max();
                    changed |= grew || lastWrite != state.LastWriteUtc || state.Restored;
                    state.LastWriteUtc = lastWrite;
                    state.Restored = false;
                    state.ListedLength = info.Length;
                    state.ListedWriteUtc = info.LastWriteTimeUtc;
                    dirty |= grew;
                }
            }
            catch (IOException) { changed = true; /* being rotated or locked: next poll */ }
            catch (UnauthorizedAccessException) { changed = true; }
        }

        foreach (var gone in files.Keys.Where(k => !present.Contains(k)).ToList())
        {
            files.Remove(gone);
            dirty = changed = true;
        }

        if (dirty && DateTime.UtcNow - savedUtc >= SaveEvery) SaveCache();

        if (!changed && sessions is not null) return sessions;
        Version++;
        return sessions = [.. files.Values
            .Where(s => s.Cwd is not null)
            .GroupBy(s => s.SessionId ?? "")
            .Select(g => new TranscriptSession(
                g.Key,
                g.OrderBy(s => s.SessionId is null).First().Cwd!,
                g.Max(s => s.LastWriteUtc),
                default,
                g.OrderByDescending(s => s.LastWriteUtc).Select(s => s.Model).FirstOrDefault(m => m is not null))
            {
                Events = [.. g.SelectMany(s => s.Events).OrderBy(e => e.AtUtc)],
                Hits = [.. g.SelectMany(s => s.Hits).OrderBy(h => h.AtUtc)],
                TicketId = g.Select(s => s.TicketId).FirstOrDefault(id => id is not null) ?? TicketAttribution.FromWorktree(g.First().Cwd),
                TicketType = g.Select(s => s.TicketType).FirstOrDefault(x => x is not null),
                Loop = LoopDetector.Find(g.SelectMany(s => s.Tools).OrderBy(c => c.AtUtc).ToList()),
            })];
    }

    private void Trim(DateTime sinceUtc)
    {
        foreach (var state in files.Values)
        {
            state.Events.RemoveAll(e => e.AtUtc < sinceUtc);
            state.Hits.RemoveAll(h => h.AtUtc < sinceUtc);
        }
    }

    // Events as [ticks, input, cache write, cache read, output]: a third the size of named fields.
    private sealed record CachedFile(string Path, long Offset, string? Cwd, string? SessionId, string? Model, string? TicketId, string? TicketType, DateTime LastWriteUtc,
        long[][] Events, LimitHit[] Hits, string[] Counted);

    private sealed record Cache(int Version, DateTime SinceUtc, List<CachedFile> Files);

    /// <summary>
    /// Restores a cache written with the same or an earlier window start (trimming what fell out of
    /// the window). Anything unreadable, or newer than asked for, is ignored: the poll reads from scratch.
    /// </summary>
    private void LoadCache(DateTime sinceUtc)
    {
        if (cachePath is null || !File.Exists(cachePath)) return;
        Cache? cache;
        try { cache = JsonSerializer.Deserialize<Cache>(File.ReadAllBytes(cachePath)); }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException) { return; }
        if (cache is null || cache.Version != CacheVersion || cache.SinceUtc > sinceUtc) return;

        foreach (var f in cache.Files)
        {
            var state = new FileState
            {
                Offset = f.Offset, Cwd = f.Cwd, SessionId = f.SessionId, Model = f.Model, TicketId = f.TicketId, TicketType = f.TicketType,
                LastWriteUtc = f.LastWriteUtc, Restored = true,
            };
            foreach (var e in f.Events)
                if (e.Length == 5) state.Events.Add(new UsageEvent(new DateTime(e[0], DateTimeKind.Utc), new TokenUsage(e[1], e[2], e[3], e[4])));
            state.Hits.AddRange(f.Hits);
            state.CountedMessages.UnionWith(f.Counted);
            files[f.Path] = state;
        }
        countingSinceUtc = cache.SinceUtc;
        if (sinceUtc > cache.SinceUtc) Trim(sinceUtc);
    }

    private void SaveCache()
    {
        dirty = false;
        savedUtc = DateTime.UtcNow;
        if (cachePath is null) return;
        var cache = new Cache(CacheVersion, countingSinceUtc, [.. files.Select(kv => new CachedFile(
            kv.Key, kv.Value.Offset, kv.Value.Cwd, kv.Value.SessionId, kv.Value.Model, kv.Value.TicketId, kv.Value.TicketType, kv.Value.LastWriteUtc,
            [.. kv.Value.Events.Select(e => new[] { e.AtUtc.Ticks, e.Usage.Input, e.Usage.CacheCreation, e.Usage.CacheRead, e.Usage.Output })],
            [.. kv.Value.Hits], [.. kv.Value.CountedMessages]))]);
        // The daemon, the dashboard and CLI runs share the cache: each writes a private temp file and
        // swaps it in whole, so a reader never sees half of one.
        var temp = $"{cachePath}.{Environment.ProcessId}.tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            File.WriteAllBytes(temp, JsonSerializer.SerializeToUtf8Bytes(cache));
            File.Move(temp, cachePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try { File.Delete(temp); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    private IEnumerable<FileInfo> EnumerateTranscripts()
    {
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        return new DirectoryInfo(projectsDir).EnumerateFiles("*.jsonl", options);
    }

    private static void ScanHead(string path, FileState state)
    {
        using var stream = Open(path);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        long read = 0;
        while (reader.ReadLine() is { } line)
        {
            read += line.Length;
            if (line.Contains("\"cwd\"", StringComparison.Ordinal)) Apply(line, state, countFrom: null);
            if (state.Cwd is not null || read > HeadScanLimit) return;
        }
    }

    /// <summary>Parses complete lines appended since the last call; true when any were consumed.</summary>
    private static bool ReadAppended(string path, FileState state, DateTime sinceUtc)
    {
        using var stream = Open(path);
        if (stream.Length < state.Offset) // truncated or replaced: start over
        {
            state.Offset = 0;
            state.Events.Clear();
            state.Hits.Clear();
            state.CountedMessages.Clear();
        }
        if (stream.Length == state.Offset) return false;

        stream.Seek(state.Offset, SeekOrigin.Begin);
        var buffer = new byte[stream.Length - state.Offset];
        var total = 0;
        while (total < buffer.Length)
        {
            var n = stream.Read(buffer, total, buffer.Length - total);
            if (n == 0) break;
            total += n;
        }

        // Only consume through the last newline: a line still being written is picked up next poll.
        var span = buffer.AsSpan(0, total);
        var end = span.LastIndexOf((byte)'\n');
        if (end < 0) return false;

        var consumed = span[..(end + 1)];
        while (!consumed.IsEmpty)
        {
            var nl = consumed.IndexOf((byte)'\n');
            var line = consumed[..nl];
            consumed = consumed[(nl + 1)..];
            if (line.IsEmpty) continue;
            if (state.Cwd is null || line.IndexOf(UsageMarker) >= 0 || (state.TicketId is null && line.IndexOf(TicketMarker) >= 0))
                Apply(Encoding.UTF8.GetString(line), state, sinceUtc);
        }
        state.Offset += end + 1;
        return true;
    }

    /// <summary>Folds one transcript line into the file's state.</summary>
    internal static void ApplyLine(string line, TranscriptLineSink sink, DateTime? countFrom)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(line); }
        catch (JsonException) { return; }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return;
            if (sink.Cwd is null && Str(root, "cwd") is { } cwd) sink.Cwd = cwd;
            if (sink.SessionId is null && Str(root, "sessionId") is { } sid) sink.SessionId = sid;

            if (countFrom is null) return;
            // The orchestrator's dispatch prompt names the ticket: that's what ties a session's cost to it.
            if (sink.TicketId is null && Str(root, "type") == "user" && root.TryGetProperty("message", out var prompt) &&
                prompt.ValueKind == JsonValueKind.Object && TicketAttribution.FromPrompt(Text(prompt)) is { } ticket)
            {
                (sink.TicketId, sink.TicketType) = ticket;
                return;
            }
            if (!root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object) return;
            if (!message.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object) return;
            if (Str(message, "model") is { } model && !model.StartsWith('<')) sink.Model = model;
            RecordTools(message, root, sink);

            var at = Str(root, "timestamp") is { } ts &&
                DateTime.TryParse(ts, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed)
                ? parsed
                : DateTime.UtcNow;
            if (at < countFrom.Value) return;

            // A rate-limit wall is a synthetic message with zero usage: record the hit, not a response.
            if (Str(root, "error") == "rate_limit")
            {
                if (LimitHitParser.TryParse(Text(message), at, out var hit)) sink.Hits.Add(hit);
                return;
            }

            // A response with several content blocks is written as several lines that repeat the
            // same message id and usage; count it once.
            if (Str(message, "id") is { } id && !sink.CountedMessages.Add(id)) return;

            sink.Events.Add(new UsageEvent(at, new TokenUsage(
                Num(usage, "input_tokens"),
                Num(usage, "cache_creation_input_tokens"),
                Num(usage, "cache_read_input_tokens"),
                Num(usage, "output_tokens"))));
        }
    }

    private static void Apply(string line, FileState state, DateTime? countFrom)
    {
        var sink = new TranscriptLineSink(state.CountedMessages, state.Events, state.Hits, state.Tools)
        {
            Cwd = state.Cwd, SessionId = state.SessionId, Model = state.Model, TicketId = state.TicketId, TicketType = state.TicketType,
        };
        ApplyLine(line, sink, countFrom);
        state.Cwd = sink.Cwd;
        state.SessionId = sink.SessionId;
        state.Model = sink.Model;
        state.TicketId = sink.TicketId;
        state.TicketType = sink.TicketType;
    }

    // Each content block is its own line, so a tool call is seen exactly once.
    private static void RecordTools(JsonElement message, JsonElement root, TranscriptLineSink sink)
    {
        if (!message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) return;
        var at = Str(root, "timestamp") is { } ts && DateTime.TryParse(ts, null,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var p) ? p : DateTime.UtcNow;
        foreach (var block in content.EnumerateArray())
        {
            if (block.ValueKind != JsonValueKind.Object || Str(block, "type") != "tool_use") continue;
            var input = block.TryGetProperty("input", out var i) ? i.GetRawText() : "";
            sink.Tools.Add(new ToolCall(at, Str(block, "name") ?? "?", input));
            if (sink.Tools.Count > LoopDetector.Window * 2) sink.Tools.RemoveRange(0, sink.Tools.Count - LoopDetector.Window);
        }
    }

    private static FileStream Open(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static long Num(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : 0;

    private static string Text(JsonElement message)
    {
        if (!message.TryGetProperty("content", out var content)) return "";
        if (content.ValueKind == JsonValueKind.String) return content.GetString() ?? "";
        if (content.ValueKind != JsonValueKind.Array) return "";
        var sb = new StringBuilder();
        foreach (var block in content.EnumerateArray())
            if (block.ValueKind == JsonValueKind.Object && Str(block, "text") is { } t) sb.Append(t);
        return sb.ToString();
    }
}

/// <summary>The mutable fields one transcript line can update.</summary>
internal sealed class TranscriptLineSink(HashSet<string> countedMessages, List<UsageEvent> events, List<LimitHit>? hits = null, List<ToolCall>? tools = null)
{
    public string? Cwd { get; set; }
    public string? SessionId { get; set; }
    public string? Model { get; set; }
    public string? TicketId { get; set; }
    public string? TicketType { get; set; }
    public HashSet<string> CountedMessages { get; } = countedMessages;
    public List<UsageEvent> Events { get; } = events;
    public List<LimitHit> Hits { get; } = hits ?? [];
    public List<ToolCall> Tools { get; } = tools ?? [];
}
