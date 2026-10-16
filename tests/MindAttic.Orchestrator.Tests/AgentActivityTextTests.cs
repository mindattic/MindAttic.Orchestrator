using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>What the agent board says each CLI and subagent is doing, and which CLIs it shows (M.ORCH-T169).</summary>
public sealed class AgentActivityTextTests
{
    [TestCase("Edit", """{"file_path":"D:\\x\\src\\Daemon.cs","old_string":"a"}""", "Editing Daemon.cs")]
    [TestCase("Read", """{"file_path":"/c/x/README.md"}""", "Reading README.md")]
    [TestCase("Bash", """{"command":"dotnet test\nmore","description":"Run the tests"}""", "Run the tests")]
    [TestCase("Bash", """{"command":"dotnet test tests/X\nmore"}""", "Running dotnet test tests/X")]
    [TestCase("Grep", """{"pattern":"LiveSessions"}""", "Searching for 'LiveSessions'")]
    [TestCase("Agent", """{"description":"Map the hooks","prompt":"..."}""", "Starting a subagent: Map the hooks")]
    [TestCase("mcp__claude_ai_Docs__batch", "{}", "Using batch")]
    [TestCase("Write", "not json", "Writing a file")]
    public void A_tool_call_reads_as_a_few_plain_words(string name, string input, string expected) =>
        Assert.That(ToolCallText.Describe(name, input), Is.EqualTo(expected));

    [Test]
    public void A_long_description_is_cut_to_one_short_line() =>
        Assert.That(ToolCallText.Describe("Bash", $$"""{"command":"{{new string('x', 200)}}"}"""), Has.Length.EqualTo(ToolCallText.MaxLength).And.EndsWith("…"));

    [Test]
    public void Only_running_clis_are_on_the_board_each_with_its_latest_session_and_its_own_busy_or_idle()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        var me = Environment.ProcessId; // not a Claude CLI, so nothing of this test's own is live...
        File.WriteAllText(Path.Combine(dir, "old.state.json"), $$"""{"state":"busy","pid":{{me}}}""");
        var cycler = new ContextCycler(dir);
        Assert.That(cycler.LiveSessions(DateTime.UtcNow), Is.Empty, "a pid that isn't claude.exe isn't a live CLI");

        // ...so the board's filter is checked through Build directly.
        var now = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        TranscriptSession S(string id) => new(id, @"D:\repo", now.AddHours(-3), new TokenUsage(1, 0, 0, 1), null);
        var repos = new[] { new RepoStatus("A", @"D:\A", RepoState.Working, [], [S("busy"), S("idle"), S("gone")], [], now, default) };
        var rows = AgentBoard.Build(repos, null, [], [], TimeSpan.FromMinutes(10), now,
            new Dictionary<string, bool> { ["busy"] = true, ["idle"] = false });
        Assert.That(rows.Select(r => (r.SessionId, r.State)), Is.EquivalentTo(new[] { ("busy", AgentState.Working), ("idle", AgentState.Waiting) }),
            "a closed CLI's session is gone, and a live one's busy flag beats its transcript's age");
    }

    [Test]
    public void A_subagent_says_what_it_was_asked_and_what_its_doing()
    {
        var sessionDir = Directory.CreateTempSubdirectory().FullName;
        var projectsDir = Directory.CreateTempSubdirectory().FullName;
        Directory.CreateDirectory(Path.Combine(sessionDir, "subagents"));
        File.WriteAllText(Path.Combine(sessionDir, "subagents", "a1"), "");
        var folder = Path.Combine(projectsDir, "proj", "session-1", "subagents");
        Directory.CreateDirectory(folder);
        File.WriteAllLines(Path.Combine(folder, "agent-a1.jsonl"),
        [
            """{"type":"user","timestamp":"2026-10-15T11:55:00Z","message":{"role":"user","content":"Find where the pace gate logs holds\nThen report back."}}""",
            """{"type":"assistant","timestamp":"2026-10-15T11:56:00Z","attributionAgent":"Explore","message":{"content":[{"type":"tool_use","name":"Grep","input":{"pattern":"gate_log"}}]}}""",
            """{"type":"user","timestamp":"2026-10-15T11:56:10Z","message":{"role":"user","content":[{"type":"tool_result","content":"x"}]}}""",
            """{"type":"assistant","timestamp":"2026-10-15T11:57:00Z","attributionAgent":"Explore","message":{"content":[{"type":"tool_use","name":"Read","input":{"file_path":"D:\\x\\AgentGate.cs"}}]}}""",
        ]);

        var s = SubagentActivityReader.Running(new ContextCycler(sessionDir), projectsDir, new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc)).Single();
        Assert.That((s.Task, s.Doing), Is.EqualTo(("Find where the pace gate logs holds", "Reading AgentGate.cs")));
    }
}
