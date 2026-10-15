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
