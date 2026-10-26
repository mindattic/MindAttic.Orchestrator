using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>Session restore (M.ORCH-T862): the dashboard's terminal tabs as a <see cref="RestoreState"/>, and reopening one's.</summary>
public sealed partial class MainViewModel
{
    /// <summary>The tabs a restore reopened, each with whether it was its person's chosen one, and the one on screen.</summary>
    private List<(TerminalPaneViewModel Pane, bool Chosen)>? restored;
    private TerminalPaneViewModel? restoredShown;
    private string? restoredPerson;

    /// <summary>Every terminal tab now, the session its CLI is in, who's shown and which tab (global login's tab; UI thread).</summary>
    internal RestoreState CaptureRestore(string reason, bool clean)
    {
        var all = Root.AllTerminals;
        var panes = all.Panes.Where(p => !p.Ended).ToList();
        var tabs = panes.Select(p => new RestoreTab
        {
            Request = p.Request,
            SessionId = p.SessionId,
            Login = p.Login,
            Chosen = Root.loginTabs.Any(t => ReferenceEquals(t.Terminals?.SelectedPane, p)),
            // Its TermHost session (M.ORCH-T926): while that host lives the next dashboard reattaches the tab, so a restore
            // never offers it (M.ORCH-T928). Null for an in-process tab.
            HostId = p.IsHosted ? p.TermId : null,
        }).ToList();
        var shown = all.SelectedPane is { } s ? panes.IndexOf(s) : -1;
        return new RestoreState
        {
            SavedUtc = DateTime.UtcNow,
            Clean = clean,
            Reason = reason,
            Pid = Environment.ProcessId,
            SelectedPerson = Root.SelectedTab.tab.Key,
            SelectedTab = shown >= 0 ? shown : null,
            Tabs = tabs,
        };
    }

    /// <summary>
    /// Reopens <paramref name="state"/>'s tabs (<see cref="SessionRestoreStore.ReopenRequest"/>): each Claude CLI on its login
    /// with <c>claude --resume</c>, or fresh in its folder when its session isn't known; a Launcher menu pane as one, unless that
    /// login's is already open. Each tab is contained on its own. Returns a line per tab of what was done, for the log.
    /// </summary>
    internal IReadOnlyList<string> RestoreTabs(RestoreState state)
    {
        if (!IsGlobalTab) return Root.RestoreTabs(state);
        var lines = new List<string>();
        restored = [];
        restoredShown = null;
        restoredPerson = state.SelectedPerson;
        var dirs = new AccountDirs();
        string ConfigDirOf(string? login) => login is null ? Setup.AgentGate.ClaudeDir : dirs.For(login).Directory;
        for (var i = 0; i < state.Tabs.Count; i++)
        {
            var tab = state.Tabs[i];
            try
            {
                var request = SessionRestoreStore.ReopenRequest(tab, ConfigDirOf, LoginLook.ForConfigDir);
                if (request.IsMenu && AllTerminals.Panes.Any(p => p.Request.IsMenu && !p.Ended &&
                        string.Equals(p.Login, request.Login, StringComparison.OrdinalIgnoreCase)))
                {
                    lines.Add($"{request.Title}: its Launcher menu was already open");
                    continue;
                }
                var before = AllTerminals.Panes.Count;
                if (AllTerminals.Open(request) is { } error)
                {
                    lines.Add($"{request.Title} in {request.Directory}: not reopened ({error})");
                    continue;
                }
                if (AllTerminals.Panes.Count <= before) continue;
                var pane = AllTerminals.Panes[^1];
                if (!request.IsMenu && tab.SessionId is { Length: > 0 } session && string.Equals(request.Provider, "Claude", StringComparison.OrdinalIgnoreCase))
                    pane.SessionId = session;
                restored.Add((pane, tab.Chosen));
                if (state.SelectedTab == i) restoredShown = pane;
                lines.Add(request.IsMenu ? $"{request.Title}: reopened the Launcher menu"
                    : pane.SessionId is { } s ? $"{request.Title} in {request.Directory}: reopened, resuming session {s}"
                    : $"{request.Title} in {request.Directory}: reopened fresh (no session known)");
            }
            catch (Exception ex)
            {
                OrchestratorLog.For("Restore").Error(ex, "Reopening {Title:l} failed; contained", tab.Request.Title);
                lines.Add($"{tab.Request.Title}: not reopened ({ex.Message})");
            }
        }
        if (restored.Count > 0)
            StatusMessage = $"Restored {restored.Count} of {state.Tabs.Count} terminal tab(s) from the last session.";
        return lines;
    }

    /// <summary>After the first refresh (every login's tab is up by then): each restored person's chosen tab chosen again, then
    /// the person and the tab that were on screen.</summary>
    internal void ReselectRestored()
    {
        if (!IsGlobalTab) { Root.ReselectRestored(); return; }
        if (restored is null) return;
        try
        {
            SyncTerminals();
            foreach (var (pane, chosen) in restored)
                if (chosen && AllTerminals.Panes.Contains(pane)) OwnerOf(pane).Terminals.SelectedPane = pane;
            if (restoredShown is { } shown && AllTerminals.Panes.Contains(shown)) ShowPane(shown);
            else if (restoredPerson is { } key && loginTabs.FirstOrDefault(t => string.Equals(t.tab.Key, key, StringComparison.OrdinalIgnoreCase)) is { } person)
            {
                if (person.OrgId != selectedOrgId) SelectOrg(person.OrgId);
                Select(person);
            }
        }
        catch (Exception ex) { OrchestratorLog.For("Restore").Error(ex, "Reselecting the restored tabs failed; contained"); }
        finally
        {
            restored = null;
            restoredShown = null;
            restoredPerson = null;
        }
    }
}
