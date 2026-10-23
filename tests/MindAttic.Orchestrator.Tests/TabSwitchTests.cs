using System.Reflection;
using System.Xml.Linq;
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
        Assert.That(list.Name.LocalName, Is.EqualTo(nameof(MindAttic.Orchestrator.Wpf.VirtualList)));
        Assert.That((string?)list.Attribute("ItemsSource"), Is.EqualTo("{Binding Queue}"));
        Assert.That((string?)list.Attribute("VirtualizingPanel.IsVirtualizing"), Is.EqualTo("True"));
        Assert.That(list.Descendants().Any(e => e.Name.LocalName == "VirtualizingStackPanel"), Is.True);
        Assert.That(list.Ancestors().Any(e => e.Name.LocalName == "ScrollViewer"), Is.False, "inside another scroll view every row would be built");

        // The list scrolls itself, by its panel (logical scrolling), or it isn't virtualized.
        var code = File.ReadAllText(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Wpf", "VirtualList.cs"));
        Assert.That(code, Does.Contain("ScrollViewer.CanContentScrollProperty, true").And.Contain("VirtualizationMode.Recycling"));
        // Its template is set on the list itself: as default metadata, the theme's ItemsControl style replaced it with one that
        // has no scroll view, and every one of 405 rows was built on each org switch (measured: 1.1s, then 4 rows and 100ms).
        Assert.That(code, Does.Contain("Template = Scrolling;"));
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

    private static string RepoRoot()
    {
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        return root;
    }
}
