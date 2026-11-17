using System.Text.Json.Serialization;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Activity;

/// <summary>New work is held for disk space: the fullest watched drive, the floor it's under and the free space that lifts the hold.</summary>
public sealed record DiskHold(DriveFree Drive, double FloorGb, double ResumeGb)
{
    /// <summary>GB still to free before new work starts again.</summary>
    [JsonIgnore]
    public double NeededGb => Math.Max(0, ResumeGb - Drive.FreeGb);

    /// <summary>The drive and its floor, short: for a log line or a held test run.</summary>
    [JsonIgnore]
    public string Short => $"disk is nearly full ({Drive.Root} has {Drive.FreeGb:0.0} GB free, under the {FloorGb:0.#} GB floor)";

    /// <summary>Why new work is held and how much space lifts it, as a refused start or an empty hand-out says it.</summary>
    [JsonIgnore]
    public string Why => $"Disk is nearly full: {Drive.Root} has {Drive.FreeGb:0.0} GB free, under the {FloorGb:0.#} GB floor (settings --disk-hold-gb), " +
        $"and a self-cleanup didn't get it back. Nothing new starts until {ResumeGb:0.#} GB is free there: free {Math.Max(0.1, Math.Round(NeededGb, 1)):0.#} GB more. " +
        "Running work carries on";
}

/// <summary>
/// The hard disk floor (M.ORCH-T1679). Low disk used to alert only (<see cref="DiskWatch"/>) and prune ended sessions' temp
/// (<see cref="DiskCleanup"/>), so enough queued work could still fill the drive. Under
/// <see cref="OrchestratorSettings.DiskHoldGb"/> free on the system drive or the worktree root's (the drives
/// <see cref="DiskWatch"/> reads), the same prune runs first; only if the drive is still under the floor is new work held:
/// /allocate hands out nothing, no new CLI opens, and the daemon's main-line tests and branch builds wait. Running work is
/// never stopped or killed for disk. The hold lifts by itself once the drive is
/// <see cref="OrchestratorSettings.DiskHoldResumeMarginGb"/> clear of the floor, so one hovering on it doesn't flap.
/// It's an exhaustion floor, not a throttle: over the floor it holds nothing and caps nothing.
/// </summary>
public sealed class DiskGuard
{
    /// <summary>The guard this process judges by: the daemon's carries the hysteresis; a short-lived CLI's reads raw, told
    /// by its caller whether the daemon already holds.</summary>
    public static DiskGuard Shared { get; internal set; } = new() { Reclaim = Prune };

    /// <summary>Reads free space on the drives to watch; a test's stands in for the real drives.</summary>
    internal Func<OrchestratorSettings, IReadOnlyList<DriveFree>> Read { get; init; } =
        s => DiskWatch.Read(DiskWatch.Roots(s.WorktreeRoot));

    /// <summary>Gets space back before a hold: returns a line on what it did, or null when it had nothing to do. Only
    /// <see cref="Shared"/> carries one, so a test's guard never deletes from the machine it runs on.</summary>
    internal Func<OrchestratorSettings, DateTime, string?>? Reclaim { get; init; }

    private readonly Lock gate = new();
    private DiskHold? last;
    private DateTime? reclaimedUtc;
    private string? lastReclaim;

    /// <summary>The hold last judged, without reading again; null while nothing is held.</summary>
    public DiskHold? Last => Volatile.Read(ref last);

    /// <summary>What the last reclaim said it did; null before the first, or when it had nothing to do.</summary>
    public string? LastReclaim { get { lock (gate) return lastReclaim; } }

    /// <summary>Whether new work is held at <paramref name="freeGb"/>: under the floor, or, once held, until clear of it by
    /// the margin. A floor of 0 never holds.</summary>
    public static bool Held(double freeGb, double floorGb, double resumeMarginGb, bool wasHeld) =>
        floorGb > 0 && (freeGb < floorGb || (wasHeld && freeGb < floorGb + Math.Max(0, resumeMarginGb)));

    /// <summary>The hold now, or null: reads the drives (cheap), and under the floor runs <see cref="Reclaim"/> first (at most
    /// every <see cref="OrchestratorSettings.DiskCleanupEveryMinutes"/>) and reads again. <paramref name="heldElsewhere"/>
    /// says the daemon's judgment already holds, so this process applies the resume margin too. Drives that can't be read
    /// leave the last judgment as it was.</summary>
    public DiskHold? Observe(OrchestratorSettings s, DateTime nowUtc, bool heldElsewhere = false)
    {
        lock (gate)
        {
            if (s.DiskHoldGb <= 0) return Set(null);
            var wasHeld = last is not null || heldElsewhere;
            if (Lowest(s) is not { } drive) return last;
            if (!Held(drive.FreeGb, s.DiskHoldGb, s.DiskHoldResumeMarginGb, wasHeld)) return Set(null);
            // Under the floor itself (not merely inside the margin on the way back up): get space back first, then look again.
            if (drive.FreeGb < s.DiskHoldGb && Reclaim is { } reclaim && ReclaimDue(s, nowUtc))
            {
                reclaimedUtc = nowUtc;
                try { lastReclaim = reclaim(s, nowUtc); }
                catch (Exception ex) when (ex is not OutOfMemoryException) { lastReclaim = $"self-cleanup failed: {ex.Message}"; }
                drive = Lowest(s) ?? drive;
                if (!Held(drive.FreeGb, s.DiskHoldGb, s.DiskHoldResumeMarginGb, wasHeld)) return Set(null);
            }
            return Set(new DiskHold(drive, s.DiskHoldGb, s.DiskHoldGb + Math.Max(0, s.DiskHoldResumeMarginGb)));
        }
    }

    private DiskHold? Set(DiskHold? hold)
    {
        Volatile.Write(ref last, hold);
        return hold;
    }

    private DriveFree? Lowest(OrchestratorSettings s) => Read(s) is { Count: > 0 } drives ? drives.MinBy(d => d.FreeGb) : null;

    private bool ReclaimDue(OrchestratorSettings s, DateTime nowUtc) =>
        reclaimedUtc is not { } at || nowUtc < at || nowUtc - at >= OrchestratorSettings.SecondsSpan(s.DiskCleanupEveryMinutes * 60);

    /// <summary>Why new work mayn't start now, or null: the disk is under its floor even after a self-cleanup.</summary>
    public static string? StartHeld(OrchestratorSettings s) => Shared.Observe(s, DateTime.UtcNow)?.Why;

    /// <summary>Where the last reclaim's time is kept for every process: a refused start in one CLI after another doesn't walk
    /// %TEMP% again each time.</summary>
    internal static string StampPath => Path.Combine(Path.GetDirectoryName(DiskCleanup.LogPath) ?? ".", "disk-hold-reclaim.stamp");

    /// <summary>The same prune the daemon's disk self-cleanup runs (<see cref="DiskCleanup.Run(SessionLiveness, bool, string?, string?, out TempPruneResult, double)"/>):
    /// ended sessions' %TEMP%\claude folders, by liveness. One run at a time across processes (the disk-cleanup command's
    /// mutex), and none while another process ran one within <see cref="OrchestratorSettings.DiskCleanupEveryMinutes"/>.</summary>
    internal static string? Prune(OrchestratorSettings s, DateTime nowUtc)
    {
        var every = OrchestratorSettings.SecondsSpan(s.DiskCleanupEveryMinutes * 60);
        try
        {
            if (File.Exists(StampPath) && File.GetLastWriteTimeUtc(StampPath) is var at && nowUtc >= at && nowUtc - at < every) return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        using var mutex = new Mutex(false, @"Global\MindAttic.Orchestrator.DiskCleanup");
        try { if (!mutex.WaitOne(0)) return null; }
        catch (AbandonedMutexException) { }
        try
        {
            // A stamp that can't be written (the disk is full) only means the next process prunes again.
            try { File.WriteAllText(StampPath, ""); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            var r = DiskCleanup.Run(SessionLiveness.Current(OrchestratorOptions.Load().WorkspaceRoot), dryRun: false, DiskCleanup.LogPath, null, out _, s.TempRetentionHours);
            return DiskCleanup.Describe(r);
        }
        finally { mutex.ReleaseMutex(); }
    }
}
