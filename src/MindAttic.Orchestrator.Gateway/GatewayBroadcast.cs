using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Gateway;

/// <summary>The owner's broadcast choices as they are now (M.ORCH-T935): which sessions are shared, masked and paused.</summary>
internal interface IBroadcastControl
{
    TermBroadcastSettings Current { get; }
}

/// <summary>Fixed choices that a test changes by hand.</summary>
internal sealed class FixedBroadcast(TermBroadcastSettings settings) : IBroadcastControl
{
    public TermBroadcastSettings Current { get; set; } = settings;
}

/// <summary>
/// <c>broadcast.json</c> (<see cref="TermBroadcast"/>), reread whenever it changes: its time and size are looked at every
/// <see cref="Interval"/>, so a share, mask or pause from the dashboard or <c>orchestrator broadcast</c> takes effect within a
/// fraction of a second, without restarting the gateway. A file that can't be read keeps the last good settings.
/// </summary>
internal sealed class BroadcastFile : IBroadcastControl, IDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(150);

    private readonly string? directory;
    private readonly Timer timer;
    // Timer doesn't serialize its own callbacks: a poll slower than Interval (slow disk, AV, contention with the file being
    // rewritten) could overlap the next one, so 'seen' (read and written only here) is protected rather than left to race
    // (bughunt 2026-10-28). 'current' stays volatile; it's only ever replaced wholesale, never read-modify-written.
    private readonly object pollGate = new();
    private (DateTime Written, long Length) seen;
    private volatile TermBroadcastSettings current;

    public BroadcastFile(string? directory)
    {
        this.directory = directory;
        var stamp = Stamp();
        // Not readable at start: nothing shared until it is, and the poll keeps trying (M.ORCH-T986).
        if (TermBroadcast.TryRead(directory, out var settings)) seen = stamp;
        else seen = (DateTime.MinValue, long.MinValue);
        current = settings;
        timer = new Timer(_ => Poll(), null, Interval, Interval);
    }

    public TermBroadcastSettings Current => current;

    private (DateTime, long) Stamp()
    {
        try
        {
            var info = new FileInfo(TermBroadcast.PathIn(directory));
            return info.Exists ? (info.LastWriteTimeUtc, info.Length) : (DateTime.MinValue, -1);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { lock (pollGate) return seen; }
    }

    private void Poll()
    {
        try
        {
            lock (pollGate)
            {
                var stamp = Stamp();
                if (stamp == seen) return;
                // Not readable just now (a sharing violation as a writer replaces it): the last good settings stay, and the stamp
                // isn't taken, so the next poll reads it again rather than waiting for the file to change (M.ORCH-T986).
                if (!TermBroadcast.TryRead(directory, out var settings)) return;
                seen = stamp;
                current = settings;
            }
        }
        catch (Exception) { }
    }

    public void Dispose() => timer.Dispose();
}

/// <summary>
/// Who is attached remotely, right now (M.ORCH-T935): on each attach and detach it rewrites <c>watchers.json</c> and announces
/// a <c>term-watchers</c> message on the status bus (<see cref="TermWatchersBus"/>), so the owner's dashboard shows a "being
/// watched" banner on that session. The announcement goes off the request's thread and never blocks or throws.
/// </summary>
internal sealed class WatcherBoard
{
    private readonly object gate = new();
    private readonly List<TermWatcher> watchers = [];
    private readonly string? directory;
    private readonly Action<TermWatchersEvent>? publish;
    private int next;

    /// <param name="directory">Where <c>watchers.json</c> goes; null writes none (a test).</param>
    /// <param name="publish">Sends a message on the bus; null sends none (a test).</param>
    public WatcherBoard(string? directory, Action<TermWatchersEvent>? publish)
    {
        this.directory = directory;
        this.publish = publish;
    }

    /// <summary>The ones attached now.</summary>
    public IReadOnlyList<TermWatcher> Current
    {
        get { lock (gate) return [.. watchers]; }
    }

    /// <summary>Raised (off the caller's thread) with each change: what a test or the log watches.</summary>
    public event Action<TermWatchersEvent>? Changed;

    public TermWatcher Attach(string sessionId, string viewer, GatewayAccess access, string? from)
    {
        var watcher = new TermWatcher
        {
            Connection = $"{Environment.ProcessId}-{Interlocked.Increment(ref next)}",
            SessionId = sessionId, Viewer = viewer, Mode = access == GatewayAccess.Interact ? GatewayScopes.Interact : GatewayScopes.Watch,
            From = from, SinceUtc = DateTime.UtcNow,
        };
        Change(TermWatchersEvents.Attached, watcher, () => watchers.Add(watcher));
        return watcher;
    }

    public void Detach(TermWatcher watcher) =>
        Change(TermWatchersEvents.Detached, watcher, () => watchers.RemoveAll(w => w.Connection == watcher.Connection));

    /// <summary>The gateway started or is stopping: nobody is attached.</summary>
    public void Reset() => Change(TermWatchersEvents.Reset, null, watchers.Clear);

    /// <summary>Waits (briefly) for the announcements made so far: the gateway, before it exits.</summary>
    public void Drain(TimeSpan wait)
    {
        Task last;
        lock (gate) last = announced;
        try { last.Wait(wait); } catch (Exception) { }
    }

    private Task announced = Task.CompletedTask;

    /// <summary>Changes the list, rewrites the file and queues the announcement, in that order under the lock, so the file and
    /// the bus see the changes in the order they happened.</summary>
    private void Change(string what, TermWatcher? watcher, Action change)
    {
        lock (gate)
        {
            change();
            List<TermWatcher> now = [.. watchers];
            if (directory is not null)
            {
                if (now.Count == 0 && what == TermWatchersEvents.Reset) TermWatchersBus.Delete(directory);
                else TermWatchersBus.Write(now, directory);
            }
            var message = new TermWatchersEvent { Event = what, Watcher = watcher, Watchers = now, GatewayPid = Environment.ProcessId };
            var publish = this.publish;
            announced = announced.ContinueWith(_ =>
            {
                try { publish?.Invoke(message); } catch (Exception) { }
                try { Changed?.Invoke(message); } catch (Exception) { }
            }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        }
    }
}

/// <summary>What a bridge needs to apply the owner's choices to one session's remote stream (M.ORCH-T935).</summary>
internal sealed record BroadcastStream(string SessionId, DateTime StartedUtc, IBroadcastControl Control, SecretMasker Secrets);
