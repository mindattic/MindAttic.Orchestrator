using System.Globalization;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>A stretch of a login's readings set against what its CLIs here spent. Points are the meter's percent.</summary>
/// <param name="Rise">How far the 5-hour meter rose over the stretch.</param>
/// <param name="Explained">How much of that the login's CLIs spent by their transcripts (at its learned tokens per percent).</param>
/// <param name="FivePoints">What's left: the 5-hour points nobody here spent.</param>
/// <param name="WeekPoints">The same for the week's meter (0 when it can't be told).</param>
public sealed record OutsideStretch(DateTime FromUtc, DateTime ToUtc, double Rise, double Explained, double FivePoints, double WeekPoints)
{
    /// <summary>Whether the rise is past what the CLIs explain by more than the noise margin (<see cref="OutsideUse.Margin"/>).</summary>
    public bool IsOutside { get; init; }
}

/// <summary>
/// Tells a member using Claude outside the orchestrator (M.ORCH-T682) by subtraction: Claude's meter has no per-device data and
/// no addresses, so for each login the rise in its 5-hour (and weekly) percent over the last <see cref="Lookback"/> of readings
/// (read at least every five minutes even while idle, M.ORCH-T661) is set against what that login's orchestrated CLIs spent by
/// their transcripts over the same stretch (each reading carries the login's CLI spend up to then), converted at the login's
/// learned tokens per percent. What the CLIs can't explain, past <see cref="Margin"/>, is someone's own use.
/// <para>The margin is a base in points (<see cref="OrchestratorSettings.OutsideUseMarginPoints"/>, default 2, learned per login
/// within its bounds) plus a share (default 25%) of what the CLIs explain: the meter reports whole percents, so the two readings
/// bounding a stretch can each be up to a point off (two in all), and a reading can land a moment before or after a CLI's last
/// reply registers; the tokens per percent is an estimate whose error grows with the spend it converts (Opus turns cost more
/// per token than Haiku subagents). So with the CLIs idle it takes three points nobody here spent; with them spending 20 points
/// in the stretch, more than seven. While every CLI on the login bills the API key, only rounding is left (<see cref="KeyPoints"/>).</para>
/// </summary>
public static class OutsideUse
{
    /// <summary>How far back the readings are compared: long enough for six idle readings, short enough to notice within minutes.</summary>
    public static readonly TimeSpan Lookback = TimeSpan.FromMinutes(30);

    /// <summary>The newest reading must be this fresh to judge a stretch by.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(15);

    /// <summary>After a login's CLIs went onto the key, how long before their spend is taken to have left its meter.</summary>
    public static readonly TimeSpan Settle = TimeSpan.FromMinutes(5);

    /// <summary>The margin while every CLI on the login bills the key (none of their spend reaches its meter): only the rounding
    /// of the two readings is left, at most a point, so a rise of two whole points is someone's.</summary>
    public const double KeyPoints = 1;

    /// <summary>The unexplained points a stretch must exceed to count as outside use: <paramref name="basePoints"/> plus
    /// <paramref name="share"/> of what the CLIs explain.</summary>
    public static double Margin(double explained, double basePoints = 2, double share = 0.25) => basePoints + share * Math.Max(0, explained);

    /// <summary>Tokens per meter point: the median of each window's (the change from its first reading to the first at its
    /// highest, taken a point wider than reported, so it errs small, as <see cref="ReportedLimit.Estimate"/> does). A median, so
    /// one window that outside use made cheap, or one whose CLIs spent on the key after its top, doesn't skew it; stopping at the
    /// first reading at the top leaves out spend billed to the key with the meter standing still. Null until a window moved at
    /// least <see cref="ReportedLimit.MinDelta"/>.</summary>
    public static double? TokensPerPercent(IEnumerable<UsageSample> samples)
    {
        var per = new List<double>();
        foreach (var window in samples.GroupBy(s => s.ResetUtc))
        {
            var ordered = Rises(window);
            var first = ordered[0];
            var last = ordered.First(s => s.Percent >= ordered[^1].Percent);
            var delta = last.Percent - first.Percent;
            var spent = last.WindowFresh - first.WindowFresh;
            if (delta >= ReportedLimit.MinDelta && spent > 0) per.Add(spent / (delta + 1));
        }
        if (per.Count == 0) return null;
        per.Sort();
        return per.Count % 2 == 1 ? per[per.Count / 2] : (per[per.Count / 2 - 1] + per[per.Count / 2]) / 2;
    }

    /// <summary>The readings in time order, leaving out any below one before it (a stale report: usage never falls in a window).</summary>
    private static List<UsageSample> Rises(IEnumerable<UsageSample> window)
    {
        var ordered = new List<UsageSample>();
        foreach (var s in window.OrderBy(s => s.AtUtc))
            if (ordered.Count == 0 || s.Percent >= ordered[^1].Percent) ordered.Add(s);
        return ordered;
    }

    /// <summary>
    /// The last <see cref="Lookback"/> of the running window's readings, judged; null when there's too little to tell (fewer than
    /// two readings, the newest older than <see cref="MaxAge"/>, or CLI spend with no tokens per percent learned to convert it).
    /// </summary>
    /// <param name="onKeySinceUtc">Since when every CLI on the login bills the API key: from <see cref="Settle"/> after it, their
    /// spend no longer reaches the meter, so only readings from then are compared, and all of their rise is someone else's.</param>
    public static OutsideStretch? Judge(IReadOnlyList<UsageSample> samples, double? tokensPerPercent, double? weekRatio, DateTime nowUtc,
        DateTime? onKeySinceUtc = null, double basePoints = 2, double share = 0.25)
    {
        var from = nowUtc - Lookback;
        if (onKeySinceUtc is { } key && key + Settle > from) from = key + Settle;
        var stretch = Between(samples, from, nowUtc, tokensPerPercent, weekRatio, onKeySinceUtc is not null, basePoints, share);
        return stretch is not null && nowUtc - stretch.ToUtc <= MaxAge ? stretch : null;
    }

    /// <summary>The running window's readings from <paramref name="fromUtc"/> to <paramref name="toUtc"/>, compared as
    /// <see cref="Judge"/> does; <paramref name="onKey"/>: every CLI on the login billed the key (and had settled) all along.</summary>
    public static OutsideStretch? Between(IReadOnlyList<UsageSample> samples, DateTime fromUtc, DateTime toUtc, double? tokensPerPercent,
        double? weekRatio, bool onKey, double basePoints = 2, double share = 0.25)
    {
        var running = samples.Where(s => s.ResetUtc > toUtc && s.AtUtc <= toUtc).ToList();
        if (running.Count == 0) return null;
        var reset = running.Max(s => s.ResetUtc);
        var stretch = Rises(running.Where(s => s.ResetUtc == reset && s.AtUtc >= fromUtc));
        if (stretch.Count < 2) return null;
        var (first, last) = (stretch[0], stretch[^1]);
        var rise = last.Percent - first.Percent;
        var spent = onKey ? 0 : Math.Max(0, last.WindowFresh - first.WindowFresh);
        double explained;
        if (spent == 0) explained = 0;
        else if (tokensPerPercent is { } tpp && tpp > 0) explained = spent / tpp;
        else return null;
        var five = rise - explained;

        double week = 0;
        var outsideWeek = false;
        if (first.WeekPercent is { } w0 && last.WeekPercent is { } w1 && first.WeekResetUtc == last.WeekResetUtc &&
            (explained == 0 || weekRatio is not null))
        {
            var weekExplained = explained * (weekRatio ?? 0);
            week = w1 - w0 - weekExplained;
            outsideWeek = week > (onKey ? KeyPoints : Margin(weekExplained, basePoints, share));
        }
        return new OutsideStretch(first.AtUtc, last.AtUtc, rise, explained, five, week)
        {
            IsOutside = five > (onKey ? KeyPoints : Margin(explained, basePoints, share)) || outsideWeek,
        };
    }
}

/// <summary>One window's (or week's) own use outside the orchestrator: the meter points nobody here spent.</summary>
public sealed class OutsideWindow
{
    public DateTime ResetUtc { get; set; }
    public double Points { get; set; }
}

/// <summary>A login's outside use as the store keeps it (<see cref="OutsideUseWatch.StoreName"/>): whether someone's using
/// Claude on it now and the ceilings that holds its CLIs to, the values it has learned, and the points per window and week.</summary>
public sealed class OutsideUseLogin
{
    /// <summary>The per-CLI login's email; "" for the global login.</summary>
    public string Key { get; set; } = "";

    /// <summary>The member's name, for the toasts and the Accounts view.</summary>
    public string Name { get; set; } = "";

    /// <summary>Since when someone's been using Claude on it; null while nobody is.</summary>
    public DateTime? HeldSinceUtc { get; set; }

    /// <summary>The reading at which outside activity was last seen.</summary>
    public DateTime? LastSeenUtc { get; set; }

    /// <summary>Where the stretch that showed this episode began, to measure its rate from.</summary>
    public DateTime? EpisodeFromUtc { get; set; }

    /// <summary>The newest reading counted so far in this episode.</summary>
    public DateTime? CountedThroughUtc { get; set; }

    /// <summary>This episode's 5-hour and week points nobody here spent.</summary>
    public double EpisodePoints { get; set; }
    public double EpisodeWeekPoints { get; set; }

    /// <summary>How many readings showed new outside activity this episode.</summary>
    public int EpisodeSeen { get; set; }

    /// <summary>The 5-hour ceiling its CLIs are held to now, and why; null while nobody's using it.</summary>
    public int? CeilingPercent { get; set; }

    /// <summary>The weekly stop its CLIs are held to now; null while nobody's using it, or with no week to read.</summary>
    public int? WeeklyStopPercent { get; set; }

    /// <summary>The ceiling the episode started at (the room it left the member: 100 minus it), and the rate it projected.</summary>
    public double CeilingAtStart { get; set; }
    public double RateAtStart { get; set; }

    // Learned per login, each within its settings' bounds; null: the setting's own value.
    public double? LearnedCeiling { get; set; }
    public double? LearnedQuietMinutes { get; set; }
    public double? LearnedMargin { get; set; }
    public double? LearnedHeadroom { get; set; }

    /// <summary>Minutes between bursts of outside activity seen (newest last).</summary>
    public List<double> Gaps { get; set; } = [];

    /// <summary>Unexplained 5-hour points of stretches when nobody was using it (newest last): the meter's noise.</summary>
    public List<double> Noise { get; set; } = [];
    public DateTime? NoiseThroughUtc { get; set; }

    public List<OutsideWindow> Windows { get; set; } = [];
    public List<OutsideWindow> Weeks { get; set; } = [];
}

/// <summary>The outside-use numbers in force for one login: the settings', or what it has learned within their bounds.</summary>
public sealed record OutsideTuning(double Ceiling, double QuietMinutes, double Margin, double Share, double Headroom)
{
    public TimeSpan Quiet => TimeSpan.FromMinutes(QuietMinutes);

    public static OutsideTuning For(OrchestratorSettings s, OutsideUseLogin? login) => new(
        Bound(login?.LearnedCeiling ?? s.OutsideUseCeilingPercent, s.OutsideUseCeilingMinPercent, s.OutsideUseCeilingMaxPercent),
        Bound(login?.LearnedQuietMinutes ?? s.OutsideUseQuietMinutes, s.OutsideUseQuietMinMinutes, s.OutsideUseQuietMaxMinutes),
        Bound(login?.LearnedMargin ?? s.OutsideUseMarginPoints, s.OutsideUseMarginMinPoints, s.OutsideUseMarginMaxPoints),
        Math.Clamp(s.OutsideUseMarginSharePercent, 0, 100) / 100.0,
        Bound(login?.LearnedHeadroom ?? s.OutsideUseHeadroomPoints, s.OutsideUseHeadroomMinPoints, s.OutsideUseHeadroomMaxPoints));

    /// <summary>"70-95" as (70, 95), each within <paramref name="lo"/>..<paramref name="hi"/> and the first no more than the
    /// second; null otherwise.</summary>
    public static (int Min, int Max)? ParseBounds(string text, int lo, int hi)
    {
        var parts = text.Split(['-', ':', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 && int.TryParse(parts[0], out var a) && int.TryParse(parts[1], out var b) && a >= lo && b <= hi && a <= b
            ? (a, b) : null;
    }

    /// <summary><paramref name="value"/> within the bounds (a hand-edited min above its max reads as the max).</summary>
    public static double Bound(double value, double min, double max) => Math.Clamp(value, Math.Min(min, max), max);
}

/// <summary>A member's outside use, averaged, with the reserves and ceiling it suggests (the Accounts view).</summary>
/// <param name="PerWindow">Average 5-hour points of own use per window seen.</param>
/// <param name="PerWeek">Average week points per week seen; null with no week seen.</param>
/// <param name="FiveHourReserve">The suggested 5-hour reserve (100 minus the pace target): what covers four windows in five.</param>
/// <param name="WeeklyReserve">The suggested weekly reserve, the same way; null with no week seen.</param>
/// <param name="Ceiling">The outside-use ceiling learned for it (or the setting's).</param>
/// <param name="HeldTo">The 5-hour ceiling its CLIs are held to now; null while nobody's using it.</param>
public sealed record OutsideUseStats(string Key, string Name, bool InUse, int WindowCount, double PerWindow, int WeekCount, double? PerWeek,
    int FiveHourReserve, int? WeeklyReserve, double? Ceiling = null, int? HeldTo = null)
{
    /// <summary>"Own use outside the orchestrator: 3% a window, 6% a week on average (12 windows, 2 weeks) · suggested: 5-hour
    /// reserve 4% (pace target 96%), weekly reserve 8%, outside-use ceiling 88%".</summary>
    public string Text
    {
        get
        {
            var now = InUse ? $"{Name} is using Claude now: this login's CLIs are held to {HeldTo?.ToString(CultureInfo.InvariantCulture) ?? "a lower"}% of this window. " : "";
            if (WindowCount == 0) return now + "No outside use measured yet.";
            var week = PerWeek is { } w ? $", {Pct(w)}% a week" : "";
            var counts = $"{WindowCount} window{(WindowCount == 1 ? "" : "s")}" + (WeekCount > 0 ? $", {WeekCount} week{(WeekCount == 1 ? "" : "s")}" : "");
            var suggested = $"5-hour reserve {FiveHourReserve}% (pace target {100 - Math.Clamp(FiveHourReserve, 0, 99)}%)" +
                            (WeeklyReserve is { } r ? $", weekly reserve {r}%" : "") +
                            (Ceiling is { } c ? $", outside-use ceiling {c.ToString("0", CultureInfo.InvariantCulture)}%" : "");
            return $"{now}Own use outside the orchestrator: {Pct(PerWindow)}% a window{week} on average ({counts}) · suggested: {suggested}";
        }
    }

    private static string Pct(double v) => v.ToString(v < 10 ? "0.#" : "0", CultureInfo.InvariantCulture);
}

/// <summary>What <see cref="OutsideUseWatch.Observe"/> saw change for a login.</summary>
public enum OutsideChange { None, Started, Lowered, Ended }

/// <summary>
/// Keeps each login's outside use (M.ORCH-T682): starts an episode when a stretch is outside use (<see cref="OutsideUse.Judge"/>),
/// holds the login's CLIs to a lower 5-hour ceiling (and weekly stop) while it lasts, lower still when the member's measured rate
/// would run the window out above it, ends it once the quiet period passes with no new activity, and counts the points nobody
/// here spent into the window's and the week's record. Each login learns its own ceiling, quiet period, noise margin and
/// headroom within the settings' bounds, every change logged with its reason. Kept in the store, so a daemon restart keeps an
/// episode, the pacer (in every process) applies the ceilings, and the Accounts view and Settings read the numbers.
/// </summary>
public sealed class OutsideUseWatch(JsonStore? store = null)
{
    public const string StoreName = "outside-use";

    /// <summary>How long a window's or week's record is kept.</summary>
    public static readonly TimeSpan Keep = TimeSpan.FromDays(56);

    /// <summary>Gaps between bursts closer than this are one burst (a reading every few minutes); longer than the quiet
    /// period's upper bound, separate sessions, which no quiet period is meant to span.</summary>
    public static readonly TimeSpan MinGap = TimeSpan.FromMinutes(6);

    /// <summary>The share of the room the ceiling left a member that, once used, lowers that login's ceiling next time; under
    /// <see cref="RaiseUnder"/> of it, the ceiling goes back up toward the bound.</summary>
    public const double LowerAt = 0.8, RaiseUnder = 0.4;

    /// <summary>The noise readings needed (three hours of them) before the margin is tuned down from them.</summary>
    public const int NoiseNeeded = 36;

    private List<OutsideUseLogin>? book;

    public IReadOnlyList<OutsideUseLogin> Logins => Book();

    private List<OutsideUseLogin> Book() => book ??= store is { } s ? [.. Load(s)] : [];

    /// <summary>What <paramref name="store"/> keeps; none when it can't be read.</summary>
    public static IReadOnlyList<OutsideUseLogin> Load(JsonStore store)
    {
        try { return store.Load<List<OutsideUseLogin>>(StoreName); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { return []; }
    }

    private void Save()
    {
        if (store is null) return;
        try { store.Save(StoreName, Book()); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { }
    }

    public OutsideUseLogin? Find(string? key) => Find(Book(), key);

    public static OutsideUseLogin? Find(IEnumerable<OutsideUseLogin> logins, string? key) =>
        logins.FirstOrDefault(l => string.Equals(l.Key, key ?? "", StringComparison.OrdinalIgnoreCase));

    private OutsideUseLogin Of(string key, string name)
    {
        var login = Find(key);
        if (login is null) Book().Add(login = new OutsideUseLogin { Key = key, Name = name });
        login.Name = name;
        return login;
    }

    /// <summary>Whether someone's using Claude on <paramref name="key"/>'s login now.</summary>
    public bool InUse(string? key) => Find(key)?.HeldSinceUtc is not null;

    /// <summary>
    /// <paramref name="settings"/> for one login's pace: while its member uses Claude elsewhere, its pace target lowered to the
    /// ceiling in force and its weekly stop capped (M.ORCH-T682), with why for its pace note; else as they are.
    /// </summary>
    public static (OrchestratorSettings Settings, string? Why) Apply(IEnumerable<OutsideUseLogin>? logins, string? key, OrchestratorSettings settings)
    {
        if (logins is null || Find(logins, key) is not { HeldSinceUtc: not null, CeilingPercent: { } ceiling } l) return (settings, null);
        var target = Math.Min(settings.PaceTargetPercent, ceiling);
        var week = l.WeeklyStopPercent is { } w && w < settings.WeeklyStopPercent ? w : (int?)null;
        var why = $"Held to {target}% of this window{(week is { } ws ? $" and {ws}% of the week" : "")}: {l.Name} is using Claude outside the orchestrator.";
        return (settings with { PaceTargetPercent = target, WeeklyStopCapPercent = week ?? settings.WeeklyStopCapPercent }, why);
    }

    /// <summary>
    /// Judges <paramref name="key"/>'s login by its readings, records the window (and week) it's in, sets the ceilings while
    /// someone's using it, learns, and says whether outside use started, pushed the ceiling lower, or ended.
    /// <paramref name="stretch"/> is the judged stretch (null: too little to tell), and <paramref name="between"/> judges any other
    /// stretch of the same readings. Every learned change is added to <paramref name="log"/> with its reason.
    /// </summary>
    public OutsideChange Observe(string key, string name, IReadOnlyList<UsageSample> samples, OutsideStretch? stretch,
        Func<DateTime, DateTime, OutsideStretch?> between, OrchestratorSettings settings, DateTime nowUtc, List<string>? log = null)
    {
        var login = Of(key, name);
        var tuning = OutsideTuning.For(settings, login);
        var change = OutsideChange.None;
        var running = samples.Where(s => s.ResetUtc > nowUtc && s.AtUtc <= nowUtc).OrderBy(s => s.AtUtc).ToList();
        var last = running.Count > 0 ? running[^1] : null;
        // Every window (and week) seen is one to average over, use in it or none.
        if (last is not null)
        {
            Window(login.Windows, last.ResetUtc);
            if (last.WeekResetUtc is { } wr) Window(login.Weeks, wr);
        }

        if (stretch is { IsOutside: false } && login.HeldSinceUtc is null && stretch.ToUtc != login.NoiseThroughUtc)
            Noise(login, stretch, settings, tuning, log);

        if (stretch is { IsOutside: true })
        {
            // New activity: the newest reading itself rose past what the CLIs explain (an old burst still in the stretch isn't).
            var prev = running.Count > 1 ? running[^2] : null;
            var fresh = login.HeldSinceUtc is null || prev is not null && last is not null &&
                        between(prev.AtUtc, last.AtUtc) is { } pair && pair.FivePoints >= 1 && last.AtUtc != login.LastSeenUtc;
            if (fresh)
            {
                var at = login.HeldSinceUtc is null ? stretch.ToUtc : last!.AtUtc;
                if (login.LastSeenUtc is { } ls && at - ls >= MinGap && at - ls <= TimeSpan.FromMinutes(Math.Max(settings.OutsideUseQuietMaxMinutes, 1)))
                {
                    login.Gaps.Add((at - ls).TotalMinutes);
                    if (login.Gaps.Count > 50) login.Gaps.RemoveAt(0);
                }
                login.LastSeenUtc = at;
                login.EpisodeSeen++;
            }
            if (login.HeldSinceUtc is null)
            {
                login.HeldSinceUtc = nowUtc;
                login.EpisodeFromUtc = stretch.FromUtc;
                login.EpisodeSeen = 1;
                login.EpisodePoints = Math.Max(0, stretch.FivePoints);
                login.EpisodeWeekPoints = Math.Max(0, stretch.WeekPoints);
                Count(login, stretch, samples);
                login.CountedThroughUtc = stretch.ToUtc;
                change = OutsideChange.Started;
            }
        }
        if (login.HeldSinceUtc is not null && login.CountedThroughUtc is { } through && last is not null && last.AtUtc > through &&
            between(through, last.AtUtc) is { } more)
        {
            login.EpisodePoints = Math.Max(0, login.EpisodePoints + more.FivePoints);
            login.EpisodeWeekPoints = Math.Max(0, login.EpisodeWeekPoints + more.WeekPoints);
            Count(login, more, samples);
            login.CountedThroughUtc = more.ToUtc;
        }

        if (login.HeldSinceUtc is not null && login.LastSeenUtc is { } seen && nowUtc - seen >= tuning.Quiet)
        {
            Learn(login, settings, tuning, nowUtc, log);
            End(login);
            change = OutsideChange.Ended;
        }
        else if (login.HeldSinceUtc is not null)
        {
            var was = login.CeilingPercent;
            Ceilings(login, last, settings, tuning, nowUtc);
            if (change == OutsideChange.Started)
            {
                login.CeilingAtStart = login.CeilingPercent ?? tuning.Ceiling;
                login.RateAtStart = Rate(login, nowUtc);
            }
            else if (was is { } w && login.CeilingPercent < w) change = OutsideChange.Lowered;
        }
        login.Windows.RemoveAll(w => nowUtc - w.ResetUtc > Keep);
        login.Weeks.RemoveAll(w => nowUtc - w.ResetUtc > Keep);
        Save();
        return change;
    }

    private static double Rate(OutsideUseLogin login, DateTime nowUtc) =>
        login.EpisodePoints / Math.Max(10, (nowUtc - (login.EpisodeFromUtc ?? login.HeldSinceUtc ?? nowUtc)).TotalMinutes);

    /// <summary>The ceilings while someone's using the login: the tuned ceiling, lower when their rate (points nobody here spent
    /// per minute this episode) carried to the reset, plus the headroom, wouldn't fit above it; never below what's already spent
    /// nor above the pace target. The week the same way against its stop.</summary>
    private static void Ceilings(OutsideUseLogin login, UsageSample? last, OrchestratorSettings settings, OutsideTuning tuning, DateTime nowUtc)
    {
        var left = last is null ? 0 : Math.Max(0, (last.ResetUtc - nowUtc).TotalMinutes);
        var projected = Rate(login, nowUtc) * left;
        var ceiling = Math.Min(tuning.Ceiling, 100 - projected - tuning.Headroom);
        if (last is not null) ceiling = Math.Max(ceiling, Math.Ceiling(last.Percent));
        login.CeilingPercent = (int)Math.Floor(Math.Min(ceiling, settings.PaceTargetPercent) + 1e-9);

        if (last?.WeekPercent is { } week)
        {
            var weekRate = login.EpisodeWeekPoints / Math.Max(10, (nowUtc - (login.EpisodeFromUtc ?? nowUtc)).TotalMinutes);
            var stop = settings.WeeklyStopPercent;
            var capped = Math.Max(Math.Ceiling(week), stop - weekRate * left - tuning.Headroom);
            login.WeeklyStopPercent = (int)Math.Floor(Math.Min(capped, stop) + 1e-9);
        }
        else login.WeeklyStopPercent = null;
    }

    private static void End(OutsideUseLogin login)
    {
        login.HeldSinceUtc = null;
        login.CountedThroughUtc = null;
        login.EpisodeFromUtc = null;
        login.CeilingPercent = null;
        login.WeeklyStopPercent = null;
        login.EpisodePoints = login.EpisodeWeekPoints = 0;
        login.EpisodeSeen = 0;
    }

    /// <summary>What an ended episode teaches, each within the settings' bounds:
    /// the ceiling: the member used most of the room it left them, so next time it's lower; little of it, back up;
    /// the headroom: they used more than the rate they started at foretold by more than it, so it grows; far less, it shrinks;
    /// the margin: a lone reading barely past it and never again looks like noise, so it grows;
    /// the quiet period: from the gaps seen between their bursts (four in five covered, plus five minutes).</summary>
    private static void Learn(OutsideUseLogin login, OrchestratorSettings s, OutsideTuning tuning, DateTime nowUtc, List<string>? log)
    {
        var room = Math.Max(1, 100 - login.CeilingAtStart);
        var used = login.EpisodePoints;
        if (used >= LowerAt * room)
            Set(login, l => l.LearnedCeiling, (l, v) => l.LearnedCeiling = v, tuning.Ceiling,
                OutsideTuning.Bound(tuning.Ceiling - 2, s.OutsideUseCeilingMinPercent, s.OutsideUseCeilingMaxPercent), "outside-use ceiling",
                $"{login.Name} used {P(used)} of the {P(room)} points of room it left", log);
        else if (used < RaiseUnder * room)
            Set(login, l => l.LearnedCeiling, (l, v) => l.LearnedCeiling = v, tuning.Ceiling,
                OutsideTuning.Bound(tuning.Ceiling + 1, s.OutsideUseCeilingMinPercent, s.OutsideUseCeilingMaxPercent), "outside-use ceiling",
                $"{login.Name} used only {P(used)} of the {P(room)} points of room it left", log);

        var minutes = Math.Max(10, ((login.LastSeenUtc ?? nowUtc) - (login.EpisodeFromUtc ?? nowUtc)).TotalMinutes);
        var foretold = login.RateAtStart * minutes;
        if (used - foretold > tuning.Headroom)
            Set(login, l => l.LearnedHeadroom, (l, v) => l.LearnedHeadroom = v, tuning.Headroom,
                OutsideTuning.Bound(tuning.Headroom + 1, s.OutsideUseHeadroomMinPoints, s.OutsideUseHeadroomMaxPoints), "outside-use headroom",
                $"{login.Name} used {P(used)} points where the starting rate foretold {P(foretold)}", log);
        else if (foretold - used > 2 * tuning.Headroom)
            Set(login, l => l.LearnedHeadroom, (l, v) => l.LearnedHeadroom = v, tuning.Headroom,
                OutsideTuning.Bound(tuning.Headroom - 0.5, s.OutsideUseHeadroomMinPoints, s.OutsideUseHeadroomMaxPoints), "outside-use headroom",
                $"{login.Name} used {P(used)} points where the starting rate foretold {P(foretold)}", log);

        if (login.EpisodeSeen <= 1 && used < tuning.Margin + 1)
            Set(login, l => l.LearnedMargin, (l, v) => l.LearnedMargin = v, tuning.Margin,
                OutsideTuning.Bound(tuning.Margin + 0.5, s.OutsideUseMarginMinPoints, s.OutsideUseMarginMaxPoints), "noise margin",
                $"a lone rise of {P(used)} points on {login.Name}'s login was never seen again: likely the meter's noise", log);

        if (login.Gaps.Count >= 3)
        {
            var p80 = Percentile(login.Gaps, 0.8);
            Set(login, l => l.LearnedQuietMinutes, (l, v) => l.LearnedQuietMinutes = v, tuning.QuietMinutes,
                OutsideTuning.Bound(Math.Round(p80 + 5), s.OutsideUseQuietMinMinutes, s.OutsideUseQuietMaxMinutes), "quiet period",
                $"{login.Name}'s gaps between bursts run to {P(p80)} minutes (four in five)", log, unit: " min");
        }
    }

    /// <summary>Records a stretch's unexplained points while nobody's using the login, and tunes the margin down from them once
    /// there are <see cref="NoiseNeeded"/>: when nearly all (19 in 20) sit a point under it, it's wider than the meter needs.</summary>
    private static void Noise(OutsideUseLogin login, OutsideStretch stretch, OrchestratorSettings s, OutsideTuning tuning, List<string>? log)
    {
        login.NoiseThroughUtc = stretch.ToUtc;
        login.Noise.Add(Math.Max(0, stretch.FivePoints));
        if (login.Noise.Count > 288) login.Noise.RemoveAt(0);
        if (login.Noise.Count < NoiseNeeded || login.Noise.Count % 12 != 0) return;
        var p95 = Percentile(login.Noise, 0.95);
        if (p95 + 1 < tuning.Margin)
            Set(login, l => l.LearnedMargin, (l, v) => l.LearnedMargin = v, tuning.Margin,
                OutsideTuning.Bound(tuning.Margin - 0.25, s.OutsideUseMarginMinPoints, s.OutsideUseMarginMaxPoints), "noise margin",
                $"with nobody using {login.Name}'s login, 19 in 20 stretches rose at most {P(p95)} points nobody here spent", log);
    }

    private static void Set(OutsideUseLogin login, Func<OutsideUseLogin, double?> get, Action<OutsideUseLogin, double> set, double was, double now,
        string what, string reason, List<string>? log, string unit = "")
    {
        if (Math.Abs(now - was) < 1e-9) return;
        set(login, now);
        log?.Add($"outside use: {login.Name}'s {what} {(now < was ? "lowered" : "raised")} from {P(was)}{unit} to {P(now)}{unit}: {reason}.");
    }

    private static string P(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    private static double Percentile(List<double> values, double q)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted[Math.Max(0, (int)Math.Ceiling(q * sorted.Count) - 1)];
    }

    /// <summary>Ends the episodes of logins not judged now (signed out, or sharing an account with another login) once their quiet
    /// period has passed since their use was last seen; returns them.</summary>
    public IReadOnlyList<OutsideUseLogin> Expire(IReadOnlyCollection<string> judged, OrchestratorSettings settings, DateTime nowUtc)
    {
        var ended = Book().Where(l => l.HeldSinceUtc is not null && !judged.Contains(l.Key, StringComparer.OrdinalIgnoreCase) &&
                                      (l.LastSeenUtc is not { } seen || nowUtc - seen >= OutsideTuning.For(settings, l).Quiet)).ToList();
        foreach (var l in ended) End(l);
        if (ended.Count > 0) Save();
        return ended;
    }

    private static OutsideWindow Window(List<OutsideWindow> list, DateTime resetUtc)
    {
        var w = list.FirstOrDefault(x => x.ResetUtc == resetUtc);
        if (w is null) list.Add(w = new OutsideWindow { ResetUtc = resetUtc });
        return w;
    }

    /// <summary>Adds a stretch's unexplained points to its window and week (never below none: noise at its ends can't take back
    /// what was counted).</summary>
    private static void Count(OutsideUseLogin login, OutsideStretch stretch, IReadOnlyList<UsageSample> samples)
    {
        var at = samples.LastOrDefault(s => s.AtUtc == stretch.ToUtc) ?? samples.LastOrDefault(s => s.AtUtc <= stretch.ToUtc);
        if (at is null) return;
        Window(login.Windows, at.ResetUtc).Points += Math.Max(0, stretch.FivePoints);
        if (at.WeekResetUtc is { } wr) Window(login.Weeks, wr).Points += Math.Max(0, stretch.WeekPoints);
    }

    /// <summary>Each login's averages and the reserves they suggest (four windows, or weeks, in five covered, rounded up), with
    /// its learned outside-use ceiling under <paramref name="settings"/>' bounds.</summary>
    public static IReadOnlyList<OutsideUseStats> Stats(IEnumerable<OutsideUseLogin> logins, OrchestratorSettings? settings = null) =>
    [
        .. logins.Select(l => new OutsideUseStats(l.Key, l.Name, l.HeldSinceUtc is not null,
            l.Windows.Count, l.Windows.Count == 0 ? 0 : l.Windows.Average(w => w.Points),
            l.Weeks.Count, l.Weeks.Count == 0 ? null : l.Weeks.Average(w => w.Points),
            Reserve(l.Windows) ?? 0, Reserve(l.Weeks),
            OutsideTuning.For(settings ?? new OrchestratorSettings(), l).Ceiling, l.CeilingPercent)),
    ];

    /// <summary>"Honey 88% · Ryan 90%": each login's learned value of one number, for the Settings modal; "" with none learned.</summary>
    public static string Learned(IEnumerable<OutsideUseLogin> logins, Func<OutsideUseLogin, double?> value, string unit) =>
        string.Join(" · ", logins.Where(l => value(l) is not null).Select(l => $"{l.Name} {P(value(l)!.Value)}{unit}"));

    private static int? Reserve(List<OutsideWindow> list)
    {
        if (list.Count == 0) return null;
        return (int)Math.Ceiling(Percentile([.. list.Select(w => w.Points)], 0.8) - 1e-9);
    }
}
