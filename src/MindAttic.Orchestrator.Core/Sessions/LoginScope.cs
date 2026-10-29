using MindAttic.Orchestrator.Budget;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>One dashboard tab (M.ORCH-T613): a Claude login, the global <c>~/.claude</c> one (<see cref="Email"/> null) or a
/// per-CLI one under <c>~/.claude-accounts</c>, by the name its folder gives it (M.ORCH-T614) and its terminal tabs' color
/// (M.ORCH-T616), with its email for the tooltip. A roster account not signed in on this machine yet still has a tab
/// (M.ORCH-T615), <see cref="SignedIn"/> false: it shows only its Sign in until it is.</summary>
public sealed record LoginTab(string? Email, string Title, string ToolTip, string Color, bool SignedIn = true)
{
    /// <summary>The API key person's tab key (M.ORCH-T906): its email, as any login's.</summary>
    public const string ApiKeyKey = Billing.ApiKeyPerson.Email;

    /// <summary>The API key's person tab (M.ORCH-T906, which replaced M.ORCH-T630's 'API key' billing tab): a member like any
    /// other, on its own login folder (<see cref="Billing.ApiKeyPerson"/>), its CLIs billing API credits. "Signed in" while the
    /// MindAttic Vault holds a Claude key.</summary>
    public static LoginTab ApiKey(bool hasKey) =>
        new(Billing.ApiKeyPerson.Email, Billing.ApiKeyPerson.Name,
            hasKey ? "The Vault's API key, as a person: its own Launcher and CLIs bill API credits, never a Claude Team seat"
                   : "The Vault's API key, as a person: there's no Claude key in the MindAttic Vault keyring yet",
            Billing.ApiKeyPerson.Color, hasKey) { IsApiKey = true };

    /// <summary>Whether this is the API key's person tab rather than a Claude login's.</summary>
    public bool IsApiKey { get; init; }

    public bool IsGlobal => Email is null && !IsApiKey;

    /// <summary>The key a tab is kept by across refreshes: "" for the global login, else its email.</summary>
    public string Key => Email ?? "";
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
        var scoped = Keep(all, login => Same(login, email), workingWindow, waitingWindow);
        if (email is null) return scoped;
        var own = all.Accounts.FirstOrDefault(a => Same(a.Email, email));
        return scoped with
        {
            Budget = own?.Budget, Pace = own?.Pace, Utilization = own?.Utilization, Account = own?.Account,
            // Codex's and Gemini's usage runs on no Claude login: the global tab shows it.
            ProviderUsage = [.. all.ProviderUsage.Where(u => u.Provider == Providers.Claude)],
        };
    }

    /// <summary>
    /// <paramref name="all"/> with only the sessions, Launcher tabs and agents on the logins <paramref name="keep"/> takes (its
    /// email, null for the global login), each repo's state worked out again from what's left: a login's tab keeps one login,
    /// an org's view (<see cref="OrgScope"/>) its members' (M.ORCH-T655).
    /// </summary>
    internal static Snapshot Keep(Snapshot all, Func<string?, bool> keep, TimeSpan workingWindow, TimeSpan waitingWindow)
    {
        var repos = all.Repos.Select(r =>
        {
            var sessions = r.Sessions.Where(s => keep(LoginOf(all, s.SessionId))).ToList();
            var hosts = r.Hosts.Where(h => keep(h.OwnLogin)).ToList();
            if (sessions.Count == r.Sessions.Count && hosts.Count == r.Hosts.Count) return r;
            DateTime? last = sessions.Count > 0 ? sessions.Max(s => s.LastWriteUtc) : null;
            var usage = sessions.Aggregate(default(TokenUsage), (sum, s) => sum + s.UsageToday);
            return r with
            {
                State = Classify(last, hosts.Count, all.TakenUtc, workingWindow, waitingWindow),
                Hosts = hosts, Sessions = sessions, LastActivityUtc = last, UsageToday = usage,
            };
        }).ToList();
        return all with { Repos = repos, Agents = [.. all.Agents.Where(a => keep(a.OwnLogin))] };
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
    /// The dashboard's tabs, sorted by title case-insensitively (M.ORCH-T752): the global login, and each per-CLI login signed in (the ones <see cref="Snapshot.Accounts"/>
    /// paces) or with a live CLI or Launcher tab on it, and every member of the organization (<paramref name="members"/>,
    /// M.ORCH-T670) not signed in on this machine, which gets a tab that only signs it in (M.ORCH-T615). A member's
    /// tab is titled by its member name, else its login's name (<see cref="LoginLook.Alias"/>), else its email's part before the
    /// @; any other login's by its login's name. The global one is its member's own seat, never marked a fallback (the API key tab is, while the key bills), each has its email in the tooltip and
    /// its terminal tabs' color as its accent. The global login's own account gets no second tab: the fallback one is its.
    /// </summary>
    /// <para>The API key's person (M.ORCH-T906) has its tab after everyone's: <paramref name="apiKey"/> says whether the
    /// Vault holds a key for it (its "signed in"); null leaves it out.</para>
    public static IReadOnlyList<LoginTab> Tabs(Snapshot snapshot, AccountDirs dirs, IEnumerable<Member>? members = null, bool? apiKey = null) =>
        Tabs(snapshot, email => LoginLook.For(dirs.For(email).Directory, email), LoginLook.For(dirs.ClaudeDir, snapshot.GlobalEmail), members, apiKey,
            dirs.All().Select(d => (d.Email, d.SignedIn)));

    /// <summary><paramref name="knownLogins"/> (M.ORCH-T1254): every per-CLI login folder on this machine, signed in or not, so
    /// one a tick's <paramref name="snapshot"/> happens to carry no live session, agent or host for (a cutoff just set, a
    /// transient read, between CLIs) still keeps its tab rather than dropping out of the strip -- only a login this machine has
    /// never heard of (no folder, no roster row) goes without one.</summary>
    internal static IReadOnlyList<LoginTab> Tabs(Snapshot snapshot, Func<string, LoginLook> ownLook, LoginLook globalLook, IEnumerable<Member>? members = null,
        bool? apiKey = null, IEnumerable<(string Email, bool SignedIn)>? knownLogins = null)
    {
        var byEmail = new Dictionary<string, Member>(StringComparer.Ordinal);
        foreach (var m in members ?? [])
            if (AccountDirs.Normalize(m.Email) is { } key && !Billing.ApiKeyPerson.Is(key)) byEmail.TryAdd(key, m);
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
                : new LoginTab(null, "Global login", "The global login (~/.claude): nobody's email is known for it yet, this member's own seat", globalLook.TabColor);
        var live = snapshot.Accounts.Select(a => a.Email)
            .Concat(snapshot.Agents.Select(a => a.OwnLogin))
            .Concat(snapshot.Repos.SelectMany(r => r.Hosts).Select(h => h.OwnLogin))
            .Select(AccountDirs.Normalize).OfType<string>()
            .Where(e => e != globalEmail && !Billing.ApiKeyPerson.Is(e))
            .ToHashSet(StringComparer.Ordinal);
        var knownSignedIn = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var (email, signedIn) in knownLogins ?? [])
            if (AccountDirs.Normalize(email) is { } k && k != globalEmail && !Billing.ApiKeyPerson.Is(k))
                knownSignedIn[k] = knownSignedIn.TryGetValue(k, out var already) ? already || signedIn : signedIn;
        var waiting = byEmail.Keys.Concat(knownSignedIn.Keys)
            .Where(e => !live.Contains(e) && e != globalEmail)
            .ToHashSet(StringComparer.Ordinal);
        var own = live.Concat(waiting).Order(StringComparer.Ordinal).Select(e =>
        {
            var look = ownLook(e);
            var title = Title(e, look.Alias);
            // A known per-CLI login folder (M.ORCH-T1254) keeps its real sign-in state even while waiting: it's a login this
            // machine already knows, just one the snapshot happens to carry nothing live for this tick, not one still to sign
            // in to.
            var signedIn = !waiting.Contains(e) || knownSignedIn.GetValueOrDefault(e, false);
            return signedIn
                ? new LoginTab(e, title, $"{e}: its own login (~/.claude-accounts)", look.TabColor)
                : new LoginTab(e, title, $"{e}: not signed in on this machine yet; sign in to see its dashboard", look.TabColor, SignedIn: false);
        });
        // By the name each shows, case-insensitive (M.ORCH-T752): the global login's tab is just its member's, so it sorts
        // with the others; ties by email, the global one first. The API key's person goes after all of them (M.ORCH-T906).
        return [.. own.Prepend(global)
            .OrderBy(t => t.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.Email ?? "", StringComparer.Ordinal), .. apiKey is { } hasKey ? [LoginTab.ApiKey(hasKey)] : Array.Empty<LoginTab>()];
    }
}
