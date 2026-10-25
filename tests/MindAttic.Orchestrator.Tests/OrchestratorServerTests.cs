using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The orchestrator's data on SQL Server (M.ORCH-T658): the same tables as SQLite's, a round trip through every store,
/// the hooks' copy kept in the file, and the move both ways with nothing lost. The round trips run against a scratch database
/// on this machine's SQL Server (localhost), dropped afterwards; with no server reachable they're skipped.</summary>
[NonParallelizable]
public sealed class OrchestratorServerTests
{
    private readonly TempDirs temps = new();
    private string? database;

    [TearDown]
    public void TearDown()
    {
        temps.DeleteAll();
        if (database is null) return;
        SqlConnection.ClearAllPools();
        using var master = new SqlConnection(OrchestratorServer.DefaultConnection(database: "master"));
        master.Open();
        Sql.Run(master, null, $"IF DB_ID(N'{database}') IS NOT NULL BEGIN ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]; END");
        database = null;
    }

    [Test]
    public void The_server_keeps_every_column_of_SQLites_tables_in_the_same_order()
    {
        // The dump is SELECT * from either store, so a column added to SQLite needs adding to the server's schema too.
        var db = new OrchestratorDb(Path.Combine(temps.New(), "orchestrator.db"));
        using var c = db.Open();
        foreach (var (table, columns, _) in OrchestratorServer.Tables)
        {
            var sqlite = Sql.Read(c, null, $"PRAGMA table_info({table});", r => r.GetString(1));
            var server = columns.Split(',').Select(d => Regex.Match(d.Trim(), "^\"?(\\w+)\"?").Groups[1].Value).ToList();
            Assert.That(server, Is.EqualTo(sqlite), table);
        }
        // Every table the dump lists is one the server keeps (M.ORCH-T670: members among them), so either store dumps it.
        var dumped = OrchestratorDb.Dump(c).Split('\n').Skip(1).Where(l => l.StartsWith("-- ", StringComparison.Ordinal)).Select(l => l[3..]).ToList();
        Assert.That(dumped, Does.Contain("members"));
        Assert.That(OrchestratorServer.Tables.Select(t => t.Table), Is.SupersetOf(dumped));
    }

    [Test]
    public void A_version_1_server_database_gets_the_members_table_and_keeps_its_rows()
    {
        // M.ORCH-T670: the live server was made at version 1, before members; its next open adds the table.
        var connection = Connection();
        using (var c = new OrchestratorServer(connection).Open())
        {
            Sql.Run(c, null, "INSERT INTO accounts (id, email) VALUES ('acct', 'a@example.com');");
            Sql.Run(c, null, "DROP TABLE members; ALTER TABLE assignments DROP COLUMN account; DELETE FROM schema_version; INSERT INTO schema_version (version) VALUES (1);");
        }
        // Another connection string to the same database: a process that hasn't checked its schema yet.
        var fresh = new SqlConnectionStringBuilder(connection) { ApplicationName = "MindAttic.Orchestrator.Test" }.ConnectionString;
        using (var c = new OrchestratorServer(fresh).Open())
        {
            Assert.That(Sql.Scalar(c, null, "SELECT MAX(version) FROM schema_version;"), Is.EqualTo(OrchestratorServer.SchemaVersion));
            Assert.That(Sql.Scalar(c, null, "SELECT COUNT(*) FROM members;"), Is.EqualTo(0));
            Assert.That(Sql.Scalar(c, null, "SELECT email FROM accounts;"), Is.EqualTo("a@example.com"));
            Assert.That(Sql.Scalar(c, null, "SELECT COL_LENGTH(N'assignments', N'account');"), Is.Not.Null.And.Not.EqualTo(DBNull.Value),
                "step 3 (M.ORCH-T668): the login each assignment was dispatched on");
        }
        var db = new OrchestratorDb(Path.Combine(temps.New(), "orchestrator.db")) { TestServer = new OrchestratorServer(fresh) };
        MemberRoster.Add(db, "n@example.com", "N", "member", "premium", null, DateTime.UtcNow);
        Assert.That(MemberRoster.Load(db).Single().Email, Is.EqualTo("n@example.com"));
    }

    [Test]
    public void Every_store_reads_back_what_it_wrote_on_SQL_Server_and_the_hooks_copy_follows()
    {
        var (db, store) = Scratch();

        store.SaveSettings(new OrchestratorSettings { MaxSessionsPerRepo = 7 });
        store.UpdateAssignments(a => a.Add(new Assignment("X-T1", "X", @"C:\x", "Claude", T(1), true) { Cli = 42 }));
        store.Save("reminders", new List<string> { "a" });
        db.AddUsageSamples([new UsageSample(T(1), 12.5, T(5), 100)], T(0));
        db.AddUsageSamples([new UsageSample(T(1), 99, T(5), 1)], T(0)); // the same reading again is left as it was
        db.AppendLedger([Window(null), Window(null), Window("acct")]);
        db.SaveUsageHistory([new DayUsage(new DateOnly(2026, 10, 22), "X", "Claude", 5, null, null)]);
        db.SaveTicketCosts([new SessionCost("s1", "X-T1", "fix", "X", 9, T(2), true, "sonnet")]);

        Assert.That(store.LoadSettings().MaxSessionsPerRepo, Is.EqualTo(7));
        Assert.That(store.LoadAssignments().Single(), Has.Property(nameof(Assignment.Cli)).EqualTo(42L).And.Property(nameof(Assignment.Worktree)).True);
        Assert.That(store.Load<List<string>>("reminders"), Is.EqualTo(new[] { "a" }));
        Assert.That(db.LoadUsageSamples().Single().Percent, Is.EqualTo(12.5));
        Assert.That(db.LoadLedger(), Has.Count.EqualTo(2), "one window per end and account, a NULL account matching NULL");
        Assert.That(db.LoadUsageHistory().Single().Fresh, Is.EqualTo(5));
        Assert.That(db.LoadTicketCosts().Single().Model, Is.EqualTo("sonnet"));

        // The hooks read the settings, assignments and saved state from the file: each write was copied there.
        var local = new OrchestratorDb(db.Path);
        Assert.That(new JsonStore(store.Directory, local).LoadSettings().MaxSessionsPerRepo, Is.EqualTo(7));
        Assert.That(new JsonStore(store.Directory, local).LoadAssignments().Single().TicketId, Is.EqualTo("X-T1"));
        using (var c = local.Open())
            Assert.That(Sql.Scalar(c, null, "SELECT COUNT(*) FROM pace_ledger;"), Is.EqualTo(0L), "the server alone keeps the rest");

        // A volatile row stays this machine's: in the file only.
        store.Save(Daemon.AllocateHoldsName, new List<string> { "held" });
        using (var s = db.OpenData())
            Assert.That(Sql.Scalar(s, null, "SELECT COUNT(*) FROM state WHERE name = @n;", ("@n", Daemon.AllocateHoldsName)), Is.EqualTo(0));
        Assert.That(new JsonStore(store.Directory, local).Load<List<string>>(Daemon.AllocateHoldsName), Is.EqualTo(new[] { "held" }));
    }

    /// <summary>M.ORCH-T811: under heavy concurrent load (many CLIs and subagents writing at once, orchestrator next --claim
    /// among them) the server's write lock (<see cref="OrchestratorServer.Lock"/>) can stay busy past its timeout; a write
    /// through the store retries rather than failing the command outright the moment that happens.</summary>
    [Test]
    public void A_write_retries_past_the_servers_write_lock_staying_busy_and_succeeds_once_it_frees_up()
    {
        var connection = Connection();
        var (_, store) = Scratch(connection);

        var busyBefore = OrchestratorDb.BusyTimeoutMs;
        var retriesBefore = OrchestratorDb.BusyRetries;
        var delayBefore = OrchestratorDb.BusyRetryDelay;
        OrchestratorDb.BusyTimeoutMs = 150; // short enough that the write below times out its first try, well before the lock frees
        OrchestratorDb.BusyRetries = 5;
        OrchestratorDb.BusyRetryDelay = TimeSpan.FromMilliseconds(100);
        try
        {
            using var holder = new OrchestratorServer(connection).Open();
            using var holderTx = holder.BeginTransaction();
            OrchestratorServer.Lock(holder, holderTx); // another writer has the lock first
            var release = Task.Run(() => { Thread.Sleep(350); holderTx.Commit(); }); // frees it only after the first try has already timed out

            store.SaveSettings(new OrchestratorSettings { MaxSessionsPerRepo = 11 }); // waits out the busy lock instead of throwing

            release.Wait();
        }
        finally
        {
            OrchestratorDb.BusyTimeoutMs = busyBefore;
            OrchestratorDb.BusyRetries = retriesBefore;
            OrchestratorDb.BusyRetryDelay = delayBefore;
        }
        Assert.That(store.LoadSettings().MaxSessionsPerRepo, Is.EqualTo(11), "the write went through once the other writer let go");
    }

    [Test]
    public void A_write_gives_up_once_the_servers_write_lock_outlasts_every_retry()
    {
        var connection = Connection();
        var (_, store) = Scratch(connection);

        var busyBefore = OrchestratorDb.BusyTimeoutMs;
        var retriesBefore = OrchestratorDb.BusyRetries;
        var delayBefore = OrchestratorDb.BusyRetryDelay;
        OrchestratorDb.BusyTimeoutMs = 100;
        OrchestratorDb.BusyRetries = 1;
        OrchestratorDb.BusyRetryDelay = TimeSpan.FromMilliseconds(50);
        try
        {
            using var holder = new OrchestratorServer(connection).Open();
            using var holderTx = holder.BeginTransaction();
            OrchestratorServer.Lock(holder, holderTx); // held for the whole test: every retry still finds it busy

            var ex = Assert.Throws<IOException>(() => store.SaveSettings(new OrchestratorSettings { MaxSessionsPerRepo = 12 }));
            Assert.That(ex!.Message, Does.Contain("stayed busy past its timeout"));

            holderTx.Rollback();
        }
        finally
        {
            OrchestratorDb.BusyTimeoutMs = busyBefore;
            OrchestratorDb.BusyRetries = retriesBefore;
            OrchestratorDb.BusyRetryDelay = delayBefore;
        }
    }

    [Test]
    public void Moving_to_SQL_Server_and_back_loses_nothing_and_writes_the_same_dump()
    {
        var dir = temps.New();
        var file = new OrchestratorDb(Path.Combine(dir, "orchestrator.db"));
        var store = new JsonStore(Path.Combine(dir, "store"), file);
        store.SaveSettings(new OrchestratorSettings { MaxSessionsPerRepo = 3 });
        store.SaveAssignments([new Assignment("Y-T2", "Y", @"C:\y", "Claude", T(2), false)]);
        file.AddUsageSamples([new UsageSample(T(1), 40, T(5), 7), new UsageSample(T(2), 41, T(5), 8)], T(0));
        file.AppendLedger([Window("a"), Window(null)]);
        file.SaveTicketCosts([new SessionCost("s2", "Y-T2", null, "Y", 3, T(3), false, null)]);
        var before = file.Dump();

        var connection = Connection();
        var copied = OrchestratorMigration.ToServer(file, connection, configure: false);
        Assert.That(copied.Single(c => c.Table == "usage_samples").Rows, Is.EqualTo(2));
        Assert.That(copied.Single(c => c.Table == "pace_ledger").Rows, Is.EqualTo(2));
        Assert.Throws<InvalidOperationException>(() => OrchestratorMigration.ToServer(file, connection, configure: false), "a server holding data is never copied over without --replace");

        var onServer = new OrchestratorDb(file.Path) { TestServer = new OrchestratorServer(connection) };
        Assert.That(onServer.Dump(), Is.EqualTo(before), "the dump reads the same from either store");
        new JsonStore(store.Directory, onServer).SaveSettings(new OrchestratorSettings { MaxSessionsPerRepo = 9 });

        OrchestratorMigration.ToSqlite(file, connection, configure: false);
        Assert.That(new JsonStore(store.Directory, file).LoadSettings().MaxSessionsPerRepo, Is.EqualTo(9), "back in the file, with what was changed on the server");
        Assert.That(file.LoadUsageSamples(), Has.Count.EqualTo(2));
        Assert.That(file.LoadLedger(), Has.Count.EqualTo(2));
    }

    [Test]
    public void The_orgs_live_on_SQL_Server_and_a_schema_3_database_gains_their_tables_keeping_its_data()
    {
        // M.ORCH-T655: the organizations and who's in each are the server's, behind the same store as the rest.
        var connection = Connection();
        var db = new OrchestratorDb(Path.Combine(temps.New(), "orchestrator.db")) { TestServer = new OrchestratorServer(connection) };
        db.SaveOrg(new Organization("sales", "Sales", 1));
        db.SetMember(new OrgMember("sales", "Jordan", "Jordan@Example.com"));
        db.SetMember(new OrgMember("sales", "Jordan", "jordan", "codex"));
        Assert.That(db.LoadOrgs().OrgOf(Providers.Codex, "jordan"), Is.EqualTo("sales"));
        Assert.That(db.LoadOrgs().OrgOfClaude("jordan@example.com"), Is.EqualTo("sales"));
        using (var c = new OrchestratorDb(db.Path).Open())
            Assert.That(Sql.Scalar(c, null, "SELECT COUNT(*) FROM org_members;"), Is.EqualTo(0L), "the server alone keeps them");

        // A server made at schema 3, before the orgs: its next open adds their tables and loses nothing.
        using (var c = new OrchestratorServer(connection).Open())
        {
            Sql.Run(c, null, "INSERT INTO accounts (id, email) VALUES ('acct', 'a@example.com');");
            Sql.Run(c, null, "DROP TABLE org_members; DROP TABLE organizations; DELETE FROM schema_version; INSERT INTO schema_version (version) VALUES (3);");
        }
        var fresh = new SqlConnectionStringBuilder(connection) { ApplicationName = "MindAttic.Orchestrator.Test" }.ConnectionString;
        var reopened = new OrchestratorDb(db.Path) { TestServer = new OrchestratorServer(fresh) };
        Assert.That(reopened.LoadOrgs().HasOrgs, Is.False, "one org, the default, until another is added");
        using (var c = new OrchestratorServer(fresh).Open())
        {
            Assert.That(Sql.Scalar(c, null, "SELECT MAX(version) FROM schema_version;"), Is.EqualTo(OrchestratorServer.SchemaVersion));
            Assert.That(Sql.Scalar(c, null, "SELECT email FROM accounts;"), Is.EqualTo("a@example.com"));
        }
    }

    private (OrchestratorDb Db, JsonStore Store) Scratch() => Scratch(Connection());

    private (OrchestratorDb Db, JsonStore Store) Scratch(string connection)
    {
        var dir = temps.New();
        var db = new OrchestratorDb(Path.Combine(dir, "orchestrator.db")) { TestServer = new OrchestratorServer(connection) };
        return (db, new JsonStore(Path.Combine(dir, "store"), db));
    }

    private string Connection()
    {
        database = "MindAttic.Orchestrator.Test-" + Guid.NewGuid().ToString("N")[..12];
        var connection = OrchestratorServer.DefaultConnection(database: database);
        try { OrchestratorServer.CreateDatabase(connection); }
        catch (SqlException ex)
        {
            database = null;
            Assert.Ignore($"No SQL Server reachable on localhost: {ex.Message}");
        }
        return connection;
    }

    private static DateTime T(int hours) => new DateTime(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc).AddHours(hours);

    private static LedgerWindow Window(string? account) =>
        new(T(0), T(5), WindowGrade.Ok, "fine", 98, 97, 1000, 10, 3, null, null, 0, 0, 0, 0, [], 0, account);
}
