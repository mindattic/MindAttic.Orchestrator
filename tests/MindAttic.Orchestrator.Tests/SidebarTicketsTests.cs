using System.Xml.Linq;
using MindAttic.Orchestrator.Wpf.ViewModels;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The sidebar lists the shown person's Active, then Pending, then the top-priority tickets as cards under the
/// logins (M.ORCH-T1439, restructured by M.ORCH-T1770 to unify tasks and tickets into one Active group and add the
/// top-priority backlog); the logins keep their room and the cards take the rest (the window tools are in the menu bar
/// since M.ORCH-T1499).</summary>
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
        // Since M.ORCH-T2176 the sidebar is an MaPanelGroup of titled panels, each with a stable PanelKey, and the Tickets
        // panel fills what the others leave.
        var top = Named(doc, "TopRow");
        Assert.That(top.Name.LocalName, Is.EqualTo("MaPanelGroup"));
        Assert.That(top.Attribute("VerticalAlignment"), Is.Null, "it fills the sidebar so the last panel can take the rest");
        var keys = top.Elements().Where(e => e.Name.LocalName == "MaPanel")
            .Select(p => p.Attributes().SingleOrDefault(a => a.Name.LocalName == "MaPanelGroup.PanelKey")?.Value).ToList();
        Assert.That(keys, Is.EqualTo(new[] { "orgs", "users", "host", "tickets" }));
        Assert.That((string?)Named(doc, "LoginTabScroller").Attribute("MaxHeight"), Does.Contain("Converters.Share").And.Contain("ElementName=TopRow"),
            "the logins are capped, so they never push the cards off");
        var area = Named(doc, "SidebarTicketArea");
        Assert.That(area.Parent!.Name.LocalName, Is.EqualTo("MaPanel"));
        Assert.That(area.Parent.Parent, Is.EqualTo(top));
        Assert.That(area.Parent.Attributes().Single(a => a.Name.LocalName == "MaPanelGroup.FillsRemainingSpace").Value, Is.EqualTo("True"));
        Assert.That(area.Name.LocalName, Is.EqualTo("Canvas"), "wants no width of its own, so a title never widens the column");

        var list = Named(doc, "SidebarTickets");
        Assert.That((string?)list.Attribute("KeyboardNavigation.TabNavigation"), Is.EqualTo("Once"));
        Assert.That((string?)list.Attribute("KeyboardNavigation.DirectionalNavigation"), Is.EqualTo("Contained"));
        // The unified Active section (M.ORCH-T1770) mixes the Task List with this login's own open tickets, so it binds a
        // mixed collection with no explicit ItemTemplate: WPF picks each row's DataTemplate by its own runtime type.
        var active = Named(doc, "SidebarActiveItems");
        Assert.That((string?)active.Attribute("ItemsSource"), Is.EqualTo("{Binding ActiveItems}"));
        Assert.That(active.Attribute("ItemTemplate"), Is.Null, "mixed task/ticket rows: each picks its own template by type");
        Assert.That((string?)Named(doc, "SidebarPendingTickets").Attribute("ItemsSource"), Is.EqualTo("{Binding PendingTab.Tickets}"));
        Assert.That((string?)Named(doc, "SidebarTop100Tickets").Attribute("ItemsSource"), Is.EqualTo("{Binding Top100Items}"));
        Assert.That(list.ToString().IndexOf("SidebarActiveItems", StringComparison.Ordinal),
            Is.LessThan(list.ToString().IndexOf("SidebarPendingTickets", StringComparison.Ordinal)), "Active first");
        Assert.That(list.ToString().IndexOf("SidebarPendingTickets", StringComparison.Ordinal),
            Is.LessThan(list.ToString().IndexOf("SidebarTop100Tickets", StringComparison.Ordinal)), "Pending before the top-priority backlog");

        // Each card is a Border (M.ORCH-T1770), not a button: its two independent click targets are its own Play and
        // Ticket buttons (jump to its CLI, or open it in Tickets), not the whole row.
        var card = Named(doc, "SidebarTicketCard");
        Assert.That(card.Name.LocalName, Is.EqualTo("Border"), "the row itself isn't a click target any more");
        var playButton = Named(doc, "SidebarTicketPlayButton");
        Assert.That((string?)playButton.Attribute("Command"), Does.Contain("JumpToCliCommand"));
        var openButton = Named(doc, "SidebarTicketOpenButton");
        Assert.That((string?)openButton.Attribute("Command"), Does.Contain("OpenInTicketsCommand"), "the same command as every ticket link");
        Assert.That((string?)openButton.Attribute("CommandParameter"), Is.EqualTo("{Binding Id}"));
        Assert.That((string?)openButton.Attribute("AutomationProperties.Name"), Is.EqualTo("{Binding OpenName}"));
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
