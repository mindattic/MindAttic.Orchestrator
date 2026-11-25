using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Gateway;

/// <summary>The owner's broadcast choices as they are now (M.ORCH-T935): which sessions are shared, masked and paused.</summary>
internal interface IBroadcastControl
{
    TermBroadcastSettings Current { get; }

    /// <summary>Raised after <see cref="Current"/> changes, so a stream acts on it at once rather than on a tick (M.ORCH-T2215).</summary>
    event Action? Changed;
}

/// <summary>Fixed choices that a test changes by hand.</summary>
internal sealed class FixedBroadcast(TermBroadcastSettings settings) : IBroadcastControl
{
    private TermBroadcastSettings current = settings;

    public TermBroadcastSettings Current
    {
        get => current;
        set
        {
            current = value;
            Changed?.Invoke();
        }
    }

    public event Action? Changed;
}

/// <summary>
/// <c>broadcast.json</c> (<see cref="TermBroadcast"/>), reread whenever it changes: a FileSystemWatcher on its folder reads it
/// <see cref="Debounce"/> after a write (M.ORCH-T2215), with a <see cref="Backstop"/> look for a missed event, so a share, mask
/// or pause from the dashboard or <c>orchestrator broadcast</c> takes effect within a fraction of a second, without restarting
/// the gateway. A file that can't be read keeps the last good settings and is read again <see cref="RetryAfter"/>.
/// </summary>
internal sealed class BroadcastFile : IBroadcastControl, IDisposable
{
    public static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(50);
    public static readonly TimeSpan RetryAfter = TimeSpan.FromMilliseconds(150);
    public static readonly TimeSpan Backstop = TimeSpan.FromSeconds(10);

    private readonly string? directory;
    private readonly Timer timer;
    // The watcher's events and the timer's polls come on different threads: 'seen', the watcher and each reschedule are
    // protected so an event during a poll is never overwritten by that poll's later Backstop (bughunt 2026-10-28, M.ORCH-T2215).
    // 'current' stays volatile; it's only ever replaced wholesale, never read-modify-written.
    private readonly object pollGate = new();
    private (DateTime Written, long Length) seen;
    private volatile TermBroadcastSettings current;
    private FileSystemWatcher? watcher;
    private bool disposed;

    public BroadcastFile(string? directory)
    {
        this.directory = directory;
        var stamp = Stamp();
        // Not readable at start: nothing shared until it is, and the poll keeps trying (M.ORCH-T986).
        var read = TermBroadcast.TryRead(directory, out var settings);
        seen = read ? stamp : (DateTime.MinValue, long.MinValue);
        current = settings;
        timer = new Timer(_ => Poll(), null, Timeout.Infinite, Timeout.Infinite);
        lock (pollGate)
        {
            Watch();
            timer.Change(read ? Backstop : RetryAfter, Timeout.InfiniteTimeSpan);
        }
    }

    public TermBroadcastSettings Current => current;

    public event Action? Changed;

    /// <summary>Starts the watcher when the folder exists; a folder made later is picked up by the backstop poll.</summary>
    private void Watch()
    {
        if (watcher is not null || disposed) return;
        try
        {
            var path = TermBroadcast.PathIn(directory);
            var folder = Path.GetDirectoryName(path);
            if (folder is null || !Directory.Exists(folder)) return;
            var w = new FileSystemWatcher(folder, Path.GetFileName(path))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime,
            };
            w.Changed += (_, _) => Soon();
            w.Created += (_, _) => Soon();
            // A writer replaces the file atomically (temp file renamed over it).
            w.Renamed += (_, _) => Soon();
            w.Deleted += (_, _) => Soon();
            w.Error += (_, e) => Lost(w, e.GetException());
            w.EnableRaisingEvents = true;
            watcher = w;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or PlatformNotSupportedException) { }
    }

    /// <summary>An overflow only lost events; anything else (the folder gone) lost the watcher, which the backstop poll restarts.</summary>
    private void Lost(FileSystemWatcher w, Exception error)
    {
        lock (pollGate)
        {
            if (error is not InternalBufferOverflowException && watcher == w)
            {
                watcher = null;
                try { w.Dispose(); } catch (Exception) { }
            }
        }
        Soon();
    }

    private void Soon()
    {
        try
        {
            lock (pollGate)
                if (!disposed) timer.Change(Debounce, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException) { }
    }

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
        var changed = false;
        try
        {
            lock (pollGate)
            {
                if (disposed) return;
                var next = Backstop;
                try
                {
                    Watch();
                    var stamp = Stamp();
                    if (stamp != seen)
                    {
                        // Not readable just now (a sharing violation as a writer replaces it): the last good settings stay, and the
                        // stamp isn't taken, so it's read again shortly rather than on the next change (M.ORCH-T986).
                        if (TermBroadcast.TryRead(directory, out var settings))
                        {
                            seen = stamp;
                            current = settings;
                            changed = true;
                        }
                        else next = RetryAfter;
                    }
                }
                finally { timer.Change(next, Timeout.InfiniteTimeSpan); }
            }
        }
        catch (Exception) { }
        if (changed)
            try { Changed?.Invoke(); } catch (Exception) { }
    }

    public void Dispose()
    {
        lock (pollGate)
        {
            disposed = true;
            watcher?.Dispose();
            watcher = null;
        }
        timer.Dispose();
    }
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

    /// <summary>A snapshot of the remote viewers attached now.</summary>
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
