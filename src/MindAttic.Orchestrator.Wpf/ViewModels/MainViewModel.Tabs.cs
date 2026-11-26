using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Media;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Billing;
using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// One dashboard tab per Claude login (M.ORCH-T613): the global <c>~/.claude</c> login's first, then each per-CLI login under
/// <c>~/.claude-accounts</c> and each org member, titled by its name (M.ORCH-T614) in its terminal tabs' color
/// (M.ORCH-T616); a login's tab is permanent (M.ORCH-T1950), shown signed out rather than removed when it signs out. Each tab is this whole view model, scoped to its
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

    /// <summary>The login this tab shows: its name, email and kind.</summary>
    public LoginTab Tab => tab;

    /// <summary>This tab's per-CLI login (its email); null on the global login's tab.</summary>
    public string? ScopeEmail => tab.Email;

    /// <summary>This is the global login's tab (it has no root): the one that holds the workspace-wide state.</summary>
    public bool IsGlobalTab => root is null;

    /// <summary>The login's name ("Jordan", "Ryan (fallback)").</summary>
    public string TabTitle => tab.Title;

    /// <summary>The name its row shows (M.ORCH-T1816): <see cref="TabTitle"/>, and on the API key's person which key it
    /// is, "API key (…4921)" or "API key (none)". Only the face: everything that names the login keeps TabTitle.</summary>
    public string TabFaceTitle => tab.IsApiKey ? ApiKeyTitleOf(tab.Title, Root.lastApiKeyTail) : tab.Title;

    /// <summary>"API key (…4921)"; "API key (none)" with no key in the Vault; the bare title for a key too short to show part of.</summary>
    internal static string ApiKeyTitleOf(string title, string? tail) =>
        tail is null ? $"{title} (none)" : tail.Length == 0 ? title : $"{title} (…{tail})";

    /// <summary>The API key's row's money (M.ORCH-T1816), where a Claude login's row has its usage bars: what it spent today
    /// and, with a starting credit set, that as a share of the credit, "$0.17 (3%)"; "" on every other row, with no key, or
    /// before the first read.</summary>
    public string ApiSpendFace => tab.IsApiKey && tab.SignedIn ? ApiSpendFaceOf(Spend) : "";

    /// <summary>"$0.17 (3%)", or "$0.17" with no starting credit to take a percent of.</summary>
    internal static string ApiSpendFaceOf(ApiSpend? spend) => spend is null ? ""
        : spend.CreditUsd is > 0 and var credit
            ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{ApiSpend.Usd(spend.TodayUsd)} ({spend.TodayUsd / credit * 100:0}%)")
            : ApiSpend.Usd(spend.TodayUsd);

    /// <summary>The API key's row's own tooltip line: what the figure beside its name means and what is left.</summary>
    internal string ApiSpendTipLine => !tab.IsApiKey || Spend is not { } s ? ""
        : s.CreditUsd is > 0 and var credit
            ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"\n• Today: {ApiSpend.Usd(s.TodayUsd)}, {s.TodayUsd / credit * 100:0}% of the {ApiSpend.Usd(credit)} starting credit; {ApiSpend.Usd(s.LeftUsd ?? 0)} left")
            : $"\n• Today: {ApiSpend.Usd(s.TodayUsd)} (no starting credit set: orchestrator billing credit <usd>)";

    /// <summary>Its title, its status icon's word (M.ORCH-T757) and its light's word (M.ORCH-T630: colour is never the only cue), its week left, its email and which
    /// login it is, and, with its red flag (M.ORCH-T790), how many of its CLIs are blocked waiting on your answer.</summary>
    public string TabTip =>
        $"{tab.Title}: {TabActivityWord}, {TabStatusWord}" + (TabTrailing.Length > 0 ? $", {TabTrailing}" : "") + $". {tab.ToolTip}." + TabStaleLine + TabCutoffsLine +
        (tab.SignedIn && !tab.IsApiKey ? ResetsLineOf(FiveHourResetUtc, WeekReset, DateTime.UtcNow) : "") +
        NeedsYouLine(TabAskingCount);

    /// <summary>What hovering a person's tab shows (M.ORCH-T1120, bulleted and given their open CLIs M.ORCH-T1424): its own
    /// first, plain line is the current Σ/λ percents (TabUsageLine, M.ORCH-T1536 - now that the tab face shows bars rather
    /// than those figures as text), then their cutoffs, when their 5-hour window and week reset, and the names of their open
    /// CLI tabs, each its own bullet line, e.g. "Σ: 15% | λ: 29%\n• Cutoffs: 5-hour 98%, weekly 95%\n• 5-hour reset: Tue
    /// 10/27 5:00 PM (in 2hr 17m)\n• Weekly reset: Tue 10/27 5:00 PM (in 7hr 3m)\n• Active CLIs: repo-foo". Shown with no
    /// hover delay (MainWindow.xaml's ToolTipService.InitialShowDelay="0" on the tab strip) so it's there to read the
    /// instant the pointer lands, not after the usual tooltip pause. A tab with no cutoffs (signed out, the API key, the
    /// global login's) keeps the full <see cref="TabTip"/>, which stays the accessible name of every tab.</summary>
    public string TabHoverTip => (TabHoverTipOf(TabUsageLine, TabCutoffsLine, FiveHourResetUtc, WeekReset, DateTime.UtcNow, ActiveCliTitles) ?? TabTip + ApiSpendTipLine) + SignInTipLine;

    /// <summary>"\n• Sign-in: 1.3 days in, 18.7 / 20.0 days left" (M.ORCH-T1854); "" signed out, on the API key, or with no saved expiry.</summary>
    private string SignInTipLine
    {
        get
        {
            if (!tab.SignedIn || tab.IsApiKey) return "";
            var path = tab.Email is { } email && AccountDirs.Normalize(email) is not null
                ? new AccountDirs().For(email).CredentialsPath : MindAttic.Orchestrator.Budget.ClaudeMeterProbe.DefaultCredentialsPath;
            var now = DateTime.UtcNow;
            return MindAttic.Orchestrator.Budget.SignInAge.Of(path, now) is { } age ? $"\n• {age.Line(now)}" : "";
        }
    }

    /// <summary>Milliseconds before this tab's hover tooltip (its usage text, MainWindow.xaml's LoginHoverTipTemplate) opens:
    /// <see cref="MindAttic.Orchestrator.Settings.OrchestratorSettings.CliTooltipDelayMs"/>, live from the Vault (M.ORCH-T1823)
    /// in place of the old hardcoded <c>ToolTipService.InitialShowDelay="0"</c>.</summary>
    public double CliTooltipDelayMs => ShownSettings.CliTooltipDelayMs;

    /// <summary>The gap below this login's whole tree node in the sidebar (M.ORCH-T1824, the owner's mockup), so one
    /// person's section visibly ends before the next one's colour stripe begins: <see
    /// cref="MindAttic.Orchestrator.Settings.OrchestratorSettings.LoginTreeGutterPx"/>, live from the Vault, in place of
    /// the old hardcoded 2px row gap every tree node shared with its own sibling rows.</summary>
    public System.Windows.Thickness LoginTreeGutter => new(0, 0, 0, ShownSettings.LoginTreeGutterPx);

    /// <summary>This tab's own open CLI tabs' names, in tab order: the hover tip's "Active CLIs" line. The Launcher's own
    /// menu pane is always open (M.ORCH-T855) but is never a CLI (M.ORCH-T1430), so it's left out here the same way
    /// <c>Presume</c> and the Team Wall already leave it out elsewhere; a login with nothing else open reads "none open".</summary>
    internal IReadOnlyList<string> ActiveCliTitles => Terminals.Panes.Where(p => !p.Request.IsMenu).Select(p => p.Title).ToList();

    internal static string? TabHoverTipOf(string usageLine, string cutoffsLine, DateTime? fiveHourResetUtc, DateTime? weekResetUtc, DateTime nowUtc, IReadOnlyList<string> activeCliTitles)
    {
        if (cutoffsLine.Length == 0) return null;
        string ResetLine(string what, DateTime? at) => at is { } t && t > nowUtc
            ? $"{what} reset: {t.ToLocalTime().ToString("ddd M/d h:mm tt", System.Globalization.CultureInfo.InvariantCulture)} (in {Countdown(t - nowUtc)})"
            : $"{what} reset: not known yet";
        var activeCli = activeCliTitles.Count == 0 ? "Active CLIs: none open" : $"Active CLIs: {string.Join(", ", activeCliTitles)}";
        var lead = usageLine.Length == 0 ? "" : $"{usageLine}\n";
        return $"{lead}• {cutoffsLine.Trim().TrimEnd('.')}\n• {ResetLine("5-hour", fiveHourResetUtc)}\n• {ResetLine("Weekly", weekResetUtc)}\n• {activeCli}";
    }

    /// <summary>When this login's 5-hour window resets: the pushed reading's while it's the one shown, else its budget's block, else the roster's.</summary>
    internal DateTime? FiveHourResetUtc => (LiveFiveHour is not null ? Live?.FiveHourResetUtc : null)
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
    public int TabAskingCount => IsPersonTab ? AskingOf(snapshot?.Agents?.Where(a => !PresumedSessions.Contains(a.SessionId))) : 0;

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

    /// <summary>Whether this is the tab on screen (M.ORCH-T1759): the tree sidebar's own selected-row highlight, since it has
    /// no single control like <c>MaTabControl.SelectedItem</c> to carry that state for it.</summary>
    public bool IsTabSelected => ReferenceEquals(this, SelectedTab);

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

    /// <summary>Whether this login's tree node (<see cref="MainViewModel.TabStatus.ShowsLoginTree"/>) is expanded, showing its
    /// open panes as children (M.ORCH-T1759). The header row's own click toggles this alone now (M.ORCH-T2238: there's no
    /// per-login home page to select into any more, so a click there is never anything but expand/collapse); the chevron
    /// does the exact same thing, just as a smaller, unambiguous hit target.</summary>
    private bool isExpanded;
    public bool IsExpanded
    {
        get => isExpanded;
        set { if (isExpanded == value) return; isExpanded = value; Raise(nameof(IsExpanded)); }
    }

    private ICommand? selectTreeChildCommand;

    /// <summary>A tree child row's click (M.ORCH-T1759): selects this login, then shows the clicked pane, exactly as
    /// choosing it from the flat CLI tab bar would.</summary>
    public ICommand SelectTreeChildCommand => selectTreeChildCommand ??= new RelayCommand(p =>
    {
        StripSelection = this;
        if (p is TerminalPaneViewModel pane) Terminals.SelectedPane = pane;
    });

    /// <summary>The tab on screen, which the shared queue's rows act through (M.ORCH-T654).</summary>
    internal MainViewModel Shown => Root.SelectedTab;

    /// <summary>Raised on the global login's tab when another tab is chosen.</summary>
    public event EventHandler? SelectedTabChanged;

    /// <summary>Raised on the global login's tab when a toast goes up on any tab, so the window can flash for it.</summary>
    public event EventHandler<ToastViewModel>? ToastRaised;

    /// <summary>Ctrl+Tab and Ctrl+Shift+Tab: the next and the previous login's tab in the shown org.</summary>
    public ICommand NextTabCommand => nextTabCommand ??= new RelayCommand(() => Root.Step(+1));
    /// <summary>Ctrl+Shift+Tab: the previous login's tab in the shown org (see <see cref="NextTabCommand"/>).</summary>
    public ICommand PreviousTabCommand => previousTabCommand ??= new RelayCommand(() => Root.Step(-1));

    private void Select(MainViewModel? next)
    {
        if (next is null || ReferenceEquals(next, SelectedTab) || !loginTabs.Contains(next)) return;
        // Timed (M.ORCH-T1537): a user report of one login's tab taking ~30s to switch to, unlike the rest, with nothing
        // in the log to say where the time goes. Logged only past SlowSelectThreshold so an ordinary switch stays silent.
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var previous = SelectedTab;
        selectedTab = next;
        next.IsExpanded = true;
        // SelectedTab and StripSelection read the one shared field, so every tab's own answer to them changes here and every
        // one needs telling. IsTabSelected (M.ORCH-T1801) already does its own ReferenceEquals(this, SelectedTab) compare,
        // so only the tab that was true and the one that's true now actually flip value; raising it for every login tab on
        // every switch (M.ORCH-T1797) made each one's tree sidebar - every login's header highlight and every one of its
        // open panes' MultiDataTrigger, none of whose own value had changed - re-run its trigger and measure its rows again,
        // which a dotnet-trace across Launcher tab switches showed costing seconds of TextBlock measuring under the tree's
        // MaStackPanel/DockPanel/Border/ContentPresenter chains. Telling only the two tabs whose value truly moved is enough.
        foreach (var t in loginTabs) { t.Raise(nameof(SelectedTab)); t.Raise(nameof(StripSelection)); }
        previous.Raise(nameof(IsTabSelected));
        next.Raise(nameof(IsTabSelected));
        SelectedTabChanged?.Invoke(this, EventArgs.Empty);
        // The person shown is the one whose chosen terminal shows (M.ORCH-T810).
        ShowChosenPane();
        if (clock.Elapsed > SlowSelectThreshold)
            OrchestratorLog.For("Dashboard").Warning("Switching to {Login:l}'s tab took {Ms}ms", next.TabTitle, clock.ElapsedMilliseconds);
    }

    /// <summary>How long <see cref="Select"/> may take before it's logged (M.ORCH-T1537): generous, so only a real stall is
    /// noisy, never an ordinary switch.</summary>
    private static readonly TimeSpan SlowSelectThreshold = TimeSpan.FromMilliseconds(300);

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

    private void ApplyTabs(Snapshot all, IReadOnlyList<LoginTab> tabs, OrgDirectory nextOrgs, ScopedViews? views)
    {
        using var stall = Doing("");
        // What each step cost (M.ORCH-T2191): a slow pass is logged step by step, and a stall names the step it waited behind.
        var started = Stopwatch.GetTimestamp();
        var mark = started;
        var steps = new List<(string Step, long Ms)>();
        void Took(string step)
        {
            var at = Stopwatch.GetTimestamp();
            steps.Add((step, (long)Stopwatch.GetElapsedTime(mark, at).TotalMilliseconds));
            mark = at;
        }
        workspace = all;
        using (Doing("applying the orgs")) ApplyOrgs(nextOrgs);
        Took("orgs");
        var before = loginTabs.Count;
        var previous = loginTabs.ToList();
        // Who was signed in at the last refresh, so a login disconnected since opens its sign-in by itself (M.ORCH-T796).
        var wasSignedIn = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in previous) wasSignedIn.TryAdd(t.tab.Key, t.tab.SignedIn);
        // The API key's person tab comes in the list itself, after every Claude login's (M.ORCH-T906). No more 'Miscellaneous'
        // tab (M.ORCH-T825): its content (allocate, a pacing hold, reminders, finished work, the workspace's problems) now shows
        // on the global login's tab.
        using (Doing("syncing login tabs"))
        {
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
        }
        Took("syncing login tabs");

        var options = aggregator.Options;
        // Each tab's view was cut with the read, off the UI thread (M.ORCH-T2195); one it didn't cut (a tab come since, the orgs
        // or windows changed meanwhile) is cut here. One view per org, cut once and shared by its tabs.
        var cut = views is not null && views.Fits(all, orgs, options.WorkingWindow, options.WaitingWindow) ? views : null;
        var byOrg = new Dictionary<string, Snapshot>(StringComparer.Ordinal);
        var now = DateTime.UtcNow;
        // The global login's tab applied first (M.ORCH-T1273): ApplyAccounts's settings read (symbolTabLabels, the pace and
        // weekly-stop percents, the blink tokens, the cutoffs and allowances) lands on it alone, and every other tab either
        // copies it or reads it live through Root -- stale until the global tab's own turn otherwise, which a changed Save
        // could leave showing the old figures until some unrelated push or poll happened to run another pass. Its place in
        // this loop is no longer tied to loginTabs' own order, which sits the tab by name since M.ORCH-T752.
        foreach (var t in ApplyOrderOf(loginTabs.ToList(), this))
        {
            using var tabStall = Doing($"applying tab {t.TabTitle}");
            var org = t.OrgId;
            if (cut?.Of(org, t.ScopeEmail) is { } ready)
                t.Apply(ready.Org, ready.View, ready.ReposKey);
            else
            {
                if (!byOrg.TryGetValue(org, out var orgAll)) byOrg[org] = orgAll = OrgScope.For(all, orgs, org, options.WorkingWindow, options.WaitingWindow);
                // The API key's person is scoped to its own login folder's sessions, as any per-CLI login (M.ORCH-T906).
                t.Apply(orgAll, LoginScope.For(orgAll, t.ScopeEmail, options.WorkingWindow, options.WaitingWindow), null);
            }
            // Its token-growth signal from the count this refresh read (M.ORCH-T757): no read of its own. Feeds TabActivity's
            // "spending" input (Blinker.Grew), not just the now-removed blink animation.
            t.CountTabTokens(now);
            t.RaiseTabStatus();
            Took($"tab {t.TabTitle}");
        }
        using (Doing("UpdateOrgPills")) UpdateOrgPills();
        Took("UpdateOrgPills");
        // Each login's Agents panel (M.ORCH-T1747), from the agents its read found.
        using (Doing("ApplyAgents")) ApplyAgents();
        Took("ApplyAgents");
        // A login's tab come or gone takes its terminals with it (to the global login's when gone), M.ORCH-T810.
        using (Doing("SyncTerminals")) SyncTerminals();
        Took("SyncTerminals");
        // The CLIs already open take their login's name and Prefix as they are now (M.ORCH-T1440): a rename in User Settings
        // (said on the bus as a roster change, M.ORCH-T1438) retitles their tabs at this pass, no reopen.
        using (Doing("RefreshLooksAsync")) _ = AllTerminals.RefreshLooksAsync();
        Took("RefreshLooksAsync");
        using (Doing("OpenLaunchers")) OpenLaunchers();
        Took("OpenLaunchers");
        using (Doing("EnsureGeneralPrompts")) EnsureGeneralPrompts();
        Took("EnsureGeneralPrompts");
        if (NewlyDisconnected(wasSignedIn, loginTabs.Select(t => t.tab)) is { Count: > 0 } disconnected)
            using (Doing("AutoSignIn")) AutoSignIn(disconnected);
        Took("AutoSignIn");
        if (Stopwatch.GetElapsedTime(started) is var total && total > SlowApplyTabsThreshold)
            OrchestratorLog.For("Dashboard").Information("Applying a refresh to the tabs took {Ms}ms: {Steps:l}", (long)total.TotalMilliseconds,
                string.Join(", ", steps.Where(s => s.Ms > 0).OrderByDescending(s => s.Ms).Select(s => $"{s.Step} {s.Ms}ms")));

        static MindAttic.Orchestrator.Term.Wpf.UiStallProbe.Scope Doing(string step) =>
            MindAttic.Orchestrator.Term.Wpf.UiStallProbe.Doing(step.Length == 0 ? ApplyTabsWork : $"{ApplyTabsWork}: {step}");
    }

    private const string ApplyTabsWork = "applying a refresh to the tabs";

    /// <summary>The views of <paramref name="all"/> the tabs in <paramref name="tabs"/> (and the global login's,
    /// <paramref name="global"/>) show, cut off the UI thread with the read (M.ORCH-T2195).</summary>
    private ScopedViews CutViews(Snapshot all, IReadOnlyList<LoginTab> tabs, LoginTab global, OrgDirectory nextOrgs)
    {
        var options = aggregator.Options;
        return ScopedViews.Cut(all, tabs.Append(global), nextOrgs, options.WorkingWindow, options.WaitingWindow);
    }

    /// <summary>
    /// Each login tab's view of one snapshot (M.ORCH-T2195), cut where the snapshot was read rather than on the UI thread: its
    /// org's view (<see cref="OrgScope"/>), its own (<see cref="LoginScope"/>) and a digest of the repos in it, which lets a
    /// tab whose repos didn't move skip rebuilding its repo cards. A tab's view is found by its org and email, so a tab
    /// added, removed or moved to another org since the read finds none and is cut on the UI thread as before.
    /// </summary>
    internal sealed class ScopedViews
    {
        private readonly Snapshot all;
        private readonly OrgDirectory orgs;
        private readonly TimeSpan working, waiting;
        private readonly Dictionary<(string Org, string? Email), (Snapshot Org, Snapshot View, string ReposKey)> views = [];

        private ScopedViews(Snapshot all, OrgDirectory orgs, TimeSpan working, TimeSpan waiting) =>
            (this.all, this.orgs, this.working, this.waiting) = (all, orgs, working, waiting);

        internal static ScopedViews Cut(Snapshot all, IEnumerable<LoginTab> tabs, OrgDirectory orgs, TimeSpan working, TimeSpan waiting)
        {
            var cut = new ScopedViews(all, orgs, working, waiting);
            var byOrg = new Dictionary<string, Snapshot>(StringComparer.Ordinal);
            // A repo no login's scope narrowed is the same record in every tab's view: hashed once.
            var hashed = new Dictionary<RepoStatus, byte[]>(ReferenceEqualityComparer.Instance);
            foreach (var tab in tabs)
            {
                var org = orgs.OrgOfTab(tab);
                if (cut.views.ContainsKey((org, tab.Email))) continue;
                if (!byOrg.TryGetValue(org, out var orgAll)) byOrg[org] = orgAll = OrgScope.For(all, orgs, org, working, waiting);
                var view = LoginScope.For(orgAll, tab.Email, working, waiting);
                cut.views[(org, tab.Email)] = (orgAll, view, ReposKey(view.Repos, hashed));
            }
            return cut;
        }

        /// <summary>Whether these views were cut from what the tabs are being applied with now.</summary>
        internal bool Fits(Snapshot shown, OrgDirectory orgsNow, TimeSpan workingNow, TimeSpan waitingNow) =>
            ReferenceEquals(all, shown) && ReferenceEquals(orgs, orgsNow) && working == workingNow && waiting == waitingNow;

        internal (Snapshot Org, Snapshot View, string ReposKey)? Of(string org, string? email) =>
            views.TryGetValue((org, email), out var v) ? v : null;

        /// <summary>A digest of <paramref name="repos"/> as the published file would hold them: equal when no card would change.</summary>
        private static string ReposKey(IReadOnlyList<RepoStatus> repos, Dictionary<RepoStatus, byte[]> hashed)
        {
            using var digest = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
            foreach (var repo in repos)
            {
                if (!hashed.TryGetValue(repo, out var hash))
                    hashed[repo] = hash = System.Security.Cryptography.SHA256.HashData(
                        System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(repo, MindAttic.Orchestrator.Status.StatusStream.PayloadJson));
                digest.AppendData(hash);
            }
            return Convert.ToHexString(digest.GetHashAndReset());
        }
    }

    /// <summary>An <see cref="ApplyTabs"/> pass longer than this logs what each of its steps cost (M.ORCH-T2191).</summary>
    private static readonly TimeSpan SlowApplyTabsThreshold = TimeSpan.FromMilliseconds(250);

    /// <summary>Every tab proxies some state to the global login's (the window shows whichever tab is chosen), so a change
    /// is said on each of them.</summary>
    private void RaiseOnEveryTab(string name)
    {
        foreach (var t in Root.loginTabs) t.Raise(name);
        if (!Root.loginTabs.Contains(Root)) Root.Raise(name);
    }

    /// <summary>The logins whose Launcher tab this dashboard has opened (or tried to): each once while it stays open: one you
    /// close or exit is reopened by the next refresh (M.ORCH-T855: the Launcher tab is always open). A fallback open in its own
    /// window, never embedded, is held, so a failing embed doesn't spawn a window every tick.</summary>
    private readonly HashSet<string> launchersOpened = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> launchersEmbedded = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// At most one Launcher tab per account (M.ORCH-T699), embedded first in its Terminals panel (M.LNCH-T77): extras and ended
    /// ones are closed; a new one is opened for every signed-in login only with <c>AutoOpenLaunchers</c> on (M.ORCH-T2103).
    /// A login whose Launcher tab is already open (embedded, or a standalone one running outside the dashboard) keeps it.
    /// </summary>
    private void OpenLaunchers()
    {
        if (!IsGlobalTab) return;
        foreach (var t in loginTabs)
        {
            var key = t.tab.Key;
            var normalized = AccountDirs.Normalize(t.ScopeEmail);
            var liveLaunchers = AllTerminals.Panes.Where(p => p.Request.IsMenu && !p.Ended &&
                string.Equals(p.Login, normalized, StringComparison.OrdinalIgnoreCase)).ToList();
            // M.ORCH-T1890 (the owner's request): only ever one Launcher tab per login, so any extra left over (e.g. from
            // before this, or a race between two opens) is closed, keeping whichever is shown or else the first.
            if (liveLaunchers.Count > 1)
            {
                var keep = liveLaunchers.FirstOrDefault(p => p.IsSelected) ?? liveLaunchers[0];
                foreach (var extra in liveLaunchers.Where(p => p != keep))
                {
                    if (!tabsGone.TryGetValue(extra, out _)) tabsGone.Add(extra, null);
                    AllTerminals.Close(extra);
                }
            }
            if (liveLaunchers.Count > 0) launchersEmbedded.Add(key);
            else if (launchersEmbedded.Remove(key)) launchersOpened.Remove(key);
            // An ended Launcher otherwise waits for CloseEndedLaunchers to run alongside a respawn -- which a login
            // that isn't signed in here, or that keeps losing the 30s respawn race, may not do again for a long time,
            // leaving stale "Launcher" rows piled up under its tree node. This pass closes them unconditionally, so
            // none outlives the refresh that saw it end, whether or not a respawn follows.
            var endedLaunchers = AllTerminals.Panes.Where(p => p.Request.IsMenu && p.Ended &&
                string.Equals(p.Login, normalized, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var ended in endedLaunchers)
            {
                if (!tabsGone.TryGetValue(ended, out _)) tabsGone.Add(ended, null);
                AllTerminals.Close(ended);
            }
        }
        // No Launcher is opened for a login on its own any more (M.ORCH-T2103, reversing M.ORCH-T1886): every login's tree node
        // always has its '+ Add' child to open one (or any repo) by hand. AutoOpenLaunchers (off by default) still opts back in.
        if (ShuttingDown || !autoOpenLaunchers) return;
        // Through the same cap as a last tab's respawn (M.ORCH-T1150): a Launcher that exits at once is reopened at most every 30s.
        var now = DateTime.UtcNow;
        // Locked (M.ORCH-T2125): a locked login never gets a new Launcher opened here either.
        var wanted = loginTabs.Where(t => t.IsTabSignedIn && !IsBeingDeleted(t.ScopeEmail) && !t.IsLoginLocked && !launchersOpened.Contains(t.tab.Key) && launcherRespawn.TryTake(t.tab.Key, now))
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

    // ── General Prompt, auto-kept per login (M.ORCH-T1951/M.ORCH-T1957, the owner's request) ─────────────

    /// <summary>The logins whose General Prompt this dashboard has opened and still sees live; cleared once its pane is gone,
    /// so the next refresh reopens it through <see cref="generalPromptRespawn"/>'s cap.</summary>
    private readonly HashSet<string> generalPromptsOpened = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The logins with an open in flight (M.ORCH-T1957): a resync landing before the pane exists never starts a second.</summary>
    private readonly HashSet<string> generalPromptsPending = new(StringComparer.OrdinalIgnoreCase);
    private readonly LauncherRespawn generalPromptRespawn = new();
    private bool autoOpenGeneralPrompts = new MindAttic.Orchestrator.Settings.OrchestratorSettings().AutoOpenGeneralPrompts;

    /// <summary>A pane is <paramref name="login"/>'s General Prompt (M.ORCH-T1957): a live CLI, not the Launcher's menu, on that
    /// login's folder (<see cref="TreeLoginKey"/>), at the workspace root (case and a trailing slash ignored).</summary>
    private static bool IsGeneralPromptOf(TerminalPaneViewModel p, string? login, string root) =>
        !p.Request.IsMenu && string.Equals(TreeLoginKey(p), login, StringComparison.OrdinalIgnoreCase) && SameFolder(p.Request.Directory, root);

    private static bool SameFolder(string? a, string? b) =>
        a is not null && b is not null && string.Equals(a.TrimEnd('\\', '/'), b.TrimEnd('\\', '/'),StringComparison.OrdinalIgnoreCase);

    /// <summary>This login is signed in as itself (M.ORCH-T1957): signed in, not the API key, a per-CLI folder, not borrowed
    /// (its folder isn't OAuth-signed in as another account, <see cref="SignedInAsOther"/>).</summary>
    private bool SignedInAsItself => ScopeEmail is not null && !tab.IsApiKey && tab.SignedIn && SignedInAsOther is null;

    /// <summary>Eligible for a General Prompt CLI (M.ORCH-T1959): signed in as itself, or cleanly disconnected (not signed in
    /// at all) - widening <see cref="SignedInAsItself"/> to a disconnected login too, so it gets a real, quiet,
    /// unauthenticated CLI on its own folder to run its own <c>/login</c> in, scoped correctly, instead of just the
    /// signed-out stand-in row. Never a login whose folder is borrowed by another account (signed in, but as somebody else -
    /// opening a CLI there would silently run as them) or one that's <see cref="IsLoginLocked"/>
    /// (M.ORCH-T2125): a locked login never gets a new one opened here, though one already live is left running (this only
    /// gates opening, never closes a live pane).</summary>
    private bool EligibleForGeneralPrompt => ScopeEmail is not null && !tab.IsApiKey && !IsLoginLocked && (SignedInAsItself || !tab.SignedIn);

    /// <summary>
    /// One General Prompt CLI per eligible login (M.ORCH-T1951/M.ORCH-T1957/M.ORCH-T1959), kept open like its Launcher: one
    /// closed or ended is reopened at a later refresh (capped by <see cref="generalPromptRespawn"/>), extras are closed. Only
    /// per-CLI folder logins get one: the global (~/.claude) login has none, so a global login that is the same person as a
    /// folder login never doubles up. Opened quietly (<see cref="OpenGeneralPromptQuietlyAsync"/>), never through
    /// <see cref="StartSessionAsync"/>'s RAM dialog or sign-in.
    /// </summary>
    private void EnsureGeneralPrompts()
    {
        if (!IsGlobalTab || !autoOpenGeneralPrompts) return;
        var root = WorkspaceRoot;
        if (string.IsNullOrEmpty(root)) return;
        generalPromptRespawn.Spacing = DashboardTimings.LauncherSpacing;
        var now = DateTime.UtcNow;
        foreach (var t in loginTabs)
        {
            if (!t.EligibleForGeneralPrompt || IsBeingDeleted(t.ScopeEmail)) continue;
            var key = t.tab.Key;
            var login = AccountDirs.Normalize(t.ScopeEmail);
            // A parked General Prompt's stub (M.ORCH-T2073) is still this login's General Prompt: it resumes when shown, so
            // keep-one never opens a second beside it; with two, the running one is kept over the stub.
            var live = AllTerminals.Panes.Where(p => (!p.Ended || p.Parked) && IsGeneralPromptOf(p, login, root)).ToList();
            if (live.Count > 1)
            {
                var keep = live.FirstOrDefault(p => p.IsSelected) ?? live.FirstOrDefault(p => !p.Parked) ?? live[0];
                foreach (var extra in live.Where(p => p != keep))
                {
                    if (!tabsGone.TryGetValue(extra, out _)) tabsGone.Add(extra, null);
                    AllTerminals.Close(extra);
                }
            }
            if (live.Count > 0) { generalPromptsOpened.Add(key); continue; }
            generalPromptsOpened.Remove(key);
            if (ShuttingDown || RestoringTerminals || generalPromptsPending.Contains(key)) continue;
            foreach (var ended in AllTerminals.Panes.Where(p => p.Ended && IsGeneralPromptOf(p, login, root)).ToList())
            {
                if (!tabsGone.TryGetValue(ended, out _)) tabsGone.Add(ended, null);
                AllTerminals.Close(ended);
            }
            if (!generalPromptRespawn.TryTake(key, now)) continue;
            generalPromptsPending.Add(key);
            _ = OpenGeneralPromptQuietlyAsync(key, t.ScopeEmail!, t.TabTitle, root);
        }
    }

    /// <summary>The unattended open (M.ORCH-T1957/M.ORCH-T1959): skipped (and tried again at a later refresh) while RAM or the
    /// disk holds new CLIs, or the folder is borrowed by another account; never a dialog or a sign-in window - a disconnected
    /// (never-signed-in, or cleanly signed out) folder opens its own unauthenticated CLI just the same, so <c>/login</c> run
    /// inside it is scoped to this account (M.ORCH-T1959). Never throws.</summary>
    private async Task OpenGeneralPromptQuietlyAsync(string key, string email, string title, string root)
    {
        var log = OrchestratorLog.For("Dashboard");
        try
        {
            var settings = store.LoadSettings();
            var held = await Task.Run(() => MindAttic.Orchestrator.Activity.MemoryGuard.StartHeld(settings) ?? MindAttic.Orchestrator.Activity.DiskGuard.StartHeld(settings));
            if (held is not null) { log.Information("{Login:l}'s General Prompt waits: {Why:l}", title, held); return; }
            var dir = accountDirs.For(email);
            var ready = await Task.Run(() =>
            {
                if (dir.BorrowedFrom is not null) return false;
                accountDirs.Prepare(dir);
                return true;
            });
            if (!ready || ShuttingDown || RestoringTerminals) return;
            if (AllTerminals.Panes.Any(p => (!p.Ended || p.Parked) && IsGeneralPromptOf(p, AccountDirs.Normalize(email), root))) return;
            if (OpenTerminal(control.RepoPaneRequest("General", root, "Claude", null, dir.Directory, email)) is { } error)
                log.Warning("Couldn't open {Login:l}'s General Prompt tab: {Error:l}", title, error);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.Warning(ex, "Couldn't open {Login:l}'s General Prompt tab", title);
        }
        finally { generalPromptsPending.Remove(key); }
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
                added.PropertyChanged += (s, p) =>
                {
                    if (p.PropertyName != nameof(TerminalPaneViewModel.Ended) || s is not TerminalPaneViewModel pane) return;
                    if (pane.Ended) AfterChange.Post(() => OnTabGone(pane));
                    // A revived pane (Ended back to false) is live again: its next end counts as a tab gone anew (M.ORCH-T1970).
                    // Posted too, so it runs after an end posted just before it.
                    else AfterChange.Post(() => Root.tabsGone.Remove(pane));
                };
            // Not inside the change: the replacement Launcher closes and adds panes, which an ObservableCollection refuses during
            // its own CollectionChanged ("Cannot change ObservableCollection during a CollectionChanged event"), so closing a
            // person's last tab never opened their new Launcher (M.ORCH-T1317).
            foreach (var gone in e.OldItems?.OfType<TerminalPaneViewModel>() ?? []) AfterChange.Post(() => OnTabGone(gone));
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
            // The Launcher's own tab is just another tab now (M.ORCH-T1902 dropped the old always-redeploy special case: the
            // Launcher's own in-app Redeploy menu item already handles redeploying and relaunching itself, so the dashboard
            // doesn't need to): it falls through to the same last-tab-gone decision as everything else below.
            // With automatic Launchers off (M.ORCH-T1585), any other tab gone opens nothing: Open Project on the tab's right-click does.
            // Locked (M.ORCH-T2125): a locked login's last tab closing never respawns it a new Launcher either.
            if (!autoOpenLaunchers || IsBeingDeleted(owner.ScopeEmail) || owner.IsLoginLocked) return;
            var key = owner.tab.Key;
            // A parked tab (M.ORCH-T2073) is still theirs: it resumes when shown, so it leaves no person without a tab.
            var hasLive = AllTerminals.Panes.Any(p => (!p.Ended || p.Parked) && ReferenceEquals(OwnerOf(p), owner));
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
    /// brought up rather than getting a second (M.ORCH-T1890: only ever one Launcher per login), and so is a standalone one
    /// already running outside the dashboard (from before this, or started by hand) -- off the UI thread, since finding it
    /// asks WMI. Embedding failing (rare) falls back to the login's own Windows Terminal window, as every Launcher tab
    /// opened before M.LNCH-T77. Returns an error for a toast (null on success, embedded or not) and a status line either
    /// way.</summary>
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

    private ICommand? runLauncherCommand;

    /// <summary>Run Launcher (M.ORCH-T1525), from this tab's right-click menu: opens this login's Launcher by hand, the same
    /// path the automatic opens use (<see cref="OpenLauncherPaneAsync"/>) -- an escape hatch for when a login is left with no
    /// tabs and the automatic one (<see cref="OpenLaunchers"/>, the last-tab respawn) didn't fire or failed quietly.</summary>
    public ICommand RunLauncherCommand => runLauncherCommand ??= new RelayCommand(async () =>
    {
        // An async void handler: anything thrown here (WMI, wt.exe, the pane host) would reach the dispatcher and crash the
        // dashboard, so it's caught and shown as the toast instead (M.ORCH-T1392).
        string? error, status;
        try { (error, status) = await OpenLauncherPaneAsync(ScopeEmail, TabTitle); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            OrchestratorLog.For("Launcher").Error(ex, "Run Launcher for {Login:l} failed", TabTitle);
            (error, status) = (ex.Message, $"Couldn't run {TabTitle}'s Launcher: {ex.Message}");
        }
        if (error is { } e) Toasts.Insert(0, new ToastViewModel($"Couldn't run {TabTitle}'s Launcher", e, Palette.Over, null));
        AccountsMessage = status;
    }, () => IsTabSignedIn && !IsLoginLocked);

    /// <summary>
    /// /launcher (M.ORCH-T1487, <c>orchestrator launcher</c> over the bus; UI thread): brings up <paramref name="login"/>'s
    /// one live Launcher tab, or opens it (as <see cref="RunLauncherCommand"/> does) when they have none. Only ever one
    /// Launcher per login (M.ORCH-T1890), so there's nothing left to cycle through. Null is the global login. Never throws.
    /// </summary>
    public void CycleLauncher(string? login)
    {
        if (!IsGlobalTab) { Root.CycleLauncher(login); return; }
        try
        {
            var global = workspace?.GlobalEmail;
            var wanted = AccountDirs.Normalize(login ?? global);
            var person = loginTabs.FirstOrDefault(t => t.IsPersonTab &&
                string.Equals(AccountDirs.Normalize(t.ScopeEmail ?? global), wanted, StringComparison.OrdinalIgnoreCase));
            if (person is null)
            {
                OrchestratorLog.For("Launcher").Warning("/launcher for {Login:l}: no tab for that login here", login ?? "the global login");
                return;
            }
            SyncTerminals();
            if (person.Terminals.Panes.FirstOrDefault(p => p.Request.IsMenu && !p.Ended) is { } launcher) ShowPane(launcher);
            else if (person.RunLauncherCommand.CanExecute(null)) person.RunLauncherCommand.Execute(null);
        }
        catch (Exception ex) { OrchestratorLog.For("Launcher").Error(ex, "/launcher for {Login:l} failed; contained", login ?? "the global login"); }
    }

    /// <summary>The Launcher's project tree (M.ORCH-T1585), shared by every tab's right-click menu: null until the first read.</summary>
    internal static IReadOnlyList<ProjectNode>? ProjectTree { get; private set; }

    private static Task? projectsLoading;
    private static DateTime projectsReadUtc;

    /// <summary>Reads the roster again (off the UI thread, GitHub's wip topic included) unless a read is already running;
    /// <paramref name="ifOlderThan"/> skips it while the last read is fresher than that.</summary>
    internal static Task RefreshProjectsAsync(TimeSpan? ifOlderThan = null)
    {
        if (projectsLoading is { IsCompleted: false } running) return running;
        if (ifOlderThan is { } age && ProjectTree is not null && DateTime.UtcNow - projectsReadUtc < age) return Task.CompletedTask;
        return projectsLoading = Task.Run(async () =>
        {
            var tree = ProjectRoster.Tree(await ProjectRoster.LoadTaggedAsync());
            ProjectTree = tree;
            projectsReadUtc = DateTime.UtcNow;
        });
    }

    /// <summary>Refresh projects, from the menu: reads the roster again and says how many it found.</summary>
    internal async Task RefreshProjectListAsync()
    {
        AccountsMessage = "Refreshing the project list…";
        await RefreshProjectsAsync();
        await RefreshProjectChangesAsync();
        AccountsMessage = $"Project list refreshed: {ProjectTree?.Sum(CountProjects) ?? 0} projects.";
    }

    private static int CountProjects(ProjectNode node) => node is ProjectBranch b ? b.Children.Sum(CountProjects) : 1;

    /// <summary>Each project folder's uncommitted file count (M.ORCH-T1587), as the picker tree shows it beside the project:
    /// null for a folder git can't read; a folder not read yet isn't in it.</summary>
    internal static System.Collections.Concurrent.ConcurrentDictionary<string, int?> ProjectChanges { get; } = new(StringComparer.OrdinalIgnoreCase);

    private static Task? changesLoading;
    private static DateTime changesReadUtc;

    /// <summary>Reads every project's git status again (off the UI thread, a few at a time) unless a read is already running;
    /// <paramref name="ifOlderThan"/> skips it while the last read is fresher than that. Waits for a running roster read first.</summary>
    internal static Task RefreshProjectChangesAsync(TimeSpan? ifOlderThan = null)
    {
        if (changesLoading is { IsCompleted: false } running) return running;
        if (ifOlderThan is { } age && DateTime.UtcNow - changesReadUtc < age) return Task.CompletedTask;
        return changesLoading = Task.Run(async () =>
        {
            if (projectsLoading is { } roster) { try { await roster; } catch (Exception ex) when (ex is not OutOfMemoryException) { } }
            if (ProjectTree is not { } tree) return;
            var paths = Leaves(tree).Select(p => p.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            Parallel.ForEach(paths, new ParallelOptions { MaxDegreeOfParallelism = 6 }, p => ProjectChanges[p] = ProjectCommit.ChangedFiles(p));
            changesReadUtc = DateTime.UtcNow;
        });
    }

    private static IEnumerable<RosterProject> Leaves(IEnumerable<ProjectNode> nodes) =>
        nodes.SelectMany<ProjectNode, RosterProject>(n => n switch { ProjectLeaf l => [l.Project], ProjectBranch b => Leaves(b.Children), _ => [] });

    /// <summary>Opens a <paramref name="kind"/> CLI (the Open Project window's provider, M.ORCH-T2041) for
    /// <paramref name="project"/> on this tab's login, as the tile grid's Start does.</summary>
    internal Task OpenProjectAsync(RosterProject project, string kind) => StartSessionAsync(project.Name, project.Path, kind, AutoLogin, null);

    /// <summary>Opens a <paramref name="kind"/> CLI at the workspace root (the Launcher's "General Prompt"), on this tab's login.</summary>
    internal Task OpenGeneralPromptAsync(string kind) => StartSessionAsync("General", WorkspaceRoot, kind, AutoLogin, null);

    /// <summary>Commit + push every dirty project under one message (M.ORCH-T1590), the project tree root's right-click:
    /// the Launcher's own <c>commit</c>, its lines to <paramref name="line"/>; its exit code (0: all fine).</summary>
    internal Task<int> CommitAllProjectsAsync(string? message, Action<string> line) => control.CommitAllAsync(message, line);

    private void Retitle(LoginTab next)
    {
        if (next == tab) return;
        tab = next;
        Raise(nameof(Tab)); Raise(nameof(TabTitle)); Raise(nameof(TabFaceTitle)); Raise(nameof(TabTip)); Raise(nameof(TabHoverTip)); Raise(nameof(TabBrush));
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
        : tab.Email is null ? "The global login (~/.claude) is signed out. Sign in to see its dashboard and open its Launcher again."
        : $"{tab.Title} ({tab.Email}) isn't signed in on this machine yet. Sign in to see its dashboard and open the Launcher as {tab.Title}.";

    /// <summary>Sign in, on a tab whose login isn't signed in yet: its own sign-in in a private browser window, then the tab
    /// becomes its dashboard.</summary>
    public ICommand SignInTabCommand => signInTabCommand ??= new RelayCommand(async () =>
    {
        // The global login's tab, logged out (M.ORCH-T1821): the same sign-in its menu's Login runs.
        if (ScopeEmail is not { } email) { LoginCommand.Execute(null); return; }
        if (await SignInOwnAsync(email)) await RefreshAsync();
    }, () => !tab.SignedIn && !tab.IsApiKey && !IsSigningIn);

    /// <summary>Tags each new toast with this tab while there's more than one (toasts tagged by tab), mirrors every tab's
    /// toasts into the shared <c>allToasts</c> list (M.ORCH-T1574), and tells the window (<see cref="ToastRaised"/>, which
    /// flashes it).</summary>
    private void OnToastsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var all = Root.allToasts;
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            for (var i = all.Count - 1; i >= 0; i--)
                if (!Root.loginTabs.Any(tab => tab.Toasts.Contains(all[i]))) all.RemoveAt(i);
            RaiseNotificationCounts();
            return;
        }
        if (e.OldItems is not null && e.Action != NotifyCollectionChangedAction.Move)
            foreach (var t in e.OldItems.OfType<ToastViewModel>()) all.Remove(t);
        if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems is null) { RaiseNotificationCounts(); return; }
        foreach (var t in e.NewItems.OfType<ToastViewModel>())
        {
            if (Root.loginTabs.Count > 1) t.Tab ??= TabTitle;
            if (!all.Contains(t)) all.Insert(0, t);
            Root.ToastRaised?.Invoke(this, t);
        }
        PruneToasts(all, DateTime.UtcNow);
        RaiseNotificationCounts();
    }

    /// <summary>Takes <paramref name="toast"/> away everywhere: from whichever tab raised it and from <see cref="AllToasts"/>.</summary>
    private void RemoveToast(ToastViewModel toast)
    {
        foreach (var tab in Root.loginTabs) tab.Toasts.Remove(toast);
        Root.allToasts.Remove(toast);
        RaiseNotificationCounts();
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
