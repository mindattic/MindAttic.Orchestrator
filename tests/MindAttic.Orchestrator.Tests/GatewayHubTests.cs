using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using MindAttic.Orchestrator.Gateway;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Setup;
using MindAttic.Orchestrator.Term;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// Hub phase 2 (M.ORCH-T1641): session ownership by login, typing only with isolation or "trusted org", the session owner's
/// consent to another member's interact grant, members starting and stopping their own CLIs, and the per-member Windows user
/// policy, all through fakes and seams: nothing here touches a real Windows account, group, ACL or credential. With no hub policy
/// the gateway decides exactly as before.
/// </summary>
public sealed partial class GatewayTests
{
    private const string SamAccount = @"HOSTBOX\sam";

    /// <summary><see cref="Acl"/> with a hub policy: member sessions on, and the given isolation.</summary>
    private static TermGatewayAclSnapshot HubAcl(bool trusted = false, bool windowsUsers = false, TermAclGrant[]? grants = null) => Acl(grants ?? []) with
    {
        Hub = new TermHubPolicy
        {
            MemberSessions = true, TrustedOrg = trusted, WindowsUsers = windowsUsers, ConsentSeconds = 30,
            Accounts = windowsUsers ? [new TermHubAccount(Sam, SamAccount)] : [],
        },
    };

    // ── off: exactly as before ──────────────────────────────────────────────────

    [Test]
    public void Without_a_hub_policy_the_decision_is_the_old_evaluation_with_no_consent()
    {
        var grant = new TermAclGrant(Owner, Pat, "", "interact", Now.AddHours(1));
        foreach (var viewer in new[] { Owner, Erin, Sam, Pat, "stranger@example.com" })
        {
            var d = AclGrants.Decide(Acl(grant), Owner, viewer, SessionOn("s1", Sam), Now, SamAccount);
            Assert.That(d.Access, Is.EqualTo(AclGrants.Evaluate(Acl(grant), Owner, viewer, SessionOn("s1", Sam), Now)), viewer);
            Assert.That(d.ConsentKey, Is.Null, viewer);
            Assert.That(d.SessionOwner, Is.Null, viewer);
        }
    }

    [Test]
    public void A_snapshot_without_a_hub_policy_writes_no_hub_section()
    {
        var text = JsonSerializer.Serialize(Acl());
        Assert.That(text, Does.Not.Contain("\"hub\""));
        Assert.That(JsonSerializer.Serialize(HubAcl()), Does.Contain("\"hub\""));
    }

    [Test]
    public void The_policy_is_null_unless_member_sessions_are_on_and_this_is_the_host()
    {
        var keys = new List<TermMemberKey>();
        var accounts = new FakeAccounts();
        var on = new OrchestratorSettings { HubMemberSessions = true };
        Assert.That(HubPolicy.From(new OrchestratorSettings(), MachineTopology.For(MachineRoleKind.Host), keys, accounts), Is.Null, "off by default");
        Assert.That(HubPolicy.From(on, MachineTopology.For(MachineRoleKind.Standalone), keys, accounts), Is.Null, "standalone never has one");
        Assert.That(HubPolicy.From(on, MachineTopology.For(MachineRoleKind.Client), keys, accounts), Is.Null, "nor a client");
        var policy = HubPolicy.From(on, MachineTopology.For(MachineRoleKind.Host), keys, accounts);
        Assert.That(policy, Is.Not.Null);
        Assert.That(policy!.TrustedOrg || policy.WindowsUsers, Is.False, "isolation and trust stay off unless set");
        Assert.That(policy.ConsentSeconds, Is.EqualTo(OrchestratorSettings.DefaultHubConsentSeconds));
    }

    // ── ownership and typing ────────────────────────────────────────────────────

    [Test]
    public void The_sessions_owner_is_the_member_whose_login_it_runs_on()
    {
        Assert.That(AclGrants.SessionOwner(HubAcl(), SessionOn("s1", Sam)), Is.EqualTo(Sam));
        Assert.That(AclGrants.SessionOwner(HubAcl(), SessionOn("s1", "SAM@example.com")), Is.EqualTo(Sam), "any case");
        Assert.That(AclGrants.SessionOwner(HubAcl(), SessionOn("s1", null)), Is.Null, "the global login is nobody's");
        Assert.That(AclGrants.SessionOwner(HubAcl(), SessionOn("s1", "someone@example.com")), Is.Null, "a login no member owns");
    }

    [Test]
    public void A_member_only_watches_their_own_cli_while_it_runs_as_the_hosts_one_user()
    {
        var d = AclGrants.Decide(HubAcl(), Owner, Sam, SessionOn("s1", Sam), Now, @"HOSTBOX\owner");
        Assert.That(d.Access, Is.EqualTo(GatewayAccess.Watch), "no isolation and no trusted org: typing refused");
        Assert.That(d.SessionOwner, Is.EqualTo(Sam));
    }

    [Test]
    public void A_member_types_into_their_own_cli_under_trusted_org_or_their_own_windows_account()
    {
        Assert.That(AclGrants.Decide(HubAcl(trusted: true), Owner, Sam, SessionOn("s1", Sam), Now, @"HOSTBOX\owner").Access, Is.EqualTo(GatewayAccess.Interact));
        Assert.That(AclGrants.Decide(HubAcl(windowsUsers: true), Owner, Sam, SessionOn("s1", Sam), Now, SamAccount).Access, Is.EqualTo(GatewayAccess.Interact));
        Assert.That(AclGrants.Decide(HubAcl(windowsUsers: true), Owner, Sam, SessionOn("s1", Sam), Now, @"HOSTBOX\owner").Access,
            Is.EqualTo(GatewayAccess.Watch), "mapped, but this CLI still runs as the host's user");
    }

    [Test]
    public void The_machine_owner_always_interacts_and_a_stranger_gets_nothing()
    {
        Assert.That(AclGrants.Decide(HubAcl(), Owner, Owner, SessionOn("s1", Sam), Now, null).Access, Is.EqualTo(GatewayAccess.Interact));
        Assert.That(AclGrants.Decide(HubAcl(trusted: true), Owner, "stranger@example.com", SessionOn("s1", Sam), Now, null).Access, Is.EqualTo(GatewayAccess.None));
    }

    [Test]
    public void Another_members_interact_grant_on_a_members_cli_waits_on_its_owners_consent()
    {
        var grant = new TermAclGrant(Owner, Erin, "", "interact", Now.AddHours(1));
        var d = AclGrants.Decide(HubAcl(trusted: true, grants: [grant]), Owner, Erin, SessionOn("s1", Sam), Now, null);
        Assert.That(d.Access, Is.EqualTo(GatewayAccess.Interact));
        Assert.That(d.ConsentKey, Is.Not.Null.And.Contains(Sam), "Sam owns s1 and is asked");
        Assert.That(d.SessionOwner, Is.EqualTo(Sam));

        var later = new TermAclGrant(Owner, Erin, "", "interact", Now.AddHours(2));
        Assert.That(AclGrants.Decide(HubAcl(trusted: true, grants: [later]), Owner, Erin, SessionOn("s1", Sam), Now, null).ConsentKey,
            Is.Not.EqualTo(d.ConsentKey), "a new grant is asked about again");
    }

    [Test]
    public void An_interact_grant_counts_only_as_watch_on_a_cli_that_may_not_be_typed_into()
    {
        var grant = new TermAclGrant(Owner, Pat, "", "interact", Now.AddHours(1));
        var d = AclGrants.Decide(HubAcl(grants: [grant]), Owner, Pat, SessionOn("s1", Sam), Now, @"HOSTBOX\owner");
        Assert.That(d.Access, Is.EqualTo(GatewayAccess.Watch), "Pat is in ops, so only the grant lets him see it, read-only");
        Assert.That(d.ConsentKey, Is.Null);
    }

    [Test]
    public void A_grant_on_the_machine_owners_own_cli_needs_no_consent_under_trusted_org()
    {
        var grant = new TermAclGrant(Owner, Erin, "", "interact", Now.AddHours(1));
        var d = AclGrants.Decide(HubAcl(trusted: true, grants: [grant]), Owner, Erin, SessionOn("s1", "nobodys@example.com"), Now, null);
        Assert.That(d.Access, Is.EqualTo(GatewayAccess.Interact));
        Assert.That(d.ConsentKey, Is.Null, "the machine owner granted it on their own CLI");
    }

    // ── consent ────────────────────────────────────────────────────────────────

    [Test]
    public void A_consent_request_waits_then_lapses_to_watch_and_may_be_asked_again()
    {
        var clock = Now;
        var consents = new GatewayConsents(() => clock);
        Assert.That(consents.State("k"), Is.EqualTo(ConsentState.None));
        var asked = consents.Ask("k", Sam, Erin, "s1", TimeSpan.FromSeconds(30))!;
        Assert.That(consents.State("k"), Is.EqualTo(ConsentState.Pending));
        Assert.That(consents.Ask("k", Sam, Erin, "s1", TimeSpan.FromSeconds(30))!.Id, Is.EqualTo(asked.Id), "one request per grant at a time");
        Assert.That(consents.PendingFor(Sam), Has.Count.EqualTo(1));
        Assert.That(consents.PendingFor(Erin), Is.Empty, "only the session's owner sees it");

        clock = Now.AddSeconds(31);
        Assert.That(consents.State("k"), Is.EqualTo(ConsentState.None), "no answer in time: watch only");
        Assert.That(consents.Answer(asked.Id, Sam, allow: true), Is.Null, "too late to answer");
        Assert.That(consents.Ask("k", Sam, Erin, "s1", TimeSpan.FromSeconds(30))!.Id, Is.Not.EqualTo(asked.Id));
    }

    [Test]
    public void Only_the_sessions_owner_answers_and_the_answer_lasts()
    {
        var consents = new GatewayConsents(() => Now);
        var asked = consents.Ask("k", Sam, Erin, "s1", TimeSpan.FromSeconds(30))!;
        Assert.That(consents.Answer(asked.Id, Erin, allow: true), Is.Null, "the viewer can't consent for the owner");
        Assert.That(consents.Answer(asked.Id, "SAM@example.com", allow: true), Is.Not.Null);
        Assert.That(consents.State("k"), Is.EqualTo(ConsentState.Allowed));
        Assert.That(consents.Ask("k", Sam, Erin, "s1", TimeSpan.FromSeconds(30)), Is.Null, "answered: nothing more to ask");

        var refused = consents.Ask("k2", Sam, Pat, "s1", TimeSpan.FromSeconds(30))!;
        consents.Answer(refused.Id, Sam, allow: false);
        Assert.That(consents.State("k2"), Is.EqualTo(ConsentState.Denied));
    }

    [Test]
    public void Waiting_requests_are_bounded()
    {
        var consents = new GatewayConsents(() => Now);
        for (var i = 0; i < GatewayConsents.MaxPending + 10; i++) consents.Ask("k" + i, Sam, Erin, "s1", TimeSpan.FromMinutes(1));
        Assert.That(consents.PendingFor(Sam), Has.Count.EqualTo(GatewayConsents.MaxPending));
    }

    // ── launch plans and Windows accounts ───────────────────────────────────────

    [Test]
    public void A_member_cli_starts_under_their_own_account_as_the_host_user_when_trusted_and_otherwise_not_at_all()
    {
        var isolated = HubAcl(windowsUsers: true).Hub!;
        Assert.That(isolated.PlanLaunch(Sam), Is.EqualTo(new TermLaunchPlan(TermLaunchKind.MemberUser, SamAccount)));
        Assert.That(isolated.PlanLaunch(Erin).Kind, Is.EqualTo(TermLaunchKind.Refused), "no account mapped for Erin");
        Assert.That(HubAcl(trusted: true).Hub!.PlanLaunch(Erin), Is.EqualTo(TermLaunchPlan.Host));
        Assert.That(HubAcl().Hub!.PlanLaunch(Sam).Kind, Is.EqualTo(TermLaunchKind.Refused));
        Assert.That(new TermHubPolicy().PlanLaunch(Sam).Kind, Is.EqualTo(TermLaunchKind.Refused), "member sessions off");
    }

    [Test]
    public void Windows_account_names_are_checked_and_never_the_hosts_own_user()
    {
        Assert.That(TermWindowsAccounts.Normalize(@" HOSTBOX\sam ", @"HOSTBOX\owner", out _), Is.EqualTo(@"HOSTBOX\sam"));
        Assert.That(TermWindowsAccounts.Normalize(@".\sam", @"HOSTBOX\owner", out _), Is.EqualTo(@".\sam"));
        Assert.That(TermWindowsAccounts.Normalize("sam", @"HOSTBOX\owner", out _), Is.EqualTo("sam"));
        Assert.That(TermWindowsAccounts.Normalize("owner", @"HOSTBOX\owner", out var self), Is.Null);
        Assert.That(self, Does.Contain("host's own"));
        Assert.That(TermWindowsAccounts.Same(@"HOSTBOX\Sam", @"hostbox\sam"), Is.True);
        Assert.That(TermWindowsAccounts.Same("sam", @"HOSTBOX\sam"), Is.True);
        Assert.That(TermWindowsAccounts.Same(@"OTHER\sam", @"HOSTBOX\sam"), Is.False);
    }

    [Test]
    public void Bad_windows_account_names_are_refused_with_a_reason()
    {
        foreach (var bad in new[] { "", "sam@example.com", @"a\b\c", @"\sam", "sa*m", "sam.", new string('x', 121) })
        {
            Assert.That(TermWindowsAccounts.Normalize(bad, @"HOSTBOX\owner", out var why), Is.Null, bad);
            Assert.That(why, Is.Not.Empty, bad);
        }
    }

    [Test]
    public void The_policy_maps_only_live_members_whose_account_is_well_formed_not_the_hosts_and_exists()
    {
        var s = new OrchestratorSettings { HubMemberSessions = true, HubMemberWindowsUsers = true, HubConsentSeconds = 45 };
        var keys = new List<TermMemberKey>
        {
            new() { Email = Sam, Hash = "h", WindowsUser = SamAccount },
            new() { Email = Erin, Hash = "h", WindowsUser = @"HOSTBOX\erin" },        // no such account here
            new() { Email = Pat, Hash = "h", WindowsUser = @"HOSTBOX\owner" },        // the host's own user
            new() { Email = "gone@example.com", Hash = "h", WindowsUser = @"HOSTBOX\gone", RevokedUtc = Now },
        };
        var accounts = new FakeAccounts(SamAccount, @"HOSTBOX\gone", @"HOSTBOX\owner");
        var policy = HubPolicy.From(s, MachineTopology.For(MachineRoleKind.Host), keys, accounts)!;
        Assert.That(policy.Accounts, Is.EqualTo(new[] { new TermHubAccount(Sam, SamAccount) }));
        Assert.That(policy.ConsentSeconds, Is.EqualTo(45));
        Assert.That(accounts.Looked, Does.Not.Contain(@"HOSTBOX\owner"), "the host's own user is refused before any lookup");

        var withoutUsers = HubPolicy.From(s with { HubMemberWindowsUsers = false }, MachineTopology.For(MachineRoleKind.Host), keys, accounts)!;
        Assert.That(withoutUsers.Accounts, Is.Empty, "per-member users off: no accounts, so no isolation");
    }

    [Test]
    public void A_members_windows_account_is_kept_by_name_and_survives_a_new_invite()
    {
        using var dir = new TempDir();
        TermMemberKeys.Invite(Sam, "owner", Now, dir.Path);
        Assert.That(TermMemberKeys.SetWindowsUser(Sam, SamAccount, dir.Path), Is.True);
        Assert.That(TermMemberKeys.SetWindowsUser("never@example.com", SamAccount, dir.Path), Is.False, "never invited");
        TermMemberKeys.Invite(Sam, "owner", Now.AddMinutes(1), dir.Path);
        Assert.That(TermMemberKeys.List(dir.Path).Single().WindowsUser, Is.EqualTo(SamAccount));
        TermMemberKeys.SetWindowsUser(Sam, null, dir.Path);
        Assert.That(TermMemberKeys.List(dir.Path).Single().WindowsUser, Is.Null);
        Assert.That(File.ReadAllText(TermMemberKeys.KeysPath(dir.Path)), Does.Not.Contain("windowsUser"), "left out when unset");
    }

    // ── endpoints ───────────────────────────────────────────────────────────────

    [Test]
    public async Task Without_a_hub_policy_the_new_endpoints_answer_404()
    {
        using var dir = new TempDir();
        TermGatewayAudit.WriteAcl(Acl(), dir.Path);
        await using var gateway = await HubGateway.StartAsync(dir.Path);
        using var http = gateway.Http(Sam);
        Assert.That((await http.PostAsync("/sessions", Json("{\"repo\":\"MindAttic.Orchestrator\"}"))).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await http.DeleteAsync("/sessions/s1")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await http.GetAsync("/consents")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(gateway.Launcher.Started, Is.Empty);
        Assert.That(gateway.Launcher.Stopped, Is.Empty);
    }

    [Test]
    public async Task A_member_starts_a_cli_on_their_own_login_but_never_on_another_members()
    {
        using var dir = new TempDir();
        TermGatewayAudit.WriteAcl(HubAcl(trusted: true), dir.Path);
        await using var gateway = await HubGateway.StartAsync(dir.Path);
        using var http = gateway.Http(Sam);

        var mine = await http.PostAsync("/sessions", Json("{\"repo\":\"MindAttic.Orchestrator\"}"));
        Assert.That(mine.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(gateway.Launcher.Started, Is.EqualTo(new[] { ("MindAttic.Orchestrator", Sam, TermLaunchKind.HostUser) }));

        var theirs = await http.PostAsync("/sessions", Json($"{{\"repo\":\"MindAttic.Orchestrator\",\"login\":\"{Erin}\"}}"));
        Assert.That(theirs.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That((await http.PostAsync("/sessions", Json("{\"repo\":\"..\\\\Windows\"}"))).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), "a repo name, never a path");
        Assert.That(gateway.Launcher.Started, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Without_isolation_or_trust_a_member_cant_start_a_cli()
    {
        using var dir = new TempDir();
        TermGatewayAudit.WriteAcl(HubAcl(), dir.Path);
        await using var gateway = await HubGateway.StartAsync(dir.Path);
        using var http = gateway.Http(Sam);
        Assert.That((await http.PostAsync("/sessions", Json("{\"repo\":\"MindAttic.Orchestrator\"}"))).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(gateway.Launcher.Started, Is.Empty);
    }

    [Test]
    public async Task A_member_stops_only_their_own_cli()
    {
        using var dir = new TempDir();
        TermGatewayAudit.WriteAcl(HubAcl(trusted: true), dir.Path);
        await using var gateway = await HubGateway.StartAsync(dir.Path);
        using (var erin = gateway.Http(Erin))
            Assert.That((await erin.DeleteAsync("/sessions/s1")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound), "s1 is Sam's");
        Assert.That(gateway.Launcher.Stopped, Is.Empty);
        using (var sam = gateway.Http(Sam))
            Assert.That((await sam.DeleteAsync("/sessions/s1")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(gateway.Launcher.Stopped, Is.EqualTo(new[] { "s1" }));
    }

    [Test]
    public async Task A_granted_member_watches_until_the_owner_consents_and_then_interacts()
    {
        using var dir = new TempDir();
        TermGatewayAudit.WriteAcl(HubAcl(trusted: true, grants: [new TermAclGrant(Owner, Erin, "", "interact", DateTime.UtcNow.AddHours(1))]), dir.Path);
        await using var gateway = await HubGateway.StartAsync(dir.Path);
        using var erin = gateway.Http(Erin);

        Assert.That(await erin.GetStringAsync("/sessions"), Does.Contain("\"access\":\"watch\"").And.Contain("\"consent\":\"ask\""));
        var key = AclGrants.Decide(TermGatewayAudit.ReadAcl(dir.Path), Owner, Erin, gateway.Session, DateTime.UtcNow, null).ConsentKey!;
        var asked = gateway.Services.Consents.Ask(key, Sam, Erin, "s1", TimeSpan.FromSeconds(30))!;
        Assert.That(await erin.GetStringAsync("/sessions"), Does.Contain("\"consent\":\"pending\""));

        using (var sam = gateway.Http(Sam))
        {
            Assert.That(await sam.GetStringAsync("/consents"), Does.Contain(asked.Id));
            using (var other = gateway.Http(Pat))
                Assert.That((await other.PostAsync($"/consents/{asked.Id}", Json("{\"allow\":true}"))).StatusCode, Is.EqualTo(HttpStatusCode.NotFound), "not Pat's to answer");
            Assert.That((await sam.PostAsync($"/consents/{asked.Id}", Json("{\"allow\":true}"))).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }
        var listed = await erin.GetStringAsync("/sessions");
        Assert.That(listed, Does.Contain("\"access\":\"interact\"").And.Not.Contain("\"consent\""));
    }

    /// <summary>A gateway on a TestServer for hub phase 2: the ACL from a snapshot in a folder (the real <see cref="AclGrants"/>),
    /// session "s1" on Sam's login, a recording launcher, and every session running as Sam's account.</summary>
    private sealed class HubGateway : IAsyncDisposable
    {
        private WebApplication app = null!;
        public GatewayServices Services { get; private set; } = null!;
        public RecordingLauncher Launcher { get; } = new();
        public TermSessionRecord Session { get; } = GatewayTests.Session("s1") with { Login = Sam };

        public static async Task<HubGateway> StartAsync(string aclDirectory)
        {
            var gateway = new HubGateway();
            gateway.Services = new GatewayServices
            {
                Options = new GatewayOptions { Owner = Owner },
                Tokens = new GatewayTokens(SigningKey),
                OwnerKey = OwnerKey,
                Grants = new AclGrants(Owner, aclDirectory),
                Sessions = new FixedSessions(gateway.Session),
                Pipes = new MemoryPipes(),
                Log = new GatewayLog(null),
                Broadcast = new FixedBroadcast(new TermBroadcastSettings().Share("s1", true).Mask("s1", false)),
                Watchers = new WatcherBoard(null, null),
                SessionAccounts = new FixedAccount(SamAccount),
                Launcher = gateway.Launcher,
            };
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], EnvironmentName = "Testing" });
            builder.WebHost.UseTestServer();
            gateway.app = GatewayApp.Build(builder, gateway.Services);
            await gateway.app.StartAsync();
            return gateway;
        }

        public System.Net.Http.HttpClient Http(string subject, string scope = GatewayScopes.Interact)
        {
            var http = app.GetTestServer().CreateClient();
            http.DefaultRequestHeaders.Authorization = new("Bearer", Services.Tokens.Issue(subject, scope, TimeSpan.FromMinutes(5)).Token);
            return http;
        }

        public async ValueTask DisposeAsync()
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    private sealed class RecordingLauncher : ISessionLauncher
    {
        public List<(string Repo, string Login, TermLaunchKind Kind)> Started { get; } = [];
        public List<string> Stopped { get; } = [];

        public Task<LaunchResult> StartAsync(string repo, string login, TermLaunchPlan plan, CancellationToken ct)
        {
            lock (Started) Started.Add((repo, login, plan.Kind));
            return Task.FromResult(new LaunchResult(true, "started"));
        }

        public LaunchResult Stop(TermSessionRecord session)
        {
            lock (Stopped) Stopped.Add(session.Id);
            return new LaunchResult(true, "stopped");
        }
    }

    private sealed class FixedAccount(string account) : ISessionAccounts
    {
        public string? AccountOf(TermSessionRecord session) => account;
    }

    /// <summary>A fake account database: the accounts that "exist", and every name looked up. Touches no real account.</summary>
    private sealed class FakeAccounts(params string[] existing) : IWindowsAccounts
    {
        public List<string> Looked { get; } = [];
        public bool Exists(string account)
        {
            Looked.Add(account);
            return existing.Any(e => TermWindowsAccounts.Same(e, account));
        }
        public string CurrentUser => @"HOSTBOX\owner";
    }
}
