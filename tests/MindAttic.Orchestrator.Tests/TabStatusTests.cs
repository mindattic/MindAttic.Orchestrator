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
    public void The_tooltip_counts_down_to_the_5_hour_and_weekly_resets()
    {
        Assert.That(MainViewModel.Countdown(new TimeSpan(2, 3, 14, 59)), Is.EqualTo("2d 3hr 14m"));
        Assert.That(MainViewModel.Countdown(new TimeSpan(0, 3, 14, 0)), Is.EqualTo("3hr 14m"));
        Assert.That(MainViewModel.Countdown(TimeSpan.FromMinutes(14)), Is.EqualTo("14m"));
        Assert.That(MainViewModel.Countdown(new TimeSpan(1, 0, 5, 0)), Is.EqualTo("1d 0hr 5m"));

        var line = MainViewModel.ResetsLineOf(Now.AddHours(3).AddMinutes(14), Now.AddDays(2).AddHours(3).AddMinutes(14), Now);
        Assert.That(line, Does.StartWith(" 5-hour resets "));
        Assert.That(line, Does.Contain("(in 3hr 14m); week resets "));
        Assert.That(line, Does.EndWith("(in 2d 3hr 14m)."));
        Assert.That(MainViewModel.ResetsLineOf(null, Now.AddMinutes(14), Now), Does.StartWith(" Week resets ").And.EndWith("(in 14m)."));
        Assert.That(MainViewModel.ResetsLineOf(null, Now.AddMinutes(-1), Now), Is.Empty, "nothing known or ahead: nothing said");
    }

    [Test]
    public void Hovering_a_persons_tab_leads_with_its_current_sigma_lambda_then_cutoffs_weekly_reset_and_active_clis()
    {
        // M.ORCH-T1120, bulleted and given the open CLIs M.ORCH-T1424, led by the current Σ/λ percents M.ORCH-T1536 (now
        // that the tab face itself shows bars rather than that text): no name, status or email; just the usage line, the
        // cutoffs, when the week resets, and the CLI tabs open on this login, each of the latter three its own bullet
        // line. The Launcher's own menu pane is never one of them (M.ORCH-T1430: MainViewModel.ActiveCliTitles filters it
        // out before it reaches here).
        var tip = MainViewModel.TabHoverTipOf("Σ: 15% | λ: 29%", " Cutoffs: 5-hour 98%, weekly 95%.", Now.AddHours(7).AddMinutes(3), Now, ["repo-foo"]);
        Assert.That(tip, Is.Not.Null);
        var lines = tip!.Split('\n');
        Assert.That(lines[0], Is.EqualTo("Σ: 15% | λ: 29%"));
        Assert.That(lines[1], Is.EqualTo("• Cutoffs: 5-hour 98%, weekly 95%"));
        Assert.That(lines[2], Does.StartWith("• Weekly reset: ").And.EndWith("(in 7hr 3m)"));
        Assert.That(lines[3], Is.EqualTo("• Active CLIs: repo-foo"));

        Assert.That(MainViewModel.TabHoverTipOf("Σ: 15% | λ: 29%", " Cutoffs: 5-hour 98%, weekly 95%.", null, Now, []),
            Is.EqualTo("Σ: 15% | λ: 29%\n• Cutoffs: 5-hour 98%, weekly 95%\n• Weekly reset: not known yet\n• Active CLIs: none open"));
        Assert.That(MainViewModel.TabHoverTipOf("", " Cutoffs: 5-hour 98%, weekly 95%.", null, Now, []),
            Is.EqualTo("• Cutoffs: 5-hour 98%, weekly 95%\n• Weekly reset: not known yet\n• Active CLIs: none open"), "no usage line (the API key tab): no lead line, same as before");
        Assert.That(MainViewModel.TabHoverTipOf("Σ: 15% | λ: 29%", "", Now.AddHours(1), Now, []), Is.Null, "no cutoffs (signed out, the API key): the full tip instead");
    }

    [Test]
    public void The_tab_shows_live_5_hour_and_weekly_use_against_the_persons_own_cutoffs()
    {
        // M.ORCH-T793: current/target pairs, the target the person's own cutoff as passed in, never a fixed number.
        // M.ORCH-T1267: λ always shows, 0% with no 5-hour block started yet, so an idle login never looks unread.
        Assert.That(MainViewModel.TabUsagePairsOf(10, 55, null, true), Is.EqualTo("Σ: 55% | λ: 0%"));
        Assert.That(MainViewModel.TabUsagePairsOf(42.6, 3.2, null, true), Is.EqualTo("Σ: 3% | λ: 0%"));
        Assert.That(MainViewModel.TabUsagePairsOf(null, null, null, true), Is.EqualTo("Σ: —% | λ: 0%"), "never read");

        // M.ORCH-T1207: the 5-hour figure joins it, as λ, whenever it's known.
        Assert.That(MainViewModel.TabUsagePairsOf(10, 55, 89, true), Is.EqualTo("Σ: 55% | λ: 89%"));
        Assert.That(MainViewModel.TabUsagePairsOf(10, 55, 120, true), Is.EqualTo("Σ: 55% | λ: 120%"), "uncapped, as the others read past their own cutoff");

        // M.ORCH-T1255: the SymbolTabLabels setting off switches to words, same figures and the same rules.
        Assert.That(MainViewModel.TabUsagePairsOf(10, 55, null, false), Is.EqualTo("wk: 55% | 5h: 0%"));
        Assert.That(MainViewModel.TabUsagePairsOf(10, 55, 89, false), Is.EqualTo("wk: 55% | 5h: 89%"));
    }

    [Test]
    public void The_global_tab_applies_first_whatever_order_the_strip_shows_it_in()
    {
        // M.ORCH-T1273: ApplyTabs sits the global login's tab among the others by name (M.ORCH-T752), but it alone rereads
        // the settings store (symbolTabLabels among them) -- every other tab's trailing text either copies what it read or
        // reads it live through Root. Applying a tab before the global one's turn would show it the settings as they stood
        // before the refresh, with nothing left to notice it changed until some unrelated later refresh happened to run:
        // after a Settings Save toggled SymbolTabLabels, a tab sorted ahead of the global one's name would keep showing the
        // old "Σ/λ" or "wk/5h" labels. Root always applies first, regardless of where it sits for display.
        var root = "The global login";
        var alice = "Alice"; // sorts before "The global login" by name
        var zack = "Zack"; // sorts after it

        Assert.That(MainViewModel.ApplyOrderOf(new[] { alice, root, zack }, root), Is.EqualTo(new[] { root, alice, zack }));
        Assert.That(MainViewModel.ApplyOrderOf(new[] { root, alice, zack }, root), Is.EqualTo(new[] { root, alice, zack }));
        Assert.That(MainViewModel.ApplyOrderOf(new[] { alice, zack, root }, root), Is.EqualTo(new[] { root, alice, zack }));
    }

    [Test]
    public void A_stale_reading_is_marked_stale_on_the_tab_and_one_needing_a_new_sign_in_says_so()
    {
        // M.ORCH-T1119: Ryan's tab showed "Σ: 0%" from an 11-hour-old reading as if current.
        Assert.That(MainViewModel.TabUsageOf(10, 55, Now.AddMinutes(-3), false, Now, null, true), Is.EqualTo("Σ: 55% | λ: 0%"), "fresh");
        Assert.That(MainViewModel.TabUsageOf(10, 55, null, false, Now, null, true), Is.EqualTo("Σ: 55% | λ: 0%"), "never read: nothing to call stale");
        Assert.That(MainViewModel.TabUsageOf(0, 91, Now.AddHours(-11), false, Now, null, true), Is.EqualTo("stale · read 11h ago"), "its old figures never shown as current");
        Assert.That(MainViewModel.TabUsageOf(0, 91, Now - UsageReport.StaleAfter - TimeSpan.FromMinutes(1), false, Now, null, true), Does.StartWith("stale"),
            "stale as org usage calls it");
        Assert.That(MainViewModel.TabUsageOf(0, 91, Now.AddHours(-11), true, Now, null, true), Is.EqualTo("sign in again"));
        Assert.That(MainViewModel.TabStaleLineOf(0, 91, Now.AddHours(-11), Now, null, true),
            Is.EqualTo(" Claude's meter was last read 11h ago (Σ: 91% | λ: 0%), so these figures may be out of date."), "the old figures, in the tooltip");
        Assert.That(MainViewModel.TabStaleLineOf(0, 91, Now.AddMinutes(-3), Now, null, true), Is.Empty);
        Assert.That(MainViewModel.TabUsageOf(10, 55, Now.AddMinutes(-3), false, Now, null, false), Is.EqualTo("wk: 55% | 5h: 0%"), "M.ORCH-T1255: word labels");

        var (light, word) = MainViewModel.StatusOf(true, false, null, 50, 95, 40, 98, ClaudeMeterProbe.SignInAgain("ryan@x.com"));
        Assert.That(light, Is.EqualTo(TabLight.Other));
        Assert.That(word, Is.EqualTo("Claude's meter can't be read: sign in again (orchestrator login --account ryan@x.com)"));
    }

    [Test]
    public void The_tab_line_has_no_need_you_count_and_no_percent_left()
    {
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        var xaml = File.ReadAllText(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        var strip = xaml[xaml.IndexOf("x:Name=\"LoginTabStrip\"", StringComparison.Ordinal)..xaml.IndexOf("x:Name=\"Header\"", StringComparison.Ordinal)];
        Assert.That(strip, Does.Not.Contain("need you\" />"));
        Assert.That(strip, Does.Not.Contain("TabBadge"));
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
    public void The_API_key_is_a_person_on_its_own_login_signed_in_while_the_vault_has_a_key()
    {
        // M.ORCH-T906: no longer a special billing tab, but a person whose login is its own folder.
        var tab = LoginTab.ApiKey(hasKey: true);
        Assert.That(tab.IsApiKey, Is.True);
        Assert.That(tab.IsGlobal, Is.False);
        Assert.That(tab.Email, Is.EqualTo(Billing.ApiKeyPerson.Email));
        Assert.That(tab.Key, Is.EqualTo(LoginTab.ApiKeyKey).And.EqualTo(Billing.ApiKeyPerson.Email));
        Assert.That(tab.SignedIn, Is.True);
        Assert.That(LoginTab.ApiKey(hasKey: false).SignedIn, Is.False, "no key in the Vault: nothing to sign it in with");
        Assert.That(new LoginTab(null, "Ryan (fallback)", "", "").Key, Is.Empty);
    }

    [Test]
    public void There_is_no_miscellaneous_tab_any_more_its_content_shows_on_the_global_tab_instead()
    {
        // M.ORCH-T825: the 'Miscellaneous' tab (M.ORCH-T763) is gone; what fit on no person's page now shows on the global
        // login's tab (ShowsWorkspaceExtras, MainViewModel.Tabs.cs), and the API key tab's icon is still a key.
        Assert.That(typeof(LoginTab).GetProperty("IsMisc"), Is.Null);
        Assert.That(typeof(LoginTab).GetField("MiscKey"), Is.Null);
        Assert.That(typeof(LoginTab).GetField("Misc"), Is.Null);

        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        var xaml = File.ReadAllText(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        // M.ORCH-T906: the API key's person shows the activity icon like everyone; no key icon of its own.
        Assert.That(xaml, Does.Not.Contain("x:Name=\"TabKeyIcon\"")
            .And.Not.Contain("x:Name=\"TabMiscIcon\"").And.Not.Contain("x:Name=\"MiscBody\""));
        Assert.That(xaml, Does.Contain("x:Name=\"WorkspaceExtras\"").And.Contain("{Binding ShowsWorkspaceExtras,"));
        var tabs = File.ReadAllText(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "ViewModels", "MainViewModel.Tabs.cs"));
        Assert.That(tabs, Does.Not.Contain("LoginTab.Misc"));
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
        // The library's MaTabItem draws the trailing text after the name (M.ORCH-T667); the status icon (M.ORCH-T757), boxed into
        // status badges (M.ORCH-T1173), replaced its status light: a gray "zzz" box idle (or every open CLI stopped), a green
        // box with a spinning glyph busy (M.ORCH-T1270, in place of the M.ORCH-T1261 CLI-count digit), a plain static "?" when a
        // CLI is blocked on the person (M.ORCH-T1270, in place of the M.ORCH-T1261 count/? flash), and a plain gray glyph
        // (unboxed) disabled.
        Assert.That(strip, Does.Contain("<mc:MaTabControl").And.Contain("TargetType=\"mc:MaTabItem\""));
        Assert.That(strip, Does.Not.Contain("Property=\"Status\" Value="), "the icon, not the status light");
        Assert.That(strip, Does.Contain("x:Name=\"TabIdleBadge\"").And.Contain("Content=\"zzz\"").And.Contain("Background=\"{DynamicResource MaMuted}\"")
            .And.Contain("{Binding ShowsIdleBadge").And.Contain("x:Name=\"TabBusyBadge\"").And.Contain("<mc:MaSpinner")
            .And.Contain("Background=\"{DynamicResource MaGood}\"").And.Contain("{Binding ShowsBusyBadge")
            .And.Contain("x:Name=\"TabAskingBadge\"").And.Contain("Content=\"?\"").And.Contain("Background=\"{DynamicResource MaWarn}\"")
            .And.Contain("{Binding TabNeedsYou,").And.Contain("x:Name=\"TabDisabledIcon\" Text=\"{Binding TabDisabledGlyph}\"").And.Contain("Foreground=\"{DynamicResource MaMuted}\"")
            .And.Contain("{Binding IsTabDisabled").And.Contain("FontFamily=\"Segoe Fluent Icons, Segoe MDL2 Assets\""));
        Assert.That(strip, Does.Not.Contain("TabAskingBadgeContent"), "a plain static ? now (M.ORCH-T1270), not a flash");
        Assert.That(strip, Does.Contain("x:Name=\"TabStatusIcon\" Width=\"20\" Height=\"20\""), "20x20 (M.ORCH-T1272)");
        // M.ORCH-T1173: "every open CLI stopped" is folded into the idle badge (ShowsIdleBadge covers IsTabAllStopped too), so
        // there's no separate pause/stopped icon any more. M.ORCH-T890: the busy badge is a steady green play (no blink).
        Assert.That(strip, Does.Not.Contain("TabStoppedIcon"), "folded into the idle badge now (M.ORCH-T1173)");
        Assert.That(strip, Does.Not.Contain("TabBusyIcon_Loaded"), "the busy badge no longer blinks (M.ORCH-T890)");
        Assert.That(strip, Does.Not.Contain("Opacity=\"{Binding TabIconOpacity}\""), "replaced by the soothing pulse (M.ORCH-T770)");
        Assert.That(strip, Does.Not.Contain("<Storyboard"), "the pulse is driven from code-behind now (M.ORCH-T775), not a Storyboard element");
        Assert.That(strip, Does.Not.Contain("(UIElement.Effect)"), "self-targeting an Effect's sub-property throws at runtime; name it and target it directly");
        // M.ORCH-T1536: the plain percent figures move to the tooltip; the tab face shows bars instead (TrailingContent),
        // right-aligned the same way Trailing itself is - Trailing now only carries the text a bar can't say.
        Assert.That(strip, Does.Contain("Property=\"Trailing\" Value=\"{Binding TabTrailingFace}\"")
            .And.Contain("Property=\"TrailingContent\" Value=\"{Binding}\"")
            .And.Contain("Property=\"TrailingContentTemplate\" Value=\"{StaticResource LoginUsageBarsTemplate}\"")
            .And.Contain("Property=\"Accent\" Value=\"{Binding TabBrush}\"")
            .And.Contain("Property=\"AutomationProperties.Name\" Value=\"{Binding TabTip}\"")
            .And.Contain("{Binding TabTitle}"));

        // M.ORCH-T906: no 'API key' billing body or switch; the API key's person page shows money in place of the meters.
        Assert.That(xaml, Does.Not.Contain("x:Name=\"ApiKeyBody\"").And.Not.Contain("x:Name=\"BillingToggleButton\""));
        Assert.That(xaml, Does.Contain("x:Name=\"ApiLeftTile\"").And.Contain("x:Name=\"ApiBurnChart\"")
            .And.Contain("{Binding ShowsClaudeMeters, Converter"));
    }

    /// <summary>M.ORCH-T803: every person's tab is the same width. Since M.ORCH-T1431 the person tabs are a vertical sidebar
    /// (TabStripPlacement Left), whose TabPanel stretches every tab to the column, so the code-behind sizing is gone.</summary>
    [Test]
    public void Every_person_tab_is_as_wide_as_the_sidebar()
    {
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        var xaml = File.ReadAllText(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        // SidebarTools is gone (M.ORCH-T1499 moved the sidebar's window-tools row into the menu bar entirely): the next
        // landmark after the tab strip's own markup is the ticket area below it.
        var strip = xaml[xaml.IndexOf("x:Name=\"LoginTabStrip\"", StringComparison.Ordinal)..xaml.IndexOf("x:Name=\"SidebarTicketArea\"", StringComparison.Ordinal)];
        Assert.That(strip, Does.Contain("TabStripPlacement=\"Left\"").And.Contain("x:Name=\"LoginTabItem\"").And.Not.Contain("LoginTabItem_Loaded"));

        var code = File.ReadAllText(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml.cs"));
        Assert.That(code, Does.Not.Contain("SizePersonTabs").And.Not.Contain("LoginTabItem_Loaded"));
        // M.ORCH-T1250: the CLI tabs keep their own equal-width sizing.
        Assert.That(code, Does.Contain("private const double TabSidePadding").And.Contain("tabs.Max(NaturalWidth) + 2 * TabSidePadding"));
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

    /// <summary>M.ORCH-T1393: a CLI counts as working for <see cref="MainViewModel.AnyAgentWorkingOf"/> while its own turn runs
    /// or the pace gate holds it mid-turn, or while it has a subagent running or an /allocate claim open (the same "not
    /// stopped" signal <see cref="MainViewModel.AllStoppedOf"/> and the Terminals panel's own pane badge already use) --
    /// never from merely being asked a question (that's "needs you", not "working").</summary>
    [Test]
    public void Any_agent_working_reads_the_live_agent_board_not_a_transcripts_age()
    {
        AgentBoardRow Row(AgentState state, int subagents = 0, int claims = 0, bool asking = false) =>
            new("a", "A", null, null, DateTime.UtcNow, DateTime.UtcNow, state, "", false, default,
                [.. Enumerable.Range(0, subagents).Select(i => new SubagentActivity($"s{i}", "a", "general", DateTime.UtcNow, DateTime.UtcNow, default))])
            { Claims = [.. Enumerable.Range(0, claims).Select(i => $"T{i}")], Asking = asking };

        Assert.That(MainViewModel.AnyAgentWorkingOf(null), Is.False, "no agent board at all");
        Assert.That(MainViewModel.AnyAgentWorkingOf([]), Is.False, "no CLI open");
        Assert.That(MainViewModel.AnyAgentWorkingOf([Row(AgentState.Waiting)]), Is.False, "genuinely idle at a prompt");
        Assert.That(MainViewModel.AnyAgentWorkingOf([Row(AgentState.Waiting, asking: true)]), Is.False, "asking isn't working, it's needing you");
        Assert.That(MainViewModel.AnyAgentWorkingOf([Row(AgentState.Working)]), Is.True, "its own turn is running");
        Assert.That(MainViewModel.AnyAgentWorkingOf([Row(AgentState.HeldByPace)]), Is.True, "working, just held back by the pace gate");
        Assert.That(MainViewModel.AnyAgentWorkingOf([Row(AgentState.Waiting, subagents: 1)]), Is.True, "a subagent is running under it");
        Assert.That(MainViewModel.AnyAgentWorkingOf([Row(AgentState.Waiting, claims: 1)]), Is.True, "it holds an /allocate claim");
        Assert.That(MainViewModel.AnyAgentWorkingOf([Row(AgentState.Waiting), Row(AgentState.Working)]), Is.True, "any one of several CLIs busy is enough");
    }

    /// <summary>
    /// M.ORCH-T1393: "Every User tab is supposed to have a spinner when a CLI tab belonging to that user is operating, but it
    /// doesn't -- most stay gray 'zzz'." Root cause: <c>TabActivity</c>'s "working" input came only from
    /// <see cref="RepoState.Working"/> (a transcript-age window) and <see cref="MainViewModel.HostCount"/> (open Launcher
    /// tabs); a headless <c>/allocate</c> agent has no host pipe to count and can go quiet past the working window mid-tool-call
    /// while still genuinely running, so most of a busy login's tabs read idle. The fix blends in
    /// <see cref="MainViewModel.AnyAgentWorkingOf"/>, the same live per-session board the stopped/idle badge
    /// (<see cref="MainViewModel.AllStoppedOf"/>) and the needs-you flag (<see cref="MainViewModel.AskingOf"/>) already trust.
    /// This test composes those three exactly as <c>MainViewModel.TabStatus.cs</c>/<c>MainViewModel.Tabs.cs</c> do, to pin the
    /// four contracted outcomes without standing up a full view model.
    /// </summary>
    [Test]
    public void A_tabs_busy_idle_and_needs_you_badges_follow_the_live_agent_board()
    {
        AgentBoardRow Row(string id, AgentState state, HostSession? host = null, bool asking = false) =>
            new(id, "A", null, null, DateTime.UtcNow, DateTime.UtcNow, state, "", false, default, []) { Host = host, Asking = asking };

        // What MainViewModel.TabStatus.cs/MainViewModel.Tabs.cs compose from the raw signals (M.ORCH-T1393's blend).
        static (bool Busy, bool Idle, bool NeedsYou) BadgesOf(IReadOnlyList<AgentBoardRow> agents, IReadOnlyList<HostSession> hosts,
            bool workingCountPositive, bool hostCountPositive)
        {
            var activity = MainViewModel.ActivityOf(usable: true, pastCutoff: false,
                working: workingCountPositive || hostCountPositive || MainViewModel.AnyAgentWorkingOf(agents), spending: false);
            var allStopped = MainViewModel.AllStoppedOf(agents, hosts) > 0;
            var needsYou = MainViewModel.AskingOf(agents) > 0;
            var isBusy = activity == TabActivity.Busy && !allStopped;
            var isIdle = (activity == TabActivity.Idle && !allStopped) || allStopped;
            return (isBusy && !needsYou, isIdle && !needsYou, needsYou);
        }

        // A headless /allocate agent mid-turn: no Launcher host, and its transcript write is stale enough that the repo-level
        // age window (WorkingCount) already reads Dormant/Waiting -- this is the bug's exact shape.
        var headlessBusy = BadgesOf([Row("h1", AgentState.Working)], [], workingCountPositive: false, hostCountPositive: false);
        Assert.That(headlessBusy, Is.EqualTo((true, false, false)), "a live-busy headless CLI shows busy though its transcript looks old and it has no host");

        // A hosted CLI genuinely idle at a prompt, whose transcript write still falls inside the working window (a stale
        // WorkingCount=true) and whose Launcher tab is open (HostCount=true): the live board says it's truly stopped, so the
        // stale/host signals must not win the tug-of-war into a false busy badge.
        var host = new HostSession("Claude", 1, "p1", @"D:\W\A");
        var staleIdle = BadgesOf([Row("h2", AgentState.Waiting, host)], [host], workingCountPositive: true, hostCountPositive: true);
        Assert.That(staleIdle, Is.EqualTo((false, true, false)), "a recent transcript write alone never fakes a busy badge over a genuinely idle live CLI");

        // Every open CLI on the login has genuinely stopped.
        var allStopped = BadgesOf([Row("h3", AgentState.Waiting, host), Row("h4", AgentState.Waiting)], [host], workingCountPositive: false, hostCountPositive: true);
        Assert.That(allStopped, Is.EqualTo((false, true, false)), "every open CLI idle: the stopped/idle badge, not busy");

        // A CLI blocked on the person: needs-you wins over busy or idle either way.
        var asking = BadgesOf([Row("h5", AgentState.Working, asking: true)], [], workingCountPositive: false, hostCountPositive: false);
        Assert.That(asking, Is.EqualTo((false, false, true)), "asking shows needs-you regardless of how busy-looking the rest of the signal is");
    }

    /// <summary>M.ORCH-T1270: the login tab's busy badge is a spinner and its asking badge a plain "?", not a CLI-count digit
    /// or a count/? flash (M.ORCH-T1261's design); the count itself still exists for the tooltip (<see cref="MainViewModel.TabCliCount"/>).</summary>
    [Test]
    public void The_busy_and_asking_badges_no_longer_show_a_CLI_count()
    {
        AgentBoardRow Row(string id) => new(id, "A", null, null, DateTime.UtcNow, DateTime.UtcNow, AgentState.Waiting, "Waiting for you", false, default, []);

        Assert.That(MainViewModel.CliCountOf(null), Is.Zero, "no CLI open");
        Assert.That(MainViewModel.CliCountOf([Row("a")]), Is.EqualTo(1));
        Assert.That(MainViewModel.CliCountOf([Row("a"), Row("b")]), Is.EqualTo(2));
    }

    /// <summary>M.ORCH-T1264: a login with no configured 5-hour budget (peak-relative, the common case) still shows its real
    /// λ figure from Claude's own report, the same block percent the 5-hour tile's fill bar already trusts unconditionally
    /// (<c>BudgetFill</c>); only with no block running at all does it fall back to the roster's last reading.</summary>
    [Test]
    public void The_5_hour_figure_comes_from_a_peak_relative_block_too_not_only_a_real_limit()
    {
        var now = DateTime.UtcNow;
        var block = new UsageBlock(now.AddHours(-2), now.AddHours(3), default, now);
        var peaked = new BudgetStatus(block, 0, true, 42, default, default, null, null, 0, null, 90);
        var real = new BudgetStatus(block, 10_000_000, false, 17, default, default, null, null, 0, null, 90);
        var idle = new BudgetStatus(null, 0, true, 0, default, default, null, null, 0, null, 90);

        Assert.That(MainViewModel.SnapshotFiveHourOf(peaked, rosterFiveHourUsed: 5), Is.EqualTo(42),
            "Claude's own reported percent, even with no configured limit");
        Assert.That(MainViewModel.SnapshotFiveHourOf(real, rosterFiveHourUsed: 5), Is.EqualTo(17), "a real limit still wins over the roster");
        Assert.That(MainViewModel.SnapshotFiveHourOf(idle, rosterFiveHourUsed: 5), Is.EqualTo(5), "no block running: the roster's last reading");
        Assert.That(MainViewModel.SnapshotFiveHourOf(null, rosterFiveHourUsed: 5), Is.EqualTo(5), "no budget at all: the roster's last reading");
        Assert.That(MainViewModel.SnapshotFiveHourOf(null, rosterFiveHourUsed: null), Is.Null, "never read anywhere");
    }

    /// <summary>M.ORCH-T1528: "you're working on something in the background and you know it, so the CLI tab should be green and
    /// spinning, and therefore the User tab should be green and spinning." A CLI idle at its prompt with a background shell
    /// running counts as working for the person tab, and keeps the tab from reading "all stopped".</summary>
    [Test]
    public void A_cli_with_a_background_job_running_keeps_its_person_tab_busy()
    {
        AgentBoardRow Row(string id, int background) =>
            new(id, "A", null, null, DateTime.UtcNow, DateTime.UtcNow, AgentState.Waiting, "", false, default, []) { BackgroundJobs = background };

        Assert.That(MainViewModel.AnyAgentWorkingOf([Row("a", 0)]), Is.False, "idle at a prompt with nothing running");
        Assert.That(MainViewModel.AnyAgentWorkingOf([Row("a", 1)]), Is.True, "a background job is running");
        Assert.That(MainViewModel.AllStoppedOf([Row("a", 0), Row("b", 0)], []), Is.EqualTo(2));
        Assert.That(MainViewModel.AllStoppedOf([Row("a", 0), Row("b", 1)], []), Is.Zero, "one CLI's background job means not all stopped");
    }
}
