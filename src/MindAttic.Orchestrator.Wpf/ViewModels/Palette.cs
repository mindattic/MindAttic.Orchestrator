using System.Windows;
using System.Windows.Media;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>What each repo and agent state is drawn in, for the view models that hand a brush to the view. The colours are
/// MindAttic.Components.Wpf's own (M.ORCH-T181): working is McGood, waiting McWarn, tickets McLink, over McBad and quiet
/// McMuted, each 4.5:1 or better on the theme's surfaces (WCAG 2.2 AA), and each swapped for the system colour by
/// McHighContrast. Each is read from the theme when asked, so a brush handed out while high contrast is on is the high-contrast
/// one. The XAML refers to the same keys as DynamicResource. Colour never carries meaning on its own: a word or glyph says the
/// same.</summary>
public static class Palette
{
    public static Brush Working => Theme("McGood");
    public static Brush Waiting => Theme("McWarn");
    public static Brush Tickets => Theme("McLink");
    public static Brush Quiet => Theme("McMuted");
    public static Brush Over => Theme("McBad");

    public static Brush For(RepoState state, bool hasTickets) => state switch
    {
        RepoState.Working => Working,
        RepoState.Waiting => Waiting,
        _ => hasTickets ? Tickets : Quiet,
    };

    /// <summary>The app's brush for <paramref name="key"/> (the theme's, or McHighContrast's while it is on); outside the app
    /// (tests) the library theme's own.</summary>
    private static Brush Theme(string key) =>
        Application.Current?.TryFindResource(key) as Brush ?? LibraryTheme.Value[key];

    /// <summary>The library theme's brushes, frozen so any thread can use them.</summary>
    private static readonly Lazy<IReadOnlyDictionary<string, Brush>> LibraryTheme = new(() =>
    {
        _ = System.IO.Packaging.PackUriHelper.UriSchemePack; // registers pack:// outside an Application
        var theme = (ResourceDictionary)Application.LoadComponent(new Uri("/MindAttic.Components.Wpf;component/Theme.xaml", UriKind.Relative));
        var brushes = new Dictionary<string, Brush>();
        foreach (var key in new[] { "McGood", "McWarn", "McLink", "McMuted", "McBad", "McAccent" })
        {
            var brush = ((Brush)theme[key]).CloneCurrentValue();
            brush.Freeze();
            brushes[key] = brush;
        }
        return brushes;
    });

    /// <summary>The theme's McAccent (the chart's target line).</summary>
    public static Brush Accent => Theme("McAccent");
}
