using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using MindAttic.Components.Wpf.Controls;
using MindAttic.Orchestrator.Wpf;
using MindAttic.Orchestrator.Wpf.ViewModels;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>A switch between people's tabs is instant (M.ORCH-T654): the window shows another tab's view model, so every list
/// whose collection differs between tabs is torn down and rebuilt. The workspace's lists are one set for every tab, and a
/// person's own lists are bound only while their view shows.</summary>
public sealed class TabSwitchTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    [TestCase(nameof(MainViewModel.Queue))]
    [TestCase(nameof(MainViewModel.Proposals))]
    public void The_workspaces_lists_are_not_a_tab_of_their_own(string property)
    {
        // An auto-property would give each tab its own collection: a 761-ticket queue took 2s to rebuild on every switch.
        var backing = typeof(MainViewModel).GetField($"<{property}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(typeof(MainViewModel).GetProperty(property), Is.Not.Null);
        Assert.That(backing, Is.Null, $"{property} is read from the global login's tab, not held by each tab");
    }

    [Test]
    public void The_ticket_link_command_is_the_global_tabs()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Wpf", "ViewModels", "MainViewModel.cs"));
        Assert.That(source, Does.Contain("OpenInTicketsCommand = root?.OpenInTicketsCommand ??"));
    }

    [Test]
    public void A_persons_page_has_no_agent_board_repos_queue_audit_or_accounts_view()
    {
        // M.ORCH-T763: the Orchestrator shows only what's running; Tickets Desktop owns the Repos, the Queue and the audit. The
        // Current work agent card board is gone too (M.ORCH-T837): each CLI is its own terminal tab.
        var doc = XDocument.Load(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        // The Agents panel under each login in the tree (M.ORCH-T1747) is the one place Agents is bound; it is not a board.
        Assert.That(doc.Descendants().Where(e => (string?)e.Attribute("ItemsSource") == "{Binding Agents}")
            .All(e => e.Ancestors().Any(a => (string?)a.Attribute(X + "Name") == "LoginTreeAgents")), Is.True, "no agent board outside the login tree's Agents panel");
        foreach (var gone in new[] { "AgentCards", "WorkHeading", "ReposBody", "DetailPane", "QueueView", "QueueList", "AuditBody", "AccountsBody", "LoginCardList", "SearchBox" })
            Assert.That(doc.Descendants().Any(e => (string?)e.Attribute(X + "Name") == gone), Is.False, gone);
        Assert.That(doc.Descendants().Any(e => (string?)e.Attribute("ItemsSource") == "{Binding ViewChoices}"), Is.False, "no view pills");
        Assert.That(typeof(MainViewModel).GetProperty("ViewChoice"), Is.Null);
    }

    [Test]
    public void The_person_strip_lists_the_shown_orgs_tabs_under_the_org_switcher()
    {
        var doc = XDocument.Load(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        var strip = doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == "LoginTabStrip");
        var orgs = doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == "OrgStrip");
        Assert.That((string?)strip.Attribute("ItemsSource"), Is.EqualTo("{Binding OrgTabs}"));
        Assert.That((string?)orgs.Attribute("ItemsSource"), Is.EqualTo("{Binding Orgs}"));
        Assert.That((string?)orgs.Attribute("Style"), Is.EqualTo("{StaticResource MaPillChoice}"));
        // M.ORCH-T822: the strip now sits inside a ScrollViewer (paged by < and > buttons once it's wider than the window),
        // so it's no longer OrgStrip's own sibling; compare document order instead.
        var names = doc.Descendants().Select(e => (string?)e.Attribute(X + "Name")).ToList();
        Assert.That(names.IndexOf("LoginTabStrip"), Is.GreaterThan(names.IndexOf("OrgStrip")), "the orgs above the people");

        // Both kept on the global login's tab, which holds every org and tab: the window's DataContext is the tab shown.
        var code = File.ReadAllText(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml.cs"));
        Assert.That(code, Does.Contain("OrgStrip.DataContext = model;").And.Contain("LoginTabStrip.DataContext = model;"));

        // The org's lists are the global tab's, one set: an org switch hands the window the same collections.
        foreach (var property in new[] { nameof(MainViewModel.Orgs), nameof(MainViewModel.OrgTabs) })
            Assert.That(typeof(MainViewModel).GetField($"<{property}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic), Is.Null, property);
    }

    [Test, Apartment(ApartmentState.STA)]
    public void A_virtualized_body_builds_only_the_rows_and_lines_of_tiles_on_screen()
    {
        _ = System.IO.Packaging.PackUriHelper.UriSchemePack; // registers pack:// outside an Application
        var theme = (ResourceDictionary)Application.LoadComponent(new Uri("/MindAttic.Components.Wpf;component/Theme.xaml", UriKind.Relative));
        var heading = new TextBlock { Text = "Agents" };
        var cards = new ItemsControl { Style = (Style)theme["MaTileList"], ItemsSource = Enumerable.Range(0, 400).Select(i => $"card {i}").ToList() };
        MaList.SetMinItemWidth(cards, 190);
        var rows = new ItemsControl { Style = (Style)theme["MaRowList"], ItemsSource = Enumerable.Range(0, 600).Select(i => $"row {i}").ToList() };
        var body = new ScrollViewer { Content = new StackPanel { Children = { heading, cards, rows } } };
        body.Resources.MergedDictionaries.Add(theme);
        VirtualizedBody? made = MindAttic.Orchestrator.Wpf.VirtualizedList.ApplyBody(body);
        Assert.That(made, Is.Not.Null);

        void Layout()
        {
            for (var i = 0; i < 3; i++)
            {
                body.Measure(new Size(800, 600));
                body.Arrange(new Rect(0, 0, 800, 600));
                body.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background); // the body's scheduled rebuild
            }
        }
        List<string> Built() => [.. Descendants(body).OfType<TextBlock>().Select(t => t.Text)];
        Layout();

        var built = Built();
        Assert.That(built, Does.Contain("Agents").And.Contain("card 0"));
        Assert.That(built.Count(t => t.StartsWith("card ")), Is.InRange(1, 150), "only the lines of tiles on screen (and the panel's cache), of 400");
        Assert.That(built.Any(t => t.StartsWith("row ")), Is.False, "the rows are far below");
        // Each line of tiles holds as many as the library's grid fits the width, so the tiles keep their columns.
        var line = Descendants(body).OfType<MaTileGrid>().First();
        Assert.That(line.Children.Count, Is.EqualTo(MaTileGrid.ColumnsFor(line.ActualWidth, 400, 190, 10, false)).And.GreaterThan(1));

        // Another tab's collection: rebuilt, still only a screenful.
        cards.ItemsSource = Enumerable.Range(0, 3).Select(i => $"other {i}").ToList();
        Layout();
        built = Built();
        Assert.That(built.Any(t => t.StartsWith("card ")), Is.False);
        Assert.That(built, Does.Contain("other 2").And.Contain("row 0"));
        Assert.That(built.Count(t => t.StartsWith("row ")), Is.InRange(1, 150), "only the rows on screen (and the panel's cache), of 600");

        // A list hidden takes its rows with it.
        rows.Visibility = Visibility.Collapsed;
        Layout();
        Assert.That(Built().Any(t => t.StartsWith("row ")), Is.False);
    }

    [Test, Apartment(ApartmentState.STA)]
    public void A_nested_virtualized_body_never_realizes_or_derealizes_what_comes_before_it()
    {
        // M.ORCH-T1302: AgentsBody wraps the chosen terminal (a native HwndHost that must never be torn down just because
        // it scrolled out of view) ahead of any virtualized list sharing its scroll. ApplyBody(body, content) must virtualize
        // content's rows alone, leaving the plain sibling ahead of it in body's own StackPanel exactly as untouched as any
        // ordinary WPF layout scrolled out of a ScrollViewer's viewport: still a real element, never removed from the visual
        // tree. (As of M.ORCH-T2238, MainWindow's own AgentsBody holds only the terminal and no virtualized list at all --
        // this test exercises the generic ApplyBody mechanism in isolation, which other bodies may still use.)
        _ = System.IO.Packaging.PackUriHelper.UriSchemePack; // registers pack:// outside an Application
        var theme = (ResourceDictionary)Application.LoadComponent(new Uri("/MindAttic.Components.Wpf;component/Theme.xaml", UriKind.Relative));
        var pinned = new TextBlock { Text = "pinned" };
        var rows = new ItemsControl { Style = (Style)theme["MaRowList"], ItemsSource = Enumerable.Range(0, 600).Select(i => $"row {i}").ToList() };
        var nested = new StackPanel { Children = { rows } };
        var body = new ScrollViewer { Content = new StackPanel { Children = { pinned, nested } } };
        body.Resources.MergedDictionaries.Add(theme);
        VirtualizedBody? made = MindAttic.Orchestrator.Wpf.VirtualizedList.ApplyBody(body, nested);
        Assert.That(made, Is.Not.Null);

        void Layout()
        {
            for (var i = 0; i < 3; i++)
            {
                body.Measure(new Size(800, 600));
                body.Arrange(new Rect(0, 0, 800, 600));
                body.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background); // the body's scheduled rebuild
            }
        }
        Layout();
        Assert.That(Descendants(body).Contains(pinned), Is.True, "pinned realized up front");
        Assert.That(Built(body).Any(t => t.StartsWith("row ")), Is.True, "the rows nearest the top are built too");

        // Scroll the rows far down, well past where a flat virtualized body would derealize an item this far from the top.
        body.ScrollToVerticalOffset(20000);
        Layout();
        Assert.That(Descendants(body).Contains(pinned), Is.True, "still never torn down, only scrolled out of the viewport");
        Assert.That(Built(body).Any(t => t.StartsWith("row ")), Is.True, "rows now on screen are built");
    }

    [Test, Apartment(ApartmentState.STA)]
    public void A_nested_virtualized_body_keeps_the_body_scrolling_with_its_gutters_and_scroll_bar()
    {
        // M.ORCH-T1326: a person's page (AgentsBody) holds the terminal ahead of the virtualized list. Disabling the body's own
        // scrolling there left the page unable to scroll past the terminal to the 5-hour chart, with no scroll bar or gutter.
        _ = System.IO.Packaging.PackUriHelper.UriSchemePack;
        var theme = (ResourceDictionary)Application.LoadComponent(new Uri("/MindAttic.Components.Wpf;component/Theme.xaml", UriKind.Relative));
        var pinned = new Border { Height = 500 }; // the terminal's stand-in: most of the first screen
        var rows = new ItemsControl { Style = (Style)theme["MaRowList"], ItemsSource = Enumerable.Range(0, 40).Select(i => $"row {i}").ToList() };
        var nested = new StackPanel { Children = { rows } };
        var body = new ScrollViewer { Style = (Style)theme["MaBodyScroll"], Content = new StackPanel { Children = { pinned, nested } } };
        body.Resources.MergedDictionaries.Add(theme);
        MindAttic.Orchestrator.Wpf.VirtualizedList.ApplyBody(body, nested);

        Assert.That(body.VerticalScrollBarVisibility, Is.Not.EqualTo(ScrollBarVisibility.Disabled), "the body still scrolls");
        Assert.That(MindAttic.Components.Wpf.Controls.MaScrollArea.GetGutter(body).Left, Is.EqualTo(24), "and keeps its gutters");

        for (var i = 0; i < 3; i++)
        {
            body.Measure(new Size(800, 600));
            body.Arrange(new Rect(0, 0, 800, 600));
            body.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
        }
        Assert.That(body.ScrollableHeight, Is.GreaterThan(0), "terminal plus rows overflow the viewport, so the body can scroll");
        Assert.That(body.ComputedVerticalScrollBarVisibility, Is.EqualTo(Visibility.Visible), "with a scroll bar showing");
        body.ScrollToVerticalOffset(400);
        body.UpdateLayout();
        Assert.That(body.VerticalOffset, Is.GreaterThan(0), "scrolling moves past the terminal");
    }

    [Test, Apartment(ApartmentState.STA)]
    public void A_whole_virtualized_body_still_hands_its_scrolling_to_the_list()
    {
        // The flat case (the list is the body's whole content) is unchanged: the list scrolls and pads itself.
        _ = System.IO.Packaging.PackUriHelper.UriSchemePack;
        var theme = (ResourceDictionary)Application.LoadComponent(new Uri("/MindAttic.Components.Wpf;component/Theme.xaml", UriKind.Relative));
        var rows = new ItemsControl { Style = (Style)theme["MaRowList"], ItemsSource = new[] { "a", "b" } };
        var body = new ScrollViewer { Content = new StackPanel { Children = { rows } } };
        body.Resources.MergedDictionaries.Add(theme);
        MindAttic.Orchestrator.Wpf.VirtualizedList.ApplyBody(body);
        Assert.That(body.VerticalScrollBarVisibility, Is.EqualTo(ScrollBarVisibility.Disabled));
        Assert.That(MindAttic.Components.Wpf.Controls.MaScrollArea.GetGutter(body), Is.EqualTo(new Thickness(0)));
    }

    [Test, Apartment(ApartmentState.STA)]
    public void The_terminals_own_scroll_bar_is_collapsed_so_the_CLI_runs_flush_with_the_tab_bar()
    {
        // M.ORCH-T1325: the terminal control's empty "scrollbar" column left the CLI 10px short of the tab bar on the right.
        var bar = new System.Windows.Controls.Primitives.ScrollBar { Name = "scrollbar", Orientation = Orientation.Vertical };
        var other = new System.Windows.Controls.Primitives.ScrollBar { Name = "other", Orientation = Orientation.Vertical };
        var terminal = new Grid { Children = { new Border(), bar, other } };
        terminal.Measure(new Size(400, 300));
        terminal.Arrange(new Rect(0, 0, 400, 300));
        Assert.That(MindAttic.Orchestrator.Term.Wpf.TerminalScrollbar.Collapse(terminal), Is.True);
        Assert.That(bar.Visibility, Is.EqualTo(Visibility.Collapsed));
        Assert.That(other.Visibility, Is.EqualTo(Visibility.Visible), "only the terminal's own bar");
    }

    private static List<string> Built(DependencyObject root) => [.. Descendants(root).OfType<TextBlock>().Select(t => t.Text)];

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var d in Descendants(child)) yield return d;
        }
    }

    private static string RepoRoot()
    {
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        return root;
    }
}
