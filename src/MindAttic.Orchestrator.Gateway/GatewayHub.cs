using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Gateway;

/// <summary>
/// An access decision (hub phase 2, M.ORCH-T1641): the most a viewer may do with a session; when it is interact through another
/// member's grant on a member's CLI, the consent it waits on (<see cref="ConsentKey"/>); and the session's owning member, if any.
/// </summary>
internal sealed record GatewayDecision(GatewayAccess Access, string? ConsentKey = null, string? SessionOwner = null);

/// <summary>Where a consent request stands.</summary>
internal enum ConsentState
{
    /// <summary>Never asked (or the last request lapsed, so it may be asked again).</summary>
    None,

    /// <summary>Asked; waiting for the session owner within the window.</summary>
    Pending,

    /// <summary>The session owner said yes: the grant takes effect.</summary>
    Allowed,

    /// <summary>The session owner said no: watch only, for as long as the grant lasts.</summary>
    Denied,
}

/// <summary>A consent request, as <c>GET /consents</c> lists it to the session's owner: never a token, never content.</summary>
internal sealed record ConsentRequest(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("session")] string Session,
    [property: JsonPropertyName("viewer")] string Viewer,
    [property: JsonPropertyName("owner")] string Owner,
    [property: JsonPropertyName("askedUtc")] DateTime AskedUtc,
    [property: JsonPropertyName("deadlineUtc")] DateTime DeadlineUtc)
{
    [JsonIgnore] internal string Key { get; init; } = "";
}

/// <summary><c>POST /consents/{id}</c>'s body.</summary>
internal sealed record ConsentAnswer([property: JsonPropertyName("allow")] bool? Allow);

/// <summary><c>POST /sessions</c>'s body (hub phase 2): the repo to start a CLI in, and the login to run it on (the caller's own
/// email when left out).</summary>
internal sealed record StartRequest(
    [property: JsonPropertyName("repo")] string? Repo,
    [property: JsonPropertyName("login")] string? Login);

/// <summary>
/// The session owners' consents (hub phase 2, M.ORCH-T1641; docs/research/hub-and-spoke.md §3.4): the first use of another
/// member's interact grant on a member's CLI asks that CLI's owner, who answers through <c>POST /consents/{id}</c> (a toast on
/// their dashboard, or <c>orchestrator machine consents</c>). No answer within the window leaves the viewer watching; the request
/// lapses and the next attach asks again. An answer lasts as long as the grant (its key names the grant, expiry included), and is
/// kept in memory only: a restarted gateway asks again. Bounded, so a flood of attaches can't grow it.
/// </summary>
internal sealed class GatewayConsents(Func<DateTime>? clock = null)
{
    /// <summary>The most requests kept waiting at once; past it, the oldest is let go (that viewer is left watching).</summary>
    public const int MaxPending = 256;

    /// <summary>The most answers kept; past it, the oldest is let go (that grant is asked about again).</summary>
    public const int MaxAnswers = 4096;

    private readonly Func<DateTime> now = clock ?? (() => DateTime.UtcNow);
    private readonly System.Threading.Lock gate = new();
    private readonly Dictionary<string, ConsentRequest> pending = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (bool Allow, DateTime AtUtc)> answers = new(StringComparer.Ordinal);

    /// <summary>Where the consent <paramref name="key"/> names stands now.</summary>
    public ConsentState State(string key)
    {
        lock (gate)
        {
            if (answers.TryGetValue(key, out var a)) return a.Allow ? ConsentState.Allowed : ConsentState.Denied;
            Prune();
            return pending.Values.Any(p => p.Key == key) ? ConsentState.Pending : ConsentState.None;
        }
    }

    /// <summary>Asks <paramref name="sessionOwner"/> whether <paramref name="viewer"/> may type into <paramref name="session"/> under
    /// the grant <paramref name="key"/> names, waiting <paramref name="window"/>; the request already waiting when there is one, and
    /// null once it was answered.</summary>
    public ConsentRequest? Ask(string key, string sessionOwner, string viewer, string session, TimeSpan window)
    {
        lock (gate)
        {
            if (answers.ContainsKey(key)) return null;
            Prune();
            if (pending.Values.FirstOrDefault(p => p.Key == key) is { } waiting) return waiting;
            while (pending.Count >= MaxPending) pending.Remove(pending.Values.OrderBy(p => p.AskedUtc).First().Id);
            var at = now();
            var request = new ConsentRequest(Guid.NewGuid().ToString("N")[..12], session, viewer, sessionOwner, at, at + window) { Key = key };
            pending[request.Id] = request;
            return request;
        }
    }

    /// <summary>The requests waiting on <paramref name="sessionOwner"/>'s answer, oldest first.</summary>
    public IReadOnlyList<ConsentRequest> PendingFor(string sessionOwner)
    {
        var who = TermMemberKeys.Normalize(sessionOwner);
        lock (gate)
        {
            Prune();
            return [.. pending.Values.Where(p => p.Owner == who).OrderBy(p => p.AskedUtc)];
        }
    }

    /// <summary>Records <paramref name="sessionOwner"/>'s answer to request <paramref name="id"/>; null when there is no such request
    /// waiting on them (unknown, lapsed, or another member's), else the request answered.</summary>
    public ConsentRequest? Answer(string id, string sessionOwner, bool allow)
    {
        var who = TermMemberKeys.Normalize(sessionOwner);
        lock (gate)
        {
            Prune();
            if (!pending.TryGetValue(id.Trim(), out var request) || request.Owner != who) return null;
            pending.Remove(request.Id);
            while (answers.Count >= MaxAnswers) answers.Remove(answers.OrderBy(a => a.Value.AtUtc).First().Key);
            answers[request.Key] = (allow, now());
            return request;
        }
    }

    /// <summary>Lets lapsed requests go: their viewers stay watching, and a later attach asks again.</summary>
    private void Prune()
    {
        var at = now();
        foreach (var id in pending.Values.Where(p => p.DeadlineUtc <= at).Select(p => p.Id).ToList()) pending.Remove(id);
    }
}

/// <summary>The Windows account a hosted session runs under (hub phase 2): decides whether it may be typed into by anyone but the
/// machine owner (<see cref="TermHubPolicy.MayInteract"/>).</summary>
internal interface ISessionAccounts
{
    string? AccountOf(TermSessionRecord session);
}

/// <summary>
/// Every hosted session runs as this gateway's own Windows user: what a session is when the gateway didn't start it under a member's
/// account. <see cref="MemberAwareAccounts"/> (hub phase 2b, M.ORCH-T1667) answers the member's account for the hosts its ledger
/// says it started under one, and falls back to this for every other session.
/// </summary>
internal sealed class HostUserAccounts : ISessionAccounts
{
    public static readonly HostUserAccounts Instance = new();
    public string? AccountOf(TermSessionRecord session) => $"{Environment.UserDomainName}\\{Environment.UserName}";
}

/// <summary>What starting or stopping a CLI through the gateway came to.</summary>
internal sealed record LaunchResult(bool Ok, string Output, bool NotSupported = false);

/// <summary>Starts and stops CLIs on this host for <c>POST /sessions</c> and <c>DELETE /sessions/{id}</c> (hub phase 2).</summary>
internal interface ISessionLauncher
{
    /// <summary>Starts a CLI in <paramref name="repo"/> on <paramref name="login"/> as <paramref name="plan"/> says.</summary>
    Task<LaunchResult> StartAsync(string repo, string login, TermLaunchPlan plan, CancellationToken ct);

    /// <summary>Ends <paramref name="session"/>'s TermHost (and the CLI under it).</summary>
    LaunchResult Stop(TermSessionRecord session);
}

/// <summary>Neither: a gateway without member sessions (tests, and the default).</summary>
internal sealed class NoLauncher : ISessionLauncher
{
    public static readonly NoLauncher Instance = new();
    public Task<LaunchResult> StartAsync(string repo, string login, TermLaunchPlan plan, CancellationToken ct) =>
        Task.FromResult(new LaunchResult(false, "This gateway doesn't start CLIs.", NotSupported: true));
    public LaunchResult Stop(TermSessionRecord session) => new(false, "This gateway doesn't stop CLIs.", NotSupported: true);
}

/// <summary>
/// The real launcher: <c>orchestrator start &lt;repo&gt; --account &lt;login&gt;</c> (the Launcher opens the hosted CLI, as the
/// dashboard's own start does) as the host's user, and a stop that ends the session's TermHost process tree once its pid and start
/// time still name it. A <see cref="TermLaunchKind.MemberUser"/> plan (hub phase 2b, M.ORCH-T1667) goes to
/// <paramref name="members"/>, which starts the TermHost under the member's own account; without one it is refused as not supported
/// (501). A member's host is stopped by ending the TermHost alone (its ConPTY takes the CLI with it): the peer may end that one
/// process, not the member's other processes.
/// </summary>
internal sealed class OrchestratorLauncher(MemberSessionLauncher? members = null) : ISessionLauncher
{
    public async Task<LaunchResult> StartAsync(string repo, string login, TermLaunchPlan plan, CancellationToken ct)
    {
        if (plan.Kind == TermLaunchKind.MemberUser)
        {
            if (members is null || string.IsNullOrWhiteSpace(plan.Account))
                return new LaunchResult(false, $"This host's gateway doesn't start CLIs under members' own Windows accounts ({plan.Account}), so nothing was started.", NotSupported: true);
            return await members.StartAsync(repo, login, plan.Account, ct).ConfigureAwait(false);
        }
        if (plan.Kind != TermLaunchKind.HostUser) return new LaunchResult(false, plan.Why ?? "Not started.");
        var (code, output) = await TicketsRunner.RunStartAsync(repo, login, ct).ConfigureAwait(false);
        return new LaunchResult(code == 0, output);
    }

    public LaunchResult Stop(TermSessionRecord session)
    {
        if (!TermSessions.IsHostRunning(session)) return new LaunchResult(false, "That CLI isn't running any more.");
        var member = members?.Ledger.Of(session);
        try
        {
            using var p = Process.GetProcessById(session.HostPid);
            p.Kill(entireProcessTree: member is null);
            if (member is not null) members!.Ledger.Remove(member.Id);
            return new LaunchResult(true, $"Stopped {session.Id}.");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return new LaunchResult(false, $"Couldn't stop it: {ex.Message}");
        }
    }
}

/// <summary>Shapes the gateway accepts from a start request.</summary>
internal static partial class HubNames
{
    /// <summary>A repo folder name: letters, digits, dot, dash and underscore, never a path or a leading dot.</summary>
    public static bool IsRepo(string? repo) => repo is { Length: > 0 and <= 100 } r && !r.StartsWith('.') && RepoName().IsMatch(r);

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]*$")]
    private static partial Regex RepoName();
}
