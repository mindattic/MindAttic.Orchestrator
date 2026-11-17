using System.Text.Json.Serialization;

namespace MindAttic.Orchestrator.Term;

/// <summary>An org member and the Windows account their CLIs on the host run under (hub phase 2, M.ORCH-T1641).</summary>
public sealed record TermHubAccount(
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("account")] string Account);

/// <summary>How a member's CLI on the host would be started (<see cref="TermHubPolicy.PlanLaunch"/>).</summary>
public enum TermLaunchKind
{
    /// <summary>Not at all: <see cref="TermLaunchPlan.Why"/> says why.</summary>
    Refused,

    /// <summary>As the host's own Windows user, as every CLI runs today (the owner's, or a member's under "trusted org").</summary>
    HostUser,

    /// <summary>As the member's own Windows account (<see cref="TermLaunchPlan.Account"/>), with its own profile and DPAPI scope.</summary>
    MemberUser,
}

/// <summary>A launch decision: how, under which account (for <see cref="TermLaunchKind.MemberUser"/>), or why not.</summary>
public sealed record TermLaunchPlan(TermLaunchKind Kind, string? Account = null, string? Why = null)
{
    public static TermLaunchPlan Host { get; } = new(TermLaunchKind.HostUser);
    public static TermLaunchPlan Refuse(string why) => new(TermLaunchKind.Refused, null, why);
}

/// <summary>
/// Hub phase 2 (M.ORCH-T1641; docs/research/hub-and-spoke.md §3.4, §7): what org members may do with CLIs on the central host,
/// as the Vault's settings say it, carried to the gateway inside the ACL snapshot (<see cref="TermGatewayAclSnapshot.Hub"/>). The
/// snapshot has none (null) unless the Vault's <c>HubMemberSessions</c> is on and the machine is the host, so a standalone
/// install, a client and today's host behave exactly as before.
/// <list type="bullet">
/// <item><b>Session ownership follows the login</b>: the owner of a session is the member whose Claude login it runs on.</item>
/// <item><b>Typing needs isolation</b>: a member types into a CLI on the host only when it runs under its owner's own Windows
/// account (<see cref="WindowsUsers"/>, with that account in <see cref="Accounts"/>), or when <see cref="TrustedOrg"/> says the
/// org accepts that every CLI runs as the host's one user, which can read every login's credentials.</item>
/// <item><b>Consent</b>: another member's interact grant on a member's session takes effect only once that session's owner says
/// yes, within <see cref="ConsentSeconds"/>; no answer leaves the viewer watching.</item>
/// </list>
/// Never holds a secret: the account names are names, never passwords.
/// </summary>
public sealed record TermHubPolicy
{
    /// <summary>The default consent window, seconds (the plan's 30 s).</summary>
    public const double DefaultConsentSeconds = 30;

    /// <summary>Members start, stop and type into their own CLIs on the host (the master switch; the policy is only written
    /// while it's on, so it's always true in a snapshot that has one).</summary>
    [JsonPropertyName("memberSessions")] public bool MemberSessions { get; init; }

    /// <summary>The org accepts that members' CLIs run as the host's one Windows user: members may type into them (and start
    /// them) without per-member Windows users. Off by default.</summary>
    [JsonPropertyName("trustedOrg")] public bool TrustedOrg { get; init; }

    /// <summary>Members' CLIs run under their own Windows accounts (<see cref="Accounts"/>). Off by default.</summary>
    [JsonPropertyName("windowsUsers")] public bool WindowsUsers { get; init; }

    /// <summary>How long a consent request waits for the session owner's answer before the viewer is left watching.</summary>
    [JsonPropertyName("consentSeconds")] public double ConsentSeconds { get; init; } = DefaultConsentSeconds;

    /// <summary>The default wait for a member's TermHost to register after it's started under their account, seconds.</summary>
    public const double DefaultStartWaitSeconds = 15;

    /// <summary>How long the gateway waits for a TermHost it started under a member's own account to write its registry file before
    /// answering the start (hub phase 2b, M.ORCH-T1667); the Vault's HubMemberStartWaitSeconds.</summary>
    [JsonPropertyName("startWaitSeconds")] public double StartWaitSeconds { get; init; } = DefaultStartWaitSeconds;

    /// <summary>The start wait, clamped to something sane.</summary>
    [JsonIgnore] public TimeSpan StartWait => TimeSpan.FromSeconds(Math.Clamp(double.IsFinite(StartWaitSeconds) ? StartWaitSeconds : DefaultStartWaitSeconds, 1, 300));

    /// <summary>The members whose own Windows account the host found (only while <see cref="WindowsUsers"/> is on).</summary>
    [JsonPropertyName("accounts")] public List<TermHubAccount> Accounts { get; init; } = [];

    /// <summary>Whether the hub's member sessions are on.</summary>
    [JsonIgnore] public bool IsOn => MemberSessions;

    /// <summary>The consent window, clamped to something sane.</summary>
    [JsonIgnore] public TimeSpan ConsentWindow => TimeSpan.FromSeconds(Math.Clamp(double.IsFinite(ConsentSeconds) ? ConsentSeconds : DefaultConsentSeconds, 1, 3600));

    /// <summary>The Windows account <paramref name="member"/>'s CLIs run under, or null (none mapped, or per-member users off).</summary>
    public string? AccountOf(string? member)
    {
        if (!WindowsUsers || string.IsNullOrWhiteSpace(member)) return null;
        var who = TermMemberKeys.Normalize(member);
        return (Accounts ?? []).FirstOrDefault(a => a is not null && TermMemberKeys.Normalize(a.Email) == who)?.Account is { Length: > 0 } account ? account : null;
    }

    /// <summary>
    /// Whether anyone but the machine owner may type into a session owned by <paramref name="sessionOwner"/> (a member's email, or
    /// null for a session on a login no member owns) that runs under Windows account <paramref name="sessionAccount"/>: with
    /// "trusted org", always; otherwise only a member's session running under that member's own mapped account.
    /// </summary>
    public bool MayInteract(string? sessionOwner, string? sessionAccount)
    {
        if (TrustedOrg) return true;
        return AccountOf(sessionOwner) is { } mapped && sessionAccount is { Length: > 0 } running && TermWindowsAccounts.Same(mapped, running);
    }

    /// <summary>How a CLI on <paramref name="member"/>'s login would be started: under their own account when per-member users are
    /// on and one is mapped; as the host's user when the org is trusted; otherwise not at all.</summary>
    public TermLaunchPlan PlanLaunch(string member)
    {
        if (!IsOn) return TermLaunchPlan.Refuse("member sessions are off on this host (the Vault's HubMemberSessions)");
        if (AccountOf(member) is { } account) return new TermLaunchPlan(TermLaunchKind.MemberUser, account);
        if (TrustedOrg) return TermLaunchPlan.Host;
        return TermLaunchPlan.Refuse(WindowsUsers
            ? "no Windows account of your own is set up on this host yet (its owner runs orchestrator machine isolate)"
            : "this host runs every CLI as one Windows user, and its owner hasn't marked the org trusted (HubTrustedOrg) or turned on per-member Windows users (HubMemberWindowsUsers)");
    }
}

/// <summary>Windows account names as the hub keeps them (hub phase 2, M.ORCH-T1641): checked for shape, compared loosely.</summary>
public static class TermWindowsAccounts
{
    /// <summary>An account name in a form the host would accept, trimmed (<c>user</c>, <c>.\user</c>, <c>MACHINE\user</c> or
    /// <c>DOMAIN\user</c>), or null with <paramref name="why"/> when it isn't one or is the host's own user
    /// (<paramref name="hostUser"/>, which would isolate nothing).</summary>
    public static string? Normalize(string? account, string? hostUser, out string? why)
    {
        why = null;
        var a = account?.Trim() ?? "";
        if (a.Length is 0 or > 120) { why = "a Windows account name is 1 to 120 characters"; return null; }
        if (a.Any(c => char.IsControl(c) || c is '/' or ':' or '*' or '?' or '"' or '<' or '>' or '|' or '@' or '[' or ']' or ';' or '=' or ',' or '+'))
        {
            why = "that isn't a Windows account name (user, .\\user, MACHINE\\user or DOMAIN\\user)";
            return null;
        }
        var parts = a.Split('\\');
        if (parts.Length > 2 || parts.Any(p => p.Length == 0) || parts[^1].Trim().Length == 0 || parts[^1].EndsWith('.'))
        {
            why = "that isn't a Windows account name (user, .\\user, MACHINE\\user or DOMAIN\\user)";
            return null;
        }
        if (hostUser is { Length: > 0 } && Same(a, hostUser))
        {
            why = "that's the host's own Windows user, which every CLI already runs as: a member's account must be their own";
            return null;
        }
        return a;
    }

    /// <summary>Whether two account names name the same account: the user parts equal (any case), and the domains too when both
    /// name one (<c>.</c> and this machine's name count as no domain).</summary>
    public static bool Same(string a, string b)
    {
        var (da, ua) = Split(a);
        var (db, ub) = Split(b);
        if (!string.Equals(ua, ub, StringComparison.OrdinalIgnoreCase)) return false;
        return da is null || db is null || string.Equals(da, db, StringComparison.OrdinalIgnoreCase);
    }

    private static (string? Domain, string User) Split(string account)
    {
        var t = account.Trim();
        var at = t.IndexOf('\\', StringComparison.Ordinal);
        if (at < 0) return (null, t);
        var domain = t[..at].Trim();
        var user = t[(at + 1)..].Trim();
        return (domain is "." || string.Equals(domain, Environment.MachineName, StringComparison.OrdinalIgnoreCase) ? null : domain, user);
    }
}
