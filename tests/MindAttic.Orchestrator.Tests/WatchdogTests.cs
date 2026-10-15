using System.Text.Json;
using MindAttic.Orchestrator.Activity;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class WatchdogTests
{
    private static string ToolLine(int i, string command, DateTime at) => JsonSerializer.Serialize(new
    {
        type = "assistant", timestamp = at.ToString("o"), sessionId = "s", cwd = @"D:\W\Repo",
        message = new
        {
            id = $"m{i}", model = "claude-sonnet-5",
            usage = new { input_tokens = 1, cache_creation_input_tokens = 0, cache_read_input_tokens = 0, output_tokens = 1 },
            content = new[] { new { type = "tool_use", id = $"t{i}", name = "Bash", input = new { command } } },
        },
    });

    private static TranscriptSession Read(IEnumerable<string> lines)
    {
        var dir = Path.Combine(Path.GetTempPath(), "orch-loop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "p"));
        try
        {
            File.WriteAllLines(Path.Combine(dir, "p", "s.jsonl"), lines);
            return new TranscriptReader(dir).Poll(DateTime.UtcNow.AddDays(-1)).Single();
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public void The_same_call_over_and_over_is_a_loop()
    {
        var at = DateTime.UtcNow.AddMinutes(-5);
        var s = Read(Enumerable.Range(0, 6).Select(i => ToolLine(i, i % 2 == 0 ? "dotnet test" : "cat log.txt", at.AddSeconds(i))));

        Assert.That(s.Loop, Is.Null, "two calls alternating three times each is under the bar");

        var stuck = Read(Enumerable.Range(0, 8).Select(i => ToolLine(i, i < 2 ? $"step {i}" : "dotnet test", at.AddSeconds(i))));
        Assert.That(stuck.Loop, Is.Not.Null);
        Assert.That((stuck.Loop!.Tool, stuck.Loop.Repeats), Is.EqualTo(("Bash", 6)));
        Assert.That(stuck.Loop.Describe(), Does.Contain("dotnet test"));
    }

    [Test]
    public void Only_a_session_still_writing_counts_as_stuck()
    {
        var loop = new Loop("Bash", "{}", 6, DateTime.UtcNow.AddHours(-3));
        TranscriptSession S(string id, TimeSpan ago) => new(id, @"D:\W\A", DateTime.UtcNow - ago, default, null) { Loop = loop };
        var snap = new Snapshot(DateTime.UtcNow, [new RepoStatus("A", @"D:\W\A", RepoState.Working, [],
            [S("live", TimeSpan.FromMinutes(1)), S("old", TimeSpan.FromHours(2))], [], DateTime.UtcNow, default)], []);

        Assert.That(Watchdog.Stuck(snap).Select(x => x.SessionId), Is.EqualTo(new[] { "live" }));
    }
}
