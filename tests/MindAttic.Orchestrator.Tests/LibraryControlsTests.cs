using System.Text.RegularExpressions;
using System.Xml.Linq;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The dashboard is made of MindAttic.Components.Wpf controls (M.ORCH-T499, M.ORCH-T531, M.ORCH-T181): every button
/// takes one of the library's styles, the toasts, badges, progress bars, spinner, placeholders, links, message boxes, chart,
/// cards, pills, focus ring, text styles, colours, view model base, commands, zoom, persistent tooltips and high contrast are
/// the library's, and nothing hand-built creeps back.</summary>
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
        // The repo tile is the library's MaAccentTile, not a Button (M.ORCH-T667); every Button takes a library style itself.
        string[] libraryBased = ["MaToolButton", "MaTextButton", "MaButton", "MaPrimaryButton", "MaTileButton", "MaStatCard", "MaSmallStat"];

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
            // Not even an AA override: the library's palette meets WCAG 2.2 AA by itself (M.COMP-T11).
            var colours = root.Descendants().Attributes().Where(a => Regex.IsMatch(a.Value, "^#[0-9A-Fa-f]{6,8}$"));
            Assert.That(colours.Select(a => $"{a.Parent!.Name.LocalName}.{a.Name}={a.Value}"), Is.Empty);
        }

    }

    [Test]
    public void App_xaml_keeps_no_style_of_its_own()
    {
        var keys = App().Descendants().Select(e => (string?)e.Attribute(X + "Key")).OfType<string>().ToList();

        // No FieldBorder, MaTextBox or MaTextArea override: the library's MaFieldBorder outline is 3:1 (M.COMP-T11).
        // The repo tile is the library's MaAccentTile now (M.ORCH-T667).
        Assert.That(keys, Is.EquivalentTo(new[] { "Visible" }));

        // The old copies are gone, and nothing refers to them.
        var window = File.ReadAllText(Wpf("MainWindow.xaml"));
        foreach (var old in new[] { "FocusRing", "Heading", "SectionLabel", "Muted", "StatCard", "SmallStat", "Card", "PillChoice", "PillToggle", "PulseDot", "MaRec", "FieldBorder" })
        {
            Assert.That(window, Does.Not.Contain($"{{StaticResource {old}}}"), old);
            Assert.That(window, Does.Not.Contain($"{{DynamicResource {old}}}"), old);
        }
    }

    [Test]
    public void The_spacing_dictionary_holds_only_thickness_and_double_tokens()
    {
        var spacing = XDocument.Load(Wpf(Path.Combine("Themes", "Spacing.xaml"))).Root!;
        var tokens = spacing.Elements().Where(e => e.Attribute(X + "Key") is not null).ToList();

        Assert.That(tokens, Is.Not.Empty);
        Assert.That(tokens.Select(e => e.Name.LocalName).Distinct(), Is.EquivalentTo(new[] { "Thickness", "Double" }));
        Assert.That(spacing.Descendants().Where(e => e.Name.LocalName is "Style" or "ControlTemplate" or "DataTemplate"), Is.Empty);
        Assert.That(File.ReadAllText(Wpf("App.xaml")), Does.Contain("Themes/Spacing.xaml"));
    }

    /// <summary>The theme's brushes (not its styles), which MaHighContrast re-keys.</summary>
    private static readonly string[] PaletteKeys =
    [
        "MaChrome", "MaChromeBorder", "MaSurface", "MaHover", "MaText", "MaMuted", "MaRec", "MaStar", "MaControlBorder", "MaFieldBorder",
        "MaFocus", "MaAccent", "MaAccentHover", "MaOnAccent", "MaTileHover", "MaGood", "MaWarn", "MaBad", "MaLink", "MaChartGrid",
    ];

    [Test]
    public void The_window_takes_every_colour_from_the_theme_dynamically_so_high_contrast_reaches_it()
    {
        var window = File.ReadAllText(Wpf("MainWindow.xaml"));

        Assert.That(window, Does.Not.Contain("vm:Palette."), "a state colour is the theme's MaGood, MaWarn, MaLink, MaMuted or MaBad");
        // App.xaml too: RepoTile's selected border was a StaticResource MaFocus (M.ORCH-T556).
        var app = File.ReadAllText(Wpf("App.xaml"));
        foreach (var key in PaletteKeys)
        {
            Assert.That(window, Does.Not.Contain($"{{StaticResource {key}}}"), $"{key}: a StaticResource keeps the dark colour under high contrast");
            Assert.That(app, Does.Not.Contain($"{{StaticResource {key}}}"), $"App.xaml's {key}: a StaticResource keeps the dark colour under high contrast");
        }
        Assert.That(window, Does.Contain("{DynamicResource MaGood}"));
        Assert.That(window, Does.Contain("{DynamicResource MaWarn}"));

        // The view models' state brushes are the theme's too, read when asked: no colour of the dashboard's own.
        var palette = File.ReadAllText(Wpf(Path.Combine("ViewModels", "Palette.cs")));
        Assert.That(Regex.IsMatch(palette, @"Color\.From|0x[0-9A-Fa-f]{2}\b|#[0-9A-Fa-f]{6}|new SolidColorBrush"), Is.False, "Palette defines a colour of its own");
        foreach (var key in new[] { "MaGood", "MaWarn", "MaLink", "MaMuted", "MaBad" })
            Assert.That(palette, Does.Contain($"\"{key}\""), key);
        Assert.That(File.ReadAllText(Wpf("UtilizationChartData.cs")), Does.Not.Contain("Brushes."));
    }

    [Test]
    public void The_state_brushes_are_the_themes_good_warn_link_muted_and_bad()
    {
        _ = System.IO.Packaging.PackUriHelper.UriSchemePack; // registers pack:// outside an Application
        var theme = (System.Windows.ResourceDictionary)System.Windows.Application.LoadComponent(
            new Uri("/MindAttic.Components.Wpf;component/Theme.xaml", UriKind.Relative));
        static System.Windows.Media.Color Colour(object brush) => ((System.Windows.Media.SolidColorBrush)brush).Color;

        Assert.That(Colour(MindAttic.Orchestrator.Wpf.ViewModels.Palette.Working), Is.EqualTo(Colour(theme["MaGood"])));
        Assert.That(Colour(MindAttic.Orchestrator.Wpf.ViewModels.Palette.Waiting), Is.EqualTo(Colour(theme["MaWarn"])));
        Assert.That(Colour(MindAttic.Orchestrator.Wpf.ViewModels.Palette.Tickets), Is.EqualTo(Colour(theme["MaLink"])));
        Assert.That(Colour(MindAttic.Orchestrator.Wpf.ViewModels.Palette.Quiet), Is.EqualTo(Colour(theme["MaMuted"])));
        Assert.That(Colour(MindAttic.Orchestrator.Wpf.ViewModels.Palette.Over), Is.EqualTo(Colour(theme["MaBad"])));
        Assert.That(MindAttic.Orchestrator.Wpf.ViewModels.Palette.Working.IsFrozen, Is.True, "usable from any thread outside the app");
    }

    [Test]
    public void The_view_model_base_commands_zoom_tooltips_and_high_contrast_are_the_librarys()
    {
        Assert.That(File.Exists(Wpf(Path.Combine("ViewModels", "Mvvm.cs"))), Is.False);
        Assert.That(File.Exists(Wpf(Path.Combine("ViewModels", "MainViewModel.Zoom.cs"))), Is.False);

        var sources = Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Wpf"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(d => d is "bin" or "obj"))
            .ToDictionary(f => Path.GetFileName(f), File.ReadAllText);
        foreach (var (file, text) in sources)
        {
            Assert.That(Regex.IsMatch(text, @"\b(class|record)\s+(ObservableObject|RelayCommand|CollectionSync|ZoomLevel)\b"), Is.False, $"{file} copies a library Mvvm type");
            Assert.That(text, Does.Not.Contain("ShowDurationProperty"), $"{file}: MaToolTips.KeepOpen keeps tooltips up");
            Assert.That(text, Does.Not.Contain("SystemParameters.HighContrast"), $"{file}: MaHighContrast follows high contrast");
            Assert.That(text, Does.Not.Contain("ScaleTransform"), $"{file}: ZoomLevel.Attach scales the window");
        }

        var app = sources["App.xaml.cs"];
        Assert.That(app, Does.Contain("MaToolTips.KeepOpen()"));
        Assert.That(app, Does.Contain("MaHighContrast.Track(this"));
        Assert.That(sources["MainViewModel.cs"], Does.Contain("public ZoomLevel Zoom { get; }"));
        Assert.That(sources["MainWindow.xaml.cs"], Does.Contain("model.Zoom.Attach(Root, this)"));

        // The zoom keys and scale are ZoomLevel's, not the window's own; its status text is ZoomLevel's.
        var window = Window();
        Assert.That(window.Descendants(P + "KeyBinding").Select(k => (string?)k.Attribute("Command")).OfType<string>().Where(c => c.Contains("Zoom")), Is.Empty);
        Assert.That(window.Descendants().Where(e => e.Name.LocalName.EndsWith(".LayoutTransform")), Is.Empty);
        Assert.That(window.Descendants(P + "TextBlock").Count(t => (string?)t.Attribute("Text") == "{Binding Zoom.Text}"), Is.EqualTo(1));

        var csproj = XDocument.Load(Wpf("MindAttic.Orchestrator.Wpf.csproj"));
        var version = (string?)csproj.Descendants("PackageReference").Single(r => (string?)r.Attribute("Include") == "MindAttic.Components.Wpf").Attribute("Version");
        Assert.That(int.Parse(version!.Split('.')[0]), Is.GreaterThanOrEqualTo(18), "Mvvm, ZoomLevel, MaToolTips and MaHighContrast arrived in 18.0.0");
    }

    [Test]
    public void The_hand_built_chart_spinner_and_message_boxes_are_gone()
    {
        Assert.That(File.Exists(Wpf("UtilizationChart.cs")), Is.False);
        Assert.That(File.Exists(Wpf("ThreadedSpinner.cs")), Is.False);

        var window = Window();
        Assert.That(Named(window, "UtilizationChart").Name, Is.EqualTo(Mc + "MaLineChart"));
        Assert.That(Named(window, "LoaderSpinner").Name, Is.EqualTo(Mc + "MaSpinner"));

        var sources = Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Wpf"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(d => d is "bin" or "obj"));
        foreach (var file in sources)
            Assert.That(Regex.IsMatch(File.ReadAllText(file), @"(?<!Ma)MessageBox\.Show\("), Is.False, $"{Path.GetFileName(file)} uses the system message box; use MaMessageBox");
        Assert.That(File.ReadAllText(Wpf("App.xaml.cs")), Does.Contain("MaMessageBox.Show("));
    }

    [Test]
    public void Toast_dots_are_bound_to_the_view_models_toast_list_and_open_command()
    {
        // The full-width McToastHost row (M.ORCH-T531) was deliberately replaced by small hoverable notification dots
        // (M.ORCH-T1167, M.ORCH-T1180): a dot's own click now does what the old row's click and its separate dismiss both
        // did, since OpenToastCommand removes the toast from Toasts itself (MainViewModel.cs), so there is no separate
        // DismissCommand binding to check for any more.
        var dots = Named(Window(), "ToastDots");

        Assert.That((string?)dots.Attribute("ItemsSource"), Is.EqualTo("{Binding Toasts}"));

        var button = dots.Descendants(P + "Button").Single();
        Assert.That((string?)button.Attribute("Style"), Is.EqualTo("{StaticResource MaToolButton}"));
        Assert.That(button.Descendants(Mc + "MaStatusLight").Single().Attribute("Foreground")?.Value, Is.EqualTo("{Binding Accent}"));
        Assert.That((string?)button.Attribute("Command"),
            Is.EqualTo("{Binding DataContext.OpenToastCommand, RelativeSource={RelativeSource AncestorType=Window}}"));

        var viewModel = File.ReadAllText(Wpf(Path.Combine("ViewModels", "MainViewModel.cs")));
        Assert.That(Regex.IsMatch(viewModel, @"OpenToastCommand\s*=\s*new RelayCommand\(async p =>\s*\{\s*if \(p is not ToastViewModel t\) return;\s*Toasts\.Remove\(t\);"),
            Is.True, "opening a toast still removes it from Toasts, like the old McToastHost's dismiss did");
    }

    [Test]
    public void No_card_is_hand_built()
    {
        var window = Window();

        // A rounded surface drawn by hand is a card the library already has.
        bool MaCardStyled(XElement b) => ((string?)b.Attribute("Style"))?.StartsWith("{StaticResource Ma") == true;
        var handMade = window.Descendants(P + "Border")
            .Where(b => b.Attribute("CornerRadius") is not null && Regex.IsMatch((string?)b.Attribute("Background") ?? "", @"^\{(Static|Dynamic)Resource Ma"))
            .Where(b => !MaCardStyled(b));
        Assert.That(handMade.Select(b => b.ToString()), Is.Empty);
    }

    [Test]
    public void Progress_bars_text_areas_and_placeholders_are_the_librarys()
    {
        var window = Window();
        // The 5-hour tile's BudgetBar went with the limit tiles (M.ORCH-T1094); any progress bar left is the library's.
        Assert.That(window.Descendants(P + "ProgressBar").Select(b => (string?)b.Attribute("Style")), Has.All.EqualTo("{StaticResource MaProgressBar}"));

        // No placeholder drawn by hand over a box: every box's hint is the library's attached Placeholder.
        Assert.That(window.Descendants(P + "TextBlock").Where(t => (string?)t.Attribute("IsHitTestVisible") == "False"), Is.Empty);
        var boxes = window.Descendants(P + "TextBox").ToList();
        Assert.That(boxes.Select(b => (string?)b.Attribute("Style")), Has.All.Matches<string?>(s => s is "{StaticResource MaTextBox}" or "{StaticResource MaTextArea}"));
        var placeholders = boxes.Select(b => (string?)b.Attribute(Mc + "MaTextBox.Placeholder")).OfType<string>().ToList();
        Assert.That(placeholders, Is.EquivalentTo(new[] { "name@company.com" }), "the Repos view's search and the repo pane's boxes went to Tickets Desktop (M.ORCH-T763)");
        Assert.That(boxes.Where(b => (string?)b.Attribute("AcceptsReturn") == "True"), Is.Empty, "a multi-line box is an MaTextArea");

        var build = File.ReadAllText(Wpf("BuildWorkspaceWindow.cs"));
        Assert.That(build, Does.Contain("SetResourceReference(StyleProperty, \"MaTextArea\")"));
        Assert.That(build, Does.Contain("TextWrapping.NoWrap"));
        Assert.That(build, Does.Contain("SetResourceReference(StyleProperty, \"MaProgressBar\")"));
    }

    [Test]
    public void Ticket_links_are_McHyperlinks_and_the_scroll_views_keep_the_themed_scroll_bars()
    {
        var window = Window();
        var link = window.Descendants(P + "Style").Single(s => (string?)s.Attribute(X + "Key") == "TicketLink");
        Assert.That((string?)link.Attribute("BasedOn"), Is.EqualTo("{StaticResource MaHyperlink}"));
        Assert.That(link.Elements(P + "Setter").Select(s => (string?)s.Attribute("Property")), Is.EquivalentTo(new[] { "Command", "ToolTip" }));

        // A ScrollViewer style not based on the theme's implicit one would bring back the light corner.
        foreach (var style in window.Descendants(P + "Style").Where(s => (string?)s.Attribute("TargetType") == "ScrollViewer"))
            Assert.That((string?)style.Attribute("BasedOn"), Is.EqualTo("{StaticResource {x:Type ScrollViewer}}"));
        Assert.That(window.Descendants().Where(e => e.Name.LocalName is "ScrollBar" or "ScrollViewer.Template"), Is.Empty);
    }

    [Test]
    public void The_tab_strips_and_sign_in_panel_stay_on_library_styles_with_no_colour_of_their_own()
    {
        var window = Window();

        // The person tab strip is the library's MaTabControl, a bare strip (ShowContent false) whose selection drives the window
        // (M.ORCH-T667). There are no Dashboard/Terminals page tabs (M.ORCH-T751).
        Assert.That(Named(window, "LoginTabStrip").Name, Is.EqualTo(Mc + "MaTabControl"));
        Assert.That((string?)Named(window, "LoginTabStrip").Attribute("ShowContent"), Is.EqualTo("False"));
        // The others are Settings' own sections (M.ORCH-T804), inside its modal, showing their content, and a person's Terminals
        // panel's tabs (M.ORCH-T810), a bare strip too, inside their page's body.
        var tabControls = window.Descendants(Mc + "MaTabControl").ToList();
        Assert.That(tabControls.Select(t => (string?)t.Attribute(X + "Name")), Is.EquivalentTo(new[] { "LoginTabStrip", "SettingsTabs", "TerminalTabs", "QueueStatusTabs" }), "no page tabs");
        // A person Queue's status strip (M.ORCH-T849) is a bare strip too, with no colour of its own.
        Assert.That((string?)Named(window, "QueueStatusTabs").Attribute("ShowContent"), Is.EqualTo("False"));
        Assert.That(Named(window, "QueueStatusTabs").DescendantsAndSelf().Attributes().Where(a => a.Name.LocalName is "Background" or "Foreground" or "BorderBrush"), Is.Empty);
        Assert.That(Named(window, "SettingsTabs").Ancestors().Contains(Named(window, "SettingsModal")), Is.True);
        Assert.That((string?)Named(window, "TerminalTabs").Attribute("ShowContent"), Is.EqualTo("False"));
        // Fixed under the person tabs, not in the scrolling body (M.ORCH-T861).
        Assert.That(Named(window, "TerminalTabs").Ancestors().Contains(Named(window, "AgentsBody")), Is.False);
        Assert.That(Named(window, "TerminalTabs").Ancestors().Contains(Named(window, "TerminalsPanel")), Is.True);
        // The dashboard is always on screen on a signed-in login's tab: no page decides whether its header and toolbar show.
        foreach (var name in new[] { "Header", "Toolbar" })
            Assert.That((string?)Named(window, name).Attribute("Visibility"), Is.EqualTo("{Binding IsTabSignedIn, Converter={StaticResource Visible}}"), name);
        Assert.That(typeof(Wpf.ViewModels.MainViewModel).GetProperty("Page"), Is.Null);

        // The status light and the tab underline take their colours from view-model brushes (themed in Palette), never from a literal.
        var brushes = Named(window, "LoginTabItem").DescendantsAndSelf().Attributes()
            .Where(a => a.Name.LocalName is "Background" or "Foreground" or "Fill" or "Stroke" or "BorderBrush");
        Assert.That(brushes.Where(a => !a.Value.StartsWith('{')).Select(a => a.ToString()), Is.Empty);

        // The sign-in-only panel's button is the library's primary button.
        var signIn = window.Descendants(P + "Button").Single(b => (string?)b.Attribute("Command") == "{Binding SignInTabCommand}");
        Assert.That((string?)signIn.Attribute("Style"), Is.EqualTo("{StaticResource MaPrimaryButton}"));

        // Every Ellipse (a status light or accent dot) is filled from a binding or the theme: no literal or Brushes.* colour.
        foreach (var dot in window.Descendants(P + "Ellipse"))
            Assert.That((string?)dot.Attribute("Fill"), Does.StartWith("{"), dot.ToString());
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
            // App.xaml's merged dictionaries resolve at runtime too; follow the ones in the app itself (Themes/Spacing.xaml).
            .Concat(App().Descendants(P + "ResourceDictionary").Select(d => (string?)d.Attribute("Source"))
                .Where(src => src is not null && !src.StartsWith("pack:", StringComparison.Ordinal))
                .SelectMany(src => XDocument.Load(Wpf(src!)).Root!.Descendants().Select(e => (string?)e.Attribute(X + "Key")).OfType<string>()))
            .ToHashSet();

        var named = window.Descendants().Attributes().SelectMany(a => Regex.Matches(a.Value, @"\{(?:Static|Dynamic)Resource (\w+)\}").Select(m => m.Groups[1].Value))
            .Distinct().ToList();
        Assert.That(named, Is.Not.Empty);
        Assert.That(named.Where(k => !keys.Contains(k)), Is.Empty);
        // The scroll bar the gutter makes room for is the theme's.
        var scrollBar = (System.Windows.Style)theme["MaScrollBar"];
        Assert.That(scrollBar.Setters.OfType<System.Windows.Setter>().Single(s => s.Property.Name == "Width").Value, Is.EqualTo(MindAttic.Components.Wpf.Controls.MaScrollArea.ScrollBarWidth));
    }

    [Test]
    public void The_workspace_builder_asks_its_question_in_an_McModal_not_a_system_message_box()
    {
        var source = File.ReadAllText(Wpf("BuildWorkspaceWindow.cs"));

        Assert.That(source, Does.Contain("MaModal question = new()"));
        Assert.That(source, Does.Not.Contain("MessageBox.Show"));
        Assert.That(source, Does.Contain("\"MaPrimaryButton\""));
    }
}
