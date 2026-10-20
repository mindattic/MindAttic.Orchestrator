namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// Claude Code gives each session a folder %TEMP%\claude\&lt;project&gt;\&lt;session id&gt; (its scratchpad, and tasks\*.output, the
/// logs of its background commands) and never removes it: 151 folders, 6.7 GB, when C: hit 0 bytes free (M.ORCH-T473). Once a
/// session has ended its folder is deleted: on the daemon's start and every <see cref="Every"/> after, which is how a
/// session's folder goes soon after its ticket is closed and its CLI exits, or the reaper ends it.
/// </summary>
public static class SessionTempPruner
{
    /// <summary>How often the daemon looks for ended sessions' folders.</summary>
    public static readonly TimeSpan Every = TimeSpan.FromMinutes(10);

    /// <summary>A folder whose session the context gate never recorded (no process id to check) is ended once nothing in
    /// it has been written for this long: longer than any session stays quiet while it's still open.</summary>
    public static readonly TimeSpan UnknownQuietLife = TimeSpan.FromDays(1);

    public static string BaseDir() => Path.Combine(Path.GetTempPath(), "claude");

    // Hidden and system files are the session's too; only links are skipped.
    private static readonly EnumerationOptions Walk = new()
    {
        RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false,
    };

    // Every folder in one folder (not below it), links included.
    private static readonly EnumerationOptions Links = new() { AttributesToSkip = 0, IgnoreInaccessible = false };

    /// <summary>Deletes the folder of every session that has ended; returns how many and their bytes.</summary>
    /// <param name="running">Whether a session is running: true or false when its CLI is known, null when it isn't (then
    /// only <see cref="UnknownQuietLife"/> of quiet ends it).</param>
    public static (int Folders, long Bytes) Prune(string baseDir, Func<string, bool?> running, DateTime nowUtc)
    {
        if (!Directory.Exists(baseDir)) return (0, 0);
        var (folders, bytes) = (0, 0L);
        foreach (var project in SafeDirectories(baseDir))
        {
            foreach (var dir in SafeDirectories(project))
            {
                // Only a session's own folder (its name is the session id): anything else in there isn't one to judge.
                if (!Guid.TryParse(Path.GetFileName(dir), out _)) continue;
                try
                {
                    // Never through a junction or symlink (a scratchpad's pnpm node_modules): its target isn't the session's,
                    // so it's not cleared, held or counted; the delete removes only the link.
                    var files = new DirectoryInfo(dir).GetFiles("*", Walk);
                    var ended = running(Path.GetFileName(dir)) switch
                    {
                        true => false,
                        false => true,
                        null => nowUtc - LastWrite(dir, files) >= UnknownQuietLife,
                    };
                    if (!ended) continue;
                    // A read-only file (a git checkout's objects, build output) can't be opened for writing nor deleted:
                    // clear the flag first, so only a file another process holds keeps the folder (M.ORCH-T482).
                    foreach (var f in files) ClearReadOnly(f);
                    // A background command still writing its output holds the file open: that folder waits for the next look.
                    if (files.Any(InUse)) continue;
                    // Summed before the delete: a FileInfo that's been refreshed (clearing its flag does) can't be read after.
                    var size = files.Sum(f => f.Length);
                    // Links first, each by itself: a recursive delete removes a junction but then throws (it tries it as a
                    // volume mount point too), so the folder stayed.
                    foreach (var d in new DirectoryInfo(dir).GetDirectories("*", Walk).Prepend(new DirectoryInfo(dir)))
                        foreach (var link in d.GetDirectories("*", Links).Where(l => l.Attributes.HasFlag(FileAttributes.ReparsePoint))) link.Delete();
                    Directory.Delete(dir, recursive: true);
                    folders++;
                    bytes += size;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
            try { if (!Directory.EnumerateFileSystemEntries(project).Any()) Directory.Delete(project); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return (folders, bytes);
    }

    private static IEnumerable<string> SafeDirectories(string dir)
    {
        try { return Directory.GetDirectories(dir); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }
    }

    private static DateTime LastWrite(string dir, FileInfo[] files) =>
        files.Select(f => f.LastWriteTimeUtc).Append(Directory.GetLastWriteTimeUtc(dir)).Max();

    private static void ClearReadOnly(FileInfo file)
    {
        try { if (file.IsReadOnly) file.IsReadOnly = false; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private static bool InUse(FileInfo file)
    {
        try
        {
            using var _ = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return true; }
    }
}
