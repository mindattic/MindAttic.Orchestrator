using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using MindAttic.Orchestrator.Wpf.ViewModels;

namespace MindAttic.Orchestrator.Wpf;

public partial class MainWindow : Window
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    /// <summary>Hidden in the tray while the daemon builds the snapshots: just enough to keep the tray's summary current.</summary>
    private static readonly TimeSpan HiddenInterval = TimeSpan.FromSeconds(60);

    /// <summary>Hidden but holding the lease (no daemon running): the daemon's own pace, since rl-status.json and autopilot depend on it.</summary>
    private static readonly TimeSpan HiddenHolderInterval = TimeSpan.FromSeconds(15);

    private readonly MainViewModel model = new();
    private readonly DispatcherTimer timer;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = model;

        CommandBindings.Add(new CommandBinding(WindowCommands.FocusSearch, (_, _) =>
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
        }));
        CommandBindings.Add(new CommandBinding(WindowCommands.Escape, (_, _) => OnEscape()));

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
        // Minimized to the tray, nobody is looking: no 5-second refresh (M.ORCH-T115). Shown again, it catches up at once.
        IsVisibleChanged += async (_, _) => await OnShownOrHidden();
        StateChanged += async (_, _) => await OnShownOrHidden();
    }

    private bool Hidden => !IsVisible || WindowState == WindowState.Minimized;

    private void Retime()
    {
        var interval = RefreshInterval(Hidden, model.HoldsLease);
        if (timer.Interval != interval) timer.Interval = interval;
    }

    /// <summary>How often to refresh: every 5s while shown; hidden, every 15s if this window holds the lease (it feeds
    /// rl-status.json and autopilot), else every 60s, just enough for the tray's summary.</summary>
    internal static TimeSpan RefreshInterval(bool hidden, bool holdsLease) =>
        !hidden ? PollInterval : holdsLease ? HiddenHolderInterval : HiddenInterval;

    private bool wasHidden;

    private async Task OnShownOrHidden()
    {
        Retime();
        var shown = wasHidden && !Hidden;
        wasHidden = Hidden;
        if (shown && IsLoaded) await model.RefreshAsync();
    }

    private void OnEscape()
    {
        if (model.IsSettingsOpen) model.IsSettingsOpen = false;
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

    /// <summary>A session that needs you should be noticed even when this window is behind others, and heard by a screen reader.</summary>
    private void OnToastsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;
        if (e.NewItems?.Count > 0 && e.NewItems[0] is ToastViewModel toast) Announce($"{toast.Title}. {toast.Body}");
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
