using System.Xml.Linq;
using MindAttic.Orchestrator.Tickets;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>Every ticket id the dashboard lists links to Tickets Desktop, which opens on that ticket (M.ORCH-T500).</summary>
public sealed class TicketsDesktopTests
{
    [TestCase("M.ORCH-T500", true)]
    [TestCase("AUT-T1", true)]
    [TestCase("m.orch-t5", true)]
    [TestCase("U.SPC-T316", true)]
    [TestCase("", false)]
    [TestCase(null, false)]
    [TestCase("M.ORCH-T", false)]
    [TestCase("MAO-12", false)]
    [TestCase("M.ORCH-T5 --root C:\\", false)]
    [TestCase("M.ORCH-T5\" --x", false)]
    [TestCase("-T5", false)]
    public void Only_a_ticket_id_reaches_the_command_line(string? id, bool ok) =>
        Assert.That(TicketsDesktop.IsTicketId(id), Is.EqualTo(ok));

    [Test]
    public void It_starts_on_the_dashboards_workspace_with_the_ticket_named() =>
        Assert.That(TicketsDesktop.Arguments("m.orch-t500", @"D:\Projects\MindAttic"),
            Is.EqualTo(new[] { "--root", @"D:\Projects\MindAttic", "--ticket", "M.ORCH-T500" }));

    [Test]
    public void A_missing_install_or_a_bad_id_says_why_instead_of_starting_anything()
    {
        var options = new OrchestratorOptions { TicketsDesktopExe = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "none.exe") };
        Assert.That(TicketsDesktop.Open("M.ORCH-T500", options), Does.StartWith("Tickets Desktop not found at").And.Contain("deploy.ps1"));
        Assert.That(TicketsDesktop.Open("not an id", options), Is.EqualTo("Not a ticket id: not an id"));
    }

    [Test]
    public void Each_ticket_id_in_the_window_is_a_link_into_tickets_desktop()
    {
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        var doc = XDocument.Load(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var style = doc.Descendants(p + "Style").Single(s => (string?)s.Attribute(x + "Key") == "TicketLink");
        Assert.That(style.Elements(p + "Setter").Single(s => (string?)s.Attribute("Property") == "Command").Attribute("Value")!.Value,
            Does.Contain("OpenInTicketsCommand"));

        var links = doc.Descendants(p + "Hyperlink").Where(h => (string?)h.Attribute("Style") == "{StaticResource TicketLink}").ToList();
        // The per-user Tickets view's ticket (M.ORCH-T668), its title and its Assign in Tickets (M.ORCH-T762), a finished ticket and
        // an agent's ticket snippet (M.ORCH-T763); the Repos and Queue views' links went with them to Tickets Desktop.
        Assert.That(links.Select(h => (string)h.Attribute("CommandParameter")!),
            Is.EquivalentTo(new[] { "{Binding Id}", "{Binding Id}", "{Binding Id}", "{Binding TicketId}", "{Binding TicketId}" }));
        var snippet = doc.Descendants().Single(e => (string?)e.Attribute(x + "Name") == "AgentTicketSnippet");
        Assert.That(snippet.Element(p + "Hyperlink")!.Element(p + "Run")!.Attribute("Text")!.Value, Is.EqualTo("{Binding Headline, Mode=OneWay}"),
            "the snippet (id and title) is the link itself");
        // Each one opens through the same style's OpenInTicketsCommand (no link overrides it with its own Command).
        Assert.That(links, Has.None.Matches<XElement>(h => h.Attribute("Command") is not null));
        Assert.That(links, Has.All.Matches<XElement>(h => h.Attribute(XName.Get("AutomationProperties.Name")) is not null));

        // No ticket id left as plain text: a Run or TextBlock bound to an id outside a link.
        var plain = doc.Descendants().Where(e => e.Name == p + "Run" || e.Name == p + "TextBlock")
            .Where(e => (string?)e.Attribute("Text") is { } t && (t.StartsWith("{Binding Id") || t.StartsWith("{Binding TicketId") || t.StartsWith("{Binding Summary")))
            .Where(e => e.Parent?.Name != p + "Hyperlink")
            // An organization's summary isn't a ticket's.
            .Where(e => e.Ancestors(p + "DataTemplate").FirstOrDefault()?.Attribute("DataType")?.Value.Contains("OrgViewModel") != true);
        Assert.That(plain, Is.Empty, "no ticket id or title left as plain text");
        // Free text that names tickets (the dashboard's message, a status, a held note, a CLI's claims) links each id it names.
        Assert.That(doc.Descendants(p + "TextBlock").Count(e => e.Attribute(XName.Get("{clr-namespace:MindAttic.Orchestrator.Wpf}TicketText.Text")) is not null),
            Is.GreaterThanOrEqualTo(4));
    }

    [Test]
    public void Every_ticket_id_in_a_line_of_text_is_found_for_its_link()
    {
        var parts = TicketsDesktop.Split("M.ORCH-T5 waits for AUT-T12, (U.SPC-T3). Not MAO-12, x-t1 or ABC-T4x.");
        Assert.That(parts.Where(s => s.TicketId is not null).Select(s => s.TicketId), Is.EqualTo(new[] { "M.ORCH-T5", "AUT-T12", "U.SPC-T3" }));
        Assert.That(string.Concat(parts.Select(s => s.Text)), Is.EqualTo("M.ORCH-T5 waits for AUT-T12, (U.SPC-T3). Not MAO-12, x-t1 or ABC-T4x."), "nothing lost");
        Assert.That(TicketsDesktop.Split(null), Is.Empty);
        Assert.That(TicketsDesktop.Split("no tickets").Single(), Is.EqualTo(new TicketsDesktop.Segment("no tickets", null)));

        Assert.That(TicketsDesktop.OnlyTicketIn("Needs you: M.ORCH-T5 waits for your approval (tickets ticket approve M.ORCH-T5)"), Is.EqualTo("M.ORCH-T5"));
        Assert.That(TicketsDesktop.OnlyTicketIn("A-T1 and A-T2"), Is.Null, "two tickets: no one to open");
        Assert.That(TicketsDesktop.OnlyTicketIn("pacing"), Is.Null);
    }
}
