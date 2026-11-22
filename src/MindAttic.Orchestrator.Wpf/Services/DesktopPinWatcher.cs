using System.Diagnostics;
using System.Runtime.InteropServices;
using MindAttic.Vault.Settings;
using Serilog;

namespace MindAttic.Orchestrator.Wpf.Services;

/// <summary>
/// M.ORCH-T2030, ported from the Launcher's own (M.LNCH-T111): keeps MindAttic windows off whatever virtual desktop
/// (2+) the user is gaming on, and on Desktop 1 instead. Hosted by the always-running dashboard, started once in
/// <see cref="App"/> alongside <see cref="Tray"/> and disposed on its exit; the Launcher's own copy retires under
/// M.LNCH-T140.
///
/// <para><b>Detection:</b> <c>SetWinEventHook</c> on <c>EVENT_OBJECT_SHOW</c> and <c>EVENT_SYSTEM_FOREGROUND</c>, each with
/// its own exact-range hook (a <c>min..max</c> span across the two would also deliver every event code in between).
/// <c>WINEVENT_OUTOFCONTEXT</c> (no DLL injection) and <c>WINEVENT_SKIPOWNPROCESS</c> (the dashboard's own windows are never
/// candidates). The callback keeps only <c>idObject == OBJID_WINDOW</c> and <c>idChild == CHILDID_SELF</c>, a window that
/// is its own <c>GetAncestor(..., GA_ROOT)</c> and currently visible.</para>
///
/// <para><b>Scope (hard, see <see cref="DesktopPinPolicy.IsUnderMindAtticRoot"/>):</b> the events fire for every top-level
/// window on the system, so the one thing trusted is the owning process's image path, resolved by PID (never a title or
/// class name) and checked strictly against the workspace root. A path that can't be resolved (exited, access denied) is
/// "not MindAttic": only a window positively identified is ever moved.</para>
///
/// <para><b>The move</b> goes through <see cref="VirtualDesktops"/>, only when <see cref="MindAtticGlobalSettings.DefaultToDesktop1"/>
/// is on, more than one virtual desktop exists, and the window isn't already on Desktop 1 (see <see cref="DesktopPinPolicy.ShouldPin"/>).</para>
///
/// <para><b>Failure posture:</b> every step from the raw event onward is wrapped, so a bad window, a denied process handle or a
/// Windows update breaking the vendored DLL degrades to "this window wasn't moved": logged, never thrown into the dashboard.</para>
/// </summary>
public sealed class DesktopPinWatcher : IDisposable
{
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint EVENT_OBJECT_SHOW = 0x8002;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;
    private const int OBJID_WINDOW = 0;
    private const int CHILDID_SELF = 0;
    private const uint GA_ROOT = 2;
    private const uint WM_QUIT = 0x0012;

    private delegate void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventProc lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    private static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    private static readonly ILogger Log = OrchestratorLog.For("DesktopPin");

    private readonly string mindAtticRoot;

    // Fields, not locals: native code holds a raw pointer into these delegates while the hooks are installed, and a
    // delegate nothing managed references is fair game for the GC.
    private WinEventProc? showCallback;
    private WinEventProc? foregroundCallback;

    private IntPtr showHook;
    private IntPtr foregroundHook;
    private Thread? thread;
    private volatile uint nativeThreadId;

    // A window already confirmed on Desktop 1, or just moved there, isn't re-decided on every later event for the same
    // hwnd. Bounded in practice by how many MindAttic top-level windows exist at once. Touched only on the hook thread.
    private readonly HashSet<IntPtr> settled = [];

    /// <param name="mindAtticRoot">The workspace root a window's exe must live under (the Orchestrator's own workspace).</param>
    public DesktopPinWatcher(string mindAtticRoot)
    {
        this.mindAtticRoot = mindAtticRoot;
    }

    /// <summary>Starts the background hook thread. Idempotent: a second call is a no-op.</summary>
    public void Start()
    {
        if (thread is not null) return;
        thread = new Thread(RunMessageLoop) { IsBackground = true, Name = "MindAttic.DesktopPinWatcher" };
        thread.Start();
    }

    private void RunMessageLoop()
    {
        nativeThreadId = GetCurrentThreadId();
        showCallback = OnWinEvent;
        foregroundCallback = OnWinEvent;

        try
        {
            showHook = SetWinEventHook(EVENT_OBJECT_SHOW, EVENT_OBJECT_SHOW, IntPtr.Zero, showCallback,
                0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
            foregroundHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, foregroundCallback,
                0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

            if (showHook == IntPtr.Zero && foregroundHook == IntPtr.Zero)
            {
                Log.Warning("DesktopPinWatcher: SetWinEventHook failed for both events; the watcher is disabled for this run");
                return;
            }
            Log.Information("DesktopPinWatcher: hooked window show/foreground (show {Show}, foreground {Foreground}); pinning windows under {Root:l} to Desktop 1",
                showHook != IntPtr.Zero, foregroundHook != IntPtr.Zero, mindAtticRoot);

            // WINEVENT_OUTOFCONTEXT callbacks are delivered only to a thread pumping messages; this loop exists solely for that.
            while (GetMessage(out var msg, IntPtr.Zero, 0, 0))
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "DesktopPinWatcher: stopped after an unexpected error");
        }
        finally
        {
            if (showHook != IntPtr.Zero) { UnhookWinEvent(showHook); showHook = IntPtr.Zero; }
            if (foregroundHook != IntPtr.Zero) { UnhookWinEvent(foregroundHook); foregroundHook = IntPtr.Zero; }
        }
    }

    private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        try
        {
            if (idObject != OBJID_WINDOW || idChild != CHILDID_SELF) return; // a real top-level window object only
            if (hwnd == IntPtr.Zero || settled.Contains(hwnd)) return;
            if (!IsWindowVisible(hwnd)) return;
            if (GetAncestor(hwnd, GA_ROOT) != hwnd) return; // top-level only, not a child control or owned popup

            var imagePath = TryGetProcessImagePath(hwnd);
            if (!DesktopPinPolicy.IsUnderMindAtticRoot(imagePath, mindAtticRoot)) return;

            MindAtticGlobalSettings settings;
            try { settings = MindAtticGlobalSettings.Store().Load(); }
            catch (Exception ex)
            {
                // Can't confirm the setting is on: do nothing rather than guess.
                Log.Warning(ex, "DesktopPinWatcher: couldn't read Vault's Global settings; leaving {Window:l} alone", imagePath);
                return;
            }

            var count = VirtualDesktops.TryGetDesktopCount();
            var current = VirtualDesktops.TryGetWindowDesktopNumber(hwnd);

            if (!DesktopPinPolicy.ShouldPin(settings.DefaultToDesktop1, count, current))
            {
                if (current == 0) settled.Add(hwnd); // already home; stop re-checking this window
                return;
            }

            if (VirtualDesktops.TryMoveWindowToDesktop(hwnd, 0))
            {
                settled.Add(hwnd);
                Log.Information("DesktopPinWatcher: moved {Window:l} to Desktop 1 (was desktop {From})", imagePath, current + 1);
            }
        }
        catch (Exception ex)
        {
            // One bad event must never take the hook thread, or the dashboard hosting it, down.
            try { Log.Warning(ex, "DesktopPinWatcher: skipped a window after an unexpected error"); } catch { /* logging must never throw further */ }
        }
    }

    private static string? TryGetProcessImagePath(IntPtr hwnd)
    {
        try
        {
            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0) return null;
            using var process = Process.GetProcessById((int)pid);
            return process.MainModule?.FileName;
        }
        catch
        {
            // Exited, access denied (elevated or protected), or a bitness mismatch: not positively identified, so left alone.
            return null;
        }
    }

    /// <summary>Stops the hook thread. Best-effort and safe to call more than once.</summary>
    public void Dispose()
    {
        if (thread is null) return;
        try
        {
            if (nativeThreadId != 0) PostThreadMessage(nativeThreadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            thread.Join(TimeSpan.FromSeconds(2));
        }
        catch { /* best-effort; process exit tears the hooks down regardless */ }
        thread = null;
    }
}
