using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using MindAttic.Orchestrator.Wpf.ViewModels;

namespace MindAttic.Orchestrator.Wpf;

public partial class MainWindow : Window
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    /// <summary>Minimized while the daemon builds the snapshots: nobody is looking, so just enough to stay roughly current.</summary>
    private static readonly TimeSpan HiddenInterval = TimeSpan.FromSeconds(60);

    /// <summary>Hidden but holding the lease (no daemon running): the daemon's own pace, since rl-status.json and autopilot depend on it.</summary>
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
        model.SelectedTabChanged += (_, _) => DataContext = model.SelectedTab;
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is MainViewModel was) was.PropertyChanged -= OnShownChanged;
            if (e.NewValue is MainViewModel now) now.PropertyChanged += OnShownChanged;
        };
        model.PropertyChanged += OnShownChanged;
        // Scales the window's content and binds Ctrl+Plus / Ctrl+Minus / Ctrl+0 on the window, wherever focus is.
        model.Zoom.Attach(Root, this);

        CommandBindings.Add(new CommandBinding(WindowCommands.FocusSearch, (_, _) =>
        {
            // The search box is only shown in the Repos view, and a collapsed box can't take focus: show it first.
            Shown.ShowViewCommand.Execute("Repos");
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
            });
        }));
        CommandBindings.Add(new CommandBinding(WindowCommands.Escape, (_, _) => OnEscape()));

        // The account menu in the upper right (M.ORCH-T492, M.ORCH-T566): a table popup, its rows rebuilt from the roster
        // every time AccountMenuItems changes. AccountPopup.StaysOpen="False" already closes it on an outside click or lost
        // focus; a plain Click on the trigger would immediately reopen what that outside click just closed, so the toggle
        // is done on the preview mouse-down instead, closing (and swallowing the press) when it's already open.
        AccountMenuButton.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (!AccountPopup.IsOpen) return;
            AccountPopup.IsOpen = false;
            e.Handled = true;
        };
        AccountMenuButton.Click += (_, _) => AccountPopup.IsOpen = true;
        AccountPopup.CustomPopupPlacementCallback = PlaceAccountPopup;
        // A row's Button has no Command (its CommandParameter alone can't pick which VM method to call); caught here instead,
        // once, since every row's Click bubbles up through the ItemsControl regardless of which DataTemplate instance it's in.
        AccountRows.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnAccountRowClick));

        // Each pane's terminal lives in the Terminals view's host grid from its opening to its closing (M.ORCH-T618): a
        // template would build a new terminal (and a new CLI) every time its tab was shown.
        model.Terminals.Panes.CollectionChanged += (_, e) =>
        {
            foreach (var gone in e.OldItems?.OfType<TerminalPaneViewModel>() ?? []) TerminalPanesHost.Children.Remove(gone.Control);
            foreach (var added in e.NewItems?.OfType<TerminalPaneViewModel>() ?? []) TerminalPanesHost.Children.Add(added.Control);
        };

        // A toast on any login's tab flashes the window.
        model.ToastRaised += (_, _) => OnToastAdded();

        timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = PollInterval };
        timer.Tick += async (_, _) =>
        {
            await model.RefreshAsync();
            Retime();
        };
        Loaded += async (_, _) =>
        {
            model.Clock.Start();
            await model.RefreshAsync();
            timer.Start();
        };
        Closed += (_, _) =>
        {
            timer.Stop();
            model.Clock.Stop();
        };
        // Minimized, nobody is looking: no 5-second refresh (M.ORCH-T115). Shown again, it catches up at once.
        IsVisibleChanged += async (_, _) => await OnShownOrHidden();
        StateChanged += async (_, _) => await OnShownOrHidden();
    }

    /// <summary>Lines up the account menu table's right edge with the trigger button's right edge, directly beneath it,
    /// rather than the left-aligned spot a plain Placement="Bottom" would give a trigger sitting at the window's right
    /// edge (which would push most of a ~750px table off past that edge).</summary>
    private CancellationTokenSource? paneBroker;

    /// <summary>
    /// Makes this window the one that holds terminal panes (M.ORCH-T618): a start in this process opens its pane here directly,
    /// and one from another process (the daemon's autopilot, <c>orchestrator start</c>) arrives on the pane pipe. Called by the
    /// app for its one dashboard only, never by a test's window.
    /// </summary>
    internal void HostPanes()
    {
        Sessions.PaneBroker.Local = request => Dispatcher.CheckAccess() ? model.Terminals.Open(request) : Dispatcher.Invoke(() => model.Terminals.Open(request));
        paneBroker = new CancellationTokenSource();
        _ = Sessions.PaneBroker.ServeAsync(request => Dispatcher.InvokeAsync(() => model.Terminals.Open(request)).Task, paneBroker.Token);
        Closed += (_, _) =>
        {
            paneBroker.Cancel();
            Sessions.PaneBroker.Local = null;
        };
    }

    /// <summary>Reads out the shown tab's status and queue lines.</summary>
    private void OnShownChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender != Shown) return;
        if (e.PropertyName == nameof(MainViewModel.StatusMessage)) Announce(Shown.StatusMessage);
        else if (e.PropertyName == nameof(MainViewModel.QueueMessage)) Announce(Shown.QueueMessage);
    }

    private static CustomPopupPlacement[] PlaceAccountPopup(Size popupSize, Size targetSize, Point offset) =>
        [new CustomPopupPlacement(new Point(targetSize.Width - popupSize.Width, targetSize.Height), PopupPrimaryAxis.Horizontal)];

    /// <summary>A row clicked in the account menu table (M.ORCH-T566): close the popup first, so it's never on screen for a
    /// screenshot or the start of a recording, then run the switch itself.</summary>
    private void OnAccountRowClick(object sender, RoutedEventArgs e)
    {
        AccountPopup.IsOpen = false;
        if (e.OriginalSource is not Button { CommandParameter: string id }) return;
        var tab = Shown;
        Dispatcher.BeginInvoke(async () => await tab.ChooseAccountAsync(id), DispatcherPriority.Background);
    }

    /// <summary>"Move to" on an agent card or an Accounts-view CLI row (M.ORCH-T607): a menu of the other signed-in logins,
    /// read when it's opened, each moving that CLI to its login.</summary>
    private async void OnMoveToClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        (string Repo, string Session, int? Pid, string? Own)? cli = button.DataContext switch
        {
            AgentRowViewModel a => (a.Repo, a.SessionId, a.CliPid, a.OwnLogin),
            LoginCliViewModel c => (c.Repo, c.SessionId, c.CliPid, c.OwnLogin),
            _ => null,
        };
        if (cli is not { } it) return;
        var tab = Shown;
        var targets = await tab.MoveTargetsAsync(it.Own);
        var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Bottom };
        AutomationProperties.SetName(menu, $"Move the {it.Repo} CLI to");
        if (targets.Count == 0)
            menu.Items.Add(new MenuItem { Header = "No other signed-in login", IsEnabled = false });
        foreach (var target in targets)
        {
            var item = new MenuItem { Header = target.Label, ToolTip = target.IsGlobal ? "The global login (~/.claude)" : $"{target.Email}'s own login (~/.claude-accounts)" };
            item.Click += async (_, _) => await tab.MoveToAccountAsync(it.Repo, it.Session, it.Pid, target);
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    private bool Hidden => !IsVisible || WindowState == WindowState.Minimized;

    private void Retime()
    {
        var interval = RefreshInterval(Hidden, model.HoldsLease);
        if (timer.Interval != interval) timer.Interval = interval;
    }

    /// <summary>How often to refresh: every 5s while shown; hidden, every 15s if this window holds the lease (it feeds
    /// rl-status.json and autopilot), else every 60s.</summary>
    internal static TimeSpan RefreshInterval(bool hidden, bool holdsLease) =>
        !hidden ? PollInterval : holdsLease ? HiddenHolderInterval : HiddenInterval;

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
        if (AccountPopup.IsOpen) AccountPopup.IsOpen = false;
        else if (shown.IsSettingsOpen) shown.IsSettingsOpen = false;
        else if (SearchBox.IsKeyboardFocusWithin && shown.Search.Length > 0) shown.Search = "";
        else if (shown.Selected is not null) shown.SelectCommand.Execute(shown.Selected);
        else shown.Search = "";
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
