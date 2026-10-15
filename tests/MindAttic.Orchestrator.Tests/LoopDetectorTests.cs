using MindAttic.Orchestrator.Activity;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class LoopDetectorTests
{
    private static readonly DateTime T0 = new(2026, 10, 15, 4, 0, 0, DateTimeKind.Utc);

    private static List<ToolCall> Calls(params (string Tool, string Input)[] calls) =>
        [.. calls.Select((c, i) => new ToolCall(T0.AddSeconds(i), c.Tool, c.Input))];

    private static (string, string)[] Repeat(int n, string tool = "Bash", string input = "dotnet test") =>
        [.. Enumerable.Repeat((tool, input), n)];

    [Test]
    public void Threshold_identical_calls_is_a_loop_and_one_fewer_is_not()
    {
        Assert.That(LoopDetector.Find(Calls(Repeat(LoopDetector.Threshold))), Is.Not.Null);
        Assert.That(LoopDetector.Find(Calls(Repeat(LoopDetector.Threshold - 1))), Is.Null);
        Assert.That(LoopDetector.Find([]), Is.Null);
    }

    [Test]
    public void Only_the_last_window_of_calls_counts()
    {
        var old = Repeat(LoopDetector.Threshold);
        var fresh = Enumerable.Range(0, LoopDetector.Window).Select(i => ("Read", $"file{i}.cs")).ToArray();
        Assert.That(LoopDetector.Find(Calls([.. old, .. fresh])), Is.Null, "the repeats scrolled out of the window");
    }

    [Test]
    public void The_same_tool_with_different_input_is_not_a_repeat()
    {
        var calls = Enumerable.Range(0, 8).Select(i => ("Bash", $"dotnet test --filter T{i}")).ToArray();
        Assert.That(LoopDetector.Find(Calls(calls)), Is.Null);
    }

    [Test]
    public void A_loop_reports_its_count_its_first_repeat_and_a_short_description()
    {
        var input = new string('x', 80);
        var loop = LoopDetector.Find(Calls([("Read", "a"), .. Repeat(6, "Bash", input)]))!;
        Assert.That((loop.Tool, loop.Repeats, loop.SinceUtc), Is.EqualTo(("Bash", 6, T0.AddSeconds(1))));
        Assert.That(loop.Describe(), Is.EqualTo($"Bash {new string('x', 60)}… ×6"), "input past 60 characters is cut");
    }
}
