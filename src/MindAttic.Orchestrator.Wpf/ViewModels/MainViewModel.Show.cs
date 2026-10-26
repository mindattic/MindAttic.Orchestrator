using MindAttic.Orchestrator.Setup;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// <c>MindAttic.Orchestrator.Desktop.exe --show &lt;email|session|ticket&gt;</c> (M.ORCH-T763), Tickets Desktop's deep link: the window
/// opens the person the target names (a login's email), or the person whose CLI runs that session or that ticket, switching
/// org first when the person is in another, and says what it showed; a target nothing matches is said, not guessed at.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>Shows what <paramref name="target"/> names, once the first snapshot is in; true when something matched.</summary>
    public async Task<bool> ShowAsync(string target)
    {
        if (root is not null) return await root.ShowAsync(target);
        for (var waited = 0; IsLoading && waited < 600; waited++) await Task.Delay(100);
        var log = OrchestratorLog.For("Dashboard");
        var kind = DashboardRequest.KindOf(target);
        var globalEmail = workspace?.GlobalEmail;
        var found = loginTabs.FirstOrDefault(t => t.IsPersonTab &&
            ShowMatches(kind, target, t.ScopeEmail ?? globalEmail, t.Agents.Select(a => (a.SessionId, (Func<string, bool>)a.Holds))));
        if (found is null)
        {
            var what = kind switch { ShowKind.Person => "No person here has the login", ShowKind.Ticket => "No running CLI is on", _ => "No running CLI has the session" };
            log.Information("--show {Target:l}: nothing matched", target);
            StatusMessage = $"{what} {target}.";
            Toasts.Insert(0, new ToastViewModel("Nothing to show", StatusMessage, Palette.Waiting, null));
            return false;
        }
        if (found.OrgId != selectedOrgId) SelectOrg(found.OrgId);
        Select(found);
        found.StatusMessage = kind switch
        {
            ShowKind.Person => $"Showing {found.TabTitle}, from Tickets Desktop.",
            ShowKind.Ticket => $"{target} is running on {found.TabTitle}'s login.",
            _ => $"Session {target} is running on {found.TabTitle}'s login.",
        };
        log.Information("--show {Target:l}: showing {Tab:l}", target, found.TabTitle);
        return true;
    }

    /// <summary>Whether a person with login <paramref name="email"/> and CLIs <paramref name="agents"/> (each its session id and
    /// whether it's on a ticket) is what <paramref name="target"/> names: their email; a session id, or its start (at least 8
    /// characters); or a ticket one of their CLIs is on.</summary>
    internal static bool ShowMatches(ShowKind kind, string target, string? email, IEnumerable<(string Session, Func<string, bool> Holds)> agents) => kind switch
    {
        ShowKind.Person => string.Equals(email, target, StringComparison.OrdinalIgnoreCase),
        ShowKind.Ticket => agents.Any(a => a.Holds(target)),
        _ => agents.Any(a => string.Equals(a.Session, target, StringComparison.OrdinalIgnoreCase) ||
                             (target.Length >= 8 && a.Session.StartsWith(target, StringComparison.OrdinalIgnoreCase))),
    };
}
