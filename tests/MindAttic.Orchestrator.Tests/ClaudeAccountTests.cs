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
        Assert.That(watching.SwitchedAtUtc(Now.AddHours(4)), Is.EqualTo(Now.AddHours(3)), "a rejected key and no login is signed out: the last account stays");
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

    // M.ORCH-T285: an account history, so going back to an account keeps what was its.

    [Test]
    public void Going_back_to_an_account_counts_its_earlier_span_again()
    {
        var watching = Account();
        SignIn("a", Now);
        watching.Read(Now);
        SignIn("b", Now.AddHours(1));
        var b = watching.Read(Now.AddHours(1))!;
        Assert.That((b.Account, b.Since, b.Switched, b.Contains(Now.AddMinutes(30)), b.Contains(Now.AddHours(1.5))), Is.EqualTo(("b", Now.AddHours(1), true, false, true)));

        SignIn("a", Now.AddHours(2));
        var a = Account().Read(Now.AddHours(2))!;
        Assert.That(a.Since, Is.EqualTo(Now.AddHours(2)));
        Assert.That((a.Contains(Now.AddMinutes(30)), a.Contains(Now.AddHours(1.5)), a.Contains(Now.AddHours(3))), Is.EqualTo((true, false, true)),
            "A's span before B counts again; B's doesn't");
    }

    [Test]
    public void Signing_out_keeps_the_last_account_and_back_in_to_it_is_no_switch()
    {
        var watching = Account();
        SignIn("a", Now);
        watching.Read(Now);
        Config("""{"numStartups":4}""", Now.AddHours(1));
        Assert.That(watching.Read(Now.AddHours(1))!.Account, Is.EqualTo("a"));
        SignIn("a", Now.AddHours(2));
        Assert.That(watching.Read(Now.AddHours(2))!.Since, Is.EqualTo(Now), "the same account: no switch");
        Config("""{"numStartups":5}""", Now.AddHours(3));
        watching.Read(Now.AddHours(3));
        SignIn("b", Now.AddHours(4));
        Assert.That(watching.Read(Now.AddHours(4))!.Since, Is.EqualTo(Now.AddHours(4)), "another account: a switch");
    }

    [Test]
    public void Spans_older_than_the_history_are_dropped()
    {
        var watching = Account();
        SignIn("a", Now);
        watching.Read(Now);
        SignIn("b", Now.AddHours(1));
        watching.Read(Now.AddHours(1));
        SignIn("c", Now.AddDays(10));
        var c = watching.Read(Now.AddDays(10))!;
        Assert.That(c.History.Select(s => s.Account), Is.EqualTo(new[] { "b", "c" }), "A's span closed ten days ago");
    }

    private static ReportedUsage Reading(DateTime five, DateTime? week, string? account = null) =>
        new(new ReportedWindow(99, five), week is { } w ? new ReportedWindow(99, w) : null, Now) { Account = account };

    [Test]
    public void After_a_switch_a_reading_counts_only_when_tied_to_the_current_account()
    {
        var watching = Account();
        SignIn("a", Now);
        var five = Now.AddHours(2);
        var week = Now.AddDays(3);
        Assert.That(watching.Read(Now)!.Owns(Reading(five, week)), Is.True, "never switched: every reading is the account's");
        watching.Confirm("a", Reading(five, week), Now);

        SignIn("b", Now.AddHours(1));
        var b = watching.Read(Now.AddHours(1))!;
        Assert.That(b.Owns(Reading(five, week)), Is.False, "A's windows, restamped by another CLI");
        Assert.That(b.Owns(Reading(Now.AddHours(3), Now.AddDays(5))), Is.False, "nothing is confirmed for B yet");
        Assert.That(b.Owns(Reading(Now.AddHours(3), Now.AddDays(5), "a")), Is.False, "the probe read A's meter");
        Assert.That(b.Owns(Reading(Now.AddHours(3), Now.AddDays(5), "b")), Is.True, "the probe read B's meter");
        Assert.That(b.HasWindow(Now.AddHours(1)), Is.False);

        watching.Confirm("b", Reading(Now.AddHours(3), Now.AddDays(5)), Now.AddHours(1));
        b = watching.Read(Now.AddHours(1))!;
        Assert.That(b.HasWindow(Now.AddHours(1)), Is.True);
        Assert.That(b.Owns(Reading(Now.AddHours(3).AddSeconds(20), Now.AddDays(5))), Is.True, "B's confirmed windows, a statusline's rounding apart");
        Assert.That(b.Owns(Reading(Now.AddHours(8), Now.AddDays(5))), Is.True, "B's next 5-hour window, known by B's week");
        Assert.That(b.Owns(Reading(Now.AddHours(8), week)), Is.False, "A's week");
    }

    [Test]
    public void An_api_key_is_its_own_account()
    {
        var watching = Account();
        SignIn("a", Now);
        watching.Read(Now);
        Config("""{"primaryApiKey":"sk-ant-x"}""", Now.AddHours(1));
        Assert.That(watching.Read(Now.AddHours(1)) is { IsApiKey: true, Switched: true }, Is.True);
        SignIn("a", Now.AddHours(2));
        Assert.That(watching.Read(Now.AddHours(2))!.Contains(Now.AddMinutes(10)), Is.True, "back from the key: A's span before it counts");
    }

    [Test]
    public void A_record_from_the_version_before_keeps_its_date_and_gains_a_history()
    {
        var state = Path.Combine(dir, "session-context", ClaudeAccount.FileName);
        Directory.CreateDirectory(Path.GetDirectoryName(state)!);
        File.WriteAllText(state, $$"""{"account":"a/max","since":{{(Now.AddDays(-1) - DateTime.UnixEpoch).TotalSeconds}}}""");
        Config("""{"oauthAccount":{"accountUuid":"a","organizationUuid":"max"}}""", Now);
        Assert.That(Account().Read(Now)!.Since, Is.EqualTo(Now.AddDays(-1)));

        Config("""{"oauthAccount":{"accountUuid":"b","organizationUuid":"max"}}""", Now.AddHours(1));
        Account().Read(Now.AddHours(1));
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(state))!;
        Assert.That(((string)json["account"]!, json["spans"]!.AsArray().Count), Is.EqualTo(("b/max", 2)), "the current account stays readable by the hooks before");
        Config("""{"oauthAccount":{"accountUuid":"a","organizationUuid":"max"}}""", Now.AddHours(2));
        Assert.That(Account().Read(Now.AddHours(2))!.Contains(Now.AddHours(-12)), Is.True, "A's first span kept its date");
    }
}
