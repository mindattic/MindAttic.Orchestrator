using System.Diagnostics;
using System.Text.Json;
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
        foreach (var r in new[] { "UnitySolarSystem", "MindAttic.Workflow" }) Directory.CreateDirectory(Path.Combine(ws, r, ".git"));
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
        psi.Environment.Remove("MINDATTIC_WORKFLOW_DB"); // the scratch workspace's database, never one this machine names
        // Keep-going never asks this machine's orchestrator what `next` would hand out: none is found unless a test sets one.
        psi.Environment["MINDATTIC_ORCHESTRATOR"] = Path.Combine(dir, "no-orchestrator.exe");
        // Inside claude.exe on its /login, whatever the machine running the tests is signed in with.
        foreach (var auth in AuthVariables) psi.Environment.Remove(auth);
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
    private (string Output, TimeSpan Took) GateHeld(object input, int maxHold)
    {
        var clock = Stopwatch.StartNew();
        using var p = Run(AgentGate.ScriptName, input, MaxHold(maxHold));
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
        var db = Path.Combine(ws, "MindAttic.Workflow", "data", "workflow.db");
        Directory.CreateDirectory(Path.GetDirectoryName(db)!);
        var values = string.Join(",", rows.Select(r => $"('{r.Id}','{r.Repo}','open',1,'fix')"));
        using var mk = Process.Start(new ProcessStartInfo("python") { ArgumentList = { "-I", "-c",
            "import sqlite3,sys; d=sqlite3.connect(sys.argv[1]); d.execute('create table tickets(id,repo,status,ordinal,type)'); " +
            $"d.execute(\"insert into tickets values{values}\"); d.commit()", db }, UseShellExecute = false })!;
        Assert.That(mk.WaitForExit(20_000), Is.True);
    }

    private static readonly object ReadCall = new { tool_name = "Read", tool_input = new { file_path = "x" } };

    private string[] GateLog()
    {
        var path = Path.Combine(Marks, AgentGate.GateLogName);
        return File.Exists(path) ? File.ReadAllLines(path) : [];
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
        var db = Path.Combine(ws, "MindAttic.Workflow", "data", "workflow.db");
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
        File.WriteAllText(Path.Combine(Marks, $"autopilot-{Cli}"), "UnitySolarSystem");
        object Asking(string sid) => new { hook_event_name = "Stop", session_id = sid, cwd = uss, last_assistant_message = "Shall I start the next one?" };
        Status(new { });

        Assert.That(Gate(Asking("b1"), AgentGate.ContextScriptName).Output, Is.Empty, "its only fix requires an open ticket");
        Sql("delete from links; update tickets set summary='blocked by U.SPC-T2 for now' where id='U.SPC-T1';");
        Assert.That(Gate(Asking("b2"), AgentGate.ContextScriptName).Output, Is.Empty, "'blocked by' in its summary counts the same");
        Sql("update tickets set status='closed' where id='U.SPC-T2';");
        Assert.That(Gate(Asking("b3"), AgentGate.ContextScriptName).Output, Does.Contain("\"block\""), "its blocker closed: it's work again");
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
        var db = Path.Combine(ws, "MindAttic.Workflow", "data", "workflow.db");
        Directory.CreateDirectory(Path.GetDirectoryName(db)!);
        using (var mk = Process.Start(new ProcessStartInfo("python") { ArgumentList = { "-I", "-c",
            "import sqlite3,sys; d=sqlite3.connect(sys.argv[1]); d.execute('create table tickets(id,repo,status,ordinal,type)'); " +
            "d.execute(\"insert into tickets values('U.SPC-T1','UnitySolarSystem','open',1,'fix')\"); d.commit()", db }, UseShellExecute = false })!)
            mk.WaitForExit(20_000);
        var uss = Path.Combine(ws, "UnitySolarSystem");
        Directory.CreateDirectory(Marks);
        File.WriteAllText(Path.Combine(Marks, $"autopilot-{Cli}"), "UnitySolarSystem");
        var store = Path.Combine(dir, "MindAttic", "MindAttic.Orchestrator");
        Directory.CreateDirectory(store);
        var holds = Path.Combine(store, Daemon.AutopilotHoldsName + ".json");
        var settings = Path.Combine(store, "settings.json");
        object Asking(string sid) => new { hook_event_name = "Stop", session_id = sid, cwd = uss, last_assistant_message = "Shall I start the next one?" };
        Status(new { });
        string Asked(string sid) => Gate(Asking(sid), AgentGate.ContextScriptName).Output;

        Assert.That(Asked("h1"), Does.Contain("\"block\""), "nothing held: it's work");
        // Written as the daemon writes it, so the names the hook reads are the ones it gets.
        new MindAttic.Orchestrator.Settings.JsonStore(store).Save(Daemon.AutopilotHoldsName,
            new Daemon.AutopilotHolds(false, [new MindAttic.Orchestrator.Scheduling.WorkedTicket("U.SPC-T1", "other", null, "filed", Filed: true)], []));
        Assert.That(Asked("h2"), Is.Empty, "another live session filed it and is fixing it: next won't hand it out");
        Assert.That(Asked("other"), Does.Contain("\"block\""), "the session that filed it isn't held from it");
        new MindAttic.Orchestrator.Settings.JsonStore(store).Save(Daemon.AutopilotHoldsName,
            new Daemon.AutopilotHolds(false, [new MindAttic.Orchestrator.Scheduling.WorkedTicket("U.SPC-T1", "started", null, "started on it")], []));
        Assert.That(Asked("started"), Is.Empty, "but the one it was started on is its own work, not one for its subagents (M.ORCH-T328)");
        File.WriteAllText(holds, """{"Quiet":true,"Worked":[],"LivePilot":[]}""");
        Assert.That(Asked("h3"), Is.Empty, "quiet hours");
        File.WriteAllText(holds, """{"Quiet":false,"Worked":[],"LivePilot":[]}""");
        File.WriteAllText(settings, """{ "Autopilot": false }""");
        Assert.That(Asked("h4"), Is.Empty, "autopilot paused");
        File.WriteAllText(settings, """{ "AutopilotSkip": ["unitysolarsystem"] }""");
        Assert.That(Asked("h5"), Is.Empty, "a skipped repo");
    }

    [Test]
    public void Keep_going_pushes_a_stop_back_only_for_what_orchestrator_next_would_hand_out()
    {
        // M.ORCH-T385: another live session filed U.SPC-T1 a moment ago. `next` held it back at once ("already being worked"),
        // but the daemon's holds hadn't caught up, so every stop was sent back for it.
        var db = Path.Combine(ws, "MindAttic.Workflow", "data", "workflow.db");
        Directory.CreateDirectory(Path.GetDirectoryName(db)!);
        using (var mk = Process.Start(new ProcessStartInfo("python") { ArgumentList = { "-I", "-c",
            "import sqlite3,sys; d=sqlite3.connect(sys.argv[1]); d.execute('create table tickets(id,repo,status,ordinal,type)'); " +
            "d.execute(\"insert into tickets values('U.SPC-T1','UnitySolarSystem','open',1,'fix')\"); d.commit()", db }, UseShellExecute = false })!)
            mk.WaitForExit(20_000);
        var uss = Path.Combine(ws, "UnitySolarSystem");
        Directory.CreateDirectory(Marks);
        File.WriteAllText(Path.Combine(Marks, $"autopilot-{Cli}"), "UnitySolarSystem");
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
    public void A_claim_gone_stale_is_work_again_as_orchestrator_next_hands_it_out()
    {
        var db = Path.Combine(ws, "MindAttic.Workflow", "data", "workflow.db");
        Directory.CreateDirectory(Path.GetDirectoryName(db)!);
        using (var mk = Process.Start(new ProcessStartInfo("python") { ArgumentList = { "-I", "-c",
            "import sqlite3,sys; d=sqlite3.connect(sys.argv[1]); d.execute('create table tickets(id,repo,status,ordinal,type)'); " +
            "d.execute(\"insert into tickets values('U.SPC-T1','UnitySolarSystem','open',1,'fix')\"); d.commit()", db }, UseShellExecute = false })!)
            mk.WaitForExit(20_000);
        var uss = Path.Combine(ws, "UnitySolarSystem");
        Directory.CreateDirectory(Marks);
        File.WriteAllText(Path.Combine(Marks, $"autopilot-{Cli}"), "UnitySolarSystem");
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
        var db = Path.Combine(ws, "MindAttic.Workflow", "data", "workflow.db");
        Directory.CreateDirectory(Path.GetDirectoryName(db)!);
        using (var mk = Process.Start(new ProcessStartInfo("python") { ArgumentList = { "-I", "-c",
            "import sqlite3,sys; d=sqlite3.connect(sys.argv[1]); d.execute('create table tickets(id,repo,status,ordinal,type)'); " +
            "d.execute(\"insert into tickets values('U.SPC-T1','UnitySolarSystem','open',1,'fix')\"); d.commit()", db }, UseShellExecute = false })!)
            mk.WaitForExit(20_000);
        var uss = Path.Combine(ws, "UnitySolarSystem");
        Directory.CreateDirectory(Marks);
        File.WriteAllText(Path.Combine(Marks, $"autopilot-{Cli}"), "UnitySolarSystem");
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
        // An /autopilot run's stops are all pushed back, so the Stop that used to record the pid never comes.
        Gate(new { hook_event_name = "UserPromptSubmit", session_id = "p1", prompt = "/autopilot", cwd = ws }, AgentGate.ContextScriptName);
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
        Assert.That(Gate(Prompt("/autopilot off"), AgentGate.ContextScriptName).Output, Does.Not.Contain("\"block\""));

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
    public void Wind_down_is_told_at_the_turn_and_once_mid_turn_and_a_stop_is_never_pushed_back_to_work()
    {
        Status(new { wind_down_note = "Past 90%: finish or file it." });
        Assert.That(Gate(new { hook_event_name = "UserPromptSubmit", session_id = "w1", prompt = "go", cwd = ws }, AgentGate.ContextScriptName).Output,
            Does.Contain("Past 90%"));

        Status(new { });
        Gate(new { hook_event_name = "UserPromptSubmit", session_id = "w2", prompt = "go", cwd = ws }, AgentGate.ContextScriptName);
        Status(new { wind_down_note = "Past 90%: finish or file it." });
        object Tool() => new { hook_event_name = "PostToolUse", session_id = "w2", cwd = ws, tool_input = new { command = "ls" } };
        Assert.That(Gate(Tool(), AgentGate.ContextScriptName).Output, Does.Contain("Past 90%"), "crossed mid-turn");
        Assert.That(Gate(Tool(), AgentGate.ContextScriptName).Output, Is.Empty, "once");

        var db = Path.Combine(ws, "MindAttic.Workflow", "data", "workflow.db");
        Directory.CreateDirectory(Path.GetDirectoryName(db)!);
        using (var mk = Process.Start(new ProcessStartInfo("python") { ArgumentList = { "-I", "-c",
            "import sqlite3,sys; d=sqlite3.connect(sys.argv[1]); d.execute('create table tickets(id,repo,status,ordinal,type)'); " +
            "d.execute(\"insert into tickets values('U.SPC-T1','UnitySolarSystem','open',1,'fix')\"); d.commit()", db }, UseShellExecute = false })!)
            mk.WaitForExit(20_000);
        var uss = Path.Combine(ws, "UnitySolarSystem");
        File.WriteAllText(Path.Combine(Marks, $"autopilot-{Cli}"), "UnitySolarSystem"); // keep-going acts only in the CLI /autopilot is on in
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

        Status(new { wind_down_note = "Past 90%.", window_reset = reset.ToString("yyyy-MM-ddTHH:mm:ssZ") });
        Gate(Ends("h3", "Handoff saved: continue after 3:30 PM with the docket review."), AgentGate.ContextScriptName);
        Assert.That(Held("h3")!.Value.GetProperty("until").GetDouble(), Is.EqualTo(Epoch(reset)).Within(1), "past the wind-down line, leaving work for the reset");
        Gate(Ends("h4", "All done: the tests pass and it's pushed."), AgentGate.ContextScriptName);
        Assert.That(Held("h4"), Is.Null, "past the wind-down line with nothing left for the reset: not waiting on it");

        Status(new { });
        Gate(Ends("h5", "Stopping until the limit resets."), AgentGate.ContextScriptName);
        Assert.That(Held("h5"), Is.Null, "nothing holds the window: not waiting on it");

        Gate(new { hook_event_name = "UserPromptSubmit", session_id = "h3", prompt = "carry on", cwd = ws }, AgentGate.ContextScriptName);
        Assert.That(Held("h3"), Is.Null, "a new prompt (the daemon's resume, or yours) is the CLI going on");
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
        Assert.That(Gate(Bash("workflow ticket new --repo MindAttic.Workflow --type chore --summary 'bring it up to date'")).Output, Is.Empty,
            "the edit refusal itself says to file a ticket for the other repo");
        Assert.That(Gate(Bash("workflow ticket close M.ORCH-T1 --repo MindAttic.Workflow --ref x")).Output, Does.Contain("\"deny\""));
        Assert.That(Gate(Bash("workflow ticket correct M.ORCH-T1 --repo MindAttic.Workflow --summary x")).Output, Does.Contain("\"deny\""));
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
                                  "powershell -NoProfile -File deploy.ps1" })
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
    public void Autopilot_takes_repo_names_or_wildcards_and_its_repos_are_this_clis_cross_repo_exception()
    {
        var mark = Path.Combine(Marks, $"autopilot-{Cli}");
        object Prompt(string prompt) => new { hook_event_name = "UserPromptSubmit", session_id = "s1", prompt, cwd = ws };
        Gate(Prompt("/autopilot -repo=a,b on"), AgentGate.ContextScriptName);
        Assert.That(File.ReadAllText(mark), Is.EqualTo("a,b"));
        Assert.That(Gate(Prompt("/autopilot -repos=MindAttic.Workflow"), AgentGate.ContextScriptName).Output,
            Does.Contain("\"block\"").And.Contain("-repos is gone"), "the -repos alias is turned away, not guessed at");
        Assert.That(File.ReadAllText(mark), Is.EqualTo("a,b"), "and changes nothing");

        // Its subagents edit and close tickets in the repos it's working, though this CLI sits in another repo.
        var uss = Path.Combine(ws, "UnitySolarSystem");
        var file = Path.Combine(ws, "MindAttic.Workflow", "src", "x.cs");
        Assert.That(Gate(new { tool_name = "Edit", tool_input = new { file_path = file }, cwd = uss }).Output, Does.Contain("\"deny\""), "not named yet");
        Gate(Prompt("/autopilot -repo=MINDATTIC.*"), AgentGate.ContextScriptName);
        Assert.That(File.ReadAllText(mark), Is.EqualTo("a,b,MINDATTIC.*"), "a wildcard is kept as typed");
        Assert.That(Gate(new { tool_name = "Edit", tool_input = new { file_path = file }, cwd = uss }).Output, Is.Empty, "MindAttic.* names MindAttic.Workflow");
        Assert.That(Gate(new { tool_name = "Edit", tool_input = new { file_path = Path.Combine(uss, "x.cs") }, cwd = Path.Combine(ws, "MindAttic.Workflow") }).Output,
            Does.Contain("\"deny\""), "but not UnitySolarSystem");
        Gate(Prompt("/autopilot off"), AgentGate.ContextScriptName);
        Assert.That(File.Exists(mark), Is.False);
        Assert.That(Gate(new { tool_name = "Edit", tool_input = new { file_path = file }, cwd = uss }).Output, Does.Contain("\"deny\""), "off: back to its own repo");
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

    // ---- keep-going (M.ORCH-T140): what context-gate.py does when an /autopilot turn stops ----

    private string Uss => Path.Combine(ws, "UnitySolarSystem");
    private string KeepGoingLog => Path.Combine(Marks, "keep-going.jsonl");

    /// <summary>Two open fixes for UnitySolarSystem in the workflow db, and /autopilot on for it in this CLI.</summary>
    private void AutopilotWithOpenFixes()
    {
        var db = Path.Combine(ws, "MindAttic.Workflow", "data", "workflow.db");
        Directory.CreateDirectory(Path.GetDirectoryName(db)!);
        using (var mk = Process.Start(new ProcessStartInfo("python") { ArgumentList = { "-I", "-c",
            "import sqlite3,sys; d=sqlite3.connect(sys.argv[1]); d.execute('create table tickets(id,repo,status,ordinal,type)'); " +
            "d.execute(\"insert into tickets values('U.SPC-T1','UnitySolarSystem','open',1,'fix')\"); " +
            "d.execute(\"insert into tickets values('U.SPC-T2','UnitySolarSystem','open',2,'fix')\"); d.commit()", db }, UseShellExecute = false })!)
            Assert.That(mk.WaitForExit(20_000), Is.True);
        Directory.CreateDirectory(Marks);
        File.WriteAllText(Path.Combine(Marks, $"autopilot-{Cli}"), "UnitySolarSystem");
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
        AutopilotWithOpenFixes();
        Status(new { });
        Turn("k1", "go");
        Ran("k1", "workflow ticket close U.SPC-T1 --ref abc123");
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
            l.GetProperty("why").GetString(), l.GetProperty("open").GetInt32())), Is.All.EqualTo(("UnitySolarSystem", "k1", "Claude", "autopilot", 2)));

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
        AutopilotWithOpenFixes();
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
    public void Past_the_wind_down_line_only_an_unthrottled_cli_is_nudged_and_hears_the_note()
    {
        AutopilotWithOpenFixes();
        Status(new { wind_down_note = "Past 90%: finish or file it." });
        Assert.That(Stopped("k1"), Is.Empty, "orchestrator next starts nothing new past the line (M.ORCH-T254): a nudge would only waste a turn");
        File.WriteAllText(Path.Combine(Marks, $"unthrottled-{Cli}"), "x");
        var reason = Reason(Stopped("k2"));
        Assert.That(reason, Does.StartWith("Don't stop").And.EndWith(" Past 90%: finish or file it."), "after the carry-on, so the agent hears both");

        Status(new { });
        Assert.That(Reason(Stopped("k3")), Does.Not.Contain("Past 90%"), "no note, nothing appended");
    }

    [Test]
    public void A_goal_turn_is_not_nudged_until_goal_clear_or_its_stop_summary_ends_the_goal()
    {
        AutopilotWithOpenFixes();
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
    public void Keep_going_reads_the_workflow_database_MINDATTIC_WORKFLOW_DB_names()
    {
        // M.ORCH-T398: as the orchestrator and the workflow CLI do; the workspace's own (two open fixes) is not the one in use.
        AutopilotWithOpenFixes();
        Status(new { });
        var elsewhere = Path.Combine(dir, "elsewhere.db");
        using (var mk = Process.Start(new ProcessStartInfo("python") { ArgumentList = { "-I", "-c",
            "import sqlite3,sys; d=sqlite3.connect(sys.argv[1]); d.execute('create table tickets(id,repo,status,ordinal,type)'); d.commit()", elsewhere },
            UseShellExecute = false })!)
            Assert.That(mk.WaitForExit(20_000), Is.True);
        var env = new Dictionary<string, string> { ["MINDATTIC_WORKFLOW_DB"] = elsewhere };
        Gate(new { hook_event_name = "UserPromptSubmit", session_id = "w1", prompt = "go", cwd = Uss }, env, AgentGate.ContextScriptName);

        var stop = Gate(new { hook_event_name = "Stop", session_id = "w1", cwd = Uss, last_assistant_message = "Done." }, env, AgentGate.ContextScriptName).Output;

        Assert.That(stop, Does.Not.Contain("\"block\""), "no open ticket in the database in use: nothing to keep going on");
        Assert.That(Stopped("w2"), Does.Contain("\"block\""), "without it, the workspace's database and its open fixes");
    }

    [Test]
    public void A_goal_with_a_non_ascii_character_is_kept_as_typed_so_its_stop_summary_ends_it()
    {
        // Read in the ANSI code page, "café" was kept garbled and never matched the summary, so the goal never ended (M.ORCH-T392).
        AutopilotWithOpenFixes();
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
        // The installer hooks both up to context-gate.py (codex / gemini on the command line), but /autopilot runs in a
        // Claude CLI: theirs are let stop, with nothing logged and no session state written (84d9b91).
        AutopilotWithOpenFixes();
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
            pace_delay_seconds = 60, subagent_allowance = 0, subagents_running = 0, wind_down_note = "Past 90%.",
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
    public void The_repo_window_guard_looks_up_a_closes_first_ticket_argument_in_the_workflow_db()
    {
        Tickets(("U.SPC-T1", "UnitySolarSystem"), ("M.WF-T7", "MindAttic.Workflow"));
        var uss = Path.Combine(ws, "UnitySolarSystem");
        object Bash(string command) => new { tool_name = "Bash", tool_input = new { command }, cwd = uss };

        Assert.That(Gate(Bash("workflow ticket close M.WF-T7 --ref abc123")).Output,
            Does.Contain("\"deny\"").And.Contain("belongs to MindAttic.Workflow"), "no --repo: the db says whose ticket it is");
        Assert.That(Gate(Bash("cd x && workflow ticket close M.WF-T7 --ref abc")).Output, Does.Contain("\"deny\""), "as any segment of a command");
        Assert.That(Gate(Bash("workflow ticket close U.SPC-T1 --ref M.WF-T7 --summary 'like M.WF-T7'")).Output, Is.Empty,
            "this repo's own ticket; the IDs in --ref and --summary are just text");
        Assert.That(Gate(Bash("workflow ticket correct M.WF-T7 --summary x")).Output, Does.Contain("\"deny\""), "correct is looked up the same way");
        Assert.That(Gate(Bash("workflow ticket close X.NOPE-T1 --ref x")).Output, Is.Empty, "a ticket the db doesn't know isn't refused");
    }

    [Test]
    public void An_exempt_commands_turn_skips_every_pace_gate_until_it_stops_and_is_logged_as_exempt()
    {
        // M.ORCH-T198: a turn typed as /deploy (a default exempt command) is let through at once: no delay, no subagent wait, no stop.
        object Prompt(string prompt) => new { hook_event_name = "UserPromptSubmit", session_id = "e1", prompt, cwd = ws };
        object Call(string tool) => new { tool_name = tool, session_id = "e1", tool_input = new { prompt = "x", file_path = "x" }, cwd = ws };
        Status(new { stop = true, pace_delay_seconds = 60, subagent_allowance = 0, subagents_running = 3, reset_time = "9:50 PM" });
        Assert.That(Gate(Prompt("/deploy now"), AgentGate.ContextScriptName).Output, Does.Not.Contain("\"block\""), "typed at the stop, it still goes through");
        foreach (var tool in new[] { "Read", "Agent", "Bash" })
        {
            var (output, took) = Gate(Call(tool));
            Assert.That((output, took < TimeSpan.FromSeconds(10)), Is.EqualTo(("", true)), $"{tool} runs at once inside the exempt turn");
        }
        Assert.That(GateLog().Count(l => l.Contains("\"decision\": \"exempt\"")), Is.EqualTo(3), "each exempt call is logged so its spend is counted");
        Assert.That(Directory.Exists(Path.Combine(Marks, AgentGate.SlotDirName)) ? Directory.GetFiles(Path.Combine(Marks, AgentGate.SlotDirName)) : [], Is.Empty,
            "an exempt Agent call takes no slot");

        Gate(new { hook_event_name = "Stop", session_id = "e1", cwd = ws }, AgentGate.ContextScriptName);
        Assert.That(GateHeld(Call("Read"), 2).Output, Does.Contain("\"deny\""), "the exemption ends when the turn stops");

        Status(new { pace_delay_seconds = 60 });
        Gate(Prompt("/deployment plan"), AgentGate.ContextScriptName);
        using (var held = Run(AgentGate.ScriptName, Call("Read")))
        {
            Assert.That(held.WaitForExit(5_000), Is.False, "only the command itself, not a longer word that starts like it");
            held.Kill();
        }
    }

    [Test]
    public void A_skill_naming_an_exempt_command_is_let_through_anywhere_and_the_list_comes_from_settings()
    {
        Status(new { stop = true, pace_delay_seconds = 60 });
        object Skill(string skill) => new { tool_name = "Skill", session_id = "k1", tool_input = new { skill }, cwd = ws };
        Assert.That(Gate(Skill("run")).Output, Is.Empty, "outside any exempt turn, the Skill call itself goes through");
        Assert.That(GateLog().Single(), Does.Contain("\"decision\": \"exempt\"").And.Contain("\"tool\": \"Skill\""));
        Assert.That(GateHeld(Skill("bughunt"), 2).Output, Does.Contain("\"deny\""), "a skill that isn't exempt is paced as ever");

        var settings = Path.Combine(dir, "MindAttic", "MindAttic.Orchestrator", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
        File.WriteAllText(settings, """{ "ExemptCommands": ["bughunt"] }""");
        Assert.That(Gate(Skill("bughunt")).Output, Is.Empty, "the settings list is read live, with or without the slash");
        Assert.That(GateHeld(Skill("run"), 2).Output, Does.Contain("\"deny\""), "and replaces the defaults");
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
}
