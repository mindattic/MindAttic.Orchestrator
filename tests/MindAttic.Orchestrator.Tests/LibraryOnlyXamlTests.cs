using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// Every element of the dashboard's XAML is MindAttic.Components.Wpf's (M.ORCH-T667), so each piece of the UI is one the
/// library already tested and the user can trust: a library control, or a plain WPF control wearing a library style, set up
/// only through the library's own settings. Nothing hand-made may drift back in.
/// <list type="bullet">
/// <item>Allowed: a control from MindAttic.Components.Wpf.Controls; a control whose Style is a library key; an app style whose
/// BasedOn chain reaches a library key and only adds behaviour (a command, a tooltip, visibility, an accessible name) or sets
/// the library control's own properties (an MaTabItem's Accent, Status, Trailing); a type the library theme styles implicitly.</item>
/// <item>Also allowed: layout panels with no visual of their own (Grid, StackPanel, DockPanel, ...), text content (Run,
/// LineBreak), and markup that isn't an element on screen (templates, styles, triggers, bindings, resources).</item>
/// <item>Everything else fails: a raw Button, ListBox, Border, ScrollViewer, ItemsControl, TextBox, ToggleButton, ComboBox,
/// Popup, Ellipse, a ControlTemplate, an app style not based on the library, a literal colour.</item>
/// </list>
/// The exceptions, each with its reason, are <see cref="Exceptions"/>.
/// </summary>
[Apartment(ApartmentState.STA)]
public sealed class LibraryOnlyXamlTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    private const string LibraryNamespace = "MindAttic.Components.Wpf.Controls";
    private static readonly Assembly Library = typeof(MindAttic.Components.Wpf.Controls.MaTabControl).Assembly;

    /// <summary>Markup that is not an element on screen.</summary>
    private static readonly HashSet<string> Markup =
    [
        "Application", "ResourceDictionary", "DataTemplate", "ItemsPanelTemplate", "Style", "Setter", "Trigger", "DataTrigger",
        "MultiTrigger", "MultiDataTrigger", "Condition", "Binding", "MultiBinding", "KeyBinding", "BooleanToVisibilityConverter",
        "Thickness", "Double",
    ];

    /// <summary>Layout panels: they place their children and draw nothing of their own. Viewbox is here too (M.ORCH-T775): it
    /// only scales its one child, with no fill, border or template of its own to approve.</summary>
    private static readonly HashSet<string> Layout = ["Grid", "StackPanel", "DockPanel", "WrapPanel", "UniformGrid", "Canvas", "ColumnDefinition", "RowDefinition", "Viewbox"];

    /// <summary>Text content inside a TextBlock or a Hyperlink.</summary>
    private static readonly HashSet<string> Text = ["Run", "LineBreak"];

    /// <summary>Types the theme styles implicitly that still need a keyed library style: the implicit ScrollViewer is only the
    /// base of MaBodyScroll / MaPaneScroll (the gutters and the scroll bar inside them), so a raw one is a scroll area of its own.</summary>
    private static readonly HashSet<string> ImplicitButKeyed = ["ScrollViewer"];

    /// <summary>The only elements allowed without a library style, and why. Keep this list short.</summary>
    internal static readonly IReadOnlyDictionary<string, string> Exceptions = new Dictionary<string, string>
    {
        ["Window"] = "the document root: its title bar is the library's MaWindow.ThemedTitleBar and its colours library tokens",
        ["TextBlock"] = "unstyled body text: the library has no body-text key or implicit TextBlock style (an implicit TextBlock " +
                        "style would leak into every control template), so plain text inherits the window's library font and " +
                        "MaText; it may set no colour but a library token or a binding",
        ["Path"] = "an unstyled vector icon (M.ORCH-T775): the library has no Path style to base one on and none should exist " +
                   "(a style would leak into every library control that draws with Path internally), so a one-off icon the " +
                   "library's own glyph set can't safely render (a wrong icon-font codepoint is a blank tofu box) draws its own " +
                   "geometry instead; like TextBlock, its Fill may set no colour but a library token or a binding",
    };

    /// <summary>Setters an app style may add on top of a library style: behaviour, never looks.</summary>
    private static readonly HashSet<string> BehaviourSetters = ["Command", "CommandParameter", "ToolTip", "Visibility"];

    /// <summary>The brush properties an element may set, only to a library token, a binding or no fill.</summary>
    private static bool IsBrushProperty(string name) =>
        name.EndsWith("Background") || name.EndsWith("Foreground") || name.EndsWith("Brush") || name is "Fill" or "Stroke" or "Accent";

    private static string RepoRoot()
    {
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        return root;
    }

    private static IEnumerable<string> XamlFiles() =>
        Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Wpf"), "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(d => d is "bin" or "obj"));

    /// <summary>The theme's keys: its style names, and the types it styles implicitly.</summary>
    private static (HashSet<string> Keys, HashSet<string> Implicit) Theme()
    {
        _ = System.IO.Packaging.PackUriHelper.UriSchemePack; // registers pack:// outside an Application
        var theme = (System.Windows.ResourceDictionary)System.Windows.Application.LoadComponent(
            new Uri("/MindAttic.Components.Wpf;component/Theme.xaml", UriKind.Relative));
        var styles = theme.Keys.Cast<object>().Where(k => theme[k] is System.Windows.Style).ToList();
        return (styles.OfType<string>().ToHashSet(), styles.OfType<Type>().Select(t => t.Name).ToHashSet());
    }

    /// <summary>Every element in <paramref name="files"/> that isn't the library's, as "file: element (why)".</summary>
    internal static List<string> Violations(IEnumerable<(string Name, XElement Root)> files, IEnumerable<XElement>? shared = null)
    {
        var (themeKeys, themeImplicit) = Theme();
        var roots = files.ToList();
        // The app's and the window's own keyed styles, which the chain check follows to the library.
        var appStyles = roots.Select(f => f.Root).Concat(shared ?? [])
            .SelectMany(r => r.Descendants().Where(e => e.Name.LocalName == "Style" && e.Attribute(X + "Key") is not null))
            .ToDictionary(s => (string)s.Attribute(X + "Key")!, s => s);
        var problems = new List<string>();

        foreach (var (file, root) in roots)
        {
            string Where(XElement e) => $"{file}:{((IXmlLineInfo)e).LineNumber} <{e.Name.LocalName}>";

            // Every style in the file, keyed, inline or an ItemContainerStyle, must come from the library.
            foreach (var style in root.Descendants().Where(e => e.Name.LocalName == "Style"))
                if (StyleProblem(style, themeKeys, appStyles, []) is { } why) problems.Add($"{Where(style)}: {why}");

            foreach (var element in root.DescendantsAndSelf())
            {
                var name = element.Name.LocalName;
                if (name.Contains('.')) continue; // a property element (Grid.ColumnDefinitions, Button.ToolTip, ...)

                foreach (var attribute in element.Attributes().Where(a => IsBrushProperty(a.Name.LocalName)))
                {
                    var value = attribute.Value;
                    if (!(value.StartsWith("{DynamicResource Ma") || value.StartsWith("{Binding") || value == "Transparent"))
                        problems.Add($"{Where(element)}: {attribute.Name.LocalName}=\"{value}\" is a colour of its own; use a library token or a binding");
                }
                if (element.Attributes().Any(a => a.Value.StartsWith('#')))
                    problems.Add($"{Where(element)}: a literal colour");

                var library = element.Name.NamespaceName.Contains($"clr-namespace:{LibraryNamespace}");
                var styleRef = (string?)element.Attribute("Style");
                var inline = element.Elements().FirstOrDefault(e => e.Name.LocalName == $"{name}.Style")?.Elements().FirstOrDefault();

                if (styleRef is not null)
                {
                    if (StyleRefProblem(styleRef, themeKeys, appStyles, []) is { } why) problems.Add($"{Where(element)}: {why}");
                    continue;
                }
                if (inline is not null) continue; // checked with every style above
                if (library || Markup.Contains(name) || Layout.Contains(name) || Text.Contains(name)) continue;
                if (themeImplicit.Contains(name) && !ImplicitButKeyed.Contains(name)) continue;
                if (name == "Window" && element == root) continue;
                if (name == "TextBlock") continue; // Exceptions["TextBlock"]: its colours are checked above
                if (name == "Path") continue; // Exceptions["Path"]: its Fill is checked above too
                problems.Add($"{Where(element)}: neither a library control nor library-styled");
            }
        }
        return problems;
    }

    /// <summary>Why a Style="{StaticResource Key}" isn't the library's, or null when it is.</summary>
    private static string? StyleRefProblem(string reference, HashSet<string> themeKeys, Dictionary<string, XElement> appStyles, HashSet<string> seen)
    {
        var key = KeyOf(reference);
        if (key is null) return $"Style=\"{reference}\" names no resource";
        if (themeKeys.Contains(key)) return null;
        if (!appStyles.TryGetValue(key, out var style)) return $"Style \"{key}\" is neither the library's nor the app's";
        if (!seen.Add(key)) return $"Style \"{key}\" is based on itself";
        return StyleProblem(style, themeKeys, appStyles, seen) is { } why ? $"Style \"{key}\": {why}" : null;
    }

    /// <summary>Why a Style element isn't the library's (based on a library style, adding only behaviour or the library control's
    /// own settings), or null when it is.</summary>
    private static string? StyleProblem(XElement style, HashSet<string> themeKeys, Dictionary<string, XElement> appStyles, HashSet<string> seen)
    {
        var target = ((string?)style.Attribute("TargetType") ?? "").Replace("{x:Type ", "").TrimEnd('}');
        var targetName = target.Contains(':') ? target[(target.IndexOf(':') + 1)..] : target;
        var setters = style.Descendants().Where(e => e.Name.LocalName == "Setter").Select(s => (string?)s.Attribute("Property") ?? "").ToList();

        // A layout panel's own style may only show or hide it.
        if (Layout.Contains(targetName))
            return setters.All(p => p == "Visibility") ? null : $"a {targetName} style that sets {string.Join(", ", setters.Where(p => p != "Visibility"))}";

        if (style.Descendants().Any(e => e.Name.LocalName is "Style.Resources" or "ControlTemplate" or "EventSetter"))
            return "a style with a template or resources of its own";

        var basedOn = (string?)style.Attribute("BasedOn");
        if (basedOn is null) return $"a {targetName} style not based on a library style";
        if (basedOn.StartsWith("{StaticResource {x:Type "))
        {
            var basedType = basedOn["{StaticResource {x:Type ".Length..].TrimEnd('}');
            if (!basedType.Contains(':') || Library.GetType($"{LibraryNamespace}.{basedType[(basedType.IndexOf(':') + 1)..]}") is null)
                return $"based on the implicit {basedType} style, which isn't the library's";
        }
        else if (StyleRefProblem(basedOn, themeKeys, appStyles, seen) is { } why) return why;

        var libraryType = target.Contains(':') ? Library.GetType($"{LibraryNamespace}.{targetName}") : null;
        var looks = setters.Where(p => !BehaviourSetters.Contains(p) && !p.StartsWith("AutomationProperties.")
                                       && libraryType?.GetProperty(p)?.DeclaringType?.Namespace != LibraryNamespace).ToList();
        return looks.Count == 0 ? null : $"adds looks of its own ({string.Join(", ", looks)}) to a library style";
    }

    private static string? KeyOf(string reference)
    {
        foreach (var prefix in new[] { "{StaticResource ", "{DynamicResource " })
            if (reference.StartsWith(prefix) && reference.EndsWith('}')) return reference[prefix.Length..^1].Trim();
        return null;
    }

    private static IEnumerable<(string, XElement)> Load() =>
        XamlFiles().Select(f => (Path.GetFileName(f), XDocument.Load(f, LoadOptions.SetLineInfo).Root!));

    [Test]
    public void Every_xaml_element_is_a_library_control_or_library_styled()
    {
        var files = Load().ToList();
        Assert.That(files.Select(f => f.Item1), Is.SupersetOf(new[] { "App.xaml", "MainWindow.xaml" }));

        Assert.That(Violations(files), Is.Empty);
    }

    [Test]
    public void The_dashboard_keeps_no_style_or_panel_of_its_own()
    {
        var files = Load().ToList();
        // Only TicketLink (MaHyperlink plus its command and tooltip) is the app's; RepoTile, StatValue, BodyScroll and TileTemplate
        // are the library's MaAccentTile, MaStatValue and MaBodyScroll now, and AlignedGrid its MaTileGrid / MaTileList.
        var keys = files.SelectMany(f => f.Item2.Descendants()).Where(e => e.Name.LocalName == "Style")
            .Select(e => (string?)e.Attribute(X + "Key")).OfType<string>();
        Assert.That(keys, Is.EquivalentTo(new[] { "TicketLink" }));
        Assert.That(files.SelectMany(f => f.Item2.Descendants()).Any(e => (string?)e.Attribute(X + "Key") is "TileTemplate" or "RepoTile" or "StatValue" or "BodyScroll"), Is.False);
        Assert.That(files.SelectMany(f => f.Item2.Descendants()).Where(e => e.Name.NamespaceName.Contains("clr-namespace:MindAttic.Orchestrator.Wpf;")),
            Is.Empty, "a control of the dashboard's own");
        Assert.That(File.Exists(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Wpf", "AlignedGrid.cs")), Is.False);
    }

    [Test]
    public void Controls_built_in_code_take_a_library_style_too()
    {
        // A control made in C# on WPF's default template draws light on the dark window (the Move to menu did): each one gets
        // a library style by SetResourceReference(StyleProperty, "Ma...").
        var sources = Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Wpf"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(d => d is "bin" or "obj"));
        foreach (var file in sources)
        {
            var text = File.ReadAllText(file);
            var made = System.Text.RegularExpressions.Regex.Matches(text, @"new (ContextMenu|MenuItem|Button|TextBox|ProgressBar|ListBox|ComboBox|ToggleButton|CheckBox)").Count;
            var styled = System.Text.RegularExpressions.Regex.Matches(text, @"SetResourceReference\(StyleProperty, ""Ma\w+""\)").Count;
            Assert.That(styled, Is.GreaterThanOrEqualTo(made), $"{Path.GetFileName(file)}: {made} controls made in code, {styled} given a library style");
        }
        Assert.That(File.ReadAllText(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml.cs")),
            Does.Contain("\"MaDropdownMenu\"").And.Contain("\"MaDropdownMenuItem\""));
    }

    [Test]
    public void Every_tooltip_is_the_librarys_never_windows_default()
    {
        // M.ORCH-T805: no native/Windows-default tooltip anywhere on the dashboard. Every ToolTip, whether a string, a bound
        // value, a property element or one set in code, is drawn by the library theme's implicit ToolTip style (the chrome
        // look, light or dark) and kept up by MaToolTips.KeepOpen. The theme is merged into the Application's resources, not
        // a window's, so a tooltip's popup (outside the window's tree) and the context menus' still find it.
        var (_, themeImplicit) = Theme();
        Assert.That(themeImplicit, Does.Contain("ToolTip"), "the library theme styles every ToolTip itself");

        var files = Load().ToList();
        var app = files.Single(f => f.Item1 == "App.xaml").Item2;
        Assert.That(app.Descendants().Where(e => e.Name.LocalName == "ResourceDictionary" && ((string?)e.Attribute("Source") ?? "").EndsWith("MindAttic.Components.Wpf;component/Theme.xaml"))
            .Any(d => d.Ancestors().Any(a => a.Name.LocalName == "Application.Resources")), Is.True, "the theme is the Application's");

        foreach (var (file, root) in files)
        {
            var mine = root.Descendants().Where(e =>
                e.Name.LocalName is "ToolTip.Style" or "ToolTip.Template"
                || (e.Name.LocalName == "ToolTip" && e.Attributes().Any(a => a.Name.LocalName is "Style" or "Template" or "Background" or "Foreground" or "BorderBrush" or "HasDropShadow"))
                || (e.Name.LocalName == "Style" && ((string?)e.Attribute("TargetType") ?? "").Replace("{x:Type ", "").TrimEnd('}') == "ToolTip")
                || e.Attributes().Any(a => a.Name.LocalName is "ToolTipService.ShowDuration" or "ToolTipService.HasDropShadow"));
            Assert.That(mine.Select(e => ((System.Xml.IXmlLineInfo)e).LineNumber), Is.Empty, $"{file}: a tooltip look of its own instead of the library's");
        }

        var sources = Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Wpf"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(d => d is "bin" or "obj"));
        foreach (var file in sources)
        {
            var text = File.ReadAllText(file);
            Assert.That(System.Text.RegularExpressions.Regex.IsMatch(text, @"new (System\.Windows\.Controls\.)?ToolTip\b|typeof\(ToolTip\)|ShowDurationProperty"), Is.False,
                $"{Path.GetFileName(file)}: a tooltip built or restyled in code; set ToolTip to the text and let the library theme draw it");
        }
        Assert.That(File.ReadAllText(Path.Combine(RepoRoot(), "src", "MindAttic.Orchestrator.Wpf", "App.xaml.cs")), Does.Contain("MaToolTips.KeepOpen()"));
    }

    [Test]
    public void The_exceptions_stay_few()
    {
        Assert.That(Exceptions.Keys, Is.EquivalentTo(new[] { "Window", "TextBlock", "Path" }));
        Assert.That(Exceptions.Values, Has.All.Length.GreaterThan(40), "every exception says why");
    }

    [TestCase("<Border Padding=\"4\" />")]
    [TestCase("<Button Content=\"x\" />")]
    [TestCase("<ListBox />")]
    [TestCase("<ItemsControl />")]
    [TestCase("<ScrollViewer />")]
    [TestCase("<TextBox />")]
    [TestCase("<ToggleButton />")]
    [TestCase("<ComboBox />")]
    [TestCase("<Popup />")]
    [TestCase("<Ellipse Fill=\"{Binding Accent}\" />")]
    [TestCase("<Border Style=\"{StaticResource MaCard}\" Background=\"Red\" />")]
    [TestCase("<TextBlock Foreground=\"#FF0000\" />")]
    [TestCase("<Path Data=\"M0,0 L1,1\" Fill=\"Red\" />")]
    [TestCase("<Button Style=\"{StaticResource Mine}\" />")]
    [TestCase("<Border><Border.Style><Style TargetType=\"Border\" BasedOn=\"{StaticResource MaCard}\"><Setter Property=\"Margin\" Value=\"0\" /></Style></Border.Style></Border>")]
    [TestCase("<TextBlock><TextBlock.Style><Style TargetType=\"TextBlock\"><Setter Property=\"FontSize\" Value=\"9\" /></Style></TextBlock.Style></TextBlock>")]
    [TestCase("<Button Style=\"{StaticResource MaButton}\"><Button.Template><ControlTemplate TargetType=\"Button\" /></Button.Template></Button>")]
    public void The_rule_fails_anything_hand_made(string snippet)
    {
        const string Ns = "xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";
        var root = XElement.Parse($"<Grid {Ns}>{snippet}</Grid>", LoadOptions.SetLineInfo);
        // An app style of its own, looked up but not itself under test here.
        var mine = XElement.Parse($"<Style {Ns} x:Key=\"Mine\" TargetType=\"Button\"><Setter Property=\"Padding\" Value=\"1\" /></Style>");

        Assert.That(Violations([("snippet", root)], [mine]), Is.Not.Empty);
    }

    [TestCase("<Border Style=\"{StaticResource MaRowCard}\" Opacity=\"{Binding RowOpacity}\" />")]
    [TestCase("<ItemsControl Style=\"{StaticResource MaTileList}\" mc:MaList.MinItemWidth=\"240\" />")]
    [TestCase("<mc:MaAccentTile Accent=\"{Binding Accent}\" />")]
    [TestCase("<TextBlock Text=\"x\" FontWeight=\"SemiBold\" Foreground=\"{DynamicResource MaWarn}\" />")]
    [TestCase("<Path Data=\"M0,0 L1,1\" Fill=\"{DynamicResource MaText}\" />")]
    [TestCase("<mc:MaTabControl><mc:MaTabControl.ItemContainerStyle><Style TargetType=\"mc:MaTabItem\" BasedOn=\"{StaticResource {x:Type mc:MaTabItem}}\"><Setter Property=\"Accent\" Value=\"{Binding TabBrush}\" /><Setter Property=\"ToolTip\" Value=\"{Binding Tip}\" /></Style></mc:MaTabControl.ItemContainerStyle></mc:MaTabControl>")]
    [TestCase("<Hyperlink Style=\"{StaticResource Link}\" />")]
    public void The_rule_passes_the_librarys_own_pieces(string snippet)
    {
        var root = XElement.Parse(
            "<Grid xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" " +
            "xmlns:mc=\"clr-namespace:MindAttic.Components.Wpf.Controls;assembly=MindAttic.Components.Wpf\">" +
            "<Grid.Resources><Style x:Key=\"Link\" TargetType=\"Hyperlink\" BasedOn=\"{StaticResource MaHyperlink}\"><Setter Property=\"Command\" Value=\"{Binding Open}\" /></Style></Grid.Resources>" +
            snippet + "</Grid>", LoadOptions.SetLineInfo);

        Assert.That(Violations([("snippet", root)]), Is.Empty);
    }
}
