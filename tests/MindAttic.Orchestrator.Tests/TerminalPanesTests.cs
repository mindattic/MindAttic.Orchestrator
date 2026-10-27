using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml.Linq;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Wpf;
using MindAttic.Orchestrator.Wpf.ViewModels;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>Each person's Terminals panel (M.ORCH-T810): live ConPTY terminals on their page, and the pipe that hands the
/// dashboard a CLI to open there.</summary>
public sealed class TerminalPanesTests
{
    private readonly TempDirs temp = new();

    [TearDown]
    public void TearDown() => temp.DeleteAll();

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
        var line = PaneRequest.JoinCommandLine(["first.exe", arg, "last"]);
        Assert.That(HostSessionScanner.SplitCommandLine(line), Is.EqualTo(new[] { "first.exe", arg, "last" }));
    }

    [Test]
    public void A_terminal_runs_the_Launcher_s_own_command_with_color_on_and_the_inside_mark_set()
    {
        var request = new PaneRequest("RD: Repo [Claude]", @"D:\W\Repo")
        {
            Command = [@"C:\Apps\MindAttic.Launcher.exe", "host", "--path", @"D:\W\Repo", "--title", "RD: Repo [Claude]", "--provider", "Claude", "--prompt", "do 'it'\nnow"],
        };
        Assert.That(Script(request.ProcessCommandLine("the-pipe")), Is.EqualTo(
            "Remove-Item Env:NO_COLOR -ErrorAction SilentlyContinue; $env:COLORTERM = 'truecolor'; $env:MINDATTIC_ORCHESTRATOR_TERMINALS = 'the-pipe'; " +
            "& 'C:\\Apps\\MindAttic.Launcher.exe' 'host' '--path' 'D:\\W\\Repo' '--title' 'RD: Repo [Claude]' '--provider' 'Claude' '--prompt' 'do ''it''\nnow'; exit $LASTEXITCODE"));

        var bare = new PaneRequest("T", @"D:\W") { CommandLine = "cmd.exe /k echo hi" };
        Assert.That(Script(bare.ProcessCommandLine("p")), Does.EndWith("& cmd.exe /d /c 'cmd.exe /k echo hi'; exit $LASTEXITCODE"));
        Assert.That(PaneBroker.InsideVariable, Is.EqualTo("MINDATTIC_ORCHESTRATOR_TERMINALS"));
        Assert.That(PaneBroker.PipeName, Does.StartWith("mindattic-orchestrator-terminals-"));
    }

    [TestCase(null, null, null)]
    [TestCase(null, "", null)]
    [TestCase(null, @"C:\Users\me\.claude-accounts\Amy@Example.com", "amy@example.com")]
    [TestCase(null, @"C:\Users\me\.claude-accounts\amy@example.com\", "amy@example.com")]
    [TestCase(null, @"C:\Users\me\.claude", null)]
    [TestCase("Jordan@Example.com", @"C:\Users\me\.claude-accounts\amy@example.com", "jordan@example.com")]
    public void A_terminal_goes_on_the_page_of_the_login_it_runs_on(string? email, string? configDir, string? login) =>
        Assert.That(new PaneRequest("T", "D") { Email = email, ConfigDir = configDir }.Login, Is.EqualTo(login));

    [Test]
    public void A_request_with_no_folder_or_no_command_is_refused_with_why()
    {
        var dir = temp.New();
        Assert.That(new PaneRequest("T", dir) { Command = ["x.exe"] }.Problem(), Is.Null);
        Assert.That(new PaneRequest("T", dir) { CommandLine = "x.exe" }.Problem(), Is.Null);
        Assert.That(new PaneRequest("T", dir).Problem(), Is.EqualTo("No command to run."));
        Assert.That(new PaneRequest("T", Path.Combine(dir, "gone")) { Command = ["x.exe"] }.Problem(), Does.StartWith("Folder not found"));
        Assert.That(new PaneRequest("", dir) { Command = ["x.exe"] }.Problem(), Is.EqualTo("No title."));
        Assert.That(new PaneRequest("T", @"D:\W\MindAttic.Launcher").Name, Is.EqualTo("MindAttic.Launcher"), "named for its folder");
        Assert.That(new PaneRequest("T", @"D:\W\X") { Repo = "Repo" }.Name, Is.EqualTo("Repo"));
    }

    [Test]
    public void The_Launcher_s_JSON_reads_whatever_the_case_of_its_names()
    {
        // M.LNCH-T77 writes it: the wt tab's title, folder and command, and its login.
        var request = PaneBroker.Deserialize("""{"title":"AM: Repo [Claude]","directory":"D:\\W\\Repo","command":["L.exe","host"],"configDir":"C:\\a\\amy@example.com","tabColor":"#2E7D32","initials":"AM"}""");
        Assert.That(request!.Title, Is.EqualTo("AM: Repo [Claude]"));
        Assert.That(request.Directory, Is.EqualTo(@"D:\W\Repo"));
        Assert.That(request.Command, Is.EqualTo(new[] { "L.exe", "host" }));
        Assert.That(request.Login, Is.EqualTo("amy@example.com"));
        Assert.That((request.TabColor, request.Initials, request.Provider), Is.EqualTo(("#2E7D32", "AM", "Claude")));
        Assert.That(PaneBroker.Serialize(request), Does.Not.Contain("Login").And.Not.Contain("\"Name\""), "derived, never sent");
    }

    [Test]
    public async Task The_pipe_hands_a_request_to_the_dashboard_and_carries_its_answer_back()
    {
        var dir = temp.New();
        var pipe = "orch-terminals-test-" + Guid.NewGuid().ToString("N");
        using var cts = new CancellationTokenSource();
        var got = new List<PaneRequest>();
        var serving = PaneBroker.ServeAsync(r => { got.Add(r); return Task.FromResult<string?>(r.Repo == "bad" ? "Couldn't open it" : null); }, cts.Token, pipe);

        var request = new PaneRequest("JO: Repo [Claude]", dir) { Command = ["x.exe", "host"], ConfigDir = @"C:\a\b", Initials = "JO", TabColor = "#2E7D32" };
        Assert.That(await Task.Run(() => (PaneBroker.TrySend(request, out var e, pipe), e)), Is.EqualTo((true, (string?)null)));
        Assert.That(got.Single().Title, Is.EqualTo(request.Title));
        Assert.That(got.Single().Command, Is.EqualTo(request.Command));
        Assert.That(got.Single().ConfigDir, Is.EqualTo(request.ConfigDir));
        Assert.That(await Task.Run(() => (PaneBroker.TrySend(request with { Repo = "bad" }, out var e, pipe), e)), Is.EqualTo((true, "Couldn't open it")));
        // Refused before the dashboard sees it: there's nothing it could open.
        var (taken, refused) = await Task.Run(() => (PaneBroker.TrySend(request with { Directory = Path.Combine(dir, "gone") }, out var e, pipe), e));
        Assert.That(taken, Is.True);
        Assert.That(refused, Does.StartWith("Folder not found"));
        Assert.That(got, Has.Count.EqualTo(2));

        cts.Cancel();
        await serving.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Test]
    public async Task A_dashboard_that_throws_opening_a_terminal_still_answers_so_the_caller_doesn_t_wait()
    {
        var pipe = "orch-terminals-test-" + Guid.NewGuid().ToString("N");
        using var cts = new CancellationTokenSource();
        var serving = PaneBroker.ServeAsync(_ => throw new InvalidOperationException("boom"), cts.Token, pipe);
        var request = new PaneRequest("R", temp.New()) { Command = ["x.exe"] };
        var started = DateTime.UtcNow;
        var (taken, error) = await Task.Run(() => (PaneBroker.TrySend(request, out var e, pipe), e));
        Assert.That(taken, Is.True);
        Assert.That(error, Does.Contain("boom"));
        Assert.That(DateTime.UtcNow - started, Is.LessThan(TimeSpan.FromSeconds(20)));
        cts.Cancel();
        await serving.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Test]
    public void With_no_dashboard_listening_a_request_says_so_at_once()
    {
        var started = DateTime.UtcNow;
        Assert.That(PaneBroker.TrySend(new PaneRequest("R", "D") { Command = ["x"] }, out _, "orch-terminals-nobody-" + Guid.NewGuid().ToString("N"), connectMs: 200), Is.False);
        Assert.That(DateTime.UtcNow - started, Is.LessThan(TimeSpan.FromSeconds(5)));
    }

    [Test, Apartment(ApartmentState.STA)]
    public void A_person_s_panel_keeps_their_chosen_terminal_and_picks_a_neighbor_when_it_goes()
    {
        var all = new TerminalsViewModel();
        var person = new PersonTerminalsViewModel(all);
        TerminalPaneViewModel Pane(string repo) =>
            new(new PaneRequest(repo, temp.New()) { Repo = repo, CommandLine = "cmd.exe", ConfigDir = @"C:\a\.claude-accounts\amy@example.com" });
        var (a, b, c) = (Pane("A"), Pane("B"), Pane("C"));
        Assert.That(a.Login, Is.EqualTo("amy@example.com"));
        var changes = 0;
        person.SelectionChanged += () => changes++;

        person.SyncTo([a]);
        Assert.That(person.SelectedPane, Is.SameAs(a), "a first terminal is chosen as it arrives");
        person.SyncTo([a, b, c]);
        Assert.That(person.Panes, Is.EqualTo(new[] { a, b, c }));
        Assert.That(person.Summary, Is.EqualTo("3 open"));
        Assert.That(person.SelectedPane, Is.SameAs(a), "new ones don't take the choice from the one chosen");
        person.SelectedPane = b;
        person.SelectedPane = null;
        Assert.That(person.SelectedPane, Is.SameAs(b), "Ctrl+click can't leave it with none chosen");
        person.SelectedPane = Pane("someone else's");
        Assert.That(person.SelectedPane, Is.SameAs(b), "only its own");

        person.SyncTo([a, c]);
        Assert.That(person.SelectedPane, Is.SameAs(c), "the closed one's neighbor");
        person.SyncTo([]);
        Assert.That(person.SelectedPane, Is.Null);
        Assert.That(person.HasPanes, Is.False);
        Assert.That(person.Summary, Is.Empty);
        Assert.That(changes, Is.EqualTo(4));
    }

    [Test, Apartment(ApartmentState.STA)]
    public void Next_and_previous_cycle_the_persons_own_terminal_tabs_and_do_nothing_with_fewer_than_two()
    {
        // M.ORCH-T824: Ctrl+Left/Ctrl+Right, so switching works even while a terminal holds keyboard focus.
        var all = new TerminalsViewModel();
        var person = new PersonTerminalsViewModel(all);
        TerminalPaneViewModel Pane(string repo) =>
            new(new PaneRequest(repo, temp.New()) { Repo = repo, CommandLine = "cmd.exe", ConfigDir = @"C:\a\.claude-accounts\amy@example.com" });
        var (a, b, c) = (Pane("A"), Pane("B"), Pane("C"));

        person.SyncTo([a]);
        person.NextCommand.Execute(null);
        Assert.That(person.SelectedPane, Is.SameAs(a), "nothing to cycle to with only one tab");

        person.SyncTo([a, b, c]);
        person.NextCommand.Execute(null);
        Assert.That(person.SelectedPane, Is.SameAs(b));
        person.NextCommand.Execute(null);
        Assert.That(person.SelectedPane, Is.SameAs(c));
        person.NextCommand.Execute(null);
        Assert.That(person.SelectedPane, Is.SameAs(a), "wraps from the last tab back to the first");
        person.PreviousCommand.Execute(null);
        Assert.That(person.SelectedPane, Is.SameAs(c), "and back the other way from the first to the last");
    }

    [Test, Apartment(ApartmentState.STA)]
    public void The_Launcher_s_own_menu_pane_shows_without_a_provider_bracket()
    {
        var menu = new TerminalPaneViewModel(new PaneRequest("RD: Launcher", temp.New()) { Repo = "Launcher", Provider = "Launcher", CommandLine = "cmd.exe", IsMenu = true, Initials = "RD" });
        Assert.That(menu.Name, Is.EqualTo("Launcher"));
        var cli = new TerminalPaneViewModel(new PaneRequest("RD: Repo [Claude]", temp.New()) { Repo = "Repo", CommandLine = "cmd.exe", Initials = "RD" });
        Assert.That(cli.Name, Is.EqualTo("Repo"), "nor a Claude CLI tab its provider tag (M.ORCH-T1176): every tab was one");
        Assert.That(menu.Badge, Is.EqualTo("RD:"), "the Launcher tab still carries its login's badge (M.ORCH-T1172)");
        Assert.That(cli.Badge, Is.EqualTo(""), "a CLI tab no longer repeats it: it already sits under that Launcher tab (M.ORCH-T1172)");
    }

    [Test]
    public void The_terminal_draws_in_Windows_Terminal_s_own_colors()
    {
        // Without a theme the renderer's 16-color table was never set: the old panes lost their colors (M.ORCH-T751).
        Assert.That(TerminalPaneViewModel.Ref(0xC50F1F), Is.EqualTo(0x1F0FC5u), "#RRGGBB as a COLORREF, 0x00BBGGRR");
        Assert.That(TerminalPaneViewModel.Campbell.ColorTable, Has.Length.EqualTo(16));
        Assert.That(TerminalPaneViewModel.Campbell.ColorTable.Distinct().Count(), Is.EqualTo(16));
        Assert.That(TerminalPaneViewModel.Campbell.DefaultBackground, Is.Not.EqualTo(TerminalPaneViewModel.Campbell.DefaultForeground));
    }

    [Test]
    public void The_badge_text_is_black_or_white_by_the_luminance_of_its_tab_color_chip()
    {
        foreach (var hex in new[] { "#6A1B9A", "#1565C0", "#AD1457", "#C62828", "#2E7D32" })
            Assert.That(TerminalPaneViewModel.ReadableOn(new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex))),
                Is.SameAs(System.Windows.Media.Brushes.White), hex);
        Assert.That(TerminalPaneViewModel.ReadableOn(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Yellow)), Is.SameAs(System.Windows.Media.Brushes.Black));
    }

    [Test]
    public void A_terminal_scrolled_part_way_out_of_the_page_is_clipped_to_what_s_still_inside()
    {
        var viewport = new Rect(100, 100, 800, 600);
        Assert.That(TerminalClip.Region(new Rect(150, 200, 400, 300), viewport), Is.Null, "wholly inside: no region");
        Assert.That(TerminalClip.Region(new Rect(150, 50, 400, 300), viewport), Is.EqualTo(new Int32Rect(0, 50, 400, 250)), "its top under the toolbar is cut");
        Assert.That(TerminalClip.Region(new Rect(150, 500, 400, 300), viewport), Is.EqualTo(new Int32Rect(0, 0, 400, 200)), "its bottom below the page is cut");
        Assert.That(TerminalClip.Region(new Rect(150, 800, 400, 300), viewport), Is.EqualTo(Int32Rect.Empty), "scrolled out of sight: nothing drawn");
    }

    [Test]
    public void The_panel_sits_between_the_person_s_Current_work_and_their_Tickets()
    {
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        var xaml = XDocument.Load(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement Named(string name) => xaml.Descendants().Single(e => (string?)e.Attribute(x + "Name") == name);
        // M.ORCH-T861: the person's terminal tabs, then their chosen terminal, are fixed rows right under the person tabs
        // (Root's rows 1 and 2), outside the scrolling body, so a native terminal window can never scroll over the tabs.
        var rootGrid = Named("Root");
        Assert.That(Named("TerminalsPanel").Parent, Is.SameAs(rootGrid));
        Assert.That((string?)Named("TerminalsPanel").Attribute("Grid.Row"), Is.EqualTo("1"));
        // Wrapped in a scroller row (M.ORCH-T1171, mirroring the login tabs' M.ORCH-T822), so its direct parent is that
        // row's ScrollViewer rather than TerminalsPanel itself; it must still live inside TerminalsPanel.
        Assert.That(Named("TerminalTabs").Ancestors().Contains(Named("TerminalsPanel")), Is.True);
        Assert.That(Named("TerminalFrameRow").Parent, Is.SameAs(rootGrid));
        Assert.That((string?)Named("TerminalFrameRow").Attribute("Grid.Row"), Is.EqualTo("2"));
        Assert.That(Named("TerminalFrame").Parent, Is.SameAs(Named("TerminalFrameRow")));
        var rows = rootGrid.Elements().Single(e => e.Name.LocalName == "Grid.RowDefinitions").Elements().Select(r => (string?)r.Attribute("Height")).ToList();
        Assert.That(rows[1], Is.EqualTo("Auto"));
        Assert.That(rows[2], Is.EqualTo("Auto"));
        Assert.That((string?)Named("AgentsBody").Attribute("Grid.Row"), Is.EqualTo(rows.IndexOf("*").ToString()), "everything under them scrolls");
        foreach (var name in new[] { "TerminalsPanel", "TerminalTabs", "TerminalFrame" })
            Assert.That(Named(name).Ancestors().Contains(Named("AgentsBody")), Is.False, name);
        // In the body: the stats, the toolbar, then the 5-hour chart and Tickets (M.ORCH-T826; no Current work board, M.ORCH-T837).
        var body = Named("AgentsBody").Elements().Single().Elements().Select(e => (string?)e.Attribute(x + "Name")).ToList();
        Assert.That(body.IndexOf("Toolbar"), Is.EqualTo(body.IndexOf("Header") + 1));
        Assert.That(body.IndexOf("UtilizationSection"), Is.EqualTo(body.IndexOf("Toolbar") + 1));
        Assert.That(body.IndexOf("UserTickets"), Is.EqualTo(body.IndexOf("UtilizationSection") + 1));
        Assert.That((string?)Named("TerminalTabs").Attribute("ItemsSource"), Is.EqualTo("{Binding Terminals.Panes}"));
        Assert.That(Named("TerminalPanesHost").Parent, Is.SameAs(Named("TerminalFrame")));
    }

    [Test, Apartment(ApartmentState.STA)]
    public void A_terminal_runs_its_command_in_a_real_terminal_and_closing_it_ends_the_process()
    {
        // The in-process fallback (no TermHost, M.ORCH-T926): the CLI runs under the dashboard's own ConPTY.
        var terminals = new TerminalsViewModel { UseTermHost = false };
        var host = new System.Windows.Controls.Grid();
        terminals.Panes.CollectionChanged += (_, e) =>
        {
            foreach (var p in e.NewItems?.OfType<TerminalPaneViewModel>() ?? []) host.Children.Add(p.Control);
            foreach (var p in e.OldItems?.OfType<TerminalPaneViewModel>() ?? []) host.Children.Remove(p.Control);
        };
        var window = OffScreenWindow.Show(600, 300, host);
        try
        {
            var opened = new List<TerminalPaneViewModel>();
            terminals.Opened += opened.Add;
            Assert.That(terminals.Open(new PaneRequest("JO: Repo [Claude]", temp.New()) { Command = ["cmd.exe", "/k", "echo pane"] }), Is.Null);
            var pane = terminals.Panes.Single();
            Assert.That(opened, Is.EqualTo(new[] { pane }), "the window brings it up on its person's page");
            Assert.That(terminals.Open(new PaneRequest("X", Path.Combine(temp.New(), "gone")) { Command = ["cmd.exe"] }), Does.StartWith("Folder not found"));

            Assert.That(Pump(() => pane.RootPid is not null, TimeSpan.FromSeconds(20)), Is.True, "ConPTY started its process");
            var pid = pane.RootPid!.Value;
            terminals.Close(pane);
            Assert.That(terminals.Panes, Is.Empty);
            Assert.That(Pump(() => !Alive(pid), TimeSpan.FromSeconds(10)), Is.True, "closing the tab ended its process");
        }
        finally { window.Close(); }
    }

    [Test, Apartment(ApartmentState.STA)]
    public void A_hosted_terminal_attaches_to_its_TermHost_and_closing_it_ends_the_host()
    {
        // M.ORCH-T926: the CLI lives in its own TermHost; the tab is a RelayTerminal on the host's pipe.
        if (TermLauncher.FindHost() is null) Assert.Ignore("No TermHost built or installed here.");
        var terminals = new TerminalsViewModel();
        var host = new System.Windows.Controls.Grid();
        terminals.Panes.CollectionChanged += (_, e) =>
        {
            foreach (var p in e.NewItems?.OfType<TerminalPaneViewModel>() ?? []) host.Children.Add(p.Control);
            foreach (var p in e.OldItems?.OfType<TerminalPaneViewModel>() ?? []) host.Children.Remove(p.Control);
        };
        var window = OffScreenWindow.Show(600, 300, host);
        try
        {
            Assert.That(terminals.Open(new PaneRequest("JO: Repo [Claude]", temp.New()) { Command = ["cmd.exe", "/k", "echo pane"] }), Is.Null);
            var pane = terminals.Panes.Single();
            Assert.That(pane.IsHosted, Is.True);
            Assert.That(pane.Control, Is.InstanceOf<TerminalView>());
            Assert.That(Pump(() => pane.Session is not null, TimeSpan.FromSeconds(20)), Is.True, "its TermHost registered");
            var session = pane.Session!;
            Assert.That(pane.RootPid, Is.EqualTo(session.HostPid), "the CLI runs under its host");
            Assert.That(TermPaneFile.Read(session.Id)?.Title, Is.EqualTo("JO: Repo [Claude]"), "the tab's look is kept for a reattach");
            Assert.That(Pump(() => pane.Relay?.Connection is not null, TimeSpan.FromSeconds(10)), Is.True, "the relay attached");

            terminals.Close(pane);
            Assert.That(terminals.Panes, Is.Empty);
            Assert.That(Pump(() => !Alive(session.HostPid), TimeSpan.FromSeconds(10)), Is.True, "closing the tab ended its host");
            Assert.That(TermPaneFile.Read(session.Id), Is.Null);
        }
        finally { window.Close(); }
    }

    [Test, Apartment(ApartmentState.STA)]
    public void A_host_that_can_t_start_leaves_the_tab_running_its_CLI_in_process()
    {
        var terminals = new TerminalsViewModel { HostAvailable = () => true, Launch = _ => throw new InvalidOperationException("no host") };
        var window = OffScreenWindow.Show(600, 300, new System.Windows.Controls.Grid());
        try
        {
            Assert.That(terminals.Open(new PaneRequest("JO: Repo [Claude]", temp.New()) { Command = ["cmd.exe", "/k", "echo pane"] }), Is.Null);
            var pane = terminals.Panes.Single();
            Assert.That(Pump(() => pane.Local is not null, TimeSpan.FromSeconds(10)), Is.True, "fell back to an in-process terminal");
            Assert.That(pane.IsHosted, Is.False);
            Assert.That(pane.TermId, Is.Null);
            terminals.Close(pane);
        }
        finally { window.Close(); }
    }

    [Test, Apartment(ApartmentState.STA)]
    public void A_failed_launch_whose_host_runs_anyway_is_adopted_by_its_tab_not_run_twice()
    {
        string? launchedId = null;
        var terminals = new TerminalsViewModel
        {
            HostAvailable = () => true,
            Launch = spec => { launchedId = spec.Id; throw new InvalidOperationException("TermHost --launch didn't finish"); },
            Lookup = id => new Term.TermSessionRecord { Id = id, HostPid = 999999, Pipe = "mindattic-term-nobody-" + id, CommandLine = "cmd.exe" },
            KillOrphan = _ => Assert.Fail("an adopted host is never ended"),
        };
        var window = OffScreenWindow.Show(600, 300, new System.Windows.Controls.Grid());
        try
        {
            Assert.That(terminals.Open(new PaneRequest("JO: Repo [Claude]", temp.New()) { Command = ["cmd.exe", "/k", "echo pane"] }), Is.Null);
            var pane = terminals.Panes.Single();
            Assert.That(Pump(() => pane.Session is not null, TimeSpan.FromSeconds(10)), Is.True, "adopted the host");
            Assert.That(pane.Session!.Id, Is.EqualTo(launchedId), "matched by the id the launch named");
            Assert.That(pane.Local, Is.Null, "no in-process copy");
            Assert.That(pane.IsHosted, Is.True);
            pane.DisposeControl();
            TermPaneFile.Delete(launchedId!);
        }
        finally { window.Close(); }
    }

    [Test, Apartment(ApartmentState.STA)]
    public void A_host_that_registers_after_its_tab_fell_back_is_ended_not_given_a_second_tab()
    {
        string? launchedId = null;
        var ended = new List<string>();
        var terminals = new TerminalsViewModel
        {
            HostAvailable = () => true,
            Launch = spec => { launchedId = spec.Id; throw new InvalidOperationException("no record"); },
            Lookup = _ => null,
            KillOrphan = s => { lock (ended) ended.Add(s.Id); },
            OrphanWait = TimeSpan.Zero,
            OrphanWatch = TimeSpan.Zero,
        };
        var window = OffScreenWindow.Show(600, 300, new System.Windows.Controls.Grid());
        try
        {
            Assert.That(terminals.Open(new PaneRequest("JO: Repo [Claude]", temp.New()) { Command = ["cmd.exe", "/k", "echo pane"] }), Is.Null);
            var pane = terminals.Panes.Single();
            Assert.That(Pump(() => pane.Local is not null, TimeSpan.FromSeconds(10)), Is.True, "fell back to an in-process terminal");
            var orphan = new Term.TermSessionRecord { Id = launchedId!, HostPid = 999999, Pipe = "mindattic-term-nobody-" + launchedId, CommandLine = "cmd.exe" };
            terminals.OnTermSession(new Term.TermSessionEvent(orphan, Term.TermSessionEvents.Started));
            Assert.That(terminals.Panes, Has.Count.EqualTo(1), "no second tab");
            Assert.That(ended, Is.EqualTo(new[] { launchedId }), "the orphan host was ended");
            terminals.Close(pane);
        }
        finally { window.Close(); }
    }

    [Test]
    public void A_host_not_registered_yet_counts_as_alive_while_its_process_runs_for_a_grace_period()
    {
        using var me = System.Diagnostics.Process.GetCurrentProcess();
        var unregistered = new Term.TermSessionRecord
        {
            Id = Term.TermProtocol.NewSessionId(), HostPid = me.Id, HostStartUtc = me.StartTime.ToUniversalTime(), Pipe = "mindattic-term-nobody-x", CommandLine = "cmd.exe",
        };
        var now = DateTime.UtcNow;
        Assert.That(TerminalPaneViewModel.HostAlive(unregistered, now, now.AddSeconds(5)), Is.True, "within the grace, its process runs");
        Assert.That(TerminalPaneViewModel.HostAlive(unregistered, now, now + TerminalPaneViewModel.RegisterGrace), Is.False, "past the grace");
        Assert.That(TerminalPaneViewModel.HostAlive(unregistered with { HostPid = 999999 }, now, now.AddSeconds(5)), Is.False, "its process is gone");
    }

    [Test, Apartment(ApartmentState.STA)]
    public void A_term_session_started_revives_an_ended_tab_with_the_same_id()
    {
        var terminals = new TerminalsViewModel();
        var session = new Term.TermSessionRecord
        {
            Id = "late123", HostPid = 999999, Pipe = "mindattic-term-nobody-late123", Title = "AM: Repo [Claude]", Cwd = temp.New(), CommandLine = "cmd.exe",
        };
        var pane = terminals.Reattach(session, new PaneRequest("AM: Repo [Claude]", session.Cwd!) { CommandLine = "cmd.exe" });
        Assert.That(pane, Is.Not.Null);
        pane!.Ended = true;
        var before = pane.Relay;
        terminals.OnTermSession(new Term.TermSessionEvent(session, Term.TermSessionEvents.Started));
        Assert.That(pane.Ended, Is.False, "revived");
        Assert.That(pane.Relay, Is.Not.SameAs(before), "attached afresh");
        Assert.That(terminals.Panes, Has.Count.EqualTo(1));
        pane.DisposeControl();
    }

    [Test, Apartment(ApartmentState.STA)]
    public void Opening_tabs_in_order_stamps_each_one_with_a_rising_position_that_a_reattach_keeps()
    {
        // M.ORCH-T1170: so a dashboard crash and restart puts a still-live session's tab back where it was, Open() stamps it
        // with its spot in the sequence, saved in its sidecar; Reattach must never overwrite that with a fresh one.
        var terminals = new TerminalsViewModel { HostAvailable = () => false };
        Assert.That(terminals.Open(new PaneRequest("A", temp.New()) { CommandLine = "cmd.exe" }), Is.Null);
        Assert.That(terminals.Open(new PaneRequest("B", temp.New()) { CommandLine = "cmd.exe" }), Is.Null);
        Assert.That(terminals.Open(new PaneRequest("C", temp.New()) { CommandLine = "cmd.exe" }), Is.Null);
        var (a, b, c) = (terminals.Panes[0], terminals.Panes[1], terminals.Panes[2]);
        Assert.That(a.Request.Order, Is.Not.Null);
        Assert.That(new[] { a, b, c }.Select(p => p.Request.Order), Is.Ordered.Ascending, "later opens always sort after earlier ones");
        foreach (var p in terminals.Panes.ToList()) terminals.Close(p);

        // A session reattached in some other order (arrival/registry order after a crash) still carries its original Order,
        // and sorting by it (as MainViewModel.Terminals.SyncTerminals does) restores the pre-crash tab strip.
        var fresh = new TerminalsViewModel();
        var reattachedC = fresh.Reattach(new Term.TermSessionRecord { Id = "c1", Pipe = "mindattic-term-nobody-c1", CommandLine = "cmd.exe", Cwd = temp.New() }, c.Request);
        var reattachedA = fresh.Reattach(new Term.TermSessionRecord { Id = "a1", Pipe = "mindattic-term-nobody-a1", CommandLine = "cmd.exe", Cwd = temp.New() }, a.Request);
        var reattachedB = fresh.Reattach(new Term.TermSessionRecord { Id = "b1", Pipe = "mindattic-term-nobody-b1", CommandLine = "cmd.exe", Cwd = temp.New() }, b.Request);
        Assert.That(new[] { reattachedC, reattachedA, reattachedB }.OrderBy(p => p!.Request.Order), Is.EqualTo(new[] { reattachedA, reattachedB, reattachedC }));
        reattachedA!.DisposeControl();
        reattachedB!.DisposeControl();
        reattachedC!.DisposeControl();
    }

    [Test, Apartment(ApartmentState.STA)]
    public void Without_a_TermHost_a_tab_runs_its_CLI_in_process()
    {
        var terminals = new TerminalsViewModel { HostAvailable = () => false };
        Assert.That(terminals.Open(new PaneRequest("JO: Repo [Claude]", temp.New()) { Command = ["cmd.exe"] }), Is.Null);
        var pane = terminals.Panes.Single();
        Assert.That(pane.Local, Is.Not.Null);
        Assert.That(pane.IsHosted, Is.False);
        terminals.Close(pane);
    }

    [Test, Apartment(ApartmentState.STA)]
    public void A_live_session_is_reattached_once_on_its_login_s_page_and_a_bus_message_marks_it_ended()
    {
        var terminals = new TerminalsViewModel();
        var session = new Term.TermSessionRecord
        {
            Id = "abc123", HostPid = 999999, Pipe = "mindattic-term-nobody-abc123", Title = "AM: Repo [Claude]", Cwd = temp.New(),
            ConfigDir = @"C:\a\.claude-accounts\amy@example.com", CommandLine = "cmd.exe",
        };
        var request = new PaneRequest("AM: Repo [Claude]", session.Cwd!) { Repo = "Repo", Initials = "AM", TabColor = "#1565C0", ConfigDir = session.ConfigDir, CommandLine = "cmd.exe" };
        var pane = terminals.Reattach(session, request);
        Assert.That(pane, Is.Not.Null);
        Assert.That(pane!.IsHosted, Is.True);
        Assert.That(pane.TermId, Is.EqualTo("abc123"));
        Assert.That(pane.Login, Is.EqualTo("amy@example.com"));
        Assert.That(pane.Badge, Is.EqualTo(""), "a CLI tab carries no badge: only the Launcher's own tab does (M.ORCH-T1172)");
        Assert.That(terminals.Reattach(session, request), Is.Null, "one tab per session");

        var named = terminals.Reattach(session with { Id = "def456", Pipe = "mindattic-term-nobody-def456" }, request with { Alias = "Amy" });
        Assert.That(named!.Badge, Is.EqualTo(""), "still no badge on a CLI tab, named or not (M.ORCH-T1172)");
        named.DisposeControl();

        terminals.OnTermSession(new Term.TermSessionEvent(session, Term.TermSessionEvents.Ended));
        Assert.That(pane.Ended, Is.True);
        pane.DisposeControl();
    }

    [Test]
    public void A_session_without_a_tab_file_gets_its_badge_provider_and_login_from_its_record()
    {
        var record = new Term.TermSessionRecord
        {
            Id = "s1", Title = "RD: MindAttic.Ideas [Codex]", Cwd = @"C:\repo", CommandLine = "cmd.exe",
            ConfigDir = @"C:\u\.claude-accounts\ryan@example.com",
        };
        var request = TermPaneFile.FromRecord(record, _ => new LoginLook("Ryan", "RY", "#AD1457"));
        Assert.That(request.Initials, Is.EqualTo("RD"), "the title's badge");
        Assert.That(request.Repo, Is.EqualTo("MindAttic.Ideas"));
        Assert.That(request.Provider, Is.EqualTo("Codex"));
        Assert.That(request.TabColor, Is.EqualTo("#AD1457"), "the login's colour");
        Assert.That(request.Login, Is.EqualTo("ryan@example.com"));
        Assert.That(request.Directory, Is.EqualTo(@"C:\repo"));

        var plain = TermPaneFile.FromRecord(record with { Title = "pwsh" }, _ => new LoginLook("Ryan", "RY", "#AD1457"));
        Assert.That(plain.Initials, Is.EqualTo("RY"), "no badge in the title: the login's");
    }

    [Test]
    public void A_tab_file_round_trips_and_is_pruned_once_its_session_is_gone()
    {
        var dir = temp.New();
        var request = new PaneRequest("RD: Repo [Claude]", @"C:\repo") { Repo = "Repo", Initials = "RD", TabColor = "#2E7D32", CommandLine = "cmd.exe" };
        Assert.That(TermPaneFile.Write("live1", request, dir), Is.True);
        Assert.That(TermPaneFile.Write("gone1", request, dir), Is.True);
        Assert.That(TermPaneFile.Read("live1", dir), Is.EqualTo(request with { Command = null }));
        File.SetLastWriteTimeUtc(TermPaneFile.PathOf("gone1", dir), DateTime.UtcNow.AddMinutes(-5));
        File.SetLastWriteTimeUtc(TermPaneFile.PathOf("live1", dir), DateTime.UtcNow.AddMinutes(-5));
        TermPaneFile.Prune(["live1"], dir);
        Assert.That(TermPaneFile.Read("live1", dir), Is.Not.Null);
        Assert.That(TermPaneFile.Read("gone1", dir), Is.Null);
    }

    [Test]
    public void A_navigation_key_is_sent_as_win32_input_mode_key_down_then_up()
    {
        // Phase 0 lesson 3 (M.ORCH-T926): ESC[Vk;Sc;Uc;Kd;Cs;Rc_ with ENHANCED_KEY (256), plus SHIFT_PRESSED (16) when held.
        Assert.That(TerminalKeyboard.Win32Key(0x25, 0x4B, shift: false), Is.EqualTo("\u001b[37;75;0;1;256;1_\u001b[37;75;0;0;256;1_"));
        Assert.That(TerminalKeyboard.Win32Key(0x24, 0x47, shift: true), Is.EqualTo("\u001b[36;71;0;1;272;1_\u001b[36;71;0;0;272;1_"));
    }

    [Test, Apartment(ApartmentState.STA)]
    public void A_gated_window_key_matches_nothing_while_a_terminal_holds_focus()
    {
        // M.ORCH-T1108: a matching KeyBinding marks Esc handled even when its command can't run, so the gesture stands aside.
        var focused = true;
        var gesture = new TerminalKeyboard.GatedGesture(new KeyGesture(Key.Escape), _ => focused);
        using var source = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("esc"));
        var esc = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Escape) { RoutedEvent = Keyboard.KeyDownEvent };
        var target = new System.Windows.Controls.Grid();
        Assert.That(gesture.Matches(target, esc), Is.False, "the terminal's: Esc goes on to the CLI");
        focused = false;
        Assert.That(gesture.Matches(target, esc), Is.True, "elsewhere the window's Esc still acts");
    }

    [Test]
    public void Bracketed_paste_is_followed_from_the_last_mode_change_in_the_output()
    {
        Assert.That(TerminalPaste.Scan("x\u001b[?2004hy"), Is.True);
        Assert.That(TerminalPaste.Scan("\u001b[?2004h..\u001b[?2004l"), Is.False);
        Assert.That(TerminalPaste.Scan("plain"), Is.Null);
        Assert.That(TerminalPaste.Payload("a\r\nb\nc", bracketed: true), Is.EqualTo("\u001b[200~a\rb\rc\u001b[201~"));
    }

    [Test, Apartment(ApartmentState.STA)]
    public void A_paste_or_copy_chord_finds_its_tab_from_the_key_s_target_not_the_keyboard_focus()
    {
        // While the renderer has Win32 focus WPF's Keyboard.FocusedElement is null; the key is routed to the renderer itself
        // (M.ORCH-T1109), so the chord's tab is the one that holds its original source.
        var pane = new TerminalPaneViewModel(new PaneRequest("RD: Repo [Claude]", temp.New()) { Repo = "Repo", CommandLine = "cmd.exe" }, hosted: true);
        // KeyEventArgs needs a real PresentationSource: a hidden message-only HwndSource is enough.
        using var source = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("keys") { ParentWindow = new IntPtr(-3) });
        var e = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.V) { RoutedEvent = Keyboard.KeyDownEvent, Source = pane.Relay!.Terminal };
        Assert.That(TerminalPaste.Focused(e), Is.SameAs(pane));
        var elsewhere = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.V) { RoutedEvent = Keyboard.KeyDownEvent, Source = new System.Windows.Controls.TextBox() };
        Assert.That(TerminalPaste.Focused(elsewhere), Is.Null, "a key aimed anywhere else is no terminal's");
    }

    [Test, Apartment(ApartmentState.STA)]
    public void A_key_from_a_hyperlink_is_no_terminals_and_never_throws()
    {
        // bughunt 2026-10-27: a ContentElement (a ticket Hyperlink) made VisualTreeHelper.GetParent throw on every key.
        var link = new System.Windows.Documents.Hyperlink();
        _ = new System.Windows.Controls.TextBlock(link);
        Assert.That(TerminalPaneViewModel.Of(link), Is.Null);
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
