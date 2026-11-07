using System.Xml.Linq;
using MindAttic.Orchestrator.Wpf.ViewModels;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The sidebar lists the shown person's Active, then Pending tickets as cards under the logins, each opening in
/// Tickets Desktop (M.ORCH-T1439); the logins keep their room and the cards take the rest (the window tools are in the menu
/// bar since M.ORCH-T1499).</summary>
public sealed class SidebarTicketsTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Window()
    {
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        return XDocument.Load(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
    }

    private static XElement Named(XDocument doc, string name) => doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);

    [Test]
    public void The_cards_take_the_rest_of_the_sidebar_under_the_logins()
    {
        // The window tools moved out of the sidebar to the top menu bar (M.ORCH-T1499), so the sidebar is orgs, logins, then
        // the cards (M.ORCH-T1503).
        var doc = Window();
        var top = Named(doc, "TopRow");
        Assert.That(top.Attribute("VerticalAlignment"), Is.Null, "it fills the sidebar so the last row can take the rest");
        var rows = top.Elements().Single(e => e.Name.LocalName == "Grid.RowDefinitions").Elements().Select(r => (string?)r.Attribute("Height")).ToList();
        Assert.That(rows, Is.EqualTo(new[] { "Auto", "Auto", "*" }));
        Assert.That((string?)Named(doc, "LoginTabScroller").Attribute("MaxHeight"), Does.Contain("Converters.Share").And.Contain("ElementName=TopRow"),
            "the logins are capped, so they never push the cards off");
        var area = Named(doc, "SidebarTicketArea");
        Assert.That(area.Parent, Is.EqualTo(top));
        Assert.That((string?)area.Attribute("Grid.Row"), Is.EqualTo("2"));
        Assert.That(area.Name.LocalName, Is.EqualTo("Canvas"), "wants no width of its own, so a title never widens the column");

        var list = Named(doc, "SidebarTickets");
        Assert.That((string?)list.Attribute("KeyboardNavigation.TabNavigation"), Is.EqualTo("Once"));
        Assert.That((string?)list.Attribute("KeyboardNavigation.DirectionalNavigation"), Is.EqualTo("Contained"));
        Assert.That((string?)Named(doc, "SidebarActiveTickets").Attribute("ItemsSource"), Is.EqualTo("{Binding ActiveTab.Tickets}"));
        Assert.That((string?)Named(doc, "SidebarPendingTickets").Attribute("ItemsSource"), Is.EqualTo("{Binding PendingTab.Tickets}"));
        Assert.That(list.ToString().IndexOf("SidebarActiveTickets", StringComparison.Ordinal),
            Is.LessThan(list.ToString().IndexOf("SidebarPendingTickets", StringComparison.Ordinal)), "Active first");

        var card = Named(doc, "SidebarTicketCard");
        Assert.That(card.Name.LocalName, Is.EqualTo("Button"), "a click, Enter or Space opens it");
        Assert.That((string?)card.Attribute("Command"), Does.Contain("OpenInTicketsCommand"), "the same command as every ticket link");
        Assert.That((string?)card.Attribute("CommandParameter"), Is.EqualTo("{Binding Id}"));
        Assert.That((string?)card.Attribute("AutomationProperties.Name"), Is.EqualTo("{Binding OpenName}"));
    }

    [Test]
    public void An_empty_group_says_so()
    {
        var active = new QueueTabViewModel(QueueStatus.Active, "Active");
        Assert.That(active.HasTickets, Is.False);
        Assert.That(active.EmptyText, Is.EqualTo("No active tickets"));
        Assert.That(new QueueTabViewModel(QueueStatus.Pending, "Pending").EmptyText, Is.EqualTo("No pending tickets"));
    }

    [Test]
    public void Share_is_a_fraction_of_a_length()
    {
        var share = Converters.Share;
        Assert.That(share.Convert(800.0, typeof(double), "0.5", System.Globalization.CultureInfo.InvariantCulture), Is.EqualTo(400.0));
        Assert.That(share.Convert(0.0, typeof(double), "0.5", System.Globalization.CultureInfo.InvariantCulture), Is.EqualTo(double.PositiveInfinity), "no cap before layout");
    }
}
