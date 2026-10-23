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

    private static string RepoRoot()
    {
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        return root;
    }
}
