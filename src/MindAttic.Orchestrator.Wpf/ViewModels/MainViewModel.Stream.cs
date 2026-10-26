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
        if (!connected) streamBuilds = false;
        Raise(nameof(IsStreamLive));
    }

    /// <summary>
    /// Applies one message from the stream on the UI thread: its readings at once to every tab they're for. Returns whether the
    /// caller should refresh now: a snapshot newer than the one shown was published, or a login signed in or out.
    /// </summary>
    public bool ApplyStatus(StatusMessage message)
    {
        if (root is not null) return root.ApplyStatus(message);
        // Every message says when its daemon last built a snapshot: one that hasn't lately (another process holds the lease)
        // pushes none, so the poll mustn't slow down for it.
        var builds = message.SnapshotTakenUtc is { } built && message.AtUtc - built < 2 * SnapshotFile.MaxAge;
        // This process's own probe readings (it holds the lease) say nothing about the stream.
        if (message.Pid != Environment.ProcessId && builds != streamBuilds)
        {
            streamBuilds = builds;
            Raise(nameof(IsStreamLive));
        }
        var refresh = message.SnapshotTakenUtc is { } taken && (workspace is null || taken > workspace.TakenUtc);
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
