using System.IO;
using System.Windows;
using MindAttic.Components.Wpf.Controls;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// The dashboard's light or dark theme (M.ORCH-T678), kept in the orchestrator's settings (<see cref="OrchestratorSettings.Theme"/>,
/// in the database) so it holds across restarts. Dark unless light was chosen: the app applies the saved choice at start, every
/// view following at once (each brush is the library theme's, as DynamicResource or through <see cref="ViewModels.Palette"/>).
/// The dashboard's own window no longer carries a switch of its own (M.ORCH-T1512: it wasn't used often enough to earn a
/// permanent icon); it is set instead from MindAttic.Vault.Desktop's Orchestrator settings page, and <see cref="Follow"/> picks
/// up a change made there live, with no restart. <see cref="SaveSwitches"/> and the library's theme switch (MaToggleIcon with
/// IsThemeToggle) remain here as the
/// general wiring a switch saves through, for whatever UI hosts one.
/// </summary>
public static class ThemeChoice
{
    /// <summary>The saved theme: Dark when none was ever chosen, or what's saved can't be read.</summary>
    public static MaThemeMode Saved(JsonStore store)
    {
        try { return MaTheme.Parse(store.LoadSettings().Theme); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { return MaThemeMode.Dark; }
    }

    /// <summary>Switches <paramref name="resources"/> (the Application's, or a test window's) to the saved theme, and returns it.</summary>
    public static MaThemeMode Apply(ResourceDictionary resources, JsonStore store)
    {
        var theme = Saved(store);
        MaTheme.Set(resources, theme);
        return theme;
    }

    /// <summary>Switches <paramref name="resources"/> to the saved theme when it isn't showing it already (the settings changed
    /// elsewhere: MindAttic.Vault.Desktop's Orchestrator tab, LAMP M.ORCH-T1308), and returns the saved theme.</summary>
    public static MaThemeMode Follow(ResourceDictionary? resources, JsonStore store)
    {
        var theme = Saved(store);
        if (resources is not null && MaTheme.Get(resources) != theme) MaTheme.Set(resources, theme);
        return theme;
    }

    /// <summary>Saves <paramref name="theme"/> as the choice; false (nothing changed) when the settings can't be written, the
    /// switch itself staying in force for this run.</summary>
    public static bool Save(JsonStore store, MaThemeMode theme)
    {
        try
        {
            // LoadSettingsToChange: a settings row that can't be read is never saved over with defaults for the sake of a theme.
            var settings = store.LoadSettingsToChange();
            if (settings.Theme == theme.ToString()) return true;
            store.SaveSettings(settings with { Theme = theme.ToString() });
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            OrchestratorLog.For("Dashboard").Warning(ex, "Couldn't save the {Theme} theme", theme);
            return false;
        }
    }

    /// <summary>Saves every switch a theme MaToggleIcon under <paramref name="root"/> makes.</summary>
    public static void SaveSwitches(UIElement root, JsonStore store) =>
        root.AddHandler(MaToggleIcon.ThemeChangedEvent, new EventHandler<MaThemeChangedEventArgs>((_, e) => Save(store, e.Theme)));
}
