using System.Xml.Linq;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// The settings dialog is the shared MaModal from MindAttic.Components.Wpf 7.0.0 (M.ORCH-T498), not the hand-built
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

        // Other dialogs (Rename, M.ORCH-T633) follow Settings, so find it by its binding; it must sit in the trailing run of modals, over the page.
        var children = grid.Elements().ToList();
        var modal = children.Single(e => e.Name == Mc + "MaModal" && (string?)e.Attribute("IsOpen") == "{Binding IsSettingsOpen}");
        Assert.That(children.Skip(children.IndexOf(modal)).All(e => e.Name == Mc + "MaModal"), Is.True, "the modals come last in the root grid, over everything");
        Assert.That((string?)modal.Attribute("Grid.RowSpan"), Is.EqualTo(rows.ToString()), "it spans every row");
        Assert.That((string?)modal.Attribute("Grid.Row"), Is.Null.Or.EqualTo("0"));
        Assert.That((string?)modal.Attribute("IsOpen"), Is.EqualTo("{Binding IsSettingsOpen}"));
        Assert.That((string?)modal.Attribute("CloseCommand"), Is.EqualTo("{Binding ToggleSettingsCommand}"));
        Assert.That((string?)modal.Attribute("Title"), Is.EqualTo("Settings"));

        var content = modal.Elements(P + "Grid").Single();
        Assert.That((string?)content.Attribute("DataContext"), Is.EqualTo("{Binding Settings}"));
        Assert.That(content.Descendants(P + "TextBox").Any(t => (string?)t.Attribute("AutomationProperties.Name") == "Budget per 5-hour block"), Is.True);
        Assert.That(content.Descendants(P + "ToggleButton").Any(t => (string?)t.Attribute("Content") == "Allocate"), Is.True);

        var footer = modal.Element(Mc + "MaModal.Footer")!.Elements().Single();
        Assert.That((string?)footer.Attribute("DataContext"), Is.EqualTo("{Binding Settings}"));
        Assert.That(footer.Descendants(P + "Button").Single(b => (string?)b.Attribute("Content") == "Save").Attribute("Command")?.Value,
            Is.EqualTo("{Binding SaveCommand}"));

        Assert.That(doc.Descendants().Count(e => (string?)e.Attribute("Visibility") == "{Binding IsSettingsOpen, Converter={StaticResource Visible}}"),
            Is.Zero, "the old flyout is gone");
    }

    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Window() => XDocument.Load(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));

    private static XElement Named(XDocument doc, string name) => doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);

    [Test]
    public void A_single_cog_between_the_key_and_the_theme_toggle_is_the_only_way_into_settings()
    {
        // M.ORCH-T804: the top-right strip reads key, pause/resume, cog, sun/moon (pause/resume added by M.ORCH-T808).
        // Each docks Right, so the XAML lists them right to left.
        var doc = Window();
        var top = Named(doc, "TopRow");
        var right = top.Elements().Where(e => (string?)e.Attribute("DockPanel.Dock") == "Right").Select(e => (string?)e.Attribute(X + "Name")).ToList();
        Assert.That(right, Is.EqualTo(new[] { "ThemeToggle", "SettingsButton", "PauseButton", "EmergencyBillingButton" }));

        var cog = Named(doc, "SettingsButton");
        Assert.That((string?)cog.Attribute("Style"), Is.EqualTo("{StaticResource MaToolButton}"));
        Assert.That((string?)cog.Attribute("Content"), Is.EqualTo(""), "Segoe's Settings (cog) glyph");
        Assert.That((string?)cog.Attribute("Command"), Is.EqualTo("{Binding ToggleSettingsCommand}"));
        Assert.That((string?)cog.Attribute("AutomationProperties.Name"), Is.EqualTo("Settings"));

        // The only opener: no Settings button under the chart, and the 5-hour and week cards no longer open it.
        var openers = doc.Descendants().Where(e => (string?)e.Attribute("Command") == "{Binding ToggleSettingsCommand}").ToList();
        Assert.That(openers, Is.EqualTo(new[] { cog }));
        Assert.That(doc.Descendants(P + "Button").Any(b => (string?)b.Attribute("Content") == "Settings"), Is.False);
    }

    [Test]
    public void Settings_is_tabbed_with_the_org_login_toolbar_on_its_own_tab()
    {
        var doc = Window();
        var modal = Named(doc, "SettingsModal");
        var tabs = Named(doc, "SettingsTabs");
        Assert.That(tabs.Name, Is.EqualTo(Mc + "MaTabControl"));
        Assert.That(tabs.Ancestors().Contains(modal), Is.True);
        Assert.That(tabs.Attribute("ShowContent"), Is.Null, "a real tab control showing each section's content");
        var headers = tabs.Elements(Mc + "MaTabItem").Select(t => (string?)t.Attribute("Header")).ToList();
        Assert.That(headers, Is.EqualTo(new[] { "Budget", "Pacing", "Scheduler", "Logins", "Person tabs" }));
        // Each section scrolls on its own; no section label sits outside a tab, so it isn't one long list.
        foreach (var tab in tabs.Elements(Mc + "MaTabItem"))
            Assert.That(tab.Elements(P + "ScrollViewer").Count(), Is.EqualTo(1), (string?)tab.Attribute("Header"));
        Assert.That(Named(doc, "SettingsBody").Elements(P + "TextBlock").Any(t => (string?)t.Attribute("Style") == "{StaticResource MaSectionLabel}"), Is.False);

        // The org-wide login toolbar: the email box, Add login, Sign in all and Report, on the Logins tab, off the dashboard.
        var logins = Named(doc, "SettingsLoginsTab");
        var toolbar = Named(doc, "OrgToolbar");
        Assert.That(toolbar.Ancestors().Contains(logins), Is.True);
        foreach (var name in new[] { "NewAccountBox", "AddLoginButton", "SignInAllButton", "UsageReportButton" })
            Assert.That(Named(doc, name).Ancestors().Contains(toolbar), Is.True, name);
        Assert.That(Named(doc, "TopRow").Descendants().Any(e => (string?)e.Attribute(X + "Name") is "OrgToolbar" or "NewAccountBox"), Is.False);
    }

    [Test]
    public void Settings_fills_80_percent_of_its_container()
    {
        var (width, body) = MindAttic.Orchestrator.Wpf.MainWindow.SettingsSize(1480, 920);
        Assert.That(width, Is.EqualTo(1480 * 0.8).Within(0.01));
        Assert.That(body, Is.LessThan(920 * 0.8).And.GreaterThan(920 * 0.6), "80% tall for the whole card, less its title and footer");
        var (narrow, shortBody) = MindAttic.Orchestrator.Wpf.MainWindow.SettingsSize(100, 100);
        Assert.That(narrow, Is.GreaterThan(0));
        Assert.That(shortBody, Is.GreaterThan(0));

        var code = File.ReadAllText(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml.cs"));
        Assert.That(code, Does.Contain("Root.SizeChanged += (_, _) => SizeSettings();"));
        Assert.That(code, Does.Contain("SettingsModal.DialogWidth = width;").And.Contain("SettingsBody.Height = height;"));
        Assert.That(Named(Window(), "SettingsModal").Attribute("DialogWidth"), Is.Null, "sized from code, not a fixed width");
    }

    [Test]
    public void The_app_merges_the_components_theme_and_references_23_0_0()
    {
        var root = RepoRoot();
        var app = File.ReadAllText(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "App.xaml"));
        Assert.That(app, Does.Contain("pack://application:,,,/MindAttic.Components.Wpf;component/Theme.xaml"), "MaModal's look lives in Theme.xaml");
        var csproj = XDocument.Load(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MindAttic.Orchestrator.Wpf.csproj"));
        var reference = csproj.Descendants("PackageReference").Single(r => (string?)r.Attribute("Include") == "MindAttic.Components.Wpf");
        Assert.That((string?)reference.Attribute("Version"), Is.EqualTo("23.0.0"), "M.COMP-T116: the shared ToolTip style wraps at MaxWidth 420");
    }
}
