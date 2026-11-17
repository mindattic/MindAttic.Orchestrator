using System.Security.Principal;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Setup;

/// <summary>
/// The Windows accounts the host can see (hub phase 2, M.ORCH-T1641). Read-only: whether a name resolves to an account, and who
/// this process runs as. Nothing here creates, changes or deletes an account, a group, an ACL or a credential; tests use a fake.
/// </summary>
public interface IWindowsAccounts
{
    /// <summary>Whether <paramref name="account"/> names an existing Windows user account (a lookup only).</summary>
    bool Exists(string account);

    /// <summary>The Windows user this process (the daemon, the host's CLIs) runs as: <c>DOMAIN\user</c>.</summary>
    string CurrentUser { get; }
}

/// <summary>The real lookup: <see cref="NTAccount.Translate"/> to a SID, which only reads the account database.</summary>
public sealed class LocalWindowsAccounts : IWindowsAccounts
{
    public static readonly LocalWindowsAccounts Instance = new();

    public bool Exists(string account)
    {
        try
        {
            var name = account.Trim();
            if (name.StartsWith(@".\", StringComparison.Ordinal)) name = Environment.MachineName + name[1..];
            return new NTAccount(name).Translate(typeof(SecurityIdentifier)) is SecurityIdentifier;
        }
        catch (Exception ex) when (ex is IdentityNotMappedException or ArgumentException or SystemException) { return false; }
    }

    public string CurrentUser => $"{Environment.UserDomainName}\\{Environment.UserName}";
}

/// <summary>
/// The hub phase 2 policy the gateway decides from (M.ORCH-T1641; <see cref="TermHubPolicy"/>), built from the Vault's settings
/// and the member keys file each time the daemon (or an <c>orchestrator gateway</c>/<c>machine</c> command) writes the ACL
/// snapshot. Null, so the snapshot has no hub section at all, unless <see cref="OrchestratorSettings.HubMemberSessions"/> is on
/// and this machine is the host: a standalone install, a client and today's host are untouched.
/// </summary>
public static class HubPolicy
{
    /// <summary>The policy for this machine now (the real topology, members file and account lookup).</summary>
    public static TermHubPolicy? Current(OrchestratorSettings settings) =>
        From(settings, MachineTopology.Current, TermMemberKeys.List(), LocalWindowsAccounts.Instance);

    /// <summary>
    /// The policy <paramref name="settings"/> mean on a machine of <paramref name="topology"/>: null unless member sessions are on
    /// and it's the host. With per-member Windows users on, <see cref="TermHubPolicy.Accounts"/> lists each member with a live key
    /// whose mapped account is well formed, isn't the host's own user and exists here; a member whose account is missing is left
    /// out, so they get no typing or starting until an admin makes it (never a fallback to the host's user).
    /// </summary>
    public static TermHubPolicy? From(OrchestratorSettings settings, MachineTopology topology, IReadOnlyList<TermMemberKey> keys, IWindowsAccounts accounts)
    {
        if (!settings.HubMemberSessions || !topology.IsHost) return null;
        var mapped = new List<TermHubAccount>();
        if (settings.HubMemberWindowsUsers)
        {
            var host = accounts.CurrentUser;
            foreach (var k in keys)
            {
                if (k is null || !k.IsLive || string.IsNullOrWhiteSpace(k.WindowsUser)) continue;
                if (TermWindowsAccounts.Normalize(k.WindowsUser, host, out _) is not { } account) continue;
                if (!accounts.Exists(account)) continue;
                mapped.Add(new TermHubAccount(TermMemberKeys.Normalize(k.Email), account));
            }
        }
        return new TermHubPolicy
        {
            MemberSessions = true,
            TrustedOrg = settings.HubTrustedOrg,
            WindowsUsers = settings.HubMemberWindowsUsers,
            ConsentSeconds = settings.HubConsentSeconds,
            StartWaitSeconds = settings.HubMemberStartWaitSeconds,
            Accounts = [.. mapped.OrderBy(a => a.Email, StringComparer.Ordinal)],
        };
    }
}
