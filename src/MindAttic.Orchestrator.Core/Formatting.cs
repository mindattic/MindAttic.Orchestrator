using System.Globalization;

namespace MindAttic.Orchestrator;

public static class Formatting
{
    /// <summary>When a limit resets, local time: the clock alone today, with the weekday on any other day (a weekly wall's
    /// reset can be days away, and "3:00 PM" alone reads as today, even when it's tomorrow's, under a day off: M.ORCH-T403).</summary>
    public static string ResetTime(DateTime resetUtc, DateTime nowUtc) =>
        resetUtc.ToLocalTime().ToString(resetUtc.ToLocalTime().Date != nowUtc.ToLocalTime().Date ? "ddd h:mm tt" : "h:mm tt",
            System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A reset with its weekday, date and time, local: "Mon 10/27 5:00 PM" (the weekly tile, M.ORCH-T628).</summary>
    public static string ResetOnDay(DateTime resetUtc) =>
        resetUtc.ToLocalTime().ToString("ddd M/d h:mm tt", CultureInfo.InvariantCulture);

    /// <summary>A weekly reset with how far off it is: "10/27 5:00pm (6.3 days)".</summary>
    public static string ResetInDays(DateTime resetUtc, DateTime nowUtc) =>
        $"{DateAndTime(resetUtc)} ({Tenths((resetUtc - nowUtc).TotalDays)} days)";

    /// <summary>A 5-hour window's reset with how far off it is: "10/21 1:40pm (4.5 hours)".</summary>
    public static string ResetInHours(DateTime resetUtc, DateTime nowUtc) =>
        $"{DateAndTime(resetUtc)} ({Tenths((resetUtc - nowUtc).TotalHours)} hours)";

    private static string DateAndTime(DateTime utc) =>
        utc.ToLocalTime().ToString("M/d h:mmtt", CultureInfo.InvariantCulture).ToLowerInvariant();

    private static string Tenths(double n) => n.ToString("0.0", CultureInfo.InvariantCulture);

    /// <summary>Where the 5-hour percent gains a decimal: near the 98% target a whole percent hides the last few points.</summary>
    public const double FiveHourTenthsFrom = 90;

    /// <summary>The 5-hour usage percent, with its sign: whole percents below 90 ("42%"), one decimal from 90 on ("90.0%",
    /// "96.4%"), so the last stretch to the stop shows its movement (M.ORCH-T594). A value just under 90 never rounds up
    /// to a whole "90%": 89.6 reads "89%", since it hasn't reached the line where the decimal starts.</summary>
    public static string FiveHourPercent(double percent) =>
        (percent >= FiveHourTenthsFrom ? percent.ToString("0.0", CultureInfo.InvariantCulture)
            : Math.Min(Math.Round(percent, MidpointRounding.AwayFromZero), 89).ToString("0", CultureInfo.InvariantCulture)) + "%";

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
