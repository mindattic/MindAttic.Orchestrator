using MindAttic.Orchestrator.Settings;
using MindAttic.Vault.Credentials;

namespace MindAttic.Orchestrator.Billing;

/// <summary>
/// The orchestrator's API keys under LAMP (M.ORCH-T1308): the shared MindAttic named keys (MindAttic.Vault.Desktop's Keys tab),
/// never a copy of its own. Claude's is the only one it uses: the key the API billing mode and the API key person bill
/// (<see cref="BillingSwitch"/>). When keys are assigned to the Orchestrator for Claude on its Vault tab, the first usable one
/// (in the assigned order, skipping disabled, failed and cooling-down keys) is its key; with none assigned it is exactly the key
/// it always used, the Vault keyring's Claude key. Read on every call, so a change in Vault applies without a restart.
/// </summary>
public sealed class OrchestratorKeys
{
    public const string ClaudeProvider = "claude";

    private readonly NamedKeyStore keys;
    private readonly KeySelector selector;
    private readonly Func<string?> fallback;

    /// <param name="keys">The shared named keys and their assignments.</param>
    /// <param name="health">Their observed health; null treats every key as healthy.</param>
    /// <param name="fallback">The key used with no assignment: the Vault keyring's Claude key, as before LAMP.</param>
    public OrchestratorKeys(NamedKeyStore keys, KeyHealthStore? health = null, Func<string?>? fallback = null)
    {
        this.keys = keys ?? throw new ArgumentNullException(nameof(keys));
        selector = new KeySelector(new FileKeySource(keys), health);
        this.fallback = fallback ?? (() => LlmCredentialStore.Default.GetKey(BillingSwitch.VaultProvider));
    }

    /// <summary>The current user's keys (the Vault roots, honouring <c>MINDATTIC_VAULT_ROAMING_ROOT</c>/<c>_LOCAL_ROOT</c>).</summary>
    public static OrchestratorKeys ForCurrentUser() => new(NamedKeyStore.ForRoamingRoot(), KeyHealthStore.ForLocalRoot());

    /// <summary>Whether keys are assigned to the Orchestrator for <paramref name="providerId"/> on its Vault tab.</summary>
    public bool HasAssignment(string providerId)
    {
        try { return keys.Assignments.Get(OrchestratorVaultSettings.AppId, providerId).Count > 0; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { return false; }
    }

    /// <summary>The Claude key the orchestrator bills: its assigned active key, else the shared keyring's Claude key. Null when
    /// there is none (or every assigned key is disabled or failing).</summary>
    public string? ClaudeKey()
    {
        if (!HasAssignment(ClaudeProvider)) return fallback();
        return selector.GetActiveKey(ClaudeProvider, OrchestratorVaultSettings.AppId)?.Value;
    }
}
