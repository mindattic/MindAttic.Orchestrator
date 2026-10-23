using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>Members' info synced from claude.ai (M.ORCH-T701): read from a login's profile at sign-in and fetched periodically
/// after, always written over the members table's name, role and status; logins that aren't members are left alone.</summary>
public sealed class MemberProfileSyncTests
{
    private static readonly DateTime Now = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
    private readonly TempDirs temps = new();

    [TearDown]
    public void TearDown() => temps.DeleteAll();

    private OrchestratorDb Scratch() => new(Path.Combine(temps.New(), "data", "orchestrator.db"));

    [Test]
    public void Sync_overwrites_the_members_name_role_and_status_and_leaves_non_members_alone()
    {
        var db = Scratch();
        MemberRoster.Add(db, "honey@example.com", "Typed By Hand", "member", "standard", "pending", Now);

        var later = Now.AddHours(1);
        Assert.That(MemberRoster.Sync(db, new ClaudeProfile("Honey@Example.com", "Honey", "admin"), later), Is.True);
        var synced = MemberRoster.Find(db, "honey@example.com")!;
        Assert.That((synced.Name, synced.Role, synced.Tier, synced.Status, synced.UpdatedUtc), Is.EqualTo(("Honey", "admin", "standard", "active", later)));
        Assert.That(File.ReadAllText(db.DumpPath), Does.Contain("'honey@example.com', 'Honey', 'admin', 'standard', 'active'"));

        Assert.That(MemberRoster.Sync(db, new ClaudeProfile("honey@example.com", "Honey", "admin"), later.AddHours(1)), Is.False, "nothing changed: not written");
        Assert.That(MemberRoster.Sync(db, new ClaudeProfile("honey@example.com", null, null), later), Is.False, "what claude.ai didn't give is kept");
        Assert.That(MemberRoster.Sync(db, new ClaudeProfile("stranger@example.com", "Stranger", "owner"), later), Is.False);
        Assert.That(MemberRoster.Find(db, "stranger@example.com"), Is.Null, "not added: members are entered by hand");
    }

    [Test]
    public void Claude_roles_map_to_the_members_roles()
    {
        Assert.That(MemberProfileSync.RoleOf("primary_owner"), Is.EqualTo("primary-owner"));
        Assert.That(MemberProfileSync.RoleOf("owner"), Is.EqualTo("owner"));
        Assert.That(MemberProfileSync.RoleOf("membership_admin"), Is.EqualTo("admin"));
        Assert.That(MemberProfileSync.RoleOf("user"), Is.EqualTo("member"));
        Assert.That(MemberProfileSync.RoleOf("something_new"), Is.Null);
        Assert.That(MemberProfileSync.RoleOf(null), Is.Null);
    }

    [Test]
    public void The_profile_is_read_from_a_logins_config_and_from_claudes_profile_endpoint()
    {
        var config = Path.Combine(temps.New(), ".claude.json");
        File.WriteAllText(config, """{"oauthAccount":{"accountUuid":"a","emailAddress":"Amy@Example.com","displayName":"Amy","fullName":"Amy Full","organizationRole":"owner"}}""");
        Assert.That(MemberProfileSync.Read(config), Is.EqualTo(new ClaudeProfile("amy@example.com", "Amy", "owner")));
        File.WriteAllText(config, """{"primaryApiKey":"sk"}""");
        Assert.That(MemberProfileSync.Read(config), Is.Null, "an API key has no profile");

        var profile = """{"account":{"uuid":"a","email":"jordan@example.com","full_name":"Jordan Full","display_name":"Jordan"},"organization":{"uuid":"o"}}""";
        Assert.That(MemberProfileSync.Parse(profile, """{"organization_role":"primary_owner"}"""), Is.EqualTo(new ClaudeProfile("jordan@example.com", "Jordan", "primary-owner")));
        Assert.That(MemberProfileSync.Parse(profile, null)!.Role, Is.Null);
        Assert.That(MemberProfileSync.Parse("""{"account":{"full_name":"No Email"}}""", null), Is.Null);
        Assert.That(MemberProfileSync.Parse("not json", null), Is.Null);
    }

    [Test]
    public async Task Tick_syncs_at_sign_in_and_from_a_periodic_fetch()
    {
        var db = Scratch();
        MemberRoster.Add(db, "amy@example.com", "Old Name", "member", "premium", null, Now);
        var dir = temps.New();
        var config = Path.Combine(dir, ".claude.json");
        var credentials = Path.Combine(dir, ".credentials.json");
        File.WriteAllText(config, """{"oauthAccount":{"accountUuid":"a","emailAddress":"amy@example.com","displayName":"Amy","organizationRole":"owner"}}""");
        File.WriteAllText(credentials, $$$"""{"claudeAiOauth":{"accessToken":"tok","expiresAt":{{{DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds()}}}}}""");
        var asked = new List<string>();
        var sync = new MemberProfileSync
        {
            Fetch = (endpoint, token, _) =>
            {
                asked.Add(endpoint);
                Assert.That(token, Is.EqualTo("tok"));
                return Task.FromResult<string?>(endpoint == MemberProfileSync.ProfileEndpoint
                    ? """{"account":{"email":"amy@example.com","display_name":"Amy From Claude"}}"""
                    : """{"organization_role":"membership_admin"}""");
            },
        };

        sync.Tick(db, [(config, credentials)], Now);
        Assert.That(MemberRoster.Find(db, "amy@example.com")!.Name, Is.EqualTo("Amy"), "the sign-in's profile, at once");
        await sync.Pending;
        Assert.That(asked, Is.EqualTo(new[] { MemberProfileSync.ProfileEndpoint, MemberProfileSync.RolesEndpoint }));

        sync.Tick(db, [(config, credentials)], Now.AddSeconds(5));
        var fetched = MemberRoster.Find(db, "amy@example.com")!;
        Assert.That((fetched.Name, fetched.Role), Is.EqualTo(("Amy From Claude", "admin")), "claude.ai's own profile, fetched");
        await sync.Pending;
        Assert.That(asked, Has.Count.EqualTo(2), "not fetched again until FetchEvery");

        sync.Tick(db, [(config, credentials)], Now + MemberProfileSync.FetchEvery);
        await sync.Pending;
        Assert.That(asked, Has.Count.EqualTo(4), "fetched again periodically");
    }
}
