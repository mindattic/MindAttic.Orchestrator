using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// The phone layout (M.ORCH-T1543): a window narrower than <see cref="CompactBelow"/>, or portrait and narrower than
/// <see cref="CompactPortraitBelow"/> (a Remote Desktop session sized to a phone, over Tailscale), drops the people sidebar,
/// the status bar and the host readouts, lets the chosen CLI fill the first screen, and shows <see cref="TouchBar"/>'s keys a
/// phone keyboard lacks. Widening the window puts the desktop layout back exactly as it was. Only properties the XAML doesn't
/// bind are changed, and each one's own value is put back.
/// </summary>
public partial class MainWindow
{
    internal const double CompactBelow = 760;
    internal const double CompactPortraitBelow = 1000;

    /// <summary>How far past a breakpoint the window must widen to leave the phone layout, so a drag across it doesn't flicker.</summary>
    internal const double CompactHysteresis = 40;

    private readonly Dictionary<(DependencyObject, DependencyProperty), object> desktopValues = [];
    private bool compact;

    /// <summary>Whether the window fits the phone layout at <paramref name="width"/> x <paramref name="height"/> (device-independent
    /// pixels), given whether it is in it <paramref name="now"/>.</summary>
    internal static bool WantsCompact(double width, double height, bool now)
    {
        if (width <= 0 || height <= 0) return now;
        var slack = now ? CompactHysteresis : 0;
        return width < CompactBelow + slack || (height > width && width < CompactPortraitBelow + slack);
    }

    internal bool IsCompact => compact;

    private void AttachCompactLayout()
    {
        // The phone layout's drawer (the sidebar, M.ORCH-T1570) closes once a person is chosen in it.
        model.SelectedTabChanged += (_, _) => { if (compact) Sidebar.Visibility = Visibility.Collapsed; };
        SizeChanged += (_, _) => ApplyCompact();
        // Reconnecting Remote Desktop from a phone shrinks the display under a window sized for the desk: it would hang off the
        // screen, so it's maximized to whatever the display is now.
        Loaded += (_, _) => FitToScreen();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        Closed += (_, _) => Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(FitToScreen);

    private void FitToScreen()
    {
        if (WindowState != WindowState.Normal) return;
        // The work area of the monitor the window is on, not the primary one's (SystemParameters.WorkArea): a window on a larger
        // second monitor was maximized for being bigger than the primary's screen.
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (hwnd == IntPtr.Zero || !GetMonitorInfo(MonitorFromWindow(hwnd, MonitorDefaultToNearest), ref info)) return;
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        var width = (info.Work.Right - info.Work.Left) / dpi.DpiScaleX;
        var height = (info.Work.Bottom - info.Work.Top) / dpi.DpiScaleY;
        if (ActualWidth > width + 1 || ActualHeight > height + 1) WindowState = WindowState.Maximized;
    }

    private const uint MonitorDefaultToNearest = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    private void ApplyCompact()
    {
        var want = WantsCompact(ActualWidth, ActualHeight, compact);
        if (want == compact) return;
        compact = want;
        Hold(Sidebar, VisibilityProperty, Visibility.Collapsed);
        Hold(StatusBar, VisibilityProperty, Visibility.Collapsed);
        Hold(TouchBar, VisibilityProperty, Visibility.Visible);
        // Who, as a picker at the top left in place of the sidebar (M.ORCH-T1568); the CLI tab strip stays, the row under it
        // (M.ORCH-T1571). The readouts and status bar it hides are a wrapped line in the touch bar (PhoneStatus, M.ORCH-T1570).
        Hold(PhonePickers, VisibilityProperty, Visibility.Visible);
        // MaxWidth, not Visibility: their Visibility is bound to whether they have anything to say.
        foreach (var readout in new FrameworkElement[] { AllocatePausedText, DiskFreeText, RamUtilizationText, RamWarnText, RamBadText })
            Hold(readout, MaxWidthProperty, 0d);
        Hold(TerminalsPanel, MarginProperty, new Thickness(6, 4, 6, 0));
        Hold(AgentsBody, ScrollViewer.PanningModeProperty, PanningMode.VerticalOnly);
        SizeTerminalRow();
        if (compact) AgentsBody.ScrollToTop();
    }

    /// <summary>Sets <paramref name="property"/> to <paramref name="compactValue"/> entering the phone layout, and back to the
    /// desktop's own value (or none) leaving it.</summary>
    private void Hold(DependencyObject element, DependencyProperty property, object compactValue)
    {
        var key = (element, property);
        if (compact)
        {
            if (!desktopValues.ContainsKey(key)) desktopValues[key] = element.ReadLocalValue(property);
            element.SetValue(property, compactValue);
        }
        else if (desktopValues.Remove(key, out var desktop))
        {
            if (desktop == DependencyProperty.UnsetValue) element.ClearValue(property);
            else element.SetValue(property, desktop);
        }
    }

    /// <summary>A touch key: writes its <see cref="FrameworkElement.Tag"/>'s sequence to the CLI on screen. The keys aren't
    /// focusable, so the terminal keeps the keyboard.</summary>
    private void TouchKey_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string name } || TouchKeys.Sequence(name) is not { } sequence) return;
        ShownPane?.WriteInput(sequence);
    }

    /// <summary>The CLI on screen; null on the Usage tab, where the chosen CLI is hidden and nothing should type into it.</summary>
    private ViewModels.TerminalPaneViewModel? ShownPane => Shown.Terminals.ShowsTerminal ? Shown.Terminals.SelectedPane : null;

    /// <summary>The mic (M.ORCH-T1544): the say box takes the keyboard, then Windows voice typing (Win+H) opens on it. Over
    /// Remote Desktop it hears the phone's microphone once the Windows App redirects it.</summary>
    private void MicButton_Click(object sender, RoutedEventArgs e)
    {
        SayBox.Focus();
        Keyboard.Focus(SayBox);
        SayBox.CaretIndex = SayBox.Text.Length;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, VoiceTyping.Open);
    }

    private void SayButton_Click(object sender, RoutedEventArgs e) => Say();

    private void SayBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None) return;
        e.Handled = true;
        Say();
    }

    /// <summary>Pastes the say box's text into the CLI on screen as a paste (so a long dictation lands whole in its prompt)
    /// and submits it; an empty box just sends Enter.</summary>
    private void Say()
    {
        if (ShownPane is not { } pane) return;
        var text = SayBox.Text.Trim();
        if (text.Length > 0 && !pane.WriteInput(TerminalPaste.Payload(text, pane.Bracketed))) return;
        pane.WriteInput("\r");
        SayBox.Clear();
    }
}

/// <summary>Opens Windows voice typing (Win+H) on whatever has the keyboard (M.ORCH-T1544).</summary>
internal static class VoiceTyping
{
    private const byte WinKey = 0x5B, HKey = 0x48;
    private const uint KeyUp = 0x0002;

    public static void Open()
    {
        keybd_event(WinKey, 0, 0, 0);
        keybd_event(HKey, 0, 0, 0);
        keybd_event(HKey, 0, KeyUp, 0);
        keybd_event(WinKey, 0, KeyUp, 0);
    }

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte vk, byte scan, uint flags, nuint extraInfo);
}

/// <summary>What each touch key sends a CLI: the bytes the key itself would (M.ORCH-T1543).</summary>
internal static class TouchKeys
{
    private static readonly Dictionary<string, string> Sequences = new(StringComparer.OrdinalIgnoreCase)
    {
        ["esc"] = "\x1b",
        ["tab"] = "\t",
        ["shift-tab"] = "\x1b[Z",
        ["up"] = "\x1b[A",
        ["down"] = "\x1b[B",
        ["right"] = "\x1b[C",
        ["left"] = "\x1b[D",
        ["ctrl-c"] = "\x03",
        ["enter"] = "\r",
        ["voice"] = "/voice\r",
    };

    public static string? Sequence(string name) => Sequences.GetValueOrDefault(name);
}
