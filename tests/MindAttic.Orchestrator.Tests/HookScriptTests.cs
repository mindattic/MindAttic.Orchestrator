using System.Diagnostics;
using System.Text.Json;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Setup;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>Runs the generated pace-gate.py and context-gate.py the way Claude Code does: JSON on stdin, a scratch profile.</summary>
public sealed class HookScriptTests
{
    private string dir = "";
    private string ws = "";
    private string Claude => Path.Combine(dir, ".claude");
    private string Marks => Path.Combine(Claude, "session-context");
    private const int Cli = 4242;

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "hooks-" + Guid.NewGuid().ToString("N"));
        ws = Path.Combine(dir, "ws");
        foreach (var r in new[] { "UnitySolarSystem", "MindAttic.Tickets" }) Directory.CreateDirectory(Path.Combine(ws, r, ".git"));
        AgentGate.Ensure(Claude);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(dir, recursive: true);

    private Process Run(string script, object input, params string[] args) => Run(script, input, null, args);

    private Process Run(string script, object input, IDictionary<string, string>? env, params string[] args)
    {
        var psi = new ProcessStartInfo("python") { RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false };
        psi.ArgumentList.Add("-I");
        psi.ArgumentList.Add(Path.Combine(Claude, script));
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.Environment["USERPROFILE"] = dir;
        psi.Environment["APPDATA"] = dir; // the orchestrator's settings (approvals) come from here, never this machine's
        psi.Environment["MINDATTIC_CLI_PID"] = Cli.ToString(); // as one CLI process, never the one running these tests
        psi.Environment["MINDATTIC_WORKSPACE"] = ws;
        // The scratch workspace's database, never one this machine names (by the variable's current name or its legacy one).
        psi.Environment.Remove("MINDATTIC_TICKETS_DB");
        psi.Environment.Remove("MINDATTIC_WORKFLOW_DB");
        // Keep-going never asks this machine's orchestrator what `next` would hand out: none is found unless a test sets one.
        psi.Environment["MINDATTIC_ORCHESTRATOR"] = Path.Combine(dir, "no-orchestrator.exe");
        // Inside claude.exe on its /login, whatever the machine running the tests is signed in with.
        foreach (var auth in AuthVariables) psi.Environment.Remove(auth);
        // On the global login unless a test puts the CLI on one of its own (M.ORCH-T559), whatever runs the tests.
        psi.Environment.Remove("CLAUDE_CONFIG_DIR");
        psi.Environment["CLAUDECODE"] = "1";
        psi.Environment["CLAUDE_CODE_ENTRYPOINT"] = "cli";
        foreach (var (k, v) in env ?? new Dictionary<string, string>()) psi.Environment[k] = v;
        var p = Process.Start(psi)!;
        // Raw UTF-8, non-ASCII unescaped, as Claude Code writes it (M.ORCH-T392).
        p.StandardInput.BaseStream.Write(JsonSerializer.SerializeToUtf8Bytes(input, Utf8Input));
        p.StandardInput.Close();
        return p;
    }

    private static readonly JsonSerializerOptions Utf8Input = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static readonly string[] AuthVariables =
    [
        "CLAUDE_CODE_USE_BEDROCK", "CLAUDE_CODE_USE_VERTEX", "CLAUDE_CODE_USE_FOUNDRY", "CLAUDE_CODE_USE_ANTHROPIC_AWS", "CLAUDE_CODE_USE_MANTLE",
        "ANTHROPIC_AUTH_TOKEN", "ANTHROPIC_API_KEY", "CLAUDE_CODE_OAUTH_TOKEN", "ANTHROPIC_PROFILE", "ANTHROPIC_FEDERATION_RULE_ID", "ANTHROPIC_ORGANIZATION_ID",
    ];

    private (string Output, TimeSpan Took) Gate(object input, string script = AgentGate.ScriptName, params string[] args) => Gate(input, (IDictionary<string, string>?)null, script, args);

    private (string Output, TimeSpan Took) Gate(object input, IDictionary<string, string>? env, string script = AgentGate.ScriptName, params string[] args)
    {
        var clock = Stopwatch.StartNew();
        using var p = Run(script, input, env, args);
        var output = p.StandardOutput.ReadToEnd();
        Assert.That(p.WaitForExit(30_000), Is.True);
        return (output, clock.Elapsed);
    }

    /// <summary>The pace gate with MINDATTIC_MAX_HOLD at <paramref name="maxHold"/> seconds, so the longest hold ends in seconds.</summary>
    private (string Output, TimeSpan Took) GateHeld(object input, int maxHold, IDictionary<string, string>? env = null)
    {
        var clock = Stopwatch.StartNew();
        var all = MaxHold(maxHold)!;
        foreach (var (k, v) in env ?? new Dictionary<string, string>()) all[k] = v;
        using var p = Run(AgentGate.ScriptName, input, all);
        var output = p.StandardOutput.ReadToEnd();
        Assert.That(p.WaitForExit(30_000), Is.True);
        return (output, clock.Elapsed);
    }

    /// <summary>MINDATTIC_MAX_HOLD, so a hold that would last the full 300s ends (and is refused) in seconds.</summary>
    private static Dictionary<string, string>? MaxHold(int? seconds) =>
        seconds is { } s ? new() { ["MINDATTIC_MAX_HOLD"] = s.ToString() } : null;

    /// <summary>Starts the gate, then after <paramref name="after"/> rewrites rl-status.json while the gate is waiting.</summary>
    private (string Output, TimeSpan Took) GateWhile(object input, TimeSpan after, Action change, int? maxHold = null)
    {
        var clock = Stopwatch.StartNew();
        using var p = Run(AgentGate.ScriptName, input, MaxHold(maxHold));
        var read = p.StandardOutput.ReadToEndAsync();
        Assert.That(p.WaitForExit((int)after.TotalMilliseconds), Is.False, "still held when the file changes");
        change();
        Assert.That(p.WaitForExit(30_000), Is.True);
        return (read.Result, clock.Elapsed);
    }

    private void Tickets(params (string Id, string Repo)[] rows)
    {
        var db = Path.Combine(ws, "MindAttic.Tickets", "data", "Tickets.db");
        Directory.CreateDirectory(Path.GetDirectoryName(db)!);
        var values = string.Join(",", rows.Select(r => $"('{r.Id}','{r.Repo}','open',1,'fix')"));
        using var mk = Process.Start(new ProcessStartInfo("python") { ArgumentList = { "-I", "-c",
            "import sqlite3,sys; d=sqlite3.connect(sys.argv[1]); d.execute('create table tickets(id,repo,status,ordinal,type)'); " +
            $"d.execute(\"insert into tickets values{values}\"); d.commit()", db }, UseShellExecute = false })!;
        Assert.That(mk.WaitForExit(20_000), Is.True);
    }

    /// <summary>Records repo renames in the ticket database's repo_renames table, as `tickets repo rename` would.</summary>
    private void RepoRenames(params (string Old, string New)[] renames)
    {
        var db = Path.Combine(ws, "MindAttic.Tickets", "data", "Tickets.db");
        Directory.CreateDirectory(Path.GetDirectoryName(db)!);
        var values = string.Join(",", renames.Select(r => $"('{r.Old}','{r.New}','2026-01-01T00:00:00Z','rename')"));
        using var mk = Process.Start(new ProcessStartInfo("python") { ArgumentList = { "-I", "-c",
            "import sqlite3,sys; d=sqlite3.connect(sys.argv[1]); " +
            "d.execute('create table if not exists repo_renames(seq integer primary key, old_name, new_name, at, reason)'); " +
            $"d.execute(\"insert into repo_renames(old_name,new_name,at,reason) values{values}\"); d.commit()", db }, UseShellExecute = false })!;
        Assert.That(mk.WaitForExit(20_000), Is.True);
    }

    private static readonly object ReadCall = new { tool_name = "Read", tool_input = new { file_path = "x" } };

    /// <summary>The pace gate's events, as the old log's lines: the file a hook falls back to, then the database's gate_events
    /// rows (M.ORCH-T526) once a test's database exists, written out as the hook wrote its lines.</summary>
    private string[] GateLog()
    {
        var path = Path.Combine(Marks, AgentGate.GateLogName);
        var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : [];
        var db = MindAttic.Orchestrator.Settings.OrchestratorDb.DefaultPath();
        if (!File.Exists(db)) return [.. lines];
        using var connection = new MindAttic.Orchestrator.Settings.OrchestratorDb(db).Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT at, decision, held, tool, percent, account FROM gate_events ORDER BY id;";
        using var r = command.ExecuteReader();
        string Json(int i) => r.IsDBNull(i) ? "null" : JsonSerializer.Serialize(r.GetValue(i));
        while (r.Read())
            lines.Add($"{{\"at\": {Json(0)}, \"decision\": {Json(1)}, \"held\": {Json(2)}, \"tool\": {Json(3)}, \"percent\": {Json(4)}, \"account\": {Json(5)}}}");
        return [.. lines];
    }

    private void Status(object fields, DateTime? at = null, string source = "MindAttic.Orchestrator")
    {
        var body = JsonSerializer.SerializeToNode(fields)!.AsObject();
        body["source"] = source;
        body["timestamp"] = (at ?? DateTime.UtcNow).ToString("yyyy-MM-ddTHH:mm:ssZ");
        File.WriteAllText(Path.Combine(Claude, "rl-status.json"), body.ToJsonString());
    }

    [Test]
    public void The_pace_hold_is_obeyed_only_from_a_fresh_file_the_orchestrator_wrote()
    {
        var call = new { tool_name = "Read", tool_input = new { file_path = "x" } };
        Status(new { pace_delay_seconds = 2 });
        Assert.That(Gate(call).Took, Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(1.9)), "a live hold is waited out");
        Assert.That(GateLog().Single(), Does.Contain("\"decision\": \"held\"").And.Contain("\"tool\": \"Read\""), "the hold is on the pace ledger's record");

        Status(new { pace_delay_seconds = 30 }, at: DateTime.UtcNow.AddMinutes(-10));
        Assert.That(Gate(call).Took, Is.LessThan(TimeSpan.FromSeconds(10)), "stale, and no window it was for: nothing paces");

        Status(new { pace_delay_seconds = 30 }, source: "something else");
        Assert.That(Gate(call).Took, Is.LessThan(TimeSpan.FromSeconds(10)), "not the orchestrator's file");
    }

    [Test]
    public void At_the_windows_target_a_call_is_refused_even_from_an_unthrottled_cli()
    {
        Status(new { stop = true, stop_note = "At the target until 9:50 PM. End your turn now." });
        Directory.CreateDirectory(Marks);
        File.WriteAllText(Path.Combine(Marks, "unthrottled-4242"), "x");
        // MAX_HOLD is 300s; the test shortens nothing, so it only checks the call is still held well past a normal delay.
        using var held = Run(AgentGate.ScriptName, new { tool_name = "Read", tool_input = new { file_path = "x" } });
        Assert.That(held.WaitForExit(7_000), Is.False, "held, not let through");
        held.Kill();

        Status(new { stop = false });
        Assert.That(Gate(new { tool_name = "Read", tool_input = new { file_path = "x" } }).Took, Is.LessThan(TimeSpan.FromSeconds(10)), "lifted: runs at once");
        Assert.That(File.ReadAllText(Path.Combine(Claude, AgentGate.ScriptName)), Does.Contain("refuse(note)"),
            "past the longest hold it's refused, not let through");
    }

    private void ClaudeReports(double percent, DateTime? resetUtc = null) =>
        File.WriteAllText(Path.Combine(Claude, "claude-usage.json"), JsonSerializer.Serialize(new
        {
            rate_limits = new { five_hour = new { used_percentage = percent, resets_at = new DateTimeOffset(resetUtc ?? DateTime.UtcNow.AddHours(1)).ToUnixTimeSeconds() } },
            at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        }));

    [Test]
    public void A_ticket_waiting_on_another_open_one_is_not_work_to_push_on_to()
    {
        // `orchestrator next` hands out nothing blocked, so keep-going pushing back to it only spent a turn and raised "needs you".
        var db = Path.Combine(ws, "MindAttic.Tickets", "data", "Tickets.db");
        Directory.CreateDirectory(Path.GetDirectoryName(db)!);
        void Sql(string sql)
        {
            using var mk = Process.Start(new ProcessStartInfo("python") { ArgumentList = { "-I", "-c",
                "import sqlite3,sys; d=sqlite3.connect(sys.argv[1]); d.executescript(sys.argv[2]); d.commit()", db, sql }, UseShellExecute = false })!;
            mk.WaitForExit(20_000);
        }
        Sql("create table tickets(id,repo,status,ordinal,type,summary); create table links(from_id,to_id,kind);" +
            "insert into tickets values('U.SPC-T1','UnitySolarSystem','open',1,'fix','needs the API');" +
            "insert into tickets values('U.SPC-T2','UnitySolarSystem','open',2,'feature','the API (not approved yet)');" +
            "insert into links values('U.SPC-T1','U.SPC-T2','requires');");
        var uss = Path.Combine(ws, "UnitySolarSystem");
        Directory.CreateDirectory(Marks);
        File.WriteAllText(Path.Combine(Marks, $"allocate-{Cli}"), "UnitySolarSystem");
        object Asking(string sid) => new { hook_event_name = "Stop", session_id = sid, cwd = uss, last_assistant_message = "Shall I start the next one?" };
        Status(new { });

        Assert.That(Gate(Asking("b1"), AgentGate.ContextScriptName).Output, Is.Empty, "its only fix requires an open ticket");
        Sql("delete from links; update tickets set summary='blocked by U.SPC-T2 for now' where id='U.SPC-T1';");
        Assert.That(Gate(Asking("b2"), AgentGate.ContextScriptName).Output, Is.Empty, "'blocked by' in its summary counts the same");
        Sql("update tickets set status='closed' where id='U.SPC-T2';");
        Assert.That(Gate(Asking("b3"), AgentGate.ContextScriptName).Output, Does.Contain("\"block\""), "its blocker closed: it's work again");
    }

    [Test]
    public void A_ticket_requiring_an_epic_is_work_once_the_epic_is_approved_and_the_epic_never_is()
    {
        // M.ORCH-T1146, as the scheduler reads it: a 'requires' link to an epic waits only until the epic is approved.
        var db = Path.Combine(ws, "MindAttic.Tickets", "data", "Tickets.db");
        Directory.CreateDirectory(Path.GetDirectoryName(db)!);
        void Sql(string sql)
        {
            using var mk = Process.Start(new ProcessStartInfo("python") { ArgumentList = { "-I", "-c",
                "import sqlite3,sys; d=sqlite3.connect(sys.argv[1]); d.executescript(sys.argv[2]); d.commit()", db, sql }, UseShellExecute = false })!;
            mk.WaitForExit(20_000);
        }
        Sql("create table tickets(id,repo,status,ordinal,type,summary); create table links(from_id,to_id,kind);" +
            "create table ticket_queue(ticket_id,approval,priority,updated); create table ticket_labels(ticket_id,label,added);" +
            "insert into tickets values('U.SPC-T1','UnitySolarSystem','open',1,'feature','the epic');" +
            "insert into tickets values('U.SPC-T2','UnitySolarSystem','open',2,'feature','a child');" +
            "insert into ticket_labels values('U.SPC-T1','epic','');" +
            "insert into ticket_queue values('U.SPC-T2','approved',0,'');" +
            "insert into links values('U.SPC-T2','U.SPC-T1','requires');");
        var uss = Path.Combine(ws, "UnitySolarSystem");
        Directory.CreateDirectory(Marks);
        File.WriteAllText(Path.Combine(Marks, $"allocate-{Cli}"), "UnitySolarSystem");
        object Asking(string sid) => new { hook_event_name = "Stop", session_id = sid, cwd = uss, last_assistant_message = "Shall I start the next one?" };
        Status(new { });

        Assert.That(Gate(Asking("e1"), AgentGate.ContextScriptName).Output, Is.Empty, "its epic isn't approved yet");
        Sql("insert into ticket_queue values('U.SPC-T1','approved',0,'');");
        var told = Gate(Asking("e2"), AgentGate.ContextScriptName).Output;
        Assert.That(told, Does.Contain("\"block\"").And.Contain("U.SPC-T2"), "its epic is started: it's work");
        Assert.That(told, Does.Not.Contain("U.SPC-T1"), "the epic itself never is");
    }

    [Test]
    public void A_prompt_refused_at_a_weekly_stop_names_the_week_and_its_reset()
    {
        Status(new { stop = true, stop_window = "weekly", reset_time = "Mon 9:00 AM", stop_until = DateTime.UtcNow.AddDays(3).ToString("yyyy-MM-ddTHH:mm:ssZ") });
        var said = Gate(new { hook_event_name = "UserPromptSubmit", session_id = "w9", prompt = "go on", cwd = ws }, AgentGate.ContextScriptName).Output;
        Assert.That(said, Does.Contain("the weekly window is at its target").And.Contain("resets at Mon 9:00 AM"));

        // The daemon stalls: the latched stop still says which window and when.
        Status(new { stop = true, stop_window = "weekly", reset_time = "Mon 9:00 AM" }, at: DateTime.UtcNow.AddMinutes(-10));
        said = Gate(new { hook_event_name = "UserPromptSubmit", session_id = "w9", prompt = "go on", cwd = ws }, AgentGate.ContextScriptName).Output;
        Assert.That(said, Does.Contain("the weekly window is at its target").And.Contain("resets at Mon 9:00 AM"));
    }

    [Test]
    public void At_the_stop_a_goal_ends_its_turn_instead_of_looping_and_a_new_one_isnt_started()
    {
        object Typed(string sid, string prompt) => new { hook_event_name = "UserPromptSubmit", session_id = sid, prompt, cwd = ws };
        Status(new { });
        Gate(Typed("g1", "/goal all the tests pass"), AgentGate.ContextScriptName);

        Status(new { stop = true });
        var stop = Gate(new { hook_event_name = "Stop", session_id = "g1", cwd = ws }, AgentGate.ContextScriptName).Output;
        Assert.That(stop, Does.Contain("\"continue\": false"), "the goal's own Stop hook would send it back to calls that are all refused");
        Assert.That(stop, Does.Contain("The 5-hour window is at its target"));
        Status(new { stop = true, stop_window = "weekly" });
        Assert.That(Gate(new { hook_event_name = "Stop", session_id = "g1", cwd = ws }, AgentGate.ContextScriptName).Output,
            Does.Contain("The weekly window is at its target"), "a weekly wall says so");
        Status(new { stop = true });
        Assert.That(Gate(Typed("g2", "/goal all the tests pass"), AgentGate.ContextScriptName).Output, Does.Contain("Not sent").And.Contain("/goal clear still go through"),
            "a new goal at the stop is a turn past the line, and the refusal doesn't say /goal goes through");
        Assert.That(Gate(Typed("g2", "/goal clear all lint warnings"), AgentGate.ContextScriptName).Output, Does.Contain("Not sent"),
            "a new goal that starts with a clear word is still a new goal");
        Assert.That(Gate(Typed("g1", "/goal clear"), AgentGate.ContextScriptName).Output, Does.Not.Contain("Not sent"), "clearing one goes through");
        Assert.That(Gate(new { hook_event_name = "Stop", session_id = "g3", cwd = ws }, AgentGate.ContextScriptName).Output, Is.Empty,
            "no goal: an ordinary turn ending at the stop just ends");
    }

    [Test]
    public void What_orchestrator_next_holds_back_isnt_work_to_push_on_to()
    {
        var db = Path.Combine(ws, "MindAttic.Tickets", "data", "Tickets.db");
        Directory.CreateDirectory(Path.GetDirectoryName(db)!);
        using (var mk = Process.Start(new ProcessStartInfo("python") { ArgumentList = { "-I", "-c",
            "import sqlite3,sys; d=sqlite3.connect(sys.argv[1]); d.execute('create table tickets(id,repo,status,ordinal,type)'); " +
            "d.execute(\"insert into tickets values('U.SPC-T1','UnitySolarSystem','open',1,'fix')\"); d.commit()", db }, UseShellExecute = false })!)
            mk.WaitForExit(20_000);
        var uss = Path.Combine(ws, "UnitySolarSystem");
        Directory.CreateDirectory(Marks);
        File.WriteAllText(Path.Combine(Marks, $"allocate-{Cli}"), "UnitySolarSystem");
        var store = Path.Combine(dir, "MindAttic", "MindAttic.Orchestrator");
        Directory.CreateDirectory(store);
        var holds = Path.Combine(store, Daemon.AllocateHoldsName + ".json");
        var settings = Path.Combine(store, "settings.json");
        object Asking(string sid) => new { hook_event_name = "Stop", session_id = sid, cwd = uss, last_assistant_message = "Shall I start the next one?" };
        Status(new { });
        string Asked(string sid) => Gate(Asking(sid), AgentGate.ContextScriptName).Output;

        Assert.That(Asked("h1"), Does.Contain("\"block\""), "nothing held: it's work");
        // Written as the daemon writes it, so the names the hook reads are the ones it gets.
        new MindAttic.Orchestrator.Settings.JsonStore(store).Save(Daemon.AllocateHoldsName,
            new Daemon.AllocateHolds(false, [new MindAttic.Orchestrator.Scheduling.WorkedTicket("U.SPC-T1", "other", null, "filed", Filed: true)], []));
        Assert.That(Asked("h2"), Is.Empty, "another live session filed it and is fixing it: next won't hand it out");
        Assert.That(Asked("other"), Does.Contain("\"block\""), "the session that filed it isn't held from it");
        new MindAttic.Orchestrator.Settings.JsonStore(store).Save(Daemon.AllocateHoldsName,
            new Daemon.AllocateHolds(false, [new MindAttic.Orchestrator.Scheduling.WorkedTicket("U.SPC-T1", "started", null, "started on it")], []));
        Assert.That(Asked("started"), Is.Empty, "but the one it was started on is its own work, not one for its subagents (M.ORCH-T328)");
        File.WriteAllText(holds, """{"Quiet":true,"Worked":[],"LivePilot":[]}""");
        Assert.That(Asked("h3"), Is.Empty, "quiet hours");
        File.WriteAllText(holds, """{"Quiet":false,"Worked":[],"LivePilot":[]}""");
        File.WriteAllText(settings, """{ "Allocate": false }""");
        Assert.That(Asked("h4"), Is.Empty, "allocate paused");
        File.WriteAllText(settings, """{ "AllocateSkip": ["unitysolarsystem"] }""");
        Assert.That(Asked("h5"), Is.Empty, "a skipped repo");
    }

    [Test]
    public void Keep_going_pushes_a_stop_back_only_for_what_orchestrator_next_would_hand_out()
    {
        // M.ORCH-T385: another live session filed U.SPC-T1 a moment ago. `next` held it back at once ("already being worked"),
        // but the daemon's holds hadn't caught up, so every stop was sent back for it.
        var db = Path.Combine(ws, "MindAttic.Tickets", "data", "Tickets.db");
        Directory.CreateDirectory(Path.GetDirectoryName(db)!);
        using (var mk = Process.Start(new ProcessStartInfo("python") { ArgumentList = { "-I", "-c",
            "import sqlite3,sys; d=sqlite3.connect(sys.argv[1]); d.execute('create table tickets(id,repo,status,ordinal,type)'); " +
            "d.execute(\"insert into tickets values('U.SPC-T1','UnitySolarSystem','open',1,'fix')\"); d.commit()", db }, UseShellExecute = false })!)
            mk.WaitForExit(20_000);
        var uss = Path.Combine(ws, "UnitySolarSystem");
        Directory.CreateDirectory(Marks);
        File.WriteAllText(Path.Combine(Marks, $"allocate-{Cli}"), "UnitySolarSystem");
        Status(new { });
        // A stand-in for orchestrator.exe: records how it was asked and prints what `next` would.
        var output = Path.Combine(dir, "next-out.txt");
        var asked = Path.Combine(dir, "next-asked.txt");
        var fake = Path.Combine(dir, "orchestrator.cmd");
        var code = 0;
        void Fake() => File.WriteAllText(fake, $"@echo off\r\necho %* %MINDATTIC_CLI_PID%> \"{asked}\"\r\ntype \"{output}\"\r\nexit /b {code}\r\n");
        Fake();
        var env = new Dictionary<string, string> { ["MINDATTIC_ORCHESTRATOR"] = fake };
        string Asked(string sid) => Gate(new { hook_event_name = "Stop", session_id = sid, cwd = uss, last_assistant_message = "Shall I start the next one?" },
            env, AgentGate.ContextScriptName).Output;

        File.WriteAllText(output, "Nothing handed out: 1 already being worked; nothing else is ready.\r\n");
        Assert.That(Asked("k1"), Is.Empty, "next holds it back, so the stop isn't sent back for it");
        Assert.That(File.ReadAllText(asked).Trim(), Is.EqualTo($"next --repos UnitySolarSystem {Cli}"),
            "asked as a dry run (nothing claimed), as this CLI, so what it filed itself stays its own");

        File.WriteAllText(output, "=== U.SPC-T1 (UnitySolarSystem, fix): a bug\r\n");
        var told = Asked("k2");
        Assert.That(told, Does.Contain("\"block\"").And.Contain("U.SPC-T1"), "next would hand it out: it's work");

        code = 1;
        Fake();
        File.WriteAllText(output, "");
        Assert.That(Asked("k3"), Does.Contain("\"block\""), "next couldn't be asked: the hook's own count stands");
    }

    [Test]
    public void Keep_going_asks_orchestrator_next_for_a_repo_on_its_own_story_file()
    {
        // M.ORCH-T445: UnitySolarSystem keeps its tickets in docs/USER_STORIES.md, not the database, so the hook's own count
        // never saw its approved tickets and let an /allocate CLI stop with work left.
        var uss = Path.Combine(ws, "UnitySolarSystem");
        Directory.CreateDirectory(Path.Combine(uss, "docs"));
        File.WriteAllText(Path.Combine(uss, "docs", "USER_STORIES.md"), "| U.SPC-T9 | 2026-10-18 | feature | open | A feature | — |\n");
        Directory.CreateDirectory(Marks);
        File.WriteAllText(Path.Combine(Marks, $"allocate-{Cli}"), "UnitySolarSystem");
        Status(new { });
        var output = Path.Combine(dir, "next-out.txt");
        var fake = Path.Combine(dir, "orchestrator.cmd");
        File.WriteAllText(fake, $"@echo off\r\ntype \"{output}\"\r\nexit /b 0\r\n");
        var env = new Dictionary<string, string> { ["MINDATTIC_ORCHESTRATOR"] = fake };
        string Asked(string sid) => Gate(new { hook_event_name = "Stop", session_id = sid, cwd = uss, last_assistant_message = "Shall I start the next one?" },
            env, AgentGate.ContextScriptName).Output;

        File.WriteAllText(output, "=== U.SPC-T9 (UnitySolarSystem, feature): A feature\r\n");
        Assert.That(Asked("s1"), Does.Contain("\"block\"").And.Contain("U.SPC-T9"), "next hands out the story-file ticket: it's work");

        File.WriteAllText(output, "Nothing handed out: nothing is ready.\r\n");
        Assert.That(Asked("s2"), Is.Empty, "next has nothing for it: let stop");

        // Once the database holds the repo, its own count is the first look again (the story file is then generated).
        var db = Path.Combine(ws, "MindAttic.Tickets", "data", "Tickets.db");
        Directory.CreateDirectory(Path.GetDirectoryName(db)!);
        using (var mk = Process.Start(new ProcessStartInfo("python") { ArgumentList = { "-I", "-c",
            "import sqlite3,sys; d=sqlite3.connect(sys.argv[1]); d.execute('create table tickets(id,repo,status,ordinal,type)'); " +
            "d.execute(\"insert into tickets values('U.SPC-T9','UnitySolarSystem','closed',1,'feature')\"); d.commit()", db }, UseShellExecute = false })!)
            mk.WaitForExit(20_000);
        File.WriteAllText(output, "=== U.SPC-T9 (UnitySolarSystem, feature): A feature\r\n");
        Assert.That(Asked("s3"), Is.Empty, "a database repo with nothing workable isn't asked about");
    }

    [Test]
    public void A_claim_gone_stale_is_work_again_as_orchestrator_next_hands_it_out()
    {
        var db = Path.Combine(ws, "MindAttic.Tickets", "data", "Tickets.db");
        Directory.CreateDirectory(Path.GetDirectoryName(db)!);
        using (var mk = Process.Start(new ProcessStartInfo("python") { ArgumentList = { "-I", "-c",
            "import sqlite3,sys; d=sqlite3.connect(sys.argv[1]); d.execute('create table tickets(id,repo,status,ordinal,type)'); " +
            "d.execute(\"insert into tickets values('U.SPC-T1','UnitySolarSystem','open',1,'fix')\"); d.commit()", db }, UseShellExecute = false })!)
            mk.WaitForExit(20_000);
        var uss = Path.Combine(ws, "UnitySolarSystem");
        Directory.CreateDirectory(Marks);
        File.WriteAllText(Path.Combine(Marks, $"allocate-{Cli}"), "UnitySolarSystem");
        var store = Path.Combine(dir, "MindAttic", "MindAttic.Orchestrator");
        Directory.CreateDirectory(store);
        File.WriteAllText(Path.Combine(store, "assignments.json"), """[{"TicketId":"U.SPC-T1","Repo":"UnitySolarSystem"}]""");
        object Asking(string sid) => new { hook_event_name = "Stop", session_id = sid, cwd = uss, last_assistant_message = "Shall I start the next one?" };
        Status(new { });

        Assert.That(Gate(Asking("s1"), AgentGate.ContextScriptName).Output, Is.Empty, "claimed and live: being worked");
        File.WriteAllText(Path.Combine(store, Daemon.StaleTicketsName + ".json"), """["U.SPC-T1"]""");
        Assert.That(Gate(Asking("s2"), AgentGate.ContextScriptName).Output, Does.Contain("\"block\""), "its claim went stale: next hands it out again");
        // The daemon stopped (the dashboard may still keep rl-status.json fresh): its list stops being rewritten each minute.
        File.SetLastWriteTimeUtc(Path.Combine(store, Daemon.StaleTicketsName + ".json"), DateTime.UtcNow.AddMinutes(-10));
        Assert.That(Gate(Asking("s3"), AgentGate.ContextScriptName).Output, Is.Empty,
            "a list left by a daemon that stopped isn't trusted: the ticket may have been claimed afresh since");
    }

    [Test]
    public void At_claudes_own_line_with_the_daemon_down_keep_going_never_pushes_a_stop_back()
    {
        var db = Path.Combine(ws, "MindAttic.Tickets", "data", "Tickets.db");
        Directory.CreateDirectory(Path.GetDirectoryName(db)!);
        using (var mk = Process.Start(new ProcessStartInfo("python") { ArgumentList = { "-I", "-c",
            "import sqlite3,sys; d=sqlite3.connect(sys.argv[1]); d.execute('create table tickets(id,repo,status,ordinal,type)'); " +
            "d.execute(\"insert into tickets values('U.SPC-T1','UnitySolarSystem','open',1,'fix')\"); d.commit()", db }, UseShellExecute = false })!)
            mk.WaitForExit(20_000);
        var uss = Path.Combine(ws, "UnitySolarSystem");
        Directory.CreateDirectory(Marks);
        File.WriteAllText(Path.Combine(Marks, $"allocate-{Cli}"), "UnitySolarSystem");
        object Asking(string sid) => new { hook_event_name = "Stop", session_id = sid, cwd = uss, last_assistant_message = "Shall I start the next one?" };
        Status(new { }, at: DateTime.UtcNow.AddMinutes(-10)); // the daemon died
        Assert.That(Gate(Asking("k1"), AgentGate.ContextScriptName).Output, Does.Contain("\"block\""), "under the line: told to carry on");

        ClaudeReports(99);
        Assert.That(Gate(Asking("k2"), AgentGate.ContextScriptName).Output, Is.Empty, "at Claude's own 99% the stop is let stop: another turn is spent past it");
    }

    [Test]
    public void With_the_daemon_down_claudes_own_percent_still_stops_every_call_at_the_line()
    {
        // The orchestrator's file is 10 minutes old: its daemon died. Before M.ORCH-T167 that meant no pacing and no stop at all.
        Status(new { stop = false }, at: DateTime.UtcNow.AddMinutes(-10));
        var call = new { tool_name = "Read", tool_input = new { file_path = "x" } };

        ClaudeReports(99);
        using (var held = Run(AgentGate.ScriptName, call))
        {
            Assert.That(held.WaitForExit(7_000), Is.False, "Claude at 99% holds the call (then refuses it), daemon or not");
            held.Kill();
        }
        Assert.That(File.ReadAllText(Path.Combine(Claude, AgentGate.ScriptName)), Does.Contain("claude_stop_note(own, running=bool(s))"), "and refuses it saying why");

        ClaudeReports(50, resetUtc: DateTime.UtcNow.AddHours(2));
        File.Delete(Path.Combine(Marks, AgentGate.StopLatchName)); // the window it was seen in has reset
        Assert.That(Gate(call).Took, Is.LessThan(TimeSpan.FromSeconds(10)), "below the line it runs");
        ClaudeReports(99.5, resetUtc: DateTime.UtcNow.AddMinutes(-1));
        Assert.That(Gate(call).Took, Is.LessThan(TimeSpan.FromSeconds(10)), "a window that already reset doesn't stop anything");
    }

    [Test]
    public void The_weekly_stop_line_is_read_live_from_the_status_not_baked_into_the_hook()
    {
        // M.ORCH-T609: the week stops at 100 minus the weekly reserve (95% by default), read from the status the daemon writes.
        void Week(double percent) => File.WriteAllText(Path.Combine(Claude, "claude-usage.json"), JsonSerializer.Serialize(new
        {
            rate_limits = new
            {
                five_hour = new { used_percentage = 10, resets_at = new DateTimeOffset(DateTime.UtcNow.AddHours(2)).ToUnixTimeSeconds() },
                seven_day = new { used_percentage = percent, resets_at = new DateTimeOffset(DateTime.UtcNow.AddDays(3)).ToUnixTimeSeconds() },
            },
            at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        }));
        var read = new { tool_name = "Read", tool_input = new { file_path = "x" } };
        Status(new { stop = false, weekly_stop_percent = 98 });
        Week(96);
        Assert.That(Gate(read).Took, Is.LessThan(TimeSpan.FromSeconds(10)), "a 2% reserve: 96% of the week still runs");
        Status(new { stop = false });
        Assert.That(GateHeld(read, maxHold: 2).Output, Does.Contain("\"deny\"").And.Contain("96% of the weekly window used"),
            "no line written: the default 95% holds");
    }

    [Test]
    public void The_hooks_own_5_hour_stop_is_the_logins_cutoff_from_its_status_not_a_fixed_99()
    {
        // bughunt 2026-10-27: with the daemon down or stale every login ran to 99%, whatever its own cutoff (98% by default).
        File.WriteAllText(Path.Combine(Claude, "claude-usage.json"), JsonSerializer.Serialize(new
        {
            rate_limits = new { five_hour = new { used_percentage = 98.4, resets_at = new DateTimeOffset(DateTime.UtcNow.AddHours(2)).ToUnixTimeSeconds() } },
            at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        }));
        var read = new { tool_name = "Read", tool_input = new { file_path = "x" } };
        Status(new { stop = false });
        Assert.That(Gate(read).Took, Is.LessThan(TimeSpan.FromSeconds(10)), "no line written: the 99% fallback");
        Status(new { stop = false, five_hour_stop_percent = 98 }, DateTime.UtcNow.AddMinutes(-30));
        Assert.That(GateHeld(read, maxHold: 2).Output, Does.Contain("\"deny\"").And.Contain("5-hour window used"),
            "the login's 98% holds, from a status gone stale too");
    }

    [Test]
    public void A_login_allowed_past_its_weekly_target_runs_past_it_and_only_while_its_status_says_so()
    {
        // M.ORCH-T694: past the 90% target (a 10% reserve) the gate refuses; /org allow has the daemon write the wall (100) as
        // that login's weekly line, and the gate lets its calls through, the latch from the refusal included.
        var settings = Path.Combine(dir, "MindAttic", "MindAttic.Orchestrator", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
        File.WriteAllText(settings, JsonSerializer.Serialize(new { WorkspaceRoot = ws, WeeklyReservePercent = 10 }));
        File.WriteAllText(Path.Combine(Claude, "claude-usage.json"), JsonSerializer.Serialize(new
        {
            rate_limits = new
            {
                five_hour = new { used_percentage = 10, resets_at = new DateTimeOffset(DateTime.UtcNow.AddHours(2)).ToUnixTimeSeconds() },
                seven_day = new { used_percentage = 92, resets_at = new DateTimeOffset(DateTime.UtcNow.AddDays(3)).ToUnixTimeSeconds() },
            },
            at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        }));
        var read = new { tool_name = "Read", tool_input = new { file_path = "x" } };
        Status(new { stop = false, weekly_stop_percent = 90 });
        Assert.That(GateHeld(read, maxHold: 2).Output, Does.Contain("\"deny\"").And.Contain("92% of the weekly window used"), "held to the target");

        Status(new { stop = false, weekly_stop_percent = 100 });
        Assert.That(Gate(read).Took, Is.LessThan(TimeSpan.FromSeconds(10)), "allowed: it runs past the target");

        Status(new { stop = false, weekly_stop_percent = 90 });
        Assert.That(GateHeld(read, maxHold: 2).Output, Does.Contain("\"deny\""), "taken back (or its reset passed): held again");
    }

    [Test]
    public void With_weekly_aware_pacing_on_the_gate_stops_the_week_at_its_target_from_the_reserve_not_a_fixed_line()
    {
        // M.ORCH-T680: weekly-aware pacing's target is 100 minus the weekly reserve; the gate used a fixed 98% with it on.
        var settings = Path.Combine(dir, "MindAttic", "MindAttic.Orchestrator", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
        File.WriteAllText(settings, JsonSerializer.Serialize(new { WorkspaceRoot = ws, WeeklyAwarePacing = true, WeeklyReservePercent = 10 }));
        void Week(double percent) => File.WriteAllText(Path.Combine(Claude, "claude-usage.json"), JsonSerializer.Serialize(new
        {
            rate_limits = new
            {
                five_hour = new { used_percentage = 10, resets_at = new DateTimeOffset(DateTime.UtcNow.AddHours(2)).ToUnixTimeSeconds() },
                seven_day = new { used_percentage = percent, resets_at = new DateTimeOffset(DateTime.UtcNow.AddDays(3)).ToUnixTimeSeconds() },
            },
            at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        }));
        var read = new { tool_name = "Read", tool_input = new { file_path = "x" } };
        Status(new { stop = false });
        Week(89);
        Assert.That(Gate(read).Took, Is.LessThan(TimeSpan.FromSeconds(10)), "under the 90% target it runs");
        Week(91);
        Assert.That(GateHeld(read, maxHold: 2).Output, Does.Contain("\"deny\"").And.Contain("91% of the weekly window used"),
            "a 10% reserve stops the week at 90%, not 98%");
    }

    [Test]
    public void With_the_daemon_down_claudes_own_weekly_percent_at_the_line_stops_too()
    {
        // The week at 99% (its wall locks you out for days), the 5 hours at 50%, the daemon dead.
        Status(new { stop = false }, at: DateTime.UtcNow.AddMinutes(-10));
        var weekReset = DateTime.UtcNow.AddDays(3);
        File.WriteAllText(Path.Combine(Claude, "claude-usage.json"), JsonSerializer.Serialize(new
        {
            rate_limits = new
            {
                five_hour = new { used_percentage = 50, resets_at = new DateTimeOffset(DateTime.UtcNow.AddHours(2)).ToUnixTimeSeconds() },
                seven_day = new { used_percentage = 99, resets_at = new DateTimeOffset(weekReset).ToUnixTimeSeconds() },
            },
            at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        }));
        var refused = GateHeld(new { tool_name = "Read", tool_input = new { file_path = "x" } }, maxHold: 2).Output;
        Assert.That(refused, Does.Contain("\"deny\"").And.Contain("of the weekly window used").And.Contain("isn't running"));
        Status(new { stop = false });
        Assert.That(GateHeld(new { tool_name = "Read", tool_input = new { file_path = "x" } }, maxHold: 2).Output,
            Does.Contain("\"deny\"").And.Contain("of the weekly window used").And.Not.Contain("isn't running"),
            "the daemon is up (it just hasn't stopped yet): still refused at Claude's own line, but not said to be down");
        Status(new { stop = false }, at: DateTime.UtcNow.AddMinutes(-10));
        var prompt = Gate(new { hook_event_name = "UserPromptSubmit", session_id = "wk", prompt = "carry on", cwd = ws }, AgentGate.ContextScriptName).Output;
        Assert.That(prompt, Does.Contain("Not sent").And.Contain("the weekly window").And.Contain(weekReset.ToLocalTime().ToString("ddd", System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Test]
    public void After_a_login_to_another_account_the_old_accounts_weekly_stop_no_longer_holds_prompts()
    {
        // Signed in as A with A's week at 99%: the stop is latched, the daemon down.
        Status(new { stop = false }, at: DateTime.UtcNow.AddMinutes(-10));
        File.WriteAllText(Path.Combine(dir, ".claude.json"), """{"oauthAccount":{"accountUuid":"account-a"}}""");
        Directory.CreateDirectory(Marks);
        File.WriteAllText(Path.Combine(Marks, Budget.ClaudeAccount.FileName),
            $$"""{"account":"account-a","since":{{DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds()}}}""");
        File.WriteAllText(Path.Combine(Claude, "claude-usage.json"), JsonSerializer.Serialize(new
        {
            rate_limits = new { seven_day = new { used_percentage = 99, resets_at = new DateTimeOffset(DateTime.UtcNow.AddDays(3)).ToUnixTimeSeconds() } },
            at = DateTime.UtcNow.AddMinutes(-1).ToString("yyyy-MM-ddTHH:mm:ssZ"),
        }));
        var prompt = new { hook_event_name = "UserPromptSubmit", session_id = "acct", prompt = "hi", cwd = ws };
        Assert.That(Gate(prompt, AgentGate.ContextScriptName).Output, Does.Contain("Not sent"));

        // /login as B: A's report and latch are A's, and B hasn't reported yet.
        File.WriteAllText(Path.Combine(dir, ".claude.json"), """{"oauthAccount":{"accountUuid":"account-b"}}""");
        Assert.That(Gate(prompt, AgentGate.ContextScriptName).Output, Does.Not.Contain("Not sent"));
    }

    [Test]
    public void Claudes_own_stop_names_the_weekday_of_a_reset_tomorrow_under_a_day_off()
    {
        // M.ORCH-T407: "resets at 12:01 AM" reads as today's; it's tomorrow's, under 24 hours off.
        Status(new { stop = false }, at: DateTime.UtcNow.AddMinutes(-10));
        var reset = DateTime.Today.AddDays(1).AddMinutes(1);
        File.WriteAllText(Path.Combine(Claude, "claude-usage.json"), JsonSerializer.Serialize(new
        {
            rate_limits = new { seven_day = new { used_percentage = 99, resets_at = new DateTimeOffset(reset).ToUnixTimeSeconds() } },
            at = DateTime.UtcNow.AddMinutes(-1).ToString("yyyy-MM-ddTHH:mm:ssZ"),
        }));

        var said = Gate(new { hook_event_name = "UserPromptSubmit", session_id = "wk", prompt = "hi", cwd = ws }, AgentGate.ContextScriptName).Output;

        Assert.That(said, Does.Contain($"{reset.ToString("ddd", System.Globalization.CultureInfo.InvariantCulture)} 12:01 AM"));
    }

    [Test]
    public void The_images_pasted_into_a_prompt_are_kept_beside_the_ticket_database_and_the_agent_told_to_link_them()
    {
        // M.ORCH-T1117: Claude Code keeps a pasted image only in %TEMP%\claude\<cwd slug>\<session>\images\<N>.png.
        var temp = Path.Combine(dir, "tmp");
        var images = Path.Combine(temp, "claude", System.Text.RegularExpressions.Regex.Replace(ws, "[^A-Za-z0-9]", "-"), "img1", "images");
        Directory.CreateDirectory(images);
        File.WriteAllBytes(Path.Combine(images, "9.png"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(images, "8.png"), [4]); // not in this prompt: left alone
        Status(new { });
        var env = new Dictionary<string, string> { ["TMPDIR"] = temp, ["TEMP"] = temp, ["TMP"] = temp, ["CLAUDE_CODE_TMPDIR"] = "" };
        object Prompt(string prompt) => new { hook_event_name = "UserPromptSubmit", session_id = "img1", prompt, cwd = ws };

        var said = Gate(Prompt("this is broken [Image #9] and [Image #12]"), env, AgentGate.ContextScriptName).Output;

        var inbox = Path.Combine(ws, "MindAttic.Tickets", "data", "attachments", "_inbox", "img1");
        var kept = Path.Combine(inbox, DateTime.Now.ToString("yyyy-MM-dd") + "-image-9.png");
        Assert.That(File.ReadAllBytes(kept), Is.EqualTo(new byte[] { 1, 2, 3 }), "copied out of the session's temp folder");
        Assert.That(Directory.GetFiles(inbox), Has.Length.EqualTo(1), "only the prompt's own images, and #12 isn't there to copy");
        Assert.That(said, Does.Contain("[Images]").And.Contain(new Uri(kept).AbsoluteUri).And.Contain("tickets ticket note"));
        Assert.That(Gate(Prompt("no pictures here"), env, AgentGate.ContextScriptName).Output, Does.Not.Contain("[Images]"));
    }

    [Test]
    public void The_status_the_daemon_writes_in_the_second_of_a_switch_is_obeyed()
    {
        // M.ORCH-T325: rl-status.json's timestamp is cut to the second; the switch is dated to the fraction.
        var second = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var switched = (second.ToUnixTimeMilliseconds() + 700) / 1000.0;
        File.WriteAllText(Path.Combine(dir, ".claude.json"), """{"oauthAccount":{"accountUuid":"account-b"}}""");
        Directory.CreateDirectory(Marks);
        File.WriteAllText(Path.Combine(Marks, Budget.ClaudeAccount.FileName), $$"""
            {"account":"account-b","since":{{switched}},"spans":[{"account":"account-a","from":{{switched - 86400}},"to":{{switched}}},
            {"account":"account-b","from":{{switched}},"to":null}]}
            """);
        Status(new { pace_delay_seconds = 2 }, at: second.UtcDateTime);

        Assert.That(Gate(ReadCall).Took, Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(1.9)), "the new account's pace holds");
    }

    [Test]
    public void After_moving_to_an_api_key_the_claude_accounts_weekly_stop_no_longer_holds_prompts()
    {
        // Signed in to a Team seat with its week at 99% (recorded before the organization was keyed): the stop holds.
        Status(new { stop = false }, at: DateTime.UtcNow.AddMinutes(-10));
        File.WriteAllText(Path.Combine(dir, ".claude.json"), """{"oauthAccount":{"accountUuid":"account-a","organizationUuid":"team"}}""");
        Directory.CreateDirectory(Marks);
        File.WriteAllText(Path.Combine(Marks, Budget.ClaudeAccount.FileName),
            $$"""{"account":"account-a","since":{{DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds()}}}""");
        File.WriteAllText(Path.Combine(Claude, "claude-usage.json"), JsonSerializer.Serialize(new
        {
            rate_limits = new { seven_day = new { used_percentage = 99, resets_at = new DateTimeOffset(DateTime.UtcNow.AddDays(3)).ToUnixTimeSeconds() } },
            at = DateTime.UtcNow.AddMinutes(-1).ToString("yyyy-MM-ddTHH:mm:ssZ"),
        }));
        var prompt = new { hook_event_name = "UserPromptSubmit", session_id = "key", prompt = "hi", cwd = ws };
        Assert.That(Gate(prompt, AgentGate.ContextScriptName).Output, Does.Contain("Not sent"));

        // Logged out of Claude and running on an approved ANTHROPIC_API_KEY: the Team seat's week isn't the key's.
        File.WriteAllText(Path.Combine(dir, ".claude.json"), """{"customApiKeyResponses":{"approved":["key-tail"],"rejected":[]}}""");
        Assert.That(Gate(prompt, AgentGate.ContextScriptName).Output, Does.Not.Contain("Not sent"));
    }

    [Test]
    public void A_cli_records_its_process_from_its_first_prompt()
    {
        // An /allocate run's stops are all pushed back, so the Stop that used to record the pid never comes.
        Gate(new { hook_event_name = "UserPromptSubmit", session_id = "p1", prompt = "/allocate", cwd = ws }, AgentGate.ContextScriptName);
        Assert.That(File.ReadAllText(Path.Combine(Marks, "p1.state.json")), Does.Contain("\"pid\""));
    }

    [Test]
    public void With_the_daemon_down_you_are_told_and_a_typed_prompt_at_claudes_own_line_is_answered_unsent()
    {
        object Prompt(string prompt) => new { hook_event_name = "UserPromptSubmit", session_id = "d1", prompt, cwd = ws };
        Status(new { stop = false }, at: DateTime.UtcNow.AddMinutes(-12));

        ClaudeReports(40);
        var told = Gate(Prompt("carry on"), AgentGate.ContextScriptName).Output;
        Assert.That(told, Does.Contain("systemMessage").And.Contain("12 min").And.Contain("isn't running"));
        Assert.That(told, Does.Not.Contain("\"block\""));

        ClaudeReports(99);
        Assert.That(Gate(Prompt("fix the bug"), AgentGate.ContextScriptName).Output, Does.Contain("\"block\"").And.Contain("no tokens were spent"));

        Status(new { stop = false }); // the daemon is back
        ClaudeReports(40);
        Assert.That(Gate(Prompt("carry on"), AgentGate.ContextScriptName).Output, Does.Not.Contain("systemMessage"));
    }

    [Test]
    public void At_the_stop_a_typed_prompt_is_answered_at_once_without_reaching_the_model_but_control_commands_go_through()
    {
        object Prompt(string prompt) => new { hook_event_name = "UserPromptSubmit", session_id = "p1", prompt, cwd = ws };
        Status(new { stop = true, reset_time = "9:50 PM" });
        var blocked = Gate(Prompt("fix the login bug"), AgentGate.ContextScriptName).Output;
        Assert.That(blocked, Does.Contain("\"block\"").And.Contain("9:50 PM").And.Contain("no tokens were spent"));
        Assert.That(GateLog().Single(), Does.Contain("\"decision\": \"prompt-answered\""), "the answered prompt is on the pace ledger's record");
        Assert.That(Gate(Prompt("/allocate off"), AgentGate.ContextScriptName).Output, Does.Not.Contain("\"block\""));

        Status(new { });
        Assert.That(Gate(Prompt("fix the login bug"), AgentGate.ContextScriptName).Output, Does.Not.Contain("\"block\""), "under the target typing is never held");
    }

    [Test]
    public void The_end_of_every_turn_and_subagent_signals_the_daemon_to_recount_at_once()
    {
        var signal = Path.Combine(Marks, "refresh.now");
        Gate(new { hook_event_name = "Stop", session_id = "r1", cwd = ws }, AgentGate.ContextScriptName);
        Assert.That(File.Exists(signal), Is.True, "a reply with no tool calls is counted when its turn ends, not at the next tick");
        var first = File.GetLastWriteTimeUtc(signal);
        Thread.Sleep(50);
        Gate(new { hook_event_name = "SubagentStop", session_id = "r1", agent_id = "a9", cwd = ws }, AgentGate.ContextScriptName);
        Assert.That(File.GetLastWriteTimeUtc(signal), Is.GreaterThan(first));
    }

    [Test]
    public void An_idle_tab_redraws_its_statusline_on_a_timer_so_claudes_own_percent_stays_fresh()
    {
        File.WriteAllText(Path.Combine(Claude, "settings.json"), """{ "statusLine": { "type": "command", "command": "x.ps1" } }""");
        AgentGate.Ensure(Claude);
        var line = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(Claude, "settings.json")))!["statusLine"]!;
        Assert.That((line["command"]!.GetValue<string>(), line["refreshInterval"]!.GetValue<int>()), Is.EqualTo(("x.ps1", AgentGate.StatusLineRefreshSeconds)));
        Assert.That(AgentGate.Ensure(Claude), Is.Null, "set once");
    }

    [Test]
    public void A_stop_is_never_pushed_back_to_work()
    {
        var db = Path.Combine(ws, "MindAttic.Tickets", "data", "Tickets.db");
        Directory.CreateDirectory(Path.GetDirectoryName(db)!);
        using (var mk = Process.Start(new ProcessStartInfo("python") { ArgumentList = { "-I", "-c",
            "import sqlite3,sys; d=sqlite3.connect(sys.argv[1]); d.execute('create table tickets(id,repo,status,ordinal,type)'); " +
            "d.execute(\"insert into tickets values('U.SPC-T1','UnitySolarSystem','open',1,'fix')\"); d.commit()", db }, UseShellExecute = false })!)
            mk.WaitForExit(20_000);
        var uss = Path.Combine(ws, "UnitySolarSystem");
        Directory.CreateDirectory(Marks);
        File.WriteAllText(Path.Combine(Marks, $"allocate-{Cli}"), "UnitySolarSystem"); // keep-going acts only in the CLI /allocate is on in
        object Asking(string sid) => new { hook_event_name = "Stop", session_id = sid, cwd = uss, last_assistant_message = "Shall I start the next one?" };
        Status(new { });
        Assert.That(Gate(Asking("w3"), AgentGate.ContextScriptName).Output, Does.Contain("\"block\""), "open tickets: told to carry on");

        // Only a fix or an approved ticket is work to be pushed on to.
        using (var mk = Process.Start(new ProcessStartInfo("python") { ArgumentList = { "-I", "-c",
            "import sqlite3,sys; d=sqlite3.connect(sys.argv[1]); d.execute(\"update tickets set type='feature'\"); d.commit()", db }, UseShellExecute = false })!)
            mk.WaitForExit(20_000);
        Assert.That(Gate(Asking("w5"), AgentGate.ContextScriptName).Output, Is.Empty, "a feature you haven't approved isn't work");
        var settings = Path.Combine(dir, "MindAttic", "MindAttic.Orchestrator", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
        File.WriteAllText(settings, """{ "Approved": ["u.spc-t1"] }""");
        Assert.That(Gate(Asking("w7"), AgentGate.ContextScriptName).Output, Is.Empty,
            "settings.json's old approvals aren't read: approval lives in the database's queue (M.ORCH-T384)");
        using (var mk = Process.Start(new ProcessStartInfo("python") { ArgumentList = { "-I", "-c",
            "import sqlite3,sys; d=sqlite3.connect(sys.argv[1]); d.execute('create table ticket_queue(ticket_id,approval,priority,updated)'); " +
            "d.execute(\"insert into ticket_queue values('u.spc-t1','approved',0,'')\"); d.commit()", db }, UseShellExecute = false })!)
            mk.WaitForExit(20_000);
        Assert.That(Gate(Asking("w6"), AgentGate.ContextScriptName).Output, Does.Contain("\"block\""), "approved in the database (any case): it is");
        Status(new { stop = true });
        Assert.That(Gate(Asking("w4"), AgentGate.ContextScriptName).Output, Is.Empty, "at the target, a stop is let stop");
    }

    [Test]
    public void A_turn_that_ends_held_by_a_window_is_recorded_for_the_daemon_to_wake_and_a_new_prompt_clears_it()
    {
        JsonElement? Held(string sid)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Marks, sid + ".state.json")));
            return doc.RootElement.TryGetProperty("limit_stop", out var l) ? l.Clone() : null;
        }
        object Ends(string sid, string said) => new { hook_event_name = "Stop", session_id = sid, cwd = ws, last_assistant_message = said };
        var reset = DateTime.UtcNow.AddHours(2);
        var week = DateTime.UtcNow.AddDays(2);

        Status(new { stop = true, stop_window = "5-hour", stop_until = reset.ToString("yyyy-MM-ddTHH:mm:ssZ") });
        Gate(Ends("h1", "Stopped where the refusals began."), AgentGate.ContextScriptName);
        Assert.That(Held("h1")!.Value.GetProperty("until").GetDouble(), Is.EqualTo(Epoch(reset)).Within(1), "at the stop: held until its reset");
        Assert.That(Held("h1")!.Value.GetProperty("window").GetString(), Is.EqualTo("5-hour"));

        Status(new { stop = true, stop_window = "weekly", stop_until = week.ToString("yyyy-MM-ddTHH:mm:ssZ") });
        Gate(Ends("h2", "Stopped."), AgentGate.ContextScriptName);
        Assert.That(Held("h2")!.Value.GetProperty("window").GetString(), Is.EqualTo("weekly"), "a weekly stop is held until the week resets");

        Status(new { });
        Gate(Ends("h5", "Stopping until the limit resets."), AgentGate.ContextScriptName);
        Assert.That(Held("h5"), Is.Null, "nothing holds the window: not waiting on it");

        Gate(new { hook_event_name = "UserPromptSubmit", session_id = "h1", prompt = "carry on", cwd = ws }, AgentGate.ContextScriptName);
        Assert.That(Held("h1"), Is.Null, "a new prompt (the daemon's resume, or yours) is the CLI going on");
    }

    [Test]
    public void A_new_subagent_counts_the_daemons_live_subagents_not_marks_whose_stop_never_came()
    {
        var sub = Path.Combine(Marks, "subagents");
        Directory.CreateDirectory(sub);
        foreach (var dead in new[] { "d1", "d2", "d3" })
        {
            File.WriteAllText(Path.Combine(sub, dead), "");
            File.SetLastWriteTimeUtc(Path.Combine(sub, dead), DateTime.UtcNow.AddHours(-2));
        }
        Status(new { subagent_allowance = 1, subagents_running = 0 });
        var agent = new { tool_name = "Agent", tool_input = new { prompt = "x" } };
        Assert.That(Gate(agent).Took, Is.LessThan(TimeSpan.FromSeconds(10)), "the daemon says none run: three dead marks don't hold it");

        Status(new { subagent_allowance = 1, subagents_running = 0 }, at: DateTime.UtcNow.AddSeconds(-30));
        File.WriteAllText(Path.Combine(sub, "fresh"), ""); // started after the daemon's count
        using var held = Run(AgentGate.ScriptName, agent);
        Assert.That(held.WaitForExit(7_000), Is.False, "one started since the count fills the allowance of one");
        held.Kill();
    }

    /// <summary>M.ORCH-T320: marks were compared with the file's whole-second timestamp, which stays up to a minute old,
    /// so one the daemon had already counted was counted again and an Agent call held with a slot free.</summary>
    [Test]
    public void A_mark_the_daemons_count_saw_is_not_counted_again_though_newer_than_the_files_timestamp()
    {
        var sub = Path.Combine(Marks, "subagents");
        Directory.CreateDirectory(sub);
        var counted = Path.Combine(sub, "counted");
        File.WriteAllText(counted, "");
        var at = DateTime.UtcNow.AddSeconds(-30);
        File.SetLastWriteTimeUtc(counted, at.AddSeconds(10)); // started after the file's timestamp, before the count
        long Ns(string f) => (File.GetLastWriteTimeUtc(f).Ticks - DateTime.UnixEpoch.Ticks) * 100;
        Status(new { subagent_allowance = 2, subagents_running = 1, subagents_seen_through_ns = Ns(counted) }, at: at);
        var agent = new { tool_name = "Agent", tool_input = new { prompt = "x" } };
        Assert.That(Gate(agent).Took, Is.LessThan(TimeSpan.FromSeconds(10)), "the one running is counted once: a slot is free");

        foreach (var f in Directory.GetFiles(Path.Combine(Marks, AgentGate.SlotDirName))) File.Delete(f);
        File.WriteAllText(Path.Combine(sub, "fresh"), ""); // started after the count
        using var held = Run(AgentGate.ScriptName, agent);
        Assert.That(held.WaitForExit(7_000), Is.False, "one started since the count fills the allowance of two");
        held.Kill();
    }

    [Test]
    public void Agent_calls_sent_together_take_the_allowance_one_slot_at_a_time()
    {
        // M.ORCH-T197: a message of Agent calls ran the check together before any SubagentStart, so all got through.
        Status(new { subagent_allowance = 1, subagents_running = 0 });
        var agent = new { tool_name = "Agent", tool_input = new { prompt = "x" } };
        var burst = Enumerable.Range(0, 4).Select(_ => Run(AgentGate.ScriptName, agent)).ToList();
        try
        {
            Thread.Sleep(8_000);
            Assert.That(burst.Count(p => p.HasExited), Is.EqualTo(1), "one free place lets one call through, the rest wait");
            var slots = Path.Combine(Marks, AgentGate.SlotDirName);
            Assert.That(Directory.GetFiles(slots), Has.Length.EqualTo(1), "the call let through holds its slot until it starts");

            // Its SubagentStart turns the slot into the subagent's mark, which the count then sees instead.
            Gate(new { hook_event_name = "SubagentStart", session_id = "s5", agent_id = "a1", cwd = ws }, AgentGate.ContextScriptName);
            Assert.That(Directory.GetFiles(slots), Is.Empty);
            Assert.That(File.Exists(Path.Combine(Marks, "subagents", "a1")), Is.True);
            Thread.Sleep(7_000);
            Assert.That(burst.Count(p => p.HasExited), Is.EqualTo(1), "the started subagent fills the allowance just as its slot did");
        }
        finally
        {
            foreach (var p in burst) { if (!p.HasExited) p.Kill(); p.Dispose(); }
        }
    }

    /// <summary>M.ORCH-T201: past MAX_HOLD a held subagent used to start anyway, so on 2026-10-17 seven ran on an allowance of one.</summary>
    [Test]
    public void A_subagent_still_over_the_allowance_past_max_hold_is_refused_not_started()
    {
        Status(new { subagent_allowance = 1, subagents_running = 1 });
        var agent = new { tool_name = "Agent", tool_input = new { prompt = "x" } };
        var (output, took) = GateHeld(agent, 2);
        Assert.That(output, Does.Contain("\"deny\"").And.Contain("allowance is full").And.Contain("slot frees"), "refused, told to try again later");
        Assert.That(took, Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(1.9)).And.LessThan(TimeSpan.FromSeconds(20)), "held for MAX_HOLD first");
        Assert.That(GateLog().Last(), Does.Contain("\"decision\": \"refused\"").And.Contain("\"tool\": \"Agent\""));
        var slots = Path.Combine(Marks, AgentGate.SlotDirName);
        Assert.That(Directory.Exists(slots) ? Directory.GetFiles(slots) : [], Is.Empty, "no slot taken for a call that never ran");

        Status(new { subagent_allowance = 2, subagents_running = 1 });
        Assert.That(GateHeld(agent, 2).Output, Is.Empty, "with room it starts at once");
    }

    [Test]
    public void A_slot_whose_subagent_never_started_expires_and_frees_its_place()
    {
        Status(new { subagent_allowance = 1, subagents_running = 0 });
        var slots = Path.Combine(Marks, AgentGate.SlotDirName);
        Directory.CreateDirectory(slots);
        var refused = Path.Combine(slots, "4242-1-1");
        File.WriteAllText(refused, "");
        var agent = new { tool_name = "Agent", tool_input = new { prompt = "x" } };
        using (var held = Run(AgentGate.ScriptName, agent))
        {
            Assert.That(held.WaitForExit(7_000), Is.False, "a fresh slot fills the allowance of one");
            held.Kill();
        }

        File.SetLastWriteTimeUtc(refused, DateTime.UtcNow.AddSeconds(-AgentGate.SlotLifeSeconds - 30));
        Assert.That(Gate(agent).Took, Is.LessThan(TimeSpan.FromSeconds(10)), "its call never ran: the slot is dropped");
        Assert.That(File.Exists(refused), Is.False);
        Assert.That(Directory.GetFiles(slots), Has.Length.EqualTo(1), "and the call let through holds its own");
    }

    [Test]
    public void A_slot_held_by_a_pace_delay_stays_fresh_so_it_never_expires_while_its_call_waits()
    {
        Status(new { subagent_allowance = 4, subagents_running = 0, pace_delay_seconds = 60 });
        var slots = Path.Combine(Marks, AgentGate.SlotDirName);
        using var held = Run(AgentGate.ScriptName, new { tool_name = "Agent", tool_input = new { prompt = "x" } });
        try
        {
            Assert.That(held.WaitForExit(2_000), Is.False, "held by the pace delay");
            var slot = Directory.GetFiles(slots).Single();
            File.SetLastWriteTimeUtc(slot, DateTime.UtcNow.AddSeconds(-AgentGate.SlotLifeSeconds + 5));
            Thread.Sleep(6_000);
            Assert.That(DateTime.UtcNow - File.GetLastWriteTimeUtc(slot), Is.LessThan(TimeSpan.FromSeconds(10)),
                "a call still held past the slot's life keeps its slot; another gate would otherwise drop it");
        }
        finally { held.Kill(); }
    }

    /// <summary>M.ORCH-T495: one repo's /allocate took all 8 places at once while the other CLIs got none.</summary>
    [Test]
    public void A_cli_at_its_fair_share_is_held_while_another_cli_waits_and_one_under_its_share_goes_first()
    {
        var wants = Path.Combine(Marks, AgentGate.WantDirName);
        Directory.CreateDirectory(wants);
        var other = Path.Combine(wants, "999-1");
        File.WriteAllText(other, ""); // CLI 999 has an Agent call waiting
        var agent = new { tool_name = "Agent", tool_input = new { prompt = "x" } };
        Status(new { subagent_allowance = 4, subagents_running = 2, subagents_seen_through_ns = 0, subagents_by_cli = new Dictionary<string, int> { [Cli.ToString()] = 2 } });
        using (var held = Run(AgentGate.ScriptName, agent))
        {
            Assert.That(held.WaitForExit(4_000), Is.False, "2 of 4 is this CLI's share while CLI 999 waits, though 2 places are free");
            Assert.That(Directory.GetFiles(wants).Select(Path.GetFileName), Has.Some.StartsWith(Cli + "-"), "and it waits its turn too");
            held.Kill();
        }
        foreach (var f in Directory.GetFiles(wants, Cli + "-*")) File.Delete(f); // the killed call's
        var (output, _) = GateHeld(agent, 2);
        Assert.That(output, Does.Contain("\"deny\"").And.Contain("fair share"));
        Assert.That(Directory.GetFiles(wants).Select(Path.GetFileName), Has.None.StartsWith(Cli + "-"), "a refused call no longer waits");

        File.SetLastWriteTimeUtc(other, DateTime.UtcNow.AddSeconds(-AgentGate.WantLifeSeconds - 10));
        Assert.That(Gate(agent).Took, Is.LessThan(TimeSpan.FromSeconds(10)), "nobody else wants one: this CLI borrows a free place");

        // The other way round: CLI 999 is over its share and waits for more; this CLI, under its share, goes at once.
        foreach (var f in Directory.GetFiles(Path.Combine(Marks, AgentGate.SlotDirName))) File.Delete(f);
        File.WriteAllText(other, "");
        Status(new { subagent_allowance = 4, subagents_running = 3, subagents_seen_through_ns = 0, subagents_by_cli = new Dictionary<string, int> { ["999"] = 3 } });
        Assert.That(Gate(agent).Took, Is.LessThan(TimeSpan.FromSeconds(10)));
    }

    /// <summary>M.ORCH-T558: the first CLI to turn /allocate on filled every place before the CLIs opened just after it could.</summary>
    [Test]
    public void Allocate_warms_up_one_more_subagent_each_minute_since_it_turned_on_and_keeps_that_time_when_its_repos_change()
    {
        var path = MindAttic.Orchestrator.Settings.OrchestratorDb.DefaultPath();
        new MindAttic.Orchestrator.Settings.OrchestratorDb(path).Open().Dispose();
        double? Since()
        {
            using var connection = new MindAttic.Orchestrator.Settings.OrchestratorDb(path).Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT since FROM cli_marks WHERE kind = 'allocate' AND pid = {Cli};";
            return command.ExecuteScalar() is double d ? d : null;
        }
        void Shift(int seconds)
        {
            using var connection = new MindAttic.Orchestrator.Settings.OrchestratorDb(path).Open();
            MindAttic.Orchestrator.Settings.OrchestratorDb.Execute(connection, $"UPDATE cli_marks SET since = since - {seconds};");
        }
        object Prompt(string prompt) => new { hook_event_name = "UserPromptSubmit", session_id = "s1", prompt, cwd = ws };
        Gate(Prompt("/allocate -repo=a"), AgentGate.ContextScriptName);
        var on = Since();
        Assert.That(on, Is.Not.Null.And.EqualTo((DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds).Within(30), "when it turned on, in its row");
        Thread.Sleep(1_100);
        Gate(Prompt("/allocate -repo=b"), AgentGate.ContextScriptName);
        Assert.That(Since(), Is.EqualTo(on), "repos added: still when it turned on");

        var agent = new { tool_name = "Agent", tool_input = new { prompt = "x" } };
        Status(new { subagent_allowance = 8, subagents_running = 1, subagents_seen_through_ns = 0, subagents_by_cli = new Dictionary<string, int> { [Cli.ToString()] = 1 } });
        var (output, _) = GateHeld(agent, 2);
        Assert.That(output, Does.Contain("\"deny\"").And.Contain("warming up"), "minute 0: one, though 7 places are free");
        Shift(150);
        Assert.That(GateHeld(agent, 2).Output, Is.Empty, "two and a half minutes on: up to 3");

        Gate(Prompt("/allocate off"), AgentGate.ContextScriptName);
        Gate(Prompt("/allocate -repo=a"), AgentGate.ContextScriptName);
        Assert.That(Since(), Is.GreaterThan(on!.Value - 10), "off and on again: it warms up from the start");
    }

    /// <summary>M.ORCH-T558: a freed place goes to the waiting CLI furthest under its share, not whichever asks first.</summary>
    [Test]
    public void A_free_place_goes_to_the_waiting_cli_furthest_under_its_share()
    {
        var wants = Path.Combine(Marks, AgentGate.WantDirName);
        Directory.CreateDirectory(wants);
        File.WriteAllText(Path.Combine(wants, "999-1"), ""); // CLI 999 waits with none running
        var agent = new { tool_name = "Agent", tool_input = new { prompt = "x" } };
        // 6 allowed across 3 CLIs (a share of 2): one place free. This CLI has 1, CLI 999 none: 999 is further under.
        Status(new { subagent_allowance = 6, subagents_running = 5, subagents_seen_through_ns = 0,
            subagents_by_cli = new Dictionary<string, int> { [Cli.ToString()] = 1, ["777"] = 4 } });
        var (output, _) = GateHeld(agent, 2);
        Assert.That(output, Does.Contain("\"deny\"").And.Contain("further under its fair share"));

        // Both one under: the tie goes the same way in every gate (the lower process id first), so one of them takes it.
        foreach (var f in Directory.GetFiles(wants, Cli + "-*")) File.Delete(f);
        Status(new { subagent_allowance = 6, subagents_running = 5, subagents_seen_through_ns = 0,
            subagents_by_cli = new Dictionary<string, int> { [Cli.ToString()] = 1, ["999"] = 1, ["777"] = 3 } });
        Assert.That(GateHeld(agent, 2).Output, Is.Empty, "4242 goes before 999");
    }

    [Test]
    public void A_subagent_start_never_frees_another_clis_slot()
    {
        var slots = Path.Combine(Marks, AgentGate.SlotDirName);
        Directory.CreateDirectory(slots);
        var other = Path.Combine(slots, "999-1-1");
        File.WriteAllText(other, "");

        // This CLI took no slot (it's /unthrottle'd, or the call was exempt): another CLI's pending call keeps its place.
        Gate(new { hook_event_name = "SubagentStart", session_id = "s6", agent_id = "a2", cwd = ws }, AgentGate.ContextScriptName);
        Assert.That(File.Exists(other), Is.True);

        File.WriteAllText(Path.Combine(slots, $"{Cli}-2-2"), "");
        Gate(new { hook_event_name = "SubagentStart", session_id = "s6", agent_id = "a3", cwd = ws }, AgentGate.ContextScriptName);
        Assert.That(Directory.GetFiles(slots).Select(Path.GetFileName), Is.EqualTo(new[] { "999-1-1" }), "its own slot is the one let go");
    }

    [Test]
    public void Filing_a_ticket_for_another_repo_hands_it_off_but_closing_one_is_still_refused()
    {
        var uss = Path.Combine(ws, "UnitySolarSystem");
        object Bash(string command) => new { tool_name = "Bash", tool_input = new { command }, cwd = uss };
        Assert.That(Gate(Bash("tickets ticket new --repo MindAttic.Tickets --type chore --summary 'bring it up to date'")).Output, Is.Empty,
            "the edit refusal itself says to file a ticket for the other repo");
        Assert.That(Gate(Bash("tickets ticket close M.ORCH-T1 --repo MindAttic.Tickets --ref x")).Output, Does.Contain("\"deny\""));
        Assert.That(Gate(Bash("tickets ticket correct M.ORCH-T1 --repo MindAttic.Tickets --summary x")).Output, Does.Contain("\"deny\""));
    }

    /// <summary>M.ORCH-T565: `tickets repo rename` moves every ticket to a repo's new name right away, but its folder on
    /// disk may not be renamed yet; the window gate must still recognize its own tickets by the rename history.</summary>
    [Test]
    public void A_renamed_repos_window_still_works_its_own_tickets_by_either_name()
    {
        Tickets(("U.SYS-T1", "UnitySystem"), ("M.ORCH-T1", "MindAttic.Tickets"));
        RepoRenames(("UnitySolarSystem", "UnitySystem")); // the folder is still named UnitySolarSystem (not renamed yet)
        var uss = Path.Combine(ws, "UnitySolarSystem");
        object Bash(string command) => new { tool_name = "Bash", tool_input = new { command }, cwd = uss };
        Assert.That(Gate(Bash("tickets ticket close U.SYS-T1 --ref x")).Output, Is.Empty,
            "UnitySystem is this folder's new name: still its own ticket");
        Assert.That(Gate(Bash("tickets ticket correct U.SYS-T1 --summary x")).Output, Is.Empty);
        Assert.That(Gate(Bash("tickets ticket close M.ORCH-T1 --ref x")).Output, Does.Contain("\"deny\""),
            "a genuinely different repo is still refused");

        var file = Path.Combine(ws, "UnitySystem", "src", "x.cs"); // UnitySystem's own worktree folder, say
        Directory.CreateDirectory(Path.Combine(ws, "UnitySystem", ".git"));
        Assert.That(Gate(new { tool_name = "Edit", tool_input = new { file_path = file }, cwd = uss }).Output, Is.Empty,
            "a folder already named UnitySystem is the same repo as the UnitySolarSystem window sits in");
    }

    [Test]
    public void A_push_to_master_is_refused_now_that_every_repo_lands_on_main()
    {
        object Bash(string command) => new { tool_name = "Bash", tool_input = new { command } };
        foreach (var c in new[] { "git push origin master", "git -C ../MindAttic.Ideas push origin master", "git push origin HEAD:master",
                                  "cd x && git push -u origin master", "git push origin master:master", "git -c core.x=1 push origin master",
                                  // M.ORCH-T361: refspec forms that recreate it just the same
                                  "git push origin HEAD:refs/heads/master", "git push origin +master", "git push origin \"master\"", "git push origin 'HEAD:master'" })
            Assert.That(Gate(Bash(c)).Output, Does.Contain("\"deny\"").And.Contain("HEAD:main"), c);
        foreach (var c in new[] { "git push origin main", "git push origin HEAD:main", "git log master..main", "git push origin --delete master",
                                  "echo 'git push origin master' > notes.txt", "git push origin master-fix", "git push origin HEAD:master.old",
                                  "git commit -m \"Never push master: land on main\"", "git -C x commit -m 'the push guard keeps master out'" })
            Assert.That(Gate(Bash(c)).Output, Does.Not.Contain("main line is main"), c);
    }

    [Test]
    public void A_heredocs_body_is_not_read_as_a_search_but_a_search_on_a_later_line_is()
    {
        object Bash(string command) => new { tool_name = "Bash", tool_input = new { command } };
        const string heredoc = "cd /d/x; python - <<'EOF'\nwith open(DIR / \"a.txt\") as f:\n    pass\nEOF\ndotnet test | grep -E \"Passed\" | head";
        Assert.That(Gate(Bash(heredoc)).Output, Is.Empty, "open(DIR / x) inside a heredoc is Python, not `dir /`");
        Assert.That(Gate(new { tool_name = "PowerShell", tool_input = new { command = "$s = @'\nopen(DIR / x)\n'@\nWrite-Output $s" } }).Output, Is.Empty);
        Assert.That(Gate(Bash("cd /d/x\nfind / -name foo")).Output, Does.Contain("\"deny\""), "the second line is a command too");
        Assert.That(Gate(Bash("cat <<EOF > x.txt\nhi\nEOF\nfind / -name foo")).Output, Does.Contain("\"deny\""), "after the heredoc ends");
    }

    [Test]
    public void PowerShell_recursion_is_caught_in_any_case_but_bash_ls_r_only_reverses()
    {
        object Ps(string command) => new { tool_name = "PowerShell", tool_input = new { command } };
        foreach (var c in new[] { "Get-ChildItem C:\\ -recurse -Filter x", "gci C:\\ -r", "ls C:\\ -Recurse", "dir C:\\ /s" })
            Assert.That(Gate(Ps(c)).Output, Does.Contain("\"deny\""), c);
        Assert.That(Gate(Ps("Get-ChildItem C:\\")).Output, Is.Empty, "listing a root isn't a search");
        Assert.That(Gate(new { tool_name = "Bash", tool_input = new { command = "ls -r /" } }).Output, Is.Empty, "bash ls -r is reverse order");
        Assert.That(Gate(new { tool_name = "Bash", tool_input = new { command = "ls -R /" } }).Output, Does.Contain("\"deny\""));
    }

    [Test]
    public void A_whole_drive_walk_wrapped_in_cmd_or_powershell_or_through_where_is_refused()
    {
        // M.ORCH-T362: only a command at the start of a line was looked at.
        object Bash(string command) => new { tool_name = "Bash", tool_input = new { command } };
        foreach (var c in new[] { "cmd /c dir /s /b C:\\foo.exe", "cmd /c \"dir C:\\ /s /b\"", "powershell -c \"Get-ChildItem C:\\ -Recurse\"",
                                  "powershell -NoProfile -Command \"gci C:\\ -r -Filter x\"", "where /r C:\\ python.exe", "where.exe /R \"D:\\\" x" })
            Assert.That(Gate(Bash(c)).Output, Does.Contain("\"deny\""), c);
        foreach (var c in new[] { "where python", "where /r . python.exe", "cmd /c dir C:\\", "powershell -c \"Get-ChildItem C:\\Users\\me -Recurse\"",
                                  "powershell -NoProfile -File run.ps1" })
            Assert.That(Gate(Bash(c)).Output, Does.Not.Contain("\"deny\""), c);
    }

    [Test]
    public void A_switch_or_a_search_pattern_that_looks_like_a_root_is_not_a_whole_drive_search()
    {
        // M.ORCH-T364: find.exe's /i, dir's /s with a file pattern and grep's "/" were read as roots to walk.
        object Bash(string command) => new { tool_name = "Bash", tool_input = new { command } };
        object Ps(string command) => new { tool_name = "PowerShell", tool_input = new { command } };
        foreach (var c in new[] { "tasklist | find /i \"claude\"", "Get-Process | Out-String | find /c \"dotnet\"", "wmic logicaldisk get caption | find \"C:\"",
                                  "cmd /c dir /s /b *.csproj", "cmd /c \"dir /b /s *.sln\"" })
            Assert.That(Gate(Ps(c)).Output, Does.Not.Contain("\"deny\""), c);
        foreach (var c in new[] { "git ls-files | grep -v /", "git branch -r | grep \"/\"", "rg -l \"D:\" src", "grep -rn \"~\" docs" })
            Assert.That(Gate(Bash(c)).Output, Does.Not.Contain("\"deny\""), c);
        foreach (var c in new[] { "grep -r TODO /", "rg foo C:\\", "grep -e x -r /c", "find /c -name x.exe" })
            Assert.That(Gate(Bash(c)).Output, Does.Contain("\"deny\""), c);
        Assert.That(Gate(Ps("cmd /c dir /s /b C:\\foo.exe")).Output, Does.Contain("\"deny\""), "looks for foo.exe in every folder of C:");
    }

    [Test]
    public void Allocate_takes_repo_names_or_wildcards_and_its_repos_are_this_clis_cross_repo_exception()
    {
        var mark = Path.Combine(Marks, $"allocate-{Cli}");
        object Prompt(string prompt) => new { hook_event_name = "UserPromptSubmit", session_id = "s1", prompt, cwd = ws };
        Gate(Prompt("/allocate -repo=a,b on"), AgentGate.ContextScriptName);
        Assert.That(File.ReadAllText(mark), Is.EqualTo("a,b"));
        Assert.That(Gate(Prompt("/allocate -repos=MindAttic.Tickets"), AgentGate.ContextScriptName).Output,
            Does.Contain("\"block\"").And.Contain("-repos is gone"), "the -repos alias is turned away, not guessed at");
        Assert.That(File.ReadAllText(mark), Is.EqualTo("a,b"), "and changes nothing");

        // Its subagents edit and close tickets in the repos it's working, though this CLI sits in another repo.
        var uss = Path.Combine(ws, "UnitySolarSystem");
        var file = Path.Combine(ws, "MindAttic.Tickets", "src", "x.cs");
        Assert.That(Gate(new { tool_name = "Edit", tool_input = new { file_path = file }, cwd = uss }).Output, Does.Contain("\"deny\""), "not named yet");
        Gate(Prompt("/allocate -repo=MINDATTIC.*"), AgentGate.ContextScriptName);
        Assert.That(File.ReadAllText(mark), Is.EqualTo("a,b,MINDATTIC.*"), "a wildcard is kept as typed");
        Assert.That(Gate(new { tool_name = "Edit", tool_input = new { file_path = file }, cwd = uss }).Output, Is.Empty, "MindAttic.* names MindAttic.Tickets");
        Assert.That(Gate(new { tool_name = "Edit", tool_input = new { file_path = Path.Combine(uss, "x.cs") }, cwd = Path.Combine(ws, "MindAttic.Tickets") }).Output,
            Does.Contain("\"deny\""), "but not UnitySolarSystem");
        Gate(Prompt("/allocate off"), AgentGate.ContextScriptName);
        Assert.That(File.Exists(mark), Is.False);
        Assert.That(Gate(new { tool_name = "Edit", tool_input = new { file_path = file }, cwd = uss }).Output, Does.Contain("\"deny\""), "off: back to its own repo");
    }

    // ---- /crossrepo and /allocate through the Skill tool (M.ORCH-T435): only when the user's own prompt asked ----

    private object Said(string prompt) => new { hook_event_name = "UserPromptSubmit", session_id = "sk", prompt, cwd = Uss };
    private object SkillCall(string skill, string args = "") => new { hook_event_name = "PreToolUse", session_id = "sk", tool_name = "Skill", tool_input = new { skill, args }, cwd = Uss };
    private object EditIn(string repo) => new { tool_name = "Edit", tool_input = new { file_path = Path.Combine(ws, repo, "src", "x.cs") }, session_id = "sk", cwd = Uss };

    [Test]
    public void A_crossrepo_skill_call_the_user_asked_for_is_recorded_as_typed_and_the_guard_lets_its_edits_through()
    {
        Gate(Said("use cross-repo for MindAttic.Tickets and fix the parser there"), AgentGate.ContextScriptName);
        Assert.That(Gate(SkillCall("crossrepo", "MindAttic.Tickets")).Output, Does.Not.Contain("\"deny\""));
        Assert.That(File.ReadAllText(Path.Combine(Marks, $"crossrepo-{Cli}")), Is.EqualTo("MindAttic.Tickets"));
        Assert.That(Gate(EditIn("MindAttic.Tickets")).Output, Is.Empty, "the guard reads the same mark");
    }

    [Test]
    public void A_crossrepo_skill_call_the_user_never_asked_for_is_refused_and_records_nothing()
    {
        Gate(Said("fix the parser in MindAttic.Tickets"), AgentGate.ContextScriptName);
        var refused = Gate(SkillCall("crossrepo", "MindAttic.Tickets")).Output;
        Assert.That(refused, Does.Contain("\"deny\"").And.Contain("/crossrepo <repos>"));
        Assert.That(File.Exists(Path.Combine(Marks, $"crossrepo-{Cli}")), Is.False);
        Assert.That(Gate(EditIn("MindAttic.Tickets")).Output, Does.Contain("\"deny\"").And.Contain("This window is for UnitySolarSystem"));

        // A plugin-style name is the same skill, and asking once doesn't carry over to the next prompt.
        Gate(Said("crossrepo please"), AgentGate.ContextScriptName);
        Gate(Said("now carry on"), AgentGate.ContextScriptName);
        Assert.That(Gate(SkillCall("mindattic:crossrepo", "all")).Output, Does.Contain("\"deny\""), "the note is cleared at the next prompt");
        Assert.That(File.Exists(Path.Combine(Marks, $"crossrepo-{Cli}")), Is.False);
    }

    // ---- Blocked on you (M.ORCH-T790): the tab's red flag reads the context gate's asking mark ----

    private string? Asking(string sid)
    {
        var file = Path.Combine(Marks, sid + ".state.json");
        if (!File.Exists(file)) return null;
        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        return doc.RootElement.TryGetProperty("asking", out var a) ? a.GetString() : null;
    }

    [Test]
    public void A_permission_prompt_marks_the_cli_blocked_on_you_until_its_tool_runs()
    {
        Turn("bk", "go");
        Gate(new { hook_event_name = "Notification", session_id = "bk", cwd = Uss, notification_type = "idle_prompt", message = "Claude is waiting for your input" },
            AgentGate.ContextScriptName);
        Assert.That(Asking("bk"), Is.Null, "a turn that simply ended isn't blocked on you");

        Gate(new { hook_event_name = "Notification", session_id = "bk", cwd = Uss, notification_type = "permission_prompt", message = "Claude needs your permission to use Bash" },
            AgentGate.ContextScriptName);
        Assert.That(Asking("bk"), Is.Not.Null, "holding on a permission prompt");
        Ran("bk", "dotnet build");
        Assert.That(Asking("bk"), Is.Null, "answered: its tool ran, so it's going again");

        Gate(new { hook_event_name = "Notification", session_id = "bk", cwd = Uss, message = "Claude needs your permission to use Edit" }, AgentGate.ContextScriptName);
        Assert.That(Asking("bk"), Is.Not.Null, "an older Claude Code sends no notification_type: the message says it");
        Turn("bk", "no, do it differently");
        Assert.That(Asking("bk"), Is.Null, "your next prompt answers it");
    }

    [Test]
    public void An_ask_user_question_marks_the_cli_blocked_and_its_stop_clears_it()
    {
        Turn("bq", "go");
        Gate(new { hook_event_name = "PreToolUse", session_id = "bq", cwd = Uss, tool_name = AgentGate.AskUserQuestionTool, tool_input = new { } },
            AgentGate.ContextScriptName);
        Assert.That(Asking("bq"), Is.Not.Null, "waiting on your answer to its question");
        Stopped("bq", "Stopped.");
        Assert.That(Asking("bq"), Is.Null, "a turn that ends isn't holding on a prompt");
    }

    [Test]
    public void A_crossrepo_the_model_was_refused_leaves_the_cli_blocked_on_you_at_its_stop_until_you_type()
    {
        Gate(Said("fix the parser in MindAttic.Tickets"), AgentGate.ContextScriptName);
        Assert.That(Gate(SkillCall("crossrepo", "MindAttic.Tickets")).Output, Does.Contain("\"deny\""));
        Assert.That(Asking("sk"), Is.Null, "still mid-turn");
        Stopped("sk", "Please type /crossrepo MindAttic.Tickets so I can work there.");
        Assert.That(Asking("sk"), Is.Not.Null, "stopped waiting on you to type /crossrepo");
        Gate(Said("/crossrepo MindAttic.Tickets"), AgentGate.ContextScriptName);
        Assert.That(Asking("sk"), Is.Null, "typed: it's running again");
        Stopped("sk", "Done.");
        Assert.That(Asking("sk"), Is.Null, "the refusal is only flagged once");
    }

    [Test]
    public void The_context_gate_hears_permission_prompts_and_only_the_ask_user_question_tool()
    {
        var hooks = JsonDocument.Parse(File.ReadAllText(Path.Combine(Claude, "settings.json"))).RootElement.GetProperty("hooks");
        Assert.That(hooks.GetProperty("Notification").GetRawText(), Does.Contain(AgentGate.ContextScriptName));
        var pre = hooks.GetProperty("PreToolUse").EnumerateArray()
            .Single(e => e.GetProperty("hooks").GetRawText().Contains(AgentGate.ContextScriptName));
        Assert.That(pre.GetProperty("matcher").GetString(), Is.EqualTo(AgentGate.AskUserQuestionTool), "no other tool call starts it");
    }

    [Test]
    public void An_allocate_skill_call_is_recorded_only_when_the_user_asked_for_allocate()
    {
        var mark = Path.Combine(Marks, $"allocate-{Cli}");
        Gate(Said("work the Tickets tickets"), AgentGate.ContextScriptName);
        // The refusal names the command the user would type, /allocate.
        Assert.That(Gate(SkillCall("allocate", "-repo=MindAttic.Tickets")).Output, Does.Contain("\"deny\"").And.Contain("/allocate"));
        Assert.That(File.Exists(mark), Is.False);
        Assert.That(Gate(EditIn("MindAttic.Tickets")).Output, Does.Contain("\"deny\""));

        Gate(Said("turn auto pilot on for MindAttic.Tickets"), AgentGate.ContextScriptName);
        Assert.That(Gate(SkillCall("allocate", "-repo=MindAttic.Tickets")).Output, Does.Not.Contain("\"deny\""));
        Assert.That(File.ReadAllText(mark), Is.EqualTo("MindAttic.Tickets"));
        Assert.That(Gate(EditIn("MindAttic.Tickets")).Output, Is.Empty, "its repos are this CLI's exception, as typed /allocate's are");
        Assert.That(Gate(SkillCall("allocate", "-repos=X")).Output, Does.Contain("\"deny\"").And.Contain("-repos is gone"), "the same rules as typed");
        Assert.That(File.ReadAllText(mark), Is.EqualTo("MindAttic.Tickets"));
    }

    [Test]
    public void Switching_crossrepo_or_allocate_off_through_the_skill_tool_always_goes_through()
    {
        Directory.CreateDirectory(Marks);
        File.WriteAllText(Path.Combine(Marks, $"crossrepo-{Cli}"), "all");
        File.WriteAllText(Path.Combine(Marks, $"allocate-{Cli}"), "UnitySolarSystem");
        Gate(Said("that's enough for today"), AgentGate.ContextScriptName);
        Assert.That(Gate(SkillCall("crossrepo", "off")).Output, Does.Not.Contain("\"deny\""));
        Assert.That(Gate(SkillCall("allocate", "off")).Output, Does.Not.Contain("\"deny\""));
        Assert.That(File.Exists(Path.Combine(Marks, $"crossrepo-{Cli}")), Is.False);
        Assert.That(File.Exists(Path.Combine(Marks, $"allocate-{Cli}")), Is.False);
        Assert.That(Gate(EditIn("MindAttic.Tickets")).Output, Does.Contain("\"deny\""));
    }

    [Test]
    public void A_typed_crossrepo_still_marks_this_cli_and_off_ends_it()
    {
        var mark = Path.Combine(Marks, $"crossrepo-{Cli}");
        Gate(Said("/crossrepo MindAttic.Tickets"), AgentGate.ContextScriptName);
        Assert.That(File.ReadAllText(mark), Is.EqualTo("MindAttic.Tickets"));
        Assert.That(Gate(EditIn("MindAttic.Tickets")).Output, Is.Empty);
        Gate(Said("/crossrepo off"), AgentGate.ContextScriptName);
        Assert.That(File.Exists(mark), Is.False);
        Assert.That(Gate(EditIn("MindAttic.Tickets")).Output, Does.Contain("\"deny\""));
    }

    [Test]
    public void A_typed_crossrepo_accepts_the_repo_form_and_trims_punctuation_per_name()
    {
        var mark = Path.Combine(Marks, $"crossrepo-{Cli}");
        Directory.CreateDirectory(Path.Combine(ws, "Unity.Shared", ".git"));
        Gate(Said("/crossrepo -repo=Unity.Shared,MindAttic.Tickets."), AgentGate.ContextScriptName);
        Assert.That(File.ReadAllText(mark), Is.EqualTo("Unity.Shared,MindAttic.Tickets"));
        Assert.That(Gate(EditIn("MindAttic.Tickets")).Output, Is.Empty);
        Assert.That(Gate(EditIn("Unity.Shared")).Output, Is.Empty);
        Gate(Said("/crossrepo off"), AgentGate.ContextScriptName);
        Gate(Said("/crossrepo  MindAttic.Tickets. "), AgentGate.ContextScriptName);
        Assert.That(File.ReadAllText(mark), Is.EqualTo("MindAttic.Tickets"));
    }

    [Test]
    public void A_typed_crossrepo_confirmation_names_only_known_repos_and_warns_about_the_rest()
    {
        var output = Gate(Said("/crossrepo -repo=MindAttic.Tickets,NoSuchRepo"), AgentGate.ContextScriptName).Output;
        Assert.That(output, Does.Contain("may work MindAttic.Tickets.").And.Contain("NoSuchRepo; edits there stay refused"));
    }

    // ---- /pacing on|off replaces /unthrottle and /throttle (M.ORCH-T483) ----

    [Test]
    public void A_new_cli_is_paced_by_default_and_a_bare_pacing_says_so_unsent()
    {
        var status = Gate(Said("/pacing"), AgentGate.ContextScriptName).Output;
        Assert.That(status, Does.Contain("\"block\"").And.Contain("Pacing is on in this CLI (the default)"), "answered by the hook, not sent");
        Assert.That(File.Exists(Path.Combine(Marks, $"unthrottled-{Cli}")), Is.False, "asking changes nothing");
        Status(new { pace_delay_seconds = 3 });
        Assert.That(Gate(ReadCall).Took, Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(2.9)), "a new CLI is held to the pace");
    }

    [Test]
    public void Pacing_off_lifts_this_clis_holds_until_pacing_on_and_a_bare_pacing_reports_each()
    {
        var mark = Path.Combine(Marks, $"unthrottled-{Cli}");
        Assert.That(Gate(Said("/pacing off"), AgentGate.ContextScriptName).Output, Does.Not.Contain("\"block\""), "on/off go through to be confirmed");
        Assert.That(File.Exists(mark), Is.True);
        Assert.That(Gate(Said("/pacing"), AgentGate.ContextScriptName).Output, Does.Contain("\"block\"").And.Contain("Pacing is off in this CLI"));
        Status(new { pace_delay_seconds = 60 });
        Assert.That(Gate(ReadCall).Took, Is.LessThan(TimeSpan.FromSeconds(10)), "off: no pace hold");

        Gate(Said("/pacing on"), AgentGate.ContextScriptName);
        Assert.That(File.Exists(mark), Is.False);
        Assert.That(Gate(Said("/pacing"), AgentGate.ContextScriptName).Output, Does.Contain("Pacing is on in this CLI"));
        Status(new { pace_delay_seconds = 3 });
        Assert.That(Gate(ReadCall).Took, Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(2.9)), "on again: held to the pace");
    }

    [Test]
    public void Pacing_off_still_stops_at_the_target_and_an_unknown_argument_changes_nothing()
    {
        Gate(Said("/pacing off"), AgentGate.ContextScriptName);
        Status(new { stop = true, pace_delay_seconds = 60 });
        Assert.That(GateHeld(ReadCall, 2).Output, Does.Contain("\"deny\""), "the stop holds and refuses it all the same");
        Assert.That(Gate(Said("/pacing on"), AgentGate.ContextScriptName).Output, Does.Not.Contain("no tokens were spent"), "typed at the stop, it still goes through");
        Assert.That(File.Exists(Path.Combine(Marks, $"unthrottled-{Cli}")), Is.False);

        Assert.That(Gate(Said("/pacing maybe"), AgentGate.ContextScriptName).Output, Does.Contain("/pacing takes on or off; nothing changed"));
        Assert.That(File.Exists(Path.Combine(Marks, $"unthrottled-{Cli}")), Is.False);
    }

    [Test]
    public void The_retired_throttle_commands_do_nothing()
    {
        Gate(Said("/unthrottle"), AgentGate.ContextScriptName);
        Assert.That(File.Exists(Path.Combine(Marks, $"unthrottled-{Cli}")), Is.False, "/unthrottle is gone, with no redirect");
        Assert.That(File.Exists(Path.Combine(Claude, "commands", "unthrottle.md")) || File.Exists(Path.Combine(Claude, "commands", "throttle.md")), Is.False);
        Assert.That(File.Exists(Path.Combine(Claude, "commands", "pacing.md")), Is.True);
    }

    [Test]
    public void A_turn_hears_the_subagent_note_only_from_a_fresh_orchestrator_file()
    {
        object Prompt() => new { hook_event_name = "UserPromptSubmit", session_id = "s2", prompt = "hi", cwd = ws };
        Status(new { subagent_note = "Subagents: up to 3." });
        Assert.That(Gate(Prompt(), AgentGate.ContextScriptName).Output, Does.Contain("Subagents: up to 3."));
        Status(new { subagent_note = "Subagents: up to 3." }, at: DateTime.UtcNow.AddMinutes(-10));
        var stale = Gate(Prompt(), AgentGate.ContextScriptName).Output;
        Assert.That(stale, Does.Not.Contain("Subagents: up to 3."), "a stale note isn't passed on");
        Assert.That(stale, Does.Contain("isn't running"), "the stale file is reported as the daemon being down (M.ORCH-T167)");
    }

    [Test]
    public void A_full_context_never_tells_a_turn_to_stop()
    {
        // Nothing quicksaves, clears or reloads a session for its context any more, so nothing tells it to wrap up either.
        Directory.CreateDirectory(Marks);
        File.WriteAllText(Path.Combine(Marks, "s3.json"), """{ "used_percentage": 95 }""");
        Gate(new { hook_event_name = "UserPromptSubmit", session_id = "s3", prompt = "go", cwd = ws }, AgentGate.ContextScriptName);
        Assert.That(Gate(new { hook_event_name = "PostToolUse", session_id = "s3", cwd = ws, tool_input = new { command = "ls" } },
            AgentGate.ContextScriptName).Output, Is.Empty);
    }

    [Test]
    public void Subagent_start_and_stop_marks_come_and_go()
    {
        object Event(string e) => new { hook_event_name = e, session_id = "s4", agent_id = "agent/1", cwd = ws };
        Gate(Event("SubagentStart"), AgentGate.ContextScriptName);
        Assert.That(File.Exists(Path.Combine(Marks, "subagents", "agent_1")), Is.True, "the id is made a safe file name");
        Gate(Event("SubagentStop"), AgentGate.ContextScriptName);
        Assert.That(File.Exists(Path.Combine(Marks, "subagents", "agent_1")), Is.False);

        Gate(Event("SubagentStart"), new Dictionary<string, string> { ["ANTHROPIC_AUTH_TOKEN"] = "gateway-token" }, AgentGate.ContextScriptName);
        Assert.That(File.Exists(Path.Combine(Marks, "subagents", "agent_1")), Is.False, "a gateway CLI's subagent doesn't fill the subscription's allowance");

        // M.ORCH-T506: the mark names its CLI as the pace gate does (MINDATTIC_CLI_PID first), or it counts as another CLI's.
        Gate(Event("SubagentStart"), new Dictionary<string, string> { ["MINDATTIC_CLI_PID"] = "4242" }, AgentGate.ContextScriptName);
        Assert.That(File.ReadAllText(Path.Combine(Marks, "subagents", "agent_1")), Is.EqualTo("4242"));
    }

    [Test]
    public void A_gate_entry_with_a_stale_or_odd_shaped_timeout_is_repaired_not_a_crash()
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse("""
            { "hooks": { "PreToolUse": [ { "matcher": "", "hooks": [ { "type": "command", "command": "python pace-gate.py", "timeout": "330" } ] } ] } }
            """)!.AsObject();
        Assert.That(AgentGate.AddHook(root, "PreToolUse", AgentGate.ScriptName, "python \"x/pace-gate.py\"", 330), Is.True);
        var hook = root["hooks"]!["PreToolUse"]![0]!["hooks"]![0]!;
        Assert.That((hook["command"]!.GetValue<string>(), hook["timeout"]!.GetValue<int>()), Is.EqualTo(("python \"x/pace-gate.py\"", 330)));
        Assert.That(AgentGate.AddHook(root, "PreToolUse", AgentGate.ScriptName, "python \"x/pace-gate.py\"", 330), Is.False, "now in place");

        var odd = System.Text.Json.Nodes.JsonNode.Parse("""{ "hooks": { "Stop": [ { "hooks": [ { "command": 42 } ] } ] } }""")!.AsObject();
        Assert.That(AgentGate.AddHook(odd, "Stop", AgentGate.ContextScriptName, "python c.py", 10), Is.True, "a non-string command is someone else's: ours is added beside it");
    }

    // ---- keep-going (M.ORCH-T140): what context-gate.py does when an /allocate turn stops ----

    private string Uss => Path.Combine(ws, "UnitySolarSystem");
    private string KeepGoingLog => Path.Combine(Marks, "keep-going.jsonl");

    /// <summary>Two open fixes for UnitySolarSystem in the ticket database, and /allocate on for it in this CLI.</summary>
    private void AllocateWithOpenFixes()
    {
        var db = Path.Combine(ws, "MindAttic.Tickets", "data", "Tickets.db");
        Directory.CreateDirectory(Path.GetDirectoryName(db)!);
        using (var mk = Process.Start(new ProcessStartInfo("python") { ArgumentList = { "-I", "-c",
            "import sqlite3,sys; d=sqlite3.connect(sys.argv[1]); d.execute('create table tickets(id,repo,status,ordinal,type)'); " +
            "d.execute(\"insert into tickets values('U.SPC-T1','UnitySolarSystem','open',1,'fix')\"); " +
            "d.execute(\"insert into tickets values('U.SPC-T2','UnitySolarSystem','open',2,'fix')\"); d.commit()", db }, UseShellExecute = false })!)
            Assert.That(mk.WaitForExit(20_000), Is.True);
        Directory.CreateDirectory(Marks);
        File.WriteAllText(Path.Combine(Marks, $"allocate-{Cli}"), "UnitySolarSystem");
    }

    private string Turn(string sid, string prompt, string? transcript = null) =>
        Gate(new { hook_event_name = "UserPromptSubmit", session_id = sid, prompt, cwd = Uss, transcript_path = transcript }, AgentGate.ContextScriptName).Output;

    private string Ran(string sid, string command) =>
        Gate(new { hook_event_name = "PostToolUse", session_id = sid, cwd = Uss, tool_name = "Bash", tool_input = new { command } }, AgentGate.ContextScriptName).Output;

    private string Stopped(string sid, string said = "Done: U.SPC-T1 is closed.") =>
        Gate(new { hook_event_name = "Stop", session_id = sid, cwd = Uss, last_assistant_message = said }, AgentGate.ContextScriptName).Output;

    private JsonElement[] KeepGoingLines() =>
        File.Exists(KeepGoingLog) ? File.ReadAllLines(KeepGoingLog).Select(l => JsonDocument.Parse(l).RootElement).ToArray() : [];

    private static string Reason(string output) => JsonDocument.Parse(output).RootElement.GetProperty("reason").GetString()!;

    [Test]
    public void A_turn_that_closed_a_ticket_and_stopped_is_nudged_and_a_second_stop_with_no_work_between_is_let_go()
    {
        AllocateWithOpenFixes();
        Status(new { });
        Turn("k1", "go");
        Ran("k1", "tickets ticket close U.SPC-T1 --ref abc123");
        var state = Path.Combine(Marks, "k1.state.json");
        Assert.That(File.ReadAllText(state), Does.Contain("U.SPC-T1"), "the close is remembered for the stop");

        var nudge = Stopped("k1");
        Assert.That(nudge, Does.Contain("\"decision\": \"block\""));
        Assert.That(Reason(nudge), Does.StartWith("Don't stop").And.Contain("2 workable tickets remain (U.SPC-T1, U.SPC-T2)")
            .And.Contain("orchestrator next --repos \"UnitySolarSystem\" --claim"));
        Assert.That(File.ReadAllText(state), Does.Contain("\"nudged\": true").And.Contain("\"worked\": false").And.Not.Contain("closed"));

        Assert.That(Stopped("k1"), Is.Empty, "stopped again straight after with nothing done: something really needs you");
        Assert.That(File.ReadAllText(state), Does.Not.Contain("nudged").And.Contain("\"state\": \"idle\""), "and the next stop starts fresh");

        var lines = KeepGoingLines();
        Assert.That(lines.Select(l => l.GetProperty("action").GetString()), Is.EqualTo(new[] { "kept going", "let stop" }));
        Assert.That(lines.Select(l => (l.GetProperty("repo").GetString(), l.GetProperty("session").GetString(), l.GetProperty("provider").GetString(),
            l.GetProperty("why").GetString(), l.GetProperty("open").GetInt32())), Is.All.EqualTo(("UnitySolarSystem", "k1", "Claude", "allocate", 2)));

        // Nudged, then carried on (any tool call counts as work): its next stop is nudged again rather than let go.
        Assert.That(Stopped("k1"), Does.Contain("\"block\""));
        Ran("k1", "dotnet test");
        Assert.That(Stopped("k1"), Does.Contain("\"block\""), "it worked after the nudge, so stopping again isn't insisting");
        Assert.That(KeepGoingLines().Select(l => l.GetProperty("action").GetString()), Is.EqualTo(new[] { "kept going", "let stop", "kept going", "kept going" }));
    }

    [Test]
    public void A_stop_is_not_pushed_back_when_the_subagent_allowance_is_full_or_every_ticket_is_claimed()
    {
        // M.ORCH-T188: `orchestrator next --claim` hands out nothing then, so pushing back only looped.
        AllocateWithOpenFixes();
        Status(new { subagent_allowance = 2, subagents_running = 2 });
        Assert.That(Stopped("c1"), Is.Empty, "the allowance is full: next would hand out nothing");
        Status(new { subagent_allowance = 3, subagents_running = 2 });
        Assert.That(Reason(Stopped("c2")), Does.Contain("2 workable tickets remain"), "room for one more: kept going");

        var assignments = Path.Combine(dir, "MindAttic", "MindAttic.Orchestrator", "assignments.json");
        Directory.CreateDirectory(Path.GetDirectoryName(assignments)!);
        File.WriteAllText(assignments, """[ { "TicketId": "U.SPC-T1", "Repo": "UnitySolarSystem" } ]""");
        Assert.That(Reason(Stopped("c3")), Does.Contain("1 workable ticket remain (U.SPC-T2)"), "a claimed ticket is being worked, not left");
        File.WriteAllText(assignments, """[ { "TicketId": "U.SPC-T1" }, { "TicketId": "u.spc-t2" } ]""");
        Assert.That(Stopped("c4"), Is.Empty, "every ticket is claimed by a running subagent");

        // Running `orchestrator next` after a nudge isn't work: when it hands out nothing, the next stop is let go.
        File.WriteAllText(assignments, "[]");
        Turn("c5", "go");
        Assert.That(Stopped("c5"), Does.Contain("\"block\""));
        Ran("c5", "orchestrator next --repos \"UnitySolarSystem\" --claim");
        Assert.That(Stopped("c5"), Is.Empty, "asking for work isn't doing it, so the insisted stop goes through");
    }

    [Test]
    public void A_goal_turn_is_not_nudged_until_goal_clear_or_its_stop_summary_ends_the_goal()
    {
        AllocateWithOpenFixes();
        Status(new { });
        Turn("g1", "/goal all the tests pass");
        Assert.That(Stopped("g1"), Is.Empty, "a /goal runs turn after turn on its own: keep-going leaves it alone");
        Assert.That(KeepGoingLines(), Is.Empty, "and logs nothing, since it never considered it");

        Turn("g1", "/goal clear");
        Assert.That(Stopped("g1"), Does.Contain("\"block\""), "/goal clear ends the goal");

        // A goal that finished on its own: the transcript's last entry is the stop summary naming it.
        var transcript = Path.Combine(dir, "g2.jsonl");
        File.WriteAllText(transcript, "");
        Turn("g2", "/goal ship it", transcript);
        Assert.That(Stopped("g2"), Is.Empty);
        File.WriteAllLines(transcript, [
            JsonSerializer.Serialize(new { type = "assistant", message = new { content = new[] { new { type = "text", text = "Shipped." } } } }),
            JsonSerializer.Serialize(new { type = "system", subtype = "stop_hook_summary", hookInfos = new[] { new { promptText = "something else" } } }),
        ]);
        Turn("g2", "go");
        Assert.That(Stopped("g2"), Is.Empty, "a summary naming another goal doesn't end this one");
        File.AppendAllLines(transcript, [
            JsonSerializer.Serialize(new { type = "system", subtype = "stop_hook_summary", hookInfos = new[] { new { promptText = "ship it" } } }),
            "not json",
        ]);
        Turn("g2", "go");
        Assert.That(File.ReadAllText(Path.Combine(Marks, "g2.state.json")), Does.Not.Contain("ship it"), "goal_finished: the goal is gone");
        Assert.That(Stopped("g2"), Does.Contain("\"block\""), "so its stops are kept going again");
    }

    [Test]
    public void An_allocate_list_outside_the_ansi_code_page_is_read_as_the_utf8_it_is_written_in()
    {
        // M.ORCH-T400: the marks are written as UTF-8 now; read in the ANSI code page, "Łódź" (0x81 in its bytes) failed the read,
        // and /allocate read as off in that CLI. (The write takes a claude.exe ancestor, which a test doesn't have.)
        AllocateWithOpenFixes();
        Status(new { });
        File.WriteAllText(Path.Combine(Marks, $"allocate-{Cli}"), "UnitySolarSystem,Łódź", new System.Text.UTF8Encoding(false));

        Assert.That(Stopped("x1"), Does.Contain("\"block\""), "still on /allocate for UnitySolarSystem");
    }

    [Test]
    [TestCase("MINDATTIC_TICKETS_DB")]
    [TestCase("MINDATTIC_WORKFLOW_DB")] // its name before MindAttic.Workflow became MindAttic.Tickets (M.ORCH-T564)
    public void Keep_going_reads_the_ticket_database_the_env_var_names(string variable)
    {
        // M.ORCH-T398: as the orchestrator and the tickets CLI do; the workspace's own (two open fixes) is not the one in use.
        AllocateWithOpenFixes();
        Status(new { });
        var elsewhere = Path.Combine(dir, "elsewhere.db");
        using (var mk = Process.Start(new ProcessStartInfo("python") { ArgumentList = { "-I", "-c",
            "import sqlite3,sys; d=sqlite3.connect(sys.argv[1]); d.execute('create table tickets(id,repo,status,ordinal,type)'); d.commit()", elsewhere },
            UseShellExecute = false })!)
            Assert.That(mk.WaitForExit(20_000), Is.True);
        var env = new Dictionary<string, string> { [variable] = elsewhere };
        Gate(new { hook_event_name = "UserPromptSubmit", session_id = "w1", prompt = "go", cwd = Uss }, env, AgentGate.ContextScriptName);

        var stop = Gate(new { hook_event_name = "Stop", session_id = "w1", cwd = Uss, last_assistant_message = "Done." }, env, AgentGate.ContextScriptName).Output;

        Assert.That(stop, Does.Not.Contain("\"block\""), "no open ticket in the database in use: nothing to keep going on");
        Assert.That(Stopped("w2"), Does.Contain("\"block\""), "without it, the workspace's database and its open fixes");
    }

    [Test]
    public void A_goal_with_a_non_ascii_character_is_kept_as_typed_so_its_stop_summary_ends_it()
    {
        // Read in the ANSI code page, "café" was kept garbled and never matched the summary, so the goal never ended (M.ORCH-T392).
        AllocateWithOpenFixes();
        Status(new { });
        var transcript = Path.Combine(dir, "g3.jsonl");
        File.WriteAllText(transcript, "");
        Turn("g3", "/goal fix the café page", transcript);
        using (var state = JsonDocument.Parse(File.ReadAllText(Path.Combine(Marks, "g3.state.json"))))
            Assert.That(state.RootElement.GetProperty("goal").GetString(), Is.EqualTo("fix the café page"));

        File.WriteAllLines(transcript, [
            JsonSerializer.Serialize(new { type = "system", subtype = "stop_hook_summary", hookInfos = new[] { new { promptText = "fix the café page" } } }),
        ], new System.Text.UTF8Encoding(false));
        Turn("g3", "go");
        Assert.That(Stopped("g3"), Does.Contain("\"block\""), "the goal ended, so its stops are kept going again");
    }

    [Test]
    public void A_codex_stop_or_gemini_after_agent_is_never_pushed_onto_tickets()
    {
        // The installer hooks both up to context-gate.py (codex / gemini on the command line), but /allocate runs in a
        // Claude CLI: theirs are let stop, with nothing logged and no session state written (84d9b91).
        AllocateWithOpenFixes();
        Status(new { });
        Assert.That(Gate(new { hook_event_name = "Stop", session_id = "c1", cwd = Uss, last_assistant_message = "Shall I go on?" },
            AgentGate.ContextScriptName, "codex").Output, Is.Empty);
        Assert.That(Gate(new { hook_event_name = "AfterAgent", session_id = "m1", cwd = Uss, prompt_response = "Shall I go on?" },
            AgentGate.ContextScriptName, "gemini").Output, Is.Empty);
        Assert.That(KeepGoingLines(), Is.Empty);
        Assert.That(Directory.GetFiles(Marks, "*.state.json"), Is.Empty);
        Assert.That(Stopped("c2", "Shall I go on?"), Does.Contain("\"block\""), "the same stop from the Claude CLI is");
    }

    [Test]
    public void A_hold_ends_early_when_rl_status_is_lowered_mid_wait()
    {
        Status(new { pace_delay_seconds = 60 });
        var (output, took) = GateWhile(ReadCall, TimeSpan.FromSeconds(2), () => Status(new { pace_delay_seconds = 0 }));
        Assert.That(output, Is.Empty, "let through, not refused");
        Assert.That(took, Is.LessThan(TimeSpan.FromSeconds(15)), "the file is re-read while waiting: a 60s hold lowered to 0 ends within a poll");
        Assert.That(GateLog().Single(), Does.Contain("\"decision\": \"held\""), "the part it did wait is on the record");
    }

    [Test]
    public void A_stop_lifted_mid_wait_lets_the_call_through_and_one_that_lasts_past_max_hold_is_refused()
    {
        Status(new { stop = true, stop_note = "At the target until 9:50 PM. End your turn now." });
        var (output, took) = GateWhile(ReadCall, TimeSpan.FromSeconds(2), () => Status(new { stop = false }));
        Assert.That(output, Is.Empty, "lifted mid-wait: let through");
        Assert.That(took, Is.LessThan(TimeSpan.FromSeconds(15)));
        Assert.That(GateLog(), Has.None.Contains("refused"));

        Status(new { stop = true, stop_note = "At the target until 9:50 PM. End your turn now." });
        var (refused, longest) = GateHeld(ReadCall, 2);
        Assert.That(refused, Does.Contain("\"deny\"").And.Contain("9:50 PM"), "still at the stop past the longest hold: refused with the daemon's note");
        Assert.That(longest, Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(1.9)).And.LessThan(TimeSpan.FromSeconds(15)), "held for MAX_HOLD first");
        Assert.That(GateLog().Last(), Does.Contain("\"decision\": \"refused\""));
    }

    /// <summary>The window of 2026-10-17 04:58Z (M.ORCH-T196): after the 99% stop, an idle session's statusline put Claude's
    /// percent back to 85% once a minute, and the daemon stalled with its stop in a file that went stale; each time, every
    /// call held at the stop went through. A stop once seen holds until its window resets.</summary>
    [Test]
    public void A_stop_seen_once_holds_until_the_reset_whatever_a_later_report_or_a_stalled_daemon_says()
    {
        Status(new { stop = false }, at: DateTime.UtcNow.AddMinutes(-10)); // the daemon has stalled
        ClaudeReports(99);
        Assert.That(GateHeld(ReadCall, 2).Output, Does.Contain("\"deny\""), "Claude at 99%: refused");
        ClaudeReports(85); // an idle session's last response, stamped now, for the same window
        Assert.That(GateHeld(ReadCall, 2).Output, Does.Contain("\"deny\""), "a lower report for the same window doesn't lift it");
        ClaudeReports(10, resetUtc: DateTime.UtcNow.AddHours(5));
        File.Delete(Path.Combine(Marks, AgentGate.StopLatchName));
        Assert.That(GateHeld(ReadCall, 2).Output, Is.Empty, "a new window starts clear");

        Status(new { stop = true, stop_note = "At the target until 9:50 PM. End your turn now.", stop_until = DateTime.UtcNow.AddHours(1).ToString("yyyy-MM-ddTHH:mm:ssZ") });
        Assert.That(GateHeld(ReadCall, 2).Output, Does.Contain("\"deny\""), "the orchestrator's stop: refused");
        Status(new { stop = true, stop_note = "At the target until 9:50 PM. End your turn now." }, at: DateTime.UtcNow.AddMinutes(-10));
        Assert.That(GateHeld(ReadCall, 2).Output, Does.Contain("\"deny\"").And.Contain("9:50 PM"), "its daemon stalled and the file went stale: still refused");
        File.WriteAllText(Path.Combine(Claude, "rl-status.json"), "{ half written");
        Assert.That(GateHeld(ReadCall, 2).Output, Does.Contain("\"deny\""), "an unreadable file doesn't lift it either");

        Status(new { stop = false });
        Assert.That(GateHeld(ReadCall, 2).Output, Is.Empty, "the orchestrator lifting it, fresh, does");
        Status(new { stop = false }, at: DateTime.UtcNow.AddMinutes(-10));
        Assert.That(GateHeld(ReadCall, 2).Output, Is.Empty, "and the lifted stop stays lifted once the file is stale");
    }

    [Test]
    public void Claudes_weekly_99_percent_and_its_latch_end_at_the_weeks_reset()
    {
        var reset = DateTime.UtcNow.AddSeconds(10); // past the first refusal, which waits out one 5-second recheck
        void Week(double percent) => File.WriteAllText(Path.Combine(Claude, "claude-usage.json"), JsonSerializer.Serialize(new
        {
            rate_limits = new
            {
                five_hour = new { used_percentage = 20, resets_at = new DateTimeOffset(DateTime.UtcNow.AddHours(3)).ToUnixTimeSeconds() },
                seven_day = new { used_percentage = percent, resets_at = new DateTimeOffset(reset).ToUnixTimeSeconds() },
            },
            at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        }));
        Status(new { stop = false }, at: DateTime.UtcNow.AddMinutes(-10)); // the daemon is down: Claude's own percent decides
        Week(99);
        Assert.That(GateHeld(ReadCall, 1).Output, Does.Contain("\"deny\"").And.Contain("weekly"));
        Thread.Sleep(Math.Max(0, (int)(reset - DateTime.UtcNow).TotalMilliseconds) + 1500);
        // Past the reset, the statusline still restamps the old week's 99% until a reply brings the new one.
        Week(99);
        Assert.That(GateHeld(ReadCall, 1).Output, Is.Empty, "neither the reading nor the claude-week latch outlives the week");
    }

    [Test]
    public void A_fresh_status_from_before_its_window_reset_holds_nothing_after_it()
    {
        string At(int seconds) => DateTime.UtcNow.AddSeconds(seconds).ToString("yyyy-MM-ddTHH:mm:ssZ");
        object Weekly(int reset) => new
        {
            stop = true, stop_window = "weekly", stop_note = "The weekly window is at its target. End your turn now.", stop_until = At(reset), window_reset = At(reset),
            pace_delay_seconds = 60, subagent_allowance = 0, subagents_running = 0,
        };
        // 6:59:50: the weekly stop until 7:00, latched.
        Status(Weekly(10));
        Assert.That(GateHeld(ReadCall, 2).Output, Does.Contain("\"deny\""));

        // 7:00:10, before the daemon's next write: the file is still fresh, but everything in it was the old week's.
        Status(Weekly(-10));
        var (output, took) = GateHeld(ReadCall, 10);
        Assert.That(output, Is.Empty, "no stop, and no hold at the old pace");
        Assert.That(took, Is.LessThan(TimeSpan.FromSeconds(8)));
        (output, took) = GateHeld(new { tool_name = "Agent", tool_input = new { prompt = "x" } }, 10);
        Assert.That(output, Is.Empty, "the old window's subagent allowance holds no new one");
        Assert.That(took, Is.LessThan(TimeSpan.FromSeconds(8)));
        Assert.That(Gate(new { hook_event_name = "UserPromptSubmit", session_id = "r1", prompt = "go on", cwd = ws }, AgentGate.ContextScriptName).Output,
            Does.Not.Contain("Not sent"));
        var latch = Path.Combine(Marks, AgentGate.StopLatchName);
        Assert.That(File.Exists(latch) ? File.ReadAllText(latch) : "", Does.Not.Contain("orchestrator@"), "the latch ended at its until");
    }

    [Test]
    public void A_stop_that_comes_on_while_a_call_is_held_for_the_pace_refuses_it_rather_than_letting_it_through()
    {
        Status(new { pace_delay_seconds = 60 });
        var (output, took) = GateWhile(ReadCall, TimeSpan.FromSeconds(2),
            () => Status(new { stop = true, pace_delay_seconds = 60, stop_note = "At the target until 9:50 PM. End your turn now." }), maxHold: 5);
        Assert.That(output, Does.Contain("\"deny\"").And.Contain("9:50 PM"), "its pace wait ended at the stop: refused, not let through");
        Assert.That(took, Is.LessThan(TimeSpan.FromSeconds(20)));
        Assert.That(GateLog().Last(), Does.Contain("\"decision\": \"refused\""));
    }

    [Test]
    public void A_stop_that_comes_on_while_a_subagent_waits_for_a_slot_refuses_it_and_gives_the_slot_back()
    {
        var agent = new { tool_name = "Agent", tool_input = new { prompt = "x" } };
        Status(new { subagent_allowance = 0, subagents_running = 0 });
        var (output, _) = GateWhile(agent, TimeSpan.FromSeconds(2),
            () => Status(new { stop = true, subagent_allowance = 0, subagents_running = 0 }), maxHold: 5);
        Assert.That(output, Does.Contain("\"deny\""), "past its wait at the stop: refused, never started over the line");
        var slots = Path.Combine(Marks, AgentGate.SlotDirName);
        Assert.That(Directory.Exists(slots) ? Directory.GetFiles(slots) : [], Is.Empty, "no slot left behind for a call that never ran");
    }

    [Test]
    public void An_unthrottled_cli_skips_the_pace_delay_but_not_the_stop()
    {
        Directory.CreateDirectory(Marks);
        File.WriteAllText(Path.Combine(Marks, $"unthrottled-{Cli}"), "x");
        Status(new { pace_delay_seconds = 60 });
        Assert.That(Gate(ReadCall).Took, Is.LessThan(TimeSpan.FromSeconds(10)), "this CLI is exempt from the pace hold");

        File.Delete(Path.Combine(Marks, $"unthrottled-{Cli}"));
        File.WriteAllText(Path.Combine(Marks, "unthrottled-1"), "x");
        using (var held = Run(AgentGate.ScriptName, ReadCall))
        {
            Assert.That(held.WaitForExit(5_000), Is.False, "another CLI's /unthrottle doesn't exempt this one");
            held.Kill();
        }

        File.WriteAllText(Path.Combine(Marks, $"unthrottled-{Cli}"), "x");
        Status(new { stop = true, pace_delay_seconds = 60 });
        Assert.That(GateHeld(ReadCall, 2).Output, Does.Contain("\"deny\""), "the stop holds and refuses it all the same");
    }

    /// <summary>2026-10-17 04:47-04:52Z (M.ORCH-T205): the daemon stalled under heavy load, its file went stale at 88%,
    /// and every call ran unheld until 96%. A stalled daemon's last pace still holds until the window it was for resets.</summary>
    [Test]
    public void A_stalled_daemons_last_pace_holds_until_its_window_resets()
    {
        string Reset(TimeSpan from) => DateTime.UtcNow.Add(from).ToString("yyyy-MM-ddTHH:mm:ssZ");
        Status(new { pace_delay_seconds = 3, window_reset = Reset(TimeSpan.FromHours(1)) }, at: DateTime.UtcNow.AddMinutes(-10));
        Assert.That(Gate(ReadCall).Took, Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(2.9)), "stale inside its window: held at its last pace");
        Assert.That(GateLog().Last(), Does.Contain("\"decision\": \"held\""));

        Status(new { pace_delay_seconds = 30, window_reset = Reset(TimeSpan.FromMinutes(-1)) }, at: DateTime.UtcNow.AddMinutes(-10));
        Assert.That(Gate(ReadCall).Took, Is.LessThan(TimeSpan.FromSeconds(10)), "its window has reset: nothing is known, nothing held");

        Status(new { pace_delay_seconds = 30, window_reset = Reset(TimeSpan.FromHours(1)) }, at: DateTime.UtcNow.AddMinutes(-10), source: "something else");
        Assert.That(Gate(ReadCall).Took, Is.LessThan(TimeSpan.FromSeconds(10)), "not the orchestrator's file");

        // The daemon comes back: its fresh word is the pace again at once.
        Status(new { pace_delay_seconds = 60, window_reset = Reset(TimeSpan.FromHours(1)) }, at: DateTime.UtcNow.AddMinutes(-10));
        var (_, took) = GateWhile(ReadCall, TimeSpan.FromSeconds(2), () => Status(new { pace_delay_seconds = 0, window_reset = Reset(TimeSpan.FromHours(1)) }));
        Assert.That(took, Is.LessThan(TimeSpan.FromSeconds(10)));
    }

    [Test]
    public void A_stale_status_never_holds_an_agent_call()
    {
        Directory.CreateDirectory(Path.Combine(Marks, "subagents"));
        foreach (var m in new[] { "a1", "a2", "a3" }) File.WriteAllText(Path.Combine(Marks, "subagents", m), "");
        var agent = new { tool_name = "Agent", tool_input = new { prompt = "x" } };
        Status(new { subagent_allowance = 1, subagents_running = 3, pace_delay_seconds = 60 }, at: DateTime.UtcNow.AddMinutes(-10));
        var (output, took) = Gate(agent);
        Assert.That(output, Is.Empty);
        Assert.That(took, Is.LessThan(TimeSpan.FromSeconds(10)), "a full allowance and a long delay from a file nobody refreshes hold nothing");

        Status(new { subagent_allowance = 1, subagents_running = 3 }); // the same numbers, fresh: it waits
        using var held = Run(AgentGate.ScriptName, agent);
        Assert.That(held.WaitForExit(5_000), Is.False);
        held.Kill();
    }

    [Test]
    public void The_repo_window_guard_looks_up_a_closes_first_ticket_argument_in_the_ticket_database()
    {
        Tickets(("U.SPC-T1", "UnitySolarSystem"), ("M.TIX-T7", "MindAttic.Tickets"));
        var uss = Path.Combine(ws, "UnitySolarSystem");
        object Bash(string command) => new { tool_name = "Bash", tool_input = new { command }, cwd = uss };

        Assert.That(Gate(Bash("tickets ticket close M.TIX-T7 --ref abc123")).Output,
            Does.Contain("\"deny\"").And.Contain("belongs to MindAttic.Tickets"), "no --repo: the db says whose ticket it is");
        Assert.That(Gate(Bash("cd x && tickets ticket close M.TIX-T7 --ref abc")).Output, Does.Contain("\"deny\""), "as any segment of a command");
        Assert.That(Gate(Bash("tickets ticket close U.SPC-T1 --ref M.TIX-T7 --summary 'like M.TIX-T7'")).Output, Is.Empty,
            "this repo's own ticket; the IDs in --ref and --summary are just text");
        Assert.That(Gate(Bash("tickets ticket correct M.TIX-T7 --summary x")).Output, Does.Contain("\"deny\""), "correct is looked up the same way");
        Assert.That(Gate(Bash("tickets ticket close X.NOPE-T1 --ref x")).Output, Is.Empty, "a ticket the db doesn't know isn't refused");
        // M.ORCH-T468: the lookup ignores case, so the ID and the command name must too (Windows runs Tickets.exe all the same).
        Assert.That(Gate(Bash("tickets ticket close m.tix-t7 --ref abc")).Output, Does.Contain("\"deny\""), "a lower-case ID");
        Assert.That(Gate(Bash("& Tickets.exe ticket close M.TIX-T7 --ref abc")).Output, Does.Contain("\"deny\""), "the command name in any case");
        // M.ORCH-T564: the legacy `workflow` shim forwards to tickets, so a close through it is looked up the same way.
        Assert.That(Gate(Bash("& Workflow.exe ticket close M.TIX-T7 --ref abc")).Output, Does.Contain("\"deny\""), "the legacy workflow shim");
        Assert.That(Gate(Bash("tickets ticket close u.spc-t1 --ref abc")).Output, Is.Empty, "this repo's own ticket in lower case");
    }

    [Test]
    public void The_hooks_find_the_workspace_the_orchestrator_is_set_to()
    {
        // M.ORCH-T409: with the workspace anywhere but D:\Projects\MindAttic, the hooks found no repo and no tickets.
        Tickets(("U.SPC-T1", "UnitySolarSystem"), ("M.TIX-T7", "MindAttic.Tickets"));
        var settings = Path.Combine(dir, "MindAttic", "MindAttic.Orchestrator", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
        File.WriteAllText(settings, JsonSerializer.Serialize(new { WorkspaceRoot = ws }));
        var close = new { tool_name = "Bash", tool_input = new { command = "tickets ticket close M.TIX-T7 --ref abc123" }, cwd = Path.Combine(ws, "UnitySolarSystem") };

        Assert.That(Gate(close, new Dictionary<string, string> { ["MINDATTIC_WORKSPACE"] = "" }).Output,
            Does.Contain("\"deny\"").And.Contain("belongs to MindAttic.Tickets"), "settings.json's workspace, with no MINDATTIC_WORKSPACE");
    }

    [Test]
    public void The_hooks_find_the_ticket_database_in_the_MindAttic_Workflow_folder_until_it_is_renamed()
    {
        // M.ORCH-T564: MindAttic.Workflow became MindAttic.Tickets, but the folder is renamed last; until then the database
        // is MindAttic.Workflow\data\Tickets.db, as TicketsDb.HomeFolder finds it.
        Tickets(("U.SPC-T1", "UnitySolarSystem"), ("M.TIX-T7", "MindAttic.Tickets"));
        Directory.Move(Path.Combine(ws, "MindAttic.Tickets"), Path.Combine(ws, "MindAttic.Workflow"));
        var close = new { tool_name = "Bash", tool_input = new { command = "tickets ticket close M.TIX-T7 --ref abc123" }, cwd = Path.Combine(ws, "UnitySolarSystem") };

        Assert.That(Gate(close).Output, Does.Contain("\"deny\"").And.Contain("belongs to MindAttic.Tickets"));
    }

    [Test]
    public void An_exempt_commands_turn_skips_every_pace_gate_until_it_stops_and_is_logged_as_exempt()
    {
        // M.ORCH-T198: a turn typed as /deploy (a default exempt command) is let through at once: no delay, no subagent wait.
        object Prompt(string prompt) => new { hook_event_name = "UserPromptSubmit", session_id = "e1", prompt, cwd = ws };
        object Call(string tool) => new { tool_name = tool, session_id = "e1", tool_input = new { prompt = "x", file_path = "x" }, cwd = ws };
        Status(new { stop = true, pace_delay_seconds = 60, subagent_allowance = 0, subagents_running = 3, reset_time = "9:50 PM" });
        Assert.That(Gate(Prompt("/deploy now"), AgentGate.ContextScriptName).Output, Does.Not.Contain("\"block\""), "typed at the stop, it still goes through");
        // But never past the stop (bughunt 2026-10-27): an exempt /allocate turn, its subagents on the same session, ran a ticket round past it.
        Assert.That(GateHeld(Call("Agent"), 2).Output, Does.Contain("\"deny\""), "the stop refuses even an exempt turn's calls");
        Status(new { pace_delay_seconds = 60, subagent_allowance = 0, subagents_running = 3, reset_time = "9:50 PM" });
        foreach (var tool in new[] { "Read", "Agent", "Bash" })
        {
            var (output, took) = Gate(Call(tool));
            Assert.That((output, took < TimeSpan.FromSeconds(10)), Is.EqualTo(("", true)), $"{tool} runs at once inside the exempt turn");
        }
        Assert.That(GateLog().Count(l => l.Contains("\"decision\": \"exempt\"")), Is.EqualTo(3), "each exempt call is logged so its spend is counted");
        Assert.That(Directory.Exists(Path.Combine(Marks, AgentGate.SlotDirName)) ? Directory.GetFiles(Path.Combine(Marks, AgentGate.SlotDirName)) : [], Is.Empty,
            "an exempt Agent call takes no slot");

        Gate(new { hook_event_name = "Stop", session_id = "e1", cwd = ws }, AgentGate.ContextScriptName);
        Assert.That(GateHeld(Call("Read"), 2).Took, Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(1.9)), "the exemption ends when the turn stops");

        Status(new { pace_delay_seconds = 60 });
        Gate(Prompt("/deployment plan"), AgentGate.ContextScriptName);
        using (var held = Run(AgentGate.ScriptName, Call("Read")))
        {
            Assert.That(held.WaitForExit(5_000), Is.False, "only the command itself, not a longer word that starts like it");
            held.Kill();
        }
    }

    [Test]
    public void The_hooks_never_create_the_ticket_database()
    {
        // M.ORCH-T546: sqlite3.connect(WORKFLOW_DB) left a 0-byte database wherever it pointed, which the tickets CLI then opened.
        var data = Path.Combine(ws, "MindAttic.Tickets", "data");
        Directory.CreateDirectory(data);
        Gate(new { tool_name = "Bash", tool_input = new { command = "tickets ticket close M.TIX-T7 --ref abc123" }, cwd = Path.Combine(ws, "UnitySolarSystem") });
        Gate(new { hook_event_name = "Stop", session_id = "w1", cwd = Path.Combine(ws, "UnitySolarSystem") }, AgentGate.ContextScriptName);
        Assert.That(File.Exists(Path.Combine(data, "Tickets.db")), Is.False);
    }

    [Test]
    public void An_exempt_turn_is_read_from_the_newer_of_the_session_row_and_its_file()
    {
        // M.ORCH-T543: the gate read only the exported .state.json, so a Stop whose file write failed (held open) left the
        // turn exempt in the file while its session_states row, written later, said it had ended.
        Status(new { pace_delay_seconds = 60 });
        var file = Path.Combine(Marks, "x9.state.json");
        Directory.CreateDirectory(Marks);
        File.WriteAllText(file, """{"exempt": "/deploy"}""");
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(-5));
        using (var connection = OrchDb.Open())
        {
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT OR REPLACE INTO session_states (session_id, value, written_at) VALUES ('x9', '{\"exempt\": null}', $at);";
            command.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0);
            command.ExecuteNonQuery();
        }
        var read = new { tool_name = "Read", session_id = "x9", tool_input = new { file_path = "x" }, cwd = ws };
        Assert.That(GateHeld(read, 2).Took, Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(1.9)), "the row, written later, ended the exempt turn");
    }

    [Test]
    public void A_skill_naming_an_exempt_command_is_let_through_anywhere_and_the_list_comes_from_settings()
    {
        Status(new { pace_delay_seconds = 60 });
        object Skill(string skill) => new { tool_name = "Skill", session_id = "k1", tool_input = new { skill }, cwd = ws };
        var exempt = Gate(Skill("run"));
        Assert.That((exempt.Output, exempt.Took < TimeSpan.FromSeconds(10)), Is.EqualTo(("", true)), "outside any exempt turn, the Skill call itself goes through");
        Assert.That(GateLog().Single(), Does.Contain("\"decision\": \"exempt\"").And.Contain("\"tool\": \"Skill\""));
        Assert.That(GateHeld(Skill("bughunt"), 2).Took, Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(1.9)), "a skill that isn't exempt is paced as ever");
        // Exempt from the pace, never from the stop (bughunt 2026-10-27).
        Status(new { stop = true, pace_delay_seconds = 60 });
        Assert.That(GateHeld(Skill("run"), 2).Output, Does.Contain("\"deny\""), "at the stop an exempt skill is refused too");
        Status(new { pace_delay_seconds = 60 });

        var settings = Path.Combine(dir, "MindAttic", "MindAttic.Orchestrator", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
        // With a byte-order mark, as a hand edit in PowerShell 5.1 or Notepad saves it, read as the orchestrator reads it (M.ORCH-T411).
        File.WriteAllText(settings, """{ "ExemptCommands": ["bughunt"] }""", new System.Text.UTF8Encoding(true));
        var listed = Gate(Skill("bughunt"));
        Assert.That((listed.Output, listed.Took < TimeSpan.FromSeconds(10)), Is.EqualTo(("", true)), "the settings list is read live, with or without the slash");
        Assert.That(GateHeld(Skill("run"), 2).Took, Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(1.9)), "and replaces the defaults");
        Status(new { stop = true, pace_delay_seconds = 60 });
        Assert.That(Gate(new { hook_event_name = "UserPromptSubmit", session_id = "k2", prompt = "/deploy", cwd = ws }, AgentGate.ContextScriptName).Output,
            Does.Contain("\"block\""), "/deploy is no longer exempt, so at the stop it's answered unsent");
    }

    // M.ORCH-T285: every combination of sign-in changes, with the daemon down so only the hooks' own record decides.

    private const string AccountA = """{"oauthAccount":{"accountUuid":"account-a","organizationUuid":"max"}}""";
    private const string AccountB = """{"oauthAccount":{"accountUuid":"account-b","organizationUuid":"team"}}""";

    private void SignedIn(string config) => File.WriteAllText(Path.Combine(dir, ".claude.json"), config);

    private static long Epoch(DateTime utc) => new DateTimeOffset(utc).ToUnixTimeSeconds();

    private static readonly DateTime WeekReset = DateTime.UtcNow.AddDays(3);
    private static readonly DateTime FiveReset = DateTime.UtcNow.AddHours(2);

    /// <summary>A statusline's reading, stamped now: the week at <paramref name="weekPercent"/>, tagged when the probe wrote it.</summary>
    private void Reading(double weekPercent, DateTime? weekReset = null, DateTime? fiveReset = null, string? account = null)
    {
        var body = new System.Text.Json.Nodes.JsonObject
        {
            ["rate_limits"] = new System.Text.Json.Nodes.JsonObject
            {
                ["five_hour"] = new System.Text.Json.Nodes.JsonObject { ["used_percentage"] = 40, ["resets_at"] = Epoch(fiveReset ?? FiveReset) },
                ["seven_day"] = new System.Text.Json.Nodes.JsonObject { ["used_percentage"] = weekPercent, ["resets_at"] = Epoch(weekReset ?? WeekReset) },
            },
            ["at"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        };
        if (account is not null) body["account"] = account;
        File.WriteAllText(Path.Combine(Claude, "claude-usage.json"), body.ToJsonString());
    }

    private bool PromptHeld(IDictionary<string, string>? env = null) =>
        Gate(new { hook_event_name = "UserPromptSubmit", session_id = "acct", prompt = "hi", cwd = ws }, env, AgentGate.ContextScriptName).Output.Contains("Not sent");

    private void DaemonDown() => Status(new { stop = false }, at: DateTime.UtcNow.AddMinutes(-10));

    /// <summary>Signed in to A with A's week at 99%: the stop is seen and latched for A.</summary>
    private void AAtItsWeeklyStop()
    {
        DaemonDown();
        SignedIn(AccountA);
        Reading(99);
        Assert.That(PromptHeld(), Is.True, "A at 99% of its week");
    }

    [Test]
    public void A_login_to_another_account_or_organization_leaves_the_old_ones_stop_behind_and_going_back_brings_it_back()
    {
        AAtItsWeeklyStop();

        SignedIn(AccountB);
        Assert.That(PromptHeld(), Is.False, "B isn't A: A's latch and reading hold nothing back");
        Reading(99);
        Assert.That(PromptHeld(), Is.False, "another CLI restamps A's last reply as new: it isn't B's, so it's ignored");

        SignedIn(AccountA);
        Assert.That(PromptHeld(), Is.True, "back on A inside its window: A's latch holds again");

        SignedIn("""{"oauthAccount":{"accountUuid":"account-a","organizationUuid":"team"}}""");
        Assert.That(PromptHeld(), Is.False, "the same person's other organization has limits of its own");
    }

    [Test]
    public void Moving_to_the_configs_api_key_isnt_held_and_moving_back_holds_again()
    {
        AAtItsWeeklyStop();
        SignedIn("""{"primaryApiKey":"sk-ant-x"}""");
        Assert.That(PromptHeld(), Is.False, "an API key has no weekly window");
        Status(new { stop = true, pace_delay_seconds = 60 });
        Assert.That(Gate(ReadCall).Took, Is.LessThan(TimeSpan.FromSeconds(10)), "nor does the orchestrator's stop or pace hold it");
        DaemonDown();
        SignedIn(AccountA);
        Assert.That(PromptHeld(), Is.True, "back on A: its stop holds");
    }

    [Test]
    public void Signing_out_isnt_a_switch_back_in_to_the_same_account_still_holds_and_to_another_doesnt()
    {
        AAtItsWeeklyStop();
        SignedIn("""{"numStartups":4}""");
        Assert.That(PromptHeld(), Is.True, "signed out: still A's stop");
        SignedIn(AccountA);
        Assert.That(PromptHeld(), Is.True, "back in to A: no switch, the stop holds");
        SignedIn("""{"numStartups":4}""");
        SignedIn(AccountB);
        Assert.That(PromptHeld(), Is.False, "in to B: a switch");
    }

    [Test]
    public void After_a_switch_only_a_reading_tied_to_the_new_account_stops_it()
    {
        AAtItsWeeklyStop();
        SignedIn(AccountB);
        var bWeek = DateTime.UtcNow.AddDays(5);
        Reading(99, account: "account-a/max");
        Assert.That(PromptHeld(), Is.False, "tagged with A");
        Reading(99, weekReset: bWeek, fiveReset: DateTime.UtcNow.AddHours(1), account: "account-b/team");
        Assert.That(PromptHeld(), Is.True, "the meter probe read B's own meter: B's week is at the line");

        // The probe's reading confirmed B's windows (the daemon records them): B's statusline readings now count too.
        File.Delete(Path.Combine(Marks, AgentGate.StopLatchName));
        var record = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(Marks, Budget.ClaudeAccount.FileName)))!.AsObject();
        record["confirmed"] = new System.Text.Json.Nodes.JsonObject
        {
            ["account-b/team"] = new System.Text.Json.Nodes.JsonObject { ["five_hour"] = Epoch(DateTime.UtcNow.AddHours(1)), ["seven_day"] = Epoch(bWeek) },
            ["account-a/max"] = new System.Text.Json.Nodes.JsonObject { ["five_hour"] = Epoch(FiveReset), ["seven_day"] = Epoch(WeekReset) },
        };
        File.WriteAllText(Path.Combine(Marks, Budget.ClaudeAccount.FileName), record.ToJsonString());
        Thread.Sleep(1100); // a statusline's stamp is to the second: not the second B was signed in to
        Reading(99, weekReset: bWeek, fiveReset: DateTime.UtcNow.AddHours(1));
        Assert.That(PromptHeld(), Is.True, "B's statusline, its windows confirmed");
        File.Delete(Path.Combine(Marks, AgentGate.StopLatchName));
        Reading(99, weekReset: bWeek, fiveReset: DateTime.UtcNow.AddHours(4));
        Assert.That(PromptHeld(), Is.True, "B's next 5-hour window, known by B's own week");
        File.Delete(Path.Combine(Marks, AgentGate.StopLatchName));
        Reading(99);
        Assert.That(PromptHeld(), Is.False, "A's windows, restamped by another CLI");
    }

    [Test]
    public void A_record_and_latch_from_the_version_before_are_migrated_without_losing_the_date()
    {
        DaemonDown();
        SignedIn(AccountA);
        Directory.CreateDirectory(Marks);
        var since = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds();
        File.WriteAllText(Path.Combine(Marks, Budget.ClaudeAccount.FileName), $$"""{"account":"account-a/max","since":{{since}}}""");
        File.WriteAllText(Path.Combine(Marks, AgentGate.StopLatchName),
            $$$"""{"claude-week":{"percent":99.0,"until":{{{Epoch(WeekReset)}}},"epoch":{{{since}}}}}""");
        Assert.That(PromptHeld(), Is.True, "the old latch is the current account's: its epoch is the account's date");

        SignedIn(AccountB);
        Assert.That(PromptHeld(), Is.False);
        var record = File.ReadAllText(Path.Combine(Marks, Budget.ClaudeAccount.FileName));
        Assert.That(record, Does.Contain($"\"from\": {since}").And.Contain("account-b/team"), "A's span keeps its date");
        SignedIn(AccountA);
        Assert.That(PromptHeld(), Is.True, "A's latch, moved to A's key, holds again on the way back");
    }

    [Test]
    public void A_cli_on_an_approved_api_key_a_gateway_a_cloud_provider_or_a_key_helper_is_never_held_by_the_subscription()
    {
        AAtItsWeeklyStop();
        SignedIn("""{"oauthAccount":{"accountUuid":"account-a","organizationUuid":"max"},"customApiKeyResponses":{"approved":["abcdefghijklmnopqrst"],"rejected":["zyxwvutsrqponmlkjihg"]}}""");
        Dictionary<string, string> Env(string name, string value) => new() { [name] = value };

        Assert.That(PromptHeld(Env("ANTHROPIC_API_KEY", "sk-ant-api03-abcdefghijklmnopqrst")), Is.False, "an approved key is used instead of the login");
        Assert.That(PromptHeld(Env("ANTHROPIC_API_KEY", "sk-ant-api03-zyxwvutsrqponmlkjihg")), Is.True, "a rejected one leaves the login in charge");
        Assert.That(PromptHeld(new Dictionary<string, string> { ["ANTHROPIC_API_KEY"] = "sk-ant-api03-zyxwvutsrqponmlkjihg", ["CLAUDE_CODE_ENTRYPOINT"] = "sdk-cli" }), Is.False,
            "-p always uses the key");
        foreach (var cloud in new[] { "CLAUDE_CODE_USE_BEDROCK", "CLAUDE_CODE_USE_VERTEX", "CLAUDE_CODE_USE_FOUNDRY" })
            Assert.That(PromptHeld(Env(cloud, "1")), Is.False, cloud);
        Assert.That(PromptHeld(Env("CLAUDE_CODE_USE_BEDROCK", "0")), Is.True, "switched off is the login");
        Assert.That(PromptHeld(Env("ANTHROPIC_AUTH_TOKEN", "gateway-token")), Is.False, "a gateway's token");
        Assert.That(PromptHeld(Env("CLAUDE_CODE_OAUTH_TOKEN", "sk-ant-oat01-x")), Is.True, "a subscription token is paced as the config's account");

        var settings = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(Claude, "settings.json")))!.AsObject();
        settings["apiKeyHelper"] = "vault-key.cmd";
        File.WriteAllText(Path.Combine(Claude, "settings.json"), settings.ToJsonString());
        Assert.That(PromptHeld(), Is.False, "a key helper");
        Assert.That(File.ReadAllText(Path.Combine(Marks, "acct.state.json")), Does.Contain("\"auth\": \"helper\""), "recorded for the daemon");
    }

    [Test]
    public void The_pace_gate_holds_a_cli_on_its_login_but_not_one_on_an_approved_api_key()
    {
        SignedIn("""{"oauthAccount":{"accountUuid":"account-a"},"customApiKeyResponses":{"approved":["abcdefghijklmnopqrst"]}}""");
        Status(new { stop = true });
        using (var held = Run(AgentGate.ScriptName, ReadCall))
        {
            Assert.That(held.WaitForExit(5_000), Is.False, "the login is held at the stop");
            held.Kill();
        }
        var keyed = Gate(ReadCall, new Dictionary<string, string> { ["ANTHROPIC_API_KEY"] = "sk-ant-api03-abcdefghijklmnopqrst" });
        Assert.That((keyed.Output, keyed.Took < TimeSpan.FromSeconds(10)), Is.EqualTo(("", true)), "the key's calls run at once");
    }

    // M.ORCH-T434: the last sign-in decides what a CLI bills, and every CLI says so at its start.
    private const string EnvKey = "sk-ant-api03-0123456789abcdefghijWXYZ";
    private static string KeyTail => EnvKey[^20..];
    private const string TeamLogin = "\"oauthAccount\":" + """{"accountUuid":"acct-1","organizationUuid":"org-1","emailAddress":"pdftransferjr@mindattic.com","organizationName":"MindAttic","organizationType":"claude_team"}""";

    /// <summary>customApiKeyResponses with these tails, as a top-level member.</summary>
    private static string KeyResponses(string[] approved, string[] rejected) =>
        "\"customApiKeyResponses\":" + JsonSerializer.Serialize(new { approved, rejected });
    private string ConfigPath => Path.Combine(dir, ".claude.json");

    private static readonly object Starting = new { hook_event_name = "SessionStart", session_id = "s1", source = "startup", cwd = "." };

    /// <summary>The SessionStart message's billing line (its first line: the "[Billing]" line billing_check returns), from
    /// this CLI's environment (the test host as the CLI, so it started before any set-aside). Asserts the two
    /// "[Orchestrator]" lines that follow it read pacing and allocation on by default (no mark of either on this pid),
    /// so callers that only care about billing don't have to restate them.</summary>
    private string StartLine(IDictionary<string, string>? env = null)
    {
        var all = new Dictionary<string, string> { ["MINDATTIC_CLI_PID"] = Environment.ProcessId.ToString() };
        foreach (var (k, v) in env ?? new Dictionary<string, string>()) all[k] = v;
        var output = Gate(Starting, all, AgentGate.ContextScriptName).Output;
        using var doc = JsonDocument.Parse(output);
        var full = doc.RootElement.GetProperty("systemMessage").GetString()!;
        Assert.That(doc.RootElement.GetProperty("hookSpecificOutput").GetProperty("additionalContext").GetString(), Is.EqualTo(full));
        var lines = full.Split('\n');
        Assert.That(lines, Has.Length.EqualTo(5), full);
        Assert.That(lines[0], Is.Empty, "a leading blank line so the billing line never shares a line with the hook's own label");
        Assert.That(lines[2], Is.EqualTo("[Orchestrator] Pacing is on."));
        Assert.That(lines[3], Is.EqualTo("[Orchestrator] Allocation is off."));
        Assert.That(lines[4], Is.EqualTo("[Orchestrator] Crossrepo is off."));
        return lines[1];
    }

    private static Dictionary<string, string> WithKey => new() { ["ANTHROPIC_API_KEY"] = EnvKey };

    private JsonElement Responses()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(ConfigPath));
        return doc.RootElement.GetProperty("customApiKeyResponses").Clone();
    }

    // ---- SessionStart also reports this CLI's pacing (/pacing) and allocation (/allocate), after the billing line ----

    [Test]
    public void SessionStart_reports_this_clis_pacing_and_allocation_after_the_billing_line()
    {
        SignedIn("{" + TeamLogin + "}");
        string FullMessage() => JsonDocument.Parse(Gate(Starting, AgentGate.ContextScriptName).Output).RootElement.GetProperty("systemMessage").GetString()!;
        Assert.That(FullMessage(), Does.EndWith("\n[Orchestrator] Pacing is on.\n[Orchestrator] Allocation is off.\n[Orchestrator] Crossrepo is off."), "a fresh CLI is paced, not allocating, not crossrepo");

        Gate(Said("/pacing off"), AgentGate.ContextScriptName);
        Gate(Said("/allocate"), AgentGate.ContextScriptName);
        Assert.That(FullMessage(), Does.EndWith("\n[Orchestrator] Pacing is off.\n[Orchestrator] Allocation is on.\n[Orchestrator] Crossrepo is off."));

        Gate(Said("/pacing on"), AgentGate.ContextScriptName);
        Gate(Said("/allocate off"), AgentGate.ContextScriptName);
        Assert.That(FullMessage(), Does.EndWith("\n[Orchestrator] Pacing is on.\n[Orchestrator] Allocation is off.\n[Orchestrator] Crossrepo is off."), "back off/off once each is turned back");
    }

    [Test]
    public void Signed_in_to_claude_ai_an_approved_env_key_is_set_aside_and_the_start_warns_naming_the_account()
    {
        SignedIn("{" + TeamLogin + "," + KeyResponses([KeyTail], []) + "}");
        var line = StartLine(WithKey);
        Assert.That(line, Does.StartWith("[Billing] Warning: this CLI bills the API key in ANTHROPIC_API_KEY (\u2026WXYZ)")
            .And.Contain("not Claude Team: pdftransferjr@mindattic.com (MindAttic), which is signed in")
            .And.Contain("The key has now been set aside: close and reopen this CLI so it bills Claude Team.")
            .And.Not.Contain(KeyTail));
        var responses = Responses();
        Assert.That(responses.GetProperty("approved").GetArrayLength(), Is.EqualTo(0));
        Assert.That(responses.GetProperty("rejected")[0].GetString(), Is.EqualTo(KeyTail));
        var state = File.ReadAllText(Path.Combine(Marks, "s1.state.json"));
        Assert.That(state, Does.Contain("\"auth\": \"api-key\"").And.Contain("\"billing\": \"[Billing] Warning"), "recorded for the daemon and the dashboard");

        // This CLI started on the key, so it still bills it until it's reopened: still kept out of the subscription's pace.
        Assert.That(StartLine(WithKey), Does.Contain("The key has now been set aside"));
        var prompt = new { hook_event_name = "UserPromptSubmit", session_id = "s1", prompt = "hi", cwd = ws };
        Gate(prompt, new Dictionary<string, string>(WithKey) { ["MINDATTIC_CLI_PID"] = Environment.ProcessId.ToString() }, AgentGate.ContextScriptName);
        Assert.That(File.ReadAllText(Path.Combine(Marks, "s1.state.json")), Does.Contain("\"auth\": \"api-key\""));
    }

    [Test]
    public void A_cli_already_running_on_the_key_is_warned_on_its_first_prompt()
    {
        // Started before the SessionStart hook was registered: its first prompt sets the key aside and says so, once.
        SignedIn("{" + TeamLogin + "," + KeyResponses([KeyTail], []) + "}");
        var env = new Dictionary<string, string>(WithKey) { ["MINDATTIC_CLI_PID"] = Environment.ProcessId.ToString() };
        var prompt = new { hook_event_name = "UserPromptSubmit", session_id = "old", prompt = "hi", cwd = ws };
        var first = Gate(prompt, env, AgentGate.ContextScriptName).Output;
        Assert.That(JsonDocument.Parse(first).RootElement.GetProperty("systemMessage").GetString(), Does.StartWith("[Billing] Warning"));
        Assert.That(Responses().GetProperty("rejected")[0].GetString(), Is.EqualTo(KeyTail));
        Assert.That(Gate(prompt, env, AgentGate.ContextScriptName).Output, Does.Not.Contain("[Billing]"), "said once");
    }

    [Test]
    public void After_the_key_is_set_aside_a_new_cli_bills_claude_team()
    {
        SignedIn("{" + TeamLogin + "," + KeyResponses([], [KeyTail]) + "}");
        var before = File.ReadAllText(ConfigPath);
        Assert.That(StartLine(WithKey), Is.EqualTo("[Billing] This CLI bills Claude Team: pdftransferjr@mindattic.com (MindAttic)."));
        Assert.That(File.ReadAllText(ConfigPath), Is.EqualTo(before));
        Assert.That(File.ReadAllText(Path.Combine(Marks, "s1.state.json")), Does.Contain("\"auth\": \"subscription\""));
    }

    [Test]
    public void After_a_login_to_another_account_the_next_prompt_says_the_new_billing_line_once()
    {
        // M.ORCH-T486: the line from the CLI's start named the old account until the CLI was reopened.
        SignedIn("{" + TeamLogin + "}");
        Assert.That(StartLine(), Is.EqualTo("[Billing] This CLI bills Claude Team: pdftransferjr@mindattic.com (MindAttic)."));
        var env = new Dictionary<string, string> { ["MINDATTIC_CLI_PID"] = Environment.ProcessId.ToString() };
        var prompt = new { hook_event_name = "UserPromptSubmit", session_id = "s1", prompt = "hi", cwd = ws };
        Assert.That(Gate(prompt, env, AgentGate.ContextScriptName).Output, Does.Not.Contain("[Billing]"), "same account: nothing to say");

        SignedIn("""{"oauthAccount":{"accountUuid":"acct-2","organizationUuid":"org-2","emailAddress":"ohleo199@mindattic.com","organizationName":"Leo","organizationType":"claude_max"}}""");
        var switched = Gate(prompt, env, AgentGate.ContextScriptName).Output;
        Assert.That(JsonDocument.Parse(switched).RootElement.GetProperty("systemMessage").GetString(),
            Is.EqualTo("[Billing] This CLI bills Claude Max: ohleo199@mindattic.com (Leo)."));
        Assert.That(File.ReadAllText(Path.Combine(Marks, "s1.state.json")), Does.Contain("ohleo199@mindattic.com"), "recorded for the dashboard");
        Assert.That(Gate(prompt, env, AgentGate.ContextScriptName).Output, Does.Not.Contain("[Billing]"), "said once");
        Assert.That(StartLine(), Does.Contain("ohleo199@mindattic.com"), "a CLI starting (or resumed) now names the new account");
    }

    [Test]
    public void A_console_login_keeps_the_env_key_and_bills_the_api()
    {
        SignedIn("""{"primaryApiKey":"sk-ant-console","oauthAccount":{"accountUuid":"acct-1","emailAddress":"dev@mindattic.com"},""" + KeyResponses([KeyTail], []) + "}");
        var before = File.ReadAllText(ConfigPath);
        Assert.That(StartLine(WithKey), Is.EqualTo("[Billing] This CLI bills the API key in ANTHROPIC_API_KEY (\u2026WXYZ)."));
        Assert.That(File.ReadAllText(ConfigPath), Is.EqualTo(before), "the API is what was chosen");
        Assert.That(StartLine(), Is.EqualTo("[Billing] This CLI bills the Anthropic Console API: dev@mindattic.com."));
    }

    [Test]
    public void With_no_claude_ai_login_the_env_key_is_left_alone_and_billed()
    {
        SignedIn("{" + KeyResponses([KeyTail], []) + "}");
        var before = File.ReadAllText(ConfigPath);
        Assert.That(StartLine(WithKey), Is.EqualTo("[Billing] This CLI bills the API key in ANTHROPIC_API_KEY (\u2026WXYZ)."));
        Assert.That(File.ReadAllText(ConfigPath), Is.EqualTo(before));
        Assert.That(File.Exists(Path.Combine(Marks, AgentGate.ApiKeySetAsideName)), Is.False);
    }

    [Test]
    public void A_cloud_provider_or_a_gateway_is_named()
    {
        SignedIn("{" + TeamLogin + "}");
        Assert.That(StartLine(new Dictionary<string, string> { ["CLAUDE_CODE_USE_BEDROCK"] = "1", ["ANTHROPIC_API_KEY"] = EnvKey }),
            Is.EqualTo("[Billing] This CLI bills Amazon Bedrock (CLAUDE_CODE_USE_BEDROCK)."));
        Assert.That(StartLine(new Dictionary<string, string> { ["CLAUDE_CODE_USE_VERTEX"] = "1" }), Is.EqualTo("[Billing] This CLI bills Google Vertex AI (CLAUDE_CODE_USE_VERTEX)."));
        Assert.That(StartLine(new Dictionary<string, string> { ["ANTHROPIC_AUTH_TOKEN"] = "t", ["ANTHROPIC_BASE_URL"] = "https://llm.example" }),
            Is.EqualTo("[Billing] This CLI bills the LLM gateway at https://llm.example through ANTHROPIC_AUTH_TOKEN."));
    }

    [Test]
    public void Setting_the_key_aside_changes_that_field_alone_and_keeps_the_rest_byte_for_byte()
    {
        var projects = string.Join(",\n", Enumerable.Range(0, 800).Select(i =>
            $"    \"D:\\\\Projects\\\\Repo{i}\": {{\n      \"allowedTools\": [],\n      \"lastCost\": {i}.10,\n      \"lastDuration\": 1e-7,\n      \"big\": 12345678901234567890,\n      \"name\": \"caf\u00e9 \u2603 \\u00e9 {i}\"\n    }}"));
        string Config(string responses) =>
            "{\n  \"numStartups\": 2138,\n  \"autoUpdates\": false,\n  \"projects\": {\n" + projects + "\n  },\n" +
            "  \"customApiKeyResponses\": " + responses + ",\n  " + TeamLogin + ",\n  \"tipsHistory\": {\n    \"a\": 1.0\n  }\n}";
        var original = Config($"{{\n    \"approved\": [\n      \"other-tail-xxxxxxxxxx\",\n      \"{KeyTail}\"\n    ],\n    \"rejected\": []\n  }}");
        var expected = Config($"{{\n    \"approved\": [\n      \"other-tail-xxxxxxxxxx\"\n    ],\n    \"rejected\": [\n      \"{KeyTail}\"\n    ]\n  }}");
        File.WriteAllText(ConfigPath, original);
        Assert.That(original.Length, Is.GreaterThan(100_000));
        Assert.That(StartLine(WithKey), Does.Contain("The key has now been set aside"));
        Assert.That(File.ReadAllText(ConfigPath), Is.EqualTo(expected));
        Assert.That(Directory.GetFiles(dir, "*.tmp"), Is.Empty, "no temp file left behind");
    }

    [Test]
    public void A_missing_or_garbled_config_never_fails_a_session_start()
    {
        foreach (var config in new string?[] { null, "{not json", "[]", """{"oauthAccount":"x","customApiKeyResponses":{"approved":"abc"}}""" })
        {
            if (config is null) File.Delete(ConfigPath); else File.WriteAllText(ConfigPath, config);
            using var p = Run(AgentGate.ContextScriptName, Starting, WithKey);
            var output = p.StandardOutput.ReadToEnd();
            Assert.That(p.WaitForExit(30_000), Is.True);
            Assert.That(p.ExitCode, Is.EqualTo(0), config ?? "missing");
            if (output.Trim().Length > 0)
                Assert.That(JsonDocument.Parse(output).RootElement.GetProperty("systemMessage").GetString(), Does.StartWith("\n[Billing]"));
            if (config is not null) Assert.That(File.ReadAllText(ConfigPath), Is.EqualTo(config));
        }
        using var garbled = Run(AgentGate.ContextScriptName, "not an event");
        Assert.That(garbled.StandardOutput.ReadToEnd(), Is.Empty);
        Assert.That(garbled.WaitForExit(30_000) && garbled.ExitCode == 0, Is.True);
    }

    [Test]
    public void The_session_start_hook_is_registered()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Claude, "settings.json")));
        Assert.That(doc.RootElement.GetProperty("hooks").GetProperty("SessionStart")[0].GetProperty("hooks")[0].GetProperty("command").GetString(),
            Does.Contain(AgentGate.ContextScriptName));
    }

    // M.ORCH-T436: the context gate follows the billing switch's mode.
    private void BillingMode(string mode, string login, double? approvedAt = null)
    {
        Directory.CreateDirectory(Marks);
        File.WriteAllText(Path.Combine(Marks, MindAttic.Orchestrator.Billing.BillingModeFile.FileName),
            JsonSerializer.Serialize(new { mode, since = 1.0, login, by = "cli", requested = Array.Empty<string>(), refused = Array.Empty<string>(), approved_at = approvedAt }));
    }

    [Test]
    public void On_the_api_key_mode_an_approved_env_key_is_kept_and_a_cli_on_the_login_is_the_one_warned()
    {
        SignedIn("{" + TeamLogin + "," + KeyResponses([KeyTail], []) + "}");
        BillingMode("api", "acct-1/org-1");
        var before = File.ReadAllText(ConfigPath);
        Assert.That(StartLine(WithKey), Does.StartWith("[Billing] This CLI bills the API key in ANTHROPIC_API_KEY (…WXYZ). Billing is set to the API key, for emergencies only"));
        Assert.That(File.ReadAllText(ConfigPath), Is.EqualTo(before), "the key is what was chosen: not set aside");
        Assert.That(StartLine(), Does.StartWith("[Billing] Warning: billing is set to the API key, but this CLI bills Claude Team: pdftransferjr@mindattic.com (MindAttic)."));
    }

    [Test]
    public void A_cli_started_before_the_switch_approved_the_key_still_bills_the_login()
    {
        SignedIn("{" + TeamLogin + "," + KeyResponses([KeyTail], []) + "}");
        BillingMode("api", "acct-1/org-1", approvedAt: DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60.0);
        Assert.That(StartLine(WithKey), Does.StartWith("[Billing] Warning: billing is set to the API key, but this CLI bills Claude Team"));
        Assert.That(File.ReadAllText(Path.Combine(Marks, "s1.state.json")), Does.Contain("\"auth\": \"subscription\""));
    }

    [Test]
    public void A_login_to_another_claude_ai_account_leaves_api_key_billing_on()
    {
        SignedIn("{" + TeamLogin + "," + KeyResponses([KeyTail], []) + "}");
        BillingMode("api", "acct-1/org-before");
        Assert.That(StartLine(WithKey), Does.Not.Contain("The key has now been set aside"));
        using var mode = JsonDocument.Parse(File.ReadAllText(Path.Combine(Marks, MindAttic.Orchestrator.Billing.BillingModeFile.FileName)));
        Assert.That((mode.RootElement.GetProperty("mode").GetString(), mode.RootElement.GetProperty("login").GetString()),
            Is.EqualTo(("api", "acct-1/org-before")));
        Assert.That(Responses().GetProperty("approved")[0].GetString(), Is.EqualTo(KeyTail));
    }

    // ---- /allocate off winds a CLI down and quicksaves it (M.ORCH-T572, which retired /handoff) ----

    /// <summary>A CLI's mark of <paramref name="kind"/>: its cli_marks row once the orchestrator's database is there, else its file (M.ORCH-T528).</summary>
    private string? CliMark(string kind, int pid) => MindAttic.Orchestrator.Sessions.CliMarks.Read(Marks, kind).GetValueOrDefault(pid);

    [Test]
    public void Allocate_off_lets_its_subagents_land_then_has_the_cli_quicksave_its_handoff_once()
    {
        Tickets(("U.SPC-T1", "UnitySolarSystem"));
        Status(new { });
        Gate(Said("/allocate"), AgentGate.ContextScriptName);
        var sub = Path.Combine(Marks, "subagents");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "a1"), Cli.ToString()); // one of this CLI's subagents, still working
        File.WriteAllText(Path.Combine(sub, "b1"), "777");           // another CLI's

        Gate(Said("/allocate off"), AgentGate.ContextScriptName);
        Assert.That((CliMark("allocate", Cli), CliMark("noallocate", Cli)), Is.EqualTo(((string?)null, "wind")), "off, and winding down");

        object Stop(string sid) => new { hook_event_name = "Stop", session_id = sid, cwd = Uss, last_assistant_message = "Done." };
        Assert.That(Gate(Stop("sk"), AgentGate.ContextScriptName).Output, Is.Empty, "its subagent is still working; off sends it back for no ticket");

        File.Delete(Path.Combine(sub, "a1")); // landed and closed
        Assert.That(Gate(Stop("sk"), AgentGate.ContextScriptName).Output, Does.Contain("\"block\"").And.Contain("UnitySolarSystem quiet, handoff saved"),
            "the last of its subagents is done: quicksave, whatever another CLI's are doing");
        Assert.That(Gate(Stop("sk"), AgentGate.ContextScriptName).Output, Is.Empty, "told once");

        // At the window's stop every other call is refused, but the quicksave goes through.
        Status(new { stop = true });
        var command = @"& D:\Projects\MindAttic\mindattic-agent-standard\prose-agent.ps1 save -RepoRoot (Get-Location) -Task 'a' -Next 'b'";
        Assert.That(Gate(new { tool_name = "PowerShell", session_id = "sk", cwd = Uss, hook_event_name = "PreToolUse", tool_input = new { command } }).Output, Is.Empty);
        Status(new { });
        Gate(new { hook_event_name = "PostToolUse", session_id = "sk", cwd = Uss, tool_name = "PowerShell", tool_input = new { command } }, AgentGate.ContextScriptName);
        Assert.That(CliMark("noallocate", Cli), Is.EqualTo(""), "saved: still off, wound down");

        Gate(Said("what's left?"), AgentGate.ContextScriptName);
        Assert.That(Gate(Stop("sk"), AgentGate.ContextScriptName).Output, Is.Empty, "nothing more to save, and nothing sends it back for work");
    }

    [Test]
    public void Allocate_off_all_winds_every_allocate_cli_down()
    {
        Gate(Said("/allocate"), AgentGate.ContextScriptName);
        Gate(new { hook_event_name = "UserPromptSubmit", session_id = "o2", prompt = "/allocate", cwd = Uss },
            new Dictionary<string, string> { ["MINDATTIC_CLI_PID"] = "5301" }, AgentGate.ContextScriptName);
        Gate(Said("/allocate off all"), AgentGate.ContextScriptName);
        Assert.That((CliMark("noallocate", Cli), CliMark("noallocate", 5301), CliMark("allocate", 5301)), Is.EqualTo(("wind", "wind", (string?)null)));
    }

    // ---- M.ORCH-T526: the status row and the event log in orchestrator.db ----

    private static MindAttic.Orchestrator.Settings.OrchestratorDb OrchDb => new(MindAttic.Orchestrator.Settings.OrchestratorDb.DefaultPath());

    private static void StatusRow(object fields, DateTime? at = null)
    {
        var body = JsonSerializer.SerializeToNode(fields)!.AsObject();
        body["source"] = "MindAttic.Orchestrator";
        body["timestamp"] = (at ?? DateTime.UtcNow).ToString("yyyy-MM-ddTHH:mm:ssZ");
        OrchDb.SaveState(MindAttic.Orchestrator.Budget.RateLimitStatusWriter.StateName, body.ToJsonString());
    }

    [Test]
    public void The_pace_hold_is_read_from_the_databases_row_and_its_event_goes_into_gate_events()
    {
        StatusRow(new { pace_delay_seconds = 2 });
        Assert.That(File.Exists(Path.Combine(Claude, "rl-status.json")), Is.False, "no file: the row alone");
        Assert.That(Gate(ReadCall).Took, Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(1.9)), "the row's hold is waited out");
        Assert.That(File.Exists(Path.Combine(Marks, AgentGate.GateLogName)), Is.False, "the event went into the database");
        var held = OrchDb.LoadGateEvents(DateTime.UtcNow.AddMinutes(-5)).Single();
        Assert.That((held.Decision, held.Tool), Is.EqualTo(("held", "Read")));

        // The newer of the row and the file decides: a file written later (a daemon from before the move) wins.
        StatusRow(new { pace_delay_seconds = 30 }, DateTime.UtcNow.AddSeconds(-20));
        Status(new { pace_delay_seconds = 0 });
        Assert.That(Gate(ReadCall).Took, Is.LessThan(TimeSpan.FromSeconds(10)), "the file is newer");
    }

    [Test]
    public void A_held_calls_gate_event_is_tagged_with_the_signed_in_account()
    {
        // M.ORCH-T774: a per-CLI login's own window can only tell its held and refused calls from another account's sharing
        // the same machine-wide gate log once each event names the account it ran for.
        SignedIn("""{"oauthAccount":{"accountUuid":"account-a","organizationUuid":"team"}}""");
        StatusRow(new { pace_delay_seconds = 2 });
        Assert.That(Gate(ReadCall).Took, Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(1.9)));
        var held = OrchDb.LoadGateEvents(DateTime.UtcNow.AddMinutes(-5)).Single();
        Assert.That(held.Account, Is.EqualTo("account-a/team"));
        Assert.That(GateLog().Single(), Does.Contain("\"account\": \"account-a/team\""));
    }

    /// <summary>M.ORCH-T774 (review): the key the hook tags a per-CLI login's events with must be exactly the key
    /// <see cref="MindAttic.Orchestrator.Sessions.AccountDirs"/> reads for the same folder, or a per-CLI window
    /// (<see cref="MindAttic.Orchestrator.Budget.PaceLedgerWatch"/>) could never match its own events to it.</summary>
    [Test]
    public void A_per_cli_logins_gate_event_is_tagged_with_exactly_its_accountdirs_key()
    {
        OrchDb.Open().Dispose(); // the database exists before the hook runs, or it falls back to the old file (no table yet)
        var (folder, env) = OwnLogin(email: "c@example.com", uuid: "acct-c");
        OwnStatus(folder, new { pace_delay_seconds = 2 });
        Assert.That(Gate(ReadCall, env).Took, Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(1.9)));
        var held = OrchDb.LoadGateEvents(DateTime.UtcNow.AddMinutes(-5)).Single();
        var expected = new MindAttic.Orchestrator.Sessions.AccountDirs(Path.Combine(dir, ".claude-accounts")).For("c@example.com").Account;
        Assert.That(held.Account, Is.EqualTo(expected).And.EqualTo("acct-c"));
    }

    [Test]
    public void A_hook_reading_while_a_write_holds_the_database_falls_back_to_the_file_and_its_event_is_read_in_later()
    {
        StatusRow(new { pace_delay_seconds = 0 }, DateTime.UtcNow.AddSeconds(-30));
        Status(new { pace_delay_seconds = 3 });
        (string Output, TimeSpan Took) result;
        using (var connection = OrchDb.Open())
        {
            // Another process mid-write, holding the database past every wait the hook makes.
            MindAttic.Orchestrator.Settings.OrchestratorDb.Execute(connection, "BEGIN EXCLUSIVE;");
            result = Gate(ReadCall);
            MindAttic.Orchestrator.Settings.OrchestratorDb.Execute(connection, "ROLLBACK;");
        }
        Assert.That(result.Took, Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(2.9)).And.LessThan(TimeSpan.FromSeconds(25)), "held by the file, not stuck on the database");
        Assert.That(result.Output, Does.Not.Contain("deny"));
        var log = Path.Combine(Marks, AgentGate.GateLogName);
        Assert.That(File.ReadAllLines(log).Single(), Does.Contain("\"decision\": \"held\""), "the event fell back to the old log");

        OrchDb.ImportGateLog(log);
        Assert.That(OrchDb.LoadGateEvents(DateTime.UtcNow.AddMinutes(-5)).Single().Decision, Is.EqualTo("held"), "read in once the database is free");
        Assert.That(File.Exists(log), Is.False);
    }

    // ---- M.ORCH-T527: the subagent slots and wants in orchestrator.db ----

    private static long Rows(string table)
    {
        using var connection = OrchDb.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table};";
        return (long)command.ExecuteScalar()!;
    }

    private string[] SlotFiles()
    {
        var slots = Path.Combine(Marks, AgentGate.SlotDirName);
        return Directory.Exists(slots) ? Directory.GetFiles(slots) : [];
    }

    [Test]
    public void Agent_calls_sent_together_each_decide_in_one_write_transaction_and_take_a_slot_row_not_a_file()
    {
        OrchDb.Open().Dispose();
        Status(new { subagent_allowance = 1, subagents_running = 0 });
        var agent = new { tool_name = "Agent", tool_input = new { prompt = "x" } };
        var burst = Enumerable.Range(0, 4).Select(_ => Run(AgentGate.ScriptName, agent)).ToList();
        try
        {
            Thread.Sleep(8_000);
            Assert.That(burst.Count(p => p.HasExited), Is.EqualTo(1), "concurrent writers: one free place lets one call through");
            Assert.That(Rows("subagent_slots"), Is.EqualTo(1), "the call let through holds a row");
            Assert.That(Rows("subagent_wants"), Is.GreaterThanOrEqualTo(1), "the waiting calls are rows too");
            Assert.That(SlotFiles(), Is.Empty);
            Assert.That(File.Exists(Path.Combine(Marks, AgentGate.SlotDirName + ".lock")), Is.False, "no lock file: the transaction is the lock");
            Assert.That(Directory.Exists(Path.Combine(Marks, AgentGate.WantDirName)), Is.False);

            Gate(new { hook_event_name = "SubagentStart", session_id = "s7", agent_id = "a7", cwd = ws }, AgentGate.ContextScriptName);
            Assert.That(Rows("subagent_slots"), Is.Zero, "its SubagentStart lets the row go");
            Thread.Sleep(7_000);
            Assert.That(burst.Count(p => p.HasExited), Is.EqualTo(1), "the started subagent fills the allowance just as its slot did");
        }
        finally
        {
            foreach (var p in burst) { if (!p.HasExited) p.Kill(); p.Dispose(); }
        }
        Assert.That(OrchDb.Dump(), Does.Not.Contain("subagent_"), "slots and wants last seconds: never in the dump");
    }

    [Test]
    public void A_gate_deciding_while_another_write_holds_the_database_keeps_its_slot_as_a_file_which_the_next_gate_counts()
    {
        OrchDb.Open().Dispose();
        Status(new { subagent_allowance = 1, subagents_running = 0 });
        var agent = new { tool_name = "Agent", tool_input = new { prompt = "x" } };
        (string Output, TimeSpan Took) result;
        using (var connection = OrchDb.Open())
        {
            // Another process mid-write: reads go on, a second write waits.
            MindAttic.Orchestrator.Settings.OrchestratorDb.Execute(connection, "BEGIN IMMEDIATE;");
            result = Gate(agent);
            MindAttic.Orchestrator.Settings.OrchestratorDb.Execute(connection, "ROLLBACK;");
        }
        Assert.That(result.Output, Does.Not.Contain("deny"));
        Assert.That(result.Took, Is.LessThan(TimeSpan.FromSeconds(20)), "not stuck on the database");
        Assert.That(SlotFiles(), Has.Length.EqualTo(1), "the place it took is a file");
        Assert.That(Rows("subagent_slots"), Is.Zero);

        using (var held = Run(AgentGate.ScriptName, agent))
        {
            Assert.That(held.WaitForExit(7_000), Is.False, "a gate on the database counts the file's place: the allowance of one is full");
            held.Kill();
        }
        Gate(new { hook_event_name = "SubagentStart", session_id = "s8", agent_id = "a8", cwd = ws }, AgentGate.ContextScriptName);
        Assert.That(SlotFiles(), Is.Empty, "SubagentStart lets the file go too");
        Status(new { subagent_allowance = 2, subagents_running = 0 });
        Assert.That(Gate(agent).Took, Is.LessThan(TimeSpan.FromSeconds(10)));
        Assert.That(Rows("subagent_slots"), Is.EqualTo(1), "with the database free the slot is a row");
    }

    [Test]
    public void A_database_from_before_the_slot_tables_leaves_the_gate_on_its_files()
    {
        var path = MindAttic.Orchestrator.Settings.OrchestratorDb.DefaultPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var old = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Pooling=False"))
        {
            old.Open();
            // A version-4 database has step 3's assignments table, which step 6 adds the claiming CLI to (M.ORCH-T532), and step
            // 2's ticket_costs, which step 10 adds the model to (M.ORCH-T563); and step 4's gate_events, which step 14 adds the
            // account to (M.ORCH-T774).
            MindAttic.Orchestrator.Settings.OrchestratorDb.Execute(old, "CREATE TABLE settings (name TEXT PRIMARY KEY, value TEXT NOT NULL); " +
                "CREATE TABLE assignments (position INTEGER PRIMARY KEY, ticket_id TEXT); CREATE TABLE ticket_costs (session_id TEXT PRIMARY KEY); " +
                "CREATE TABLE gate_events (id INTEGER PRIMARY KEY AUTOINCREMENT, at TEXT NOT NULL, decision TEXT NOT NULL, held REAL NOT NULL, tool TEXT, percent REAL); " +
                "PRAGMA user_version = 4;");
        }
        Status(new { subagent_allowance = 2, subagents_running = 0 });
        Assert.That(Gate(new { tool_name = "Agent", tool_input = new { prompt = "x" } }).Took, Is.LessThan(TimeSpan.FromSeconds(10)));
        Assert.That(SlotFiles(), Has.Length.EqualTo(1), "an older database: the slot is a file, as before");

        Assert.That(Rows("subagent_slots"), Is.Zero, "the orchestrator's next open adds the tables (schema step 5)");
        Assert.That(MindAttic.Orchestrator.Settings.OrchestratorDb.Version, Is.GreaterThanOrEqualTo(5));
    }

    /// <summary>A per-CLI login's folder (M.ORCH-T559) signed in to <paramref name="uuid"/>, and the environment of a CLI on it.</summary>
    private (string Folder, Dictionary<string, string> Env) OwnLogin(string email = "b@example.com", string uuid = "acct-b")
    {
        var folder = Path.Combine(dir, ".claude-accounts", email);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, ".claude.json"), JsonSerializer.Serialize(new { oauthAccount = new { accountUuid = uuid, emailAddress = email } }));
        return (folder, new Dictionary<string, string> { ["CLAUDE_CONFIG_DIR"] = folder });
    }

    private static void OwnStatus(string folder, object fields)
    {
        var body = JsonSerializer.SerializeToNode(fields)!.AsObject();
        body["source"] = "MindAttic.Orchestrator";
        body["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        File.WriteAllText(Path.Combine(folder, "rl-status.json"), body.ToJsonString());
    }

    /// <summary>M.ORCH-T559: a CLI on its own login spends its account's pool, so the global login's stop isn't its stop, and
    /// its account's stop holds it while the global login runs on.</summary>
    [Test]
    public void A_cli_on_its_own_login_is_held_by_its_accounts_pace_not_the_global_logins()
    {
        var (folder, env) = OwnLogin();
        Status(new { stop = true, stop_note = "At the target until 9:50 PM. End your turn now." });
        OwnStatus(folder, new { stop = false });
        Assert.That(Gate(ReadCall, env).Took, Is.LessThan(TimeSpan.FromSeconds(10)), "the global login's stop isn't this account's");

        OwnStatus(folder, new { stop = true, stop_note = "At the target until 9:50 PM. End your turn now." });
        Status(new { stop = false });
        using (var held = Run(AgentGate.ScriptName, ReadCall, env))
        {
            Assert.That(held.WaitForExit(7_000), Is.False, "its own account at the target holds it");
            held.Kill();
        }
        Assert.That(Gate(ReadCall).Took, Is.LessThan(TimeSpan.FromSeconds(10)), "and a CLI on the global login runs on");
    }

    [Test]
    public void A_cli_on_its_own_login_stops_at_its_accounts_own_meter_and_never_reads_the_global_one()
    {
        var (folder, env) = OwnLogin();
        Status(new { stop = false }, at: DateTime.UtcNow.AddMinutes(-10)); // the daemon is down
        ClaudeReports(99); // the global login at the line
        Assert.That(Gate(ReadCall, env).Took, Is.LessThan(TimeSpan.FromSeconds(10)), "another account's 99% isn't this one's");

        File.WriteAllText(Path.Combine(folder, "claude-usage.json"), JsonSerializer.Serialize(new
        {
            rate_limits = new { five_hour = new { used_percentage = 99, resets_at = new DateTimeOffset(DateTime.UtcNow.AddHours(1)).ToUnixTimeSeconds() } },
            at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        }));
        Assert.That(GateHeld(ReadCall, 2, env).Output, Does.Contain("\"deny\"").And.Contain("5-hour window used"), "its own meter at 99% refuses it");
    }

    [Test]
    public void A_cli_on_its_own_login_never_moves_the_global_sign_in_record()
    {
        var (_, env) = OwnLogin(uuid: "acct-b");
        File.WriteAllText(Path.Combine(dir, ".claude.json"), JsonSerializer.Serialize(new { oauthAccount = new { accountUuid = "acct-a" } }));
        Gate(ReadCall);
        var record = Path.Combine(Marks, MindAttic.Orchestrator.Budget.ClaudeAccount.FileName);
        Assert.That(File.ReadAllText(record), Does.Contain("acct-a"));
        var before = File.ReadAllText(record);
        Gate(ReadCall, env);
        Assert.That(File.ReadAllText(record), Is.EqualTo(before), "the other account isn't a /login switch of the global one");
    }

    /// <summary>M.ORCH-T786: a subagent the orchestrator aborted has every tool call refused with "stop now", so it stops between
    /// calls with its worktree left as it is; every other subagent and the CLI itself go on as before.</summary>
    [Test]
    public void A_subagent_the_orchestrator_aborted_has_every_call_refused_and_no_other()
    {
        var aborts = Path.Combine(Marks, MindAttic.Orchestrator.Scheduling.AgentAbort.DirName);
        Directory.CreateDirectory(aborts);
        File.WriteAllText(Path.Combine(aborts, "agent_1"), "{\"ticket\": \"A-T7\", \"why\": \"b@example.com's 5-hour window is on course to end near 118%\"}");

        foreach (var call in new object[]
                 {
                     new { tool_name = "Read", tool_input = new { file_path = "x" }, agent_id = "agent/1", cwd = ws },
                     new { tool_name = "Bash", tool_input = new { command = "git commit -m x" }, agent_id = "agent/1", cwd = ws },
                 })
        {
            var refused = JsonDocument.Parse(Gate(call).Output).RootElement.GetProperty("hookSpecificOutput");
            Assert.That(refused.GetProperty("permissionDecision").GetString(), Is.EqualTo("deny"));
            Assert.That(refused.GetProperty("permissionDecisionReason").GetString(), Does.StartWith("ABORTED by the orchestrator")
                .And.Contain("near 118%").And.Contain("A-T7's claim").And.Contain("Stop now"));
        }
        Assert.That(Gate(new { tool_name = "Read", tool_input = new { file_path = "x" }, agent_id = "agent_2", cwd = ws }).Output, Does.Not.Contain("ABORTED"), "another subagent");
        Assert.That(Gate(new { tool_name = "Read", tool_input = new { file_path = "x" }, cwd = ws }).Output, Does.Not.Contain("ABORTED"), "the CLI itself");
    }

    /// <summary>M.ORCH-T559: each login's subagents count against its own allowance only, marked, slotted and waited in its folder.</summary>
    [Test]
    public void A_cli_on_its_own_login_counts_only_its_accounts_subagents()
    {
        var (folder, env) = OwnLogin();
        var own = Path.Combine(folder, "session-context");
        var global = Path.Combine(Marks, "subagents");
        Directory.CreateDirectory(global);
        foreach (var m in new[] { "g1", "g2", "g3" }) File.WriteAllText(Path.Combine(global, m), "");
        Status(new { subagent_allowance = 1, subagents_running = 3 });
        OwnStatus(folder, new { subagent_allowance = 1, subagents_running = 0 });

        var agent = new { tool_name = "Agent", tool_input = new { prompt = "x" } };
        Assert.That(Gate(agent, env).Took, Is.LessThan(TimeSpan.FromSeconds(10)), "the global login's full allowance isn't this account's");
        Assert.That(Directory.GetFiles(Path.Combine(own, AgentGate.SlotDirName)), Has.Length.EqualTo(1), "its slot is in its own folder");
        Assert.That(Directory.Exists(Path.Combine(Marks, AgentGate.SlotDirName)) ? Directory.GetFiles(Path.Combine(Marks, AgentGate.SlotDirName)) : [], Is.Empty);

        Gate(new { hook_event_name = "SubagentStart", session_id = "own1", agent_id = "a1", cwd = ws }, env, AgentGate.ContextScriptName);
        Assert.That(File.Exists(Path.Combine(own, "subagents", "a1")), Is.True, "its subagent is marked in its folder");
        Assert.That(File.Exists(Path.Combine(global, "a1")), Is.False, "and never fills the global login's allowance");
        Assert.That(Directory.GetFiles(Path.Combine(own, AgentGate.SlotDirName)), Is.Empty, "its start let its own slot go");

        using var held = Run(AgentGate.ScriptName, agent, env);
        Assert.That(held.WaitForExit(7_000), Is.False, "its own one subagent fills its own allowance of one");
        held.Kill();
    }

    [Test]
    public void A_cli_on_its_own_login_is_recorded_so_the_daemon_paces_its_tokens_on_its_account()
    {
        var (folder, env) = OwnLogin();
        Gate(new { hook_event_name = "UserPromptSubmit", session_id = "own2", prompt = "hi", cwd = ws }, env, AgentGate.ContextScriptName);
        Gate(new { hook_event_name = "UserPromptSubmit", session_id = "glob2", prompt = "hi", cwd = ws }, AgentGate.ContextScriptName);
        var logins = new MindAttic.Orchestrator.Sessions.ContextCycler(Marks).OnOwnLogin();
        Assert.That(logins.Keys, Is.EquivalentTo(new[] { "own2" }));
        Assert.That(new MindAttic.Orchestrator.Sessions.AccountDirs(Path.Combine(dir, ".claude-accounts")).Of(logins["own2"])?.Email, Is.EqualTo("b@example.com"));
    }
}
