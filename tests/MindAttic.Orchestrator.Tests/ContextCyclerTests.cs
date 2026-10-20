using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class ContextCyclerTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 2, 0, 0, DateTimeKind.Utc);

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
    public void A_state_file_another_process_holds_keeps_its_session_so_the_cli_reads_as_busy()
    {
        // M.ORCH-T539: a non-shared read of a state file a hook had open threw and dropped the session, so the CLI's older,
        // idle session stood in for it and the wakers could type into the busy one.
        var dir = Path.Combine(Path.GetTempPath(), "ctx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "old.state.json"), """{"state":"idle","at":"2026-10-15T01:00:00Z","pid":42}""");
            var current = Path.Combine(dir, "cur.state.json");
            File.WriteAllText(current, """{"state":"busy","at":"2026-10-15T01:59:00Z","pid":42}""");
            var cycler = new ContextCycler(dir);

            using (new FileStream(current, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                Assert.That(cycler.LatestSessionOf(42), Is.EqualTo("cur"), "a file being written is read shared");

            // Held outright: its session_states row stands in.
            new OrchestratorDb(OrchestratorDb.StatePath(dir)).Write((connection, tx) =>
            {
                using var command = connection.CreateCommand();
                command.Transaction = tx;
                command.CommandText = "INSERT INTO session_states (session_id, value, written_at) VALUES ('cur', $v, $at);";
                command.Parameters.AddWithValue("$v", """{"state":"busy","at":"2026-10-15T01:59:00Z","pid":42}""");
                command.Parameters.AddWithValue("$at", (File.GetLastWriteTimeUtc(current) - DateTime.UnixEpoch).TotalSeconds - 1);
                command.ExecuteNonQuery();
            }, dump: false);
            using (new FileStream(current, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var cur = cycler.Load().Single(s => s.SessionId == "cur");
                Assert.That((cur.Pid, cur.Idle), Is.EqualTo(((int?)42, false)));
                Assert.That(cycler.LatestSessionOf(42), Is.EqualTo("cur"));
            }
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
            Assert.That(File.ReadAllText(Path.Combine(dir, "context-gate.py")), Does.Contain("PERCENT = 75").And.Contains("/pacing"));
            Assert.That(File.ReadAllText(Path.Combine(dir, "pace-gate.py")), Does.Contain("def cli_pid").And.Contains("unthrottled-"),
                "the pace gate lets a CLI with /pacing off through");
            Assert.That(File.Exists(Path.Combine(dir, "commands", "pacing.md")), "a real user-level slash command");
            Assert.That(File.Exists(Path.Combine(dir, "commands", "unthrottle.md")) || File.Exists(Path.Combine(dir, "commands", "throttle.md")), Is.False);

            Assert.That(Setup.AgentGate.Ensure(dir, contextPercent: 75), Is.Null, "unchanged: nothing rewritten");
            Assert.That(Setup.AgentGate.Ensure(dir, contextPercent: 85), Is.EqualTo("wrote context-gate.py"), "a new cycle point rewrites only the script");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public void Only_the_cli_that_typed_autopilot_is_kept_going_on_its_own_repos_workable_tickets()
    {
        // Found live: a CLI the user was working in was told to start on its repo's 22 tickets because autopilot was
        // on for that repo elsewhere. Now /allocate belongs to the CLI it's typed into, which works the tickets itself.
        var dir = Path.Combine(Path.GetTempPath(), "claude-" + Guid.NewGuid().ToString("N"));
        var ws = Path.Combine(dir, "ws");
        var repo = Path.Combine(ws, "A");
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        var data = Path.Combine(ws, "MindAttic.Tickets", "data");
        Directory.CreateDirectory(data);
        try
        {
            Python($"""
                import sqlite3
                d = sqlite3.connect(r"{Path.Combine(data, "Tickets.db")}")
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
            Gate(200, Typed("pilot", "/allocate -repo=a*"));
            Assert.That(Gate(100, Stop("mine", ask)), Is.Empty, "autopilot in another CLI marks nothing here, though it's in A");

            var told = Gate(200, Stop("pilot", "PRS-T16 landed."));
            Assert.That(told, Does.Contain("\"block\"").And.Contain("2 workable tickets remain (A-T2, A-T3)").And.Contain("orchestrator next --repos \\\"a*\\\" --claim"),
                "the autopilot CLI goes on to its next tickets (the wildcard a* names A; fixes only: A-T4 awaits approval; B isn't its repo)");
            Assert.That(Gate(200, Stop("pilot", "Blocked on a decision.")), Is.Empty, "stopped again without doing anything: let go");

            Gate(200, Typed("pilot", "/allocate off"));
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
        // /clear would silently end the /goal (M.ORCH-T92's finding), so its compaction carries it on instead.
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

            // The goal met: Claude Code's stop summary naming it is the last entry (shape trimmed from a real run, M.ORCH-T109).
            const string summary = """{"type":"system","subtype":"stop_hook_summary","hookCount":2,"hookInfos":[{"command":"python context-gate.py","durationMs":106},{"command":"all the tests pass","promptText":"all the tests pass","durationMs":1637}],"preventedContinuation":false}""";
            File.AppendAllText(transcript, summary + "\n" + """{"type":"last-prompt"}""" + "\n" + """{"type":"cost-state"}""" + "\n");
            Gate(new { session_id = "g", hook_event_name = "UserPromptSubmit", cwd = @"D:\W\A", prompt = "thanks", transcript_path = transcript });
            Assert.That(new ContextCycler(ctx).Load().Single().Goal, Is.Null, "the gate unmarks a finished goal at the next prompt");

            Gate(new { session_id = "g", hook_event_name = "UserPromptSubmit", cwd = @"D:\W\A", prompt = "/goal all the tests pass", transcript_path = transcript });
            File.AppendAllText(transcript, """{"type":"assistant","message":{"content":[{"type":"text","text":"Working on it."}]}}""" + "\n"); // the new goal runs
            Gate(new { session_id = "g", hook_event_name = "UserPromptSubmit", cwd = @"D:\W\A", prompt = "/goal" });
            Assert.That(new ContextCycler(ctx).Load().Single().Goal, Is.EqualTo("all the tests pass"), "a bare /goal only shows its status");
            Gate(new { session_id = "g", hook_event_name = "UserPromptSubmit", cwd = @"D:\W\A", prompt = "/goal cancel" });
            Assert.That(new ContextCycler(ctx).Load().Single().Goal, Is.Null, "cancel clears it, as Claude Code does (not a goal named 'cancel')");
            Gate(new { session_id = "g", hook_event_name = "UserPromptSubmit", cwd = @"D:\W\A", prompt = "/goal clear all lint warnings", transcript_path = transcript });
            Assert.That(new ContextCycler(ctx).Load().Single().Goal, Is.EqualTo("clear all lint warnings"), "only the word alone clears; with more it's a new goal, as in Claude Code");
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
            string Typed(int cli, string cwd, string prompt)
            {
                var psi = new System.Diagnostics.ProcessStartInfo("python", $"-I \"{Path.Combine(dir, "context-gate.py")}\"")
                    { RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false };
                psi.Environment["USERPROFILE"] = dir;
                psi.Environment["MINDATTIC_WORKSPACE"] = ws;
                psi.Environment["MINDATTIC_CLI_PID"] = cli.ToString();
                using var p = System.Diagnostics.Process.Start(psi)!;
                p.StandardInput.Write(System.Text.Json.JsonSerializer.Serialize(new { session_id = "s" + cli, hook_event_name = "UserPromptSubmit", cwd, prompt }));
                p.StandardInput.Close();
                var output = p.StandardOutput.ReadToEnd();
                Assert.That(p.WaitForExit(20_000), Is.True);
                return output;
            }
            var ctx = Path.Combine(dir, ".claude", "session-context");
            string? Mark(int cli) => File.Exists(Path.Combine(ctx, $"autopilot-{cli}")) ? File.ReadAllText(Path.Combine(ctx, $"autopilot-{cli}")) : null;

            Typed(1, repo, "/allocate");
            Assert.That(Mark(1), Is.EqualTo("A"), "this CLI's own repo, for this CLI only");
            Typed(2, ws, "/allocate -repo=B");
            Typed(2, ws, "/allocate -repo=C,GTFO.* on");
            Assert.That((Mark(1), Mark(2)), Is.EqualTo(("A", "B,C,GTFO.*")), "each CLI has its own list; a wildcard is kept as typed");
            Typed(2, ws, "/allocate off -repo=C");
            Assert.That(Mark(2), Is.EqualTo("B,GTFO.*"));
            Typed(2, ws, "/allocate off -repos=GTFO.*");
            Assert.That(Mark(2), Is.EqualTo("B,GTFO.*"), "-repos is gone: refused, nothing changed");
            Typed(2, ws, "/allocate off -repo=GTFO.*");
            Assert.That(Mark(2), Is.EqualTo("B"));
            Typed(2, ws, "/allocate -repo=D");
            Assert.That(Mark(2), Is.EqualTo("B,D"));
            Typed(1, repo, "/allocate off");
            Assert.That((Mark(1), Mark(2)), Is.EqualTo(((string?)null, "B,D")), "off is for this CLI only");
            Typed(1, repo, "/allocate all");
            Assert.That(Mark(1), Is.EqualTo("*"));
            Assert.That(Typed(1, repo, "/allocate off -repo=B"), Does.Contain("\"block\"").And.Contain("through /allocate all"),
                "B is worked through *: switching it off alone would change nothing, so it says so instead of seeming to");
            Assert.That(Mark(1), Is.EqualTo("*"));
            Typed(4, ws, "/allocate -repo=GTFO.*");
            Typed(4, ws, "/allocate -repo=GTFO.A");
            Assert.That(Typed(4, ws, "/allocate off -repo=GTFO.A"), Does.Contain("through /allocate -repo=GTFO.*"),
                "named and covered by a wildcard too: dropping the name alone would leave it worked");
            Assert.That(Mark(4), Is.EqualTo("GTFO.*,GTFO.A"));
            Typed(3, ws, "/allocate off all");
            Assert.That((Mark(1), Mark(2)), Is.EqualTo(((string?)null, (string?)null)), "off all: in every CLI");
            Assert.That(File.Exists(Path.Combine(ctx, "autopilot.order")), Is.False, "nothing workspace-wide any more");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public void A_freshly_opened_cli_starts_with_autopilot_off_until_it_is_typed_there()
    {
        // M.ORCH-T522 reverses M.ORCH-T476's on-by-default: no CLI works tickets until /allocate is typed in it.
        var dir = Path.Combine(Path.GetTempPath(), "claude-" + Guid.NewGuid().ToString("N"));
        var ws = Path.Combine(dir, "ws");
        string RepoDir(string name) { var r = Path.Combine(ws, name); Directory.CreateDirectory(Path.Combine(r, ".git")); return r; }
        var (a, b) = (RepoDir("A"), RepoDir("B"));
        try
        {
            Setup.AgentGate.Ensure(dir);
            void Hook(int cli, string cwd, string hookEvent, string prompt = "")
            {
                var psi = new System.Diagnostics.ProcessStartInfo("python", $"-I \"{Path.Combine(dir, "context-gate.py")}\"")
                    { RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false };
                psi.Environment["USERPROFILE"] = dir;
                psi.Environment["APPDATA"] = dir;
                psi.Environment["MINDATTIC_WORKSPACE"] = ws;
                psi.Environment["MINDATTIC_CLI_PID"] = cli.ToString();
                using var p = System.Diagnostics.Process.Start(psi)!;
                p.StandardInput.Write(System.Text.Json.JsonSerializer.Serialize(new { session_id = "s" + cli, hook_event_name = hookEvent, cwd, prompt }));
                p.StandardInput.Close();
                p.StandardOutput.ReadToEnd();
                Assert.That(p.WaitForExit(20_000), Is.True);
            }
            var ctx = Path.Combine(dir, ".claude", "session-context");
            string? Mark(int cli) => File.Exists(Path.Combine(ctx, $"autopilot-{cli}")) ? File.ReadAllText(Path.Combine(ctx, $"autopilot-{cli}")) : null;

            Hook(1, a, "SessionStart");
            Hook(1, a, "UserPromptSubmit", "fix the build");
            Hook(2, b, "SessionStart");
            Hook(2, b, "UserPromptSubmit", "hello");
            Assert.That((Mark(1), Mark(2)), Is.EqualTo(((string?)null, (string?)null)), "opened and prompted, but nobody typed /allocate: off");

            Hook(1, a, "UserPromptSubmit", "/allocate");
            Assert.That((Mark(1), Mark(2)), Is.EqualTo(("A", (string?)null)), "typed in one CLI: on there, for its own repo, and only there");
            Hook(1, a, "UserPromptSubmit", "next thing");
            Assert.That(Mark(1), Is.EqualTo("A"), "stays on across prompts");

            Hook(1, a, "UserPromptSubmit", "/allocate off");
            Hook(1, a, "UserPromptSubmit", "carry on");
            Assert.That(Mark(1), Is.Null, "/allocate off still stops it");

            Hook(1, a, "UserPromptSubmit", "/allocate");
            Hook(2, b, "UserPromptSubmit", "/allocate");
            Hook(3, b, "UserPromptSubmit", "/allocate off all");
            Hook(2, b, "UserPromptSubmit", "carry on");
            Assert.That((Mark(1), Mark(2), Mark(3)), Is.EqualTo(((string?)null, (string?)null, (string?)null)), "off all stops every CLI");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public void Allocate_is_the_one_command_and_autopilot_and_work_are_gone()
    {
        var dir = Path.Combine(Path.GetTempPath(), "claude-" + Guid.NewGuid().ToString("N"));
        var ws = Path.Combine(dir, "ws");
        var repo = Path.Combine(ws, "A");
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        try
        {
            Setup.AgentGate.Ensure(dir);
            void Typed(string prompt)
            {
                var psi = new System.Diagnostics.ProcessStartInfo("python", $"-I \"{Path.Combine(dir, "context-gate.py")}\"")
                    { RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false };
                psi.Environment["USERPROFILE"] = dir;
                psi.Environment["APPDATA"] = dir;
                psi.Environment["MINDATTIC_WORKSPACE"] = ws;
                psi.Environment["MINDATTIC_CLI_PID"] = "7";
                using var p = System.Diagnostics.Process.Start(psi)!;
                p.StandardInput.Write(System.Text.Json.JsonSerializer.Serialize(new { session_id = "s7", hook_event_name = "UserPromptSubmit", cwd = repo, prompt }));
                p.StandardInput.Close();
                p.StandardOutput.ReadToEnd();
                Assert.That(p.WaitForExit(20_000), Is.True);
            }
            var mark = Path.Combine(dir, ".claude", "session-context", "autopilot-7");

            // M.ORCH-T560: /allocate replaced /autopilot and its alias /work; the old command files go once they're still ours.
            File.WriteAllText(Path.Combine(dir, "commands", "autopilot.md"), Setup.AgentGate.RetiredCommands.Single(c => c.File == "autopilot.md").Body);
            File.WriteAllText(Path.Combine(dir, "commands", "work.md"), Setup.AgentGate.RetiredCommands.Single(c => c.File == "work.md").Body);
            Setup.AgentGate.Ensure(dir);
            Assert.That(File.Exists(Path.Combine(dir, "commands", "autopilot.md")), Is.False);
            Assert.That(File.Exists(Path.Combine(dir, "commands", "work.md")), Is.False);

            Typed("/work");
            Typed("/autopilot");
            Assert.That(File.Exists(mark), Is.False, "/work and /autopilot no longer switch it on");
            Typed("/allocate");
            Assert.That(File.ReadAllText(mark), Is.EqualTo("A"), "on for this CLI's repo");
            Typed("/allocate off");
            Assert.That(File.Exists(mark), Is.False);
            Typed("/allocate -repo=B,C");
            Assert.That(File.ReadAllText(mark), Is.EqualTo("B,C"));

            var command = File.ReadAllText(Path.Combine(dir, "commands", "allocate.md"));
            Assert.That(command, Does.Contain("Auto-allocates").And.Contain("orchestrator next").And.Not.Contain("/autopilot"));
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
        // A SubagentStop that never came kept a mark alive for up to 6 hours, and its transcript re-read every refresh (M.ORCH-T114).
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
