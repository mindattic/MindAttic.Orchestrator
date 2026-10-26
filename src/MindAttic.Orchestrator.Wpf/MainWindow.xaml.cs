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
        // build a new terminal (and a new CLI) every time its tab was shown. Native windows, they're clipped to the page's scroll
        // view, and hidden while a dialog or the loader covers the window, which they'd otherwise draw over.
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
        model.PropertyChanged += (_, _) => UpdateTerminalsCover();
        DataContextChanged += (_, _) => UpdateTerminalsCover();

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

    /// <summary><c>--start &lt;repo&gt;</c> (M.ORCH-T767): a CLI in that workspace repo, as Start opens one; anything else is refused.</summary>
    public Task StartRepoAsync(string repo) => model.StartRepoAsync(repo);

    /// <summary><c>--show &lt;email|session|ticket&gt;</c> (M.ORCH-T763): the person it names, or whose CLI runs that session or ticket.</summary>
    public Task<bool> ShowAsync(string target) => model.ShowAsync(target);

    private CancellationTokenSource? terminalsPipe;

    /// <summary>
    /// Makes this window the one that holds the Terminals panels' terminals (M.ORCH-T810): the hosting entry point. A CLI sent
    /// from another process (MindAttic.Launcher running inside the Orchestrator, M.LNCH-T77) arrives on the terminals pipe
    /// (<see cref="Sessions.PaneBroker"/>) and opens as a new tab on its person's page. Called by the app for its one dashboard
    /// only, never by a test's window.
    /// </summary>
    internal void HostTerminals()
    {
        terminalsPipe = new CancellationTokenSource();
        _ = Sessions.PaneBroker.ServeAsync(request => Dispatcher.InvokeAsync(() => model.OpenTerminal(request)).Task, terminalsPipe.Token)
            .ContinueWith(t => OrchestratorLog.For("Terminals").Error(t.Exception, "The terminals pipe server ended"), TaskContinuationOptions.OnlyOnFaulted);
        Closed += (_, _) => terminalsPipe.Cancel();
    }

    /// <summary>The terminals are native windows WPF can't draw over: hidden (never collapsed, so each keeps its size) while
    /// Settings, Rename or Org limit is open, the loader covers the window or an org with no login covers the page.</summary>
    private void UpdateTerminalsCover()
    {
        var shown = Shown;
        var covered = shown.IsSettingsOpen || shown.IsRenameOpen || shown.IsLimitOpen || model.IsLoading || model.IsOrgEmpty;
        TerminalPanesHost.Visibility = covered ? Visibility.Hidden : Visibility.Visible;
    }

    /// <summary>Reads out the shown tab's status and queue lines.</summary>
    private void OnShownChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender != Shown) return;
        if (e.PropertyName is nameof(MainViewModel.IsSettingsOpen) or nameof(MainViewModel.IsRenameOpen) or nameof(MainViewModel.IsLimitOpen))
            UpdateTerminalsCover();
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

    /// <summary>Starts <paramref name="icon"/>'s rapid forever blink while <paramref name="on"/>, or settles it fully opaque
    /// once it isn't, so it's solid rather than left mid-fade the next time it's shown.</summary>
    private static void Blink(TextBlock icon, bool on) =>
        icon.BeginAnimation(UIElement.OpacityProperty, on
            ? new DoubleAnimation(1.0, 0.15, TimeSpan.FromSeconds(0.35)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever }
            : new DoubleAnimation(1.0, TimeSpan.FromSeconds(0.15)));

    /// <summary>A busy login tab's play icon blinks green rapidly (reversing M.ORCH-T770's calmer glow pulse, at the owner's
    /// ask) whenever the login has a live CLI, inside the Orchestrator's Terminals panel or outside it: it fires once this
    /// instance of the repeated <c>DataTemplate</c> loads, so one login's blink never drives another's.</summary>
    private void TabBusyIcon_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBlock icon) Blink(icon, icon.IsVisible);
    }

    /// <summary>The icon is only visible while its login's tab <c>IsTabBusy</c> (bound through a Visibility converter): showing
    /// or hiding it is exactly when the blink should start or stop.</summary>
    private void TabBusyIcon_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBlock icon) Blink(icon, icon.IsVisible);
    }

    /// <summary>"Move to" on an agent card (M.ORCH-T607): a menu of the other signed-in logins, read when it's opened, each moving
    /// that CLI to its login.</summary>
    private async void OnMoveToClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: AgentRowViewModel a } button) return;
        var tab = Shown;
        try
        {
            ShowMoveMenu(button, $"Move the {a.Repo} CLI to", await tab.MoveTargetsAsync(a.OwnLogin),
                async target =>
                {
                    try { await tab.MoveToAccountAsync(a.Repo, a.SessionId, a.CliPid, target); }
                    catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Dashboard").Error(ex, "Moving a CLI failed; contained"); }
                });
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

    /// <summary>Open Launcher's right-click menu (M.ORCH-Tnnn): "Open in Tab" (embeds, even over a standalone one already
    /// running for this login elsewhere) and "Open in Terminal" (always a Windows Terminal window, no embed attempt), beside
    /// the plain click's automatic embed-or-fallback.</summary>
    private void OnOpenLauncherContextMenu(object sender, ContextMenuEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: MainViewModel tab } button || !tab.IsTabSignedIn) return;
        e.Handled = true;
        var menu = DropdownMenu(button, $"Open the Launcher as {tab.TabTitle}", PlacementMode.Bottom);
        void Add(string header, string tip, Func<Task> action)
        {
            var item = DropdownItem(header);
            item.ToolTip = tip;
            item.Click += async (_, _) => await action();
            menu.Items.Add(item);
        }
        Add("Open in Tab", "Embeds the Launcher's menu as a Terminals panel tab, even if a standalone window is already running for this login elsewhere.", tab.OpenLauncherInTabAsync);
        Add("Open in Terminal", "Opens the Launcher's menu in its own Windows Terminal window, bypassing the embedded panel.", tab.OpenLauncherInTerminalAsync);
        menu.IsOpen = true;
    }

    /// <summary>A Terminals panel tab's right-click menu (M.ORCH-Tnnn): "Undock", sending it out to its own Windows Terminal
    /// window once it's idle, keeping its conversation. Disabled, with why, for the Launcher's own menu pane, a non-Claude
    /// pane (resume has no equivalent for Codex/Gemini/Kimi, same restriction "Move to account" already has), an ended pane,
    /// one whose session isn't known yet, and one already being relocated.</summary>
    private void OnTerminalTabContextMenu(object sender, ContextMenuEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source
            || ItemsControl.ContainerFromElement(TerminalTabs, source) is not FrameworkElement { DataContext: TerminalPaneViewModel pane } container) return;
        e.Handled = true;
        var menu = DropdownMenu(container, pane.Title, e.CursorLeft < 0 ? PlacementMode.Bottom : PlacementMode.MousePoint);
        var undock = DropdownItem("Undock");
        var reason = pane.Request.IsMenu ? "The Launcher's own menu can't be undocked."
            : !string.Equals(pane.Request.Provider, "Claude", StringComparison.OrdinalIgnoreCase) ? $"Undock only supports Claude tabs, not {pane.Request.Provider}."
            : pane.Ended ? "This CLI has already ended."
            : pane.Relocating ? "Already undocking."
            : pane.SessionId is null ? "This tab's session isn't known yet; try again once it's finished starting."
            : null;
        undock.IsEnabled = reason is null;
        if (reason is not null) { undock.ToolTip = reason; ToolTipService.SetShowOnDisabled(undock, true); }
        undock.Click += async (_, _) => await model.UndockAsync(pane);
        menu.Items.Add(undock);
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

    private void ShowMoveMenu(Button button, string name, IReadOnlyList<MindAttic.Orchestrator.Sessions.MoveTarget> targets,
        Func<MindAttic.Orchestrator.Sessions.MoveTarget, Task> move)
    {
        var menu = DropdownMenu(button, name, PlacementMode.Bottom);
        MenuItem Item(object header) => DropdownItem(header);
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
            item.Click += async (_, _) => await move(target);
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
    /// rl-status.json and allocate), else every 60s.</summary>
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
