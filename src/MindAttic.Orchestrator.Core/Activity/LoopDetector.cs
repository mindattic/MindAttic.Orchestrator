namespace MindAttic.Orchestrator.Activity;

/// <summary>One tool call an agent made: when, which tool, and its exact input.</summary>
public sealed record ToolCall(DateTime AtUtc, string Name, string Input);

/// <summary>A session repeating itself: the call it keeps making, how often in the recent window, and since when.</summary>
public sealed record Loop(string Tool, string Input, int Repeats, DateTime SinceUtc)
{
    public string Describe() => $"{Tool} {(Input.Length > 60 ? Input[..60] + "…" : Input)} ×{Repeats}";
}

/// <summary>
/// Spots an agent that's growing its transcript without getting anywhere: the identical tool call
/// (same tool, same input) made <see cref="Threshold"/> or more times among its last
/// <see cref="Window"/> calls. Re-running tests between edits looks similar but changes nothing in
/// between only when stuck, so the bar is set high.
/// </summary>
public static class LoopDetector
{
    public const int Window = 12;
    public const int Threshold = 5;

    public static Loop? Find(IReadOnlyList<ToolCall> calls)
    {
        var recent = calls.Skip(Math.Max(0, calls.Count - Window)).ToList();
        var worst = recent.GroupBy(c => (c.Name, c.Input)).MaxBy(g => g.Count());
        return worst is { } g && g.Count() >= Threshold
            ? new Loop(g.Key.Name, g.Key.Input, g.Count(), g.Min(c => c.AtUtc))
            : null;
    }
}
