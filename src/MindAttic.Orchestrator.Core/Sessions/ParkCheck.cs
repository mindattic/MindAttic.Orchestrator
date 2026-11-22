using System.Globalization;
using System.Text.Json;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>What the park check read about one open CLI (a seam for tests): its process and newest session, where the busy check
/// put it (<see cref="BusyClis"/>, with the hosted CLI judged by its real state), whether /allocate is on in it, its /goal, the
/// window its last turn stopped for, whether a resume is recorded for it at the wall (<see cref="LimitWaker.ScheduledName"/>),
/// the wakes its transcripts still hold (<see cref="ParkCheck.PendingWakes"/>; null when a transcript couldn't be read) and
/// when its turn went idle.</summary>
public sealed record ParkFacts(int Pid, string? SessionId, CliActivity Activity, IReadOnlyList<string> BusyWhy, bool Allocate, string? Goal,
    LimitStop? LimitStop, bool LimitResume, IReadOnlyList<string>? PendingWakes, DateTime? IdleSinceUtc);

/// <summary>Whether one CLI may be parked now, and every reason it may not (empty when <see cref="Safe"/>).</summary>
public sealed record ParkVerdict(int Pid, string? SessionId, bool Safe, IReadOnlyList<string> Why)
{
    /// <summary>When its turn will have been idle for the idle time asked (M.ORCH-T2074): its idle start plus that time; null when
    /// it isn't idle, when it went idle isn't recorded, or parking is off. What the dashboard's park timer waits for next.</summary>
    public DateTime? IdleDueUtc { get; init; }
}

/// <summary>
/// Whether an open Claude CLI is safe to park (M.ORCH-T2072, part of M.ORCH-T1739): closing it (about 1.2 GB each) and keeping its
/// tab as a stub that resumes the conversation. Closing a CLI silently drops what only it holds, so the owner's rule (2026-11-22) is
/// that a CLI is parked only with ALL of: /allocate off; no pending ScheduleWakeup, CronCreate or /loop; no pace hold; no running
/// background task or subagent. On top of that it must be plainly idle (no turn running, no prompt waiting on you, no claimed
/// ticket, not on a <c>/goal</c>) for at least the idle time asked (ParkIdleCliMinutes; zero or less parks nothing).
/// <para>Wakes are read from the CLI's own transcripts (every session its process ran, so a <c>/clear</c> doesn't hide one), as
/// Claude Code keeps them in-process and nowhere else: a ScheduleWakeup is pending until its delay has passed (plus
/// <see cref="WakeGrace"/>); a CronCreate until a later CronDelete names the id its result gave, it failed, a one-shot one's prompt
/// came back as a turn, or it is older than <see cref="CronLife"/> (Claude Code's own expiry for a session's recurring jobs). A
/// <c>/loop</c> runs on those two tools, so a loop still going always has one of them pending. A transcript that can't be found
/// or read is never taken for "nothing pending".</para>
/// <para>A pace hold is the context gate's <c>limit_stop</c> on its newest session (the <see cref="LimitWaker"/> wakes it at the
/// reset) or a resume recorded for its process at the wall (<see cref="LimitWaker.ScheduledName"/>). It never closes anything itself.</para>
/// </summary>
public static class ParkCheck
{
    /// <summary>How long past a ScheduleWakeup's due time it still counts: the wake's own turn may not have started yet.</summary>
    public static readonly TimeSpan WakeGrace = TimeSpan.FromMinutes(2);

    /// <summary>A CronCreate older than this no longer counts: Claude Code expires a session's recurring jobs after seven days.</summary>
    public static readonly TimeSpan CronLife = TimeSpan.FromDays(7);

    /// <summary>A ScheduleWakeup whose delay can't be read counts for this long after it was made.</summary>
    public static readonly TimeSpan UnknownWakeLife = TimeSpan.FromHours(24);

    /// <summary>The verdict on one CLI. <paramref name="idleFor"/>: how long its turn must have been idle (zero or less: parking is off).</summary>
    public static ParkVerdict Judge(ParkFacts f, TimeSpan idleFor, DateTime nowUtc)
    {
        var why = new List<string>();
        if (idleFor <= TimeSpan.Zero) why.Add("parking is off (ParkIdleCliMinutes is 0)");
        switch (f.Activity)
        {
            case CliActivity.Idle: break;
            case CliActivity.Self: why.Add("it is the CLI asking"); break;
            case CliActivity.Working: why.Add("it is working: " + (f.BusyWhy.Count > 0 ? string.Join("; ", f.BusyWhy) : "a turn, prompt, subagent, background job or claim")); break;
            default: why.Add("its state can't be read" + (f.BusyWhy.Count > 0 ? ": " + string.Join("; ", f.BusyWhy) : "")); break;
        }
        if (f.Allocate) why.Add("/allocate is on in it");
        if (f.Goal is { Length: > 0 }) why.Add("it is on a /goal");
        if (f.LimitStop is { } stop) why.Add($"pace hold: its last turn stopped for the {stop.Window} window, which resets {stop.UntilUtc:u}");
        if (f.LimitResume) why.Add("pace hold: a resume is recorded for it at the wall");
        if (f.PendingWakes is null) why.Add("its transcript couldn't be read, so a pending ScheduleWakeup, CronCreate or /loop can't be ruled out");
        else why.AddRange(f.PendingWakes);
        if (f.SessionId is null) why.Add("no session is recorded for it, so it can't be resumed");
        if (idleFor > TimeSpan.Zero && f.Activity == CliActivity.Idle)
        {
            if (f.IdleSinceUtc is not { } since || since <= DateTime.MinValue) why.Add("when it went idle isn't recorded");
            else if (nowUtc - since < idleFor) why.Add($"idle only {Formatting.Ago(since, nowUtc)}, under {idleFor.TotalMinutes:0} min");
        }
        DateTime? due = idleFor > TimeSpan.Zero && f.Activity == CliActivity.Idle && f.IdleSinceUtc is { } idle && idle > DateTime.MinValue
            ? idle + idleFor : null;
        return new ParkVerdict(f.Pid, f.SessionId, why.Count == 0, why) { IdleDueUtc = due };
    }

    /// <summary>The wakes a transcript still holds at <paramref name="nowUtc"/>, one line each: every ScheduleWakeup not yet due
    /// and every CronCreate not yet ended (see <see cref="ParkCheck"/>). Lines that aren't JSON are skipped.</summary>
    public static IReadOnlyList<string> PendingWakes(IEnumerable<string> transcriptLines, DateTime nowUtc)
    {
        var wakes = new List<(string Id, DateTime DueUtc)>();
        var crons = new List<Cron>();
        var byToolUse = new Dictionary<string, Cron>(StringComparer.Ordinal);
        foreach (var line in transcriptLines)
        {
            var hasTool = line.Contains("\"ScheduleWakeup\"", StringComparison.Ordinal) || line.Contains("\"CronCreate\"", StringComparison.Ordinal)
                || line.Contains("\"CronDelete\"", StringComparison.Ordinal);
            var hasResult = byToolUse.Count > 0 && line.Contains("\"tool_result\"", StringComparison.Ordinal);
            var hasPrompt = crons.Count > 0 && line.Contains("\"user\"", StringComparison.Ordinal);
            if (!hasTool && !hasResult && !hasPrompt) continue;
            JsonDocument doc;
            try { doc = JsonDocument.Parse(line); }
            catch (JsonException) { continue; }
            using (doc)
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) continue;
                var at = Text(root, "timestamp") is { } ts && DateTime.TryParse(ts, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var w) ? w : (DateTime?)null;
                if (!root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object) continue;
                if (!message.TryGetProperty("content", out var content)) continue;
                if (content.ValueKind == JsonValueKind.String)
                {
                    // A typed turn: a one-shot cron's prompt coming back is that cron having fired.
                    MarkFired(crons, content.GetString());
                    continue;
                }
                if (content.ValueKind != JsonValueKind.Array) continue;
                foreach (var block in content.EnumerateArray())
                {
                    if (block.ValueKind != JsonValueKind.Object) continue;
                    switch (Text(block, "type"))
                    {
                        case "tool_use":
                            var input = block.TryGetProperty("input", out var i) && i.ValueKind == JsonValueKind.Object ? i : default;
                            var useId = Text(block, "id") ?? "";
                            switch (Text(block, "name"))
                            {
                                case "ScheduleWakeup":
                                    var made = at ?? nowUtc;
                                    var delay = Number(input, "delaySeconds") ?? Number(input, "delay_seconds") ?? Number(input, "seconds");
                                    wakes.Add((useId, delay is { } d && d >= 0 && d < 1e8 ? made.AddSeconds(d) + WakeGrace : made + UnknownWakeLife));
                                    break;
                                case "CronCreate":
                                    var cron = new Cron(at ?? nowUtc, Text(input, "cron") ?? "?", Text(input, "prompt"),
                                        !(input.ValueKind == JsonValueKind.Object && input.TryGetProperty("recurring", out var r) && r.ValueKind == JsonValueKind.False));
                                    crons.Add(cron);
                                    if (useId.Length > 0) byToolUse[useId] = cron;
                                    break;
                                case "CronDelete":
                                    if ((Text(input, "id") ?? Text(input, "job_id") ?? Text(input, "jobId")) is { Length: > 0 } gone)
                                        foreach (var c in crons.Where(c => c.Result is not null && c.Result.Contains(gone, StringComparison.Ordinal))) c.Ended = true;
                                    break;
                            }
                            break;
                        case "tool_result":
                            if (Text(block, "tool_use_id") is { } id && byToolUse.Remove(id, out var created))
                            {
                                created.Result = ResultText(block);
                                if (block.TryGetProperty("is_error", out var e) && e.ValueKind == JsonValueKind.True) created.Ended = true;
                            }
                            break;
                        case "text":
                            MarkFired(crons, Text(block, "text"));
                            break;
                    }
                }
            }
        }
        var pending = new List<string>();
        foreach (var (_, due) in wakes.Where(w => w.DueUtc > nowUtc))
            pending.Add($"a ScheduleWakeup is pending (due by {due - WakeGrace:u})");
        foreach (var c in crons.Where(c => !c.Ended && nowUtc - c.MadeUtc < CronLife))
            pending.Add($"a {(c.Recurring ? "recurring" : "one-shot")} CronCreate ({c.Schedule}) is pending, made {c.MadeUtc:u}");
        return pending;
    }

    /// <summary>The wakes still pending across every transcript of <paramref name="sessionIds"/> under <paramref name="projectsDir"/>;
    /// null when any of them can't be found or read.</summary>
    public static IReadOnlyList<string>? PendingWakes(string projectsDir, IEnumerable<string> sessionIds, DateTime nowUtc)
    {
        var all = new List<string>();
        foreach (var session in sessionIds)
        {
            if (TranscriptOf(projectsDir, session) is not { } path) return null;
            try { all.AddRange(PendingWakes(ReadLines(path), nowUtc)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
        }
        return all;
    }

    /// <summary>A session's own transcript: <c>&lt;projects&gt;\&lt;project&gt;\&lt;session&gt;.jsonl</c>; null when there is none.</summary>
    public static string? TranscriptOf(string projectsDir, string sessionId)
    {
        if (sessionId.Length == 0 || sessionId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || !Directory.Exists(projectsDir)) return null;
        try
        {
            return Directory.EnumerateFiles(projectsDir, sessionId + ".jsonl", new EnumerationOptions
            {
                RecurseSubdirectories = true, MaxRecursionDepth = 1, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint,
            }).FirstOrDefault();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>Every open CLI on this machine and whether it may be parked now, read as it stands. Nothing is changed.</summary>
    public static IReadOnlyList<ParkVerdict> Scan(TimeSpan idleFor, ContextCycler? cycler = null, JsonStore? store = null, int? self = null)
    {
        var now = DateTime.UtcNow;
        cycler ??= new ContextCycler();
        store ??= new JsonStore();
        var report = BusyClis.Scan(self, cycler, store: store, scope: new BusyScope(Hosted: true));
        var sessions = cycler.Load().Where(s => s.Pid is not null).GroupBy(s => s.Pid!.Value).ToDictionary(g => g.Key, g => g.ToList());
        var allocate = cycler.AllocateClis();
        // None recorded is none; a read that fails holds every CLI back, as a resume may be among them.
        IReadOnlyDictionary<string, LimitWaker.ScheduledResume>? resumes = new Dictionary<string, LimitWaker.ScheduledResume>();
        try { if (store.TryLoad<Dictionary<string, LimitWaker.ScheduledResume>>(LimitWaker.ScheduledName, out var r) && r is not null) resumes = r; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or System.Data.Common.DbException) { resumes = null; }
        var verdicts = new List<ParkVerdict>();
        foreach (var cli in report.Clis.Where(c => c.Pid is not null))
        {
            var pid = cli.Pid!.Value;
            var mine = sessions.GetValueOrDefault(pid) ?? [];
            var newest = mine.MaxBy(s => s.TurnUtc);
            var facts = new ParkFacts(pid, newest?.SessionId ?? cli.SessionId, cli.Activity, cli.Why, allocate.ContainsKey(pid), newest?.Goal,
                newest?.LimitStop, resumes is null || resumes.ContainsKey(pid.ToString(CultureInfo.InvariantCulture)),
                mine.Count == 0 ? null : PendingWakes(cycler.ProjectsDir, mine.Select(s => s.SessionId), now),
                newest is { Idle: true } ? newest.TurnUtc : null);
            verdicts.Add(Judge(facts, idleFor, now));
        }
        return verdicts;
    }

    private sealed class Cron(DateTime madeUtc, string schedule, string? prompt, bool recurring)
    {
        public DateTime MadeUtc { get; } = madeUtc;
        public string Schedule { get; } = schedule;
        public string? Prompt { get; } = prompt;
        public bool Recurring { get; } = recurring;
        public string? Result { get; set; }
        public bool Ended { get; set; }
    }

    private static void MarkFired(List<Cron> crons, string? said)
    {
        if (string.IsNullOrWhiteSpace(said)) return;
        foreach (var c in crons.Where(c => !c.Recurring && !c.Ended && c.Result is not null && c.Prompt is { Length: > 0 } p && said.Contains(p.Trim(), StringComparison.Ordinal)))
            c.Ended = true;
    }

    private static string? Text(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static double? Number(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) ? v.ValueKind switch
        {
            JsonValueKind.Number when v.TryGetDouble(out var d) => d,
            JsonValueKind.String when double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
            _ => null,
        } : null;

    private static string ResultText(JsonElement block)
    {
        if (!block.TryGetProperty("content", out var c)) return "";
        if (c.ValueKind == JsonValueKind.String) return c.GetString() ?? "";
        if (c.ValueKind != JsonValueKind.Array) return c.GetRawText();
        return string.Join("\n", c.EnumerateArray().Select(x => Text(x, "text")).OfType<string>());
    }

    private static IEnumerable<string> ReadLines(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line) yield return line;
    }
}
