using System.Windows;
using MindAttic.Orchestrator.Setup;
using MindAttic.Orchestrator.Wpf;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The only way a test shows a WPF window (M.ORCH-T530): far off every screen, borderless, fully transparent
/// (so even maximized onto a monitor it draws nothing and clicks pass through), out of the taskbar and Alt+Tab, and
/// never activated. Tests ran while agents worked and popped blank 'testhost' windows onto the user's screen.
/// <see cref="TestWindowHygieneTests"/> fails any test source that shows or activates a window any other way.</summary>
internal static class OffScreenWindow
{
    public const double Offset = -20000;

    /// <summary>A window set up as above, not yet shown.</summary>
    public static Window Create(double width = 200, double height = 120, object? content = null) => new()
    {
        Content = content, Width = width, Height = height, Left = Offset, Top = Offset,
        WindowStartupLocation = WindowStartupLocation.Manual,
        WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, AllowsTransparency = true, Opacity = 0,
        ShowInTaskbar = false, ShowActivated = false, Topmost = false, Focusable = false, Title = "MindAttic test window (off-screen)",
    };

    /// <summary>The first-run workspace builder, set up as above and never shown: its test drives the build itself, with
    /// <paramref name="clone"/> standing in for the clone (M.ORCH-T555).</summary>
    public static BuildWorkspaceWindow BuildWorkspace(string folder, Func<IProgress<string>, Task<BootstrapResult>> clone) =>
        new(folder, Serilog.Core.Logger.None, clone)
        {
            Width = 200, Height = 120, Left = Offset, Top = Offset, WindowStartupLocation = WindowStartupLocation.Manual,
            WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, AllowsTransparency = true, Opacity = 0,
            ShowInTaskbar = false, ShowActivated = false, Topmost = false, Focusable = false,
        };

    /// <summary>Creates and shows one without activating it.</summary>
    public static Window Show(double width = 200, double height = 120, object? content = null)
    {
        var window = Create(width, height, content);
        window.Show();
        return window;
    }
}
