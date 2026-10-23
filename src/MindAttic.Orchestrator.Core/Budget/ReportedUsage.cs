using System.Globalization;
using System.Text.Json;

namespace MindAttic.Orchestrator.Budget;

/// <summary>One limit window as Claude Code reports it: percent used and when it resets.</summary>
public sealed record ReportedWindow(double Percent, DateTime ResetUtc);

/// <summary>One 5-hour report, with the local uncached tokens counted in its window by then.</summary>
public sealed record UsageSample(DateTime AtUtc, double Percent, DateTime ResetUtc, long WindowFresh)
{
    /// <summary>The week's percent in the same report (M.ORCH-T666), to learn how much of the week a 5-hour point costs
    /// (<see cref="WeeklyRatio"/>); null when it didn't carry one, or for a sample kept before it was recorded.</summary>
    public double? WeekPercent { get; init; }

    /// <summary>When that week resets; null with <see cref="WeekPercent"/>.</summary>
    public DateTime? WeekResetUtc { get; init; }
}

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
    /// already recorded for its window is stale (see <see cref="Highest"/>) and isn't recorded; only samples <paramref name="mine"/>
    /// says are the current account's count for that, since another account's window may reset at the same time. With
    /// <paramref name="withWeek"/> the week's percent is kept beside it (<see cref="UsageSample.WeekPercent"/>).</summary>
    public static bool Record(List<UsageSample> samples, ReportedUsage? reported, IReadOnlyList<UsageEvent> events, DateTime nowUtc,
        Func<DateTime, bool>? mine = null, bool withWeek = false)
    {
        if (reported?.FiveHour is not { } w || samples.Any(s => s.AtUtc == reported.AtUtc)) return false;
        if (samples.Any(s => s.ResetUtc == w.ResetUtc && s.Percent > w.Percent && (mine is null || mine(s.AtUtc)))) return false;
        // An idle tab keeps stamping its last response's window after that window reset: counted to the report's time, the
        // next window's tokens would land in it and inflate the limit sized from it.
        if (w.ResetUtc <= reported.AtUtc) return false;
        var start = w.ResetUtc - BudgetCalculator.BlockLength;
        var fresh = events.Where(e => e.AtUtc >= start && e.AtUtc <= reported.AtUtc).Sum(e => e.Usage.Fresh);
        // A per-CLI login's samples keep the week's percent too (M.ORCH-T666); the global login's table has no column for it.
        var week = withWeek ? ReportedUsage.Usable(reported.Week, reported.AtUtc) : null;
        samples.Add(new UsageSample(reported.AtUtc, w.Percent, w.ResetUtc, fresh) { WeekPercent = week?.Percent, WeekResetUtc = week?.ResetUtc });
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

    /// <summary>The account the meter probe read this for (<see cref="ClaudeMeterProbe"/> tags its writes); null from a statusline.</summary>
    public string? Account { get; init; }

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

    /// <summary>The <c>state</c> row of the orchestrator's database the meter probe keeps its readings in (M.ORCH-T528), left
    /// out of the dump: <see cref="DefaultPath"/> is the statusline's, which the orchestrator reads and never replaces.</summary>
    public const string StateName = "claude-usage";

    /// <summary>The database the probe's readings are kept in.</summary>
    public static Settings.OrchestratorDb DefaultDb => new(Settings.OrchestratorDb.StatePath(Settings.JsonStore.LiveDirectory));

    /// <summary>The latest report, or null when there's none, it's unreadable, or it's older than <see cref="MaxAge"/>: the
    /// later of the statusline's file and the meter probe's row in <paramref name="db"/> (the default database for the real
    /// file; none for another file unless given).</summary>
    public static ReportedUsage? Load(DateTime nowUtc, string? path = null, Settings.OrchestratorDb? db = null)
    {
        db ??= path is null ? DefaultDb : null;
        var file = LoadFile(nowUtc, path ?? DefaultPath);
        var row = LoadRow(nowUtc, db);
        return row is not null && (file is null || row.AtUtc > file.AtUtc) ? row : file;
    }

    /// <summary>Just the meter probe's row in <paramref name="db"/> (null when none, unreadable or too old), never the statusline's file.</summary>
    internal static ReportedUsage? LoadRow(DateTime nowUtc, Settings.OrchestratorDb? db)
    {
        try
        {
            if (db is not null && File.Exists(db.Path) && db.LoadState(StateName) is { } state)
            {
                using var doc = JsonDocument.Parse(state.Value);
                return Parse(doc.RootElement, nowUtc);
            }
        }
        catch (Exception ex) when (ex is System.Data.Common.DbException or IOException or JsonException or InvalidOperationException or FormatException) { }
        return null;
    }

    /// <summary>The reading in the statusline's file at <paramref name="path"/> whatever its age (the usage report shows how old
    /// it is, M.ORCH-T685); null when there's none or it can't be read.</summary>
    public static ReportedUsage? LoadAnyAge(string path) => LoadFile(DateTime.UtcNow, path, anyAge: true);

    private static ReportedUsage? LoadFile(DateTime nowUtc, string file, bool anyAge = false)
    {
        try
        {
            if (!File.Exists(file)) return null;
            // Shared with delete: a statusline renaming its fresh report over this file mid-read must not fail (it left
            // its .tmp behind and the reading was lost).
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var doc = JsonDocument.Parse(stream);
            return Parse(doc.RootElement, nowUtc, anyAge);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private static ReportedUsage? Parse(JsonElement root, DateTime nowUtc, bool anyAge = false)
    {
        if (!root.TryGetProperty("at", out var a) ||
            !DateTime.TryParse(a.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var at) ||
            (!anyAge && nowUtc - at > MaxAge) || !root.TryGetProperty("rate_limits", out var limits))
            return null;
        return new ReportedUsage(Window(limits, "five_hour"), Window(limits, "seven_day"), at)
        {
            Account = root.TryGetProperty("account", out var who) && who.ValueKind == JsonValueKind.String ? who.GetString() : null,
        };
    }

    private static ReportedWindow? Window(JsonElement limits, string name) =>
        limits.TryGetProperty(name, out var w) && w.ValueKind == JsonValueKind.Object &&
        w.TryGetProperty("used_percentage", out var p) && p.ValueKind == JsonValueKind.Number &&
        w.TryGetProperty("resets_at", out var r) && r.ValueKind == JsonValueKind.Number
            ? new ReportedWindow(p.GetDouble(), DateTimeOffset.FromUnixTimeSeconds(r.GetInt64()).UtcDateTime)
            : null;
}
