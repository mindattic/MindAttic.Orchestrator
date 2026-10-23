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
        // The org switcher above it, and what an org with no Claude login says, are the global login's tab's too (M.ORCH-T655).
        OrgStrip.DataContext = model;
        OrgEmptyPanel.DataContext = model;
        // The Queue view's header scrolls in at most 45% of the view, so the virtualized queue below it always has room.
        QueueView.SizeChanged += (_, e) => QueueBody.MaxHeight = QueueHeaderHeight(e.NewSize.Height);
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

    /// <summary>Reads out the shown tab's status and queue lines.</summary>
    private void OnShownChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender != Shown) return;
        if (e.PropertyName == nameof(MainViewModel.StatusMessage)) Announce(Shown.StatusMessage);
        else if (e.PropertyName == nameof(MainViewModel.QueueMessage)) Announce(Shown.QueueMessage);
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
        // The library's dark dropdown (M.ORCH-T667): a bare ContextMenu and MenuItem draw WPF's light system menu.
        menu.SetResourceReference(StyleProperty, "MaDropdownMenu");
        MenuItem Item(object header) { var i = new MenuItem { Header = header }; i.SetResourceReference(StyleProperty, "MaDropdownMenuItem"); return i; }
        AutomationProperties.SetName(menu, $"Move the {it.Repo} CLI to");
        if (targets.Count == 0)
        {
            var none = Item("No other signed-in login");
            none.IsEnabled = false;
            menu.Items.Add(none);
        }
        foreach (var target in targets)
        {
            var item = Item(target.Label);
            item.ToolTip = target.IsGlobal ? "The global login (~/.claude)" : $"{target.Email}'s own login (~/.claude-accounts)";
            item.Click += async (_, _) => await tab.MoveToAccountAsync(it.Repo, it.Session, it.Pid, target);
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    /// <summary>The most the Queue view's header (autopilot line, holds, reminders, finished work, Next up) takes of a view
    /// <paramref name="viewHeight"/> tall: 45%, never under 120px.</summary>
    internal static double QueueHeaderHeight(double viewHeight) => Math.Max(120, viewHeight * 0.45);

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
        if (shown.IsSettingsOpen) shown.IsSettingsOpen = false;
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
