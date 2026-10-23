using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using MindAttic.Components.Wpf.Controls;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>What each repo and agent state is drawn in, for the view models that hand a brush to the view. The colours are
/// MindAttic.Components.Wpf's own (M.ORCH-T181): working is MaGood, waiting MaWarn, tickets MaLink, over MaBad and quiet
/// MaMuted, each 4.5:1 or better on the theme's surfaces (WCAG 2.2 AA), and each swapped for the system colour by
/// MaHighContrast. The XAML refers to the same keys as DynamicResource. Colour never carries meaning on its own: a word or
/// glyph says the same.
/// <para>Inside the app each key is one brush, kept to the theme in force (M.ORCH-T678): a switch between dark and light, or
/// high contrast going on or off, recolours it in place, so a brush a view model, a toast or the chart already holds follows
/// at once and none is left in the other theme's colour. Outside the app (tests) it is the library theme's own, frozen.</para></summary>
public static class Palette
{
    public static Brush Working => Theme("MaGood");
    public static Brush Waiting => Theme("MaWarn");
    public static Brush Tickets => Theme("MaLink");
    public static Brush Quiet => Theme("MaMuted");
    public static Brush Over => Theme("MaBad");

    /// <summary>The fourth status, neither good, warning nor bad (a tab's light in any other state, M.ORCH-T630): the theme's
    /// purple MaOther.</summary>
    public static Brush Other => Theme("MaOther");

    /// <summary>The theme's MaAccent (the chart's target line).</summary>
    public static Brush Accent => Theme("MaAccent");

    public static Brush For(RepoState state, bool hasTickets) => state switch
    {
        RepoState.Working => Working,
        RepoState.Waiting => Waiting,
        _ => hasTickets ? Tickets : Quiet,
    };

    private static readonly string[] Keys = ["MaGood", "MaWarn", "MaLink", "MaMuted", "MaBad", "MaAccent", "MaOther"];

    /// <summary>The brush handed out for each key inside the app, owned by the UI thread and recoloured by <see cref="Follow"/>.</summary>
    private static readonly Dictionary<string, SolidColorBrush> Live = [];
    private static readonly Lock Gate = new();

    static Palette()
    {
        MaTheme.Changed += (_, _) => Follow();
        // MaHighContrast rewrites the theme's entries when Windows high contrast goes on or off; recolour once it has.
        SystemParameters.StaticPropertyChanged += OnSystemChanged;
    }

    private static void OnSystemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "HighContrast")
            Source?.Dispatcher.BeginInvoke(DispatcherPriority.Background, Follow);
    }

    /// <summary>Where the theme in force is read: the Application's resources on its UI thread, or, while a test has set
    /// <see cref="Scope"/>, that dictionary on the test's thread; null outside the app.</summary>
    private static (Func<string, object?> Find, Dispatcher Dispatcher)? Source =>
        testScope is { } test ? (key => test.Resources[key], test.Dispatcher)
        : Application.Current is { } app ? (app.TryFindResource, app.Dispatcher)
        : null;

    private static (ResourceDictionary Resources, Dispatcher Dispatcher)? testScope;

    /// <summary>For a test, with no Application: hand out live brushes read from <paramref name="resources"/> (a window's, with
    /// the theme merged in) on this thread until disposed.</summary>
    internal static IDisposable Scope(ResourceDictionary resources)
    {
        lock (Gate) { Live.Clear(); testScope = (resources, Dispatcher.CurrentDispatcher); }
        return new Ended();
    }

    private sealed class Ended : IDisposable
    {
        public void Dispose() { lock (Gate) { Live.Clear(); testScope = null; } }
    }

    /// <summary>The brush for <paramref name="key"/>, live (see the remarks above); outside the app the library theme's.</summary>
    private static Brush Theme(string key)
    {
        if (Source is not { } source) return Own(key);
        lock (Gate)
        {
            if (Live.TryGetValue(key, out var live)) return live;
        }
        var current = Current(source.Find, key);
        // Off the UI thread, or a theme entry that isn't a plain colour: the entry itself.
        if (!source.Dispatcher.CheckAccess() || current is not SolidColorBrush solid) return current;
        lock (Gate)
        {
            if (!Live.TryGetValue(key, out var live)) Live[key] = live = solid.CloneCurrentValue(); // unfrozen, so Follow can recolour it
            return live;
        }
    }

    /// <summary>Recolours every brush handed out to the theme now in force: after a light/dark switch or a high-contrast change.</summary>
    internal static void Follow()
    {
        if (Source is not { } source) return;
        if (!source.Dispatcher.CheckAccess()) { source.Dispatcher.BeginInvoke(Follow); return; }
        KeyValuePair<string, SolidColorBrush>[] handedOut;
        lock (Gate) handedOut = [.. Live];
        foreach (var (key, live) in handedOut)
            if (Current(source.Find, key) is SolidColorBrush now)
            {
                live.Color = now.Color;
                live.Opacity = now.Opacity;
            }
    }

    private static Brush Current(Func<string, object?> find, string key) => find(key) as Brush ?? Own(key);

    /// <summary>The library theme's own brush for <paramref name="key"/> (MaLink for a key an older theme lacks).</summary>
    internal static Brush LibraryDark(string key) => Own(key);

    private static Brush Own(string key) => LibraryTheme.Value.TryGetValue(key, out var own) ? own : LibraryTheme.Value["MaLink"];

    /// <summary>The library theme's brushes, frozen so any thread can use them.</summary>
    private static readonly Lazy<IReadOnlyDictionary<string, Brush>> LibraryTheme = new(() =>
    {
        _ = System.IO.Packaging.PackUriHelper.UriSchemePack; // registers pack:// outside an Application
        var theme = (ResourceDictionary)Application.LoadComponent(new Uri("/MindAttic.Components.Wpf;component/Theme.xaml", UriKind.Relative));
        var brushes = new Dictionary<string, Brush>();
        foreach (var key in Keys)
        {
            if (!theme.Contains(key)) continue;
            var brush = ((Brush)theme[key]).CloneCurrentValue();
            brush.Freeze();
            brushes[key] = brush;
        }
        return brushes;
    });
}
