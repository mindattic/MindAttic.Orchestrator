using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace MindAttic.Orchestrator.Settings;

/// <summary>
/// The SQL both of the orchestrator's stores take (M.ORCH-T658): SQLite (<c>data/orchestrator.db</c>) and SQL Server
/// (<see cref="OrchestratorServer"/>). Parameters are named <c>@name</c>, which both accept; <c>"percent"</c> and <c>"plan"</c>,
/// reserved words in T-SQL, are quoted where they're named. What only one dialect has (SQLite's <c>INSERT OR IGNORE</c> and
/// <c>INSERT OR REPLACE</c>) is written here once for each, and a column is read by converting its value, since SQL Server
/// hands back a <c>bigint</c> where SQLite hands back whatever the reader asks for.
/// </summary>
public static class Sql
{
    public static bool IsServer(DbConnection connection) => connection is SqlConnection;

    public static DbCommand Command(DbConnection connection, DbTransaction? tx, string sql, params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            var p = command.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            command.Parameters.Add(p);
        }
        return command;
    }

    public static int Run(DbConnection connection, DbTransaction? tx, string sql, params (string Name, object? Value)[] parameters)
    {
        using var command = Command(connection, tx, sql, parameters);
        return command.ExecuteNonQuery();
    }

    public static object? Scalar(DbConnection connection, DbTransaction? tx, string sql, params (string Name, object? Value)[] parameters)
    {
        using var command = Command(connection, tx, sql, parameters);
        return command.ExecuteScalar() is { } v and not DBNull ? v : null;
    }

    public static List<T> Read<T>(DbConnection connection, DbTransaction? tx, string sql, Func<DbDataReader, T> row, params (string Name, object? Value)[] parameters)
    {
        using var command = Command(connection, tx, sql, parameters);
        using var r = command.ExecuteReader();
        var rows = new List<T>();
        while (r.Read()) rows.Add(row(r));
        return rows;
    }

    /// <summary>Adds the row unless one with the same <paramref name="keys"/> is there (a NULL key matching NULL or '', as the
    /// SQLite tables' unique indexes on <c>IFNULL(column, '')</c> match them).</summary>
    public static void InsertIfMissing(DbConnection connection, DbTransaction tx, string table, string[] keys, params (string Column, object? Value)[] columns)
    {
        var insert = Insert(table, columns);
        var sql = IsServer(connection)
            ? $"IF NOT EXISTS (SELECT 1 FROM {table} WHERE {Match(keys)}) {insert}"
            : insert.Replace("INSERT INTO", "INSERT OR IGNORE INTO", StringComparison.Ordinal);
        Run(connection, tx, sql, Parameters(columns));
    }

    /// <summary>Replaces the row with the same <paramref name="keys"/>, or adds it.</summary>
    public static void Upsert(DbConnection connection, DbTransaction tx, string table, string[] keys, params (string Column, object? Value)[] columns)
    {
        var insert = Insert(table, columns);
        var sql = IsServer(connection)
            ? $"UPDATE {table} SET {string.Join(", ", columns.Where(c => !keys.Contains(c.Column)).Select(c => $"{Quote(c.Column)} = {Name(c.Column)}"))} " +
              $"WHERE {Match(keys)}; IF @@ROWCOUNT = 0 {insert}"
            : insert.Replace("INSERT INTO", "INSERT OR REPLACE INTO", StringComparison.Ordinal);
        Run(connection, tx, sql, Parameters(columns));
    }

    private static string Insert(string table, (string Column, object? Value)[] columns) =>
        $"INSERT INTO {table} ({string.Join(", ", columns.Select(c => Quote(c.Column)))}) VALUES ({string.Join(", ", columns.Select(c => Name(c.Column)))});";

    private static string Match(string[] keys) => string.Join(" AND ", keys.Select(k => $"ISNULL({Quote(k)}, N'') = ISNULL({Name(k)}, N'')"));

    private static (string Name, object? Value)[] Parameters((string Column, object? Value)[] columns) =>
        [.. columns.Select(c => (Name(c.Column), c.Value))];

    private static string Name(string column) => "@" + column;

    /// <summary>A column as both dialects take it: the T-SQL reserved words quoted.</summary>
    public static string Quote(string column) => column is "percent" or "plan" ? $"\"{column}\"" : column;

    // ---- reading a column, whichever store it came from ----

    public static long Long(DbDataReader r, int i) => Convert.ToInt64(r.GetValue(i), System.Globalization.CultureInfo.InvariantCulture);
    public static int Int(DbDataReader r, int i) => Convert.ToInt32(r.GetValue(i), System.Globalization.CultureInfo.InvariantCulture);
    public static double Real(DbDataReader r, int i) => Convert.ToDouble(r.GetValue(i), System.Globalization.CultureInfo.InvariantCulture);
    public static double? OptionalReal(DbDataReader r, int i) => r.IsDBNull(i) ? null : Real(r, i);
    public static string? OptionalText(DbDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
}
