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

    private static AgentBoardRow Agent(string session, string repo, AgentState state = AgentState.Working) =>
        new(session, repo, null, null, Now, Now, state, "", false, default, []);

    [Test]
    public void Each_live_cli_shows_on_the_login_its_session_runs_under()
    {
        var b = Folder("b@example.com", signedIn: true);
        Folder("c@example.com", signedIn: false);
        var fiveReset = Now.AddHours(2);
        var weekReset = Now.AddDays(3);
        File.WriteAllText(b.UsagePath, JsonSerializer.Serialize(new
        {
            rate_limits = new
            {
                five_hour = new { used_percentage = 40.0, resets_at = new DateTimeOffset(fiveReset).ToUnixTimeSeconds() },
                seven_day = new { used_percentage = 12.0, resets_at = new DateTimeOffset(weekReset).ToUnixTimeSeconds() },
            },
            at = Now.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        }));
        File.WriteAllText(b.StatusPath, $$"""{"pace":"open","subagent_allowance":6,"subagents_running":2,"timestamp":"{{Now:yyyy-MM-ddTHH:mm:ss}}Z"}""");
        File.WriteAllText(Path.Combine(dirs.ClaudeDir, "rl-status.json"), """{"stop":true}""");
        var row = new AccountRow { Account = "g/org", Email = "g@example.com", Plan = "Team · Max 5x", FiveHourPercent = 70, FiveHourResetUtc = Now.AddHours(1) };

        var cards = MainViewModel.CardsOf(dirs, row, signedIn: true,
            [Agent("s1", "Zeta"), Agent("s2", "Alpha", AgentState.Waiting), Agent("s3", "Beta"), Agent("s4", "Gamma")],
            new Dictionary<string, string> { ["s2"] = b.Directory, ["s3"] = b.Directory + Path.DirectorySeparatorChar, ["s4"] = dirs.For("d@example.com").Directory },
            Now);

        Assert.That(cards.Select(c => c.Email), Is.EqualTo(new string?[] { null, "b@example.com", "c@example.com", "d@example.com" }),
            "the global login first, then each per-CLI login, and a CLI on a folder since gone still gets one");
        var global = cards[0];
        Assert.That(global.SignedInAs, Is.EqualTo("g@example.com"));
        Assert.That(global.FiveHourPercent, Is.EqualTo(70));
        Assert.That(global.Pace?.Pace, Is.EqualTo("stop"));
        Assert.That(global.Clis.Select(c => c.Repo), Is.EqualTo(new[] { "Zeta" }), "a session with no config folder of its own is on the global login");

        var own = cards[1];
        Assert.That(own.SignedIn, Is.True);
        Assert.That(own.Plan, Is.EqualTo("Team · Max 5x"));
        Assert.That(own.FiveHourPercent, Is.EqualTo(40));
        Assert.That(own.WeekPercent, Is.EqualTo(12));
        Assert.That(own.Pace?.Subagents, Is.EqualTo("2 of 6"));
        Assert.That(own.Clis.Select(c => c.Repo), Is.EqualTo(new[] { "Alpha", "Beta" }));

        Assert.That(cards[2].SignedIn, Is.False);
        Assert.That(cards[2].Clis, Is.Empty);
        Assert.That(cards[3].Clis.Single().Repo, Is.EqualTo("Gamma"));
    }

    [Test]
    public void A_login_nobody_is_signed_in_to_offers_sign_in_and_the_global_one_says_to_use_login()
    {
        var signedOut = new LoginCard("c@example.com", "x", false, null, null, null, null, null, null, null, []);
        var card = new LoginCardViewModel("c@example.com");
        card.Update(signedOut, Now);
        Assert.That(card.CanSignIn, Is.True);
        Assert.That(card.SignedInText, Is.EqualTo("Not signed in"));
        Assert.That(card.FiveHourUsed, Is.EqualTo("—"));
        Assert.That(card.ClisHeader, Is.EqualTo("No CLI running on it"));
        Assert.That(card.PaceText, Does.StartWith("Not paced yet"));

        var global = new LoginCardViewModel("");
        global.Update(signedOut with { Email = null }, Now);
        Assert.That(global.Title, Is.EqualTo(MainViewModel.GlobalLogin));
        Assert.That(global.CanSignIn, Is.False);
        Assert.That(global.SignInHint, Does.Contain("/login"));

        var other = new LoginCardViewModel("b@example.com");
        other.Update(new LoginCard("b@example.com", "x", true, "z@example.com", "Team", 40, Now.AddHours(2), 12, Now.AddDays(3),
            new LoginPace("open", "1", "4", Now.AddMinutes(-2)), [new LoginCli("s1", "Repo", AgentState.Working), new LoginCli("s2", "Repo2", AgentState.Waiting)]), Now);
        Assert.That(other.CanSignIn, Is.False);
        Assert.That(other.SignedInText, Is.EqualTo("z@example.com"));
        Assert.That(other.Mismatch, Does.Contain("spend z@example.com's tokens"));
        Assert.That(other.FiveHourUsed, Is.EqualTo("40%"));
        Assert.That(other.WeekResets, Does.StartWith("stops at 95% · resets "), "its weekly stop line (M.ORCH-T609), then the reset");
        Assert.That(other.PaceText, Is.EqualTo("Pace open · subagents 1 of 4 · written 2m ago"));
        Assert.That(other.ClisHeader, Is.EqualTo("2 CLIs running on it"));
        Assert.That(other.Clis.Select(c => c.StateLabel), Is.EqualTo(new[] { "Working", "Needs you" }));
    }

    [Test]
    public void A_card_shows_its_logins_name_with_the_email_in_the_tooltip_and_renames_it()
    {
        var b = Folder("b@example.com", signedIn: true);
        var card = new LoginCardViewModel(b.Email);
        card.Update(LoginBoard.Own(b, Now), Now);
        Assert.That((card.Title, card.TitleTip, card.CanRename), Is.EqualTo(("b", "b@example.com", true)), "the email's part before the @ by default");

        card.BeginRename();
        Assert.That((card.IsRenaming, card.AliasDraft), Is.EqualTo((true, "b")));
        Assert.That(MainViewModel.SaveAlias(dirs, b.Directory, b.Email, "Bee"), Is.EqualTo("b@example.com is now called Bee."));
        card.Update(LoginBoard.Own(b, Now), Now);
        Assert.That(card.Title, Is.EqualTo("Bee"));

        // The global login is named for whoever's signed in to it, marked "(global)".
        MainViewModel.SaveAlias(dirs, dirs.ClaudeDir, "g@example.com", "Gee");
        var global = new LoginCardViewModel("");
        global.Update(LoginBoard.Global(dirs.ClaudeDir, new AccountRow { Account = "g", Email = "g@example.com" }, true, Path.Combine(dirs.ClaudeDir, "rl-status.json"), Now), Now);
        Assert.That(global.Title, Is.EqualTo("Gee (global)"));
        Assert.That(global.TitleTip, Does.StartWith("g@example.com"));
    }

    [Test]
    public void Accounts_is_a_view_beside_the_others()
    {
        Assert.That(MainViewModel.ViewNames, Is.EqualTo(new[] { "Agents", "Repos", "Queue", "Audit", "Accounts", "Terminals" }));
        Assert.That(MainViewModel.ViewNames.Select(n => Enum.TryParse<View>(n, out _)), Is.All.True);
        Assert.That(MainViewModel.LoginsSummary(1, 1), Is.EqualTo("1 login · 1 live CLI"));
        Assert.That(MainViewModel.LoginsSummary(3, 4), Is.EqualTo("3 logins · 4 live CLIs"));
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        var xaml = File.ReadAllText(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        Assert.That(xaml, Does.Contain("x:Name=\"AccountsBody\"").And.Contain("IsAccountsView").And.Contain("x:Name=\"SignInAllButton\""));
    }
}
