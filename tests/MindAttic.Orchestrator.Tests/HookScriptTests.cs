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

    private Process Run(string script, object input, params string[] args)
    {
        var psi = new ProcessStartInfo("python") { RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false };
        psi.ArgumentList.Add("-I");
        psi.ArgumentList.Add(Path.Combine(Claude, script));
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.Environment["USERPROFILE"] = dir;
        psi.Environment["APPDATA"] = dir; // the orchestrator's settings (approvals) come from here, never this machine's
        psi.Environment["MINDATTIC_CLI_PID"] = Cli.ToString(); // as one CLI process, never the one running these tests
        psi.Environment["MINDATTIC_WORKSPACE"] = ws;
        var p = Process.Start(psi)!;
        p.StandardInput.Write(JsonSerializer.Serialize(input));
        p.StandardInput.Close();
        return p;
    }

    private (string Output, TimeSpan Took) Gate(object input, string script = AgentGate.ScriptName)
    {
        var clock = Stopwatch.StartNew();
        using var p = Run(script, input);
        var output = p.StandardOutput.ReadToEnd();
        Assert.That(p.WaitForExit(30_000), Is.True);
        return (output, clock.Elapsed);
    }

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
        Assert.That(Gate(call).Took, Is.LessThan(TimeSpan.FromSeconds(10)), "stale: the orchestrator isn't running, nothing paces");

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
        Assert.That(File.ReadAllText(Path.Combine(Claude, AgentGate.ScriptName)), Does.Contain("refuse(status().get(\"stop_note\")"),
            "past the longest hold it's refused, not let through");
    }

    private void ClaudeReports(double percent, DateTime? resetUtc = null) =>
        File.WriteAllText(Path.Combine(Claude, "claude-usage.json"), JsonSerializer.Serialize(new
        {
            rate_limits = new { five_hour = new { used_percentage = percent, resets_at = new DateTimeOffset(resetUtc ?? DateTime.UtcNow.AddHours(1)).ToUnixTimeSeconds() } },
            at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        }));

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

        ClaudeReports(50);
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
                                  "cd x && git push -u origin master" })
            Assert.That(Gate(Bash(c)).Output, Does.Contain("\"deny\"").And.Contain("HEAD:main"), c);
        foreach (var c in new[] { "git push origin main", "git push origin HEAD:main", "git log master..main", "git push origin --delete master",
                                  "echo 'git push origin master' > notes.txt" })
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
}
