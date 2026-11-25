using System.Xml.Linq;
using MindAttic.Orchestrator.Wpf.ViewModels;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The sidebar's ticket cards (M.ORCH-T1439, M.ORCH-T1770) are gone (M.ORCH-T2202); the Queue tabs' own groups
/// remain.</summary>
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
    public void The_sidebar_has_no_tickets_panel()
    {
        // Since M.ORCH-T2176 the sidebar is an MaPanelGroup of titled panels, each with a stable PanelKey. Its Tickets panel
        // (Active, Pending, Top priority cards) is gone (M.ORCH-T2202): the Queue tabs list the tickets.
        var doc = Window();
        var top = Named(doc, "TopRow");
        Assert.That(top.Name.LocalName, Is.EqualTo("MaPanelGroup"));
        var keys = top.Elements().Where(e => e.Name.LocalName == "MaPanel")
            .Select(p => p.Attributes().SingleOrDefault(a => a.Name.LocalName == "MaPanelGroup.PanelKey")?.Value).ToList();
        Assert.That(keys, Is.EqualTo(new[] { "orgs", "users", "host" }));
        Assert.That((string?)Named(doc, "LoginTabScroller").Attribute("MaxHeight"), Does.Contain("Converters.Share").And.Contain("ElementName=TopRow"),
            "the logins are capped");
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
