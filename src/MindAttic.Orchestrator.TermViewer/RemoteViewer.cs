using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Term;
using MindAttic.Orchestrator.Term.Wpf;

namespace MindAttic.Orchestrator.TermViewer;

/// <summary>
/// <c>MindAttic.Orchestrator.TermViewer --remote &lt;name&gt; --session &lt;id&gt; [--interact]</c> (M.ORCH-T936): the Team Wall's
/// pop-out for a teammate's CLI, through their gateway. The remote (URL, pin, sealed token) is read from the settings by name,
/// so the token never appears on a command line. Watch only unless <c>--interact</c> is given and the gateway says the grant
/// allows it (<c>access: interact</c>).
/// </summary>
internal static class RemoteViewer
{
    /// <summary>The name after <c>--remote</c>, or null.</summary>
    internal static string? RemoteArg(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count - 1; i++)
            if (string.Equals(args[i], "--remote", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(args[i + 1]))
                return args[i + 1].Trim();
        return null;
    }

    public static int Run(string remoteName, string? id, bool interact, Serilog.ILogger log)
    {
        if (id is null)
        {
            log.Warning("--remote {Name:l} without --session <id>; nothing to show", remoteName);
            return 2;
        }
        if (GatewayRemotes.Find(new JsonStore().LoadSettings().Gateway, remoteName) is not { } remote)
        {
            log.Warning("No teammate's gateway is called {Name:l}", remoteName);
            MessageBox.Show($"No teammate's gateway is called {remoteName}.\n\norchestrator gateway remote list shows them.", "Team Wall",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return 0;
        }
        // The stream sends the gateway the fresh token the member is given since, before its own expires (M.ORCH-T1041).
        using var client = new GatewayClient(remote, () => GatewayRemotes.Find(new JsonStore().LoadSettings().Gateway, remoteName));
        var listing = Task.Run(() => client.ListAsync(CancellationToken.None)).GetAwaiter().GetResult();
        var info = listing.Sessions.FirstOrDefault(s => s.Id == id);
        if (!listing.Online || info is null)
        {
            var why = listing.Online ? "it no longer lists that CLI" : listing.Problem;
            log.Information("Gateway {Name:l} can't show {Id:l}: {Why:l}", remote.Name, id, why ?? "");
            MessageBox.Show($"{remote.Who}'s gateway can't show that CLI: {why}.", "Team Wall", MessageBoxButton.OK, MessageBoxImage.Information);
            return 0;
        }
        var interactive = interact && string.Equals(info.Access, "interact", StringComparison.OrdinalIgnoreCase);
        if (interact && !interactive) log.Information("{Name:l}/{Id:l}: the grant allows watching only", remote.Name, id);
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        app.DispatcherUnhandledException += (_, e) =>
        {
            log.Error(e.Exception, "TermViewer hit an unhandled exception on the UI thread; contained");
            e.Handled = true;
        };
        log.Information("Showing {Name:l}/{Id:l} ({Mode:l})", remote.Name, id, interactive ? "interact" : "watch");
        return app.Run(new RemoteViewerWindow(client, info, interactive));
    }
}

/// <summary>One teammate's CLI, larger, through their gateway (M.ORCH-T936). Watch only, or interactive when the grant allows.
/// Closing it only drops the stream: their CLI keeps running.</summary>
internal sealed class RemoteViewerWindow : Window
{
    private static readonly Serilog.ILogger log = OrchestratorLog.For("TermViewer");

    private readonly GatewayClient client;
    private readonly bool interactive;
    private readonly TextBlock status = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 10, 0), Foreground = Brushes.Gainsboro };
    private readonly Border host = new() { Background = Brushes.Black };
    private readonly DispatcherTimer look = new() { Interval = TimeSpan.FromSeconds(10) };
    private RemoteSessionInfo info;
    private volatile bool listed = true;
    private RelayTerminal? relay;
    private int? exitCode;

    public RemoteViewerWindow(GatewayClient client, RemoteSessionInfo info, bool interactive)
    {
        this.client = client;
        this.info = info;
        this.interactive = interactive;
        Width = 1100;
        Height = 700;
        Background = Brushes.Black;
        try { Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/logo.ico")); }
        catch (Exception ex) { log.Warning(ex, "The window icon couldn't be loaded"); }

        var menu = new Menu { VerticalAlignment = VerticalAlignment.Center };
        var item = new MenuItem { Header = "_Session" };
        item.Items.Add(Item("_Copy", "Ctrl+C", Copy));
        if (interactive) item.Items.Add(Item("_Paste", "Ctrl+V", Paste));
        item.Items.Add(new Separator());
        item.Items.Add(Item("_Reattach", null, Attach));
        item.Items.Add(Item("Close (their CLI keeps running)", null, Close));
        menu.Items.Add(item);

        var tag = new Border
        {
            Background = interactive ? Brushes.DarkGoldenrod : Brushes.DimGray, CornerRadius = new CornerRadius(3), Margin = new Thickness(6, 3, 6, 3),
            Padding = new Thickness(6, 0, 6, 0),
            Child = new TextBlock { Text = interactive ? "INTERACTIVE" : "WATCH ONLY", Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center },
        };
        var who = new Border
        {
            Background = Brushes.SteelBlue, CornerRadius = new CornerRadius(3), Margin = new Thickness(6, 3, 0, 3), Padding = new Thickness(6, 0, 6, 0),
            Child = new TextBlock { Text = client.Remote.Who, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center },
        };
        var bar = new DockPanel { LastChildFill = true, Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x1F)) };
        DockPanel.SetDock(tag, Dock.Right);
        DockPanel.SetDock(who, Dock.Right);
        DockPanel.SetDock(menu, Dock.Left);
        bar.Children.Add(tag);
        bar.Children.Add(who);
        bar.Children.Add(menu);
        bar.Children.Add(status);

        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);
        root.Children.Add(host);
        Content = root;

        UpdateTitle();
        status.Text = $"Through {client.Remote.Name}'s gateway";
        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += (_, _) => Attach();
        Activated += (_, _) => relay?.Terminal.Focus();
        Closing += (_, _) => Detach();
        Closed += (_, _) => look.Stop();
        look.Tick += async (_, _) => await LookAsync();
        look.Start();
    }

    private static MenuItem Item(string header, string? gesture, Action act)
    {
        var item = new MenuItem { Header = header, InputGestureText = gesture ?? "" };
        item.Click += (_, _) =>
        {
            try { act(); }
            catch (Exception ex) { log.Error(ex, "{Header:l} failed; contained", header); }
        };
        return item;
    }

    private void UpdateTitle()
    {
        var text = string.IsNullOrWhiteSpace(info.Title) ? info.Repo ?? info.Id : info.Title.Trim();
        Title = $"{client.Remote.Who}: {text}" + (exitCode is { } c ? $" (exited {c})" : "");
    }

    private void Attach()
    {
        Detach();
        var r = new RelayTerminal(TerminalThemes.Campbell);
        r.Exited += code =>
        {
            exitCode = code;
            UpdateTitle();
            status.Text = $"Their CLI exited with {code}.";
        };
        r.Detached += why => status.Text = $"Detached: {why}. Session > Reattach tries again.";
        r.ReconnectingChanged += reconnecting => status.Text = reconnecting ? "Reconnecting…" : $"Through {client.Remote.Name}'s gateway";
        host.Child = r;
        relay = r;
        var id = info.Id;
        r.AttachLink($"{client.Remote.Name}/{id}", ct => client.ConnectAsync(id, watch: !interactive, ct), info.Protocol, () => listed, interactive,
            $"popout-{Environment.ProcessId}");
        r.Terminal.Focus();
    }

    private void Detach()
    {
        if (relay is not { } r) return;
        relay = null;
        host.Child = null;
        try { r.Dispose(); }
        catch (Exception ex) { log.Error(ex, "Detaching from {Id:l} failed; contained", info.Id); }
    }

    /// <summary>Every ten seconds: the CLI's latest title, and whether the gateway still lists it (a dropped stream attaches
    /// again only while it does).</summary>
    private async Task LookAsync()
    {
        try
        {
            var listing = await client.ListAsync(CancellationToken.None);
            if (!listing.Online) return;
            var now = listing.Sessions.FirstOrDefault(s => s.Id == info.Id);
            listed = now is not null;
            if (now is not null && now.Title != info.Title)
            {
                info = now;
                UpdateTitle();
            }
        }
        catch (Exception ex) { log.Error(ex, "Looking at the gateway failed; contained"); }
    }

    private void Copy()
    {
        if (relay?.Terminal.GetSelectedText() is { Length: > 0 } text)
            try { Clipboard.SetDataObject(text, copy: true); }
            catch (System.Runtime.InteropServices.ExternalException ex) { log.Warning(ex, "The clipboard was busy"); }
    }

    private void Paste()
    {
        if (!interactive || relay is not { Connection: { } connection }) return;
        string text;
        try { text = Clipboard.ContainsText() ? Clipboard.GetText() : ""; }
        catch (System.Runtime.InteropServices.ExternalException) { return; }
        if (RelayInput.PastePayload(text, (connection.Modes & TermModes.BracketedPaste) != 0) is { Length: > 0 } payload) connection.WriteInput(payload);
        relay.Terminal.Focus();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        try
        {
            if (e.Handled || relay is not { Connection: { } connection } r || Keyboard.FocusedElement is not HwndHost) return;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            var mods = Keyboard.Modifiers;
            if (mods == ModifierKeys.Control && key is Key.C or Key.X)
            {
                string? selected;
                try { selected = r.Terminal.GetSelectedText(); }
                catch (Exception) { return; }
                if (string.IsNullOrEmpty(selected)) return;
                try { Clipboard.SetDataObject(selected, copy: true); }
                catch (System.Runtime.InteropServices.ExternalException) { }
                e.Handled = true;
                return;
            }
            if (!interactive) return;
            // As in a dashboard tab: WPF would take these for focus and scrolling; they go to the CLI as win32-input-mode events.
            if (RelayInput.IsForwarded(key, mods))
            {
                connection.WriteInput(RelayInput.NavigationKey(key, (mods & ModifierKeys.Shift) != 0));
                e.Handled = true;
                return;
            }
            if (RelayInput.IsPasteChord(key, mods))
            {
                Paste();
                e.Handled = true;
            }
        }
        catch (Exception ex) { log.Error(ex, "A key in the viewer failed; contained"); }
    }
}
