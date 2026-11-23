using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// The Launcher's agent providers as the Settings menu's Provider and Model pickers offer them (M.ORCH-T2041, M.ORCH-T2106):
/// read straight from MindAttic.Launcher's own settings.json (<c>agentProviders</c>), the way
/// <see cref="BackgroundAgent.ClaudeCommand"/> already reads its Claude command, rather than a project reference to the
/// Launcher's exe. Mirrors the Launcher's <c>AgentProviderRegistry</c> (its <c>Defaults</c> when nothing's configured, its
/// <c>KnownModels</c>) and <c>ProviderModel</c> (the <c>--model</c>/<c>-m</c> token in a provider's RunCommand). The
/// Launcher's <c>host</c> has no <c>--model</c> of its own, so a model picked here reaches the CLI the same way the
/// Launcher's Settings model picker does: rewritten into that provider's RunCommand (<see cref="SetModel"/>).
/// </summary>
public static partial class LauncherProviders
{
    /// <summary>One provider: its key (what <c>host --provider</c> takes), display name and RunCommand.</summary>
    public sealed record Provider(string Key, string Name, string RunCommand)
    {
        /// <summary>The model its RunCommand names, or null for the CLI's own default.</summary>
        public string? Model => ModelOf(RunCommand);
    }

    /// <summary>The Launcher's own defaults (AgentProviderRegistry.Defaults), used when its settings configure none.</summary>
    public static IReadOnlyList<Provider> Defaults { get; } =
    [
        new(Providers.Claude, "Claude Code", "claude --dangerously-skip-permissions --model claude-sonnet-5-5"),
        new(Providers.Codex, "OpenAI Codex", "codex --dangerously-bypass-approvals-and-sandbox"),
        new(Providers.Gemini, "Google Antigravity", "agy --dangerously-skip-permissions"),
        new("Kimi", "Kimi Code", "kimi --yolo"),
    ];

    /// <summary>Known model ids per provider key, newest first (AgentProviderRegistry.KnownModels).</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> KnownModels { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [Providers.Claude] =
            [
                "claude-fable-5", "claude-opus-4-8", "claude-opus-4-7", "claude-opus-4-6",
                "claude-sonnet-5-5", "claude-sonnet-5", "claude-sonnet-4-6", "claude-haiku-4-5",
            ],
        };

    /// <summary>The Launcher's settings.json.</summary>
    public static string SettingsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MindAttic", "MindAttic.Launcher", "settings.json");

    /// <summary>The configured providers (a complete entry each), else <see cref="Defaults"/>.</summary>
    public static IReadOnlyList<Provider> All(string? settingsPath = null)
    {
        try
        {
            if (JsonNode.Parse(File.ReadAllText(settingsPath ?? SettingsPath)) is JsonObject root &&
                Field(root, "agentProviders") is JsonArray array)
            {
                var configured = array.OfType<JsonObject>()
                    .Select(p => new Provider(Text(p, "key"), Text(p, "name"), Text(p, "runCommand")))
                    .Where(p => p.Key.Length > 0 && p.Name.Length > 0 && p.RunCommand.Length > 0)
                    .ToList();
                if (configured.Count > 0) return configured;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { }
        return Defaults;
    }

    /// <summary>The models to offer for <paramref name="provider"/>: its current one first (when set), then the known ones.</summary>
    public static IReadOnlyList<string> ModelsFor(Provider provider)
    {
        List<string> models = [];
        if (provider.Model is { } current) models.Add(current);
        foreach (var m in KnownModels.GetValueOrDefault(provider.Key) ?? [])
            if (!models.Contains(m, StringComparer.OrdinalIgnoreCase)) models.Add(m);
        return models;
    }

    /// <summary>
    /// Sets <paramref name="providerKey"/>'s model in the Launcher's settings (AgentProviderRegistry.SetModel): its RunCommand's
    /// <c>--model</c> rewritten, or cleared for a blank <paramref name="model"/>. With no providers configured yet the defaults
    /// are written first, so the edit has a home; every other setting is left as it is. Returns an error, or null.
    /// </summary>
    public static string? SetModel(string providerKey, string? model, string? settingsPath = null)
    {
        var path = settingsPath ?? SettingsPath;
        try
        {
            var root = File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? [] : [];
            var name = root.FirstOrDefault(p => p.Key.Equals("agentProviders", StringComparison.OrdinalIgnoreCase)).Key ?? "agentProviders";
            if (root[name] is not JsonArray array || array.Count == 0)
            {
                array = [.. Defaults.Select(d => (JsonNode)new JsonObject { ["key"] = d.Key, ["name"] = d.Name, ["runCommand"] = d.RunCommand })];
                root[name] = array;
            }
            var entry = array.OfType<JsonObject>().FirstOrDefault(p => Text(p, "key").Equals(providerKey, StringComparison.OrdinalIgnoreCase));
            if (entry is null) return $"The Launcher has no provider {providerKey}.";
            var field = entry.FirstOrDefault(p => p.Key.Equals("runCommand", StringComparison.OrdinalIgnoreCase)).Key ?? "runCommand";
            entry[field] = WithModel(Text(entry, "runCommand"), model);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return $"Couldn't set {providerKey}'s model in the Launcher's settings: {ex.Message}";
        }
    }

    // ProviderModel's own pattern: leading whitespace included so clearing the flag leaves no double space.
    [GeneratedRegex("""(?<lead>\s*)(?<flag>--model|-m)[=\s]+(?<value>"[^"]*"|\S+)""", RegexOptions.IgnoreCase)]
    private static partial Regex ModelFlag();

    /// <summary>The model id in <paramref name="runCommand"/> (ProviderModel.Get), or null.</summary>
    public static string? ModelOf(string? runCommand)
    {
        if (string.IsNullOrWhiteSpace(runCommand)) return null;
        var m = ModelFlag().Match(runCommand);
        if (!m.Success) return null;
        var value = m.Groups["value"].Value;
        return value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;
    }

    /// <summary><paramref name="runCommand"/> with its model set to <paramref name="model"/> (ProviderModel.Set).</summary>
    public static string WithModel(string? runCommand, string? model)
    {
        var command = runCommand ?? "";
        var trimmed = model?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return ModelFlag().Replace(command, "").Trim();
        if (ModelFlag().IsMatch(command)) return ModelFlag().Replace(command, m => $"{m.Groups["lead"].Value}--model {trimmed}");
        return string.IsNullOrWhiteSpace(command) ? $"--model {trimmed}" : $"{command.TrimEnd()} --model {trimmed}";
    }

    private static JsonNode? Field(JsonObject o, string name) =>
        o.FirstOrDefault(p => p.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;

    private static string Text(JsonObject o, string name) =>
        Field(o, name) is JsonValue v && v.TryGetValue<string>(out var s) ? s.Trim() : "";
}
