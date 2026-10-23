using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using System.Windows.Media;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Billing;
using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// One dashboard tab per Claude login (M.ORCH-T613): the global <c>~/.claude</c> login's first, then each per-CLI login under
/// <c>~/.claude-accounts</c> that's signed in or has a CLI on it, titled by its name (M.ORCH-T614) in its terminal tabs' color
/// (M.ORCH-T616), a tab added or removed as logins are signed in or out. Each tab is this whole view model, scoped to its
/// login: its stat tiles, its 5-hour and weekly meter and chart from its own pace, its agents and repos (only the CLIs and
/// Launcher tabs on it), its toasts, and Start opening on its login. Tickets stay workspace-wide, so the queue and the audit
/// read the same everywhere; the Accounts view stays the overview of every login. The global login's tab owns the one
/// shared refresh and hands each tab its part of the snapshot (<see cref="LoginScope.For"/>): no tab refreshes on its own.
/// A known roster account not signed in yet has a tab too, with only its Sign in (M.ORCH-T615); each signed-in tab can open
/// the Launcher as its login.
/// </summary>
public sealed partial class MainViewModel
{
    private LoginTab tab;
    private readonly ObservableCollection<MainViewModel> loginTabs = null!;
    private MainViewModel? selectedTab;
    private ICommand? nextTabCommand, previousTabCommand;

    /// <summary>The login this tab shows.</summary>
    public LoginTab Tab => tab;

    /// <summary>This tab's per-CLI login (its email); null on the global login's tab.</summary>
    public string? ScopeEmail => tab.Email;

    public bool IsGlobalTab => root is null;

    /// <summary>The login's name ("Jordan", "Ryan (fallback)").</summary>
    public string TabTitle => tab.Title;

    /// <summary>Its title and its light's word (M.ORCH-T630: colour is never the only cue), its week left, its email and which
    /// login it is, and how many of its CLIs wait on you.</summary>
    public string TabTip => $"{tab.Title}: {TabStatusWord}" + (tab.IsApiKey ? $", {TabTrailing}" : $", week {TabTrailing}") + $". {tab.ToolTip}." +
        (WaitingCount == 0 ? "" : $" {WaitingCount} need{(WaitingCount == 1 ? "s" : "")} you.");

    /// <summary>The login's terminal tab color (M.ORCH-T616), the tab's accent; the theme's muted brush when it has none.</summary>
    public Brush TabBrush => BrushOf(tab.Color);

    /// <summary>How many of its repos need you, on the tab itself so one waiting on another tab is seen; empty when none.</summary>
    public string TabBadge => WaitingCount > 0 ? WaitingCount.ToString(System.Globalization.CultureInfo.InvariantCulture) : "";

    /// <summary>Every login's tab, the global login's first; the same list in every tab.</summary>
    public ObservableCollection<MainViewModel> LoginTabs => Root.loginTabs;

    /// <summary>The tab strip shows once there's a second login: with only the global one it would be a lone tab.</summary>
    public bool HasLoginTabs => LoginTabs.Count > 1;

    /// <summary>The tab on screen; the same in every tab. The window shows it (its DataContext follows this).</summary>
    public MainViewModel SelectedTab
    {
        get => Root.selectedTab ?? Root;
        // Ctrl+click on the selected pill deselects it, writing null: it stays on the one it was.
        set => Root.Select(value);
    }

    /// <summary>The tab on screen, which the shared queue's rows act through (M.ORCH-T654).</summary>
    internal MainViewModel Shown => Root.SelectedTab;

    /// <summary>Raised on the global login's tab when another tab is chosen.</summary>
    public event EventHandler? SelectedTabChanged;

    /// <summary>Raised on the global login's tab when a toast goes up on any tab, so the window can flash for it.</summary>
    public event EventHandler<ToastViewModel>? ToastRaised;

    /// <summary>Ctrl+Tab and Ctrl+Shift+Tab: the next and the previous login's tab.</summary>
    public ICommand NextTabCommand => nextTabCommand ??= new RelayCommand(() => Root.Step(+1));
    public ICommand PreviousTabCommand => previousTabCommand ??= new RelayCommand(() => Root.Step(-1));

    private void Select(MainViewModel? next)
    {
        if (next is null || ReferenceEquals(next, SelectedTab) || !loginTabs.Contains(next)) return;
        selectedTab = next;
        foreach (var t in loginTabs) t.Raise(nameof(SelectedTab));
        SelectedTabChanged?.Invoke(this, EventArgs.Empty);
        // The person shown is the one whose chosen terminal shows (M.ORCH-T622).
        ShowChosenPane();
        // The Accounts view reads its cards only while open: one opened in the newly shown tab gets them now.
        if (next.IsAccountsView) _ = next.RefreshLoginCardsAsync();
    }

    private void Step(int by)
    {
        if (loginTabs.Count < 2) return;
        var at = loginTabs.IndexOf(SelectedTab);
        Select(loginTabs[((at + by) % loginTabs.Count + loginTabs.Count) % loginTabs.Count]);
    }

    /// <summary>
    /// Keeps a tab per login in <paramref name="tabs"/> (the global login's, this one, first), adding a tab for a login just
    /// signed in and dropping one signed out (back to the global login's tab if it was showing), then hands each tab its own
    /// view of <paramref name="all"/>, this one first: the others reuse the roster it reads.
    /// </summary>
    private void ApplyTabs(Snapshot all, IReadOnlyList<LoginTab> tabs)
    {
        var before = loginTabs.Count;
        var keysBefore = loginTabs.Select(t => t.tab.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // The 'API key' tab after every person's (M.ORCH-T630).
        loginTabs.SyncBy([.. tabs.Where(t => !t.IsApiKey), LoginTab.ApiKeyTab(all.Billing?.Mode == BillingModes.Api)], t => t.Key, v => v.tab.Key,
            t => t.IsGlobal ? this : new MainViewModel(this, t),
            (v, t) => v.Retitle(t));
        // The global login's tab is always first and always here, whatever the list said.
        if (!loginTabs.Contains(this)) loginTabs.Insert(0, this);
        else if (loginTabs.IndexOf(this) != 0) loginTabs.Move(loginTabs.IndexOf(this), 0);
        if (selectedTab is not null && !loginTabs.Contains(selectedTab)) Select(this);
        if (before != loginTabs.Count) foreach (var t in loginTabs) t.Raise(nameof(HasLoginTabs));

        var loginsChanged = !keysBefore.SetEquals(loginTabs.Select(t => t.tab.Key));
        var options = aggregator.Options;
        foreach (var t in loginTabs.ToList())
        {
            // The 'API key' tab is no login's: scoped to a key no session runs on, it has no repos' sessions or toasts of its own.
            t.Apply(all, LoginScope.For(all, t.IsApiKeyTab ? LoginTab.ApiKeyKey : t.ScopeEmail, options.WorkingWindow, options.WaitingWindow));
            t.RaiseTabStatus();
        }
        // A login's tab come or gone takes its panes with it (to the global login's when gone).
        // (a tab gone and another arrived in one refresh leaves the count alone, so compare the logins themselves.)
        if (loginsChanged) SyncTerminals();
        OpenLaunchers();
    }

    /// <summary>The logins whose Launcher tab this dashboard has opened (or tried to): each once per run, so one you close
    /// or exit stays closed until you ask for it again with Open Launcher.</summary>
    private readonly HashSet<string> launchersOpened = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// One terminal tab per account (M.ORCH-T699): every signed-in login's tab gets a pane on its Terminals tab running the
    /// Launcher already signed in to it, left idle for you to drive, opened quietly so it doesn't take the window. A login
    /// that already has its Launcher tab keeps it rather than getting a second.
    /// </summary>
    private void OpenLaunchers()
    {
        if (!IsGlobalTab) return;
        foreach (var t in loginTabs.ToList())
        {
            if (!t.IsTabSignedIn || !launchersOpened.Add(t.tab.Key)) continue;
            if (control.LauncherPaneRequest(t.ScopeEmail, out var error) is { } request) error = AllTerminals.OpenLauncher(request, show: false);
            if (error is not null) OrchestratorLog.For("Terminals").Warning("Couldn't open {Login:l}'s Launcher tab: {Error:l}", t.TabTitle, error);
        }
    }

    // ── Each person's terminals (M.ORCH-T622) ─────────────────────────────────

    private ICommand? showPaneRepoCommand;

    /// <summary>The tab whose Terminals tab <paramref name="pane"/> is on: its CLI's login's, else (no tab for that login) the global login's.</summary>
    private MainViewModel OwnerOf(TerminalPaneViewModel pane) =>
        loginTabs.FirstOrDefault(t => string.Equals(t.tab.Key, pane.Login ?? "", StringComparison.OrdinalIgnoreCase)) ?? this;

    /// <summary>Hands each login's tab its own panes (on the global login's tab, which holds them all), then shows the chosen one.</summary>
    private void SyncTerminals()
    {
        if (!IsGlobalTab) { Root.SyncTerminals(); return; }
        var owners = AllTerminals.Panes.ToLookup(OwnerOf);
        foreach (var t in loginTabs) t.Terminals.SyncTo([.. owners[t]]);
        ShowChosenPane();
    }

    /// <summary>The one pane on screen is the shown person's chosen one; every other pane's terminal is hidden.</summary>
    private void ShowChosenPane()
    {
        if (!IsGlobalTab) { Root.ShowChosenPane(); return; }
        AllTerminals.SelectedPane = SelectedTab.Terminals.SelectedPane;
    }

    /// <summary>Brings <paramref name="pane"/> up (a pane just opened, Go to tab, a toast): its person's tab, their Terminals
    /// tab, and it chosen there.</summary>
    internal void ShowPane(TerminalPaneViewModel pane)
    {
        if (!IsGlobalTab) { Root.ShowPane(pane); return; }
        SyncTerminals();
        var owner = OwnerOf(pane);
        Select(owner);
        owner.Page = TerminalsPage;
        owner.Terminals.SelectedPane = pane;
        ShowChosenPane();
    }

    /// <summary>"Go to" on a terminal tab: its repo, on this person's dashboard (its detail, with its tickets and Start).</summary>
    public ICommand ShowPaneRepoCommand => showPaneRepoCommand ??= new RelayCommand(p =>
    {
        if (p is TerminalPaneViewModel pane) SelectRepo(pane.Request.Repo);
    });

    private void Retitle(LoginTab next)
    {
        if (next == tab) return;
        tab = next;
        Raise(nameof(Tab)); Raise(nameof(TabTitle)); Raise(nameof(TabTip)); Raise(nameof(TabBrush));
        Raise(nameof(IsTabSignedIn)); Raise(nameof(ShowsDashboard)); Raise(nameof(SignInPrompt)); Raise(nameof(TerminalsVisibility));
        Raise(nameof(IsApiKeyTab)); Raise(nameof(IsPersonTab)); Raise(nameof(ShowsSignIn)); RaiseTabStatus();
        CommandManager.InvalidateRequerySuggested();
    }

    private ICommand? openLauncherCommand, signInTabCommand;

    /// <summary>Whether this tab's login is signed in (M.ORCH-T615). A roster account that isn't yet has a tab showing only its
    /// Sign in; once signed in, the next refresh gives it the whole dashboard and Open Launcher.</summary>
    public bool IsTabSignedIn => tab.SignedIn && !tab.IsApiKey;

    /// <summary>A person's tab (signed in or not), not the 'API key' tab (M.ORCH-T630), which shows only its billing panel.</summary>
    public bool IsPersonTab => !tab.IsApiKey;

    /// <summary>The panel with only its Sign in: on a person's tab not signed in yet.</summary>
    public bool ShowsSignIn => !tab.SignedIn;

    /// <summary>What a tab not signed in yet says above its Sign in.</summary>
    public string SignInPrompt => tab.SignedIn ? "" :
        $"{tab.Title} ({tab.Email}) isn't signed in on this machine yet. Sign in to see its dashboard and open the Launcher as {tab.Title}.";

    /// <summary>Open Launcher (M.ORCH-T615): the Launcher's menu as this tab's login (<c>MindAttic.Launcher --account &lt;email&gt;</c>),
    /// so every repo CLI started from it runs on that login and shows in this tab; on the global login's tab, the Launcher
    /// unscoped. One login may own many CLIs. It brings up the login's own Launcher tab on its Terminals (M.ORCH-T699), the
    /// one already open when there is one; Windows Terminal only when a pane can't be made.</summary>
    public ICommand OpenLauncherCommand => openLauncherCommand ??= new RelayCommand(OpenLauncher, () => tab.SignedIn && !tab.IsApiKey);

    /// <summary>Sign in, on a tab whose login isn't signed in yet: its own sign-in in a private browser window, then the tab
    /// becomes its dashboard.</summary>
    public ICommand SignInTabCommand => signInTabCommand ??= new RelayCommand(async () =>
    {
        if (ScopeEmail is not { } email) return;
        if (await SignInOwnAsync(email)) await RefreshAsync();
    }, () => !tab.SignedIn && ScopeEmail is not null && !IsSigningIn);

    private void OpenLauncher()
    {
        // Its own terminal tab here (M.ORCH-T699), the one it has reused rather than a second; Windows Terminal only when a
        // pane can't be made.
        if (control.LauncherPaneRequest(ScopeEmail, out var error) is { } request && (error = AllTerminals.OpenLauncher(request, show: true)) is null)
        {
            Root.launchersOpened.Add(tab.Key);
            StatusMessage = ScopeEmail is null
                ? "The Launcher on the global login is on this tab's Terminals."
                : $"The Launcher as {TabTitle} is on this tab's Terminals: every repo CLI started from it runs on {ScopeEmail} and shows in this tab.";
            return;
        }
        if (error is not null) OrchestratorLog.For("Terminals").Warning("No Launcher pane for {Login:l}: {Error:l}; opening it in Windows Terminal", TabTitle, error);
        error = control.OpenLauncher(ScopeEmail);
        StatusMessage = error ?? (ScopeEmail is null
            ? "Opened the Launcher on the global login."
            : $"Opened the Launcher as {TabTitle}: every repo CLI started from it runs on {ScopeEmail} and shows in this tab.");
        if (error is not null) Toasts.Insert(0, new ToastViewModel("Launcher not opened", error, Palette.Over, null));
    }

    /// <summary>Tags each new toast with this tab while there's more than one (toasts tagged by tab), and tells the window.</summary>
    private void OnToastsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems is null) return;
        foreach (var t in e.NewItems.OfType<ToastViewModel>())
        {
            if (Root.loginTabs.Count > 1) t.Tab ??= TabTitle;
            Root.ToastRaised?.Invoke(this, t);
        }
    }

    private static readonly Dictionary<string, Brush> brushes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A login's tab color ("#2E7D32") as a frozen brush; the theme's muted brush when it isn't one.</summary>
    internal static Brush BrushOf(string? color)
    {
        if (string.IsNullOrEmpty(color)) return Palette.Quiet;
        if (brushes.TryGetValue(color, out var known)) return known;
        Brush brush;
        try { brush = (Brush)new BrushConverter().ConvertFromInvariantString(color)!; }
        catch (Exception ex) when (ex is FormatException or NotSupportedException or InvalidOperationException) { return Palette.Quiet; }
        brush.Freeze();
        return brushes[color] = brush;
    }
}
