namespace MindAttic.Orchestrator.Activity;

/// <summary>One tool call an agent made: when, which tool, and its exact input.</summary>
public sealed record ToolCall(DateTime AtUtc, string Name, string Input);

/// <summary>A session repeating itself: the call it keeps making, how often in the recent window, and since when.</summary>
public sealed record Loop(string Tool, string Input, int Repeats, DateTime SinceUtc)
{
    /// <summary>The loop in one line: tool, input cut to 60 characters, and the repeat count.</summary>
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
    /// <summary>How many of the latest calls are looked at.</summary>
    public const int Window = 12;
    /// <summary>How many identical calls within the window count as a loop.</summary>
    public const int Threshold = 5;

    /// <summary>The most repeated identical call among the last <see cref="Window"/> of <paramref name="calls"/>, if made at least
    /// <see cref="Threshold"/> times; null otherwise.</summary>
    public static Loop? Find(IReadOnlyList<ToolCall> calls)
    {
        var recent = calls.Skip(Math.Max(0, calls.Count - Window)).ToList();
        var worst = recent.GroupBy(c => (c.Name, c.Input)).MaxBy(g => g.Count());
        return worst is { } g && g.Count() >= Threshold
            ? new Loop(g.Key.Name, g.Key.Input, g.Count(), g.Min(c => c.AtUtc))
            : null;
    }
}
