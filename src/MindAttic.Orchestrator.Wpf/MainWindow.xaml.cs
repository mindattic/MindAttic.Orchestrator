using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using MindAttic.Orchestrator.Wpf.ViewModels;

namespace MindAttic.Orchestrator.Wpf;

public partial class MainWindow : Window
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    /// <summary>Minimized while the daemon builds the snapshots: nobody is looking, so just enough to stay roughly current.</summary>
    private static readonly TimeSpan HiddenInterval = TimeSpan.FromSeconds(60);

    /// <summary>Hidden but holding the lease (no daemon running): the daemon's own pace, since rl-status.json and allocate depend on it.</summary>
    private static readonly TimeSpan HiddenHolderInterval = TimeSpan.FromSeconds(15);

    /// <summary>The global login's tab, which owns the refresh and every login's tab (M.ORCH-T613).</summary>
    private readonly MainViewModel model = new();
    private readonly DispatcherTimer timer;

    /// <summary>The login's tab on screen: the window shows its view model.</summary>
    private MainViewModel Shown => DataContext as MainViewModel ?? model;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = model;
        // One dashboard tab per Claude login (M.ORCH-T613): the whole window shows the chosen login's tab, while the tab strip
        // keeps the global login's, which lists them all.
        LoginTabStrip.DataContext = model;
        // The org switcher above it, and what an org with no Claude login says, are the global login's tab's too (M.ORCH-T655).
        OrgStrip.DataContext = model;
        OrgEmptyPanel.DataContext = model;
        // The org-wide login actions (Add login, Sign in all, Report), on Settings' Logins tab since M.ORCH-T804, are the global
        // login's tab's too (M.ORCH-T763), not the shown tab's Settings the rest of the dialog binds to.
        OrgToolbar.DataContext = model;
        // Settings fills 80% of the window both ways (M.ORCH-T804), following it as it resizes or zooms.
        Root.SizeChanged += (_, _) => SizeSettings();
        // A person's current work (including, on the global login's tab, what fits on no person's page, M.ORCH-T825) is one
        // virtualized list (M.ORCH-T754): headings and notes, then the lists' items a row (or a line of cards) each, so a
        // switch of tab or org builds only what's on screen.
        VirtualizedList.ApplyBody(AgentsBody);
        model.SelectedTabChanged += (_, _) => DataContext = model.SelectedTab;
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is MainViewModel was) was.PropertyChanged -= OnShownChanged;
            if (e.NewValue is MainViewModel now) now.PropertyChanged += OnShownChanged;
        };
        model.PropertyChanged += OnShownChanged;
        // Scales the window's content and binds Ctrl+Plus / Ctrl+Minus / Ctrl+0 on the window, wherever focus is.
        model.Zoom.Attach(Root, this);
        // The upper-right sun/moon switch (M.ORCH-T678) changes the theme live; each switch is saved, so it holds at the next start.
        ThemeChoice.SaveSwitches(this, model.Store);
        // The emergency billing button's glow pulse (M.ORCH-T770), set up here rather than a Storyboard in its Style
        // (M.ORCH-T775): Storyboard.TargetName only resolves within a ControlTemplate or DataTemplate's own NameScope, and
        // this button wears the library's own MaToolButton style with no named part of its own to target. A DropShadowEffect
        // assigned straight to the button and animated with BeginAnimation needs no NameScope at all.
        var emergencyGlow = new DropShadowEffect { Color = GlowColor, ShadowDepth = 0, BlurRadius = 14, Opacity = 0 };
        EmergencyBillingButton.Effect = emergencyGlow;
        void UpdateEmergencyGlow() => Pulse(emergencyGlow, model.IsOnApiKey, exitOpacity: 0, exitSeconds: 0.3);
        model.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MainViewModel.IsOnApiKey)) UpdateEmergencyGlow(); };
        UpdateEmergencyGlow();

        CommandBindings.Add(new CommandBinding(WindowCommands.Escape, (_, _) => OnEscape(),
            // Esc belongs to the CLI while a terminal has focus (M.ORCH-T852): not executable, so the key passes through.
            (_, e) => e.CanExecute = !TerminalKeyboard.HoldsFocus(TerminalPanesHost)));

        // Each terminal lives in the Terminals panel's host grid from its opening to its closing (M.ORCH-T810): a template would
        // build a new terminal (and a new CLI) every time its tab was shown. Native windows, they sit in a fixed row under the
        // person's terminal tabs, outside the scrolling body (M.ORCH-T861), so they can't scroll over the tabs (TerminalClip only
        // clips them should they ever sit in a scroll view again), and are hidden while a dialog or the loader covers the window,
        // which they'd otherwise draw over.
        model.AllTerminals.Panes.CollectionChanged += (_, e) =>
        {
            foreach (var gone in e.OldItems?.OfType<TerminalPaneViewModel>() ?? []) TerminalPanesHost.Children.Remove(gone.Control);
            foreach (var added in e.NewItems?.OfType<TerminalPaneViewModel>() ?? [])
            {
                TerminalPanesHost.Children.Add(added.Control);
                TerminalPaste.Track(added);
            }
        };
        TerminalClip.Attach(TerminalPanesHost);
        TerminalKeyboard.Attach(this, TerminalPanesHost);
        PreviewKeyDown += TerminalPaste.OnPreviewKeyDown;
        // Ctrl+C / Ctrl+X with a selection copy / cut it (M.ORCH-T867, M.ORCH-T866); with none they still reach the CLI (^C, ^X).
        PreviewKeyDown += TerminalCopy.OnPreviewKeyDown;
        // Only the kept chords act while a terminal holds focus (M.ORCH-T846): the zoom keys and Esc go to the CLI.
        TerminalKeyboard.GateBindings(this, TerminalPanesHost);
        // Focus stays on the shown CLI's terminal (M.ORCH-T846): back to it after the window is activated, a person's tab is
        // chosen, a dialog closes, a toast appears, a click on the page (not into a text box, a menu or a dialog) and a refresh
        // that took the focused element away.
        Activated += (_, _) => PinTerminalFocus();
        model.SelectedTabChanged += (_, _) => PinTerminalFocus();
        PreviewMouseUp += (_, _) => PinTerminalFocus();
        model.PropertyChanged += (_, _) => UpdateTerminalsCover();
        DataContextChanged += (_, _) => UpdateTerminalsCover();

        // A toast on any login's tab flashes the window.
        model.ToastRaised += (_, _) =>
        {
            OnToastAdded();
            PinTerminalFocus();
        };

        timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = PollInterval };
        timer.Tick += async (_, _) =>
        {
            await model.RefreshAsync();
            Retime();
            PinTerminalFocus(onlyIfLost: true);
        };
        Loaded += async (_, _) =>
        {
            // The last run's tabs, when it didn't close cleanly (M.ORCH-T862): offered before the first refresh, so a restored
            // Launcher menu pane counts as its login's and the refresh doesn't open a second; reselected once the tabs are up.
            var restored = restoreOnLoad && SessionRestore.Offer(this, model);
            await model.RefreshAsync();
            if (restored) model.ReselectRestored();
            polling = true;
            Retime();
        };
        Closed += (_, _) =>
        {
            polling = false;
            timer.Stop();
        };
        // Minimized, nobody is looking: no 5-second refresh (M.ORCH-T115). Shown again, it catches up at once.
        IsVisibleChanged += async (_, _) => await OnShownOrHidden();
        StateChanged += async (_, _) => await OnShownOrHidden();
    }

    /// <summary><c>--start &lt;repo&gt;</c> (M.ORCH-T767): a CLI in that workspace repo, as Start opens one; anything else is refused.</summary>
    public Task StartRepoAsync(string repo) => model.StartRepoAsync(repo);

    /// <summary><c>--show &lt;email|session|ticket&gt;</c> (M.ORCH-T763): the person it names, or whose CLI runs that session or ticket.</summary>
    public Task<bool> ShowAsync(string target) => model.ShowAsync(target);

    private CancellationTokenSource? terminalsPipe;
    private bool restoreOnLoad;

    /// <summary>
    /// Makes this window the one that holds the Terminals panels' terminals (M.ORCH-T810): the hosting entry point. A CLI sent
    /// from another process (MindAttic.Launcher running inside the Orchestrator, M.LNCH-T77) arrives on the terminals pipe
    /// (<see cref="Sessions.PaneBroker"/>) and opens as a new tab on its person's page. Called by the app for its one dashboard
    /// only, never by a test's window.
    /// </summary>
    internal void HostTerminals()
    {
        // Its tabs are written to the session-restore file as they change, and offered back after a crash (M.ORCH-T862).
        restoreOnLoad = true;
        SessionRestore.Track(model);
        // Kept as they stand when the window starts closing: the tabs' teardown after that isn't what was open.
        Closing += (_, _) => SessionRestore.Freeze();
        terminalsPipe = new CancellationTokenSource();
        _ = Sessions.PaneBroker.ServeAsync(request => Dispatcher.InvokeAsync(() => model.OpenTerminal(request)).Task, terminalsPipe.Token)
            .ContinueWith(t => OrchestratorLog.For("Terminals").Error(t.Exception, "The terminals pipe server ended"), TaskContinuationOptions.OnlyOnFaulted);
        Closed += (_, _) => terminalsPipe.Cancel();
    }

    private CancellationTokenSource? statusStream;

    /// <summary>
    /// Subscribes to the daemon's status stream (M.ORCH-T854): each reading of Claude's meter is applied to the tabs the moment
    /// it lands, and each new snapshot's figures the daemon pushes are laid over the tabs at once (M.ORCH-T872; the published
    /// file read only when the rest of the snapshot moved), and the roster, orgs, settings, pause and reminders as anyone changes
    /// them (M.ORCH-T891), so there's no poll while the stream is up (<see cref="RefreshInterval"/>). It connects again on its own
    /// whenever the daemon restarts. Called by the app for its one
    /// dashboard only, never by a test's window.
    /// </summary>
    internal void ListenForStatus()
    {
        statusStream = new CancellationTokenSource();
        var token = statusStream.Token;
        // Each kind this window reads (M.ORCH-T872); any other on the bus (another app's) is skipped.
        var bus = new Status.StatusBus()
            .On<Status.SnapshotFigures>(Status.SnapshotFigures.Kind, OnFigures, Status.SnapshotFigures.Version);
        foreach (var kind in new[] { Status.StatusMessage.State, Status.StatusMessage.Usage, Status.StatusMessage.Snapshot, Status.StatusMessage.Heartbeat })
            bus.On<Status.StatusMessage>(kind, (e, m) => OnStatus(Status.StatusMessage.From(e, m)), Status.StatusMessage.Version);
        // The app bus (M.ORCH-T879): ticket changes refresh the Queue, Tickets Desktop's presence says it can open a ticket
        // without a new process, and it can ask this window to show who works a ticket.
        bus.On<Status.TicketBusEvent>(Status.AppBus.Ticket, (_, change) => OnTicketChange(change), Status.AppBus.Version)
            .On<Status.AppPresence>(Status.AppBus.Presence, (_, presence) => model.Presences.Note(presence), Status.AppBus.Version)
            .On<Status.ShowWorkerRequest>(Status.AppBus.ShowWorker, (_, request) => OnShowWorker(request), Status.AppBus.Version);
        // The roster, the orgs, the settings, the pause and the reminders (M.ORCH-T891), pushed by whoever changed them: with
        // these, nothing the dashboard shows needs a timed read while the stream is up.
        bus.On<Status.MembersPayload>(Status.StateBus.Members, (_, p) => OnPushedState(p), Status.StateBus.Version)
            .On<Status.OrgsPayload>(Status.StateBus.Orgs, (_, p) => OnPushedState(p), Status.StateBus.Version)
            .On<Status.SettingsPayload>(Status.StateBus.Settings, (_, p) => OnPushedState(p), Status.StateBus.Version)
            .On<Status.PausePayload>(Status.StateBus.Pause, (_, p) => OnPushedState(p), Status.StateBus.Version)
            .On<Status.RemindersPayload>(Status.StateBus.Reminders, (_, p) => OnPushedState(p), Status.StateBus.Version);
        // And what this dashboard changes of them is pushed the same way, to every other subscriber, off the UI thread.
        Status.StateBus.Enable(background: true);
        _ = Task.Run(() => Status.StatusStreamClient.RunAsync(
            (Status.StatusEnvelope envelope) => Dispatcher.InvokeAsync(() => bus.Dispatch(envelope)),
            live =>
            {
                // Said on every connection, as the daemon forgets it on a restart: this window acts on "show who is working X".
                if (live) Status.AppBusClient.PublishPresence(Status.AppBus.OrchestratorDesktop, [Status.AppBus.ShowWorker]);
                Dispatcher.InvokeAsync(() =>
                {
                    model.SetStreamLive(live);
                    // Gone with the stream: each listening app says so again on the next connection.
                    if (!live) model.Presences.Clear();
                    Retime();
                });
            },
            token), token);
        // Holding the status lease itself (no daemon running), this process's own meter probe reads the meters: each reading is
        // applied as it lands too, as the daemon's would be.
        void OnLocalReading(Budget.ClaudeMeterProbe probe, Budget.ReportedUsage reading)
        {
            var message = new Status.StatusMessage
            {
                Kind = Status.StatusMessage.Usage, AtUtc = DateTime.UtcNow, Pid = Environment.ProcessId,
                Readings = Status.MeterReading.Of(probe.Label, reading, signedIn: null, "probe") is { } r ? [r] : [],
            };
            Dispatcher.InvokeAsync(() => OnStatus(message));
        }
        Budget.ClaudeMeterProbe.ReadingLanded += OnLocalReading;
        Closed += (_, _) =>
        {
            statusStream.Cancel();
            Budget.ClaudeMeterProbe.ReadingLanded -= OnLocalReading;
        };
    }

    /// <summary>One message off the stream, on the UI thread. Hidden, a new snapshot waits for the window to be shown (it catches
    /// up then); the readings are applied either way, being cheap.</summary>
    private async void OnStatus(Status.StatusMessage message)
    {
        try
        {
            var refresh = model.ApplyStatus(message);
            Retime();
            if (refresh && IsLoaded && !Hidden)
            {
                await model.RefreshAsync();
                PinTerminalFocus(onlyIfLost: true);
            }
        }
        // async void on the dispatcher: an escaped exception would close the dashboard.
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            OrchestratorLog.For("Dashboard").Error(ex, "Applying a status stream message failed");
        }
    }

    /// <summary>A snapshot's figures pushed off the stream (M.ORCH-T872), on the UI thread: laid over the tabs at once, or, when
    /// the rest of the snapshot moved, the published file read now.</summary>
    private async void OnFigures(Status.StatusEnvelope envelope, Status.SnapshotFigures figures)
    {
        try
        {
            // The figures say their daemon builds the snapshots, as every other message does.
            model.ApplyStatus(new Status.StatusMessage { Kind = envelope.Kind, AtUtc = envelope.AtUtc, Pid = envelope.Pid, SnapshotTakenUtc = figures.TakenUtc });
            var read = await model.ApplyFiguresAsync(figures, Hidden || !IsLoaded);
            Retime();
            if (read && IsLoaded && !Hidden) await model.RefreshAsync();
            PinTerminalFocus(onlyIfLost: true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            OrchestratorLog.For("Dashboard").Error(ex, "Applying a snapshot's pushed figures failed");
        }
    }

    /// <summary>The roster, the orgs, the settings, the pause or the reminders pushed off the stream (M.ORCH-T891), on the UI
    /// thread: laid over the tabs at once, or, when a read was under way that may have missed the change, the store read again.</summary>
    private async void OnPushedState(Status.StatePayload payload)
    {
        try
        {
            if (!model.ApplyPushedState(payload)) return;
            var read = await model.ReapplyStateAsync(Hidden || !IsLoaded);
            if (read && IsLoaded && !Hidden) await model.RefreshAsync();
            PinTerminalFocus(onlyIfLost: true);
        }
        // async void on the dispatcher: an escaped exception would close the dashboard.
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            OrchestratorLog.For("Dashboard").Error(ex, "Applying pushed {Kind:l} failed", payload.GetType().Name);
        }
    }

    private bool queueReloadQueued;

    /// <summary>
    /// A ticket changed (M.ORCH-T879), on the UI thread: the Queue's tabs are read again (<see cref="ViewModels.MainViewModel.ReloadTicketQueues"/>)
    /// instead of waiting for the next timed read. A burst (a sync, an import) is read once, after it, at background priority;
    /// the open tickets' own move (claimed, closed) follows with the snapshot the daemon builds for it within seconds.
    /// </summary>
    private void OnTicketChange(Status.TicketBusEvent change)
    {
        if (queueReloadQueued) return;
        queueReloadQueued = true;
        Dispatcher.InvokeAsync(() =>
        {
            queueReloadQueued = false;
            try { model.ReloadTicketQueues(); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("TicketBus").Error(ex, "Reloading the Queue for {Ticket:l} failed", change.Id); }
        }, DispatcherPriority.Background);
    }

    /// <summary>"Show who is working X" from Tickets Desktop (M.ORCH-T879), on the UI thread: the window comes forward (Tickets
    /// Desktop lets it take the foreground) on the person and CLI tab working the ticket.</summary>
    private void OnShowWorker(Status.ShowWorkerRequest request)
    {
        try
        {
            if (request.To is { Length: > 0 } to && !string.Equals(to, Status.AppBus.OrchestratorDesktop, StringComparison.OrdinalIgnoreCase)) return;
            if (!Tickets.TicketsDesktop.IsTicketId(request.Id)) return;
            model.ShowWorker(request);
            DashboardWindow.Restore(this);
            PinTerminalFocus();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            OrchestratorLog.For("TicketBus").Error(ex, "Showing who works {Ticket:l} failed", request.Id);
        }
    }

    /// <summary>The terminals are native windows WPF can't draw over: hidden (never collapsed, so each keeps its size) while
    /// Settings, Rename or Org limit is open, the loader covers the window or an org with no login covers the page.</summary>
    private void UpdateTerminalsCover()
    {
        var shown = Shown;
        var covered = shown.IsSettingsOpen || shown.IsRenameOpen || shown.IsLimitOpen || model.IsLoading || model.IsOrgEmpty;
        TerminalPanesHost.Visibility = covered ? Visibility.Hidden : Visibility.Visible;
    }

    /// <summary>
    /// Hands the keyboard back to the shown CLI's terminal (M.ORCH-T846), queued past the click, layout or refresh that
    /// prompted it (a terminal shown again only gets its native window back on the layout pass). It stays put while the window
    /// isn't active, a dialog is open, the terminals are covered, or focus is somewhere the user chose to be: a text box (Settings,
    /// Rename), an open menu or dropdown, another window. With <paramref name="onlyIfLost"/> (a refresh) it only takes focus
    /// back that went nowhere, as when a rebuilt list dropped the focused row, so tabbing round the page still works.
    /// </summary>
    private void PinTerminalFocus(bool onlyIfLost = false) =>
        Dispatcher.InvokeAsync(() =>
        {
            var shown = Shown;
            if (!IsActive || shown.IsSettingsOpen || shown.IsRenameOpen || shown.IsLimitOpen
                || TerminalPanesHost.Visibility != Visibility.Visible) return;
            if (model.AllTerminals.SelectedPane?.Control is not { IsVisible: true } terminal) return;
            var focused = Keyboard.FocusedElement as DependencyObject;
            if (ReferenceEquals(focused, terminal) || KeepsFocus(focused, onlyIfLost)) return;
            terminal.Focus();
        }, DispatcherPriority.ContextIdle);

    /// <summary>Whether <paramref name="focused"/> keeps the keyboard rather than handing it to the terminal: a text box
    /// the user clicked into, anything in a popup (a menu, a dropdown) or another window, and with <paramref name="onlyIfLost"/>
    /// any element still on screen.</summary>
    private bool KeepsFocus(DependencyObject? focused, bool onlyIfLost)
    {
        if (focused is null || ReferenceEquals(focused, this)) return false;
        var source = PresentationSource.FromDependencyObject(focused);
        if (source is null) return false; // taken off screen with its list: lost
        if (!ReferenceEquals(source.RootVisual, this)) return true; // a popup (a menu, a dropdown) or another window
        if (focused is TextBoxBase or PasswordBox) return true; // an editable ComboBox's text too
        return onlyIfLost && focused is UIElement { IsVisible: true };
    }

    /// <summary>Reads out the shown tab's status and queue lines.</summary>
    private void OnShownChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender != Shown) return;
        if (e.PropertyName is nameof(MainViewModel.IsSettingsOpen) or nameof(MainViewModel.IsRenameOpen) or nameof(MainViewModel.IsLimitOpen))
        {
            UpdateTerminalsCover();
            PinTerminalFocus(); // a dialog closing hands the keyboard back to the CLI
        }
        if (e.PropertyName == nameof(MainViewModel.StatusMessage)) Announce(Shown.StatusMessage);
        else if (e.PropertyName == nameof(MainViewModel.QueueMessage)) Announce(Shown.QueueMessage);
    }

    /// <summary>The slow glowing pulse's colour (M.ORCH-T770): the emergency billing button and a busy login tab's icon both
    /// use it, built and animated in code rather than a Storyboard (M.ORCH-T775 below).</summary>
    private static readonly Color GlowColor = Color.FromRgb(0x2F, 0xBF, 0x71);

    /// <summary>Starts <paramref name="glow"/>'s slow forever pulse while <paramref name="on"/>, or settles it at
    /// <paramref name="exitOpacity"/> over <paramref name="exitSeconds"/> once it isn't: the code-behind equivalent of the
    /// Storyboard.TargetName animation M.ORCH-T770 used, which needs a NameScope neither the emergency button's library style
    /// nor a plain TextBlock inside a DataTemplate's repeated items can give it cleanly (M.ORCH-T775). A DropShadowEffect's own
    /// dependency property animates with BeginAnimation, no NameScope required.</summary>
    private static void Pulse(DropShadowEffect glow, bool on, double exitOpacity, double exitSeconds) =>
        glow.BeginAnimation(DropShadowEffect.OpacityProperty, on
            ? new DoubleAnimation(0.25, 0.95, TimeSpan.FromSeconds(1.3)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever }
            : new DoubleAnimation(exitOpacity, TimeSpan.FromSeconds(exitSeconds)));

    /// <summary>Every person's tab is the same width (M.ORCH-T803): the library's MaTabItem has no Width of its own (it's plain
    /// FrameworkElement's), so the ItemContainerStyle (M.ORCH-T667) can't set it without "adding looks of its own" to a
    /// library style; set directly on the generated container instead, once its header content loads.</summary>
    private const double PersonTabWidth = 230;

    /// <summary>A person tab's header content (M.ORCH-T803): sets its generated MaTabItem to <see cref="PersonTabWidth"/> so
    /// every name's tab (Amy, Erin, Honey, Jordan, Ryan, ...) is the same width; the 'API key' and 'Miscellaneous' tabs, whose
    /// header is the same <c>DataTemplate</c>, are left to their own content.</summary>
    private void LoginTabItem_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: MainViewModel { IsPersonTab: true } } content) return;
        if (ItemsControl.ContainerFromElement(LoginTabStrip, content) is TabItem tab) tab.Width = PersonTabWidth;
    }

    /// <summary>No wrap, ever (M.ORCH-T822): the < and > buttons page <see cref="LoginTabScroller"/> by one tab's width,
    /// hidden while every login's tab already fits and disabled at whichever end is already reached, rather than wrapping
    /// around. Fires on load, resize and scroll alike, since all three change <see cref="ScrollViewer.ScrollableWidth"/>.</summary>
    private void LoginTabScroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        var can = LoginTabScroller.ScrollableWidth > 0.5;
        LoginTabScrollLeft.Visibility = LoginTabScrollRight.Visibility = can ? Visibility.Visible : Visibility.Collapsed;
        LoginTabScrollLeft.IsEnabled = LoginTabScroller.HorizontalOffset > 0.5;
        LoginTabScrollRight.IsEnabled = LoginTabScroller.HorizontalOffset < LoginTabScroller.ScrollableWidth - 0.5;
    }

    private void LoginTabScrollLeft_Click(object sender, RoutedEventArgs e) =>
        LoginTabScroller.ScrollToHorizontalOffset(LoginTabScroller.HorizontalOffset - PersonTabWidth);

    private void LoginTabScrollRight_Click(object sender, RoutedEventArgs e) =>
        LoginTabScroller.ScrollToHorizontalOffset(LoginTabScroller.HorizontalOffset + PersonTabWidth);

    /// <summary>Pulses <paramref name="icon"/>'s opacity forever while <paramref name="on"/> and the system's client-area animation
    /// is on (reduced motion off), else clears the animation so it rests fully opaque, never left mid-fade (M.ORCH-T890).</summary>
    private static void Flash(TextBlock icon, bool on)
    {
        try
        {
            if (on && SystemParameters.ClientAreaAnimation)
                icon.BeginAnimation(UIElement.OpacityProperty,
                    new DoubleAnimation(1.0, 0.2, TimeSpan.FromSeconds(0.6)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            else
            {
                icon.BeginAnimation(UIElement.OpacityProperty, null);
                icon.Opacity = 1.0;
            }
        }
        catch (Exception) { } // a flash is decoration: never let it take the tab strip down
    }

    /// <summary>A person tab's pause icon flashes once every open CLI of theirs has stopped (M.ORCH-T890). Each instance of the
    /// repeated <c>DataTemplate</c> drives only its own icon; it's visible exactly while its tab <c>IsTabAllStopped</c>, so showing
    /// or hiding it is when the flash starts or stops.</summary>
    private void TabStoppedIcon_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBlock icon) Flash(icon, icon.IsVisible);
    }

    private void TabStoppedIcon_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBlock icon) Flash(icon, icon.IsVisible);
    }

    private void TabStoppedIcon_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBlock icon) Flash(icon, false);
    }

    /// <summary>A CLI tab's right-click menu (M.ORCH-T883; Shift+F10 too): "Move to" each other signed-in login, read when it's
    /// opened, moving that CLI there. Not on the Launcher's own menu tab; disabled until the CLI's host is known.</summary>
    private async void OnTerminalTabContextMenu(object sender, ContextMenuEventArgs e)
    {
        try
        {
            if (e.OriginalSource is not DependencyObject source
                || ItemsControl.ContainerFromElement(TerminalTabs, source) is not FrameworkElement { DataContext: TerminalPaneViewModel pane } container
                || pane.Request.IsMenu) return;
            e.Handled = true;
            var tab = Shown;
            var placement = e.CursorLeft < 0 ? PlacementMode.Bottom : PlacementMode.MousePoint;
            var agent = pane.HostPid is { } host ? tab.AgentOfHost(host) : null;
            var menu = DropdownMenu(container, $"Move the {pane.Name} CLI to", placement);
            if (agent is null)
            {
                var wait = DropdownItem("Move to");
                wait.IsEnabled = false;
                wait.ToolTip = "This CLI isn't known to the Orchestrator yet: it shows up once its first turn starts";
                ToolTipService.SetShowOnDisabled(wait, true);
                menu.Items.Add(wait);
                menu.IsOpen = true;
                return;
            }
            var targets = await tab.MoveTargetsAsync(agent.OwnLogin);
            if (targets.Count == 0)
            {
                var none = DropdownItem("Move to: no other signed-in login");
                none.IsEnabled = false;
                menu.Items.Add(none);
            }
            foreach (var target in targets)
            {
                var item = DropdownItem($"Move to {target.Label}");
                item.ToolTip = target.IsGlobal ? "The global login (~/.claude)" : $"{target.Email}'s own login (~/.claude-accounts)";
                item.Click += async (_, _) =>
                {
                    try { await tab.MoveToAccountAsync(agent.Repo, agent.SessionId, agent.CliPid, target); }
                    catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Error(ex, "Moving a CLI failed; contained"); }
                };
                menu.Items.Add(item);
            }
            menu.IsOpen = true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Error(ex, "Building the Move to menu failed; contained"); }
    }

    /// <summary>A person's tab's right-click menu (M.ORCH-T797; Shift+F10 or the menu key on a focused tab too): Rename, Org
    /// limit and Make default, the login actions that were standalone buttons on the person's page. Make default is this
    /// person's own (it signs the global fallback login in to their account), so it's here, shown only while it applies. The
    /// tab is selected first, since the Rename and Org limit dialogs are the shown tab's.</summary>
    private void OnLoginTabContextMenu(object sender, ContextMenuEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source
            || ItemsControl.ContainerFromElement(LoginTabStrip, source) is not FrameworkElement { DataContext: MainViewModel tab } container
            || !tab.IsPersonTab) return;
        e.Handled = true;
        var menu = DropdownMenu(container, $"{tab.TabTitle}'s login",
            e.CursorLeft < 0 ? PlacementMode.Bottom : PlacementMode.MousePoint); // from the keyboard: under the tab
        void Add(string header, string tip, ICommand command)
        {
            var item = DropdownItem(header);
            item.ToolTip = tip;
            ToolTipService.SetShowOnDisabled(item, true);
            item.IsEnabled = command.CanExecute(null);
            item.Click += (_, _) =>
            {
                model.SelectedTab = tab;
                if (command.CanExecute(null)) command.Execute(null);
            };
            menu.Items.Add(item);
        }
        Add("Rename…", "Give this login a name and a terminal-title prefix (orchestrator accounts alias <email> <name>)", tab.RenamePersonCommand);
        Add("Org limit…", "Set this login's own 5-hour and weekly cutoffs (orchestrator org limit <who> --weekly --5h)", tab.OrgLimitCommand);
        if (tab.CanMakeDefault)
            Add("Make default", "Make this login the fallback: sign the global login (~/.claude) in to this account, opening its login in the browser. "
                + "Every CLI not started on a login of its own follows at its next turn. Billing goes back to Claude Team first if the API key bills.",
                tab.MakeDefaultCommand);
        menu.IsOpen = true;
    }

    /// <summary>The library's dark dropdown (M.ORCH-T667): a bare ContextMenu and MenuItem draw WPF's light system menu.</summary>
    private static ContextMenu DropdownMenu(UIElement target, string name, PlacementMode placement)
    {
        var menu = new ContextMenu { PlacementTarget = target, Placement = placement };
        menu.SetResourceReference(StyleProperty, "MaDropdownMenu");
        AutomationProperties.SetName(menu, name);
        return menu;
    }

    private static MenuItem DropdownItem(object header)
    {
        var item = new MenuItem { Header = header };
        item.SetResourceReference(StyleProperty, "MaDropdownMenuItem");
        return item;
    }

    private bool Hidden => !IsVisible || WindowState == WindowState.Minimized;

    // Whether the poll may run: set once the first refresh is done, cleared as the window closes.
    private bool polling;

    /// <summary>Starts, retimes or stops the poll (<see cref="RefreshInterval"/>): stopped while the daemon's stream pushes everything.</summary>
    private void Retime()
    {
        if (RefreshInterval(Hidden, model.HoldsLease, model.IsStreamLive) is not { } interval)
        {
            timer.Stop();
            return;
        }
        if (timer.Interval != interval) timer.Interval = interval;
        if (polling && !timer.IsEnabled) timer.Start();
    }

    /// <summary>
    /// How often to refresh: while this window holds the lease (no daemon builds the snapshots) every 5s shown and 15s hidden, as
    /// it feeds rl-status.json and allocate; with no daemon's status stream up (disconnected, or its daemon builds no snapshots),
    /// every 5s shown and 60s hidden. With it up, never (null; M.ORCH-T891, was a 5-minute heartbeat, M.ORCH-T872): every figure
    /// a snapshot gives is pushed with it and the figures say when the rest of the snapshot moved (M.ORCH-T872), ticket changes
    /// are pushed (M.ORCH-T879), and so are the member roster, the orgs, the settings, the pause and the reminders, by whoever
    /// changes them (M.ORCH-T891), so nothing shown is left to a timed read. A stream that drops, or whose daemon stops building
    /// snapshots, goes back to the poll at once.
    /// </summary>
    internal static TimeSpan? RefreshInterval(bool hidden, bool holdsLease, bool streamLive = false) =>
        holdsLease ? (hidden ? HiddenHolderInterval : PollInterval)
        : streamLive ? null
        : !hidden ? PollInterval : HiddenInterval;

    private bool wasHidden;

    private async Task OnShownOrHidden()
    {
        Retime();
        var shown = wasHidden && !Hidden;
        wasHidden = Hidden;
        model.SetWatched(!Hidden);
        if (shown && IsLoaded) await model.RefreshAsync();
    }

    /// <summary>The share of the window the Settings dialog fills, across and down (M.ORCH-T804).</summary>
    internal const double SettingsShare = 0.8;

    /// <summary>MaModal's own chrome around its content: the title row, the content padding, the footer and the card's border,
    /// taken off the 80% so the whole card, not just its body, is 80% of the window tall.</summary>
    private const double SettingsChrome = 132;

    /// <summary>Settings at 80% of its container (M.ORCH-T804), not a tall fixed-width modal: MaModal has a width
    /// (DialogWidth) but no height of its own, so the body's Height sets the card's. Root's size is before the zoom's scale,
    /// the same space the modal lays out in.</summary>
    private void SizeSettings()
    {
        var (width, height) = SettingsSize(Root.ActualWidth, Root.ActualHeight);
        SettingsModal.DialogWidth = width;
        SettingsBody.Height = height;
    }

    /// <summary>The Settings card's width and its body's height for a container of <paramref name="width"/> by
    /// <paramref name="height"/>: 80% across, and 80% down less MaModal's chrome, never under a usable minimum.</summary>
    internal static (double Width, double BodyHeight) SettingsSize(double width, double height) =>
        (Math.Max(360, width * SettingsShare), Math.Max(220, height * SettingsShare - SettingsChrome));

    private void OnEscape()
    {
        var shown = Shown;
        if (shown.IsSettingsOpen) shown.IsSettingsOpen = false;
        else if (shown.IsRenameOpen) shown.IsRenameOpen = false;
        else if (shown.IsLimitOpen) shown.IsLimitOpen = false;
    }

    /// <summary>Says <paramref name="text"/> through a screen reader without moving focus (WCAG 4.1.3, status messages):
    /// the status bar's hidden live region takes the text and raises LiveRegionChanged.</summary>
    private void Announce(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        Announcer.Text = text;
        (UIElementAutomationPeer.FromElement(Announcer) ?? UIElementAutomationPeer.CreatePeerForElement(Announcer))
            ?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    /// <summary>A session that needs you should be noticed even when this window is behind others. The toast row (MaToastHost)
    /// reads each new one out itself.</summary>
    private void OnToastAdded()
    {
        if (IsActive) return;
        var info = new FlashInfo
        {
            Size = (uint)Marshal.SizeOf<FlashInfo>(),
            Hwnd = new WindowInteropHelper(this).Handle,
            Flags = FlashTray | FlashTimerNoForeground,
            Count = 3,
        };
        FlashWindowEx(ref info);
    }

    private const uint FlashTray = 0x2, FlashTimerNoForeground = 0xC;

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashInfo
    {
        public uint Size;
        public IntPtr Hwnd;
        public uint Flags;
        public uint Count;
        public uint Timeout;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FlashInfo info);
}
