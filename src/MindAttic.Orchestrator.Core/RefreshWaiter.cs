namespace MindAttic.Orchestrator;

/// <summary>Why <see cref="RefreshWaiter.Wait"/> returned.</summary>
internal enum RefreshWake { Interval, Signal, Cancelled }

/// <summary>
/// The daemon's wait between refreshes: the full interval, unless one of the watched files is written (a turn ended,
/// Claude's usage report changed), and then no sooner than <c>minGap</c> after the wait began, so a burst of signals
/// never brings two refreshes closer than that. Cancelling returns at once.
/// </summary>
internal sealed class RefreshWaiter
{
    private readonly string[] paths;
    private readonly TimeSpan minGap;
    private readonly TimeSpan poll;
    private DateTime lastSignal;

    public RefreshWaiter(string[] paths, TimeSpan minGap, TimeSpan? poll = null)
    {
        this.paths = paths;
        this.minGap = minGap;
        this.poll = poll ?? TimeSpan.FromMilliseconds(500);
        lastSignal = Stamp();
    }

    private int poked;

    /// <summary>Counts as a signal, from any thread: a reading that landed somewhere no watched file shows (the meter probe's
    /// database row, M.ORCH-T854) brings the next refresh forward like a file write does.</summary>
    public void Poke() => Interlocked.Exchange(ref poked, 1);

    public RefreshWake Wait(TimeSpan interval, CancellationToken cancel)
    {
        var until = DateTime.UtcNow + interval;
        var earliest = DateTime.UtcNow + minGap;
        while (DateTime.UtcNow < until)
        {
            var left = until - DateTime.UtcNow;
            if (cancel.WaitHandle.WaitOne(left < poll ? (left > TimeSpan.Zero ? left : TimeSpan.Zero) : poll)) return RefreshWake.Cancelled;
            if (DateTime.UtcNow < earliest) continue;
            if (Interlocked.Exchange(ref poked, 0) == 1)
            {
                lastSignal = Stamp();
                return RefreshWake.Signal;
            }
            var stamp = Stamp();
            if (stamp == lastSignal) continue;
            lastSignal = stamp;
            return RefreshWake.Signal;
        }
        lastSignal = Stamp();
        return RefreshWake.Interval;
    }

    /// <summary>The newest write time among the watched files (missing or unreadable ones count as never written).</summary>
    private DateTime Stamp()
    {
        static DateTime Of(string path) { try { return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : default; } catch (IOException) { return default; } }
        var newest = default(DateTime);
        foreach (var p in paths) { var t = Of(p); if (t > newest) newest = t; }
        return newest;
    }
}
