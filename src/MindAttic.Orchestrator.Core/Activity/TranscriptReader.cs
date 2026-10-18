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
public sealed partial class TranscriptReader(string projectsDir, string? cachePath = null)
{
    private const int HeadScanLimit = 256 * 1024;
    private const int CacheVersion = 5;
    private static readonly TimeSpan SaveEvery = TimeSpan.FromMinutes(1);

    /// <summary>A file written this recently is opened every poll whatever its directory entry says (a file held open can
    /// show a stale length and timestamp there); one quiet longer is skipped while its entry is unchanged.</summary>
    internal static readonly TimeSpan HotFor = TimeSpan.FromMinutes(10);

    /// <summary>How often every file in the window is opened anyway, so growth a stale directory entry hid is still found.</summary>
    internal static readonly TimeSpan FullCheckEvery = TimeSpan.FromMinutes(5);
    private static readonly byte[] UsageMarker = Encoding.UTF8.GetBytes("\"usage\"");
    private static readonly byte[] TicketMarker = Encoding.UTF8.GetBytes(TicketAttribution.PromptMarker);
    private static readonly byte[] ToolResultMarker = Encoding.UTF8.GetBytes("\"tool_result\"");

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

    /// <summary>The clock for the full check, hot files and cache saves (a seam for tests).</summary>
    internal Func<DateTime> UtcNow { get; init; } = () => DateTime.UtcNow;

    private sealed class FileState
    {
        public long Offset;
        public string? Cwd;
        public string? SessionId;
        public string? Model;
        public string? TicketId;
        public string? TicketType;
        /// <summary>Set for a subagent's transcript (<c>&lt;session&gt;\subagents\agent-&lt;id&gt;.jsonl</c>): it shares its parent's
        /// session id, but a ticket its prompt names is its own, not the parent's (M.ORCH-T252).</summary>
        public string? AgentId;
        public DateTime LastWriteUtc;
        /// <summary>The length and timestamp its directory entry showed when last read; while both are unchanged and the
        /// file has been quiet a while, it isn't opened again (M.ORCH-T116).</summary>
        public long ListedLength = -1;
        public DateTime ListedWriteUtc;
        /// <summary>Loaded from the cache and not read since: growth found now happened while nobody was watching.</summary>
        public bool Restored;
        public readonly List<UsageEvent> Events = [];
        public readonly List<LimitHit> Hits = [];
        public readonly List<ToolCall> Tools = [];
        public readonly Dictionary<string, TokenUsage> CountedMessages = new(StringComparer.Ordinal);
        /// <summary><c>workflow ticket new</c> calls whose output hasn't come back yet (their tool_use ids).</summary>
        public readonly HashSet<string> PendingFiles = new(StringComparer.Ordinal);
        /// <summary>Tickets this transcript filed in its current turn: an agent that files a ticket and fixes it is working it (M.ORCH-T255).</summary>
        public readonly List<string> Filed = [];
        /// <summary>Between a fork's <c>fork-context-ref</c> line and its first prompt: the parent's line copied there (M.ORCH-T323).</summary>
        public bool ForkPrologue;
        /// <summary>A fork's Agent call (its <c>.meta.json</c>'s toolUseId): its prologue ends at the prompt answering it (M.ORCH-T348).</summary>
        public string? ForkToolUseId;
        /// <summary>Its <c>.meta.json</c> has been read. Claude Code often writes it minutes after the transcript (up to half an
        /// hour), so until then a subagent's transcript is checked again each time it's read (M.ORCH-T378).</summary>
        public bool MetaRead;
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

        var now = UtcNow();
        LastPollOpened = 0;
        var fullCheck = now - fullCheckUtc >= FullCheckEvery;
        if (fullCheck) fullCheckUtc = now;
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var info in EnumerateTranscripts())
        {
            present.Add(info.FullName);
            if (!files.TryGetValue(info.FullName, out var state))
                files[info.FullName] = state = new FileState { AgentId = SubagentId(info.FullName) };

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
                    state.Tools.Clear(); // read again from 0 when it's resumed: tool calls aren't de-duplicated like usage
                    state.PendingFiles.Clear();
                    state.Filed.Clear();
                    state.LastWriteUtc = info.LastWriteTimeUtc;
                }
                else
                {
                    // Opening every file in the window every poll was most of a refresh's cost (M.ORCH-T116). One whose
                    // directory entry hasn't moved since it was last read, and that has been quiet a while, is skipped.
                    if (!fullCheck && !state.Restored && state.ListedLength == info.Length && state.ListedWriteUtc == info.LastWriteTimeUtc &&
                        now - state.LastWriteUtc >= HotFor)
                        continue;

                    // A file Claude Code holds open can show a stale timestamp in the directory listing,
                    // so recent files are checked by their real length rather than trusted by mtime.
                    var known = state.LastWriteUtc != default && !state.Restored;
                    // A fork found only now is read again from 0: that's its history again, not writing just now.
                    if (state.AgentId is not null && !state.MetaRead && ReadMeta(info.FullName, state)) known = false;
                    LastPollOpened++;
                    var grew = ReadAppended(info.FullName, state, sinceUtc);
                    // Growth on a file we'd already read means "written just now"; the first read of a
                    // file (or of one restored from the cache) only tells us its history, so it keeps
                    // the directory's timestamp.
                    var seen = grew && known ? UtcNow() : DateTime.MinValue;
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

        if (dirty && UtcNow() - savedUtc >= SaveEvery) SaveCache();

        if (!changed && sessions is not null) return sessions;
        Version++;
        return sessions = [.. files.Values
            .Where(s => s.Cwd is not null)
            .GroupBy(s => s.SessionId ?? "")
            .Select(g =>
            {
                // A subagent handed a ticket (an /autopilot handout) names it in its own prompt: that ticket is the
                // subagent's alone. Only the CLI's own transcript says which ticket the session itself works on (M.ORCH-T252).
                var own = g.Where(s => s.AgentId is null).ToList();
                var ticket = own.Select(s => (Id: s.TicketId, Type: s.TicketType)).FirstOrDefault(t => t.Id is not null);
                return new TranscriptSession(
                    g.Key,
                    g.OrderBy(s => s.SessionId is null).ThenBy(s => s.AgentId is not null).First().Cwd!,
                    g.Max(s => s.LastWriteUtc),
                    default,
                    g.OrderByDescending(s => s.LastWriteUtc).Select(s => s.Model).FirstOrDefault(m => m is not null))
                {
                    Events = [.. g.SelectMany(s => s.Events).OrderBy(e => e.AtUtc)],
                    Hits = [.. g.SelectMany(s => s.Hits).OrderBy(h => h.AtUtc)],
                    TicketId = ticket.Id ?? own.Select(s => TicketAttribution.FromWorktree(s.Cwd)).FirstOrDefault(id => id is not null),
                    TicketType = ticket.Type,
                    TicketedSubagents = [.. g.Where(s => s.AgentId is not null && s.TicketId is not null)
                        .Select(s => new TicketedSubagent(s.AgentId!, s.TicketId!, s.TicketType, s.Cwd!, [.. s.Events.OrderBy(e => e.AtUtc)]))],
                    Filing = [.. g.SelectMany(s => s.Filed.Select(id => new FiledTicket(id, s.AgentId, s.LastWriteUtc)))],
                    Loop = LoopDetector.Find(g.SelectMany(s => s.Tools).OrderBy(c => c.AtUtc).ToList()),
                    Doing = g.SelectMany(s => s.Tools).MaxBy(c => c.AtUtc) is { } last ? ToolCallText.Describe(last.Name, last.Input) : null,
                    DoingAtUtc = g.SelectMany(s => s.Tools).MaxBy(c => c.AtUtc)?.AtUtc,
                };
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
        long[][] Events, LimitHit[] Hits, Dictionary<string, long[]> Counted, string[]? Pending = null, string[]? Filed = null, bool ForkPrologue = false, string? ForkToolUseId = null);

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
                LastWriteUtc = f.LastWriteUtc, Restored = true, AgentId = SubagentId(f.Path), ForkPrologue = f.ForkPrologue, ForkToolUseId = f.ForkToolUseId,
            };
            foreach (var e in f.Events)
                if (e.Length == 5) state.Events.Add(new UsageEvent(new DateTime(e[0], DateTimeKind.Utc), new TokenUsage(e[1], e[2], e[3], e[4])));
            state.Hits.AddRange(f.Hits);
            foreach (var (id, u) in f.Counted)
                if (u.Length == 4) state.CountedMessages[id] = new TokenUsage(u[0], u[1], u[2], u[3]);
            state.PendingFiles.UnionWith(f.Pending ?? []);
            state.Filed.AddRange(f.Filed ?? []);
            files[f.Path] = state;
        }
        countingSinceUtc = cache.SinceUtc;
        if (sinceUtc > cache.SinceUtc) Trim(sinceUtc);
    }

    private void SaveCache()
    {
        dirty = false;
        savedUtc = UtcNow();
        if (cachePath is null) return;
        var cache = new Cache(CacheVersion, countingSinceUtc, [.. files.Select(kv => new CachedFile(
            kv.Key, kv.Value.Offset, kv.Value.Cwd, kv.Value.SessionId, kv.Value.Model, kv.Value.TicketId, kv.Value.TicketType, kv.Value.LastWriteUtc,
            [.. kv.Value.Events.Select(e => new[] { e.AtUtc.Ticks, e.Usage.Input, e.Usage.CacheCreation, e.Usage.CacheRead, e.Usage.Output })],
            [.. kv.Value.Hits], kv.Value.CountedMessages.ToDictionary(c => c.Key, c => new[] { c.Value.Input, c.Value.CacheCreation, c.Value.CacheRead, c.Value.Output }),
            [.. kv.Value.PendingFiles], [.. kv.Value.Filed], kv.Value.ForkPrologue, kv.Value.ForkToolUseId))]);
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

    /// <summary>The agent id of a subagent's transcript (<c>...\subagents\agent-&lt;id&gt;.jsonl</c>); null for a CLI's own.</summary>
    internal static string? SubagentId(string path) =>
        Path.GetFileName(Path.GetDirectoryName(path)) is { } parent && parent.Equals("subagents", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(path) : null;

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

    /// <summary>
    /// Reads a subagent's <c>.meta.json</c> once it's there. A fork found only now, after its transcript was read without
    /// knowing which Agent call it answers, is read again from the start: what it copied from the subagent that started it
    /// (that subagent's prompt, usage and tool calls) was taken as its own and counted a second time (M.ORCH-T378).
    /// </summary>
    /// <returns>True when it was found a fork just now, so it's read again from the start.</returns>
    private static bool ReadMeta(string path, FileState state)
    {
        if (!File.Exists(Path.ChangeExtension(path, ".meta.json"))) return false;
        // One caught mid-write (or held) is read again next time: taken for "not a fork" for good, its copied prologue would
        // count a second time.
        if (state.ForkToolUseId is not null) { state.MetaRead = true; return false; }
        if (!TryForkToolUseId(path, out var read)) return false;
        state.MetaRead = true;
        if (read is not { } forkCall) return false;
        state.ForkToolUseId = forkCall;
        state.ForkPrologue = true;
        state.Offset = 0;
        state.Events.Clear();
        state.Hits.Clear();
        state.Tools.Clear();
        state.CountedMessages.Clear();
        state.PendingFiles.Clear();
        state.Filed.Clear();
        (state.TicketId, state.TicketType) = (null, null);
        return true;
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
            state.Tools.Clear();
            state.CountedMessages.Clear();
            state.PendingFiles.Clear();
            state.Filed.Clear();
            state.ForkPrologue = state.ForkToolUseId is not null;
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
            if (state.Cwd is null || state.ForkPrologue || line.IndexOf(UsageMarker) >= 0 || (state.TicketId is null && line.IndexOf(TicketMarker) >= 0) ||
                (state.PendingFiles.Count > 0 && line.IndexOf(ToolResultMarker) >= 0))
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
            // A fork subagent's transcript opens with a copy of its parent's: after a fork-context-ref line the CLI's line that
            // started it, or (a fork a subagent started) that subagent's whole transcript, usage, prompt and tool calls and all.
            // Nothing before the prompt answering the fork's Agent call is its own (M.ORCH-T323, M.ORCH-T348).
            if (Str(root, "type") is "fork-context-ref")
            {
                sink.ForkPrologue = true;
                return;
            }
            if (sink.ForkPrologue)
            {
                if (!EndsForkPrologue(root, sink.ForkToolUseId)) return;
                sink.ForkPrologue = false;
            }
            if (CopiedFromAnotherSession(root)) return;

            if (countFrom is null) return;
            // The orchestrator's dispatch prompt names the ticket: that's what ties a session's cost to it.
            if (sink.TicketId is null && Str(root, "type") == "user" && root.TryGetProperty("message", out var prompt) &&
                prompt.ValueKind == JsonValueKind.Object && TicketAttribution.FromPrompt(Text(prompt)) is { } ticket)
            {
                (sink.TicketId, sink.TicketType) = ticket;
                return;
            }
            if (!root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object) return;
            if (sink.PendingFiles.Count > 0 && Str(root, "type") == "user") NoteFiled(message, sink);
            if (!message.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object) return;
            if (Str(message, "model") is { } model && !model.StartsWith('<')) sink.Model = model;
            RecordTools(message, root, sink);
            // The turn is over: what it filed and didn't fix is left for whoever works the queue.
            if (Str(message, "stop_reason") == "end_turn")
            {
                sink.PendingFiles.Clear();
                sink.Filed.Clear();
            }

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

            var used = new TokenUsage(
                Num(usage, "input_tokens"),
                Num(usage, "cache_creation_input_tokens"),
                Num(usage, "cache_read_input_tokens"),
                Num(usage, "output_tokens"));
            if (Str(message, "id") is { } id)
            {
                // A response with several content blocks is written as several lines with the same message id. A CLI's own
                // repeat the same usage, but a subagent's grow their output_tokens line by line and only the last has the
                // whole count (M.ORCH-T322): count the response once, at the most any of its lines says.
                if (sink.CountedMessages.TryGetValue(id, out var counted))
                {
                    var grown = TokenUsageGrowth(counted, used);
                    if (grown == default) return;
                    sink.CountedMessages[id] = counted + grown;
                    sink.Events.Add(new UsageEvent(at, grown));
                    return;
                }
                sink.CountedMessages[id] = used;
            }
            sink.Events.Add(new UsageEvent(at, used));
        }
    }

    /// <summary>How much a later line of the same response adds to what its earlier lines were counted at (never less than none).</summary>
    internal static TokenUsage TokenUsageGrowth(TokenUsage counted, TokenUsage line) => new(
        Math.Max(0, line.Input - counted.Input), Math.Max(0, line.CacheCreation - counted.CacheCreation),
        Math.Max(0, line.CacheRead - counted.CacheRead), Math.Max(0, line.Output - counted.Output));

    private static void Apply(string line, FileState state, DateTime? countFrom)
    {
        var sink = new TranscriptLineSink(state.CountedMessages, state.Events, state.Hits, state.Tools, state.PendingFiles, state.Filed)
        {
            Cwd = state.Cwd, SessionId = state.SessionId, Model = state.Model, TicketId = state.TicketId, TicketType = state.TicketType,
            ForkPrologue = state.ForkPrologue, ForkToolUseId = state.ForkToolUseId,
        };
        ApplyLine(line, sink, countFrom);
        state.Cwd = sink.Cwd;
        state.SessionId = sink.SessionId;
        state.Model = sink.Model;
        state.TicketId = sink.TicketId;
        state.TicketType = sink.TicketType;
        state.ForkPrologue = sink.ForkPrologue;
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
            if (input.Contains(FileTicketCommand, StringComparison.Ordinal) && Str(block, "id") is { } toolId) sink.PendingFiles.Add(toolId);
            if (sink.Tools.Count > LoopDetector.Window * 2) sink.Tools.RemoveRange(0, sink.Tools.Count - LoopDetector.Window);
        }
    }

    /// <summary>The Agent call a fork answers, from its <c>agent-&lt;id&gt;.meta.json</c> (<c>isFork</c>, <c>toolUseId</c>); null for
    /// any other transcript, or one whose meta can't be read.</summary>
    internal static string? ForkToolUseId(string transcriptPath) => TryForkToolUseId(transcriptPath, out var id) ? id : null;

    /// <summary>Like <see cref="ForkToolUseId"/>, but false when the meta couldn't be read (missing, held, or caught half
    /// written), so a caller that reads it once can try again rather than take it for "not a fork".</summary>
    internal static bool TryForkToolUseId(string transcriptPath, out string? forkCall)
    {
        forkCall = null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(Path.ChangeExtension(transcriptPath, ".meta.json")));
            var root = doc.RootElement;
            forkCall = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("isFork", out var fork) && fork.ValueKind == JsonValueKind.True &&
                Str(root, "toolUseId") is { Length: > 0 } id ? id : null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return false; }
    }

    /// <summary>Whether a fork's line is its own first prompt: the user line answering <paramref name="toolUseId"/> (or, not
    /// known, its first user line).</summary>
    internal static bool EndsForkPrologue(JsonElement root, string? toolUseId)
    {
        if (Str(root, "type") != "user") return false;
        if (toolUseId is null) return true;
        return root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.Object && m.TryGetProperty("content", out var content) &&
            content.ValueKind == JsonValueKind.Array && content.EnumerateArray().Any(b => b.ValueKind == JsonValueKind.Object &&
                Str(b, "type") == "tool_result" && Str(b, "tool_use_id") == toolUseId);
    }

    /// <summary>
    /// Whether a line is a background session's copy of another session's history (M.ORCH-T375). A CLI session sent to the
    /// background continues in a new transcript (<c>"sessionKind":"bg"</c>) that opens with the old one's lines copied over:
    /// same uuids, timestamps and message ids, rewritten with the new <c>sessionId</c> but still carrying the old session's
    /// <c>session_id</c>, so its responses were counted once in each transcript. A line's <c>session_id</c> differing from its
    /// <c>sessionId</c> isn't enough on its own: a session's lines after a /clear or a resume carry the earlier session's (about
    /// 16,000 real responses in 30 days), but those are never in a background transcript, whose own lines carry its own.
    /// </summary>
    internal static bool CopiedFromAnotherSession(JsonElement root) =>
        Str(root, "sessionKind") == "bg" && Str(root, "session_id") is { Length: > 0 } from && Str(root, "sessionId") is { } own && from != own;

    /// <summary>How an agent files a ticket; it prints the new ticket's id.</summary>
    internal const string FileTicketCommand = "workflow ticket new";

    [System.Text.RegularExpressions.GeneratedRegex(@"^\s*(?<id>(?:[A-Z]\.)?[A-Z][A-Z0-9]*-T\d+)\s*$", System.Text.RegularExpressions.RegexOptions.Multiline)]
    private static partial System.Text.RegularExpressions.Regex FiledId();

    /// <summary>The output of a <c>workflow ticket new</c> call: the id it printed is a ticket this transcript filed.</summary>
    private static void NoteFiled(JsonElement message, TranscriptLineSink sink)
    {
        if (!message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) return;
        foreach (var block in content.EnumerateArray())
        {
            if (block.ValueKind != JsonValueKind.Object || Str(block, "type") != "tool_result" ||
                Str(block, "tool_use_id") is not { } toolId || !sink.PendingFiles.Remove(toolId)) continue;
            if (block.TryGetProperty("is_error", out var error) && error.ValueKind == JsonValueKind.True) continue;
            var output = block.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() ?? ""
                : block.TryGetProperty("content", out c) && c.ValueKind == JsonValueKind.Array ? string.Concat(c.EnumerateArray()
                    .Where(b => b.ValueKind == JsonValueKind.Object).Select(b => Str(b, "text"))) : "";
            foreach (var id in FiledId().Matches(output).Select(m => m.Groups["id"].Value.ToUpperInvariant()))
                if (!sink.Filed.Contains(id)) sink.Filed.Add(id);
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
internal sealed class TranscriptLineSink(Dictionary<string, TokenUsage> countedMessages, List<UsageEvent> events, List<LimitHit>? hits = null, List<ToolCall>? tools = null,
    HashSet<string>? pendingFiles = null, List<string>? filed = null)
{
    public HashSet<string> PendingFiles { get; } = pendingFiles ?? new(StringComparer.Ordinal);
    public List<string> Filed { get; } = filed ?? [];
    public string? Cwd { get; set; }
    public string? SessionId { get; set; }
    public string? Model { get; set; }
    public string? TicketId { get; set; }
    public string? TicketType { get; set; }
    public bool ForkPrologue { get; set; }
    public string? ForkToolUseId { get; set; }
    public Dictionary<string, TokenUsage> CountedMessages { get; } = countedMessages;
    public List<UsageEvent> Events { get; } = events;
    public List<LimitHit> Hits { get; } = hits ?? [];
    public List<ToolCall> Tools { get; } = tools ?? [];
}
