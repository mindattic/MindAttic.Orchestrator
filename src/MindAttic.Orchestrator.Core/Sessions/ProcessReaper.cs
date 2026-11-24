using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// Kills tool processes a dead agent left behind. A <c>find</c>/<c>rg</c>/<c>grep</c>/<c>git</c>/
/// <c>bash</c>/<c>python</c>/<c>node</c>/<c>dotnet</c> whose Claude CLI (or the shell an agent ran it through) has
/// already exited leaks handles forever once orphaned (M.ORCH-T87 found two orphaned <c>find /</c> runs
/// holding 660k handles and freezing the desktop); the pace gate in <see cref="Setup.AgentGate"/> stops
/// new ones from starting, this reaps the ones that already did. It also catches a still-attached search
/// that's gone rogue: past <see cref="MaxHandleCount"/> handles or <see cref="MaxAge"/> old.
/// <para><c>dotnet</c> (M.ORCH-T1586) found live 2026-11-10: 105 idle MSBuild/vstest worker processes (~5.7 GB)
/// an agent's own <c>dotnet build</c>/<c>dotnet test</c> calls left running once the CLI that started them
/// exited. <c>dotnet build-server shutdown</c> doesn't reach them (it only knows the server for the current
/// directory's resolved SDK, not every repo's), so without this they outlive the agent forever, same as an
/// orphaned <c>find</c>.</para>
/// </summary>
public static class ProcessReaper
{
    /// <summary>The default handle cap; live, <see cref="Settings.OrchestratorSettings.ReapMaxHandles"/> (M.ORCH-T1595).</summary>
    public const long MaxHandleCount = 50_000;

    /// <summary>The default runaway-search age; live, <see cref="Settings.OrchestratorSettings.ReapSearchMaxMinutes"/>.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(30);

    /// <summary>The default scan interval; live, <see cref="Settings.OrchestratorSettings.ReapEverySeconds"/>: a leak takes
    /// minutes to matter, and each scan is a WMI query that WmiPrvSE pays for (M.ORCH-T117: every 15s it was a steady CPU cost of its own).</summary>
    public static readonly TimeSpan Every = TimeSpan.FromSeconds(60);

    /// <summary>Ancestor hops to walk looking for a live Claude CLI before giving up on telling either way.</summary>
    private const int MaxAncestorHops = 20;

    /// <summary>A start time WMI couldn't give: never compared, never counted as old.</summary>
    public static readonly DateTime Unknown = DateTime.MinValue;

    private static readonly string[] TargetNames = ["find", "rg", "grep", "git", "bash", "python", "node", "dotnet"];

    /// <summary>One process in a scan: its id, name, parent, handle count, start time (<see cref="Unknown"/> when WMI gave none) and command line.</summary>
    public sealed record ProcessInfo(int Pid, string Name, int ParentPid, long HandleCount, DateTime StartUtc, string? CommandLine);

    /// <summary>A process <see cref="Select"/> picked to kill, and why.</summary>
    public sealed record Reaped(ProcessInfo Process, string Reason);

    /// <summary>
    /// Which of <paramref name="processes"/> to kill, and why: pure logic over a snapshot so it's testable
    /// without touching real processes. A target process (one of <see cref="TargetNames"/>) is reaped when
    /// either its ancestor chain breaks before reaching a live Claude CLI and it (or an ancestor up to the
    /// break) was seen under a live Claude CLI on an earlier scan, so its agent has exited; or it does reach
    /// one but has grown past <see cref="MaxHandleCount"/> handles or <see cref="MaxAge"/> old. Anything never
    /// seen under an agent (a dev server or script you started yourself, from a terminal since closed) is left
    /// alone. <paramref name="underAgent"/> is what earlier scans saw under a CLI; this one's are added to it.
    /// <paramref name="maxHandles"/> and <paramref name="maxAge"/> are the live settings' caps (defaults when not given).
    /// </summary>
    public static IReadOnlyList<Reaped> Select(IReadOnlyList<ProcessInfo> processes, DateTime nowUtc, ISet<(int Pid, DateTime Start)> underAgent,
        long maxHandles = MaxHandleCount, TimeSpan? maxAge = null)
    {
        var ageCap = maxAge ?? MaxAge;
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
            if (p.HandleCount > maxHandles) reaped.Add(new Reaped(p, $"over {maxHandles:N0} handles ({p.HandleCount:N0})"));
            // Age only marks a runaway search: a test suite, build or dev server under an agent may rightly run longer
            // (found live: an agent's 30-minute python test run was killed).
            else if (IsSearch(p.Name) && p.StartUtc != Unknown && nowUtc - p.StartUtc > ageCap) reaped.Add(new Reaped(p, $"running {MindAttic.Orchestrator.Formatting.Ago(p.StartUtc, nowUtc)}, over {ageCap.TotalMinutes:0} min"));
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

    /// <summary>Every process seen under a live Claude CLI by earlier scans (pid and start time, so a reused id isn't it),
    /// this daemon's and, through <see cref="SeenStore"/>, the ones before it.</summary>
    private static readonly HashSet<(int Pid, DateTime Start)> SeenUnderAgent = [];

    /// <summary>Snapshots every process via WMI, kills the ones <see cref="Select"/> flags (the whole tree under
    /// each, so an orphaned shell takes its own still-running children with it), and returns what it killed.
    /// Best effort: a process that exits mid-kill, or WMI being unavailable, is swallowed rather than thrown.</summary>
    /// <param name="store">Where the seen set is kept between runs (M.ORCH-T1678): read in before the first scan, so that scan
    /// already reaps what an earlier daemon saw under an agent that has since exited, and written back as it changes. Null
    /// keeps it in memory alone.</param>
    public static IReadOnlyList<Reaped> ReapOnce(DateTime nowUtc, long maxHandles = MaxHandleCount, TimeSpan? maxAge = null, Settings.JsonStore? store = null)
    {
        lock (SeenUnderAgent)
        {
            if (store is not null && (kept is null || !ReferenceEquals(kept.Store, store))) kept = new SeenStore(store);
            kept?.Load(SeenUnderAgent);
            if (SeenUnderAgent.Count > MaxSeen) SeenUnderAgent.Clear(); // every process ever seen under an agent, for the whole run (M.ORCH-T1607)
            var reaped = ReapOnce(nowUtc, Snapshot, CommandLineOf, Kill, SeenUnderAgent, maxHandles, maxAge);
            kept?.SaveIfDue(SeenUnderAgent, nowUtc);
            return reaped;
        }
    }

    /// <summary>The most processes the seen set holds, in memory and as kept.</summary>
    internal const int MaxSeen = 50_000;

    private static SeenStore? kept;

    /// <summary>One process seen under a live Claude CLI as it's kept: its id and when it started (UTC ticks, exactly as the
    /// scan read them), which together are the process. An id alone is not: Windows hands it to another process later.</summary>
    public sealed record SeenProcess(int Pid, long StartTicks);

    /// <summary>The seen set as its state row holds it. <see cref="Machine"/> is the machine whose processes they are: the
    /// database file travels (git, a copy), and another machine's ids mean nothing here.</summary>
    public sealed class SeenState
    {
        /// <summary>The machine name.</summary>
        public string? Machine { get; set; }

        /// <summary>The processes seen.</summary>
        public List<SeenProcess> Seen { get; set; } = [];
    }

    /// <summary>
    /// Keeps the seen-under-agent set in the store's <see cref="StateName"/> row (M.ORCH-T1678), so a process an earlier daemon
    /// saw under an agent is still known after a restart and is reaped on the first scan once its agent has gone. Only this
    /// machine's row is read, and each entry is the pid and the start time together, so a pid Windows has reused since is not
    /// the process that was seen: <see cref="Select"/> matches both against the scan, and the kill checks the start again.
    /// </summary>
    internal sealed class SeenStore(Settings.JsonStore store, string? machine = null)
    {
        /// <summary>The state row's name (volatile: this machine's file only, not in the dump).</summary>
        public const string StateName = "reaper-seen";

        /// <summary>The least time between two writes; a little under a scan's minute, so a scan a moment early still writes.</summary>
        public static readonly TimeSpan SaveEvery = TimeSpan.FromSeconds(55);

        private readonly string machine = machine ?? Environment.MachineName;
        private HashSet<(int Pid, DateTime Start)>? saved; // what the row holds; null until it has been read
        private DateTime savedUtc;

        /// <summary>The store.</summary>
        public Settings.JsonStore Store { get; } = store;

        /// <summary>Whether the row has been read: until then nothing is written, which would replace it unread.</summary>
        public bool Loaded => saved is not null;

        /// <summary>Adds what the row holds to <paramref name="seen"/>, once; a row that can't be read now is tried again on
        /// the next call. Returns how many it added.</summary>
        public int Load(ISet<(int Pid, DateTime Start)> seen)
        {
            if (saved is not null) return 0;
            if (!Store.TryLoad<SeenState>(StateName, out var state)) return 0;
            var mine = FromState(state, machine);
            saved = [.. mine];
            var added = 0;
            foreach (var entry in mine)
                if (seen.Add(entry)) added++;
            return added;
        }

        /// <summary>Writes <paramref name="seen"/> when it differs from what the row holds and the last write was at least
        /// <see cref="SaveEvery"/> ago. Returns whether it wrote. A write that fails is logged and tried again next time.</summary>
        public bool SaveIfDue(IReadOnlySet<(int Pid, DateTime Start)> seen, DateTime nowUtc)
        {
            if (saved is null || nowUtc - savedUtc < SaveEvery) return false;
            var state = ToState(seen, machine);
            var now = FromState(state, machine);
            if (now.SetEquals(saved)) return false;
            try { Store.Save(StateName, state); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Data.Common.DbException)
            {
                savedUtc = nowUtc; // not every scan: the next try is a minute on
                OrchestratorLog.For("ProcessReaper").Warning(ex, "Couldn't save the processes seen under an agent; trying again in a minute");
                return false;
            }
            saved = now;
            savedUtc = nowUtc;
            return true;
        }

        /// <summary>The set as it's kept: entries with a known start only (an id alone is never an identity), at most
        /// <see cref="MaxSeen"/>, in a steady order.</summary>
        internal static SeenState ToState(IEnumerable<(int Pid, DateTime Start)> seen, string machine) => new()
        {
            Machine = machine,
            Seen = [.. seen.Where(s => s.Pid > 0 && s.Start != Unknown)
                .Select(s => new SeenProcess(s.Pid, s.Start.ToUniversalTime().Ticks))
                .Distinct().OrderBy(s => s.Pid).ThenBy(s => s.StartTicks).Take(MaxSeen)],
        };

        /// <summary>The kept set read back: nothing from another machine's row, and no entry without a pid and a start time.</summary>
        internal static HashSet<(int Pid, DateTime Start)> FromState(SeenState? state, string machine)
        {
            var seen = new HashSet<(int Pid, DateTime Start)>();
            if (state?.Seen is not { } entries || !string.Equals(state.Machine, machine, StringComparison.OrdinalIgnoreCase)) return seen;
            foreach (var e in entries)
            {
                if (e is null || e.Pid <= 0 || e.StartTicks <= 0 || e.StartTicks > DateTime.MaxValue.Ticks) continue;
                seen.Add((e.Pid, new DateTime(e.StartTicks, DateTimeKind.Utc)));
                if (seen.Count >= MaxSeen) break;
            }
            return seen;
        }
    }

    /// <summary>The scan lists processes without their command lines (reading one means opening the process and
    /// its memory, the costly part of a WMI process query); only the ones about to be killed have theirs fetched,
    /// for the log, before they go. <paramref name="underAgent"/> is kept to processes still alive.</summary>
    internal static IReadOnlyList<Reaped> ReapOnce(DateTime nowUtc, Func<IReadOnlyList<ProcessInfo>> snapshot, Func<int, string?> commandLine, Action<ProcessInfo> kill,
        ISet<(int Pid, DateTime Start)> underAgent, long maxHandles = MaxHandleCount, TimeSpan? maxAge = null)
    {
        var processes = snapshot();
        if (processes.Count == 0) return []; // WMI failed: forget nothing, kill nothing
        var reaped = Select(processes, nowUtc, underAgent, maxHandles, maxAge)
            .Select(r => r with { Process = r.Process with { CommandLine = commandLine(r.Process.Pid) } })
            .ToList();
        foreach (var r in reaped) kill(r.Process);
        underAgent.IntersectWith(processes.Select(p => (p.Pid, p.StartUtc)));
        return reaped;
    }

    private static void Kill(ProcessInfo target)
    {
        try
        {
            using var p = Process.GetProcessById(target.Pid);
            // Its id may have gone to another process since the scan (it exited while command lines were read): not that one.
            if (target.StartUtc != Unknown && !BackgroundAgent.SameStart(p.StartTime.ToUniversalTime(), target.StartUtc)) return;
            p.Kill(true);
        }
        // AggregateException: part of its tree couldn't be ended; the rest of the sweep still runs.
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or AggregateException) { }
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
        catch (ManagementException ex) { OrchestratorLog.For("ProcessReaper").Warning(ex, "The WMI process snapshot failed; contained"); }
        catch (COMException ex) { OrchestratorLog.For("ProcessReaper").Warning(ex, "The WMI process snapshot failed; contained"); }
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
