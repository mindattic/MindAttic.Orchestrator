using MindAttic.Orchestrator.Billing;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Wpf.ViewModels;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>Status at a glance on the person tab strip (M.ORCH-T630): each person's tab has a status light (green usable, red
/// signed out, purple any other state) with its word, and its week left on its far right; an 'API key' tab
/// with an orange light holds the API key's switch, what it spent today and which CLIs bill it.</summary>
public sealed class TabStatusTests
{
    private static readonly DateTime Now = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void The_week_left_is_what_remains_before_the_stop_line_and_a_dash_when_never_read()
    {
        Assert.That(MainViewModel.TabWeekLeftOf(72), Is.EqualTo("28% left"));
        Assert.That(MainViewModel.TabWeekLeftOf(4), Is.EqualTo("96% left"), "the card's 100 - used, no reserve taken off");
        Assert.That(MainViewModel.TabWeekLeftOf(94), Is.EqualTo("6% left"));
        Assert.That(MainViewModel.TabWeekLeftOf(104), Is.EqualTo("0% left"), "never below 0");
        Assert.That(MainViewModel.TabWeekLeftOf(null), Is.EqualTo("—"));
    }

    [Test]
    public void The_light_is_green_usable_red_signed_out_and_purple_for_every_other_state_each_with_a_word()
    {
        Assert.That(MainViewModel.StatusOf(true, false, null, 50, 95, 40), Is.EqualTo((TabLight.Good, "Signed in and usable")));
        Assert.That(MainViewModel.StatusOf(false, false, null, null, 95, null), Is.EqualTo((TabLight.Bad, "Signed out")));
        Assert.That(MainViewModel.StatusOf(false, true, null, null, 95, null), Is.EqualTo((TabLight.Other, "Signing in")));
        Assert.That(MainViewModel.StatusOf(true, false, "b@x.com", 50, 95, 40).Light, Is.EqualTo(TabLight.Other));
        Assert.That(MainViewModel.StatusOf(true, false, "b@x.com", 50, 95, 40).Word, Does.Contain("b@x.com"));
        Assert.That(MainViewModel.StatusOf(true, false, null, 95, 95, 40), Is.EqualTo((TabLight.Other, "At its weekly stop line (95%)")));
        Assert.That(MainViewModel.StatusOf(true, false, null, 50, 95, 100), Is.EqualTo((TabLight.Other, "5-hour limit hit")));
        Assert.That(MainViewModel.StatusOf(true, false, null, null, 95, null).Light, Is.EqualTo(TabLight.Good), "never read: nothing says it's out of room");
    }

    [Test]
    public void The_lights_are_the_themes_tokens()
    {
        Assert.That(MainViewModel.LightBrush(TabLight.Good), Is.SameAs(Palette.Working));
        Assert.That(MainViewModel.LightBrush(TabLight.Bad), Is.SameAs(Palette.Over));
        Assert.That(MainViewModel.LightBrush(TabLight.Warning), Is.SameAs(Palette.Waiting));
        Assert.That(MainViewModel.LightBrush(TabLight.Other), Is.SameAs(Palette.Other));
        // The same light as the library's MaStatus, for the tab's MaTabItem (M.ORCH-T667).
        Assert.That(MainViewModel.StatusOf(TabLight.Good), Is.EqualTo(MindAttic.Components.Wpf.Controls.MaStatus.Good));
        Assert.That(MainViewModel.StatusOf(TabLight.Bad), Is.EqualTo(MindAttic.Components.Wpf.Controls.MaStatus.Bad));
        Assert.That(MainViewModel.StatusOf(TabLight.Warning), Is.EqualTo(MindAttic.Components.Wpf.Controls.MaStatus.Warning));
        Assert.That(MainViewModel.StatusOf(TabLight.Other), Is.EqualTo(MindAttic.Components.Wpf.Controls.MaStatus.Other));
        Assert.That(Palette.Other, Is.Not.Null);
    }

    [Test]
    public void A_per_CLI_login_signed_in_as_another_account_is_a_mismatch()
    {
        IReadOnlyList<AccountRow> rows = [new() { Account = "a/org", Email = "a@x.com" }, new() { Account = "b/org", Email = "b@x.com" }];
        Assert.That(MainViewModel.MismatchOf("a@x.com", "b/org", rows), Is.EqualTo("b@x.com"));
        Assert.That(MainViewModel.MismatchOf("A@X.com", "a/org", rows), Is.Null, "its own account, any case");
        Assert.That(MainViewModel.MismatchOf(null, "b/org", rows), Is.Null, "the global login is whoever's signed in to it");
        Assert.That(MainViewModel.MismatchOf("a@x.com", "c/org", rows), Is.Null, "an account whose email isn't known yet");
        Assert.That(MainViewModel.MismatchOf("a@x.com", null, rows), Is.Null);
    }

    [Test]
    public void The_API_key_tab_counts_the_tokens_and_names_the_CLIs_billing_the_key()
    {
        var repo = new RepoStatus("A", @"D:\W\A", RepoState.Working, [],
            [new TranscriptSession("k1", @"D:\W\A", Now, new TokenUsage(100, 0, 50, 10), null),
             new TranscriptSession("t1", @"D:\W\A", Now, new TokenUsage(7, 0, 0, 7), null)], [], Now, default);
        var all = new Snapshot(Now, [repo], []);
        var status = new BillingStatus(BillingModes.Api, Now, "API KEY: sk-ant-...WXYZ",
            [new CliBilling(11, "k1", @"D:\W\A", "api-key", null), new CliBilling(12, "t1", @"D:\W\A", ContextCycler.SubscriptionAuth, null)]);

        var used = MainViewModel.ApiKeyUsageOf(status, all);
        Assert.That(used.Fresh, Is.EqualTo(110));
        Assert.That(used.Total, Is.EqualTo(160));
        Assert.That(MainViewModel.ApiKeyUsageOf(null, all), Is.EqualTo(default(TokenUsage)));
        Assert.That(MainViewModel.ApiKeyClisOf(status), Does.StartWith("1 CLI bills the API key: ").And.Contain("pid 11").And.Not.Contain("pid 12"));
        Assert.That(MainViewModel.ApiKeyClisOf(null), Is.EqualTo("No CLI bills the API key."));
    }

    [Test]
    public void The_API_key_tab_is_no_logins_and_keyed_apart_from_every_login()
    {
        Assert.That(LoginTab.ApiKey.IsApiKey, Is.True);
        Assert.That(LoginTab.ApiKey.IsGlobal, Is.False);
        Assert.That(LoginTab.ApiKey.Key, Is.EqualTo(LoginTab.ApiKeyKey).And.Not.Contains("@").And.Not.Empty);
        Assert.That(new LoginTab(null, "Ryan (fallback)", "", "").Key, Is.Empty);
    }

    [Test]
    public void The_strip_shows_the_light_and_week_left_and_the_API_key_switch_lives_on_its_tab()
    {
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        var xaml = File.ReadAllText(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));

        var strip = xaml[xaml.IndexOf("x:Name=\"LoginTabStrip\"", StringComparison.Ordinal)..xaml.IndexOf("x:Name=\"PageTabStrip\"", StringComparison.Ordinal)];
        // The library's MaTabItem draws the light before the name and the trailing text after it (M.ORCH-T667).
        Assert.That(strip, Does.Contain("<mc:MaTabControl").And.Contain("TargetType=\"mc:MaTabItem\""));
        Assert.That(strip, Does.Contain("Property=\"Status\" Value=\"{Binding TabLightStatus}\"")
            .And.Contain("Property=\"StatusLabel\" Value=\"{Binding TabStatusWord}\"")
            .And.Contain("Property=\"Trailing\" Value=\"{Binding TabTrailing}\"")
            .And.Contain("Property=\"Accent\" Value=\"{Binding TabBrush}\"")
            .And.Contain("Property=\"AutomationProperties.Name\" Value=\"{Binding TabTip}\"")
            .And.Contain("{Binding TabTitle}"));

        var api = xaml[xaml.IndexOf("x:Name=\"ApiKeyBody\"", StringComparison.Ordinal)..];
        api = api[..api.IndexOf("</ScrollViewer>", StringComparison.Ordinal)];
        Assert.That(api, Does.Contain("{Binding IsApiKeyTab").And.Contain("x:Name=\"BillingToggleButton\"").And.Contain("{Binding ToggleBillingCommand}")
            .And.Contain("{Binding ApiKeyCostText}").And.Contain("{Binding ApiKeyClis}"));
        Assert.That(System.Text.RegularExpressions.Regex.Matches(xaml, "x:Name=\"BillingToggleButton\"").Count, Is.EqualTo(1), "one switch, on the 'API key' tab");
    }
}
