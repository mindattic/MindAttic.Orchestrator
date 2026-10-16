using System.Xml.Linq;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>A full-screen loader covers the dashboard until its first refresh lands (M.ORCH-T184).</summary>
public sealed class LoaderTests
{
    [Test]
    public void The_loader_covers_every_row_of_the_dashboard_until_the_first_refresh()
    {
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation", x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var grid = XDocument.Load(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml")).Root!.Element(p + "Grid")!;
        var rows = grid.Element(p + "Grid.RowDefinitions")!.Elements(p + "RowDefinition").Count();
        var loader = grid.Elements().Last();

        Assert.That((string?)loader.Attribute(x + "Name"), Is.EqualTo("Loader"), "the loader is the root grid's last child, so it draws over everything");
        Assert.That((int?)loader.Attribute("Grid.RowSpan"), Is.EqualTo(rows));
        Assert.That((string?)loader.Attribute("Visibility"), Does.StartWith("{Binding IsLoading,"));
    }
}
