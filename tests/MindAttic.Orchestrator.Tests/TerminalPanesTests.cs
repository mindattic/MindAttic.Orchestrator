using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml.Linq;
using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Term.Wpf;
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

    /// <summary>M.ORCH-T1446: the pinned Usage tab is never one of <see cref="PersonTerminalsViewModel.Panes"/> (it isn't a
    /// CLI's pane), so choosing it only swaps what the terminal area shows; the chosen CLI underneath is remembered, and
    /// choosing any real tab again -- even the one already chosen, which raises no SelectedPane change on its own -- leaves it.</summary>
    [Test, Apartment(ApartmentState.STA)]
    public void Choosing_the_pinned_Usage_tab_hides_the_terminal_without_touching_the_chosen_pane_or_Panes()
    {
        var all = new TerminalsViewModel();
        var person = new PersonTerminalsViewModel(all);
        TerminalPaneViewModel Pane(string repo) =>
            new(new PaneRequest(repo, temp.New()) { Repo = repo, CommandLine = "cmd.exe", ConfigDir = @"C:\a\.claude-accounts\amy@example.com" });
        var (a, b) = (Pane("A"), Pane("B"));
        var changes = 0;
        person.SelectionChanged += () => changes++;

        person.SyncTo([a, b]);
        // Chosen here, not left to SyncTo (which chooses the newest of tabs arriving together): this is about the Usage tab.
        person.SelectedPane = a;
        changes = 0;
        Assert.That(person.SelectedPane, Is.SameAs(a));
        Assert.That(person.ShowsTerminal, Is.True);
        Assert.That(person.IsUsageShown, Is.False);

        person.ShowUsageCommand.Execute(null);
        Assert.That(person.IsUsageShown, Is.True);
        Assert.That(person.ShowsTerminal, Is.False, "the terminal area shows the charts instead");
        Assert.That(person.SelectedPane, Is.SameAs(a), "the chosen CLI is remembered, not cleared");
        Assert.That(person.Panes, Is.EqualTo(new[] { a, b }), "the pinned tab is never one of the real panes");
        Assert.That(changes, Is.EqualTo(1));

        person.ShowUsageCommand.Execute(null);
        Assert.That(changes, Is.EqualTo(1), "showing it again while already shown is a no-op");

        person.SelectedPane = a; // the real strip's own already-selected tab clicked again
        Assert.That(person.IsUsageShown, Is.False, "choosing a real CLI tab, even the one already chosen, leaves the pinned tab");
        Assert.That(person.ShowsTerminal, Is.True);
        Assert.That(changes, Is.EqualTo(2));

        person.ShowUsageCommand.Execute(null);
        person.NextCommand.Execute(null); // cycling to a different real tab leaves it too
        Assert.That(person.IsUsageShown, Is.False);
        Assert.That(person.SelectedPane, Is.SameAs(b));
    }

    /// <summary>M.ORCH-T1446: the terminal area never shows while there's no real pane open, whether or not the pinned
    /// Usage tab was last chosen -- there's nothing for either of them to replace.</summary>
    [Test, Apartment(ApartmentState.STA)]
    public void The_terminal_area_stays_hidden_with_no_panes_whichever_tab_was_last_chosen()
    {
        var all = new TerminalsViewModel();
        var person = new PersonTerminalsViewModel(all);
        Assert.That(person.HasPanes, Is.False);
        Assert.That(person.ShowsTerminal, Is.False);
        person.ShowUsageCommand.Execute(null);
        Assert.That(person.ShowsTerminal, Is.False, "nothing to show either way");
    }

    // ---- drag-and-drop reordering (M.ORCH-T1526) ----------------------------------------------------

    [Test, Apartment(ApartmentState.STA)]
    public void Reorder_moves_the_pane_and_leaves_the_chosen_one_untouched()
    {
        var all = new TerminalsViewModel();
        var person = new PersonTerminalsViewModel(all);
        TerminalPaneViewModel Pane(string repo) => new(new PaneRequest(repo, temp.New()) { Repo = repo, CommandLine = "cmd.exe" });
        var (a, b, c) = (Pane("A"), Pane("B"), Pane("C"));
        person.SyncTo([a, b, c]);
        person.SelectedPane = b;

        Assert.That(person.Reorder(a, 2), Is.True);

        Assert.That(person.Panes, Is.EqualTo(new[] { b, c, a }));
        Assert.That(person.SelectedPane, Is.SameAs(b), "reordering never changes which tab is chosen");
    }

    [Test, Apartment(ApartmentState.STA)]
    public void Reorder_is_a_no_op_for_a_pane_that_is_not_this_person_s_or_an_index_already_its_own()
    {
        var all = new TerminalsViewModel();
        var person = new PersonTerminalsViewModel(all);
        TerminalPaneViewModel Pane(string repo) => new(new PaneRequest(repo, temp.New()) { Repo = repo, CommandLine = "cmd.exe" });
        var (a, b) = (Pane("A"), Pane("B"));
        person.SyncTo([a, b]);

        Assert.That(person.Reorder(Pane("elsewhere"), 0), Is.False);
        Assert.That(person.Reorder(a, 0), Is.False, "already at index 0");
        Assert.That(person.Panes, Is.EqualTo(new[] { a, b }), "neither no-op moved anything");
    }

    [Test, Apartment(ApartmentState.STA)]
    public void After_a_reorder_every_pane_s_Order_is_strictly_ascending_in_the_new_order()
    {
        var all = new TerminalsViewModel();
        var person = new PersonTerminalsViewModel(all);
        TerminalPaneViewModel Pane(string repo) => new(new PaneRequest(repo, temp.New()) { Repo = repo, CommandLine = "cmd.exe" });
        var (a, b, c) = (Pane("A"), Pane("B"), Pane("C"));
        person.SyncTo([a, b, c]);

        Assert.That(person.Reorder(c, 0), Is.True);

        Assert.That(person.Panes, Is.EqualTo(new[] { c, a, b }));
        Assert.That(person.Panes.Select(p => p.Request.Order), Is.Ordered.Ascending);
    }

    [Test, Apartment(ApartmentState.STA)]
    public void A_reordered_pane_s_Order_round_trips_through_its_tab_file()
    {
        var dir = temp.New();
        var all = new TerminalsViewModel { PaneDirectory = dir };
        var person = new PersonTerminalsViewModel(all);
        TerminalPaneViewModel Pane(string repo, string id)
        {
            var pane = new TerminalPaneViewModel(new PaneRequest(repo, temp.New()) { Repo = repo, CommandLine = "cmd.exe" }, hosted: true) { TermId = id };
            TermPaneFile.Write(id, pane.Request, dir);
            return pane;
        }
        var (a, b) = (Pane("A", "a1"), Pane("B", "b1"));
        person.SyncTo([a, b]);

        Assert.That(person.Reorder(b, 0), Is.True);

        Assert.That(TermPaneFile.Read("b1", dir)!.Order, Is.EqualTo(b.Request.Order));
        Assert.That(TermPaneFile.Read("a1", dir)!.Order, Is.EqualTo(a.Request.Order));
        Assert.That(b.Request.Order!.Value, Is.LessThan(a.Request.Order!.Value));
        a.DisposeControl();
        b.DisposeControl();
    }

    [Test, Apartment(ApartmentState.STA)]
    public void A_reorder_survives_a_resync_because_it_is_stamped_into_Order_not_just_moved_in_Panes()
    {
        // Regression for the whole point of Restamp: SyncTerminals (MainViewModel.Terminals.cs) re-derives each
        // person's tab order from AllTerminals.Panes.OrderBy(Request.Order) on every resync (any login's tab
        // opening or closing) - a reorder that only called ObservableCollection.Move without also restamping
        // Order would silently revert the next time that happens.
        var all = new TerminalsViewModel();
        var person = new PersonTerminalsViewModel(all);
        TerminalPaneViewModel Pane(string repo) => new(new PaneRequest(repo, temp.New()) { Repo = repo, CommandLine = "cmd.exe" });
        var (a, b, c) = (Pane("A"), Pane("B"), Pane("C"));
        person.SyncTo([a, b, c]);

        Assert.That(person.Reorder(c, 0), Is.True);
        var resynced = new[] { a, b, c }.OrderBy(p => p.Request.Order ?? long.MaxValue).ToArray();

        Assert.That(resynced, Is.EqualTo(new[] { c, a, b }), "the drag's order, not the original open order");
    }

    [Test, Apartment(ApartmentState.STA)]
    public void The_Launcher_s_own_tab_is_an_ordinary_pane_to_Reorder_no_longer_pinned_first()
    {
        var all = new TerminalsViewModel();
        var person = new PersonTerminalsViewModel(all);
        var menu = new TerminalPaneViewModel(new PaneRequest("RD: Launcher", temp.New()) { Repo = "Launcher", Provider = "Launcher", CommandLine = "cmd.exe", IsMenu = true, Initials = "RD" });
        TerminalPaneViewModel Pane(string repo) => new(new PaneRequest(repo, temp.New()) { Repo = repo, CommandLine = "cmd.exe" });
        var cli = Pane("Repo");
        person.SyncTo([menu, cli]);

        Assert.That(person.Reorder(menu, 1), Is.True, "the Launcher's tab can be dragged like any other");

        Assert.That(person.Panes, Is.EqualTo(new[] { cli, menu }));
    }

    [Test, Apartment(ApartmentState.STA)]
    public void The_Launcher_s_own_menu_pane_shows_without_a_provider_bracket()
    {
        var menu = new TerminalPaneViewModel(new PaneRequest("RD: Launcher", temp.New()) { Repo = "Launcher", Provider = "Launcher", CommandLine = "cmd.exe", IsMenu = true, Initials = "RD" });
        Assert.That(menu.Name, Is.EqualTo("Launcher"));
        var cli = new TerminalPaneViewModel(new PaneRequest("RD: Repo [Claude]", temp.New()) { Repo = "Repo", CommandLine = "cmd.exe", Initials = "RD" });
        Assert.That(cli.Name, Is.EqualTo("Repo"), "nor a Claude CLI tab its provider tag (M.ORCH-T1176): every tab was one");
    }

    [Test, Apartment(ApartmentState.STA)]
    public void A_redeploying_placeholder_reads_redeploying_even_once_its_inert_relay_marks_it_ended()
    {
        // M.ORCH-T1511: the "redeploying…" placeholder that stands in the Launcher's slot while MindAttic.Launcher rebuilds
        // must still read "redeploying…", not "ended", however its own (never-attached) state settles.
        var placeholder = new TerminalPaneViewModel(new PaneRequest("RD: Launcher", temp.New()) { Repo = "Launcher", Provider = "Launcher", CommandLine = "cmd.exe", IsMenu = true, Initials = "RD" }, hosted: true)
        { Redeploying = true };
        Assert.That(placeholder.StateText, Is.EqualTo("redeploying…"));
        placeholder.Ended = true;
        Assert.That(placeholder.StateText, Is.EqualTo("redeploying…"), "Redeploying still wins over Ended");
        placeholder.Redeploying = false;
        Assert.That(placeholder.StateText, Is.EqualTo("ended"), "once redeploying is over, the usual state shows again");
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
        // M.ORCH-T1302: only the person's CLI tab bar is a fixed row right under the person tabs (Root's row 2, after the menu
        // bar's own row 0 and the login tabs' row 1, M.ORCH-T1499); the chosen terminal scrolls with everything else in
        // AgentsBody (row 3), a plain sibling of AgentsBodyList rather than one of its virtualized rows, so a native terminal
        // window is never torn down just because it scrolled out of view (TerminalClip.cs keeps it clipped to AgentsBody's own
        // viewport instead).
        var rootGrid = Named("Root");
        Assert.That(Named("TerminalsPanel").Parent, Is.SameAs(rootGrid));
        Assert.That((string?)Named("TerminalsPanel").Attribute("Grid.Row"), Is.EqualTo("2"));
        // Wrapped in a scroller row (M.ORCH-T1171, mirroring the login tabs' M.ORCH-T822), so its direct parent is that
        // row's ScrollViewer rather than TerminalsPanel itself; it must still live inside TerminalsPanel.
        Assert.That(Named("TerminalTabs").Ancestors().Contains(Named("TerminalsPanel")), Is.True);
        var rows = rootGrid.Elements().Single(e => e.Name.LocalName == "Grid.RowDefinitions").Elements().Select(r => (string?)r.Attribute("Height")).ToList();
        Assert.That(rows[2], Is.EqualTo("Auto"), "the CLI tab bar's own row");
        Assert.That((string?)Named("AgentsBody").Attribute("Grid.Row"), Is.EqualTo(rows.IndexOf("*").ToString()), "everything under the CLI tab bar scrolls");
        foreach (var name in new[] { "TerminalsPanel", "TerminalTabs" })
            Assert.That(Named(name).Ancestors().Contains(Named("AgentsBody")), Is.False, $"{name} stays fixed");
        Assert.That(Named("TerminalFrame").Ancestors().Contains(Named("AgentsBody")), Is.True, "the chosen terminal scrolls with the body");
        Assert.That(Named("TerminalFrame").Ancestors().Contains(Named("AgentsBodyList")), Is.False, "never one of AgentsBodyList's virtualized rows");
        // The pinned Usage tab's own page (M.ORCH-T1532): a plain sibling of TerminalFrame, not one of AgentsBodyList's
        // virtualized rows either, holding the 5-hour/weekly chart that used to open AgentsBodyList inline.
        Assert.That(Named("UsageFrame").Ancestors().Contains(Named("AgentsBody")), Is.True, "the Usage tab's page scrolls with the body");
        Assert.That(Named("UsageFrame").Ancestors().Contains(Named("AgentsBodyList")), Is.False, "never one of AgentsBodyList's virtualized rows");
        // In AgentsBodyList: the stats first (the chart moved into UsageFrame, M.ORCH-T1532), then the toolbar and Tickets.
        // The pinned limit pills are gone (they rendered as blank boxes, M.ORCH-T1318).
        var body = Named("AgentsBodyList").Elements().Select(e => (string?)e.Attribute(x + "Name")).ToList();
        Assert.That(body.IndexOf("Header"), Is.Zero, "the stats come right after the CLI terminal, the chart now in its own Usage tab page");
        Assert.That(body, Has.No.Member("PinnedLimitBars"));
        Assert.That(body, Has.No.Member("UtilizationSection"), "moved into UsageFrame, a sibling of TerminalFrame");
        Assert.That(body.IndexOf("Toolbar"), Is.EqualTo(body.IndexOf("Header") + 1));
        Assert.That(body.IndexOf("UserTickets"), Is.EqualTo(body.IndexOf("Toolbar") + 1));
        // No side margin of its own: the page body's 24px gutters place the CLI in line with the CLI tab bar (M.ORCH-T1326 keeps
        // those gutters on a person's page); a margin here would double them or push it off the page (M.ORCH-T1321).
        Assert.That((string?)Named("TerminalFrame").Attribute("Margin"), Is.EqualTo("{StaticResource SpaceTS}"));
        // M.ORCH-T1569: a plain binding to the view model's strip list (the pinned Usage tab, then Terminals.Panes); see
        // The_pinned_Usage_tab_sits_first_inside_the_shared_tab_control for its own shape.
        Assert.That((string?)Named("TerminalTabs").Attribute("ItemsSource"), Is.EqualTo("{Binding Terminals.StripItems}"));
        Assert.That(Named("TerminalPanesHost").Parent, Is.SameAs(Named("TerminalFrame")));
    }

    /// <summary>M.ORCH-T1555: the pinned Usage tab, the Launcher's own tab and every CLI tab share one mc:MaTabControl
    /// (TerminalTabs) now -- Usage used to be a separate MaTabItem bolted on beside it (M.ORCH-T1446), wired with its own
    /// MouseLeftButtonDown/KeyDown handlers instead of the control's own selection, which is what left it inert. It's the
    /// first item of a CompositeCollection (PersonTerminalsViewModel.UsageItems ahead of Terminals.Panes). Its own look
    /// (locked in place, no close button) comes from one shared, unkeyed ItemContainerStyle/ItemTemplate with a
    /// DataTrigger that switches what's set for the shared UsageTab marker, not a DataTemplateSelector/StyleSelector of
    /// the dashboard's own, which M.ORCH-T667's library-only rule (LibraryOnlyXamlTests) doesn't allow.</summary>
    [Test]
    public void The_pinned_Usage_tab_sits_first_inside_the_shared_tab_control()
    {
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        var xaml = XDocument.Load(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement Named(string name) => xaml.Descendants().Single(e => (string?)e.Attribute(x + "Name") == name);

        var tabs = Named("TerminalTabs");
        // M.ORCH-T1569: no XAML CompositeCollection (its {Binding} container could lose its panes); the view model's own list.
        Assert.That((string?)tabs.Attribute("ItemsSource"), Is.EqualTo("{Binding Terminals.StripItems}"));
        Assert.That(tabs.Descendants().Any(e => e.Name.LocalName == "CompositeCollection"), Is.False);

        var containerStyle = tabs.Elements().Single(e => e.Name.LocalName == "MaTabControl.ItemContainerStyle").Elements().Single(e => e.Name.LocalName == "Style");
        var usageTrigger = containerStyle.Descendants().Single(e => e.Name.LocalName == "DataTrigger");
        Assert.That(usageTrigger.Elements().Any(s => (string?)s.Attribute("Property") == "IsLocked" && (string?)s.Attribute("Value") == "True"), Is.True,
            "locked: drag-to-reorder can never move it or drop another tab before it");

        var itemTemplate = tabs.Elements().Single(e => e.Name.LocalName == "MaTabControl.ItemTemplate").Elements().Single(e => e.Name.LocalName == "DataTemplate");
        var usageHeader = itemTemplate.Descendants().Single(e => (string?)e.Attribute(x + "Name") == "UsageTabHeader");
        Assert.That(usageHeader.Descendants().Any(e => e.Name.LocalName == "Button"), Is.False, "no close button: it can't be closed");
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
            Assert.That(terminals.Open(new PaneRequest("JO: Repo [Claude]", temp.New()) { Command = ["cmd.exe", "/k", "echo pane"], Unlisted = true }), Is.Null);
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
    public void A_letterboxed_viewer_reclaims_the_size_when_its_own_row_grows()
    {
        // M.ORCH-T1266: a viewer that loses the size to another one (M.ORCH-T930 — a second window on the same session, a
        // redeploy's dashboard racing a still-live one to reattach, M.ORCH-T926) letterboxes. Before this fix, only focus
        // (Terminal.GotFocus, M.ORCH-T930 spike section 3) reclaimed it; a pane nobody clicked into stayed letterboxed
        // forever even as MainWindow.SizeTerminalRow grew its row (M.ORCH-T1208's taller floor made the dead gap around it
        // obvious). It must reclaim the size on its own once its own hosting row actually grows, with no click into it.
        // Two viewers are used, rather than asserting who wins attaching first: B's own TakeSize() deterministically takes the
        // size away from A, so this test only ever depends on A reclaiming it back by resizing. The session is unlisted
        // (M.ORCH-T1444) precisely so a live Orchestrator on this machine can no longer see its registry entry and race it as
        // a third viewer (M.ORCH-T971).
        if (TermLauncher.FindHost() is null) Assert.Ignore("No TermHost built or installed here.");
        var terminalsA = new TerminalsViewModel();
        var hostA = new System.Windows.Controls.Grid();
        terminalsA.Panes.CollectionChanged += (_, e) => { foreach (var p in e.NewItems?.OfType<TerminalPaneViewModel>() ?? []) hostA.Children.Add(p.Control); };
        var windowA = OffScreenWindow.Show(600, 300, hostA);
        try
        {
            Assert.That(terminalsA.Open(new PaneRequest("RD: Repo [Claude]", temp.New()) { Repo = "Repo", Command = ["cmd.exe", "/k", "echo pane"], Unlisted = true }), Is.Null);
            var paneA = terminalsA.Panes.Single();
            paneA.IsSelected = true;
            Assert.That(Pump(() => paneA.Session is not null, TimeSpan.FromSeconds(20)), Is.True, "its TermHost registered");
            Assert.That(Pump(() => paneA.Relay?.Connection is not null, TimeSpan.FromSeconds(10)), Is.True, "the first viewer attached");
            var session = paneA.Session!;

            var terminalsB = new TerminalsViewModel();
            var hostB = new System.Windows.Controls.Grid();
            terminalsB.Panes.CollectionChanged += (_, e) => { foreach (var p in e.NewItems?.OfType<TerminalPaneViewModel>() ?? []) hostB.Children.Add(p.Control); };
            var windowB = OffScreenWindow.Show(200, 120, hostB);
            try
            {
                var paneB = terminalsB.Reattach(session, paneA.Request);
                Assert.That(paneB, Is.Not.Null, "a second viewer can attach to the same host");
                Assert.That(Pump(() => paneB!.Relay?.Connection is not null, TimeSpan.FromSeconds(10)), Is.True, "the second viewer attached");

                paneB!.Relay!.Connection!.TakeSize();
                Assert.That(Pump(() => paneA.Relay!.Connection!.OwnsSize == false, TimeSpan.FromSeconds(10)), Is.True, "A letterboxes once B takes the size");
                Assert.That(Pump(() => paneA.Relay!.Terminal.AutoResize == false, TimeSpan.FromSeconds(10)), Is.True, "AutoResize off while letterboxed");

                // The hosting row growing, never a click into the pane: no focus call anywhere in this test.
                windowA.Width = 1400;
                windowA.Height = 1000;
                Assert.That(Pump(() => paneA.Relay!.Connection!.OwnsSize, TimeSpan.FromSeconds(10)), Is.True, "growing its row reclaims the size on its own");
                Assert.That(Pump(() => paneA.Relay!.Terminal.AutoResize, TimeSpan.FromSeconds(10)), Is.True, "autosizing resumes, closing the gap");
            }
            finally { windowB.Close(); }

            terminalsA.Close(paneA);
        }
        finally { windowA.Close(); }
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
        Assert.That(terminals.Reattach(session, request), Is.Null, "one tab per session");

        var named = terminals.Reattach(session with { Id = "def456", Pipe = "mindattic-term-nobody-def456" }, request with { Alias = "Amy" });
        Assert.That(named, Is.Not.Null);
        named!.DisposeControl();

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
        // KeyGesture.Matches checks its Modifiers against the real, ambient Keyboard.Modifiers (Win32 GetKeyState), not a
        // fake: on a box where a modifier is actually held when this runs, a gesture built with ModifierKeys.None would never
        // match, failing the "still acts" assertion below for a reason that has nothing to do with the gate (M.ORCH-T1307).
        var focused = true;
        var gesture = new TerminalKeyboard.GatedGesture(new KeyGesture(Key.Escape, Keyboard.Modifiers), _ => focused);
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

    /// <summary>M.ORCH-T1272: the CLI tab's own status badge (spinner/?/zzz) from its matched AgentBoardRow, the CLI tab's
    /// own version of the login tab's aggregate one (M.ORCH-T1270); none of the three until its agent is known.</summary>
    [Test, Apartment(ApartmentState.STA)]
    public void A_CLI_tab_s_own_status_badge_follows_its_matched_agent_row()
    {
        var pane = new TerminalPaneViewModel(new PaneRequest("RD: Repo [Claude]", temp.New()) { Repo = "Repo", CommandLine = "cmd.exe" }, hosted: true);
        Assert.That(pane.ShowsPaneBusyBadge, Is.False);
        Assert.That(pane.ShowsPaneAskingBadge, Is.False);
        Assert.That(pane.ShowsPaneIdleBadge, Is.False, "no agent matched yet: none of the three badges");

        AgentBoardRow Row(AgentState state, bool asking) => new("s", "Repo", null, null, DateTime.UtcNow, DateTime.UtcNow, state, "", false, default, []) { Asking = asking };

        pane.ApplyAgentStatus(Row(AgentState.Working, asking: false), DateTime.UtcNow);
        Assert.That(pane.ShowsPaneBusyBadge, Is.True);
        Assert.That(pane.ShowsPaneAskingBadge, Is.False);
        Assert.That(pane.ShowsPaneIdleBadge, Is.False);

        pane.ApplyAgentStatus(Row(AgentState.Waiting, asking: true), DateTime.UtcNow);
        Assert.That(pane.ShowsPaneBusyBadge, Is.False);
        Assert.That(pane.ShowsPaneAskingBadge, Is.True, "asking wins over idle");
        Assert.That(pane.ShowsPaneIdleBadge, Is.False);

        pane.ApplyAgentStatus(Row(AgentState.Waiting, asking: false), DateTime.UtcNow);
        Assert.That(pane.ShowsPaneBusyBadge, Is.False);
        Assert.That(pane.ShowsPaneAskingBadge, Is.False);
        Assert.That(pane.ShowsPaneIdleBadge, Is.True);

        pane.ApplyAgentStatus(null, DateTime.UtcNow);
        Assert.That(pane.ShowsPaneIdleBadge, Is.False, "unmatched again: back to none of the three");
    }

    /// <summary>M.ORCH-T1413: Enter lights the CLI tab busy at once; a status read taken before the Enter, or too soon after it,
    /// can't take that back; a later one still reading idle does; one reading busy hands over to the real signal.</summary>
    [Test, Apartment(ApartmentState.STA)]
    public void Enter_presumes_a_CLI_tab_busy_until_a_later_status_read_says_otherwise()
    {
        var pane = new TerminalPaneViewModel(new PaneRequest("RD: Repo [Claude]", temp.New()) { Repo = "Repo", CommandLine = "cmd.exe" }, hosted: true);
        AgentBoardRow Row(AgentState state, bool asking = false) => new("s", "Repo", null, null, DateTime.UtcNow, DateTime.UtcNow, state, "", false, default, []) { Asking = asking };
        var t0 = new DateTime(2026, 11, 2, 18, 0, 0, DateTimeKind.Utc);
        pane.ApplyAgentStatus(Row(AgentState.Waiting, asking: true), t0.AddSeconds(-30));

        pane.Presume(t0);
        Assert.That(pane.ShowsPaneBusyBadge, Is.True, "lit the moment Enter was typed");
        Assert.That(pane.ShowsPaneAskingBadge, Is.False, "the Enter answered the question");
        Assert.That(pane.ShowsPaneIdleBadge, Is.False);

        pane.ApplyAgentStatus(Row(AgentState.Waiting), t0.AddSeconds(-1));
        Assert.That(pane.IsPresumedBusy, Is.True, "a read taken before the Enter can't end it");
        pane.ApplyAgentStatus(Row(AgentState.Waiting), t0 + TerminalPaneViewModel.PresumeConfirmAfter - TimeSpan.FromSeconds(1));
        Assert.That(pane.IsPresumedBusy, Is.True, "nor one taken before the gate could mark it busy");
        pane.ApplyAgentStatus(Row(AgentState.Waiting), t0 + TerminalPaneViewModel.PresumeConfirmAfter);
        Assert.That(pane.IsPresumedBusy, Is.False);
        Assert.That(pane.ShowsPaneIdleBadge, Is.True, "a later read still idle: nothing started");

        pane.Presume(t0.AddMinutes(1));
        pane.ApplyAgentStatus(Row(AgentState.Working), t0.AddMinutes(1).AddSeconds(1));
        Assert.That(pane.IsPresumedBusy, Is.False, "the real signal took over");
        Assert.That(pane.ShowsPaneBusyBadge, Is.True);
    }

    /// <summary>M.ORCH-T1443: a CLI closed right after Enter, before any status read landed, must not keep its login's badge
    /// green for up to PresumeLife after it's already gone -- Ended ends the presumption at once.</summary>
    [Test, Apartment(ApartmentState.STA)]
    public void Ending_a_pane_clears_a_presumption_with_no_status_read_yet()
    {
        var pane = new TerminalPaneViewModel(new PaneRequest("RD: Repo [Claude]", temp.New()) { Repo = "Repo", CommandLine = "cmd.exe" }, hosted: true);
        pane.Presume(DateTime.UtcNow);
        Assert.That(pane.IsPresumedBusy, Is.True);

        pane.Ended = true;
        Assert.That(pane.IsPresumedBusy, Is.False, "the CLI is gone; nothing left to presume busy");
        Assert.That(pane.ShowsPaneBusyBadge, Is.False);
    }

    [Test]
    public void Only_a_plain_Enter_counts_as_a_submit()
    {
        Assert.That(RelayInput.IsSubmit("\r"), Is.True);
        Assert.That(RelayInput.IsSubmit("\u001b[13;28;13;1;0;1_"), Is.True, "win32-input-mode Enter down");
        Assert.That(RelayInput.IsSubmit("\u001b[13;28;13;0;0;1_"), Is.False, "key up");
        Assert.That(RelayInput.IsSubmit("\u001b[13;28;13;1;16;1_"), Is.False, "Shift+Enter is a new line");
        Assert.That(RelayInput.IsSubmit("\u001b[65;30;97;1;0;1_"), Is.False, "a letter");
        Assert.That(RelayInput.IsSubmit("\u001b[200~a\rb\u001b[201~"), Is.False, "a paste's line breaks");
        Assert.That(RelayInput.IsSubmit("a"), Is.False);
    }

    /// <summary>M.ORCH-T1407 (bughunt 2026-11-02): a tick where SessionOf misses (a transient process-tree read failure, or the
    /// context gate's agent rows not caught up yet) must not blank an already-known CLI's badge back to "unknown" -- it keeps
    /// matching the pane's own last-known session instead.</summary>
    [Test]
    public void The_badges_session_falls_back_to_the_panes_own_known_one_when_this_ticks_lookup_misses()
    {
        Assert.That(TerminalsViewModel.BadgeSessionOf("fresh", "known"), Is.EqualTo("fresh"), "a fresh match always wins");
        Assert.That(TerminalsViewModel.BadgeSessionOf(null, "known"), Is.EqualTo("known"), "a miss falls back to what's already known");
        Assert.That(TerminalsViewModel.BadgeSessionOf("", "known"), Is.EqualTo("known"), "empty counts as a miss too");
        Assert.That(TerminalsViewModel.BadgeSessionOf(null, null), Is.Null, "nothing known yet: still nothing");
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
