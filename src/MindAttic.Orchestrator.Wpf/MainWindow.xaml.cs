using System.Collections.Specialized;
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

    private readonly MainViewModel model = new();
    private readonly DispatcherTimer timer;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = model;
        // Scales the window's content and binds Ctrl+Plus / Ctrl+Minus / Ctrl+0 on the window, wherever focus is.
        model.Zoom.Attach(Root, this);

        CommandBindings.Add(new CommandBinding(WindowCommands.FocusSearch, (_, _) =>
        {
            // The search box is only shown in the Repos view, and a collapsed box can't take focus: show it first.
            model.ShowViewCommand.Execute("Repos");
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

        model.Toasts.CollectionChanged += OnToastsChanged;
        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.StatusMessage)) Announce(model.StatusMessage);
            else if (e.PropertyName == nameof(MainViewModel.QueueMessage)) Announce(model.QueueMessage);
        };

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
    private static CustomPopupPlacement[] PlaceAccountPopup(Size popupSize, Size targetSize, Point offset) =>
        [new CustomPopupPlacement(new Point(targetSize.Width - popupSize.Width, targetSize.Height), PopupPrimaryAxis.Horizontal)];

    /// <summary>A row clicked in the account menu table (M.ORCH-T566): close the popup first, so it's never on screen for a
    /// screenshot or the start of a recording, then run the switch itself.</summary>
    private void OnAccountRowClick(object sender, RoutedEventArgs e)
    {
        AccountPopup.IsOpen = false;
        if (e.OriginalSource is not Button { CommandParameter: string id }) return;
        Dispatcher.BeginInvoke(async () => await model.ChooseAccountAsync(id), DispatcherPriority.Background);
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
        if (AccountPopup.IsOpen) AccountPopup.IsOpen = false;
        else if (model.IsSettingsOpen) model.IsSettingsOpen = false;
        else if (SearchBox.IsKeyboardFocusWithin && model.Search.Length > 0) model.Search = "";
        else if (model.Selected is not null) model.SelectCommand.Execute(model.Selected);
        else model.Search = "";
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

    /// <summary>A session that needs you should be noticed even when this window is behind others. The toast row (McToastHost)
    /// reads each new one out itself.</summary>
    private void OnToastsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || IsActive) return;
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
