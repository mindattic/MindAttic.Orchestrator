using System.Text.Json.Nodes;
using MindAttic.Orchestrator.Billing;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Wpf.ViewModels;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The Vault's API key as a person (M.ORCH-T906): its own login folder marked to bill the key, a pane environment of
/// its own, and money (a starting credit, spend priced per model, a burndown) in place of Claude's 5-hour and weekly windows.</summary>
public sealed class ApiKeyPersonTests
{
    private static readonly DateTime Now = new(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);
    private string dir = "";

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "api-key-person-" + Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
    }

    [Test]
    public void Its_email_names_a_login_folder_no_claude_ai_account_has()
    {
        Assert.That(AccountDirs.Normalize(ApiKeyPerson.Email), Is.EqualTo(ApiKeyPerson.Email));
        Assert.That(ApiKeyPerson.Is(" API-Key@MindAttic.Vault "), Is.True);
        Assert.That(ApiKeyPerson.Is("ryan@example.com"), Is.False);
        Assert.That(ApiKeyPerson.Is(null), Is.False);
        Assert.That(PaneRequest.LoginOf(Path.Combine(dir, ApiKeyPerson.Email)), Is.EqualTo(ApiKeyPerson.Email));
    }

    [Test]
    public void Signing_it_in_never_opens_a_claude_ai_sign_in_it_is_made_ready_from_the_vault_key()
    {
        var was = OwnLogins.PrepareApiKey;
        var opened = false;
        try
        {
            var dirs = new AccountDirs(dir);
            OwnLogins.PrepareApiKey = _ => null;
            var ready = OwnLogins.SignIn(dirs, ApiKeyPerson.Email, sso: false, opening: (_, _) => opened = true);
            Assert.That(ready.Ok, Is.True);
            Assert.That(ready.Outcome, Is.EqualTo(OwnLoginOutcome.AlreadySignedIn));

            OwnLogins.PrepareApiKey = _ => "There's no Claude key in the MindAttic Vault keyring.";
            var noKey = OwnLogins.SignIn(dirs, ApiKeyPerson.Email, sso: false, opening: (_, _) => opened = true);
            Assert.That(noKey.Ok, Is.False);
            Assert.That(noKey.Message, Does.Contain("Vault"));
            Assert.That(opened, Is.False, "no browser sign-in, ever, for the API key (M.ORCH-T1101)");
        }
        finally { OwnLogins.PrepareApiKey = was; }
    }

    [Test]
    public void Its_look_marks_the_folder_to_bill_the_key_and_keeps_a_name_given_since()
    {
        File.WriteAllText(Path.Combine(dir, LoginLook.FileName), """{"alias":"Credits","other":1}""");
        ApiKeyPerson.WriteLook(dir);

        var look = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, LoginLook.FileName)))!.AsObject();
        Assert.That((string?)look["billing"], Is.EqualTo(BillingModes.Api));
        Assert.That((string?)look["email"], Is.EqualTo(ApiKeyPerson.Email));
        Assert.That((string?)look["alias"], Is.EqualTo("Credits"), "a rename is kept");
        Assert.That((int?)look["other"], Is.EqualTo(1));
        Assert.That(LoginLook.For(dir, ApiKeyPerson.Email).Initials, Is.EqualTo(ApiKeyPerson.Initials));
    }

    [Test]
    public void Its_config_is_marked_past_onboarding_and_never_holds_the_key()
    {
        var config = Path.Combine(dir, ".claude.json");
        ApiKeyPerson.EnsureConfig(config);
        Assert.That(JsonNode.Parse(File.ReadAllText(config))!["hasCompletedOnboarding"]!.GetValue<bool>(), Is.True);
        Assert.That(File.ReadAllText(config), Does.Not.Contain("sk-ant"));
    }

    [Test]
    public void Its_panes_run_on_its_folder_without_a_seats_token_and_never_carry_the_key()
    {
        var folder = Path.Combine(dir, ApiKeyPerson.Email);
        var pane = new PaneRequest("API: Launcher", dir) { Command = ["launcher.exe", "--account", ApiKeyPerson.Email], Email = ApiKeyPerson.Email, ConfigDir = folder };
        var setup = pane.ApiKeySetup();
        Assert.That(setup, Does.Contain($"$env:{AccountDirs.EnvVar} = '{folder}'").And.Contain($"Remove-Item Env:{LoginTokens.EnvVar}")
            .And.Contain($"$env:{PaneRequest.BillingVariable} = 'api'").And.Not.Contain("ANTHROPIC_API_KEY ="));
        Assert.That(new PaneRequest("RD: Repo [Claude]", dir) { Email = "ryan@example.com" }.ApiKeySetup(), Is.Empty, "every other pane is as it was");
    }

    [Test]
    public void Spend_is_priced_per_model_and_an_unknown_one_as_opus()
    {
        var usage = new TokenUsage(1_000_000, 0, 0, 1_000_000);
        Assert.That(ApiPricing.Cost("claude-opus-5-5", usage), Is.EqualTo(24.0).Within(1e-9));
        Assert.That(ApiPricing.Cost("claude-sonnet-5-5", usage), Is.EqualTo(12.0).Within(1e-9));
        Assert.That(ApiPricing.Cost(null, usage), Is.EqualTo(24.0).Within(1e-9));
        Assert.That(ApiPricing.Cost("claude-opus-5-5", new TokenUsage(0, 1_000_000, 1_000_000, 0)), Is.EqualTo(5.2).Within(1e-9));
    }

    [Test]
    public void The_ledger_keeps_each_hours_most_and_burns_down_from_the_credit()
    {
        var path = Path.Combine(dir, ApiSpendLedger.FileName);
        var ledger = new ApiSpendLedger(path);
        var since = Now.AddHours(-3);
        TranscriptSession Session(params (DateTime At, long Output)[] events) =>
            new("s1", dir, Now, default, "claude-sonnet-5-5") { Events = [.. events.Select(e => new UsageEvent(e.At, new TokenUsage(0, 0, 0, e.Output)))] };

        Assert.That(ledger.Observe([Session((Now.AddHours(-2), 100_000), (Now.AddMinutes(-30), 100_000))], Now), Is.True);
        Assert.That(ledger.Save(), Is.True);
        // A later read that sees less of an hour (its transcript aged out) never lowers what was recorded.
        var again = new ApiSpendLedger(path);
        Assert.That(again.Observe([Session((Now.AddMinutes(-30), 50_000))], Now), Is.False);

        var spend = again.Spend(10, since, Now, Now.Date);
        Assert.That(spend.SpentUsd, Is.EqualTo(2.0).Within(1e-9), "200K output tokens of Sonnet at $10/M");
        Assert.That(spend.LeftUsd, Is.EqualTo(8.0).Within(1e-9));
        Assert.That(spend.TodayUsd, Is.EqualTo(2.0).Within(1e-9));
        Assert.That(spend.PerHourUsd, Is.EqualTo(2.0 / 24).Within(1e-9));
        Assert.That(spend.Burn[0], Is.EqualTo(new ApiSpendPoint(since, 0)));
        Assert.That(spend.Burn[^1].SpentUsd, Is.EqualTo(2.0).Within(1e-9));
        Assert.That(spend.RunsOutUtc(Now), Is.EqualTo(Now.AddHours(8.0 / (2.0 / 24))).Within(TimeSpan.FromSeconds(1)));
        Assert.That(spend.OutOfCredit, Is.False);
        Assert.That(again.Spend(1.5, since, Now, Now.Date).OutOfCredit, Is.True);
        Assert.That(again.Spend(null, null, Now, Now.Date).LeftUsd, Is.Null, "no credit set: nothing to burn down");
    }

    [Test]
    public void Its_tab_reads_money_and_its_light_follows_the_key_and_the_credit()
    {
        var spend = new ApiSpend(50, Now.AddDays(-1), 12.34, 1.2, 0.5, []);
        Assert.That(MainViewModel.ApiTrailingOf(spend), Is.EqualTo("$37.66 left"));
        Assert.That(MainViewModel.ApiTrailingOf(spend with { CreditUsd = null }), Is.EqualTo("$1.20 today"));
        Assert.That(MainViewModel.ApiTrailingOf(null), Is.Empty);
        Assert.That(MainViewModel.ApiStatusOf(false, spend).Light, Is.EqualTo(TabLight.Bad));
        Assert.That(MainViewModel.ApiStatusOf(true, spend).Light, Is.EqualTo(TabLight.Good));
        Assert.That(MainViewModel.ApiStatusOf(true, spend with { SpentUsd = 60 }).Light, Is.EqualTo(TabLight.Other));
        Assert.That(MainViewModel.ApiBurnTextOf(spend, Now), Does.Contain("$37.66 left of $50.00").And.Contain("runs out"));
    }
}
