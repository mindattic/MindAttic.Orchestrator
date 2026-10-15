using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MindAttic.Orchestrator.Activity;

public enum HealthLevel { Ok, Warn, Danger }

/// <summary>One process holding a lot of kernel handles.</summary>
public sealed record HandleHog(int Pid, string Name, int Handles);

/// <summary>A snapshot of what runs a desktop out of resources before it freezes.</summary>
public sealed record HealthSample(long TotalHandles, double CommitPercent, double FreeRamGb, IReadOnlyList<HandleHog> Top);

/// <summary>
/// Watches the machine the agents run on. Found live: two orphaned <c>find /</c> searches held 660k handles and the
/// desktop froze while CPU sat idle, so CPU says nothing; kernel handles, commit charge and free RAM do. Past the warn
/// line autopilot starts no agents and you're alerted with the worst offenders; past the danger line too (the
/// <see cref="Sessions.ProcessReaper"/> kills runaway agent-spawned processes on its own).
/// </summary>
public static partial class MachineHealth
{
    /// <summary>Handle counts that are normal for the kernel itself, not a leak.</summary>
    private static readonly HashSet<int> Exempt = [0, 4];

    public static HealthSample Sample()
    {
        var hogs = new List<HandleHog>();
        long total = 0;
        foreach (var p in Process.GetProcesses())
            using (p)
            {
                try
                {
                    var h = p.HandleCount;
                    total += h;
                    if (!Exempt.Contains(p.Id)) hogs.Add(new HandleHog(p.Id, p.ProcessName, h));
                }
                catch (InvalidOperationException) { } // exited while we looked
            }

        var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        var commit = 0.0;
        var freeGb = double.MaxValue;
        if (GlobalMemoryStatusEx(ref mem))
        {
            commit = mem.ullTotalPageFile == 0 ? 0 : 100.0 * (mem.ullTotalPageFile - mem.ullAvailPageFile) / mem.ullTotalPageFile;
            freeGb = mem.ullAvailPhys / (1024.0 * 1024 * 1024);
        }
        return new HealthSample(total, commit, freeGb, [.. hogs.OrderByDescending(x => x.Handles).Take(3)]);
    }

    /// <summary>How the machine is doing, and in a sentence why.</summary>
    public static (HealthLevel Level, string? Why) Judge(HealthSample s)
    {
        var top = s.Top.FirstOrDefault();
        var hog = top is null ? "" : $"{top.Name} (pid {top.Pid}) holds {top.Handles:N0} handles";
        var reasons = new List<string>();
        var level = HealthLevel.Ok;
        void Mark(HealthLevel l, string why) { if (l > level) level = l; reasons.Add(why); }

        if (top is { Handles: >= 250_000 }) Mark(HealthLevel.Danger, hog);
        else if (top is { Handles: >= 100_000 }) Mark(HealthLevel.Warn, hog);
        if (s.TotalHandles >= 1_500_000) Mark(HealthLevel.Warn, $"{s.TotalHandles:N0} handles open in all");
        if (s.CommitPercent >= 95) Mark(HealthLevel.Danger, $"memory commit at {s.CommitPercent:0}%");
        else if (s.CommitPercent >= 88) Mark(HealthLevel.Warn, $"memory commit at {s.CommitPercent:0}%");
        if (s.FreeRamGb < 0.75) Mark(HealthLevel.Danger, $"{s.FreeRamGb:0.0} GB RAM free");
        else if (s.FreeRamGb < 2) Mark(HealthLevel.Warn, $"{s.FreeRamGb:0.0} GB RAM free");

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
