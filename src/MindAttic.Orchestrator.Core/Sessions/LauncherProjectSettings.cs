using System.Text.Json;
using MindAttic.Vault.Settings.Manifest;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// Where a Launcher project's own settings (alias, description, color scheme, tab color, ...) sit in MindAttic Vault
/// (M.ORCH-T2026): the full setting name of its roster row's <c>tabAlias</c>, <c>&lt;prefix&gt;:projects:&lt;i&gt;:tabAlias</c>, with
/// <c>i</c> its index in the Launcher's settings.json <c>projects</c> array, ported from the Launcher's
/// ProjectSetupMenu.SettingFor. Vault.Desktop opens scrolled to it.
/// </summary>
public static class LauncherProjectSettings
{
    /// <summary>The setting name of <paramref name="projectName"/>'s tab alias row, matched by name (ignoring case) or else by
    /// <paramref name="directory"/> against each project's path; null when the project isn't on the roster or the settings
    /// can't be read. The index is the raw array's, not <see cref="ProjectRoster.Load"/>'s sorted, deduplicated one.</summary>
    public static string? SettingFor(string? projectName, string? directory = null, string? settingsPath = null) =>
        IndexOf(projectName, directory, settingsPath ?? ProjectRoster.SettingsPath) is { } index
            ? $"{SettingsManifest.PrefixFor(ProviderCredentials.LauncherAppId)}:projects:{index}:tabAlias"
            : null;

    private static int? IndexOf(string? projectName, string? directory, string file)
    {
        try
        {
            if (!File.Exists(file)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            if (Property(doc.RootElement, "projects") is not { ValueKind: JsonValueKind.Array } list) return null;
            var projects = list.EnumerateArray().ToList();
            if (!string.IsNullOrWhiteSpace(projectName))
                for (var i = 0; i < projects.Count; i++)
                    if (string.Equals(Text(projects[i], "name"), projectName.Trim(), StringComparison.OrdinalIgnoreCase)) return i;
            if (Normalize(directory) is { } dir)
                for (var i = 0; i < projects.Count; i++)
                    if (string.Equals(Normalize(Text(projects[i], "path")), dir, StringComparison.OrdinalIgnoreCase)) return i;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { }
        return null;
    }

    private static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try { return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim())); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return null; }
    }

    private static string? Text(JsonElement element, string name) =>
        Property(element, name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

    private static JsonElement? Property(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        foreach (var p in element.EnumerateObject())
            if (p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return p.Value;
        return null;
    }
}
