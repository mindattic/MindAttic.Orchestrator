using System.Globalization;

namespace MindAttic.Orchestrator.Backup;

/// <summary>
/// The dated backup folder: <c>{base}\yyyy-MM-dd</c>, or <c>_a</c>..<c>_z</c> after it when that day already has
/// one, and the pruning rule that keeps only the newest N dates. Ported from MindAttic.Launcher's BackupService
/// (M.ORCH-T2046); pure over an injected existence check so tests never touch the disk.
/// </summary>
public static class BackupFolders
{
    public const string DefaultBackupBase = @"R:\Backup\MindAttic";

    /// <summary>
    /// <c>{base}\{date}</c> when it doesn't exist, else the first free of <c>{base}\{date}_a</c>..<c>_z</c>.
    /// The date is formatted with the invariant culture so a non-Gregorian locale can't rename the sequence.
    /// </summary>
    public static string ResolveTarget(string backupBase, DateTime now, Func<string, bool>? exists = null)
    {
        exists ??= Directory.Exists;
        var date = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var baseFolder = Path.Combine(backupBase, date);
        if (!exists(baseFolder)) return baseFolder;

        for (var c = 'a'; c <= 'z'; c++)
        {
            var candidate = $"{baseFolder}_{c}";
            if (!exists(candidate)) return candidate;
        }

        throw new InvalidOperationException($"All 27 dated folders for {date} already exist under {backupBase}.");
    }

    /// <summary>
    /// The dated folders to delete so only the newest <paramref name="keep"/> distinct dates remain; a date's
    /// <c>_a</c>..<c>_z</c> siblings go with their date, never one by one. Folders without a date prefix are
    /// never returned. <paramref name="keep"/> &lt;= 0 makes every dated folder stale. With <paramref name="isComplete"/>,
    /// only a date holding a complete backup counts toward <paramref name="keep"/>: failed nights newer than the oldest
    /// kept good one stay, and nothing goes until <paramref name="keep"/> good nights are newer (M.ORCH-T2296).
    /// </summary>
    public static IReadOnlyList<string> ResolveStale(IReadOnlyList<string> existingFolders, int keep, Func<string, bool>? isComplete = null)
    {
        var stale = new List<string>();
        var kept = 0;
        foreach (var night in existingFolders
                     .Select(folder => (Folder: folder, Date: ParseDatePrefix(Path.GetFileName(folder))))
                     .Where(x => x.Date.HasValue)
                     .GroupBy(x => x.Date!.Value)
                     .OrderByDescending(g => g.Key))
        {
            if (kept >= Math.Max(keep, 0)) stale.AddRange(night.Select(x => x.Folder));
            else if (isComplete is null || night.Any(x => isComplete(x.Folder))) kept++;
        }
        return stale;
    }

    /// <summary>
    /// <c>{folder}\Databases\backup-complete.txt</c>: written last by <see cref="BackupService.Run"/>, and only when every
    /// step succeeded; it sits under <c>Databases</c> so the Azure copy carries it too (M.ORCH-T2296).
    /// </summary>
    public const string CompleteMarkerFileName = "backup-complete.txt";

    public static string ResolveCompleteMarkerPath(string folder) =>
        Path.Combine(folder, SqliteBackup.DatabasesSubfolder, CompleteMarkerFileName);

    public static bool IsComplete(string folder) => File.Exists(ResolveCompleteMarkerPath(folder));

    /// <summary>
    /// The newest dated folder under <paramref name="backupBase"/>: the latest date, and of that day's folders the last
    /// (<c>_z</c> after <c>_a</c> after the bare date). Null when the base isn't reachable or holds no dated folder.
    /// <paramref name="listFolders"/> lists a folder's subfolders; tests give a stand-in (M.ORCH-T2052). A complete backup
    /// wins over a newer failed one; with none complete (backups older than the marker) the newest dated folder is used.
    /// </summary>
    public static string? FindNewest(string backupBase, Func<string, IEnumerable<string>>? listFolders = null,
        Func<string, bool>? isComplete = null)
    {
        listFolders ??= dir => Directory.Exists(dir) ? Directory.GetDirectories(dir) : [];
        isComplete ??= IsComplete;
        try
        {
            var folders = listFolders(backupBase)
                .Select(folder => (Folder: folder, Name: Path.GetFileName(folder.TrimEnd('\\', '/'))))
                .Where(x => ParseDatePrefix(x.Name).HasValue)
                .OrderByDescending(x => x.Name, StringComparer.Ordinal)
                .Select(x => x.Folder)
                .ToList();
            return folders.FirstOrDefault(isComplete) ?? folders.FirstOrDefault();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    internal static DateTime? ParseDatePrefix(string folderName)
    {
        // "2026-11-22" or "2026-11-22_a"; anything else after the date isn't one of ours.
        if (folderName.Length != 10 && !(folderName.Length == 12 && folderName[10] == '_' && folderName[11] is >= 'a' and <= 'z'))
            return null;
        return DateTime.TryParseExact(folderName[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;
    }

    internal static string Tail(string text, int maxChars)
    {
        if (string.IsNullOrEmpty(text)) return "";
        text = text.Trim();
        return text.Length <= maxChars ? text : text[^maxChars..];
    }
}
