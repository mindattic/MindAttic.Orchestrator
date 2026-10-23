using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using MindAttic.Components.Wpf.Controls;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Wpf;
using MindAttic.Orchestrator.Wpf.ViewModels;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// Dark mode by default with a sun/moon switch in the dashboard's upper-right corner (M.ORCH-T678): the dashboard opens dark
/// unless light was chosen, the library's MaThemeToggle switches it live, and the choice is kept in the orchestrator's settings
/// so it holds across restarts. The state colours view models hand out follow a switch too, none left in the other theme's colour.
/// </summary>
[Apartment(ApartmentState.STA)]
public sealed class ThemeChoiceTests
{
    private static readonly XNamespace P = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Mc = "clr-namespace:MindAttic.Components.Wpf.Controls;assembly=MindAttic.Components.Wpf";

    private string dir = "";

    [SetUp]
    public void SetUp() => Directory.CreateDirectory(dir = Path.Combine(Path.GetTempPath(), "orch-theme-" + Guid.NewGuid().ToString("N")));

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(dir, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private JsonStore Store() => new(dir);

    /// <summary>A dictionary holding the library theme, as App.xaml merges it.</summary>
    private static ResourceDictionary Themed()
    {
        _ = System.IO.Packaging.PackUriHelper.UriSchemePack; // registers pack:// outside an Application
        var resources = new ResourceDictionary();
        resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/MindAttic.Components.Wpf;component/Theme.xaml") });
        return resources;
    }

    private static Color ColourOf(object brush) => ((SolidColorBrush)brush).Color;

    [Test]
    public void It_is_dark_when_nothing_is_set()
    {
        var store = Store();
        Assert.That(store.LoadSettings().Theme, Is.Null, "a fresh settings row names no theme");
        Assert.That(ThemeChoice.Saved(store), Is.EqualTo(MaThemeMode.Dark));

        var resources = Themed();
        Assert.That(ThemeChoice.Apply(resources, store), Is.EqualTo(MaThemeMode.Dark));
        Assert.That(MaTheme.Get(resources), Is.EqualTo(MaThemeMode.Dark));

        store.SaveSettings(store.LoadSettingsToChange() with { Theme = "purple" });
        Assert.That(ThemeChoice.Saved(store), Is.EqualTo(MaThemeMode.Dark), "a value it can't read is dark too");
    }

    [Test]
    public void A_saved_light_choice_is_used_at_start()
    {
        var store = Store();
        store.SaveSettings(store.LoadSettingsToChange() with { Theme = "Light" });

        var resources = Themed();
        Assert.That(ThemeChoice.Apply(resources, store), Is.EqualTo(MaThemeMode.Light));
        Assert.That(MaTheme.Get(resources), Is.EqualTo(MaThemeMode.Light));

        var toggle = new MaThemeToggle();
        var window = OffScreenWindow.Create(content: toggle);
        window.Resources = resources;
        try
        {
            Assert.That(toggle.Theme, Is.EqualTo(MaThemeMode.Light), "the switch shows the saved theme");
            Assert.That(toggle.Glyph, Is.EqualTo(MaThemeToggle.MoonGlyph));
            Assert.That(toggle.SwitchText, Is.EqualTo("Switch to dark theme"));
        }
        finally { window.Close(); }
    }

    [Test]
    public void The_toggle_switches_the_theme_and_saves_each_choice()
    {
        var store = Store();
        var toggle = new MaThemeToggle();
        var window = OffScreenWindow.Create(content: toggle);
        window.Resources = Themed();
        ThemeChoice.Apply(window.Resources, store);
        ThemeChoice.SaveSwitches(window, store);
        try
        {
            var invoke = (IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(toggle).GetPattern(PatternInterface.Invoke);

            invoke.Invoke();
            Pump();
            Assert.That(MaTheme.Get(window.Resources), Is.EqualTo(MaThemeMode.Light), "switched at once, no restart");
            Assert.That(store.LoadSettings().Theme, Is.EqualTo("Light"), "and saved");
            Assert.That(ThemeChoice.Saved(Store()), Is.EqualTo(MaThemeMode.Light), "a new start reads it back");

            invoke.Invoke();
            Pump();
            Assert.That(MaTheme.Get(window.Resources), Is.EqualTo(MaThemeMode.Dark));
            Assert.That(store.LoadSettings().Theme, Is.EqualTo("Dark"));
        }
        finally { window.Close(); }
    }

    [Test]
    public void Saving_the_theme_keeps_every_other_setting()
    {
        var store = Store();
        store.SaveSettings(store.LoadSettingsToChange() with { MaxConcurrentSessions = 7, WarnPercent = 61 });

        Assert.That(ThemeChoice.Save(store, MaThemeMode.Light), Is.True);

        var s = store.LoadSettings();
        Assert.That((s.Theme, s.MaxConcurrentSessions, s.WarnPercent), Is.EqualTo(("Light", 7, 61)));
    }

    [Test]
    public void The_state_colours_handed_out_follow_a_live_switch()
    {
        var resources = Themed();
        using (Palette.Scope(resources))
        {
            var working = Palette.Working;
            var over = Palette.Over;
            Assert.That(ColourOf(working), Is.EqualTo(ColourOf(resources["MaGood"])));

            MaTheme.Set(resources, MaThemeMode.Light);
            Assert.That(ColourOf(resources["MaGood"]), Is.Not.EqualTo(ColourOf(Palette.LibraryDark("MaGood"))), "the light palette has its own MaGood");
            Assert.That(ColourOf(working), Is.EqualTo(ColourOf(resources["MaGood"])), "a brush already handed out is recoloured");
            Assert.That(ColourOf(over), Is.EqualTo(ColourOf(resources["MaBad"])));
            Assert.That(Palette.Working, Is.SameAs(working), "one brush per colour, so nothing holds a stale one");

            MaTheme.Set(resources, MaThemeMode.Dark);
            Assert.That(ColourOf(working), Is.EqualTo(ColourOf(Palette.LibraryDark("MaGood"))), "and back");
        }
        Assert.That(Palette.Working.IsFrozen, Is.True, "outside the app it is the library theme's own again");
    }

    [Test]
    public void The_switch_sits_in_the_windows_upper_right_corner_and_the_title_bar_follows_the_theme()
    {
        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        var wpf = Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf");
        var doc = XDocument.Load(Path.Combine(wpf, "MainWindow.xaml"));

        Assert.That((string?)doc.Root!.Attribute(Mc + "MaWindow.ThemedTitleBar"), Is.EqualTo("True"));
        Assert.That(doc.Root!.Attribute(Mc + "MaWindow.DarkTitleBar"), Is.Null, "ThemedTitleBar replaces DarkTitleBar");

        var top = doc.Root!.Element(P + "Grid")!.Elements().First(e => !e.Name.LocalName.Contains('.'));
        Assert.That((string?)top.Attribute("Grid.Row"), Is.EqualTo("0"), "the window's top row");
        Assert.That((string?)top.Attribute("Visibility"), Is.Null, "always shown, even with the login strip hidden");
        var toggle = top.Elements(Mc + "MaThemeToggle").Single();
        Assert.That((string?)toggle.Attribute("DockPanel.Dock"), Is.EqualTo("Right"));

        Assert.That(File.ReadAllText(Path.Combine(wpf, "MainWindow.xaml.cs")), Does.Contain("ThemeChoice.SaveSwitches(this,"));
        Assert.That(File.ReadAllText(Path.Combine(wpf, "App.xaml.cs")), Does.Contain("ThemeChoice.Apply(Resources,"));
        Assert.That(File.ReadAllText(Path.Combine(wpf, "BuildWorkspaceWindow.cs")), Does.Contain("SetThemedTitleBar(this, true)"));
    }

    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
}
