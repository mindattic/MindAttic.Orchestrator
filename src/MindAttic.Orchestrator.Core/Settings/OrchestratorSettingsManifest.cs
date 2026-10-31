using MindAttic.Vault.Settings.Manifest;

namespace MindAttic.Orchestrator.Settings;

/// <summary>
/// <c>--write-settings-manifest</c> (LAMP, M.ORCH-T1308), which run.ps1 runs after installing: imports today's settings from the
/// orchestrator's database into the MindAttic Vault file once (<see cref="JsonStore.SyncVaultSettings"/>), then lets Vault's
/// <see cref="SettingsManifestCommand"/> write <c>settings.manifest.json</c> beside it, so MindAttic.Vault.Desktop shows the
/// Orchestrator tab with the real values. When the settings row can't be read now, nothing is seeded (Vault's defaults would
/// otherwise stand in for every real value) and it fails.
/// </summary>
public static class OrchestratorSettingsManifest
{
    public const string Switch = SettingsManifestCommand.Switch;

    /// <summary>When <paramref name="args"/> holds <see cref="Switch"/>: imports, writes the manifest, prints where, and returns
    /// true with <paramref name="exitCode"/> set (0 done, 1 failed); otherwise false and nothing is done.</summary>
    public static bool TryRun(string[] args, TextWriter output, out int exitCode, JsonStore? store = null)
    {
        exitCode = 0;
        if (args is null || !args.Any(a => string.Equals(a, Switch, StringComparison.OrdinalIgnoreCase))) return false;
        store ??= new JsonStore();
        if (store.Vault is null)
        {
            output.WriteLine("This store has no MindAttic Vault settings (a scratch database); nothing written.");
            exitCode = 1;
            return true;
        }
        try
        {
            store.SyncVaultSettings();
            if (!store.TryLoadSettings(out _) || !store.Vault.Exists())
            {
                output.WriteLine($"The settings in {store.Describe(JsonStore.SettingsName)} couldn't be read, so nothing was imported into {store.Vault.FilePath}.");
                exitCode = 1;
                return true;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            output.WriteLine($"Couldn't import the settings into {store.Vault.FilePath}: {ex.Message}");
            exitCode = 1;
            return true;
        }
        // The Vault file exists now, so this only loads it and writes the manifest; it never seeds defaults over it.
        SettingsManifestCommand.TryRun<OrchestratorVaultSettings>([Switch], output);
        return true;
    }
}
