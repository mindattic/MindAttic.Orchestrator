using System.Text.RegularExpressions;
using System.Xml.Linq;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The dashboard is made of MindAttic.Components.Wpf controls (M.ORCH-T499, finished in M.ORCH-T531): every button
/// takes one of the library's styles, the toasts, badges, progress bars, spinner, placeholders, links, message boxes, chart,
/// cards, pills, focus ring and text styles are the library's, and nothing hand-built creeps back.</summary>
public sealed class LibraryControlsTests
{
    private static readonly XNamespace P = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace Mc = "clr-namespace:MindAttic.Components.Wpf.Controls;assembly=MindAttic.Components.Wpf";

    private static string RepoRoot()
    {
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        return root;
    }

    private static string Wpf(string file) => Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Wpf", file);

    private static XElement Window() => XDocument.Load(Wpf("MainWindow.xaml")).Root!;

    private static XElement App() => XDocument.Load(Wpf("App.xaml")).Root!;

    private static XElement Named(XElement root, string name) => root.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);

    [Test]
    public void Every_dashboard_button_uses_a_library_button_style()
    {
        // RepoTile, App.xaml's one button style, is based on McTileButton.
        string[] libraryBased = ["McToolButton", "McTextButton", "McButton", "McPrimaryButton", "McTileButton", "McStatCard", "McSmallStat", "RepoTile"];

        bool Styled(XElement button)
        {
            var style = (string?)button.Attribute("Style") ?? (string?)button.Element(P + "Button.Style")?.Element(P + "Style")?.Attribute("BasedOn");
            return style is not null && libraryBased.Any(s => style == $"{{StaticResource {s}}}");
        }

        var handMade = Window().Descendants(P + "Button").Where(b => !Styled(b) || b.Element(P + "Button.Template") is not null);

        Assert.That(handMade.Select(b => b.ToString()), Is.Empty);
    }

    [Test]
    public void No_control_template_or_hard_coded_colour_is_left_in_the_dashboard()
    {
        foreach (var root in new[] { Window(), App() })
        {
            Assert.That(root.Descendants().Where(e => e.Name.LocalName == "ControlTemplate"), Is.Empty, "a template is a hand-built control");
            // App.xaml keeps one colour of its own: the text fields' 3:1 outline (WCAG 1.4.11), which the library's is not.
            var colours = root.Descendants().Attributes().Where(a => Regex.IsMatch(a.Value, "^#[0-9A-Fa-f]{6,8}$"))
                .Where(a => (string?)a.Parent!.Attribute(X + "Key") != "FieldBorder");
            Assert.That(colours.Select(a => $"{a.Parent!.Name.LocalName}.{a.Name}={a.Value}"), Is.Empty);
        }
        Assert.That((string?)Named(Window(), "DetailPane").Attribute("Background"), Is.EqualTo("{StaticResource McChrome}"));
    }

    [Test]
    public void App_xaml_keeps_only_the_repo_tile_and_the_field_outline_of_its_own()
    {
        var keys = App().Descendants().Select(e => (string?)e.Attribute(X + "Key")).OfType<string>().ToList();

        Assert.That(keys, Is.EquivalentTo(new[] { "Visible", "FieldBorder", "McTextBox", "McTextArea", "RepoTile" }));
        // The two text box styles only re-outline the library's own.
        foreach (var key in new[] { "McTextBox", "McTextArea" })
        {
            var style = App().Descendants(P + "Style").Single(s => (string?)s.Attribute(X + "Key") == key);
            Assert.That((string?)style.Attribute("BasedOn"), Is.EqualTo($"{{StaticResource {key}}}"));
            Assert.That(style.Elements(P + "Setter").Select(s => (string?)s.Attribute("Property")), Is.EqualTo(new[] { "BorderBrush" }));
        }

        // The old copies are gone, and nothing refers to them.
        var window = File.ReadAllText(Wpf("MainWindow.xaml"));
        foreach (var old in new[] { "FocusRing", "Heading", "SectionLabel", "Muted", "StatCard", "SmallStat", "Card", "PillChoice", "PillToggle", "PulseDot", "McRec" })
            Assert.That(window, Does.Not.Contain($"{{StaticResource {old}}}"), old);
    }

    [Test]
    public void The_hand_built_chart_spinner_and_message_boxes_are_gone()
    {
        Assert.That(File.Exists(Wpf("UtilizationChart.cs")), Is.False);
        Assert.That(File.Exists(Wpf("ThreadedSpinner.cs")), Is.False);

        var window = Window();
        Assert.That(Named(window, "UtilizationChart").Name, Is.EqualTo(Mc + "McLineChart"));
        Assert.That(Named(window, "LoaderSpinner").Name, Is.EqualTo(Mc + "McSpinner"));

        var sources = Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Wpf"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(d => d is "bin" or "obj"));
        foreach (var file in sources)
            Assert.That(Regex.IsMatch(File.ReadAllText(file), @"(?<!Mc)MessageBox\.Show\("), Is.False, $"{Path.GetFileName(file)} uses the system message box; use McMessageBox");
        Assert.That(File.ReadAllText(Wpf("App.xaml.cs")), Does.Contain("McMessageBox.Show("));
    }

    [Test]
    public void Toasts_are_an_McToastHost_with_the_view_models_commands()
    {
        var row = Named(Window(), "ToastRow");

        Assert.That(row.Name, Is.EqualTo(Mc + "McToastHost"));
        Assert.That((string?)row.Attribute("ItemsSource"), Is.EqualTo("{Binding Toasts}"));
        Assert.That((string?)row.Attribute("OpenCommand"), Is.EqualTo("{Binding OpenToastCommand}"));
        Assert.That((string?)row.Attribute("DismissCommand"), Is.EqualTo("{Binding DismissToastCommand}"));
        Assert.That(row.Elements(), Is.Empty, "no item template of its own");
    }

    [Test]
    public void The_agent_state_pill_and_subagent_tag_are_McBadges()
    {
        var window = Window();
        var pill = Named(window, "StatePill");
        Assert.That(pill.Name, Is.EqualTo(Mc + "McBadge"));
        Assert.That((string?)pill.Attribute("Glyph"), Is.EqualTo("{Binding StateGlyph}"));
        Assert.That((string?)pill.Attribute("Content"), Is.EqualTo("{Binding StateLabel}"));
        Assert.That((string?)pill.Attribute("Foreground"), Is.EqualTo("{Binding StateBrush}"));

        var tag = Named(window, "SubagentTag");
        Assert.That(tag.Name, Is.EqualTo(Mc + "McBadge"));
        Assert.That((string?)tag.Attribute("Style"), Is.EqualTo("{StaticResource McTag}"));
    }

    [Test]
    public void Progress_bars_text_areas_and_placeholders_are_the_librarys()
    {
        var window = Window();
        var bar = Named(window, "BudgetBar");
        Assert.That(bar.Name, Is.EqualTo(P + "ProgressBar"));
        Assert.That((string?)bar.Attribute("Style"), Is.EqualTo("{StaticResource McProgressBar}"));

        // No placeholder drawn by hand over a box: every box's hint is the library's attached Placeholder.
        Assert.That(window.Descendants(P + "TextBlock").Where(t => (string?)t.Attribute("IsHitTestVisible") == "False"), Is.Empty);
        var boxes = window.Descendants(P + "TextBox").ToList();
        Assert.That(boxes.Select(b => (string?)b.Attribute("Style")), Has.All.Matches<string?>(s => s is "{StaticResource McTextBox}" or "{StaticResource McTextArea}"));
        var placeholders = boxes.Select(b => (string?)b.Attribute(Mc + "McTextBox.Placeholder")).OfType<string>().ToList();
        Assert.That(placeholders, Is.EquivalentTo(new[] { "Search repos and tickets   Ctrl+F", "Opening prompt (optional), or pick a ticket below", "Reply to this tab…" }));
        Assert.That(boxes.Where(b => (string?)b.Attribute("AcceptsReturn") == "True"), Is.Empty, "a multi-line box is an McTextArea");

        var build = File.ReadAllText(Wpf("BuildWorkspaceWindow.cs"));
        Assert.That(build, Does.Contain("SetResourceReference(StyleProperty, \"McTextArea\")"));
        Assert.That(build, Does.Contain("TextWrapping.NoWrap"));
        Assert.That(build, Does.Contain("SetResourceReference(StyleProperty, \"McProgressBar\")"));
    }

    [Test]
    public void Ticket_links_are_McHyperlinks_and_the_scroll_views_keep_the_themed_scroll_bars()
    {
        var window = Window();
        var link = window.Descendants(P + "Style").Single(s => (string?)s.Attribute(X + "Key") == "TicketLink");
        Assert.That((string?)link.Attribute("BasedOn"), Is.EqualTo("{StaticResource McHyperlink}"));
        Assert.That(link.Elements(P + "Setter").Select(s => (string?)s.Attribute("Property")), Is.EquivalentTo(new[] { "Command", "ToolTip" }));

        // A ScrollViewer style not based on the theme's implicit one would bring back the light corner.
        foreach (var style in window.Descendants(P + "Style").Where(s => (string?)s.Attribute("TargetType") == "ScrollViewer"))
            Assert.That((string?)style.Attribute("BasedOn"), Is.EqualTo("{StaticResource {x:Type ScrollViewer}}"));
        Assert.That(window.Descendants().Where(e => e.Name.LocalName is "ScrollBar" or "ScrollViewer.Template"), Is.Empty);
    }

    [Test, Apartment(ApartmentState.STA)]
    public void Every_resource_the_window_names_is_in_the_theme_the_app_or_the_window()
    {
        _ = System.IO.Packaging.PackUriHelper.UriSchemePack; // registers pack:// outside an Application
        var theme = (System.Windows.ResourceDictionary)System.Windows.Application.LoadComponent(
            new Uri("/MindAttic.Components.Wpf;component/Theme.xaml", UriKind.Relative));
        var window = Window();
        var keys = theme.Keys.OfType<string>()
            .Concat(App().Descendants().Select(e => (string?)e.Attribute(X + "Key")).OfType<string>())
            .Concat(window.Descendants().Select(e => (string?)e.Attribute(X + "Key")).OfType<string>())
            .ToHashSet();

        var named = window.Descendants().Attributes().SelectMany(a => Regex.Matches(a.Value, @"\{StaticResource (\w+)\}").Select(m => m.Groups[1].Value))
            .Distinct().ToList();
        Assert.That(named, Is.Not.Empty);
        Assert.That(named.Where(k => !keys.Contains(k)), Is.Empty);
        // The scroll bar the gutter makes room for is the theme's.
        var scrollBar = (System.Windows.Style)theme["McScrollBar"];
        Assert.That(scrollBar.Setters.OfType<System.Windows.Setter>().Single(s => s.Property.Name == "Width").Value, Is.EqualTo(MindAttic.Orchestrator.Wpf.Gutters.ScrollBarWidth));
    }

    [Test]
    public void The_workspace_builder_asks_its_question_in_an_McModal_not_a_system_message_box()
    {
        var source = File.ReadAllText(Wpf("BuildWorkspaceWindow.cs"));

        Assert.That(source, Does.Contain("McModal question = new()"));
        Assert.That(source, Does.Not.Contain("MessageBox.Show"));
        Assert.That(source, Does.Contain("\"McPrimaryButton\""));
    }
}
