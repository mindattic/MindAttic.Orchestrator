using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Term;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The gateway's grants and audit in orchestrator.db (M.ORCH-T934): term_grants written, replaced, listed and revoked,
/// the snapshot the gateway decides from, and term_audit imported from the spool once per record.</summary>
public sealed class TermGrantsTests
{
    private static readonly DateTime Now = new(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);
    private const string Owner = "ryand";
    private string dir = "";
    private OrchestratorDb db = null!;

    [SetUp]
    public void SetUp()
    {
        dir = Directory.CreateTempSubdirectory("term-grants-").FullName;
        db = new OrchestratorDb(Path.Combine(dir, "orchestrator.db"));
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(dir, true); } catch (IOException) { }
    }

    [Test]
    public void A_grant_is_kept_replaced_for_the_same_scope_and_listed_until_it_expires()
    {
        TermGrants.Grant(db, Owner, "Erin@Example.com", "watch", null, null, "ryand", Now);
        TermGrants.Grant(db, Owner, "erin@example.com", "interact", null, Now.AddHours(2), "ryand", Now);
        TermGrants.Grant(db, Owner, "erin@example.com", "interact", "s1", Now.AddMinutes(30), "ryand", Now);

        var live = TermGrants.List(db, Now);
        Assert.That(live.Select(g => (g.Viewer, g.Session, g.Level)), Is.EqualTo(new[] { ("erin@example.com", "", "interact"), ("erin@example.com", "s1", "interact") }),
            "the person-wide grant was replaced, not added");
        Assert.That(live[0].ExpiresUtc, Is.EqualTo(Now.AddHours(2)));
        Assert.That(TermGrants.List(db, Now.AddHours(1)).Select(g => g.Session), Is.EqualTo(new[] { "" }), "the session grant lapsed");
        Assert.That(TermGrants.List(db, Now.AddHours(1), includeExpired: true), Has.Count.EqualTo(2));
    }

    [Test]
    public void An_interact_grant_needs_an_expiry_in_the_future_and_within_the_limit()
    {
        Assert.Throws<ArgumentException>(() => TermGrants.Grant(db, Owner, "erin@example.com", "interact", null, null, null, Now));
        Assert.Throws<ArgumentException>(() => TermGrants.Grant(db, Owner, "erin@example.com", "watch", null, Now.AddMinutes(-1), null, Now));
        Assert.Throws<ArgumentException>(() => TermGrants.Grant(db, Owner, "erin@example.com", "watch", null, Now.AddDays(60), null, Now));
        Assert.Throws<ArgumentException>(() => TermGrants.Grant(db, Owner, "erin@example.com", "admin", null, Now.AddHours(1), null, Now));
        Assert.That(TermGrants.List(db, Now), Is.Empty);
    }

    [Test]
    public void Revoke_takes_one_sessions_grant_or_every_one()
    {
        TermGrants.Grant(db, Owner, "erin@example.com", "watch", null, null, null, Now);
        TermGrants.Grant(db, Owner, "erin@example.com", "interact", "s1", Now.AddHours(1), null, Now);
        TermGrants.Grant(db, Owner, "sam@example.com", "watch", null, null, null, Now);

        Assert.That(TermGrants.Revoke(db, Owner, "erin@example.com", "s1"), Is.EqualTo(1));
        Assert.That(TermGrants.Revoke(db, Owner, "erin@example.com", "s1"), Is.Zero);
        TermGrants.Grant(db, Owner, "erin@example.com", "interact", "s2", Now.AddHours(1), null, Now);
        Assert.That(TermGrants.Revoke(db, Owner, "ERIN@example.com", null), Is.EqualTo(2));
        Assert.That(TermGrants.List(db, Now).Select(g => g.Viewer), Is.EqualTo(new[] { "sam@example.com" }));
    }

    [Test]
    public void The_snapshot_holds_active_members_their_orgs_and_live_grants_only()
    {
        MemberRoster.Add(db, "erin@example.com", "Erin", "member", "standard", "active", Now);
        MemberRoster.Add(db, "invited@example.com", null, "member", "standard", "pending", Now);
        db.SaveOrg(new Organization("sales", "Sales", 1));
        db.SetMember(new OrgMember("sales", "Erin", "erin@example.com"));
        TermGrants.Grant(db, Owner, "erin@example.com", "interact", null, Now.AddHours(1), null, Now);
        TermGrants.Grant(db, Owner, "erin@example.com", "interact", "old", Now.AddMinutes(5), null, Now);

        var snapshot = TermGrants.Snapshot(db, Now.AddMinutes(10));
        Assert.That(snapshot.Members.Select(m => m.Email), Is.EqualTo(new[] { "erin@example.com" }), "a pending member isn't one yet");
        Assert.That(snapshot.Logins.Single(), Is.EqualTo(new TermAclLogin("erin@example.com", "sales", "Erin")));
        Assert.That(snapshot.Grants.Select(g => g.Session), Is.EqualTo(new[] { "" }), "the lapsed grant is left out");

        var gatewayDir = Path.Combine(dir, "gateway");
        Assert.That(TermGrants.Publish(db, Now.AddMinutes(10), gatewayDir), Is.True);
        Assert.That(TermGatewayAudit.ReadAcl(gatewayDir)!.Grants.Single().Level, Is.EqualTo("interact"));
        Assert.That(TermGrants.Publish(db, Now.AddMinutes(10), gatewayDir), Is.False, "nothing changed");
    }

    [Test]
    public void Audit_records_import_once_each_and_read_back_by_time_without_content()
    {
        var gatewayDir = Path.Combine(dir, "gateway");
        var attach = new TermGatewayAuditRecord { AtUtc = Now, Event = TermGatewayAudit.Attach, Viewer = "erin@example.com", Owner = Owner, Session = "s1", Level = "interact", Remote = "100.64.0.2" };
        var input = attach with { Id = Guid.NewGuid().ToString("N"), AtUtc = Now.AddSeconds(10), Event = TermGatewayAudit.Input, Frames = 3, Bytes = 42 };
        var detach = attach with { Id = Guid.NewGuid().ToString("N"), AtUtc = Now.AddMinutes(1), Event = TermGatewayAudit.Detach, Frames = 5, Bytes = 9000, Detail = "ended by viewer" };
        foreach (var r in new[] { attach, input, detach }) TermGatewayAudit.Append(r, gatewayDir);
        TermGatewayAudit.Append(attach, gatewayDir); // the same record spooled twice (an import that died before clearing it)

        Assert.That(TermGrants.ImportAudit(db, gatewayDir), Is.EqualTo(3));
        Assert.That(TermGrants.ImportAudit(db, gatewayDir), Is.Zero, "the spool was cleared");

        var all = TermGrants.Audit(db, null);
        Assert.That(all.Select(r => r.Event), Is.EqualTo(new[] { "attach", "input", "detach" }), "oldest first");
        Assert.That((all[1].Frames, all[1].Bytes, all[1].Viewer, all[1].Session, all[1].Level), Is.EqualTo((3L, 42L, "erin@example.com", "s1", "interact")));
        Assert.That(all[0].AtUtc, Is.EqualTo(Now));
        Assert.That(TermGrants.Audit(db, Now.AddSeconds(30)).Select(r => r.Event), Is.EqualTo(new[] { "detach" }));
        Assert.That(TermGrants.Audit(db, null, limit: 1).Single().Event, Is.EqualTo("detach"), "the newest");
    }

    [Test]
    public void Durations_read_as_for_and_since_take_them()
    {
        Assert.That(TermGrants.ParseDuration("2h"), Is.EqualTo(TimeSpan.FromHours(2)));
        Assert.That(TermGrants.ParseDuration("1h30m"), Is.EqualTo(TimeSpan.FromMinutes(90)));
        Assert.That(TermGrants.ParseDuration("7d"), Is.EqualTo(TimeSpan.FromDays(7)));
        Assert.That(TermGrants.ParseDuration("45"), Is.EqualTo(TimeSpan.FromMinutes(45)));
        Assert.That(TermGrants.ParseDuration("0"), Is.Null);
        Assert.That(TermGrants.ParseDuration("2x"), Is.Null);
        Assert.That(TermGrants.ParseDuration("h"), Is.Null);
        Assert.That(TermGrants.ParseDuration("99999999999999w"), Is.Null);
    }
}
