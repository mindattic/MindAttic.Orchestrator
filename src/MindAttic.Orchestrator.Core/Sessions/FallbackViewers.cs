using System.Diagnostics;
using MindAttic.Orchestrator.Setup;
using MindAttic.Orchestrator.Term;
using Serilog.Events;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// Whether Orchestrator.Desktop (the dashboard) is running (M.ORCH-T960): it holds its single-instance mutex and its show event
/// for its whole life (<see cref="SingleInstance"/>), and a crashed or closed one holds neither. Both are asked for, because a
/// later launch briefly opens the mutex too while it hands over; only the owner creates the show event. Never throws.
/// </summary>
public static class DashboardPresence
{
    public static bool IsUp() => IsUp(SingleInstance.DashboardName);

    /// <summary>Whether the dashboard's mutex or its show event exists (M.ORCH-T1050): looser than <see cref="IsUp"/>, which
    /// wants both, so a fallback viewer whose link was just taken folds while the dashboard is still starting or handing over,
    /// rather than reconnecting and taking a protocol 1 session back from its tab. Never throws.</summary>
    public static bool IsPresent() => IsPresent(SingleInstance.DashboardName);

    internal static bool IsPresent(string name)
    {
        try
        {
            if (Mutex.TryOpenExisting(SingleInstance.MutexName(name), out var mutex))
            {
                mutex.Dispose();
                return true;
            }
        }
        catch (UnauthorizedAccessException) { return true; }
        catch (Exception ex) when (ex is IOException or WaitHandleCannotBeOpenedException or ArgumentException) { }
        try
        {
            if (!EventWaitHandle.TryOpenExisting(SingleInstance.ShowEvent(name), out var show)) return false;
            show.Dispose();
            return true;
        }
        catch (UnauthorizedAccessException) { return true; }
        catch (Exception ex) when (ex is IOException or WaitHandleCannotBeOpenedException or ArgumentException) { return false; }
    }

    internal static bool IsUp(string name)
    {
        try
        {
            if (!Mutex.TryOpenExisting(SingleInstance.MutexName(name), out var mutex)) return false;
            mutex.Dispose();
            if (!EventWaitHandle.TryOpenExisting(SingleInstance.ShowEvent(name), out var show)) return false;
            show.Dispose();
            return true;
        }
        // It exists but can't be opened (another user's or elevated copy): something holds it, so it's up.
        catch (UnauthorizedAccessException) { return true; }
        catch (Exception ex) when (ex is IOException or WaitHandleCannotBeOpenedException or ArgumentException) { return false; }
    }
}

/// <summary>
/// "The dashboard is down" (M.ORCH-T960): not seen up for <see cref="Threshold"/> in a row, so a dashboard restarting for an
/// upgrade, or one still starting, doesn't flash every CLI into its own window. Fed one observation at a time; pure.
/// </summary>
public sealed class DashboardDownWatch(TimeSpan? threshold = null)
{
    public static readonly TimeSpan DefaultThreshold = TimeSpan.FromSeconds(5);

    private DateTime? downSinceUtc;

    public TimeSpan Threshold { get; } = threshold ?? DefaultThreshold;

    /// <summary>When it was first seen down in the current run of downs; null while up.</summary>
    public DateTime? DownSinceUtc => downSinceUtc;

    /// <summary>Down for at least <see cref="Threshold"/> at the last observation.</summary>
    public bool IsDown { get; private set; }

    /// <summary>Records one look at the dashboard; true when it has now been down for at least <see cref="Threshold"/>.</summary>
    public bool Observe(bool up, DateTime nowUtc)
    {
        if (up)
        {
            downSinceUtc = null;
            return IsDown = false;
        }
        downSinceUtc ??= nowUtc;
        // A clock stepped back restarts the count rather than leaving it negative for good.
        if (nowUtc < downSinceUtc) downSinceUtc = nowUtc;
        return IsDown = nowUtc - downSinceUtc.Value >= Threshold;
    }
}

/// <summary>
/// A standalone TermViewer's claim on one session (M.ORCH-T960): an interactive viewer holds this named mutex while its window is
/// open, so the daemon knows the session already has a window and a second viewer of the same session exits at once. A --watch
/// pop-out never holds it (M.ORCH-T977): a read-only window doesn't stand in for the interactive fallback one.
/// </summary>
public static class TermViewerSlot
{
    public static string MutexName(string sessionId) => @"Local\MindAttic.Orchestrator.TermViewer." + TermProtocol.Sanitize(sessionId);

    /// <summary>Whether a viewer holds <paramref name="sessionId"/>'s slot. One that can't be read counts as held: no window
    /// is better than a pile of them.</summary>
    public static bool IsHeld(string sessionId)
    {
        try
        {
            if (!Mutex.TryOpenExisting(MutexName(sessionId), out var mutex)) return false;
            mutex.Dispose();
            return true;
        }
        catch (UnauthorizedAccessException) { return true; }
        catch (Exception ex) when (ex is IOException or WaitHandleCannotBeOpenedException or ArgumentException) { return true; }
    }
}

/// <summary>
/// The fallback viewers (M.ORCH-T960): while the dashboard is down (<see cref="DashboardDownWatch"/>), every live hosted CLI
/// (<see cref="TermSessions.Enumerate()"/>) without a viewer gets its own standalone TermViewer window, and one started while it
/// stays down (the Launcher, M.LNCH-T82) gets one within a second or so, from the registry. The windows fold back themselves
/// when the dashboard returns (docs/app-bus.md, "Fallback viewers"). Runs in the always-on daemon on its own thread: every
/// failure is contained and logged, and nothing here can end the daemon.
/// <para>A viewer the person closed (it exits 0) isn't reopened until the dashboard has been back and gone down again; one that
/// crashed (any other exit code) is reopened after a wait that doubles with each crash in a row, and after
/// <see cref="MaxCrashes"/> of them is left closed this time round too (M.ORCH-T974).</para>
/// </summary>
public sealed class FallbackViewers
{
    public const string ExeName = TermViewerInstalls.ExeName;
    /// <summary>The folder under the install dir its versions are deployed to (<c>C:\Apps\MindAttic.Orchestrator\TermViewer\&lt;version&gt;\</c>).</summary>
    public const string FolderName = TermViewerInstalls.FolderName;
    /// <summary>A dev override: the full path of the TermViewer exe to start.</summary>
    public const string ExeVariable = "MINDATTIC_TERMVIEWER";

    public static readonly TimeSpan Every = TimeSpan.FromSeconds(1);

    /// <summary>How long a started viewer has to claim its slot before the session counts as unviewed again.</summary>
    public static readonly TimeSpan LaunchGrace = TimeSpan.FromSeconds(20);

    private readonly Serilog.ILogger log = OrchestratorLog.For("FallbackViewers");
    private readonly DashboardDownWatch watch;
    private readonly Dictionary<string, Launched> launched = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> dismissed = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Crashes> crashes = new(StringComparer.OrdinalIgnoreCase);
    private bool wasDown;
    private string? exe;
    private DateTime exeResolvedUtc = DateTime.MinValue;
    private bool saidMissing;

    public FallbackViewers(TimeSpan? threshold = null) => watch = new DashboardDownWatch(threshold);

    /// <summary>One started viewer: when, and its process (null when the launcher gave none).</summary>
    private sealed record Launched(DateTime AtUtc, Process? Process);

    /// <summary>A session's viewer crashes in a row (M.ORCH-T974), and when the next window may open.</summary>
    private sealed record Crashes(int Count, DateTime RetryAfterUtc);

    /// <summary>After this many crashes in a row a session's window isn't reopened until the dashboard has been back and gone
    /// down again (M.ORCH-T974).</summary>
    public const int MaxCrashes = 6;

    /// <summary>The longest wait before reopening a crashed viewer.</summary>
    public static readonly TimeSpan MaxCrashDelay = TimeSpan.FromMinutes(1);

    /// <summary>A viewer that ran this long before it crashed starts the count over.</summary>
    public static readonly TimeSpan StableRun = TimeSpan.FromMinutes(2);

    /// <summary>How long to wait before reopening a viewer after its <paramref name="crashes"/>th crash in a row: 2 s, 4 s, 8 s
    /// and so on, capped at <see cref="MaxCrashDelay"/>.</summary>
    public static TimeSpan CrashDelay(int crashes) =>
        TimeSpan.FromSeconds(Math.Min(Math.Pow(2, Math.Clamp(crashes, 1, 16)), MaxCrashDelay.TotalSeconds));

    public Func<bool> DashboardUp { get; init; } = DashboardPresence.IsUp;
    public Func<IReadOnlyList<TermSessionRecord>> Sessions { get; init; } = TermSessions.Enumerate;
    public Func<string, bool> HasViewer { get; init; } = TermViewerSlot.IsHeld;
    public Func<string?> ResolveExe { get; init; } = DefaultExe;
    public Func<string, TermSessionRecord, Process?> Launch { get; init; } = StartViewer;
    public Action<LogEventLevel, string>? Say { get; init; }

    /// <summary>
    /// The sessions that need a window now: running (an exited CLI's host only lingers), with a pipe, no viewer holding its slot,
    /// none started for it within <paramref name="grace"/>, and not closed by the person this time round. A slot that can't be
    /// read counts as held.
    /// </summary>
    public static IReadOnlyList<TermSessionRecord> NeedingWindow(IEnumerable<TermSessionRecord> sessions, Func<string, bool> hasViewer,
        IReadOnlyDictionary<string, DateTime> launchedUtc, IReadOnlySet<string> dismissed, DateTime nowUtc, TimeSpan grace)
    {
        var need = new List<TermSessionRecord>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in sessions)
        {
            if (s is null || string.IsNullOrWhiteSpace(s.Id) || string.IsNullOrWhiteSpace(s.Pipe) || s.Exited) continue;
            if (!seen.Add(s.Id) || dismissed.Contains(s.Id)) continue;
            if (launchedUtc.TryGetValue(s.Id, out var at) && nowUtc - at < grace && nowUtc >= at) continue;
            bool held;
            try { held = hasViewer(s.Id); }
            catch (Exception) { held = true; }
            if (!held) need.Add(s);
        }
        return need;
    }

    /// <summary>Runs until <paramref name="cancel"/>, one look every <see cref="Every"/>. Never throws.</summary>
    public void Run(CancellationToken cancel)
    {
        while (!cancel.IsCancellationRequested)
        {
            try { Tick(DateTime.UtcNow); }
            catch (Exception ex) { log.Error(ex, "A fallback viewers pass failed; contained"); }
            try { if (cancel.WaitHandle.WaitOne(Every)) return; }
            catch (ObjectDisposedException) { return; }
        }
    }

    /// <summary>One look: is the dashboard down, and which sessions need a window. Returns how many viewers it started.</summary>
    public int Tick(DateTime nowUtc)
    {
        bool up;
        try { up = DashboardUp(); }
        catch (Exception) { up = true; } // unsure: never open windows over a dashboard that may be there
        var down = watch.Observe(up, nowUtc);
        Reap(nowUtc);
        if (!down)
        {
            if (wasDown) Tell(LogEventLevel.Information, "fallback viewers: the dashboard is back; the standalone viewers fold into its tabs");
            wasDown = false;
            dismissed.Clear();
            crashes.Clear();
            Forget();
            return 0;
        }
        if (!wasDown)
        {
            wasDown = true;
            saidMissing = false;
            Tell(LogEventLevel.Information, $"fallback viewers: the dashboard has been down since {watch.DownSinceUtc:HH:mm:ss}Z; each hosted CLI gets its own window");
        }

        var sessions = Sessions();
        var since = launched.ToDictionary(p => p.Key, p => p.Value.AtUtc, StringComparer.OrdinalIgnoreCase);
        var need = NeedingWindow(sessions, HasViewer, since, dismissed, nowUtc, LaunchGrace)
            .Where(s => !crashes.TryGetValue(s.Id, out var c) || nowUtc >= c.RetryAfterUtc).ToList();
        if (need.Count == 0) return 0;
        if (Exe(nowUtc) is not { } path) return 0;
        var started = 0;
        foreach (var session in need)
        {
            try
            {
                var process = Launch(path, session);
                if (launched.Remove(session.Id, out var old)) old.Process?.Dispose();
                launched[session.Id] = new Launched(nowUtc, process);
                started++;
                Tell(LogEventLevel.Information, $"fallback viewers: opened a window for {session.Title ?? session.Id} ({session.Id})");
            }
            catch (Exception ex)
            {
                // Remembered all the same, so a viewer that can't start isn't retried every second.
                launched[session.Id] = new Launched(nowUtc, null);
                log.Error(ex, "Starting a TermViewer for {Id:l} failed", session.Id);
            }
        }
        return started;
    }

    /// <summary>Drops the viewers that exited: one that exited 0 (closed by the person, or folded) is left closed this time round;
    /// one that crashed opens again after a growing wait (M.ORCH-T974), and after <see cref="MaxCrashes"/> in a row not at all
    /// this time round, said once.</summary>
    private void Reap(DateTime nowUtc)
    {
        foreach (var (id, entry) in launched.ToList())
        {
            if (entry.Process is not { } p) continue;
            try
            {
                if (!p.HasExited) continue;
                if (p.ExitCode == 0) dismissed.Add(id);
                else
                {
                    var count = crashes.TryGetValue(id, out var before) && nowUtc - entry.AtUtc < StableRun ? before.Count + 1 : 1;
                    if (count >= MaxCrashes)
                    {
                        crashes.Remove(id);
                        dismissed.Add(id);
                        Tell(LogEventLevel.Error, $"fallback viewers: the window for {id} crashed {count} times in a row (last exit {p.ExitCode}); it isn't reopened until the dashboard has been back");
                    }
                    else
                    {
                        var wait = CrashDelay(count);
                        crashes[id] = new Crashes(count, nowUtc + wait);
                        Tell(LogEventLevel.Warning, $"fallback viewers: the window for {id} exited with {p.ExitCode}; it opens again in {wait.TotalSeconds:0}s");
                    }
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
            p.Dispose();
            launched.Remove(id);
        }
    }

    private void Forget()
    {
        foreach (var entry in launched.Values) entry.Process?.Dispose();
        launched.Clear();
    }

    private string? Exe(DateTime nowUtc)
    {
        if (exe is not null && File.Exists(exe)) return exe;
        if (nowUtc - exeResolvedUtc < TimeSpan.FromMinutes(1)) return exe;
        exeResolvedUtc = nowUtc;
        try { exe = ResolveExe(); }
        catch (Exception ex) { log.Error(ex, "Finding the TermViewer failed"); exe = null; }
        if (exe is null && !saidMissing)
        {
            saidMissing = true;
            Tell(LogEventLevel.Warning, $"fallback viewers: no {ExeName} found (run.ps1 installs it under {FolderName}\\<version>\\); the hosted CLIs keep running without a window");
        }
        return exe;
    }

    /// <summary>The TermViewer to start: <c>%MINDATTIC_TERMVIEWER%</c>, then the newest <c>TermViewer\&lt;version&gt;\</c> under the install
    /// folder (<see cref="TermViewerInstalls"/>, as TermHost and the Gateway), then a plain <c>TermViewer\</c> there, then one beside
    /// this app (for a dev build). Null when none exists.</summary>
    public static string? DefaultExe()
    {
        var candidates = new List<string?> { Environment.GetEnvironmentVariable(ExeVariable) };
        try
        {
            var installDir = new Installer().InstallDir;
            candidates.Add(TermViewerInstalls.ResolveExe(installDir));
            candidates.Add(Path.Combine(installDir, FolderName, ExeName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException) { }
        candidates.Add(Path.Combine(AppContext.BaseDirectory, FolderName, ExeName));
        candidates.Add(Path.Combine(AppContext.BaseDirectory, ExeName));
        return candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c) && File.Exists(c));
    }

    /// <summary>Starts a viewer of <paramref name="session"/>: <c>TermViewer --session &lt;id&gt;</c>.</summary>
    public static Process? StartViewer(string exe, TermSessionRecord session)
    {
        var psi = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe) ?? "" };
        psi.ArgumentList.Add("--session");
        psi.ArgumentList.Add(session.Id);
        return Process.Start(psi);
    }

    /// <summary>A pop-out window on <paramref name="session"/> beside the dashboard's tab (M.ORCH-T931): <c>TermViewer --session
    /// &lt;id&gt; --stay [--watch]</c>. It never folds back while the dashboard is up, since the person asked for it. Never throws;
    /// returns null (and says why) when no TermViewer is installed or it couldn't start.</summary>
    public static Process? PopOut(TermSessionRecord session, bool watchOnly)
    {
        try
        {
            if (DefaultExe() is not { } exe) return null;
            var psi = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe) ?? "" };
            psi.ArgumentList.Add("--session");
            psi.ArgumentList.Add(session.Id);
            psi.ArgumentList.Add("--stay");
            if (watchOnly) psi.ArgumentList.Add("--watch");
            return Process.Start(psi);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            OrchestratorLog.For("FallbackViewers").Error(ex, "Popping out {Id:l} failed; contained", session.Id);
            return null;
        }
    }

    private void Tell(LogEventLevel level, string line)
    {
        try
        {
            if (Say is { } say) say(level, line);
            else log.Write(level, "{Line:l}", line);
        }
        catch (Exception) { }
    }
}
