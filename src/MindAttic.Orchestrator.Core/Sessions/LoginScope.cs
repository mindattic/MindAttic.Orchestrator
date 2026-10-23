using MindAttic.Orchestrator.Budget;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>One dashboard tab (M.ORCH-T613): a Claude login, the global <c>~/.claude</c> one (<see cref="Email"/> null) or a
/// per-CLI one under <c>~/.claude-accounts</c>, by the name its folder gives it (M.ORCH-T614) and its terminal tabs' color
/// (M.ORCH-T616), with its email for the tooltip. A roster account not signed in on this machine yet still has a tab
/// (M.ORCH-T615), <see cref="SignedIn"/> false: it shows only its Sign in until it is.</summary>
public sealed record LoginTab(string? Email, string Title, string ToolTip, string Color, bool SignedIn = true)
{
    /// <summary>The 'API key' tab's key: never an email, so never a login's.</summary>
    public const string ApiKeyKey = "api-key";

    /// <summary>The 'API key' tab (M.ORCH-T630): no login, but the Vault API key's billing, after every person's tab.</summary>
    public static readonly LoginTab ApiKey = new(null, "API key",
        "The Vault's API key: billing every CLI on it for emergencies, and back to Claude Team", "") { IsApiKey = true };

    /// <summary>Whether this is the 'API key' tab rather than a login's.</summary>
    public bool IsApiKey { get; init; }

    /// <summary>The 'API key' tab as billing has it (M.ORCH-T672): marked "(fallback)" while the API key bills, since a CLI
    /// started with no account of its own then runs on the key, else as <see cref="ApiKey"/>.</summary>
    public static LoginTab ApiKeyTab(bool billingIsApiKey) =>
        billingIsApiKey ? ApiKey with { Title = "API key (fallback)", ToolTip = "The Vault's API key, billing now: the fallback for a CLI with no login of its own" } : ApiKey;

    public bool IsGlobal => Email is null && !IsApiKey;

    /// <summary>The key a tab is kept by across refreshes: "" for the global login, <see cref="ApiKeyKey"/> for the 'API key'
    /// tab, else its email.</summary>
    public string Key => IsApiKey ? ApiKeyKey : Email ?? "";
}

/// <summary>
/// Splits one workspace snapshot into a view per Claude login (M.ORCH-T613), so the dashboard has one tab per login over the
/// one shared refresh rather than a refresh per tab: each login's CLIs, Launcher tabs and sessions, its repos' states as its
/// own CLIs leave them, and its own 5-hour and weekly meter and pace (the global login's from the snapshot, a per-CLI login's
/// from <see cref="Snapshot.Accounts"/>). Tickets, the audit and the history stay workspace-wide.
/// </summary>
public static class LoginScope
{
    /// <summary>The login a session runs on: its per-CLI login's email, or null for the global one (and any other provider's).</summary>
    public static string? LoginOf(Snapshot snapshot, string? sessionId) =>
        sessionId is not null && snapshot.OwnLogins.TryGetValue(sessionId, out var email) ? email : null;

    private static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <paramref name="all"/> as the tab for <paramref name="email"/> (null: the global login) sees it: every repo with its
    /// open tickets, but only that login's sessions and Launcher tabs, its state worked out again from them
    /// (<see cref="Classify"/>); only that login's agents; and that login's own budget, pace and utilization. A Launcher tab
    /// whose login isn't known shows on the global login's tab.
    /// </summary>
    public static Snapshot For(Snapshot all, string? email, TimeSpan workingWindow, TimeSpan waitingWindow)
    {
        var repos = all.Repos.Select(r =>
        {
            var sessions = r.Sessions.Where(s => Same(LoginOf(all, s.SessionId), email)).ToList();
            var hosts = r.Hosts.Where(h => Same(h.OwnLogin, email)).ToList();
            if (sessions.Count == r.Sessions.Count && hosts.Count == r.Hosts.Count) return r;
            DateTime? last = sessions.Count > 0 ? sessions.Max(s => s.LastWriteUtc) : null;
            var usage = sessions.Aggregate(default(TokenUsage), (sum, s) => sum + s.UsageToday);
            return r with
            {
                State = Classify(last, hosts.Count, all.TakenUtc, workingWindow, waitingWindow),
                Hosts = hosts, Sessions = sessions, LastActivityUtc = last, UsageToday = usage,
            };
        }).ToList();
        var scoped = all with { Repos = repos, Agents = [.. all.Agents.Where(a => Same(a.OwnLogin, email))] };
        if (email is null) return scoped;
        var own = all.Accounts.FirstOrDefault(a => Same(a.Email, email));
        return scoped with
        {
            Budget = own?.Budget, Pace = own?.Pace, Utilization = own?.Utilization, Account = own?.Account,
            // Codex's and Gemini's usage runs on no Claude login: the global tab shows it.
            ProviderUsage = [.. all.ProviderUsage.Where(u => u.Provider == Providers.Claude)],
        };
    }

    /// <summary>A repo's state from its last write and its open Launcher tabs, as <see cref="StatusAggregator"/> classifies it.</summary>
    public static RepoState Classify(DateTime? lastActivityUtc, int hostCount, DateTime nowUtc, TimeSpan workingWindow, TimeSpan waitingWindow)
    {
        var age = lastActivityUtc is null ? TimeSpan.MaxValue : nowUtc - lastActivityUtc.Value;
        if (age <= workingWindow) return RepoState.Working;
        if (hostCount > 0 || age <= waitingWindow) return RepoState.Waiting;
        return RepoState.Dormant;
    }

    /// <summary>
    /// The dashboard's tabs: the global login first, then each per-CLI login signed in (the ones <see cref="Snapshot.Accounts"/>
    /// paces) or with a live CLI or Launcher tab on it, and every member of the organization (<paramref name="members"/>,
    /// M.ORCH-T670) not signed in on this machine, which gets a tab that only signs it in (M.ORCH-T615); by email. A member's
    /// tab is titled by its member name, else its login's name (<see cref="LoginLook.Alias"/>), else its email's part before the
    /// @; any other login's by its login's name. The global one is its member's own seat, never marked a fallback (the API key tab is, while the key bills), each has its email in the tooltip and
    /// its terminal tabs' color as its accent. The global login's own account gets no second tab: the fallback one is its.
    /// </summary>
    public static IReadOnlyList<LoginTab> Tabs(Snapshot snapshot, AccountDirs dirs, IEnumerable<Member>? members = null) =>
        Tabs(snapshot, email => LoginLook.For(dirs.For(email).Directory, email), LoginLook.For(dirs.ClaudeDir, snapshot.GlobalEmail), members);

    internal static IReadOnlyList<LoginTab> Tabs(Snapshot snapshot, Func<string, LoginLook> ownLook, LoginLook globalLook, IEnumerable<Member>? members = null)
    {
        var byEmail = new Dictionary<string, Member>(StringComparer.Ordinal);
        foreach (var m in members ?? [])
            if (AccountDirs.Normalize(m.Email) is { } key) byEmail.TryAdd(key, m);
        string Title(string email, string alias) => byEmail.TryGetValue(email, out var m) ? MemberRoster.TitleOf(m, alias) : alias;
        var globalEmail = AccountDirs.Normalize(snapshot.GlobalEmail);
        // While the API key bills, the fallback is the key (its tab is marked), and the global login's tab is any member's.
        var onKey = snapshot.Billing?.Mode == Billing.BillingModes.Api;
        var global = snapshot.GlobalEmail is { } g
            ? onKey
                ? new LoginTab(null, Title(globalEmail ?? g, globalLook.Alias), $"{g}: the global login (~/.claude)", globalLook.TabColor)
                : new LoginTab(null, Title(globalEmail ?? g, globalLook.Alias), $"{g}: the global login (~/.claude), this member's own seat", globalLook.TabColor)
            : onKey
                ? new LoginTab(null, "Global login", "The global login (~/.claude): nobody's email is known for it yet", globalLook.TabColor)
                : new LoginTab(null, "Global login", "The global login (~/.claude): nobody's email is known for it yet", globalLook.TabColor);
        var live = snapshot.Accounts.Select(a => a.Email)
            .Concat(snapshot.Agents.Select(a => a.OwnLogin))
            .Concat(snapshot.Repos.SelectMany(r => r.Hosts).Select(h => h.OwnLogin))
            .Select(AccountDirs.Normalize).OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        var waiting = byEmail.Keys
            .Where(e => !live.Contains(e) && e != globalEmail)
            .ToHashSet(StringComparer.Ordinal);
        return [global, .. live.Concat(waiting).Order(StringComparer.Ordinal).Select(e =>
        {
            var look = ownLook(e);
            var title = Title(e, look.Alias);
            return waiting.Contains(e)
                ? new LoginTab(e, title, $"{e}: not signed in on this machine yet; sign in to see its dashboard", look.TabColor, SignedIn: false)
                : new LoginTab(e, title, $"{e}: its own login (~/.claude-accounts)", look.TabColor);
        })];
    }
}
