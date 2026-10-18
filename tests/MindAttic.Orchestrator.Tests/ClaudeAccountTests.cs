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
}
