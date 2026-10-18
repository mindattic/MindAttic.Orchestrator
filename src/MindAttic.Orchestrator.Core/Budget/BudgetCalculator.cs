using System.Globalization;
using System.Text.Json;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Budget;

public enum BudgetLevel { Ok, Warn, Over }

/// <summary>Where a limit came from: you set it, it was learned from your limit hits, or (5-hour
/// block only) there is none and the block is compared to your busiest one as context.</summary>
public enum LimitSource { Peak, Learned, Manual, Reported }

/// <summary>
/// A 5-hour usage block, the unit Claude's subscription limits reset on: it starts at the first
/// response after the previous block ended, rounded down to 10 minutes, and lasts five hours.
/// </summary>
public sealed record UsageBlock(DateTime StartUtc, DateTime EndUtc, TokenUsage Usage, DateTime LastActivityUtc)
{
    public bool IsActive(DateTime nowUtc) => nowUtc < EndUtc;
}

public sealed record BudgetStatus(
    UsageBlock? CurrentBlock,
    long BlockLimit,
    bool BlockLimitIsPeak,
    double BlockPercent,
    TokenUsage Today,
    TokenUsage Week,
    long? WeeklyLimit,
    double? WeeklyPercent,
    double FreshPerHour,
    DateTime? ProjectedLimitUtc,
    int WarnPercent)
{
    /// <summary>The highest percentage of a real limit. Peak-relative block numbers are context, not a
    /// limit, so they never raise the level on their own.</summary>
    public double WorstPercent => AtLimitUntilUtc is not null ? Math.Max(100, RawWorstPercent) : RawWorstPercent;

    private double RawWorstPercent => Math.Max(BlockLimitIsPeak ? 0 : BlockPercent, WeeklyPercent ?? 0);

    public BudgetLevel Level =>
        WorstPercent >= 100 ? BudgetLevel.Over
        : WorstPercent >= WarnPercent ? BudgetLevel.Warn
        : BudgetLevel.Ok;

    public LimitSource BlockLimitSource { get; init; } = LimitSource.Peak;
    public LimitSource? WeeklyLimitSource { get; init; }

    /// <summary>Uncached tokens per hour over the last <see cref="BudgetCalculator.RecentWindow"/>:
    /// the burn rate to steer by, since the whole-block average lags.</summary>
    public double RecentFreshPerHour { get; init; }

    /// <summary>Uncached tokens per hour over the last <see cref="BudgetCalculator.ControlWindow"/>, every
    /// session and subagent together: what holding running agents steers by, so a hold follows the draw
    /// within minutes instead of lingering on a burst that already ended.</summary>
    public double ControlFreshPerHour { get; init; }

    /// <summary>Set while you're behind a limit Claude Code actually reported hitting.</summary>
    public DateTime? AtLimitUntilUtc { get; init; }
    public LimitKind? AtLimitKind { get; init; }

    public LimitCalibration? Calibration { get; init; }

    /// <summary>This window already spent more than the learned limit without hitting a wall, so the
    /// learned value is stale and isn't used until the next hit relearns it.</summary>
    public bool BlockLimitOutdated { get; init; }
    public bool WeeklyLimitOutdated { get; init; }

    public long BlockRemaining => Math.Max(0, BlockLimit - (CurrentBlock?.Usage.Fresh ?? 0));

    /// <summary>Where <see cref="Week"/> starts counting: the last weekly reset, or 7 days ago.</summary>
    public DateTime WeekStartUtc { get; init; }

    /// <summary>The next weekly reset; null when the week is a rolling 7 days.</summary>
    public DateTime? WeekResetUtc { get; init; }

    public bool WeekIsRolling => WeekResetUtc is null;
}

/// <summary>
/// Measures usage against budgets. The budget metric is uncached tokens (input + cache write +
/// output): cache reads are cheap and dominate raw totals, so counting them would make every
/// percentage meaningless. Blocks follow the convention community tools such as ccusage use, since
/// Anthropic doesn't publish the limit or the window to clients.
/// </summary>
public static class BudgetCalculator
{
    public static readonly TimeSpan BlockLength = TimeSpan.FromHours(5);
    public static readonly TimeSpan RecentWindow = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan ControlWindow = TimeSpan.FromMinutes(10);

    // Claude Code's own reset times land on 10-minute marks just before the window's first message
    // (checked against 20 recorded session-limit hits); flooring to the hour put resets up to 50 minutes early.
    private static readonly long BlockGrain = TimeSpan.FromMinutes(10).Ticks;

    public static IReadOnlyList<UsageBlock> Blocks(IEnumerable<UsageEvent> events)
    {
        var blocks = new List<UsageBlock>();
        DateTime start = default, end = default, last = default;
        TokenUsage sum = default;
        var open = false;
        foreach (var e in events.OrderBy(e => e.AtUtc))
        {
            if (!open || e.AtUtc >= end)
            {
                if (open) blocks.Add(new UsageBlock(start, end, sum, last));
                start = new DateTime(e.AtUtc.Ticks - e.AtUtc.Ticks % BlockGrain, DateTimeKind.Utc);
                end = start + BlockLength;
                sum = default;
                open = true;
            }
            sum += e.Usage;
            last = e.AtUtc;
        }
        if (open) blocks.Add(new UsageBlock(start, end, sum, last));
        return blocks;
    }

    /// <summary>
    /// Limits come from settings first, then from <paramref name="learned"/> limit hits; with
    /// neither, the 5-hour block is compared to your busiest block as context only.
    /// </summary>
    public static BudgetStatus Compute(IReadOnlyList<UsageEvent> events, OrchestratorSettings settings, DateTime nowUtc, DateTime startOfTodayUtc,
        TimeZoneInfo? zone = null, LimitCalibration? learned = null, ReportedUsage? reported = null, long? reportedLimit = null)
    {
        var blocks = Blocks(events);
        var current = blocks.Count > 0 && blocks[^1].IsActive(nowUtc) ? blocks[^1] : null;

        // Behind a reported wall, or with Claude's own reset time, the block's real end is known; trust it over the estimate.
        var sessionWall = learned?.Active(LimitKind.Session, nowUtc);
        var reported5h = ReportedUsage.Usable(reported?.FiveHour, nowUtc);
        if ((sessionWall?.ResetUtc ?? reported5h?.ResetUtc) is { } blockEnd)
        {
            var start = blockEnd - BlockLength;
            var inBlock = events.Where(e => e.AtUtc >= start && e.AtUtc <= nowUtc).ToList();
            current = new UsageBlock(start, blockEnd, Sum(inBlock),
                inBlock.Count > 0 ? inBlock.Max(e => e.AtUtc) : sessionWall?.HitUtc ?? start);
        }
        var currentFresh = current?.Usage.Fresh ?? 0;

        // Limits move. Spending past a learned limit without hitting a wall proves it's out of date,
        // so drop it until the next hit relearns it rather than report a wall that isn't there.
        var learned5h = learned?.Learned5h;
        var blockRefuted = learned5h is { } l5 && sessionWall is null && currentFresh > l5;
        if (blockRefuted) learned5h = null;

        // With Claude's own percent, the limit's size comes from how the percent moved between reports
        // (reportedLimit), else the learned one (unless this window disproved it), else (5%+) this window's tokens over its percent.
        var reportedLimit5h = reported5h is null ? null
            : reportedLimit ?? learned5h ?? (reported5h.Percent >= ReportedUsage.MinPercent ? ImpliedLimit(events, reported5h, BlockLength, reported?.AtUtc) : null);

        // Without a budget, compare against the heaviest earlier block: "how hard am I going vs usual".
        // It can pass 100%, and it isn't a limit, so it never warns or projects.
        var peak = blocks.Where(b => b.EndUtc <= (current?.StartUtc ?? nowUtc)).Select(b => b.Usage.Fresh).DefaultIfEmpty(0).Max();

        var (blockSource, blockLimit) = settings.BlockBudget is { } manual ? (LimitSource.Manual, manual)
            : reportedLimit5h is { } rep5 ? (LimitSource.Reported, rep5)
            : learned5h is { } lim5 ? (LimitSource.Learned, lim5)
            : (LimitSource.Peak, peak > 0 ? peak : currentFresh);
        var isPeak = blockSource == LimitSource.Peak;
        var limit = blockLimit;

        // Claude's percent is what's used, whoever spent it (another account, claude.ai, another machine);
        // the local count only adds what's been spent since its last report. Taken a point high, like the size.
        if (blockSource == LimitSource.Reported && reported5h is not null && reported is not null && current is not null)
        {
            var since = events.Where(e => e.AtUtc > reported.AtUtc && e.AtUtc <= nowUtc).Sum(e => e.Usage.Fresh);
            currentFresh = (long)(Math.Min(100, reported5h.Percent + 1) / 100 * limit) + since;
            current = current with { Usage = new TokenUsage(currentFresh, 0, 0, 0) };
        }
        // Whatever sized the limit (your budget, a learned one), Claude's own percent is what's already used in its
        // window: the local count never shows less (it read 26% while Claude said 36%, M.ORCH-T179).
        else if (!isPeak && limit > 0 && reported5h is not null && current is not null &&
                 (long)(reported5h.Percent / 100 * limit) is var floor && floor > currentFresh)
        {
            currentFresh = floor;
            current = current with { Usage = new TokenUsage(currentFresh, 0, 0, 0) };
        }
        var blockPercent = limit > 0 ? 100.0 * currentFresh / limit : 0;

        // The week Claude itself reports (its seven_day reset) comes first; failing that, the configured reset
        // day/time, then the one your weekly hits taught; otherwise it rolls. An unparseable setting is skipped
        // rather than failing the whole refresh.
        DateTime weekStart;
        DateTime? weekReset = null;
        var resetSetting = WeeklyReset.TryParse(settings.WeeklyReset, out var reset) ? reset : null;
        var reportedWeek = ReportedUsage.Usable(reported?.Week, nowUtc);
        if (reportedWeek is not null)
            (weekStart, weekReset) = (reportedWeek.ResetUtc - TimeSpan.FromDays(7), reportedWeek.ResetUtc);
        else if ((resetSetting ?? learned?.LearnedWeeklyReset(zone)) is { } r)
            (weekStart, weekReset) = r.Window(nowUtc, zone);
        else
            weekStart = nowUtc - TimeSpan.FromDays(7);
        var week = Sum(events.Where(e => e.AtUtc >= weekStart));
        var weeklyWall = learned?.Active(LimitKind.Weekly, nowUtc);
        var learnedWeekly = learned?.LearnedWeekly;
        var weeklyRefuted = learnedWeekly is { } lw && weeklyWall is null && week.Fresh > lw;
        if (weeklyRefuted) learnedWeekly = null;
        var (weeklySource, weeklyLimit) = settings.WeeklyBudget is > 0 ? (LimitSource.Manual, settings.WeeklyBudget)
            : reportedWeek is { Percent: >= ReportedUsage.MinPercent } && ImpliedLimit(events, reportedWeek, TimeSpan.FromDays(7), reported?.WeekAt) is { } repW ? (LimitSource.Reported, repW)
            : learnedWeekly is { } limW ? (LimitSource.Learned, limW)
            : ((LimitSource?)null, (long?)null);
        double? weeklyPercent = reportedWeek is not null && settings.WeeklyBudget is not > 0 ? reportedWeek.Percent
            : weeklyLimit is > 0 ? 100.0 * week.Fresh / weeklyLimit.Value : null;

        var wall = weeklyWall ?? sessionWall;
        var recentFrom = nowUtc - RecentWindow;
        var recent = events.Where(e => e.AtUtc >= recentFrom && e.AtUtc <= nowUtc).Sum(e => e.Usage.Fresh);

        // Burn rate over the current block (at least 10 minutes, so one burst isn't extrapolated).
        double perHour = 0;
        DateTime? projected = null;
        if (current is not null)
        {
            var hours = Math.Max((nowUtc - current.StartUtc).TotalHours, 10.0 / 60);
            perHour = currentFresh / hours;
            if (!isPeak && limit > 0 && currentFresh >= limit)
            {
                projected = nowUtc;
            }
            else if (!isPeak && perHour > 0)
            {
                var at = nowUtc + TimeSpan.FromHours((limit - currentFresh) / perHour);
                if (at < current.EndUtc) projected = at;
            }
        }

        return new BudgetStatus(current, limit, isPeak, blockPercent, Sum(events.Where(e => e.AtUtc >= startOfTodayUtc)),
            week, weeklyLimit, weeklyPercent, perHour, projected, settings.WarnPercent)
        {
            WeekStartUtc = weekStart,
            WeekResetUtc = weekReset,
            BlockLimitSource = blockSource,
            WeeklyLimitSource = weeklySource,
            RecentFreshPerHour = recent / RecentWindow.TotalHours,
            ControlFreshPerHour = events.Where(e => e.AtUtc >= nowUtc - ControlWindow && e.AtUtc <= nowUtc).Sum(e => e.Usage.Fresh) / ControlWindow.TotalHours,
            AtLimitUntilUtc = wall?.ResetUtc,
            AtLimitKind = wall?.Kind,
            Calibration = learned,
            BlockLimitOutdated = blockRefuted,
            WeeklyLimitOutdated = weeklyRefuted,
        };
    }

    /// <summary>A window's limit in tokens from Claude's own percent: what the window had spent by the
    /// report, over the fraction it said that was. Claude reports whole percents, so the fraction is
    /// taken a point high: the limit errs small (by under 1% near the end), never large enough to walk
    /// into the wall. Null when there's nothing to size it by.</summary>
    internal static long? ImpliedLimit(IEnumerable<UsageEvent> events, ReportedWindow? window, TimeSpan length, DateTime? reportedAtUtc)
    {
        if (window is null || reportedAtUtc is not { } at) return null;
        var start = window.ResetUtc - length;
        var spent = events.Where(e => e.AtUtc >= start && e.AtUtc <= at).Sum(e => e.Usage.Fresh);
        return spent > 0 ? (long)(spent / ((window.Percent + 1) / 100)) : null;
    }

    private static TokenUsage Sum(IEnumerable<UsageEvent> events) =>
        events.Aggregate(default(TokenUsage), (s, e) => s + e.Usage);
}

/// <summary>
/// Keeps <c>~/.claude/rl-status.json</c> current, the file the global <c>rl-check.py</c> prompt hook
/// and the Launcher's status block already read but nothing had been writing.
/// </summary>
public sealed class RateLimitStatusWriter(string? path = null)
{
    public string Path { get; } = path ?? System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "rl-status.json");

    /// <summary>The clock the writer stamps and dedupes by; tests move it.</summary>
    internal Func<DateTime> UtcNow { get; init; } = () => DateTime.UtcNow;

    private string? lastWritten;
    private DateTime lastWrittenUtc;

    /// <summary>With a <paramref name="pace"/>, also tells running agents whether to conserve: rl-check.py
    /// prints <c>pace_note</c> into every prompt while the pace isn't open.</summary>
    public void Write(BudgetStatus status, Pace? pace = null)
    {
        // The file means "this much of a limit is used". With no budget there's no limit to report,
        // so withdraw a file this writer produced rather than leave stale numbers behind. A stop still has to reach the
        // pace gate (a weekly wall, say, with no 5-hour limit known): then the file carries the stop and no numbers.
        if (status.BlockLimitIsPeak && pace is not { Stop: true })
        {
            RemoveOwnFile();
            return;
        }

        var reset = status.AtLimitKind == LimitKind.Session && status.AtLimitUntilUtc is { } wall ? Clock(wall)
            : status.CurrentBlock is { } b ? Clock(b.EndUtc)
            : "no active block";
        var body = new Dictionary<string, object>
        {
            ["percent_used"] = Math.Round(status.AtLimitKind == LimitKind.Session ? Math.Max(100, status.BlockPercent) : status.BlockPercent, 1),
            ["reset_time"] = reset,
            ["tokens_remaining"] = status.AtLimitKind == LimitKind.Session ? 0 : status.BlockRemaining,
            ["basis"] = status.BlockLimitSource switch
            {
                LimitSource.Reported => "Claude Code's own 5h usage percent",
                LimitSource.Learned => $"uncached tokens vs the 5h limit learned from your last {status.Calibration?.Basis(LimitKind.Session)} limit hits",
                _ => "uncached tokens vs your 5h block budget",
            },
            ["source"] = "MindAttic.Orchestrator",
        };
        if (status.BlockLimitIsPeak)
        {
            // Only here for the stop: no 5-hour limit is known to count against, and nothing more may be spent.
            body["percent_used"] = 100.0;
            // The stop's own reset (a weekly wall's), not a 5-hour block's end or "no active block".
            if (pace?.ResetUtc is { } stopReset) body["reset_time"] = Clock(stopReset);
            body.Remove("tokens_remaining");
            body.Remove("basis");
        }
        if (status.WeeklyPercent is { } wp) body["weekly_percent"] = Math.Round(wp, 1);
        // pace-gate.py refuses tool calls while this is set (after holding them its longest), so nothing passes the limit;
        // written paced or not, since the stop doesn't depend on pacing.
        if (pace is { Stop: true })
        {
            body["stop"] = true;
            body["stop_note"] = $"The {pace.Governing ?? "5-hour"} window is at its target" +
                (pace.ResetUtc is { } r ? $" until {Formatting.ResetTime(r, UtcNow())}" : "") + "; no more tokens are spent before the reset, so tool calls are refused. " +
                "End your turn now with one short line saying where you stopped; your uncommitted work stays where it is.";
            // The hooks latch the stop until then, so a daemon that stalls once it's set can't lift it (M.ORCH-T196).
            if (pace.ResetUtc is { } until)
            {
                body["stop_until"] = until.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) + "Z";
                // What a prompt refused at the stop is told: the stop's own window and reset (a weekly wall's can be days
                // off), not the 5-hour block's end.
                body["reset_time"] = Formatting.ResetTime(until, UtcNow());
            }
            body["stop_window"] = pace.Governing ?? "5-hour";
        }
        if (pace is { IsPaced: true })
        {
            body["pace"] = pace.Key;
            if (pace.Advise) body["pace_note"] = pace.AgentNote;
            if (!pace.Stop && pace.WindDown) body["wind_down_note"] = pace.WindDownNote;
            // pace-gate.py holds a new subagent while this many run; context-gate.py tells each turn.
            if (pace.SubagentAllowance is { } allowed)
            {
                body["subagent_allowance"] = allowed;
                body["subagents_running"] = pace.Subagents;
                // The gates add marks started since the count: those newer than the newest one it saw, to the 100 ns of
                // the file times (st_mtime_ns), not the file's timestamp, which is cut to the second and only rewritten
                // once a minute while nothing else changes, so marks already counted were counted again (M.ORCH-T320).
                // It's part of the key, so a count that saw a new mark is always written.
                body["subagents_seen_through_ns"] = pace.SubagentsSeenThroughUtc is { } seen
                    ? (seen.ToUniversalTime().Ticks - DateTime.UnixEpoch.Ticks) * 100 : 0L;
                body["subagent_note"] = pace.SubagentNote;
            }
        }
        // pace-gate.py holds each tool call this long; the average is what the next refresh corrects the
        // measured rate by (Pacer.AgentDelay), so it's kept until it decays even once the hold is off.
        var nowUtc = UtcNow();
        var average = AgentDelay(nowUtc);
        var delay = pace?.AgentDelaySeconds ?? 0;
        if (delay > 0) body["pace_delay_seconds"] = delay;
        if (average >= 0.5) body["pace_delay_avg"] = Math.Round(average, 1);
        // While this file is stale (a stalled or dead daemon) the gate keeps holding at its last pace until this reset,
        // rather than letting every call through unheld (M.ORCH-T205).
        if ((pace?.ResetUtc ?? status.CurrentBlock?.EndUtc) is { } windowReset)
            body["window_reset"] = windowReset.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) + "Z";
        var key = JsonSerializer.Serialize(body);
        // Readers show the file's age, so refresh it at least once a minute even when nothing changed.
        if (key == lastWritten && nowUtc - lastWrittenUtc < TimeSpan.FromMinutes(1)) return;

        body["timestamp"] = nowUtc.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) + "Z";
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var temp = Path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(body, new JsonSerializerOptions { WriteIndented = true }));
            Replace(temp);
            lastWritten = key;
            lastWrittenUtc = nowUtc;
            FailingSinceUtc = null;
            LastError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not silent any more: the daemon reports it, since a file that can't be replaced goes stale (M.ORCH-T205).
            FailingSinceUtc ??= nowUtc;
            LastError = ex.Message;
        }
    }

    /// <summary>Since when every write has failed (the file couldn't be replaced); null while writes land.</summary>
    public DateTime? FailingSinceUtc { get; private set; }

    /// <summary>Why the last write failed; null once one lands.</summary>
    public string? LastError { get; private set; }

    /// <summary>How long a replace is retried while the file is held open; tests shorten it.</summary>
    internal TimeSpan ReplaceRetry { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Moves <paramref name="temp"/> over the file, retrying while it's held open. Every pace gate re-reads this file every
    /// few seconds while it holds a call, and Python opens it without delete sharing, so a replace that lands while any one
    /// of them has it open fails. Under heavy load (dozens of held calls) one try after another failed and the file went
    /// stale for minutes while the daemon kept ticking (M.ORCH-T205, 2026-10-17 04:47-04:52Z); one quick try was the whole
    /// write, and its failure was swallowed.
    /// </summary>
    private void Replace(string temp)
    {
        var giveUp = DateTime.UtcNow + ReplaceRetry;
        for (var wait = 10; ; wait = Math.Min(wait * 2, 100))
        {
            try
            {
                File.Move(temp, Path, overwrite: true);
                return;
            }
            catch (Exception ex) when ((ex is IOException or UnauthorizedAccessException) && DateTime.UtcNow < giveUp)
            {
                Thread.Sleep(wait);
            }
        }
    }

    private static string Clock(DateTime utc) => utc.ToLocalTime().ToString("h:mm tt", CultureInfo.InvariantCulture);

    /// <summary>How long the gate stops honoring a file nobody refreshes: the orchestrator isn't running, so nothing paces.</summary>
    public static readonly TimeSpan GateStale = TimeSpan.FromMinutes(3);

    /// <summary>
    /// The agent delay averaged over <see cref="BudgetCalculator.ControlWindow"/> as of <paramref name="nowUtc"/>
    /// (an exponential average of the delay the file has been asking for), which is how much the recent
    /// burn rate was slowed. 0 when no file of ours, or one too old to have been obeyed.
    /// </summary>
    public double AgentDelay(DateTime nowUtc)
    {
        try
        {
            if (!File.Exists(Path)) return 0;
            using var doc = JsonDocument.Parse(File.ReadAllText(Path));
            var root = doc.RootElement;
            if (!root.TryGetProperty("source", out var s) || s.GetString() != "MindAttic.Orchestrator") return 0;
            if (!root.TryGetProperty("timestamp", out var t) ||
                !DateTime.TryParse(t.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var at))
                return 0;
            var held = root.TryGetProperty("pace_delay_seconds", out var d) ? d.GetDouble() : 0;
            var average = root.TryGetProperty("pace_delay_avg", out var a) ? a.GetDouble() : 0;
            // The gate holds at a stale file's pace until its window resets (M.ORCH-T205), and ignores one without that
            // reset once it's stale; past either the agents weren't held at all.
            var trusted = root.TryGetProperty("window_reset", out var w) && w.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(w.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var reset)
                ? Math.Max((reset - at).TotalMinutes, GateStale.TotalMinutes) : GateStale.TotalMinutes;
            var obeyed = Math.Clamp((nowUtc - at).TotalMinutes, 0, trusted);
            var idle = Math.Max(0, (nowUtc - at).TotalMinutes - obeyed);
            var window = BudgetCalculator.ControlWindow.TotalMinutes;
            average += (held - average) * (1 - Math.Exp(-obeyed / window));
            return average * Math.Exp(-idle / window);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
        {
            return 0;
        }
    }

    private void RemoveOwnFile()
    {
        lastWritten = null;
        try
        {
            if (!File.Exists(Path)) return;
            using (var doc = JsonDocument.Parse(File.ReadAllText(Path)))
            {
                if (!doc.RootElement.TryGetProperty("source", out var s) || s.GetString() != "MindAttic.Orchestrator") return;
            }
            File.Delete(Path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
    }
}
