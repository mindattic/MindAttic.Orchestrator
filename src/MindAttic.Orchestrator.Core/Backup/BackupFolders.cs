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
    /// never returned. <paramref name="keep"/> &lt;= 0 makes every dated folder stale.
    /// </summary>
    public static IReadOnlyList<string> ResolveStale(IReadOnlyList<string> existingFolders, int keep) =>
        existingFolders
            .Select(folder => (Folder: folder, Date: ParseDatePrefix(Path.GetFileName(folder))))
            .Where(x => x.Date.HasValue)
            .GroupBy(x => x.Date!.Value)
            .OrderByDescending(g => g.Key)
            .Skip(Math.Max(keep, 0))
            .SelectMany(g => g.Select(x => x.Folder))
            .ToList();

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
