using System.Xml.Linq;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The dashboard is made of MindAttic.Components.Wpf controls (M.ORCH-T499): every button takes one of the library's
/// styles (directly or through a style based on one) rather than a hand-made template. The one left is the toast's body,
/// which waits for the library's toast control (M.ORCH-T531, requiring M.COMP-T30).</summary>
public sealed class LibraryControlsTests
{
    private static string RepoRoot()
    {
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        return root;
    }

    [Test]
    public void Every_dashboard_button_but_the_toast_body_uses_a_library_button_style()
    {
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var window = XDocument.Load(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml")).Root!;
        // App.xaml's own button styles are all based on McTileButton.
        string[] libraryBased = ["McToolButton", "McTextButton", "McButton", "McPrimaryButton", "McTileButton", "RepoTile", "SmallStat", "StatCard"];

        bool Styled(XElement button)
        {
            var style = (string?)button.Attribute("Style") ?? (string?)button.Element(p + "Button.Style")?.Element(p + "Style")?.Attribute("BasedOn");
            return style is not null && libraryBased.Any(s => style == $"{{StaticResource {s}}}");
        }

        var handMade = window.Descendants(p + "Button").Where(b => !Styled(b) || b.Element(p + "Button.Template") is not null).ToList();

        Assert.That(handMade.Select(b => (string?)b.Attribute("Command")),
            Is.EqualTo(new[] { "{Binding DataContext.OpenToastCommand, RelativeSource={RelativeSource AncestorType=Window}}" }));
    }

    [Test]
    public void The_workspace_builder_asks_its_question_in_an_McModal_not_a_system_message_box()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Wpf", "BuildWorkspaceWindow.cs"));

        Assert.That(source, Does.Contain("McModal question = new()"));
        Assert.That(source, Does.Not.Contain("MessageBox.Show"));
        Assert.That(source, Does.Contain("\"McPrimaryButton\""));
    }
}
