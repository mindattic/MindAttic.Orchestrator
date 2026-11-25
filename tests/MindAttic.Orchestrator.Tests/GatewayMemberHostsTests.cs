using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using MindAttic.Orchestrator.Gateway;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Setup;
using MindAttic.Orchestrator.Term;
using MindAttic.Orchestrator.TermHost;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// Hub phase 2b (M.ORCH-T1667): member TermHosts started under their own Windows account, through fakes only. Nothing here creates,
/// changes or deletes a Windows account, group, service, scheduled task or stored credential: the run-as and the credential store are
/// fakes, folders are a test's own temp folders, and the one real pipe is a throwaway this test creates and ACLs to the current user.
/// </summary>
[TestFixture]
public sealed class GatewayMemberHostsTests
{
    private const string Account = @"HOSTBOX\erin-cli";
    private const string Login = "erin@example.com";
    private const string Repo = "MindAttic.Orchestrator";
    private const string PeerSid = "S-1-5-21-1000000000-2000000000-3000000000-1001";
    private static readonly DateTime HostStart = new(2026, 11, 16, 20, 0, 0, DateTimeKind.Utc);

    private string root = "";

    [SetUp]
    public void SetUp() => root = Directory.CreateTempSubdirectory("mindattic-member-hosts-").FullName;

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    // ── the ledger ──────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Test]
    public void The_ledger_names_a_session_only_by_its_id_pid_and_start_time_and_drops_gone_hosts()
    {
        var running = new Dictionary<int, DateTime> { [4242] = HostStart };
        var ledger = new TermMemberHosts(root, pid => running.TryGetValue(pid, out var at) ? at : null);
        Assert.That(ledger.Add(Host("abc123", 4242)), Is.True);

        Assert.That(ledger.Of(Record("abc123", 4242, HostStart))?.Account, Is.EqualTo(Account));
        Assert.That(ledger.Of(Record("abc123", 4242, HostStart.AddSeconds(5))), Is.Null, "a reused pid is not that host");
        Assert.That(ledger.Of(Record("other", 4242, HostStart)), Is.Null, "another session's file naming the same pid");
        Assert.That(ledger.Of(Record("abc123", 9999, HostStart)), Is.Null);

        running.Clear();
        Assert.That(ledger.Live(), Is.Empty, "a host that's gone leaves the ledger");
        Assert.That(new TermMemberHosts(root, _ => HostStart).Live(), Is.Empty, "and the file was rewritten without it");
    }

    [Test]
    public void An_unreadable_ledger_is_empty_so_no_session_counts_as_a_members()
    {
        File.WriteAllText(Path.Combine(root, TermMemberHosts.FileName), "{ not json");
        var ledger = new TermMemberHosts(root, _ => HostStart);
        Assert.That(ledger.Live(), Is.Empty);
        Assert.That(ledger.Of(Record("abc123", 4242, HostStart)), Is.Null);
    }

    [Test]
    public void A_ledger_that_cant_be_read_right_now_is_never_written_over()
    {
        var ledger = new TermMemberHosts(root, _ => HostStart);
        Assert.That(ledger.Add(Host("abc123", 4242)), Is.True);
        using (new FileStream(Path.Combine(root, TermMemberHosts.FileName), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.That(ledger.Live(), Is.Empty, "a locked ledger reads as empty");
            Assert.That(ledger.Add(Host("def456", 4343)), Is.False, "and adding to it writes nothing");
            Assert.That(ledger.Remove("abc123"), Is.False);
        }
        Assert.That(ledger.Live().Select(h => h.Id), Is.EqualTo(new[] { "abc123" }), "the other host is still recorded");
    }

    [Test]
    public void Remove_forgets_a_host()
    {
        var ledger = new TermMemberHosts(root, _ => HostStart);
        ledger.Add(Host("abc123", 4242));
        Assert.That(ledger.Remove("abc123"), Is.True);
        Assert.That(ledger.Find("abc123"), Is.Null);
        Assert.That(ledger.Remove("abc123"), Is.False);
    }

    [Test]
    public void A_members_registry_folder_and_pipe_follow_the_user_part_of_their_account()
    {
        Assert.That(TermMemberHosts.UserPart(@"HOSTBOX\Erin-CLI"), Is.EqualTo("Erin-CLI"));
        Assert.That(TermMemberHosts.UserPart("erin-cli"), Is.EqualTo("erin-cli"));
        Assert.That(TermMemberHosts.RegistryDirOf(@".\Erin-CLI", root), Is.EqualTo(Path.Combine(root, "erin-cli")));
    }

    // ── the session source, accounts and pipe ───────────────────────────────────────────────────────────────────────────────

    [Test]
    public void A_members_record_is_offered_only_when_the_ledger_started_that_very_host_and_with_the_ledgers_fields()
    {
        var ledger = new TermMemberHosts(root, _ => HostStart);
        ledger.Add(Host("abc123", 4242));
        // What the member's own (writable) registry file claims: another login, repo and pipe.
        var claimed = Record("abc123", 4242, HostStart) with { Login = "sam@example.com", Repo = "Elsewhere", Pipe = "mindattic-term-sam-abc123", Account = @"HOSTBOX\sam" };
        var forged = Record("zzz999", 4242, HostStart) with { Login = Login };
        var files = new Dictionary<string, TermSessionRecord> { ["abc123"] = claimed, ["zzz999"] = forged };
        var sessions = new MemberAwareSessions(new ListSessions(), ledger, (id, _) => files.GetValueOrDefault(id));

        var listed = sessions.Enumerate();
        Assert.That(listed.Select(s => s.Id), Is.EqualTo(new[] { "abc123" }), "a file the ledger doesn't name is never offered");
        var s = listed[0];
        Assert.That((s.Login, s.Repo, s.Account), Is.EqualTo((Login, Repo, Account)));
        Assert.That(s.Pipe, Is.EqualTo(TermProtocol.PipeName("abc123", "erin-cli")));
        Assert.That(sessions.Get("abc123")?.Login, Is.EqualTo(Login));

        files["abc123"] = claimed with { HostPid = 5555 };
        Assert.That(sessions.Enumerate(), Is.Empty, "a record that isn't that host's pid is left out");
    }

    [Test]
    public void The_session_account_is_the_members_only_for_a_host_the_ledger_started()
    {
        var ledger = new TermMemberHosts(root, _ => HostStart);
        ledger.Add(Host("abc123", 4242));
        var accounts = new MemberAwareAccounts(new FixedAccounts(@"HOSTBOX\owner"), ledger);
        Assert.That(accounts.AccountOf(Record("abc123", 4242, HostStart)), Is.EqualTo(Account));
        Assert.That(accounts.AccountOf(Record("abc123", 4242, HostStart) with { Account = Account, HostPid = 1 }), Is.EqualTo(@"HOSTBOX\owner"),
            "the record's own account field is never trusted");
        Assert.That(accounts.AccountOf(Record("plain", 77, HostStart)), Is.EqualTo(@"HOSTBOX\owner"));
    }

    [Test]
    public void With_the_reported_account_a_mapped_members_session_may_be_typed_into()
    {
        var policy = new TermHubPolicy { MemberSessions = true, WindowsUsers = true, Accounts = [new TermHubAccount(Login, Account)] };
        var ledger = new TermMemberHosts(root, _ => HostStart);
        ledger.Add(Host("abc123", 4242));
        var accounts = new MemberAwareAccounts(new FixedAccounts(@"HOSTBOX\owner"), ledger);
        Assert.That(policy.MayInteract(Login, accounts.AccountOf(Record("abc123", 4242, HostStart))), Is.True);
        Assert.That(policy.MayInteract(Login, accounts.AccountOf(Record("plain", 77, HostStart))), Is.False);
    }

    // ── starting one ────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Without_a_stored_password_nothing_is_started()
    {
        var (launcher, runAs, _) = Launcher(new FakeCredentials());
        var result = await launcher.StartAsync(Repo, Login, Account, CancellationToken.None);
        Assert.That(result.Ok, Is.False);
        Assert.That(result.Output, Does.Contain("--password"));
        Assert.That(runAs.Calls, Is.Empty);
    }

    [Test]
    public async Task Without_the_admin_made_registry_folder_nothing_is_started()
    {
        var credentials = new FakeCredentials();
        credentials.Save(Account, "hunter2-not-real");
        var (launcher, runAs, _) = Launcher(credentials, makeRegistry: false);
        var result = await launcher.StartAsync(Repo, Login, Account, CancellationToken.None);
        Assert.That(result.Ok, Is.False);
        Assert.That(runAs.Calls, Is.Empty);
    }

    [Test]
    public async Task A_login_that_isnt_a_plain_email_is_refused()
    {
        var credentials = new FakeCredentials();
        credentials.Save(Account, "hunter2-not-real");
        var (launcher, runAs, _) = Launcher(credentials);
        foreach (var bad in new[] { "erin%USERNAME%@example.com", "erin\"@example.com", "erin @example.com", "erin" })
            Assert.That((await launcher.StartAsync(Repo, bad, Account, CancellationToken.None)).Ok, Is.False, bad);
        Assert.That(runAs.Calls, Is.Empty);
    }

    [Test]
    public async Task Starting_runs_the_term_host_as_the_member_with_the_peer_and_registry_and_records_it_before_it_registers()
    {
        var credentials = new FakeCredentials();
        credentials.Save(Account, "hunter2-not-real");
        var (launcher, runAs, ledger) = Launcher(credentials, registers: true);
        var result = await launcher.StartAsync(Repo, Login, Account, CancellationToken.None);

        Assert.That(result.Ok, Is.True, result.Output);
        Assert.That(runAs.Calls, Has.Count.EqualTo(1));
        var (account, line) = runAs.Calls[0];
        Assert.That(account, Is.EqualTo(Account));
        Assert.That(runAs.PasswordsSeen, Is.EqualTo(new[] { "hunter2-not-real" }), "the run-as got the stored password");
        Assert.That(line, Does.Contain("--peer-sid " + PeerSid));
        Assert.That(line, Does.Contain("--registry " + MemberSessionLauncher.Quote(TermMemberHosts.RegistryDirOf(Account, Path.Combine(root, "members")))));
        Assert.That(line, Does.Contain("--login " + Login));
        Assert.That(line, Does.Contain(@"CLAUDE_CONFIG_DIR=%USERPROFILE%\.claude-accounts\" + Login));
        var temp = Path.Combine(root, "temp", "members", "erin-cli");
        Assert.That(line, Does.Contain("--env " + MemberSessionLauncher.Quote("TEMP=" + temp)).And.Contain("--env " + MemberSessionLauncher.Quote("TMP=" + temp)),
            "the member's TEMP is under the temp root, not their profile's on C: (M.ORCH-T2218)");
        Assert.That(Directory.Exists(temp), Is.True, "and made before the host starts");
        Assert.That(line, Does.Not.Contain("hunter2"), "the password is never on a command line");
        Assert.That(result.Output, Does.Not.Contain("hunter2"));

        var host = ledger.Live().Single();
        Assert.That((host.Account, host.Login, host.Repo, host.HostPid, host.HostStartUtc), Is.EqualTo((Account, Login, Repo, 4242, HostStart)));
    }

    [Test]
    public void The_command_line_sets_temp_tmp_and_the_dotnet_unpack_folder_only_when_given_a_temp_folder()
    {
        var with = MemberSessionLauncher.CommandLine("host.exe", "abc123", @"C:\reg", PeerSid, Login, Repo, @"D:\ws\repo", @"R:\Temp\members\erin-cli");
        Assert.That(with, Does.Contain(@"--env TEMP=R:\Temp\members\erin-cli --env TMP=R:\Temp\members\erin-cli")
            .And.Contain(@"--env DOTNET_BUNDLE_EXTRACT_BASE_DIR=R:\Temp\members\erin-cli\.net"));
        Assert.That(with, Does.EndWith("-- claude"));
        var without = MemberSessionLauncher.CommandLine("host.exe", "abc123", @"C:\reg", PeerSid, Login, Repo, @"D:\ws\repo");
        Assert.That(without, Does.Not.Contain("TEMP=").And.EndWith("-- claude"));
    }

    [Test]
    public async Task Without_the_temp_root_the_member_keeps_their_own_temp()
    {
        var credentials = new FakeCredentials();
        credentials.Save(Account, "hunter2-not-real");
        var (launcher, runAs, _) = Launcher(credentials, tempRoot: Path.Combine(root, "no-such-drive"));
        Assert.That((await launcher.StartAsync(Repo, Login, Account, CancellationToken.None)).Ok, Is.True);
        Assert.That(runAs.Calls.Single().CommandLine, Does.Not.Contain("TEMP="));
    }

    [Test]
    public async Task A_temp_folder_that_cant_be_made_starts_nothing()
    {
        var credentials = new FakeCredentials();
        credentials.Save(Account, "hunter2-not-real");
        var (launcher, runAs, _) = Launcher(credentials, prepareTemp: (_, _) => "Access is denied.");
        var result = await launcher.StartAsync(Repo, Login, Account, CancellationToken.None);
        Assert.That(result.Ok, Is.False);
        Assert.That(result.Output, Does.Contain("Access is denied."));
        Assert.That(runAs.Calls, Is.Empty);
    }

    [Test]
    public void A_members_temp_folder_is_cut_from_inheritance_and_names_only_system_admins_the_host_user_and_the_member()
    {
        var self = TermPipeAcl.CurrentUser;
        var member = new SecurityIdentifier(PeerSid);
        Assert.That(MemberTemp.DirOf(@"R:\Temp", @"HOSTBOX\Erin-CLI"), Is.EqualTo(@"R:\Temp\members\erin-cli"));
        var acl = MemberTemp.Acl(self, member);
        Assert.That(acl.AreAccessRulesProtected, Is.True);
        var rules = acl.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToList();
        Assert.That(rules.Select(r => r.IdentityReference), Is.EquivalentTo(new IdentityReference[]
        {
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), self, member,
        }));
        Assert.That(rules.Single(r => r.IdentityReference.Equals(member)).FileSystemRights & FileSystemRights.Modify, Is.EqualTo(FileSystemRights.Modify));
        Assert.That(rules.Single(r => r.IdentityReference.Equals(member)).FileSystemRights.HasFlag(FileSystemRights.ChangePermissions), Is.False);
        Assert.That(rules.All(r => r.AccessControlType == AccessControlType.Allow && r.InheritanceFlags == (InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit)));
    }

    [Test]
    public async Task A_host_that_exits_at_once_is_reported_and_forgotten()
    {
        var credentials = new FakeCredentials();
        credentials.Save(Account, "hunter2-not-real");
        var alive = true;
        var (launcher, _, ledger) = Launcher(credentials, registers: false, hostStart: _ => alive ? HostStart : null,
            onDelay: () => alive = false);
        var result = await launcher.StartAsync(Repo, Login, Account, CancellationToken.None);
        Assert.That(result.Ok, Is.False);
        Assert.That(new TermMemberHosts(Path.Combine(root, "gateway"), _ => HostStart).Live(), Is.Empty);
        Assert.That(ledger.Live(), Is.Empty);
    }

    [Test]
    public async Task A_run_as_failure_says_why_and_records_nothing()
    {
        var credentials = new FakeCredentials();
        credentials.Save(Account, "hunter2-not-real");
        var (launcher, runAs, ledger) = Launcher(credentials);
        runAs.Fail = "The user name or password is incorrect.";
        var result = await launcher.StartAsync(Repo, Login, Account, CancellationToken.None);
        Assert.That(result.Ok, Is.False);
        Assert.That(result.Output, Does.Contain("incorrect"));
        Assert.That(ledger.Live(), Is.Empty);
    }

    [Test]
    public async Task Without_a_member_launcher_a_member_user_plan_is_not_supported()
    {
        var result = await new OrchestratorLauncher().StartAsync(Repo, Login, new TermLaunchPlan(TermLaunchKind.MemberUser, Account), CancellationToken.None);
        Assert.That(result.NotSupported, Is.True);
    }

    [Test]
    public void Arguments_are_quoted_for_create_process()
    {
        Assert.That(MemberSessionLauncher.Quote("plain"), Is.EqualTo("plain"));
        Assert.That(MemberSessionLauncher.Quote(@"C:\Program Files\x"), Is.EqualTo("\"C:\\Program Files\\x\""));
        Assert.That(MemberSessionLauncher.Quote(@"C:\dir with space\"), Is.EqualTo("\"C:\\dir with space\\\\\""));
        Assert.That(MemberSessionLauncher.Quote("a\"b"), Is.EqualTo("\"a\\\"b\""));
        Assert.That(MemberSessionLauncher.Quote(""), Is.EqualTo("\"\""));
    }

    [Test]
    public void Account_names_split_into_domain_and_user()
    {
        Assert.That(LogonRunAs.Split("erin-cli"), Is.EqualTo((".", "erin-cli")));
        Assert.That(LogonRunAs.Split(@".\erin-cli"), Is.EqualTo((".", "erin-cli")));
        Assert.That(LogonRunAs.Split(@"HOSTBOX\erin-cli"), Is.EqualTo(("HOSTBOX", "erin-cli")));
    }

    // ── the policy and setting ──────────────────────────────────────────────────────────────────────────────────────────────

    [Test]
    public void The_start_wait_comes_from_the_vault_and_is_clamped()
    {
        var s = new OrchestratorSettings { HubMemberSessions = true, HubMemberStartWaitSeconds = 40 };
        var policy = HubPolicy.From(s, MachineTopology.For(MachineRoleKind.Host), [], new NoAccounts())!;
        Assert.That(policy.StartWait, Is.EqualTo(TimeSpan.FromSeconds(40)));
        Assert.That(new TermHubPolicy { StartWaitSeconds = double.NaN }.StartWait, Is.EqualTo(TimeSpan.FromSeconds(TermHubPolicy.DefaultStartWaitSeconds)));
        Assert.That(new TermHubPolicy { StartWaitSeconds = 99999 }.StartWait, Is.EqualTo(TimeSpan.FromSeconds(300)));
        Assert.That(new OrchestratorSettings().HubMemberStartWaitSeconds, Is.EqualTo(TermHubPolicy.DefaultStartWaitSeconds));
    }

    // ── the TermHost side ───────────────────────────────────────────────────────────────────────────────────────────────────

    [Test]
    public void A_member_host_takes_an_account_sid_and_a_full_registry_path_together()
    {
        var ok = HostOptions.Parse(["--peer-sid", PeerSid, "--registry", root, "--", "claude"], out var error);
        Assert.That(ok, Is.Not.Null, error);
        Assert.That((ok!.PeerSid, ok.RegistryDir, ok.IsMemberHost), Is.EqualTo((PeerSid, root, true)));
        var detached = ok.DetachedArguments();
        Assert.That(detached, Does.Contain("--peer-sid").And.Contain(PeerSid).And.Contain("--registry").And.Contain(root));

        Assert.That(HostOptions.Parse(["--peer-sid", PeerSid], out _), Is.Null, "a peer without a registry folder");
        Assert.That(HostOptions.Parse(["--registry", root], out _), Is.Null, "a registry folder without a peer");
        Assert.That(HostOptions.Parse(["--peer-sid", "S-1-1-0", "--registry", root], out _), Is.Null, "Everyone is never a peer");
        Assert.That(HostOptions.Parse(["--peer-sid", "S-1-5-32-545", "--registry", root], out _), Is.Null, "nor BUILTIN\\Users");
        Assert.That(HostOptions.Parse(["--peer-sid", "nonsense", "--registry", root], out _), Is.Null);
        Assert.That(HostOptions.Parse(["--peer-sid", PeerSid, "--registry", "relative"], out _), Is.Null);
        Assert.That(HostOptions.Parse(["--", "claude"], out _)!.IsMemberHost, Is.False);
    }

    [Test]
    public void The_peer_is_added_to_the_process_dacl_with_query_terminate_and_synchronize_only()
    {
        var self = TermPipeAcl.CurrentUser;
        var peer = new SecurityIdentifier(PeerSid);
        var dacl = new RawAcl(GenericAcl.AclRevision, 1);
        dacl.InsertAce(0, new CommonAce(AceFlags.None, AceQualifier.AccessAllowed, 0x1FFFFF, self, false, null));

        var result = MemberHost.WithPeer(dacl, peer)!;
        Assert.That(result.Count, Is.EqualTo(2));
        var added = (CommonAce)result[1];
        Assert.That((added.SecurityIdentifier, added.AccessMask, added.AceQualifier), Is.EqualTo((peer, MemberHost.PeerProcessRights, AceQualifier.AccessAllowed)));
        Assert.That(dacl.Count, Is.EqualTo(1), "the input is left alone");
        Assert.That(MemberHost.WithPeer(null, peer), Is.Null, "a null DACL stays null");
    }

    [Test]
    public void The_pipe_dacl_is_protected_and_names_only_the_host_the_peer_and_a_network_deny()
    {
        var self = TermPipeAcl.CurrentUser;
        var peer = new SecurityIdentifier(PeerSid);
        var security = TermPipeAcl.For(self, peer);
        Assert.That(security.AreAccessRulesProtected, Is.True);
        var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().ToList();
        Assert.That(rules, Has.Count.EqualTo(3));
        Assert.That(rules.Single(r => r.IdentityReference.Equals(self)).PipeAccessRights, Is.EqualTo(PipeAccessRights.FullControl));
        var peerRule = rules.Single(r => r.IdentityReference.Equals(peer));
        Assert.That((peerRule.PipeAccessRights, peerRule.AccessControlType), Is.EqualTo((TermPipeAcl.PeerRights, AccessControlType.Allow)));
        Assert.That(peerRule.PipeAccessRights.HasFlag(PipeAccessRights.CreateNewInstance), Is.False);
        Assert.That(peerRule.PipeAccessRights.HasFlag(PipeAccessRights.ChangePermissions), Is.False);
        var network = rules.Single(r => r.IdentityReference.Equals(new SecurityIdentifier(WellKnownSidType.NetworkSid, null)));
        Assert.That(network.AccessControlType, Is.EqualTo(AccessControlType.Deny));
    }

    [Test]
    public async Task A_throwaway_pipe_acld_to_the_current_user_serves_it_and_names_its_server_process()
    {
        // The one real object: a pipe this test creates and ACLs to the current user only (as peer too), then disposes.
        var name = "mindattic-test-member-pipe-" + Guid.NewGuid().ToString("N")[..12];
        await using var server = TermPipeAcl.CreateServer(name, TermPipeAcl.CurrentUser, firstInstance: true);
        var waiting = server.WaitForConnectionAsync();
        await using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Identification);
        await client.ConnectAsync(5000);
        await waiting;
        Assert.That(TermPipeAcl.ServerProcessId(client), Is.EqualTo(Environment.ProcessId));
        // A taken name fails as a first instance.
        Assert.Throws(Is.InstanceOf<IOException>().Or.InstanceOf<UnauthorizedAccessException>(),
            () => TermPipeAcl.CreateServer(name, TermPipeAcl.CurrentUser, firstInstance: true).Dispose());
    }

    [Test]
    public void Only_account_sids_parse()
    {
        Assert.That(TermPipeAcl.ParseSid(PeerSid)?.Value, Is.EqualTo(PeerSid));
        Assert.That(TermPipeAcl.ParseSid("S-1-1-0"), Is.Null);
        Assert.That(TermPipeAcl.ParseSid(null), Is.Null);
        Assert.That(TermPipeAcl.ParseSid("erin"), Is.Null);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    private static TermMemberHost Host(string id, int pid) => new()
    {
        Id = id, Account = Account, Login = Login, Repo = Repo, HostPid = pid, HostStartUtc = HostStart, RegistryDir = @"C:\nowhere",
        Pipe = TermProtocol.PipeName(id, "erin-cli"), StartedUtc = HostStart,
    };

    private static TermSessionRecord Record(string id, int pid, DateTime start) => new()
    {
        Id = id, HostPid = pid, HostStartUtc = start, Pipe = TermProtocol.PipeName(id), StartedUtc = start, Login = Login,
    };

    private (MemberSessionLauncher Launcher, FakeRunAs RunAs, TermMemberHosts Ledger) Launcher(FakeCredentials credentials, bool makeRegistry = true,
        bool registers = true, Func<int, DateTime?>? hostStart = null, Action? onDelay = null, string? tempRoot = null,
        Func<string, string, string?>? prepareTemp = null)
    {
        tempRoot ??= Directory.CreateDirectory(Path.Combine(root, "temp")).FullName;
        var exe = Path.Combine(root, "TermHost", "MindAttic.Orchestrator.TermHost.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(exe)!);
        File.WriteAllText(exe, "");
        var workspace = Path.Combine(root, "workspace");
        Directory.CreateDirectory(Path.Combine(workspace, Repo));
        var members = Path.Combine(root, "members");
        if (makeRegistry) Directory.CreateDirectory(TermMemberHosts.RegistryDirOf(Account, members));
        hostStart ??= _ => HostStart;
        var ledger = new TermMemberHosts(Path.Combine(root, "gateway"), hostStart);
        var runAs = new FakeRunAs();
        var launcher = new MemberSessionLauncher(runAs, credentials, ledger, () => exe, () => workspace, () => members, () => PeerSid,
            () => TimeSpan.FromSeconds(5),
            delay: (_, _) => { onDelay?.Invoke(); return Task.CompletedTask; },
            registered: (id, _) => registers ? new TermSessionRecord { Id = id, HostPid = 4242, HostStartUtc = HostStart } : null,
            hostStart: hostStart, tempRoot: () => tempRoot, prepareTemp: prepareTemp ?? ((_, dir) => { Directory.CreateDirectory(dir); return null; }));
        return (launcher, runAs, ledger);
    }

    private sealed class FakeRunAs : IRunAs
    {
        public List<(string Account, string CommandLine)> Calls { get; } = [];
        public List<string> PasswordsSeen { get; } = [];
        public string? Fail { get; set; }

        public RunAsResult Start(string account, IMemberCredentials credentials, string application, string commandLine, string workingDirectory)
        {
            Calls.Add((account, commandLine));
            PasswordsSeen.Add(credentials.Use(account, p => Marshal.PtrToStringUni(p) ?? ""));
            return Fail is { } why ? RunAsResult.Fail(why) : new RunAsResult(4242, HostStart);
        }
    }

    private sealed class FakeCredentials : IMemberCredentials
    {
        private readonly Dictionary<string, string> store = new(StringComparer.OrdinalIgnoreCase);
        public bool Has(string account) => store.ContainsKey(account);
        public void Save(string account, ReadOnlySpan<char> password) => store[account] = password.ToString();
        public bool Forget(string account) => store.Remove(account);

        public T Use<T>(string account, Func<IntPtr, T> use)
        {
            if (!store.TryGetValue(account, out var password)) throw new KeyNotFoundException(account);
            var buffer = Marshal.StringToHGlobalUni(password);
            try { return use(buffer); }
            finally { Marshal.ZeroFreeGlobalAllocUnicode(buffer); }
        }
    }

    private sealed class ListSessions(params TermSessionRecord[] records) : ISessionSource
    {
        public IReadOnlyList<TermSessionRecord> Enumerate() => records;
        public TermSessionRecord? Get(string id) => records.FirstOrDefault(r => r.Id == id);
    }

    private sealed class FixedAccounts(string account) : ISessionAccounts
    {
        public string? AccountOf(TermSessionRecord session) => account;
    }

    private sealed class NoAccounts : IWindowsAccounts
    {
        public bool Exists(string account) => false;
        public string CurrentUser => @"HOSTBOX\owner";
    }
}
