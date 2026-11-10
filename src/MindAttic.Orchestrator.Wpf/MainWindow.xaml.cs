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
using System.Windows.Threading;
using MindAttic.Components.Wpf.Controls;
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
        // The org-wide login actions (Add login, Sign in all, Report), in the Logins dialog since LAMP moved the settings to
        // MindAttic Vault (M.ORCH-T1308), are the global login's tab's too (M.ORCH-T763), not the shown tab's.
        OrgToolbar.DataContext = model;
        // The menu bar's window tools and host readout (M.ORCH-T1499) are the global login's tab's too, like the org strip:
        // the same Team Wall/Tickets/Logins/Settings/Wind down/theme toggle and the same C: free space and RAM reading,
        // regardless of which login's tab is shown.
        MenuBarTools.DataContext = model;
        // The phone layout's person picker (M.ORCH-T1568) is the person sidebar's stand-in: the global login's tab's too.
        PersonPicker.DataContext = model;
        PhoneStatus.DataContext = model;
        Root.SizeChanged += (_, _) => SizeTerminalRow();
        TerminalFrame.IsVisibleChanged += (_, _) => SizeTerminalRow();
        // AgentsBody's own height can change without Root's (e.g. TerminalsPanel's tab strip showing or hiding above it),
        // and TerminalFrame's size is now a share of AgentsBody's viewport, not Root's (M.ORCH-T1302).
        AgentsBody.SizeChanged += (_, _) => SizeTerminalRow();
        AttachCompactLayout();
        // A person's current work (including, on the global login's tab, what fits on no person's page, M.ORCH-T825) is one
        // virtualized list (M.ORCH-T754): headings and notes, then the lists' items a row (or a line of cards) each, so a
        // switch of tab or org builds only what's on screen. AgentsBodyList, not AgentsBody's own direct content, is what's
        // flattened (M.ORCH-T1302): TerminalFrame, ahead of it in AgentsBody's content, stays a plain, always-realized sibling
        // so a native terminal window is never torn down by the virtualizing panel's own realize/derealize cycle.
        VirtualizedList.ApplyBody(AgentsBody, AgentsBodyList);
        // One scroll gesture works anywhere over the window (M.ORCH-T1299), not only once the cursor already sits over
        // AgentsBody's own viewport: the login tabs and CLI tabs are horizontal ScrollViewers that mark a wheel tick handled
        // on their own even with vertical scrolling disabled (swallowing it before it would ever bubble up).
        PreviewMouseWheel += MainWindow_PreviewMouseWheel;
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
        // Compensates for the root tab's own DataContext assignment above (line 43) happening before DataContextChanged is
        // wired (M.ORCH-T1282): without this, the root tab's terminal strip would only start auto-scrolling once the user
        // switched login tabs and back, the same reason model.PropertyChanged is compensated for here too.
        model.Terminals.SelectionChanged += BringSelectedTerminalTabIntoView;
        model.Terminals.Panes.CollectionChanged += OnTerminalPanesChanged;
        // Scales the window's content and binds Ctrl+Plus / Ctrl+Minus / Ctrl+0 on the window, wherever focus is.
        model.Zoom.Attach(Root, this);
        // No theme switch of its own any more (M.ORCH-T1512): changed from MindAttic.Vault.Desktop's Orchestrator settings
        // page instead, and ThemeChoice.Follow (below) picks it up live, with no restart.
        // No emergency billing button any more (M.ORCH-T906): the API key is a person with its own tab, Launcher and CLIs.

        CommandBindings.Add(new CommandBinding(WindowCommands.Escape, (_, _) => OnEscape(),
            // Esc belongs to the CLI while a terminal has focus (M.ORCH-T852): not executable, so the key passes through.
            (_, e) => e.CanExecute = !TerminalKeyboard.HoldsFocus(TerminalPanesHost)));

        // Each terminal lives in the Terminals panel's host grid from its opening to its closing (M.ORCH-T810): a template would
        // build a new terminal (and a new CLI) every time its tab was shown. Native windows, they sit inside AgentsBody's scroll
        // view (M.ORCH-T1302, under the fixed CLI tab bar), so TerminalClip (below) keeps them clipped to its viewport as it
        // scrolls rather than painting over the tabs above, and they're hidden while a dialog or the loader covers the window,
        // which they'd otherwise draw over.
        model.AllTerminals.Panes.CollectionChanged += (_, e) =>
        {
            foreach (var gone in e.OldItems?.OfType<TerminalPaneViewModel>() ?? []) TerminalPanesHost.Children.Remove(gone.Control);
            foreach (var added in e.NewItems?.OfType<TerminalPaneViewModel>() ?? [])
            {
                TerminalPanesHost.Children.Add(added.Control);
                if (added.Control is FrameworkElement terminal) TerminalScrollbar.Hide(terminal);
            }
        };
        // Closing a CLI tab by hand asks first (M.ORCH-T1437).
        model.AllTerminals.ConfirmClose = ConfirmCloseTerminal;
        // The Rename tab modal closed by its own X or backdrop is a cancel (M.ORCH-T1509).
        RenameTabModal.Closed += (_, _) => { if (renamingTab is not null) CloseRenameTab(null); };
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
            // Each person back on the CLI tab they last had chosen (M.ORCH-T1436), kept by TermHost session in the restore file.
            if (restoreOnLoad) model.ReselectRemembered(SessionRestore.Previous);
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
        // Back where it was last time, maximized or not, before it's shown (M.ORCH-T1510).
        SessionRestore.TrackWindow(this);
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
            // LAMP (M.ORCH-T1308): the theme chosen in MindAttic.Vault.Desktop's Orchestrator settings page (M.ORCH-T1512) applies
            // here live, with no restart.
            if (payload is Status.SettingsPayload) ThemeChoice.Follow(Application.Current?.Resources, model.Store);
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
    /// Logins or User Settings is open, the loader covers the window or an org with no login covers the page.</summary>
    private void UpdateTerminalsCover()
    {
        var shown = Shown;
        var covered = shown.IsLoginsOpen || shown.IsUserSettingsOpen || RenameTabModal.IsOpen || model.IsLoading || model.IsOrgEmpty;
        TerminalPanesHost.Visibility = covered ? Visibility.Hidden : Visibility.Visible;
    }

    /// <summary>
    /// Hands the keyboard back to the shown CLI's terminal (M.ORCH-T846), queued past the click, layout or refresh that
    /// prompted it (a terminal shown again only gets its native window back on the layout pass). It stays put while the window
    /// isn't active, a dialog is open, the terminals are covered, or focus is somewhere the user chose to be: a text box (Logins,
    /// User Settings), an open menu or dropdown, another window. With <paramref name="onlyIfLost"/> (a refresh) it only takes focus
    /// back that went nowhere, as when a rebuilt list dropped the focused row, so tabbing round the page still works.
    /// </summary>
    private void PinTerminalFocus(bool onlyIfLost = false) =>
        Dispatcher.InvokeAsync(() =>
        {
            var shown = Shown;
            if (!IsActive || shown.IsLoginsOpen || shown.IsUserSettingsOpen || RenameTabModal.IsOpen
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
        if (e.PropertyName is nameof(MainViewModel.IsLoginsOpen) or nameof(MainViewModel.IsUserSettingsOpen))
        {
            UpdateTerminalsCover();
            PinTerminalFocus(); // a dialog closing hands the keyboard back to the CLI
        }
        if (e.PropertyName == nameof(MainViewModel.StatusMessage)) Announce(Shown.StatusMessage);
        else if (e.PropertyName == nameof(MainViewModel.QueueMessage)) Announce(Shown.QueueMessage);
    }

    // M.ORCH-T770/T775's glow-pulse animation (GlowColor/Pulse) was superseded by the boxed status badges (M.ORCH-T1173/T1270)
    // and had no caller left anywhere in the project; removed as dead code (M.ORCH-T1406).

    /// <summary>The terminal's floor while it shows: never shorter than this even in a short window.</summary>
    private const double TerminalRowMinHeight = 160;

    /// <summary>Sizes <see cref="TerminalFrame"/>, a plain part of <see cref="AgentsBody"/>'s content (M.ORCH-T1302), to
    /// AgentsBody's whole viewport: since the stats and tickets moved to the Usage tab (M.ORCH-T1572, ShowsBody) there's
    /// nothing under a CLI's terminal to scroll to, so the body doesn't scroll while one shows (no scroll bar) and the
    /// terminal fills it exactly; floored at <see cref="TerminalRowMinHeight"/> so it never pinches thin in a short window.
    /// Hidden (the Usage tab, or no terminals open on this login), the body scrolls again, over the charts and tickets.</summary>
    private void SizeTerminalRow()
    {
        try
        {
            var shows = TerminalFrame.IsVisible;
            AgentsBody.VerticalScrollBarVisibility = shows ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
            if (!shows) return;
            AgentsBody.ScrollToTop();
            var viewport = AgentsBody.ActualHeight;
            // Less the scroll area's own gutter (MaBodyScroll's 16 at the bottom), or the terminal's last row sat under its edge.
            var gutter = Components.Wpf.Controls.MaScrollArea.GetGutter(AgentsBody);
            if (viewport > 0) TerminalFrame.Height = Math.Max(TerminalRowMinHeight,
                viewport - gutter.Top - gutter.Bottom - TerminalFrame.Margin.Top - TerminalFrame.Margin.Bottom);
        }
        catch (Exception ex) { OrchestratorLog.For("Dashboard").Error(ex, "Sizing the terminal's row failed; contained"); }
    }

    /// <summary>Redirects a wheel tick anywhere over the window into <see cref="AgentsBody"/>'s own scroll (M.ORCH-T1299),
    /// except one already inside it (left alone so its native scrolling runs unchanged) or one while
    /// <see cref="LoginsModal"/> is open (it scrolls itself) or <see cref="UserSettingsModal"/> is (the page behind a modal's
    /// dimmed cover stays put). PreviewMouseWheel tunnels from the window
    /// down to the actual element before MouseWheel bubbles back up, so this fires ahead of any inner ScrollViewer along the
    /// way and can mark the tick handled before one swallows it.</summary>
    private void MainWindow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || !AgentsBody.IsVisible || LoginsModal.IsOpen || UserSettingsModal.IsOpen) return;
        if (e.OriginalSource is DependencyObject source && IsWithin(source, AgentsBody)) return;
        AgentsBody.ScrollToVerticalOffset(AgentsBody.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    private static bool IsWithin(DependencyObject? source, DependencyObject ancestor)
    {
        for (var node = source; node != null; node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, ancestor)) return true;
        }
        return false;
    }

    /// <summary>Room added on each side of the widest CLI tab's own content (M.ORCH-T1250).</summary>
    private const double TabSidePadding = 10;

    /// <summary>Extra width on top of <see cref="TabSidePadding"/> for the CLI tabs (M.ORCH-T1269).</summary>
    private const double CliTabWidthBoost = 10;

    /// <summary>The tab parts already watched for size changes, so a header loading again doesn't subscribe twice.</summary>
    private readonly HashSet<FrameworkElement> watchedTabParts = [];

    /// <summary>A CLI tab's header content: every CLI tab is the same width (M.ORCH-T1250), that of the widest one's own
    /// content plus <see cref="TabSidePadding"/> a side, set on each generated container (MaTabItem has no Width of its own
    /// for the ItemContainerStyle to set), again whenever a tab's parts change size.</summary>
    private void TerminalTabItem_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TerminalPaneViewModel } content) return;
        if (ItemsControl.ContainerFromElement(TerminalTabs, content) is not TabItem tab) return;
        tab.ApplyTemplate();
        // The header itself is stretched to the tab (M.ORCH-T1437), so its own parts are what grow with a longer name.
        var header = (content as Panel)?.Children.OfType<Panel>().SelectMany(p => p.Children.OfType<FrameworkElement>()) ?? [];
        if (TabRow(tab) is { } row)
        {
            var added = new List<FrameworkElement>();
            foreach (var part in row.Children.OfType<FrameworkElement>().Concat(header))
                if (watchedTabParts.Add(part))
                {
                    part.SizeChanged += OnTabPartSizeChanged;
                    added.Add(part);
                }
            // Released when the tab goes (closed, or another login's strip shown): the set held every header ever built, and
            // through them their closed panes, for the life of the window.
            if (added.Count > 0)
            {
                void Release(object? s, RoutedEventArgs a)
                {
                    content.Unloaded -= Release;
                    foreach (var part in added)
                    {
                        part.SizeChanged -= OnTabPartSizeChanged;
                        watchedTabParts.Remove(part);
                    }
                }
                content.Unloaded += Release;
            }
        }
        SizeTerminalTabs();
    }

    private void OnTabPartSizeChanged(object sender, SizeChangedEventArgs e) => SizeTerminalTabs();

    /// <summary>A double-click on a CLI tab's name renames it in place (M.ORCH-T1432): its TextBox, in the same cell, takes the
    /// name, all selected. A single click falls through, so it still selects the tab.</summary>
    private void TerminalTabName_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // The name sits in its marquee Canvas (MarqueeBox.Enabled, M.ORCH-T1509), which is what's swapped for the box.
        if (e.ClickCount != 2 || sender is not TextBlock { DataContext: TerminalPaneViewModel pane, Parent: Canvas { Parent: Grid cell } name }
            || cell.Children.OfType<TextBox>().FirstOrDefault() is not { } box) return;
        e.Handled = true;
        box.Tag = pane;
        box.Text = pane.Name;
        box.MinWidth = name.ActualWidth;
        box.MaxWidth = name.MaxWidth;
        name.Visibility = Visibility.Collapsed;
        box.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(() =>
        {
            if (!ReferenceEquals(box.Tag, pane)) return;
            box.Focus();
            Keyboard.Focus(box);
            box.SelectAll();
        }, DispatcherPriority.Input);
    }

    /// <summary>Enter keeps the new name (trimmed; blank keeps the old one); Esc cancels.</summary>
    private void TerminalTabNameEdit_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box || e.Key is not (Key.Enter or Key.Return or Key.Escape)) return;
        e.Handled = true;
        EndTerminalTabRename(box, commit: e.Key != Key.Escape);
    }

    /// <summary>Leaving the box without Enter cancels the rename (M.ORCH-T1432).</summary>
    private void TerminalTabNameEdit_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox box) EndTerminalTabRename(box, commit: false);
    }

    private void EndTerminalTabRename(TextBox box, bool commit)
    {
        if (box.Tag is not TerminalPaneViewModel pane) return;
        box.Tag = null; // first: hiding the focused box raises LostKeyboardFocus, which must find no edit left to cancel
        try { if (commit) model.AllTerminals.Rename(pane, box.Text); }
        finally
        {
            box.Visibility = Visibility.Collapsed;
            box.MinWidth = 0;
            if (box.Parent is Grid cell) foreach (var name in cell.Children.OfType<Canvas>()) name.Visibility = Visibility.Visible;
            if (commit && pane.IsSelected) pane.Control.Focus();
        }
    }

    /// <summary>The tab the Rename tab modal is naming (M.ORCH-T1509); null while it's closed.</summary>
    private TerminalPaneViewModel? renamingTab;

    /// <summary>Opens the Rename tab modal on <paramref name="pane"/> (M.ORCH-T1509, its right-click menu's Rename…): its name now
    /// in the box, all selected. The terminals are hidden meanwhile, since their native windows would draw over it.</summary>
    private void OpenRenameTab(TerminalPaneViewModel pane)
    {
        try
        {
            renamingTab = pane;
            RenameTabBox.Text = pane.Name;
            RenameTabHint.Text = $"Its own name is {pane.DefaultName}; Reset goes back to it. A name too long for the tab scrolls across it.";
            RenameTabModal.IsOpen = true;
            UpdateTerminalsCover();
            Dispatcher.BeginInvoke(() =>
            {
                if (!ReferenceEquals(renamingTab, pane)) return;
                RenameTabBox.Focus();
                Keyboard.Focus(RenameTabBox);
                RenameTabBox.SelectAll();
            }, DispatcherPriority.Input);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Error(ex, "Opening Rename tab failed; contained"); }
    }

    /// <summary>Closes the Rename tab modal, keeping <paramref name="name"/> as the tab's name when given (blank keeps the old one;
    /// its default name clears the custom one), saved by <see cref="TerminalsViewModel.Rename"/>.</summary>
    private void CloseRenameTab(string? name)
    {
        var pane = renamingTab;
        renamingTab = null;
        try { if (pane is not null && name is not null && model.AllTerminals.Panes.Contains(pane)) model.AllTerminals.Rename(pane, name); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Error(ex, "Renaming a tab failed; contained"); }
        finally
        {
            RenameTabModal.IsOpen = false;
            UpdateTerminalsCover();
            PinTerminalFocus();
        }
    }

    private void RenameTabSave_Click(object sender, RoutedEventArgs e) => CloseRenameTab(RenameTabBox.Text);

    private void RenameTabCancel_Click(object sender, RoutedEventArgs e) => CloseRenameTab(null);

    private void RenameTabReset_Click(object sender, RoutedEventArgs e) => CloseRenameTab(renamingTab?.DefaultName);

    /// <summary>Enter saves, Esc cancels.</summary>
    private void RenameTabBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Return) { e.Handled = true; CloseRenameTab(RenameTabBox.Text); }
        else if (e.Key == Key.Escape) { e.Handled = true; CloseRenameTab(null); }
    }

    /// <summary>The row an MaTabItem lays its status light, header and trailing text out in (its template's Bd border's child).</summary>
    private static Panel? TabRow(TabItem tab) => (tab.Template?.FindName("Bd", tab) as Border)?.Child as Panel;

    /// <summary>"Close CLI?" (M.ORCH-T1437): the library's message box, its OK relabelled Close, Cancel the default (Enter, Esc).</summary>
    private bool ConfirmCloseTerminal(TerminalPaneViewModel pane)
    {
        var box = new MindAttic.Components.Wpf.Controls.MaMessageBox(TerminalsViewModel.CloseQuestion(pane), "Close CLI?", MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel)
        {
            Owner = this,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        if (box.Buttons.FirstOrDefault(b => b.Tag is MessageBoxResult.OK) is { } close)
        {
            close.Content = "_Close";
            AutomationProperties.SetName(close, "Close");
        }
        box.ShowDialog();
        return box.Result == MessageBoxResult.OK;
    }

    /// <summary>MaTabItem's header presenter (its template's HeaderHost), which SizeTerminalTabs stretches (M.ORCH-T1437).</summary>
    private static ContentPresenter? TabHeader(TabItem tab) => tab.Template?.FindName("HeaderHost", tab) as ContentPresenter;

    /// <summary>Its content's natural width: each part of its row is measured with unbounded width, so this never depends on
    /// the Width already set on it. The stretched header counts its own parts instead (M.ORCH-T1437): the close button, and
    /// the horizontal row whose children a StackPanel always measures unbounded.</summary>
    private static double NaturalWidth(TabItem tab)
    {
        if (TabRow(tab) is not { } row) return 0;
        var header = TabHeader(tab);
        return tab.Padding.Left + tab.Padding.Right + row.Children.OfType<UIElement>().Where(c => c.Visibility != Visibility.Collapsed)
            .Sum(c => ReferenceEquals(c, header) && HeaderNaturalWidth(header!) is { } own ? own : c.DesiredSize.Width);
    }

    /// <summary>A CLI tab header's natural width (M.ORCH-T1437): its docked close button plus every visible part of its row.</summary>
    private static double? HeaderNaturalWidth(ContentPresenter header)
    {
        if (VisualTreeHelper.GetChildrenCount(header) == 0) return null;
        // The template's root is a Grid holding the Usage header and this DockPanel (M.ORCH-T1555). Not finding it, the header's
        // own DesiredSize (the Width set last pass) counted as its natural width, and every pass widened the tabs (M.ORCH-T1569).
        var root = VisualTreeHelper.GetChild(header, 0);
        if ((root as DockPanel ?? (root as Panel)?.Children.OfType<DockPanel>().FirstOrDefault()) is not { } dock) return null;
        return dock.Children.OfType<FrameworkElement>().Where(c => c.Visibility != Visibility.Collapsed).Sum(c => c is StackPanel parts
            ? parts.Margin.Left + parts.Margin.Right + parts.Children.OfType<UIElement>().Where(p => p.Visibility != Visibility.Collapsed).Sum(p => p.DesiredSize.Width)
            : c.DesiredSize.Width);
    }

    /// <summary>Every CLI tab the same width (M.ORCH-T1250): each no longer just sizes to its own content (as the no-wrap
    /// scroller comment on <see cref="TerminalTabScroller_ScrollChanged"/> used to assume), but to the widest one's plus
    /// <see cref="TabSidePadding"/> a side, plus <see cref="CliTabWidthBoost"/>.</summary>
    private void SizeTerminalTabs()
    {
        // The CLI and Launcher tabs only: the pinned Usage tab keeps its own width.
        var tabs = TerminalTabs.Items.OfType<TerminalPaneViewModel>()
            .Select(item => TerminalTabs.ItemContainerGenerator.ContainerFromItem(item) as TabItem)
            .Where(t => t is not null).Cast<TabItem>().ToList();
        if (tabs.Count == 0) return;
        var width = Math.Ceiling(tabs.Max(NaturalWidth) + 2 * TabSidePadding + CliTabWidthBoost);
        foreach (var t in tabs)
        {
            if (double.IsNaN(t.Width) || Math.Abs(t.Width - width) > 0.5) t.Width = width;
            // The header fills what its row's other parts leave, so the close X sits at the tab's right edge (M.ORCH-T1437).
            if (TabHeader(t) is not { } header || TabRow(t) is not { } row) continue;
            var others = row.Children.OfType<UIElement>().Where(c => !ReferenceEquals(c, header) && c.Visibility != Visibility.Collapsed).Sum(c => c.DesiredSize.Width);
            var fill = Math.Max(0, width - t.Padding.Left - t.Padding.Right - others);
            if (double.IsNaN(header.Width) || Math.Abs(header.Width - fill) > 0.5) header.Width = fill;
        }
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

    /// <summary>Scrolls <see cref="TerminalTabScroller"/> to the shown tab's current selection (a real pane, or the pinned
    /// Usage tab, M.ORCH-T1555) once the strip has laid it out, the same way <see cref="BringSelectedTabIntoView"/> does for
    /// the login tabs; choosing Usage also scrolls <see cref="AgentsBody"/> back to its top, so its charts are there without
    /// scrolling past where the terminal normally sits (replaces the old UsageTabItem click handler's own ScrollToTop, now
    /// that choosing Usage flows through SelectedStripItem like any other tab). Never throws.</summary>
    private void BringSelectedTerminalTabIntoView() =>
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                if (Shown.Terminals.IsUsageShown) AgentsBody.ScrollToTop();
                if (TerminalTabs.ItemContainerGenerator.ContainerFromItem(Shown.Terminals.SelectedStripItem) is FrameworkElement tab) tab.BringIntoView();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Error(ex, "Scrolling to the selected CLI tab failed; contained"); }
        }, DispatcherPriority.Loaded);

    private void OnTerminalPanesChanged(object? sender, NotifyCollectionChangedEventArgs e) => BringSelectedTerminalTabIntoView();

    /// <summary>One CLI tab's width, what the strip's arrows scroll by: the first real pane's (index 1, past the pinned
    /// Usage tab at index 0, M.ORCH-T1555), which is narrower and would undersize the step if used instead.</summary>
    private double TerminalTabScrollStep =>
        TerminalTabs.Items.Count > 1 && TerminalTabs.ItemContainerGenerator.ContainerFromIndex(1) is FrameworkElement { ActualWidth: > 0 } first
            ? first.ActualWidth : 200;

    private void TerminalTabScrollLeft_Click(object sender, RoutedEventArgs e) =>
        TerminalTabScroller.ScrollToHorizontalOffset(TerminalTabScroller.HorizontalOffset - TerminalTabScrollStep);

    private void TerminalTabScrollRight_Click(object sender, RoutedEventArgs e) =>
        TerminalTabScroller.ScrollToHorizontalOffset(TerminalTabScroller.HorizontalOffset + TerminalTabScrollStep);

    /// <summary>A completed drag on the shared strip (M.ORCH-T1526, TerminalTabs.AllowReorder, M.ORCH-T1555 added the
    /// pinned Usage tab to the same control): applies and persists the move via <see cref="PersonTerminalsViewModel.Reorder"/>.
    /// The Usage tab is locked (MaTabItem.IsLocked, see its ItemContainerStyle) so it's never the moved item and nothing can
    /// land before it, which is why <see cref="TabReorderEventArgs.NewIndex"/> - counted over the whole strip including that
    /// locked slot 0 - is always at least 1; subtracting 1 gives the index <see cref="PersonTerminalsViewModel.Reorder"/>
    /// expects, relative to Panes alone. Silently ignores a moved item that isn't a real pane (shouldn't happen, but a stray
    /// event is a no-op here, not a crash).</summary>
    private void TerminalTabs_Reordered(object sender, TabReorderEventArgs e)
    {
        if (e.MovedItem is TerminalPaneViewModel pane) Shown.Terminals.Reorder(pane, e.NewIndex - 1);
    }

    /// <summary>A CLI tab's right-click menu (M.ORCH-T883; Shift+F10 too): "Move to" each other signed-in login, read when it's
    /// opened, moving that CLI there. Not on the Launcher's own menu tab; disabled until the CLI's host is known.</summary>
    private async void OnTerminalTabContextMenu(object sender, ContextMenuEventArgs e)
    {
        try
        {
            if (e.OriginalSource is not DependencyObject source
                || ItemsControl.ContainerFromElement(TerminalTabs, source) is not FrameworkElement { DataContext: TerminalPaneViewModel pane } container) return;
            e.Handled = true;
            await ShowTerminalMenuAsync(container, pane, e.CursorLeft < 0 ? PlacementMode.Bottom : PlacementMode.MousePoint);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Error(ex, "Building the Move to menu failed; contained"); }
    }

    /// <summary>The phone layout's menu button (M.ORCH-T1570): shows or hides the sidebar (orgs, people, their tickets) as a
    /// drawer. Choosing a person closes it again.</summary>
    private void DrawerButton_Click(object sender, RoutedEventArgs e) =>
        Sidebar.Visibility = Sidebar.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>A CLI tab's right-click menu at <paramref name="container"/>.</summary>
    private async Task ShowTerminalMenuAsync(FrameworkElement container, TerminalPaneViewModel pane, PlacementMode placement)
    {
        {
            var undocked = await Task.Run(TerminalsViewModel.UndockedSessions);
            if (pane.Request.IsMenu)
            {
                // The Launcher's tab has no CLI of its own; it's where an undocked CLI is docked back from (M.ORCH-T1448).
                if (undocked.Count == 0) return;
                var dockMenu = DropdownMenu(container, "Dock a CLI", placement);
                AddDockItems(dockMenu, undocked);
                dockMenu.IsOpen = true;
                return;
            }
            var tab = Shown;
            var agent = pane.HostPid is { } host ? tab.AgentOfHost(host) : null;
            var menu = DropdownMenu(container, $"Move the {pane.Name} CLI to", placement);
            // Copy PID (M.ORCH-T1518): the CLI's own pid, which CLIs address each other by (MINDATTIC_CLI_PID, nudge-<pid>-*.txt).
            AddCopyPidItems(menu, agent?.CliPid, pane.HostPid);
            // Rename… (M.ORCH-T1509): a little modal for the tab's own name, kept across reattach, undock/dock and restarts.
            var rename = DropdownItem("Rename…");
            rename.ToolTip = "Give this tab a name of your own (double-clicking its name does too)";
            rename.Click += (_, _) => OpenRenameTab(pane);
            menu.Items.Add(rename);
            if (pane.IsHosted)
            {
                var undock = DropdownItem("Undock");
                undock.ToolTip = "Drop this tab but keep its CLI running headless; Dock (on the Launcher's tab) or /dock brings it back";
                undock.Click += (_, _) =>
                {
                    if (!model.AllTerminals.Undock(pane))
                        OrchestratorLog.For("Dashboard").Warning("Couldn't undock {Title:l}: its tab file couldn't be marked", pane.Title);
                };
                menu.Items.Add(undock);
            }
            AddDockItems(menu, undocked);
            menu.Items.Add(new Separator());
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
    }

    /// <summary>"Dock <title>" for each undocked hosted CLI (M.ORCH-T1448).</summary>
    private void AddDockItems(ContextMenu menu, IReadOnlyList<(TermSessionRecord Session, string Title)> undocked)
    {
        foreach (var (session, title) in undocked)
        {
            var item = DropdownItem($"Dock {title}");
            item.ToolTip = "Give this undocked CLI its tab back; it was never stopped";
            item.Click += (_, _) =>
            {
                if (model.AllTerminals.Dock(session) is { } docked) model.AllTerminals.SelectedPane = docked;
            };
            menu.Items.Add(item);
        }
    }

    /// <summary>"Copy PID n" (M.ORCH-T1518): the CLI's pid, copied as just the number; disabled until the CLI's first turn makes it known.
    /// Then "Copy host PID n" when the tab's TermHost is known, which helps meanwhile.</summary>
    private static void AddCopyPidItems(ContextMenu menu, int? cliPid, int? hostPid)
    {
        var cli = DropdownItem(cliPid is { } pid ? $"Copy PID {pid}" : "Copy PID");
        if (cliPid is { } known)
        {
            cli.ToolTip = "This CLI's process id, the one other CLIs address it by (MINDATTIC_CLI_PID, nudge-<pid>-*.txt): click to copy it";
            cli.Click += (_, _) => CopyPid(known);
        }
        else
        {
            cli.IsEnabled = false;
            cli.ToolTip = "This CLI's PID isn't known yet: it shows up once its first turn starts";
            ToolTipService.SetShowOnDisabled(cli, true);
        }
        menu.Items.Add(cli);
        if (hostPid is not { } host) return;
        var hostItem = DropdownItem($"Copy host PID {host}");
        hostItem.ToolTip = "The TermHost process that hosts this CLI (not the CLI itself): click to copy it";
        hostItem.Click += (_, _) => CopyPid(host);
        menu.Items.Add(hostItem);
    }

    private static void CopyPid(int pid)
    {
        try { Clipboard.SetDataObject(pid.ToString(System.Globalization.CultureInfo.InvariantCulture), copy: true); }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or ExternalException or InvalidOperationException)
        { OrchestratorLog.For("Dashboard").Warning(ex, "Copying a PID failed (the clipboard is locked by another process); contained"); }
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

    /// <summary>A person's tab's right-click menu (M.ORCH-T797; Shift+F10 or the menu key on a focused tab too): Run Launcher
    /// (M.ORCH-T1525, an escape hatch for when a login is left with no tabs and the automatic one didn't fire) above User Settings
    /// (M.ORCH-T1098, Rename and Org limit merged), the login actions that were standalone buttons on the person's page. The tab is
    /// selected first, since each dialog or action is the shown tab's.</summary>
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
        Add("Run Launcher", "Opens this login's Launcher by hand (brings an already-open one to front): for when a login is left with no tabs and the automatic Launcher didn't open",
            tab.RunLauncherCommand);
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

    private void OnEscape()
    {
        var shown = Shown;
        if (shown.IsLoginsOpen) shown.IsLoginsOpen = false;
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
