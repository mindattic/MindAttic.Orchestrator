using System.Globalization;
using System.Text.Json;

namespace MindAttic.Orchestrator.Budget;

/// <summary>How a 5-hour window went against the pacing goal: 99% of it spent, never over.</summary>
public enum WindowGrade
{
    /// <summary>Ended at or above <see cref="PaceLedger.OkPercent"/> without a limit hit.</summary>
    Ok,
    /// <summary>Ended below <see cref="PaceLedger.OkPercent"/>: tokens left on the table, nothing broken.</summary>
    Under,
    /// <summary>Claude's meter went unseen while a real share of the window was spent; the pace was estimating.</summary>
    Blind,
    /// <summary>The 5-hour limit was hit, or Claude reported 100%: agents ran out of tokens.</summary>
    Over,
}

/// <summary>A stretch with no report from Claude's own meter while the transcripts show tokens being spent.</summary>
public sealed record MeterGap(DateTime FromUtc, DateTime ToUtc, long Fresh)
{
    public TimeSpan Length => ToUtc - FromUtc;
}

/// <summary>One line of the pace gate's event log (<c>pace-gate.jsonl</c>): a held tool call, a refusal at the stop, or a typed prompt answered at the stop.</summary>
public sealed record GateEvent(DateTime AtUtc, string Decision, double HeldSeconds, string? Tool)
{
    /// <summary><see cref="Exempt"/>: a call let straight through because its turn (or Skill) is an exempt command (M.ORCH-T198).</summary>
    public const string Held = "held", Refused = "refused", PromptAnswered = "prompt-answered", Exempt = "exempt";
}

/// <summary>One finished 5-hour window as the ledger records it, for good.</summary>
public sealed record LedgerWindow(
    DateTime StartUtc,
    DateTime EndUtc,
    WindowGrade Grade,
    string Why,
    double? PeakPercent,
    double? FinalPercent,
    long Fresh,
    int Samples,
    double MinutesAtTarget,
    DateTime? LimitHitUtc,
    DateTime? WeeklyHitUtc,
    int Held,
    double HeldMinutes,
    int Refused,
    int PromptsAnswered,
    IReadOnlyList<MeterGap> Gaps,
    int Exempt = 0,
    string? Account = null)
{
    public TimeSpan Blind => Gaps.Aggregate(TimeSpan.Zero, (t, g) => t + g.Length);
}

/// <summary>
/// The pace ledger (M.ORCH-T166): one permanent row per 5-hour window in orchestrator.db's <c>pace_ledger</c> (M.ORCH-T494; it
/// was <c>pace-ledger.jsonl</c>), graded against the goal, so "agents never run out of tokens" is something measured over time
/// rather than assumed. Built from what is already recorded: Claude's own percent (<c>usage_samples</c>), the limit hits read from transcripts
/// (<c>limits.json</c>) and the pace gate's holds and refusals (<c>pace-gate.jsonl</c>).
/// </summary>
public static class PaceLedger
{
    /// <summary>Where the ledger is kept, as the tickets it files cite it.</summary>
    public const string Ref = "data/orchestrator.db pace_ledger";

    /// <summary>A window ending here or above, with no hit, met the goal (99% is the target; whole-percent reports and a
    /// last turn that ends just short make a few points under it the same result).</summary>
    public const double OkPercent = 95;

    /// <summary>The pacing target: the stop holds every call from here.</summary>
    public const double TargetPercent = 99;

    /// <summary>A gap in Claude's reports is blind once it's this long and this much of the window was spent in it.</summary>
    public static readonly TimeSpan MinGap = TimeSpan.FromMinutes(15);
    public const double MinGapPercent = 2;

    /// <summary>How long after its reset a window is closed: late samples and limit hits read from transcripts land by then.</summary>
    public static readonly TimeSpan Settle = TimeSpan.FromMinutes(2);

    private static readonly JsonSerializerOptions Json = new() { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    /// <summary>Grades one window from everything recorded about it. <paramref name="endUtc"/> is its reset.</summary>
    /// <param name="nowUtc">For a window still running: minutes at the target are counted to now, not to its end.</param>
    /// <param name="accountAt">Which Claude account was signed in at a time (null: unknown). Samples carry no account, and after
    /// a /login the new account's window can reset within minutes of the old one's: the window is graded from the samples
    /// of the account its last one was taken under, not both mixed (M.ORCH-T343).</param>
    public static LedgerWindow Build(DateTime endUtc, IEnumerable<UsageSample> samples, IEnumerable<LimitObservation> hits, IEnumerable<GateEvent> events,
        DateTime? nowUtc = null, Func<DateTime, string?>? accountAt = null)
    {
        var start = endUtc - BudgetCalculator.BlockLength;
        var mine = samples.Where(s => Near(s.ResetUtc, endUtc)).OrderBy(s => s.AtUtc).ToList();
        var owner = accountAt is not null && mine.Count > 0 ? accountAt(mine[^1].AtUtc) : null;
        if (owner is not null)
        {
            mine = [.. mine.Where(s => accountAt!(s.AtUtc) is not { } a || a == owner)];
            // The old account's limit hit (the usual reason for the /login) isn't this window's either (M.ORCH-T365).
            hits = [.. hits.Where(h => accountAt!(h.HitUtc) is not { } a || a == owner)];
        }
        var hit = hits.Where(h => h.Kind == LimitKind.Session && (Near(h.ResetUtc, endUtc) || h.HitUtc >= start && h.HitUtc < endUtc)).MinBy(h => h.HitUtc);
        var weekly = hits.Where(h => h.Kind == LimitKind.Weekly && h.HitUtc >= start && h.HitUtc < endUtc).MinBy(h => h.HitUtc);
        var gate = events.Where(e => e.AtUtc >= start && e.AtUtc < endUtc).ToList();

        double? peak = mine.Count == 0 ? null : mine.Max(s => s.Percent);
        double? final = mine.Count == 0 ? null : mine[^1].Percent;
        var fresh = mine.Count == 0 ? hit?.FreshUsed ?? 0 : mine.Max(s => s.WindowFresh);

        // Tokens per percent from this window's own reports, to say how much of it a gap spent.
        var perPercent = mine.LastOrDefault(s => s.Percent >= ReportedUsage.MinPercent) is { } sized ? sized.WindowFresh / sized.Percent : (double?)null;
        var gaps = new List<MeterGap>();
        var (prevAt, prevFresh) = (start, 0L);
        foreach (var s in mine)
        {
            var spent = s.WindowFresh - prevFresh;
            if (s.AtUtc - prevAt >= MinGap && spent > 0 && (perPercent is not { } pp || spent / pp >= MinGapPercent))
                gaps.Add(new MeterGap(prevAt, s.AtUtc, spent));
            (prevAt, prevFresh) = (s.AtUtc, s.WindowFresh);
        }

        var atTarget = 0.0;
        for (var i = 0; i < mine.Count; i++)
            if (mine[i].Percent >= TargetPercent)
                atTarget += ((i + 1 < mine.Count ? mine[i + 1].AtUtc : nowUtc is { } n && n < endUtc ? n : endUtc) - mine[i].AtUtc).TotalMinutes;

        var held = gate.Where(e => e.Decision == GateEvent.Held).ToList();
        var (grade, why) =
            hit is not null ? (WindowGrade.Over, $"hit the 5-hour limit at {Formatting.ResetTime(hit.HitUtc, hit.HitUtc)}") :
            peak >= 100 ? (WindowGrade.Over, "Claude reported 100%") :
            gaps.Count > 0 ? (WindowGrade.Blind, $"Claude's meter unseen for {Formatting.Span(gaps.Aggregate(TimeSpan.Zero, (t, g) => t + g.Length))} while {Formatting.Tokens(gaps.Sum(g => g.Fresh))} were spent") :
            final is null ? (WindowGrade.Under, "no reports from Claude's meter") :
            final < OkPercent ? (WindowGrade.Under, $"ended at {final:0}%") :
            (WindowGrade.Ok, $"ended at {final:0}%");
        return new LedgerWindow(start, endUtc, grade, why, peak, final, fresh, mine.Count, Math.Round(atTarget, 1),
            hit?.HitUtc, weekly?.HitUtc, held.Count, Math.Round(held.Sum(e => e.HeldSeconds) / 60, 1),
            gate.Count(e => e.Decision == GateEvent.Refused), gate.Count(e => e.Decision == GateEvent.PromptAnswered), gaps,
            gate.Count(e => e.Decision == GateEvent.Exempt), owner);
    }

    /// <summary>
    /// The windows that ended (by <paramref name="nowUtc"/> less <see cref="Settle"/>) and aren't in <paramref name="recorded"/> yet,
    /// oldest first: every window the samples saw, plus any limit hit's window from before the samples began.
    /// </summary>
    public static IReadOnlyList<LedgerWindow> Due(IReadOnlyList<LedgerWindow> recorded, IReadOnlyList<UsageSample> samples,
        IReadOnlyList<LimitObservation> hits, IReadOnlyList<GateEvent> events, DateTime nowUtc, Func<DateTime, string?>? accountAt = null)
    {
        var ends = samples.Select(s => s.ResetUtc)
            .Concat(hits.Where(h => h.Kind == LimitKind.Session).Select(h => h.ResetUtc))
            .Where(e => e + Settle <= nowUtc)
            .Order().ToList();
        // A reset within a window already taken (recorded, or earlier in this pass) is that window seen again, not another;
        // but after a /login the new account's window overlaps the old one's by hours and is its own (M.ORCH-T345).
        string? AccountOf(DateTime end) => accountAt is null ? null
            : samples.Where(s => Near(s.ResetUtc, end)).MaxBy(s => s.AtUtc) is { } last ? accountAt(last.AtUtc) : null;
        var taken = recorded.Select(r => (End: r.EndUtc, r.Account)).ToList();
        var due = new List<DateTime>();
        foreach (var end in ends)
        {
            var account = AccountOf(end);
            if (taken.Any(t => Near(end, t.End) || Overlaps(end, t.End) && (account is null || t.Account is null || account == t.Account))) continue;
            due.Add(end);
            taken.Add((end, account));
        }
        return [.. due.Select(d => Build(d, samples, hits, events, accountAt: accountAt))];
    }

    // Two windows are the same one when their resets are within 10 minutes (Claude rounds resets to the 10 minutes).
    private static bool Near(DateTime a, DateTime b) => Math.Abs((a - b).TotalMinutes) <= 10;

    // Windows are 5 hours ending at their reset; ones sharing more than 10 minutes are the same window.
    private static bool Overlaps(DateTime a, DateTime b) => Math.Abs((a - b).TotalMinutes) < BudgetCalculator.BlockLength.TotalMinutes - 10;

    /// <summary>The window still running, graded as it stands; null when Claude hasn't reported it.</summary>
    public static LedgerWindow? Current(IReadOnlyList<UsageSample> samples, IReadOnlyList<LimitObservation> hits, IReadOnlyList<GateEvent> events, DateTime nowUtc,
        Func<DateTime, string?>? accountAt = null) =>
        samples.Where(s => s.ResetUtc > nowUtc).MaxBy(s => s.ResetUtc) is { } latest ? Build(latest.ResetUtc, samples, hits, events, nowUtc, accountAt) : null;

    /// <summary>Which account the sign-in record says was signed in at a time; null before the first one seen or with no record.</summary>
    public static Func<DateTime, string?> AccountAt(ClaudeAccount account, DateTime nowUtc)
    {
        IReadOnlyList<AccountSpan> history;
        try { history = account.Read(nowUtc)?.History ?? []; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { history = []; }
        return utc => history.LastOrDefault(s => utc >= s.From && (s.To is not { } to || utc < to))?.Account;
    }

    public static string Serialize(LedgerWindow w) => JsonSerializer.Serialize(w, Json);

    /// <summary>Every row in a <c>pace-ledger.jsonl</c> as the ledger was kept before orchestrator.db (read once by
    /// <see cref="Settings.OrchestratorHistory.ImportJson"/>), oldest first; a torn or unreadable line is skipped.</summary>
    public static List<LedgerWindow> Load(string path)
    {
        var rows = new List<LedgerWindow>();
        if (!File.Exists(path)) return rows;
        foreach (var line in ReadLines(path))
        {
            try { if (JsonSerializer.Deserialize<LedgerWindow>(line, Json) is { } w) rows.Add(w); }
            catch (JsonException) { }
        }
        return [.. rows.OrderBy(r => r.EndUtc)];
    }

    /// <summary>The pace gate's events since <paramref name="sinceUtc"/>; a torn line (a hook mid-write) is skipped.</summary>
    public static List<GateEvent> LoadEvents(string path, DateTime sinceUtc)
    {
        var events = new List<GateEvent>();
        if (!File.Exists(path)) return events;
        foreach (var line in ReadLines(path))
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                var r = doc.RootElement;
                if (!r.TryGetProperty("at", out var a) || !DateTime.TryParse(a.GetString(), CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var at) || at < sinceUtc) continue;
                events.Add(new GateEvent(at,
                    r.TryGetProperty("decision", out var d) ? d.GetString() ?? "" : "",
                    r.TryGetProperty("held", out var h) && h.ValueKind == JsonValueKind.Number ? h.GetDouble() : 0,
                    r.TryGetProperty("tool", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null));
            }
            catch (JsonException) { }
        }
        return events;
    }

    /// <summary>Drops events older than <paramref name="keep"/> once they're counted into the ledger. A hook appending in
    /// the moment between the read and the swap loses that one line, which only shortens one count.</summary>
    public static void TrimEvents(string path, DateTime keepFromUtc)
    {
        if (!File.Exists(path)) return;
        var lines = ReadLines(path).ToList();
        var kept = lines.Where(l => !(DateTime.TryParse(AtOf(l), CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var at) && at < keepFromUtc)).ToList();
        if (kept.Count == lines.Count) return;
        var tmp = path + ".tmp";
        File.WriteAllLines(tmp, kept);
        File.Move(tmp, path, overwrite: true);
    }

    private static string? AtOf(string line)
    {
        try { using var doc = JsonDocument.Parse(line); return doc.RootElement.TryGetProperty("at", out var a) ? a.GetString() : null; }
        catch (JsonException) { return null; }
    }

    // Shared read: the hooks append while this reads.
    private static IEnumerable<string> ReadLines(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
            if (!string.IsNullOrWhiteSpace(line)) yield return line;
    }

    /// <summary>One line on the whole record: "142 windows since Oct 15, 0 over, median end 97%, 2 blind; last over: 3d ago".</summary>
    public static string Summary(IReadOnlyList<LedgerWindow> rows, DateTime nowUtc)
    {
        if (rows.Count == 0) return "no windows recorded yet";
        var ended = rows.Where(r => r.FinalPercent is not null).Select(r => r.FinalPercent!.Value).Order().ToList();
        var median = ended.Count == 0 ? "" : $", median end {ended[(ended.Count - 1) / 2]:0}%";
        var over = rows.Where(r => r.Grade == WindowGrade.Over).ToList();
        var lastOver = over.Count == 0 ? "never" : Formatting.Ago(over[^1].LimitHitUtc ?? over[^1].EndUtc, nowUtc);
        return $"{rows.Count} window{(rows.Count == 1 ? "" : "s")} since {rows[0].StartUtc.ToLocalTime():MMM d}: " +
               $"{over.Count} over, {rows.Count(r => r.Grade == WindowGrade.Blind)} blind, {rows.Count(r => r.Grade == WindowGrade.Under)} under, " +
               $"{rows.Count(r => r.Grade == WindowGrade.Ok)} ok{median}; last over: {lastOver}";
    }
}
