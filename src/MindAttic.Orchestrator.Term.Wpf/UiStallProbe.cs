using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace MindAttic.Orchestrator.Term.Wpf;

/// <summary>
/// Watches one UI thread for stalls, for good (M.ORCH-T1733): typing into a CLI tab waits behind whatever that thread is doing,
/// and each earlier lag fix (M.ORCH-T634, T1537, T1596) removed one cause without anything left to name the next. A pool timer
/// posts a ping at Input priority, what a keystroke waits behind; a ping answered later than the threshold is logged with the
/// work the thread was in (<see cref="Doing"/>) while it waited. The timer runs at half the threshold, so a stall that long is
/// always seen in progress. A threshold of zero turns it off. It runs only while <see cref="Arm"/>ed (M.ORCH-T2206): a few
/// seconds after real input, since a stall nobody is typing into keeps nobody waiting, so an idle or minimized window costs nothing.
/// </summary>
public sealed partial class UiStallProbe : IDisposable
{
    private static volatile string? work;

    /// <summary>Names the UI-thread work running until the scope is disposed, for a stall's log line. UI thread only.</summary>
    public static Scope Doing(string label)
    {
        var previous = work;
        work = label;
        return new Scope(previous);
    }

    /// <summary>Ends a <see cref="Doing"/>, back to the work around it.</summary>
    public readonly struct Scope(string? previous) : IDisposable
    {
        /// <inheritdoc/>
        public void Dispose() => work = previous;
    }

    private static readonly long ArmedFor = 5 * Stopwatch.Frequency;

    private readonly Dispatcher dispatcher;
    private readonly Func<TimeSpan> threshold;
    private readonly Timer timer;
    private long armedUntil;
    private int ticking;
    private long sent;
    private volatile string? seen;
    // What the dispatcher is running (M.ORCH-T1738): the priority of its current operation, or -1 between operations, where a
    // stall is a native window message (a terminal's own window) or the thread held outside it (a GC, a page fault).
    private volatile int running = -1;
    private volatile int seenRunning = -2;
    private TimeSpan gcAtSend;
    private (uint Faults, long WorkingSet, long Private) memoryAtSend;
    private long lastLine;
    private int unlogged;
    private TimeSpan worst;
    private string? worstWork;

    /// <summary>Starts watching <paramref name="dispatcher"/>'s thread; <paramref name="threshold"/> is read at each ping, so it
    /// can be dialed live.</summary>
    public UiStallProbe(Dispatcher dispatcher, Func<TimeSpan> threshold)
    {
        this.dispatcher = dispatcher;
        this.threshold = threshold;
        // On the dispatcher's own thread (the window's constructor): its hooks are raised there.
        dispatcher.Hooks.OperationStarted += (_, e) => running = (int)e.Operation.Priority;
        dispatcher.Hooks.OperationCompleted += (_, _) => running = -1;
        dispatcher.Hooks.OperationAborted += (_, _) => running = -1;
        timer = new Timer(_ => Tick(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Watches for the next few seconds: called on real input, the only time a stall keeps anyone waiting.</summary>
    public void Arm()
    {
        Interlocked.Exchange(ref armedUntil, Stopwatch.GetTimestamp() + ArmedFor);
        Start();
    }

    /// <summary>Stops watching once any ping in flight is answered (the window hidden or minimized).</summary>
    public void Disarm() => Interlocked.Exchange(ref armedUntil, 0);

    private bool Armed => Stopwatch.GetTimestamp() < Interlocked.Read(ref armedUntil);

    private void Start()
    {
        if (Interlocked.CompareExchange(ref ticking, 1, 0) == 0) Reschedule(TimeSpan.Zero);
    }

    private void Tick()
    {
        var limit = threshold();
        var stop = limit <= TimeSpan.Zero || dispatcher.HasShutdownStarted;
        try
        {
            if (stop) return;
            if (Interlocked.Read(ref sent) != 0)
            {
                // Still unanswered: what the thread is in right now is what the ping waits behind.
                if (work is { } now) seen = now;
                seenRunning = running;
                return;
            }
            seen = null;
            seenRunning = -2;
            gcAtSend = GC.GetTotalPauseDuration();
            memoryAtSend = Memory();
            Interlocked.Exchange(ref sent, Stopwatch.GetTimestamp());
            dispatcher.BeginInvoke(DispatcherPriority.Input, () => Answered(limit));
        }
        catch (Exception ex) when (ex is InvalidOperationException or TaskCanceledException) { }
        finally
        {
            // A ping in flight is followed to its answer, armed or not, so the stall it is in is still named. Otherwise the timer
            // idles until the next Arm; one that lands between the check and the reset restarts it here.
            if (!stop && (Armed || Interlocked.Read(ref sent) != 0)) Reschedule(limit / 2);
            else
            {
                Volatile.Write(ref ticking, 0);
                if (!stop && Armed) Start();
            }
        }
    }

    private void Reschedule(TimeSpan due)
    {
        try { timer.Change(due, Timeout.InfiniteTimeSpan); }
        catch (ObjectDisposedException) { }
    }

    private void Answered(TimeSpan limit)
    {
        var took = Stopwatch.GetElapsedTime(Interlocked.Exchange(ref sent, 0));
        if (took < limit) return;
        unlogged++;
        if (took > worst)
        {
            var gc = GC.GetTotalPauseDuration() - gcAtSend;
            worst = took;
            var memory = Memory();
            // Page faults with the working set growing and private bytes flat are memory paged back in (the machine is short of
            // RAM); with private bytes growing they are new allocations.
            worstWork = $"{seen ?? Where(seenRunning)}; the GC paused {(long)gc.TotalMilliseconds}ms of it, {memory.Faults - memoryAtSend.Faults} page faults, " +
                $"working set {(memory.WorkingSet - memoryAtSend.WorkingSet) / Mb:+0;-0;0} MB, private {(memory.Private - memoryAtSend.Private) / Mb:+0;-0;0} MB";
        }
        // One line per ten thresholds at most, so a jammed thread doesn't flood the log; the line carries the worst of the rest.
        var now = Stopwatch.GetTimestamp();
        if (lastLine != 0 && Stopwatch.GetElapsedTime(lastLine, now) < limit * 10) return;
        OrchestratorLog.For("UiStall").Warning("The UI thread kept input waiting {Ms}ms during {Work:l} ({Count} stall(s) since the last line)",
            (long)worst.TotalMilliseconds, worstWork ?? "unnamed work", unlogged);
        (lastLine, unlogged, worst, worstWork) = (now, 0, TimeSpan.Zero, null);
    }

    /// <summary>What unlabelled work was, from the dispatcher operation seen while input waited.</summary>
    private static string Where(int priority) => priority switch
    {
        -2 => "unnamed work, over before it was looked at",
        -1 => "no dispatcher operation (a native window message, or the thread held: GC, paging)",
        (int)DispatcherPriority.Render => "layout and render",
        (int)DispatcherPriority.Loaded => "a Loaded-priority operation (after layout)",
        (int)DispatcherPriority.DataBind => "data binding",
        (int)DispatcherPriority.Input => "an input handler",
        _ => $"an unnamed {(DispatcherPriority)priority}-priority operation",
    };

    private const long Mb = 1024 * 1024;

    /// <summary>This process's page faults so far, its working set and its private bytes; zeros when they can't be read.</summary>
    private static (uint Faults, long WorkingSet, long Private) Memory()
    {
        var counters = new ProcessMemoryCounters { cb = (uint)Marshal.SizeOf<ProcessMemoryCounters>() };
        return K32GetProcessMemoryInfo(Self.Handle, ref counters, counters.cb)
            ? (counters.PageFaultCount, (long)counters.WorkingSetSize, (long)counters.PagefileUsage) : default;
    }

    private static readonly Process Self = Process.GetCurrentProcess();

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessMemoryCounters
    {
        public uint cb, PageFaultCount;
        public nuint PeakWorkingSetSize, WorkingSetSize, QuotaPeakPagedPoolUsage, QuotaPagedPoolUsage, QuotaPeakNonPagedPoolUsage,
            QuotaNonPagedPoolUsage, PagefileUsage, PeakPagefileUsage;
    }

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool K32GetProcessMemoryInfo(IntPtr process, ref ProcessMemoryCounters counters, uint size);

    /// <inheritdoc/>
    public void Dispose() => timer.Dispose();
}
