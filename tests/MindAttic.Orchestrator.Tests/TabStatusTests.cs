using MindAttic.Orchestrator.Activity;
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
    public void The_miscellaneous_tab_is_no_logins_shows_under_every_org_and_the_api_key_tab_shows_a_key()
    {
        // M.ORCH-T763: after the API key tab, for what fits on no person's page; the API key tab's icon is a key.
        Assert.That(LoginTab.Misc.IsMisc, Is.True);
        Assert.That(LoginTab.Misc.IsGlobal, Is.False);
        Assert.That(LoginTab.Misc.Key, Is.EqualTo(LoginTab.MiscKey).And.Not.EqualTo(LoginTab.ApiKeyKey).And.Not.Contains("@"));
        Assert.That(OrgDirectory.Single.InOrg("any-org", LoginTab.Misc), Is.True);
        Assert.That(OrgDirectory.Single.InOrg("any-org", LoginTab.ApiKey), Is.False);

        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        var xaml = File.ReadAllText(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        Assert.That(xaml, Does.Contain("x:Name=\"TabKeyIcon\" Text=\"&#xE192;\"").And.Contain("{Binding IsApiKeyTab, Converter")
            .And.Contain("x:Name=\"TabMiscIcon\"").And.Contain("x:Name=\"MiscBody\""));
        var tabs = File.ReadAllText(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "ViewModels", "MainViewModel.Tabs.cs"));
        Assert.That(tabs, Does.Contain("LoginTab.ApiKeyTab(all.Billing?.Mode == BillingModes.Api), LoginTab.Misc]"), "the API key tab, then Miscellaneous, last");
    }

    [Test]
    public void A_person_past_its_own_5_hour_cutoff_says_so()
    {
        Assert.That(MainViewModel.StatusOf(true, false, null, 50, 90, 98, 98), Is.EqualTo((TabLight.Other, "At its 5-hour cutoff (98%)")));
        Assert.That(MainViewModel.StatusOf(true, false, null, 50, 90, 97, 98).Light, Is.EqualTo(TabLight.Good));
    }

    [Test]
    public void The_icon_is_disabled_past_a_cutoff_or_unusable_busy_while_working_or_spending_else_idle()
    {
        Assert.That(MainViewModel.ActivityOf(usable: true, pastCutoff: false, working: false, spending: false), Is.EqualTo(TabActivity.Idle));
        Assert.That(MainViewModel.ActivityOf(true, false, working: true, spending: false), Is.EqualTo(TabActivity.Busy));
        Assert.That(MainViewModel.ActivityOf(true, false, false, spending: true), Is.EqualTo(TabActivity.Busy));
        Assert.That(MainViewModel.ActivityOf(true, pastCutoff: true, true, true), Is.EqualTo(TabActivity.Disabled));
        Assert.That(MainViewModel.ActivityOf(usable: false, false, true, true), Is.EqualTo(TabActivity.Disabled), "signed out, or API billing off");

        Assert.That(MainViewModel.PastCutoffOf(90, 90, 10, 98), Is.True, "at its weekly cutoff");
        Assert.That(MainViewModel.PastCutoffOf(50, 90, 98, 98), Is.True, "at its 5-hour cutoff");
        Assert.That(MainViewModel.PastCutoffOf(89, 90, 97, 98), Is.False);
        Assert.That(MainViewModel.PastCutoffOf(null, 90, null, 98), Is.False, "never read is not past");
    }

    [Test]
    public void A_tab_blinks_once_per_N_tokens_spread_over_the_refresh_at_its_own_phase()
    {
        var b = new TabBlinker("a@x.com");
        Assert.That(b.Count(5_000, 1000, Now), Is.Zero, "the first count only sets where counting starts");
        Assert.That(b.Grew, Is.False);
        Assert.That(b.Count(5_999, 1000, Now), Is.Zero, "no multiple of N crossed");
        Assert.That(b.Grew, Is.True, "but it spent");
        Assert.That(b.Count(8_100, 1000, Now), Is.EqualTo(3), "6000, 7000 and 8000 crossed");
        Assert.That(b.Pending, Is.EqualTo(3));

        // Three blinks over the next refresh: each dims the icon for a moment, then the next one a third of the spread later.
        var blinks = 0;
        for (var t = Now; t < Now + TabBlinker.Spread + TabBlinker.Spread; t += TimeSpan.FromMilliseconds(10))
            if (b.Tick(t) && b.IsDim) blinks++;
        Assert.That(blinks, Is.EqualTo(3));
        Assert.That(b.IsActive, Is.False);

        Assert.That(b.Count(100, 1000, Now), Is.Zero, "a new day starts the count again");
        Assert.That(b.Count(1_000_000, 1000, Now), Is.EqualTo(1000), "every multiple counted");
        Assert.That(b.Pending, Is.EqualTo(TabBlinker.MaxQueued), "but no more queued than fit in a refresh");

        Assert.That(TabBlinker.PhaseOf("a@x.com"), Is.Not.EqualTo(TabBlinker.PhaseOf("b@x.com")), "tabs at one rate don't blink in unison");
        Assert.That(TabBlinker.PhaseOf("a@x.com"), Is.EqualTo(TabBlinker.PhaseOf("a@x.com")).And.InRange(0, 1));
        b.Clear();
        Assert.That(b.IsActive, Is.False);
    }

    [Test]
    public void The_strip_shows_the_light_and_week_left_and_the_API_key_switch_lives_on_its_tab()
    {
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        var xaml = File.ReadAllText(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));

        var strip = xaml[xaml.IndexOf("x:Name=\"LoginTabStrip\"", StringComparison.Ordinal)..xaml.IndexOf("x:Name=\"Header\"", StringComparison.Ordinal)];
        // The library's MaTabItem draws the trailing text after the name (M.ORCH-T667); the status icon (M.ORCH-T757) replaced its
        // status light: pause idle, a blinking green play arrow busy, gray disabled, each in the library's icon font and a theme token.
        Assert.That(strip, Does.Contain("<mc:MaTabControl").And.Contain("TargetType=\"mc:MaTabItem\""));
        Assert.That(strip, Does.Not.Contain("Property=\"Status\" Value="), "the icon, not the status light");
        Assert.That(strip, Does.Contain("x:Name=\"TabIdleIcon\" Text=\"&#xE769;\"").And.Contain("{Binding IsTabIdle")
            .And.Contain("x:Name=\"TabBusyIcon\" Text=\"&#xE768;\"").And.Contain("Foreground=\"{DynamicResource MaGood}\"")
            .And.Contain("{Binding IsTabBusy").And.Contain("x:Name=\"TabDisabledIcon\" Text=\"&#xE733;\"").And.Contain("Foreground=\"{DynamicResource MaMuted}\"")
            .And.Contain("{Binding IsTabDisabled").And.Contain("FontFamily=\"Segoe Fluent Icons, Segoe MDL2 Assets\""));
        // M.ORCH-T770: the busy icon glows and pulses instead of the old per-token TabIconOpacity blink, so it reads as
        // "action is taking place" the same way the emergency billing toggle does. M.ORCH-T775 moved the glow's DropShadowEffect
        // and its pulse into code-behind (MainWindow.xaml.cs's TabBusyIcon_Loaded/TabBusyIcon_IsVisibleChanged): a Storyboard
        // here would need a NameScope the library-only XAML rule leaves no library-styled place to put, and a bare
        // (UIElement.Effect).(DropShadowEffect.Opacity) compound path throws at runtime ("'Effect' property does not point to
        // a DependencyObject") rather than resolving to a freshly-set local value.
        Assert.That(strip, Does.Contain("x:Name=\"TabBusyIcon\"").And.Contain("Loaded=\"TabBusyIcon_Loaded\"").And.Contain("IsVisibleChanged=\"TabBusyIcon_IsVisibleChanged\""));
        Assert.That(strip, Does.Not.Contain("Opacity=\"{Binding TabIconOpacity}\""), "replaced by the soothing pulse (M.ORCH-T770)");
        Assert.That(strip, Does.Not.Contain("<Storyboard"), "the pulse is driven from code-behind now (M.ORCH-T775), not a Storyboard element");
        Assert.That(strip, Does.Not.Contain("(UIElement.Effect)"), "self-targeting an Effect's sub-property throws at runtime; name it and target it directly");
        Assert.That(strip, Does.Contain("Property=\"Trailing\" Value=\"{Binding TabTrailing}\"")
            .And.Contain("Property=\"Accent\" Value=\"{Binding TabBrush}\"")
            .And.Contain("Property=\"AutomationProperties.Name\" Value=\"{Binding TabTip}\"")
            .And.Contain("{Binding TabTitle}"));

        var api = xaml[xaml.IndexOf("x:Name=\"ApiKeyBody\"", StringComparison.Ordinal)..];
        api = api[..api.IndexOf("</ScrollViewer>", StringComparison.Ordinal)];
        Assert.That(api, Does.Contain("{Binding IsApiKeyTab").And.Contain("x:Name=\"BillingToggleButton\"").And.Contain("{Binding ToggleBillingCommand}")
            .And.Contain("{Binding ApiKeyCostText}").And.Contain("{Binding ApiKeyClis}"));
        Assert.That(System.Text.RegularExpressions.Regex.Matches(xaml, "x:Name=\"BillingToggleButton\"").Count, Is.EqualTo(1), "one switch, on the 'API key' tab");
    }

    /// <summary>M.ORCH-T790: the tab's red flag counts only CLIs blocked on you, and says so in words.</summary>
    [Test]
    public void The_needs_you_flag_counts_only_clis_blocked_on_you()
    {
        AgentBoardRow Row(string id, bool asking) => new(id, "A", null, null, DateTime.UtcNow, DateTime.UtcNow, AgentState.Waiting, "Waiting for you",
            false, default, []) { Asking = asking };

        Assert.That(MainViewModel.AskingOf([Row("a", false), Row("b", false)]), Is.Zero, "idle isn't blocked");
        Assert.That(MainViewModel.AskingOf([Row("a", true), Row("b", false)]), Is.EqualTo(1));
        Assert.That(MainViewModel.AskingOf(null), Is.Zero);
        Assert.That(MainViewModel.NeedsYouLine(0), Is.Empty);
        Assert.That(MainViewModel.NeedsYouLine(1), Is.EqualTo(" A CLI here is blocked waiting on your answer."));
        Assert.That(MainViewModel.NeedsYouLine(2), Is.EqualTo(" 2 CLIs here are blocked waiting on your answer."));
    }
}
