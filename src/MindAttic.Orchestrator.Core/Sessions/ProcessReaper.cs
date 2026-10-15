using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// Kills tool processes a dead agent left behind. A <c>find</c>/<c>rg</c>/<c>grep</c>/<c>git</c>/
/// <c>bash</c>/<c>python</c>/<c>node</c> whose Claude CLI (or the shell an agent ran it through) has
/// already exited leaks handles forever once orphaned (MAO-T87 found two orphaned <c>find /</c> runs
/// holding 660k handles and freezing the desktop); the pace gate in <see cref="Setup.AgentGate"/> stops
/// new ones from starting, this reaps the ones that already did. It also catches a still-attached search
/// that's gone rogue: past <see cref="MaxHandleCount"/> handles or <see cref="MaxAge"/> old.
/// </summary>
public static class ProcessReaper
{
    public const long MaxHandleCount = 50_000;
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(30);

    /// <summary>How often the daemon scans: a leak takes minutes to matter, and each scan is a WMI query that
    /// WmiPrvSE pays for (MAO-T117: every 15s it was a steady CPU cost of its own).</summary>
    public static readonly TimeSpan Every = TimeSpan.FromSeconds(60);

    /// <summary>Ancestor hops to walk looking for a live Claude CLI before giving up on telling either way.</summary>
    private const int MaxAncestorHops = 20;

    private static readonly string[] TargetNames = ["find", "rg", "grep", "git", "bash", "python", "node"];

    public sealed record ProcessInfo(int Pid, string Name, int ParentPid, long HandleCount, DateTime StartUtc, string? CommandLine);

    public sealed record Reaped(ProcessInfo Process, string Reason);

    /// <summary>
    /// Which of <paramref name="processes"/> to kill, and why: pure logic over a snapshot so it's testable
    /// without touching real processes. A target process (one of <see cref="TargetNames"/>) is reaped when
    /// either its ancestor chain breaks before reaching a live Claude CLI (its agent has exited), or it
    /// does reach one but has grown past <see cref="MaxHandleCount"/> handles or <see cref="MaxAge"/> old.
    /// A target process whose chain reaches no Claude CLI but also never breaks (a plain dev process, not
    /// agent-spawned) is left alone.
    /// </summary>
    public static IReadOnlyList<Reaped> Select(IReadOnlyList<ProcessInfo> processes, DateTime nowUtc)
    {
        var byPid = processes.GroupBy(p => p.Pid).ToDictionary(g => g.Key, g => g.First());
        var reaped = new List<Reaped>();
        foreach (var p in processes)
        {
            if (!IsTarget(p.Name)) continue;
            var (foundClaude, chainBroken) = WalkAncestors(p, byPid);
            if (chainBroken && !foundClaude)
            {
                reaped.Add(new Reaped(p, "its Claude CLI or agent has already exited"));
                continue;
            }
            if (!foundClaude) continue; // ancestry reaches no agent at all: a plain dev process, leave it alone
            if (p.HandleCount > MaxHandleCount) reaped.Add(new Reaped(p, $"over {MaxHandleCount:N0} handles ({p.HandleCount:N0})"));
            else if (nowUtc - p.StartUtc > MaxAge) reaped.Add(new Reaped(p, $"running {MindAttic.Orchestrator.Formatting.Ago(p.StartUtc, nowUtc)}, over {MaxAge.TotalMinutes:0} min"));
        }
        return reaped;
    }

    private static bool IsTarget(string name) => TargetNames.Any(t => Bare(name).Equals(t, StringComparison.OrdinalIgnoreCase));

    private static string Bare(string name) => name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;

    /// <summary>Walks up from <paramref name="start"/>'s parent: true/true if a live Claude CLI is found, true/false
    /// (first tuple item) if found, or false/true once a recorded parent pid no longer has any process (it already
    /// exited, breaking the chain). Gives up after <see cref="MaxAncestorHops"/> hops without resolving either way.</summary>
    private static (bool FoundClaude, bool ChainBroken) WalkAncestors(ProcessInfo start, IReadOnlyDictionary<int, ProcessInfo> byPid)
    {
        var pid = start.ParentPid;
        for (var hop = 0; hop < MaxAncestorHops; hop++)
        {
            if (pid == 0) return (false, false);
            if (!byPid.TryGetValue(pid, out var parent)) return (false, true);
            if (ConsoleTyper.IsClaude(Bare(parent.Name))) return (true, false);
            pid = parent.ParentPid;
        }
        return (false, false);
    }

    /// <summary>Snapshots every process via WMI, kills the ones <see cref="Select"/> flags (the whole tree under
    /// each, so an orphaned shell takes its own still-running children with it), and returns what it killed.
    /// Best effort: a process that exits mid-kill, or WMI being unavailable, is swallowed rather than thrown.</summary>
    public static IReadOnlyList<Reaped> ReapOnce(DateTime nowUtc) => ReapOnce(nowUtc, Snapshot, CommandLineOf, Kill);

    /// <summary>The scan lists processes without their command lines (reading one means opening the process and
    /// its memory, the costly part of a WMI process query); only the ones about to be killed have theirs fetched,
    /// for the log, before they go.</summary>
    internal static IReadOnlyList<Reaped> ReapOnce(DateTime nowUtc, Func<IReadOnlyList<ProcessInfo>> snapshot, Func<int, string?> commandLine, Action<int> kill)
    {
        var reaped = Select(snapshot(), nowUtc)
            .Select(r => r with { Process = r.Process with { CommandLine = commandLine(r.Process.Pid) } })
            .ToList();
        foreach (var r in reaped) kill(r.Process.Pid);
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
            foreach (var o in searcher.Get())
            {
                using (o)
                {
                    if (o["ProcessId"] is not uint pid || o["Name"] is not string name) continue;
                    var parentPid = o["ParentProcessId"] is uint ppid ? (int)ppid : 0;
                    var handles = o["HandleCount"] is uint h ? h : 0;
                    var started = o["CreationDate"] is string cd ? SafeParse(cd) : DateTime.UtcNow;
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
            foreach (var o in searcher.Get())
                using (o) return o["CommandLine"] as string;
        }
        catch (ManagementException) { }
        catch (COMException) { }
        return null;
    }

    private static DateTime SafeParse(string dmtf)
    {
        try { return ManagementDateTimeConverter.ToDateTime(dmtf).ToUniversalTime(); }
        catch (Exception ex) when (ex is FormatException or ArgumentException) { return DateTime.UtcNow; }
    }
}
