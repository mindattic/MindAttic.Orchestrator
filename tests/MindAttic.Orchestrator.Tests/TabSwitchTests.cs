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
    [TestCase(nameof(MainViewModel.Finished))]
    [TestCase(nameof(MainViewModel.Duplicates))]
    [TestCase(nameof(MainViewModel.Drift))]
    [TestCase(nameof(MainViewModel.Missing))]
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

    [TestCase("AgentCards", "{Binding AgentsInView}")]
    [TestCase("LoginCardList", "{Binding LoginCardsInView}")]
    public void A_persons_own_lists_are_bound_only_while_their_view_shows(string name, string binding)
    {
        var doc = XDocument.Load(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        var list = doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);
        Assert.That((string?)list.Attribute("ItemsSource"), Is.EqualTo(binding));

        var repos = doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == "ReposBody");
        Assert.That(repos.Descendants().Select(e => (string?)e.Attribute("ItemsSource")), Has.Member("{Binding GroupsInView}").And.No.Member("{Binding Groups}"));
    }

    [Test]
    public void The_queue_is_a_virtualized_list_so_a_switch_builds_only_the_rows_on_screen()
    {
        // M.ORCH-T655: a 9-person and a 2-person org switch equally fast; the queue's rows are built only as they scroll in.
        var doc = XDocument.Load(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        var list = doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == "QueueList");
        Assert.That(list.Name.LocalName, Is.EqualTo("ItemsControl"), "a library-styled list; LibraryOnlyXamlTests allows no control of the dashboard's own");
        Assert.That((string?)list.Attribute("Style"), Is.EqualTo("{StaticResource MaList}"));
        Assert.That((string?)list.Attribute("ItemsSource"), Is.EqualTo("{Binding Queue}"));
        Assert.That(list.Ancestors().Any(e => e.Name.LocalName == "ScrollViewer"), Is.False, "inside another scroll view every row would be built");
        Assert.That(File.ReadAllText(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml.cs")),
            Does.Contain("VirtualizedList.Apply(QueueList"));

        // The list scrolls itself, by its panel (logical scrolling), or it isn't virtualized.
        var code = File.ReadAllText(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Wpf", "VirtualizedList.cs"));
        Assert.That(code, Does.Contain("ScrollViewer.CanContentScrollProperty, true").And.Contain("VirtualizationMode.Recycling")
            .And.Contain("typeof(VirtualizingStackPanel)"));
        // Its template is set on the list itself: as a style's setter, the theme's ItemsControl style replaced it with one that
        // has no scroll view, and every one of 405 rows was built on each org switch (measured: 1.1s, then 4 rows and 100ms).
        Assert.That(code, Does.Contain("list.Template ="));
        Assert.That(MindAttic.Orchestrator.Wpf.MainWindow.QueueHeaderHeight(280), Is.EqualTo(126), "the queue keeps most of a short view");
        Assert.That(MindAttic.Orchestrator.Wpf.MainWindow.QueueHeaderHeight(100), Is.EqualTo(120));
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
        Assert.That(orgs.ElementsAfterSelf().Contains(strip), Is.True, "the orgs above the people");

        // Both kept on the global login's tab, which holds every org and tab: the window's DataContext is the tab shown.
        var code = File.ReadAllText(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml.cs"));
        Assert.That(code, Does.Contain("OrgStrip.DataContext = model;").And.Contain("LoginTabStrip.DataContext = model;"));

        // The org's lists are the global tab's, one set: an org switch hands the window the same collections.
        foreach (var property in new[] { nameof(MainViewModel.Orgs), nameof(MainViewModel.OrgTabs) })
            Assert.That(typeof(MainViewModel).GetField($"<{property}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic), Is.Null, property);
    }

    [TestCase("ReposBody")]
    [TestCase("AgentsBody")]
    [TestCase("AccountsBody")]
    [TestCase("AuditBody")]
    public void Every_other_view_is_one_virtualized_list_too(string body)
    {
        // M.ORCH-T754: the Repos, Agents (with each person's Tickets), Accounts and Audit views build only what's on screen, so a
        // 9-person and a 2-person org switch equally fast: each body's lists sit straight in it, the body made one list in code.
        var doc = XDocument.Load(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        var view = doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == body);
        var content = view.Elements().Single();
        Assert.That(content.Name.LocalName, Is.EqualTo("StackPanel"), "a vertical panel whose children become the list's items");
        var lists = view.Descendants().Where(e => e.Name.LocalName == "ItemsControl" && !e.Ancestors().Any(a => a.Name.LocalName == "DataTemplate")).ToList();
        Assert.That(lists, Is.Not.Empty);
        Assert.That(lists.Select(l => l.Parent), Is.All.EqualTo(content), "a list nested in a panel of its own would build every row");
        Assert.That(File.ReadAllText(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml.cs")),
            Does.Contain($"VirtualizedList.ApplyBody({body})"));
    }

    [Test]
    public void Each_persons_tickets_sit_below_the_agent_cards_not_in_them()
    {
        var doc = XDocument.Load(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        foreach (var name in new[] { "UserTickets", "AssignedTicketList", "UnassignedTicketList" })
        {
            var element = doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);
            Assert.That(element.Ancestors().Any(a => a.Name.LocalName == "DataTemplate"), Is.False, $"{name} was inside every agent card's template");
            Assert.That((string?)element.Attribute("Visibility"), Is.EqualTo("{Binding ShowsUserTickets, Converter={StaticResource Visible}}"), name);
        }
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
