using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Gateway;

// Hub phase 2b (M.ORCH-T1667; docs/research/hub-and-spoke.md §3.4, docs/REMOTE_HOST.md "Per-member Windows users"): a member's
// CLI on the host starts under their own Windows account, its pipe ACL'd to that account and the host's user, and the gateway reports
// that account to its own session-account check. Everything here is reachable only through a TermLaunchKind.MemberUser plan, which
// TermHubPolicy gives only while HubMemberSessions and HubMemberWindowsUsers are both on, on the host, for a member an admin mapped
// to an existing account. Nothing here creates, changes or deletes a Windows account, group, service or credential.

/// <summary>A process started under another Windows account: its pid and start time (UTC), or why it couldn't be.</summary>
internal sealed record RunAsResult(int Pid, DateTime StartUtc, string? Error = null)
{
    public bool Ok => Error is null && Pid > 0;
    public static RunAsResult Fail(string why) => new(0, default, why);
}

/// <summary>Starts a process under another Windows account with that account's stored password (the real one,
/// <see cref="LogonRunAs"/>; tests use a fake).</summary>
internal interface IRunAs
{
    /// <summary>Starts <paramref name="commandLine"/> (whose program is <paramref name="application"/>) in
    /// <paramref name="workingDirectory"/> as <paramref name="account"/>, with that account's own profile and environment.</summary>
    RunAsResult Start(string account, IMemberCredentials credentials, string application, string commandLine, string workingDirectory);
}

/// <summary>
/// <c>CreateProcessWithLogonW</c> with <c>LOGON_WITH_PROFILE</c>: the process runs as the member's account, with that account's
/// profile loaded and its own environment (<c>%USERPROFILE%</c>, <c>%LOCALAPPDATA%</c>, its DPAPI scope), in a new hidden console,
/// not a child of the gateway (the Secondary Logon service makes it). Needs no elevation and no privilege; the password is read
/// from <see cref="IMemberCredentials"/> into native memory for the one call, then zeroed. The seclogon service must be running.
/// </summary>
internal sealed class LogonRunAs : IRunAs
{
    public static readonly LogonRunAs Instance = new();

    private const uint LOGON_WITH_PROFILE = 0x1;
    private const uint CREATE_NEW_CONSOLE = 0x10, CREATE_NEW_PROCESS_GROUP = 0x200, CREATE_UNICODE_ENVIRONMENT = 0x400;
    private const int STARTF_USESHOWWINDOW = 0x1;
    private const short SW_HIDE = 0;

    public RunAsResult Start(string account, IMemberCredentials credentials, string application, string commandLine, string workingDirectory)
    {
        var (domain, user) = Split(account);
        try
        {
            return credentials.Use(account, password =>
            {
                var startup = new STARTUPINFO { cb = Marshal.SizeOf<STARTUPINFO>(), dwFlags = STARTF_USESHOWWINDOW, wShowWindow = SW_HIDE };
                var line = new StringBuilder(commandLine, commandLine.Length + 1);
                if (!CreateProcessWithLogonW(user, domain, password, LOGON_WITH_PROFILE, application, line,
                        CREATE_NEW_CONSOLE | CREATE_NEW_PROCESS_GROUP | CREATE_UNICODE_ENVIRONMENT, IntPtr.Zero, workingDirectory, ref startup, out var info))
                    return RunAsResult.Fail(new Win32Exception(Marshal.GetLastWin32Error()).Message);
                try
                {
                    var start = GetProcessTimes(info.hProcess, out var created, out _, out _, out _) ? DateTime.FromFileTimeUtc(created) : DateTime.UtcNow;
                    return new RunAsResult(info.dwProcessId, start);
                }
                finally
                {
                    CloseHandle(info.hThread);
                    CloseHandle(info.hProcess);
                }
            });
        }
        catch (KeyNotFoundException ex) { return RunAsResult.Fail(ex.Message); }
    }

    /// <summary>The domain (<c>.</c> for this machine) and user of an account name.</summary>
    internal static (string Domain, string User) Split(string account)
    {
        var t = account.Trim();
        var at = t.IndexOf('\\', StringComparison.Ordinal);
        return at < 0 ? (".", t) : (t[..at].Trim() is { Length: > 0 } d ? d : ".", t[(at + 1)..].Trim());
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess, hThread;
        public int dwProcessId, dwThreadId;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessWithLogonW(string username, string domain, IntPtr password, uint logonFlags, string? applicationName,
        StringBuilder commandLine, uint creationFlags, IntPtr environment, string? currentDirectory, ref STARTUPINFO startupInfo, out PROCESS_INFORMATION processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(IntPtr process, out long creation, out long exit, out long kernel, out long user);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}

/// <summary>
/// Starts a member's CLI under their own Windows account (hub phase 2b): a TermHost run as that account through
/// <see cref="IRunAs"/>, told the host's user as its one peer (<c>--peer-sid</c>, so its pipe is ACL'd to the two of them) and the
/// member's registry folder (<c>--registry</c>), recorded in the gateway's ledger (<see cref="TermMemberHosts"/>) before anything
/// trusts it, then waited on until it registers. Refuses, starting nothing, when the account has no stored password, the TermHost or
/// the repo isn't there, or the member's registry folder hasn't been made by an admin.
/// </summary>
internal sealed partial class MemberSessionLauncher(
    IRunAs runAs,
    IMemberCredentials credentials,
    TermMemberHosts ledger,
    Func<string?> termHostExe,
    Func<string> workspaceRoot,
    Func<string> registryRoot,
    Func<string> peerSid,
    Func<TimeSpan> startWait,
    Func<TimeSpan, CancellationToken, Task>? delay = null,
    Func<string, string, TermSessionRecord?>? registered = null,
    Func<int, DateTime?>? hostStart = null,
    Func<string>? tempRoot = null,
    Func<string, string, string?>? prepareTemp = null)
{
    /// <summary>How often the start waits look for the registry file.</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    private readonly Func<TimeSpan, CancellationToken, Task> delay = delay ?? Task.Delay;
    private readonly Func<string, string, TermSessionRecord?> registered = registered ?? ((id, dir) => TermSessions.Get(id, dir));
    private readonly Func<int, DateTime?> hostStart = hostStart ?? TermSessions.HostStartOf;
    // The daemon passes its temp-root setting down in MINDATTIC_TEMP_ROOT (M.ORCH-T2219).
    private readonly Func<string> tempRoot = tempRoot ?? (() => TempRoot.Normalize(Environment.GetEnvironmentVariable(TempRoot.RootVariable)));
    private readonly Func<string, string, string?> prepareTemp = prepareTemp ?? MemberTemp.Prepare;

    /// <summary>The ledger the gateway's session source, pipe connector and account check read.</summary>
    public TermMemberHosts Ledger => ledger;

    public async Task<LaunchResult> StartAsync(string repo, string login, string account, CancellationToken ct)
    {
        if (!HubNames.IsRepo(repo)) return new LaunchResult(false, "That isn't a repo name.");
        // The login goes into the CLI's environment and the host's command line: an email of plain characters only (no %, quote or space).
        if (!LoginShape().IsMatch(login)) return new LaunchResult(false, "That login isn't a plain email address.");
        if (!credentials.Has(account))
            return new LaunchResult(false, $"No password for {account} is stored on this host yet: its owner runs orchestrator machine isolate <member> {account} --password once (docs/REMOTE_HOST.md).");
        if (termHostExe() is not { } exe || !File.Exists(exe)) return new LaunchResult(false, "The terminal host (TermHost) isn't installed on this host.");
        var repoDir = Path.Combine(workspaceRoot(), repo);
        if (!Directory.Exists(repoDir)) return new LaunchResult(false, $"There's no {repo} in this host's workspace.");
        var registry = TermMemberHosts.RegistryDirOf(account, registryRoot());
        if (!Directory.Exists(registry))
            return new LaunchResult(false, $"{account}'s folder for its terminal hosts ({registry}) hasn't been made yet: an admin makes it once (docs/REMOTE_HOST.md).");

        // M.ORCH-T2218: the account's own profile would put its temp on C:; a missing temp root (drive gone) leaves it there.
        string? temp = null;
        if (tempRoot() is { } top && Directory.Exists(top))
        {
            temp = MemberTemp.DirOf(top, account);
            if (prepareTemp(account, temp) is { } why) return new LaunchResult(false, $"{account}'s temp folder ({temp}) couldn't be made for it: {why}");
        }

        var id = TermProtocol.NewSessionId();
        var commandLine = CommandLine(exe, id, registry, peerSid(), login, repo, repoDir, temp);
        var started = runAs.Start(account, credentials, exe, commandLine, Path.GetDirectoryName(exe) ?? repoDir);
        if (!started.Ok) return new LaunchResult(false, $"Windows didn't start it as {account}: {started.Error}");

        var host = new TermMemberHost
        {
            Id = id, Account = account, Login = login, Repo = repo, HostPid = started.Pid, HostStartUtc = started.StartUtc, RegistryDir = registry,
            Pipe = TermProtocol.PipeName(id, TermMemberHosts.UserPart(account)), StartedUtc = DateTime.UtcNow,
        };
        if (!ledger.Add(host))
            return new LaunchResult(false, $"It started (pid {started.Pid}) but the gateway couldn't record it, so it won't be offered; stop it on the host.");

        var deadline = DateTime.UtcNow + startWait();
        while (true)
        {
            if (registered(id, registry) is { } record && TermMemberHosts.Matches(host, record))
                return new LaunchResult(true, $"Started {repo} on {login} as {account} (session {id}).");
            if (hostStart(started.Pid) is null)
            {
                ledger.Remove(id);
                return new LaunchResult(false, $"Its terminal host exited as soon as it started as {account}; the host owner can read its log in that account's %LOCALAPPDATA%\\MindAttic\\MindAttic.Orchestrator\\terms\\{id}.log.");
            }
            if (DateTime.UtcNow >= deadline)
                return new LaunchResult(true, $"Started {repo} on {login} as {account} (session {id}); it hasn't come up yet, and shows on your Team Wall once it does.");
            await delay(PollInterval, ct).ConfigureAwait(false);
        }
    }

    /// <summary>The TermHost's command line: its own exe, then options, each quoted for CreateProcess's argument parsing. With
    /// <paramref name="temp"/>, the CLI's TEMP, TMP and .NET unpack folder are there (M.ORCH-T2218).</summary>
    internal static string CommandLine(string exe, string id, string registry, string peerSid, string login, string repo, string repoDir, string? temp = null)
    {
        List<string> args =
        [
            exe, "--id", id, "--peer-sid", peerSid, "--registry", registry, "--login", login, "--repo", repo, "--cwd", repoDir, "--title", repo,
            // Expanded by the host from the member's own environment (HostOptions: a member's host expands --env values).
            "--env", $"CLAUDE_CONFIG_DIR=%USERPROFILE%\\.claude-accounts\\{login}",
        ];
        if (temp is not null)
            args.AddRange(["--env", $"TEMP={temp}", "--env", $"TMP={temp}", "--env", $"{TempRoot.BundleVariable}={Path.Combine(temp, ".net")}"]);
        args.AddRange(["--", "claude"]);
        return string.Join(' ', args.Select(Quote));
    }

    /// <summary>One argument quoted for CreateProcess's parser (the CommandLineToArgvW rules).</summary>
    internal static string Quote(string arg)
    {
        if (arg.Length > 0 && arg.IndexOfAny([' ', '\t', '"']) < 0) return arg;
        var sb = new StringBuilder("\"");
        var slashes = 0;
        foreach (var c in arg)
        {
            if (c == '\\') { slashes++; continue; }
            if (c == '"') { sb.Append('\\', slashes * 2 + 1).Append('"'); slashes = 0; continue; }
            sb.Append('\\', slashes).Append(c);
            slashes = 0;
        }
        return sb.Append('\\', slashes * 2).Append('"').ToString();
    }

    [GeneratedRegex(@"^[A-Za-z0-9._+\-]{1,64}@[A-Za-z0-9.\-]{1,190}$")]
    private static partial Regex LoginShape();
}

/// <summary>
/// The hosted sessions, members' own included (hub phase 2b): the host user's registry, plus each live ledger entry's record from
/// its member's registry folder, taken only when it is that very host (id, pid, start time), with its login, repo, pipe and account
/// replaced by the ledger's, which only the host's user writes.
/// </summary>
internal sealed class MemberAwareSessions(ISessionSource inner, TermMemberHosts ledger, Func<string, string, TermSessionRecord?>? read = null) : ISessionSource
{
    private readonly Func<string, string, TermSessionRecord?> read = read ?? ((id, dir) => TermSessions.Get(id, dir));

    public IReadOnlyList<TermSessionRecord> Enumerate()
    {
        var hosts = ledger.Live();
        var all = inner.Enumerate().Where(r => hosts.All(h => h.Id != r.Id)).ToList();
        foreach (var host in hosts)
            if (Member(host) is { } record) all.Add(record);
        return [.. all.OrderBy(r => r.StartedUtc).ThenBy(r => r.Id, StringComparer.Ordinal)];
    }

    public TermSessionRecord? Get(string id) =>
        ledger.Find(id) is { } host ? Member(host) : inner.Get(id);

    private TermSessionRecord? Member(TermMemberHost host) =>
        read(host.Id, host.RegistryDir) is { } record && TermMemberHosts.Matches(host, record)
            ? record with { Login = host.Login, Repo = host.Repo, Pipe = host.Pipe, Account = host.Account }
            : null;
}

/// <summary>
/// Opens a session's pipe (hub phase 2b): a member's host (in the ledger) without <see cref="PipeOptions.CurrentUserOnly"/>, which
/// would refuse a pipe its own user doesn't own, but at identification level only (the member's host can't act as the host's user),
/// and only once Windows says the process serving it is the very TermHost the gateway started; every other session as before.
/// </summary>
internal sealed class MemberAwarePipeConnector(IPipeConnector inner, TermMemberHosts ledger) : IPipeConnector
{
    public async Task<Stream> ConnectAsync(TermSessionRecord session, CancellationToken ct)
    {
        if (ledger.Of(session) is not { } host) return await inner.ConnectAsync(session, ct).ConfigureAwait(false);
        var pipe = new NamedPipeClientStream(".", host.Pipe, PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Identification);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(NamedPipeConnector.Timeout);
            await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
            if (TermPipeAcl.ServerProcessId(pipe) != host.HostPid)
                throw new IOException($"The pipe of session {host.Id} isn't served by the terminal host the gateway started; refused.");
            return pipe;
        }
        catch
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}

/// <summary>The TermHost executable the gateway starts members' hosts from (hub phase 2b).</summary>
internal static class GatewayTermHost
{
    /// <summary>The Orchestrator's install folder, where the side-by-side TermHost versions live.</summary>
    public const string InstallDir = @"C:\Apps\MindAttic.Orchestrator";

    /// <summary><c>MINDATTIC_TERMHOST</c> when it names a file, else the newest installed TermHost, else null.</summary>
    public static string? Find()
    {
        if (Environment.GetEnvironmentVariable("MINDATTIC_TERMHOST") is { Length: > 0 } set && File.Exists(set)) return Path.GetFullPath(set);
        try { return TermHostInstalls.ResolveExe(InstallDir); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException) { return null; }
    }
}

/// <summary>
/// A member's own temp folder under the temp root (M.ORCH-T2218): <c>&lt;root&gt;\members\&lt;user&gt;</c>, made by the gateway before
/// it starts their host, with its inheritance cut and the ACL docs/REMOTE_HOST.md gives the member-terms folder: SYSTEM and
/// Administrators full, the host's user and the member's account modify, nobody else.
/// </summary>
internal static class MemberTemp
{
    /// <summary>The folder of <paramref name="account"/> under <paramref name="root"/>.</summary>
    public static string DirOf(string root, string account) =>
        Path.Combine(root, "members", TermProtocol.Sanitize(TermMemberHosts.UserPart(account).ToLowerInvariant()));

    /// <summary>The folder's DACL: protected, inherited by everything made in it.</summary>
    public static DirectorySecurity Acl(SecurityIdentifier hostUser, SecurityIdentifier member)
    {
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        const InheritanceFlags inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        void Allow(SecurityIdentifier who, FileSystemRights rights) =>
            security.AddAccessRule(new FileSystemAccessRule(who, rights, inherit, PropagationFlags.None, AccessControlType.Allow));
        Allow(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl);
        Allow(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl);
        Allow(hostUser, FileSystemRights.Modify);
        Allow(member, FileSystemRights.Modify);
        return security;
    }

    /// <summary>Makes <paramref name="dir"/> and sets its <see cref="Acl"/> for <paramref name="account"/>; why it couldn't, or null.</summary>
    public static string? Prepare(string account, string dir)
    {
        try
        {
            var (domain, user) = LogonRunAs.Split(account);
            var member = (SecurityIdentifier)new NTAccount(domain == "." ? Environment.MachineName : domain, user).Translate(typeof(SecurityIdentifier));
            Directory.CreateDirectory(dir);
            new DirectoryInfo(dir).SetAccessControl(Acl(TermPipeAcl.CurrentUser, member));
            return null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return ex.Message;
        }
    }
}

/// <summary>The Windows account a hosted session runs under (hub phase 2b): the member's account when the gateway's ledger says it
/// started that very host under it, else the fallback (the gateway's own user).</summary>
internal sealed class MemberAwareAccounts(ISessionAccounts fallback, TermMemberHosts ledger) : ISessionAccounts
{
    public string? AccountOf(TermSessionRecord session) => ledger.Of(session)?.Account ?? fallback.AccountOf(session);
}
