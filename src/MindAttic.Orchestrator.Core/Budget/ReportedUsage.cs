using System.Globalization;
using System.Text.Json;

namespace MindAttic.Orchestrator.Budget;

/// <summary>One limit window as Claude Code reports it: percent used and when it resets.</summary>
public sealed record ReportedWindow(double Percent, DateTime ResetUtc);

/// <summary>
/// The usage Claude Code itself reports (<c>rate_limits</c> in the statusline input), saved to
/// <c>~/.claude/claude-usage.json</c> by the shared statusline. It's the real meter, so it beats any
/// limit learned from past hits: the window's limit in tokens is what this window spent by the time of
/// the report divided by the percent, and the reset is exact.
/// </summary>
public sealed record ReportedUsage(ReportedWindow? FiveHour, ReportedWindow? Week, DateTime AtUtc)
{
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "claude-usage.json");

    /// <summary>Below this the reported whole percent is too coarse to size a limit by (at 2%, ±0.5 is ±25%).</summary>
    public const double MinPercent = 5;

    /// <summary>How long a report stays usable without a Claude session redrawing its statusline.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(1);

    /// <summary>A window that's still running and reported precisely enough; null otherwise.</summary>
    public static ReportedWindow? Usable(ReportedWindow? w, DateTime nowUtc) =>
        w is { Percent: >= MinPercent } && w.ResetUtc > nowUtc ? w : null;

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
