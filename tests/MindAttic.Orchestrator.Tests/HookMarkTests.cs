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
