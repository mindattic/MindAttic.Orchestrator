using System.Globalization;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Activity;

/// <summary>How short the machine is of RAM, worst last; each level does what the one before it does and more.</summary>
public enum MemoryLevel
{
    Ok,
    /// <summary>Nothing new starts (subagents, CLIs, main-line tests); running work finishes.</summary>
    Tight,
    /// <summary>The newest /allocate subagent is shed each minute and its ticket requeued.</summary>
    Critical,
    /// <summary>Every tool call is refused so turns end, until memory recovers.</summary>
    Emergency,
}

/// <summary>Free physical RAM, and commit charge as a percent of the commit limit; the commit is only
/// <paramref name="CommitCounted"/> while the pagefile has a fixed size (a system-managed one moves the limit as it grows).
/// <paramref name="UsedPercent"/> is physical RAM in use (Windows' memory load); unknown reads as 100, so the guard stays awake.</summary>
public sealed record MemoryReading(double FreeGb, double CommitPercent, bool CommitCounted, bool Simulated = false, double UsedPercent = 100)
{
    /// <summary>Under <see cref="OrchestratorSettings.MemoryGuardStartPercent"/> of RAM in use the guard is dormant (M.ORCH-T1591):
    /// no level, no subagent cap; it only scales work back near the edge.</summary>
    public bool Dormant(OrchestratorSettings s) => UsedPercent < s.MemoryGuardStartPercent;

    public string Describe() =>
        $"{FreeGb.ToString("0.0", CultureInfo.InvariantCulture)} GB RAM free" +
        (CommitCounted ? $", commit {CommitPercent.ToString("0", CultureInfo.InvariantCulture)}%" : "") +
        (Simulated ? " (simulated)" : "");
}

/// <summary>The guard's judgment: its level, the reading it was judged on, and why in a few words.</summary>
public sealed record MemoryState(MemoryLevel Level, MemoryReading Reading, string Why)
{
    public string Key => Level.ToString().ToLowerInvariant();

    /// <summary>The settings' free-RAM lines it was judged against, for the hooks' own live check.</summary>
    public MemoryLines Lines { get; init; } = MemoryLines.Of(new OrchestratorSettings());
}

/// <summary>The free-RAM lines (GB) for each level, what one more subagent is assumed to take, the matching commit-charge
/// percent lines, and the margin a reading must clear by (GB and commit points) before stepping back down.</summary>
public sealed record MemoryLines(double TightGb, double CriticalGb, double EmergencyGb, double PerSubagentGb,
    double TightCommitPercent, double CriticalCommitPercent, double EmergencyCommitPercent,
    double ResumeMarginGb, double ResumeMarginCommit, double StartPercent)
{
    public static MemoryLines Of(OrchestratorSettings s) => new(s.MemoryTightGb, s.MemoryCriticalGb, s.MemoryEmergencyGb, s.MemoryPerSubagentGb,
        s.MemoryTightCommitPercent, s.MemoryCriticalCommitPercent, s.MemoryEmergencyCommitPercent,
        s.MemoryResumeMarginGb, s.MemoryResumeMarginCommit, s.MemoryGuardStartPercent);
}

/// <summary>
/// The memory guard (M.ORCH-T1494). Out of RAM, Windows pages to a pagefile that, system-managed, grows until the disk is
/// full and only a hard restart recovers, losing everything in flight. So low RAM scales work back the way the 5-hour and
/// weekly stops do: <see cref="MemoryLevel.Tight"/> starts nothing new and lets running work finish,
/// <see cref="MemoryLevel.Critical"/> sheds the newest subagent, <see cref="MemoryLevel.Emergency"/> refuses tool calls.
/// <para>This reverses M.ORCH-T357/T374, which kept machine health alert-only because a hold judged fresh every sample
/// flapped and /allocate refilled the moment it read normal. Here a level is entered at once but left only one step at a time,
/// once clear of the line by <see cref="OrchestratorSettings.MemoryResumeMarginGb"/> (and
/// <see cref="OrchestratorSettings.MemoryResumeMarginCommit"/> points) for
/// <see cref="SettleFor"/>: at the defaults new work resumes at 6 GB free, not 4.</para>
/// </summary>
public sealed class MemoryGuard
{
    /// <summary>How long a reading must stay clear before each step down.</summary>
    public static readonly TimeSpan SettleFor = TimeSpan.FromSeconds(60);

    /// <summary>How often the machine is read; readings in between reuse the last.</summary>
    public static readonly TimeSpan SampleEvery = TimeSpan.FromSeconds(5);

    /// <summary>A simulated free-RAM figure (GB) in this file stands in for the real one while the file is under
    /// <see cref="SimulateFor"/> old, for trying the guard out live; it lapses by itself so it can't be left on.</summary>
    public static string SimulatePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MindAttic", "Orchestrator", "memory-simulate.txt");

    public static readonly TimeSpan SimulateFor = TimeSpan.FromMinutes(30);

    /// <summary>The guard this process judges by: the daemon's carries the hysteresis; a short-lived CLI's reads raw.</summary>
    public static MemoryGuard Shared { get; internal set; } = new();

    /// <summary>Reads the machine; tests stand in their own.</summary>
    internal Func<MemoryReading?> Read { get; init; } = ReadMachine;

    private readonly Lock gate = new();
    private MemoryState? last;
    private DateTime sampledUtc;
    private DateTime? clearSinceUtc;

    /// <summary>The level now, read at most every <see cref="SampleEvery"/>; null while the guard is off or memory can't be read.</summary>
    public MemoryState? Observe(OrchestratorSettings s, DateTime nowUtc)
    {
        if (!s.MemoryGuard) return null;
        lock (gate)
        {
            if (last is not null && nowUtc - sampledUtc < SampleEvery && nowUtc >= sampledUtc) return last;
            if (Read() is not { } reading) return last;
            sampledUtc = nowUtc;
            return last = Step(last?.Level ?? MemoryLevel.Ok, reading, s, nowUtc) with { Lines = MemoryLines.Of(s) };
        }
    }

    /// <summary>The last level judged, without reading again; null before the first.</summary>
    public MemoryState? Last { get { lock (gate) return last; } }

    /// <summary>One judgment from <paramref name="previous"/>: up at once, down a step at a time once clear by the margin for
    /// <see cref="SettleFor"/>.</summary>
    internal MemoryState Step(MemoryLevel previous, MemoryReading r, OrchestratorSettings s, DateTime nowUtc)
    {
        var (raw, why) = Judge(r, s, margin: false);
        if (raw >= previous)
        {
            clearSinceUtc = null;
            return new(raw, r, why);
        }
        var (held, heldWhy) = Judge(r, s, margin: true);
        if (held >= previous)
        {
            clearSinceUtc = null;
            return new(previous, r, $"recovering, not yet clear: {heldWhy}");
        }
        clearSinceUtc ??= nowUtc;
        if (nowUtc - clearSinceUtc < SettleFor) return new(previous, r, $"recovering: {r.Describe()}");
        var next = (MemoryLevel)Math.Max((int)held, (int)previous - 1);
        // Each further step down waits its own settle.
        clearSinceUtc = next == MemoryLevel.Ok ? null : nowUtc;
        return new(next, r, next == MemoryLevel.Ok ? r.Describe() : $"recovering: {r.Describe()}");
    }

    /// <summary>The level <paramref name="r"/> reads at on its own, with the step-down margin or without.</summary>
    public static (MemoryLevel Level, string Why) Judge(MemoryReading r, OrchestratorSettings s, bool margin)
    {
        if (r.Dormant(s)) return (MemoryLevel.Ok, r.Describe());
        var (gb, pts) = margin ? (s.MemoryResumeMarginGb, s.MemoryResumeMarginCommit) : (0.0, 0.0);
        bool Under(double line, double commitLine) => r.FreeGb < line + gb || (r.CommitCounted && r.CommitPercent >= commitLine - pts);
        var level = Under(s.MemoryEmergencyGb, s.MemoryEmergencyCommitPercent) ? MemoryLevel.Emergency
            : Under(s.MemoryCriticalGb, s.MemoryCriticalCommitPercent) ? MemoryLevel.Critical
            : Under(s.MemoryTightGb, s.MemoryTightCommitPercent) ? MemoryLevel.Tight
            : MemoryLevel.Ok;
        return (level, r.Describe());
    }

    /// <summary>How many more subagents fit in free RAM before the hold line, at <see cref="OrchestratorSettings.MemoryPerSubagentGb"/> each.</summary>
    public static int Fits(MemoryReading r, OrchestratorSettings s) =>
        (int)Math.Clamp(Math.Floor((r.FreeGb - s.MemoryTightGb) / Math.Max(0.1, s.MemoryPerSubagentGb)), 0, 1000);

    /// <summary>Why a new CLI mayn't start now (M.ORCH-T1496), or null: short of RAM a new one (each holds 0.5-1 GB, more with
    /// its subagents) is what tips the machine into paging.</summary>
    public static string? StartHeld(OrchestratorSettings s) =>
        Shared.Observe(s, DateTime.UtcNow) is { Level: > MemoryLevel.Ok } m
            ? $"RAM is low ({m.Reading.Describe()}): no new CLI starts until it recovers, so the machine doesn't page itself to a full disk."
            : null;

    /// <summary>What an agent or a refused start is told at <paramref name="m"/>'s level; empty at <see cref="MemoryLevel.Ok"/>.</summary>
    public static string Note(MemoryState m, int running) => m.Level switch
    {
        MemoryLevel.Emergency => $"RAM emergency ({m.Reading.Describe()}): tool calls are refused until memory recovers, so the machine " +
            "doesn't page itself to a full disk. End your turn now with one short line saying where you stopped; your uncommitted work stays where it is.",
        MemoryLevel.Critical => $"Subagents: none new: RAM is critically low ({m.Reading.Describe()}). The newest /allocate subagent is " +
            $"stopped each minute until it recovers. Finish with the {running} running; start nothing heavy (builds, test runs) you can avoid.",
        MemoryLevel.Tight => $"Subagents: none new: RAM is low ({m.Reading.Describe()}). Finish with the {running} running; " +
            "new subagents wait until memory recovers.",
        _ => "",
    };

    private static DateTime unboundedReadUtc = DateTime.MinValue;
    private static bool unbounded;

    private static MemoryReading? ReadMachine()
    {
        var now = DateTime.UtcNow;
        if (now - unboundedReadUtc > TimeSpan.FromMinutes(10))
        {
            unbounded = MachineHealth.PagefileUnbounded();
            unboundedReadUtc = now;
        }
        if (Simulated(now) is { } fake) return new MemoryReading(fake, 0, CommitCounted: false, Simulated: true);
        return MachineHealth.Memory() is { } m ? new MemoryReading(m.FreeGb, m.CommitPercent, CommitCounted: !unbounded, UsedPercent: m.UsedPercent) : null;
    }

    /// <summary>The simulated free RAM in <see cref="SimulatePath"/>, while it's fresh; null otherwise.</summary>
    internal static double? Simulated(DateTime nowUtc)
    {
        try
        {
            var info = new FileInfo(SimulatePath);
            if (!info.Exists || nowUtc - info.LastWriteTimeUtc > SimulateFor) return null;
            return double.TryParse(File.ReadAllText(info.FullName).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var gb) && gb >= 0 ? gb : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }
}
