using System.Windows;
using System.Windows.Controls;

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
        var area = SystemParameters.WorkArea;
        if (WindowState == WindowState.Normal && (ActualWidth > area.Width + 1 || ActualHeight > area.Height + 1))
            WindowState = WindowState.Maximized;
    }

    private void ApplyCompact()
    {
        var want = WantsCompact(ActualWidth, ActualHeight, compact);
        if (want == compact) return;
        compact = want;
        Hold(Sidebar, VisibilityProperty, Visibility.Collapsed);
        Hold(StatusBar, VisibilityProperty, Visibility.Collapsed);
        Hold(TouchBar, VisibilityProperty, Visibility.Visible);
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
        Shown.Terminals.SelectedPane?.WriteInput(sequence);
    }
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
    };

    public static string? Sequence(string name) => Sequences.GetValueOrDefault(name);
}
