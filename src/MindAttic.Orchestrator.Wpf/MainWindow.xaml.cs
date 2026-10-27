using System.Collections.Specialized;
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
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Term;
using MindAttic.Orchestrator.Wpf.ViewModels;

namespace MindAttic.Orchestrator.Wpf;

public partial class MainWindow : Window
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    /// <summary>Minimized while the daemon builds the snapshots: nobody is looking, so just enough to stay roughly current.</summary>
    private static readonly TimeSpan HiddenInterval = TimeSpan.FromSeconds(60);

    /// <summary>Hidden but holding the lease (no daemon running): the daemon's own pace, since rl-status.json and allocate depend on it.</summary>
    private static readonly TimeSpan HiddenHolderInterval = TimeSpan.FromSeconds(15);

    /// <summary>How often the hosted-session registry is read again for a host whose bus announcement was lost (M.ORCH-T1001).</summary>
    private static readonly TimeSpan TermResyncInterval = TimeSpan.FromSeconds(45);

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
        TerminalFrame.IsVisibleChanged += (_, _) => SizeTerminalRow();
        // A person's current work (including, on the global login's tab, what fits on no person's page, M.ORCH-T825) is one
        // virtualized list (M.ORCH-T754): headings and notes, then the lists' items a row (or a line of cards) each, so a
        // switch of tab or org builds only what's on screen.
        VirtualizedList.ApplyBody(AgentsBody);
        model.SelectedTabChanged += (_, _) => DataContext = model.SelectedTab;
        // The scrolled login strip follows the selected tab (M.ORCH-T1025): Ctrl+Tab, Show who's working or an org switch
        // can select one off screen.
        model.SelectedTabChanged += (_, _) => BringSelectedTabIntoView();
        model.OrgTabs.CollectionChanged += (_, _) => BringSelectedTabIntoView();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is MainViewModel was)
            {
                was.PropertyChanged -= OnShownChanged;
                was.Terminals.SelectionChanged -= BringSelectedTerminalTabIntoView;
                was.Terminals.Panes.CollectionChanged -= OnTerminalPanesChanged;
            }
            if (e.NewValue is MainViewModel now)
            {
                now.PropertyChanged += OnShownChanged;
                now.Terminals.SelectionChanged += BringSelectedTerminalTabIntoView;
                now.Terminals.Panes.CollectionChanged += OnTerminalPanesChanged;
            }
        };
        model.PropertyChanged += OnShownChanged;
        // Scales the window's content and binds Ctrl+Plus / Ctrl+Minus / Ctrl+0 on the window, wherever focus is.
        model.Zoom.Attach(Root, this);
        // The upper-right sun/moon switch (M.ORCH-T678) changes the theme live; each switch is saved, so it holds at the next start.
        ThemeChoice.SaveSwitches(this, model.Store);
        // No emergency billing button any more (M.ORCH-T906): the API key is a person with its own tab, Launcher and CLIs.

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
            foreach (var added in e.NewItems?.OfType<TerminalPaneViewModel>() ?? []) TerminalPanesHost.Children.Add(added.Control);
        };
        TerminalClip.Attach(TerminalPanesHost);
        TerminalKeyboard.Attach(this, TerminalPanesHost);
        TerminalMouse.Attach(this, TerminalPanesHost);
        PreviewKeyDown += TerminalPaste.OnPreviewKeyDown;
        // Ctrl+C / Ctrl+X with a selection copy / cut it (M.ORCH-T867, M.ORCH-T866); with none they still reach the CLI (^C, ^X).
        PreviewKeyDown += TerminalCopy.OnPreviewKeyDown;
        // Ctrl+Shift+B pauses or resumes the shown CLI's remote broadcast (M.ORCH-T935), even while its terminal holds focus.
        PreviewKeyDown += OnBroadcastKey;
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
            // Every CLI still running in its own TermHost gets its tab back first (M.ORCH-T926): a crash or a redeploy of the
            // dashboard never ended it, so it needs no restore.
            // No Launcher respawns for a tab gone while the tabs come back (M.ORCH-T1150); the first refresh opens what's missing.
            model.RestoringTerminals = true;
            if (restoreOnLoad) await model.AllTerminals.ReattachAsync();
            // And every 45s after (M.ORCH-T1001): a term-session announcement lost on the bus (sent while the daemon restarted)
            // never gave its host a tab, since the bus keeps no copy and the registry was read only here.
            if (restoreOnLoad)
            {
                var resync = new DispatcherTimer(DispatcherPriority.Background) { Interval = TermResyncInterval };
                resync.Tick += async (_, _) => await model.AllTerminals.ResyncAsync();
                resync.Start();
                Closed += (_, _) => resync.Stop();
            }
            var restored = restoreOnLoad && SessionRestore.Offer(this, model);
            await model.RefreshAsync();
            if (restored) model.ReselectRestored();
            model.RestoringTerminals = false;
            polling = true;
            Retime();
        };
        // Closing, no tab gone spawns a Launcher (M.ORCH-T1150).
        Closing += (_, _) => model.ShuttingDown = true;
        Closed += (_, _) =>
        {
            model.ShuttingDown = true;
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
        // Hosted CLIs (M.ORCH-T926): one started elsewhere (the Launcher, M.LNCH-T82) gets a tab, one that ended is marked ended.
        bus.On<Term.TermSessionEvent>(Term.TermBus.Kind, (_, session) => model.AllTerminals.OnTermSession(session), Term.TermBus.Version);
        // Remote viewers attaching and detaching through the team gateway (M.ORCH-T935): the "being watched" banner.
        bus.On<Term.TermWatchersEvent>(Term.TermWatchersBus.Kind, (_, watchers) => model.AllTerminals.OnWatchers(watchers), Term.TermWatchersBus.Version);
        // The roster, the orgs, the settings and the reminders (M.ORCH-T891), pushed by whoever changed them: with
        // these, nothing the dashboard shows needs a timed read while the stream is up.
        bus.On<Status.MembersPayload>(Status.StateBus.Members, (_, p) => OnPushedState(p), Status.StateBus.Version)
            .On<Status.OrgsPayload>(Status.StateBus.Orgs, (_, p) => OnPushedState(p), Status.StateBus.Version)
            .On<Status.SettingsPayload>(Status.StateBus.Settings, (_, p) => OnPushedState(p), Status.StateBus.Version)
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
        // A tab's CLI drew its usage figures (M.ORCH-T1118): laid over that login's tab at once, as a probe's reading is; the
        // daemon, told on its inbox, publishes it to every dashboard and brings the pace and stops forward.
        void OnScreenReading(string? email, Budget.ReportedUsage reading)
        {
            var message = new Status.StatusMessage
            {
                Kind = Status.StatusMessage.Usage, AtUtc = DateTime.UtcNow, Pid = Environment.ProcessId,
                Readings = Status.MeterReading.Of(email, reading, signedIn: null, Budget.ScreenUsage.Source) is { } r ? [r] : [],
            };
            Dispatcher.InvokeAsync(() => OnStatus(message));
        }
        Budget.ScreenUsage.Landed += OnScreenReading;
        Closed += (_, _) =>
        {
            statusStream.Cancel();
            Budget.ClaudeMeterProbe.ReadingLanded -= OnLocalReading;
            Budget.ScreenUsage.Landed -= OnScreenReading;
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
    /// Settings or User Settings is open, the loader covers the window or an org with no login covers the page.</summary>
    private void UpdateTerminalsCover()
    {
        var shown = Shown;
        var covered = shown.IsSettingsOpen || shown.IsUserSettingsOpen || model.IsLoading || model.IsOrgEmpty;
        TerminalPanesHost.Visibility = covered ? Visibility.Hidden : Visibility.Visible;
    }

    /// <summary>
    /// Hands the keyboard back to the shown CLI's terminal (M.ORCH-T846), queued past the click, layout or refresh that
    /// prompted it (a terminal shown again only gets its native window back on the layout pass). It stays put while the window
    /// isn't active, a dialog is open, the terminals are covered, or focus is somewhere the user chose to be: a text box (Settings,
    /// User Settings), an open menu or dropdown, another window. With <paramref name="onlyIfLost"/> (a refresh) it only takes focus
    /// back that went nowhere, as when a rebuilt list dropped the focused row, so tabbing round the page still works.
    /// </summary>
    private void PinTerminalFocus(bool onlyIfLost = false) =>
        Dispatcher.InvokeAsync(() =>
        {
            var shown = Shown;
            if (!IsActive || shown.IsSettingsOpen || shown.IsUserSettingsOpen
                || TerminalPanesHost.Visibility != Visibility.Visible) return;
            if (model.AllTerminals.SelectedPane?.Control is not { IsVisible: true } terminal) return;
            var focused = Keyboard.FocusedElement as DependencyObject;
            if (ReferenceEquals(focused, terminal) || KeepsFocus(focused, onlyIfLost)) return;
            // Null FocusedElement is the normal state while the renderer's own window has Win32 focus: keep it, or every
            // refresh bounces focus (flicker, a cancelled IME composition) (bughunt 2026-10-27).
            if (focused is null && TerminalKeyboard.HoldsWin32Focus(terminal)) return;
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
        if (e.PropertyName is nameof(MainViewModel.IsSettingsOpen) or nameof(MainViewModel.IsUserSettingsOpen))
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

    /// <summary>The least <see cref="AgentsBody"/> (and the sign-in prompt sharing its row) is ever given while the terminal
    /// shows: enough of the 5-hour/weekly chart card's own header and its 130px chart to clear the status bar below, so the
    /// chart's own bottom axis labels (its target and projection, M.ORCH-T191) never clip against it (M.ORCH-T1072, M.ORCH-T1095,
    /// M.ORCH-T1169, M.ORCH-T1180). The card's own top padding and border (11), its now-two-line header — a combined row of the
    /// notification dots and the 5hr/weekly limit boxes (M.ORCH-T1180, moved out of sharing a row with the title), then the
    /// title (MaSectionLabel) on its own line with its SpaceTS top margin — and the chart's own top margin (8) and height (130)
    /// come to an estimated ~215 before the caption below it, which the floor leaves to the body's own ScrollViewer
    /// (MaBodyScroll) rather than guaranteeing too. This is a reasoned estimate (the exact MaSectionLabel/MaCard metrics live in
    /// a referenced library with no local source to measure against), the same way M.ORCH-T1095 and M.ORCH-T1169 nudged this
    /// same constant after watching it run; if the chart's bottom labels clip again, nudge this up a little further rather than
    /// reworking the approach. Above this floor the body shares the leftover room with the terminal (M.ORCH-T1166) rather than
    /// being pinned to it as a fixed peek, so it isn't left showing only a sliver with a dead gap below the terminal above it.</summary>
    private const double AgentsBodyPeek = 215;

    /// <summary>How tall the terminal row is let grow while a terminal shows (M.ORCH-T1166): comfortably more than a CLI
    /// needs for its visible transcript, so scrollback and a tall prompt still have room, but not so much that a short
    /// transcript (just the startup banner) leaves a dead stretch of unwritten terminal background under it, as it did while
    /// the row's Height was a plain, uncapped star (M.ORCH-T1072). The same 480 that row shared 3:1 with the body before
    /// M.ORCH-T1072 made it the star row outright.</summary>
    private const double TerminalRowMaxHeight = 480;

    /// <summary>The terminal and the scrolling body (stats, queue tabs, the 5-hour/weekly chart, tickets) share whatever room
    /// is left under the fixed rows above and below, both star rows, while the terminal shows (M.ORCH-T1072): the terminal's
    /// capped at <see cref="TerminalRowMaxHeight"/>, past which it stops claiming room it has nothing to show in, and the body
    /// picks up everything beyond that, floored at <see cref="AgentsBodyPeek"/> so it's never squeezed thinner than that even
    /// in a short window (M.ORCH-T1020, M.ORCH-T1166) — never pinned to that floor as a fixed height the way it used to be,
    /// which is what left a dead gap of unwritten terminal background above it whenever the transcript was short. Hidden (no
    /// terminals open on this login), the terminal's row takes no room and the body reclaims the whole star share, filling the
    /// window as it did before there was a terminal to share it with.</summary>
    private void SizeTerminalRow()
    {
        try
        {
            var terminalRow = Root.RowDefinitions[2];
            var bodyRow = Root.RowDefinitions[4];
            var shown = TerminalFrame.IsVisible;
            terminalRow.Height = shown ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
            terminalRow.MaxHeight = shown ? TerminalRowMaxHeight : double.PositiveInfinity;
            bodyRow.Height = new GridLength(1, GridUnitType.Star);
            bodyRow.MinHeight = shown ? AgentsBodyPeek : 120;
        }
        catch (Exception ex) { OrchestratorLog.For("Dashboard").Error(ex, "Sizing the terminal's row failed; contained"); }
    }

    /// <summary>Room added on each side of the widest person tab's own content (M.ORCH-T1096).</summary>
    private const double PersonTabSidePadding = 6;

    /// <summary>The tab parts already watched for size changes, so a header loading again doesn't subscribe twice.</summary>
    private readonly HashSet<FrameworkElement> watchedTabParts = [];

    /// <summary>A person tab's header content: every person's tab is the same width (M.ORCH-T803), that of the widest one's
    /// own content plus <see cref="PersonTabSidePadding"/> a side (M.ORCH-T1096), so short names don't sit in a wide tab. The
    /// library's MaTabItem has no Width of its own (it's plain FrameworkElement's), so the ItemContainerStyle (M.ORCH-T667) can't
    /// set it; it's set directly on each generated container, again whenever a tab's icon, name or trailing text changes size.</summary>
    private void LoginTabItem_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: MainViewModel { IsPersonTab: true } } content) return;
        if (ItemsControl.ContainerFromElement(LoginTabStrip, content) is not TabItem tab) return;
        tab.ApplyTemplate();
        if (TabRow(tab) is { } row)
            foreach (var part in row.Children.OfType<FrameworkElement>())
                if (watchedTabParts.Add(part)) part.SizeChanged += (_, _) => SizePersonTabs();
        SizePersonTabs();
    }

    /// <summary>The row an MaTabItem lays its status light, header and trailing text out in (its template's Bd border's child).</summary>
    private static StackPanel? TabRow(TabItem tab) => (tab.Template?.FindName("Bd", tab) as Border)?.Child as StackPanel;

    /// <summary>Its content's natural width: each part of its row is measured with unbounded width, so this never depends on
    /// the Width already set on it.</summary>
    private static double NaturalWidth(TabItem tab) =>
        TabRow(tab) is { } row
            ? tab.Padding.Left + tab.Padding.Right + row.Children.OfType<UIElement>().Where(c => c.Visibility != Visibility.Collapsed).Sum(c => c.DesiredSize.Width)
            : 0;

    private void SizePersonTabs()
    {
        var tabs = LoginTabStrip.Items.Cast<object>()
            .Select(item => LoginTabStrip.ItemContainerGenerator.ContainerFromItem(item) as TabItem)
            .Where(t => t is { DataContext: MainViewModel { IsPersonTab: true } }).Cast<TabItem>().ToList();
        if (tabs.Count == 0) return;
        var width = Math.Ceiling(tabs.Max(NaturalWidth) + 2 * PersonTabSidePadding);
        foreach (var t in tabs)
            if (double.IsNaN(t.Width) || Math.Abs(t.Width - width) > 0.5) t.Width = width;
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

    /// <summary>Scrolls <see cref="LoginTabScroller"/> to the selected person's tab once the strip has laid it out
    /// (M.ORCH-T1025). Never throws.</summary>
    private void BringSelectedTabIntoView() =>
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                if (LoginTabStrip.ItemContainerGenerator.ContainerFromItem(model.SelectedTab) is FrameworkElement tab) tab.BringIntoView();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Error(ex, "Scrolling to the selected tab failed; contained"); }
        }, DispatcherPriority.Loaded);

    /// <summary>One person tab's width, what the strip's arrows scroll by.</summary>
    private double TabScrollStep =>
        LoginTabStrip.Items.Count > 0 && LoginTabStrip.ItemContainerGenerator.ContainerFromIndex(0) is FrameworkElement { ActualWidth: > 0 } first
            ? first.ActualWidth : 200;

    private void LoginTabScrollLeft_Click(object sender, RoutedEventArgs e) =>
        LoginTabScroller.ScrollToHorizontalOffset(LoginTabScroller.HorizontalOffset - TabScrollStep);

    private void LoginTabScrollRight_Click(object sender, RoutedEventArgs e) =>
        LoginTabScroller.ScrollToHorizontalOffset(LoginTabScroller.HorizontalOffset + TabScrollStep);

    /// <summary>No wrap, ever (M.ORCH-T1171), the CLI tabs' own version of <see cref="LoginTabScroller_ScrollChanged"/>: each
    /// tab already sizes to its own content, so these arrows only page the strip past the window's width rather than let it
    /// wrap to a second row.</summary>
    private void TerminalTabScroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        var can = TerminalTabScroller.ScrollableWidth > 0.5;
        TerminalTabScrollLeft.Visibility = TerminalTabScrollRight.Visibility = can ? Visibility.Visible : Visibility.Collapsed;
        TerminalTabScrollLeft.IsEnabled = TerminalTabScroller.HorizontalOffset > 0.5;
        TerminalTabScrollRight.IsEnabled = TerminalTabScroller.HorizontalOffset < TerminalTabScroller.ScrollableWidth - 0.5;
    }

    /// <summary>Scrolls <see cref="TerminalTabScroller"/> to the shown tab's selected CLI once the strip has laid it out, the
    /// same way <see cref="BringSelectedTabIntoView"/> does for the login tabs. Never throws.</summary>
    private void BringSelectedTerminalTabIntoView() =>
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                if (TerminalTabs.ItemContainerGenerator.ContainerFromItem(Shown.Terminals.SelectedPane) is FrameworkElement tab) tab.BringIntoView();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Error(ex, "Scrolling to the selected CLI tab failed; contained"); }
        }, DispatcherPriority.Loaded);

    private void OnTerminalPanesChanged(object? sender, NotifyCollectionChangedEventArgs e) => BringSelectedTerminalTabIntoView();

    /// <summary>One CLI tab's width, what the strip's arrows scroll by.</summary>
    private double TerminalTabScrollStep =>
        TerminalTabs.Items.Count > 0 && TerminalTabs.ItemContainerGenerator.ContainerFromIndex(0) is FrameworkElement { ActualWidth: > 0 } first
            ? first.ActualWidth : 200;

    private void TerminalTabScrollLeft_Click(object sender, RoutedEventArgs e) =>
        TerminalTabScroller.ScrollToHorizontalOffset(TerminalTabScroller.HorizontalOffset - TerminalTabScrollStep);

    private void TerminalTabScrollRight_Click(object sender, RoutedEventArgs e) =>
        TerminalTabScroller.ScrollToHorizontalOffset(TerminalTabScroller.HorizontalOffset + TerminalTabScrollStep);

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
            AddPopOutItems(menu, pane);
            AddBroadcastItems(menu, pane);
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

    /// <summary>"Pop out (interactive)" and "Pop out (watch only)" (M.ORCH-T931): a TermViewer window on this CLI beside its tab,
    /// which stays attached. Needs a hosted CLI on a protocol 2 host (one that serves several viewers).</summary>
    private static void AddPopOutItems(ContextMenu menu, TerminalPaneViewModel pane)
    {
        foreach (var (header, watchOnly) in new[] { ("Pop out (interactive)", false), ("Pop out (watch only)", true) })
        {
            var item = DropdownItem(header);
            var session = pane.Session;
            if (session is null || session.Protocol < TermProtocol.MultiViewer)
            {
                item.IsEnabled = false;
                item.ToolTip = session is null ? "Only a hosted CLI can be popped out" : "This CLI's host serves one viewer; it can be popped out once restarted on a newer host";
                ToolTipService.SetShowOnDisabled(item, true);
            }
            else
                item.Click += (_, _) =>
                {
                    try
                    {
                        if (FallbackViewers.PopOut(session, watchOnly) is { } started) started.Dispose();
                        else OrchestratorLog.For("Dashboard").Warning("No TermViewer could be started for {Id:l}", session.Id);
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Error(ex, "Popping out a CLI failed; contained"); }
                };
            menu.Items.Add(item);
        }
        menu.Items.Add(new Separator());
    }

    /// <summary>A person's tab's right-click menu (M.ORCH-T797; Shift+F10 or the menu key on a focused tab too): User Settings
    /// (M.ORCH-T1098, Rename and Org limit merged), the login action that was standalone buttons on the person's page. The tab is
    /// selected first, since the dialog is the shown tab's.</summary>
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
        Add("User Settings…", "This login's name, terminal-title prefix and own 5-hour and weekly cutoffs (orchestrator accounts alias, orchestrator org limit)",
            tab.UserSettingsCommand);
        menu.IsOpen = true;
    }

    /// <summary>Ctrl+Shift+B (M.ORCH-T935): pauses or resumes the shown CLI's remote broadcast, wherever focus is.</summary>
    private void OnBroadcastKey(object sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.B || Keyboard.Modifiers != (ModifierKeys.Control | ModifierKeys.Shift)) return;
        if (model.AllTerminals.SelectedPane is not { IsHosted: true } pane) return;
        model.AllTerminals.TogglePauseCommand.Execute(pane);
        e.Handled = true;
    }

    /// <summary>The broadcast items of a hosted CLI tab's right-click menu (M.ORCH-T935): share it with remote viewers, mask
    /// secrets on its remote streams, pause its broadcast, and "share my sessions".</summary>
    private void AddBroadcastItems(ContextMenu menu, TerminalPaneViewModel pane)
    {
        if (!pane.IsHosted || pane.TermId is null) return;
        var all = model.AllTerminals;
        void Add(string header, bool isChecked, string tip, ICommand command, object? parameter)
        {
            var item = DropdownItem(header);
            item.IsCheckable = true;
            item.IsChecked = isChecked;
            item.ToolTip = tip;
            item.Click += (_, _) => { if (command.CanExecute(parameter)) command.Execute(parameter); };
            menu.Items.Add(item);
        }
        Add("Share with remote viewers", pane.IsShared, "Let the team gateway show this CLI to remote viewers you've granted (off by default)", all.ToggleShareCommand, pane);
        Add("Mask secrets for remote viewers", pane.IsMasked, "Hide API keys, tokens, private keys and Vault-held values on remote streams (on by default; never on this screen)", all.ToggleMaskCommand, pane);
        Add("Pause broadcast", pane.IsBroadcastPaused, "Remote viewers see \"paused by owner\" and nothing else until you resume (Ctrl+Shift+B)", all.TogglePauseCommand, pane);
        Add("Share all my sessions", all.ShareAll, "Share every CLI with remote viewers unless one says otherwise", all.ShareAllCommand, null);
        menu.Items.Add(new Separator());
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
        else if (shown.IsUserSettingsOpen) shown.IsUserSettingsOpen = false;
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
