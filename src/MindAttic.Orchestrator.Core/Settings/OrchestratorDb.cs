using System.Data.Common;
using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace MindAttic.Orchestrator.Settings;

/// <summary>
/// The orchestrator's SQLite database (M.ORCH-T490), kept in the MindAttic.Orchestrator repo and committed, as
/// MindAttic.Tickets keeps <c>data/Tickets.db</c>: <c>&lt;workspace&gt;\MindAttic.Orchestrator\data\orchestrator.db</c>, or the
/// file <see cref="EnvVar"/> names, so every process and worktree uses the main checkout's copy. Rollback journal (not WAL),
/// so the .db file alone holds every committed write and git commits it whole; <c>data/orchestrator.sql</c> beside it is a
/// readable dump (<see cref="Dump"/>) so its changes show up as text diffs. The schema is versioned by <c>PRAGMA user_version</c>:
/// each step in <see cref="Migrations"/> runs once, in order, so later tables (the rest of the orchestrator's JSON state) are
/// added as new steps.
/// Once <see cref="OrchestratorServer"/> is configured (M.ORCH-T658), that's true only of <see cref="Mirrored"/> (what the
/// hooks still read here): the rest of this file — <c>usage_samples</c>, <c>pace_ledger</c> among them — was cleared at the
/// cutover (<see cref="OrchestratorMigration.ToServer"/>) and stays empty from then on, the server alone keeping it current.
/// <c>data/orchestrator.sql</c> keeps dumping the live store either way, so it (or <c>orchestrator db status</c> /
/// <c>orchestrator ledger</c>) is where to look for the current history — not a bare read of this file, which an empty
/// pace_ledger here correctly says nothing about (M.ORCH-T812: a stale, un-cleared copy of it once did, and was mistaken for
/// a daemon that had stopped closing windows).
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
            : System.IO.Path.Combine(workspaceRoot ?? MindAttic.Tickets.Workspace.DefaultRoot, "MindAttic.Orchestrator",
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
            : System.IO.Directory.Exists(System.IO.Path.Combine(MindAttic.Tickets.Workspace.DefaultRoot, "MindAttic.Orchestrator"))
                ? DefaultPath(MindAttic.Tickets.Workspace.DefaultRoot)
                : System.IO.Path.Combine(fallbackDirectory, "orchestrator.db");

    /// <summary>The one database every process opens (M.ORCH-T535): <see cref="StatePath"/> with the live store's folder as the
    /// fallback. Nothing opens <see cref="DefaultPath"/> under the configured workspace root, which would split the state across
    /// two files whenever that root isn't the default one or has no checkout.</summary>
    public static string LivePath => StatePath(JsonStore.LiveDirectory);

    /// <summary>The checkout <paramref name="dbPath"/> is committed in: the folder holding <c>data/orchestrator.db</c>, or null for
    /// a database kept anywhere else (the machine-local fallback, a file the environment names), which no sync commits.</summary>
    public static string? RepoOf(string dbPath)
    {
        var full = System.IO.Path.GetFullPath(dbPath);
        var data = System.IO.Path.GetDirectoryName(full);
        return string.Equals(System.IO.Path.GetFileName(full), "orchestrator.db", StringComparison.OrdinalIgnoreCase)
            && string.Equals(System.IO.Path.GetFileName(data), "data", StringComparison.OrdinalIgnoreCase)
            ? System.IO.Path.GetDirectoryName(data) : null;
    }

    public string Path { get; } = System.IO.Path.GetFullPath(path);

    /// <summary>The readable dump written beside the database: <c>orchestrator.sql</c>.</summary>
    public string DumpPath => System.IO.Path.ChangeExtension(Path, ".sql");

    // ---- SQL Server, the store of record once configured (M.ORCH-T658) ----

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, OrchestratorServer> Servers = new(StringComparer.Ordinal);

    /// <summary>Whether this is the live database every process opens (not a test's or a scratch one), the one SQL Server
    /// takes over from.</summary>
    public bool IsLive => Environment.GetEnvironmentVariable(EnvVar) is not { Length: > 0 } &&
        string.Equals(Path, System.IO.Path.GetFullPath(LivePath), StringComparison.OrdinalIgnoreCase);

    /// <summary>The orchestrator's SQL Server database, the store of its data, when this is the live database and this machine
    /// has one configured (<see cref="OrchestratorServer.Configured"/>); null keeps everything in this SQLite file.</summary>
    public OrchestratorServer? Server => TestServer ?? (IsLive && OrchestratorServer.Configured() is { } c ? Servers.GetOrAdd(c, s => new OrchestratorServer(s)) : null);

    /// <summary>A scratch SQL Server database for a test's scratch file, which the live setting never reaches.</summary>
    internal OrchestratorServer? TestServer { get; init; }

    /// <summary>The tables the hooks read through stdlib sqlite3 that SQL Server keeps: every change to them is copied into this
    /// file as well (<see cref="WriteData(Action{DbConnection, DbTransaction}, bool, string[])"/>'s mirror).</summary>
    public static readonly string[] Mirrored = ["settings", "assignments", "state"];

    /// <summary>An open connection to the store of the orchestrator's data: SQL Server once configured, else this file.</summary>
    public DbConnection OpenData() => Server is { } server ? server.Open() : Open();

    /// <summary>
    /// Runs <paramref name="change"/> in one write transaction on the store of the orchestrator's data (its write lock taken at
    /// once, so writers queue rather than fail at commit), rewrites the dump from what it committed, then copies what
    /// <paramref name="mirror"/> names ("settings", "assignments", "state:&lt;name&gt;") into this file for the hooks.
    /// </summary>
    public void WriteData(Action<DbConnection, DbTransaction> change, bool dump = true, params string[] mirror) =>
        WriteDataIf((connection, tx) => { change(connection, tx); return dump; }, mirror);

    /// <summary><see cref="WriteData(Action{DbConnection, DbTransaction}, bool, string[])"/>, the change saying whether the dump
    /// needs writing (whether it changed anything the dump holds).</summary>
    public void WriteDataIf(Func<DbConnection, DbTransaction, bool> change, params string[] mirror)
    {
        string? text = null;
        var server = Server;
        if (server is null)
        {
            using var local = Open();
            using var localTx = local.BeginTransaction(deferred: false);
            // Waited on the file's lock while orchestrator db migrate held it: the data is SQL Server's now, written there below.
            server = Server;
            if (server is null)
            {
                text = change(local, localTx) ? Dump(local, localTx) : null;
                localTx.Commit();
            }
        }
        if (server is not null)
            text = WithBusyRetry(() =>
            {
                using var connection = server.Open();
                using var tx = connection.BeginTransaction();
                OrchestratorServer.Lock(connection, tx);
                var written = change(connection, tx) ? Dump(connection, tx) : null;
                tx.Commit();
                // Committed: a copy that can't be made now (the file held past its timeout) is made by the daemon's SyncMirror.
                if (mirror.Length > 0)
                    try { Mirror(connection, mirror); }
                    catch (Exception ex) when (ex is DbException or IOException or UnauthorizedAccessException) { }
                return written;
            });
        if (text is null) return;
        try { WriteDump(text); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>How many extra times a write retries taking SQL Server's write lock (<see cref="OrchestratorServer.Lock"/>) after
    /// it stayed busy past its timeout, and how long it waits before each retry (longer each time): many CLIs and subagents
    /// writing at once under heavy load is the normal case here (M.ORCH-T811), not a reason to fail the command outright over a
    /// lock another writer was about to release. Tests shorten both.</summary>
    internal static int BusyRetries { get; set; } = 3;
    internal static TimeSpan BusyRetryDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>Runs <paramref name="attempt"/>, retrying it (waiting <see cref="BusyRetryDelay"/> longer each time) whenever it
    /// throws because SQL Server's write lock stayed busy past its timeout, up to <see cref="BusyRetries"/> extra times. The
    /// exception from the last try is thrown as is once every retry still finds it busy.</summary>
    internal static T WithBusyRetry<T>(Func<T> attempt)
    {
        for (var retry = 0; ; retry++)
        {
            try { return attempt(); }
            catch (IOException) when (retry < BusyRetries)
            {
                Thread.Sleep(BusyRetryDelay * (retry + 1));
            }
        }
    }

    /// <summary>Copies what <paramref name="what"/> names from SQL Server (<paramref name="server"/>) into this file, replacing it.</summary>
    private void Mirror(DbConnection server, IEnumerable<string> what)
    {
        var copies = what.Select(w => (Name: w, Rows: w.StartsWith("state:", StringComparison.Ordinal)
                ? Sql.Read(server, null, "SELECT name, value, written_at FROM state WHERE name = @name;", Row, ("@name", w["state:".Length..]))
                : Sql.Read(server, null, $"SELECT * FROM {w};", Row)))
            .ToList();
        Write((connection, tx) =>
        {
            foreach (var (name, rows) in copies)
            {
                var table = name.StartsWith("state:", StringComparison.Ordinal) ? "state" : name;
                if (table == "state") Sql.Run(connection, tx, "DELETE FROM state WHERE name = @name;", ("@name", name["state:".Length..]));
                else Sql.Run(connection, tx, $"DELETE FROM {table};");
                foreach (var row in rows)
                    Sql.Run(connection, tx, $"INSERT INTO {table} ({string.Join(", ", row.Select(c => Sql.Quote(c.Column)))}) VALUES ({string.Join(", ", row.Select(c => "@" + c.Column))});",
                        [.. row.Select(c => ("@" + c.Column, c.Value))]);
            }
        }, dump: false);

        static (string Column, object? Value)[] Row(DbDataReader r) =>
            [.. Enumerable.Range(0, r.FieldCount).Select(i => (r.GetName(i), r.IsDBNull(i) ? null : r.GetValue(i)))];
    }

    /// <summary>Copies every mirrored table (<see cref="Mirrored"/>, the saved state but its volatile rows, which stay here)
    /// from SQL Server into this file: the daemon runs it now and then, so a copy a write couldn't make (this file held past
    /// its timeout) is made later.</summary>
    public void SyncMirror()
    {
        if (Server is not { } server) return;
        using var connection = server.Open();
        var names = Sql.Read(connection, null, "SELECT name FROM state;", r => r.GetString(0)).Where(n => !Volatile.Contains(n)).ToList();
        Mirror(connection, ["settings", "assignments", .. names.Select(n => "state:" + n)]);
        // A saved row gone from the server (deleted there) goes from here too, but never a volatile one, which only this file keeps.
        Write((c, tx) =>
        {
            foreach (var stale in Sql.Read(c, tx, "SELECT name FROM state;", r => r.GetString(0)).Where(n => !Volatile.Contains(n) && !names.Contains(n)))
                Sql.Run(c, tx, "DELETE FROM state WHERE name = @name;", ("@name", stale));
        }, dump: false);
    }

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
        // every other JsonStore file (limits, reminders, audit, the pace alarms, allocate holds, ...) one row of JSON under
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
        // 6: each CLI's /allocate, /allocate off and /crossrepo marks (M.ORCH-T528), were allocate-<pid>, noallocate-<pid>
        // and crossrepo-<pid> files in ~/.claude/session-context: one row per kind and CLI process, its text as the file held
        // it (the repos, comma-separated; empty for noallocate). The hooks write them through stdlib sqlite3; a file is
        // written again only while the database is held, and the daemon reads it in. Not in the dump: they last as long as
        // a CLI does.
        """
        CREATE TABLE cli_marks (
            kind       TEXT NOT NULL,
            pid        INTEGER NOT NULL,
            value      TEXT NOT NULL,
            written_at REAL NOT NULL,
            PRIMARY KEY (kind, pid)
        );
        """,
        // 7: which CLI made each claim (M.ORCH-T532): its process id as the pace gate resolves it, and its session then, so the
        // idle-claim nudge, the fair share and the agent board give a claim to the CLI that made it, not whichever /allocate
        // CLI covers its repo. Rows already there keep NULL in both: claimed by a CLI not known.
        """
        ALTER TABLE assignments ADD COLUMN cli INTEGER;
        ALTER TABLE assignments ADD COLUMN cli_session TEXT;
        """,
        // 8: each session's state as the context gate keeps it (M.ORCH-T528): its turn, CLI process, cwd, /goal, limit stop...
        // one row per session, its value the JSON <session>.state.json held. The context gate writes the row and still exports
        // the file (the Launcher's ClaudeRestartGate reads it); the daemon reads in a file without a row. Not in the dump.
        """
        CREATE TABLE session_states (
            session_id TEXT PRIMARY KEY,
            value      TEXT NOT NULL,
            written_at REAL NOT NULL
        );
        """,
        // 9: when each CLI's mark was first written (M.ORCH-T558), kept while the mark is rewritten (repos added) and gone with
        // it: /allocate warms up from it, one more subagent each minute since it turned on, across restarts. Rows already there
        // keep NULL: their written_at stands in.
        """
        ALTER TABLE cli_marks ADD COLUMN since REAL;
        """,
        // 10: the model each ticket is worked on (M.ORCH-T563): the one a claim's subagent was told to run on, and when its diff
        // had its one Opus review; and the model each session's cost was spent on, so orchestrator report can compare what a
        // completed ticket costs on Sonnet and on Opus. Rows already there keep NULL: not known.
        """
        ALTER TABLE assignments ADD COLUMN model TEXT;
        ALTER TABLE assignments ADD COLUMN reviewed_utc TEXT;
        ALTER TABLE ticket_costs ADD COLUMN model TEXT;
        """,
        // 11: the organization's members (M.ORCH-T670; see MemberRoster), the one record of who's in it: keyed by email
        // (normalized as AccountDirs.Normalize does), with a name, a role, a seat tier and whether they've joined yet (active)
        // or are still invited (pending). Edited with orchestrator members; joined to accounts (this machine's sign-ins) by
        // email. Times are Unix seconds, as accounts keeps them. A column added later is one more step here (ALTER TABLE).
        """
        CREATE TABLE members (
            email      TEXT PRIMARY KEY,
            name       TEXT,
            role       TEXT NOT NULL,
            tier       TEXT NOT NULL,
            status     TEXT NOT NULL,
            joined_at  REAL NOT NULL,
            updated_at REAL NOT NULL
        );
        """,
        // 12: the login each assignment was dispatched on, by its account email (M.ORCH-T668): a ticket assigned to a user is
        // dispatched only on that user's login, and its claim says so. Rows already there keep NULL: not known.
        """
        ALTER TABLE assignments ADD COLUMN account TEXT;
        """,
        // 13: organizations above people (M.ORCH-T655): each org by id, its name and its place in the switcher; and who's in
        // each, one row per login (its LLM provider and the login, for Claude its email or '' for the global login) naming its
        // org and the person it's theirs. A login no row names is the default org's ('default', MindAttic; not a row until
        // renamed), so everything that was here before is that one org.
        """
        CREATE TABLE organizations (
            id          TEXT PRIMARY KEY,
            name        TEXT NOT NULL,
            position    INTEGER NOT NULL,
            created_utc TEXT
        );
        CREATE TABLE org_members (
            provider  TEXT NOT NULL,
            login     TEXT NOT NULL,
            org_id    TEXT NOT NULL,
            person    TEXT NOT NULL,
            added_utc TEXT,
            PRIMARY KEY (provider, login)
        );
        CREATE INDEX org_members_org ON org_members (org_id);
        """,
        // 14: the Autopilot subsystem renamed to Allocate (M.ORCH-T779), internal naming only: the /allocate and /allocate off
        // marks this database already held under the old internal kind ("autopilot", "noautopilot") carry forward under the
        // new one, so a CLI with /allocate already on (or off) doesn't appear to flip the moment this ships; same for the
        // settings a saved settings.json or this table already held by their old property names. A guard row (same kind or
        // name already under the new one) is dropped first so the rename can't collide with its own primary key.
        """
        DELETE FROM cli_marks WHERE kind = 'allocate' AND pid IN (SELECT pid FROM cli_marks WHERE kind = 'autopilot');
        UPDATE cli_marks SET kind = 'allocate' WHERE kind = 'autopilot';
        DELETE FROM cli_marks WHERE kind = 'noallocate' AND pid IN (SELECT pid FROM cli_marks WHERE kind = 'noautopilot');
        UPDATE cli_marks SET kind = 'noallocate' WHERE kind = 'noautopilot';
        DELETE FROM settings WHERE name = 'Allocate' AND EXISTS (SELECT 1 FROM settings WHERE name = 'Autopilot');
        UPDATE settings SET name = 'Allocate' WHERE name = 'Autopilot';
        DELETE FROM settings WHERE name = 'AllocateSkip' AND EXISTS (SELECT 1 FROM settings WHERE name = 'AutopilotSkip');
        UPDATE settings SET name = 'AllocateSkip' WHERE name = 'AutopilotSkip';
        DELETE FROM settings WHERE name = 'AllocateHandMinutes' AND EXISTS (SELECT 1 FROM settings WHERE name = 'AutopilotHandMinutes');
        UPDATE settings SET name = 'AllocateHandMinutes' WHERE name = 'AutopilotHandMinutes';
        """,
        // 15: which Claude login each gate event was for (M.ORCH-T774): the gate log is one shared table for every CLI on the
        // machine, so a per-CLI login's own window (PaceLedgerWatch.TickAccounts) couldn't tell its own held and refused calls
        // from another account's and counted none at all, leaving its "closed over" tickets with no idea what the gate did
        // around the window. Rows already there keep NULL (from before this was kept), which still counts everywhere, as before.
        """
        ALTER TABLE gate_events ADD COLUMN account TEXT;
        """,
        // 16: the team gateway's viewing ACL and audit (M.ORCH-T934; see Sessions.TermGrants). term_grants: who (viewer, a
        // member's email) may watch or interact with the owner's sessions, all of them (session '') or one, until when (ISO-8601
        // UTC; NULL only for watch), and who granted it when. term_audit: one row per attach, detach, refusal or batch of remote
        // input (counts and sizes, never content), keyed by the id the gateway gave it so a repeated import adds it once; not
        // in the dump, as it grows with every attach.
        """
        CREATE TABLE term_grants (
            owner       TEXT NOT NULL,
            viewer      TEXT NOT NULL,
            session     TEXT NOT NULL,
            level       TEXT NOT NULL,
            expires_utc TEXT,
            created_by  TEXT,
            created_utc TEXT NOT NULL,
            PRIMARY KEY (owner, viewer, session)
        );
        CREATE TABLE term_audit (
            id      TEXT PRIMARY KEY,
            at_utc  TEXT NOT NULL,
            event   TEXT NOT NULL,
            viewer  TEXT NOT NULL,
            owner   TEXT,
            session TEXT NOT NULL,
            level   TEXT NOT NULL,
            remote  TEXT,
            frames  INTEGER NOT NULL,
            bytes   INTEGER NOT NULL,
            detail  TEXT
        );
        CREATE INDEX term_audit_at ON term_audit (at_utc);
        """,
    ];

    /// <summary>State rows rewritten every daemon tick or dashboard minute, left out of the dump so they don't produce a
    /// commit every <see cref="Daemon.SyncOrchestratorDbIfDue"/>: what allocate holds back right now, the stale claims, when
    /// the dashboard was last looked at, the pace status the hooks read (rl-status) and the roster of open CLIs. They're
    /// still in the .db, which is committed whenever the dump changes.</summary>
    internal static readonly string[] Volatile =
        [Daemon.AllocateHoldsName, Daemon.StaleTicketsName, "dashboard-seen", Budget.RateLimitStatusWriter.StateName, Sessions.CliRoster.StateName,
         // The hooks' own marks (M.ORCH-T528): written by every CLI's hooks, never history worth a commit.
         Setup.AgentGate.StopLatchStateName, Setup.AgentGate.ApiKeySetAsideStateName,
         "handoff", // the retired /handoff's mark (M.ORCH-T572), should a row of it be left
         Billing.BillingModeFile.StateName, Budget.ClaudeAccount.StateName, Budget.ReportedUsage.StateName,
         OrchestratorHistory.GateLogImportedName,
         "terminal-panes", // the retired terminal panes (M.ORCH-T618, removed by M.ORCH-T751), should a row of them be left
         Sessions.MoveLocks.StateName, // the account moves in flight (M.ORCH-T637)
         Budget.AccountPacer.ProbeStateName, // each login's meter probe: its asks and what came of them (M.ORCH-T659)
         Budget.PacePlan.StateName]; // each login's windows, planned against actual end percent (M.ORCH-T669)

    /// <summary>Every table, with the order the dump lists its rows in: by key, so a change shows up as a few lines in its diff.</summary>
    private static readonly (string Table, string Order, string Where)[] Tables =
    [
        ("accounts", "id", ""),
        ("members", "email", ""),
        ("usage_samples", "at", ""),
        ("pace_ledger", "end_utc, account", ""),
        ("usage_history", "day, repo, provider, account, before_utc", ""),
        ("ticket_costs", "session_id", ""),
        ("settings", "name", ""),
        ("assignments", "position", ""),
        ("organizations", "id", ""),
        ("org_members", "provider, login", ""),
        ("term_grants", "owner, viewer, session", ""),
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
    public static string Dump(DbConnection connection, DbTransaction? tx = null)
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

    /// <summary>The dump as it stands now, from the store of the orchestrator's data.</summary>
    public string Dump()
    {
        using var connection = OpenData();
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
        // A change only to rows the dump leaves out (Volatile) can't change it: not worth reading every table for. With SQL
        // Server the store of record, this file holds only what this machine shares, and the dump is written from the server.
        var text = dump && Server is null ? Dump(connection, tx) : null;
        tx.Commit();
        if (text is null) return;
        try { WriteDump(text); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Runs <paramref name="action"/> holding the database's write lock (BEGIN IMMEDIATE), so no other process's write is
    /// mid-flight and none starts until it returns: the file is whole while git reads it (M.ORCH-T536). Nothing is written.</summary>
    public T HoldingWrites<T>(Func<T> action)
    {
        using var connection = Open();
        using var tx = connection.BeginTransaction(deferred: false);
        try { return action(); }
        finally { tx.Rollback(); }
    }

    // ---- one state row, for the stores kept outside JsonStore (M.ORCH-T526) ----

    /// <summary><paramref name="name"/>'s <c>state</c> value and when it was written; null when none is kept.</summary>
    public (string Value, DateTime WrittenUtc)? LoadState(string name)
    {
        // A volatile row is this machine's, kept in this file; the rest are the orchestrator's data (M.ORCH-T658).
        using var connection = Volatile.Contains(name) ? Open() : OpenData();
        return Sql.Read(connection, null, "SELECT value, written_at FROM state WHERE name = @name;",
                r => (r.GetString(0), DateTime.UnixEpoch.AddTicks((long)(Sql.Real(r, 1) * TimeSpan.TicksPerSecond))), ("@name", name))
            .Select(row => ((string, DateTime)?)row).FirstOrDefault();
    }

    /// <summary>Replaces <paramref name="name"/>'s <c>state</c> value (a volatile one without rewriting the dump).</summary>
    public void SaveState(string name, string json)
    {
        (string, object?)[] row = [("name", name), ("value", json), ("written_at", (DateTime.UtcNow - DateTime.UnixEpoch).Ticks / (double)TimeSpan.TicksPerSecond)];
        if (Volatile.Contains(name)) Write((connection, tx) => Sql.Upsert(connection, tx, "state", ["name"], row), dump: false);
        else WriteData((connection, tx) => Sql.Upsert(connection, tx, "state", ["name"], row), true, "state:" + name);
    }

    /// <summary>Drops <paramref name="name"/>'s <c>state</c> value.</summary>
    public void DeleteState(string name)
    {
        if (Volatile.Contains(name)) Write((connection, tx) => Sql.Run(connection, tx, "DELETE FROM state WHERE name = @name;", ("@name", name)), dump: false);
        else WriteData((connection, tx) => Sql.Run(connection, tx, "DELETE FROM state WHERE name = @name;", ("@name", name)), true, "state:" + name);
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
