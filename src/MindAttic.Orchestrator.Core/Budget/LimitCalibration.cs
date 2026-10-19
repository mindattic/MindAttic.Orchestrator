using System.Globalization;
using System.Text.RegularExpressions;

namespace MindAttic.Orchestrator.Budget;

/// <summary>
/// One time you ran into the wall: when, which limit, when it reset, and how many uncached tokens the
/// local transcripts show for that window up to the hit. <see cref="Covered"/> is false when the
/// transcripts didn't reach back to the window's start, so the count would be short.
/// </summary>
public sealed record LimitObservation(LimitKind Kind, DateTime HitUtc, DateTime ResetUtc, long FreshUsed, bool Covered = true);

/// <summary>
/// The limits learned from your own limit hits. Anthropic doesn't publish them, and they have moved
/// (hits on this machine went from ~3M to ~13M uncached per session window in October 2026), so only
/// the most recent hits count. The local count is a floor: usage on claude.ai or another machine
/// spends the same limit without showing up here, which errs toward pacing too early, not too late.
/// </summary>
public sealed record LimitCalibration(IReadOnlyList<LimitObservation> Observations)
{
    public const int RecentHits = 5;
    public static readonly TimeSpan Keep = TimeSpan.FromDays(60);

    public static TimeSpan Window(LimitKind kind) => kind == LimitKind.Session ? BudgetCalculator.BlockLength : TimeSpan.FromDays(7);

    public long? Learned5h => Median(LimitKind.Session);
    public long? LearnedWeekly => Median(LimitKind.Weekly);

    /// <summary>Whether a hit at this time was the signed-in account's (null: every hit is). Another account's week resets at its
    /// own time and is sized by its own plan, so its weekly hits neither date nor size this account's week; nor do its 5-hour
    /// hits size this account's 5-hour window: after a /login its learned limit was paired with the new account's percent
    /// (M.ORCH-T486). Left out of JSON: the serializer refuses a delegate, and the calibration travels in the published
    /// snapshot (M.ORCH-T390).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Func<DateTime, bool>? Mine { get; init; }

    private readonly IReadOnlyList<DateTime>? theirs;

    /// <summary>The hit times of the observations that were another account's: worked out from <see cref="Mine"/> by the
    /// process that knows the sign-in, and what a process reading the published snapshot (which has no <see cref="Mine"/>)
    /// filters by, or its learned limits and hit counts took in every account's hits (M.ORCH-T447, M.ORCH-T486).</summary>
    public IReadOnlyList<DateTime>? Theirs
    {
        get => Mine is { } mine
            ? [.. Observations.Where(o => !mine(o.HitUtc)).Select(o => o.HitUtc)]
            : theirs;
        init => theirs = value;
    }

    private bool Own(LimitObservation o) => Mine is { } mine ? mine(o.HitUtc) : theirs is null || !theirs.Contains(o.HitUtc);

    /// <summary>How many hits the learned value for <paramref name="kind"/> is drawn from.</summary>
    public int Basis(LimitKind kind) => Usable(kind).Count();

    /// <summary>The latest wall of this kind you're still behind, or null.</summary>
    public LimitObservation? Active(LimitKind kind, DateTime nowUtc) => Observations
        .Where(o => o.Kind == kind && o.HitUtc <= nowUtc && o.ResetUtc > nowUtc)
        .MaxBy(o => o.ResetUtc);

    /// <summary>The weekly reset day and time your latest weekly hit reported, in local time.</summary>
    public WeeklyReset? LearnedWeeklyReset(TimeZoneInfo? zone = null)
    {
        var last = Observations.Where(o => o.Kind == LimitKind.Weekly && Own(o)).MaxBy(o => o.HitUtc);
        if (last is null) return null;
        var local = TimeZoneInfo.ConvertTimeFromUtc(last.ResetUtc, zone ?? TimeZoneInfo.Local);
        return new WeeklyReset(local.DayOfWeek, TimeOnly.FromDateTime(local));
    }

    private IEnumerable<LimitObservation> Usable(LimitKind kind) => Observations
        .Where(o => o.Kind == kind && o.Covered && o.FreshUsed > 0 && Own(o))
        .OrderByDescending(o => o.HitUtc)
        .Take(RecentHits);

    // With an even count, the lower middle: when hits disagree, pace to the tighter one.
    private long? Median(LimitKind kind)
    {
        var used = Usable(kind).Select(o => o.FreshUsed).Order().ToList();
        return used.Count == 0 ? null : used[(used.Count - 1) / 2];
    }

    /// <summary>
    /// Turns hits into observations. Every session open at the wall writes its own message, so hits
    /// that share a reset are one observation, dated by the first.
    /// </summary>
    public static IReadOnlyList<LimitObservation> Observe(IEnumerable<LimitHit> hits, IReadOnlyList<UsageEvent> events, DateTime coverageStartUtc) =>
        [.. hits
            .GroupBy(h => (h.Kind, h.ResetUtc))
            .Select(g =>
            {
                var at = g.Min(h => h.AtUtc);
                var start = g.Key.ResetUtc - Window(g.Key.Kind);
                long used = 0;
                foreach (var e in events)
                    if (e.AtUtc >= start && e.AtUtc <= at) used += e.Usage.Fresh;
                return new LimitObservation(g.Key.Kind, at, g.Key.ResetUtc, used, start >= coverageStartUtc);
            })
            .OrderBy(o => o.HitUtc)];

    /// <summary>Adds newly seen observations to the stored ones, preferring the earliest and best-covered sighting of each wall.</summary>
    public static List<LimitObservation> Merge(IEnumerable<LimitObservation> stored, IEnumerable<LimitObservation> observed, DateTime nowUtc)
    {
        var byWall = new Dictionary<(LimitKind, DateTime), LimitObservation>();
        foreach (var o in stored) byWall[(o.Kind, o.ResetUtc)] = o;
        foreach (var o in observed)
        {
            if (!byWall.TryGetValue((o.Kind, o.ResetUtc), out var had) || (o.Covered && !had.Covered) || (o.Covered == had.Covered && o.HitUtc < had.HitUtc))
                byWall[(o.Kind, o.ResetUtc)] = o;
        }
        return [.. byWall.Values.Where(o => o.HitUtc >= nowUtc - Keep).OrderBy(o => o.HitUtc)];
    }
}

/// <summary>Reads Claude Code's limit message: "You've hit your session limit · resets 2pm (America/Chicago)".</summary>
public static partial class LimitHitParser
{
    [GeneratedRegex(@"hit your (?<kind>session|weekly) limit\W+resets (?:(?<mon>[A-Za-z]{3})[a-z]* (?<day>\d{1,2}),? )?(?<h>\d{1,2})(?::(?<m>\d{2}))?\s*(?<ap>am|pm)(?:\s*\((?<zone>[^)]+)\))?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Message();

    /// <summary>The furthest a session limit's reset can be from the message, by the clock: its 5 hours, and one for a DST change.</summary>
    private static readonly TimeSpan SessionResetReach = TimeSpan.FromHours(6);

    /// <summary>
    /// A time with no date is the next occurrence after the message (a session reset just passed stays the one passed). A date with no year is this
    /// year's, or next year's when that's the only way it lands after the message.
    /// </summary>
    public static bool TryParse(string text, DateTime atUtc, out LimitHit hit)
    {
        hit = default;
        var m = Message().Match(text);
        if (!m.Success) return false;

        var zone = Zone(m.Groups["zone"].Value);
        var local = TimeZoneInfo.ConvertTimeFromUtc(atUtc, zone);
        var hour = int.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture) % 12 +
                   (m.Groups["ap"].Value.Equals("pm", StringComparison.OrdinalIgnoreCase) ? 12 : 0);
        var minute = m.Groups["m"].Success ? int.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture) : 0;
        if (minute > 59) return false;

        DateTime reset;
        if (m.Groups["mon"].Success)
        {
            var month = Array.FindIndex(CultureInfo.InvariantCulture.DateTimeFormat.AbbreviatedMonthNames,
                n => n.Equals(m.Groups["mon"].Value, StringComparison.OrdinalIgnoreCase)) + 1;
            var day = int.Parse(m.Groups["day"].Value, CultureInfo.InvariantCulture);
            if (month == 0 || day < 1 || day > DateTime.DaysInMonth(local.Year, month)) return false;
            reset = new DateTime(local.Year, month, day, hour, minute, 0);
            if (reset < local.AddDays(-1)) reset = reset.AddYears(1);
        }
        else
        {
            reset = local.Date.AddHours(hour).AddMinutes(minute);
            // A session window is 5 hours, so a session reset is never most of a day off: one the message was written just
            // after (the refusal logged a second late, or a reset shown cut to the minute) is the one just passed, not
            // tomorrow's, which would hold every CLI at the stop for a day (M.ORCH-T381). An hour's slack covers a DST change.
            var kindIsSession = m.Groups["kind"].Value.Equals("session", StringComparison.OrdinalIgnoreCase);
            if (reset <= local && (!kindIsSession || reset.AddDays(1) - local <= SessionResetReach)) reset = reset.AddDays(1);
        }

        while (zone.IsInvalidTime(reset)) reset = reset.AddMinutes(30);
        var kind = m.Groups["kind"].Value.Equals("weekly", StringComparison.OrdinalIgnoreCase) ? LimitKind.Weekly : LimitKind.Session;
        hit = new LimitHit(atUtc, kind, TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(reset, DateTimeKind.Unspecified), zone));
        return true;
    }

    private static TimeZoneInfo Zone(string id)
    {
        if (id.Length == 0) return TimeZoneInfo.Local;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { return TimeZoneInfo.Local; }
    }
}
