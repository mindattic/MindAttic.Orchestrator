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
        Assert.That(tabs.Select(t => t.Title), Is.EqualTo(new[] { "Ryan (global)", "AMY", "JORDAN" }));
        Assert.That(tabs[0].ToolTip, Does.StartWith("ryan@example.com"));
        Assert.That(tabs[2].ToolTip, Does.StartWith("jordan@example.com"));
        Assert.That((tabs[0].Color, tabs[1].Color), Is.EqualTo(("#654321", "#123456")));
        Assert.That(tabs.Select(t => t.Key), Is.EqualTo(new[] { "", "amy@example.com", "jordan@example.com" }));

        var unknown = LoginScope.Tabs(new Snapshot(Now, [], []), _ => throw new AssertionException("no per-CLI login"), new LoginLook("claude", "CL", "#000000"));
        Assert.That(unknown.Single().Title, Is.EqualTo("Global login"), "only the global login, and nobody's email known for it");
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
