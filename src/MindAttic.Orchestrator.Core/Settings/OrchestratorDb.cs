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

    /// <summary>
    /// The database <see cref="JsonStore"/> keeps the orchestrator's settings and state in (M.ORCH-T524): <see cref="DefaultPath"/>
    /// under the default workspace, found before any setting is read (the workspace root is itself a setting). On a machine
    /// without a MindAttic.Orchestrator checkout there, so no repo to commit it to, a machine-local <c>orchestrator.db</c> in
    /// <paramref name="fallbackDirectory"/> (the store's folder under <c>%APPDATA%</c>). The hooks find it the same way
    /// (<see cref="Setup.AgentGate"/>).
    /// </summary>
    public static string StatePath(string fallbackDirectory) =>
        Environment.GetEnvironmentVariable(EnvVar) is { Length: > 0 } env ? env
            : System.IO.Directory.Exists(System.IO.Path.Combine(MindAttic.Workflow.Workspace.DefaultRoot, "MindAttic.Orchestrator"))
                ? DefaultPath(MindAttic.Workflow.Workspace.DefaultRoot)
                : System.IO.Path.Combine(fallbackDirectory, "orchestrator.db");

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
        // 2: the orchestrator's history, moved out of JSON files (M.ORCH-T494; see OrchestratorHistory). Times are ISO-8601 UTC
        // text, days yyyy-MM-dd. A unique index on each row's key makes the one-time import from the old files safe to repeat.
        """
        CREATE TABLE usage_samples (
            at           TEXT PRIMARY KEY,
            percent      REAL NOT NULL,
            reset        TEXT NOT NULL,
            window_fresh INTEGER NOT NULL
        );
        CREATE TABLE pace_ledger (
            end_utc           TEXT NOT NULL,
            account           TEXT,
            start_utc         TEXT NOT NULL,
            grade             TEXT NOT NULL,
            why               TEXT NOT NULL,
            peak_percent      REAL,
            final_percent     REAL,
            fresh             INTEGER NOT NULL,
            samples           INTEGER NOT NULL,
            minutes_at_target REAL NOT NULL,
            limit_hit_utc     TEXT,
            weekly_hit_utc    TEXT,
            held              INTEGER NOT NULL,
            held_minutes      REAL NOT NULL,
            refused           INTEGER NOT NULL,
            prompts_answered  INTEGER NOT NULL,
            exempt            INTEGER NOT NULL,
            gaps              TEXT NOT NULL
        );
        CREATE UNIQUE INDEX pace_ledger_window ON pace_ledger (end_utc, IFNULL(account, ''));
        CREATE TABLE usage_history (
            day        TEXT NOT NULL,
            repo       TEXT NOT NULL,
            provider   TEXT NOT NULL,
            account    TEXT,
            before_utc TEXT,
            fresh      INTEGER NOT NULL
        );
        CREATE UNIQUE INDEX usage_history_key ON usage_history (day, repo, provider, IFNULL(account, ''), IFNULL(before_utc, ''));
        CREATE TABLE ticket_costs (
            session_id TEXT PRIMARY KEY,
            ticket_id  TEXT NOT NULL,
            type       TEXT,
            repo       TEXT NOT NULL,
            fresh      INTEGER NOT NULL,
            last_utc   TEXT NOT NULL,
            own_share  INTEGER NOT NULL
        );
        """,
        // 3: the rest of the orchestrator's JSON state (M.ORCH-T524; see JsonStore). settings.json is one row per setting, its
        // value as JSON, so a change shows up as one line of the dump; assignments.json one row per assignment, in list order;
        // every other JsonStore file (limits, reminders, audit, the pace alarms, autopilot holds, ...) one row of JSON under
        // its old file name, with when it was written (Unix seconds) so a reader can tell a running daemon's from a stale one.
        """
        CREATE TABLE settings (
            name  TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );
        CREATE TABLE assignments (
            position          INTEGER PRIMARY KEY,
            ticket_id         TEXT,
            repo              TEXT,
            directory         TEXT,
            provider          TEXT,
            started_utc       TEXT,
            worktree          INTEGER,
            pilot             INTEGER,
            pilot_started_utc TEXT,
            log               TEXT
        );
        CREATE TABLE state (
            name       TEXT PRIMARY KEY,
            value      TEXT NOT NULL,
            written_at REAL NOT NULL
        );
        """,
        // 4: the lease files and the pace gate's event log (M.ORCH-T526). A lease (dispatch, limit-wake, pace-ledger,
        // reminders) is one row naming the process holding it (its pid and start time, so a holder that exited or crashed is
        // told from a live one, and a reused pid isn't taken for it); the pace gate's holds, refusals and answered prompts, one
        // row each, were pace-gate.jsonl. Neither is in the dump: they change every tick and mean nothing on another machine.
        """
        CREATE TABLE leases (
            name     TEXT PRIMARY KEY,
            holder   TEXT NOT NULL,
            pid      INTEGER NOT NULL,
            started  REAL,
            taken_at REAL NOT NULL
        );
        CREATE TABLE gate_events (
            id       INTEGER PRIMARY KEY AUTOINCREMENT,
            at       TEXT NOT NULL,
            decision TEXT NOT NULL,
            held     REAL NOT NULL,
            tool     TEXT,
            percent  REAL
        );
        CREATE INDEX gate_events_at ON gate_events (at);
        """,
        // 5: the pace gate's subagent slots and wants (M.ORCH-T527), were files in ~/.claude/session-context's subagent-slots
        // and subagent-wants with a subagent-slots.lock beside them. A slot is a place an Agent call was let through into,
        // held for the CLI it's for until its SubagentStart marks the subagent (or it goes untouched past its life); a want is
        // one waiting call (its CLI and the gate's pid). Each gate's check and the slot it takes are one write transaction,
        // so calls sent together queue on SQLite's lock instead of the .lock file. Not in the dump: they last seconds.
        """
        CREATE TABLE subagent_slots (
            id         INTEGER PRIMARY KEY AUTOINCREMENT,
            cli        TEXT NOT NULL,
            gate_pid   INTEGER NOT NULL,
            taken_ns   INTEGER NOT NULL,
            touched_at REAL NOT NULL
        );
        CREATE TABLE subagent_wants (
            cli        TEXT NOT NULL,
            gate_pid   INTEGER NOT NULL,
            touched_at REAL NOT NULL,
            PRIMARY KEY (cli, gate_pid)
        );
        """,
    ];

    /// <summary>State rows rewritten every daemon tick or dashboard minute, left out of the dump so they don't produce a
    /// commit every <see cref="Daemon.SyncOrchestratorDbIfDue"/>: what autopilot holds back right now, the stale claims, when
    /// the dashboard was last looked at, the pace status the hooks read (rl-status) and the roster of open CLIs. They're
    /// still in the .db, which is committed whenever the dump changes.</summary>
    internal static readonly string[] Volatile =
        [Daemon.AutopilotHoldsName, Daemon.StaleTicketsName, "dashboard-seen", Budget.RateLimitStatusWriter.StateName, Sessions.CliRoster.StateName,
         // The hooks' own marks (M.ORCH-T528): written by every CLI's hooks, never history worth a commit.
         Setup.AgentGate.StopLatchStateName, Setup.AgentGate.ApiKeySetAsideStateName, Sessions.Handoff.StateName];

    /// <summary>Every table, with the order the dump lists its rows in: by key, so a change shows up as a few lines in its diff.</summary>
    private static readonly (string Table, string Order, string Where)[] Tables =
    [
        ("accounts", "id", ""),
        ("usage_samples", "at", ""),
        ("pace_ledger", "end_utc, account", ""),
        ("usage_history", "day, repo, provider, account, before_utc", ""),
        ("ticket_costs", "session_id", ""),
        ("settings", "name", ""),
        ("assignments", "position", ""),
        ("state", "name", " WHERE name NOT IN (" + string.Join(", ", Volatile.Select(v => "'" + v + "'")) + ")"),
    ];

    /// <summary>The columns the dump lists for <paramref name="table"/>: all of them, but <c>state</c>'s write time, which
    /// changes with every write where only the name and value are history worth a diff.</summary>
    private static string Columns(string table) => table == "state" ? "name, value" : "*";

    /// <summary>How long a connection waits for another process's write before giving up with SQLITE_BUSY; tests shorten it.
    /// Microsoft.Data.Sqlite also retries a busy command until its own timeout (30 s by default), so that's held to the same.</summary>
    internal static int BusyTimeoutMs { get; set; } = 5000;

    public static int Version => Migrations.Length;

    /// <summary>An open connection with the schema brought up to date. Not pooled: a closed connection lets go of the file, so
    /// git can commit or replace it.</summary>
    public SqliteConnection Open()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path,
            Pooling = false,
            DefaultTimeout = Math.Max(1, (BusyTimeoutMs + 999) / 1000),
        }.ToString());
        try
        {
            connection.Open();
            Execute(connection, $"PRAGMA busy_timeout = {BusyTimeoutMs}; PRAGMA journal_mode = DELETE;");
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
        foreach (var (table, order, where) in Tables)
        {
            sb.Append("\n-- ").Append(table).Append('\n');
            using var command = connection.CreateCommand();
            command.Transaction = tx;
            command.CommandText = $"SELECT {Columns(table)} FROM {table}{where} ORDER BY {order};";
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
        // Each writer its own temp file, so two processes writing at once can't move each other's half-written text into place.
        var temp = $"{DumpPath}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temp, dump, new UTF8Encoding(false));
            File.Move(temp, DumpPath, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    /// <summary>
    /// Runs <paramref name="change"/> in one write transaction (taken at once, so writers queue on the busy timeout rather than
    /// fail at commit), then rewrites the dump from what it committed. A dump that can't be written now (the file held) is left
    /// for the next change: the .db is the store.
    /// </summary>
    public void Write(Action<SqliteConnection, SqliteTransaction> change, bool dump = true)
    {
        using var connection = Open();
        using var tx = connection.BeginTransaction(deferred: false);
        change(connection, tx);
        // A change only to rows the dump leaves out (Volatile) can't change it: not worth reading every table for.
        var text = dump ? Dump(connection, tx) : null;
        tx.Commit();
        if (text is null) return;
        try { WriteDump(text); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    // ---- one state row, for the stores kept outside JsonStore (M.ORCH-T526) ----

    /// <summary><paramref name="name"/>'s <c>state</c> value and when it was written; null when none is kept.</summary>
    public (string Value, DateTime WrittenUtc)? LoadState(string name)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value, written_at FROM state WHERE name = $name;";
        command.Parameters.AddWithValue("$name", name);
        using var r = command.ExecuteReader();
        return r.Read() ? (r.GetString(0), DateTime.UnixEpoch.AddTicks((long)(r.GetDouble(1) * TimeSpan.TicksPerSecond))) : null;
    }

    /// <summary>Replaces <paramref name="name"/>'s <c>state</c> value (a volatile one without rewriting the dump).</summary>
    public void SaveState(string name, string json) => Write((connection, tx) =>
    {
        using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "INSERT OR REPLACE INTO state (name, value, written_at) VALUES ($name, $value, $at);";
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$value", json);
        command.Parameters.AddWithValue("$at", (DateTime.UtcNow - DateTime.UnixEpoch).Ticks / (double)TimeSpan.TicksPerSecond);
        command.ExecuteNonQuery();
    }, dump: !Volatile.Contains(name));

    /// <summary>Drops <paramref name="name"/>'s <c>state</c> value.</summary>
    public void DeleteState(string name) => Write((connection, tx) =>
    {
        using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "DELETE FROM state WHERE name = $name;";
        command.Parameters.AddWithValue("$name", name);
        command.ExecuteNonQuery();
    }, dump: !Volatile.Contains(name));

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
