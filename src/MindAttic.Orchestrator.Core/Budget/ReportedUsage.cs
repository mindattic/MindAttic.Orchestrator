using System.Globalization;
using System.Text.Json;

namespace MindAttic.Orchestrator.Budget;

/// <summary>One limit window as Claude Code reports it: percent used and when it resets.</summary>
public sealed record ReportedWindow(double Percent, DateTime ResetUtc);

/// <summary>One 5-hour report, with the local uncached tokens counted in its window by then.</summary>
public sealed record UsageSample(DateTime AtUtc, double Percent, DateTime ResetUtc, long WindowFresh);

/// <summary>
/// The 5-hour limit in tokens, from how far Claude's percent moved while local transcripts recorded a
/// known number of tokens, within one window. Using the change, not the level, means tokens from another
/// account (after a switch), claude.ai or another machine cancel out instead of skewing the size.
/// </summary>
public static class ReportedLimit
{
    /// <summary>The smallest percent change to size a limit by: whole-percent reports are ±1 at each end.</summary>
    public const double MinDelta = 4;

    /// <summary>Records <paramref name="reported"/> once (keyed by its time) and trims to a week. A report below one
    /// already recorded for its window is stale (see <see cref="Highest"/>) and isn't recorded.</summary>
    public static bool Record(List<UsageSample> samples, ReportedUsage? reported, IReadOnlyList<UsageEvent> events, DateTime nowUtc)
    {
        if (reported?.FiveHour is not { } w || samples.Any(s => s.AtUtc == reported.AtUtc)) return false;
        if (samples.Any(s => s.ResetUtc == w.ResetUtc && s.Percent > w.Percent)) return false;
        var start = w.ResetUtc - BudgetCalculator.BlockLength;
        var fresh = events.Where(e => e.AtUtc >= start && e.AtUtc <= reported.AtUtc).Sum(e => e.Usage.Fresh);
        samples.Add(new UsageSample(reported.AtUtc, w.Percent, w.ResetUtc, fresh));
        samples.RemoveAll(s => nowUtc - s.AtUtc > TimeSpan.FromDays(7));
        return true;
    }

    /// <summary>Tokens per 100% from the most recent window whose reports moved at least <see cref="MinDelta"/>;
    /// the change is taken a point wider than reported, so the limit errs small. Null until one has.</summary>
    public static long? Estimate(IReadOnlyList<UsageSample> samples)
    {
        foreach (var window in samples.GroupBy(s => s.ResetUtc).OrderByDescending(g => g.Key))
        {
            // Stale reports recorded before Record skipped them would end the window low; only rises count.
            var ordered = new List<UsageSample>();
            foreach (var s in window.OrderBy(s => s.AtUtc))
                if (ordered.Count == 0 || s.Percent >= ordered[^1].Percent) ordered.Add(s);
            var (first, last) = (ordered[0], ordered[^1]);
            var delta = last.Percent - first.Percent;
            var spent = last.WindowFresh - first.WindowFresh;
            if (delta >= MinDelta && spent > 0) return (long)(spent / ((delta + 1) / 100));
        }
        return null;
    }

    /// <summary>
    /// <paramref name="reported"/> with its 5-hour percent raised to the highest recorded for the same window (as of
    /// that sample's time). Every open Claude session's statusline rewrites <c>claude-usage.json</c> each minute with
    /// the percent from its own last API response, so an idle one stamps a stale, lower percent with the current time
    /// (12% while the window was at 35%). Usage within a window never goes down, so the highest is the truest.
    /// With <paramref name="nowUtc"/>, a report with no 5-hour window, or one from a window that already reset (an idle
    /// session's last response came before it), is replaced by the highest recorded for the window still running, so
    /// the budget doesn't drop to a learned limit between Claude's reports (M.ORCH-T179).
    /// </summary>
    public static ReportedUsage? Highest(IReadOnlyList<UsageSample> samples, ReportedUsage? reported, DateTime? nowUtc = null)
    {
        if (nowUtc is { } now && ReportedUsage.Usable(reported?.FiveHour, now) is null)
        {
            var running = samples.Where(s => s.ResetUtc > now && s.AtUtc <= now)
                .OrderByDescending(s => s.Percent).ThenByDescending(s => s.AtUtc).FirstOrDefault();
            if (running is not null)
                return new ReportedUsage(new ReportedWindow(running.Percent, running.ResetUtc), reported?.Week, running.AtUtc)
                    { WeekAtUtc = reported?.WeekAt };
        }
        if (reported?.FiveHour is not { } w) return reported;
        var best = samples.Where(s => s.ResetUtc == w.ResetUtc && s.Percent > w.Percent)
            .OrderByDescending(s => s.Percent).ThenByDescending(s => s.AtUtc).FirstOrDefault();
        return best is null ? reported : reported with { FiveHour = w with { Percent = best.Percent }, AtUtc = best.AtUtc, WeekAtUtc = reported.WeekAt };
    }
}

/// <summary>
/// The usage Claude Code itself reports (<c>rate_limits</c> in the statusline input), saved to
/// <c>~/.claude/claude-usage.json</c> by the shared statusline. It's the real meter, so it beats any
/// limit learned from past hits: the window's limit in tokens is what this window spent by the time of
/// the report divided by the percent, and the reset is exact.
/// </summary>
public sealed record ReportedUsage(ReportedWindow? FiveHour, ReportedWindow? Week, DateTime AtUtc)
{
    /// <summary>When <see cref="Week"/> was reported, where that differs from <see cref="AtUtc"/>: the 5-hour percent (and
    /// its time) may be swapped for an older, higher sample by <see cref="ReportedLimit.Highest"/>, but the week still
    /// comes from the current report, so its limit is sized by the tokens spent up to the current report's time.</summary>
    public DateTime? WeekAtUtc { get; init; }

    /// <summary>The time the weekly percent describes.</summary>
    public DateTime WeekAt => WeekAtUtc ?? AtUtc;

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "claude-usage.json");

    /// <summary>Below this the reported whole percent is too coarse to size a limit by (at 2%, ±0.5 is ±25%).</summary>
    public const double MinPercent = 5;

    /// <summary>How long a report stays usable without a Claude session redrawing its statusline: its whole 5-hour
    /// window. With no tab open (only background agents, which draw no statusline) the budget carries on from Claude's
    /// last percent plus what the transcripts recorded since, rather than dropping to a learned limit after an hour;
    /// a window that has reset is dropped by <see cref="Usable"/>.</summary>
    public static readonly TimeSpan MaxAge = BudgetCalculator.BlockLength;

    /// <summary>A window that's still running (its reset is ahead); null otherwise. Any percent counts:
    /// a low one is the truth about what's used, it's only too coarse to size a limit by.</summary>
    public static ReportedWindow? Usable(ReportedWindow? w, DateTime nowUtc) =>
        w is not null && w.ResetUtc > nowUtc ? w : null;

    /// <summary>The saved report, or null when there's none, it's unreadable, or it's older than <see cref="MaxAge"/>.</summary>
    public static ReportedUsage? Load(DateTime nowUtc, string? path = null)
    {
        try
        {
            var file = path ?? DefaultPath;
            if (!File.Exists(file)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            var root = doc.RootElement;
            if (!root.TryGetProperty("at", out var a) ||
                !DateTime.TryParse(a.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var at) ||
                nowUtc - at > MaxAge || !root.TryGetProperty("rate_limits", out var limits))
                return null;
            return new ReportedUsage(Window(limits, "five_hour"), Window(limits, "seven_day"), at);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private static ReportedWindow? Window(JsonElement limits, string name) =>
        limits.TryGetProperty(name, out var w) && w.ValueKind == JsonValueKind.Object &&
        w.TryGetProperty("used_percentage", out var p) && p.ValueKind == JsonValueKind.Number &&
        w.TryGetProperty("resets_at", out var r) && r.ValueKind == JsonValueKind.Number
            ? new ReportedWindow(p.GetDouble(), DateTimeOffset.FromUnixTimeSeconds(r.GetInt64()).UtcDateTime)
            : null;
}
