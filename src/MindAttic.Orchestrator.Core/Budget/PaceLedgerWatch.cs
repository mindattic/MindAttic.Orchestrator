using MindAttic.Orchestrator.Settings;
using MindAttic.Workflow.Tickets;
using Serilog.Events;

namespace MindAttic.Orchestrator.Budget;

/// <summary>
/// The daemon's side of the pace ledger (M.ORCH-T166). Once a minute it closes every 5-hour window that has ended into
/// <c>pace-ledger.jsonl</c>; a new 5-hour limit hit is raised the moment it's read (Error in the log, an urgent alert and
/// a fix ticket in this repo with what the pace gate did around it), and a window that closes over or blind is too.
/// Windows backfilled from before the ledger existed are only recorded: their tickets would be history, not news.
/// </summary>
public sealed class PaceLedgerWatch(JsonStore store, string workspaceRoot, Action<LogEventLevel, string> say, Action<string, string, bool> alert)
{
    public const string Repo = "MindAttic.Orchestrator";

    /// <summary>A window that closes later than this after its reset was closed by a backfill, not live.</summary>
    internal static readonly TimeSpan Live = TimeSpan.FromHours(6);

    /// <summary>How long the pace gate's raw events are kept once counted (the ledger keeps the counts for good).</summary>
    public static readonly TimeSpan KeepEvents = TimeSpan.FromDays(8);

    public string LedgerPath => Path.Combine(store.Directory, PaceLedger.FileName);
    public static string DefaultEventsPath { get; } = Path.Combine(Setup.AgentGate.ClaudeDir, "session-context", Setup.AgentGate.GateLogName);
    public string EventsPath { get; init; } = DefaultEventsPath;

    /// <summary>Files tickets; tests swap it. Returns the new id, or null when none was filed.</summary>
    internal Func<string, string?> FileTicket { get; set; } = null!;

    private DateTime checkedUtc;
    /// <summary>The walls (5-hour limits, by when each resets) already raised. Keyed by the wall, not the hit's time: the
    /// stored hit time moves to an earlier or better-covered sighting of the same wall, and that's not a second hit.</summary>
    private HashSet<DateTime>? alarmed;

    /// <summary>Where the raised walls are remembered across restarts (M.ORCH-T172's fix replaced <c>pace-alarms</c>, which held hit times).</summary>
    internal const string AlarmsName = "pace-alarm-walls";

    public void Tick(DateTime nowUtc)
    {
        if (nowUtc - checkedUtc < TimeSpan.FromMinutes(1)) return;
        var first = checkedUtc == default;
        checkedUtc = nowUtc;
        FileTicket ??= File;

        var samples = store.Load<List<UsageSample>>("usage-samples");
        var hits = store.LoadLimits();
        if (first && samples.Count > 0)
        {
            var last = samples.Max(s => s.AtUtc);
            // A daemon that was down left the pace blind; say for how long, so the gap is on the record, not a hole.
            say(nowUtc - last > PaceLedger.MinGap ? LogEventLevel.Warning : LogEventLevel.Information,
                $"pace ledger: the last report from Claude's meter was {Formatting.Ago(last, nowUtc)}");
        }

        RaiseNewHits(hits, nowUtc);

        var recorded = PaceLedger.Load(LedgerPath);
        var events = PaceLedger.LoadEvents(EventsPath, nowUtc - KeepEvents);
        var due = PaceLedger.Due(recorded, samples, hits, events, nowUtc);
        if (due.Count == 0) return;
        PaceLedger.Append(LedgerPath, due);
        foreach (var w in due) Report(w, nowUtc);
        try { PaceLedger.TrimEvents(EventsPath, nowUtc - KeepEvents); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>A 5-hour limit hit read from the transcripts is the failure the orchestrator exists to prevent: raise it at once.</summary>
    private void RaiseNewHits(List<LimitObservation> hits, DateTime nowUtc)
    {
        var session = hits.Where(h => h.Kind == LimitKind.Session).ToList();
        if (alarmed is null)
        {
            var saved = store.Load<List<DateTime>>(AlarmsName);
            // The first run knows every hit so far as history: only hits after it are news.
            alarmed = System.IO.File.Exists(Path.Combine(store.Directory, AlarmsName + ".json")) ? [.. saved] : [.. session.Select(h => h.ResetUtc)];
            if (saved.Count == 0) Save();
        }
        var fresh = session.Where(h => !alarmed.Contains(h.ResetUtc)).OrderBy(h => h.HitUtc).ToList();
        foreach (var o in fresh)
        {
            alarmed.Add(o.ResetUtc);
            var hit = o.HitUtc;
            var around = PaceLedger.LoadEvents(EventsPath, hit - TimeSpan.FromMinutes(30)).Where(e => e.AtUtc <= hit + TimeSpan.FromMinutes(1)).ToList();
            var line = $"Agents ran out of tokens: the 5-hour limit was hit at {hit.ToLocalTime():t} ({Formatting.Ago(hit, nowUtc)}), " +
                       $"{Formatting.Tokens(o.FreshUsed)} uncached into the window, resetting {o.ResetUtc.ToLocalTime():t}. " +
                       $"In the 30 minutes before it the pace gate held {around.Count(e => e.Decision == GateEvent.Held)} calls " +
                       $"and refused {around.Count(e => e.Decision == GateEvent.Refused)}.";
            say(LogEventLevel.Error, "pace ledger: " + line);
            alert("Ran out of tokens", line, true);
            if (nowUtc - hit <= Live)
                Filed(FileTicket($"[pace-ledger hit resetting {o.ResetUtc:yyyy-MM-ddTHH:mmZ}] {line} The orchestrator must never let this happen: find which draw got past the stop and close the hole."));
        }
        if (fresh.Count > 0) Save();

        void Save()
        {
            try { store.Save(AlarmsName, alarmed!.Where(reset => nowUtc - reset < LimitCalibration.Keep).Order().ToList()); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    private void Report(LedgerWindow w, DateTime nowUtc)
    {
        var live = nowUtc - w.EndUtc <= Live;
        var line = $"window {w.StartUtc.ToLocalTime():MMM d h:mm tt}–{w.EndUtc.ToLocalTime():h:mm tt} closed {w.Grade.ToString().ToLowerInvariant()}: {w.Why}" +
                   (w.PeakPercent is { } p ? $", peak {p:0}%" : "") +
                   (w.Held > 0 ? $", {w.Held} calls held ({w.HeldMinutes:0.#} min)" : "") +
                   (w.Refused > 0 ? $", {w.Refused} refused" : "") +
                   (w.PromptsAnswered > 0 ? $", {w.PromptsAnswered} typed prompts answered at the stop" : "") +
                   (live ? "" : " (backfilled)");
        var bad = w.Grade is WindowGrade.Over or WindowGrade.Blind;
        say(bad && live ? (w.Grade == WindowGrade.Over ? LogEventLevel.Error : LogEventLevel.Warning) : LogEventLevel.Information, "pace ledger: " + line);
        if (!live || !bad) return;

        // A limit hit was raised (and ticketed) when it was read; the window closing over is the same news.
        if (w.Grade == WindowGrade.Over && w.LimitHitUtc is not null) return;
        if (w.Grade == WindowGrade.Over)
        {
            alert("Window went over", line, true);
            Filed(FileTicket($"[pace-ledger over {w.EndUtc:yyyy-MM-ddTHH:mmZ}] Claude reported 100% in the 5-hour window ending {w.EndUtc.ToLocalTime():MMM d h:mm tt} without a limit hit being read: {line}. Find what got past the 99% stop."));
            return;
        }
        // Blind windows recur until the cause is fixed, so one open ticket (and one alert) stands for all of them.
        var id = FileTicket($"[pace-ledger blind] {line}. The pace ran on its own estimate this long without Claude's meter: make sure it gets a fresh reading whenever tokens are being spent.");
        Filed(id);
        if (id is not null) alert("Pace was blind", line, false);
    }

    private void Filed(string? id)
    {
        if (id is not null) say(LogEventLevel.Information, $"pace ledger: filed {id} in {Repo}");
    }

    /// <summary>Files a fix in this repo unless an open ticket already carries the summary's [pace-ledger ...] marker.</summary>
    private string? File(string summary)
    {
        var marker = summary[..(summary.IndexOf(']') + 1)];
        try
        {
            using var db = MindAttic.Workflow.Data.WorkflowDb.OpenIfMigrated(workspaceRoot);
            if (db is null) return null;
            if (db.Tickets(Repo).Any(t => t.Ticket.Status == TicketRules.Open && t.Ticket.Summary.Contains(marker, StringComparison.Ordinal))) return null;
            return db.Add(Repo, "fix", TicketRules.Open, summary, PaceLedger.FileName).Id;
        }
        catch (Exception ex)
        {
            say(LogEventLevel.Warning, $"pace ledger: couldn't file a ticket: {ex.Message}");
            return null;
        }
    }
}
