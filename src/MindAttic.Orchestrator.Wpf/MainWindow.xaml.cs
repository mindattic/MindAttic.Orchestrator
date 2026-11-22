using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
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
    // Each a Vault setting, live-dialed (M.ORCH-T1595, DashboardTimings); the comments give their defaults.

    /// <summary>Shown and polling: 5s by default.</summary>
    private static TimeSpan PollInterval => DashboardTimings.Poll;

    /// <summary>Minimized while the daemon builds the snapshots: nobody is looking, so just enough to stay roughly current (60s).</summary>
    private static TimeSpan HiddenInterval => DashboardTimings.HiddenPoll;

    /// <summary>Hidden but holding the lease (no daemon running): the daemon's own pace, since rl-status.json and allocate depend on it (15s).</summary>
    private static TimeSpan HiddenHolderInterval => DashboardTimings.HiddenHolderPoll;

    /// <summary>How often the hosted-session registry is read again for a host whose bus announcement was lost (M.ORCH-T1001; 45s).</summary>
    private static TimeSpan TermResyncInterval => DashboardTimings.TermResync;

    /// <summary>How often every login's busy/subagent-sum figures are re-raised, independent of the poll/push refresh
    /// (M.ORCH-T1838; 5s).</summary>
    private static TimeSpan BusyCountRefreshInterval => DashboardTimings.BusyCountRefresh;

    // The hosted-session resync timer (M.ORCH-T1001), on only while the status stream is down (M.ORCH-T1998); null until loaded.
    private DispatcherTimer? termResync;

    // The busy/subagent-sum figures' own tick (M.ORCH-T1838), always running; null until loaded.
    private DispatcherTimer? busyCountTimer;

    /// <summary>The global login's tab, which owns the refresh and every login's tab (M.ORCH-T613).</summary>
    private readonly MainViewModel model = new();
    private readonly DispatcherTimer timer;

    /// <summary>The login's tab on screen: the window shows its view model.</summary>
    private MainViewModel Shown => DataContext as MainViewModel ?? model;

    /// <summary>Creates the main window.</summary>
    /// <summary>Whether the window's controls are exposed to UI Automation clients (the setting UiAutomation), as of start-up.</summary>
    private readonly bool uiAutomation = new MindAttic.Orchestrator.Settings.JsonStore().TryLoadSettings(out var atStart) && atStart.UiAutomation;

    /// <summary>A window peer with no children unless the setting says otherwise (M.ORCH-T1738): with a UI Automation client
    /// listening, WPF rebuilt the whole window's peer tree after every layout pass (AutomationPeer.UpdateSubtree, 55 of 60
    /// seconds of layout in a trace), which is what froze the window when a CLI tab was shown.</summary>
    protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() =>
        uiAutomation ? base.OnCreateAutomationPeer() : new QuietWindowPeer(this);

    private sealed class QuietWindowPeer(Window owner) : System.Windows.Automation.Peers.WindowAutomationPeer(owner)
    {
        protected override List<System.Windows.Automation.Peers.AutomationPeer>? GetChildrenCore() => null;
    }

    /// <summary>The window's title (M.ORCH-T1888): the app's name and its whole-number version, the build's major
    /// (the csproj's OrchestratorVersionFromGit: one more for every commit that touches src\), so the deployed version shows.</summary>
    internal static string VersionedTitle(Version? version) =>
        version is { Major: > 0 } v ? $"MindAttic Orchestrator v{v.Major}" : "MindAttic Orchestrator";

    public MainWindow()
    {
        InitializeComponent();
        Title = VersionedTitle(typeof(MainWindow).Assembly.GetName().Version);
        DataContext = model;
        // One dashboard tab per Claude login (M.ORCH-T613): the whole window shows the chosen login's tab, while the tab strip
        // keeps the global login's, which lists them all.
        LoginTabStrip.DataContext = model;
        // The tree sidebar (M.ORCH-T1759, TreeMenuLayout's default-on view) is scoped the same way: the global login's tab,
        // which lists every login.
        LoginTree.DataContext = model;
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
        // HostStatsBars (M.ORCH-T1875) sits in the menu bar's left column, a sibling of MenuBarTools rather than inside
        // it, so it needs this same assignment on its own (M.ORCH-T1880): without it, its C:/RAM bars and text bound
        // whatever ambient DataContext fell through instead of the global login's, showing no fill or percent.
        HostStatsBars.DataContext = model;
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
        // The tree sidebar grows with its widest row instead of clipping it (M.ORCH-T1768, SizeSidebarToContent): a badge
        // or a longer name appearing has no SizeChanged of its own to catch (LoginTreeScroller's own
        // HorizontalScrollBarVisibility="Disabled" squeezes LoginTree to whatever width it's already given rather than
        // reporting its content's true desired width upward), so this re-measures on every layout pass instead.
        LoginTree.LayoutUpdated += (_, _) => SizeSidebarToContent();
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
        // Closing a CLI tab by hand asks first (M.ORCH-T1437), in the Close CLI modal (M.ORCH-T1442), closed by its own X,
        // backdrop or Esc as a cancel.
        model.AllTerminals.ConfirmClose = ConfirmCloseTerminal;
        CloseCliModal.Closed += (_, _) => { if (closingTab is not null) CloseCliModalFor(false); };
        // The Rename tab modal closed by its own X or backdrop is a cancel (M.ORCH-T1509).
        RenameTabModal.Closed += (_, _) => { if (renamingTab is not null) CloseRenameTab(null); };
        // Only the shown login's selected pane is ever visible (TerminalPaneViewModel.IsSelected); clipping walks just that
        // one each layout pass rather than every CLI ever opened across every login (M.ORCH-T1756).
        TerminalClip.Attach(TerminalPanesHost, () => model.AllTerminals.SelectedPane?.Control);
        TerminalKeyboard.Attach(this, TerminalPanesHost);
        TerminalMouse.Attach(this, TerminalPanesHost);
        // Ctrl+drag a CLI/Launcher tab or a login's own header onto another login's row to switch to it (M.ORCH-T1757),
        // the tree sidebar's own rows too (M.ORCH-T1768 note 6: TreeMenuLayout, on by default, had no equivalent at all).
        LoginDragDrop.Attach(this, LoginTabStrip, TerminalTabs, LoginTree);
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

        // Names whatever keeps typing waiting on this thread, for good (M.ORCH-T1733).
        var stalls = new MindAttic.Orchestrator.Term.Wpf.UiStallProbe(Dispatcher, () => DashboardTimings.UiStallLog);
        Closed += (_, _) => stalls.Dispose();

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
            // never gave its host a tab, since the registry was read only here. Only while the stream is down (M.ORCH-T1998): the
            // daemon keeps each live session's announcement and replays them on every (re)connection, which resyncs once too.
            if (restoreOnLoad)
            {
                var resync = new DispatcherTimer(DispatcherPriority.Background) { Interval = TermResyncInterval };
                resync.Tick += async (_, _) =>
                {
                    await model.AllTerminals.ResyncAsync();
                    if (resync.Interval != TermResyncInterval) resync.Interval = TermResyncInterval; // dialed live in the Vault
                };
                termResync = resync;
                resync.IsEnabled = !model.IsStreamLive;
                Closed += (_, _) => { resync.Stop(); termResync = null; };
            }
            // Every login's busy/subagent-sum figures, re-raised on their own tick (M.ORCH-T1838), a Vault setting live-dialed
            // through DashboardTimings like the others here: always running, whether the stream pushes or this window polls,
            // so those figures never wait on either for a refresh they don't otherwise need.
            var busyCounts = new DispatcherTimer(DispatcherPriority.Background) { Interval = BusyCountRefreshInterval };
            busyCounts.Tick += (_, _) =>
            {
                foreach (var t in model.LoginTabs) t.RaiseBusyCounts();
                if (busyCounts.Interval != BusyCountRefreshInterval) busyCounts.Interval = BusyCountRefreshInterval; // dialed live in the Vault
            };
            busyCountTimer = busyCounts;
            busyCounts.Start();
            Closed += (_, _) => { busyCounts.Stop(); busyCountTimer = null; };
            var restored = restoreOnLoad && SessionRestore.Offer(this, model);
            await model.RefreshAsync();
            // Each person back on the CLI tab they last had chosen (M.ORCH-T1436), kept by TermHost session in the restore file.
            if (restoreOnLoad) model.ReselectRemembered(SessionRestore.Previous);
            if (restored) model.ReselectRestored();
            model.RestoringTerminals = false;
            polling = true;
            Retime();
        };
        // Closing, no tab gone spawns a Launcher (M.ORCH-T1150): set in OnClosing, only once the close isn't cancelled.
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

    /// <summary>The Save button's right-click menu (M.ORCH-T1769, replacing Save all &amp; close's M.ORCH-T1724
    /// force-quicksave-then-close-and-exit, now retired): Quick Save All and Quick Load All, the library's dark dropdown
    /// like every other menu in this window.</summary>
    private void OnSaveButtonContextMenu(object sender, ContextMenuEventArgs e)
    {
        if (sender is not Button button) return;
        e.Handled = true;
        ShowSaveMenu(button, PlacementMode.Bottom);
    }

    /// <summary>The Save button's own "more" chevron (M.ORCH-T1793, SaveMoreButton): the same Quick Save All / Quick Load
    /// All menu as the right-click above, opened with a left click, since a right-click (or Shift+F10 on a button with no
    /// keyboard equivalent at all) isn't reachable on touch or in the phone layout.</summary>
    private void SaveMoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not UIElement target) return;
        e.Handled = true;
        ShowSaveMenu(target, PlacementMode.Bottom);
    }

    private void ShowSaveMenu(UIElement target, PlacementMode placement)
    {
        var menu = DropdownMenu(target, "Quick save or load every open CLI", placement);
        var saveAll = DropdownItem("", "Quick Save All");
        saveAll.ToolTip = "Save everything as one save state (confirmed first): /allocate off everywhere so nothing new starts, every open CLI quicksaved, every tab, session and /allocate kept";
        saveAll.Click += async (_, _) => await RunQuickAllAsync(isSave: true);
        menu.Items.Add(saveAll);
        var loadAll = DropdownItem("", "Quick Load All");
        loadAll.ToolTip = "Restore the last save state (or the newer autosave) (confirmed first): every closed tab reopened and resumed, each CLI's handoff loaded, /allocate back on where it was";
        loadAll.Click += async (_, _) => await RunQuickAllAsync(isSave: false);
        menu.Items.Add(loadAll);
        menu.IsOpen = true;
    }

    /// <summary>Confirms Quick Save All or Quick Load All (<see cref="MaMessageBox"/>, a themed Yes/No defaulting to No,
    /// the same pattern the API key billing switch already confirms with, M.ORCH-T773), then runs it
    /// (<see cref="MainViewModel.QuickSaveAllAsync"/>/<see cref="MainViewModel.QuickLoadAllAsync"/>) and reports how long
    /// the confirmed operation actually took once it's done.</summary>
    private async Task RunQuickAllAsync(bool isSave)
    {
        var verb = isSave ? "quick save" : "quick load";
        var noun = isSave ? "Quick Save All" : "Quick Load All";
        var question = isSave
            ? "Save everything as one save state? /allocate goes off in every CLI (running subagents finish, nothing new starts), every open CLI quicksaves, and every tab is kept."
            : "Restore the last save state? Closed tabs reopen and resume, each CLI loads its handoff, and /allocate comes back on where it was.";
        if (MaMessageBox.Show(this, question, $"{noun}?",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        var (elapsed, acted, total, note) = isSave ? await model.QuickSaveAllAsync() : await model.QuickLoadAllAsync();
        var report = (total == 0 ? $"No CLI tabs to {verb}."
            : isSave ? $"{acted} of {total} quicksave{(total == 1 ? "" : "s")} captured in {elapsed.TotalSeconds:0.0}s."
            : $"{acted} of {total} CLI{(total == 1 ? "" : "s")} resumed in {elapsed.TotalSeconds:0.0}s.")
            + (note is { Length: > 0 } ? "\n\n" + note : "");
        MaMessageBox.Show(this, report, noun, MessageBoxButton.OK, MessageBoxImage.Information);
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
    private bool freezeOnClose;

    /// <summary>
    /// A real close only (M.ORCH-T1970): every Closing handler runs first, the tray's close-to-tray among them (attached after
    /// this window's own, so a handler here couldn't see its cancel), and only a close nobody cancelled marks the dashboard
    /// shutting down and freezes its session-restore state; hidden to the tray, it keeps both live.
    /// </summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel) return;
        model.ShuttingDown = true;
        if (freezeOnClose) SessionRestore.Freeze();
    }

    internal void HostTerminals()
    {
        // Its tabs are written to the session-restore file as they change, and offered back after a crash (M.ORCH-T862).
        restoreOnLoad = true;
        SessionRestore.Track(model);
        // Back where it was last time, maximized or not, before it's shown (M.ORCH-T1510).
        SessionRestore.TrackWindow(this);
        // Kept as they stand when the window starts closing: the tabs' teardown after that isn't what was open (OnClosing).
        freezeOnClose = true;
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
            .On<Status.ShowWorkerRequest>(Status.AppBus.ShowWorker, (_, request) => OnShowWorker(request), Status.AppBus.Version)
            // A CLI's badge state changed (M.ORCH-T1997): its pane's badge follows at once, not at the next status read.
            .On<Status.AgentStateNotice>(Status.AppBus.AgentState, (_, notice) => OnAgentState(notice), Status.AppBus.Version)
            // A probe run's health report (M.ORCH-T2033): its results become the health dashboard's per-check rows and history.
            // Read as raw JSON here, since health+json groups results under "checks"; HealthReport.FromJson reads that shape.
            .On<System.Text.Json.Nodes.JsonObject>(Status.AppBus.Health, (e, _) => model.OnHealthEnvelope(e), Status.AppBus.HealthVersion);
        // Hosted CLIs (M.ORCH-T926): one started elsewhere (the Launcher, M.LNCH-T82) gets a tab, one that ended is marked ended.
        // /launcher (M.ORCH-T1487) rides the same message: cycle that login's Launcher tabs, or open one.
        bus.On<Term.TermSessionEvent>(Term.TermBus.Kind, (_, session) =>
        {
            if (session.Event == Term.TermSessionEvents.Launcher) model.CycleLauncher(session.Login);
            else model.AllTerminals.OnTermSession(session);
        }, Term.TermBus.Version);
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
                    // The hosted-session resync runs only while the stream is down, and once on each (re)connection (M.ORCH-T1998).
                    if (termResync is { } resync && resync.IsEnabled == live)
                    {
                        resync.IsEnabled = !live;
                        if (live) _ = model.AllTerminals.ResyncAsync();
                    }
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
            // The font chosen there too (M.ORCH-T1274), swapped in live the same way.
            if (payload is Status.SettingsPayload) FontChoice.Follow(Application.Current?.Resources, model.Store);
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

    /// <summary>A CLI's status change pushed off the stream (M.ORCH-T1997), on the UI thread: that session is read again and its
    /// pane's badge set from it at once (<see cref="ViewModels.TerminalsViewModel.ApplyAgentStateAsync"/>).</summary>
    private async void OnAgentState(Status.AgentStateNotice notice)
    {
        try { await model.AllTerminals.ApplyAgentStateAsync(notice); }
        // async void on the dispatcher: an escaped exception would close the dashboard.
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            OrchestratorLog.For("Dashboard").Error(ex, "Applying a pushed CLI status for {Session:l} failed", notice.Session);
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
    /// Logins, User Settings, a Task List row's details (M.ORCH-T1622), the Inbox (M.ORCH-T1689) or Plans (M.ORCH-T1826) is open, the loader covers the window or an org with no
    /// login covers the page.</summary>
    private void UpdateTerminalsCover()
    {
        var shown = Shown;
        var covered = shown.IsLoginsOpen || shown.IsUserSettingsOpen || shown.IsTaskDetailOpen || shown.IsInboxOpen || shown.IsPlansOpen || RenameTabModal.IsOpen || CloseCliModal.IsOpen || model.IsLoading || model.IsOrgEmpty;
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
            if (!IsActive || shown.IsLoginsOpen || shown.IsUserSettingsOpen || shown.IsTaskDetailOpen || shown.IsInboxOpen || shown.IsPlansOpen || RenameTabModal.IsOpen || CloseCliModal.IsOpen
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
        if (e.PropertyName is nameof(MainViewModel.IsLoginsOpen) or nameof(MainViewModel.IsUserSettingsOpen) or nameof(MainViewModel.IsTaskDetailOpen) or nameof(MainViewModel.IsInboxOpen) or nameof(MainViewModel.IsPlansOpen))
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

    /// <summary>The tree sidebar's floor (M.ORCH-T1431): <see cref="Sidebar"/>'s own XAML <c>MinWidth</c>, mirrored here so
    /// <see cref="SizeSidebarToContent"/> never sets it any narrower, only wider.</summary>
    private const double SidebarMinWidth = 170;

    /// <summary>What <see cref="SizeSidebarToContent"/> adds to the tree's own widest row: <see cref="Sidebar"/>'s left and
    /// right Padding (12 a side) plus its right BorderThickness (1) plus a few px of slack for measurement rounding.</summary>
    private const double SidebarContentPadding = 36;

    /// <summary>Grows <see cref="Sidebar"/> to fit the tree's widest row (M.ORCH-T1768), so a status badge or a longer name
    /// never overlaps it: <see cref="LoginTreeScroller"/>'s own <c>HorizontalScrollBarVisibility="Disabled"</c> (so nothing
    /// ever needs to scroll sideways under a name) squeezes <see cref="LoginTree"/> to whatever width the sidebar already
    /// has instead of reporting its rows' true desired width upward the way an unconstrained panel would, so a row that
    /// needs more room than the sidebar's current width used to just overlap instead of growing it. Measuring LoginTree
    /// with infinite available width and applying that, plus <see cref="SidebarContentPadding"/>, as <see cref="Sidebar"/>'s
    /// own MinWidth (never under <see cref="SidebarMinWidth"/>) fixes that; a no-op while the flat strip shows instead
    /// (<see cref="MainViewModel.TabStatus.ShowsLoginTree"/> false), which sizes its own column differently. Never throws.</summary>
    private void SizeSidebarToContent()
    {
        try
        {
            if (!model.ShowsLoginTree) return;
            LoginTree.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var want = Math.Ceiling(LoginTree.DesiredSize.Width) + SidebarContentPadding;
            var min = Math.Max(SidebarMinWidth, want);
            if (double.IsNaN(Sidebar.MinWidth) || Math.Abs(Sidebar.MinWidth - min) > 0.5) Sidebar.MinWidth = min;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Error(ex, "Sizing the sidebar to its tree failed; contained"); }
    }

    /// <summary>Redirects a wheel tick anywhere over the window into <see cref="AgentsBody"/>'s own scroll (M.ORCH-T1299),
    /// except one already inside it (left alone so its native scrolling runs unchanged), one inside <see cref="Sidebar"/>
    /// (its own LoginTreeScroller/SidebarTicketScroller scroll themselves, M.ORCH-T1871: over <see cref="AgentsBody"/>'s
    /// own column), or one while <see cref="LoginsModal"/> is open (it scrolls itself) or <see cref="UserSettingsModal"/>
    /// is (the page behind a modal's dimmed cover stays put). PreviewMouseWheel tunnels from the window
    /// down to the actual element before MouseWheel bubbles back up, so this fires ahead of any inner ScrollViewer along the
    /// way and can mark the tick handled before one swallows it.</summary>
    private void MainWindow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || !AgentsBody.IsVisible || LoginsModal.IsOpen || UserSettingsModal.IsOpen) return;
        if (e.OriginalSource is DependencyObject source && (IsWithin(source, AgentsBody) || IsWithin(source, Sidebar))) return;
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
            var parts = row.Children.OfType<FrameworkElement>().Concat(header).ToList();
            foreach (var part in parts)
                if (watchedTabParts.Add(part)) part.SizeChanged += OnTabPartSizeChanged;
            // Released when the tab goes (closed, or another login's strip shown): the set held every header ever built, and
            // through them their closed panes, for the life of the window. Registered on every load, not only one that found
            // new parts to watch (bughunt): a reused container whose parts a prior load already added here still needs its
            // own cleanup hook, since Remove/-= on a part another load's own Release already dropped is a safe no-op, but a
            // reused container that never gets one of its own could otherwise hold those parts, and their closed pane, for
            // the life of the window if that prior load's content never itself unloads to run its Release.
            void Release(object? s, RoutedEventArgs a)
            {
                content.Unloaded -= Release;
                foreach (var part in parts)
                {
                    part.SizeChanged -= OnTabPartSizeChanged;
                    watchedTabParts.Remove(part);
                }
            }
            content.Unloaded += Release;
        }
        SizeTerminalTabs();
    }

    private void OnTabPartSizeChanged(object sender, SizeChangedEventArgs e) => SizeTerminalTabs();

    /// <summary>The header a plain (non-Ctrl) left press landed on and where, until its release decides whether it was a
    /// click (<see cref="LoginTreeHeader_MouseLeftButtonUp"/>); null between presses.</summary>
    private FrameworkElement? treeHeaderPressed;
    private Point treeHeaderPressedAt;

    /// <summary>The tree sidebar's header row press (M.ORCH-T1877): only remembered here; the toggle itself waits for the
    /// release, so a drag is never mistaken for a click. A Ctrl press never reaches this at all (<see cref="LoginDragDrop"/>'s
    /// window-level tunnel marks it handled for its own quick-switch drag), and neither does a press on a button inside
    /// the header (<c>LoginTreeMoreButton</c>, which handles its own press) or on the chevron (which marks its own handled).</summary>
    private void LoginTreeHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement header || IsInsideButton(e.OriginalSource as DependencyObject, header)) return;
        treeHeaderPressed = header;
        treeHeaderPressedAt = e.GetPosition(this);
    }

    /// <summary>The tree sidebar's header row click (M.ORCH-T1877, M.ORCH-T1962): fired on release only when the press began
    /// on this same header and the pointer hasn't moved past the system drag threshold since. Selects that login (showing its
    /// Usage) and never expands or collapses it: only the chevron does that.</summary>
    private void LoginTreeHeader_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var pressed = treeHeaderPressed;
        treeHeaderPressed = null;
        if (!ReferenceEquals(pressed, sender) || sender is not FrameworkElement { DataContext: MainViewModel vm }) return;
        if (IsInsideButton(e.OriginalSource as DependencyObject, (FrameworkElement)sender)) return;
        var now = e.GetPosition(this);
        if (Math.Abs(now.X - treeHeaderPressedAt.X) >= SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(now.Y - treeHeaderPressedAt.Y) >= SystemParameters.MinimumVerticalDragDistance) return;
        e.Handled = true;
        // Only the chevron expands or collapses (M.ORCH-T1962): the header click selects the login (showing its Usage) and
        // leaves its node exactly as open or closed as it was, though selecting otherwise opens it.
        var expanded = vm.IsExpanded;
        vm.SelectLoginCommand.Execute(null);
        vm.IsExpanded = expanded;
    }

    /// <summary>Whether <paramref name="source"/> sits inside a button (close X, '...' menu, ...) below
    /// <paramref name="row"/>: such a press is the button's own, never the row's click or a row drag.</summary>
    private static bool IsInsideButton(DependencyObject? source, DependencyObject row)
    {
        for (var node = source; node is not null && !ReferenceEquals(node, row);
             node = node is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
            if (node is System.Windows.Controls.Primitives.ButtonBase) return true;
        return false;
    }

    /// <summary>The tree sidebar's chevron click (M.ORCH-T1759): toggles this login's node alone, never its selection, so
    /// another login's panes can be peeked at without leaving the one shown. Marks the event handled so it doesn't also
    /// reach <see cref="LoginTreeHeader_MouseLeftButtonDown"/> on its way up.</summary>
    private void LoginTreeChevron_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        treeHeaderPressed = null;
        if (sender is FrameworkElement { DataContext: MainViewModel vm }) vm.IsExpanded = !vm.IsExpanded;
    }

    /// <summary>The tree sidebar's child row click (M.ORCH-T1759): selects this item's login, then shows the clicked pane,
    /// exactly as choosing it from the flat CLI tab bar would. There's no pinned Usage marker among these children any
    /// more (M.ORCH-T1768): the tree binds this list to <see cref="PersonTerminalsViewModel.Panes"/>, not
    /// <see cref="PersonTerminalsViewModel.StripItems"/>, since the login's own header row now shows Usage on a click.</summary>
    private void LoginTreeChild_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: { } item } fe) return;
        if (FindAncestorDataContext<MainViewModel>(VisualTreeHelper.GetParent(fe)) is not { } vm) return;
        vm.SelectTreeChildCommand.Execute(item);
    }

    /// <summary>The tree sidebar's CLI/Launcher pane row right-click menu (M.ORCH-T1768 note 5): the exact same menu a CLI
    /// node gets as the flat CLI tab strip's own row (<see cref="OnTerminalTabContextMenu"/>/<see cref="ShowTerminalMenuAsync"/>),
    /// since the ticket's consistency ask is per CLI node, tree or tab. The owning
    /// login is found the same way its own click handler above does (<see cref="FindAncestorDataContext{T}"/>), rather than
    /// assuming the currently-shown tab (<see cref="Shown"/>): the tree can have another login's panes expanded at once, and
    /// the right-clicked pane's status message/toast must land on its own login, not whichever tab happens to be selected.</summary>
    private async void LoginTreeChild_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        try
        {
            if (sender is not FrameworkElement { DataContext: TerminalPaneViewModel pane } container) return;
            if (FindAncestorDataContext<MainViewModel>(VisualTreeHelper.GetParent(container)) is not { } owner) return;
            e.Handled = true;
            await ShowTerminalMenuAsync(container, pane, e.CursorLeft < 0 ? PlacementMode.Bottom : PlacementMode.MousePoint, owner);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Error(ex, "Building the tree's CLI context menu failed; contained"); }
    }

    /// <summary>The tree sidebar's own CLI-pane row drag start (M.ORCH-T1768 note 6) and the pane it started on; null
    /// between drags. A manual <see cref="DragDrop"/> pair in place of a library AllowReorder, since ItemsControl (unlike
    /// the library's MaTabControl the flat CLI tab strip already uses) has none of its own.</summary>
    private Point? treeDragStart;
    private TerminalPaneViewModel? treeDragPane;

    /// <summary>Records the press that might become a reorder drag (<see cref="LoginTreeChildRow_PreviewMouseMove"/>); a
    /// Ctrl-held press is left alone for <see cref="LoginDragDrop"/>'s own quick-switch-login drag to pick up instead
    /// (its window-level PreviewMouseLeftButtonDown runs first in the tunnel and marks it handled when Ctrl is down, so
    /// this one never even sees it then).</summary>
    private void LoginTreeChildRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // The Launcher is locked first (M.ORCH-T1848): never dragged. Nor is a press on the row's own close X or '...'
        // button (M.ORCH-T1877): that press is the button's, not the start of a row drag.
        treeDragPane = sender is FrameworkElement { DataContext: TerminalPaneViewModel { Request.IsMenu: false } pane } row
            && !IsInsideButton(e.OriginalSource as DependencyObject, row) ? pane : null;
        treeDragOwner = treeDragPane is null ? null : FindAncestorDataContext<MainViewModel>(VisualTreeHelper.GetParent((DependencyObject)sender));
        treeDragStart = treeDragPane is null ? null : e.GetPosition(null);
    }

    private MainViewModel? treeDragOwner;

    /// <summary>Past the system drag threshold with the left button still down, starts the actual drag
    /// (<see cref="LoginTreeChildRow_Drop"/> applies it); one drag per press.</summary>
    private void LoginTreeChildRow_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || treeDragStart is not { } start || treeDragPane is not { } pane
            || sender is not FrameworkElement row) return;
        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - start.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(pos.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        treeDragStart = null;
        try { DragDrop.DoDragDrop(row, pane, DragDropEffects.Move); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Error(ex, "Starting a tree CLI reorder drag failed; contained"); }
        finally { treeDragPane = null; treeDragOwner = null; ClearDropLine(); ClearHeaderHighlight(); }
    }

    /// <summary>Whether a drop on <paramref name="row"/> at <paramref name="e"/>'s pointer lands below it (pointer in its
    /// lower half); always below a Launcher row, which nothing may land above (M.ORCH-T1848).</summary>
    private static bool DropsBelow(FrameworkElement row, DragEventArgs e) =>
        row.DataContext is TerminalPaneViewModel { Request.IsMenu: true } || e.GetPosition(row).Y >= row.ActualHeight / 2;

    private void LoginTreeChildRow_DragOver(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (!e.Data.GetDataPresent(typeof(TerminalPaneViewModel)) || sender is not FrameworkElement row
            || FindAncestorDataContext<MainViewModel>(VisualTreeHelper.GetParent(row)) is not { } owner)
        {
            e.Effects = DragDropEffects.None;
            ClearDropLine();
            return;
        }
        e.Effects = DragDropEffects.Move;
        ClearHeaderHighlight();
        ShowDropLine(row, DropsBelow(row, e), owner.TabBrush);
    }

    private void LoginTreeChildRow_DragLeave(object sender, DragEventArgs e)
    {
        if (ReferenceEquals(sender, dropLineRow)) ClearDropLine();
    }

    /// <summary>The 2px insertion line a tree CLI drag shows between rows (M.ORCH-T1849); one at a time. Starts past the
    /// row's own spine column (<see cref="DropLineAdorner.OnRender"/>), not at the row's left edge (M.ORCH-T1894, Ryan:
    /// wants the dragged rows to "still read as connected like a tree" rather than the bar cutting across the spine).</summary>
    private DropLineAdorner? dropLine;
    private FrameworkElement? dropLineRow;

    private void ShowDropLine(FrameworkElement row, bool below, Brush brush)
    {
        if (ReferenceEquals(row, dropLineRow) && dropLine?.Below == below) return;
        ClearDropLine();
        if (AdornerLayer.GetAdornerLayer(row) is not { } layer) return;
        dropLine = new DropLineAdorner(row, below, brush);
        dropLineRow = row;
        layer.Add(dropLine);
    }

    private void ClearDropLine()
    {
        if (dropLine is { } line && dropLineRow is { } row) AdornerLayer.GetAdornerLayer(row)?.Remove(line);
        dropLine = null;
        dropLineRow = null;
    }

    private sealed class DropLineAdorner : Adorner
    {
        private readonly Brush brush;

        public DropLineAdorner(UIElement row, bool below, Brush brush) : base(row)
        {
            Below = below;
            this.brush = brush;
            IsHitTestVisible = false;
        }

        public bool Below { get; }

        /// <summary>Drawn from the row's own spine column onward, not from its left edge (M.ORCH-T1894): the adorned row is
        /// always the tree's <c>LoginTreeChildRow</c> Grid, whose own first column is the spine (the vertical connector line
        /// down from the login, MainWindow.xaml). Starting the bar there leaves that column clear so the spine still reads as
        /// one continuous line through the drag, rather than this bar cutting across it row to row.</summary>
        protected override void OnRender(DrawingContext dc)
        {
            var size = AdornedElement.RenderSize;
            var x = AdornedElement is Grid { ColumnDefinitions.Count: > 0 } grid ? grid.ColumnDefinitions[0].ActualWidth : 0;
            dc.DrawRectangle(brush, null, new System.Windows.Rect(x, Below ? size.Height - 1 : -1, Math.Max(0, size.Width - x), 2));
        }
    }

    /// <summary>A login header a tree CLI drag is over (M.ORCH-T1849): dimmed like LoginDragDrop's own login drags do.</summary>
    private FrameworkElement? headerHighlight;

    private void ClearHeaderHighlight()
    {
        if (headerHighlight is { } h) h.Opacity = 1;
        headerHighlight = null;
    }

    /// <summary>A tree CLI drag over a login's own header, not one of its rows (those handle it themselves): another login
    /// takes it as a transfer (M.ORCH-T1849).</summary>
    private void LoginTree_DragOver(object sender, DragEventArgs e)
    {
        if (e.Handled || e.Data.GetData(typeof(TerminalPaneViewModel)) is not TerminalPaneViewModel) return;
        e.Handled = true;
        ClearDropLine();
        var over = e.OriginalSource is DependencyObject hit && ItemsControl.ContainerFromElement(LoginTree, hit) is FrameworkElement { DataContext: MainViewModel target } row
            && !ReferenceEquals(target, treeDragOwner) ? row : null;
        e.Effects = over is null ? DragDropEffects.None : DragDropEffects.Move;
        if (ReferenceEquals(over, headerHighlight)) return;
        ClearHeaderHighlight();
        if (over is not null) { over.Opacity = 0.65; headerHighlight = over; }
    }

    private void LoginTree_Drop(object sender, DragEventArgs e)
    {
        if (e.Handled || e.Data.GetData(typeof(TerminalPaneViewModel)) is not TerminalPaneViewModel moved) return;
        e.Handled = true;
        ClearHeaderHighlight();
        if (treeDragOwner is not { } from || e.OriginalSource is not DependencyObject hit
            || ItemsControl.ContainerFromElement(LoginTree, hit) is not FrameworkElement { DataContext: MainViewModel to }
            || ReferenceEquals(from, to)) return;
        QueueTransfer(moved, from, to);
    }

    /// <summary>Runs <see cref="TransferAsync"/> once the drag's own modal loop has ended, so its confirmation isn't nested in it.</summary>
    private void QueueTransfer(TerminalPaneViewModel pane, MainViewModel from, MainViewModel to) =>
        Dispatcher.InvokeAsync(async () =>
        {
            try { await TransferAsync(pane, from, to); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Error(ex, "Transferring a dragged CLI failed; contained"); }
        });

    /// <summary>A tree CLI dropped on another login (M.ORCH-T1849): after a Yes/No, the same move as its right-click "Move to".</summary>
    private async Task TransferAsync(TerminalPaneViewModel pane, MainViewModel from, MainViewModel to)
    {
        if (!to.IsTabSignedIn) { from.SayMove($"{to.TabTitle} isn't signed in, so {pane.Title} can't move there."); return; }
        if (pane.HostPid is not { } host || from.AgentOfHost(host) is not { } agent)
        {
            from.SayMove($"{pane.Title} isn't known to the Orchestrator yet: it can move once its first turn starts.");
            return;
        }
        var targets = await from.MoveTargetsAsync(agent.OwnLogin);
        if (targets.FirstOrDefault(t => to.ScopeEmail is null ? t.IsGlobal : string.Equals(t.Email, to.ScopeEmail, StringComparison.OrdinalIgnoreCase)) is not { } target)
        {
            from.SayMove($"{pane.Title} can't move to {to.TabTitle}: it isn't one of the logins it can move to.");
            return;
        }
        if (MaMessageBox.Show(this, $"Move {pane.Title} to {to.TabTitle}? Its CLI moves at its next idle point, keeping its session.", "Move CLI?",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        await from.MoveToAccountAsync(agent.Repo, agent.SessionId, agent.CliPid, target);
    }

    /// <summary>Applies a tree reorder drag dropped on <paramref name="sender"/>'s row (M.ORCH-T1768 note 6), the tree's own
    /// version of <see cref="TerminalTabs_Reordered"/>: moves the dragged pane to just before the row dropped on, among its
    /// own login's panes alone (<see cref="PersonTerminalsViewModel.Reorder"/>, found via the dropped-on row's own ancestor
    /// login, not <see cref="Shown"/>, for the same reason <see cref="LoginTreeChild_ContextMenuOpening"/> doesn't use it
    /// either: another login's panes can be the ones shown expanded here). A no-op, not a crash, for a drop from a
    /// different login's rows (<c>Panes.Contains</c> guards it); there's no pinned Usage row among these children to drop
    /// onto any more either (M.ORCH-T1768: the tree's own child list is <see cref="PersonTerminalsViewModel.Panes"/>
    /// itself now, which never held it to begin with).</summary>
    private void LoginTreeChildRow_Drop(object sender, DragEventArgs e)
    {
        try
        {
            ClearDropLine();
            if (e.Data.GetData(typeof(TerminalPaneViewModel)) is not TerminalPaneViewModel moved
                || sender is not FrameworkElement { DataContext: TerminalPaneViewModel target } row) return;
            e.Handled = true;
            if (FindAncestorDataContext<MainViewModel>(VisualTreeHelper.GetParent(row)) is not { } owner || ReferenceEquals(moved, target)) return;
            if (!owner.Terminals.Panes.Contains(moved))
            {
                if (treeDragOwner is { } from && !ReferenceEquals(from, owner)) QueueTransfer(moved, from, owner);
                return;
            }
            var panes = owner.Terminals.Panes;
            var to = panes.IndexOf(target) + (DropsBelow(row, e) ? 1 : 0);
            ReorderWithPlaceholder(owner, moved, panes.IndexOf(moved) < to ? to - 1 : to);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Error(ex, "Dropping a tree CLI reorder failed; contained"); }
    }

    /// <summary>How long a same-login reorder (this row drop, and <see cref="TerminalTabs_Reordered"/>'s own flat-strip
    /// drag) may run before it shows M.ORCH-T1883's own placeholder instead of just happening (M.ORCH-T1884): the owner's
    /// own 500ms.</summary>
    private static readonly TimeSpan ReorderPlaceholderThreshold = TimeSpan.FromMilliseconds(500);

    /// <summary>Applies a same-login reorder (<see cref="PersonTerminalsViewModel.Reorder"/>: <c>Panes.Move</c> then
    /// <see cref="TerminalsViewModel.Restamp"/>) with M.ORCH-T1884's own 500ms threshold wired around it: a timer started
    /// the instant the drop is accepted would flip <paramref name="moved"/>'s own placeholder on
    /// (<see cref="TerminalPaneViewModel.SetReorderPending"/>, the exact same faded/dashed/spinner look M.ORCH-T1883 gives
    /// a cross-login transfer) once <see cref="ReorderPlaceholderThreshold"/> passes without Reorder having returned yet.
    /// <para>Honestly, as Reorder is written today (M.ORCH-T1768 note 6), it never does: Panes.Move and Restamp both run to
    /// completion on this very call stack, with no await anywhere in either, so the timer's own Tick - itself only ever
    /// raised on this same UI thread's dispatcher queue - can't run until the <c>try</c> below has already returned control
    /// to it. This is wired up anyway, ready for the day Reorder's own redraw genuinely becomes asynchronous (e.g. a slow
    /// disk write Restamp awaited instead of blocking on); until then the threshold is unreachable, not a simulated delay
    /// pretending otherwise.</para></summary>
    private void ReorderWithPlaceholder(MainViewModel owner, TerminalPaneViewModel moved, int newIndex)
    {
        var done = false;
        var timer = new DispatcherTimer { Interval = ReorderPlaceholderThreshold };
        timer.Tick += (_, _) => { timer.Stop(); if (!done) moved.SetReorderPending(true); };
        timer.Start();
        try { owner.Terminals.Reorder(moved, newIndex); }
        finally
        {
            done = true;
            timer.Stop();
            moved.SetReorderPending(false);
        }
    }

    /// <summary>The nearest ancestor in the visual tree whose own DataContext is a <typeparamref name="T"/>; null past the root.</summary>
    private static T? FindAncestorDataContext<T>(DependencyObject? node) where T : class
    {
        while (node is not null)
        {
            if (node is FrameworkElement { DataContext: T match }) return match;
            node = VisualTreeHelper.GetParent(node);
        }
        return null;
    }

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

    /// <summary>The tab the Close CLI modal asks about (M.ORCH-T1442); null while it's closed.</summary>
    private TerminalPaneViewModel? closingTab;

    /// <summary>"Close CLI?" (M.ORCH-T1437, in the window's own MaModal since M.ORCH-T1442): opens the Close CLI modal on
    /// <paramref name="pane"/>, Cancel focused, the terminals hidden meanwhile since their native windows would draw over it.
    /// Answers false (not closed now); the modal's Close closes the tab (<see cref="CloseCliModalFor"/>).</summary>
    private bool ConfirmCloseTerminal(TerminalPaneViewModel pane)
    {
        try
        {
            closingTab = pane;
            CloseCliQuestion.Text = TerminalsViewModel.CloseQuestion(pane);
            CloseCliModal.IsOpen = true;
            UpdateTerminalsCover();
            Dispatcher.BeginInvoke(() =>
            {
                if (!ReferenceEquals(closingTab, pane)) return;
                CloseCliCancel.Focus();
                Keyboard.Focus(CloseCliCancel);
            }, DispatcherPriority.Input);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Error(ex, "Opening Close CLI failed; contained"); }
        return false;
    }

    /// <summary>Closes the Close CLI modal, closing its tab (ending its CLI) when <paramref name="close"/>, then hands the
    /// keyboard back to the shown CLI.</summary>
    private void CloseCliModalFor(bool close)
    {
        var pane = closingTab;
        closingTab = null;
        try { if (close && pane is not null && model.AllTerminals.Panes.Contains(pane)) model.AllTerminals.Close(pane); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Error(ex, "Closing a CLI tab failed; contained"); }
        finally
        {
            CloseCliModal.IsOpen = false;
            UpdateTerminalsCover();
            PinTerminalFocus();
        }
    }

    private void CloseCliConfirm_Click(object sender, RoutedEventArgs e) => CloseCliModalFor(true);

    private void CloseCliCancel_Click(object sender, RoutedEventArgs e) => CloseCliModalFor(false);

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
        if (e.MovedItem is TerminalPaneViewModel pane) ReorderWithPlaceholder(Shown, pane, e.NewIndex - 1);
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

    /// <summary>The tree sidebar's own "more" button on a login's header row (M.ORCH-T1793, LoginTreeMoreButton): the exact
    /// same root-node menu a right-click on the header opens (<see cref="LoginTreeHeader_ContextMenuOpening"/>), reachable
    /// with a tap or a left click - the icon that makes the drawer's own menu usable without a right-click, which doesn't
    /// work well on touch or in the narrow layout to begin with.</summary>
    private void LoginTreeMoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: MainViewModel tab } button || !tab.IsPersonTab) return;
        e.Handled = true;
        ShowLoginMenu(button, tab, PlacementMode.Bottom);
    }

    /// <summary>The tree sidebar's own "more" button on a CLI/Launcher pane row (M.ORCH-T1793, TreePaneMoreButton): the
    /// exact same menu a right-click on the row opens (<see cref="LoginTreeChild_ContextMenuOpening"/>), reachable with a
    /// tap or a left click. The owning login is found the same way the row's own right-click handler finds it.</summary>
    private async void TreePaneMoreButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not FrameworkElement { DataContext: TerminalPaneViewModel pane } button) return;
            if (FindAncestorDataContext<MainViewModel>(VisualTreeHelper.GetParent(button)) is not { } owner) return;
            e.Handled = true;
            await ShowTerminalMenuAsync(button, pane, PlacementMode.Bottom, owner);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Error(ex, "Opening the tree's CLI menu from its icon button failed; contained"); }
    }

    /// <summary>The flat login tab strip's own "more" button (M.ORCH-T1793, LoginTabMoreButton): the exact same menu a
    /// right-click on the tab opens (<see cref="OnLoginTabContextMenu"/>), reachable with a tap or a left click.</summary>
    private void LoginTabMoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: MainViewModel tab } button || !tab.IsPersonTab) return;
        e.Handled = true;
        ShowLoginMenu(button, tab, PlacementMode.Bottom);
    }

    /// <summary>"+ Add" (M.ORCH-T1964): the flat tab strip's own button and the tree sidebar header's own, at the end
    /// of every login row. Opens <see cref="OpenProjectWindow"/> for that login - General Prompt, Run Launcher or a
    /// project from the tree, one click away instead of behind the row's "⋯" menu.</summary>
    private void AddTabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: MainViewModel tab } || !tab.IsPersonTab) return;
        e.Handled = true;
        ShowOpenProjectWindow(tab);
    }

    /// <summary>Opens <see cref="OpenProjectWindow"/> over this dashboard for <paramref name="tab"/>'s login: General
    /// Prompt, Run Launcher or a project from the tree, each selecting <paramref name="tab"/> first
    /// (<see cref="MainViewModel.SelectedTab"/>) so the new CLI lands on the right login's own tab strip, exactly as
    /// the old Open Project cascading menu (M.ORCH-T1585) did.</summary>
    private void ShowOpenProjectWindow(MainViewModel tab)
    {
        _ = MainViewModel.RefreshProjectsAsync(DashboardTimings.ProjectsRefresh);
        void Open(Func<Task> start) { model.SelectedTab = tab; _ = start(); }
        try
        {
            new OpenProjectWindow(MainViewModel.ProjectTree ?? [],
                generalPrompt: kind => Open(() => tab.OpenGeneralPromptAsync(kind)),
                runLauncher: () => Open(() =>
                {
                    if (tab.RunLauncherCommand.CanExecute(null)) tab.RunLauncherCommand.Execute(null);
                    return Task.CompletedTask;
                }),
                openProject: (project, kind) => Open(() => tab.OpenProjectAsync(project, kind)),
                runCommand: (project, line) => Open(() => { tab.OpenRunCommand(project, line); return Task.CompletedTask; }))
            { Owner = this }.Show();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Error(ex, "Opening the Open Project window failed; contained"); }
    }

    /// <summary>The flat CLI tab strip's own "more" button (M.ORCH-T1793, TerminalMoreButton): the exact same menu a
    /// right-click on the tab opens (<see cref="OnTerminalTabContextMenu"/>), reachable with a tap or a left click.</summary>
    private async void TerminalMoreButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not FrameworkElement { DataContext: TerminalPaneViewModel pane } button) return;
            e.Handled = true;
            await ShowTerminalMenuAsync(button, pane, PlacementMode.Bottom);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Error(ex, "Opening the CLI tab's menu from its icon button failed; contained"); }
    }

    /// <summary>A CLI tab's right-click menu at <paramref name="container"/>, for <paramref name="owner"/>'s login (its
    /// StatusMessage/AccountsMessage/Toasts land there); defaults to <see cref="Shown"/> for the flat tab strip, where
    /// that's always the pane's own login anyway, but the tree sidebar (<see cref="LoginTreeChild_ContextMenuOpening"/>)
    /// passes the pane's actual owning login explicitly, since another login's panes can be expanded there too.</summary>
    private async Task ShowTerminalMenuAsync(FrameworkElement container, TerminalPaneViewModel pane, PlacementMode placement, MainViewModel? owner = null)
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
            var tab = owner ?? Shown;
            var agent = pane.HostPid is { } host ? tab.AgentOfHost(host) : null;
            var menu = DropdownMenu(container, $"Move the {pane.Name} CLI to", placement);
            // Copy PID (M.ORCH-T1518): the CLI's own pid, which CLIs address each other by (MINDATTIC_CLI_PID, nudge-<pid>-*.txt).
            AddCopyPidItems(menu, agent?.CliPid, pane.HostPid);
            // Rename… (M.ORCH-T1509): a little modal for the tab's own name, kept across reattach, undock/dock and restarts.
            var rename = DropdownItem("", "Rename…");
            rename.ToolTip = "Give this tab a name of your own (double-clicking its name does too)";
            rename.Click += (_, _) => OpenRenameTab(pane);
            menu.Items.Add(rename);
            menu.Items.Add(PaneColorMenu(pane)); // Change Color (M.ORCH-T1594)
            if (pane.IsHosted)
            {
                var undock = DropdownItem("", "Undock");
                undock.ToolTip = "Drop this tab but keep its CLI running headless; Dock (on the Launcher's tab) or /dock brings it back";
                undock.Click += (_, _) =>
                {
                    if (!model.AllTerminals.Undock(pane))
                        OrchestratorLog.For("Dashboard").Warning("Couldn't undock {Title:l}: its tab file couldn't be marked", pane.Title);
                };
                menu.Items.Add(undock);
            }
            AddDockItems(menu, undocked);
            menu.Items.Add(DropdownSeparator());
            AddPopOutItems(menu, pane);
            AddBroadcastItems(menu, pane);
            if (agent is null)
            {
                var wait = DropdownItem("", "Move to");
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
                var none = DropdownItem("", "Move to: no other signed-in login");
                none.IsEnabled = false;
                menu.Items.Add(none);
            }
            foreach (var target in targets)
            {
                var item = DropdownItem("", $"Move to {target.Label}");
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
            var item = DropdownItem("", $"Dock {title}");
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
        var cli = DropdownItem("", cliPid is { } pid ? $"Copy PID {pid}" : "Copy PID");
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
        var hostItem = DropdownItem("", $"Copy host PID {host}");
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
        foreach (var (icon, header, watchOnly) in new[] { ("", "Pop out (interactive)", false), ("", "Pop out (watch only)", true) })
        {
            var item = DropdownItem(icon, header);
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
        menu.Items.Add(DropdownSeparator());
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
        ShowLoginMenu(container, tab, e.CursorLeft < 0 ? PlacementMode.Bottom : PlacementMode.MousePoint); // from the keyboard: under the tab
    }

    /// <summary>The tree sidebar's login header right-click menu (M.ORCH-T1768 note 6): the root-node menu the flat login
    /// tab strip's own header offers (<see cref="OnLoginTabContextMenu"/>) - Open Project, Run Launcher, User Settings,
    /// Change Color - which the tree view (TreeMenuLayout, on by default since M.ORCH-T1759) left with no
    /// way to reach at all: right-clicking a tree header did nothing before this, since it had no ContextMenuOpening wired.
    /// A root node's menu is expected to differ from a CLI node's own (ticket note 5's own clarification); this just
    /// restores the root menu itself to the layout that's now most people's default.</summary>
    private void LoginTreeHeader_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: MainViewModel tab } container || !tab.IsPersonTab) return;
        e.Handled = true;
        ShowLoginMenu(container, tab, e.CursorLeft < 0 ? PlacementMode.Bottom : PlacementMode.MousePoint);
    }

    /// <summary>A login's root-node right-click menu at <paramref name="container"/>, shared by the flat login tab strip's
    /// header and the tree sidebar's own (<see cref="OnLoginTabContextMenu"/>/<see cref="LoginTreeHeader_ContextMenuOpening"/>).</summary>
    private const string ApiCreditsUrl = "https://platform.claude.com/settings/billing";

    private void ShowLoginMenu(FrameworkElement container, MainViewModel tab, PlacementMode placement)
    {
        var menu = DropdownMenu(container, $"{tab.TabTitle}'s login", placement);
        void Add(string icon, string header, string tip, ICommand command)
        {
            var item = DropdownItem(icon, header);
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
        // Login/Log out (M.ORCH-T1820, M.ORCH-T1821): every real Claude login - not the API key pseudo-account, which has
        // Buy API credits below instead - can be re-authenticated from here without opening the Logins modal. Login always
        // shows, so a login nearing its expiry can be renewed ahead of it; Log out joins it only while actually signed in.
        // Once signed out, the rest of this menu (Projects, Run Launcher, User Settings, Change Color) is left off
        // entirely: IsTabSignedIn is the same real signed-in state those already gate (the sign-in-only panel and the tree's
        // muted "Disabled: signed out" badge), not a flag of its own, so a logged-out login looks and acts exactly like a
        // roster account never signed in here to begin with.
        if (!tab.IsApiKeyTab)
        {
            Add("", "Login", "Re-authenticates this one login (the same sign-in Sign in uses) without opening the Logins modal or touching any other login",
                tab.LoginCommand);
            if (tab.ScopeEmail is not null && tab.IsTabSignedIn)
                Add("", "Log out", "Signs this login out: its CLIs become inaccessible and its row grays out until Login (above) signs it back in",
                    tab.LogOutCommand);
            // Disable/Enable (M.ORCH-T1958): a per-CLI login only, signed in or not; the node stays shown either way (M.ORCH-T1950).
            if (tab.ScopeEmail is not null)
                Add("", tab.IsLoginDisabled ? "Enable" : "Disable",
                    tab.IsLoginDisabled ? "Lets work be picked, moved and paced to this login again" : "Stops any work being picked, moved or paced to this login; its node stays in the tree",
                    tab.ToggleDisabledCommand);
            // Delete (M.ORCH-T1953): a per-CLI login only (never the global login's tab), signed in or not; it confirms first.
            if (tab.ScopeEmail is not null)
                Add("", "Delete…", "Removes this member: closes its tabs, takes it off the member roster (orchestrator members remove) and moves its login folder to .claude-accounts\\.deleted as a dated backup",
                    tab.DeleteLoginCommand);
            if (!tab.IsTabSignedIn)
            {
                OpenLoginMenu(menu);
                return;
            }
            menu.Items.Add(DropdownSeparator());
        }
        menu.Items.Add(OpenProjectMenuItem(tab));
        var commitProjects = DropdownItem("", "Commit and push projects…");
        commitProjects.ToolTip = "Pick projects in the tree and commit and push each one under one message (M.ORCH-T1589)";
        commitProjects.IsEnabled = MainViewModel.ProjectTree is { Count: > 0 };
        commitProjects.Click += (_, e) => { e.Handled = true; OpenCommitProjects(); };
        menu.Items.Add(commitProjects);
        var refreshProjects = DropdownItem("", "Refresh projects");
        refreshProjects.ToolTip = "Read the Launcher's roster and GitHub's wip-tagged repos again";
        refreshProjects.Click += (_, e) => { e.Handled = true; _ = tab.RefreshProjectListAsync(); };
        menu.Items.Add(refreshProjects);
        var discoverProjects = DropdownItem("", "Discover projects…");
        discoverProjects.ToolTip = "Find git repos under the workspace root that aren't on the Launcher's roster yet, and add the ones you pick";
        discoverProjects.Click += (_, e) => { e.Handled = true; _ = DiscoverProjectsAsync(tab); };
        menu.Items.Add(discoverProjects);
        var commitAll = DropdownItem("", "Commit + push all dirty projects…");
        commitAll.ToolTip = "One commit message for every project with changes, each pushed (also: right-click Open Project)";
        commitAll.Click += (_, e) => { e.Handled = true; CommitAllProjects(commitAll, tab); };
        menu.Items.Add(commitAll);
        menu.Items.Add(DropdownSeparator());
        Add("", "Run Launcher", "Opens this login's standalone Launcher (its own process): Open Project above does the same in-app",
            tab.RunLauncherCommand);
        Add("", "User Settings…", "This login's name, terminal-title prefix and own 5-hour and weekly cutoffs (orchestrator accounts alias, orchestrator org limit)",
            tab.UserSettingsCommand);
        menu.Items.Add(LoginColorMenu(tab)); // Change Color (M.ORCH-T1594)
        // The API key's person (M.ORCH-T1816): where its prepaid credits are bought, since nothing here can top them up.
        if (tab.IsApiKeyTab)
        {
            menu.Items.Add(DropdownSeparator());
            var buy = DropdownItem("", "Buy API credits");
            buy.ToolTip = $"Opens {ApiCreditsUrl} in your browser: the Console's billing page, where the key's prepaid credits are bought";
            buy.Click += (_, _) =>
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ApiCreditsUrl) { UseShellExecute = true })?.Dispose(); }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
                {
                    OrchestratorLog.For("Dashboard").Warning("Couldn't open {Url:l}: {Error:l}", ApiCreditsUrl, ex.Message);
                }
            };
            menu.Items.Add(buy);
        }
        menu.IsOpen = true;
    }

    private static void OpenLoginMenu(ContextMenu menu) => menu.IsOpen = true;

    /// <summary>
    /// "Open Project…" (M.ORCH-T1964): opens <see cref="OpenProjectWindow"/> for <paramref name="tab"/>'s login -
    /// General Prompt, Run Launcher or a project from the tree, one window in place of the cascading submenu this item
    /// used to expand into (M.ORCH-T1585), unwieldy to hover open level by level once the roster grew. Its own "+ Add"
    /// button on the login row (<see cref="AddTabButton_Click"/>) reaches the very same window. Right-click still
    /// commits and pushes every dirty project under one message (M.ORCH-T1590), the one piece of the old menu kept
    /// here rather than moved into the window.
    /// </summary>
    private MenuItem OpenProjectMenuItem(MainViewModel tab)
    {
        _ = MainViewModel.RefreshProjectsAsync(DashboardTimings.ProjectsRefresh); // 10 minutes by default (M.ORCH-T1624)
        var item = DropdownItem("", "Open Project…");
        item.ToolTip = "Open a CLI for a project, the General Prompt or this login's Launcher. Right-click: commit + push every dirty project under one message";
        item.IsEnabled = tab.IsTabSignedIn;
        ToolTipService.SetShowOnDisabled(item, true);
        item.Click += (_, e) => { e.Handled = true; ShowOpenProjectWindow(tab); };
        item.PreviewMouseRightButtonUp += (_, e) => { e.Handled = true; CommitAllProjects(item, tab); };
        return item;
    }

    /// <summary>Closes the menu <paramref name="from"/> is in and opens <see cref="CommitAllWindow"/> (M.ORCH-T1590).</summary>
    private void CommitAllProjects(MenuItem from, MainViewModel tab)
    {
        try
        {
            for (DependencyObject? up = from; up is not null; up = LogicalTreeHelper.GetParent(up))
                if (up is ContextMenu menu) { menu.IsOpen = false; break; }
            CommitAllWindow.Show(this, tab.CommitAllProjectsAsync);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Error(ex, "Opening Commit + push all failed; contained"); }
    }

    /// <summary>The picker tree (M.ORCH-T1589): a window of its own, since the terminals' native windows would draw over an
    /// in-window modal.</summary>
    private void OpenCommitProjects()
    {
        if (MainViewModel.ProjectTree is not { Count: > 0 } tree) return;
        try { new CommitProjectsWindow(tree) { Owner = this }.Show(); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Error(ex, "Opening Commit and push projects failed; contained"); }
    }

    /// <summary>"Discover projects…" (M.ORCH-T1987), a sibling of "Refresh projects": finds git repos under the workspace
    /// root the Launcher's roster doesn't know about yet (<see cref="ProjectDiscovery.FindUnregistered"/>), lets the user
    /// pick which to add in <see cref="DiscoverProjectsWindow"/>, appends the picks to settings.json
    /// (<see cref="ProjectDiscovery.Append"/>) and refreshes the project list the same way "Refresh projects" does.</summary>
    private async Task DiscoverProjectsAsync(MainViewModel tab)
    {
        IReadOnlyList<DiscoveredRepo> repos;
        try { repos = await Task.Run(() => ProjectDiscovery.FindUnregistered(ProjectRoster.Load(), tab.WorkspaceRoot)); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            OrchestratorLog.For("Dashboard").Error(ex, "Discover projects failed reading the workspace root; contained");
            return;
        }
        if (repos.Count == 0)
        {
            MaMessageBox.Show(this, "No git repos under the workspace root are missing from the Launcher's roster.", "Discover Projects",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            new DiscoverProjectsWindow(repos, async picked =>
            {
                await Task.Run(() => ProjectDiscovery.Append(picked));
                await tab.RefreshProjectListAsync();
            })
            { Owner = this }.Show();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Error(ex, "Opening Discover Projects failed; contained"); }
    }

    /// <summary>The global toolbar's Projects group (M.ORCH-T1980): opens <see cref="OpenProjectWindow"/> for whichever
    /// login is currently selected (<see cref="Shown"/>) - the same window the per-login row's own "+ Add" button and
    /// "Open Project…" menu item already reach (<see cref="AddTabButton_Click"/>, <see cref="OpenProjectMenuItem"/>),
    /// just reachable without first clicking into that login's own row.</summary>
    private void ToolbarOpenProjectButton_Click(object sender, RoutedEventArgs e) => ShowOpenProjectWindow(Shown);

    /// <summary>The global toolbar's Git group, "Commit and push projects…" (M.ORCH-T1980): the same picker window the
    /// per-login menu's own item opens (<see cref="OpenCommitProjects"/>); it isn't gated on the selected login, since
    /// <see cref="OpenCommitProjects"/> never takes one either.</summary>
    private void ToolbarCommitProjectsButton_Click(object sender, RoutedEventArgs e) => OpenCommitProjects();

    /// <summary>The global toolbar's Git group, "Refresh projects" (M.ORCH-T1980): re-reads the roster for whichever
    /// login is currently selected (<see cref="Shown"/>), the same call the per-login menu's own "Refresh projects"
    /// item makes.</summary>
    private void ToolbarRefreshProjectsButton_Click(object sender, RoutedEventArgs e) => _ = Shown.RefreshProjectListAsync();

    /// <summary>The global toolbar's Azure group (M.ORCH-T2055): a dropdown of Azure tools under the button; "Kickstart web
    /// apps…" opens <see cref="KickstartWindow"/> (list every web app with its state, wake one or all).</summary>
    private void ToolbarAzureButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not UIElement target) return;
        var menu = DropdownMenu(target, "Azure", PlacementMode.Bottom);
        var kick = DropdownItem("", "Kickstart web apps…");
        kick.ToolTip = "Lists every Azure web app with its state and its database's status; wake one or all";
        kick.Click += (_, _) => OpenKickstart();
        menu.Items.Add(kick);
        var export = DropdownItem("", "Export Azure SQL DB…");
        export.ToolTip = "Exports Azure SQL databases (pick which, and a folder) to .sql scripts, schema and data (M.ORCH-T2056)";
        export.Click += (_, _) => OpenAzureExport();
        menu.Items.Add(export);
        menu.IsOpen = true;
    }

    private void OpenAzureExport()
    {
        var root = OrchestratorOptions.Load().WorkspaceRoot;
        if (string.IsNullOrWhiteSpace(root)) return;
        try { new AzureExportWindow(root) { Owner = this }.Show(); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Error(ex, "Opening Azure export failed; contained"); }
    }

    private void OpenKickstart()
    {
        var root = OrchestratorOptions.Load().WorkspaceRoot;
        if (string.IsNullOrWhiteSpace(root)) return;
        try { new KickstartWindow(root) { Owner = this }.Show(); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Error(ex, "Opening Kickstart failed; contained"); }
    }

    /// <summary>The global toolbar's Shells group (M.ORCH-T1988): Command Prompt, elevated, at the workspace root.</summary>
    private void ToolbarCmdAdminButton_Click(object sender, RoutedEventArgs e) => OpenAdminShell("cmd.exe");

    /// <summary>The global toolbar's Shells group (M.ORCH-T1988): PowerShell, elevated, at the workspace root.</summary>
    private void ToolbarPowerShellAdminButton_Click(object sender, RoutedEventArgs e) => OpenAdminShell("powershell.exe");

    /// <summary>Starts <paramref name="exe"/> with Verb=runas (the UAC prompt) in the workspace root, like Launcher's own admin-shell items.</summary>
    private static void OpenAdminShell(string exe)
    {
        var root = OrchestratorOptions.Load().WorkspaceRoot;
        if (string.IsNullOrWhiteSpace(root)) return;
        // An elevated (runas) start ignores WorkingDirectory and lands in System32, so the shell cds itself.
        var args = exe == "cmd.exe"
            ? $"/k cd /d \"{root}\""
            : $"-NoExit -Command \"Set-Location -LiteralPath '{root.Replace("'", "''")}'\"";
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, args)
            {
                Verb = "runas",
                UseShellExecute = true,
                WorkingDirectory = root,
            });
        }
        catch (System.ComponentModel.Win32Exception) { /* the UAC prompt was declined */ }
        catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Error(ex, "Opening an admin shell from the toolbar failed; contained"); }
    }

    /// <summary>The global toolbar's Git group, "Commit + push all dirty projects…" (M.ORCH-T1980): the same
    /// <see cref="CommitAllWindow"/> the per-login menu's own item opens (<see cref="CommitAllProjects"/>), for
    /// whichever login is currently selected (<see cref="Shown"/>) - the toolbar button isn't inside a dropdown, so
    /// there's no menu to close first.</summary>
    private void ToolbarCommitAllButton_Click(object sender, RoutedEventArgs e)
    {
        try { CommitAllWindow.Show(this, Shown.CommitAllProjectsAsync); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Error(ex, "Opening Commit + push all from the toolbar failed; contained"); }
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
        void Add(string icon, string header, bool isChecked, string tip, ICommand command, object? parameter)
        {
            var item = DropdownItem(icon, header);
            item.IsCheckable = true;
            item.IsChecked = isChecked;
            item.ToolTip = tip;
            item.Click += (_, _) => { if (command.CanExecute(parameter)) command.Execute(parameter); };
            menu.Items.Add(item);
        }
        Add("", "Share with remote viewers", pane.IsShared, "Let the team gateway show this CLI to remote viewers you've granted (off by default)", all.ToggleShareCommand, pane);
        Add("", "Mask secrets for remote viewers", pane.IsMasked, "Hide API keys, tokens, private keys and Vault-held values on remote streams (on by default; never on this screen)", all.ToggleMaskCommand, pane);
        Add("", "Pause broadcast", pane.IsBroadcastPaused, "Remote viewers see \"paused by owner\" and nothing else until you resume (Ctrl+Shift+B)", all.TogglePauseCommand, pane);
        Add("", "Share all my sessions", all.ShareAll, "Share every CLI with remote viewers unless one says otherwise", all.ShareAllCommand, null);
        menu.Items.Add(DropdownSeparator());
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

    /// <summary>The font every menu-item glyph below draws from: the same family the window's own toolbar icons and
    /// status badges already use (M.ORCH-T1793).</summary>
    private const string MenuIconFont = "Segoe Fluent Icons, Segoe MDL2 Assets";

    /// <summary>The glyph for every "more actions" button this window adds beside a row that owns a context menu
    /// (M.ORCH-T1793): a tap or a left click opens the exact same menu a right-click on the row would, so the menu is
    /// reachable without one - the sidebar's phone layout (MainWindow.Compact.cs) and any touch session otherwise had no
    /// way to reach these menus at all.</summary>
    private const string MoreGlyph = "";

    /// <summary>A dropdown row's header with a leading icon glyph before its label (M.ORCH-T1793: every context menu this
    /// window builds gets an icon per item, the same composite-header idiom <see cref="ColorMenu"/> already used for its
    /// colour dot, generalized here so it isn't the only one). <see cref="MaDropdownMenuItem"/>'s own template has no Icon
    /// presenter of its own (it draws whatever <c>Header</c> is, plain text or a built-up visual), which is why the icon
    /// is part of the header content rather than <see cref="MenuItem.Icon"/>, which this template never reads.</summary>
    private static object IconHeader(string icon, string label)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new TextBlock { Text = icon, FontFamily = new FontFamily(MenuIconFont), Width = 18, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) });
        return row;
    }

    /// <summary>An icon-led dropdown item (M.ORCH-T1793); see <see cref="IconHeader"/>.</summary>
    private static MenuItem DropdownItem(string icon, string label)
    {
        var item = DropdownItem(IconHeader(icon, label));
        AutomationProperties.SetName(item, label); // the glyph alone says nothing to a screen reader
        return item;
    }

    /// <summary>A full-width, light-gray rule between a dropdown's groups (a bare Separator draws WPF's
    /// light system divider, indented to the system's icon gutter).</summary>
    private static Separator DropdownSeparator()
    {
        var separator = new Separator();
        separator.SetResourceReference(StyleProperty, "MaSeparator");
        return separator;
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
        else if (shown.IsTaskDetailOpen) shown.IsTaskDetailOpen = false;
        else if (shown.IsInboxOpen) shown.IsInboxOpen = false;
        else if (shown.IsPlansOpen) shown.IsPlansOpen = false;
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
