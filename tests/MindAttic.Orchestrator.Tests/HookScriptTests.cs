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
        foreach (var (k, v) in env ?? new Dictionary<string, string>()) psi.Environment[k] = v;
        var p = Process.Start(psi)!;
        p.StandardInput.Write(JsonSerializer.Serialize(input));
        p.StandardInput.Close();
        return p;
    }

    private (string Output, TimeSpan Took) Gate(object input, string script = AgentGate.ScriptName, params string[] args)
    {
        var clock = Stopwatch.StartNew();
        using var p = Run(script, input, args);
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
    public void At_the_stop_a_goal_ends_its_turn_instead_of_looping_and_a_new_one_isnt_started()
    {
        object Typed(string sid, string prompt) => new { hook_event_name = "UserPromptSubmit", session_id = sid, prompt, cwd = ws };
        Status(new { });
        Gate(Typed("g1", "/goal all the tests pass"), AgentGate.ContextScriptName);

        Status(new { stop = true });
        var stop = Gate(new { hook_event_name = "Stop", session_id = "g1", cwd = ws }, AgentGate.ContextScriptName).Output;
        Assert.That(stop, Does.Contain("\"continue\": false"), "the goal's own Stop hook would send it back to calls that are all refused");
        Assert.That(Gate(Typed("g2", "/goal all the tests pass"), AgentGate.ContextScriptName).Output, Does.Contain("Not sent").And.Contain("/goal clear still go through"),
            "a new goal at the stop is a turn past the line, and the refusal doesn't say /goal goes through");
        Assert.That(Gate(Typed("g1", "/goal clear"), AgentGate.ContextScriptName).Output, Does.Not.Contain("Not sent"), "clearing one goes through");
        Assert.That(Gate(new { hook_event_name = "Stop", session_id = "g3", cwd = ws }, AgentGate.ContextScriptName).Output, Is.Empty,
            "no goal: an ordinary turn ending at the stop just ends");
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
        Assert.That(File.ReadAllText(Path.Combine(Claude, AgentGate.ScriptName)), Does.Contain("claude_stop_note(own)"), "and refuses it saying why");

        ClaudeReports(50, resetUtc: DateTime.UtcNow.AddHours(2));
        File.Delete(Path.Combine(Marks, AgentGate.StopLatchName)); // the window it was seen in has reset
        Assert.That(Gate(call).Took, Is.LessThan(TimeSpan.FromSeconds(10)), "below the line it runs");
        ClaudeReports(99.5, resetUtc: DateTime.UtcNow.AddMinutes(-1));
        Assert.That(Gate(call).Took, Is.LessThan(TimeSpan.FromSeconds(10)), "a window that already reset doesn't stop anything");
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
        Assert.That(Gate(Asking("w6"), AgentGate.ContextScriptName).Output, Does.Contain("\"block\""), "approved (any case): it is");
        Status(new { stop = true });
        Assert.That(Gate(Asking("w4"), AgentGate.ContextScriptName).Output, Is.Empty, "at the target, a stop is let stop");
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
                                  "cd x && git push -u origin master", "git push origin master:master", "git -c core.x=1 push origin master" })
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
    public void The_wind_down_note_is_appended_to_a_nudge()
    {
        AutopilotWithOpenFixes();
        Status(new { wind_down_note = "Past 90%: finish or file it." });
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
}
