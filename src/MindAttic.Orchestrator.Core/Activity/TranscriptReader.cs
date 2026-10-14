using System.Text;
using System.Text.Json;

namespace MindAttic.Orchestrator.Activity;

/// <summary>
/// Reads Claude Code's transcripts (<c>~/.claude/projects/**/*.jsonl</c>) incrementally: each poll
/// only parses bytes appended since the last one. Every line carries the session's <c>cwd</c>, and
/// assistant lines carry the API's <c>usage</c> block, so this is both the activity feed and the
/// token ledger without any hook having to record anything.
/// </summary>
public sealed class TranscriptReader(string projectsDir)
{
    private const int HeadScanLimit = 256 * 1024;
    private static readonly byte[] UsageMarker = Encoding.UTF8.GetBytes("\"usage\"");

    private readonly Dictionary<string, FileState> files = new(StringComparer.OrdinalIgnoreCase);
    private DateTime countingSinceUtc;

    private sealed class FileState
    {
        public long Offset;
        public string? Cwd;
        public string? SessionId;
        public string? Model;
        public DateTime LastWriteUtc;
        public readonly List<UsageEvent> Events = [];
        public readonly HashSet<string> CountedMessages = new(StringComparer.Ordinal);
    }

    /// <summary>
    /// Brings every transcript up to date and returns one entry per session, with every API response
    /// timestamped at or after <paramref name="sinceUtc"/> as a <see cref="UsageEvent"/>. Moving that
    /// boundary recounts from scratch, so callers should only move it rarely (once a day).
    /// </summary>
    public IReadOnlyList<TranscriptSession> Poll(DateTime sinceUtc)
    {
        if (sinceUtc != countingSinceUtc)
        {
            files.Clear();
            countingSinceUtc = sinceUtc;
        }
        if (!Directory.Exists(projectsDir)) return [];

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
                    if (state.LastWriteUtc != info.LastWriteTimeUtc && state.Cwd is null) ScanHead(info.FullName, state);
                    state.Offset = 0;
                    state.LastWriteUtc = info.LastWriteTimeUtc;
                }
                else
                {
                    // A file Claude Code holds open can show a stale timestamp in the directory listing,
                    // so today's files are checked by their real length rather than trusted by mtime.
                    var known = state.LastWriteUtc != default;
                    var grew = ReadAppended(info.FullName, state, sinceUtc);
                    // Growth on a file we'd already read means "written just now"; the first read of a
                    // file only tells us its history, so it keeps the directory's timestamp.
                    var seen = grew && known ? DateTime.UtcNow : DateTime.MinValue;
                    state.LastWriteUtc = new[] { state.LastWriteUtc, info.LastWriteTimeUtc, seen }.Max();
                }
            }
            catch (IOException) { /* being rotated or locked: next poll */ }
            catch (UnauthorizedAccessException) { }
        }

        foreach (var gone in files.Keys.Where(k => !present.Contains(k)).ToList())
            files.Remove(gone);

        return [.. files.Values
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
            })];
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
            if (state.Cwd is null || line.IndexOf(UsageMarker) >= 0)
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
            if (!root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object) return;
            if (!message.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object) return;
            if (Str(message, "model") is { } model && !model.StartsWith('<')) sink.Model = model;

            // A response with several content blocks is written as several lines that repeat the
            // same message id and usage; count it once.
            if (Str(message, "id") is { } id && !sink.CountedMessages.Add(id)) return;
            var at = Str(root, "timestamp") is { } ts &&
                DateTime.TryParse(ts, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed)
                ? parsed
                : DateTime.UtcNow;
            if (at < countFrom.Value) return;

            sink.Events.Add(new UsageEvent(at, new TokenUsage(
                Num(usage, "input_tokens"),
                Num(usage, "cache_creation_input_tokens"),
                Num(usage, "cache_read_input_tokens"),
                Num(usage, "output_tokens"))));
        }
    }

    private static void Apply(string line, FileState state, DateTime? countFrom)
    {
        var sink = new TranscriptLineSink(state.CountedMessages, state.Events)
        {
            Cwd = state.Cwd, SessionId = state.SessionId, Model = state.Model,
        };
        ApplyLine(line, sink, countFrom);
        state.Cwd = sink.Cwd;
        state.SessionId = sink.SessionId;
        state.Model = sink.Model;
    }

    private static FileStream Open(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static long Num(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : 0;
}

/// <summary>The mutable fields one transcript line can update.</summary>
internal sealed class TranscriptLineSink(HashSet<string> countedMessages, List<UsageEvent> events)
{
    public string? Cwd { get; set; }
    public string? SessionId { get; set; }
    public string? Model { get; set; }
    public HashSet<string> CountedMessages { get; } = countedMessages;
    public List<UsageEvent> Events { get; } = events;
}
