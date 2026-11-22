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
    /// <summary>The default database constant.</summary>
    public const string DefaultDatabase = "MindAttic.Orchestrator";

    /// <summary>A connection string to use instead of <see cref="ConfigFile"/>'s; "off" keeps everything in SQLite.</summary>
    public const string EnvVar = "MINDATTIC_ORCHESTRATOR_SQLSERVER";

    /// <summary>The machine's setting, under <c>%APPDATA%\MindAttic\MindAttic.Orchestrator</c>: <c>{"SqlServer": "..."}</c>.</summary>
    public static string ConfigFile => Path.Combine(JsonStore.LiveDirectory, "database.json");

    /// <summary>The full instance on this machine, Windows sign-in. Only what <c>orchestrator db migrate --server</c> builds when
    /// no connection string is given: a configured string (<see cref="ConfigFile"/>, <see cref="EnvVar"/>, <c>--connection</c>) is
    /// used exactly as written, so one naming its own <c>Authentication</c> (Entra ID, for Azure SQL Database), <c>Encrypt</c> and
    /// <c>Connect Timeout</c> keeps them (M.ORCH-T1748).</summary>
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

    /// <summary>The connection string.</summary>
    public string ConnectionString { get; } = connectionString;

    /// <summary>The server and database, for a message or a log line: never the connection string itself, which may hold a
    /// service principal's secret (M.ORCH-T1748).</summary>
    public string Describe()
    {
        try
        {
            var b = new SqlConnectionStringBuilder(ConnectionString);
            return $"{b.DataSource}/{b.InitialCatalog}";
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException) { return "a connection string that can't be read"; }
    }

    /// <summary><paramref name="connection"/> with its password (a SQL login's, or a service principal's secret) replaced by
    /// <c>***</c>, for anything that must show the string; one that can't be read is not shown at all (M.ORCH-T1748).</summary>
    public static string Redact(string connection)
    {
        try
        {
            var b = new SqlConnectionStringBuilder(connection);
            if (b.Password.Length > 0) b.Password = "***";
            return b.ConnectionString;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException) { return "(a connection string that can't be read)"; }
    }

    /// <summary>The host names Azure SQL Database's servers end in (the public cloud, then the sovereign ones).</summary>
    private static readonly string[] AzureHosts =
        [".database.windows.net", ".database.usgovcloudapi.net", ".database.chinacloudapi.cn", ".database.cloudapi.de"];

    private static (string? Connection, bool Azure) azureCached;

    /// <summary>Whether <paramref name="connection"/> names an Azure SQL Database server (M.ORCH-T1748), told from its host
    /// name alone (no round trip): there the database is provisioned ahead, there is no <c>master</c> to create it from, and
    /// nothing server-level (<c>sp_configure</c>) may run. False for none, for one that can't be read, and for localhost.</summary>
    public static bool IsAzure(string? connection)
    {
        if (string.IsNullOrWhiteSpace(connection)) return false;
        lock (CacheLock) if (azureCached.Connection == connection) return azureCached.Azure;
        bool azure;
        try
        {
            // "tcp:name.database.windows.net,1433": the host is what's between the protocol and the port or instance.
            var host = new SqlConnectionStringBuilder(connection).DataSource.Trim();
            var colon = host.IndexOf(':');
            if (colon >= 0) host = host[(colon + 1)..];
            var end = host.IndexOfAny([',', '\\']);
            if (end >= 0) host = host[..end];
            host = host.Trim();
            azure = AzureHosts.Any(suffix => host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException) { azure = false; }
        lock (CacheLock) azureCached = (connection, azure);
        return azure;
    }

    /// <summary>Whether the database this machine is configured for (<see cref="Configured"/>) is on Azure SQL Database.</summary>
    public static bool ConfiguredIsAzure() => IsAzure(Configured());

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
            log nvarchar(max) NULL, cli bigint NULL, cli_session nvarchar(450) NULL, model nvarchar(128) NULL, reviewed_utc nvarchar(64) NULL,
            account nvarchar(450) NULL, agent nvarchar(64) NULL, role nvarchar(128) NULL
            """, null),
        ("state", "name nvarchar(450) NOT NULL PRIMARY KEY, value nvarchar(max) NOT NULL, written_at float NOT NULL", null),
        // Organizations above people (M.ORCH-T655): each org, and each login's org and person (a login by its provider and itself).
        ("organizations", "id nvarchar(450) NOT NULL PRIMARY KEY, name nvarchar(max) NOT NULL, position bigint NOT NULL, created_utc nvarchar(64) NULL", null),
        ("org_members", """
            provider nvarchar(64) NOT NULL, login nvarchar(386) NOT NULL, org_id nvarchar(450) NOT NULL, person nvarchar(max) NOT NULL,
            added_utc nvarchar(64) NULL
            """, "ALTER TABLE org_members ADD CONSTRAINT org_members_key PRIMARY KEY (provider, login); CREATE INDEX org_members_org ON org_members (org_id);"),
        // The team gateway's viewing ACL and audit (M.ORCH-T934). The grant key's columns are sized to fit an index key (900 bytes).
        ("term_grants", """
            owner nvarchar(128) NOT NULL, viewer nvarchar(256) NOT NULL, session nvarchar(64) NOT NULL, level nvarchar(16) NOT NULL,
            expires_utc nvarchar(64) NULL, created_by nvarchar(256) NULL, created_utc nvarchar(64) NOT NULL
            """, "ALTER TABLE term_grants ADD CONSTRAINT term_grants_key PRIMARY KEY (owner, viewer, session);"),
        ("term_audit", """
            id nvarchar(64) NOT NULL PRIMARY KEY, at_utc nvarchar(64) NOT NULL, event nvarchar(16) NOT NULL, viewer nvarchar(256) NOT NULL,
            owner nvarchar(256) NULL, session nvarchar(64) NOT NULL, level nvarchar(16) NOT NULL, remote nvarchar(64) NULL,
            frames bigint NOT NULL, bytes bigint NOT NULL, detail nvarchar(max) NULL
            """, "CREATE INDEX term_audit_at ON term_audit (at_utc);"),
        // The resource ledger (M.ORCH-T1676). The key's columns are sized to fit an index key (900 bytes): a longer path is refused
        // by ResourceLedger.Normalize.
        ("resources", """
            machine nvarchar(64) NOT NULL, kind nvarchar(32) NOT NULL, path nvarchar(320) NOT NULL, owner nvarchar(450) NOT NULL,
            ticket nvarchar(128) NULL, created_utc nvarchar(64) NOT NULL, expires_utc nvarchar(64) NULL, released_utc nvarchar(64) NULL
            """, "ALTER TABLE resources ADD CONSTRAINT resources_key PRIMARY KEY (machine, kind, path); CREATE INDEX resources_owner ON resources (owner);"),
        // The reminders that nag (M.ORCH-T1932).
        ("reminders", """
            id nvarchar(64) NOT NULL PRIMARY KEY, message nvarchar(max) NOT NULL, every_minutes float NOT NULL, backoff float NOT NULL,
            max_every_minutes float NULL, until_ticket nvarchar(128) NULL, created_utc nvarchar(64) NOT NULL, next_utc nvarchar(64) NOT NULL,
            nags bigint NOT NULL, last_nag_utc nvarchar(64) NULL, ended_utc nvarchar(64) NULL, ended_why nvarchar(max) NULL
            """, null),
        // Roles and agents, the "who" (M.ORCH-T1742).
        ("roles", """
            name nvarchar(128) NOT NULL PRIMARY KEY, brief nvarchar(max) NOT NULL, model nvarchar(64) NULL, effort nvarchar(16) NULL,
            tools nvarchar(max) NULL, takes nvarchar(max) NULL, source nvarchar(max) NULL, updated_utc nvarchar(64) NOT NULL
            """, null),
        ("agents", """
            id nvarchar(64) NOT NULL PRIMARY KEY, role nvarchar(128) NOT NULL, owner nvarchar(256) NOT NULL, runtime nvarchar(32) NOT NULL,
            state nvarchar(16) NOT NULL, heartbeat_utc nvarchar(64) NULL, unit nvarchar(128) NULL, created_utc nvarchar(64) NOT NULL,
            state_utc nvarchar(64) NOT NULL
            """, "CREATE INDEX agents_owner ON agents (owner); CREATE INDEX agents_role ON agents (role);"),
    ];

    /// <summary>The schema's version: 1, the tables of SQLite's first ten steps; 2, <c>members</c> (M.ORCH-T670); 3,
    /// <c>assignments.account</c> (M.ORCH-T668); 4, <c>organizations</c> and <c>org_members</c> (M.ORCH-T655); 5, <c>term_grants</c>
    /// and <c>term_audit</c> (M.ORCH-T934); 6, <c>resources</c> (M.ORCH-T1676); 7, <c>reminders</c> (M.ORCH-T1932); 8, <c>roles</c> and
    /// <c>agents</c> (M.ORCH-T1742); 9, <c>assignments.agent</c> (M.ORCH-T1743); 10, <c>assignments.role</c> (M.ORCH-T1746).</summary>
    public const int SchemaVersion = 10;

    /// <summary>The changes to tables already there, one step per version past 1, as SQLite's migrations add them: a column
    /// added to a table there is one more step here (an <c>ALTER TABLE ... ADD</c>, also added to its <see cref="Tables"/> row so
    /// a new database has it). A new table needs no statement: every table missing is created at any version change.</summary>
    private static readonly (string Table, string Sql)[] Steps =
    [
        ("members", ""), // 2: members, a new table (created with the rest below)
        ("assignments", "IF COL_LENGTH(N'assignments', N'account') IS NULL ALTER TABLE assignments ADD account nvarchar(450) NULL;"), // 3: the login each was dispatched on (M.ORCH-T668)
        ("organizations", ""), // 4: organizations and org_members, new tables (created with the rest below; M.ORCH-T655)
        ("term_grants", ""), // 5: term_grants and term_audit, new tables (created with the rest below; M.ORCH-T934)
        ("resources", ""), // 6: resources, the resource ledger, a new table (created with the rest below; M.ORCH-T1676)
        ("reminders", ""), // 7: reminders, the reminders that nag, a new table (created with the rest below; M.ORCH-T1932)
        ("roles", ""), // 8: roles and agents, new tables (created with the rest below; M.ORCH-T1742)
        ("assignments", "IF COL_LENGTH(N'assignments', N'agent') IS NULL ALTER TABLE assignments ADD agent nvarchar(64) NULL;"), // 9: the agent working each (M.ORCH-T1743)
        ("assignments", "IF COL_LENGTH(N'assignments', N'role') IS NULL ALTER TABLE assignments ADD role nvarchar(128) NULL;"), // 10: the role each is worked as (M.ORCH-T1746)
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
    /// keys compare and sort as SQLite's do. Not on Azure SQL Database (M.ORCH-T1748): there it is provisioned ahead (with the
    /// same collation), and a login that is only a user of that database can't open <c>master</c> at all; a server found to be
    /// Azure's only once connected (engine edition 5, behind a name of its own) is left alone the same way.</summary>
    public static void CreateDatabase(string connection)
    {
        if (IsAzure(connection)) return;
        var b = new SqlConnectionStringBuilder(connection);
        var name = b.InitialCatalog;
        b.InitialCatalog = "master";
        using var master = new SqlConnection(b.ConnectionString);
        master.Open();
        Sql.Run(master, null, "IF CONVERT(int, SERVERPROPERTY('EngineEdition')) <> 5 AND DB_ID(@name) IS NULL EXEC (N'CREATE DATABASE ' + @quoted + N' COLLATE Latin1_General_100_BIN2');",
            ("@name", name), ("@quoted", "[" + name.Replace("]", "]]", StringComparison.Ordinal) + "]"));
    }
}
