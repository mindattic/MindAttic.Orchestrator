namespace MindAttic.Orchestrator.Sessions;

/// <summary>What the pruner made of one session's temp folder: its size, whether it goes, and why.</summary>
public sealed record TempFolderVerdict(string Path, string SessionId, long Bytes, bool Delete, string Why);

/// <summary>A prune: every session folder's verdict, how many went and their bytes, and the ones that couldn't be deleted.</summary>
public sealed record TempPruneResult(IReadOnlyList<TempFolderVerdict> Folders, int Deleted, long Bytes, IReadOnlyList<string> Errors);

/// <summary>
/// Claude Code gives each session a folder %TEMP%\claude\&lt;project&gt;\&lt;session id&gt; (its scratchpad, and tasks\*.output, the
/// logs of its background commands and subagents) and never removes it: 151 folders, 6.7 GB, when C: hit 0 bytes free
/// (M.ORCH-T473). The daemon deletes a folder on its start and every <see cref="Every"/> after, but only by liveness, never by
/// age (M.ORCH-T1467): its session has definitely ended (<see cref="SessionLiveness.Running"/> says false), no running process
/// names it, nothing kept points into it, and no file in it is held open. A session that can't be told is kept, however long
/// it's been quiet.
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
    /// </summary>
    public static TempPruneResult Prune(string baseDir, SessionLiveness liveness, bool dryRun = false, string? logPath = null)
    {
        var verdicts = new List<TempFolderVerdict>();
        var errors = new List<string>();
        var (deleted, bytes) = (0, 0L);
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
                    var (go, why) = Judge(dir, id, files, liveness);
                    var verdict = new TempFolderVerdict(dir, id, size, go, why);
                    verdicts.Add(verdict);
                    if (!go || dryRun) continue;
                    // A read-only file (a git checkout's objects, build output) can't be deleted: clear the flag first (M.ORCH-T482).
                    foreach (var f in files) ClearReadOnly(f);
                    // Links first, each by itself: a recursive delete removes a junction but then throws (it tries it as a
                    // volume mount point too), so the folder stayed.
                    foreach (var d in new DirectoryInfo(dir).GetDirectories("*", Walk).Prepend(new DirectoryInfo(dir)))
                        foreach (var link in d.GetDirectories("*", Links).Where(l => l.Attributes.HasFlag(FileAttributes.ReparsePoint))) link.Delete();
                    Directory.Delete(dir, recursive: true);
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
        return new TempPruneResult(verdicts, deleted, bytes, errors);
    }

    /// <summary>Whether a session's folder may go, and why: in order, the session ended, nothing kept points into it, no
    /// running process names it, no file in it is open. Each check that can't be made keeps the folder.</summary>
    internal static (bool Delete, string Why) Judge(string dir, string id, FileInfo[] files, SessionLiveness liveness)
    {
        var session = liveness.Running(id);
        if (session.Running is not false) return (false, $"kept: {(session.Running is true ? "running" : "can't tell if it ended")} ({session.Why})");
        if (liveness.ReferencedBy(id) is { } where) return (false, $"kept: ended ({session.Why}), but {where} points into it");
        if (liveness.UsedBy(dir) is { } users) return (false, $"kept: ended ({session.Why}), but {users}");
        if (files.FirstOrDefault(InUse) is { } open) return (false, $"kept: ended ({session.Why}), but {Path.GetRelativePath(dir, open.FullName)} is open");
        return (true, $"ended: {session.Why}");
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
        try { return Directory.GetDirectories(dir); }
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
