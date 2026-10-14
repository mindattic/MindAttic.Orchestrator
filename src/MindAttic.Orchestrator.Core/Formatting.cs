using System.Globalization;

namespace MindAttic.Orchestrator;

public static class Formatting
{
    public static string Tokens(long n) => n switch
    {
        >= 1_000_000_000 => (n / 1e9).ToString("0.##", CultureInfo.InvariantCulture) + "B",
        >= 1_000_000 => (n / 1e6).ToString("0.#", CultureInfo.InvariantCulture) + "M",
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
