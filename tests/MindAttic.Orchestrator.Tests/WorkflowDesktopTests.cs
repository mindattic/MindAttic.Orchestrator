using System.Xml.Linq;
using MindAttic.Orchestrator.Tickets;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>Every ticket id the dashboard lists links to Workflow Desktop, which opens on that ticket (M.ORCH-T500).</summary>
public sealed class WorkflowDesktopTests
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
        Assert.That(WorkflowDesktop.IsTicketId(id), Is.EqualTo(ok));

    [Test]
    public void It_starts_on_the_dashboards_workspace_with_the_ticket_named() =>
        Assert.That(WorkflowDesktop.Arguments("m.orch-t500", @"D:\Projects\MindAttic"),
            Is.EqualTo(new[] { "--root", @"D:\Projects\MindAttic", "--ticket", "M.ORCH-T500" }));

    [Test]
    public void A_missing_install_or_a_bad_id_says_why_instead_of_starting_anything()
    {
        var options = new OrchestratorOptions { WorkflowDesktopExe = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "none.exe") };
        Assert.That(WorkflowDesktop.Open("M.ORCH-T500", options), Does.StartWith("Workflow Desktop not found at").And.Contain("deploy.ps1"));
        Assert.That(WorkflowDesktop.Open("not an id", options), Is.EqualTo("Not a ticket id: not an id"));
    }

    [Test]
    public void Each_ticket_id_in_the_window_is_a_link_into_workflow()
    {
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        var doc = XDocument.Load(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var style = doc.Descendants(p + "Style").Single(s => (string?)s.Attribute(x + "Key") == "TicketLink");
        Assert.That(style.Elements(p + "Setter").Single(s => (string?)s.Attribute("Property") == "Command").Attribute("Value")!.Value,
            Does.Contain("OpenInWorkflowCommand"));

        var links = doc.Descendants(p + "Hyperlink").Where(h => (string?)h.Attribute("Style") == "{StaticResource TicketLink}").ToList();
        // The repo's open tickets, Next up, the full queue, a finished ticket and an agent's ticket.
        Assert.That(links.Select(h => (string)h.Attribute("CommandParameter")!),
            Is.EquivalentTo(new[] { "{Binding Id}", "{Binding Id}", "{Binding Id}", "{Binding TicketId}", "{Binding TicketId}" }));
        Assert.That(links, Has.All.Matches<XElement>(h => h.Attribute(XName.Get("AutomationProperties.Name")) is not null));

        // No ticket id left as plain text: a Run or TextBlock bound to an id outside a link.
        var plain = doc.Descendants().Where(e => e.Name == p + "Run" || e.Name == p + "TextBlock")
            .Where(e => (string?)e.Attribute("Text") is { } t && (t.StartsWith("{Binding Id") || t.StartsWith("{Binding TicketId")))
            .Where(e => e.Parent?.Name != p + "Hyperlink");
        Assert.That(plain, Is.Empty);
    }
}
