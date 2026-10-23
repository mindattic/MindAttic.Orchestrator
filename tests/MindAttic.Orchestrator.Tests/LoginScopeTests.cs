using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Wpf.ViewModels;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>One dashboard tab per Claude login (M.ORCH-T613): the one shared snapshot split into each login's view, the tabs
/// kept for the logins there are, and a Start from a tab opening on its login.</summary>
public sealed class LoginScopeTests
{
    private static readonly DateTime Now = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan WorkingWindow = TimeSpan.FromMinutes(2), WaitingWindow = TimeSpan.FromMinutes(30);

    private static AgentBoardRow Agent(string session, string repo, AgentState state, string? own) =>
        new(session, repo, null, null, Now.AddHours(-1), Now, state, "", false, default, []) { OwnLogin = own };

    /// <summary>Repo A: a global CLI waiting on you and Jordan's CLI working. Repo B: only Jordan's tab, idle.</summary>
    private static Snapshot Workspace()
    {
        var budget = BudgetCalculator.Compute([], new OrchestratorSettings(), Now, Now.Date);
        var own = new Pace(PaceMode.Open, 4, 1, 1, null, null, null, "jordan's");
        var global = new Pace(PaceMode.Open, 4, 1, 1, null, null, null, "global");
        var a = new RepoStatus("A", @"D:\W\A", RepoState.Working,
            [new HostSession("Claude", 1, "p1", @"D:\W\A"), new HostSession("Claude", 2, "p2", @"D:\W\A") { OwnLogin = "jordan@example.com" }],
            [new TranscriptSession("g1", @"D:\W\A", Now.AddMinutes(-10), new TokenUsage(1, 0, 0, 1), null),
             new TranscriptSession("j1", @"D:\W\A", Now.AddMinutes(-1), new TokenUsage(5, 0, 0, 5), null)],
            [], Now.AddMinutes(-1), new TokenUsage(6, 0, 0, 6));
        var b = new RepoStatus("B", @"D:\W\B", RepoState.Waiting,
            [new HostSession("Claude", 3, "p3", @"D:\W\B") { OwnLogin = "jordan@example.com" }], [], [], null, default);
        return new Snapshot(Now, [a, b], ["a warning"])
        {
            Budget = budget, Pace = global, Account = "acct-global",
            Agents = [Agent("g1", "A", AgentState.Waiting, null), Agent("j1", "A", AgentState.Working, "jordan@example.com")],
            OwnLogins = new Dictionary<string, string> { ["j1"] = "jordan@example.com" },
            Accounts = [new AccountPace("jordan@example.com", @"C:\x\jordan@example.com", "acct-jordan", budget, own, null, 1)],
            GlobalEmail = "ryan@example.com",
        };
    }

    [Test]
    public void The_global_logins_tab_sees_only_its_own_clis_tabs_and_sessions_and_its_own_meter()
    {
        var all = Workspace();
        var mine = LoginScope.For(all, null, WorkingWindow, WaitingWindow);

        var a = mine.Repos.Single(r => r.Name == "A");
        Assert.That(a.Sessions.Select(s => s.SessionId), Is.EqualTo(new[] { "g1" }));
        Assert.That(a.Hosts.Select(h => h.ProcessId), Is.EqualTo(new[] { 1 }));
        Assert.That(a.State, Is.EqualTo(RepoState.Waiting), "Jordan's CLI is the one working: here the repo's own CLI waits on you");
        Assert.That(a.UsageToday, Is.EqualTo(new TokenUsage(1, 0, 0, 1)));
        Assert.That(mine.Repos.Single(r => r.Name == "B").State, Is.EqualTo(RepoState.Dormant), "B's only tab is Jordan's");
        Assert.That(mine.Agents.Select(x => x.SessionId), Is.EqualTo(new[] { "g1" }));
        Assert.That((mine.Pace, mine.Account), Is.EqualTo((all.Pace, "acct-global")));
        Assert.That(mine.Warnings, Is.EqualTo(all.Warnings));
    }

    [Test]
    public void A_logins_tab_sees_its_own_clis_and_its_accounts_own_pace_and_every_repo_with_its_tickets()
    {
        var all = Workspace();
        var mine = LoginScope.For(all, "Jordan@example.com", WorkingWindow, WaitingWindow);

        var a = mine.Repos.Single(r => r.Name == "A");
        Assert.That(a.Sessions.Select(s => s.SessionId), Is.EqualTo(new[] { "j1" }));
        Assert.That(a.State, Is.EqualTo(RepoState.Working));
        Assert.That(a.OpenTickets, Is.SameAs(all.Repos[0].OpenTickets), "tickets stay workspace-wide");
        Assert.That(mine.Repos.Single(r => r.Name == "B").State, Is.EqualTo(RepoState.Waiting), "its Launcher tab there waits on you");
        Assert.That(mine.Repos.Select(r => r.Name), Is.EqualTo(new[] { "A", "B" }));
        Assert.That(mine.Agents.Select(x => x.SessionId), Is.EqualTo(new[] { "j1" }));
        Assert.That(mine.Pace, Is.SameAs(all.Accounts[0].Pace));
        Assert.That(mine.Account, Is.EqualTo("acct-jordan"));

        var unpaced = LoginScope.For(all, "amy@example.com", WorkingWindow, WaitingWindow);
        Assert.That((unpaced.Budget, unpaced.Pace, unpaced.Utilization), Is.EqualTo(((BudgetStatus?)null, (Pace?)null, (Utilization?)null)),
            "a login not paced yet never shows the global login's meter as its own");
        Assert.That(unpaced.Agents, Is.Empty);
    }

    [Test]
    public void A_repo_is_classified_as_the_aggregator_does()
    {
        Assert.That(LoginScope.Classify(Now.AddMinutes(-1), 0, Now, WorkingWindow, WaitingWindow), Is.EqualTo(RepoState.Working));
        Assert.That(LoginScope.Classify(Now.AddMinutes(-10), 0, Now, WorkingWindow, WaitingWindow), Is.EqualTo(RepoState.Waiting));
        Assert.That(LoginScope.Classify(null, 1, Now, WorkingWindow, WaitingWindow), Is.EqualTo(RepoState.Waiting), "an open tab waits on you");
        Assert.That(LoginScope.Classify(Now.AddHours(-2), 0, Now, WorkingWindow, WaitingWindow), Is.EqualTo(RepoState.Dormant));
    }

    [Test]
    public void The_tabs_are_the_global_login_then_every_login_signed_in_or_with_a_cli_on_it_by_name_and_color()
    {
        var all = Workspace() with
        {
            Agents = [Agent("x", "A", AgentState.Working, "amy@example.com")],
        };
        var tabs = LoginScope.Tabs(all, email => new LoginLook("w", "XX", "#123456") { Alias = email[..email.IndexOf('@')].ToUpperInvariant() },
            new LoginLook("ryan", "RY", "#654321") { Alias = "Ryan" });

        Assert.That(tabs.Select(t => t.Email), Is.EqualTo(new[] { null, "amy@example.com", "jordan@example.com" }));
        Assert.That(tabs.Select(t => t.Title), Is.EqualTo(new[] { "Ryan (fallback)", "AMY", "JORDAN" }));
        Assert.That(tabs[0].ToolTip, Does.StartWith("ryan@example.com"));
        Assert.That(tabs[2].ToolTip, Does.StartWith("jordan@example.com"));
        Assert.That((tabs[0].Color, tabs[1].Color), Is.EqualTo(("#654321", "#123456")));
        Assert.That(tabs.Select(t => t.Key), Is.EqualTo(new[] { "", "amy@example.com", "jordan@example.com" }));

        var unknown = LoginScope.Tabs(new Snapshot(Now, [], []), _ => throw new AssertionException("no per-CLI login"), new LoginLook("claude", "CL", "#000000"));
        Assert.That(unknown.Single().Title, Is.EqualTo("Fallback login"), "only the global login, and nobody's email known for it");
    }

    [Test]
    public void Every_member_gets_a_tab_titled_by_its_name_and_one_not_signed_in_yet_a_tab_that_only_signs_it_in()
    {
        // M.ORCH-T615, M.ORCH-T670: Amy's a member but not signed in here; Jordan's a member signed in; Ryan is the global
        // login's own account; Casey is a member with no name, whose login has an alias; Amy's alias is never used, her name is.
        Member M(string email, string? name) => new() { Email = email, Name = name, Role = "admin", Tier = "premium" };
        var tabs = LoginScope.Tabs(Workspace(), email => new LoginLook("w", "XX", "#123456") { Alias = email.StartsWith("casey", StringComparison.Ordinal) ? "Case" : email[..email.IndexOf('@')] },
            new LoginLook("ryan", "RY", "#654321") { Alias = "ryan" },
            [M("Amy@Example.com", "Amy R"), M("jordan@example.com", "Jordan J"), M("ryan@example.com", "Ryan D"), M("casey@example.com", null), M("not an email", "X")]);

        Assert.That(tabs.Select(t => t.Email), Is.EqualTo(new[] { null, "amy@example.com", "casey@example.com", "jordan@example.com" }),
            "every member gets a tab; the global login's own account gets no second one; a row with no valid email gets none");
        Assert.That(tabs.Select(t => t.Title), Is.EqualTo(new[] { "Ryan D (fallback)", "Amy R", "Case", "Jordan J" }), "by member name, else its login's alias");
        Assert.That(tabs.Select(t => t.SignedIn), Is.EqualTo(new[] { true, false, false, true }));
        Assert.That(tabs[1].ToolTip, Does.Contain("not signed in"));

        var amy = LoginScope.For(Workspace(), "amy@example.com", WorkingWindow, WaitingWindow);
        Assert.That(amy.Repos.SelectMany(r => r.Hosts), Is.Empty, "no CLI of anyone else's shows on Amy's tab");

        // No alias at all: the email's part before the @.
        Assert.That(MemberRoster.TitleOf(M("lee@example.com", null), null), Is.EqualTo("lee"));
        // A signed-in login that isn't a member still has its tab, by its alias.
        var none = LoginScope.Tabs(Workspace(), email => new LoginLook("w", "XX", "#123456") { Alias = "JJ" }, new LoginLook("ryan", "RY", "#654321") { Alias = "ryan" }, []);
        Assert.That(none.Select(t => t.Title), Is.EqualTo(new[] { "ryan (fallback)", "JJ" }));
    }

    [Test]
    public void Open_launcher_starts_the_launcher_scoped_to_the_tabs_login_in_its_own_window()
    {
        var dirs = new AccountDirs(@"C:\accounts", @"C:\claude", @"C:\claude.json");
        string? lookedUp = "unset";
        var control = new SessionControl(new OrchestratorOptions { LauncherExe = @"C:\L\MindAttic.Launcher.exe", WorkspaceRoot = @"C:\no-such-root" })
        {
            Dirs = dirs,
            Look = d => { lookedUp = d; return d is null ? new LoginLook("ryan", "RD", "#654321") : new LoginLook("Amy", "AR", "#123456"); },
        };

        var amy = control.LauncherArguments("Amy@Example.com").ToList();
        Assert.That(lookedUp, Is.EqualTo(dirs.For("amy@example.com").Directory), "colored as Amy's own login");
        Assert.That(amy.Take(7), Is.EqualTo(new[] { "-w", "Amy", "new-tab", "--title", "[AR] MindAttic.Launcher", "--tabColor", "#123456" }));
        Assert.That(amy.SkipWhile(a => a != "--").Skip(1), Is.EqualTo(new[] { @"C:\L\MindAttic.Launcher.exe", "--account", "amy@example.com" }));

        var global = control.LauncherArguments(null).ToList();
        Assert.That(lookedUp, Is.Null);
        Assert.That(global.SkipWhile(a => a != "--").Skip(1), Is.EqualTo(new[] { @"C:\L\MindAttic.Launcher.exe" }), "the global login's tab: unscoped");

        Assert.That(control.OpenLauncher("not an email"), Does.Contain("isn't an email address"));
        Assert.That(control.OpenLauncher("amy@example.com"), Does.StartWith("Launcher not found"));
    }

    [Test]
    public void A_tab_started_on_a_login_opens_there_and_auto_picks_only_when_none_was_chosen()
    {
        Assert.That(Dispatcher.ConfigDirFor("Claude", new Dispatcher.TabLogin(@"C:\a\j@x.com"), () => "picked"), Is.EqualTo(@"C:\a\j@x.com"));
        Assert.That(Dispatcher.ConfigDirFor("Claude", new Dispatcher.TabLogin(null), () => "picked"), Is.Null, "the global login's tab: the global login");
        Assert.That(Dispatcher.ConfigDirFor("Claude", null, () => "picked"), Is.EqualTo("picked"));
        Assert.That(Dispatcher.ConfigDirFor("Codex", new Dispatcher.TabLogin(@"C:\a\j@x.com"), () => "picked"), Is.Null, "Codex has no Claude login");
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void A_toast_is_tagged_with_its_tab_and_a_logins_color_is_its_accent()
    {
        var toast = new ToastViewModel("A needs you", "body", Palette.Waiting, null);
        Assert.That(toast.Title, Is.EqualTo("A needs you"));
        toast.Tab = "Jordan";
        Assert.That(toast.Title, Is.EqualTo("Jordan · A needs you"));

        var brush = (System.Windows.Media.SolidColorBrush)MainViewModel.BrushOf("#2E7D32");
        Assert.That(brush.Color, Is.EqualTo(System.Windows.Media.Color.FromRgb(0x2E, 0x7D, 0x32)));
        Assert.That(brush.IsFrozen, Is.True);
        Assert.That(MainViewModel.BrushOf("not a color"), Is.SameAs(Palette.Quiet));
        Assert.That(MainViewModel.BrushOf(""), Is.SameAs(Palette.Quiet));
    }
}
