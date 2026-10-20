using System.Xml.Linq;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// The settings dialog is the shared McModal from MindAttic.Components.Wpf 7.0.0 (M.ORCH-T498), not the hand-built
/// 400px flyout it replaced: last in the window's root grid spanning every row, opened by IsSettingsOpen and closed (Esc,
/// its close button, the backdrop) through ToggleSettingsCommand, holding the same settings with Save in its footer.
/// </summary>
public sealed class SettingsModalTests
{
    private static readonly XNamespace P = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Mc = "clr-namespace:MindAttic.Components.Wpf.Controls;assembly=MindAttic.Components.Wpf";

    private static string RepoRoot()
    {
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        return root;
    }

    [Test]
    public void Settings_open_in_the_shared_McModal_last_in_the_root_grid()
    {
        var root = RepoRoot();
        var doc = XDocument.Load(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        var grid = doc.Root!.Element(P + "Grid")!;
        var rows = grid.Element(P + "Grid.RowDefinitions")!.Elements(P + "RowDefinition").Count();

        var modal = grid.Elements().Last();
        Assert.That(modal.Name, Is.EqualTo(Mc + "McModal"), "the modal is the root grid's last child, over everything");
        Assert.That((string?)modal.Attribute("Grid.RowSpan"), Is.EqualTo(rows.ToString()), "it spans every row");
        Assert.That((string?)modal.Attribute("Grid.Row"), Is.Null.Or.EqualTo("0"));
        Assert.That((string?)modal.Attribute("IsOpen"), Is.EqualTo("{Binding IsSettingsOpen}"));
        Assert.That((string?)modal.Attribute("CloseCommand"), Is.EqualTo("{Binding ToggleSettingsCommand}"));
        Assert.That((string?)modal.Attribute("Title"), Is.EqualTo("Settings"));

        var content = modal.Elements(P + "StackPanel").Single();
        Assert.That((string?)content.Attribute("DataContext"), Is.EqualTo("{Binding Settings}"));
        Assert.That(content.Descendants(P + "TextBox").Any(t => (string?)t.Attribute("AutomationProperties.Name") == "Budget per 5-hour block"), Is.True);
        Assert.That(content.Descendants(P + "ToggleButton").Any(t => (string?)t.Attribute("Content") == "Autopilot"), Is.True);

        var footer = modal.Element(Mc + "McModal.Footer")!.Elements().Single();
        Assert.That((string?)footer.Attribute("DataContext"), Is.EqualTo("{Binding Settings}"));
        Assert.That(footer.Descendants(P + "Button").Single(b => (string?)b.Attribute("Content") == "Save").Attribute("Command")?.Value,
            Is.EqualTo("{Binding SaveCommand}"));

        Assert.That(doc.Descendants().Count(e => (string?)e.Attribute("Visibility") == "{Binding IsSettingsOpen, Converter={StaticResource Visible}}"),
            Is.Zero, "the old flyout is gone");
        Assert.That(doc.Descendants(P + "Button").Count(b => (string?)b.Attribute("Command") == "{Binding ToggleSettingsCommand}"),
            Is.EqualTo(3), "the Settings button and the 5-hour and week cards still open it");
    }

    [Test]
    public void The_app_merges_the_components_theme_and_references_17_0_0()
    {
        var root = RepoRoot();
        var app = File.ReadAllText(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "App.xaml"));
        Assert.That(app, Does.Contain("pack://application:,,,/MindAttic.Components.Wpf;component/Theme.xaml"), "McModal's look lives in Theme.xaml");
        var csproj = XDocument.Load(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MindAttic.Orchestrator.Wpf.csproj"));
        var reference = csproj.Descendants("PackageReference").Single(r => (string?)r.Attribute("Include") == "MindAttic.Components.Wpf");
        Assert.That((string?)reference.Attribute("Version"), Is.EqualTo("17.0.0"));
    }
}
