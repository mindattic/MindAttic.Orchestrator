using System.Diagnostics;
using System.Globalization;
using System.Runtime;
using System.Runtime.InteropServices;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Activity;

/// <summary>
/// The memory guard's answer to a full machine: get memory back rather than hold work, in escalating, safe-first steps
/// (M.ORCH-T1671). Windows 11 keeps RAM as full as it can on purpose, so a high "in use" figure alone isn't a reason to
/// stop; past <see cref="OrchestratorSettings.MemoryGuardStartPercent"/> (98% by default):
/// <list type="number">
/// <item>a full compacting .NET GC of this process, then a working-set trim of this process and every process it started
/// (hosted CLIs, their subagents, builds), so pages they aren't touching go back to Windows;</item>
/// <item>if that alone wasn't enough, the same working-set trim on every other process on the machine, biggest first -
/// still non-destructive, nothing closes;</item>
/// <item>if even that wasn't enough, one process is actually closed: first an idle, headless Orchestrator CLI of ours (its
/// ticket, if any, simply stays open - nothing it held is lost), and only once none of those exist, one foreign desktop
/// app from a small vetted list, least disruptive first.</item>
/// </list>
/// Nothing under a Claude CLI or an Orchestrator hosting process (<see cref="ProcessTree.IsProtected"/>) is ever closed by
/// this, only trimmed at step 1 - that's where the actual automation lives.
/// </summary>
public static partial class MemoryReclaim
{
    /// <summary>How many of the biggest non-Orchestrator processes a foreign trim pass works through: enough to matter,
    /// bounded so a machine with hundreds of tiny ones doesn't make this slow for no benefit.</summary>
    internal const int ForeignTrimLimit = 60;

    /// <summary>The foreign desktop apps a kill may ever target, least disruptive to close first: a vetted allow-list, not
    /// "everything outside the Orchestrator tree" - killing the wrong system process can crash more than it saves.</summary>
    internal static readonly string[] ForeignKillAllowList =
        ["SnippingTool", "NVIDIA Overlay", "DisplayFusion", "Overwolf", "jellyfin", "ChatGPT", "Vortex", "chrome", "Steam"];

    /// <summary>Runs one escalating reclaim pass; returns a short line on what it did. Each step only runs if the one
    /// before it left the machine still at or past <see cref="OrchestratorSettings.MemoryGuardStartPercent"/>.</summary>
    public static string Run(OrchestratorSettings s)
    {
        var before = MachineHealth.Memory()?.FreeGb;
        var notes = new List<string> { SelfTrim() };
        if (StillOver(s)) notes.Add(ForeignTrim());
        if (StillOver(s)) notes.Add(KillLeastImportant());

        var after = MachineHealth.Memory()?.FreeGb;
        var freed = before is { } b && after is { } a ? $", {(a - b).ToString("+0.0;-0.0", CultureInfo.InvariantCulture)} GB free in all" : "";
        return string.Join("; ", notes) + freed;
    }

    private static bool StillOver(OrchestratorSettings s) => MachineHealth.Memory() is { } m && m.UsedPercent >= s.MemoryGuardStartPercent;

    /// <summary>The combined working-set of <paramref name="rootPid"/> and everything it started (M.ORCH-T1876): a CLI
    /// or Launcher tab's own RAM tooltip figure, the same process-tree walk <see cref="SelfTrim"/> already uses on this
    /// process's own tree. 0 once <paramref name="rootPid"/> itself has exited.</summary>
    public static long TreeWorkingSetBytes(int rootPid)
    {
        var total = WorkingSetBytes(rootPid);
        try
        {
            var parents = ProcessTree.ParentMap();
            foreach (var pid in parents.Keys)
                if (pid != rootPid && ProcessTree.IsAncestor(rootPid, pid, parents)) total += WorkingSetBytes(pid);
        }
        catch (Exception e) when (e is InvalidOperationException or ExternalException) { }
        return total;
    }

    /// <summary>Step 1 (unchanged, M.ORCH-T1655): a compacting GC of this process, then a working-set trim of it and every
    /// process it started.</summary>
    private static string SelfTrim()
    {
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
        return $"collected garbage and trimmed {trimmed} process{(trimmed == 1 ? "" : "es")}";
    }

    /// <summary>Step 2: a working-set trim (never a kill) of every process outside the Orchestrator/Claude tree, biggest
    /// working set first, up to <see cref="ForeignTrimLimit"/> - covers system processes too, since trimming one is always
    /// safe even where killing it wouldn't be.</summary>
    private static string ForeignTrim()
    {
        var processes = ProcessTree.Snapshot();
        var candidates = processes.Keys.Where(pid => !ProcessTree.IsProtected(pid, processes))
            .Select(pid => (Pid: pid, Ws: WorkingSetBytes(pid)))
            .Where(c => c.Ws > 0)
            .OrderByDescending(c => c.Ws)
            .Take(ForeignTrimLimit);

        var trimmed = 0;
        foreach (var c in candidates)
            if (Trim(c.Pid)) trimmed++;
        return $"trimmed {trimmed} other process{(trimmed == 1 ? "" : "es")}";
    }

    /// <summary>Step 3, the only one that closes anything: an idle, headless CLI of ours first (<see cref="KillOwnIdleHeadless"/>),
    /// since its ticket simply stays open with nothing lost; only once none of those exist, a foreign app from
    /// <see cref="ForeignKillAllowList"/>.</summary>
    private static string KillLeastImportant() => KillOwnIdleHeadless() ?? KillForeignAllowList();

    /// <summary>The longest-idle Orchestrator CLI with no Launcher tab (so no one's watching it) that this can close without
    /// losing anyone's conversation or any ticket progress; null when the roster can't be read or none qualify.</summary>
    private static string? KillOwnIdleHeadless()
    {
        try
        {
            var db = new OrchestratorDb(OrchestratorDb.LivePath);
            var target = CliRoster.Read(db)?.Clis
                .Where(c => !c.InLauncher && c.Idle)
                .OrderBy(c => c.LastTurnUtc ?? DateTime.MinValue)
                .FirstOrDefault();
            return target is { } t && Kill(t.Pid) ? $"closed idle headless CLI {t.Pid} ({t.Repo})" : null;
        }
        catch (Exception e) when (e is System.Data.Common.DbException or IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>The first running process, in <see cref="ForeignKillAllowList"/> order, that this closes; "nothing left to
    /// flush" once none of the list is running.</summary>
    private static string KillForeignAllowList()
    {
        foreach (var name in ForeignKillAllowList)
        {
            var processes = Process.GetProcessesByName(name);
            try
            {
                if (processes.FirstOrDefault() is { } p && Kill(p.Id)) return $"closed {name} (pid {p.Id})";
            }
            finally { foreach (var p in processes) p.Dispose(); }
        }
        return "nothing left to flush";
    }

    private static long WorkingSetBytes(int pid)
    {
        try { using var p = Process.GetProcessById(pid); return p.WorkingSet64; }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { return 0; }
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

    private static bool Kill(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            p.Kill();
            return true;
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
