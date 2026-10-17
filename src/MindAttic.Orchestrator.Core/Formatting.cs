using System.Globalization;

namespace MindAttic.Orchestrator;

public static class Formatting
{
    /// <summary>When a limit resets, local time: the clock alone within a day, with the weekday when it's further off (a
    /// weekly wall's reset can be days away, and "3:00 PM" alone reads as today).</summary>
    public static string ResetTime(DateTime resetUtc, DateTime nowUtc) =>
        resetUtc.ToLocalTime().ToString(resetUtc - nowUtc > TimeSpan.FromDays(1) ? "ddd h:mm tt" : "h:mm tt", System.Globalization.CultureInfo.InvariantCulture);

    // The thresholds are where the smaller unit would round up to 1000 (999,999 is "1M", not "1000k").
    public static string Tokens(long n) => n switch
    {
        >= 999_950_000 => (n / 1e9).ToString("0.##", CultureInfo.InvariantCulture) + "B",
        >= 999_950 => (n / 1e6).ToString("0.#", CultureInfo.InvariantCulture) + "M",
        >= 1_000 => (n / 1e3).ToString("0.#", CultureInfo.InvariantCulture) + "k",
        _ => n.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>"now", "4m", "3h", "2d".</summary>
    public static string Span(TimeSpan d) =>
        d.TotalMinutes < 1 ? "now"
        : d.TotalHours < 1 ? $"{(int)d.TotalMinutes}m"
        : d.TotalDays < 1 ? $"{(int)d.TotalHours}h"
        : $"{(int)d.TotalDays}d";

    public static string Ago(DateTime? utc, DateTime nowUtc)
    {
        if (utc is null) return "";
        var s = Span(nowUtc - utc.Value);
        return s == "now" ? s : s + " ago";
    }
}
