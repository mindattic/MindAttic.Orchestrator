using System.Data.Common;
using System.Globalization;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator;

/// <summary>
/// Every <see cref="OrchestratorSettings.DbMaintenanceHours"/> (M.ORCH-T1681, roadmap Phase 4), run from <see cref="LogPruner"/>:
/// age-prunes the orchestrator database's rows that otherwise only ever grow, then VACUUMs its SQLite file once enough of it is
/// free pages. <c>term_audit</c> (by <c>at_utc</c>) is pruned on the store of the orchestrator's data (SQL Server once configured,
/// else the file); <c>gate_events</c>, which the hooks write and only the file keeps, on the file; <c>usage_history</c> (by day)
/// and <c>ticket_costs</c> (by last use) only when <see cref="OrchestratorSettings.DbHistoryKeepDays"/> is above 0, since they're
/// token history kept for good by default. Nothing here changes SQL Server's configuration, and SQL Server is never vacuumed.
/// </summary>
public static class DbMaintenance
{
    /// <summary>What one run did: the rows deleted per table and whether the file was vacuumed (with its size before and after).</summary>
    public sealed record Result(IReadOnlyList<(string Table, int Rows)> Deleted, bool Vacuumed, long BytesBefore, long BytesAfter, double FreePercent);

    /// <summary>Prunes and, when worth it, VACUUMs <paramref name="db"/> as <paramref name="s"/> says, at <paramref name="nowUtc"/>.
    /// Throws a <see cref="DbException"/> or <see cref="IOException"/> when the store can't be reached; a VACUUM that can't take
    /// the file's lock (a sync holding it) just doesn't happen, left for the next run.</summary>
    public static Result Run(OrchestratorDb db, OrchestratorSettings s, DateTime nowUtc)
    {
        var deleted = new List<(string, int)>();
        if (s.DbEventKeepDays > 0)
        {
            var cutoff = Cutoff(nowUtc, s.DbEventKeepDays);
            var audit = 0;
            db.WriteDataIf((connection, tx) =>
            {
                audit = Sql.Run(connection, tx, "DELETE FROM term_audit WHERE at_utc < @cutoff;", ("@cutoff", cutoff));
                return false; // term_audit isn't in the dump
            });
            deleted.Add(("term_audit", audit));
            var gate = 0;
            db.Write((connection, tx) => gate = Sql.Run(connection, tx, "DELETE FROM gate_events WHERE at < @cutoff;", ("@cutoff", cutoff)), dump: false);
            deleted.Add(("gate_events", gate));
        }
        if (s.DbHistoryKeepDays > 0)
        {
            var day = nowUtc.AddDays(-Math.Min(s.DbHistoryKeepDays, 36500)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var cutoff = Cutoff(nowUtc, s.DbHistoryKeepDays);
            int usage = 0, costs = 0;
            db.WriteDataIf((connection, tx) =>
            {
                usage = Sql.Run(connection, tx, "DELETE FROM usage_history WHERE day < @day;", ("@day", day));
                costs = Sql.Run(connection, tx, "DELETE FROM ticket_costs WHERE last_utc < @cutoff;", ("@cutoff", cutoff));
                return usage + costs > 0; // both are in the dump
            });
            deleted.Add(("usage_history", usage));
            deleted.Add(("ticket_costs", costs));
        }
        var (vacuumed, before, after, free) = VacuumIfWorthIt(db, s.DbVacuumMinFreePercent);
        return new Result(deleted, vacuumed, before, after, free);
    }

    /// <summary>VACUUMs the SQLite file when at least <paramref name="minFreePercent"/> of its pages are free (every time at 0).
    /// Run outside any transaction; a lock it can't get within the busy timeout leaves the file as it is.</summary>
    internal static (bool Vacuumed, long Before, long After, double FreePercent) VacuumIfWorthIt(OrchestratorDb db, double minFreePercent)
    {
        var before = Size(db.Path);
        using var connection = db.Open();
        var pages = Convert.ToInt64(Sql.Scalar(connection, null, "PRAGMA page_count;") ?? 0L, CultureInfo.InvariantCulture);
        var freePages = Convert.ToInt64(Sql.Scalar(connection, null, "PRAGMA freelist_count;") ?? 0L, CultureInfo.InvariantCulture);
        var free = pages <= 0 ? 0 : freePages * 100.0 / pages;
        if (pages <= 0 || (minFreePercent > 0 && free < minFreePercent)) return (false, before, before, free);
        try { Sql.Run(connection, null, "VACUUM;"); }
        catch (Microsoft.Data.Sqlite.SqliteException) { return (false, before, before, free); } // busy or locked: next run
        return (true, before, Size(db.Path), free);
    }

    /// <summary>The cutoff <paramref name="days"/> before <paramref name="nowUtc"/>, written as the tables keep their times so the
    /// rows compare as text.</summary>
    private static string Cutoff(DateTime nowUtc, int days) =>
        nowUtc.AddDays(-Math.Min(days, 36500)).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static long Size(string path)
    {
        try { return File.Exists(path) ? new FileInfo(path).Length : 0; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return 0; }
    }
}
