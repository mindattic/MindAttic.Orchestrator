namespace MindAttic.Orchestrator.Sessions;

/// <summary>What the pruner made of one session's temp folder: its size, whether it goes, and why.</summary>
public sealed record TempFolderVerdict(string Path, string SessionId, long Bytes, bool Delete, string Why);

/// <summary>A prune: every session folder's verdict, how many went and their bytes, and the ones that couldn't be deleted.</summary>
public sealed record TempPruneResult(IReadOnlyList<TempFolderVerdict> Folders, int Deleted, long Bytes, IReadOnlyList<string> Errors)
{
    /// <summary>Running sessions' scratchpad entries past the retention (M.ORCH-T1466) that went (or, in a dry run, would);
    /// their bytes are in <see cref="Bytes"/>.</summary>
    public IReadOnlyList<TempFolderVerdict> Scratch { get; init; } = [];
}

/// <summary>What the age rule (M.ORCH-T1466) makes of one session folder.</summary>
public enum TempAge
{
    /// <summary>No retention set: age decides nothing.</summary>
    Off,
    /// <summary>Created, or something in it created or written, within the retention: kept by age.</summary>
    Keep,
    /// <summary>Past the retention, quiet as long, and its session isn't running: it may go, if nothing uses it.</summary>
    MayGo,
    /// <summary>Past the retention but its session runs: the folder stays, only scratchpad entries past it and as quiet go.</summary>
    TrimScratch,
}

/// <summary>
/// Claude Code gives each session a folder %TEMP%\claude\&lt;project&gt;\&lt;session id&gt; (its scratchpad, and tasks\*.output, the
/// logs of its background commands and subagents) and never removes it: 151 folders, 6.7 GB, when C: hit 0 bytes free
/// (M.ORCH-T473). The daemon deletes a folder on its start and every <see cref="Every"/> after, but only by liveness, never by
/// age (M.ORCH-T1467): its session has definitely ended (<see cref="SessionLiveness.Running"/> says false), no running process
/// names it, nothing kept points into it, and no file in it is held open. A session that can't be told is kept, however long
/// it's been quiet; with a retention set (<see cref="Settings.OrchestratorSettings.TempRetentionHours"/>, M.ORCH-T1466), one
/// created longer ago than it, with nothing in it created or written for as long, goes too, still only when no process names it,
/// nothing kept points into it and nothing in it is open. A running session's folder is never deleted: past the retention only
/// its scratchpad entries that old and that quiet go. Links are never followed: one is deleted as a link, or left.
/// </summary>
public static class SessionTempPruner
{
    /// <summary>How often the daemon looks for ended sessions' folders.</summary>
    public static readonly TimeSpan Every = TimeSpan.FromMinutes(10);

    public static string BaseDir() => Path.Combine(Path.GetTempPath(), "claude");

    // Hidden and system files are the session's too; only links are skipped.
    private static readonly EnumerationOptions Walk = new()
    {
        RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false,
    };

    // Every folder in one folder (not below it), links included.
    private static readonly EnumerationOptions Links = new() { AttributesToSkip = 0, IgnoreInaccessible = false };

    /// <summary>Deletes the folder of every session that has definitely ended; returns how many and their bytes.</summary>
    public static (int Folders, long Bytes) Prune(string baseDir, Func<string, bool?> running) =>
        Prune(baseDir, SessionLiveness.From(running)) is var r ? (r.Deleted, r.Bytes) : default;

    /// <summary>
    /// Judges every session folder under <paramref name="baseDir"/> and deletes those that may go (with
    /// <paramref name="dryRun"/>, nothing: the verdicts say what would). Each deletion is appended to <paramref name="logPath"/>.
    /// <paramref name="retentionHours"/> over 0 adds the age rule (<see cref="PlanAge"/>), judged at <paramref name="nowUtc"/>.
    /// </summary>
    public static TempPruneResult Prune(string baseDir, SessionLiveness liveness, bool dryRun = false, string? logPath = null,
        double retentionHours = 0, DateTime? nowUtc = null)
    {
        var verdicts = new List<TempFolderVerdict>();
        var scratch = new List<TempFolderVerdict>();
        var errors = new List<string>();
        var (deleted, bytes) = (0, 0L);
        var now = nowUtc ?? DateTime.UtcNow;
        if (!Directory.Exists(baseDir)) return new TempPruneResult(verdicts, 0, 0, errors);
        foreach (var project in SafeDirectories(baseDir))
        {
            foreach (var dir in SafeDirectories(project))
            {
                var id = Path.GetFileName(dir);
                // Only a session's own folder (its name is the session id): anything else in there isn't one to judge.
                if (!Guid.TryParse(id, out _)) continue;
                try
                {
                    // Never through a junction or symlink (a scratchpad's pnpm node_modules): its target isn't the session's,
                    // so it's not held or counted; the delete removes only the link.
                    var files = new DirectoryInfo(dir).GetFiles("*", Walk);
                    // Summed before any delete: a FileInfo that's been refreshed (clearing its flag does) can't be read after.
                    var size = files.Sum(f => f.Length);
                    var (go, why) = Judge(dir, id, files, liveness, retentionHours, now, out var trim);
                    var verdict = new TempFolderVerdict(dir, id, size, go, why);
                    verdicts.Add(verdict);
                    if (trim)
                        foreach (var entry in TrimScratch(dir, id, retentionHours, now, dryRun, logPath, errors))
                        {
                            scratch.Add(entry);
                            if (!dryRun) bytes += entry.Bytes;
                        }
                    if (!go || dryRun) continue;
                    // A read-only file (a git checkout's objects, build output) can't be deleted: clear the flag first (M.ORCH-T482).
                    foreach (var f in files) ClearReadOnly(f);
                    DeleteTree(dir);
                    deleted++;
                    bytes += size;
                    Log(logPath, verdict);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    errors.Add($"{dir}: {ex.Message}");
                }
            }
            if (dryRun) continue;
            try { if (!Directory.EnumerateFileSystemEntries(project).Any()) Directory.Delete(project); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return new TempPruneResult(verdicts, deleted, bytes, errors) { Scratch = scratch };
    }

    /// <summary>Whether a session's folder may go, and why: in order, the session ended (or, with a retention, it isn't running
    /// and the folder is past the retention and as quiet, <see cref="PlanAge"/>), nothing kept points into it, no running process
    /// names it, no file in it is open. Each check that can't be made keeps the folder. <paramref name="trimScratch"/> says a
    /// running session's folder is past the retention, so its old quiet scratchpad entries may go.</summary>
    internal static (bool Delete, string Why) Judge(string dir, string id, FileInfo[] files, SessionLiveness liveness,
        double retentionHours, DateTime nowUtc, out bool trimScratch)
    {
        trimScratch = false;
        var session = liveness.Running(id);
        var state = $"ended ({session.Why})";
        if (session.Running is not false)
        {
            var age = retentionHours > 0 ? PlanAge(session.Running, CreatedUtc(dir), Newest(Tree(dir, files)), nowUtc, retentionHours) : TempAge.Off;
            trimScratch = age == TempAge.TrimScratch;
            if (age != TempAge.MayGo)
                return (false, $"kept: {(session.Running is true ? "running" : "can't tell if it ended")} ({session.Why})");
            state = $"past the {retentionHours:0.#}h retention and quiet as long ({session.Why})";
        }
        if (liveness.ReferencedBy(id) is { } where) return (false, $"kept: {state}, but {where} points into it");
        if (liveness.UsedBy(dir) is { } users) return (false, $"kept: {state}, but {users}");
        if (files.FirstOrDefault(InUse) is { } open) return (false, $"kept: {state}, but {Path.GetRelativePath(dir, open.FullName)} is open");
        return (true, session.Running is false ? $"ended: {session.Why}" : $"aged: {state}");
    }

    /// <summary>
    /// The age rule (M.ORCH-T1466), pure: a folder created at <paramref name="createdUtc"/> whose newest creation or write in it
    /// (the folder's own included) is <paramref name="newestUtc"/>. Within <paramref name="retentionHours"/> of
    /// <paramref name="nowUtc"/> by either, it's kept; past it, a running session's folder only has its scratch trimmed
    /// (<see cref="Stale"/> judges each entry), and any other may go. 0 (or less) is off.
    /// </summary>
    public static TempAge PlanAge(bool? running, DateTime createdUtc, DateTime newestUtc, DateTime nowUtc, double retentionHours)
    {
        if (!(retentionHours > 0) || !double.IsFinite(retentionHours)) return TempAge.Off;
        var window = TimeSpan.FromHours(retentionHours);
        if (nowUtc - createdUtc <= window) return TempAge.Keep;
        if (running is true) return TempAge.TrimScratch;
        return nowUtc - newestUtc <= window ? TempAge.Keep : TempAge.MayGo;
    }

    /// <summary>Whether one scratchpad entry of a running session may go (M.ORCH-T1466): created, and nothing in it created or
    /// written, longer than <paramref name="retentionHours"/> before <paramref name="nowUtc"/>.</summary>
    public static bool Stale(DateTime createdUtc, DateTime newestUtc, DateTime nowUtc, double retentionHours) =>
        retentionHours > 0 && double.IsFinite(retentionHours)
        && nowUtc - createdUtc > TimeSpan.FromHours(retentionHours) && nowUtc - newestUtc > TimeSpan.FromHours(retentionHours);

    /// <summary>
    /// Deletes a running session's scratchpad entries past the retention (<see cref="Stale"/>): top-level ones only, never a link
    /// (it's left, not followed), and never one with a file open in it. The session folder and its <c>tasks</c> are left.
    /// </summary>
    private static List<TempFolderVerdict> TrimScratch(string dir, string id, double retentionHours, DateTime nowUtc, bool dryRun,
        string? logPath, List<string> errors)
    {
        var went = new List<TempFolderVerdict>();
        var pad = new DirectoryInfo(Path.Combine(dir, "scratchpad"));
        FileSystemInfo[] entries;
        try
        {
            if (!pad.Exists || pad.Attributes.HasFlag(FileAttributes.ReparsePoint)) return went;
            entries = pad.GetFileSystemInfos("*", Links);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return went; }
        foreach (var entry in entries)
        {
            try
            {
                if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                var files = entry is DirectoryInfo d ? d.GetFiles("*", Walk) : [(FileInfo)entry];
                var newest = entry is DirectoryInfo tree ? Newest(Tree(tree.FullName, files)) : Newest([entry]);
                if (!Stale(entry.CreationTimeUtc, newest, nowUtc, retentionHours)) continue;
                if (files.Any(InUse)) continue;
                var size = files.Sum(f => f.Length);
                var verdict = new TempFolderVerdict(entry.FullName, id, size, true,
                    $"aged scratch of a running session: created {(nowUtc - entry.CreationTimeUtc).TotalHours:0}h ago, nothing written in it for {(nowUtc - newest).TotalHours:0}h (retention {retentionHours:0.#}h)");
                if (!dryRun)
                {
                    foreach (var f in files) ClearReadOnly(f);
                    if (entry is DirectoryInfo) DeleteTree(entry.FullName);
                    else entry.Delete();
                    Log(logPath, verdict);
                }
                went.Add(verdict);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add($"{entry.FullName}: {ex.Message}");
            }
        }
        return went;
    }

    /// <summary>A folder, every folder below it (not through links) and <paramref name="files"/>: what its newest time is read from.</summary>
    private static IEnumerable<FileSystemInfo> Tree(string dir, FileInfo[] files)
    {
        var root = new DirectoryInfo(dir);
        return files.Concat<FileSystemInfo>(root.GetDirectories("*", Walk)).Append(root);
    }

    /// <summary>The newest creation or last write among <paramref name="entries"/>: a copy keeps its source's write time, so its
    /// creation time is what says it's new.</summary>
    internal static DateTime Newest(IEnumerable<FileSystemInfo> entries) =>
        entries.Select(e => e.CreationTimeUtc > e.LastWriteTimeUtc ? e.CreationTimeUtc : e.LastWriteTimeUtc).DefaultIfEmpty(DateTime.MaxValue).Max();

    private static DateTime CreatedUtc(string dir) => Directory.GetCreationTimeUtc(dir);

    /// <summary>Deletes a folder: links first, each by itself (a recursive delete removes a junction but then throws, as it tries
    /// it as a volume mount point too, so the folder stayed), then the rest; nothing is followed through a link.</summary>
    private static void DeleteTree(string dir)
    {
        foreach (var d in new DirectoryInfo(dir).GetDirectories("*", Walk).Prepend(new DirectoryInfo(dir)))
            foreach (var link in d.GetDirectories("*", Links).Where(l => l.Attributes.HasFlag(FileAttributes.ReparsePoint))) link.Delete();
        Directory.Delete(dir, recursive: true);
    }

    private static void Log(string? logPath, TempFolderVerdict v)
    {
        if (logPath is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, $"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}\t{v.Bytes}\t{v.Why}\t{v.Path}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private static IEnumerable<string> SafeDirectories(string dir)
    {
        // A project or session folder that is itself a junction or symlink is left alone (M.ORCH-T1466): judging it would walk
        // its target, which isn't under %TEMP%\claude.
        try { return new DirectoryInfo(dir).GetDirectories("*", Links).Where(d => !d.Attributes.HasFlag(FileAttributes.ReparsePoint)).Select(d => d.FullName).ToList(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }
    }

    private static void ClearReadOnly(FileInfo file)
    {
        try { if (file.IsReadOnly) file.IsReadOnly = false; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Whether any process has the file open. Opened for reading, sharing nothing: any other handle on it refuses
    /// that, and a read-only file can still be opened this way, so the probe changes nothing (a dry run stays one).</summary>
    private static bool InUse(FileInfo file)
    {
        try
        {
            using var _ = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
            return false;
        }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return true; }
    }
}
