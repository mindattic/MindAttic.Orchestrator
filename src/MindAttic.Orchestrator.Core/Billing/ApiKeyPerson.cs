using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Billing;

/// <summary>
/// The Vault's API key as a person of its own (M.ORCH-T906): a member who takes tickets like any other, with its own tab, its
/// own terminals and its own Launcher, whose every CLI bills API credits while everyone else's stays on Claude Team. It is a
/// login folder under <c>~/.claude-accounts</c> named for <see cref="Email"/> (an address no claude.ai account has), with no
/// claude.ai sign-in: its <c>mindattic-account.json</c> says <c>"billing": "api"</c>, which MindAttic.Launcher reads to put
/// the Vault key into every CLI it starts there (and to count the folder signed in), and its <c>.claude.json</c> approves the
/// key so Claude Code uses it without asking. The key itself is never written anywhere by this: the Launcher reads it from the
/// Vault at launch, and Claude Code's own config keeps only its last 20 characters, as for any approved key.
/// <para>It has no 5-hour or weekly Claude window: it's controlled by money (<see cref="ApiSpend"/>), so no pace, cutoff or
/// weekly allowance of a Claude login applies to it.</para>
/// </summary>
public static class ApiKeyPerson
{
    /// <summary>Its login's email: the folder's name, the tickets' assignee, its Launcher's <c>--account</c>.</summary>
    public const string Email = "api-key@mindattic.vault";

    /// <summary>What its tab and its tickets call it.</summary>
    public const string Name = "API key";

    /// <summary>Its terminal tabs' badge.</summary>
    public const string Initials = "API";

    /// <summary>Its tabs' accent: amber, so a CLI billing money is never mistaken for a seat's.</summary>
    public const string Color = "#F9A825";

    /// <summary>The look file's field that marks a login folder's own billing (MindAttic.Launcher's <c>ClaudeBillingMode.OfLogin</c>).</summary>
    public const string BillingField = "billing";

    /// <summary>Whether <paramref name="email"/> is the API key's.</summary>
    public static bool Is(string? email) => string.Equals(AccountDirs.Normalize(email), Email, StringComparison.Ordinal);

    /// <summary>Its login folder.</summary>
    public static AccountDir Dir(AccountDirs dirs) => dirs.For(Email);

    /// <summary>
    /// Makes its login folder ready for a Launcher and its CLIs: a Claude home like any per-CLI login's (<see cref="AccountDirs.Prepare"/>),
    /// its look (name, badge, colour and <c>"billing": "api"</c>), onboarding marked done, and the Vault key approved in its
    /// <c>.claude.json</c>. Null when ready, else why not (no key in the Vault, a file that couldn't be written); never throws.
    /// </summary>
    public static string? Prepare(AccountDirs dirs, BillingSwitch billing)
    {
        try
        {
            if (!billing.HasKey) return $"There's no {BillingSwitch.VaultProvider} key in the MindAttic Vault keyring: add one there to start the API key's Launcher.";
            var dir = Dir(dirs);
            dirs.Prepare(dir);
            WriteLook(dir.Directory);
            EnsureConfig(dir.ConfigPath);
            return billing.SetKeyChoice(dir.ConfigPath, approve: true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Contained: the message never carries the key, which nothing here reads into a string it reports.
            return $"Couldn't make the API key's login folder ready: {ex.Message}";
        }
    }

    /// <summary>Writes its <c>mindattic-account.json</c>: its name, badge and colour, and the billing mark, keeping anything else.</summary>
    internal static void WriteLook(string folder)
    {
        var path = Path.Combine(folder, LoginLook.FileName);
        JsonObject file;
        try { file = File.Exists(path) && JsonNode.Parse(File.ReadAllText(path)) is JsonObject o ? o : []; }
        catch (JsonException) { file = []; }
        var before = file.ToJsonString();
        file["email"] = Email;
        file["alias"] ??= Name;
        file["initials"] ??= Initials;
        file["tabColor"] ??= Color;
        file[BillingField] = BillingModes.Api;
        if (File.Exists(path) && file.ToJsonString() == before) return;
        Directory.CreateDirectory(folder);
        var temp = path + ".tmp";
        File.WriteAllText(temp, file.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>A <c>.claude.json</c> to approve the key in (an empty one when there was nothing to seed it from), marked past
    /// Claude Code's first-run onboarding so the first CLI opens at its prompt.</summary>
    internal static void EnsureConfig(string configPath)
    {
        JsonObject config;
        // A file that won't parse (often one Claude Code is rewriting) is left alone, never replaced with an empty one (M.ORCH-T1089).
        try { config = File.Exists(configPath) && JsonNode.Parse(File.ReadAllText(configPath)) is JsonObject o ? o : []; }
        catch (JsonException ex) { throw new InvalidDataException($"{configPath} couldn't be read as JSON; it's left as it is.", ex); }
        if (File.Exists(configPath) && config["hasCompletedOnboarding"] is JsonValue v && v.TryGetValue<bool>(out var done) && done) return;
        config["hasCompletedOnboarding"] = true;
        var temp = configPath + ".tmp";
        File.WriteAllText(temp, config.ToJsonString(), new UTF8Encoding(false));
        File.Move(temp, configPath, overwrite: true);
    }
}
