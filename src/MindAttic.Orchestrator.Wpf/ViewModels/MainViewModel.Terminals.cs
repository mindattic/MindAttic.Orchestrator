using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// Each person's Terminals panel (M.ORCH-T810), between their Current work and their Tickets: one tab per CLI of theirs running
/// inside the dashboard, each a live ConPTY terminal. This reverses M.ORCH-T751 (which took the old Terminals page out over
/// its lost colors) on purpose, as a panel on the person's page rather than a page of its own. A terminal arrives on the
/// dashboard's pipe (<see cref="PaneBroker"/>), sent by MindAttic.Launcher when it runs inside the Orchestrator (M.LNCH-T77),
/// and goes on the page of the login its CLI runs on, following it when it moves to another.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>Every terminal open in the dashboard, whoever's; the same list in every tab, the global login's.</summary>
    public TerminalsViewModel AllTerminals { get; private set; } = null!;

    /// <summary>The park trigger (M.ORCH-T2074): the global login's tab's only; null on every other tab.</summary>
    private IdleParker? idleParker;

    /// <summary>This person's terminals, on their page.</summary>
    public PersonTerminalsViewModel Terminals { get; private set; } = null!;

    private void InitTerminals()
    {
        if (root is null)
        {
            AllTerminals = new TerminalsViewModel { GlobalLogin = () => workspace?.GlobalEmail };
            AllTerminals.Opened += ShowPane;
            AllTerminals.LoginsChanged += SyncTerminals;
            AllTerminals.CloseFailed += (_, why) => Toasts.Insert(0, new ToastViewModel("CLI not ended", why, Palette.Over, null));
            AllTerminals.Panes.CollectionChanged += (_, _) => SyncTerminals();
            AllTerminals.Recolored += RaiseTaskAccents;
            TerminalPaneViewModel.PresumptionChanged += () => { foreach (var t in loginTabs) t.RaiseTabStatus(); };
            WatchForLastTab();
            // Idle CLIs parked past ParkIdleCliMinutes (M.ORCH-T2074): off until the first refresh applies the setting.
            idleParker = new IdleParker(AllTerminals);
        }
        else AllTerminals = root.AllTerminals;
        Terminals = new PersonTerminalsViewModel(AllTerminals);
        Terminals.SelectionChanged += () => Root.ShowChosenPane();
    }

    /// <summary>Opens <paramref name="request"/> as a terminal tab on its person's page and brings it up (the pipe's requests
    /// arrive here, on the UI thread); returns an error, or null.</summary>
    public string? OpenTerminal(PaneRequest request) => Root.AllTerminals.Open(request);

    /// <summary>The tab whose page <paramref name="pane"/> is on: its CLI's login's person, else (no tab for that login, or the
    /// global login) the global login's.</summary>
    private MainViewModel OwnerOf(TerminalPaneViewModel pane)
    {
        if (TreeLoginKey(pane) is not { } login) return this;
        var global = workspace?.GlobalEmail;
        return loginTabs.FirstOrDefault(t => t.IsPersonTab && string.Equals(t.ScopeEmail ?? global, login, StringComparison.OrdinalIgnoreCase)) ?? this;
    }

    /// <summary>The one grouping key for which login node a CLI/Launcher pane sits under (M.ORCH-T1949): the login folder it
    /// was started with (<see cref="PaneRequest.Login"/>, its CLAUDE_CONFIG_DIR) or the one its CLI was since moved to, never
    /// the account that folder happens to be OAuth-signed in as. Null is the global login's node.</summary>
    internal static string? TreeLoginKey(TerminalPaneViewModel pane) => pane.Login;

    /// <summary>Hands each person's tab its own terminals (on the global login's tab, which holds them all), in their
    /// position (M.ORCH-T1170): the order they were first opened in, or last dragged to (M.ORCH-T1526,
    /// <see cref="PersonTerminalsViewModel.Reorder"/>) — not the order a dashboard restart happened to reattach them in, so
    /// a crash and restart leaves the tab strip looking the same as before. This includes the Launcher's own menu pane
    /// (M.LNCH-T77): it is an ordinary, freely-draggable tab here like any other, no longer hard-pinned first. A tab with no
    /// recorded position (opened before this existed, or reattached from a record with no sidecar) sorts after every tab
    /// that has one.</summary>
    private void SyncTerminals()
    {
        if (!IsGlobalTab) { Root.SyncTerminals(); return; }
        var owners = AllTerminals.Panes.ToLookup(OwnerOf);
        foreach (var t in loginTabs)
            t.Terminals.SyncTo([.. owners[t].OrderBy(p => p.Request.Order ?? long.MaxValue)]);
        foreach (var t in loginTabs)
        {
            t.Terminals.RefreshLast(); // a pane moved between logins (M.ORCH-T1955)
            t.Raise(nameof(ShowsLauncherPlaceholder));
            t.Raise(nameof(ShowsGeneralPromptPlaceholder));
        }
        ShowChosenPane();
    }

    /// <summary>Whether this login's tree node shows the disabled "Launcher (signed out)" row (M.ORCH-T1950): every org
    /// member's node stays for good and always has a Launcher child (M.ORCH-T1886), but <see cref="OpenLaunchers"/> only runs
    /// one on a signed-in login, so a signed-out one with no Launcher pane gets this stand-in instead of becoming a leaf.
    /// The API key keeps its own behaviour (no stand-in).</summary>
    public bool ShowsLauncherPlaceholder => IsPersonTab && !tab.IsApiKey && !tab.SignedIn && !Terminals.Panes.Any(p => p.Request.IsMenu && !p.Ended);

    /// <summary>Whether this login's node shows the disabled "General Prompt (unavailable)" row (M.ORCH-T1957/M.ORCH-T1959): a
    /// member login (not the API key, not the global one) that isn't <see cref="EligibleForGeneralPrompt"/> -- its folder
    /// borrowed by another account, or disabled -- so <see cref="EnsureGeneralPrompts"/> keeps no General Prompt on it, and it
    /// has none open. A merely disconnected login is eligible (M.ORCH-T1959) and gets a real one instead of this stand-in.</summary>
    public bool ShowsGeneralPromptPlaceholder => IsPersonTab && !tab.IsApiKey && ScopeEmail is not null && Root.autoOpenGeneralPrompts
        && !EligibleForGeneralPrompt && !Terminals.Panes.Any(p => (!p.Ended || p.Parked) && IsGeneralPromptOf(p, AccountDirs.Normalize(ScopeEmail), Root.WorkspaceRoot));

    /// <summary>The one terminal on screen is the shown person's chosen one; every other is hidden.</summary>
    private void ShowChosenPane()
    {
        if (!IsGlobalTab) { Root.ShowChosenPane(); return; }
        AllTerminals.SelectedPane = SelectedTab.Terminals.SelectedPane;
    }

    /// <summary>Brings <paramref name="pane"/> up (just opened, or Go to tab on its CLI): its person's page, and it chosen there.</summary>
    internal void ShowPane(TerminalPaneViewModel pane)
    {
        if (!IsGlobalTab) { Root.ShowPane(pane); return; }
        SyncTerminals();
        var owner = OwnerOf(pane);
        if (owner.OrgId != selectedOrgId) SelectOrg(owner.OrgId);
        Select(owner);
        owner.Terminals.SelectedPane = pane;
        ShowChosenPane();
    }
}
