using System.Diagnostics;
using System.Windows.Threading;

namespace MindAttic.Orchestrator.Term.Wpf;

/// <summary>
/// Watches one UI thread for stalls, for good (M.ORCH-T1733): typing into a CLI tab waits behind whatever that thread is doing,
/// and each earlier lag fix (M.ORCH-T634, T1537, T1596) removed one cause without anything left to name the next. A pool timer
/// posts a ping at Input priority, what a keystroke waits behind; a ping answered later than the threshold is logged with the
/// work the thread was in (<see cref="Doing"/>) while it waited. The timer runs at half the threshold, so a stall that long is
/// always seen in progress. A threshold of zero turns it off.
/// </summary>
public sealed class UiStallProbe : IDisposable
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

    private static readonly TimeSpan OffRecheck = TimeSpan.FromSeconds(5);

    private readonly Dispatcher dispatcher;
    private readonly Func<TimeSpan> threshold;
    private readonly Timer timer;
    private long sent;
    private volatile string? seen;
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
        timer = new Timer(_ => Tick(), null, OffRecheck, Timeout.InfiniteTimeSpan);
    }

    private void Tick()
    {
        var limit = threshold();
        try
        {
            if (limit <= TimeSpan.Zero || dispatcher.HasShutdownStarted) return;
            if (Interlocked.Read(ref sent) != 0)
            {
                // Still unanswered: what the thread is in right now is what the ping waits behind.
                if (work is { } now) seen = now;
                return;
            }
            seen = null;
            Interlocked.Exchange(ref sent, Stopwatch.GetTimestamp());
            dispatcher.BeginInvoke(DispatcherPriority.Input, () => Answered(limit));
        }
        catch (Exception ex) when (ex is InvalidOperationException or TaskCanceledException) { }
        finally
        {
            try { timer.Change(limit <= TimeSpan.Zero ? OffRecheck : limit / 2, Timeout.InfiniteTimeSpan); }
            catch (ObjectDisposedException) { }
        }
    }

    private void Answered(TimeSpan limit)
    {
        var took = Stopwatch.GetElapsedTime(Interlocked.Exchange(ref sent, 0));
        if (took < limit) return;
        unlogged++;
        if (took > worst) (worst, worstWork) = (took, seen);
        // One line per ten thresholds at most, so a jammed thread doesn't flood the log; the line carries the worst of the rest.
        var now = Stopwatch.GetTimestamp();
        if (lastLine != 0 && Stopwatch.GetElapsedTime(lastLine, now) < limit * 10) return;
        OrchestratorLog.For("UiStall").Warning("The UI thread kept input waiting {Ms}ms during {Work:l} ({Count} stall(s) since the last line)",
            (long)worst.TotalMilliseconds, worstWork ?? "unnamed work (layout, render or a handler)", unlogged);
        (lastLine, unlogged, worst, worstWork) = (now, 0, TimeSpan.Zero, null);
    }

    /// <inheritdoc/>
    public void Dispose() => timer.Dispose();
}
