using System.Globalization;

namespace MindAttic.Orchestrator.Budget;

/// <summary>
/// When the weekly limit resets: a weekday and a time of day in local time (Claude's weekly limits
/// reset on a fixed schedule, not on a rolling 7 days). Stored in settings as text like "Monday 09:00".
/// </summary>
public readonly record struct WeeklyReset(DayOfWeek Day, TimeOnly Time)
{
    // "%H", not "H": a lone letter is read as a standard format specifier and throws.
    private static readonly string[] TimeFormats = ["H:mm", "HH:mm", "h:mmtt", "h:mm tt", "htt", "h tt", "%H"];

    /// <summary>Accepts "Mon 9:00", "monday 09:00", "Tue 5pm", "Fri 5:30 PM". Blank or "none" means not set.</summary>
    public static bool TryParse(string? text, out WeeklyReset? value)
    {
        value = null;
        var t = (text ?? "").Trim();
        if (t.Length == 0 || t.Equals("none", StringComparison.OrdinalIgnoreCase)) return true;

        var space = t.IndexOf(' ');
        if (space <= 0) return false;
        var dayText = t[..space];
        var timeText = t[(space + 1)..].Trim().ToUpperInvariant();

        DayOfWeek? day = null;
        foreach (var d in Enum.GetValues<DayOfWeek>())
        {
            var name = d.ToString();
            if (dayText.Length >= 2 && name.StartsWith(dayText, StringComparison.OrdinalIgnoreCase)) { day = d; break; }
        }
        if (day is null) return false;
        if (!TimeOnly.TryParseExact(timeText, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)) return false;

        value = new WeeklyReset(day.Value, time);
        return true;
    }

    public override string ToString() => $"{Day} {Time.ToString("HH:mm", CultureInfo.InvariantCulture)}";

    /// <summary>Short display form, e.g. "Mon 9:00 AM".</summary>
    public string Display => $"{Day.ToString()[..3]} {Time.ToString("h:mm tt", CultureInfo.InvariantCulture)}";

    /// <summary>The current week: the most recent reset at or before <paramref name="nowUtc"/>, and the next one.</summary>
    public (DateTime StartUtc, DateTime NextUtc) Window(DateTime nowUtc, TimeZoneInfo? zone = null)
    {
        zone ??= TimeZoneInfo.Local;
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, zone);
        var back = ((int)nowLocal.DayOfWeek - (int)Day + 7) % 7;
        var start = nowLocal.Date.AddDays(-back).Add(Time.ToTimeSpan());
        if (start > nowLocal) start = start.AddDays(-7);
        // Convert each end separately so a DST change inside the week doesn't shift the reset hour.
        return (ToUtc(start, zone), ToUtc(start.AddDays(7), zone));
    }

    private static DateTime ToUtc(DateTime local, TimeZoneInfo zone)
    {
        // A reset time that falls in a spring-forward gap doesn't exist; use the first moment after it.
        while (zone.IsInvalidTime(local)) local = local.AddMinutes(30);
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), zone);
    }
}
