using System.Runtime.InteropServices;
using System.Windows.Automation;
using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>How far "Go to tab" got: the tab itself, its window only, or nothing (with why).</summary>
public enum GoToOutcome { Tab, Window, NotFound }

public sealed record GoToResult(GoToOutcome Outcome, string Note);

/// <summary>
/// "Go to tab" (M.ORCH-T619): brings a Launcher host's own Windows Terminal window to the front, on its tab. The window is
/// found from the host's pid (its WindowsTerminal.exe ancestor), the tab by UI Automation and <see cref="TerminalTabPicker"/>.
/// Blocking UIA and Win32 calls: run it off the UI thread.
/// </summary>
public static partial class TerminalFocus
{
    public static GoToResult GoTo(HostSession host, IEnumerable<HostSession> allHosts)
    {
        try
        {
            var terminal = ProcessTree.Ancestors(host.ProcessId)
                .FirstOrDefault(a => a.Exe.Equals("WindowsTerminal.exe", StringComparison.OrdinalIgnoreCase));
            if (terminal.Pid == 0)
                return new(GoToOutcome.NotFound, $"{host.Provider} tab (PID {host.ProcessId}) isn't running in a Windows Terminal window.");

            var windows = AutomationElement.RootElement.FindAll(TreeScope.Children,
                new PropertyCondition(AutomationElement.ProcessIdProperty, terminal.Pid)).Cast<AutomationElement>().ToList();
            var tabs = new List<(TerminalTab Tab, AutomationElement Element)>();
            foreach (var w in windows)
            {
                var hwnd = (long)w.Current.NativeWindowHandle;
                foreach (AutomationElement t in w.FindAll(TreeScope.Descendants,
                             new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem)))
                    tabs.Add((new TerminalTab(hwnd, t.Current.Name ?? ""), t));
            }

            var title = TerminalTabPicker.ExpectedTitle(host);
            var others = allHosts.Where(h => h.ProcessId != host.ProcessId).Select(TerminalTabPicker.ExpectedTitle);
            var list = tabs.Select(t => t.Tab).ToList();
            if (TerminalTabPicker.PickTab(list, title, others) is { } picked)
            {
                var element = tabs.First(t => ReferenceEquals(t.Tab, picked)).Element;
                if (element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var p)) ((SelectionItemPattern)p).Select();
                Foreground(new IntPtr(picked.Window));
                return new(GoToOutcome.Tab, $"Brought up \"{picked.Name}\".");
            }
            if (TerminalTabPicker.PickWindow(list, [.. windows.Select(w => (long)w.Current.NativeWindowHandle)], title) is { } window)
            {
                Foreground(new IntPtr(window));
                return new(GoToOutcome.Window, $"Brought up its Windows Terminal window; couldn't tell which tab is \"{title}\".");
            }
            return new(GoToOutcome.NotFound, $"Couldn't tell which Windows Terminal tab is \"{title ?? $"PID {host.ProcessId}"}\".");
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or COMException or ArgumentException)
        {
            return new(GoToOutcome.NotFound, $"Couldn't reach Windows Terminal: {ex.Message}");
        }
    }

    /// <summary>Restores the window and makes it the foreground one. This process just took the user's click, so it may hand
    /// the foreground on; when Windows refuses anyway, the input queues are joined for the call, the usual way round it.</summary>
    private static void Foreground(IntPtr hwnd)
    {
        if (IsIconic(hwnd)) ShowWindow(hwnd, SwRestore);
        if (SetForegroundWindow(hwnd)) return;
        var foreground = GetForegroundWindow();
        var theirs = GetWindowThreadProcessId(foreground, IntPtr.Zero);
        var ours = GetCurrentThreadId();
        var attached = theirs != ours && AttachThreadInput(ours, theirs, true);
        try
        {
            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);
        }
        finally { if (attached) AttachThreadInput(ours, theirs, false); }
    }

    private const int SwRestore = 9;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(IntPtr hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr hwnd, int cmd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool BringWindowToTop(IntPtr hwnd);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(IntPtr hwnd, IntPtr processId);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetCurrentThreadId();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachThreadInput(uint attach, uint attachTo, [MarshalAs(UnmanagedType.Bool)] bool doAttach);
}
