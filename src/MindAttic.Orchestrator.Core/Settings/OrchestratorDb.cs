using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace MindAttic.Orchestrator.Settings;

/// <summary>
/// The orchestrator's SQLite database (M.ORCH-T490), kept in the MindAttic.Orchestrator repo and committed, as
/// MindAttic.Workflow keeps <c>data/workflow.db</c>: <c>&lt;workspace&gt;\MindAttic.Orchestrator\data\orchestrator.db</c>, or the
/// file <see cref="EnvVar"/> names, so every process and worktree uses the main checkout's copy. Rollback journal (not WAL),
/// so the .db file alone holds every committed write and git commits it whole; <c>data/orchestrator.sql</c> beside it is a
/// readable dump (<see cref="Dump"/>) so its changes show up as text diffs. The schema is versioned by <c>PRAGMA user_version</c>:
/// each step in <see cref="Migrations"/> runs once, in order, so later tables (the rest of the orchestrator's JSON state) are
/// added as new steps.
/// </summary>
public sealed class OrchestratorDb(string path)
{
    public const string RelativePath = "data/orchestrator.db";
    public const string DumpRelativePath = "data/orchestrator.sql";
    public const string EnvVar = "MINDATTIC_ORCHESTRATOR_DB";

    /// <summary>The database every process uses: <see cref="EnvVar"/> when set, else the main checkout's
    /// <c>MindAttic.Orchestrator\data\orchestrator.db</c> under <paramref name="workspaceRoot"/> (never a worktree's).</summary>
    public static string DefaultPath(string? workspaceRoot = null) =>
        Environment.GetEnvironmentVariable(EnvVar) is { Length: > 0 } env ? env
            : System.IO.Path.Combine(workspaceRoot ?? MindAttic.Workflow.Workspace.DefaultRoot, "MindAttic.Orchestrator",
                RelativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));

    public string Path { get; } = System.IO.Path.GetFullPath(path);

    /// <summary>The readable dump written beside the database: <c>orchestrator.sql</c>.</summary>
    public string DumpPath => System.IO.Path.ChangeExtension(Path, ".sql");

    /// <summary>The schema, one step per version: step i takes the database from version i to i + 1.</summary>
    private static readonly string[] Migrations =
    [
        // 1: the account roster (M.ORCH-T490). Times are Unix seconds, as account.json and claude-usage.json keep them.
        """
        CREATE TABLE accounts (
            id                TEXT PRIMARY KEY,
            email             TEXT,
            org               TEXT,
            plan              TEXT,
            five_hour_percent REAL,
            five_hour_reset   REAL,
            week_percent      REAL,
            week_reset        REAL,
            read_at           REAL,
            signed_in_at      REAL
        );
        """,
    ];

    /// <summary>Every table, with the order the dump lists its rows in.</summary>
    private static readonly (string Table, string Order)[] Tables = [("accounts", "id")];

    public static int Version => Migrations.Length;

    /// <summary>An open connection with the schema brought up to date. Not pooled: a closed connection lets go of the file, so
    /// git can commit or replace it.</summary>
    public SqliteConnection Open()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path, Pooling = false }.ToString());
        try
        {
            connection.Open();
            Execute(connection, "PRAGMA busy_timeout = 5000; PRAGMA journal_mode = DELETE;");
            Migrate(connection);
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    /// <summary>A readable, deterministic SQL dump of the whole database (one INSERT per row, in key order), committed beside it.</summary>
    public static string Dump(SqliteConnection connection, SqliteTransaction? tx = null)
    {
        var sb = new StringBuilder("-- MindAttic.Orchestrator database dump (written with every change; the .db file is the store).\n");
        foreach (var (table, order) in Tables)
        {
            sb.Append("\n-- ").Append(table).Append('\n');
            using var command = connection.CreateCommand();
            command.Transaction = tx;
            command.CommandText = $"SELECT * FROM {table} ORDER BY {order};";
            using var r = command.ExecuteReader();
            var columns = Enumerable.Range(0, r.FieldCount).Select(r.GetName).ToList();
            while (r.Read())
            {
                sb.Append("INSERT INTO ").Append(table).Append(" (").Append(string.Join(", ", columns)).Append(") VALUES (");
                sb.Append(string.Join(", ", Enumerable.Range(0, r.FieldCount).Select(i => r.IsDBNull(i) ? "NULL" : r.GetValue(i) switch
                {
                    long n => n.ToString(CultureInfo.InvariantCulture),
                    double d => d.ToString("R", CultureInfo.InvariantCulture),
                    var v => "'" + v.ToString()!.Replace("'", "''", StringComparison.Ordinal) + "'",
                })));
                sb.Append(");\n");
            }
        }
        return sb.ToString();
    }

    /// <summary>The dump as it stands now.</summary>
    public string Dump()
    {
        using var connection = Open();
        return Dump(connection);
    }

    /// <summary>Writes <see cref="DumpPath"/> when its text changed (no BOM, LF, so it diffs as git keeps it).</summary>
    public void WriteDump(string dump)
    {
        try { if (File.Exists(DumpPath) && File.ReadAllText(DumpPath) == dump) return; }
        catch (IOException) { }
        var temp = DumpPath + ".tmp";
        File.WriteAllText(temp, dump, new UTF8Encoding(false));
        File.Move(temp, DumpPath, overwrite: true);
    }

    private static void Migrate(SqliteConnection connection)
    {
        if (Scalar(connection) >= Migrations.Length) return;
        // Taken before reading the version again, so two processes opening a new database at once don't both migrate it.
        using var tx = connection.BeginTransaction(System.Data.IsolationLevel.Serializable);
        var version = Scalar(connection, tx);
        for (; version < Migrations.Length; version++)
            Execute(connection, Migrations[version], tx);
        Execute(connection, $"PRAGMA user_version = {Migrations.Length};", tx);
        tx.Commit();
    }

    private static long Scalar(SqliteConnection connection, SqliteTransaction? tx = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "PRAGMA user_version;";
        return (long)command.ExecuteScalar()!;
    }

    internal static void Execute(SqliteConnection connection, string sql, SqliteTransaction? tx = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
