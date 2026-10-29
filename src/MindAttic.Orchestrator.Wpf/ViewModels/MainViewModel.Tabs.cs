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

    /// <summary>What fit on no person's page (M.ORCH-T763), now shown on the global login's tab instead of its own
    /// 'Miscellaneous' tab (M.ORCH-T825): allocate and the pace's slots, a pacing hold, reminders, finished work and the
    /// workspace's problems.</summary>
    public bool ShowsWorkspaceExtras => IsGlobalTab;

    public bool ShowsReminders => IsGlobalTab && HasReminders;

    public bool ShowsWarningsSection => IsGlobalTab && HasWarnings;

    public bool ShowsHold => IsGlobalTab && HasHold;

    /// <summary>The login's name ("Jordan", "Ryan (fallback)").</summary>
    public string TabTitle => tab.Title;

    /// <summary>Its title, its status icon's word (M.ORCH-T757) and its light's word (M.ORCH-T630: colour is never the only cue), its week left, its email and which
    /// login it is, and, with its red flag (M.ORCH-T790), how many of its CLIs are blocked waiting on your answer.</summary>
    public string TabTip =>
        $"{tab.Title}: {TabActivityWord}, {TabStatusWord}" + (TabTrailing.Length > 0 ? $", {TabTrailing}" : "") + $". {tab.ToolTip}." + TabStaleLine + TabCutoffsLine +
        (tab.SignedIn && !tab.IsApiKey ? ResetsLineOf(FiveHourResetUtc, WeekReset, DateTime.UtcNow) : "") +
        NeedsYouLine(TabAskingCount);

    /// <summary>What hovering a person's tab shows (M.ORCH-T1120): only their cutoffs and when their week resets, e.g. "Cutoffs:
    /// 5-hour 98%, weekly 95%. Week resets Tue 10/27 5:00 PM (in 7hr 3m)." A tab with no cutoffs (signed out, the API key,
    /// the global login's) keeps the full <see cref="TabTip"/>, which stays the accessible name of every tab.</summary>
    public string TabHoverTip => TabHoverTipOf(TabCutoffsLine, WeekReset, DateTime.UtcNow) ?? TabTip;

    internal static string? TabHoverTipOf(string cutoffsLine, DateTime? weekResetUtc, DateTime nowUtc) =>
        cutoffsLine.Length == 0 ? null : (cutoffsLine + ResetsLineOf(null, weekResetUtc, nowUtc)).Trim();

    /// <summary>When this login's 5-hour window resets: the pushed reading's while it's the one shown, else its budget's block, else the roster's.</summary>
    private DateTime? FiveHourResetUtc => (LiveFiveHour is not null ? Live?.FiveHourResetUtc : null)
        ?? Budget?.CurrentBlock?.EndUtc ?? OwnRow?.FiveHourResetAt(DateTime.UtcNow);

    /// <summary>" 5-hour resets Mon 11/2 10:00 AM (in 2d 3hr 14m); week resets ... (in ...)." (M.ORCH-T1102); a time not known,
    /// or already past, is left out, and with neither it's empty.</summary>
    internal static string ResetsLineOf(DateTime? fiveHourUtc, DateTime? weekUtc, DateTime nowUtc)
    {
        string? Part(string what, DateTime? at) => at is { } t && t > nowUtc
            ? $"{what} resets {t.ToLocalTime().ToString("ddd M/d h:mm tt", System.Globalization.CultureInfo.InvariantCulture)} (in {Countdown(t - nowUtc)})"
            : null;
        var parts = new[] { Part("5-hour", fiveHourUtc), Part("week", weekUtc) }.OfType<string>().ToList();
        return parts.Count == 0 ? "" : " " + char.ToUpperInvariant(parts[0][0]) + string.Join("; ", parts)[1..] + ".";
    }

    /// <summary>"2d 3hr 14m", "3hr 14m", "14m": the time left, days and hours only when there are any, rounded down to the minute.</summary>
    internal static string Countdown(TimeSpan left)
    {
        if (left < TimeSpan.Zero) left = TimeSpan.Zero;
        var parts = new List<string>();
        if (left.Days > 0) parts.Add($"{left.Days}d");
        if (left.Days > 0 || left.Hours > 0) parts.Add($"{left.Hours}hr");
        parts.Add($"{left.Minutes}m");
        return string.Join(' ', parts);
    }

    /// <summary>How many of this tab's CLIs are genuinely blocked on you (M.ORCH-T790): holding on a permission prompt or a question,
    /// or stopped for a /crossrepo or /allocate only you may type. Not the old idle count: a turn that simply ended isn't this.</summary>
    public int TabAskingCount => IsPersonTab ? AskingOf(snapshot?.Agents) : 0;

    /// <summary>The red flag at the end of a person's tab line (M.ORCH-T790): shown only while one of its CLIs is blocked on you,
    /// gone as soon as they're all running again. A flag, not a count; the count is in the tooltip.</summary>
    public bool TabNeedsYou => TabAskingCount > 0;

    /// <summary>The red flag's own tooltip (M.ORCH-T790), shown on hovering the glyph itself rather than the whole tab: what it means
    /// and how many CLIs it covers, e.g. "Blocked waiting on you: a CLI here is blocked waiting on your answer."</summary>
    public string TabNeedsYouTip => "Blocked waiting on you:" + NeedsYouLine(TabAskingCount);

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

    /// <summary>What the tab strip shows selected: <see cref="SelectedTab"/>, except null while the strip is emptied (M.ORCH-T1019),
    /// so the strip lets go of its selected item before that item leaves its bound collection (the crash of M.ORCH-T859).</summary>
    public MainViewModel? StripSelection
    {
        get => Root.stripCleared ? null : SelectedTab;
        set
        {
            if (value is not null) Root.Select(value);
            // Ctrl+click on the selected pill writes null: the strip is told it stays selected once the write is done (M.ORCH-T1024).
            else if (!Root.stripCleared && Root.orgTabs.Contains(SelectedTab)) SynchronizationContext.Current?.Post(_ => { foreach (var t in Root.loginTabs) t.Raise(nameof(StripSelection)); }, null);
        }
    }

    private bool stripCleared;

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
        foreach (var t in loginTabs) { t.Raise(nameof(SelectedTab)); t.Raise(nameof(StripSelection)); }
        SelectedTabChanged?.Invoke(this, EventArgs.Empty);
        // The person shown is the one whose chosen terminal shows (M.ORCH-T810).
        ShowChosenPane();
    }

    private void Step(int by)
    {
        // The API key is a person like any other now (M.ORCH-T906): it's cycled to with the rest.
        var steppable = orgTabs.ToList();
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
    /// <summary>The order to apply each tab's share of a refresh in (M.ORCH-T1273): <paramref name="root"/> first, whatever
    /// position <paramref name="tabs"/> itself sits it at for display (<see cref="ApplyTabs"/>, M.ORCH-T752 sorts the strip by
    /// name). The global login's tab is the only one whose <c>ApplyAccounts</c> rereads the settings store; every other tab
    /// either copies what it just read or reads it live through <c>Root</c>, so applying one of them first would show it the
    /// settings as they stood before this refresh until some later, unrelated push or poll happened to run another pass.</summary>
    internal static IEnumerable<T> ApplyOrderOf<T>(IEnumerable<T> tabs, T root) => tabs.OrderBy(t => ReferenceEquals(t, root) ? 0 : 1);

    private void ApplyTabs(Snapshot all, IReadOnlyList<LoginTab> tabs, OrgDirectory nextOrgs)
    {
        workspace = all;
        ApplyOrgs(nextOrgs);
        var before = loginTabs.Count;
        var previous = loginTabs.ToList();
        // Who was signed in at the last refresh, so a login disconnected since opens its sign-in by itself (M.ORCH-T796).
        var wasSignedIn = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in previous) wasSignedIn.TryAdd(t.tab.Key, t.tab.SignedIn);
        // The API key's person tab comes in the list itself, after every Claude login's (M.ORCH-T906). No more 'Miscellaneous'
        // tab (M.ORCH-T825): its content (allocate, a pacing hold, reminders, finished work, the workspace's problems) now shows
        // on the global login's tab.
        loginTabs.SyncBy([.. tabs],
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
        // The global login's tab applied first (M.ORCH-T1273): ApplyAccounts's settings read (symbolTabLabels, the pace and
        // weekly-stop percents, the blink tokens, the cutoffs and allowances) lands on it alone, and every other tab either
        // copies it or reads it live through Root -- stale until the global tab's own turn otherwise, which a changed Save
        // could leave showing the old figures until some unrelated push or poll happened to run another pass. Its place in
        // this loop is no longer tied to loginTabs' own order, which sits the tab by name since M.ORCH-T752.
        foreach (var t in ApplyOrderOf(loginTabs.ToList(), this))
        {
            var org = t.OrgId;
            if (!byOrg.TryGetValue(org, out var orgAll)) byOrg[org] = orgAll = OrgScope.For(all, orgs, org, options.WorkingWindow, options.WaitingWindow);
            // The API key's person is scoped to its own login folder's sessions, as any per-CLI login (M.ORCH-T906).
            t.Apply(orgAll, LoginScope.For(orgAll, t.ScopeEmail, options.WorkingWindow, options.WaitingWindow));
            // Its blinks from the token count this refresh read (M.ORCH-T757): no read of its own.
            t.CountTabTokens(now);
            t.RaiseTabStatus();
        }
        UpdateOrgPills();
        // A login's tab come or gone takes its terminals with it (to the global login's when gone), M.ORCH-T810.
        SyncTerminals();
        OpenLaunchers();
        if (NewlyDisconnected(wasSignedIn, loginTabs.Select(t => t.tab)) is { Count: > 0 } disconnected) AutoSignIn(disconnected);
    }

    /// <summary>The logins whose Launcher tab this dashboard has opened (or tried to): each once while it stays open: one you
    /// close or exit is reopened by the next refresh (M.ORCH-T855: the Launcher tab is always open). A fallback open in its own
    /// window, never embedded, is held, so a failing embed doesn't spawn a window every tick.</summary>
    private readonly HashSet<string> launchersOpened = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> launchersEmbedded = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// One Launcher tab per account (M.ORCH-T699), embedded first in its Terminals panel (M.LNCH-T77): every signed-in login
    /// gets its Launcher's menu already signed in to it, left idle for you to drive. A login whose Launcher tab is already
    /// open (embedded, or a standalone one running outside the dashboard) keeps it rather than getting a second.
    /// </summary>
    private void OpenLaunchers()
    {
        if (!IsGlobalTab) return;
        foreach (var t in loginTabs)
        {
            var key = t.tab.Key;
            var live = AllTerminals.Panes.Any(p => p.Request.IsMenu && !p.Ended &&
                string.Equals(p.Login, AccountDirs.Normalize(t.ScopeEmail), StringComparison.OrdinalIgnoreCase));
            if (live) launchersEmbedded.Add(key);
            else if (launchersEmbedded.Remove(key)) launchersOpened.Remove(key);
        }
        if (ShuttingDown) return;
        // Through the same cap as a last tab's respawn (M.ORCH-T1150): a Launcher that exits at once is reopened at most every 30s.
        var now = DateTime.UtcNow;
        var wanted = loginTabs.Where(t => t.IsTabSignedIn && !launchersOpened.Contains(t.tab.Key) && launcherRespawn.TryTake(t.tab.Key, now))
            .Select(t => { launchersOpened.Add(t.tab.Key); CloseEndedLaunchers(t); return (t.tab.Key, t.ScopeEmail, t.TabTitle); }).ToList();
        foreach (var (key, email, title) in wanted) _ = OpenLauncherQuietlyAsync(key, email, title);
    }

    /// <summary>Opens <paramref name="key"/>'s Launcher and logs why, quietly, on a failure — which also undoes the optimistic
    /// <see cref="launchersOpened"/> mark from whichever caller is about to fire-and-forget this (M.ORCH-T1192): left marked
    /// after a failed open, a login's last tab closing could never spawn another Launcher for it again, since every later
    /// refresh's own retry (<see cref="OpenLaunchers"/>) skips a key already in <see cref="launchersOpened"/>.</summary>
    private async Task OpenLauncherQuietlyAsync(string key, string? email, string title, bool embedOnly = false)
    {
        var (error, _) = await OpenLauncherPaneAsync(email, title, embedOnly);
        if (error is { } e)
        {
            OrchestratorLog.For("Launcher").Warning("Couldn't open {Login:l}'s Launcher tab: {Error:l}", title, e);
            launchersOpened.Remove(key);
        }
    }

    /// <summary>M.ORCH-T1153: a new Launcher for <paramref name="person"/> replaces their ended Launcher tabs rather than piling up
    /// beside them. Closing one already ended is no tab gone (<see cref="OnTabGone"/> counted it when it ended).</summary>
    private void CloseEndedLaunchers(MainViewModel person)
    {
        foreach (var pane in LauncherRespawn.Replaced(AllTerminals.Panes, p => p.Request.IsMenu, p => p.Ended, p => ReferenceEquals(OwnerOf(p), person)))
        {
            // Counted as gone already, so closing it can't run the last-tab check again for the Launcher replacing it.
            if (!tabsGone.TryGetValue(pane, out _)) tabsGone.Add(pane, null);
            AllTerminals.Close(pane);
        }
    }

    private readonly LauncherRespawn launcherRespawn = new();
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<TerminalPaneViewModel, object?> tabsGone = new();

    /// <summary>The dashboard is closing (its window's Closing): no Launcher is opened, by a refresh or a last tab's close.</summary>
    public bool ShuttingDown { get => Root.shuttingDown; set => Root.shuttingDown = value; }
    private bool shuttingDown;

    /// <summary>The dashboard is reattaching and restoring its tabs at start: a tab ended or closed meanwhile spawns no Launcher.</summary>
    public bool RestoringTerminals { get => Root.restoringTerminals; set => Root.restoringTerminals = value; }
    private bool restoringTerminals;

    /// <summary>M.ORCH-T1150: every pane's close, and its CLI's exit, is checked for leaving its person with no live tab.</summary>
    private void WatchForLastTab()
    {
        AllTerminals.Panes.CollectionChanged += (_, e) =>
        {
            foreach (var added in e.NewItems?.OfType<TerminalPaneViewModel>() ?? [])
                added.PropertyChanged += (s, p) => { if (p.PropertyName == nameof(TerminalPaneViewModel.Ended) && s is TerminalPaneViewModel { Ended: true } ended) OnTabGone(ended); };
            foreach (var gone in e.OldItems?.OfType<TerminalPaneViewModel>() ?? []) OnTabGone(gone);
        };
    }

    /// <summary>
    /// <paramref name="pane"/> was closed or its CLI exited (M.ORCH-T1150): when that leaves its person, signed in, with no live
    /// tab, a new Launcher signed in as them opens embedded on their page (at most once per 30s each, logged), never during
    /// shutdown or the start's restore. A Launcher held as open elsewhere (a standalone window, a fallback) is let go of, since
    /// their page would otherwise stay empty.
    /// </summary>
    private void OnTabGone(TerminalPaneViewModel pane)
    {
        if (!IsGlobalTab) { Root.OnTabGone(pane); return; }
        // Its CLI's exit and then its close are one tab gone, counted once.
        if (tabsGone.TryGetValue(pane, out _)) return;
        tabsGone.Add(pane, null);
        try
        {
            var owner = OwnerOf(pane);
            var key = owner.tab.Key;
            var hasLive = AllTerminals.Panes.Any(p => !p.Ended && ReferenceEquals(OwnerOf(p), owner));
            var outcome = launcherRespawn.Decide(key, owner.IsTabSignedIn, hasLive, ShuttingDown, RestoringTerminals, DateTime.UtcNow);
            var log = OrchestratorLog.For("Launcher");
            switch (outcome)
            {
                case LauncherRespawnOutcome.Spawn:
                    log.Information("{Login:l}'s last tab ({Title:l}) is gone: opening a new Launcher as them", owner.TabTitle, pane.Title);
                    launchersEmbedded.Remove(key);
                    launchersOpened.Add(key);
                    CloseEndedLaunchers(owner);
                    _ = OpenLauncherQuietlyAsync(key, owner.ScopeEmail, owner.TabTitle, embedOnly: true);
                    break;
                case LauncherRespawnOutcome.Capped:
                    // Let the refresh reopen it once the cap is up.
                    launchersEmbedded.Remove(key);
                    launchersOpened.Remove(key);
                    log.Warning("{Login:l}'s last tab ({Title:l}) is gone, but a Launcher opened for them under 30s ago: the next one waits", owner.TabTitle, pane.Title);
                    break;
            }
        }
        catch (Exception ex) { OrchestratorLog.For("Launcher").Error(ex, "Respawning a Launcher after a last tab closed failed; contained"); }
    }

    /// <summary>Opens <paramref name="email"/>'s Launcher menu (M.LNCH-T77): embedded as a Terminals panel tab, first among
    /// that login's (so every CLI it opens, seeing itself hosted, routes back into the panel too); one already open there is
    /// brought up rather than getting a second, and so is a standalone one already running outside the dashboard (from
    /// before this, or started by hand) -- off the UI thread, since finding it asks WMI. Embedding failing (rare) falls back
    /// to the login's own Windows Terminal window, as every Launcher tab opened before M.LNCH-T77. Returns an error for a
    /// toast (null on success, embedded or not) and a status line either way.</summary>
    private async Task<(string? Error, string Status)> OpenLauncherPaneAsync(string? email, string tabTitle, bool embedOnly = false)
    {
        var normalized = AccountDirs.Normalize(email);
        if (Root.AllTerminals.Panes.FirstOrDefault(p => p.Request.IsMenu && !p.Ended &&
                string.Equals(p.Login, normalized, StringComparison.OrdinalIgnoreCase)) is { } open)
        {
            Root.ShowPane(open);
            return (null, $"The Launcher as {tabTitle} was already open.");
        }
        // The API key's person (M.ORCH-T906): its login folder is made ready first (the Vault key approved there, its look
        // marked "billing": "api"), so its Launcher runs as the key and every CLI it starts bills API credits. The key itself is
        // read from the Vault by the Launcher at each CLI's launch; it's never in this pane's command line or environment.
        if (ApiKeyPerson.Is(email) && await Task.Run(() => ApiKeyPerson.Prepare(new AccountDirs(), aggregator.BillingSwitch)) is { } notReady)
            return (notReady, notReady);
        // A last tab's respawn (M.ORCH-T1150) wants a tab on the person's page, not a standalone Launcher brought up.
        var running = embedOnly ? null : await Task.Run(() => control.RunningLauncherOf(email));
        if (running is { } pid)
        {
            var title = control.LauncherTitleOf(email);
            var result = await Task.Run(() => TerminalFocus.GoTo(new HostSession("Claude", pid, "", null) { Title = title }, []));
            return (null, result.Outcome == GoToOutcome.NotFound
                ? $"The Launcher as {tabTitle} is already open (\"{title}\"): {result.Note}"
                : $"The Launcher as {tabTitle} was already open: {result.Note}");
        }
        if (Root.OpenTerminal(control.LauncherPaneRequest(email)) is not { } embedError)
            return (null, email is null
                ? "Opened the Launcher on the global login."
                : $"Opened the Launcher as {tabTitle}: every repo CLI started from it runs on {email} and shows in this tab.");
        var fallback = await Task.Run(() => control.OpenLauncher(email, out _));
        return fallback is not null ? (fallback, fallback) : (null, $"Opened the Launcher as {tabTitle} in its own window.");
    }

    private void Retitle(LoginTab next)
    {
        if (next == tab) return;
        tab = next;
        Raise(nameof(Tab)); Raise(nameof(TabTitle)); Raise(nameof(TabTip)); Raise(nameof(TabHoverTip)); Raise(nameof(TabBrush));
        Raise(nameof(IsTabSignedIn)); Raise(nameof(SignInPrompt));
        Raise(nameof(IsApiKeyTab)); Raise(nameof(IsPersonTab)); Raise(nameof(ShowsSignIn)); RaiseTabStatus();
        CommandManager.InvalidateRequerySuggested();
    }

    private ICommand? signInTabCommand;

    /// <summary>Whether this tab's login is signed in (M.ORCH-T615). A roster account that isn't yet has a tab showing only its
    /// Sign in; once signed in, the next refresh gives it the whole dashboard and Open Launcher.</summary>
    /// <para>The API key's person (M.ORCH-T906) is "signed in" while the MindAttic Vault holds a Claude key.</para>
    public bool IsTabSignedIn => tab.SignedIn;

    /// <summary>A person's tab, signed in or not: every tab is one since the API key became a person too (M.ORCH-T906).</summary>
    public bool IsPersonTab => true;

    /// <summary>The panel with only its Sign in: on a person's tab not signed in yet.</summary>
    public bool ShowsSignIn => !tab.SignedIn;

    /// <summary>What a tab not signed in yet says above its Sign in.</summary>
    public string SignInPrompt => tab.SignedIn ? ""
        : tab.IsApiKey ? $"There's no {BillingSwitch.VaultProvider} key in the MindAttic Vault keyring. Add one there (MindAttic.Vault, or the Launcher's Settings) and the API key's page and Launcher open at the next refresh."
        : $"{tab.Title} ({tab.Email}) isn't signed in on this machine yet. Sign in to see its dashboard and open the Launcher as {tab.Title}.";

    /// <summary>Sign in, on a tab whose login isn't signed in yet: its own sign-in in a private browser window, then the tab
    /// becomes its dashboard.</summary>
    public ICommand SignInTabCommand => signInTabCommand ??= new RelayCommand(async () =>
    {
        if (ScopeEmail is not { } email) return;
        if (await SignInOwnAsync(email)) await RefreshAsync();
    }, () => !tab.SignedIn && !tab.IsApiKey && ScopeEmail is not null && !IsSigningIn);

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
