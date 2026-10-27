using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MindAttic.Orchestrator.Term;

/// <summary>The owner's choices for one session's remote broadcast (M.ORCH-T935).</summary>
public sealed record TermBroadcastSession
{
    /// <summary>Shared with remote viewers: true or false for this session, null to follow <see cref="TermBroadcastSettings.ShareAll"/>.</summary>
    [JsonPropertyName("shared")] public bool? Shared { get; init; }

    /// <summary>Whether token-shaped strings and Vault-held values are masked on its remote streams. On by default.</summary>
    [JsonPropertyName("masked")] public bool Masked { get; init; } = true;

    /// <summary>Paused by the owner: remote viewers get a Paused frame and nothing else until it's resumed.</summary>
    [JsonPropertyName("paused")] public bool Paused { get; init; }

    /// <summary>When this session was last paused. Once it has been, a remote attach gets no history (the host's replay may hold
    /// what the pause hid), only what follows.</summary>
    [JsonPropertyName("lastPausedUtc")] public DateTime? LastPausedUtc { get; init; }
}

/// <summary>
/// Which hosted sessions the team gateway may show remote viewers, and how (M.ORCH-T935): <c>broadcast.json</c> in the gateway's
/// folder. The dashboard and <c>orchestrator broadcast</c> write it; the running gateway rereads it within a fraction of a second,
/// so sharing, masking and pausing change without restarting it (which would drop every viewer). Remote sharing is off by
/// default: a session goes out only when the owner shares it, or shares every session with <see cref="ShareAll"/>.
/// It never holds a secret. The local viewer is never affected by anything here.
/// </summary>
public sealed record TermBroadcastSettings
{
    /// <summary>"Share my sessions": every session is shared unless it says otherwise. Off by default.</summary>
    [JsonPropertyName("shareAll")] public bool ShareAll { get; init; }

    /// <summary>Every session's broadcast is paused.</summary>
    [JsonPropertyName("pauseAll")] public bool PauseAll { get; init; }

    /// <summary>When every session was last paused at once (see <see cref="TermBroadcastSession.LastPausedUtc"/>).</summary>
    [JsonPropertyName("lastPauseAllUtc")] public DateTime? LastPauseAllUtc { get; init; }

    /// <summary>Per session, by id (lower case).</summary>
    [JsonPropertyName("sessions")] public Dictionary<string, TermBroadcastSession> Sessions { get; init; } = [];

    public TermBroadcastSession? Of(string id) => Sessions.GetValueOrDefault(Key(id));

    public bool IsShared(string id) => Of(id)?.Shared ?? ShareAll;

    public bool IsPaused(string id) => PauseAll || Of(id)?.Paused == true;

    public bool IsMasked(string id) => Of(id)?.Masked ?? true;

    /// <summary>Whether a remote attach to the session started at <paramref name="startedUtc"/> gets its history: not once it has
    /// been paused, alone or with every session.</summary>
    public bool HistoryFenced(string id, DateTime startedUtc) =>
        Of(id)?.LastPausedUtc is not null || (LastPauseAllUtc is { } all && all >= startedUtc);

    /// <summary>A copy with <paramref name="id"/>'s entry changed by <paramref name="change"/>.</summary>
    public TermBroadcastSettings WithSession(string id, Func<TermBroadcastSession, TermBroadcastSession> change)
    {
        var sessions = new Dictionary<string, TermBroadcastSession>(Sessions) { [Key(id)] = change(Of(id) ?? new TermBroadcastSession()) };
        return this with { Sessions = sessions };
    }

    public TermBroadcastSettings Share(string id, bool shared) => WithSession(id, s => s with { Shared = shared });

    public TermBroadcastSettings Mask(string id, bool masked) => WithSession(id, s => s with { Masked = masked });

    public TermBroadcastSettings Pause(string id, bool paused, DateTime nowUtc) =>
        WithSession(id, s => s with { Paused = paused, LastPausedUtc = paused ? nowUtc : s.LastPausedUtc ?? nowUtc });

    /// <summary>Pauses or resumes every session. Resuming all also resumes each session paused on its own.</summary>
    public TermBroadcastSettings PauseEvery(bool paused, DateTime nowUtc)
    {
        if (paused) return this with { PauseAll = true, LastPauseAllUtc = nowUtc };
        var sessions = Sessions.ToDictionary(p => p.Key, p => p.Value.Paused ? p.Value with { Paused = false } : p.Value);
        // Resuming moves the fence to now: a session started during the pause has history the pause hid too.
        return this with { PauseAll = false, LastPauseAllUtc = PauseAll ? nowUtc : LastPauseAllUtc, Sessions = sessions };
    }

    /// <summary>A copy without the entries of sessions not in <paramref name="liveIds"/>.</summary>
    public TermBroadcastSettings Prune(IEnumerable<string> liveIds)
    {
        var live = liveIds.Select(Key).ToHashSet();
        return Sessions.Keys.All(live.Contains) ? this
            : this with { Sessions = Sessions.Where(p => live.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value) };
    }

    internal static string Key(string id) => id.Trim().ToLowerInvariant();
}

/// <summary>Reads and writes <see cref="TermBroadcastSettings"/> (M.ORCH-T935). Nothing here throws on an I/O failure.</summary>
public static class TermBroadcast
{
    public const string FileName = "broadcast.json";

    public static string PathIn(string? directory = null) => Path.Combine(directory ?? TermGateway.DefaultDirectory, FileName);

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>The settings as written; the defaults (nothing shared) when there are none or they can't be read.</summary>
    public static TermBroadcastSettings Read(string? directory = null) =>
        TryRead(directory, out var settings) ? settings : new TermBroadcastSettings();

    /// <summary>Reads the settings (M.ORCH-T986): false when the file couldn't be read just now (locked, a sharing violation, no
    /// access), so a caller keeps the last good settings and tries again. True otherwise, with the defaults when there are none
    /// or they aren't settings.</summary>
    public static bool TryRead(string? directory, out TermBroadcastSettings settings)
    {
        settings = new TermBroadcastSettings();
        try
        {
            var path = PathIn(directory);
            if (!File.Exists(path)) return true;
            settings = Parse(File.ReadAllText(path));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>Settings from JSON text; the defaults when it isn't any.</summary>
    public static TermBroadcastSettings Parse(string json)
    {
        try
        {
            var read = JsonSerializer.Deserialize<TermBroadcastSettings>(json, Json) ?? new TermBroadcastSettings();
            // Ids are matched in lower case, whatever wrote them.
            return read with
            {
                Sessions = (read.Sessions ?? []).GroupBy(p => TermBroadcastSettings.Key(p.Key))
                    .ToDictionary(g => g.Key, g => g.Last().Value ?? new TermBroadcastSession()),
            };
        }
        catch (JsonException) { return new TermBroadcastSettings(); }
    }

    /// <summary>Changes the settings under a lock shared by every writer, atomically; the settings now, or null when they
    /// couldn't be written.</summary>
    public static TermBroadcastSettings? Update(Func<TermBroadcastSettings, TermBroadcastSettings> change, string? directory = null)
    {
        using var mutex = new Mutex(false, @"Local\mindattic-orchestrator-broadcast");
        var owned = false;
        try
        {
            try { owned = mutex.WaitOne(TimeSpan.FromSeconds(2)); }
            catch (AbandonedMutexException) { owned = true; }
            // Never a read-modify-write without the lock: another writer's change would be lost (bughunt 2026-10-27).
            if (!owned) return null;
            // A file that can't be read just now is never replaced by a change to the defaults (M.ORCH-T986).
            TermBroadcastSettings now = new();
            var read = false;
            for (var attempt = 0; attempt < 5 && !(read = TryRead(directory, out now)); attempt++) Thread.Sleep(20 * (attempt + 1));
            if (!read) return null;
            var next = change(now);
            return Write(next, directory) ? next : null;
        }
        finally
        {
            if (owned) mutex.ReleaseMutex();
        }
    }

    private static bool Write(TermBroadcastSettings settings, string? directory)
    {
        var dir = directory ?? TermGateway.DefaultDirectory;
        var path = PathIn(dir);
        var temp = path + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, Json), new UTF8Encoding(false));
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            try { File.Delete(temp); } catch (Exception) { }
            return false;
        }
    }
}
