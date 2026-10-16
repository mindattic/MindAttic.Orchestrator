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
            "import sqlite3,sys; d=sqlite3.connect(sys.argv[1]); d.execute('create table tickets(id,repo,status,ordinal)'); " +
            "d.execute(\"insert into tickets values('USS-T1','UnitySolarSystem','open',1)\"); d.commit()", db }, UseShellExecute = false })!)
            mk.WaitForExit(20_000);
        var uss = Path.Combine(ws, "UnitySolarSystem");
        object Asking(string sid) => new { hook_event_name = "Stop", session_id = sid, cwd = uss, last_assistant_message = "Shall I start the next one?" };
        Status(new { });
        Assert.That(Gate(Asking("w3"), AgentGate.ContextScriptName).Output, Does.Contain("\"block\""), "open tickets: told to carry on");
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
        Assert.That(Gate(Bash("workflow ticket close MAO-T1 --repo MindAttic.Workflow --ref x")).Output, Does.Contain("\"deny\""));
        Assert.That(Gate(Bash("workflow ticket correct MAO-T1 --repo MindAttic.Workflow --summary x")).Output, Does.Contain("\"deny\""));
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
    public void Autopilot_repo_lists_drop_the_on_off_words()
    {
        object Prompt(string prompt) => new { hook_event_name = "UserPromptSubmit", session_id = "s1", prompt, cwd = ws };
        Gate(Prompt("/autopilot -repo=a,b on"), AgentGate.ContextScriptName);
        Assert.That(File.ReadAllText(Path.Combine(Marks, "autopilot.order")), Is.EqualTo("a,b"));
        Gate(Prompt("/autopilot this"), AgentGate.ContextScriptName);
        Assert.That(File.ReadAllText(Path.Combine(Marks, "autopilot.order")), Is.Empty, "the workspace root is no repo of its own");
        Gate(Prompt("/autopilot all"), AgentGate.ContextScriptName);
        Assert.That(File.ReadAllText(Path.Combine(Marks, "autopilot.order")), Is.EqualTo(MindAttic.Orchestrator.Scheduling.Autopilot.AllRepos));
        Gate(Prompt("/autopilot off"), AgentGate.ContextScriptName);
        Assert.That(File.Exists(Path.Combine(Marks, "autopilot.order")), Is.False);
    }

    [Test]
    public void A_turn_hears_the_subagent_note_only_from_a_fresh_orchestrator_file()
    {
        object Prompt() => new { hook_event_name = "UserPromptSubmit", session_id = "s2", prompt = "hi", cwd = ws };
        Status(new { subagent_note = "Subagents: up to 3." });
        Assert.That(Gate(Prompt(), AgentGate.ContextScriptName).Output, Does.Contain("Subagents: up to 3."));
        Status(new { subagent_note = "Subagents: up to 3." }, at: DateTime.UtcNow.AddMinutes(-10));
        Assert.That(Gate(Prompt(), AgentGate.ContextScriptName).Output, Is.Empty);
    }

    [Test]
    public void The_context_warning_comes_once_a_turn_and_never_under_a_goal()
    {
        Directory.CreateDirectory(Marks);
        File.WriteAllText(Path.Combine(Marks, "s3.json"), """{ "used_percentage": 85 }""");
        object Tool() => new { hook_event_name = "PostToolUse", session_id = "s3", cwd = ws, tool_input = new { command = "ls" } };
        Gate(new { hook_event_name = "UserPromptSubmit", session_id = "s3", prompt = "go", cwd = ws }, AgentGate.ContextScriptName);
        Assert.That(Gate(Tool(), AgentGate.ContextScriptName).Output, Does.Contain("Context is 85% full"));
        Assert.That(Gate(Tool(), AgentGate.ContextScriptName).Output, Is.Empty, "once per turn");

        Gate(new { hook_event_name = "UserPromptSubmit", session_id = "s3", prompt = "/goal tests pass", cwd = ws }, AgentGate.ContextScriptName);
        Assert.That(Gate(Tool(), AgentGate.ContextScriptName).Output, Is.Empty, "a /goal is left to Claude's own compaction");
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
