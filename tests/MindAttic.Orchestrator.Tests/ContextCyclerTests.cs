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
            Assert.That(File.ReadAllText(Path.Combine(dir, "context-gate.py")), Does.Contain("PERCENT = 75"));

            Assert.That(Setup.AgentGate.Ensure(dir, contextPercent: 75), Is.Null, "unchanged: nothing rewritten");
            Assert.That(Setup.AgentGate.Ensure(dir, contextPercent: 85), Is.EqualTo("wrote context-gate.py"), "a new cycle point rewrites only the script");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
