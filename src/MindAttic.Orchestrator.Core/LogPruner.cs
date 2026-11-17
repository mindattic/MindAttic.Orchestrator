using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator;

/// <summary>
/// Hourly (M.ORCH-T1611): trims the MindAttic.Log database to <c>LogKeepDays</c> (the library's own retention is a fixed 365
/// days) and deletes the <c>terms\&lt;id&gt;.log</c> files of finished terminal hosts (no live <c>&lt;id&gt;.json</c> beside them)
/// older than <c>TermLogKeepDays</c>; nothing else ever removed those files. It also rotates <c>disk-cleanup.log</c> past
/// <c>DiskCleanupLogMaxKb</c> and, every <c>DbMaintenanceHours</c>, runs <see cref="DbMaintenance"/> (M.ORCH-T1681).
/// </summary>
public sealed class LogPruner
{
    /// <summary>The every.</summary>
    public static readonly TimeSpan Every = TimeSpan.FromHours(1);

    private DateTime? lastUtc;
    private DateTime? maintainedUtc;

    /// <summary>Runs the if due.</summary>
    public void RunIfDue(OrchestratorSettings s, Action<string> say) =>
        RunIfDue(s, say, DateTime.UtcNow, Term.TermSessions.DefaultDirectory, Activity.DiskCleanup.LogPath, () => new OrchestratorDb(OrchestratorDb.LivePath));

    internal void RunIfDue(OrchestratorSettings s, Action<string> say, DateTime now, string termsDir) =>
        RunIfDue(s, say, now, termsDir, null, null);

    internal void RunIfDue(OrchestratorSettings s, Action<string> say, DateTime now, string termsDir, string? cleanupLog, Func<OrchestratorDb>? db)
    {
        if (lastUtc is { } at && now - at < OrchestratorSettings.SecondsSpan(s.LogPruneMinutes * 60)) return; // live (M.ORCH-T1595); Every is its default
        lastUtc = now;
        try
        {
            var rows = OrchestratorLog.Prune(s.LogKeepDays, now);
            if (rows > 0) say($"deleted {rows} log entries older than {s.LogKeepDays} days");
            var gone = PruneTermLogs(termsDir, s.TermLogKeepDays, now);
            if (gone > 0) say($"deleted {gone} finished terminal hosts' log files older than {s.TermLogKeepDays} days");
            if (cleanupLog is not null && Rotate(cleanupLog, s.DiskCleanupLogMaxKb * 1024L, s.DiskCleanupLogKeep))
                say($"rotated {Path.GetFileName(cleanupLog)} past {s.DiskCleanupLogMaxKb} KB (keeping {Math.Max(1, s.DiskCleanupLogKeep)} old copies)");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { say($"old logs: couldn't prune: {ex.Message}"); }
        if (db is not null) MaintainIfDue(s, say, now, db);
    }

    /// <summary>Runs <see cref="DbMaintenance"/> when <see cref="OrchestratorSettings.DbMaintenanceHours"/> have passed since the
    /// last run (the first at the first prune after start). One that fails is said and tried again at the next interval.</summary>
    private void MaintainIfDue(OrchestratorSettings s, Action<string> say, DateTime now, Func<OrchestratorDb> db)
    {
        if (s.DbMaintenanceHours <= 0) return;
        if (maintainedUtc is { } at && now - at < OrchestratorSettings.SecondsSpan(s.DbMaintenanceHours * 3600)) return;
        maintainedUtc = now;
        try
        {
            var r = DbMaintenance.Run(db(), s, now);
            var deleted = r.Deleted.Where(d => d.Rows > 0).Select(d => $"{d.Rows} from {d.Table}").ToList();
            if (deleted.Count > 0) say($"database maintenance: deleted {string.Join(", ", deleted)}");
            if (r.Vacuumed) say($"database maintenance: vacuumed orchestrator.db ({r.FreePercent:0}% free pages), {r.BytesBefore / 1024} KB -> {r.BytesAfter / 1024} KB");
        }
        catch (Exception ex) when (ex is System.Data.Common.DbException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            say($"database maintenance: couldn't run (next in {s.DbMaintenanceHours:0.#}h): {ex.Message}");
        }
    }

    /// <summary>Moves <paramref name="path"/> to <c>&lt;name&gt;.1&lt;ext&gt;</c> (shifting older copies up to <paramref name="keep"/>,
    /// deleting the one past it) once it's bigger than <paramref name="maxBytes"/>; 0 never rotates. Whether it rotated.</summary>
    internal static bool Rotate(string path, long maxBytes, int keep)
    {
        if (maxBytes <= 0 || !File.Exists(path) || new FileInfo(path).Length <= maxBytes) return false;
        keep = Math.Clamp(keep, 1, 100);
        string Copy(int n) => Path.Combine(Path.GetDirectoryName(path) ?? ".", $"{Path.GetFileNameWithoutExtension(path)}.{n}{Path.GetExtension(path)}");
        if (File.Exists(Copy(keep))) File.Delete(Copy(keep));
        for (var n = keep - 1; n >= 1; n--)
            if (File.Exists(Copy(n))) File.Move(Copy(n), Copy(n + 1), overwrite: true);
        File.Move(path, Copy(1), overwrite: true);
        return true;
    }

    internal static int PruneTermLogs(string dir, int keepDays, DateTime nowUtc)
    {
        if (keepDays <= 0 || !Directory.Exists(dir)) return 0;
        var cutoff = nowUtc.AddDays(-Math.Min(keepDays, 36500));
        var gone = 0;
        foreach (var log in Directory.GetFiles(dir, "*.log"))
        {
            if (File.GetLastWriteTimeUtc(log) >= cutoff || File.Exists(Path.ChangeExtension(log, ".json"))) continue;
            try { File.Delete(log); gone++; } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return gone;
    }
}
