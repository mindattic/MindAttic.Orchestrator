using System.Text;
using System.Windows.Threading;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Wpf.ViewModels;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>Terminals inside the Orchestrator (M.ORCH-T618).</summary>
public sealed class TerminalPanesTests
{
    private readonly TempDirs temp = new();

    [TearDown]
    public void TearDown()
    {
        PaneBroker.Local = null;
        temp.DeleteAll();
    }

    private static SessionControl Control(string launcher, bool embedded = false) =>
        new(new OrchestratorOptions { LauncherExe = launcher })
        {
            Look = d => LoginLook.For(d ?? @"C:\nowhere\global", d is null ? "jordan.reed@example.com" : "amy@example.com"),
            Embedded = () => embedded,
        };

    private static string Script(string commandLine)
    {
        var args = HostSessionScanner.SplitCommandLine(commandLine);
        Assert.That(args[0], Is.EqualTo("powershell.exe"));
        return Encoding.Unicode.GetString(Convert.FromBase64String(args[^1]));
    }

    [TestCase("plain")]
    [TestCase(@"C:\Program Files\My Repo")]
    [TestCase("say \"hi\" there")]
    [TestCase(@"ends in a slash\")]
    [TestCase(@"a\\""b")]
    [TestCase("")]
    public void A_joined_command_line_splits_back_into_the_same_arguments(string arg)
    {
        var line = SessionControl.JoinCommandLine(["first.exe", arg, "last"]);
        Assert.That(HostSessionScanner.SplitCommandLine(line), Is.EqualTo(new[] { "first.exe", arg, "last" }));
    }

    [Test]
    public void A_pane_runs_the_same_Launcher_host_command_a_tab_does_so_the_scanner_finds_its_folder_and_title()
    {
        var dir = temp.New();
        var request = Control(@"C:\Apps\MindAttic.Launcher\MindAttic.Launcher.exe").PaneRequestFor("Repo", dir, "Claude", "do \"it\"\nnow");
        var args = HostSessionScanner.SplitCommandLine(request.CommandLine);
        Assert.That(args, Is.EqualTo(new[]
        {
            @"C:\Apps\MindAttic.Launcher\MindAttic.Launcher.exe", "host", "--path", dir, "--title", "JO: Repo [Claude]",
            "--provider", "Claude", "--prompt", "do 'it' now",
        }));
        Assert.That(HostSessionScanner.TitleOf(request.CommandLine), Is.EqualTo("JO: Repo [Claude]"));
        Assert.That(request.Title, Is.EqualTo("JO: Repo [Claude]"));
        Assert.That(request.Initials, Is.EqualTo("JO"));
        Assert.That(request.TabColor, Does.Match("^#[0-9A-F]{6}$"));
        Assert.That(request.ConfigDir, Is.Null);
    }

    [Test]
    public void A_pane_on_a_login_of_its_own_sets_CLAUDE_CONFIG_DIR_inside_its_own_command()
    {
        var dir = temp.New();
        var request = Control(@"C:\Apps\L.exe").PaneRequestFor("Repo", dir, "Claude", null, @"C:\Users\x\.claude-accounts\amy@example.com");
        var script = Script(request.CommandLine);
        Assert.That(script, Does.StartWith("$env:CLAUDE_CONFIG_DIR = 'C:\\Users\\x\\.claude-accounts\\amy@example.com'; & 'C:\\Apps\\L.exe' 'host' '--path'"));
        Assert.That(script, Does.Contain($"'{dir}'").And.Contain("'AM: Repo [Claude]'").And.EndWith("; exit $LASTEXITCODE"));
        Assert.That(request.ConfigDir, Is.EqualTo(@"C:\Users\x\.claude-accounts\amy@example.com"));
    }

    [Test]
    public void A_recorded_pane_reopens_resuming_its_Claude_session_on_its_login()
    {
        var control = Control(@"C:\Apps\L.exe");
        var record = new PaneRecord("id", "Repo", @"D:\W\Repo", "Claude", DateTime.UtcNow) { ConfigDir = @"C:\a\amy@example.com", SessionId = "abc-123" };
        var request = control.ResumeRequest(record);
        var script = Script(request.CommandLine);
        Assert.That(script, Is.EqualTo("$env:CLAUDE_CONFIG_DIR = 'C:\\a\\amy@example.com'; & 'C:\\Apps\\L.exe' 'host' '--path' 'D:\\W\\Repo' '--title' 'AM: Repo [Claude]' '--provider' 'Claude' '--resume' 'abc-123' '--config-dir' 'C:\\a\\amy@example.com'; exit $LASTEXITCODE"));
        Assert.That(request.ResumeSession, Is.EqualTo("abc-123"));

        var global = Script(control.ResumeRequest(record with { ConfigDir = null }).CommandLine);
        Assert.That(global, Does.StartWith("Remove-Item Env:CLAUDE_CONFIG_DIR").And.Contain("'--resume' 'abc-123'").And.Not.Contain("--config-dir"), "the global login: no folder the dashboard happens to carry");

        var fresh = control.ResumeRequest(record with { SessionId = null });
        Assert.That(fresh.ResumeSession, Is.Null);
        Assert.That(Script(fresh.CommandLine), Does.Contain("'host'"), "no session seen: a new tab for the repo");
    }

    [Test]
    public void A_login_s_Launcher_tab_runs_the_Launcher_menu_signed_in_to_it_idle()
    {
        // M.ORCH-T699: one terminal tab per account, the Launcher already on its login, no repo and no prompt.
        var launcher = typeof(TerminalPanesTests).Assembly.Location; // any file that exists stands in for the Launcher
        var accounts = temp.New();
        var control = new SessionControl(new OrchestratorOptions { LauncherExe = launcher, WorkspaceRoot = accounts })
        {
            Look = d => LoginLook.For(d ?? @"C:\nowhere\global", d is null ? "jordan.reed@example.com" : "amy@example.com"),
            Dirs = new AccountDirs(accounts),
        };

        var amy = control.LauncherPaneRequest("Amy@Example.com", out var error);
        Assert.That(error, Is.Null);
        var dir = Path.Combine(accounts, "amy@example.com");
        Assert.That(amy!.IsLauncher, Is.True);
        Assert.That(amy.ConfigDir, Is.EqualTo(dir));
        Assert.That(TerminalPaneViewModel.LoginOf(amy.ConfigDir), Is.EqualTo("amy@example.com"), "it lands on Amy's Terminals tab");
        Assert.That(amy.Title, Is.EqualTo("AM: MindAttic.Launcher"));
        Assert.That(amy.Directory, Is.EqualTo(accounts), "at the workspace root, in no repo");
        Assert.That(Script(amy.CommandLine), Is.EqualTo($"$env:CLAUDE_CONFIG_DIR = '{dir}'; & '{launcher}' '--account' 'amy@example.com'; exit $LASTEXITCODE"));

        var global = control.LauncherPaneRequest(null, out error);
        Assert.That(error, Is.Null);
        Assert.That(global!.ConfigDir, Is.Null);
        Assert.That(Script(global.CommandLine), Is.EqualTo($"Remove-Item Env:CLAUDE_CONFIG_DIR -ErrorAction SilentlyContinue; & '{launcher}'; exit $LASTEXITCODE"));

        Assert.That(control.LauncherPaneRequest("not an email", out error), Is.Null);
        Assert.That(error, Does.Contain("isn't an email"));
        Assert.That(new SessionControl(new OrchestratorOptions { LauncherExe = @"C:\nope.exe" }).LauncherPaneRequest(null, out error), Is.Null);
        Assert.That(error, Does.StartWith("Launcher not found"));
    }

    [Test, Apartment(ApartmentState.STA)]
    public void An_account_that_already_has_its_Launcher_tab_reuses_it_instead_of_opening_a_second()
    {
        var store = new JsonStore(temp.New());
        var terminals = new TerminalsViewModel(Control(@"C:\nope.exe"), store);
        var shown = new List<TerminalPaneViewModel>();
        terminals.Opened += shown.Add;
        PaneRequest Launcher(string? configDir) => new(SessionControl.LauncherRepo, "AM: MindAttic.Launcher", temp.New(), "Claude", "cmd.exe") { ConfigDir = configDir, IsLauncher = true };
        const string amy = @"C:\a\.claude-accounts\amy@example.com";

        Assert.That(terminals.OpenLauncher(Launcher(amy), show: false), Is.Null);
        var pane = terminals.Panes.Single();
        Assert.That(shown, Is.Empty, "opened quietly: it doesn't take the window");
        Assert.That(terminals.SelectedPane, Is.Null);
        Assert.That(PaneRecords.Load(store), Is.Empty, "a Launcher tab isn't offered again from last time");
        Assert.That(terminals.LauncherOf("AMY@example.com"), Is.SameAs(pane));

        Assert.That(terminals.OpenLauncher(Launcher(amy), show: false), Is.Null);
        Assert.That(terminals.OpenLauncher(Launcher(amy), show: true), Is.Null);
        Assert.That(terminals.Panes.Single(), Is.SameAs(pane), "reused, never a second");
        Assert.That(shown, Is.EqualTo(new[] { pane }), "asked for, the one open is brought up");

        Assert.That(terminals.OpenLauncher(Launcher(null), show: false), Is.Null);
        Assert.That(terminals.Panes, Has.Count.EqualTo(2), "the global login has a Launcher tab of its own");
        Assert.That(terminals.LauncherOf(null), Is.Not.SameAs(pane));

        terminals.Close(pane);
        Assert.That(terminals.LauncherOf("amy@example.com"), Is.Null);
    }

    [Test]
    public void On_Orchestrator_Start_opens_a_pane_in_the_dashboard_instead_of_Windows_Terminal()
    {
        var dir = temp.New();
        var launcher = typeof(TerminalPanesTests).Assembly.Location; // any file that exists stands in for the Launcher
        PaneRequest? opened = null;
        PaneBroker.Local = r => { opened = r; return null; };
        Assert.That(Control(launcher, embedded: true).Start("Repo", dir, "Claude", "go"), Is.Null);
        Assert.That(opened, Is.Not.Null);
        Assert.That(opened!.Directory, Is.EqualTo(dir));
        Assert.That(HostSessionScanner.SplitCommandLine(opened.CommandLine), Does.Contain("--prompt"));

        PaneBroker.Local = _ => "no room";
        Assert.That(Control(launcher, embedded: true).Start("Repo", dir, "Claude"), Is.EqualTo("no room"), "the dashboard's error is the start's");
    }

    [Test]
    public async Task The_pane_pipe_hands_a_request_to_the_dashboard_and_carries_its_answer_back()
    {
        var pipe = "orch-panes-test-" + Guid.NewGuid().ToString("N");
        using var cts = new CancellationTokenSource();
        var got = new List<PaneRequest>();
        var serving = PaneBroker.ServeAsync(r => { got.Add(r); return Task.FromResult<string?>(r.Repo == "bad" ? "Couldn't open it" : null); }, cts.Token, pipe);

        var request = new PaneRequest("Repo", "JO: Repo [Claude]", @"D:\W\Repo", "Claude", "x.exe host") { ConfigDir = @"C:\a\b", Initials = "JO", TabColor = "#2E7D32" };
        Assert.That(await Task.Run(() => (PaneBroker.TrySend(request, out var e, pipe), e)), Is.EqualTo((true, (string?)null)));
        Assert.That(got.Single(), Is.EqualTo(request));
        Assert.That(await Task.Run(() => (PaneBroker.TrySend(request with { Repo = "bad" }, out var e, pipe), e)), Is.EqualTo((true, "Couldn't open it")));

        cts.Cancel();
        await serving.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Test]
    public void With_no_dashboard_listening_a_pane_request_says_so_at_once()
    {
        var started = DateTime.UtcNow;
        Assert.That(PaneBroker.TrySend(new PaneRequest("R", "R", "D", "Claude", "x"), out _, "orch-panes-nobody-" + Guid.NewGuid().ToString("N"), connectMs: 200), Is.False);
        Assert.That(DateTime.UtcNow - started, Is.LessThan(TimeSpan.FromSeconds(5)));
    }

    [Test]
    public void Panes_are_recorded_replaced_by_id_and_dropped()
    {
        var store = new JsonStore(temp.New());
        PaneRecords.Put(store, new PaneRecord("a", "A", @"D:\A", "Claude", DateTime.UtcNow));
        PaneRecords.Put(store, new PaneRecord("b", "B", @"D:\B", "Claude", DateTime.UtcNow));
        PaneRecords.Put(store, new PaneRecord("a", "A", @"D:\A", "Claude", DateTime.UtcNow) { SessionId = "s1", ConfigDir = @"C:\x\amy@example.com" });
        var kept = PaneRecords.Load(store);
        Assert.That(kept.Select(r => (r.Id, r.SessionId)), Is.EquivalentTo(new[] { ("b", (string?)null), ("a", "s1") }));
        Assert.That(kept.Single(r => r.Id == "a").Login, Is.EqualTo("amy@example.com"));
        Assert.That(kept.Single(r => r.Id == "b").Login, Is.EqualTo("global login"));
        PaneRecords.Remove(store, ["a"]);
        Assert.That(PaneRecords.Load(store).Select(r => r.Id), Is.EqualTo(new[] { "b" }));
        Assert.That(OrchestratorDb.Volatile, Does.Contain(PaneRecords.StateName), "this machine's panes, never synced");
    }

    [TestCase("Windows Terminal", OrchestratorSettings.WindowsTerminalHost)]
    [TestCase("wt", OrchestratorSettings.WindowsTerminalHost)]
    [TestCase("windows-terminal", OrchestratorSettings.WindowsTerminalHost)]
    [TestCase("Orchestrator", OrchestratorSettings.OrchestratorHost)]
    [TestCase("dashboard", OrchestratorSettings.OrchestratorHost)]
    [TestCase("tmux", null)]
    public void The_terminal_host_reads_as_typed(string typed, string? saved)
    {
        Assert.That(OrchestratorSettings.ParseTerminalHost(typed), Is.EqualTo(saved));
        Assert.That(new OrchestratorSettings().EmbeddedTerminals, Is.False, "Windows Terminal is the default");
        Assert.That(new OrchestratorSettings { TerminalHost = "typo" }.EmbeddedTerminals, Is.False);
    }

    [Test]
    public void The_Settings_page_saves_where_CLIs_open()
    {
        var store = new JsonStore(temp.New());
        var settings = new SettingsViewModel(store, () => { });
        Assert.That(settings.TerminalHost, Is.EqualTo(SettingsViewModel.WindowsTerminalChoice));
        settings.TerminalHost = SettingsViewModel.OrchestratorChoice;
        settings.SaveCommand.Execute(null);
        Assert.That(store.LoadSettings().TerminalHost, Is.EqualTo(OrchestratorSettings.OrchestratorHost));
        Assert.That(store.LoadSettings().EmbeddedTerminals, Is.True);
    }

    [Test]
    public void The_empty_terminals_hint_shows_only_in_the_Terminals_view()
    {
        // M.ORCH-T625: it drew over the Agents heading; it must be collapsed unless the Terminals view is showing.
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        var xaml = File.ReadAllText(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        var at = xaml.IndexOf("x:Name=\"TerminalsEmptyHint\"", StringComparison.Ordinal);
        Assert.That(at, Is.GreaterThan(0));
        var block = xaml[at..xaml.IndexOf("</TextBlock>", at, StringComparison.Ordinal)];
        Assert.That(block, Does.Contain("Value=\"Collapsed\"").And.Contain("IsTerminalsView"));
    }

    [Test]
    public void The_terminals_body_reads_its_visibility_from_the_window_not_from_its_own_Terminals_context()
    {
        // M.ORCH-T626: bound against its own DataContext (Terminals) the binding failed and the body showed in every view.
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        var xaml = File.ReadAllText(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        var at = xaml.IndexOf("x:Name=\"TerminalsBody\"", StringComparison.Ordinal);
        Assert.That(at, Is.GreaterThan(0));
        var tag = xaml[at..xaml.IndexOf('>', at)];
        Assert.That(tag, Does.Contain("DataContext=\"{Binding Terminals}\""));
        Assert.That(tag, Does.Contain("Visibility=\"{Binding DataContext.TerminalsVisibility, RelativeSource={RelativeSource AncestorType=Window}}\""));
        Assert.That(typeof(MindAttic.Orchestrator.Wpf.ViewModels.MainViewModel).GetProperty("TerminalsVisibility"), Is.Not.Null);
        Assert.That(typeof(PersonTerminalsViewModel).GetProperty("TerminalsVisibility"), Is.Null,
            "if Terminals ever gains one, the plain binding would be right again");
    }

    [Test]
    public void On_a_person_s_Terminals_tab_the_header_chart_and_view_pills_fold_away_for_the_panes()
    {
        // M.ORCH-T622: the terminals fill the window below the person tabs, beside a Dashboard tab.
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        var xaml = File.ReadAllText(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        string Tag(string name)
        {
            var at = xaml.IndexOf($"x:Name=\"{name}\"", StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThan(0), name);
            return xaml[at..xaml.IndexOf('>', at)];
        }
        Assert.That(Tag("PageTabStrip"), Does.Contain("ItemsSource=\"{Binding Pages}\"").And.Contain("SelectedItem=\"{Binding Page}\""));
        foreach (var folded in new[] { "Header", "Toolbar" })
            Assert.That(Tag(folded), Does.Contain("Visibility=\"{Binding ShowsDashboard, Converter={StaticResource Visible}}\""), folded);
        Assert.That(Tag("PaneGoToButton"), Does.Contain("ShowPaneRepoCommand"));
        Assert.That(Tag("PaneMoveToButton"), Does.Contain("Click=\"OnMoveToClick\""));
        Assert.That(MainViewModel.ViewNames, Does.Not.Contain(MainViewModel.TerminalsPage));
    }

    [TestCase(null, null)]
    [TestCase("", null)]
    [TestCase(@"C:\Users\me\.claude-accounts\amy@example.com", "amy@example.com")]
    [TestCase(@"C:\Users\me\.claude-accounts\amy@example.com\", "amy@example.com")]
    public void A_pane_belongs_to_the_login_its_config_folder_is(string? configDir, string? login) =>
        Assert.That(TerminalPaneViewModel.LoginOf(configDir), Is.EqualTo(login));

    [Test, Apartment(ApartmentState.STA)]
    public void A_person_s_Terminals_tab_keeps_their_chosen_pane_and_picks_a_neighbor_when_it_goes()
    {
        var all = new TerminalsViewModel(Control(@"C:\nope.exe"), new JsonStore(temp.New()));
        var person = new PersonTerminalsViewModel(all);
        TerminalPaneViewModel Pane(string repo) =>
            new(new PaneRequest(repo, repo, temp.New(), "Claude", "cmd.exe") { ConfigDir = @"C:\a\.claude-accounts\amy@example.com" },
                new PaneRecord(repo, repo, "", "Claude", DateTime.UtcNow));
        var (a, b, c) = (Pane("A"), Pane("B"), Pane("C"));
        Assert.That(a.Login, Is.EqualTo("amy@example.com"));
        var changes = 0;
        person.SelectionChanged += () => changes++;

        person.SyncTo([a]);
        Assert.That(person.SelectedPane, Is.SameAs(a), "a first pane is chosen as it arrives");
        person.SyncTo([a, b, c]);
        Assert.That(person.Panes, Is.EqualTo(new[] { a, b, c }));
        Assert.That(person.SelectedPane, Is.SameAs(a), "new panes don't take the choice from the one chosen");
        person.SelectedPane = b;
        person.SelectedPane = null;
        Assert.That(person.SelectedPane, Is.SameAs(b), "Ctrl+click can't leave it with none chosen");
        person.SelectedPane = Pane("someone else's");
        Assert.That(person.SelectedPane, Is.SameAs(b), "only its own panes");

        person.SyncTo([a, c]);
        Assert.That(person.SelectedPane, Is.SameAs(c), "the closed pane's neighbor");
        person.SyncTo([]);
        Assert.That(person.SelectedPane, Is.Null);
        Assert.That(person.HasPanes, Is.False);
        Assert.That(changes, Is.EqualTo(4));
    }

    [Test]
    public void The_badge_text_is_black_or_white_by_the_luminance_of_its_tab_color_chip()
    {
        // M.ORCH-T621: the badge sits on a chip of the tab color, so every palette color must read.
        var dark = new[] { "#6A1B9A", "#1565C0", "#AD1457", "#C62828", "#2E7D32" };
        foreach (var hex in dark)
            Assert.That(TerminalPaneViewModel.ReadableOn(new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex))),
                Is.SameAs(System.Windows.Media.Brushes.White), hex);
        Assert.That(TerminalPaneViewModel.ReadableOn(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Yellow)), Is.SameAs(System.Windows.Media.Brushes.Black));
    }

    [Test, Apartment(ApartmentState.STA)]
    public void A_pane_runs_its_command_in_a_real_terminal_and_closing_it_ends_the_process()
    {
        var store = new JsonStore(temp.New());
        var terminals = new TerminalsViewModel(Control(@"C:\nope.exe"), store);
        var host = new System.Windows.Controls.Grid();
        terminals.Panes.CollectionChanged += (_, e) =>
        {
            foreach (var p in e.NewItems?.OfType<TerminalPaneViewModel>() ?? []) host.Children.Add(p.Control);
            foreach (var p in e.OldItems?.OfType<TerminalPaneViewModel>() ?? []) host.Children.Remove(p.Control);
        };
        var window = OffScreenWindow.Show(600, 300, host);
        try
        {
            var opened = 0;
            terminals.Opened += _ => opened++;
            Assert.That(terminals.Open(new PaneRequest("Repo", "JO: Repo [Claude]", temp.New(), "Claude", "cmd.exe /k echo pane")), Is.Null);
            Assert.That(opened, Is.EqualTo(1));
            var pane = terminals.Panes.Single();
            Assert.That(pane.IsSelected, Is.True);
            Assert.That(PaneRecords.Load(store).Single().Repo, Is.EqualTo("Repo"), "recorded as it opens");

            Assert.That(Pump(() => pane.RootPid is not null, TimeSpan.FromSeconds(20)), Is.True, "ConPTY started the pane's process");
            var pid = pane.RootPid!.Value;
            terminals.Close(pane);
            Assert.That(terminals.Panes, Is.Empty);
            Assert.That(PaneRecords.Load(store), Is.Empty, "a pane closed here isn't offered again");
            Assert.That(Pump(() => !Alive(pid), TimeSpan.FromSeconds(10)), Is.True, "closing the pane ended its process");
        }
        finally { window.Close(); }
    }

    private static bool Alive(int pid)
    {
        try { using var p = System.Diagnostics.Process.GetProcessById(pid); return !p.HasExited; }
        catch (ArgumentException) { return false; }
    }

    private static bool Pump(Func<bool> until, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (until()) return true;
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
            Thread.Sleep(50);
        }
        return until();
    }
}
