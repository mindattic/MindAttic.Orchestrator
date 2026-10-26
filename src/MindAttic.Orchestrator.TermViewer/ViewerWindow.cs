using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Status;
using MindAttic.Orchestrator.Term;
using MindAttic.Orchestrator.Term.Wpf;

namespace MindAttic.Orchestrator.TermViewer;

/// <summary>
/// One hosted CLI in its own window while the dashboard is down (M.ORCH-T960): the dashboard's <see cref="RelayTerminal"/> on the
/// session's pipe, titled with the CLI's title and its login's badge. Keys, paste (Ctrl+V, Shift+Insert), copy (Ctrl+C or Ctrl+X
/// with a selection) and resize work as in a dashboard tab. Closing the window only detaches: the CLI keeps running in its host.
/// Session > End CLI kills it.
/// <para>Folding back (docs/app-bus.md, "Fallback viewers"): the window closes itself, quietly, once the dashboard is up again. The
/// signal is the dashboard's <c>app-present</c> on the bus, confirmed by its single-instance mutex and show event
/// (<see cref="DashboardPresence"/>); the same check runs every two seconds for when the bus is down, and when this window's
/// connection is replaced (a protocol 1 host serves one viewer, so the dashboard reattaching takes it from here). A protocol 2
/// host (M.ORCH-T930) keeps this window attached beside the dashboard's tab, so there the fold rests on the presence signal
/// and the two-second check, never on being replaced. This window doesn't reconnect a dropped link (no liveness probe given
/// to RelayTerminal.Attach), so it can't take a protocol 1 session back from the dashboard; only a --stay pop-out does
/// (M.ORCH-T976).</para>
/// </summary>
internal sealed class ViewerWindow : Window
{
    private static readonly Serilog.ILogger log = OrchestratorLog.For("TermViewer");

    private readonly TermSessionRecord session;
    private readonly PaneRequest pane;
    private readonly bool stay;
    private readonly bool watchOnly;
    private readonly TextBlock status = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 10, 0), Foreground = Brushes.Gainsboro };
    private readonly Border host = new() { Background = Brushes.Black };
    private readonly DispatcherTimer watch = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly CancellationTokenSource stop = new();
    private RelayTerminal? relay;
    private string liveTitle;
    private int? exitCode;
    private int upLooks;
    private bool closing;

    public ViewerWindow(TermSessionRecord session, PaneRequest pane, bool stay = false, bool watchOnly = false)
    {
        this.session = session;
        this.pane = pane;
        this.stay = stay;
        this.watchOnly = watchOnly;
        liveTitle = session.Title is { Length: > 0 } t ? t : pane.Title;
        Width = 1100;
        Height = 700;
        Background = Brushes.Black;
        try { Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/logo.ico")); }
        catch (Exception ex) { log.Warning(ex, "The window icon couldn't be loaded"); }

        var menu = new Menu { VerticalAlignment = VerticalAlignment.Center };
        var item = new MenuItem { Header = "_Session" };
        item.Items.Add(Item("_Copy", "Ctrl+C", Copy));
        item.Items.Add(Item("_Paste", "Ctrl+V", Paste));
        item.Items.Add(new Separator());
        if (!watchOnly) item.Items.Add(Item("_Take size", null, TakeSize));
        item.Items.Add(Item("_Reattach", null, Reattach));
        item.Items.Add(Item("Close (the CLI keeps running)", null, Close));
        item.Items.Add(new Separator());
        item.Items.Add(Item("_End CLI...", null, EndCli));
        menu.Items.Add(item);

        var bar = new DockPanel { LastChildFill = true, Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x1F)) };
        if (Badge() is { } badge)
        {
            DockPanel.SetDock(badge, Dock.Right);
            bar.Children.Add(badge);
        }
        DockPanel.SetDock(menu, Dock.Left);
        bar.Children.Add(menu);
        if (watchOnly)
        {
            var tag = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x5A, 0x4A, 0x10)), CornerRadius = new CornerRadius(3), Margin = new Thickness(4, 3, 4, 3),
                Padding = new Thickness(6, 0, 6, 0), ToolTip = "Read-only: nothing typed here reaches the CLI, and this window never changes its size",
                Child = new TextBlock { Text = "WATCH ONLY", Foreground = Brushes.Gold, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center },
            };
            DockPanel.SetDock(tag, Dock.Left);
            bar.Children.Add(tag);
        }
        bar.Children.Add(status);

        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);
        root.Children.Add(host);
        Content = root;

        UpdateTitle();
        SetStatus(stay
            ? (watchOnly ? "Watching only: this window shows the CLI beside its tab and never types into it" : "Popped out: this window and the CLI's tab both show it; Session > Take size makes this window's size the CLI's")
            : "Dashboard down: this CLI runs in its own window, and goes back to its tab when the dashboard returns");
        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += (_, _) => Attach();
        Activated += (_, _) => relay?.Terminal.Focus();
        Closing += (_, _) => Detach();
        Closed += (_, _) => { stop.Cancel(); watch.Stop(); };
        watch.Tick += (_, _) => Look();
        watch.Start();
        ListenForDashboard();
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

    /// <summary>The login's badge ("RD") in its colour; null when the session's login has none.</summary>
    private Border? Badge()
    {
        if (string.IsNullOrWhiteSpace(pane.Initials)) return null;
        Brush fill = Brushes.DimGray;
        try { if (pane.TabColor is { Length: > 0 } hex && new BrushConverter().ConvertFromInvariantString(hex) is Brush b) fill = b; }
        catch (Exception ex) when (ex is FormatException or NotSupportedException) { }
        return new Border
        {
            Background = fill, CornerRadius = new CornerRadius(3), Margin = new Thickness(6, 3, 6, 3), Padding = new Thickness(6, 0, 6, 0),
            Child = new TextBlock { Text = pane.Initials, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center },
            ToolTip = pane.Email ?? session.Login,
        };
    }

    /// <summary>The window's title: the login's badge, then the CLI's own title (its record's, which follows its OSC title).</summary>
    internal static string TitleOf(string? initials, string? title, string id, int? exitCode)
    {
        var text = string.IsNullOrWhiteSpace(title) ? id : title.Trim();
        if (!string.IsNullOrWhiteSpace(initials) && !text.StartsWith(initials + ":", StringComparison.OrdinalIgnoreCase)) text = initials + ": " + text;
        return exitCode is { } code ? $"{text} (exited {code})" : text;
    }

    private void UpdateTitle() => Title = TitleOf(pane.Initials, liveTitle, session.Id, exitCode);

    private void SetStatus(string text)
    {
        notSent?.Stop();
        status.Text = text;
    }

    private DispatcherTimer? notSent;
    private string? statusBeforeNotSent;

    /// <summary>Keys or a paste were dropped while the link is down (M.ORCH-T979): said for a few seconds, then the status before
    /// it comes back. The input itself is never queued or replayed.</summary>
    private void ShowNotSent()
    {
        try
        {
            if (closing || exitCode is not null) return;
            if (notSent is null)
            {
                notSent = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                notSent.Tick += (_, _) => { notSent.Stop(); if (statusBeforeNotSent is { } before) status.Text = before; statusBeforeNotSent = null; };
            }
            if (!notSent.IsEnabled) statusBeforeNotSent = status.Text;
            notSent.Stop();
            status.Text = "Not sent: reconnecting to the CLI's host; type it again once it's back";
            notSent.Start();
        }
        catch (Exception ex) { log.Error(ex, "Showing the not-sent notice failed; contained"); }
    }

    private void Attach()
    {
        if (closing) return;
        Detach();
        var r = new RelayTerminal(TerminalThemes.Campbell);
        r.Exited += code =>
        {
            exitCode = code;
            UpdateTitle();
            SetStatus($"The CLI exited with {code}. Close this window when you're done with it.");
        };
        r.Detached += OnDetached;
        r.InputDropped += ShowNotSent;
        host.Child = r;
        relay = r;
        // Only a --stay pop-out reconnects a dropped link (M.ORCH-T976). A fallback window gets no liveness probe: a replaced or
        // dropped link ends it, and OnDetached folds it while the dashboard is present, so it never takes a protocol 1 session
        // back from the dashboard's tab and ping-pongs with it.
        Func<bool>? alive = stay ? () => TermSessions.Get(session.Id) is { } live && live.HostPid == session.HostPid : null;
        r.Attach(session.Pipe, session.Protocol, alive, interactive: !watchOnly);
        r.Terminal.Focus();
    }

    private void Detach()
    {
        if (relay is not { } r) return;
        relay = null;
        host.Child = null;
        // Drops the pipe only: the host and its CLI keep running.
        try { r.Dispose(); }
        catch (Exception ex) { log.Error(ex, "Detaching from {Id:l} failed; contained", session.Id); }
    }

    /// <summary>The pipe went away: the dashboard (or another viewer) took the session, the CLI's host ended, or it never answered.</summary>
    private void OnDetached(string why)
    {
        if (closing) return;
        if (!stay && DashboardPresence.IsUp())
        {
            log.Information("{Id:l} was taken by the dashboard; folding back", session.Id);
            Fold();
            return;
        }
        if (exitCode is not null) return;
        if (TermSessions.Get(session.Id) is null)
        {
            SetStatus($"The CLI's host ended ({why}).");
            return;
        }
        SetStatus($"Detached: {why}. Another viewer may have it; Session > Reattach takes it back.");
    }

    /// <summary>Makes this window's size the session's (sends TakeSize); a no-op when it owns it, is watch only, or the host is
    /// protocol 1.</summary>
    private void TakeSize()
    {
        if (watchOnly || relay is not { Connection: { } connection } r) return;
        connection.TakeSize();
        r.Terminal.Focus();
    }

    private void Reattach()
    {
        if (TermSessions.Get(session.Id) is null)
        {
            SetStatus("The CLI's host has ended; there's nothing to reattach to.");
            return;
        }
        SetStatus("Reattached.");
        Attach();
    }

    /// <summary>Closes the window and leaves the CLI to the dashboard's tab.</summary>
    private void Fold()
    {
        if (closing || stay) return;
        closing = true;
        Close();
    }

    /// <summary>Every two seconds: the dashboard back (twice in a row, so one starting for a moment and failing doesn't take the
    /// window), and the CLI's live title.</summary>
    private void Look()
    {
        try
        {
            if (!stay && DashboardPresence.IsUp())
            {
                if (++upLooks >= 2) { log.Information("The dashboard is back; {Id:l} folds into its tab", session.Id); Fold(); return; }
            }
            else upLooks = 0;
            if (TermSessions.Get(session.Id) is { } now && now.Title is { Length: > 0 } title && title != liveTitle)
            {
                liveTitle = title;
                UpdateTitle();
            }
        }
        catch (Exception ex) { log.Error(ex, "Looking at the dashboard and session failed; contained"); }
    }

    /// <summary>The dashboard says it's present on the bus (app-present from orchestrator-desktop) as it connects: checked at once.</summary>
    private void ListenForDashboard()
    {
        var token = stop.Token;
        _ = Task.Run(() => StatusStreamClient.RunAsync((StatusEnvelope envelope) =>
        {
            if (envelope.Kind != AppBus.Presence || envelope.V > AppBus.Version) return;
            if (envelope.PayloadAs<AppPresence>() is not { App: AppBus.OrchestratorDesktop }) return;
            Dispatcher.InvokeAsync(() =>
            {
                if (closing || !DashboardPresence.IsUp()) return;
                log.Information("The dashboard is present on the bus; {Id:l} folds into its tab", session.Id);
                Fold();
            });
        }, _ => { }, token), token);
    }

    private void EndCli()
    {
        var answer = MessageBox.Show(this, $"End {Title}?\n\nThe CLI and everything it started are stopped; anything it was doing is lost.",
            "End CLI", MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel);
        if (answer != MessageBoxResult.OK) return;
        log.Information("Ending {Id:l} at the person's request", session.Id);
        Detach();
        if (!TermHostKill.Kill(TermSessions.Get(session.Id) ?? session))
        {
            // Its host still runs (M.ORCH-T1034): said, and the window stays on it.
            SetStatus("The CLI couldn't be ended (its host may run elevated); it is still running.");
            Attach();
            return;
        }
        closing = true;
        Close();
    }

    private void Copy()
    {
        if (relay?.Terminal.GetSelectedText() is { Length: > 0 } text)
            try { Clipboard.SetDataObject(text, copy: true); }
            catch (System.Runtime.InteropServices.ExternalException ex) { log.Warning(ex, "The clipboard was busy"); }
    }

    private void Paste()
    {
        if (watchOnly || relay is not { Connection: { } connection }) return;
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
            // Ctrl+C or Ctrl+X with a selection copies it (reading it clears it); with none, the key is the CLI's (^C interrupts).
            // Handled before the watch-only return (M.ORCH-T978): copying is reading, which a watch-only window does too.
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
            if (watchOnly) return;
            // Phase 0 lesson 3: WPF would take these for focus and scrolling; they go to the CLI as win32-input-mode events.
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
