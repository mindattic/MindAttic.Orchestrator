using System.IO;
using System.Windows;
using MindAttic.Components.Wpf.Controls;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// The dashboard's light or dark theme (M.ORCH-T678), kept in the orchestrator's settings (<see cref="OrchestratorSettings.Theme"/>,
/// in the database) so it holds across restarts. Dark unless light was chosen: the app applies the saved choice at start, and
/// the library's MaThemeToggle in the window's upper-right corner switches it live, every view following at once (each brush is
/// the library theme's, as DynamicResource or through <see cref="ViewModels.Palette"/>), and each switch is saved.
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

    /// <summary>Saves every switch an MaThemeToggle under <paramref name="root"/> makes.</summary>
    public static void SaveSwitches(UIElement root, JsonStore store) =>
        root.AddHandler(MaThemeToggle.ThemeChangedEvent, new EventHandler<MaThemeChangedEventArgs>((_, e) => Save(store, e.Theme)));
}
