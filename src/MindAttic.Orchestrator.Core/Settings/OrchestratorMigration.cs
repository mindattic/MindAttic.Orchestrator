using System.Data.Common;

namespace MindAttic.Orchestrator.Settings;

/// <summary>
/// Moves the orchestrator's data between <c>data/orchestrator.db</c> and its SQL Server database (M.ORCH-T658):
/// <see cref="ToServer"/> copies every table SQL Server keeps (<see cref="OrchestratorServer.Tables"/>; the saved state but its
/// volatile rows, which stay in the file), checks each table's row count matches, and only then turns SQL Server on for this
/// machine. <see cref="ToSqlite"/> copies it all back and turns it off, so SQLite always remains a working store.
/// </summary>
public static class OrchestratorMigration
{
    public sealed record Copied(string Table, long Rows);

    /// <summary>
    /// Copies <paramref name="local"/>'s data into the SQL Server database <paramref name="connection"/> names (created when it
    /// isn't there), in one transaction holding the server's write lock, then configures this machine to use it. A server that
    /// already holds data is left alone unless <paramref name="replace"/>, so a second run can't copy an older file over it.
    /// Once switched (<paramref name="configure"/>), the file's own copy of what the server now alone keeps (everything but
    /// <see cref="OrchestratorDb.Mirrored"/>) is cleared (M.ORCH-T812): left as it stood at the cutover, pace_ledger and
    /// usage_samples looked like real, if stale, data rather than a stopped clock, and a plain read of the file (not
    /// <c>orchestrator db status</c>, the live <c>data/orchestrator.sql</c>, or <c>orchestrator ledger</c>) wrongly concluded
    /// the ledger itself had stopped recording windows (M.ORCH-T802), when every window since had been landing on the server
    /// the whole time. An empty table now says plainly "the server keeps this," not "nothing happened since."
    /// </summary>
    public static IReadOnlyList<Copied> ToServer(OrchestratorDb local, string connection, bool replace = false, bool configure = true)
    {
        OrchestratorServer.CreateDatabase(connection);
        var server = new OrchestratorServer(connection);
        var copied = new List<Copied>();
        using (var source = local.Open())
        using (var target = server.Open())
        {
            using var tx = target.BeginTransaction();
            OrchestratorServer.Lock(target, tx);
            var held = OrchestratorServer.Tables.Sum(t => Count(target, tx, t.Table, null));
            if (held > 0 && !replace)
                throw new InvalidOperationException($"{server.Describe()} already holds {held} rows; pass --replace to copy {local.Path} over them.");
            foreach (var (table, _, _) in OrchestratorServer.Tables)
            {
                Sql.Run(target, tx, $"DELETE FROM {table};");
                var where = table == "state" ? NotVolatile : null;
                var rows = Rows(source, null, table, where);
                foreach (var row in rows) Insert(target, tx, table, row);
                var there = Count(target, tx, table, null);
                if (there != rows.Count)
                    throw new InvalidOperationException($"{table}: {rows.Count} rows read from {local.Path} but {there} written to {server.Describe()}; nothing was changed.");
                copied.Add(new Copied(table, rows.Count));
            }
            tx.Commit();
        }
        if (configure)
        {
            OrchestratorServer.Configure(connection);
            ClearServerOwned(local);
        }
        return copied;
    }

    /// <summary>Clears <paramref name="local"/>'s own copy of every table the server now alone keeps current (everything but
    /// <see cref="OrchestratorDb.Mirrored"/>, which the hooks still read here), once this machine is actually switched to it.
    /// Internal so a test can check it directly, without going through <see cref="OrchestratorServer.Configure"/> (which would
    /// flip this real machine's database.json).</summary>
    internal static void ClearServerOwned(OrchestratorDb local)
    {
        var serverOwned = OrchestratorServer.Tables.Select(t => t.Table).Except(OrchestratorDb.Mirrored, StringComparer.Ordinal);
        local.Write((c, tx) => { foreach (var table in serverOwned) Sql.Run(c, tx, $"DELETE FROM {table};"); }, dump: false);
    }

    /// <summary>Copies the SQL Server database's data back into <paramref name="local"/> (its volatile state rows kept as they
    /// are) and turns SQL Server off for this machine: everything is SQLite again.</summary>
    public static IReadOnlyList<Copied> ToSqlite(OrchestratorDb local, string connection, bool configure = true)
    {
        var server = new OrchestratorServer(connection);
        var copied = new List<Copied>();
        using (var source = server.Open())
        {
            var tables = OrchestratorServer.Tables.Select(t => (t.Table, Rows: Rows(source, null, t.Table, t.Table == "state" ? NotVolatile : null))).ToList();
            local.Write((target, tx) =>
            {
                foreach (var (table, rows) in tables)
                {
                    Sql.Run(target, tx, table == "state" ? $"DELETE FROM state WHERE {NotVolatile};" : $"DELETE FROM {table};");
                    foreach (var row in rows) Insert(target, tx, table, row);
                    copied.Add(new Copied(table, rows.Count));
                }
            }, dump: false);
        }
        if (configure) OrchestratorServer.Configure(null);
        try { local.WriteDump(local.Dump()); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return copied;
    }

    /// <summary>Each table's row count on SQL Server and in the file, for <c>orchestrator db status</c>.</summary>
    public static IReadOnlyList<(string Table, long? Server, long Local)> Counts(OrchestratorDb local, string? connection)
    {
        using var file = local.Open();
        using DbConnection? server = connection is null ? null : new OrchestratorServer(connection).Open();
        return [.. OrchestratorServer.Tables.Select(t => (t.Table,
            server is null ? (long?)null : Count(server, null, t.Table, t.Table == "state" ? NotVolatile : null),
            Count(file, null, t.Table, t.Table == "state" ? NotVolatile : null)))];
    }

    private static string NotVolatile => "name NOT IN (" + string.Join(", ", OrchestratorDb.Volatile.Select(v => "'" + v.Replace("'", "''", StringComparison.Ordinal) + "'")) + ")";

    private static long Count(DbConnection connection, DbTransaction? tx, string table, string? where) =>
        Convert.ToInt64(Sql.Scalar(connection, tx, $"SELECT COUNT(*) FROM {table}{(where is null ? "" : " WHERE " + where)};"), System.Globalization.CultureInfo.InvariantCulture);

    private static List<(string Column, object? Value)[]> Rows(DbConnection connection, DbTransaction? tx, string table, string? where) =>
        Sql.Read(connection, tx, $"SELECT * FROM {table}{(where is null ? "" : " WHERE " + where)};",
            r => Enumerable.Range(0, r.FieldCount).Select(i => (r.GetName(i), r.IsDBNull(i) ? null : r.GetValue(i))).ToArray());

    private static void Insert(DbConnection connection, DbTransaction tx, string table, (string Column, object? Value)[] row) =>
        Sql.Run(connection, tx,
            $"INSERT INTO {table} ({string.Join(", ", row.Select(c => Sql.Quote(c.Column)))}) VALUES ({string.Join(", ", row.Select((_, i) => "@p" + i))});",
            [.. row.Select((c, i) => ("@p" + i, c.Value))]);
}
