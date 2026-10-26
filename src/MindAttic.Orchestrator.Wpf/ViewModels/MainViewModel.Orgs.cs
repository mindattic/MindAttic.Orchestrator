using System.Collections.ObjectModel;
using System.IO;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>One org in the switcher above the person tabs (M.ORCH-T655): its name, who's in it, and how many of its people's
/// repos need you, so one waiting in another org is seen.</summary>
public sealed class OrgViewModel(Organization org) : ObservableObject
{
    private Organization org = org;
    private string summary = "";
    private int waiting;

    public string Id => org.Id;
    public string Name => org.Name;
    public string Summary => summary;
    public string Badge => waiting > 0 ? waiting.ToString(System.Globalization.CultureInfo.InvariantCulture) : "";
    public string Tip => $"{Name}: {summary}" + (waiting == 0 ? "" : $". {waiting} need{(waiting == 1 ? "s" : "")} you.");

    public void Update(Organization next, string nextSummary, int nextWaiting)
    {
        if (next == org && nextSummary == summary && nextWaiting == waiting) return;
        org = next;
        summary = nextSummary;
        waiting = nextWaiting;
        Raise(string.Empty);
    }
}

/// <summary>
/// Organizations above people (M.ORCH-T655): the dashboard's org switcher, above the person tabs, shown once there's a second
/// org (<c>orchestrator orgs add</c>). Each org owns people, each person their logins; the person tabs are the shown org's
/// (<see cref="OrgTabs"/>), each tab's snapshot is its org's (<see cref="OrgScope"/>: only its members' CLIs, agents and pace,
/// the Accounts view only its logins), and the queue lists every unclaimed ticket but only the org's own claims. A switch
/// changes which tabs the strip lists and which tab the window shows, as a tab switch does (M.ORCH-T654): only what's on
/// screen is rebuilt, whatever the org's size. With one org nothing changes: no switcher, every tab, the whole queue.
/// </summary>
public sealed partial class MainViewModel
{
    private OrgDirectory orgs = OrgDirectory.Single;
    private readonly ObservableCollection<OrgViewModel> orgItems = [];
    private readonly ObservableCollection<MainViewModel> orgTabs = [];
    private string selectedOrgId = Organization.DefaultId;
    private bool orgRestored;

    /// <summary>Every org, the default one first; the same list in every tab.</summary>
    public ObservableCollection<OrgViewModel> Orgs => Root.orgItems;

    /// <summary>The switcher shows once there's a second org.</summary>
    public bool HasOrgs => Root.orgs.HasOrgs;

    /// <summary>The org shown; the same in every tab.</summary>
    public OrgViewModel? SelectedOrg
    {
        get => Root.orgItems.FirstOrDefault(o => o.Id == Root.selectedOrgId);
        // Ctrl+click on the selected pill deselects it, writing null: it stays on the one it was.
        set { if (value is not null) Root.SelectOrg(value.Id); }
    }

    /// <summary>The shown org's person tabs, the ones the strip lists (with one org, every tab).</summary>
    public ObservableCollection<MainViewModel> OrgTabs => Root.orgTabs;

    /// <summary>The org this tab's person is in.</summary>
    public string OrgId => Root.orgs.OrgOfTab(tab);

    /// <summary>An org with no Claude login yet (only another LLM's, or nobody): the window says so instead of a person's tab.</summary>
    public bool IsOrgEmpty => HasOrgs && OrgTabs.Count == 0;

    public string OrgEmptyText => SelectedOrg is { } o
        ? $"{o.Name} has no Claude login yet ({o.Summary}). Add one with: orchestrator orgs member {o.Id} \"<person>\" <email>"
        : "";

    /// <summary>Takes the orgs read with the snapshot: the switcher's pills, and the org shown (the one chosen last time, kept
    /// across restarts; the default org once the one shown is removed).</summary>
    private void ApplyOrgs(OrgDirectory next)
    {
        var had = orgs.HasOrgs;
        orgs = next;
        if (!orgRestored)
        {
            orgRestored = true;
            if (OrgSelection.Remembered(store) is { } saved) selectedOrgId = saved;
        }
        if (next.Find(selectedOrgId) is null) selectedOrgId = Organization.DefaultId;
        orgItems.SyncBy(next.Orgs, o => o.Id, v => v.Id, o => new OrgViewModel(o), (_, _) => { });
        if (had != next.HasOrgs) foreach (var t in loginTabs) t.Raise(nameof(HasOrgs));
    }

    /// <summary>Each org's pill: its people and logins, and how many of its tabs need you.</summary>
    private void UpdateOrgPills()
    {
        foreach (var o in orgItems)
        {
            var tabs = loginTabs.Where(t => t.IsPersonTab && orgs.InOrg(o.Id, t.tab)).ToList();
            o.Update(orgs.Find(o.Id) ?? Organization.Default, OrgScope.Summary(orgs, o.Id, tabs.Count), tabs.Sum(t => t.WaitingCount));
        }
    }

    private void SelectOrg(string id)
    {
        if (id == selectedOrgId || orgs.Find(id) is null) return;
        selectedOrgId = id;
        try { OrgSelection.Remember(store, id); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        SyncOrgTabs();
        // The org's own queue rows, kept current by every plan: handed to the window as they are, nothing planned again.
        Proposals.ReorderTo([.. Queue.Where(q => q.IsNext)]);
        foreach (var t in loginTabs)
            foreach (var name in new[] { nameof(Queue), nameof(QueueCount) })
                t.Raise(name);
    }

    /// <summary>Lists the shown org's tabs in the strip and shows one of them: the tab on screen while it's the org's, else the
    /// org's first.</summary>
    private void SyncOrgTabs()
    {
        var wanted = loginTabs.Where(t => orgs.InOrg(selectedOrgId, t.tab)).ToList();
        // Move the selection off a tab leaving the strip before it's removed from the bound collection: a still-selected item
        // removed from a Selector-bound collection is the crash of M.ORCH-T859 (M.ORCH-T869).
        if (!wanted.Contains(SelectedTab) && wanted.Count > 0) Select(wanted[0]);
        if (!wanted.Contains(SelectedTab) && orgTabs.Contains(SelectedTab))
        {
            // No tab of the new org to move to (an org with no Claude login, M.ORCH-T1019): the strip lets go of the selected
            // tab instead, before it's removed, and the window keeps showing it.
            stripCleared = true;
            foreach (var t in loginTabs) t.Raise(nameof(StripSelection));
            try { orgTabs.ReorderTo(wanted); }
            finally
            {
                stripCleared = false;
                foreach (var t in loginTabs) t.Raise(nameof(StripSelection));
            }
        }
        else orgTabs.ReorderTo(wanted);
        foreach (var t in loginTabs)
        {
            t.Raise(nameof(SelectedOrg)); t.Raise(nameof(HasLoginTabs)); t.Raise(nameof(IsOrgEmpty)); t.Raise(nameof(OrgEmptyText));
        }
    }
}
