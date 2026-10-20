using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Reminders;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Setup;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The orchestrator's settings and state in data/orchestrator.db (M.ORCH-T524), where they were JSON files: each old
/// file read in once and kept as .bak, writers at once never dropping each other's change, no .lock files, a hook reading the
/// tables mid-write, and every process (a worktree's too) on the main checkout's database. Each test has its own scratch
/// database (<see cref="OrchestratorDbPerTestAttribute"/>).</summary>
public sealed class JsonStoreTests
{
    private string dir = "";

    [SetUp]
    public void SetUp() => dir = Path.Combine(Path.GetTempPath(), "orch-store-" + Guid.NewGuid().ToString("N"), "nested");

    [TearDown]
    public void TearDown()
    {
        var top = Path.GetDirectoryName(dir)!;
        if (Directory.Exists(top)) Directory.Delete(top, recursive: true);
    }

    private string File(string name) => Path.Combine(dir, name);

    private void Write(string name, string text)
    {
        Directory.CreateDirectory(dir);
        System.IO.File.WriteAllText(File(name), text);
    }

    private static List<string> Rows(OrchestratorDb db, string sql)
    {
        using var connection = db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var r = command.ExecuteReader();
        var rows = new List<string>();
        while (r.Read()) rows.Add(string.Join("|", Enumerable.Range(0, r.FieldCount).Select(i => r.IsDBNull(i) ? "NULL" : Convert.ToString(r.GetValue(i), System.Globalization.CultureInfo.InvariantCulture))));
        return rows;
    }

    [Test]
    public void The_store_is_the_scratch_database_and_nothing_is_written_beside_it()
    {
        var store = new JsonStore(dir);
        Assert.That(store.Db.Path, Is.EqualTo(Path.GetFullPath(Environment.GetEnvironmentVariable(OrchestratorDb.EnvVar)!)));
        store.SaveSettings(new OrchestratorSettings { MaxConcurrentSessions = 3 });
        store.UpdateAssignments(all => all.Add(new Assignment("X-T1", "X", @"D:\W\X", "Claude", DateTime.UtcNow, true)));
        Assert.That(Directory.Exists(dir), Is.False, "no JSON, temp or .lock file in the old folder");
        Assert.That(store.LoadSettings().MaxConcurrentSessions, Is.EqualTo(3));
    }

    [Test]
    public void Settings_json_is_read_in_once_kept_as_bak_and_never_read_over_the_table_again()
    {
        Write("settings.json", """{ "MaxConcurrentSessions": 9, "QuietHours": ["22:00-07:00"], "PaceProfile": "front-loaded" }""");
        var store = new JsonStore(dir);
        var s = store.LoadSettings();
        Assert.That((s.MaxConcurrentSessions, s.QuietHours.Single(), s.PaceProfile), Is.EqualTo((9, "22:00-07:00", "front-loaded")));
        Assert.That(System.IO.File.Exists(File("settings.json")), Is.False);
        Assert.That(System.IO.File.ReadAllText(File("settings.json.bak")), Does.Contain("\"MaxConcurrentSessions\": 9"), "the old file kept as .bak");
        Assert.That(Rows(store.Db, "SELECT name, value FROM settings WHERE name IN ('MaxConcurrentSessions', 'QuietHours') ORDER BY name"),
            Is.EqualTo(new[] { "MaxConcurrentSessions|9", "QuietHours|[\"22:00-07:00\"]" }), "one row per setting");

        // A process from before the move saves its settings.json again: the table wins, and the file is only set aside.
        Write("settings.json", """{ "MaxConcurrentSessions": 2 }""");
        Assert.That(store.LoadSettings().MaxConcurrentSessions, Is.EqualTo(9));
        Assert.That(System.IO.File.Exists(File("settings.json")), Is.False);
    }

    [Test]
    public void Assignments_json_is_read_in_once_one_row_per_assignment_in_order()
    {
        Write("assignments.json", """
            [ { "TicketId": "X-T2", "Repo": "X", "Directory": "D:\\W\\X", "Provider": "Claude", "StartedUtc": "2026-10-19T19:31:13.2113457Z", "Worktree": true, "Pilot": 42, "Log": "x.log" },
              { "TicketId": "X-T1", "Repo": "X", "Directory": "D:\\W\\X", "Provider": "Claude", "StartedUtc": "2026-10-19T20:00:00Z", "Worktree": false } ]
            """);
        var store = new JsonStore(dir);
        var all = store.LoadAssignments();
        Assert.That(all.Select(a => (a.TicketId, a.Worktree, a.Pilot)), Is.EqualTo(new[] { ("X-T2", true, (int?)42), ("X-T1", false, null) }));
        Assert.That(all[0].StartedUtc, Is.EqualTo(new DateTime(2026, 10, 19, 19, 31, 13, DateTimeKind.Utc).AddTicks(2113457)));
        Assert.That(Rows(store.Db, "SELECT position, ticket_id, worktree, pilot, log FROM assignments ORDER BY position"),
            Is.EqualTo(new[] { "0|X-T2|1|42|x.log", "1|X-T1|0|NULL|NULL" }));
        Assert.That((System.IO.File.Exists(File("assignments.json")), System.IO.File.Exists(File("assignments.json.bak"))), Is.EqualTo((false, true)));
    }

    [Test]
    public void Every_other_store_file_is_read_in_once_as_a_state_row()
    {
        var hit = new DateTime(2026, 9, 17, 5, 21, 20, DateTimeKind.Utc);
        Write("limits.json", JsonSerializer.Serialize(new[] { new LimitObservation(LimitKind.Session, hit, hit.AddHours(2), 4_387_274) }));
        Write("reminders.json", """[ { "Id": "r1", "Text": "stand up", "DueUtc": "2026-10-20T15:00:00Z" } ]""");
        Write("pace-alarm-walls.json", """[ "2026-09-17T07:10:00Z" ]""");
        Write("limit-wakes.json", """{ "s@2026-10-18T20:20:00Z": "2026-10-18T20:20:00Z" }""");
        var store = new JsonStore(dir);

        Assert.That(store.LoadLimits().Single().FreshUsed, Is.EqualTo(4_387_274));
        Assert.That(store.HasLimits, Is.True);
        Assert.That(store.Load<List<Reminder>>(ReminderBook.Name).Single().Text, Is.EqualTo("stand up"));
        Assert.That(store.Load<List<DateTime>>("pace-alarm-walls").Single(), Is.EqualTo(new DateTime(2026, 9, 17, 7, 10, 0, DateTimeKind.Utc)));
        Assert.That(store.Load<Dictionary<string, DateTime>>("limit-wakes").Keys, Is.EqualTo(new[] { "s@2026-10-18T20:20:00Z" }));
        Assert.That(Directory.GetFiles(dir).Select(Path.GetFileName).Order(),
            Is.EqualTo(new[] { "limit-wakes.json.bak", "limits.json.bak", "pace-alarm-walls.json.bak", "reminders.json.bak" }));
        Assert.That(Rows(store.Db, "SELECT name FROM state ORDER BY name"), Is.EqualTo(new[] { "limit-wakes", "limits", "pace-alarm-walls", "reminders" }));
        Assert.That(store.WrittenUtc("limits"), Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromMinutes(1)));
    }

    [Test]
    public void An_old_file_that_cant_be_read_in_is_left_where_it_is_and_loads_as_defaults_that_are_never_saved_over_it()
    {
        Write("settings.json", """{ "BlockBudget": 20000000, }, """);
        var store = new JsonStore(dir);
        Assert.That(store.LoadSettings().BlockBudget, Is.Null, "defaults");
        Assert.That(store.TryLoad<OrchestratorSettings>(JsonStore.SettingsName, out _), Is.False);
        Assert.That(() => store.LoadSettingsToChange(), Throws.InstanceOf<InvalidDataException>(), "a hand edit's typo");
        Assert.That(System.IO.File.Exists(File("settings.json")), Is.True, "left for you to fix");

        Write("assignments.json", "[ { \"TicketId\": \"X-T1\", ");
        Assert.That(() => store.UpdateAssignments(all => all.Clear()), Throws.InstanceOf<InvalidDataException>());
        Assert.That(System.IO.File.ReadAllText(File("assignments.json")), Is.EqualTo("[ { \"TicketId\": \"X-T1\", "), "untouched for the next try");

        // Fixed, it's read in; or a save replaces it outright.
        Write("settings.json", """{ "BlockBudget": 20000000 }""");
        Assert.That(store.LoadSettingsToChange().BlockBudget, Is.EqualTo(20_000_000));
        Write("limits.json", "[ {");
        Assert.That(store.TryLoadLimits(out _), Is.False);
        store.SaveLimits([]);
        Assert.That(store.TryLoadLimits(out var none) && none.Count == 0, Is.True);
        Assert.That(System.IO.File.Exists(File("limits.json.bak")), Is.True, "the saved-over file is kept as .bak");
    }

    [Test]
    public void An_old_file_held_or_denied_right_now_is_read_in_once_it_can_be()
    {
        Write("settings.json", """{ "MaxConcurrentSessions": 9 }""");
        var store = new JsonStore(dir);
        using (new FileStream(File("settings.json"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.That(store.TryLoad<OrchestratorSettings>(JsonStore.SettingsName, out var held), Is.False);
            Assert.That(held.MaxConcurrentSessions, Is.EqualTo(new OrchestratorSettings().MaxConcurrentSessions), "defaults while held");
        }
        Write("limits.json", "[]");
        var file = new FileInfo(File("limits.json"));
        var deny = new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.ReadData, AccessControlType.Deny);
        var acl = file.GetAccessControl();
        acl.AddAccessRule(deny);
        file.SetAccessControl(acl);
        try { Assert.That(store.TryLoadLimits(out _), Is.False, "denied"); }
        finally
        {
            acl = file.GetAccessControl();
            acl.RemoveAccessRule(deny);
            file.SetAccessControl(acl);
        }
        Assert.That(store.LoadSettings().MaxConcurrentSessions, Is.EqualTo(9), "read in once it's free");
        Assert.That(store.TryLoadLimits(out _), Is.True);
    }

    [Test]
    public void Writers_at_once_never_drop_each_others_change_and_no_lock_file_is_left()
    {
        // A dashboard's Dispatch and the daemon's autopilot each loaded, changed and saved: one dropped the other's (M.ORCH-T119).
        // Each change is one write transaction now: the 24 writers queue on SQLite's lock, every one's row survives.
        Parallel.For(0, 24, i => new JsonStore(dir).UpdateAssignments(all =>
            all.Add(new Assignment($"X-T{i}", "X", @"D:\W\X", "Claude", DateTime.UtcNow, true))));
        Assert.That(new JsonStore(dir).LoadAssignments().Select(a => a.TicketId), Is.EquivalentTo(Enumerable.Range(0, 24).Select(i => $"X-T{i}")));

        Parallel.For(0, 24, i => new JsonStore(dir).Update<List<int>>("counter", all => all.Add(i)));
        Assert.That(new JsonStore(dir).Load<List<int>>("counter"), Is.EquivalentTo(Enumerable.Range(0, 24)));

        Parallel.For(0, 40, i => new JsonStore(dir).SaveSettings(new OrchestratorSettings { MaxConcurrentSessions = 1 + i % 5 }));
        Assert.That(new JsonStore(dir).LoadSettings().MaxConcurrentSessions, Is.InRange(1, 5), "always one whole save, never a torn mix");
        Assert.That(Directory.Exists(dir) ? Directory.GetFiles(dir) : [], Is.Empty, "no .lock or temp file");
    }

    [Test]
    public void Writers_in_other_processes_at_once_never_drop_each_others_change()
    {
        // The daemon, a dashboard and the CLI are separate processes: SQLite's file lock, not anything in this one, keeps them apart.
        var db = Environment.GetEnvironmentVariable(OrchestratorDb.EnvVar)!;
        new JsonStore(dir).SaveAssignments([]);
        var script = Path.Combine(Path.GetDirectoryName(db)!, "writer.py");
        System.IO.File.WriteAllText(script, """
            import json, sqlite3, sys
            for i in range(20):
                db = sqlite3.connect(sys.argv[1], timeout=30, isolation_level=None)
                db.execute("BEGIN IMMEDIATE")
                n = db.execute("SELECT IFNULL(MAX(position), -1) + 1 FROM assignments").fetchone()[0]
                db.execute("INSERT INTO assignments (position, ticket_id) VALUES (?, ?)", (n, sys.argv[2] + "-T" + str(i)))
                db.execute("COMMIT")
                db.close()
            """);
        var writers = new[] { "A", "B" }.Select(who =>
        {
            var psi = new ProcessStartInfo("python") { UseShellExecute = false };
            foreach (var a in new[] { "-I", script, db, who }) psi.ArgumentList.Add(a);
            return Process.Start(psi)!;
        }).ToList();
        for (var i = 0; i < 20; i++)
            new JsonStore(dir).UpdateAssignments(all => all.Add(new Assignment($"C-T{i}", "C", "", "Claude", DateTime.UtcNow, false)));
        foreach (var w in writers) Assert.That(w.WaitForExit(60_000) && w.ExitCode == 0, Is.True);
        var ids = new JsonStore(dir).LoadAssignments().Select(a => a.TicketId).ToList();
        Assert.That(ids, Has.Count.EqualTo(60));
        Assert.That(ids.Distinct().Count(), Is.EqualTo(60));
    }

    [Test]
    public void Settings_and_assignments_round_trip_and_nulls_are_left_out()
    {
        var store = new JsonStore(dir);
        var s = new OrchestratorSettings
        {
            RepoBudgetPercent = new() { ["Prose"] = 40 },
            Reservations = new() { ["X-T5"] = 2_000_000 },
            BlockedBy = new() { ["X-T2"] = ["X-T1"] },
            QuietHours = ["22:00-07:00"],
            PaceCurveK = 0.55,
            NotifyUrl = null,
        };
        store.SaveSettings(s);
        var back = store.LoadSettings();
        Assert.That(back.RepoBudgetPercent["Prose"], Is.EqualTo(40));
        Assert.That(back.Reservations["X-T5"], Is.EqualTo(2_000_000));
        Assert.That(back.BlockedBy["X-T2"], Is.EqualTo(new[] { "X-T1" }));
        Assert.That(back.QuietHours, Is.EqualTo(new[] { "22:00-07:00" }));
        Assert.That(back.PaceCurveK, Is.EqualTo(0.55));
        Assert.That(Rows(store.Db, "SELECT name FROM settings"), Does.Not.Contain("NotifyUrl"), "nulls omitted");

        var a = new Assignment("X-T1", "X", @"D:\W\X", "Claude", new DateTime(2026, 10, 15, 4, 0, 0, DateTimeKind.Utc), true)
            { Pilot = 7, PilotStartedUtc = new DateTime(2026, 10, 15, 4, 1, 0, DateTimeKind.Utc), Log = "x.log" };
        store.SaveAssignments([a]);
        Assert.That(store.LoadAssignments().Single(), Is.EqualTo(a));
        store.SaveAssignments([]);
        Assert.That(store.LoadAssignments(), Is.Empty);
    }

    [Test]
    public void The_dump_lists_settings_and_state_but_not_what_changes_every_tick()
    {
        var store = new JsonStore(dir);
        store.SaveSettings(new OrchestratorSettings { MaxConcurrentSessions = 6 });
        store.SaveLimits([]);
        var dump = System.IO.File.ReadAllText(store.Db.DumpPath);
        Assert.That(dump, Does.Contain("INSERT INTO settings (name, value) VALUES ('MaxConcurrentSessions', '6');"));
        Assert.That(dump, Does.Contain("INSERT INTO state (name, value) VALUES ('limits', '[]');"), "no write time: it changes with every save");

        store.Save(Daemon.AutopilotHoldsName, new Daemon.AutopilotHolds(false, [], ["X-T1"]));
        store.Save(Daemon.StaleTicketsName, new List<string> { "X-T2" });
        store.Save("dashboard-seen", new Dictionary<string, DateTime> { ["LastSeenUtc"] = DateTime.UtcNow });
        Assert.That(System.IO.File.ReadAllText(store.Db.DumpPath), Is.EqualTo(dump), "rewritten every tick: in the .db, not the dump");
        Assert.That(store.Load<List<string>>(Daemon.StaleTicketsName), Is.EqualTo(new[] { "X-T2" }));
    }

    [Test]
    public void The_lease_is_one_holders_at_a_time_and_names_it()
    {
        using var first = new MindAttic.Orchestrator.Scheduling.DispatchLease(dir);
        using var second = new MindAttic.Orchestrator.Scheduling.DispatchLease(dir);
        Assert.That((first.TryAcquire(), second.TryAcquire()), Is.EqualTo((true, false)));
        using (var read = new FileStream(first.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            Assert.That(new StreamReader(read).ReadToEnd(), Is.EqualTo(Environment.ProcessId.ToString()));
        first.Dispose();
        Assert.That(second.TryAcquire(), Is.True, "free once its holder lets go");
    }

    /// <summary>The hooks' own reading code (<see cref="AgentGate.WorkspacePython"/>), run on its own with Python's stdlib
    /// sqlite3: what it finds in the orchestrator's database, as JSON.</summary>
    private static string Hook(string body, IDictionary<string, string?> env, string? cwd = null)
    {
        var script = Path.Combine(Path.GetTempPath(), "orch-hook-" + Guid.NewGuid().ToString("N") + ".py");
        System.IO.File.WriteAllText(script, "import json, os, re, sys, time\nfrom pathlib import Path\n" + AgentGate.WorkspacePython + "\n" + body + "\n");
        try
        {
            var psi = new ProcessStartInfo("python") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            if (cwd is not null) psi.WorkingDirectory = cwd;
            psi.ArgumentList.Add("-I");
            psi.ArgumentList.Add(script);
            foreach (var (k, v) in env) { if (v is null) psi.Environment.Remove(k); else psi.Environment[k] = v; }
            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEnd();
            var error = p.StandardError.ReadToEnd();
            Assert.That(p.WaitForExit(30_000), Is.True);
            Assert.That(p.ExitCode, Is.EqualTo(0), error);
            return output.Trim();
        }
        finally { System.IO.File.Delete(script); }
    }

    [Test]
    public void A_hook_reads_the_tables_and_mid_write_sees_the_last_committed_values_without_waiting()
    {
        var store = new JsonStore(dir);
        store.SaveSettings(new OrchestratorSettings { ExemptCommands = ["/deploy"], AutopilotSkip = ["Prose"] });
        store.SaveAssignments([new Assignment("X-T1", "X", "", "Claude", DateTime.UtcNow, false)]);
        store.Save(Daemon.AutopilotHoldsName, new Daemon.AutopilotHolds(true, [], []));
        var env = new Dictionary<string, string?> { ["APPDATA"] = dir };
        const string Read = """print(json.dumps([orch_settings().get("ExemptCommands"), orch_settings().get("AutopilotSkip"), orch_assignments(), (orch_state("autopilot-holds", 600) or {}).get("Quiet")]))""";
        Assert.That(Hook(Read, env), Is.EqualTo("""[["/deploy"], ["Prose"], ["X-T1"], true]"""));

        // A writer mid-transaction (the daemon saving settings): the hook reads what was committed, at once.
        using (var connection = store.Db.Open())
        using (var tx = connection.BeginTransaction(deferred: false))
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = tx;
                command.CommandText = "UPDATE settings SET value = '[\"/run\"]' WHERE name = 'ExemptCommands'; DELETE FROM assignments;";
                command.ExecuteNonQuery();
            }
            var clock = Stopwatch.StartNew();
            Assert.That(Hook(Read, env), Is.EqualTo("""[["/deploy"], ["Prose"], ["X-T1"], true]"""), "the committed values, never half a change");
            Assert.That(clock.Elapsed, Is.LessThan(TimeSpan.FromSeconds(4)), "a reader doesn't wait on the writer");
            tx.Commit();
        }
        Assert.That(Hook(Read, env), Is.EqualTo("""[["/run"], ["Prose"], [], true]"""), "and the change once it's committed");
    }

    [Test]
    public void A_hook_reads_state_only_while_fresh_and_old_files_until_they_are_read_in()
    {
        var store = new JsonStore(dir);
        var env = new Dictionary<string, string?> { ["APPDATA"] = Path.GetDirectoryName(Path.GetDirectoryName(dir)) };
        // Before any daemon read the files in: the hook reads the old files under %APPDATA%\MindAttic\MindAttic.Orchestrator.
        var live = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(dir))!, "MindAttic", "MindAttic.Orchestrator");
        Directory.CreateDirectory(live);
        System.IO.File.WriteAllText(Path.Combine(live, "settings.json"), """{ "KeepGoing": false }""");
        System.IO.File.WriteAllText(Path.Combine(live, "assignments.json"), """[ { "TicketId": "Y-T1" } ]""");
        const string Read = """print(json.dumps([orch_settings().get("KeepGoing"), orch_assignments(), orch_state("stale-tickets", 600)]))""";
        Assert.That(Hook(Read, env), Is.EqualTo("""[false, ["Y-T1"], null]"""));

        store.SaveSettings(new OrchestratorSettings { KeepGoing = true });
        store.SaveAssignments([]);
        store.Save(Daemon.StaleTicketsName, new List<string> { "Z-T1" });
        Assert.That(Hook(Read, env), Is.EqualTo("""[true, ["Y-T1"], ["Z-T1"]]"""), "the settings rows win; an empty table beside an unread file: the file");
        System.IO.File.Delete(Path.Combine(live, "assignments.json"));
        Assert.That(Hook(Read, env), Is.EqualTo("""[true, [], ["Z-T1"]]"""));

        using (var connection = store.Db.Open())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE state SET written_at = written_at - 3600 WHERE name = 'stale-tickets';";
            command.ExecuteNonQuery();
        }
        Assert.That(Hook("""print(json.dumps(orch_state("stale-tickets", 600)))""", env), Is.EqualTo("null"), "left by a daemon that stopped");
    }

    [Test]
    public void A_worktree_process_uses_the_main_checkouts_database()
    {
        // Agents work in worktrees of MindAttic.Orchestrator: theirs would be a stale copy, so neither the store nor the hooks
        // ever look in the working folder. Only paths are worked out here: the real database is never opened.
        var scratch = Environment.GetEnvironmentVariable(OrchestratorDb.EnvVar);
        var worktree = Path.Combine(Path.GetDirectoryName(dir)!, "MindAttic.worktrees", "MindAttic.Orchestrator", "M.ORCH-T1");
        Directory.CreateDirectory(Path.Combine(worktree, "data"));
        var main = Path.Combine(MindAttic.Workflow.Workspace.DefaultRoot, "MindAttic.Orchestrator");
        var expected = Directory.Exists(main) ? Path.Combine(main, "data", "orchestrator.db") : Path.Combine(dir, "orchestrator.db");
        var before = Environment.CurrentDirectory;
        try
        {
            Environment.SetEnvironmentVariable(OrchestratorDb.EnvVar, null);
            Environment.CurrentDirectory = worktree;
            Assert.That(OrchestratorDb.StatePath(dir), Is.EqualTo(expected));
            Assert.That(new JsonStore(dir).Db.Path, Is.EqualTo(Path.GetFullPath(expected)));
            Assert.That(Path.GetFullPath(Hook("print(ORCH_DB)", new Dictionary<string, string?> { [OrchestratorDb.EnvVar] = null, ["APPDATA"] = Path.GetDirectoryName(dir) }, worktree)),
                Is.EqualTo(Directory.Exists(main) ? Path.GetFullPath(expected) : Path.Combine(Path.GetDirectoryName(dir)!, "MindAttic", "MindAttic.Orchestrator", "orchestrator.db")),
                "the hooks find the same one");
        }
        finally
        {
            Environment.CurrentDirectory = before;
            Environment.SetEnvironmentVariable(OrchestratorDb.EnvVar, scratch);
        }
        Assert.That(Hook("print(ORCH_DB)", new Dictionary<string, string?> { [OrchestratorDb.EnvVar] = scratch }), Is.EqualTo(scratch), "MINDATTIC_ORCHESTRATOR_DB overrides it");
    }
}
