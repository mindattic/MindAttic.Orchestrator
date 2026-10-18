using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MindAttic.Orchestrator.Activity;

public enum HealthLevel { Ok, Warn, Danger }

/// <summary>One process holding a lot of kernel handles.</summary>
public sealed record HandleHog(int Pid, string Name, int Handles);

/// <summary>One process using a lot of memory (private bytes).</summary>
public sealed record MemoryHog(int Pid, string Name, double Gb);

/// <summary>A snapshot of what runs a desktop out of resources before it freezes.</summary>
public sealed record HealthSample(long TotalHandles, double CommitPercent, double FreeRamGb, IReadOnlyList<HandleHog> Top)
{
    /// <summary>The processes using the most memory, for when memory is what's short.</summary>
    public IReadOnlyList<MemoryHog> TopMemory { get; init; } = [];

    /// <summary>Memory every Claude process holds together (the agents are usually what fills commit).</summary>
    public double ClaudeGb { get; init; }

    /// <summary>The worst offenders for <paramref name="why"/>: by memory when memory is short, else by handles.</summary>
    public string Offenders(string? why) =>
        why is not null && (why.Contains("memory", StringComparison.Ordinal) || why.Contains("RAM", StringComparison.Ordinal)) && TopMemory.Count > 0
            ? "most memory: " + string.Join(", ", TopMemory.Select(m => $"{m.Name} {m.Gb:0.0} GB")) + (ClaudeGb > 0 ? $"; Claude processes {ClaudeGb:0.0} GB in all" : "")
            : "most handles: " + string.Join(", ", Top.Select(h => $"{h.Name} {h.Handles:N0}"));
}

/// <summary>
/// Watches the machine the agents run on. Found live: two orphaned <c>find /</c> searches held 660k handles and the
/// desktop froze while CPU sat idle, so CPU says nothing; kernel handles, commit charge and free RAM do. Past the warn
/// line, and again past the danger line, you're alerted with the worst offenders. Nothing is held for it: <c>orchestrator
/// next</c>, which /autopilot CLIs use, never reads it (M.ORCH-T357), and heavy load is the point. Nothing is killed
/// for it either (the <see cref="Sessions.ProcessReaper"/> kills runaway agent-spawned processes on its own rules).
/// M.ORCH-T374 looked for a crash a hold would have prevented and found none. The 660k-handle freeze is the only wedge
/// on record, and its cause is stopped at the source (the pace gate refuses whole-drive searches, the reaper kills
/// orphaned and runaway tool processes); no handle warning has fired since. Commit hit 97-100% often under full load
/// (lghub_agent and GTFO held most of it) and no agent tool call or Claude CLI failed for memory.
/// </summary>
public static partial class MachineHealth
{
    /// <summary>Handle counts that are normal for the kernel itself, not a leak.</summary>
    private static readonly HashSet<int> Exempt = [0, 4];

    public static HealthSample Sample()
    {
        var hogs = new List<HandleHog>();
        var memory = new List<MemoryHog>();
        long total = 0;
        double claude = 0;
        const double Gb = 1024.0 * 1024 * 1024;
        foreach (var p in Process.GetProcesses())
            using (p)
            {
                try
                {
                    var h = p.HandleCount;
                    total += h;
                    if (Exempt.Contains(p.Id)) continue;
                    hogs.Add(new HandleHog(p.Id, p.ProcessName, h));
                    var gb = p.PrivateMemorySize64 / Gb;
                    memory.Add(new MemoryHog(p.Id, p.ProcessName, gb));
                    if (p.ProcessName.StartsWith("claude", StringComparison.OrdinalIgnoreCase)) claude += gb;
                }
                catch (InvalidOperationException) { } // exited while we looked
                catch (System.ComponentModel.Win32Exception) { } // another user's or a protected process
            }

        var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        var commit = 0.0;
        var freeGb = double.MaxValue;
        if (GlobalMemoryStatusEx(ref mem))
        {
            commit = mem.ullTotalPageFile == 0 ? 0 : 100.0 * (mem.ullTotalPageFile - mem.ullAvailPageFile) / mem.ullTotalPageFile;
            freeGb = mem.ullAvailPhys / (1024.0 * 1024 * 1024);
        }
        return new HealthSample(total, commit, freeGb, [.. hogs.OrderByDescending(x => x.Handles).Take(3)])
        {
            TopMemory = [.. memory.OrderByDescending(x => x.Gb).Take(3)], ClaudeGb = claude,
        };
    }

    /// <summary>
    /// How the machine is doing, and in a sentence why. Coming down from <paramref name="previous"/> it steps one level
    /// at a time and only once clear of the line by a margin (commit 5 points under it, 1 GB more free, a fifth fewer
    /// handles): found live, judged fresh every sample it flipped between danger and normal every 30-90s as agents came
    /// and went, and autopilot refilled the moment it read normal.
    /// </summary>
    public static (HealthLevel Level, string? Why) Judge(HealthSample s, HealthLevel previous = HealthLevel.Ok)
    {
        var (level, why) = Raw(s, margin: false);
        if (level >= previous) return (level, why);
        var (held, heldWhy) = Raw(s, margin: true);
        var settled = (HealthLevel)Math.Max((int)level, Math.Max((int)held, (int)previous - 1));
        return settled == level ? (level, why) : (settled, heldWhy ?? why ?? $"recovering from {previous.ToString().ToLowerInvariant()}");
    }

    private static (HealthLevel Level, string? Why) Raw(HealthSample s, bool margin)
    {
        var (commitPad, freePad, handleShare) = margin ? (5.0, 1.0, 0.8) : (0.0, 0.0, 1.0);
        var top = s.Top.FirstOrDefault();
        var hog = top is null ? "" : $"{top.Name} (pid {top.Pid}) holds {top.Handles:N0} handles";
        var reasons = new List<string>();
        var level = HealthLevel.Ok;
        void Mark(HealthLevel l, string why) { if (l > level) level = l; reasons.Add(why); }

        if (top is not null && top.Handles >= 250_000 * handleShare) Mark(HealthLevel.Danger, hog);
        else if (top is not null && top.Handles >= 100_000 * handleShare) Mark(HealthLevel.Warn, hog);
        if (s.TotalHandles >= 1_500_000 * handleShare) Mark(HealthLevel.Warn, $"{s.TotalHandles:N0} handles open in all");
        if (s.CommitPercent >= 95 - commitPad) Mark(HealthLevel.Danger, $"memory commit at {s.CommitPercent:0}%");
        else if (s.CommitPercent >= 88 - commitPad) Mark(HealthLevel.Warn, $"memory commit at {s.CommitPercent:0}%");
        if (s.FreeRamGb < 0.75 + freePad) Mark(HealthLevel.Danger, $"{s.FreeRamGb:0.0} GB RAM free");
        else if (s.FreeRamGb < 2 + freePad) Mark(HealthLevel.Warn, $"{s.FreeRamGb:0.0} GB RAM free");

        return (level, reasons.Count == 0 ? null : string.Join("; ", reasons));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);
}
