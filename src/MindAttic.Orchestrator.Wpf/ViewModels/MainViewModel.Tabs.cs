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

    /// <summary>Its title, its status icon's word (M.ORCH-T757) and its light's word (M.ORCH-T630: colour is never the only cue), its week left, its email and which
    /// login it is, and, with its red flag (M.ORCH-T790), how many of its CLIs are blocked waiting on your answer.</summary>
    public string TabTip => tab.IsMisc ? $"{tab.Title}: {tab.ToolTip}." :
        $"{tab.Title}: {TabActivityWord}, {TabStatusWord}" + $", {TabTrailing}" + $". {tab.ToolTip}." +
        NeedsYouLine(TabAskingCount);

    /// <summary>How many of this tab's CLIs are genuinely blocked on you (M.ORCH-T790): holding on a permission prompt or a question,
    /// or stopped for a /crossrepo or /allocate only you may type. Not the old idle count: a turn that simply ended isn't this.</summary>
    public int TabAskingCount => IsPersonTab ? AskingOf(snapshot?.Agents) : 0;

    /// <summary>The red flag at the end of a person's tab line (M.ORCH-T790): shown only while one of its CLIs is blocked on you,
    /// gone as soon as they're all running again. A flag, not a count; the count is in the tooltip.</summary>
    public bool TabNeedsYou => TabAskingCount > 0;

    internal static int AskingOf(IEnumerable<MindAttic.Orchestrator.Activity.AgentBoardRow>? agents) => agents?.Count(a => a.Asking) ?? 0;

    /// <summary>The flag in words for the tooltip and the accessible name, so it's never only a picture; empty with none blocked.</summary>
    internal static string NeedsYouLine(int asking) => asking switch
    {
        0 => "",
        1 => " A CLI here is blocked waiting on your answer.",
        _ => $" {asking} CLIs here are blocked waiting on your answer.",
    };

    /// <summary>The login's terminal tab color (M.ORCH-T616), the tab's accent; the theme's muted brush when it has none.</summary>
    public Brush TabBrush => BrushOf(tab.Color);

    /// <summary>Every login's tab, the global login's first; the same list in every tab.</summary>
    public ObservableCollection<MainViewModel> LoginTabs => Root.loginTabs;

    /// <summary>The tab strip shows once the shown org has a second login (with only the global one it would be a lone tab), or
    /// once there's a second org (M.ORCH-T655), so an org's lone person is still named under the org switcher.</summary>
    public bool HasLoginTabs => OrgTabs.Count > 1 || (HasOrgs && OrgTabs.Count > 0);

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

    /// <summary>Ctrl+Tab and Ctrl+Shift+Tab: the next and the previous login's tab in the shown org.</summary>
    public ICommand NextTabCommand => nextTabCommand ??= new RelayCommand(() => Root.Step(+1));
    public ICommand PreviousTabCommand => previousTabCommand ??= new RelayCommand(() => Root.Step(-1));

    private void Select(MainViewModel? next)
    {
        if (next is null || ReferenceEquals(next, SelectedTab) || !loginTabs.Contains(next)) return;
        selectedTab = next;
        foreach (var t in loginTabs) t.Raise(nameof(SelectedTab));
        SelectedTabChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Step(int by)
    {
        // The 'API key' tab is never cycled to (M.ORCH-T770): it isn't a person, and it's hidden from the strip, so landing on
        // it here would select a tab with no visible header to show it was selected.
        var steppable = orgTabs.Where(t => !t.IsApiKeyTab).ToList();
        if (steppable.Count < 2) return;
        var at = steppable.IndexOf(SelectedTab);
        Select(steppable[((at + by) % steppable.Count + steppable.Count) % steppable.Count]);
    }

    /// <summary>
    /// Keeps a tab per login in <paramref name="tabs"/> (in its order, by name, the 'API key' one last), adding a tab for a login just
    /// signed in and dropping one signed out (back to the global login's tab if it was showing), then hands each tab its own
    /// view of <paramref name="all"/>, this one first: the others reuse the roster it reads. Each tab's view is cut from its
    /// org's (M.ORCH-T655: <see cref="OrgScope"/>), and the strip lists the shown org's tabs.
    /// </summary>
    private void ApplyTabs(Snapshot all, IReadOnlyList<LoginTab> tabs, OrgDirectory nextOrgs)
    {
        workspace = all;
        ApplyOrgs(nextOrgs);
        var before = loginTabs.Count;
        var previous = loginTabs.ToList();
        // Who was signed in at the last refresh, so a login disconnected since opens its sign-in by itself (M.ORCH-T796).
        var wasSignedIn = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in previous) wasSignedIn.TryAdd(t.tab.Key, t.tab.SignedIn);
        // The 'API key' tab after every person's (M.ORCH-T630), and 'Miscellaneous' at the very end (M.ORCH-T763).
        loginTabs.SyncBy([.. tabs.Where(t => !t.IsApiKey && !t.IsMisc), LoginTab.ApiKeyTab(all.Billing?.Mode == BillingModes.Api), LoginTab.Misc],
            t => t.Key, v => v.tab.Key,
            t => t.IsGlobal ? this : new MainViewModel(this, t),
            (v, t) => v.Retitle(t));
        // The global login's tab is always here, whatever the list said; it sits by its name among the others (M.ORCH-T752).
        if (!loginTabs.Contains(this)) loginTabs.Insert(0, this);
        if (selectedTab is not null && !loginTabs.Contains(selectedTab)) Select(this);
        // A tab signed out is gone for good: it stops listening to the static move events (M.ORCH-T734).
        foreach (var gone in previous.Where(t => !ReferenceEquals(t, this) && !loginTabs.Contains(t))) gone.Detach();
        SyncOrgTabs();

        var options = aggregator.Options;
        // One view per org, cut once and shared by its tabs.
        var byOrg = new Dictionary<string, Snapshot>(StringComparer.Ordinal);
        var now = DateTime.UtcNow;
        foreach (var t in loginTabs.ToList())
        {
            var org = t.OrgId;
            if (!byOrg.TryGetValue(org, out var orgAll)) byOrg[org] = orgAll = OrgScope.For(all, orgs, org, options.WorkingWindow, options.WaitingWindow);
            // The 'API key' and 'Miscellaneous' tabs are no login's: scoped to a key no session runs on, they have no repos'
            // sessions or toasts of their own.
            t.Apply(orgAll, LoginScope.For(orgAll, t.IsApiKeyTab || t.IsMiscTab ? t.tab.Key : t.ScopeEmail, options.WorkingWindow, options.WaitingWindow));
            // Its blinks from the token count this refresh read (M.ORCH-T757): no read of its own.
            t.CountTabTokens(now);
            t.RaiseTabStatus();
        }
        UpdateOrgPills();
        OpenLaunchers();
        if (NewlyDisconnected(wasSignedIn, loginTabs.Select(t => t.tab)) is { Count: > 0 } disconnected) AutoSignIn(disconnected);
    }

    /// <summary>The logins whose Launcher tab this dashboard has opened (or tried to): each once per run, so one you close
    /// or exit stays closed until you ask for it again with Open Launcher.</summary>
    private readonly HashSet<string> launchersOpened = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// One terminal tab per account (M.ORCH-T699): every signed-in login gets a Windows Terminal tab, in its own window, running
    /// the Launcher already signed in to it, left idle for you to drive. A login whose Launcher is already running keeps it
    /// rather than getting a second. Off the UI thread: finding the running ones asks WMI.
    /// </summary>
    private void OpenLaunchers()
    {
        if (!IsGlobalTab) return;
        var wanted = loginTabs.Where(t => t.IsTabSignedIn && launchersOpened.Add(t.tab.Key)).Select(t => (t.ScopeEmail, t.TabTitle)).ToList();
        if (wanted.Count == 0) return;
        _ = Task.Run(() =>
        {
            foreach (var (email, title) in wanted)
                if (control.OpenLauncher(email, out _) is { } error)
                    OrchestratorLog.For("Launcher").Warning("Couldn't open {Login:l}'s Launcher tab: {Error:l}", title, error);
        });
    }

    private void Retitle(LoginTab next)
    {
        if (next == tab) return;
        tab = next;
        Raise(nameof(Tab)); Raise(nameof(TabTitle)); Raise(nameof(TabTip)); Raise(nameof(TabBrush));
        Raise(nameof(IsTabSignedIn)); Raise(nameof(SignInPrompt));
        Raise(nameof(IsApiKeyTab)); Raise(nameof(IsMiscTab)); Raise(nameof(IsPersonTab)); Raise(nameof(ShowsSignIn)); RaiseTabStatus();
        CommandManager.InvalidateRequerySuggested();
    }

    private ICommand? openLauncherCommand, signInTabCommand;

    /// <summary>Whether this tab's login is signed in (M.ORCH-T615). A roster account that isn't yet has a tab showing only its
    /// Sign in; once signed in, the next refresh gives it the whole dashboard and Open Launcher.</summary>
    public bool IsTabSignedIn => tab.SignedIn && !tab.IsApiKey && !tab.IsMisc;

    /// <summary>A person's tab (signed in or not), not the 'API key' tab (M.ORCH-T630), which shows only its billing panel, nor the
    /// 'Miscellaneous' one (M.ORCH-T763).</summary>
    public bool IsPersonTab => !tab.IsApiKey && !tab.IsMisc;

    /// <summary>The panel with only its Sign in: on a person's tab not signed in yet.</summary>
    public bool ShowsSignIn => !tab.SignedIn;

    /// <summary>What a tab not signed in yet says above its Sign in.</summary>
    public string SignInPrompt => tab.SignedIn ? "" :
        $"{tab.Title} ({tab.Email}) isn't signed in on this machine yet. Sign in to see its dashboard and open the Launcher as {tab.Title}.";

    /// <summary>Open Launcher (M.ORCH-T615): the Launcher's menu as this tab's login (<c>MindAttic.Launcher --account &lt;email&gt;</c>),
    /// so every repo CLI started from it runs on that login and shows in this tab; on the global login's tab, the Launcher
    /// unscoped. One login may own many CLIs. It opens in the login's own Windows Terminal window; a login whose Launcher is
    /// already running gets that one brought up rather than a second (M.ORCH-T699).</summary>
    public ICommand OpenLauncherCommand => openLauncherCommand ??= new RelayCommand(async () => await OpenLauncherAsync(),() => IsTabSignedIn);

    /// <summary>Sign in, on a tab whose login isn't signed in yet: its own sign-in in a private browser window, then the tab
    /// becomes its dashboard.</summary>
    public ICommand SignInTabCommand => signInTabCommand ??= new RelayCommand(async () =>
    {
        if (ScopeEmail is not { } email) return;
        if (await SignInOwnAsync(email)) await RefreshAsync();
    }, () => !tab.SignedIn && ScopeEmail is not null && !IsSigningIn);

    private async Task OpenLauncherAsync()
    {
        var email = ScopeEmail;
        Root.launchersOpened.Add(tab.Key);
        // Finding a running Launcher asks WMI, and bringing its tab up uses UI Automation over Windows Terminal: off the UI thread.
        var (error, running) = await Task.Run(() => (control.OpenLauncher(email, out var pid), pid));
        if (error is not null)
        {
            StatusMessage = error;
            Toasts.Insert(0, new ToastViewModel("Launcher not opened", error, Palette.Over, null));
            return;
        }
        if (running is { } pid)
        {
            var title = control.LauncherTitleOf(email);
            var result = await Task.Run(() => TerminalFocus.GoTo(new HostSession("Claude", pid, "", null) { Title = title }, []));
            StatusMessage = result.Outcome == GoToOutcome.NotFound
                ? $"The Launcher as {TabTitle} is already open (\"{title}\"): {result.Note}"
                : $"The Launcher as {TabTitle} was already open: {result.Note}";
            return;
        }
        StatusMessage = email is null
            ? "Opened the Launcher on the global login."
            : $"Opened the Launcher as {TabTitle}: every repo CLI started from it runs on {email} and shows in this tab.";
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
