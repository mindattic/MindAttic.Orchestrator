using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class ContextCyclerTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 2, 0, 0, DateTimeKind.Utc);

    [Test]
    public void A_session_is_cycled_only_past_the_point_and_after_its_turn_has_settled()
    {
        SessionContext S(double percent, bool idle, double secondsAgo) => new("s", @"D:\W\A", percent, idle, Now.AddSeconds(-secondsAgo));

        Assert.That(ContextCycler.Due(S(85, idle: true, 30), 80, Now), Is.True);
        Assert.That(ContextCycler.Due(S(70, idle: true, 30), 80, Now), Is.False, "room left: keep using it");
        Assert.That(ContextCycler.Due(S(85, idle: false, 30), 80, Now), Is.False, "mid-turn: its subagents may still be running");
        Assert.That(ContextCycler.Due(S(85, idle: true, 2), 80, Now), Is.False, "just stopped: give a follow-up a moment");
    }

    [Test]
    public void It_only_types_into_the_one_claude_tab_rooted_where_the_session_runs()
    {
        var s = new SessionContext("s", @"D:\W\A", 85, true, Now);
        HostSession H(int pid, string provider, string dir) => new(provider, pid, "pipe-" + pid, dir);

        Assert.That(ContextCycler.HostFor(s, [H(1, "Claude", @"D:\W\B"), H(2, "Claude", @"d:\w\a\"), H(3, "Codex", @"D:\W\A")], out _)?.ProcessId, Is.EqualTo(2));
        Assert.That(ContextCycler.HostFor(s, [H(1, "Claude", @"D:\W\B")], out var none), Is.Null);
        Assert.That(none, Does.Contain("isn't in a Launcher tab"), "a plain terminal can't be typed into");
        Assert.That(ContextCycler.HostFor(s, [H(1, "Claude", @"D:\W\A"), H(2, "Claude", @"D:\W\A")], out var two), Is.Null);
        Assert.That(two, Does.Contain("more than one"), "two tabs in one folder: don't guess");
    }

    [Test]
    public void It_reads_what_the_statusline_and_the_context_gate_save()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ctx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "abc.json"), """{"session_id":"abc","cwd":"D:\\W\\A","used_percentage":83,"at":"2026-10-15T01:59:00Z"}""");
            File.WriteAllText(Path.Combine(dir, "abc.state.json"), """{"state":"idle","at":"2026-10-15T01:59:30Z","warned":true}""");
            File.WriteAllText(Path.Combine(dir, "new.json"), """{"session_id":"new","cwd":"D:\\W\\B","used_percentage":5}""");

            var cycler = new ContextCycler(dir);
            var all = cycler.Load().OrderBy(s => s.SessionId).ToList();

            Assert.That(all, Has.Count.EqualTo(2), "state files aren't sessions of their own");
            Assert.That(all[0], Is.EqualTo(new SessionContext("abc", @"D:\W\A", 83, true, new DateTime(2026, 10, 15, 1, 59, 30, DateTimeKind.Utc))));
            Assert.That((all[1].Idle, all[1].Percent), Is.EqualTo((false, 5.0)), "no turn recorded yet: never idle");

            var said = cycler.Tick([], new OrchestratorSettings(), Now);
            Assert.That(said, Has.Count.EqualTo(1).And.Some.Contains("yourself"), "no tab to type into: you're told, once");
            Assert.That(cycler.Tick([], new OrchestratorSettings(), Now), Is.Empty);
            Assert.That(new ContextCycler(dir).Tick([], new OrchestratorSettings { ContextCycle = false }, Now), Is.Empty);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public void A_session_in_a_plain_terminal_is_typed_into_through_its_console_and_a_desktop_one_is_reported()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ctx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            foreach (var (id, pid) in new[] { ("cli", 111), ("app", 222) })
            {
                File.WriteAllText(Path.Combine(dir, id + ".json"), $$"""{"session_id":"{{id}}","cwd":"D:\\W\\{{id}}","used_percentage":90}""");
                File.WriteAllText(Path.Combine(dir, id + ".state.json"), $$"""{"state":"idle","at":"2026-10-15T01:59:00Z","pid":{{pid}}}""");
            }
            var typed = new List<(int, string)>();
            var cycler = new ContextCycler(dir)
            {
                // 111 is a CLI with a console; 222 is a desktop-app session with none. The fake stops the cycle at its first line.
                Typer = (pid, text) => { if (text.Length > 0) typed.Add((pid, text)); return pid == 111 && text.Length == 0; },
            };

            var said = cycler.Tick([], new OrchestratorSettings(), Now);

            Assert.That(said, Has.Some.Contains(@"D:\W\cli").And.Some.Contains("in its own terminal"));
            Assert.That(said, Has.Some.Contains("desktop-app session").And.Some.Contains("yourself"));
            SpinWait.SpinUntil(() => typed.Count > 0, 5000);
            Assert.That(typed, Is.EqualTo(new[] { (111, "/quicksave") }));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public void The_context_gate_is_hooked_on_prompt_tool_and_stop_with_the_cycle_point()
    {
        var dir = Path.Combine(Path.GetTempPath(), "claude-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Setup.AgentGate.Ensure(dir, contextPercent: 75);
            var hooks = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "settings.json")))!["hooks"]!;
            foreach (var hookEvent in new[] { "PostToolUse", "Stop", "UserPromptSubmit" })
                Assert.That(hooks[hookEvent]!.ToJsonString(), Does.Contain("context-gate.py"), hookEvent);
            Assert.That(File.ReadAllText(Path.Combine(dir, "context-gate.py")), Does.Contain("PERCENT = 75").And.Contains("/unthrottle"));
            Assert.That(File.ReadAllText(Path.Combine(dir, "pace-gate.py")), Does.Contain("def cli_pid").And.Contains("unthrottled-"),
                "the pace gate lets an /unthrottle'd CLI through");
            Assert.That(File.Exists(Path.Combine(dir, "commands", "unthrottle.md")) && File.Exists(Path.Combine(dir, "commands", "throttle.md")),
                "both are real user-level slash commands");

            Assert.That(Setup.AgentGate.Ensure(dir, contextPercent: 75), Is.Null, "unchanged: nothing rewritten");
            Assert.That(Setup.AgentGate.Ensure(dir, contextPercent: 85), Is.EqualTo("wrote context-gate.py"), "a new cycle point rewrites only the script");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public void A_turn_ending_on_a_go_ahead_question_while_its_repo_has_open_tickets_is_told_to_keep_going()
    {
        // Found live: a CLI finished a story and asked "shall I start the next one?" with four tickets still open.
        var dir = Path.Combine(Path.GetTempPath(), "claude-" + Guid.NewGuid().ToString("N"));
        var ws = Path.Combine(dir, "ws");
        var repo = Path.Combine(ws, "A");
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        Directory.CreateDirectory(Path.Combine(ws, "B", ".git"));
        var data = Path.Combine(ws, "MindAttic.Workflow", "data");
        Directory.CreateDirectory(data);
        try
        {
            Python($"""
                import sqlite3
                d = sqlite3.connect(r"{Path.Combine(data, "workflow.db")}")
                d.execute("CREATE TABLE tickets (id TEXT PRIMARY KEY, repo TEXT, status TEXT, ordinal INTEGER)")
                d.executemany("INSERT INTO tickets VALUES (?, ?, ?, ?)", [("A-T1", "A", "closed", 1), ("A-T2", "A", "open", 2), ("A-T3", "A", "open", 3)])
                d.commit()
                """);
            Setup.AgentGate.Ensure(dir);
            string Gate(object input, string provider = "")
            {
                var psi = new System.Diagnostics.ProcessStartInfo("python", $"-I \"{Path.Combine(dir, "context-gate.py")}\" {provider}".TrimEnd())
                    { RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false };
                psi.Environment["USERPROFILE"] = dir;
                psi.Environment["MINDATTIC_WORKSPACE"] = ws;
                using var p = System.Diagnostics.Process.Start(psi)!;
                p.StandardInput.Write(System.Text.Json.JsonSerializer.Serialize(input));
                p.StandardInput.Close();
                var output = p.StandardOutput.ReadToEnd();
                Assert.That(p.WaitForExit(20_000), Is.True);
                return output;
            }
            object Stop(string said, string? cwd = null) => new { session_id = "s", hook_event_name = "Stop", cwd = cwd ?? repo, last_assistant_message = said };
            const string ask = "MAO-T91 is done and pushed. Next is MAO-T93. Shall I start it?";

            var told = Gate(Stop(ask));
            Assert.That(told, Does.Contain("\"decision\": \"block\"").And.Contain("A still has 2 open tickets (A-T2, A-T3)"));
            Assert.That(Gate(Stop(ask)), Is.Empty, "it stopped again without doing anything: something really needs the user, so it's let go");

            Gate(Stop(ask));
            Gate(new { session_id = "s", hook_event_name = "PostToolUse", cwd = repo });
            Assert.That(Gate(Stop(ask)), Does.Contain("\"block\""), "it carried on (a tool ran) and asks again at the next story: told again");

            Gate(new { session_id = "s", hook_event_name = "UserPromptSubmit", cwd = repo, prompt = "x" });
            Assert.That(Gate(Stop("Done: the tests pass and it's pushed.")), Is.Empty, "a plain report isn't asking");

            // Closing a ticket and stopping on a plain report, with more open, is caught too (MAO-T103).
            var ctx = Path.Combine(dir, ".claude", "session-context");
            var cycler = new ContextCycler(ctx);
            Assert.That(cycler.KeepGoingSince(), Is.Empty, "the first look only finds where the log ends");
            Gate(new { session_id = "s", hook_event_name = "UserPromptSubmit", cwd = repo, prompt = "do A-T1" });
            Gate(new { session_id = "s", hook_event_name = "PostToolUse", cwd = repo, tool_input = new { command = "& 'workflow' ticket close A-T1 --ref t" } });
            Assert.That(Gate(Stop("A-T1 is closed and pushed.")), Does.Contain("\"block\"").And.Contain("A still has 2 open tickets"));
            Assert.That(Gate(Stop("A-T1 is closed and pushed.")), Is.Empty, "stopped again straight away: let go");
            Assert.That(cycler.KeepGoingSince(), Is.EqualTo(new[] { "keep-going: told the CLI in A to carry on (closed A-T1, 2 tickets still open)" }),
                "the stop after it had nothing new to go on (the close was already acted on), so nothing more is logged");
            Gate(new { session_id = "s", hook_event_name = "PostToolUse", cwd = repo, tool_input = new { command = "x" } });
            Gate(Stop(ask));
            Assert.That(Gate(Stop(ask)), Is.Empty);
            Assert.That(cycler.KeepGoingSince(), Is.EqualTo(new[]
            {
                "keep-going: told the CLI in A to carry on (asked to go on, 2 tickets still open)",
                "keep-going: let the CLI in A stop (asked to go on, 2 open); it stopped again without doing anything, so it likely needs you",
            }));
            Gate(new { session_id = "s", hook_event_name = "UserPromptSubmit", cwd = repo, prompt = "x" });
            Assert.That(Gate(Stop(ask, Path.Combine(ws, "B"))), Is.Empty, "B has no open tickets");
            Assert.That(Gate(Stop(ask, ws)), Is.Empty, "the workspace root isn't a repo");
            var worktree = Directory.CreateDirectory(Path.Combine(dir, "ws.worktrees", "A", "A-T2")).FullName;
            Assert.That(Gate(Stop(ask, worktree)), Is.Empty, "an autopilot agent in its worktree works its one ticket");
            File.WriteAllText(Path.Combine(dir, ".claude", "session-context", "s.json"), """{"used_percentage": 90}""");
            Assert.That(Gate(Stop(ask)), Is.Empty, "past the cycle point it stops, so the context can be cycled");

            // Gemini's AfterAgent and Codex's Stop get the same carry-on in their own shape (MAO-T104); their own
            // stop_hook_active lets the second stop through, and nothing of theirs is recorded as a Claude session.
            Assert.That(Gate(new { session_id = "g1", hook_event_name = "AfterAgent", cwd = repo, prompt_response = ask }, "gemini"),
                Does.Contain("\"decision\": \"deny\"").And.Contain("A still has 2 open tickets"));
            Assert.That(Gate(new { session_id = "g1", hook_event_name = "AfterAgent", cwd = repo, prompt_response = ask, stop_hook_active = true }, "gemini"), Is.Empty);
            Assert.That(Gate(new { session_id = "c1", hook_event_name = "Stop", cwd = repo, last_assistant_message = ask }, "codex"), Does.Contain("\"decision\": \"block\""));
            Assert.That(Gate(new { session_id = "c1", hook_event_name = "Stop", cwd = repo, last_assistant_message = "Done." }, "codex"), Is.Empty);
            Assert.That(File.Exists(Path.Combine(ctx, "g1.state.json")) || File.Exists(Path.Combine(ctx, "c1.state.json")), Is.False);
            Assert.That(cycler.KeepGoingSince(), Has.Some.EqualTo("keep-going: told the Gemini CLI in A to carry on (asked to go on, 2 tickets still open)")
                .And.Some.EqualTo("keep-going: let the Gemini CLI in A stop (asked to go on, 2 open); it stopped again without doing anything, so it likely needs you")
                .And.Some.Contains("told the Codex CLI in A"));

            Setup.AgentGate.Ensure(dir, keepGoing: false);
            File.Delete(Path.Combine(dir, ".claude", "session-context", "s.json"));
            Assert.That(Gate(Stop(ask)), Is.Empty, "switched off (settings --keep-going false)");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public void A_session_running_a_goal_isnt_cycled_or_told_to_wrap_up_until_the_goal_is_cleared()
    {
        // /clear would silently end the /goal (MAO-T92's finding), so its compaction carries it on instead.
        var dir = Path.Combine(Path.GetTempPath(), "claude-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Setup.AgentGate.Ensure(dir, contextPercent: 80);
            string Gate(object input)
            {
                var psi = new System.Diagnostics.ProcessStartInfo("python", $"-I \"{Path.Combine(dir, "context-gate.py")}\"")
                    { RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false };
                psi.Environment["USERPROFILE"] = dir;
                using var p = System.Diagnostics.Process.Start(psi)!;
                p.StandardInput.Write(System.Text.Json.JsonSerializer.Serialize(input));
                p.StandardInput.Close();
                var output = p.StandardOutput.ReadToEnd();
                Assert.That(p.WaitForExit(20_000), Is.True);
                return output;
            }
            var ctx = Path.Combine(dir, ".claude", "session-context");
            var transcript = Path.Combine(dir, "g.jsonl");
            File.WriteAllText(transcript, """{"type":"assistant","message":{"content":[{"type":"text","text":"Working on it."}]}}""" + "\n");
            Gate(new { session_id = "g", hook_event_name = "UserPromptSubmit", cwd = @"D:\W\A", prompt = "/goal all the tests pass", transcript_path = transcript });
            File.WriteAllText(Path.Combine(ctx, "g.json"), """{"cwd":"D:\\W\\A","used_percentage":90}""");
            Assert.That(Gate(new { session_id = "g", hook_event_name = "PostToolUse" }), Is.Empty, "no wrap-up: ending the turn won't get it cycled");
            Gate(new { session_id = "g", hook_event_name = "Stop", cwd = @"D:\W\A" });

            var cycler = new ContextCycler(ctx) { Typer = (_, _) => throw new AssertionException("nothing is typed into a /goal session") };
            Assert.That(cycler.Load().Single().Goal, Is.EqualTo("all the tests pass"));
            var said = cycler.Tick([], new OrchestratorSettings(), DateTime.UtcNow.AddMinutes(1));
            Assert.That(said, Has.One.Contains("running /goal \"all the tests pass\", so it isn't cycled"));
            Assert.That(cycler.Tick([], new OrchestratorSettings(), DateTime.UtcNow.AddMinutes(2)), Is.Empty, "said once");

            // The goal met: Claude Code's stop summary naming it is the last entry (shape trimmed from a real run, MAO-T109).
            const string summary = """{"type":"system","subtype":"stop_hook_summary","hookCount":2,"hookInfos":[{"command":"python context-gate.py","durationMs":106},{"command":"all the tests pass","promptText":"all the tests pass","durationMs":1637}],"preventedContinuation":false}""";
            File.AppendAllText(transcript, summary + "\n" + """{"type":"last-prompt"}""" + "\n" + """{"type":"cost-state"}""" + "\n");
            Assert.That(ContextCycler.GoalFinished(transcript, "all the tests pass"), Is.True, "metadata after the summary doesn't count");
            Assert.That(ContextCycler.GoalFinished(transcript, "another goal"), Is.False);
            Assert.That(new ContextCycler(ctx).Tick([], new OrchestratorSettings(), DateTime.UtcNow.AddMinutes(3)), Has.One.Contains("/quicksave, /clear and /quickload it yourself"),
                "finished on its own: cycled as usual again (here, no console to type into)");
            File.AppendAllText(transcript, """{"type":"assistant","message":{"content":[{"type":"text","text":"Not yet: one test fails."}]}}""" + "\n");
            Assert.That(ContextCycler.GoalFinished(transcript, "all the tests pass"), Is.False, "the evaluator sent it back to work: still running");
            File.AppendAllText(transcript, summary + "\n");
            Gate(new { session_id = "g", hook_event_name = "UserPromptSubmit", cwd = @"D:\W\A", prompt = "thanks", transcript_path = transcript });
            Assert.That(new ContextCycler(ctx).Load().Single().Goal, Is.Null, "the gate unmarks a finished goal at the next prompt");

            Gate(new { session_id = "g", hook_event_name = "UserPromptSubmit", cwd = @"D:\W\A", prompt = "/goal all the tests pass", transcript_path = transcript });
            Gate(new { session_id = "g", hook_event_name = "UserPromptSubmit", cwd = @"D:\W\A", prompt = "/goal clear" });
            Assert.That(new ContextCycler(ctx).Load().Single().Goal, Is.Null, "cleared: cycled as usual again");
            Assert.That(Gate(new { session_id = "g", hook_event_name = "PostToolUse" }), Does.Contain("% full"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public void Hooks_firing_together_neither_fail_nor_lose_the_sessions_state()
    {
        var dir = Path.Combine(Path.GetTempPath(), "claude-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Setup.AgentGate.Ensure(dir);
            System.Diagnostics.Process Start(object input)
            {
                var psi = new System.Diagnostics.ProcessStartInfo("python", $"-I \"{Path.Combine(dir, "context-gate.py")}\"")
                    { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
                psi.Environment["USERPROFILE"] = dir;
                var p = System.Diagnostics.Process.Start(psi)!;
                p.StandardInput.Write(System.Text.Json.JsonSerializer.Serialize(input));
                p.StandardInput.Close();
                return p;
            }
            using (var first = Start(new { session_id = "c", hook_event_name = "UserPromptSubmit", cwd = @"D:\W\A", prompt = "/goal ship it" })) first.WaitForExit(20_000);

            // Prompts and stops (both write the state) overlapping, as when a turn ends while the next is typed.
            var all = Enumerable.Range(0, 32).Select(i => Start(new { session_id = "c", hook_event_name = i % 2 == 0 ? "UserPromptSubmit" : "Stop", cwd = @"D:\W\A", prompt = "go on" })).ToList();
            foreach (var p in all)
            {
                Assert.That(p.WaitForExit(30_000), Is.True);
                var err = p.StandardError.ReadToEnd();
                Assert.That((p.ExitCode, err), Is.EqualTo((0, "")), "a hook failing shows up as an error in the CLI");
                p.Dispose();
            }

            var ctx = Path.Combine(dir, ".claude", "session-context");
            Assert.That(new ContextCycler(ctx).Load().Single().Goal, Is.EqualTo("ship it"), "no write read the state as empty and wiped it");
            Assert.That(Directory.EnumerateFiles(ctx, "*.tmp"), Is.Empty, "no temp file left behind");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public void Autopilot_this_turns_it_on_for_just_the_cli_that_ran_it()
    {
        var dir = Path.Combine(Path.GetTempPath(), "claude-" + Guid.NewGuid().ToString("N"));
        var ws = Path.Combine(dir, "ws");
        var repo = Path.Combine(ws, "A");
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        Directory.CreateDirectory(Path.Combine(ws, "B", ".git"));
        try
        {
            Setup.AgentGate.Ensure(dir);
            string Gate(object input)
            {
                var psi = new System.Diagnostics.ProcessStartInfo("python", $"-I \"{Path.Combine(dir, "context-gate.py")}\"")
                    { RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false };
                psi.Environment["USERPROFILE"] = dir;
                psi.Environment["MINDATTIC_WORKSPACE"] = ws;
                using var p = System.Diagnostics.Process.Start(psi)!;
                p.StandardInput.Write(System.Text.Json.JsonSerializer.Serialize(input));
                p.StandardInput.Close();
                var output = p.StandardOutput.ReadToEnd();
                Assert.That(p.WaitForExit(20_000), Is.True);
                return output;
            }
            var order = Path.Combine(dir, ".claude", "session-context", "autopilot.order");

            Gate(new { session_id = "s", hook_event_name = "UserPromptSubmit", cwd = repo, prompt = "/autopilot this" });
            Assert.That(File.ReadAllText(order), Is.EqualTo("A"), "only this CLI's own repo, not every open one");

            Gate(new { session_id = "s", hook_event_name = "UserPromptSubmit", cwd = ws, prompt = "/autopilot this" });
            Assert.That(File.ReadAllText(order), Is.Empty, "the workspace root isn't a repo: no repo to scope it to");

            Gate(new { session_id = "s", hook_event_name = "UserPromptSubmit", cwd = repo, prompt = "/autopilot off" });
            Assert.That(File.Exists(order), Is.False);

            Gate(new { session_id = "s", hook_event_name = "UserPromptSubmit", cwd = repo, prompt = "/autopilot" });
            Assert.That(File.ReadAllText(order), Is.Empty, "plain /autopilot still means every open repo");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    private static void Python(string script)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("python", "-I -") { RedirectStandardInput = true, RedirectStandardError = true, UseShellExecute = false };
        using var p = System.Diagnostics.Process.Start(psi)!;
        p.StandardInput.Write(script);
        p.StandardInput.Close();
        var err = p.StandardError.ReadToEnd();
        p.WaitForExit();
        Assert.That(p.ExitCode, Is.Zero, err);
    }

    [Test]
    public void Alive_subagent_ids_are_the_marks_whose_age_hasnt_gone_stale()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ctx-" + Guid.NewGuid().ToString("N"));
        var sub = Path.Combine(dir, "subagents");
        Directory.CreateDirectory(sub);
        try
        {
            File.WriteAllText(Path.Combine(sub, "agent-fresh"), "");
            var stale = Path.Combine(sub, "agent-stale");
            File.WriteAllText(stale, "");
            File.SetLastWriteTimeUtc(stale, Now - ContextCycler.SubagentMarkLife - TimeSpan.FromMinutes(1));

            var ids = new ContextCycler(dir).AliveSubagentIds(Now);

            Assert.That(ids, Is.EqualTo(new[] { "agent-fresh" }), "a mark whose stop never came times out eventually");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
