using System.Globalization;
using System.Text.RegularExpressions;

namespace MindAttic.Orchestrator.Reminders;

/// <summary>
/// Reads <c>remind --at</c>, local time: a clock time ("9am", "9:30 pm", "21:00", "noon"; the next one to come), a day and
/// an optional time ("tomorrow", "tomorrow 8am", "fri 5pm", "today 3pm", "tonight"; a day alone is 9:00 AM, tonight 8:00 PM),
/// a span from now ("in 2h", "30m", "1h30m", "2d"), "now", or a full date ("2026-10-20 09:00").
/// </summary>
public static partial class ReminderTime
{
    public static readonly TimeOnly DefaultTime = new(9, 0);
    private static readonly TimeOnly Tonight = new(20, 0);

    /// <summary>The local time <paramref name="text"/> names, read against <paramref name="nowLocal"/>; false when it can't be read.</summary>
    public static bool TryParse(string? text, DateTime nowLocal, out DateTime local) => TryParse(text, nowLocal, out local, out _);

    /// <summary>
    /// When <paramref name="text"/> falls, in UTC, read against <paramref name="nowUtc"/> in <paramref name="zone"/>
    /// (<paramref name="local"/> is that time on the zone's clock). A span from now is added to <paramref name="nowUtc"/>, not to
    /// the wall clock: "in 2h" across a DST change is two hours away, not an hour more or less (M.ORCH-T552).
    /// </summary>
    public static bool TryParseUtc(string? text, DateTime nowUtc, TimeZoneInfo zone, out DateTime utc, out DateTime local)
    {
        utc = default;
        nowUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        if (!TryParse(text, TimeZoneInfo.ConvertTimeFromUtc(nowUtc, zone), out local, out var span)) return false;
        if (span is { } by)
        {
            utc = nowUtc + by;
            local = TimeZoneInfo.ConvertTimeFromUtc(utc, zone);
            return true;
        }
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        // A clock time the spring-forward skips ("2:30am" that night) is read at the zone's standard offset, as
        // DateTime.ToUniversalTime reads it.
        utc = zone.IsInvalidTime(local)
            ? DateTime.SpecifyKind(local - zone.BaseUtcOffset, DateTimeKind.Utc)
            : TimeZoneInfo.ConvertTimeToUtc(local, zone);
        return true;
    }

    private static bool TryParse(string? text, DateTime nowLocal, out DateTime local, out TimeSpan? fromNow)
    {
        local = default;
        fromNow = null;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = Spaces().Replace(text.Trim().ToLowerInvariant(), " ");
        if (t.StartsWith("at ", StringComparison.Ordinal)) t = t[3..];
        if (t == "now") { local = nowLocal; fromNow = TimeSpan.Zero; return true; } // zero span: TryParseUtc takes nowUtc as is (M.ORCH-T724)

        if (Span().Match(t) is { Success: true } span && (span.Groups["d"].Success || span.Groups["h"].Success || span.Groups["m"].Success))
        {
            double Of(string g) => span.Groups[g].Success ? double.Parse(span.Groups[g].Value, CultureInfo.InvariantCulture) : 0;
            TimeSpan by;
            // A span past what a date can hold is unreadable, not a crash (M.ORCH-T1090).
            try
            {
                by = TimeSpan.FromDays(Of("d")) + TimeSpan.FromHours(Of("h")) + TimeSpan.FromMinutes(Of("m"));
                if (by <= TimeSpan.Zero) return false;
                local = nowLocal + by;
            }
            catch (Exception ex) when (ex is OverflowException or ArgumentOutOfRangeException) { return false; }
            fromNow = by;
            return true;
        }

        var parts = t.Split(' ', 2);
        var rest = parts.Length > 1 ? parts[1].Trim() : "";
        if (rest.StartsWith("at ", StringComparison.Ordinal)) rest = rest[3..];
        if (parts[0] is "today" or "tonight" or "tomorrow" or "tmrw" or "tmr" || Weekday(parts[0]) is not null)
        {
            TimeOnly time;
            if (rest.Length == 0)
            {
                if (parts[0] == "today") return false; // today at what time?
                time = parts[0] == "tonight" ? Tonight : DefaultTime;
            }
            else if (!TryTime(rest, out time)) return false;
            var today = DateOnly.FromDateTime(nowLocal);
            DateOnly day;
            if (parts[0] is "today" or "tonight") day = today;
            else if (parts[0] is "tomorrow" or "tmrw" or "tmr") day = today.AddDays(1);
            else
            {
                var ahead = ((int)Weekday(parts[0])!.Value - (int)today.DayOfWeek + 7) % 7;
                if (ahead == 0 && time.ToTimeSpan() <= nowLocal.TimeOfDay) ahead = 7; // "mon 9am" said Monday at noon: next Monday
                day = today.AddDays(ahead);
            }
            local = day.ToDateTime(time);
            return true;
        }

        if (TryTime(t, out var at))
        {
            local = DateOnly.FromDateTime(nowLocal).ToDateTime(at);
            if (local <= nowLocal) local = local.AddDays(1);
            return true;
        }

        if (DateTime.TryParse(text.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var full) ||
            DateTime.TryParse(text.Trim(), CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out full))
        {
            // A date with no time ("2026-10-20") is that morning, as "tomorrow" is.
            local = full.TimeOfDay == TimeSpan.Zero && !text.Contains(':') ? full.Date + DefaultTime.ToTimeSpan() : full;
            local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            return true;
        }
        return false;
    }

    private static readonly string[] TimeFormats = ["htt", "h:mmtt", "H:mm", "HH:mm"];

    /// <summary>A clock time: "9am", "9 am", "9:30pm", "9a", "21:00", "noon", "midnight".</summary>
    internal static bool TryTime(string text, out TimeOnly time)
    {
        var t = text.Replace(" ", "").Replace(".", "").ToUpperInvariant();
        switch (t)
        {
            case "NOON": time = new TimeOnly(12, 0); return true;
            case "MIDNIGHT": time = new TimeOnly(0, 0); return true;
        }
        if (t.EndsWith('A') || t.EndsWith('P')) t += "M";
        return TimeOnly.TryParseExact(t, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
    }

    private static DayOfWeek? Weekday(string word)
    {
        if (word.Length < 3) return null;
        foreach (var d in Enum.GetValues<DayOfWeek>())
        {
            var name = d.ToString().ToLowerInvariant();
            if (name.StartsWith(word, StringComparison.Ordinal) || word == name + "s") return d;
        }
        return null;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"^(?:in |\+)?\s*(?:(?<d>\d+(?:\.\d+)?)\s*(?:d|days?))?\s*(?:(?<h>\d+(?:\.\d+)?)\s*(?:h|hrs?|hours?))?\s*(?:(?<m>\d+)\s*(?:m|mins?|minutes?))?$")]
    private static partial Regex Span();
}
