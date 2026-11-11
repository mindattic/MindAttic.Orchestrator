using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// The dashboard's font (M.ORCH-T1274), kept in the orchestrator's settings (<see cref="OrchestratorSettings.Font"/>, set from
/// MindAttic.Vault.Desktop's Orchestrator page like the theme): Segoe UI, the font it always had and the default, or Outfit,
/// bundled in this exe (Fonts/, Regular and Bold; MindAttic.Components.Wpf ships no font of its own). It is one Application
/// resource, <see cref="ResourceKey"/>: every window (the dashboard's and each code-built one), context menu and tooltip takes
/// its FontFamily from it by resource reference, so a change made in the Vault is swapped in live, with no restart and no
/// control touched one by one. Anything that sets its own FontFamily keeps it: the terminal panes' Cascadia Code, the
/// Consolas output boxes, the Segoe Fluent Icons glyphs.
/// </summary>
public static class FontChoice
{
    /// <summary>The Application resource holding the app's FontFamily.</summary>
    public const string ResourceKey = "OrchestratorFontFamily";

    public const string Outfit = "Outfit";

    private static readonly Lazy<FontFamily> SegoeFamily = new(() => new FontFamily(OrchestratorSettings.DefaultFont));

    // Pack URI with the assembly named, so it resolves whatever the entry assembly is; Segoe UI behind it for any glyph Outfit lacks.
    private static readonly Lazy<FontFamily> OutfitFamily = new(() =>
        new FontFamily(new Uri("pack://application:,,,/MindAttic.Orchestrator.Desktop;component/Fonts/"), "./#Outfit, " + OrchestratorSettings.DefaultFont));

    private static bool registered;

    /// <summary>The family for a saved choice: Outfit when it names Outfit (any case), else Segoe UI.</summary>
    public static FontFamily For(string? choice) =>
        string.Equals(choice?.Trim(), Outfit, StringComparison.OrdinalIgnoreCase) ? OutfitFamily.Value : SegoeFamily.Value;

    /// <summary>The saved family: Segoe UI when none was chosen, or what's saved can't be read.</summary>
    public static FontFamily Saved(JsonStore store)
    {
        try { return For(store.LoadSettings().Font); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { return SegoeFamily.Value; }
    }

    /// <summary>Puts the saved family in <paramref name="resources"/> (the Application's) and has every window, context menu and
    /// tooltip opened from now on follow it. Call before the first window opens.</summary>
    public static FontFamily Apply(ResourceDictionary resources, JsonStore store)
    {
        var family = Saved(store);
        resources[ResourceKey] = family;
        Register();
        return family;
    }

    /// <summary>Swaps the saved family into <paramref name="resources"/> when it isn't the one showing (the settings changed in
    /// MindAttic.Vault.Desktop): every element following <see cref="ResourceKey"/> re-renders in it at once.</summary>
    public static FontFamily Follow(ResourceDictionary? resources, JsonStore store)
    {
        var family = Saved(store);
        if (resources is not null && !Equals(resources[ResourceKey], family)) resources[ResourceKey] = family;
        return family;
    }

    /// <summary>Points each window's FontFamily, and each context menu's and tooltip's (popups outside a window's inheritance),
    /// at <see cref="ResourceKey"/> as it loads or opens. A window's own FontFamily="..." in XAML is replaced; its children
    /// inherit from it unless they set their own.</summary>
    private static void Register()
    {
        if (registered) return;
        registered = true;
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnShown));
        EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.OpenedEvent, new RoutedEventHandler(OnShown));
        // A tooltip is reached through its base class, so no tooltip type is named in code (the library theme draws it).
        EventManager.RegisterClassHandler(typeof(FrameworkElement), ToolTip.OpenedEvent, new RoutedEventHandler(OnShown));
    }

    private static void OnShown(object sender, RoutedEventArgs e)
    {
        if (sender is Control element && ReferenceEquals(sender, e.OriginalSource))
            element.SetResourceReference(Control.FontFamilyProperty, ResourceKey);
    }
}
