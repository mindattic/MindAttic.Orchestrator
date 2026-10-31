using System.Xml.Linq;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// The dashboard's dialogs and its cog. The settings dialog (M.ORCH-T498, M.ORCH-T804) is gone: LAMP (M.ORCH-T1308) moved every
/// app-level setting to MindAttic Vault, so the cog opens MindAttic.Vault.Desktop at the Orchestrator tab (LampSettingsTests
/// covers what it calls). The org-wide login actions it also held are the Logins dialog, the shared MaModal, last in the
/// window's root grid spanning every row, opened by IsLoginsOpen and closed (Esc, its close button, the backdrop) through
/// ToggleLoginsCommand.
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
    public void Logins_open_in_the_shared_McModal_last_in_the_root_grid()
    {
        var root = RepoRoot();
        var doc = XDocument.Load(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        var grid = doc.Root!.Element(P + "Grid")!;
        var rows = grid.Element(P + "Grid.RowDefinitions")!.Elements(P + "RowDefinition").Count();

        var children = grid.Elements().ToList();
        var modal = children.Single(e => e.Name == Mc + "MaModal" && (string?)e.Attribute("IsOpen") == "{Binding IsLoginsOpen}");
        Assert.That(children.Skip(children.IndexOf(modal)).All(e => e.Name == Mc + "MaModal"), Is.True, "the modals come last in the root grid, over everything");
        Assert.That((string?)modal.Attribute("Grid.RowSpan"), Is.EqualTo(rows.ToString()), "it spans every row");
        Assert.That((string?)modal.Attribute("Grid.Row"), Is.Null.Or.EqualTo("0"));
        Assert.That((string?)modal.Attribute("CloseCommand"), Is.EqualTo("{Binding ToggleLoginsCommand}"));
        Assert.That((string?)modal.Attribute("Title"), Is.EqualTo("Logins"));
        Assert.That(modal.Element(Mc + "MaModal.Footer"), Is.Null, "nothing to save: every action acts at once");

        // No settings dialog any more, and nothing binds what it bound.
        Assert.That(children.Any(e => (string?)e.Attribute(X + "Name") == "SettingsModal"), Is.False);
        var text = File.ReadAllText(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        Assert.That(text, Does.Not.Contain("IsSettingsOpen").And.Not.Contain("ToggleSettingsCommand").And.Not.Contain("{Binding Settings}")
            .And.Not.Contain("SaveCommand}").And.Not.Contain("x:Name=\"SettingsTabs\""));
    }

    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Window() => XDocument.Load(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));

    private static XElement Named(XDocument doc, string name) => doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);

    [Test]
    public void A_single_cog_beside_the_theme_toggle_opens_the_settings_in_MindAttic_Vault()
    {
        // M.ORCH-T804: the top-right strip ends cog, sun/moon. Each docks Right, so the XAML lists them right to left. LAMP
        // (M.ORCH-T1308) put the Logins button beside the cog.
        var doc = Window();
        var top = Named(doc, "TopRow");
        var right = top.Elements().Where(e => (string?)e.Attribute("DockPanel.Dock") == "Right").Select(e => (string?)e.Attribute(X + "Name")).ToList();
        Assert.That(right, Is.EqualTo(new[] { "ThemeToggle", "SettingsButton", "LoginsButton", "TicketsButton", "TeamWallButton" }));
        Assert.That((string?)Named(doc, "TicketsButton").Attribute("Command"), Is.EqualTo("{Binding OpenTicketsAppCommand}"));
        Assert.That((string?)Named(doc, "LoginsButton").Attribute("Command"), Is.EqualTo("{Binding ToggleLoginsCommand}"));

        var cog = Named(doc, "SettingsButton");
        Assert.That((string?)cog.Attribute("Style"), Is.EqualTo("{StaticResource MaToolButton}"));
        Assert.That((string?)cog.Attribute("Content"), Is.EqualTo(""), "Segoe's Settings (cog) glyph");
        Assert.That((string?)cog.Attribute("Command"), Is.EqualTo("{Binding OpenSettingsCommand}"));
        Assert.That((string?)cog.Attribute("AutomationProperties.Name"), Is.EqualTo("Settings"));

        // The only opener of the settings.
        var openers = doc.Descendants().Where(e => (string?)e.Attribute("Command") == "{Binding OpenSettingsCommand}").ToList();
        Assert.That(openers, Is.EqualTo(new[] { cog }));
        Assert.That(doc.Descendants(P + "Button").Any(b => (string?)b.Attribute("Content") == "Settings"), Is.False);
    }

    [Test]
    public void The_org_login_toolbar_is_the_Logins_dialogs_body()
    {
        var doc = Window();
        var modal = Named(doc, "LoginsModal");
        var toolbar = Named(doc, "OrgToolbar");
        Assert.That(toolbar.Ancestors().Contains(modal), Is.True);
        Assert.That(Named(doc, "LoginsBody").Name, Is.EqualTo(P + "ScrollViewer"), "it scrolls itself");
        foreach (var name in new[] { "NewAccountBox", "AddLoginButton", "SignInAllButton", "UsageReportButton" })
            Assert.That(Named(doc, name).Ancestors().Contains(toolbar), Is.True, name);
        Assert.That(Named(doc, "TopRow").Descendants().Any(e => (string?)e.Attribute(X + "Name") is "OrgToolbar" or "NewAccountBox"), Is.False);
        // The Team Wall's Launcher toggle that sat beside it is a Vault setting now.
        Assert.That(doc.Descendants().Any(e => (string?)e.Attribute(X + "Name") == "ExcludeLauncherToggle"), Is.False);

        var code = File.ReadAllText(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml.cs"));
        Assert.That(code, Does.Contain("OrgToolbar.DataContext = model;"));
        Assert.That(code, Does.Not.Contain("SettingsModal").And.Not.Contain("SizeSettings"));
    }

    [Test]
    public void The_app_merges_the_components_theme_and_references_24_0_0()
    {
        var root = RepoRoot();
        var app = File.ReadAllText(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "App.xaml"));
        Assert.That(app, Does.Contain("pack://application:,,,/MindAttic.Components.Wpf;component/Theme.xaml"), "MaModal's look lives in Theme.xaml");
        var csproj = XDocument.Load(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MindAttic.Orchestrator.Wpf.csproj"));
        var reference = csproj.Descendants("PackageReference").Single(r => (string?)r.Attribute("Include") == "MindAttic.Components.Wpf");
        Assert.That((string?)reference.Attribute("Version"), Is.EqualTo("24.0.0"), "M.COMP-T147: MaSpinner's Glyph design");
    }
}
