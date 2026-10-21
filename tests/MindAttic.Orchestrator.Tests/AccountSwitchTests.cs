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
        Assert.That(text, Does.Contain("premium").And.Contain("standard").And.Contain("70%"));
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
    public void The_menu_lists_every_account_then_the_api_key_and_the_corner_names_who_bills()
    {
        var menu = MainViewModel.MenuOf(Rows, "b/org", BillingModes.Team, Now);
        Assert.That(menu.Select(m => m.Id), Is.EqualTo(new[] { "b/org", "a/org", MainViewModel.ApiKeyItem }));
        Assert.That(menu[0].Label, Does.StartWith("b@mindattic.com (signed in) · standard · week 80% used, 20% left"));
        Assert.That(menu[0].Icon, Is.EqualTo(""));
        Assert.That(menu[1].Label, Does.StartWith("a@mindattic.com (most headroom) · premium").And.Contain("5h 10%").And.Contain("read "));
        Assert.That(menu[2].Label, Is.EqualTo("Use API key (emergencies)"));

        Assert.That(MainViewModel.CornerOf(BillingModes.Team, Rows, "b/org"), Is.EqualTo("b@mindattic.com"));
        Assert.That(MainViewModel.CornerOf(BillingModes.Api, Rows, "b/org"), Is.EqualTo("API key"));
        var onKey = MainViewModel.MenuOf(Rows, "b/org", BillingModes.Api, Now);
        Assert.That(onKey[0].Label, Does.Not.Contain("(signed in)"), "the key bills, not the account");
        Assert.That(onKey[^1].Label, Is.EqualTo("Use API key (emergencies) (in use)"));
    }

    [Test]
    public void The_dashboard_lists_accounts_as_a_table_and_the_billing_toggle_and_Accounts_card_are_gone_from_the_window()
    {
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        var xaml = File.ReadAllText(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml.cs"));
        Assert.That(xaml, Does.Not.Contain("BillingChoice").And.Not.Contain("x:Name=\"AccountsCard\"")
            .And.Contain("x:Name=\"AccountMenuButton\"").And.Contain("x:Name=\"AccountPopup\"").And.Contain("x:Name=\"AccountRows\""));
        Assert.That(code, Does.Contain("AccountPopup.IsOpen"));
    }
}
