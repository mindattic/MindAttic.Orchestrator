using System.Runtime.InteropServices;

namespace MindAttic.Orchestrator.Setup;

/// <summary>
/// One dashboard per user session (M.ORCH-T497): the first to <see cref="Claim"/> a named mutex owns it for its life;
/// a later launch signals the owner's show event (it brings its window up, restored if minimized) and exits, so
/// launches never pile up copies. A launch that finds the mutex held but no show event
/// (the owner is exiting for an upgrade, or still starting) waits a moment for it rather than leaving no dashboard.
/// </summary>
public sealed partial class SingleInstance : IDisposable
{
    public const string DashboardName = @"Local\MindAttic.Orchestrator.Dashboard";

    private readonly Mutex mutex;
    private readonly EventWaitHandle show;
    private readonly Thread owner;
    private RegisteredWaitHandle? registration;

    private SingleInstance(Mutex mutex, string name)
    {
        this.mutex = mutex;
        owner = Thread.CurrentThread;
        show = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEvent(name));
    }

    public static string ShowEvent(string name) => name + ".Show";

    /// <summary>The instance when this process is the first, else null after asking the running one to show itself.
    /// Ownership belongs to the calling thread: dispose it there.</summary>
    public static SingleInstance? Claim(string name, TimeSpan wait)
    {
        var mutex = new Mutex(false, name + ".Instance");
        if (TryOwn(mutex, TimeSpan.Zero)) return new SingleInstance(mutex, name);
        if (!SignalShow(name))
        {
            if (TryOwn(mutex, wait)) return new SingleInstance(mutex, name);
            SignalShow(name); // the owner was still starting: its show event may be there now
        }
        mutex.Dispose();
        return null;
    }

    /// <summary>Runs <paramref name="show"/> (on a pool thread) each time a later launch asks this one to come forward.</summary>
    public void OnShowRequested(Action show) =>
        registration = ThreadPool.RegisterWaitForSingleObject(this.show, (_, _) => show(), null, Timeout.Infinite, executeOnlyOnce: false);

    private static bool TryOwn(Mutex mutex, TimeSpan wait)
    {
        try { return mutex.WaitOne(wait); }
        catch (AbandonedMutexException) { return true; } // the owner died without releasing it (killed): ours now
    }

    private static bool SignalShow(string name)
    {
        if (!EventWaitHandle.TryOpenExisting(ShowEvent(name), out var show)) return false;
        // This launch has the foreground right (the user just started it); hand it on so the running dashboard can take it.
        if (OperatingSystem.IsWindows()) AllowSetForegroundWindow(AsfwAny);
        using (show) show.Set();
        return true;
    }

    public void Dispose()
    {
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
