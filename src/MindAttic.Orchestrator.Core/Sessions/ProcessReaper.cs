using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// Kills tool processes a dead agent left behind. A <c>find</c>/<c>rg</c>/<c>grep</c>/<c>git</c>/
/// <c>bash</c>/<c>python</c>/<c>node</c> whose Claude CLI (or the shell an agent ran it through) has
/// already exited leaks handles forever once orphaned (M.ORCH-T87 found two orphaned <c>find /</c> runs
/// holding 660k handles and freezing the desktop); the pace gate in <see cref="Setup.AgentGate"/> stops
/// new ones from starting, this reaps the ones that already did. It also catches a still-attached search
/// that's gone rogue: past <see cref="MaxHandleCount"/> handles or <see cref="MaxAge"/> old.
/// </summary>
public static class ProcessReaper
{
    public const long MaxHandleCount = 50_000;
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(30);

    /// <summary>How often the daemon scans: a leak takes minutes to matter, and each scan is a WMI query that
    /// WmiPrvSE pays for (M.ORCH-T117: every 15s it was a steady CPU cost of its own).</summary>
    public static readonly TimeSpan Every = TimeSpan.FromSeconds(60);

    /// <summary>Ancestor hops to walk looking for a live Claude CLI before giving up on telling either way.</summary>
    private const int MaxAncestorHops = 20;

    /// <summary>A start time WMI couldn't give: never compared, never counted as old.</summary>
    public static readonly DateTime Unknown = DateTime.MinValue;

    private static readonly string[] TargetNames = ["find", "rg", "grep", "git", "bash", "python", "node"];

    public sealed record ProcessInfo(int Pid, string Name, int ParentPid, long HandleCount, DateTime StartUtc, string? CommandLine);

    public sealed record Reaped(ProcessInfo Process, string Reason);

    /// <summary>
    /// Which of <paramref name="processes"/> to kill, and why: pure logic over a snapshot so it's testable
    /// without touching real processes. A target process (one of <see cref="TargetNames"/>) is reaped when
    /// either its ancestor chain breaks before reaching a live Claude CLI and it (or an ancestor up to the
    /// break) was seen under a live Claude CLI on an earlier scan, so its agent has exited; or it does reach
    /// one but has grown past <see cref="MaxHandleCount"/> handles or <see cref="MaxAge"/> old. Anything never
    /// seen under an agent (a dev server or script you started yourself, from a terminal since closed) is left
    /// alone. <paramref name="underAgent"/> is what earlier scans saw under a CLI; this one's are added to it.
    /// </summary>
    public static IReadOnlyList<Reaped> Select(IReadOnlyList<ProcessInfo> processes, DateTime nowUtc, ISet<(int Pid, DateTime Start)> underAgent)
    {
        var byPid = processes.GroupBy(p => p.Pid).ToDictionary(g => g.Key, g => g.First());
        var reaped = new List<Reaped>();
        foreach (var p in processes)
        {
            if (!IsTarget(p.Name)) continue;
            var (foundClaude, chainBroken, chain) = WalkAncestors(p, byPid);
            if (foundClaude)
                foreach (var c in chain.Where(c => c.StartUtc != Unknown)) underAgent.Add((c.Pid, c.StartUtc)); // without a start, a reused id would match
            if (chainBroken && !foundClaude)
            {
                if (chain.Any(c => underAgent.Contains((c.Pid, c.StartUtc))))
                    reaped.Add(new Reaped(p, "its Claude CLI or agent has already exited"));
                continue; // never seen under an agent: not ours to kill
            }
            if (!foundClaude) continue; // ancestry reaches no agent at all: a plain dev process, leave it alone
            if (p.HandleCount > MaxHandleCount) reaped.Add(new Reaped(p, $"over {MaxHandleCount:N0} handles ({p.HandleCount:N0})"));
            // Age only marks a runaway search: a test suite, build or dev server under an agent may rightly run longer
            // (found live: an agent's 30-minute python test run was killed).
            else if (IsSearch(p.Name) && p.StartUtc != Unknown && nowUtc - p.StartUtc > MaxAge) reaped.Add(new Reaped(p, $"running {MindAttic.Orchestrator.Formatting.Ago(p.StartUtc, nowUtc)}, over {MaxAge.TotalMinutes:0} min"));
        }
        return reaped;
    }

    private static bool IsSearch(string name) => Bare(name).ToLowerInvariant() is "find" or "rg" or "grep";

    private static bool IsTarget(string name) => TargetNames.Any(t => Bare(name).Equals(t, StringComparison.OrdinalIgnoreCase));

    private static string Bare(string name) => name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;

    /// <summary>Walks up from <paramref name="start"/>'s parent: found if a live Claude CLI is reached, broken once a
    /// recorded parent pid no longer has its process (it exited), neither after <see cref="MaxAncestorHops"/> hops or at
    /// the root. <c>Chain</c> is <paramref name="start"/> and the ancestors walked below the CLI or the break.</summary>
    private static (bool FoundClaude, bool ChainBroken, List<ProcessInfo> Chain) WalkAncestors(ProcessInfo start, IReadOnlyDictionary<int, ProcessInfo> byPid)
    {
        var chain = new List<ProcessInfo> { start };
        var child = start;
        for (var hop = 0; hop < MaxAncestorHops; hop++)
        {
            var pid = child.ParentPid;
            if (pid == 0) return (false, false, chain);
            if (!byPid.TryGetValue(pid, out var parent)) return (false, true, chain);
            // Windows reuses ids: a "parent" younger than its child is another process that took the dead one's id.
            if (parent.StartUtc != Unknown && child.StartUtc != Unknown && parent.StartUtc > child.StartUtc) return (false, true, chain);
            if (Providers.IsClaudeProcess(Bare(parent.Name))) return (true, false, chain);
            chain.Add(parent);
            child = parent;
        }
        return (false, false, chain);
    }

    /// <summary>Every process seen under a live Claude CLI by earlier scans (pid and start time, so a reused id isn't it).</summary>
    private static readonly HashSet<(int Pid, DateTime Start)> SeenUnderAgent = [];

    /// <summary>Snapshots every process via WMI, kills the ones <see cref="Select"/> flags (the whole tree under
    /// each, so an orphaned shell takes its own still-running children with it), and returns what it killed.
    /// Best effort: a process that exits mid-kill, or WMI being unavailable, is swallowed rather than thrown.</summary>
    public static IReadOnlyList<Reaped> ReapOnce(DateTime nowUtc)
    {
        lock (SeenUnderAgent) return ReapOnce(nowUtc, Snapshot, CommandLineOf, Kill, SeenUnderAgent);
    }

    /// <summary>The scan lists processes without their command lines (reading one means opening the process and
    /// its memory, the costly part of a WMI process query); only the ones about to be killed have theirs fetched,
    /// for the log, before they go. <paramref name="underAgent"/> is kept to processes still alive.</summary>
    internal static IReadOnlyList<Reaped> ReapOnce(DateTime nowUtc, Func<IReadOnlyList<ProcessInfo>> snapshot, Func<int, string?> commandLine, Action<int> kill,
        ISet<(int Pid, DateTime Start)> underAgent)
    {
        var processes = snapshot();
        if (processes.Count == 0) return []; // WMI failed: forget nothing, kill nothing
        var reaped = Select(processes, nowUtc, underAgent)
            .Select(r => r with { Process = r.Process with { CommandLine = commandLine(r.Process.Pid) } })
            .ToList();
        foreach (var r in reaped) kill(r.Process.Pid);
        underAgent.IntersectWith(processes.Select(p => (p.Pid, p.StartUtc)));
        return reaped;
    }

    private static void Kill(int pid)
    {
        try { using var p = Process.GetProcessById(pid); p.Kill(true); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
    }

    private static IReadOnlyList<ProcessInfo> Snapshot()
    {
        var result = new List<ProcessInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT ProcessId, ParentProcessId, Name, HandleCount, CreationDate FROM Win32_Process");
            using var found = searcher.Get();
            foreach (var o in found)
            {
                using (o)
                {
                    if (o["ProcessId"] is not uint pid || o["Name"] is not string name) continue;
                    var parentPid = o["ParentProcessId"] is uint ppid ? (int)ppid : 0;
                    var handles = o["HandleCount"] is uint h ? h : 0;
                    var started = o["CreationDate"] is string cd ? SafeParse(cd) : Unknown;
                    result.Add(new ProcessInfo((int)pid, name, parentPid, handles, started, null));
                }
            }
        }
        catch (ManagementException) { }
        catch (COMException) { }
        return result;
    }

    private static string? CommandLineOf(int pid)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {pid}");
            using var found = searcher.Get();
            foreach (var o in found)
                using (o) return o["CommandLine"] as string;
        }
        catch (ManagementException) { }
        catch (COMException) { }
        return null;
    }

    private static DateTime SafeParse(string dmtf)
    {
        try { return ManagementDateTimeConverter.ToDateTime(dmtf).ToUniversalTime(); }
        catch (Exception ex) when (ex is FormatException or ArgumentException) { return Unknown; }
    }
}
