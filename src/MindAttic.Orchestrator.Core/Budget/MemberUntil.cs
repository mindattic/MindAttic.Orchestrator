using System.Globalization;
using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Budget;

/// <summary>
/// Per-member "until" dates (M.ORCH-T1401): a member whose Claude seat lapses on a known day stays on the roster with its CLI
/// tabs auto-started and respawned until then; once the date has passed the dashboard hides the member's tab (so nothing starts
/// or respawns for it) -- its membership row, login folder, credentials and data are never touched, and clearing the date brings
/// the tab back. The dates are the <see cref="Settings.OrchestratorSettings.MemberUntil"/> setting: entries
/// <c>email=date</c> separated by <c>;</c> or newlines, where a date is <c>yyyy-MM-dd</c> (that whole day, until its end in local
/// time) or <c>yyyy-MM-ddTHH:mm[:ss]</c> (that local moment).
/// </summary>
public static class MemberUntil
{
    /// <summary>What a fresh settings file carries: Honey's and Waffle's Claude Team seats were revoked on 2026-11-02 (the ticket's
    /// note), so their until date is that day's start of 2026-11-11, already past.</summary>
    public const string Default = "honey@mindattic.com=2026-11-11T00:00;waffle@mindattic.com=2026-11-11T00:00";

    /// <summary>Each well-formed entry of <paramref name="setting"/>: the normalized email and the local moment it lapses at.
    /// A malformed entry is skipped, so a typo hides nobody. The last entry for an email wins.</summary>
    public static IReadOnlyDictionary<string, DateTime> Parse(string? setting)
    {
        var result = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        foreach (var entry in (setting ?? "").Split([';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = entry.IndexOf('=');
            if (eq <= 0) continue;
            if (AccountDirs.Normalize(entry[..eq]) is not { } email) continue;
            if (ParseDate(entry[(eq + 1)..]) is { } until) result[email] = until;
        }
        return result;
    }

    /// <summary>A date text as the local moment it lapses at: a bare day lapses at that day's end, a given time at that time; null
    /// when it isn't a date.</summary>
    public static DateTime? ParseDate(string? text)
    {
        var t = text?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        if (DateTime.TryParseExact(t, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            return day.Date.AddDays(1).AddTicks(-1);
        return DateTime.TryParseExact(t, ["yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var at) ? at : null;
    }

    /// <summary>Whether <paramref name="email"/>'s until date (in <paramref name="setting"/>) has passed at <paramref name="nowLocal"/>;
    /// false for a member with no date, so clearing it undoes the drop.</summary>
    public static bool IsLapsed(string? setting, string? email, DateTime nowLocal) =>
        AccountDirs.Normalize(email) is { } key && Parse(setting).TryGetValue(key, out var until) && nowLocal > until;

    /// <summary>Every email in <paramref name="setting"/> whose date has passed at <paramref name="nowLocal"/>: the logins the
    /// dashboard drops from its roster.</summary>
    public static IReadOnlySet<string> Lapsed(string? setting, DateTime nowLocal) =>
        Parse(setting).Where(p => nowLocal > p.Value).Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
}
