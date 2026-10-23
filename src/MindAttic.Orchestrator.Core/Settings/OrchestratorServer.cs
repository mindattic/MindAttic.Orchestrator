using System.Data.Common;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace MindAttic.Orchestrator.Settings;

/// <summary>
/// The orchestrator's SQL Server database (M.ORCH-T658): the store of record for its data — the account roster, Claude's
/// meter readings, the pace ledger, the usage and ticket-cost history, the settings, the assignments and the saved state —
/// on the full SQL Server instance beside the Prose database, in a database of its own (<see cref="DefaultDatabase"/>).
/// What only this machine's processes share stays in <c>data/orchestrator.db</c> (SQLite): the leases, the pace gate's
/// slots and events, each CLI's marks, the sessions' states and the volatile state rows (<see cref="OrchestratorDb.Volatile"/>),
/// which the hooks read and write on every tool call. The settings, the assignments and the saved state are copied there too
/// on every change (<see cref="OrchestratorDb.Mirrored"/>), as the hooks read them through stdlib sqlite3.
/// Turned on by <c>orchestrator db migrate</c>, which writes <see cref="ConfigFile"/>; with no connection configured (another
/// machine, a test's scratch database), everything stays in SQLite as before.
/// </summary>
public sealed class OrchestratorServer(string connectionString)
{
    public const string DefaultDatabase = "MindAttic.Orchestrator";

    /// <summary>A connection string to use instead of <see cref="ConfigFile"/>'s; "off" keeps everything in SQLite.</summary>
    public const string EnvVar = "MINDATTIC_ORCHESTRATOR_SQLSERVER";

    /// <summary>The machine's setting, under <c>%APPDATA%\MindAttic\MindAttic.Orchestrator</c>: <c>{"SqlServer": "..."}</c>.</summary>
    public static string ConfigFile => Path.Combine(JsonStore.LiveDirectory, "database.json");

    /// <summary>The full instance on this machine, Windows sign-in.</summary>
    public static string DefaultConnection(string server = "localhost", string database = DefaultDatabase) => new SqlConnectionStringBuilder
    {
        DataSource = server,
        InitialCatalog = database,
        IntegratedSecurity = true,
        TrustServerCertificate = true,
        Encrypt = false,
        ConnectTimeout = 5,
        ApplicationName = "MindAttic.Orchestrator",
    }.ConnectionString;

    public string ConnectionString { get; } = connectionString;

    public string Describe() => new SqlConnectionStringBuilder(ConnectionString) is var b ? $"{b.DataSource}/{b.InitialCatalog}" : ConnectionString;

    private static (DateTime Stamp, string? Connection) cached = (DateTime.MinValue, null);
    private static readonly System.Threading.Lock CacheLock = new();

    /// <summary>The connection string configured for the live database, or null for SQLite alone. The file is read again
    /// whenever its time changes (one stat a call), so <c>orchestrator db migrate</c> (or <c>db back</c>) reaches every running
    /// process at its next write, none still writing the store it left.</summary>
    public static string? Configured()
    {
        if (Environment.GetEnvironmentVariable(EnvVar) is { Length: > 0 } env)
            return string.Equals(env, "off", StringComparison.OrdinalIgnoreCase) ? null : env;
        var stamp = File.GetLastWriteTimeUtc(ConfigFile); // 1601-01-01 when the file isn't there
        lock (CacheLock)
        {
            if (stamp == cached.Stamp) return cached.Connection;
            string? connection = null;
            try
            {
                if (File.Exists(ConfigFile))
                    using (var doc = JsonDocument.Parse(File.ReadAllText(ConfigFile)))
                        if (doc.RootElement.TryGetProperty("SqlServer", out var s) && s.ValueKind == JsonValueKind.String && s.GetString() is { Length: > 0 } c)
                            connection = c;
            }
            // Mid-write: the setting it had stands, and the next call reads it again.
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return cached.Connection; }
            cached = (stamp, connection);
            return connection;
        }
    }

    /// <summary>Forgets the cached setting, after <c>orchestrator db migrate</c> writes it.</summary>
    public static void Reload()
    {
        lock (CacheLock) cached = (DateTime.MinValue, null);
    }

    /// <summary>Saves <paramref name="connection"/> as this machine's setting (null removes it: SQLite alone).</summary>
    public static void Configure(string? connection)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigFile)!);
        if (connection is null) File.Delete(ConfigFile);
        else File.WriteAllText(ConfigFile, JsonSerializer.Serialize(new Dictionary<string, string> { ["SqlServer"] = connection }, new JsonSerializerOptions { WriteIndented = true }));
        Reload();
    }

    /// <summary>An open connection (pooled), the schema brought up to date the first time in each process.</summary>
    public SqlConnection Open()
    {
        var connection = new SqlConnection(ConnectionString);
        try
        {
            connection.Open();
            EnsureSchema(connection);
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    /// <summary>Takes the database's one write lock for <paramref name="tx"/>, as SQLite's BEGIN IMMEDIATE did: the daemon, a
    /// dashboard and a CLI changing the same thing at once queue here, and none saves over what another added.</summary>
    public static void Lock(DbConnection connection, DbTransaction tx)
    {
        using var command = Sql.Command(connection, tx,
            "DECLARE @r int; EXEC @r = sp_getapplock @Resource = N'MindAttic.Orchestrator.write', @LockMode = N'Exclusive', " +
            "@LockOwner = N'Transaction', @LockTimeout = @timeout; SELECT @r;", ("@timeout", OrchestratorDb.BusyTimeoutMs));
        if (Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) < 0)
            throw new IOException("The orchestrator's SQL Server database stayed busy past its timeout.");
    }

    // ---- the schema ----

    private static readonly HashSet<string> Ready = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The tables the server keeps, in the column order SQLite's have after every step of <see cref="OrchestratorDb"/>'s
    /// migrations (a column added there needs adding here, as a new step: <c>OrchestratorServerTests</c> holds them alike), so
    /// <see cref="OrchestratorDb.Dump"/> writes the same text from either. Keys are <c>nvarchar(450)</c> (an index's limit);
    /// the database's binary collation compares and orders them as SQLite does.
    /// </summary>
    internal static readonly (string Table, string Columns, string? Index)[] Tables =
    [
        ("accounts", """
            id nvarchar(450) NOT NULL PRIMARY KEY, email nvarchar(max) NULL, org nvarchar(max) NULL, "plan" nvarchar(max) NULL,
            five_hour_percent float NULL, five_hour_reset float NULL, week_percent float NULL, week_reset float NULL,
            read_at float NULL, signed_in_at float NULL
            """, null),
        ("members", """
            email nvarchar(450) NOT NULL PRIMARY KEY, name nvarchar(max) NULL, role nvarchar(64) NOT NULL, tier nvarchar(64) NOT NULL,
            status nvarchar(64) NOT NULL, joined_at float NOT NULL, updated_at float NOT NULL
            """, null),
        ("usage_samples", """
            at nvarchar(64) NOT NULL PRIMARY KEY, "percent" float NOT NULL, reset nvarchar(64) NOT NULL, window_fresh bigint NOT NULL
            """, null),
        ("pace_ledger", """
            end_utc nvarchar(64) NOT NULL, account nvarchar(450) NULL, start_utc nvarchar(64) NOT NULL, grade nvarchar(64) NOT NULL,
            why nvarchar(max) NOT NULL, peak_percent float NULL, final_percent float NULL, fresh bigint NOT NULL, samples bigint NOT NULL,
            minutes_at_target float NOT NULL, limit_hit_utc nvarchar(64) NULL, weekly_hit_utc nvarchar(64) NULL, held bigint NOT NULL,
            held_minutes float NOT NULL, refused bigint NOT NULL, prompts_answered bigint NOT NULL, exempt bigint NOT NULL, gaps nvarchar(max) NOT NULL
            """, "CREATE INDEX pace_ledger_window ON pace_ledger (end_utc, account);"),
        ("usage_history", """
            day nvarchar(16) NOT NULL, repo nvarchar(450) NOT NULL, provider nvarchar(64) NOT NULL, account nvarchar(450) NULL,
            before_utc nvarchar(64) NULL, fresh bigint NOT NULL
            """, "CREATE INDEX usage_history_key ON usage_history (day, repo, provider);"),
        ("ticket_costs", """
            session_id nvarchar(450) NOT NULL PRIMARY KEY, ticket_id nvarchar(450) NOT NULL, type nvarchar(64) NULL, repo nvarchar(450) NOT NULL,
            fresh bigint NOT NULL, last_utc nvarchar(64) NOT NULL, own_share bigint NOT NULL, model nvarchar(128) NULL
            """, null),
        ("settings", "name nvarchar(450) NOT NULL PRIMARY KEY, value nvarchar(max) NOT NULL", null),
        ("assignments", """
            position bigint NOT NULL PRIMARY KEY, ticket_id nvarchar(450) NULL, repo nvarchar(450) NULL, directory nvarchar(max) NULL,
            provider nvarchar(64) NULL, started_utc nvarchar(64) NULL, worktree bigint NULL, pilot bigint NULL, pilot_started_utc nvarchar(64) NULL,
            log nvarchar(max) NULL, cli bigint NULL, cli_session nvarchar(450) NULL, model nvarchar(128) NULL, reviewed_utc nvarchar(64) NULL
            """, null),
        ("state", "name nvarchar(450) NOT NULL PRIMARY KEY, value nvarchar(max) NOT NULL, written_at float NOT NULL", null),
    ];

    /// <summary>The schema's version: 1, the tables of SQLite's first ten steps; 2, <c>members</c> (M.ORCH-T670).</summary>
    public const int SchemaVersion = 2;

    /// <summary>The changes to tables already there, one step per version past 1, as SQLite's migrations add them: a column
    /// added to a table there is one more step here (an <c>ALTER TABLE ... ADD</c>, also added to its <see cref="Tables"/> row so
    /// a new database has it). A new table needs no statement: every table missing is created at any version change.</summary>
    private static readonly (string Table, string Sql)[] Steps =
    [
        ("members", ""), // 2: members, a new table (created with the rest below)
    ];

    private void EnsureSchema(SqlConnection connection)
    {
        lock (Ready) if (Ready.Contains(ConnectionString)) return;
        using (var tx = connection.BeginTransaction())
        {
            Lock(connection, tx);
            Sql.Run(connection, tx, "IF OBJECT_ID(N'schema_version') IS NULL CREATE TABLE schema_version (version int NOT NULL);");
            var version = Sql.Scalar(connection, tx, "SELECT MAX(version) FROM schema_version;") is { } v ? Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture) : 0;
            if (version < SchemaVersion)
            {
                // A database already at 1 or later has its tables' older columns: only the steps past its version alter them.
                // Created now, a table has every column its row lists, so no step runs on it.
                var created = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var (table, columns, index) in Tables)
                {
                    if (Sql.Scalar(connection, tx, $"SELECT OBJECT_ID(N'{table}');") is not (null or DBNull)) continue;
                    Sql.Run(connection, tx, $"CREATE TABLE {table} ({columns});");
                    if (index is not null) Sql.Run(connection, tx, index);
                    created.Add(table);
                }
                if (version >= 1)
                    for (var step = Math.Max(version, 1); step < SchemaVersion; step++)
                        if (Steps[step - 1] is { Sql.Length: > 0 } change && !created.Contains(change.Table)) Sql.Run(connection, tx, change.Sql);
                Sql.Run(connection, tx, $"INSERT INTO schema_version (version) VALUES ({SchemaVersion});");
            }
            tx.Commit();
        }
        lock (Ready) Ready.Add(ConnectionString);
    }

    /// <summary>Creates the database on <paramref name="connection"/>'s server when it isn't there, with a binary collation so
    /// keys compare and sort as SQLite's do.</summary>
    public static void CreateDatabase(string connection)
    {
        var b = new SqlConnectionStringBuilder(connection);
        var name = b.InitialCatalog;
        b.InitialCatalog = "master";
        using var master = new SqlConnection(b.ConnectionString);
        master.Open();
        Sql.Run(master, null, $"IF DB_ID(@name) IS NULL EXEC (N'CREATE DATABASE ' + @quoted + N' COLLATE Latin1_General_100_BIN2');",
            ("@name", name), ("@quoted", "[" + name.Replace("]", "]]", StringComparison.Ordinal) + "]"));
    }
}
