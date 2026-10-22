using System.Diagnostics;
using System.Text.Json.Nodes;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Setup;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The hook-owned marks in data/orchestrator.db (M.ORCH-T528): the stop latch and the key set-asides are state rows
/// the hooks write through stdlib sqlite3, each old file read in once (kept as .bak), writers at once never dropping each
/// other's entry, a hook reading mid-write, and the file again while the database is held or older. Each test has its own
/// scratch database (<see cref="OrchestratorDbPerTestAttribute"/>).</summary>
public sealed class HookMarkTests
{
    private string dir = "";
    private string Marks => Path.Combine(dir, ".claude", "session-context");
    private string Latch => Path.Combine(Marks, AgentGate.StopLatchName);
    private string SetAside => Path.Combine(Marks, AgentGate.ApiKeySetAsideName);
    private static OrchestratorDb Db => new(OrchestratorDb.DefaultPath());

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "orch-marks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Marks);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(dir, recursive: true);

    private Process Start(string body)
    {
        var script = Path.Combine(dir, "hook-" + Guid.NewGuid().ToString("N") + ".py");
        File.WriteAllText(script, "import json, os, re, sys, time\nfrom datetime import datetime, timezone\nfrom pathlib import Path\n" +
            AgentGate.WorkspacePython + "\n" + AgentGate.ClaudeUsagePython + "\n" + body + "\n");
        var psi = new ProcessStartInfo("python") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        psi.ArgumentList.Add("-I");
        psi.ArgumentList.Add(script);
        psi.Environment["USERPROFILE"] = dir;
        psi.Environment["APPDATA"] = dir;
        return Process.Start(psi)!;
    }

    private string Hook(string body)
    {
        using var p = Start(body);
        var output = p.StandardOutput.ReadToEndAsync();
        var error = p.StandardError.ReadToEnd();
        Assert.That(p.WaitForExit(30_000), Is.True);
        Assert.That(p.ExitCode, Is.EqualTo(0), error);
        return output.Result.Trim();
    }

    private static JsonObject? Row(string name) =>
        Db.LoadState(name) is { } s ? JsonNode.Parse(s.Value)!.AsObject() : null;

    private const string LatchClaude = """latch("claude", time.time() + 600, percent=99.0)""";
    private const string PrintLatched = """print(json.dumps(latched("claude")))""";
    private static string Add(string name, string file, string key) =>
        $$"""change_hook_mark("{{name}}", Path.home() / ".claude" / "session-context" / "{{file}}", lambda m: dict(m, {{key}}=1))""";

    [Test]
    public void The_stop_latch_file_is_read_in_once_into_its_state_row_and_kept_as_bak()
    {
        Db.Open().Dispose();
        var until = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        File.WriteAllText(Latch, "{\"claude-week@\": {\"until\": " + until + ", \"percent\": 99.5, \"account\": \"\", \"epoch\": 0}}");

        Assert.That(Hook(PrintLatched), Is.EqualTo("null"), "nothing latched for the 5 hours yet");
        Assert.That(File.Exists(Latch), Is.True, "a read leaves the file: only a write reads it in");
        Hook(LatchClaude);

        var row = Row(AgentGate.StopLatchStateName);
        Assert.That(row, Is.Not.Null);
        Assert.That(row!.ContainsKey("claude@") && row.ContainsKey("claude-week@"), Is.True, "the new latch and the file's, in one row");
        Assert.That(File.Exists(Latch), Is.False);
        Assert.That(File.Exists(Latch + ".bak"), Is.True, "the old file is kept as .bak");
        Assert.That(Hook("""print(latched("claude-week")["percent"])"""), Is.EqualTo("99.5"), "read back from the row");

        Hook("""latch("claude", time.time() + 600, percent=99.9)""");
        Assert.That(File.Exists(Latch), Is.False, "never written again while the database takes it");
        Assert.That(Db.Dump(), Does.Not.Contain(AgentGate.StopLatchStateName), "a hook's mark, not history: out of the dump");
    }

    [Test]
    public void The_key_set_aside_file_is_read_in_once_and_still_read_by_the_hooks()
    {
        Db.Open().Dispose();
        File.WriteAllText(SetAside, """{"old": 1}""");
        Hook(Add(AgentGate.ApiKeySetAsideStateName, AgentGate.ApiKeySetAsideName, "new"));

        var row = Row(AgentGate.ApiKeySetAsideStateName)!;
        Assert.That(row.Select(kv => kv.Key), Is.EquivalentTo(new[] { "old", "new" }));
        Assert.That(File.Exists(SetAside), Is.False);
        Assert.That(File.Exists(SetAside + ".bak"), Is.True);

        // The orchestrator's billing switch still writes the file: the hooks read it over the row, and the next write reads it in.
        File.WriteAllText(SetAside, """{"switch": 2}""");
        Assert.That(Hook($"""print(sorted(hook_mark("{AgentGate.ApiKeySetAsideStateName}", Path.home() / ".claude" / "session-context" / "{AgentGate.ApiKeySetAsideName}")))"""),
            Is.EqualTo("['new', 'old', 'switch']"));
        Hook(Add(AgentGate.ApiKeySetAsideStateName, AgentGate.ApiKeySetAsideName, "more"));
        Assert.That(Row(AgentGate.ApiKeySetAsideStateName)!.Select(kv => kv.Key), Is.EquivalentTo(new[] { "old", "new", "switch", "more" }));
        Assert.That(Db.Dump(), Does.Not.Contain(AgentGate.ApiKeySetAsideStateName));
    }

    [Test]
    public void Hooks_writing_at_once_each_add_their_entry_in_one_write_transaction()
    {
        Db.Open().Dispose();
        File.WriteAllText(SetAside, """{"old": 1}""");
        var writers = Enumerable.Range(0, 8).Select(i => Start(Add(AgentGate.ApiKeySetAsideStateName, AgentGate.ApiKeySetAsideName, "k" + i))).ToList();
        foreach (var p in writers)
        {
            Assert.That(p.WaitForExit(30_000), Is.True);
            Assert.That(p.ExitCode, Is.EqualTo(0), p.StandardError.ReadToEnd());
            p.Dispose();
        }
        var keys = Row(AgentGate.ApiKeySetAsideStateName)!.Select(kv => kv.Key).ToList();
        var file = File.Exists(SetAside) ? JsonNode.Parse(File.ReadAllText(SetAside))!.AsObject().Select(kv => kv.Key) : [];
        Assert.That(keys.Union(file), Is.SupersetOf(Enumerable.Range(0, 8).Select(i => "k" + i).Append("old")), "no writer saved over another");
        Assert.That(keys, Has.Count.EqualTo(9), "with the database free none fell back to the file");
    }

    [Test]
    public void A_hook_reads_the_committed_latch_mid_write_and_a_write_while_held_goes_to_the_file_then_is_read_in()
    {
        Db.Open().Dispose();
        Hook(LatchClaude);
        using (var connection = Db.Open())
        {
            // Another process mid-write on the same row: the hook reads what was committed, at once; its own write can't wait.
            OrchestratorDb.Execute(connection, "BEGIN IMMEDIATE; UPDATE state SET value = '{}' WHERE name = 'stop-latch';");
            var clock = Stopwatch.StartNew();
            Assert.That(Hook(PrintLatched), Does.Contain("\"percent\": 99.0"), "the committed latch, not the half-written one");
            Hook("""latch("claude-week", time.time() + 600, percent=99.0)""");
            Assert.That(clock.Elapsed, Is.LessThan(TimeSpan.FromSeconds(15)), "not stuck on the database");
            OrchestratorDb.Execute(connection, "ROLLBACK;");
        }
        Assert.That(File.Exists(Latch), Is.True, "held past the short wait: the latch is kept as the file");
        Assert.That(Row(AgentGate.StopLatchStateName)!.ContainsKey("claude-week@"), Is.False);
        Assert.That(Hook("""print(latched("claude-week")["percent"])"""), Is.EqualTo("99.0"), "a hook reads the file's latch too");

        Hook("""latch("orchestrator", time.time() + 600, note="stop")""");
        var row = Row(AgentGate.StopLatchStateName)!;
        Assert.That(row.Select(kv => kv.Key), Is.EquivalentTo(new[] { "claude@", "claude-week@", "orchestrator@" }), "the next write reads the file in");
        Assert.That(File.Exists(Latch), Is.False);
    }

    /// <summary>Python wrapping state_tx so that, as its COMMIT goes in, <paramref name="file"/> is rewritten with
    /// <paramref name="json"/>: another hook, its own write held, keeping its mark as the file in between.</summary>
    private static string WrittenAtCommit(string file, string json) => $$"""
        _orig_tx = state_tx
        class _Racing:
            def __init__(self, db):
                self.db = db
            def execute(self, sql, *args):
                result = self.db.execute(sql, *args)
                if sql == "COMMIT":
                    (Path.home() / ".claude" / "session-context" / "{{file}}").write_text('{{json}}', encoding="utf-8")
                return result
            def close(self):
                self.db.close()
        def state_tx(timeout=2, table="state"):
            db = _orig_tx(timeout, table)
            return _Racing(db) if db is not None else None

        """;

    [Test]
    public void A_mark_file_written_after_it_was_read_in_is_not_moved_aside()
    {
        // M.ORCH-T544: the file was moved to .bak after the commit whatever it held by then, so a mark another hook kept as
        // the file between the read and the move was lost.
        Db.Open().Dispose();
        File.WriteAllText(SetAside, """{"old": 1}""");
        Hook(WrittenAtCommit(AgentGate.ApiKeySetAsideName, """{"old": 1, "late": 2}""") + Add(AgentGate.ApiKeySetAsideStateName, AgentGate.ApiKeySetAsideName, "new"));
        Assert.That(File.Exists(SetAside), Is.True, "the later write is still the file");
        Assert.That(Hook($"""print(sorted(hook_mark("{AgentGate.ApiKeySetAsideStateName}", Path.home() / ".claude" / "session-context" / "{AgentGate.ApiKeySetAsideName}")))"""),
            Is.EqualTo("['late', 'new', 'old']"));
    }

    [Test]
    public void Without_a_database_or_with_one_from_before_the_state_table_the_marks_stay_files()
    {
        Hook(LatchClaude);
        Assert.That(File.Exists(Latch), Is.True, "no database: the file, as before");
        Assert.That(File.Exists(OrchestratorDb.DefaultPath()), Is.False, "a hook never creates the database");

        File.Delete(Latch);
        var path = OrchestratorDb.DefaultPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var old = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Pooling=False"))
        {
            old.Open();
            OrchestratorDb.Execute(old, "CREATE TABLE settings (name TEXT PRIMARY KEY, value TEXT NOT NULL); PRAGMA user_version = 2;");
        }
        Hook(LatchClaude);
        Assert.That(File.Exists(Latch), Is.True, "an older database: the file");
        Assert.That(Hook(PrintLatched), Does.Contain("99.0"));
    }

    // ---- the per-CLI /allocate, /allocate off and /crossrepo marks (schema step 6) ----

    private static Dictionary<string, string> CliRows()
    {
        using var connection = Db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT kind, pid, value FROM cli_marks;";
        using var r = command.ExecuteReader();
        var rows = new Dictionary<string, string>();
        while (r.Read()) rows[$"{r.GetString(0)}-{r.GetInt64(1)}"] = r.GetString(2);
        return rows;
    }

    private void MarkFile(string name, string text = "") => File.WriteAllText(Path.Combine(Marks, name), text);

    [Test]
    public void The_daemon_reads_the_cli_mark_files_in_once_keeping_each_as_bak_and_a_cleared_file_removes_its_row()
    {
        Db.Open().Dispose();
        Hook("""set_cli_mark("crossrepo", 14, "Old")""");
        MarkFile("autopilot-11", "UnitySolarSystem,Prose");
        MarkFile("noautopilot-12");
        MarkFile("crossrepo-13", "all");
        MarkFile("crossrepo-14.cleared");
        Assert.That(Sessions.CliMarks.Read(Marks, Sessions.CliMarks.Crossrepo).Keys, Is.EquivalentTo(new[] { 13 }), "the cleared file hides the row before it's read in");

        Sessions.CliMarks.ReadIn(Marks);
        Assert.That(CliRows(), Is.EquivalentTo(new Dictionary<string, string> { ["autopilot-11"] = "UnitySolarSystem,Prose", ["noautopilot-12"] = "", ["crossrepo-13"] = "all" }));
        Assert.That(Directory.GetFiles(Marks).Select(Path.GetFileName), Is.EquivalentTo(new[] { "autopilot-11.bak", "noautopilot-12.bak", "crossrepo-13.bak" }));
        Assert.That(Hook("""print(cli_mark("autopilot", 11), sorted(cli_marks("noautopilot")), cli_mark("crossrepo", 14))"""), Is.EqualTo("UnitySolarSystem,Prose ['12'] None"),
            "the hooks read the rows, never a .bak");
        Sessions.CliMarks.ReadIn(Marks);
        Assert.That(CliRows(), Has.Count.EqualTo(3), "once: nothing left to read in");
        Assert.That(Db.Dump(), Does.Not.Contain("cli_marks"), "they last as long as a CLI: out of the dump");
    }

    [Test]
    public void Hooks_setting_cli_marks_at_once_each_keep_their_row_and_the_orchestrator_reads_them()
    {
        Db.Open().Dispose();
        var writers = Enumerable.Range(1, 8).Select(i => Start($"""set_cli_mark("autopilot", {i}, "R{i}")""")).ToList();
        foreach (var p in writers)
        {
            Assert.That(p.WaitForExit(30_000), Is.True);
            Assert.That(p.ExitCode, Is.EqualTo(0), p.StandardError.ReadToEnd());
            p.Dispose();
        }
        Assert.That(CliRows(), Has.Count.EqualTo(8), "concurrent writers: no row lost");
        Assert.That(Directory.GetFiles(Marks), Is.Empty, "no files while the database takes them");
        Assert.That(Sessions.CliMarks.Read(Marks, Sessions.CliMarks.Autopilot)[3], Is.EqualTo("R3"));

        Hook("""set_cli_mark("autopilot", 3, None)""");
        Assert.That(Sessions.CliMarks.Read(Marks, Sessions.CliMarks.Autopilot).ContainsKey(3), Is.False);
        Sessions.CliMarks.Prune(pid => pid % 2 == 0);
        Assert.That(CliRows().Keys, Is.EquivalentTo(new[] { "autopilot-2", "autopilot-4", "autopilot-6", "autopilot-8" }), "a closed CLI's rows go");
    }

    [Test]
    public void A_hook_reads_committed_cli_marks_mid_write_and_its_writes_while_held_are_files_the_daemon_reads_in()
    {
        Db.Open().Dispose();
        Hook("""set_cli_mark("autopilot", 5, "A"); set_cli_mark("crossrepo", 5, "B")""");
        using (var connection = Db.Open())
        {
            OrchestratorDb.Execute(connection, "BEGIN IMMEDIATE; UPDATE cli_marks SET value = 'half';");
            Assert.That(Hook("""print(cli_mark("autopilot", 5))"""), Is.EqualTo("A"), "the committed row, not the half-written one");
            Hook("""set_cli_mark("autopilot", 5, "C"); set_cli_mark("crossrepo", 5, None)""");
            OrchestratorDb.Execute(connection, "ROLLBACK;");
        }
        Assert.That(File.ReadAllText(Path.Combine(Marks, "autopilot-5")), Is.EqualTo("C"), "held: the file");
        Assert.That(File.Exists(Path.Combine(Marks, "crossrepo-5.cleared")), Is.True, "held: a removal leaves .cleared");
        Assert.That(Hook("""print(cli_mark("autopilot", 5), cli_mark("crossrepo", 5))"""), Is.EqualTo("C None"), "every reader takes the files over the rows");
        Assert.That(Sessions.CliMarks.Read(Marks, Sessions.CliMarks.Crossrepo).ContainsKey(5), Is.False);

        Sessions.CliMarks.ReadIn(Marks);
        Assert.That(CliRows(), Is.EquivalentTo(new Dictionary<string, string> { ["autopilot-5"] = "C" }));
        Assert.That(File.Exists(Path.Combine(Marks, "crossrepo-5.cleared")), Is.False);
    }

    private string Finish(Process p)
    {
        var output = p.StandardOutput.ReadToEndAsync();
        var error = p.StandardError.ReadToEnd();
        Assert.That(p.WaitForExit(30_000), Is.True);
        Assert.That(p.ExitCode, Is.EqualTo(0), error);
        return output.Result.Trim();
    }

    [Test]
    public void A_fresh_hook_waits_out_a_database_held_a_few_seconds_rather_than_reading_its_marks_as_missing()
    {
        // M.ORCH-T542: a hook process has no copy of its own to stand in, so a short wait that runs out read every mark as
        // missing: /allocate -repo=C replaced A,B with C.
        Db.Open().Dispose();
        Hook("""set_cli_mark("autopilot", 5, "A,B")""");
        string output;
        // The hook signals once its own start-up reads are done, so the write below holds it at the marks.
        using var p = Start(AgentGate.MarksPython + "\n" +
            "print('ready', flush=True)\nwhile not (Path.home() / 'go').exists():\n    time.sleep(0.05)\n" +
            """print(set_autopilot(5, "-repo=C", ""), cli_mark("autopilot", 5))""");
        Assert.That(p.StandardOutput.ReadLine(), Is.EqualTo("ready"));
        using (var connection = Db.Open())
        {
            OrchestratorDb.Execute(connection, "BEGIN EXCLUSIVE;");
            File.WriteAllText(Path.Combine(dir, "go"), "");
            Thread.Sleep(3_000);
            OrchestratorDb.Execute(connection, "ROLLBACK;");
            output = Finish(p);
        }
        Assert.That(output, Is.EqualTo("None A,B,C"));
        Assert.That(CliRows()["autopilot-5"], Is.EqualTo("A,B,C"));
    }

    [Test]
    public void A_hook_that_cant_read_a_mark_writes_nothing_made_from_it()
    {
        Db.Open().Dispose();
        Hook("""set_cli_mark("autopilot", 5, "A,B")""");
        string output;
        using (var connection = Db.Open())
        {
            OrchestratorDb.Execute(connection, "BEGIN EXCLUSIVE;");
            output = Hook(AgentGate.MarksPython + "\n" + """print(set_autopilot(5, "-repo=C", ""))""");
            OrchestratorDb.Execute(connection, "ROLLBACK;");
        }
        Assert.That(output, Does.Contain("Nothing changed"), "held past every wait: refused, not guessed");
        Assert.That(File.Exists(Path.Combine(Marks, "autopilot-5")), Is.False, "no file saying C alone");
        Assert.That(CliRows()["autopilot-5"], Is.EqualTo("A,B"));
    }

    [Test]
    public void A_cli_mark_file_held_open_while_the_database_is_held_is_reported_not_a_crash()
    {
        // M.ORCH-T545: the fallback's write_text raised PermissionError up through the context gate.
        Db.Open().Dispose();
        var file = Path.Combine(Marks, "autopilot-5");
        File.WriteAllText(file, "A");
        string output;
        using (var connection = Db.Open())
        using (new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            OrchestratorDb.Execute(connection, "BEGIN IMMEDIATE;");
            output = Hook(AgentGate.MarksPython + "\n" + """print(set_cli_mark("crossrepo", 5, None) is not False, set_autopilot(5, "-repo=B", ""))""");
            OrchestratorDb.Execute(connection, "ROLLBACK;");
        }
        Assert.That(output, Does.StartWith("True ").And.Contain("couldn't be recorded"));
        Assert.That(File.ReadAllText(file), Is.EqualTo("A"));
    }

    [Test]
    public void Without_a_database_the_cli_marks_stay_files_as_before()
    {
        Hook("""set_cli_mark("autopilot", 9, "X"); set_cli_mark("noautopilot", 9, ""); set_cli_mark("crossrepo", 9, "Y"); set_cli_mark("crossrepo", 9, None)""");
        Assert.That(Directory.GetFiles(Marks).Select(Path.GetFileName), Is.EquivalentTo(new[] { "autopilot-9", "noautopilot-9" }), "no .cleared: there's no row to hide");
        Sessions.CliMarks.ReadIn(Marks);
        Assert.That(File.Exists(OrchestratorDb.DefaultPath()), Is.False, "neither a hook nor a read-in creates the database");
        Assert.That(Sessions.CliMarks.Read(Marks, Sessions.CliMarks.Autopilot)[9], Is.EqualTo("X"));
    }

    // ---- the billing mode and the account record: rows, still exported as their files ----

    private string BillingFile => Path.Combine(Marks, Billing.BillingModeFile.FileName);
    private Billing.BillingModeFile ModeFile => new(BillingFile);
    private const string HookBilling = "BILLING = Path.home() / \".claude\" / \"session-context\" / \"billing.json\"\n";

    [Test]
    public void The_billing_file_is_read_in_once_kept_as_bak_and_still_exported_with_every_write()
    {
        Db.Open().Dispose();
        File.WriteAllText(BillingFile, """{"mode": "api", "since": 1000, "login": "a", "by": "dashboard"}""");
        Assert.That(ModeFile.Read()!.Mode, Is.EqualTo("api"));
        Assert.That(Row(Billing.BillingModeFile.StateName)!["mode"]!.GetValue<string>(), Is.EqualTo("api"), "read in");
        Assert.That(File.Exists(BillingFile + ".bak"), Is.True, "a copy kept as .bak");
        Assert.That(File.Exists(BillingFile), Is.True, "still exported for the Launcher");

        ModeFile.Write(new Billing.BillingState("team", DateTime.UtcNow, "b", "test"));
        Assert.That(Row(Billing.BillingModeFile.StateName)!["mode"]!.GetValue<string>(), Is.EqualTo("team"));
        Assert.That(JsonNode.Parse(File.ReadAllText(BillingFile))!["mode"]!.GetValue<string>(), Is.EqualTo("team"), "the export follows");
        Assert.That(Hook(HookBilling + """print(exported_mark("billing", BILLING)["mode"])"""), Is.EqualTo("team"));

        // The context gate switching back on a /login writes both too.
        Hook(HookBilling + """write_exported_mark("billing", BILLING, {"mode": "api", "since": time.time(), "by": "login"})""");
        Assert.That(Row(Billing.BillingModeFile.StateName)!["by"]!.GetValue<string>(), Is.EqualTo("login"));
        Assert.That(ModeFile.Read()!.By, Is.EqualTo("login"));
        Assert.That(Db.Dump(), Does.Not.Contain("\"login\""), "out of the dump");
    }

    [Test]
    public void A_billing_write_while_the_database_is_held_is_the_file_alone_and_reads_as_the_newer()
    {
        Db.Open().Dispose();
        ModeFile.Write(new Billing.BillingState("team", DateTime.UtcNow, "b", "test"));
        using (var connection = Db.Open())
        {
            OrchestratorDb.Execute(connection, "BEGIN IMMEDIATE; UPDATE state SET value = '{}' WHERE name = 'billing';");
            Assert.That(Hook(HookBilling + """print(exported_mark("billing", BILLING)["mode"])"""), Is.EqualTo("team"), "read mid-write: the committed mode");
            var clock = Stopwatch.StartNew();
            Hook(HookBilling + """write_exported_mark("billing", BILLING, {"mode": "api", "since": time.time(), "by": "held"})""");
            Assert.That(clock.Elapsed, Is.LessThan(TimeSpan.FromSeconds(15)));
            OrchestratorDb.Execute(connection, "ROLLBACK;");
        }
        Assert.That(Row(Billing.BillingModeFile.StateName)!["mode"]!.GetValue<string>(), Is.EqualTo("team"), "the row couldn't take it");
        Assert.That(ModeFile.Read()!.By, Is.EqualTo("held"), "the file, written later, is the mode");
        Assert.That(Hook(HookBilling + """print(exported_mark("billing", BILLING)["by"])"""), Is.EqualTo("held"));
    }

    [Test]
    public void The_account_record_is_a_row_the_hooks_and_the_orchestrator_share_and_its_file_is_still_written()
    {
        Db.Open().Dispose();
        File.WriteAllText(Path.Combine(dir, ".claude.json"), """{"oauthAccount": {"accountUuid": "A", "organizationUuid": "O"}}""");
        var account = new Budget.ClaudeAccount { ConfigPath = Path.Combine(dir, ".claude.json"), StatePath = Path.Combine(Marks, Budget.ClaudeAccount.FileName) };
        Assert.That(account.Read(DateTime.UtcNow)!.Account, Is.EqualTo("A/O"));
        Assert.That(Row(Budget.ClaudeAccount.StateName)!["account"]!.GetValue<string>(), Is.EqualTo("A/O"));
        Assert.That(File.Exists(account.StatePath), Is.True, "exported for the statusline");

        // A /login to B, first seen by a hook: it dates the switch in the row and the file, and the orchestrator reads it.
        File.WriteAllText(Path.Combine(dir, ".claude.json"), """{"oauthAccount": {"accountUuid": "B", "organizationUuid": "O"}}""");
        Assert.That(Hook("""print(account()[0])"""), Is.EqualTo("B/O"));
        Assert.That(Row(Budget.ClaudeAccount.StateName)!["account"]!.GetValue<string>(), Is.EqualTo("B/O"));
        Assert.That(new Budget.ClaudeAccount { ConfigPath = account.ConfigPath, StatePath = account.StatePath }.Read(DateTime.UtcNow)!.History, Has.Count.EqualTo(2),
            "the switch the hook dated is the orchestrator's too");
        Assert.That(Db.Dump(), Does.Not.Contain("B/O"), "out of the dump");
    }

    // ---- each session's state (schema step 7), still exported as <session>.state.json ----

    private string StateFile(string session) => Path.Combine(Marks, session + Sessions.SessionStates.Suffix);
    private static string HookState(string body) =>
        "def load(path):\n    value = exported_mark(path.name[:-len(\".state.json\")], path, table=\"session_states\", key=\"session_id\")\n    return value if isinstance(value, dict) else {}\n" +
        "def save(path, value):\n    write_exported_mark(path.name[:-len(\".state.json\")], path, value, table=\"session_states\", key=\"session_id\")\n" +
        "DIR = Path.home() / \".claude\" / \"session-context\"\n" + body;

    [Test]
    public void A_session_state_is_a_row_and_its_file_is_still_exported_for_the_Launcher()
    {
        Db.Open().Dispose();
        Hook(HookState("""save(DIR / "s1.state.json", {"pid": 42, "idle": False})"""));
        var row = Sessions.SessionStates.Rows()["s1"];
        Assert.That(JsonNode.Parse(row.Value)!["pid"]!.GetValue<int>(), Is.EqualTo(42));
        Assert.That(JsonNode.Parse(File.ReadAllText(StateFile("s1")))!["pid"]!.GetValue<int>(), Is.EqualTo(42), "exported");
        Assert.That(new Sessions.ContextCycler(Marks).Load().Single().Pid, Is.EqualTo(42), "the orchestrator's readers read it as before");
        Assert.That(Hook(HookState("""print(load(DIR / "s1.state.json")["pid"])""")), Is.EqualTo("42"));

        Sessions.SessionStates.ReadIn(Marks);
        Assert.That(File.Exists(StateFile("s1") + ".bak"), Is.False, "the gate's own export isn't read in again");
        Assert.That(Db.Dump(), Does.Not.Contain("s1"), "out of the dump");
    }

    [Test]
    public void The_daemon_reads_in_a_state_file_once_and_one_written_while_the_database_was_held()
    {
        Db.Open().Dispose();
        File.WriteAllText(StateFile("old"), """{"pid": 7}""");
        Sessions.SessionStates.ReadIn(Marks);
        Assert.That(Sessions.SessionStates.Rows()["old"].Value, Does.Contain("7"));
        Assert.That(File.Exists(StateFile("old") + ".bak"), Is.True, "a copy kept as .bak");
        Assert.That(File.Exists(StateFile("old")), Is.True, "still exported");

        Hook(HookState("""save(DIR / "s2.state.json", {"pid": 1})"""));
        using (var connection = Db.Open())
        {
            OrchestratorDb.Execute(connection, "BEGIN IMMEDIATE; UPDATE session_states SET value = '{}';");
            Assert.That(Hook(HookState("""print(load(DIR / "s2.state.json")["pid"])""")), Is.EqualTo("1"), "read mid-write: the committed state");
            Hook(HookState("""save(DIR / "s2.state.json", {"pid": 2})"""));
            OrchestratorDb.Execute(connection, "ROLLBACK;");
        }
        Assert.That(Sessions.SessionStates.Rows()["s2"].Value, Does.Contain("1"), "held: the row couldn't take it");
        Assert.That(Hook(HookState("""print(load(DIR / "s2.state.json")["pid"])""")), Is.EqualTo("2"), "the file, written later, is the state");
        Sessions.SessionStates.ReadIn(Marks);
        Assert.That(Sessions.SessionStates.Rows()["s2"].Value, Does.Contain("2"), "read in at the daemon's next tick");

        Sessions.SessionStates.Prune(DateTime.UtcNow.AddMinutes(1));
        Assert.That(Sessions.SessionStates.Rows(), Is.Empty, "rows go with their sessions' files");
    }

    [Test]
    public void Context_gates_saving_states_at_once_each_keep_their_row()
    {
        Db.Open().Dispose();
        var writers = Enumerable.Range(0, 8).Select(i => Start(HookState($$"""save(DIR / "c{{i}}.state.json", {"pid": {{i}}})"""))).ToList();
        foreach (var p in writers)
        {
            Assert.That(p.WaitForExit(30_000), Is.True);
            Assert.That(p.ExitCode, Is.EqualTo(0), p.StandardError.ReadToEnd());
            p.Dispose();
        }
        Assert.That(Sessions.SessionStates.Rows().Keys, Is.EquivalentTo(Enumerable.Range(0, 8).Select(i => "c" + i)));
    }

    // ---- the meter probe's readings: a row, the statusline's claude-usage.json read and never replaced ----

    private string UsageFile => Path.Combine(dir, ".claude", "claude-usage.json");

    private static string Usage(double percent, DateTime at, DateTime reset, string? account = null) =>
        "{\"rate_limits\": {\"five_hour\": {\"used_percentage\": " + percent.ToString(System.Globalization.CultureInfo.InvariantCulture) +
        ", \"resets_at\": " + new DateTimeOffset(reset).ToUnixTimeSeconds() + "}}, \"at\": \"" + at.ToString("yyyy-MM-ddTHH:mm:ssZ") + "\"" +
        (account is null ? "" : ", \"account\": \"" + account + "\"") + "}";

    [Test]
    public void The_meter_probe_writes_its_reading_to_a_row_and_never_replaces_the_statuslines_file()
    {
        Db.Open().Dispose();
        var now = DateTime.UtcNow;
        var reset = now.AddHours(3);
        File.WriteAllText(UsageFile, Usage(40, now.AddMinutes(-30), reset));
        var statusline = File.ReadAllText(UsageFile);

        var reading = new Budget.ReportedUsage(new Budget.ReportedWindow(55, reset), null, now);
        Budget.ClaudeMeterProbe.Write(UsageFile, reading, now, "acct", Db);
        Assert.That(File.ReadAllText(UsageFile), Is.EqualTo(statusline), "the statusline's file is left as it was");
        Assert.That(Row(Budget.ReportedUsage.StateName)!["account"]!.GetValue<string>(), Is.EqualTo("acct"));
        Assert.That(Budget.ReportedUsage.Load(now, UsageFile, Db)!.FiveHour!.Percent, Is.EqualTo(55), "the later of the file and the row");

        // A statusline drawing after it: its reading is the later one.
        File.WriteAllText(UsageFile, Usage(60, now.AddMinutes(1), reset));
        Assert.That(Budget.ReportedUsage.Load(now.AddMinutes(1), UsageFile, Db)!.FiveHour!.Percent, Is.EqualTo(60));
        Assert.That(Db.Dump(), Does.Not.Contain("acct"), "out of the dump");
    }

    [Test]
    public void The_hooks_stop_on_the_probes_reading_in_the_database_when_the_statuslines_file_is_older()
    {
        Db.Open().Dispose();
        var now = DateTime.UtcNow;
        var reset = now.AddHours(1);
        File.WriteAllText(UsageFile, Usage(80, now.AddMinutes(-10), reset));
        Assert.That(Hook("""print(claude_stop())"""), Is.EqualTo("None"), "the statusline's 80% isn't the stop");

        Db.SaveState(Budget.ReportedUsage.StateName, Usage(99.4, now, reset));
        Assert.That(Hook("""print(claude_stop()[0])"""), Is.EqualTo("99.4"), "the probe's later 99% is");

        using var connection = Db.Open();
        OrchestratorDb.Execute(connection, "BEGIN IMMEDIATE; UPDATE state SET value = '{}' WHERE name = 'claude-usage';");
        Assert.That(Hook("""print(claude_usage()["rate_limits"]["five_hour"]["used_percentage"])"""), Is.EqualTo("99.4"), "read mid-write: the committed reading");
        OrchestratorDb.Execute(connection, "ROLLBACK;");
    }

    [Test]
    public void A_latch_lift_with_nothing_latched_opens_no_write_transaction()
    {
        Db.Open().Dispose();
        using var connection = Db.Open();
        OrchestratorDb.Execute(connection, "BEGIN IMMEDIATE;");
        var clock = Stopwatch.StartNew();
        Hook("""latch("orchestrator", None)""");
        Assert.That(clock.Elapsed, Is.LessThan(TimeSpan.FromSeconds(1.8)), "nothing to lift: no wait on the write lock");
        OrchestratorDb.Execute(connection, "ROLLBACK;");
        Assert.That(File.Exists(Latch), Is.False);
    }
}
