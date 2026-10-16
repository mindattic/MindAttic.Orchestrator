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
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public void Nothing_ever_types_quicksave_clear_or_quickload_into_a_session()
    {
        // Found live: at 80% context the daemon typed /quicksave and /clear into a chat the user was working in.
        var src = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "src"));
        var typing = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            // The dashboard's reply box sends only what you type yourself; nothing may send these on its own.
            .Where(f => File.ReadAllText(f) is var t && (t.Contains("\"/quicksave\"") || t.Contains("\"/clear\"") || t.Contains("\"/quickload\"")))
            .ToList();
        Assert.That(typing, Is.Empty);
        Assert.That(new OrchestratorSettings().ContextCycle, Is.False);

        var dir = Path.Combine(Path.GetTempPath(), "claude-" + Guid.NewGuid().ToString("N"));
        try
        {
            Setup.AgentGate.Ensure(dir);
            var gate = File.ReadAllText(Path.Combine(dir, "context-gate.py"));
            Assert.That(gate, Does.Not.Contain("will /quicksave").And.Not.Contain("% full. Finish the step"), "no turn is told to stop for its context");
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
    public void Only_the_cli_that_typed_autopilot_is_kept_going_on_its_own_repos_workable_tickets()
    {
        // Found live: a CLI the user was working in was told to start on its repo's 22 tickets because autopilot was
        // on for that repo elsewhere. Now /autopilot belongs to the CLI it's typed into, which works the tickets itself.
        var dir = Path.Combine(Path.GetTempPath(), "claude-" + Guid.NewGuid().ToString("N"));
        var ws = Path.Combine(dir, "ws");
        var repo = Path.Combine(ws, "A");
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        var data = Path.Combine(ws, "MindAttic.Workflow", "data");
        Directory.CreateDirectory(data);
        try
        {
            Python($"""
                import sqlite3
                d = sqlite3.connect(r"{Path.Combine(data, "workflow.db")}")
                d.execute("CREATE TABLE tickets (id TEXT PRIMARY KEY, repo TEXT, status TEXT, ordinal INTEGER, type TEXT)")
                d.executemany("INSERT INTO tickets VALUES (?, ?, ?, ?, ?)", [("A-T1", "A", "closed", 1, "fix"), ("A-T2", "A", "open", 2, "fix"),
                    ("A-T3", "A", "open", 3, "fix"), ("A-T4", "A", "open", 4, "feature"), ("B-T1", "B", "open", 5, "fix")])
                d.commit()
                """);
            Setup.AgentGate.Ensure(dir);
            string Gate(int cli, object input, string provider = "")
            {
                var psi = new System.Diagnostics.ProcessStartInfo("python", $"-I \"{Path.Combine(dir, "context-gate.py")}\" {provider}".TrimEnd())
                    { RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false };
                psi.Environment["USERPROFILE"] = dir;
                psi.Environment["MINDATTIC_WORKSPACE"] = ws;
                psi.Environment["APPDATA"] = dir; // approvals come from here, never this machine's settings
                psi.Environment["MINDATTIC_CLI_PID"] = cli.ToString();
                using var p = System.Diagnostics.Process.Start(psi)!;
                p.StandardInput.Write(System.Text.Json.JsonSerializer.Serialize(input));
                p.StandardInput.Close();
                var output = p.StandardOutput.ReadToEnd();
                Assert.That(p.WaitForExit(20_000), Is.True);
                return output;
            }
            object Stop(string sid, string said) => new { session_id = sid, hook_event_name = "Stop", cwd = repo, last_assistant_message = said };
            object Typed(string sid, string prompt) => new { session_id = sid, hook_event_name = "UserPromptSubmit", cwd = ws, prompt };
            const string ask = "That's done. Shall I start the next one?";

            Assert.That(Gate(100, Stop("mine", ask)), Is.Empty, "no autopilot: a CLI you're using is never pushed on");
            Gate(200, Typed("pilot", "/autopilot -repos=A"));
            Assert.That(Gate(100, Stop("mine", ask)), Is.Empty, "autopilot in another CLI marks nothing here, though it's in A");

            var told = Gate(200, Stop("pilot", "PRS-T16 landed."));
            Assert.That(told, Does.Contain("\"block\"").And.Contain("2 workable tickets remain (A-T2, A-T3)").And.Contain("orchestrator next --repos A --claim"),
                "the autopilot CLI goes on to its next tickets (fixes only: A-T4 awaits approval; B isn't its repo)");
            Assert.That(Gate(200, Stop("pilot", "Blocked on a decision.")), Is.Empty, "stopped again without doing anything: let go");

            Gate(200, Typed("pilot", "/autopilot off"));
            Gate(200, Typed("pilot", "x"));
            Assert.That(Gate(200, Stop("pilot", ask)), Is.Empty, "off in this CLI");
            Assert.That(Gate(300, new { session_id = "c1", hook_event_name = "Stop", cwd = repo, last_assistant_message = ask }, "codex"), Is.Empty,
                "a Codex CLI is never pushed onto tickets");
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

            var cycler = new ContextCycler(ctx);
            Assert.That(cycler.Load().Single().Goal, Is.EqualTo("all the tests pass"));

            // The goal met: Claude Code's stop summary naming it is the last entry (shape trimmed from a real run, MAO-T109).
            const string summary = """{"type":"system","subtype":"stop_hook_summary","hookCount":2,"hookInfos":[{"command":"python context-gate.py","durationMs":106},{"command":"all the tests pass","promptText":"all the tests pass","durationMs":1637}],"preventedContinuation":false}""";
            File.AppendAllText(transcript, summary + "\n" + """{"type":"last-prompt"}""" + "\n" + """{"type":"cost-state"}""" + "\n");
            Assert.That(ContextCycler.GoalFinished(transcript, "all the tests pass"), Is.True, "metadata after the summary doesn't count");
            Assert.That(ContextCycler.GoalFinished(transcript, "another goal"), Is.False);
            File.AppendAllText(transcript, """{"type":"assistant","message":{"content":[{"type":"text","text":"Not yet: one test fails."}]}}""" + "\n");
            Assert.That(ContextCycler.GoalFinished(transcript, "all the tests pass"), Is.False, "the evaluator sent it back to work: still running");
            File.AppendAllText(transcript, summary + "\n");
            Gate(new { session_id = "g", hook_event_name = "UserPromptSubmit", cwd = @"D:\W\A", prompt = "thanks", transcript_path = transcript });
            Assert.That(new ContextCycler(ctx).Load().Single().Goal, Is.Null, "the gate unmarks a finished goal at the next prompt");

            Gate(new { session_id = "g", hook_event_name = "UserPromptSubmit", cwd = @"D:\W\A", prompt = "/goal all the tests pass", transcript_path = transcript });
            Gate(new { session_id = "g", hook_event_name = "UserPromptSubmit", cwd = @"D:\W\A", prompt = "/goal clear" });
            Assert.That(new ContextCycler(ctx).Load().Single().Goal, Is.Null, "cleared");
            Assert.That(Gate(new { session_id = "g", hook_event_name = "PostToolUse" }), Is.Empty, "no turn is ever told to stop for its context");
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
    public void Autopilot_turns_on_for_just_the_cli_it_is_typed_into()
    {
        var dir = Path.Combine(Path.GetTempPath(), "claude-" + Guid.NewGuid().ToString("N"));
        var ws = Path.Combine(dir, "ws");
        var repo = Path.Combine(ws, "A");
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        Directory.CreateDirectory(Path.Combine(ws, "B", ".git"));
        try
        {
            Setup.AgentGate.Ensure(dir);
            void Typed(int cli, string cwd, string prompt)
            {
                var psi = new System.Diagnostics.ProcessStartInfo("python", $"-I \"{Path.Combine(dir, "context-gate.py")}\"")
                    { RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false };
                psi.Environment["USERPROFILE"] = dir;
                psi.Environment["MINDATTIC_WORKSPACE"] = ws;
                psi.Environment["MINDATTIC_CLI_PID"] = cli.ToString();
                using var p = System.Diagnostics.Process.Start(psi)!;
                p.StandardInput.Write(System.Text.Json.JsonSerializer.Serialize(new { session_id = "s" + cli, hook_event_name = "UserPromptSubmit", cwd, prompt }));
                p.StandardInput.Close();
                p.StandardOutput.ReadToEnd();
                Assert.That(p.WaitForExit(20_000), Is.True);
            }
            var ctx = Path.Combine(dir, ".claude", "session-context");
            string? Mark(int cli) => File.Exists(Path.Combine(ctx, $"autopilot-{cli}")) ? File.ReadAllText(Path.Combine(ctx, $"autopilot-{cli}")) : null;

            Typed(1, repo, "/autopilot");
            Assert.That(Mark(1), Is.EqualTo("A"), "this CLI's own repo, for this CLI only");
            Typed(2, ws, "/autopilot -repo=B");
            Typed(2, ws, "/autopilot -repos=C,D on");
            Assert.That((Mark(1), Mark(2)), Is.EqualTo(("A", "B,C,D")), "-repo and -repos both name repos; each CLI has its own list");
            Typed(2, ws, "/autopilot off -repos=C");
            Assert.That(Mark(2), Is.EqualTo("B,D"));
            Typed(1, repo, "/autopilot off");
            Assert.That((Mark(1), Mark(2)), Is.EqualTo(((string?)null, "B,D")), "off is for this CLI only");
            Typed(1, repo, "/autopilot all");
            Assert.That(Mark(1), Is.EqualTo("*"));
            Typed(3, ws, "/autopilot off all");
            Assert.That((Mark(1), Mark(2)), Is.EqualTo(((string?)null, (string?)null)), "off all: in every CLI");
            Assert.That(File.Exists(Path.Combine(ctx, "autopilot.order")), Is.False, "nothing workspace-wide any more");
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
        Assert.That(p.WaitForExit(20_000) && p.ExitCode == 0, Is.True, err);
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

    [Test]
    public void A_subagent_whose_transcript_has_gone_quiet_stops_counting_without_waiting_for_its_mark_to_age_out()
    {
        // A SubagentStop that never came kept a mark alive for up to 6 hours, and its transcript re-read every refresh (MAO-T114).
        var dir = Path.Combine(Path.GetTempPath(), "ctx-" + Guid.NewGuid().ToString("N"));
        var projects = Path.Combine(dir, "projects");
        var sub = Path.Combine(dir, "subagents");
        Directory.CreateDirectory(sub);
        try
        {
            var now = DateTime.UtcNow;
            foreach (var (id, quiet) in new[] { ("busy", TimeSpan.FromMinutes(1)), ("quiet", ContextCycler.SubagentIdleLife + TimeSpan.FromMinutes(1)), ("unwritten", TimeSpan.Zero) })
            {
                File.WriteAllText(Path.Combine(sub, id), ""); // no pid: trusted while young
                if (id == "unwritten") continue; // its transcript isn't there yet: the mark alone decides
                var folder = Path.Combine(projects, "proj", "parent-session", "subagents");
                Directory.CreateDirectory(folder);
                var transcript = Path.Combine(folder, $"agent-{id}.jsonl");
                File.WriteAllText(transcript, "{}\n");
                File.SetLastWriteTimeUtc(transcript, now - quiet);
            }
            var cycler = new ContextCycler(dir) { ProjectsDir = projects };

            Assert.That(cycler.AliveSubagentIds(now).Order(), Is.EqualTo(new[] { "busy", "unwritten" }));
            Assert.That(cycler.Subagents(), Is.EqualTo(2));
            cycler.PruneMarks();
            Assert.That(Directory.GetFiles(sub).Select(Path.GetFileName).Order(), Is.EqualTo(new[] { "busy", "quiet", "unwritten" }),
                "a quiet one's mark stays, so it counts again the moment it writes");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
