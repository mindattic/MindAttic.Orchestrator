using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MindAttic.Orchestrator.Term;

/// <summary>
/// The TermHost session registry (M.ORCH-T925): one JSON file per live host, <see cref="DefaultDirectory"/>\&lt;id&gt;.json,
/// written atomically (a temporary file renamed over it), so a reader never sees half a record. The files are the truth:
/// discovery works with no daemon running, and the <c>term-session</c> bus kind (<see cref="TermBus"/>) is only the fast path.
/// <para>Writers: the host itself (<see cref="Write"/> and <see cref="Delete"/>). Readers: the dashboard on start and anyone
/// listing sessions (<see cref="Enumerate()"/>, <see cref="Get(string)"/>). A reader prunes what a crashed host left behind.
/// Nothing here throws on an I/O failure: a write that fails returns false, a file that can't be read is skipped.</para>
/// </summary>
public static class TermSessions
{
    /// <summary><c>%LOCALAPPDATA%\MindAttic\MindAttic.Orchestrator\terms</c>, beside each host's <c>&lt;id&gt;.log</c>.</summary>
    public static string DefaultDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MindAttic", "MindAttic.Orchestrator", "terms");

    /// <summary>How far a process's start time may differ from <see cref="TermSessionRecord.HostStartUtc"/> and still be that host
    /// (both come from the same FILETIME; this only absorbs rounding).</summary>
    internal static readonly TimeSpan StartTolerance = TimeSpan.FromMilliseconds(2);

    /// <summary>How old an unreadable record or a leftover temporary file must be before a reader deletes it.</summary>
    internal static readonly TimeSpan LeftoverAge = TimeSpan.FromMinutes(1);

    internal static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>The registry file of session <paramref name="id"/>.</summary>
    public static string PathOf(string id, string? directory = null) =>
        Path.Combine(directory ?? DefaultDirectory, TermProtocol.Sanitize(id) + ".json");

    /// <summary>Writes <paramref name="session"/>'s file atomically; false when it couldn't be written.</summary>
    public static bool Write(TermSessionRecord session, string? directory = null)
    {
        // Always the record alone, never a bus event's extra field.
        if (session is TermSessionEvent e) session = e.ToRecord();
        var dir = directory ?? DefaultDirectory;
        var path = PathOf(session.Id, dir);
        var temp = path + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(temp, JsonSerializer.Serialize(session, Json), new UTF8Encoding(false));
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    File.Move(temp, path, overwrite: true);
                    return true;
                }
                // A reader holding the old file open for a moment.
                catch (Exception ex) when (attempt < 10 && ex is IOException or UnauthorizedAccessException) { Thread.Sleep(20); }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            TryDelete(temp);
            return false;
        }
    }

    /// <summary>Deletes session <paramref name="id"/>'s file, as its host exits; false when it couldn't be.</summary>
    public static bool Delete(string id, string? directory = null) => TryDelete(PathOf(id, directory));

    /// <summary>Reads one registry file; null when it is missing, unreadable or not a record.</summary>
    public static TermSessionRecord? Read(string path) => Read(path, out _);

    /// <summary><see cref="Read(string)"/>; <paramref name="junk"/> is true only when the file was read and holds no record. A
    /// file that couldn't be read (held open by antivirus or an indexer) is not junk (M.ORCH-T995).</summary>
    private static TermSessionRecord? Read(string path, out bool junk) => Read(path, out junk, out _);

    /// <summary><see cref="Read(string, out bool)"/>; <paramref name="unreadable"/> is true when the file is there but couldn't
    /// be read on any try, so whether it holds a record is unknown (M.ORCH-T1035).</summary>
    private static TermSessionRecord? Read(string path, out bool junk, out bool unreadable)
    {
        junk = false;
        unreadable = false;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var record = JsonSerializer.Deserialize<TermSessionRecord>(File.ReadAllText(path), Json);
                if (record is { Id.Length: > 0, HostPid: > 0 }) return record;
                junk = true;
                return null;
            }
            catch (JsonException) { junk = true; return null; }
            // The rename of a write in progress: try again in a moment.
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Thread.Sleep(20); }
        }
        unreadable = true;
        return null;
    }

    /// <summary>The live sessions in <see cref="DefaultDirectory"/>, oldest first. See <see cref="Enumerate(string?)"/>.</summary>
    public static IReadOnlyList<TermSessionRecord> Enumerate() => Enumerate(null);

    /// <summary>
    /// The live sessions in <paramref name="directory"/> (<see cref="DefaultDirectory"/> when null), oldest first. A record whose
    /// host process is gone, or whose pid now belongs to a process that started at another time (a reused pid), is stale: it
    /// is left out and its file deleted. A file that isn't a record, and a temporary file a crashed write left, are deleted once
    /// a minute old; a file that can't be read just now is skipped, never deleted. A session whose CLI has exited is still live while its host lingers (<see cref="TermSessionRecord.Exited"/>).
    /// </summary>
    public static IReadOnlyList<TermSessionRecord> Enumerate(string? directory) => Enumerate(directory, HostStartOf);

    /// <summary><see cref="Enumerate(string?)"/> with <paramref name="hostStart"/> saying when a pid's process started (UTC),
    /// or null when no process has it; tests pass their own.</summary>
    internal static IReadOnlyList<TermSessionRecord> Enumerate(string? directory, Func<int, DateTime?> hostStart)
    {
        var dir = directory ?? DefaultDirectory;
        var live = new List<TermSessionRecord>();
        string[] files;
        try
        {
            if (!Directory.Exists(dir)) return live;
            files = Directory.GetFiles(dir, "*.json");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return live; }

        foreach (var file in files)
        {
            if (Read(file, out var junk) is not { } record)
            {
                // M.ORCH-T995: never on a read failure: a live host's file held open for a moment would vanish from discovery.
                // Only a file read whole that is no record, and only once it is old. A record is deleted only when its host
                // is gone or its pid reused (below).
                if (junk && Age(file) > LeftoverAge) TryDelete(file);
                continue;
            }
            if (IsAlive(record, hostStart)) live.Add(record);
            else TryDelete(file);
        }
        try
        {
            foreach (var temp in Directory.GetFiles(dir, "*.json.*.tmp"))
                if (Age(temp) > LeftoverAge) TryDelete(temp);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return [.. live.OrderBy(r => r.StartedUtc).ThenBy(r => r.Id, StringComparer.Ordinal)];
    }

    /// <summary>Session <paramref name="id"/> when it is live; null when it isn't (its stale file is deleted).</summary>
    public static TermSessionRecord? Get(string id, string? directory = null) => Get(id, directory, HostStartOf);

    internal static TermSessionRecord? Get(string id, string? directory, Func<int, DateTime?> hostStart)
    {
        var path = PathOf(id, directory);
        if (Read(path) is not { } record) return null;
        if (IsAlive(record, hostStart)) return record;
        TryDelete(path);
        return null;
    }

    /// <summary><see cref="Get(string, string?)"/>, telling a session that's gone from one whose file couldn't be read just now
    /// (held open by antivirus or an indexer): false then, with <paramref name="record"/> null and nothing deleted, as
    /// <see cref="Enumerate(string?)"/> does since M.ORCH-T995 (M.ORCH-T1035). True otherwise, with the live record or null.</summary>
    public static bool TryGet(string id, out TermSessionRecord? record, string? directory = null) => TryGet(id, directory, HostStartOf, out record);

    internal static bool TryGet(string id, string? directory, Func<int, DateTime?> hostStart, out TermSessionRecord? record)
    {
        var path = PathOf(id, directory);
        record = Read(path, out _, out var unreadable);
        if (unreadable) return false;
        if (record is null) return true;
        if (IsAlive(record, hostStart)) return true;
        record = null;
        TryDelete(path);
        return true;
    }

    /// <summary>Whether <paramref name="record"/>'s host process still runs (its pid, not reused), registered or not.</summary>
    public static bool IsHostRunning(TermSessionRecord record) => IsAlive(record, HostStartOf);

    /// <summary>Whether <paramref name="record"/>'s host is still the process running under its pid.</summary>
    internal static bool IsAlive(TermSessionRecord record, Func<int, DateTime?> hostStart) =>
        hostStart(record.HostPid) is { } started && (started - record.HostStartUtc).Duration() <= StartTolerance;

    /// <summary>When process <paramref name="pid"/> started (UTC); null when there's no such process, or it has exited.</summary>
    public static DateTime? HostStartOf(int pid)
    {
        if (pid <= 0) return null;
        try
        {
            using var process = Process.GetProcessById(pid);
            if (process.HasExited) return null;
            return process.StartTime.ToUniversalTime();
        }
        // No such process; or one of another user's, which can't be one of this user's hosts either.
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return null;
        }
    }

    private static TimeSpan Age(string path)
    {
        try { return DateTime.UtcNow - File.GetLastWriteTimeUtc(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return TimeSpan.Zero; }
    }

    private static bool TryDelete(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return false; }
    }
}
