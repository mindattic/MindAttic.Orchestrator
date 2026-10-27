using MindAttic.Orchestrator.Billing;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Wpf.ViewModels;
using NUnit.Framework;
using Spectre.Console;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The account menu and <c>orchestrator login</c> (M.ORCH-T492): the roster with the signed-in account marked and
/// the most headroom suggested, picking an account opens its login (<c>claude auth login --email</c>, never for real here),
/// and the API key is the menu's last entry, as the billing toggle was.</summary>
[NonParallelizable] // AccountSwitch.Login, the commands' overrides and AnsiConsole.Console are process-wide
public sealed class AccountSwitchTests
{
    private const string Key = "sk-ant-api03-0123456789abcdefghijWXYZ";
    private static readonly DateTime Now = DateTime.UtcNow;

    private readonly TempDirs temps = new();
    private string dir = null!;
    private OrchestratorDb db = null!;
    private ClaudeAccount account = null!;
    private BillingSwitch billing = null!;
    private Func<string, bool, int> realLogin = null!;
    private readonly List<(string Email, bool Sso)> logins = [];
    private int loginExit;
    private IAnsiConsole previousConsole = null!;
    private StringWriter output = null!;

    private string ConfigPath => Path.Combine(dir, ".claude.json");

    [SetUp]
    public void SetUp()
    {
        dir = temps.New();
        db = new OrchestratorDb(Path.Combine(dir, "repo", "data", "orchestrator.db"));
        account = new ClaudeAccount { ConfigPath = ConfigPath, StatePath = Path.Combine(dir, "session-context", ClaudeAccount.FileName) };
        Directory.CreateDirectory(Path.Combine(dir, "session-context"));
        billing = new BillingSwitch(Path.Combine(dir, "session-context"))
        {
            ConfigPath = ConfigPath,
            VaultKey = () => Key,
            IsAlive = _ => true,
            Send = (_, _) => Task.FromResult(true),
        };
        realLogin = AccountSwitch.Login;
        logins.Clear();
        loginExit = 0;
        AccountSwitch.Login = (email, sso) => { logins.Add((email, sso)); return loginExit; };
        AccountsCommand.Override = () => (db, account, Path.Combine(dir, "no-usage.json"));
        LoginCommand.BillingOverride = () => (billing, []);
        previousConsole = AnsiConsole.Console;
        output = new StringWriter();
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(output),
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
        });
        AnsiConsole.Profile.Width = 240;

        // a@ has 70% of its week left on a premium seat; b@ (signed in now) 20% on a standard one.
        SignIn("a", "a@mindattic.com", "default_claude_max_5x", Now.AddHours(-2));
        Observe(Reading(10, 30, Now.AddHours(-2)), Now.AddHours(-2));
        SignIn("b", "b@mindattic.com", null, Now.AddHours(-1));
        Observe(Reading(40, 80, Now.AddHours(-1)), Now.AddHours(-1));
    }

    [TearDown]
    public void TearDown()
    {
        AccountSwitch.Login = realLogin;
        AccountsCommand.Override = null;
        LoginCommand.BillingOverride = null;
        AnsiConsole.Console = previousConsole;
        temps.DeleteAll();
    }

    private void SignIn(string id, string email, string? tier, DateTime stamp)
    {
        var t = tier is null ? "" : $",\"userRateLimitTier\":\"{tier}\"";
        File.WriteAllText(ConfigPath, $$$"""{"oauthAccount":{"accountUuid":"{{{id}}}","organizationUuid":"org","emailAddress":"{{{email}}}","organizationName":"MindAttic","organizationType":"claude_team"{{{t}}}}}""");
        File.SetLastWriteTimeUtc(ConfigPath, stamp);
    }

    private static ReportedUsage Reading(double five, double week, DateTime at) =>
        new(new ReportedWindow(five, Now.AddHours(3)), new ReportedWindow(week, Now.AddDays(3)), at);

    private void Observe(ReportedUsage reading, DateTime now) => AccountRoster.Observe(db, account.Read(now)!, account.Profile, reading, now);

    private IReadOnlyList<AccountRow> Rows => AccountRoster.Load(db, "b/org");

    private int Run(params string[] args) => CliApp.Create().Run(args);

    [Test]
    public void An_account_is_found_by_email_short_name_or_a_unique_start()
    {
        Assert.That(AccountSwitch.Find(Rows, "A@MindAttic.com")?.Account, Is.EqualTo("a/org"));
        Assert.That(AccountSwitch.Find(Rows, "b")?.Account, Is.EqualTo("b/org"), "the part before the @");
        Assert.That(AccountSwitch.Find(Rows, "mind"), Is.Null, "nothing starts with it");
        Assert.That(AccountSwitch.Find(Rows, "nobody"), Is.Null);
    }

    [Test]
    public void A_name_that_matches_several_accounts_at_a_step_is_ambiguous_and_switches_to_none()
    {
        // M.ORCH-T553: "d" starts two emails, so it fell through to the account-id step and picked z@, whose id starts with d.
        foreach (var (id, email, ago) in new[] { ("e1", "d1@mindattic.com", -50), ("e2", "d2@mindattic.com", -40), ("dz", "z@mindattic.com", -30) })
        {
            SignIn(id, email, null, Now.AddMinutes(ago));
            Observe(Reading(10, 10, Now.AddMinutes(ago)), Now.AddMinutes(ago));
        }
        var rows = AccountRoster.Load(db, "b/org");

        Assert.That(AccountSwitch.Find(rows, "d", out var several), Is.Null);
        Assert.That(several.Select(r => r.Email), Is.EquivalentTo(new[] { "d1@mindattic.com", "d2@mindattic.com" }));
        Assert.That(AccountSwitch.Find(rows, "z", out several)?.Email, Is.EqualTo("z@mindattic.com"));
        Assert.That(several, Is.Empty);

        Assert.That(Run("login", "d"), Is.EqualTo(1));
        Assert.That(logins, Is.Empty, "no account is signed in to");
        Assert.That(output.ToString(), Does.Contain("\"d\" matches more than one account").And.Contain("d1@mindattic.com").And.Contain("d2@mindattic.com"));
    }

    [Test]
    public void The_seat_comes_from_the_plan_and_the_most_headroom_from_the_week_left()
    {
        Assert.That(Rows.Select(AccountSwitch.Seat), Is.EqualTo(new[] { "standard", "premium" }), "b signed in first, then a");
        Assert.That(AccountSwitch.Seat(new AccountRow { Plan = "Max 20x" }), Is.Null, "a personal plan has no seat");
        Assert.That(AccountSwitch.MostHeadroom(Rows, Now)?.Account, Is.EqualTo("a/org"));
        Assert.That(AccountSwitch.UsesSso(new AccountRow { Plan = "Enterprise · Max 5x" }), Is.True);
        Assert.That(AccountSwitch.LoginArgs("a@x.com", sso: true), Is.EqualTo(new[] { "auth", "login", "--email", "a@x.com", "--sso" }));
    }

    [Test]
    public void Bare_login_prints_the_table_with_the_signed_in_account_marked_and_the_most_headroom_suggested()
    {
        Assert.That(Run("login"), Is.EqualTo(0));
        var text = output.ToString();
        Assert.That(text, Does.Contain("b@mindattic.com (signed in)").And.Contain("a@mindattic.com (most headroom)"));
        Assert.That(text, Does.Contain("Week usage").And.Contain("Last Checked").And.Contain("30%").And.Contain("80%"));
        Assert.That(text, Does.Not.Contain("premium").And.Not.Contain("Seat"));
        Assert.That(logins, Is.Empty, "listing signs nobody in");
    }

    [Test]
    public void Login_with_a_short_name_opens_that_accounts_login()
    {
        Assert.That(Run("login", "a"), Is.EqualTo(0), output.ToString());
        Assert.That(logins, Is.EqualTo(new[] { ("a@mindattic.com", false) }));
        Assert.That(output.ToString(), Does.Contain("claude auth login --email a@mindattic.com"));

        logins.Clear();
        Assert.That(Run("login", "a", "--sso"), Is.EqualTo(0));
        Assert.That(logins, Is.EqualTo(new[] { ("a@mindattic.com", true) }), "--sso forces the SSO flow");
    }

    [Test]
    public void The_signed_in_account_needs_no_login_and_an_unknown_one_or_a_failed_login_is_an_error()
    {
        Assert.That(Run("login", "b@mindattic.com"), Is.EqualTo(0));
        Assert.That(output.ToString(), Does.Contain("Already signed in as b@mindattic.com"));
        Assert.That(Run("login", "zed"), Is.EqualTo(1));
        loginExit = 1;
        Assert.That(Run("login", "a"), Is.EqualTo(1));
        Assert.That(output.ToString(), Does.Contain("ended with exit code 1"));
        Assert.That(logins, Has.Count.EqualTo(1));
    }

    [Test]
    public void Login_api_key_bills_the_key_and_choosing_an_account_bills_claude_team_again()
    {
        Assert.That(Run("login", "--api-key"), Is.EqualTo(0), output.ToString());
        Assert.That(billing.Mode(out _), Is.EqualTo(BillingModes.Api));

        var result = AccountSwitch.SignIn(Rows.Single(r => r.Account == "b/org"), "b/org", billing, [], "dashboard", false, Now);
        Assert.That(result.Error, Is.Null);
        Assert.That(billing.Mode(out _), Is.EqualTo(BillingModes.Team), "the account signed in bills again, with no login");
        Assert.That(logins, Is.Empty);
    }

    [Test]
    public void Each_tab_finds_its_own_logins_roster_row_and_the_most_headroom_skips_any_at_its_weekly_stop()
    {
        // M.ORCH-T628: the global login's tab reads whoever's signed in to it; a per-CLI login's tab the account with its email.
        Assert.That(MainViewModel.RowFor(Rows, null, "b/org")?.Account, Is.EqualTo("b/org"));
        Assert.That(MainViewModel.RowFor(Rows, "A@MindAttic.com", "b/org")?.Account, Is.EqualTo("a/org"), "by email, any case");
        Assert.That(MainViewModel.RowFor(Rows, "nobody@x.com", "b/org"), Is.Null);
        Assert.That(MainViewModel.RowFor(Rows, null, null), Is.Null, "the global login signed out (or on the API key)");

        // M.ORCH-T609: a 30%, b 80%. With the line at 75%, b's CLIs are stopped; at 25%, both are.
        Assert.That(AccountSwitch.Columns(Rows.Single(r => r.Account == "b/org"), Now, 75).WeekUsage, Is.EqualTo("80%, stopped at 75%"));
        Assert.That(AccountSwitch.Columns(Rows.Single(r => r.Account == "a/org"), Now, 75).WeekUsage, Is.EqualTo("30%, stops at 75%"));
        Assert.That(AccountSwitch.MostHeadroom(Rows, Now, 25), Is.Null, "every account at its stop: none to move to");
        Assert.That(AccountSwitch.MostHeadroom(Rows, Now, 85)?.Account, Is.EqualTo("a/org"));
        Assert.That(AccountSwitch.Columns(Rows[0], Now).WeekUsage, Does.Not.Contain("stop"), "no line given, the bare percent");
    }

    [Test]
    public void The_weekly_tile_reads_as_percent_left_with_its_reset_day_and_its_stop_line_and_the_plan_and_last_check()
    {
        // M.ORCH-T628: "23% left", "resets Mon 10/27 5:00 PM · stops at 95%", "Team · Max 5x · checked 3m ago".
        Assert.That(MainViewModel.WeeklyHeadlineOf(77), Is.EqualTo("23% left"));
        Assert.That(MainViewModel.WeeklyHeadlineOf(null), Is.EqualTo("—"));
        var reset = new DateTime(2026, 10, 27, 17, 0, 0, DateTimeKind.Local).ToUniversalTime();
        Assert.That(Formatting.ResetOnDay(reset), Is.EqualTo("Tue 10/27 5:00 PM"));
        Assert.That(MainViewModel.WeeklyCaptionOf(77, reset, 95, Now), Is.EqualTo("resets Tue 10/27 5:00 PM · stops at 95%"));
        Assert.That(MainViewModel.WeeklyCaptionOf(96, reset, 95, Now), Does.EndWith("stopped at 95%"));
        Assert.That(MainViewModel.WeeklyCaptionOf(10, null, 95, Now), Is.EqualTo("rolling 7 days · stops at 95%"));
        Assert.That(MainViewModel.WeeklyCaptionOf(null, reset, 95, Now), Is.EqualTo("no weekly limit known yet"));

        var b = Rows.Single(r => r.Account == "b/org");
        Assert.That(MainViewModel.AccountLineOf(b with { Plan = "Team · Max 5x", ReadUtc = Now.AddMinutes(-3) }, Now),
            Is.EqualTo($"Team · Max 5x · checked {Formatting.Ago(Now.AddMinutes(-3), Now)}"));
        Assert.That(MainViewModel.AccountLineOf(b with { Plan = null, ReadUtc = null }, Now), Is.EqualTo("not checked yet"));
        Assert.That(MainViewModel.AccountLineOf(b with { Plan = null, ReadUtc = Now.AddHours(-20) }, Now), Does.StartWith("STALE: last read"));
        Assert.That(MainViewModel.AccountLineOf(null, Now), Is.Empty);
    }

    [Test]
    public void The_menu_table_shows_week_usage_as_used_over_left_and_the_week_reset_with_its_date()
    {
        var b = Rows.Single(r => r.Account == "b/org");
        var c = AccountSwitch.Columns(b, Now);
        Assert.That(c.WeekUsage, Is.EqualTo("80%"));
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var wr = b.WeekResetAt(Now)!.Value;
        var fr = b.FiveHourResetAt(Now)!.Value;
        Assert.That(c.WeekResets, Is.EqualTo($"{wr.ToLocalTime().ToString("M/d h:mmtt", inv).ToLowerInvariant()} ({(wr - Now).TotalDays.ToString("0.0", inv)} days)"));
        Assert.That(c.FiveHourResets, Is.EqualTo($"{fr.ToLocalTime().ToString("M/d h:mmtt", inv).ToLowerInvariant()} ({(fr - Now).TotalHours.ToString("0.0", inv)} hours)"));
        Assert.That(c.WeekResets, Does.Match(@"^\d{1,2}/\d{1,2} \d{1,2}:\d{2}[ap]m \("), "10/21 5:30pm: no space before the lowercase am/pm");
        Assert.That(c.WeekResets, Does.EndWith("(3.0 days)"), "the fixture's week resets 3 days out");
        Assert.That(c.FiveHourResets, Does.EndWith("(3.0 hours)"), "its 5-hour window 3 hours out");
        Assert.That(AccountSwitch.Columns(new AccountRow { Account = "x/org" }, Now).WeekUsage, Is.EqualTo("—"), "never read");

        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        var xaml = File.ReadAllText(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        Assert.That(xaml, Does.Not.Contain("Text=\"Seat\"").And.Not.Contain("Text=\"Week used\"").And.Not.Contain("Text=\"Read\""));
    }

    [Test]
    public void The_account_menu_is_gone_and_billing_and_the_global_default_live_in_the_Accounts_view()
    {
        // M.ORCH-T628: every person is signed in at once, each on a tab of their own; no upper-right account menu.
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        var xaml = File.ReadAllText(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml.cs"));
        Assert.That(xaml, Does.Not.Contain("BillingChoice").And.Not.Contain("x:Name=\"AccountsCard\"")
            .And.Not.Contain("AccountMenuButton").And.Not.Contain("AccountPopup").And.Not.Contain("AccountMenuItems"));
        Assert.That(code, Does.Not.Contain("AccountPopup"));

        // No 'Make default' any more (M.ORCH-T1097); the billing detail line stays beside the org-wide actions.
        Assert.That(code, Does.Not.Contain("MakeDefault"));
        Assert.That(xaml[xaml.IndexOf("x:Name=\"OrgToolbar\"", StringComparison.Ordinal)..xaml.IndexOf("x:Name=\"SettingsPersonTabsTab\"", StringComparison.Ordinal)],
            Does.Contain("x:Name=\"BillingDetailLine\""));
        // M.ORCH-T906: no API-key switch in the dashboard any more; the API key is a person with its own tab, and
        // orchestrator billing stays the command-line tool.
        Assert.That(xaml, Does.Not.Contain("x:Name=\"ApiKeyBody\"").And.Not.Contain("x:Name=\"BillingToggleButton\"")
            .And.Not.Contain("{Binding ToggleBillingCommand}"));
        // The plan and how fresh the reading is: the Last updated tile (M.ORCH-T763); billing's detail stays off the person's stats.
        var header = xaml[xaml.IndexOf("x:Name=\"Header\"", StringComparison.Ordinal)..xaml.IndexOf("x:Name=\"Toolbar\"", StringComparison.Ordinal)];
        Assert.That(header, Does.Contain("{Binding LastUpdatedCaption}").And.Not.Contain("{Binding BillingDetail}"));
    }
}
