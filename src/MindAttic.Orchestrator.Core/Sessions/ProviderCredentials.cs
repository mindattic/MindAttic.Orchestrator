using MindAttic.Orchestrator.Settings;
using MindAttic.Vault.Credentials;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// A non-Claude CLI's API key, from the shared MindAttic keys (the Vault), put where that CLI looks for it when the Orchestrator
/// starts a session for it (M.ORCH-T2042, ported from MindAttic.Launcher's <c>ProviderCredentials</c>/<c>LlmCredentials</c>/
/// <c>KimiConfigSync</c>): Codex and Gemini read theirs from an environment variable (<see cref="EnvironmentFor"/>, set on the
/// hosted CLI's process, never on a command line); Kimi only from its own <c>config.toml</c>, synced before it starts
/// (<see cref="Prepare"/>, <see cref="KimiConfigSync"/>). A provider with no key in the Vault gets nothing: its CLI falls back to
/// however it's already configured (its own login, a key already in its config), never a failure.
/// <para>Claude is left out on purpose: its billing is the Orchestrator's own (the per-CLI login folders, the API key person,
/// <see cref="PaneRequest.ApiKeySetup"/>), and the Launcher's <c>host</c> applies the billing mode itself.</para>
/// </summary>
public static class ProviderCredentials
{
    /// <summary>The providers whose CLI reads its API key straight from an environment variable, and that variable.</summary>
    public static IReadOnlyDictionary<string, string> EnvVarByProvider { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [Providers.Codex] = "OPENAI_API_KEY",
            [Providers.Gemini] = "GEMINI_API_KEY",
        };

    /// <summary>Kimi's provider key.</summary>
    public const string Kimi = "Kimi";

    /// <summary>The Launcher's own Vault app id (its <c>SettingsStore.AppBucket</c>), whose active key the Launcher pushes.</summary>
    public const string LauncherAppId = "MindAttic.Launcher";

    /// <summary>Whether <paramref name="provider"/> gets a key from here at all (Codex, Gemini, Kimi).</summary>
    public static bool Handles(string? provider) =>
        provider is not null && (EnvVarByProvider.ContainsKey(provider) || provider.Equals(Kimi, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The key to give <paramref name="provider"/>'s CLI: the Orchestrator's own active key when keys are assigned to it for that
    /// provider on its Vault tab; else the Launcher's active key (its assignment, else the shared order), so a CLI the
    /// Orchestrator starts gets the same key one the Launcher starts would; else the Vault keyring's entry. Null when there's
    /// none, or the Vault can't be read.
    /// </summary>
    public static string? ResolveKey(string provider, NamedKeyStore keys, KeyHealthStore? health = null, Func<string, string?>? keyring = null)
    {
        try
        {
            var selector = new KeySelector(new FileKeySource(keys), health);
            var key = HasAssignment(keys, OrchestratorVaultSettings.AppId, provider)
                ? selector.GetActiveKey(provider, OrchestratorVaultSettings.AppId)?.Value
                : selector.GetActiveKey(provider, LauncherAppId)?.Value;
            if (string.IsNullOrWhiteSpace(key)) key = (keyring ?? LlmCredentialStore.Default.GetKey)(provider);
            return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>The current user's key for <paramref name="provider"/> (<see cref="ResolveKey"/> on the real Vault roots).</summary>
    public static string? KeyFor(string provider) =>
        ResolveKey(provider, NamedKeyStore.ForRoamingRoot(), KeyHealthStore.ForLocalRoot());

    /// <summary>
    /// The variables to set on <paramref name="provider"/>'s CLI process: its key's variable (<see cref="EnvVarByProvider"/>) when
    /// the Vault has one; empty for any other provider (Claude, Kimi, the Launcher's menu) or no key. <paramref name="resolve"/>
    /// defaults to <see cref="KeyFor"/>; tests pass their own, so a run never reads the real Vault.
    /// </summary>
    public static IReadOnlyDictionary<string, string?> EnvironmentFor(string? provider, Func<string, string?>? resolve = null)
    {
        if (provider is null || !EnvVarByProvider.TryGetValue(provider, out var variable)) return new Dictionary<string, string?>();
        return (resolve ?? KeyFor)(provider) is { } key
            ? new Dictionary<string, string?> { [variable] = key }
            : new Dictionary<string, string?>();
    }

    /// <summary>
    /// What has to happen on disk before <paramref name="provider"/>'s CLI starts: for Kimi, the Vault key synced into its
    /// <c>config.toml</c> (<paramref name="kimiConfigPath"/>, the user's real one by default). A no-op for any other provider.
    /// True when a file was rewritten. Never throws.
    /// </summary>
    public static bool Prepare(string? provider, Func<string, string?>? resolve = null, string? kimiConfigPath = null)
    {
        if (provider is null || !provider.Equals(Kimi, StringComparison.OrdinalIgnoreCase)) return false;
        return (resolve ?? KeyFor)(Kimi) is { } key
            && KimiConfigSync.EnsureApiKey(kimiConfigPath ?? KimiConfigSync.DefaultConfigPath, KimiConfigSync.ProviderTable, key);
    }

    private static bool HasAssignment(NamedKeyStore keys, string appId, string provider)
    {
        try { return keys.Assignments.Get(appId, provider).Count > 0; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { return false; }
    }
}
