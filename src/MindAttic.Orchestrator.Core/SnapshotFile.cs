using System.Text.Json;
using System.Text.Json.Serialization;

namespace MindAttic.Orchestrator;

/// <summary>
/// The last <see cref="Snapshot"/> the status lease holder built, for every other orchestrator process to show
/// instead of building its own (M.ORCH-T115): each open dashboard window and the daemon used to poll every transcript,
/// parse subagents, scan the Launcher's pipes and rewrite the transcript cache on its own timer. Written whole to a
/// private temp file and swapped in, so a reader never sees half of one. Each session's usage events and its ticketed
/// subagents (which plain JSON leaves out) travel alongside, so what's worked out from them (a repo's spend this block,
/// the "while you were away" summary, the tickets subagents are working) comes out the same as in the process that built it.
/// </summary>
public sealed class SnapshotFile(string path)
{
    public const int FormatVersion = 2;

    /// <summary>A published snapshot older than this is ignored (its writer stopped or hung) and the reader builds its own.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(1);

    private static readonly JsonSerializerOptions Json = new()
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new ReadOnlySetConverter() },
    };

    public string Path { get; } = path;

    private DateTime readStamp;
    private long readLength = -1;
    private Snapshot? last;

    // Events per repo, per session, in snapshot order, as [ticks, input, cache write, cache read, output].
    // Ticketed subagents per repo, per session, likewise (M.ORCH-T255 reads them to tell what a live subagent is working).
    private sealed record Published(int Version, int Pid, Snapshot Snapshot, List<List<long[][]>> Events, List<List<PublishedSubagent[]>>? Subagents = null);

    private sealed record PublishedSubagent(string AgentId, string TicketId, string? TicketType, string Cwd, long[][] Events);

    private static long[] Pack(UsageEvent e) => [e.AtUtc.Ticks, e.Usage.Input, e.Usage.CacheCreation, e.Usage.CacheRead, e.Usage.Output];

    private static UsageEvent Unpack(long[] e) => new(new DateTime(e[0], DateTimeKind.Utc), new TokenUsage(e[1], e[2], e[3], e[4]));

    public void Write(Snapshot snapshot)
    {
        var events = snapshot.Repos.Select(r => r.Sessions.Select(s => s.Events.Select(Pack).ToArray()).ToList()).ToList();
        var subagents = snapshot.Repos.Select(r => r.Sessions.Select(s => s.TicketedSubagents
            .Select(a => new PublishedSubagent(a.AgentId, a.TicketId, a.TicketType, a.Cwd, [.. a.Events.Select(Pack)])).ToArray()).ToList()).ToList();
        var temp = $"{Path}.{Environment.ProcessId}.tmp";
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllBytes(temp, JsonSerializer.SerializeToUtf8Bytes(new Published(FormatVersion, Environment.ProcessId, snapshot, events, subagents), Json));
            File.Move(temp, Path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try { File.Delete(temp); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>The published snapshot when there's one younger than <see cref="MaxAge"/>; reread only when the file changed.</summary>
    public Snapshot? Read(DateTime nowUtc)
    {
        try
        {
            var info = new FileInfo(Path);
            if (!info.Exists) return null;
            if (last is null || info.LastWriteTimeUtc != readStamp || info.Length != readLength)
            {
                var published = JsonSerializer.Deserialize<Published>(File.ReadAllBytes(Path), Json);
                last = published is { Version: FormatVersion } ? Restore(published) : null;
                (readStamp, readLength) = (info.LastWriteTimeUtc, info.Length);
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException or IndexOutOfRangeException or ArgumentException)
        {
            last = null;
            readLength = -1;
        }
        return last is not null && nowUtc - last.TakenUtc <= MaxAge ? last : null;
    }

    private static Snapshot Restore(Published p)
    {
        var s = p.Snapshot;
        var repos = s.Repos.Select((r, i) => r with
        {
            Sessions = [.. r.Sessions.Select((x, j) => x with
            {
                Events = [.. p.Events[i][j].Select(Unpack)],
                TicketedSubagents = p.Subagents is null ? [] : [.. p.Subagents[i][j].Select(a =>
                    new TicketedSubagent(a.AgentId, a.TicketId, a.TicketType, a.Cwd, [.. a.Events.Select(Unpack)]))],
            })],
        }).ToList();
        // Lookups are case-insensitive where they're built; JSON brings back plain ordinal ones.
        return s with
        {
            Repos = repos,
            LinkedRepos = s.LinkedRepos.ToDictionary(kv => kv.Key,
                kv => (IReadOnlySet<string>)new HashSet<string>(kv.Value, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase),
            Requires = s.Requires.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase),
        };
    }

    /// <summary>System.Text.Json writes an <see cref="IReadOnlySet{T}"/> but can't make one: read it back as a case-insensitive set.</summary>
    private sealed class ReadOnlySetConverter : JsonConverter<IReadOnlySet<string>>
    {
        public override IReadOnlySet<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            new HashSet<string>(JsonSerializer.Deserialize<List<string>>(ref reader, options) ?? [], StringComparer.OrdinalIgnoreCase);

        public override void Write(Utf8JsonWriter writer, IReadOnlySet<string> value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value.ToList(), options);
    }
}
