using System.Diagnostics;
using System.Text.Json;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Setup;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T1680: the heavy-process lease. The hook's own Python (<see cref="HeavyGate.Python"/>, as it's spliced into
/// the pace gate) is run against a scratch database and a made-up machine (<see cref="HeavyGate.FakeProcessesEnvVar"/>), and
/// the orchestrator's reading of a lease (<see cref="HeavyLease.State"/>) against made-up processes.</summary>
public sealed class HeavyLeaseTests
{
    private string dir = "";
    private string Db => Path.Combine(dir, "orchestrator.db");
    private string Processes => Path.Combine(dir, "processes.json");
    private string Repo => Path.Combine(dir, "GridGame");
    private const int CliA = 5001, CliB = 5002, CliC = 5003;

    /// <summary>The names the pace gate gives the hook, stood in for: the scratch database, no settings, and process start
    /// times read from the made-up machine. Then the hook's Python, then one request from stdin.</summary>
    private static readonly string Driver = """
        import json, os, re, sys, time
        from pathlib import Path
        ORCH_DB = os.environ["HEAVY_TEST_DB"]
        def db_connect(timeout, **kw):
            import sqlite3
            return sqlite3.connect(ORCH_DB, timeout=timeout, **kw)
        def orch_settings():
            return {}
        def cli_pid(names=()):
            return None
        def account_tag():
            return None
        def process_started(pid):
            for p in heavy_fake() or []:
                if p[0] == int(pid):
                    return p[1]
            return None

        """.Replace("\r\n", "\n") + HeavyGate.Python + """

        request = json.loads(sys.stdin.buffer.read().decode("utf-8"))
        def table():
            import sqlite3
            db = sqlite3.connect(ORCH_DB, isolation_level=None)
            db.execute("CREATE TABLE IF NOT EXISTS leases (name TEXT PRIMARY KEY, holder TEXT NOT NULL, pid INTEGER NOT NULL, started REAL, taken_at REAL NOT NULL)")
            return db
        if request["op"] == "match":
            patterns = heavy_patterns(request.get("cwd"))
            print(json.dumps([(heavy_match(c, patterns) or [None])[0] for c in request["commands"]]))
        elif request["op"] == "age":
            # Every lease was taken this many seconds earlier than it says.
            db = table()
            for name, raw in db.execute("SELECT name, holder FROM leases").fetchall():
                h = json.loads(raw)
                h["at"] = h["at"] - request["seconds"]
                db.execute("UPDATE leases SET holder = ? WHERE name = ?", (json.dumps(h), name))
            db.close()
            print("null")
        elif request["op"] == "gate":
            table().close()
            refusal = heavy_gate(request["data"])
            heavy_touch()
            db = table()
            rows = db.execute("SELECT name, holder FROM leases ORDER BY name").fetchall()
            db.close()
            print(json.dumps({"refusal": refusal, "rows": rows}))
        """.Replace("\r\n", "\n");

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "heavy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(Repo, ".git"));
        File.WriteAllText(Path.Combine(dir, "driver.py"), Driver);
        Machine((CliA, 100, "claude.exe"), (CliB, 100, "claude.exe"), (CliC, 100, "claude.exe"));
    }

    [TearDown]
    public void TearDown()
    {
        for (var attempt = 0; ; attempt++)
        {
            try { Directory.Delete(dir, recursive: true); return; }
            catch (IOException) when (attempt < 10) { Thread.Sleep(300); }
            catch (UnauthorizedAccessException) when (attempt < 10) { Thread.Sleep(300); }
        }
    }

    /// <summary>The made-up machine's processes: pid, start time (epoch seconds) and command line.</summary>
    private void Machine(params (int Pid, double Started, string CommandLine)[] processes) =>
        File.WriteAllText(Processes, JsonSerializer.Serialize(processes.Select(p => new object[] { p.Pid, p.Started, p.CommandLine })));

    private JsonElement Ask(object request, int cli = CliA, string? limit = null)
    {
        var psi = new ProcessStartInfo("python") { RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false };
        psi.ArgumentList.Add("-I");
        psi.ArgumentList.Add(Path.Combine(dir, "driver.py"));
        psi.Environment["HEAVY_TEST_DB"] = Db;
        psi.Environment[HeavyGate.FakeProcessesEnvVar] = Processes;
        psi.Environment["MINDATTIC_CLI_PID"] = cli.ToString();
        if (limit is null) psi.Environment.Remove(HeavyGate.LimitEnvVar);
        else psi.Environment[HeavyGate.LimitEnvVar] = limit;
        using var p = Process.Start(psi)!;
        p.StandardInput.BaseStream.Write(JsonSerializer.SerializeToUtf8Bytes(request));
        p.StandardInput.Close();
        var output = p.StandardOutput.ReadToEnd();
        Assert.That(p.WaitForExit(30_000), Is.True);
        return JsonDocument.Parse(output).RootElement.Clone();
    }

    private string?[] Match(params string[] commands) =>
        [.. Ask(new { op = "match", cwd = Repo, commands }).EnumerateArray().Select(e => e.GetString())];

    /// <summary>One tool call through the hook: the refusal (null when it's let through) and the lease rows afterwards.</summary>
    private (string? Refusal, List<HeavyHolder> Rows) Gate(string command, string session, int cli, string agent = "", string? limit = null)
    {
        var answer = Ask(new { op = "gate", data = new { tool_name = "Bash", session_id = session, agent_id = agent, cwd = Repo, tool_input = new { command } } }, cli, limit);
        var rows = answer.GetProperty("rows").EnumerateArray().Select(r => HeavyLease.Parse(r[0].GetString()!, r[1].GetString()!)!).ToList();
        return (answer.GetProperty("refusal").GetString(), rows);
    }

    private void Age(int seconds) => Ask(new { op = "age", seconds });

    private static double Now => (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;

    [Test]
    public void The_default_patterns_are_a_readable_list()
    {
        using var doc = JsonDocument.Parse(HeavyGate.DefaultPatternsJson);
        var patterns = doc.RootElement.GetProperty("patterns").EnumerateArray().ToList();
        Assert.That(patterns, Is.Not.Empty);
        foreach (var p in patterns)
        {
            Assert.That(p.GetProperty("name").GetString(), Is.Not.Empty);
            Assert.That(p.GetProperty("regex").GetString(), Is.Not.Empty);
            if (p.TryGetProperty("at", out var at)) Assert.That(at.GetString(), Is.AnyOf("command", "program", "anywhere"));
        }
        Assert.That(HeavyGate.DefaultPatternsJson, Does.Not.Contain("'''"), "it sits inside a Python r'''...''' string");
        Assert.That(AgentGate.Script, Does.Contain("heavy_gate(data)").And.Not.Contain("__HEAVY_"), "spliced into the pace gate, every placeholder filled");
    }

    [Test]
    public void Engine_builds_players_and_soaks_are_heavy()
    {
        var hits = Match(
            "\"C:\\Program Files\\Unity\\Hub\\Editor\\6000.0.23f1\\Editor\\Unity.exe\" -batchmode -quit -projectPath . -executeMethod Build.Run",
            "& 'C:\\Program Files\\Unity\\Hub\\Editor\\6000.0.23f1\\Editor\\Unity.exe' -projectPath D:\\Projects\\MindAttic\\GridGame",
            "cd D:\\Projects\\MindAttic\\GridGame && Engine\\Build\\BatchFiles\\RunUAT.bat BuildCookRun -project=Game.uproject",
            "\"D:\\UE_5.4\\Engine\\Build\\BatchFiles\\Build.bat\" GameEditor Win64 Development",
            "UnrealEditor-Cmd.exe Game.uproject -run=cook",
            "UE4Editor.exe Game.uproject",
            "Start-Process sbox-dev.exe -ArgumentList '-project', 'game.sbproj'",
            "D:\\Projects\\MindAttic\\GridGame\\Builds\\Windows\\GridGame.exe -screen-fullscreen 0",
            "./Builds/GridGame.exe",
            "python tools/soak.py --hours 4",
            "pwsh -File tools\\run-balance.ps1",
            "python harness.py campaign --seeds 200");
        Assert.That(hits, Has.None.Null, string.Join(" | ", hits.Select(h => h ?? "(not heavy)")));
        Assert.That(hits[0], Does.Contain("Unity"));
        Assert.That(hits[2], Does.Contain("RunUAT"));
        Assert.That(hits[6], Is.EqualTo("s&box"));
        Assert.That(hits[7], Is.EqualTo("a built game player"));
    }

    [Test]
    public void Ordinary_commands_are_not_heavy()
    {
        var hits = Match(
            "dotnet build MindAttic.Orchestrator.sln",
            "git -C D:\\Projects\\MindAttic\\UnitySolarSystem status",
            "rg -n \"Unity.exe\" docs",
            "ls Builds",
            "Get-ChildItem D:\\Projects\\MindAttic\\GridGame\\Builds\\Windows\\GridGame.exe",
            "\"C:\\Program Files\\Unity Hub\\Unity Hub.exe\"",
            "C:\\Windows\\System32\\where.exe python",
            "git commit -m \"Run the soak.ps1 nightly\"",
            "cat <<'EOF' > notes.md\nUnity.exe -batchmode -quit\nEOF");
        Assert.That(hits, Has.All.Null, string.Join(" | ", hits.Select(h => h ?? "(not heavy)")));
    }

    [Test]
    public void A_repo_adds_its_own_heavy_commands()
    {
        Assert.That(Match("godot --headless --export-release Windows")[0], Is.Null, "not in the default list");
        Directory.CreateDirectory(Path.Combine(Repo, ".agent"));
        File.WriteAllText(Path.Combine(Repo, ".agent", "heavy-patterns.json"),
            """{ "patterns": [ { "name": "a Godot export", "regex": "godot(?:\\.exe)?\\s.*--export" }, { "name": "broken", "regex": "(" } ] }""");
        var hits = Match("godot --headless --export-release Windows", "Unity.exe -batchmode");
        Assert.That(hits[0], Is.EqualTo("a Godot export"));
        Assert.That(hits[1], Is.Not.Null, "added to the default list, never replacing it; a bad pattern doesn't disable the rest");
    }

    [Test]
    public void The_second_heavy_command_is_refused_while_the_first_holds_the_lease()
    {
        var (first, rows) = Gate("Unity.exe -batchmode -quit -projectPath .", "session-aaaaaaaa", CliA);
        Assert.That(first, Is.Null, "the lease was free: the first heavy command runs");
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0].Name, Is.EqualTo(HeavyLease.Prefix + "1"));
        Assert.That((rows[0].Session, rows[0].CliPid, rows[0].Phase, rows[0].Repo), Is.EqualTo(("session-aaaaaaaa", (long?)CliA, "starting", "GridGame")));
        Assert.That(rows[0].Command, Does.Contain("-batchmode"));
        Assert.That(rows[0].GatePid, Is.Null, "the gate is done with the call: the grace runs from when it let it through");

        var (second, after) = Gate("python tools/soak.py", "session-bbbbbbbb", CliB);
        Assert.That(second, Does.Contain("HEAVY-PROCESS LEASE").And.Contain("Unity.exe -batchmode").And.Contain("session session-").And.Contain("CLI pid " + CliA));
        Assert.That(second, Does.Contain("Don't kill").And.Contain("background timer"), "wait and retry, never kill the holder or ask the user");
        Assert.That(after, Has.Count.EqualTo(1), "the refused command holds nothing");
        Assert.That(after[0].Session, Is.EqualTo("session-aaaaaaaa"));

        var (subagent, _) = Gate("python tools/soak.py", "session-aaaaaaaa", CliA, agent: "agent-1");
        Assert.That(subagent, Does.Contain("HEAVY-PROCESS LEASE"), "a subagent of the holder's session is someone else");

        var (again, kept) = Gate("python harness.py campaign", "session-aaaaaaaa", CliA);
        Assert.That(again, Is.Null, "the holder's own next heavy command isn't refused by its own lease");
        Assert.That(kept, Has.Count.EqualTo(1));

        Assert.That(Gate("dotnet build", "session-bbbbbbbb", CliB).Refusal, Is.Null, "anything that isn't heavy runs as ever");
    }

    [Test]
    public void A_holder_that_is_gone_never_blocks_the_machine()
    {
        Assert.That(Gate("Unity.exe -batchmode", "session-aaaaaaaa", CliA).Refusal, Is.Null);
        Machine((CliB, 100, "claude.exe")); // the holder's CLI crashed
        var (refusal, rows) = Gate("python tools/soak.py", "session-bbbbbbbb", CliB);
        Assert.That(refusal, Is.Null);
        Assert.That(rows.Select(r => r.Session), Is.EqualTo(new[] { "session-bbbbbbbb" }), "the dead holder's row is gone and the lease is the asker's");

        // A pid reused by another process (another start time) isn't the holder either.
        Machine((CliA, 100, "claude.exe"), (CliB, 999, "claude.exe"));
        Assert.That(Gate("Unity.exe -batchmode", "session-aaaaaaaa", CliA).Refusal, Is.Null);
    }

    [Test]
    public void Past_its_grace_a_lease_lives_only_while_a_process_runs_its_command()
    {
        Assert.That(Gate("Unity.exe -batchmode -quit", "session-aaaaaaaa", CliA).Refusal, Is.Null);
        Age(HeavyLease.GraceSeconds + 30);
        var started = Now - 20; // after the lease was taken
        Machine((CliA, 100, "claude.exe"), (CliB, 100, "claude.exe"), (7001, started, "\"C:\\Unity\\Editor\\Unity.exe\" -batchmode -quit"));

        var (refusal, rows) = Gate("python tools/soak.py", "session-bbbbbbbb", CliB);
        Assert.That(refusal, Does.Contain("HEAVY-PROCESS LEASE"), "the build is still running");
        Assert.That(rows[0].Phase, Is.EqualTo("running"));
        Assert.That(rows[0].Procs.Select(p => p.Pid), Is.EqualTo(new long[] { 7001 }), "the lease is now that process's");

        Machine((CliA, 100, "claude.exe"), (CliB, 100, "claude.exe")); // the build ended; its CLI is still open
        var (free, now) = Gate("python tools/soak.py", "session-bbbbbbbb", CliB);
        Assert.That(free, Is.Null);
        Assert.That(now.Select(r => r.Session), Is.EqualTo(new[] { "session-bbbbbbbb" }));

        // A call that never ran (refused further down, or over in seconds): no process for it, so it lapses after the grace.
        Age(HeavyLease.GraceSeconds + 30);
        Assert.That(Gate("Unity.exe -batchmode", "session-cccccccc", CliC).Refusal, Is.Null);

        // A process running the same command from before the lease was taken isn't its process.
        Age(HeavyLease.GraceSeconds + 30);
        Machine((CliA, 100, "claude.exe"), (CliB, 100, "claude.exe"), (CliC, 100, "claude.exe"), (7002, 100, "Unity.exe -batchmode"));
        Assert.That(Gate("python tools/soak.py", "session-aaaaaaaa", CliA).Refusal, Is.Null);
    }

    [Test]
    public void The_limit_sets_how_many_run_at_once_and_zero_turns_it_off()
    {
        Assert.That(Gate("Unity.exe -batchmode", "session-aaaaaaaa", CliA, limit: "2").Refusal, Is.Null);
        var (second, rows) = Gate("python tools/soak.py", "session-bbbbbbbb", CliB, limit: "2");
        Assert.That(second, Is.Null, "two may run");
        Assert.That(rows.Select(r => r.Name), Is.EqualTo(new[] { HeavyLease.Prefix + "1", HeavyLease.Prefix + "2" }));
        var (third, _) = Gate("RunUAT.bat BuildCookRun", "session-cccccccc", CliC, limit: "2");
        Assert.That(third, Does.Contain("2 heavy processes").And.Contain("Unity.exe").And.Contain("soak.py"), "both holders are named");

        var (off, still) = Gate("RunUAT.bat BuildCookRun", "session-cccccccc", CliC, limit: "0");
        Assert.That(off, Is.Null, "0 turns enforcement off");
        Assert.That(still, Has.Count.EqualTo(2), "and takes no lease");
    }

    [Test]
    public void The_orchestrator_reads_a_lease_by_the_hooks_rule()
    {
        var now = DateTime.UtcNow;
        var at = (now - DateTime.UnixEpoch).TotalSeconds;
        HeavyHolder Lease(string phase, double age, long? gate = null, params (long, double?)[] procs) =>
            new(HeavyLease.Prefix + "1", "session-aaaaaaaa", "", "", 50, 10, gate, gate is null ? null : 20, "GridGame", "D:\\x", "amy", "Unity.exe -batchmode", "the Unity editor", phase, at - age, procs);
        static Func<long, double?, bool> Alive(params long[] pids) => (pid, _) => pids.Contains(pid);

        Assert.That(HeavyLease.State(Lease("starting", 5), now, Alive(50)), Is.EqualTo(HeavyLeaseState.Starting));
        Assert.That(HeavyLease.State(Lease("starting", 5), now, Alive()), Is.EqualTo(HeavyLeaseState.Free), "its CLI is gone");
        Assert.That(HeavyLease.State(Lease("starting", 500, gate: 60), now, Alive(50, 60)), Is.EqualTo(HeavyLeaseState.Starting), "its gate still holds the call");
        Assert.That(HeavyLease.State(Lease("starting", 500), now, Alive(50)), Is.EqualTo(HeavyLeaseState.Unconfirmed), "past the grace, no process known");
        Assert.That(HeavyLease.State(Lease("running", 500, null, (70, 30d)), now, Alive(50, 70)), Is.EqualTo(HeavyLeaseState.Running));
        Assert.That(HeavyLease.State(Lease("running", 500, null, (70, 30d)), now, Alive(50)), Is.EqualTo(HeavyLeaseState.Free), "its process ended, whatever its CLI does");
        Assert.That(HeavyLease.State(Lease("starting", 500, null, (70, 30d)), now, Alive(70)), Is.EqualTo(HeavyLeaseState.Running), "a process it runs outlives its CLI");

        var line = HeavyLease.Describe(Lease("running", 185, null, (70, 30d)), HeavyLeaseState.Running, now);
        Assert.That(line, Does.Contain("heavy-1").And.Contain("Unity.exe -batchmode").And.Contain("3 min").And.Contain("repo GridGame").And.Contain("CLI pid 50"));

        Assert.That(HeavyLease.Parse("heavy-1", "not json"), Is.Null);
        var parsed = HeavyLease.Parse("heavy-2", """{"session":"s","agent":"a1","cli":12,"cli_started":3.5,"gate":null,"phase":"running","at":99,"command":"x","procs":[[7,1.5],[8,null]]}""")!;
        Assert.That((parsed.Name, parsed.Session, parsed.Agent, parsed.CliPid, parsed.GatePid, parsed.At), Is.EqualTo(("heavy-2", "s", "a1", (long?)12, (long?)null, 99d)));
        Assert.That(parsed.Procs, Is.EqualTo(new (long, double?)[] { (7, 1.5), (8, null) }));
    }
}
