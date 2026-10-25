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

    /// <summary>This person's terminals, on their page.</summary>
    public PersonTerminalsViewModel Terminals { get; private set; } = null!;

    private void InitTerminals()
    {
        if (root is null)
        {
            AllTerminals = new TerminalsViewModel();
            AllTerminals.Opened += ShowPane;
            AllTerminals.LoginsChanged += SyncTerminals;
            AllTerminals.Panes.CollectionChanged += (_, _) => SyncTerminals();
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
        if (pane.Login is not { } login) return this;
        var global = workspace?.GlobalEmail;
        return loginTabs.FirstOrDefault(t => t.IsPersonTab && string.Equals(t.ScopeEmail ?? global, login, StringComparison.OrdinalIgnoreCase)) ?? this;
    }

    /// <summary>Hands each person's tab its own terminals (on the global login's tab, which holds them all), the Launcher's
    /// own menu pane first (M.LNCH-T77) and every CLI tab after it in the order each opened, then shows the chosen one.</summary>
    private void SyncTerminals()
    {
        if (!IsGlobalTab) { Root.SyncTerminals(); return; }
        var owners = AllTerminals.Panes.ToLookup(OwnerOf);
        foreach (var t in loginTabs) t.Terminals.SyncTo([.. owners[t].OrderByDescending(p => p.Request.IsMenu)]);
        ShowChosenPane();
    }

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
