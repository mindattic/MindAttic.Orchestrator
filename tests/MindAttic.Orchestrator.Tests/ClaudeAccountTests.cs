using MindAttic.Orchestrator.Budget;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>A /login to another account leaves the old account's usage behind, dated by the first sight of the switch.</summary>
public sealed class ClaudeAccountTests
{
    private static readonly DateTime Now = new(2026, 10, 18, 1, 0, 0, DateTimeKind.Utc);

    private string dir = null!;

    [SetUp]
    public void SetUp() => Directory.CreateDirectory(dir = Path.Combine(Path.GetTempPath(), "claude-account-" + Guid.NewGuid().ToString("N")));

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(dir, true); } catch (IOException) { }
    }

    private ClaudeAccount Account() => new() { ConfigPath = Path.Combine(dir, ".claude.json"), StatePath = Path.Combine(dir, "session-context", ClaudeAccount.FileName) };

    private void SignIn(string id, DateTime stamp)
    {
        var path = Path.Combine(dir, ".claude.json");
        File.WriteAllText(path, $$$"""{"numStartups":3,"oauthAccount":{"accountUuid":"{{{id}}}","emailAddress":"x@y"}}""");
        File.SetLastWriteTimeUtc(path, stamp);
    }

    [Test]
    public void Without_a_signed_in_account_nothing_is_left_out() =>
        Assert.That(Account().SwitchedAtUtc(Now), Is.Null);

    [Test]
    public void A_switch_is_dated_when_first_seen_and_kept_across_processes()
    {
        SignIn("a", Now);
        Assert.That(Account().SwitchedAtUtc(Now), Is.EqualTo(Now));
        Assert.That(Account().SwitchedAtUtc(Now.AddHours(1)), Is.EqualTo(Now), "the same account is no switch");

        var watching = Account();
        Assert.That(watching.SwitchedAtUtc(Now.AddHours(2)), Is.EqualTo(Now));
        SignIn("b", Now.AddHours(3));
        Assert.That(watching.SwitchedAtUtc(Now.AddHours(3)), Is.EqualTo(Now.AddHours(3)));
        Assert.That(Account().SwitchedAtUtc(Now.AddHours(4)), Is.EqualTo(Now.AddHours(3)));
    }

    private void Config(string json, DateTime stamp)
    {
        var path = Path.Combine(dir, ".claude.json");
        File.WriteAllText(path, json);
        File.SetLastWriteTimeUtc(path, stamp);
    }

    [Test]
    public void Moving_to_an_api_key_and_back_is_a_switch_each_way()
    {
        var watching = Account();
        SignIn("a", Now);
        Assert.That(watching.SwitchedAtUtc(Now), Is.EqualTo(Now));

        // The Claude login is gone and an approved ANTHROPIC_API_KEY is used instead: that used to read as signed out.
        Config("""{"customApiKeyResponses":{"approved":["sk-ant-tail"],"rejected":[]}}""", Now.AddHours(1));
        Assert.That(watching.SwitchedAtUtc(Now.AddHours(1)), Is.EqualTo(Now.AddHours(1)));
        Config("""{"primaryApiKey":"sk-ant-x"}""", Now.AddHours(2));
        Assert.That(watching.SwitchedAtUtc(Now.AddHours(2)), Is.EqualTo(Now.AddHours(1)), "one API key to another is the same pool");

        SignIn("a", Now.AddHours(3));
        Assert.That(watching.SwitchedAtUtc(Now.AddHours(3)), Is.EqualTo(Now.AddHours(3)));

        Config("""{"customApiKeyResponses":{"approved":[],"rejected":["sk-ant-tail"]}}""", Now.AddHours(4));
        Assert.That(watching.SwitchedAtUtc(Now.AddHours(4)), Is.Null, "a rejected key and no login is signed out");
    }

    [Test]
    public void The_same_account_in_another_organization_is_a_switch()
    {
        var watching = Account();
        Config("""{"oauthAccount":{"accountUuid":"a","organizationUuid":"max"}}""", Now);
        Assert.That(watching.SwitchedAtUtc(Now), Is.EqualTo(Now));
        Config("""{"oauthAccount":{"accountUuid":"a","organizationUuid":"team"}}""", Now.AddHours(1));
        Assert.That(watching.SwitchedAtUtc(Now.AddHours(1)), Is.EqualTo(Now.AddHours(1)));
    }

    [Test]
    public void A_record_from_before_organizations_were_keyed_keeps_its_date()
    {
        var state = Path.Combine(dir, "session-context", ClaudeAccount.FileName);
        Directory.CreateDirectory(Path.GetDirectoryName(state)!);
        File.WriteAllText(state, $$"""{"account":"a","since":{{(Now.AddDays(-1) - DateTime.UnixEpoch).TotalSeconds}}}""");
        Config("""{"oauthAccount":{"accountUuid":"a","organizationUuid":"team"}}""", Now);
        Assert.That(Account().SwitchedAtUtc(Now), Is.EqualTo(Now.AddDays(-1)));
        Assert.That(File.ReadAllText(state), Does.Contain("a/team"));
    }
}
