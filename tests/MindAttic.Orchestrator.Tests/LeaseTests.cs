using System.Diagnostics;
using System.Text.Json;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T526: the lease files, the pace status the hooks read and the pace gate's event log, moved into
/// orchestrator.db as schema step 4 (leases, gate_events, and the rl-status row of state).</summary>
public sealed class LeaseTests
{
    private string dir = "";
    private OrchestratorDb Db => new(OrchestratorDb.DefaultPath());

    [SetUp]
    public void SetUp() => dir = Directory.CreateTempSubdirectory("orch-lease-").FullName;

    [TearDown]
    public void TearDown()
    {
        OrchestratorDb.BusyTimeoutMs = 5000;
        try { Directory.Delete(dir, recursive: true); }
        catch (IOException) { }
    }

    /// <summary>The pid the lease's row names, or null when nobody holds it.</summary>
    internal static long? HolderPid(DispatchLease lease)
    {
        using var connection = lease.Db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT pid FROM leases WHERE name = $name;";
        command.Parameters.AddWithValue("$name", lease.Name);
        return command.ExecuteScalar() as long?;
    }

    private void SetHolder(string name, long pid, double? started)
    {
        using var connection = Db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR REPLACE INTO leases (name, holder, pid, started, taken_at) VALUES ($name, 'other', $pid, $started, 0);";
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$pid", pid);
        command.Parameters.AddWithValue("$started", (object?)started ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    /// <summary>Another live process, for a lease row to name.</summary>
    private static Process Sleeper() =>
        Process.Start(new ProcessStartInfo("ping", "-n 30 127.0.0.1") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true })!;

    // ---- the migration ----

    [Test]
    public void Step_4_adds_the_leases_and_gate_events_to_a_version_3_database_and_keeps_what_it_held()
    {
        var path = Path.Combine(dir, "v3.db");
        var store = new JsonStore(dir, new OrchestratorDb(path));
        store.SaveSettings(new OrchestratorSettings { BlockBudget = 1234 });
        // Back to how a version-3 database was: no step-4 tables. DropStep7's chain (which undoes gate_events.account, step
        // 14) runs before the tables it touches are dropped outright, or its ALTER would find no table left to alter.
        using (var connection = store.Db.Open())
            OrchestratorDb.Execute(connection, DropStep7 + "DROP TABLE leases; DROP TABLE gate_events; DROP TABLE subagent_slots; DROP TABLE subagent_wants; DROP TABLE cli_marks; DROP TABLE session_states; PRAGMA user_version = 3;");

        using (var connection = new OrchestratorDb(path).Open())
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version;";
            Assert.That(command.ExecuteScalar(), Is.EqualTo((long)OrchestratorDb.Version));
            command.CommandText = "SELECT count(*) FROM sqlite_master WHERE name IN ('leases', 'gate_events', 'gate_events_at');";
            Assert.That(command.ExecuteScalar(), Is.EqualTo(3L));
        }
        Assert.That(OrchestratorDb.Version, Is.GreaterThanOrEqualTo(4));
        Assert.That(new JsonStore(dir, new OrchestratorDb(path)).LoadSettings().BlockBudget, Is.EqualTo(1234), "what it held is kept");
    }

    /// <summary>Undoes step 7 (M.ORCH-T532's claimer columns), so an older version's steps run again from there.</summary>
    private const string DropStep7 = DropStep10 + "ALTER TABLE assignments DROP COLUMN cli; ALTER TABLE assignments DROP COLUMN cli_session;";

    /// <summary>Undoes step 15 (M.ORCH-T774's gate_events.account). Step 14 (M.ORCH-T779's Autopilot-to-Allocate rename) has no
    /// column or table of its own to drop, so it isn't part of this chain: replaying it against an older schema it was never
    /// meant for (cli_marks dropped and recreated, settings never touched) is harmless.</summary>
    internal const string DropStep15 = DropStep16 + "ALTER TABLE gate_events DROP COLUMN account; ";

    /// <summary>Undoes step 16 (M.ORCH-T934's term_grants and term_audit).</summary>
    internal const string DropStep16 = "DROP TABLE term_grants; DROP TABLE term_audit; ";

    /// <summary>Undoes step 13 (M.ORCH-T655's organizations and org_members).</summary>
    internal const string DropStep13 = DropStep15 + "DROP TABLE org_members; DROP TABLE organizations; ";

    /// <summary>Undoes steps 12 (M.ORCH-T668's assignments.account) and later.</summary>
    internal const string DropStep12 = DropStep13 + "ALTER TABLE assignments DROP COLUMN account; ";

    /// <summary>Undoes steps 11 (M.ORCH-T670's members table) and later.</summary>
    internal const string DropStep11 = DropStep12 + "DROP TABLE members; ";

    /// <summary>Undoes steps 10 (M.ORCH-T563's model columns) and later.</summary>
    internal const string DropStep10 = DropStep11 + "ALTER TABLE assignments DROP COLUMN model; ALTER TABLE assignments DROP COLUMN reviewed_utc; ALTER TABLE ticket_costs DROP COLUMN model; ";

    [Test]
    public void Step_5_adds_the_subagent_slots_and_wants_to_a_version_4_database_and_keeps_what_it_held()
    {
        var path = Path.Combine(dir, "v4.db");
        var store = new JsonStore(dir, new OrchestratorDb(path));
        store.SaveSettings(new OrchestratorSettings { BlockBudget = 4321 });
        // Back to how a version-4 database was: no step-5 tables (M.ORCH-T527).
        using (var connection = store.Db.Open())
            OrchestratorDb.Execute(connection, "DROP TABLE subagent_slots; DROP TABLE subagent_wants; DROP TABLE cli_marks; DROP TABLE session_states; " + DropStep7 + " PRAGMA user_version = 4;");

        using (var connection = new OrchestratorDb(path).Open())
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version;";
            Assert.That(command.ExecuteScalar(), Is.EqualTo((long)OrchestratorDb.Version));
            command.CommandText = "SELECT count(*) FROM sqlite_master WHERE name IN ('subagent_slots', 'subagent_wants');";
            Assert.That(command.ExecuteScalar(), Is.EqualTo(2L));
        }
        Assert.That(OrchestratorDb.Version, Is.GreaterThanOrEqualTo(5));
        Assert.That(new JsonStore(dir, new OrchestratorDb(path)).LoadSettings().BlockBudget, Is.EqualTo(4321), "what it held is kept");
    }

    [Test]
    public void Steps_6_to_9_add_the_cli_marks_claimers_and_session_states_to_a_version_5_database_and_keeps_what_it_held()
    {
        var path = Path.Combine(dir, "v5.db");
        var store = new JsonStore(dir, new OrchestratorDb(path));
        store.SaveSettings(new OrchestratorSettings { BlockBudget = 5555 });
        // Back to how a version-5 database was: no cli_marks, claimer columns (M.ORCH-T532) or session_states (M.ORCH-T528).
        using (var connection = store.Db.Open())
            OrchestratorDb.Execute(connection, "DROP TABLE cli_marks; DROP TABLE session_states; " + DropStep7 + " PRAGMA user_version = 5;");

        using (var connection = new OrchestratorDb(path).Open())
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version;";
            Assert.That(command.ExecuteScalar(), Is.EqualTo((long)OrchestratorDb.Version));
            command.CommandText = "SELECT count(*) FROM sqlite_master WHERE name IN ('cli_marks', 'session_states');";
            Assert.That(command.ExecuteScalar(), Is.EqualTo(2L));
            command.CommandText = "SELECT count(*) FROM pragma_table_info('cli_marks') WHERE name = 'since';";
            Assert.That(command.ExecuteScalar(), Is.EqualTo(1L), "step 9 (M.ORCH-T558): when each CLI's mark was first written");
        }
        Assert.That(OrchestratorDb.Version, Is.EqualTo(16), "step 8 (M.ORCH-T528) adds session_states, step 9 (M.ORCH-T558) cli_marks.since, step 10 (M.ORCH-T563) the models, step 11 (M.ORCH-T670) the members, step 12 (M.ORCH-T668) assignments.account, step 13 (M.ORCH-T655) the organizations, step 14 (M.ORCH-T779) renames the Autopilot cli_marks kinds and settings to Allocate, step 15 (M.ORCH-T774) gate_events.account, step 16 (M.ORCH-T934) term_grants and term_audit");
        Assert.That(new JsonStore(dir, new OrchestratorDb(path)).LoadSettings().BlockBudget, Is.EqualTo(5555), "what it held is kept");
    }

    [Test]
    public void Step_14_renames_the_old_autopilot_cli_marks_and_settings_to_allocate_and_keeps_what_it_held()
    {
        var path = Path.Combine(dir, "v13.db");
        var store = new JsonStore(dir, new OrchestratorDb(path));
        store.SaveSettings(new OrchestratorSettings { BlockBudget = 7777, Allocate = false });
        // Back to how a version-13 database held these: the internal names before M.ORCH-T779 renamed Autopilot to Allocate,
        // and no gate_events.account (step 15, M.ORCH-T774) yet.
        using (var connection = store.Db.Open())
        {
            Sql.Run(connection, null, "UPDATE settings SET name = 'Autopilot' WHERE name = 'Allocate';");
            Sql.Run(connection, null, "INSERT INTO cli_marks (kind, pid, value, written_at) VALUES ('autopilot', 111, 'GTFO.Api', 0);");
            Sql.Run(connection, null, "INSERT INTO cli_marks (kind, pid, value, written_at) VALUES ('noautopilot', 222, 'wind', 0);");
            OrchestratorDb.Execute(connection, DropStep15 + "PRAGMA user_version = 13;");
        }

        using (var connection = new OrchestratorDb(path).Open())
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version;";
            Assert.That(command.ExecuteScalar(), Is.EqualTo((long)OrchestratorDb.Version));
            Assert.That(Sql.Scalar(connection, null, "SELECT value FROM cli_marks WHERE kind = 'allocate' AND pid = 111;"), Is.EqualTo("GTFO.Api"),
                "the old autopilot mark carries forward under its new kind, so a CLI with /allocate already on doesn't appear to flip");
            Assert.That(Sql.Scalar(connection, null, "SELECT value FROM cli_marks WHERE kind = 'noallocate' AND pid = 222;"), Is.EqualTo("wind"),
                "the old noautopilot mark carries forward too");
            Assert.That(Sql.Scalar(connection, null, "SELECT count(*) FROM cli_marks WHERE kind IN ('autopilot', 'noautopilot');"), Is.EqualTo(0L),
                "nothing is left under the old kind");
        }
        Assert.That(OrchestratorDb.Version, Is.GreaterThanOrEqualTo(14));
        Assert.That(new JsonStore(dir, new OrchestratorDb(path)).LoadSettings().Allocate, Is.False,
            "the old Autopilot setting carries forward as Allocate, not silently back to its default");
        Assert.That(new JsonStore(dir, new OrchestratorDb(path)).LoadSettings().BlockBudget, Is.EqualTo(7777), "what it held is kept");
    }

    // ---- leases ----

    [Test]
    public void A_lease_is_one_holders_at_a_time_and_free_once_it_lets_go()
    {
        using var first = new DispatchLease(dir);
        using var second = new DispatchLease(dir);
        using var other = new DispatchLease(dir, "limit-wake.lock");
        Assert.That((first.TryAcquire(), second.TryAcquire(), other.TryAcquire()), Is.EqualTo((true, false, true)), "another name is a lease of its own");
        Assert.That((first.Name, HolderPid(first)), Is.EqualTo(("dispatch", (long?)Environment.ProcessId)));
        Assert.That(first.TryAcquire(), Is.True, "asked again, still its");
        Assert.That(Db.Dump(), Does.Not.Contain("leases"), "leases aren't history: not in the dump");
        first.Dispose();
        Assert.That(HolderPid(second), Is.Null, "the row goes with it");
        Assert.That((second.TryAcquire(), second.IsHeld), Is.EqualTo((true, true)));
    }

    [Test]
    public void A_holder_that_exited_or_whose_pid_was_reused_holds_nothing()
    {
        using var exited = Process.Start(new ProcessStartInfo("cmd", "/c exit") { UseShellExecute = false, CreateNoWindow = true })!;
        exited.WaitForExit();
        SetHolder("dispatch", exited.Id, null);
        using var lease = new DispatchLease(dir);
        Assert.That(lease.TryAcquire(), Is.True, "its holder crashed: taken over");

        lease.Dispose();
        // A live process, but not the one that took it: started at another time.
        using var live = Sleeper();
        using var again = new DispatchLease(dir);
        try
        {
            SetHolder("dispatch", live.Id, 1.0);
            Assert.That(again.TryAcquire(), Is.True, "a reused pid isn't the holder");
        }
        finally { live.Kill(); }

        // This process's own pid from an earlier run (a database carried over a restart).
        again.Dispose();
        SetHolder("dispatch", Environment.ProcessId, 1.0);
        using var mine = new DispatchLease(dir);
        Assert.That(mine.TryAcquire(), Is.True);
    }

    [Test]
    public void A_holder_whose_row_another_live_process_took_finds_it_lost()
    {
        using var lease = new DispatchLease(dir);
        Assert.That(lease.TryAcquire(), Is.True);
        // A database put back from git, say: another live process is named now.
        using var live = Sleeper();
        try
        {
            SetHolder("dispatch", live.Id, (live.StartTime.ToUniversalTime() - DateTime.UnixEpoch).TotalSeconds);
            Assert.That((lease.TryAcquire(), lease.IsHeld), Is.EqualTo((false, false)));
        }
        finally { live.Kill(); }
    }

    [Test]
    public void Many_asking_at_once_give_it_to_exactly_one()
    {
        var leases = Enumerable.Range(0, 12).Select(_ => new DispatchLease(dir)).ToList();
        try
        {
            using var go = new ManualResetEventSlim();
            var tasks = leases.Select(l => Task.Run(() => { go.Wait(); return l.TryAcquire(); })).ToList();
            go.Set();
            Task.WaitAll([.. tasks]);
            Assert.That(tasks.Count(t => t.Result), Is.EqualTo(1));
        }
        finally { leases.ForEach(l => l.Dispose()); }
    }

    [Test]
    public void A_lock_file_an_old_process_still_holds_keeps_the_lease_from_being_taken_and_goes_once_let_go()
    {
        using var lease = new DispatchLease(dir);
        using (new FileStream(lease.Path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
            Assert.That(lease.TryAcquire(), Is.False, "a daemon from before the move still feeds the CLIs");
        Assert.That(lease.TryAcquire(), Is.True);
        Assert.That(File.Exists(lease.Path), Is.False, "the old lock file is gone");
    }

    [Test]
    public void While_the_database_is_held_a_holder_keeps_its_lease_and_nobody_else_takes_it()
    {
        using var holder = new DispatchLease(dir);
        using var other = new DispatchLease(dir, "reminders.lock");
        Assert.That(holder.TryAcquire(), Is.True);
        OrchestratorDb.BusyTimeoutMs = 100;
        using (var connection = Db.Open())
        {
            OrchestratorDb.Execute(connection, "BEGIN EXCLUSIVE;");
            Assert.That((holder.TryAcquire(), other.TryAcquire()), Is.EqualTo((true, false)));
            OrchestratorDb.Execute(connection, "ROLLBACK;");
        }
        Assert.That(other.TryAcquire(), Is.True);
    }

    // ---- the rl-status row ----

    private static BudgetStatus Budgeted() =>
        BudgetCalculator.Compute([new UsageEvent(DateTime.UtcNow.AddMinutes(-30), new TokenUsage(0, 0, 1_000_000, 400))],
            new OrchestratorSettings { BlockBudget = 1000 }, DateTime.UtcNow, DateTime.UtcNow.Date);

    private static Pace Held(double delay) => new(PaceMode.Throttled, 2, 2000, 3000, 1000, "5-hour", DateTime.UtcNow.AddHours(2), "Throttled.", "Conserve.")
        { AgentDelaySeconds = delay };

    [Test]
    public void The_status_is_written_as_a_row_and_as_the_file_outside_readers_use_and_withdrawn_from_both()
    {
        var file = Path.Combine(dir, "rl-status.json");
        var writer = new RateLimitStatusWriter(file) { Db = Db };
        writer.Write(Budgeted(), Held(4));

        var row = Db.LoadState(RateLimitStatusWriter.StateName)!.Value.Value;
        Assert.That(row, Is.EqualTo(File.ReadAllText(file)), "the same status both ways");
        Assert.That(JsonDocument.Parse(row).RootElement.GetProperty("pace_delay_seconds").GetDouble(), Is.EqualTo(4));
        Assert.That(Db.Dump(), Does.Not.Contain("rl-status"), "rewritten every tick: not in the dump");

        writer.Write(BudgetCalculator.Compute([], new OrchestratorSettings(), DateTime.UtcNow, DateTime.UtcNow.Date));
        Assert.That(Db.LoadState(RateLimitStatusWriter.StateName), Is.Null, "no limit to report: the row withdrawn");
        Assert.That(File.Exists(file), Is.False, "and the file");
    }

    [Test]
    public void The_orchestrators_own_read_takes_the_newer_of_the_row_and_the_file()
    {
        var file = Path.Combine(dir, "rl-status.json");
        var writer = new RateLimitStatusWriter(file) { Db = Db };
        Db.SaveState(RateLimitStatusWriter.StateName, "{\"source\":\"MindAttic.Orchestrator\",\"timestamp\":\"2026-10-20T10:00:05Z\",\"pace_delay_seconds\":9}");
        File.WriteAllText(file, "{\"source\":\"MindAttic.Orchestrator\",\"timestamp\":\"2026-10-20T10:00:00Z\",\"pace_delay_seconds\":1}");
        Assert.That(writer.ReadLatest(), Does.Contain("\"pace_delay_seconds\":9"), "the row is newer");
        File.WriteAllText(file, "{\"source\":\"MindAttic.Orchestrator\",\"timestamp\":\"2026-10-20T10:00:09Z\",\"pace_delay_seconds\":1}");
        Assert.That(writer.ReadLatest(), Does.Contain("\"pace_delay_seconds\":1"), "a daemon from before the move wrote the file later");
        Assert.That(new RateLimitStatusWriter(file).ReadLatest(), Does.Contain("10:00:09"), "a writer with no database reads the file");
    }

    // ---- gate_events ----

    [Test]
    public void A_gate_log_whose_rows_went_in_but_whose_bak_append_failed_is_not_read_in_again()
    {
        // M.ORCH-T537: the .importing file outlived its committed rows and was inserted again every tick.
        var log = Path.Combine(dir, "pace-gate.jsonl");
        File.WriteAllText(log, "{\"at\": \"2026-10-15T20:00:00Z\", \"decision\": \"held\", \"held\": 30, \"tool\": \"Bash\"}\n");
        Directory.CreateDirectory(log + ".bak"); // the append fails
        Assert.Catch(() => Db.ImportGateLog(log));
        Assert.That(File.Exists(log + ".importing"), Is.True);
        Assert.Catch(() => Db.ImportGateLog(log));
        Directory.Delete(log + ".bak");
        Db.ImportGateLog(log);
        var since = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);
        Assert.That(Db.LoadGateEvents(since), Has.Count.EqualTo(1), "read in once");
        Assert.That((File.Exists(log + ".importing"), File.ReadAllLines(log + ".bak").Length), Is.EqualTo((false, 1)));
        Assert.That(Db.Dump(), Does.Not.Contain(OrchestratorHistory.GateLogImportedName), "its mark isn't history worth a commit");
    }

    [Test]
    public void The_old_event_log_is_read_in_as_rows_kept_as_bak_and_read_in_again_whenever_a_hook_fell_back_to_it()
    {
        var log = Path.Combine(dir, "pace-gate.jsonl");
        File.WriteAllLines(log,
        [
            "{\"at\": \"2026-10-15T20:00:00Z\", \"decision\": \"held\", \"held\": 30.5, \"tool\": \"Bash\", \"percent\": 88.2}",
            "{\"at\": \"2026-10-15T20:01:00Z\", \"decision\": \"refu",
            "{\"at\": \"2026-10-15T20:02:00Z\", \"decision\": \"prompt-answered\", \"held\": 0, \"tool\": null, \"percent\": null}",
        ]);
        Db.ImportGateLog(log);
        var since = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);
        Assert.That(Db.LoadGateEvents(since), Is.EqualTo(new[]
        {
            new GateEvent(since.AddHours(20), GateEvent.Held, 30.5, "Bash"),
            new GateEvent(since.AddHours(20).AddMinutes(2), GateEvent.PromptAnswered, 0, null),
        }), "a torn line is skipped");
        Assert.That((File.Exists(log), File.ReadAllLines(log + ".bak").Length), Is.EqualTo((false, 3)));

        Db.ImportGateLog(log);
        Assert.That(Db.LoadGateEvents(since), Has.Count.EqualTo(2), "read in once");

        File.WriteAllText(log, "{\"at\": \"2026-10-15T21:00:00Z\", \"decision\": \"refused\", \"held\": 300, \"tool\": \"Edit\"}\n");
        Db.ImportGateLog(log);
        Assert.That(Db.LoadGateEvents(since).Last(), Is.EqualTo(new GateEvent(since.AddHours(21), GateEvent.Refused, 300, "Edit")));
        Assert.That(File.ReadAllLines(log + ".bak"), Has.Length.EqualTo(4), "appended to the .bak");

        // A rename whose rows couldn't go in (the database held) is read in first the next time.
        File.WriteAllText(log + ".importing", "{\"at\": \"2026-10-15T22:00:00Z\", \"decision\": \"exempt\", \"held\": 0, \"tool\": \"Skill\"}\n");
        Db.ImportGateLog(log);
        Assert.That(Db.LoadGateEvents(since.AddHours(21.5)).Single().Decision, Is.EqualTo(GateEvent.Exempt));
        Assert.That(File.Exists(log + ".importing"), Is.False);

        Db.TrimGateEvents(since.AddHours(21));
        Assert.That(Db.LoadGateEvents(since).Select(e => e.AtUtc.Hour), Is.EqualTo(new[] { 21, 22 }));
        Assert.That(Db.Dump(), Does.Not.Contain("gate_events"), "not in the dump");
    }

    [Test]
    public void Gate_events_written_from_many_threads_at_once_are_all_kept()
    {
        var at = new DateTime(2026, 10, 15, 20, 0, 0, DateTimeKind.Utc);
        Parallel.For(0, 16, i => Db.AddGateEvents([(new GateEvent(at.AddSeconds(i), GateEvent.Held, i, "Bash"), null)]));
        Assert.That(Db.LoadGateEvents(at).Select(e => e.HeldSeconds), Is.EquivalentTo(Enumerable.Range(0, 16).Select(i => (double)i)));
    }
}
