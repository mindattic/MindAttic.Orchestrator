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
        // The renderer reported its grid before this connection existed, so the host would keep its old size: tell it now.
        if (terminal.Rows > 0 && terminal.Columns > 0) c.Resize((uint)terminal.Rows, (uint)terminal.Columns);
        terminal.Focus();
    }

    private void Disconnect()
    {
        connection?.Close();
    }

    /// <summary>Ctrl+V and Shift+Insert: the app's TerminalPaste payload (CR newlines, bracketed when the host's CLI turned that on).</summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // As the app's TerminalKeyboard (M.ORCH-T852): WPF's keyboard navigation takes the arrow, Home, End and Page keys before
        // the terminal's native window sees them, so while it holds focus they are posted straight to it.
        if (!e.Handled && Keyboard.FocusedElement is System.Windows.Interop.HwndHost
            && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) == 0
            && e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown
            && connection is { } nav)
        {
            // Sent straight to the CLI as win32-input-mode key events (ESC[Vk;Sc;Uc;Kd;Cs;Rc_, down then up, ENHANCED_KEY set:
            // these are the grey keys), the same encoding the renderer produces for every other key. Posting WM_KEYDOWN to the
            // renderer's window lost keys and reordered them against the typed text.
            var vk = KeyInterop.VirtualKeyFromKey(e.Key);
            var sc = MapVirtualKey((uint)vk, 0);
            nav.WriteInput($"\u001b[{vk};{sc};0;1;256;1_\u001b[{vk};{sc};0;0;256;1_");
            e.Handled = true;
            return;
        }
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

    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern uint MapVirtualKey(uint code, uint mapType);

    private static uint Ref(int rgb) => (uint)(((rgb & 0xFF) << 16) | (rgb & 0xFF00) | ((rgb >> 16) & 0xFF));
}

internal static class Program
{
    [STAThread]
    private static void Main() => new Application().Run(new MainWindow());
}
