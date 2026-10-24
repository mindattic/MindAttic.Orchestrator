using System.Text.Json;
using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Wpf.ViewModels;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The dashboard's Accounts view (M.ORCH-T603): a card per Claude login, the global one first, each with its meter,
/// pace and the live CLIs on it. Every folder is a scratch one, never ~/.claude or ~/.claude-accounts.</summary>
public sealed class AccountsViewTests
{
    private readonly TempDirs temps = new();
    private AccountDirs dirs = null!;
    private static readonly DateTime Now = DateTime.UtcNow;

    [SetUp]
    public void SetUp()
    {
        var dir = temps.New();
        Directory.CreateDirectory(Path.Combine(dir, ".claude"));
        dirs = new AccountDirs(Path.Combine(dir, ".claude-accounts"), Path.Combine(dir, ".claude"), Path.Combine(dir, ".claude.json"));
    }

    [TearDown]
    public void TearDown() => temps.DeleteAll();

    private AccountDir Folder(string email, bool signedIn)
    {
        var account = dirs.For(email);
        Directory.CreateDirectory(account.Directory);
        if (signedIn)
            File.WriteAllText(account.ConfigPath, JsonSerializer.Serialize(new
            {
                oauthAccount = new { accountUuid = "acct-" + email[0], emailAddress = email, organizationType = "claude_team", userRateLimitTier = "default_claude_max_5x" },
            }));
        return account;
    }

    [Test]
    public void A_login_is_renamed_by_its_folder_and_email_and_the_global_one_by_whoever_is_signed_in()
    {
        // M.ORCH-T763: Rename is a person's login action now; it names the login's own folder, the global one's for its tab.
        var b = Folder("b@example.com", signedIn: true);
        Assert.That(MainViewModel.SaveAlias(dirs, b.Directory, b.Email, "Bee"), Is.EqualTo("b@example.com is now called Bee [BE]."));
        Assert.That(LoginLook.For(b.Directory, b.Email).Alias, Is.EqualTo("Bee"));
        MainViewModel.SaveAlias(dirs, dirs.ClaudeDir, "g@example.com", "Gee");
        Assert.That(LoginLook.For(dirs.ClaudeDir, "g@example.com").Alias, Is.EqualTo("Gee"));
    }

    [TestCase("90", "98", null)]
    [TestCase("50", "1", null)]
    [TestCase(" 95% ", "100", null)]
    [TestCase("49", "98", "weekly")]
    [TestCase("101", "98", "weekly")]
    [TestCase("x", "98", "weekly")]
    [TestCase("90", "0", "5-hour")]
    [TestCase("90", "", "5-hour")]
    public void Org_limit_takes_each_cutoff_only_in_its_bounds(string weekly, string fiveHour, string? refused)
    {
        // M.ORCH-T763: the person's Org limit dialog, as orchestrator org limit --weekly --5h bounds them (M.ORCH-T707).
        var why = MainViewModel.ParseCutoffs(weekly, fiveHour, out var w, out var f);
        if (refused is null)
        {
            Assert.That(why, Is.Null);
            Assert.That((w, f), Is.EqualTo((int.Parse(weekly.Trim().TrimEnd('%')), int.Parse(fiveHour))));
        }
        else Assert.That(why, Does.Contain(refused));
    }

    [Test]
    public void The_rename_dialog_saves_the_name_the_prefix_and_the_members_name()
    {
        // M.ORCH-T633: Name and Prefix from the dialog; the Prefix is the login file's initials, blank derives it again.
        var b = Folder("b@example.com", signedIn: true);
        var db = new MindAttic.Orchestrator.Settings.OrchestratorDb(Path.Combine(temps.New(), "data", "orchestrator.db"));
        MemberRoster.Add(db, b.Email, "Old", "member", "standard", null, Now);

        Assert.That(MainViewModel.SaveAlias(dirs, b.Directory, b.Email, "Jordan", "jr", db), Is.EqualTo("b@example.com is now called Jordan [JR]."));
        var look = LoginLook.For(b.Directory, b.Email);
        Assert.That((look.Alias, look.Initials, LoginLook.InitialsOverride(b.Directory, b.Email)), Is.EqualTo(("Jordan", "JR", "jr")));
        Assert.That(MemberRoster.Find(db, b.Email)!.Name, Is.EqualTo("Jordan"), "the member's name kept in step (M.ORCH-T670)");

        MainViewModel.SaveAlias(dirs, b.Directory, b.Email, "Jordan Reed", "", db);
        look = LoginLook.For(b.Directory, b.Email);
        Assert.That((look.Initials, LoginLook.InitialsOverride(b.Directory, b.Email)), Is.EqualTo(("JR", (string?)null)), "a blank prefix comes from the name");

        MainViewModel.SaveAlias(dirs, b.Directory, b.Email, "Amy", null, db);
        Assert.That(LoginLook.For(b.Directory, b.Email).Initials, Is.EqualTo("AM"));

        // A login that isn't a member is renamed all the same, and adds no member.
        var c = Folder("c@example.com", signedIn: true);
        Assert.That(MainViewModel.SaveAlias(dirs, c.Directory, c.Email, "Cee", "CC", db), Is.EqualTo("c@example.com is now called Cee [CC]."));
        Assert.That(MemberRoster.Find(db, c.Email), Is.Null);
    }

    [Test]
    public void The_login_actions_live_on_each_persons_page_and_the_org_wide_ones_above_the_person_tabs()
    {
        // M.ORCH-T763: no Accounts view. A person's page leads with Rename, Org limit and Move CLIs; Add login, Sign in
        // all and Report sit in a small toolbar above the person tabs, on the global login's tab.
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        var xaml = File.ReadAllText(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml.cs"));
        Assert.That(xaml, Does.Not.Contain("x:Name=\"AccountsBody\"").And.Not.Contain("IsAccountsView"));

        var org = xaml[xaml.IndexOf("x:Name=\"OrgToolbar\"", StringComparison.Ordinal)..xaml.IndexOf("x:Name=\"LoginTabStrip\"", StringComparison.Ordinal)];
        Assert.That(org, Does.Contain("{Binding AddAccountCommand}").And.Contain("x:Name=\"SignInAllButton\"").And.Contain("{Binding UsageReportCommand}"));
        Assert.That(xaml.IndexOf("x:Name=\"OrgStrip\"", StringComparison.Ordinal), Is.LessThan(xaml.IndexOf("x:Name=\"OrgToolbar\"", StringComparison.Ordinal)), "under the org switcher");
        Assert.That(code, Does.Contain("OrgToolbar.DataContext = model;"));

        var person = xaml[xaml.IndexOf("x:Name=\"PersonActions\"", StringComparison.Ordinal)..xaml.IndexOf("x:Name=\"UtilizationCard\"", StringComparison.Ordinal)];
        // M.ORCH-T796: no per-person Sign in; a disconnected login opens its sign-in by itself.
        Assert.That(person, Does.Not.Contain("SignInPerson"));
        Assert.That(person, Does.Contain("{Binding RenamePersonCommand}")
            .And.Contain("{Binding OrgLimitCommand}").And.Contain("Click=\"OnMoveAllClick\""));
        Assert.That(xaml.IndexOf("x:Name=\"StatTiles\"", StringComparison.Ordinal), Is.LessThan(xaml.IndexOf("x:Name=\"PersonActions\"", StringComparison.Ordinal)),
            "the stats, then the actions, then the chart");
        Assert.That(xaml, Does.Contain("x:Name=\"LimitModal\"").And.Contain("{Binding SaveLimitCommand}"));
    }

    [Test]
    public void A_login_signed_out_since_the_last_refresh_is_the_one_that_opens_its_sign_in()
    {
        // M.ORCH-T796: only a login that was signed in and now isn't; never one seen for the first time, signed out all along,
        // still signed in, or the global, 'API key' and 'Miscellaneous' tabs.
        var was = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["a@example.com"] = true, ["b@example.com"] = false, ["c@example.com"] = true, [""] = true, [LoginTab.ApiKeyKey] = true,
        };
        LoginTab[] now =
        [
            new(null, "Global", "", "", SignedIn: false),
            new("a@example.com", "A", "", "", SignedIn: false),
            new("b@example.com", "B", "", "", SignedIn: false),
            new("c@example.com", "C", "", ""),
            new("d@example.com", "D", "", "", SignedIn: false),
            LoginTab.ApiKey with { SignedIn = false },
            LoginTab.Misc with { SignedIn = false },
        ];
        Assert.That(MainViewModel.NewlyDisconnected(was, now), Is.EqualTo(new[] { "a@example.com" }));
    }
}
