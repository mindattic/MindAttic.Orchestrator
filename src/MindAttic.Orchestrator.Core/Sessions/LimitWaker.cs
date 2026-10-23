using System.Globalization;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// Wakes the CLIs that stopped for a window once it has reset (M.ORCH-T256). A turn that ends at the window's stop is recorded by the context gate (<c>limit_stop</c> in its state);
/// nothing used to wake it, so it sat idle with its handoff ("continue after 3:30 PM") until its author noticed. Once that
/// window's reset is past and the pace no longer stops (the reset itself is the status' to decide), the CLI's
/// Launcher tab is typed a resume prompt; a CLI in a plain terminal, or with <see cref="OrchestratorSettings.ResumeAfterReset"/>
/// off, is said to you instead. Each stop is acted on once: its typed prompt clears the record, and the ones said or typed are
/// remembered (<see cref="WokenName"/>) so a restart doesn't say them again. As a last resort, every CLI still working when the
/// pace hits the wall also gets a resume recorded for <see cref="ResumeDelay"/> past the reset (<see cref="ScheduledName"/>), so
/// one that froze over the limit, and never recorded a stop, still goes on in the next window (M.ORCH-T595).
/// </summary>
public sealed class LimitWaker(ContextCycler cycler, JsonStore store)
{
    /// <summary>The stops already woken (session and reset), in the orchestrator's store.</summary>
    public const string WokenName = "limit-wakes";

    /// <summary>The lock file only the daemon that wakes the CLIs holds (a <see cref="Scheduling.DispatchLease"/> of its own).</summary>
    public const string LeaseName = "limit-wake.lock";

    /// <summary>How long past the reset before waking: the meter probe and the status catch up with the new window first.</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromMinutes(1);

    /// <summary>A stop whose window reset longer ago than this (or, when the pace held on past that reset, a weekly stop
    /// after a 5-hour one say, longer than this after the pace last held) is left alone: the daemon was down through it,
    /// and a resume typed hours late into a CLI you may since have taken over is more surprise than help.</summary>
    public static readonly TimeSpan Forget = TimeSpan.FromHours(6);

    /// <summary>How long a woken stop is remembered: past a week's reset, so none is said twice.</summary>
    internal static readonly TimeSpan Remember = TimeSpan.FromDays(8);

    /// <summary>Whether the CLI process is still running (seam).</summary>
    internal Func<int, bool> IsAlive { get; init; } = ContextCycler.IsClaude;

    /// <summary>The Launcher tab the CLI runs in, if any (seam).</summary>
    internal Func<int, IReadOnlyList<HostSession>, HostSession?> HostOf { get; init; } =
        (pid, hosts) => hosts.FirstOrDefault(h => ProcessTree.IsAncestor(h.ProcessId, pid));

    /// <summary>Types a prompt into a tab and presses Enter (seam).</summary>
    internal Func<string, string, Task<bool>> Submit { get; init; } = (pipe, text) => SessionControl.SubmitAsync(pipe, text);

    /// <param name="Typed">The resume prompt went into the CLI's tab; false: it's said to you to do.</param>
    public sealed record Wake(string SessionId, string Where, LimitStop Stop, bool Typed, string Line);

    /// <summary>The resumes recorded at the wall (M.ORCH-T595), by CLI process id, in the orchestrator's store.</summary>
    public const string ScheduledName = "limit-resumes";

    /// <summary>How long past the reset a resume recorded at the wall is due (M.ORCH-T595).</summary>
    public static readonly TimeSpan ResumeDelay = TimeSpan.FromMinutes(2);

    /// <summary>A resume recorded at the wall for a CLI that was working there (M.ORCH-T595): its session then, the window that
    /// stopped and when it resets, and when it was recorded. Due at <see cref="ResumeDelay"/> past the reset.</summary>
    public sealed record ScheduledResume(string SessionId, string Window, DateTime UntilUtc, DateTime StoppedUtc)
    {
        public DateTime DueUtc => UntilUtc + ResumeDelay;
    }

    private Dictionary<string, DateTime>? woken;

    private Dictionary<string, ScheduledResume>? scheduled;

    /// <summary>The resumes recorded at the wall and not yet delivered or dropped (null: not read yet, or the read failed).</summary>
    internal IReadOnlyDictionary<string, ScheduledResume>? Scheduled => scheduled;

    /// <summary>The last stretch the pace stopped or wound down without a break, as this process saw it: a CLI whose own window
    /// reset inside it (the week at its stop when the 5 hours reset) is woken when it lifts, not forgotten for having waited.
    /// Only a stop whose reset it covered: a stop already forgotten isn't brought back by a later window's wind-down.</summary>
    private DateTime? holdStartUtc, heldUtc;

    /// <summary>The tick before was held: a held tick after it continues that stretch rather than starting one.</summary>
    private bool holding;

    /// <summary>What to wake now, woken: the newest session of each live CLI, idle, recorded as stopped for a window whose
    /// reset is at least <see cref="Grace"/> and at most <see cref="Forget"/> past (or the pace held on past it until lately), while <paramref name="pace"/> doesn't stop
    /// . <paramref name="type"/> false only says each.</summary>
    public IReadOnlyList<Wake> Tick(Pace? pace, IReadOnlyList<HostSession> hosts, bool type, DateTime nowUtc)
    {
        if (pace is { Stop: true })
        {
            if (!holding) holdStartUtc = nowUtc;
            holding = true;
            heldUtc = nowUtc;
            // At the wall, every CLI working there gets a resume due just past the reset, however it then stops: one frozen
            // over the limit (its turn errored, so its Stop hook never recorded a limit_stop) is still sent back (M.ORCH-T595).
            if (pace is { Stop: true, ResetUtc: { } reset }) Schedule(pace.Governing ?? "5-hour", reset, nowUtc);
            return [];
        }
        holding = false;
        // A load that failed (the database held) isn't an empty list: nothing is woken or saved until the remembered wakes are
        // read, so they're neither said again nor saved over (M.ORCH-T541).
        if (woken is null)
        {
            if (!store.TryLoad<Dictionary<string, DateTime>>(WokenName, out var remembered)) return [];
            woken = remembered;
        }
        if (scheduled is null)
        {
            if (!store.TryLoad<Dictionary<string, ScheduledResume>>(ScheduledName, out var resumes)) return [];
            scheduled = resumes;
        }
        var pruned = woken.Where(w => nowUtc - w.Value > Remember).Select(w => w.Key).ToList();
        foreach (var key in pruned) woken.Remove(key);

        var newest = Newest();
        var due = newest
            .Where(c => c is { Idle: true, LimitStop: { } stop } && nowUtc - stop.UntilUtc >= Grace &&
                        nowUtc - HeldPast(stop.UntilUtc) <= Forget &&
                        !woken.ContainsKey(Key(c.SessionId, stop)))
            .Where(c => IsAlive(c.Pid!.Value))
            .Select(c => (Context: c, Stop: c.LimitStop!))
            .ToList();

        // The resumes recorded at the wall, once due: each is delivered once, unless its CLI is woken above or has closed, was
        // /clear-ed or prompted since the reset (it's going again), or it's long past (M.ORCH-T595).
        var scheduleChanged = false;
        foreach (var (pidKey, resume) in scheduled.Where(s => nowUtc >= s.Value.DueUtc).ToList())
        {
            scheduled.Remove(pidKey);
            scheduleChanged = true;
            var stop = new LimitStop(resume.Window, resume.UntilUtc, resume.StoppedUtc);
            if (!int.TryParse(pidKey, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid) || due.Any(d => d.Context.Pid == pid) ||
                newest.FirstOrDefault(c => c.Pid == pid) is not { } c || c.SessionId != resume.SessionId || c.TurnUtc >= resume.UntilUtc ||
                nowUtc - HeldPast(resume.UntilUtc) > Forget || woken.ContainsKey(Key(c.SessionId, stop)) || !IsAlive(pid))
                continue;
            due.Add((c, stop));
        }
        // A CLI woken for its own stop isn't sent back again by the resume recorded for it at the wall.
        foreach (var (c, _) in due)
            if (scheduled.Remove(c.Pid!.Value.ToString(CultureInfo.InvariantCulture))) scheduleChanged = true;
        if (scheduleChanged) SaveSchedule();

        var wakes = new List<Wake>();
        if (due.Count > 0)
        {
            var sends = due.Select(d =>
            {
                var host = type ? HostOf(d.Context.Pid!.Value, hosts) : null;
                return (d.Context, d.Stop, Host: host, Sent: host is null ? Task.FromResult(false) : SafeSubmit(host.PipeName, ResumePrompt(d.Stop, nowUtc)));
            }).ToList();
            Task.WaitAll([.. sends.Select(s => s.Sent)]);
            foreach (var (c, stop, host, sent) in sends)
            {
                var where = Where(c.Cwd);
                var typed = sent.Result;
                var reset = Clock(stop.UntilUtc, nowUtc);
                var line = typed
                    ? $"after the {stop.Window} window reset at {reset}, told the CLI in {where} (it stopped for the limit at {Clock(stop.StoppedUtc, nowUtc)}) to carry on"
                    : $"The {stop.Window} window reset at {reset}: the CLI in {where} stopped for it at {Clock(stop.StoppedUtc, nowUtc)} and " +
                      (c.Idle ? "is still idle" : "hasn't gone on since") + ". Tell it to carry on (" +
                      (!type ? "resume-after-reset is off" : host is null ? "it isn't in a Launcher tab the orchestrator can type into" : "typing into its tab failed") + ").";
                wakes.Add(new Wake(c.SessionId, where, stop, typed, line));
                woken[Key(c.SessionId, stop)] = stop.UntilUtc;
            }
        }
        if (wakes.Count > 0 || pruned.Count > 0)
            try { store.Save(WokenName, woken); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return wakes;
    }

    /// <summary>The newest session of each CLI that recorded its process id.</summary>
    private List<SessionContext> Newest() => [.. cycler.Load()
        .Where(c => c.Pid is not null)
        .GroupBy(c => c.Pid!.Value)
        // By when its state was written, not its last turn: a session /clear just started has had none, and the stop of the
        // one before it would be typed into it (M.ORCH-T516).
        .Select(g => g.MaxBy(c => (Written(c.SessionId), c.TurnUtc))!)];

    /// <summary>Records, at the wall, a resume due <see cref="ResumeDelay"/> past <paramref name="resetUtc"/> for each live CLI
    /// working there: its turn running (it may be frozen over the limit, its Stop hook never to come), or ended since the hold
    /// began without a <c>limit_stop</c> of its own (one that has one is woken for it). Kept in the store, so a daemon restart
    /// before the reset doesn't lose it; one already recorded for this reset is kept as it was.</summary>
    private void Schedule(string window, DateTime resetUtc, DateTime nowUtc)
    {
        if (scheduled is null)
        {
            if (!store.TryLoad<Dictionary<string, ScheduledResume>>(ScheduledName, out var resumes)) return;
            scheduled = resumes;
        }
        var changed = false;
        foreach (var c in Newest())
        {
            var working = !c.Idle || (c.LimitStop is null && holdStartUtc is { } start && c.TurnUtc >= start);
            if (!working || c.TurnUtc >= resetUtc) continue;
            var key = c.Pid!.Value.ToString(CultureInfo.InvariantCulture);
            if (scheduled.TryGetValue(key, out var known) && known.SessionId == c.SessionId && known.UntilUtc == resetUtc) continue;
            if (!IsAlive(c.Pid.Value)) continue;
            scheduled[key] = new ScheduledResume(c.SessionId, window, resetUtc, nowUtc);
            changed = true;
        }
        if (changed) SaveSchedule();
    }

    private void SaveSchedule()
    {
        try { store.Save(ScheduledName, scheduled); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>What <see cref="Forget"/> counts from for a window that reset at <paramref name="untilUtc"/>: the end of the hold
    /// that was on when it reset (within <see cref="Grace"/>), else the reset itself.</summary>
    private DateTime HeldPast(DateTime untilUtc) =>
        holdStartUtc is { } start && heldUtc is { } held && start <= untilUtc + Grace && held > untilUtc ? held : untilUtc;

    /// <summary>When the context gate last wrote the session's state, its start included (1601 when it's gone since).</summary>
    private DateTime Written(string sessionId) => File.GetLastWriteTimeUtc(Path.Combine(cycler.Directory, sessionId + ".state.json"));

    private async Task<bool> SafeSubmit(string pipe, string text)
    {
        try { return await Submit(pipe, text).ConfigureAwait(false); }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException or OperationCanceledException) { return false; }
    }

    private static string Key(string sessionId, LimitStop stop) =>
        sessionId + "@" + stop.UntilUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    /// <summary>What the CLI is typed once its window has reset: one line (a line break would be a new line in the prompt box,
    /// not Enter) that says the window's back and to carry on with what it left for the reset, or say there's nothing.</summary>
    public static string ResumePrompt(LimitStop stop, DateTime nowUtc) =>
        $"The {stop.Window} window you stopped for reset at {Clock(stop.UntilUtc, nowUtc)}, so there's room to work again. Carry on with what you " +
        "stopped for the limit: whatever your last message or handoff left for after the reset. If nothing was left, say so in one short line and end your turn.";

    /// <summary>The folder the CLI runs in, by name (a repo, or a worktree's ticket); "an unknown folder" when not recorded.</summary>
    internal static string Where(string? cwd) =>
        string.IsNullOrWhiteSpace(cwd) ? "an unknown folder" : Path.GetFileName(cwd.TrimEnd('\\', '/')) is { Length: > 0 } name ? name : cwd;

    // With the weekday when it falls on another day: a weekly stop days back, or a 5-hour one before midnight, read as today's.
    private static string Clock(DateTime utc, DateTime nowUtc) => Formatting.ResetTime(utc, nowUtc);
}
