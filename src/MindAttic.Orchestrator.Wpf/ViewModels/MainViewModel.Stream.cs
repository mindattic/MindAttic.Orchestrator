using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Status;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// The status stream's end in the view models (M.ORCH-T854): each login's reading of Claude's meter, pushed by the daemon the
/// moment it lands, is laid over the figures the last snapshot gave, so the 5-hour and weekly tiles and the tab strip's
/// "5hr | wk" show what's real now rather than what the last poll saw. A new snapshot pushed asks for a refresh at once.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>Every login's latest pushed reading, by <see cref="MeterReading.Key"/>; kept on the global login's tab.</summary>
    private readonly Dictionary<string, MeterReading> liveReadings = new(StringComparer.Ordinal);

    private bool streamConnected, streamBuilds;

    /// <summary>Whether the status stream is connected to a daemon that builds the snapshots (holds the status lease), so every
    /// new one is pushed; on the global login's tab. Only then can the poll slow down.</summary>
    public bool IsStreamLive => Root.streamLive;

    private bool streamLive => streamConnected && streamBuilds;

    /// <summary>Says the stream came up or went down.</summary>
    public void SetStreamLive(bool connected)
    {
        if (root is not null) { root.SetStreamLive(connected); return; }
        streamConnected = connected;
        if (!connected)
        {
            streamBuilds = figuresSeen = false;
            ForgetPushedState();
        }
        Raise(nameof(IsStreamLive));
    }

    /// <summary>Notes what a message says of its writer: every message says when its daemon last built a snapshot, and one that
    /// hasn't lately (another process holds the lease) pushes none, so the poll mustn't slow down for it.</summary>
    private void NoteWriter(DateTime atUtc, int pid, DateTime? snapshotTakenUtc)
    {
        var builds = snapshotTakenUtc is { } built && atUtc - built < 2 * SnapshotFile.MaxAge;
        // This process's own probe readings (it holds the lease) say nothing about the stream.
        if (pid == Environment.ProcessId || builds == streamBuilds) return;
        streamBuilds = builds;
        Raise(nameof(IsStreamLive));
    }

    // M.ORCH-T872: whether this connection has pushed a snapshot's figures; the digest of the rest of the snapshot the stream
    // last said, and of the one shown; and what the last read gave besides the snapshot, which pushed figures are applied with.
    private bool figuresSeen;
    private string? restWanted, restShown;
    private IReadOnlyList<Member> lastMembers = [];
    private OrgDirectory? lastOrgs;

    /// <summary>
    /// Lays a snapshot's pushed figures (M.ORCH-T872) over the snapshot shown, on the UI thread: pace, budget, the graph, each
    /// login's meter and pace, the agents and CLIs, billing, moves, counts and warnings go straight to every tab, with no read of
    /// the published file. Returns whether the caller should read the file now instead: the rest of the snapshot (the repos'
    /// sessions, tabs and tickets, the queue) moved, or there's nothing read yet to lay them over. Hidden, nothing is applied
    /// (shown again, the window reads the file at once). Older figures than those shown change nothing.
    /// </summary>
    public async Task<bool> ApplyFiguresAsync(SnapshotFigures figures, bool hidden)
    {
        if (root is not null) return await root.ApplyFiguresAsync(figures, hidden);
        figuresSeen = true;
        if (workspace is not { } shown || figures.TakenUtc <= shown.TakenUtc) return false;
        restWanted = figures.Rest;
        if (hidden) return false;
        if (refreshing || lastOrgs is null || restShown is null || restShown != figures.Rest) return true;
        var patched = aggregator.WithLiveBilling(figures.Over(shown), DateTime.UtcNow);
        var dirs = OwnLoginDirs();
        var members = lastMembers;
        // Each login's tab and its signed-in state, off the UI thread, as a read does it.
        var tabs = await Task.Run(() => LoginScope.Tabs(patched, dirs, members));
        // A read that landed meanwhile showed something as new or newer.
        if (refreshing || workspace is not { } now || now.TakenUtc >= patched.TakenUtc) return false;
        ApplyTabs(patched, tabs, lastOrgs);
        return false;
    }

    /// <summary>
    /// Applies one message from the stream on the UI thread: its readings at once to every tab they're for. Returns whether the
    /// caller should refresh now: a snapshot newer than the one shown was published, or a login signed in or out.
    /// </summary>
    public bool ApplyStatus(StatusMessage message)
    {
        if (root is not null) return root.ApplyStatus(message);
        NoteWriter(message.AtUtc, message.Pid, message.SnapshotTakenUtc);
        // Only a notice of a snapshot whose figures weren't pushed asks for a read of the file (M.ORCH-T872): the figures say
        // themselves when the rest of it moved (ApplyFiguresAsync).
        var refresh = message.SnapshotTakenUtc is { } taken && (workspace is null || taken > workspace.TakenUtc)
            && (message.Kind == global::MindAttic.Orchestrator.Status.StatusMessage.Snapshot || !figuresSeen);
        foreach (var r in message.Readings)
        {
            var was = liveReadings.GetValueOrDefault(r.Key);
            if (was?.SignedIn is { } before && r.SignedIn is { } now && before != now) refresh = true;
            liveReadings[r.Key] = MeterReading.Merge(was, r);
        }
        if (message.Readings.Count > 0)
            foreach (var t in LoginTabs) t.RaiseLiveNumbers();
        return refresh;
    }

    /// <summary>This tab's login's pushed reading, while it's newer than the snapshot shown (the next snapshot, built after it,
    /// already counts it); null otherwise, and on the 'API key' tab.</summary>
    private MeterReading? Live
    {
        get
        {
            if (tab.IsApiKey || !Root.liveReadings.TryGetValue(MeterReading.KeyOf(ScopeEmail), out var r)) return null;
            return snapshot is null || r.ReadUtc > snapshot.TakenUtc ? r : null;
        }
    }

    /// <summary>The 5-hour window's % used from the pushed reading; null when there's none newer than the snapshot.</summary>
    private double? LiveFiveHour => Live is { } l
        ? LiveWindow(l.FiveHourPercent, l.FiveHourResetUtc, SnapshotFiveHour, Budget?.CurrentBlock?.EndUtc ?? OwnRow?.FiveHourResetAt(DateTime.UtcNow), DateTime.UtcNow)
        : null;

    /// <summary>The week's % used from the pushed reading; null when there's none newer than the snapshot.</summary>
    private double? LiveWeek => Live is { } l
        ? LiveWindow(l.WeekPercent, l.WeekResetUtc, SnapshotWeek, Budget?.WeekResetUtc ?? OwnRow?.WeekResetAt(DateTime.UtcNow), DateTime.UtcNow)
        : null;

    /// <summary>
    /// A pushed window's % used: none once its window has reset (the snapshot's says what follows); otherwise the reading, or the
    /// snapshot's figure for the same window when that's higher (usage never falls within a window, so the higher is the truer).
    /// Full precision: only the tiles round it.
    /// </summary>
    internal static double? LiveWindow(double? livePercent, DateTime? liveResetUtc, double? shown, DateTime? shownResetUtc, DateTime nowUtc)
    {
        if (livePercent is not { } p || liveResetUtc is not { } reset || reset <= nowUtc) return null;
        return shown is { } s && MeterReading.SameWindow(shownResetUtc, reset) ? Math.Max(p, s) : p;
    }

    /// <summary>Raises every figure a pushed reading changes: the stat tiles and the tab strip.</summary>
    private void RaiseLiveNumbers()
    {
        RaiseOwnNumbers();
        Raise(nameof(BudgetFill));
        RaiseTabStatus();
    }
}
