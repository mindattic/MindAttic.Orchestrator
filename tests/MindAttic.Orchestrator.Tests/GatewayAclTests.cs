using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using MindAttic.Orchestrator.Gateway;
using MindAttic.Orchestrator.Term;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The gateway's viewing ACL and audit (M.ORCH-T934): who may watch or interact (the owner always; same-org members
/// watch by default; grants per person or per session, expiring), and the audit records an attach, a remote input and a detach
/// leave, counts only.</summary>
public sealed partial class GatewayTests
{
    private const string Erin = "erin@example.com", Sam = "sam@example.com", Pat = "pat@example.com";
    private static readonly DateTime Now = new(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Erin and Sam are active members in sales; Pat is an active member in ops. The session "s1" runs on Sam's login.</summary>
    private static TermGatewayAclSnapshot Acl(params TermAclGrant[] grants) => new()
    {
        Members = [new(Erin, "Erin"), new(Sam, "Sam"), new(Pat, "Pat")],
        Logins = [new(Erin, "sales", "Erin"), new(Sam, "sales", "Sam"), new(Pat, "ops", "Pat")],
        Grants = [.. grants],
    };

    private static TermSessionRecord SessionOn(string id, string? login) => Session(id) with { Login = login };

    // ── grant evaluation ────────────────────────────────────────────────────────

    [Test]
    public void The_owner_always_interacts_even_with_no_snapshot()
    {
        Assert.That(AclGrants.Evaluate(null, Owner, Owner, SessionOn("s1", Sam), Now), Is.EqualTo(GatewayAccess.Interact));
        Assert.That(AclGrants.Evaluate(Acl(), Owner, Owner.ToUpperInvariant(), SessionOn("s1", Sam), Now), Is.EqualTo(GatewayAccess.Interact));
        Assert.That(AclGrants.Evaluate(null, Owner, Erin, SessionOn("s1", Sam), Now), Is.EqualTo(GatewayAccess.None), "no snapshot: owner only");
    }

    [Test]
    public void A_member_of_the_sessions_org_may_watch_by_default_and_one_of_another_org_may_not()
    {
        Assert.That(AclGrants.Evaluate(Acl(), Owner, Erin, SessionOn("s1", Sam), Now), Is.EqualTo(GatewayAccess.Watch));
        Assert.That(AclGrants.Evaluate(Acl(), Owner, Pat, SessionOn("s1", Sam), Now), Is.EqualTo(GatewayAccess.None));
    }

    [Test]
    public void Unplaced_logins_and_members_are_the_default_orgs()
    {
        var acl = new TermGatewayAclSnapshot { Members = [new(Erin, null)] };
        Assert.That(AclGrants.Evaluate(acl, Owner, Erin, SessionOn("s1", null), Now), Is.EqualTo(GatewayAccess.Watch), "the global login");
        Assert.That(AclGrants.Evaluate(acl, Owner, Erin, SessionOn("s1", "someone@example.com"), Now), Is.EqualTo(GatewayAccess.Watch));
        Assert.That(AclGrants.Evaluate(Acl(), Owner, Erin, SessionOn("s1", null), Now), Is.EqualTo(GatewayAccess.None), "Erin is in sales, the session in default");
    }

    [Test]
    public void Someone_who_isnt_an_active_member_gets_nothing_whatever_a_grant_says()
    {
        var acl = Acl(new TermAclGrant(Owner, "stranger@example.com", "", "interact", Now.AddHours(1)));
        Assert.That(AclGrants.Evaluate(acl, Owner, "stranger@example.com", SessionOn("s1", Sam), Now), Is.EqualTo(GatewayAccess.None));
    }

    [Test]
    public void An_interact_grant_raises_a_member_until_it_expires()
    {
        var acl = Acl(new TermAclGrant(Owner, Pat, "", "interact", Now.AddHours(2)));
        Assert.That(AclGrants.Evaluate(acl, Owner, Pat, SessionOn("s1", Sam), Now), Is.EqualTo(GatewayAccess.Interact));
        Assert.That(AclGrants.Evaluate(acl, Owner, "PAT@example.com ", SessionOn("s1", Sam), Now), Is.EqualTo(GatewayAccess.Interact), "emails match in any case");
        Assert.That(AclGrants.Evaluate(acl, Owner, Pat, SessionOn("s1", Sam), Now.AddHours(2)), Is.EqualTo(GatewayAccess.None), "expired, and Pat isn't in sales");
    }

    [Test]
    public void An_interact_grant_without_an_expiry_is_ignored_but_a_watch_grant_may_have_none()
    {
        Assert.That(AclGrants.Evaluate(Acl(new TermAclGrant(Owner, Pat, "", "interact", null)), Owner, Pat, SessionOn("s1", Sam), Now),
            Is.EqualTo(GatewayAccess.None));
        Assert.That(AclGrants.Evaluate(Acl(new TermAclGrant(Owner, Pat, "", "watch", null)), Owner, Pat, SessionOn("s1", Sam), Now),
            Is.EqualTo(GatewayAccess.Watch));
    }

    [Test]
    public void A_session_grant_covers_only_that_session_and_another_owners_grant_none()
    {
        var acl = Acl(new TermAclGrant(Owner, Erin, "s2", "interact", Now.AddHours(1)), new TermAclGrant("someone-else", Erin, "", "interact", Now.AddHours(1)));
        Assert.That(AclGrants.Evaluate(acl, Owner, Erin, SessionOn("s2", Sam), Now), Is.EqualTo(GatewayAccess.Interact));
        Assert.That(AclGrants.Evaluate(acl, Owner, Erin, SessionOn("s1", Sam), Now), Is.EqualTo(GatewayAccess.Watch), "s1: the org default only");
    }

    [Test]
    public void The_grants_snapshot_is_read_from_the_gateways_folder()
    {
        using var dir = new TempDir();
        var grants = new AclGrants(Owner, dir.Path, () => Now);
        Assert.That(grants.AccessTo(Erin, SessionOn("s1", Sam)), Is.EqualTo(GatewayAccess.None), "no snapshot yet");
        Assert.That(TermGatewayAudit.WriteAcl(Acl(), dir.Path), Is.True);
        Assert.That(TermGatewayAudit.WriteAcl(Acl(), dir.Path), Is.False, "unchanged: not rewritten");
        Assert.That(new AclGrants(Owner, dir.Path, () => Now).AccessTo(Erin, SessionOn("s1", Sam)), Is.EqualTo(GatewayAccess.Watch));
    }

    [Test]
    public void A_snapshot_that_cant_be_read_just_now_keeps_the_last_good_list_and_is_read_again()
    {
        using var dir = new TempDir();
        TermGatewayAudit.WriteAcl(Acl(), dir.Path);
        var grants = new AclGrants(Owner, dir.Path, () => Now);
        Assert.That(grants.AccessTo(Erin, SessionOn("s1", Sam)), Is.EqualTo(GatewayAccess.Watch));

        // A new list lands, but the file is locked the moment the gateway looks (M.ORCH-T986).
        TermGatewayAudit.WriteAcl(Acl(new TermAclGrant(Owner, Erin, "", "interact", Now.AddHours(1))), dir.Path);
        using (new FileStream(TermGatewayAudit.AclPath(dir.Path), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Thread.Sleep(1100);
            Assert.That(grants.AccessTo(Erin, SessionOn("s1", Sam)), Is.EqualTo(GatewayAccess.Watch), "the last good list, not owner-only");
        }
        // Unchanged since, yet read again once it can be.
        Thread.Sleep(1100);
        Assert.That(grants.AccessTo(Erin, SessionOn("s1", Sam)), Is.EqualTo(GatewayAccess.Interact));
    }

    [Test]
    public void Broadcast_settings_that_cant_be_read_just_now_keep_the_last_good_ones_and_are_read_again()
    {
        using var dir = new TempDir();
        TermBroadcast.Update(s => s.Share("s1", true), dir.Path);
        using var file = new BroadcastFile(dir.Path);
        Assert.That(file.Current.IsShared("s1"), Is.True);

        TermBroadcast.Update(s => s.Share("s2", true), dir.Path);
        using (new FileStream(TermBroadcast.PathIn(dir.Path), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Thread.Sleep(BroadcastFile.Interval * 4);
            Assert.That(file.Current.IsShared("s1"), Is.True, "the last good settings, not nothing shared");
        }
        Thread.Sleep(BroadcastFile.Interval * 4);
        Assert.That(file.Current.IsShared("s2"), Is.True, "read again although the file didn't change since");
    }

    // ── enforcement and audit on the TestServer ─────────────────────────────────

    [Test]
    public async Task Sessions_lists_only_what_a_member_may_see_with_the_access_their_grant_gives()
    {
        var acl = Acl(new TermAclGrant(Owner, Pat, "s1", "watch", null));
        await using var gateway = await TestGateway.StartAsync(new FixedAcl(acl));
        using var http = gateway.Server.CreateClient();

        http.DefaultRequestHeaders.Authorization = new("Bearer", gateway.Token(GatewayScopes.Interact, Pat));
        Assert.That(await http.GetStringAsync("/sessions"), Does.Contain("\"access\":\"watch\""), "an interact token can't exceed a watch grant");

        http.DefaultRequestHeaders.Authorization = new("Bearer", gateway.Token(GatewayScopes.Interact, "stranger@example.com"));
        Assert.That(await http.GetStringAsync("/sessions"), Is.EqualTo("[]"));
    }

    [Test]
    public async Task A_watch_grant_forces_a_read_only_stream_and_the_audit_records_the_attach_and_detach()
    {
        var audit = new MemoryAudit();
        await using var gateway = await TestGateway.StartAsync(new FixedAcl(Acl()), audit);
        // Erin, a sales member, may watch s1 (on Sam's sales login) by default; her interact token doesn't change that.
        using var socket = await gateway.Client(GatewayScopes.Interact, Erin).ConnectAsync(new Uri("ws://localhost/sessions/s1/stream"), Timeout());
        await Send(socket, TermProtocol.Frame(TermFrameKind.Hello, TermProtocol.Hello(TermProtocol.Version, 100, 25)));
        var hello = await TermProtocol.ReadFrameAsync(gateway.Pipes.Host, Timeout());
        Assert.That(TermProtocol.ReadHello(hello.Payload), Is.EqualTo(((ushort)TermProtocol.Version, (ushort)0, (ushort)0)), "a watcher's Hello at 0x0");
        await Send(socket, TermProtocol.Frame(TermFrameKind.Input, "secret-typed-text\r"u8));
        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", Timeout());
        Assert.ThrowsAsync<EndOfStreamException>(() => TermProtocol.ReadFrameAsync(gateway.Pipes.Host, Timeout()), "its input never reached the host");

        var records = await audit.WaitFor(TermGatewayAudit.Detach);
        Assert.That(records.Select(r => r.Event), Is.EqualTo(new[] { TermGatewayAudit.Attach, TermGatewayAudit.Detach }), "nothing typed went through: no input record");
        Assert.That(records.All(r => r.Viewer == Erin && r.Session == "s1" && r.Level == "watch" && r.Owner == Owner));
        Assert.That(records[1].Detail, Does.Contain("1 dropped"));
    }

    [Test]
    public async Task An_interactive_viewers_input_is_audited_as_counts_and_sizes_never_content()
    {
        var audit = new MemoryAudit();
        var acl = Acl(new TermAclGrant(Owner, Erin, "", "interact", Now.AddYears(10)));
        await using var gateway = await TestGateway.StartAsync(new FixedAcl(acl), audit);
        using var socket = await gateway.Client(GatewayScopes.Interact, Erin).ConnectAsync(new Uri("ws://localhost/sessions/s1/stream"), Timeout());
        await Send(socket, TermProtocol.Frame(TermFrameKind.Hello, TermProtocol.Hello(TermProtocol.Version, 100, 25)));
        await Send(socket, TermProtocol.Frame(TermFrameKind.Input, "sk-ant-secret\r"u8));
        await Send(socket, TermProtocol.Frame(TermFrameKind.Input, "dir\r"u8));
        var host = gateway.Pipes.Host;
        for (var i = 0; i < 3; i++) await TermProtocol.ReadFrameAsync(host, Timeout());
        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", Timeout());

        var records = await audit.WaitFor(TermGatewayAudit.Detach);
        Assert.That(records.Select(r => r.Event), Is.EqualTo(new[] { TermGatewayAudit.Attach, TermGatewayAudit.Input, TermGatewayAudit.Detach }));
        var input = records[1];
        Assert.That((input.Frames, input.Bytes, input.Level), Is.EqualTo((2L, (long)("sk-ant-secret\r".Length + "dir\r".Length), "interact")));
        var json = string.Join("\n", records.Select(r => System.Text.Json.JsonSerializer.Serialize(r)));
        Assert.That(json, Does.Not.Contain("sk-ant-secret"));
    }

    [Test]
    public async Task A_refused_attach_is_audited_and_answers_404()
    {
        var audit = new MemoryAudit();
        await using var gateway = await TestGateway.StartAsync(new FixedAcl(Acl()), audit);
        var client = gateway.Client(GatewayScopes.Interact, Pat); // ops, and s1 is on a sales login
        Assert.ThrowsAsync<InvalidOperationException>(() => client.ConnectAsync(new Uri("ws://localhost/sessions/s1/stream"), Timeout()));
        var refused = (await audit.WaitFor(TermGatewayAudit.Refused)).Single();
        Assert.That((refused.Viewer, refused.Session, refused.Level, refused.Detail), Is.EqualTo((Pat, "s1", "none", "no grant")));
        Assert.That(gateway.Pipes.Connected, Is.Zero);
    }

    [Test]
    public async Task A_member_cant_attach_to_a_single_viewer_protocol_1_host()
    {
        var audit = new MemoryAudit();
        await using var gateway = await TestGateway.StartAsync(new FixedAcl(Acl()), audit, session: SessionOn("s1", Sam) with { Protocol = 1 });
        // Erin may watch s1 by default, but its host would drop the owner's tab to serve her.
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            gateway.Client(GatewayScopes.Watch, Erin).ConnectAsync(new Uri("ws://localhost/sessions/s1/stream"), Timeout()));
        var refused = (await audit.WaitFor(TermGatewayAudit.Refused)).Single();
        Assert.That((refused.Viewer, refused.Session, refused.Detail), Is.EqualTo((Erin, "s1", "single-viewer host (protocol 1)")));
        Assert.That(gateway.Pipes.Connected, Is.Zero);
    }

    [Test]
    public async Task Bad_tokens_and_wrong_owner_keys_are_audited_without_the_token_or_key()
    {
        var audit = new MemoryAudit();
        await using var gateway = await TestGateway.StartAsync(new FixedAcl(Acl()), audit);
        using var http = gateway.Server.CreateClient();
        const string fakeToken = "v1.not-a-real-token.fake-signature";
        http.DefaultRequestHeaders.Authorization = new("Bearer", fakeToken);
        Assert.That((await http.GetAsync("/sessions")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var ws = gateway.Server.CreateWebSocketClient();
        ws.ConfigureRequest = request => request.Headers.Authorization = "Bearer " + fakeToken;
        Assert.ThrowsAsync<InvalidOperationException>(() => ws.ConnectAsync(new Uri("ws://localhost/sessions/s1/stream"), Timeout()));
        http.DefaultRequestHeaders.Authorization = null;
        const string fakeKey = "fake-owner-key-for-tests";
        var answer = await http.PostAsync("/auth/token", new System.Net.Http.StringContent($$"""{"ownerKey":"{{fakeKey}}"}""", Encoding.UTF8, "application/json"));
        Assert.That(answer.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var refused = (await audit.WaitFor(TermGatewayAudit.Refused)).Where(r => r.Event == TermGatewayAudit.Refused).ToList();
        for (var i = 0; refused.Count < 3 && i < 50; i++)
        {
            await Task.Delay(20);
            refused = (await audit.WaitFor(TermGatewayAudit.Refused)).Where(r => r.Event == TermGatewayAudit.Refused).ToList();
        }
        Assert.That(refused.Select(r => (r.Session, r.Detail)), Is.EquivalentTo(new[]
        {
            ("", "bad or expired token"), ("s1", "bad or expired token"), ("", "wrong owner key"),
        }));
        Assert.That(refused.All(r => r.Viewer == "?" && r.Level == "none"));
        var json = string.Join("\n", refused.Select(r => System.Text.Json.JsonSerializer.Serialize(r)));
        Assert.That(json, Does.Not.Contain("not-a-real-token").And.Not.Contain(fakeKey));
    }

    [Test]
    public void Unauthenticated_refusals_reach_the_audit_at_a_bounded_rate()
    {
        var now = Now;
        var budget = new RefusalBudget(() => now);
        for (var i = 0; i < RefusalBudget.PerMinute; i++) Assert.That(budget.TryTake(out _), Is.True);
        Assert.That(budget.TryTake(out _), Is.False);
        Assert.That(budget.TryTake(out _), Is.False);
        now += TimeSpan.FromMinutes(1);
        Assert.That(budget.TryTake(out var skipped), Is.True);
        Assert.That(skipped, Is.EqualTo(2));
    }

    [Test]
    public async Task A_revoked_grant_closes_the_stream_it_allowed()
    {
        var audit = new MemoryAudit();
        var grants = new FixedAcl(Acl(new TermAclGrant(Owner, Pat, "", "watch", null)));
        await using var gateway = await TestGateway.StartAsync(grants, audit, TimeSpan.FromMilliseconds(100));
        using var socket = await gateway.Client(GatewayScopes.Watch, Pat).ConnectAsync(new Uri("ws://localhost/sessions/s1/stream"), Timeout());
        await Send(socket, TermProtocol.Frame(TermFrameKind.Hello, TermProtocol.Hello(TermProtocol.Version, 100, 25)));
        await TermProtocol.ReadFrameAsync(gateway.Pipes.Host, Timeout());

        grants.Current = Acl(); // revoked: Pat (ops) has no default access to a sales session
        var result = await socket.ReceiveAsync(new byte[256], Timeout());
        Assert.That(result.MessageType, Is.EqualTo(WebSocketMessageType.Close));
        Assert.That((socket.CloseStatus, socket.CloseStatusDescription), Is.EqualTo(((WebSocketCloseStatus?)WebSocketCloseStatus.PolicyViolation, "access revoked")));
        var detach = (await audit.WaitFor(TermGatewayAudit.Detach)).Last();
        Assert.That(detach.Detail, Does.StartWith("ended by access revoked"));
    }

    // ── the audit spool ─────────────────────────────────────────────────────────

    [Test]
    public void Spooled_audit_records_drain_once_and_survive_an_uncommitted_drain()
    {
        using var dir = new TempDir();
        var record = new TermGatewayAuditRecord { AtUtc = Now, Event = TermGatewayAudit.Attach, Viewer = Erin, Session = "s1", Level = "watch" };
        Assert.That(TermGatewayAudit.Append(record, dir.Path), Is.True);

        var (first, _) = TermGatewayAudit.Drain(dir.Path); // not committed: as if the import failed
        Assert.That(first.Single().Id, Is.EqualTo(record.Id));
        var (again, commit) = TermGatewayAudit.Drain(dir.Path);
        Assert.That(again.Single().Id, Is.EqualTo(record.Id), "an uncommitted drain is read again");
        commit();
        Assert.That(TermGatewayAudit.Drain(dir.Path).Records, Is.Empty);
    }

    [Test]
    public async Task A_stream_whose_access_check_keeps_failing_is_closed_not_left_open()
    {
        var audit = new MemoryAudit();
        var grants = new FlakyGrants();
        await using var gateway = await TestGateway.StartAsync(grants, audit, TimeSpan.FromMilliseconds(100));
        using var socket = await gateway.Client(GatewayScopes.Watch, Pat).ConnectAsync(new Uri("ws://localhost/sessions/s1/stream"), Timeout());
        await Send(socket, TermProtocol.Frame(TermFrameKind.Hello, TermProtocol.Hello(TermProtocol.Version, 100, 25)));
        await TermProtocol.ReadFrameAsync(gateway.Pipes.Host, Timeout());

        grants.Throw = true; // as a null entry in acl.json once did: the check throws from now on
        var result = await socket.ReceiveAsync(new byte[256], Timeout());
        Assert.That(result.MessageType, Is.EqualTo(WebSocketMessageType.Close));
        Assert.That(socket.CloseStatus, Is.EqualTo((WebSocketCloseStatus?)WebSocketCloseStatus.PolicyViolation));
        Assert.That(grants.Calls, Is.GreaterThanOrEqualTo(1 + GatewayApp.MaxWatchFailures), "checked again after the first throw");
    }

    [Test]
    public void Null_entries_in_the_snapshot_are_skipped_not_thrown_on()
    {
        var acl = new TermGatewayAclSnapshot { Members = [null!, new(Erin, "Erin")], Logins = [null!, new(Erin, "sales", null!)], Grants = [null!] };
        Assert.That(AclGrants.Evaluate(acl, Owner, Erin, SessionOn("s1", null), Now), Is.EqualTo(GatewayAccess.None));
        Assert.That(AclGrants.Evaluate(acl, Owner, Erin, SessionOn("s1", Erin), Now), Is.EqualTo(GatewayAccess.Watch));
    }

    private sealed class FlakyGrants : IGatewayGrants
    {
        public volatile bool Throw;
        public int Calls;

        public GatewayAccess AccessTo(string viewer, TermSessionRecord session)
        {
            Interlocked.Increment(ref Calls);
            return Throw ? throw new InvalidOperationException("a damaged grant (test)") : GatewayAccess.Watch;
        }
    }

    private sealed class FixedAcl(TermGatewayAclSnapshot acl) : IGatewayGrants
    {
        public volatile TermGatewayAclSnapshot Current = acl;
        public GatewayAccess AccessTo(string viewer, TermSessionRecord session) => AclGrants.Evaluate(Current, Owner, viewer, session, Now);
    }

    private sealed class MemoryAudit : IGatewayAudit
    {
        private readonly ConcurrentQueue<TermGatewayAuditRecord> records = new();

        public void Write(TermGatewayAuditRecord record) => records.Enqueue(record);

        /// <summary>The records so far, once one of <paramref name="what"/> is among them (the handler writes its last after the
        /// socket closes, so a test waits for it).</summary>
        public async Task<List<TermGatewayAuditRecord>> WaitFor(string what)
        {
            var until = DateTime.UtcNow.AddSeconds(10);
            while (!records.Any(r => r.Event == what))
            {
                if (DateTime.UtcNow > until) throw new TimeoutException($"no {what} record");
                await Task.Delay(20);
            }
            return [.. records];
        }
    }
}
