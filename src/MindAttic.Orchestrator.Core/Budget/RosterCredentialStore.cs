using System.Text;
using System.Text.Json;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Vault.Credentials;
using MindAttic.Vault.Paths;

namespace MindAttic.Orchestrator.Budget;

/// <summary>
/// Each roster member's own email account credential (M.ORCH-T1898): a plain email+password pair Ryan reviews, adds, edits
/// and deletes from the Orchestrator's User Management modal, kept in MindAttic.Vault's keyring - never in a
/// <c>[Setting]</c>-decorated settings class (<see cref="MindAttic.Orchestrator.Settings.OrchestratorVaultSettings"/>), which
/// would both fail its secret-name guard and land in <c>data/orchestrator.db</c>, which gets committed to git. Keyed by the
/// member's normalized email, mirroring <see cref="MindAttic.Vault.Credentials.BrokerCredentialStore"/>'s per-id keyring but
/// with the simpler flat {email, password} payload this feature needs.
/// </summary>
public sealed class RosterCredentialStore : CredentialStore
{
    /// <summary>Bucket folder name under <c>%APPDATA%\MindAttic\</c>.</summary>
    public const string Bucket = "OrchestratorRoster";

    /// <summary>Environment variable that overrides the resolved bucket directory (tests; mirrors the other credential stores').</summary>
    public const string DirectoryEnvVar = "MINDATTIC_ORCHESTRATOR_ROSTER_CREDENTIALS";

    /// <summary>
    /// Default instance pointed at <c>%APPDATA%\MindAttic\OrchestratorRoster\</c> (or <see cref="DirectoryEnvVar"/> if set).
    /// </summary>
    public static RosterCredentialStore Default { get; } = new(ResolveDefaultDirectory());

    /// <summary>Construct a roster credential store rooted at <paramref name="directory"/>.</summary>
    /// <inheritdoc />
    public RosterCredentialStore(string directory) : base(directory) { }

    private static string ResolveDefaultDirectory()
    {
        var overrideDir = Environment.GetEnvironmentVariable(DirectoryEnvVar);
        return string.IsNullOrWhiteSpace(overrideDir) ? VaultPaths.RoamingBucket(Bucket) : overrideDir;
    }

    /// <summary>A member's stored email-account credential.</summary>
    /// <param name="Email">The member's normalized email (the store's own key).</param>
    /// <param name="Password">The stored password, as entered - never masked or hashed; this is a plain reviewable credential, not an auth secret this app itself checks.</param>
    public sealed record RosterCreds(string Email, string Password);

    /// <summary><paramref name="memberEmail"/>'s stored credential, or null when there isn't one (not a member, or an empty/malformed entry).</summary>
    public RosterCreds? Get(string memberEmail)
    {
        if (AccountDirs.Normalize(memberEmail) is not { } key) return null;
        if (!LoadAllRaw().TryGetValue(key, out var json) || string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            var password = doc.RootElement.TryGetProperty("password", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "";
            return new RosterCreds(key, password);
        }
        catch (JsonException) { return null; }
    }

    /// <summary>Every stored credential, by email.</summary>
    public IReadOnlyList<RosterCreds> GetAll() =>
        [.. LoadAllRaw().Keys.Select(Get).Where(c => c is not null).Select(c => c!)];

    /// <summary>Upserts <paramref name="memberEmail"/>'s credential (add or edit - the same call either way).</summary>
    /// <exception cref="ArgumentException"><paramref name="memberEmail"/> isn't an email address.</exception>
    public void Set(string memberEmail, string password)
    {
        var key = AccountDirs.Normalize(memberEmail) ?? throw new ArgumentException($"\"{memberEmail}\" isn't an email address.", nameof(memberEmail));
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
        {
            w.WriteStartObject();
            w.WriteString("email", key);
            w.WriteString("password", password ?? "");
            w.WriteEndObject();
        }
        SaveRaw(key, Encoding.UTF8.GetString(ms.ToArray()));
    }

    /// <summary>Removes <paramref name="memberEmail"/>'s stored credential entirely (not just emptied - the base class's
    /// <see cref="CredentialStore.SaveRaw"/> alone would leave an empty entry behind).</summary>
    public void Remove(string memberEmail)
    {
        if (AccountDirs.Normalize(memberEmail) is not { } key) return;
        var all = LoadAllRaw();
        if (all.Remove(key)) SaveAllRaw(all);
    }
}
