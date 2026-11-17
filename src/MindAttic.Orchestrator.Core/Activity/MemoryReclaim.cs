using System.Diagnostics;
using System.Globalization;
using System.Runtime;
using System.Runtime.InteropServices;
using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Activity;

/// <summary>
/// The memory guard's first answer to a full machine: get memory back rather than hold work. Windows 11 keeps RAM as full as
/// it can on purpose, so a high "in use" figure alone isn't a reason to stop; past
/// <see cref="Settings.OrchestratorSettings.MemoryGuardStartPercent"/> (98% by default) the guard collects garbage instead:
/// a full compacting .NET GC of this process, then a working-set trim of this process and every process it started
/// (hosted CLIs, their subagents, builds), so pages they aren't touching go back to Windows.
/// </summary>
public static partial class MemoryReclaim
{
    /// <summary>Runs one reclaim pass; returns a short line on what it did.</summary>
    public static string Run()
    {
        var before = MachineHealth.Memory()?.FreeGb;

        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);

        var self = Environment.ProcessId;
        var trimmed = 0;
        if (Trim(self)) trimmed++;
        try
        {
            var parents = ProcessTree.ParentMap();
            foreach (var pid in parents.Keys)
                if (pid != self && ProcessTree.IsAncestor(self, pid, parents) && Trim(pid)) trimmed++;
        }
        catch (Exception e) when (e is InvalidOperationException or ExternalException) { }

        var after = MachineHealth.Memory()?.FreeGb;
        var freed = before is { } b && after is { } a ? $", {(a - b).ToString("+0.0;-0.0", CultureInfo.InvariantCulture)} GB free" : "";
        return $"collected garbage and trimmed {trimmed} process{(trimmed == 1 ? "" : "es")}{freed}";
    }

    private static bool Trim(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return EmptyWorkingSet(p.Handle);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return false;
        }
    }

    [LibraryImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EmptyWorkingSet(IntPtr process);
}
