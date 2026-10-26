using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace MindAttic.Orchestrator.Setup;

/// <summary>
/// One dashboard per user session (M.ORCH-T497): the first to <see cref="Claim"/> a named mutex owns it for its life;
/// a later launch signals the owner's show event (it brings its window up, restored if minimized) and exits, so
/// launches never pile up copies. A launch that finds the mutex held but no show event
/// (the owner is exiting for an upgrade, or still starting) waits a moment for it rather than leaving no dashboard.
/// A launch with something to hand over (<c>--start &lt;repo&gt;</c>, M.ORCH-T767) sends its arguments down the owner's
/// pipe instead, which only this user can open, and the owner acts on them as it comes forward.
/// </summary>
public sealed partial class SingleInstance : IDisposable
{
    public const string DashboardName = @"Local\MindAttic.Orchestrator.Dashboard";

    /// <summary>The most a hand-off may send: a few arguments, never a payload.</summary>
    internal const int MaxHandOffBytes = 16 * 1024;

    private static readonly TimeSpan PipeConnectTimeout = TimeSpan.FromSeconds(3);

    private readonly Mutex mutex;
    private readonly EventWaitHandle show;
    private readonly Thread owner;
    private readonly CancellationTokenSource stop = new();
    private readonly object gate = new();
    private readonly Queue<IReadOnlyList<string>> early = new(); // handed over before anyone listened
    private Action<IReadOnlyList<string>>? handler;
    private RegisteredWaitHandle? registration;

    private SingleInstance(Mutex mutex, string name)
    {
        this.mutex = mutex;
        owner = Thread.CurrentThread;
        show = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEvent(name));
        // Listening from the start: a hand-off that arrives while the window is still opening waits for OnShowRequested.
        _ = ListenAsync(PipeName(name), stop.Token);
    }

    public static string ShowEvent(string name) => name + ".Show";

    /// <summary>The mutex the running instance holds for its life.</summary>
    public static string MutexName(string name) => name + ".Instance";

    /// <summary>The hand-off pipe: per name and per Windows session, as the <c>Local\</c> mutex and event are.</summary>
    internal static string PipeName(string name) =>
        name.Replace('\\', '.') + ".HandOff." + System.Diagnostics.Process.GetCurrentProcess().SessionId;

    /// <summary>The instance when this process is the first, else null after asking the running one to show itself, handing
    /// it <paramref name="args"/> when there are any. Ownership belongs to the calling thread: dispose it there.</summary>
    public static SingleInstance? Claim(string name, TimeSpan wait, IReadOnlyList<string>? args = null)
    {
        args ??= [];
        var mutex = new Mutex(false, MutexName(name));
        if (TryOwn(mutex, TimeSpan.Zero)) return new SingleInstance(mutex, name);
        if (!SignalShow(name, args))
        {
            if (TryOwn(mutex, wait)) return new SingleInstance(mutex, name);
            SignalShow(name, args); // the owner was still starting: its show event may be there now
        }
        mutex.Dispose();
        return null;
    }

    /// <summary>Runs <paramref name="show"/> (on a pool thread) each time a later launch asks this one to come forward.</summary>
    public void OnShowRequested(Action show) => OnShowRequested(_ => show());

    /// <summary>Runs <paramref name="show"/> (on a pool thread) each time a later launch asks this one to come forward, with the
    /// arguments it handed over (none for a plain launch).</summary>
    public void OnShowRequested(Action<IReadOnlyList<string>> show)
    {
        List<IReadOnlyList<string>> waiting;
        lock (gate)
        {
            handler = show;
            waiting = [.. early];
            early.Clear();
        }
        registration = ThreadPool.RegisterWaitForSingleObject(this.show, (_, _) => Guarded(show, []), null, Timeout.Infinite, executeOnlyOnce: false);
        foreach (var args in waiting) ThreadPool.QueueUserWorkItem(_ => Guarded(show, args));
    }

    /// <summary>A pool-thread callback that throws ends the whole process, terminals and all (M.ORCH-T869): log it and carry on.</summary>
    private static void Guarded(Action<IReadOnlyList<string>> show, IReadOnlyList<string> args)
    {
        try { show(args); }
        catch (Exception ex) { OrchestratorLog.For("Dashboard").Error(ex, "A request to show the dashboard failed; contained"); }
    }

    private static bool TryOwn(Mutex mutex, TimeSpan wait)
    {
        try { return mutex.WaitOne(wait); }
        catch (AbandonedMutexException) { return true; } // the owner died without releasing it (killed): ours now
    }

    private static bool SignalShow(string name, IReadOnlyList<string> args)
    {
        if (!EventWaitHandle.TryOpenExisting(ShowEvent(name), out var show)) return false;
        // This launch has the foreground right (the user just started it); hand it on so the running dashboard can take it.
        if (OperatingSystem.IsWindows()) AllowSetForegroundWindow(AsfwAny);
        using (show)
            // An owner from before the hand-off has no pipe: it still comes forward, without the arguments.
            if (args.Count == 0 || !TrySend(name, args)) show.Set();
        return true;
    }

    private static bool TrySend(string name, IReadOnlyList<string> args)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(args);
        if (bytes.Length > MaxHandOffBytes) return false;
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName(name), PipeDirection.Out, PipeOptions.CurrentUserOnly);
            pipe.Connect(PipeConnectTimeout);
            pipe.Write(bytes);
            pipe.Flush();
            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException) { return false; }
    }

    private async Task ListenAsync(string pipeName, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(ct);
                using var read = CancellationTokenSource.CreateLinkedTokenSource(ct);
                read.CancelAfter(TimeSpan.FromSeconds(5)); // a launch that connects and sends nothing doesn't hold the pipe
                if (await ReadHandOffAsync(server, read.Token) is { } args) Deliver(args);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (ObjectDisposedException) { return; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                // A dropped or slow launch, or the pipe name briefly taken: wait a moment and listen again.
                try { await Task.Delay(250, ct); }
                catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException) { return; }
            }
        }
    }

    /// <summary>The arguments a launch sent: a JSON array of strings, at most <see cref="MaxHandOffBytes"/>; null for anything else.</summary>
    internal static async Task<IReadOnlyList<string>?> ReadHandOffAsync(Stream from, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int n;
        while ((n = await from.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + n > MaxHandOffBytes) return null;
            buffer.Write(chunk, 0, n);
        }
        return ParseHandOff(buffer.ToArray());
    }

    internal static IReadOnlyList<string>? ParseHandOff(byte[] bytes)
    {
        try { return JsonSerializer.Deserialize<string?[]>(bytes) is { } args && args.All(a => a is not null) ? args.Select(a => a!).ToArray() : null; }
        catch (Exception ex) when (ex is JsonException or DecoderFallbackException or NotSupportedException or ArgumentException) { return null; }
    }

    private void Deliver(IReadOnlyList<string> args)
    {
        Action<IReadOnlyList<string>>? to;
        lock (gate)
        {
            to = handler;
            if (to is null) { early.Enqueue(args); return; }
        }
        ThreadPool.QueueUserWorkItem(_ => Guarded(to, args));
    }

    public void Dispose()
    {
        stop.Cancel();
        registration?.Unregister(null);
        show.Dispose();
        if (Thread.CurrentThread == owner)
            try { mutex.ReleaseMutex(); }
            catch (ApplicationException) { } // not held: nothing to release
        mutex.Dispose(); // from another thread the mutex is released when this process exits
    }

    private const int AsfwAny = -1;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllowSetForegroundWindow(int processId);
}
