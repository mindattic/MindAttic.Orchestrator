using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Terminal.Wpf;
using MindAttic.Orchestrator.Wpf;
using TermHostStub;

namespace RelayViewer;

/// <summary>A toolbar over a bare Microsoft.Terminal.Wpf TerminalControl wired to a RelayConnection (M.ORCH-T923).</summary>
internal sealed class MainWindow : Window
{
    private readonly TerminalControl terminal = new() { Focusable = true, AutoResize = true };
    private readonly TextBox idBox = new() { Text = "p1", Width = 140, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
    private readonly TextBlock status = new() { Text = "not connected", Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock size = new() { Text = "- x -", Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, FontFamily = new System.Windows.Media.FontFamily("Consolas") };
    private RelayConnection? connection;

    public MainWindow()
    {
        Title = "RelayViewer (M.ORCH-T923)";
        Width = 1100; Height = 700;

        var connect = new Button { Content = "Connect", Padding = new Thickness(12, 3, 12, 3), Margin = new Thickness(0, 0, 6, 0) };
        var disconnect = new Button { Content = "Disconnect", Padding = new Thickness(12, 3, 12, 3) };
        connect.Click += (_, _) => Connect();
        disconnect.Click += (_, _) => Disconnect();
        idBox.KeyDown += (_, e) => { if (e.Key == Key.Enter) Connect(); };

        var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6) };
        bar.Children.Add(new TextBlock { Text = "Session id ", VerticalAlignment = VerticalAlignment.Center });
        bar.Children.Add(idBox);
        bar.Children.Add(connect);
        bar.Children.Add(disconnect);
        bar.Children.Add(status);
        bar.Children.Add(size);

        // Keyboard navigation stays inside the terminal (Tab and the arrows are the CLI's), like EasyTerminalControl's InputCapture,
        // and focus on the host border is forwarded to the terminal.
        var host = new Border { Focusable = true, Child = terminal };
        KeyboardNavigation.SetTabNavigation(host, KeyboardNavigationMode.Contained);
        KeyboardNavigation.SetDirectionalNavigation(host, KeyboardNavigationMode.Contained);
        host.GotFocus += (_, _) => terminal.Focus();
        terminal.Focusable = true;

        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);
        root.Children.Add(host);
        Content = root;

        Loaded += (_, _) => terminal.SetTheme(Campbell, "Cascadia Code", 12);
        PreviewKeyDown += OnPreviewKeyDown;
        Closed += (_, _) => connection?.Close();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        timer.Tick += (_, _) => size.Text = $"{terminal.Columns} x {terminal.Rows}";
        timer.Start();
    }

    private void Connect()
    {
        connection?.Close();
        var c = new RelayConnection(idBox.Text.Trim());
        connection = c;
        c.StatusChanged += s => Dispatcher.BeginInvoke(() => { if (connection == c) status.Text = s; });
        // Setting Connection resets the renderer (ESC c), then calls Start(); the replay follows from the host.
        terminal.Connection = c;
        terminal.Focus();
    }

    private void Disconnect()
    {
        connection?.Close();
    }

    /// <summary>Ctrl+V and Shift+Insert: the app's TerminalPaste payload (CR newlines, bracketed when the host's CLI turned that on).</summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled || !TerminalPaste.IsPasteChord(e) || connection is not { } c) return;
        e.Handled = true;
        string text;
        try { text = Clipboard.ContainsText() ? Clipboard.GetText() : ""; }
        catch (System.Runtime.InteropServices.ExternalException) { return; }
        var payload = TerminalPaste.Payload(text, (c.Modes & Protocol.Bracketed) != 0);
        if (payload.Length > 0) c.WriteInput(payload);
    }

    /// <summary>Windows Terminal's Campbell scheme, copied from the app's TerminalPaneViewModel.Campbell.</summary>
    private static readonly TerminalTheme Campbell = new()
    {
        DefaultBackground = Ref(0x0C0C0C),
        DefaultForeground = Ref(0xCCCCCC),
        DefaultSelectionBackground = Ref(0xFFFFFF),
        CursorStyle = CursorStyle.BlinkingBar,
        ColorTable =
        [
            Ref(0x0C0C0C), Ref(0xC50F1F), Ref(0x13A10E), Ref(0xC19C00), Ref(0x0037DA), Ref(0x881798), Ref(0x3A96DD), Ref(0xCCCCCC),
            Ref(0x767676), Ref(0xE74856), Ref(0x16C60C), Ref(0xF9F1A5), Ref(0x3B78FF), Ref(0xB4009E), Ref(0x61D6D6), Ref(0xF2F2F2),
        ],
    };

    private static uint Ref(int rgb) => (uint)(((rgb & 0xFF) << 16) | (rgb & 0xFF00) | ((rgb >> 16) & 0xFF));
}

internal static class Program
{
    [STAThread]
    private static void Main() => new Application().Run(new MainWindow());
}
