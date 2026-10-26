using System.Windows.Input;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Activity;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>"Go to tab" (M.ORCH-T619): a toast brings that CLI's own terminal up.</summary>
public sealed partial class MainViewModel
{
    /// <summary>The tab a toast about <paramref name="repo"/> goes to: the one whose CLI waits on you, else its only (or first) tab.</summary>
    internal static HostSession? HostForToast(RepoStatus? repo, IEnumerable<AgentBoardRow> agents)
    {
        if (repo is null || repo.Hosts.Count == 0) return null;
        var waiting = agents.Where(a => a.State == AgentState.Waiting && a.Host is not null).Select(a => a.Host!.ProcessId).ToHashSet();
        return repo.Hosts.FirstOrDefault(h => waiting.Contains(h.ProcessId)) ?? repo.Hosts[0];
    }

    private async Task OpenToastAsync(ToastViewModel t)
    {
        if (t.Target is null)
        {
            // An alert about one ticket opens it in Tickets Desktop, like every ticket id the dashboard shows (M.ORCH-T762).
            if (t.TicketId is { } id) OpenInTickets(id);
            return;
        }
        await GoToOrShowRepoAsync(HostForToast(t.Target.Status, snapshot?.Agents ?? []), t.Target.Name);
    }

    /// <summary>To the CLI's tab, or its window; when neither can be found, says why (the repo pane is Tickets Desktop's now,
    /// M.ORCH-T763).</summary>
    private async Task GoToOrShowRepoAsync(HostSession? host, string repo)
    {
        var result = host is null ? new GoToResult(GoToOutcome.NotFound, $"{repo} has no Launcher tab to go to.") : await GoToAsync(host);
        if (result.Outcome != GoToOutcome.NotFound) return;
        StatusMessage = result.Note;
    }

    private async Task<GoToResult> GoToAsync(HostSession host)
    {
        // A CLI in a Terminals panel here (M.ORCH-T810): its tab, on its person's page.
        if (Root.AllTerminals.Find(host.ProcessId) is { } pane)
        {
            Root.ShowPane(pane);
            return new GoToResult(GoToOutcome.Tab, "Brought up its terminal.");
        }
        var all = full?.Repos.SelectMany(r => r.Hosts).ToList() ?? [];
        try
        {
            // UI Automation over another process's windows can block: never on the UI thread.
            return await Task.Run(() => TerminalFocus.GoTo(host, all));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            OrchestratorLog.For("Dashboard").Error(ex, "Go to tab failed");
            return new GoToResult(GoToOutcome.NotFound, $"Couldn't go to that tab: {ex.Message}");
        }
    }
}
