using System.Net.Http.Headers;
using System.Text.Json;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Budget;

/// <summary>
/// Who a member is as claude.ai has it (M.ORCH-T701): the name they go by, their email and their role in the organization.
/// Read from the login's <c>oauthAccount</c> (which Claude Code fetches from claude.ai at each sign-in and refreshes after)
/// or straight from Claude's profile endpoint. A field claude.ai didn't give is null and leaves the member's as it is.
/// </summary>
public sealed record ClaudeProfile(string Email, string? Name, string? Role);

/// <summary>
/// Keeps the members table in step with claude.ai (M.ORCH-T701): a member's name, role and status are claude.ai's, read at
/// sign-in (a login's <c>.claude.json</c> is rewritten by it) and every <see cref="FetchEvery"/> after straight from Claude's
/// profile endpoint, and always written over what the table held. Only members already in the table are synced: who's in the
/// organization is still entered by hand (<c>orchestrator members add</c>).
/// </summary>
public sealed class MemberProfileSync
{
    /// <summary>How often each signed-in login's profile is read from claude.ai itself, apart from its sign-ins.</summary>
    public static readonly TimeSpan FetchEvery = TimeSpan.FromHours(1);

    /// <summary>After a failed fetch (offline, an expired token), how long before trying that login again.</summary>
    public static readonly TimeSpan Backoff = TimeSpan.FromMinutes(10);

    public const string ProfileEndpoint = "https://api.anthropic.com/api/oauth/profile";
    public const string RolesEndpoint = "https://api.anthropic.com/api/oauth/claude_cli/roles";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    /// <summary>Gets an endpoint's JSON with a token; null when it couldn't. Tests swap it.</summary>
    internal Func<string, string, CancellationToken, Task<string?>> Fetch { get; init; } = FetchAsync;

    /// <summary>Whether to ask claude.ai directly at all (off in tests that only read the configs).</summary>
    public bool Ask { get; init; } = true;

    private sealed class Login
    {
        public DateTime ConfigStamp;
        public bool ConfigRead;
        public DateTime NextFetchUtc;
        public Task Pending = Task.CompletedTask;
        public ClaudeProfile? Fetched;
    }

    private readonly Dictionary<string, Login> logins = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The fetches in flight, for tests to await.</summary>
    internal Task Pending => Task.WhenAll(logins.Values.Select(l => l.Pending));

    /// <summary>What the last sync wrote, for the log and tests.</summary>
    public string? LastResult { get; private set; }

    /// <summary>
    /// Syncs each login (its <c>.claude.json</c> and <c>.credentials.json</c>): a config written since the last tick (a sign-in,
    /// or Claude Code refreshing the profile) is read and its member updated; a login whose profile wasn't fetched within
    /// <see cref="FetchEvery"/> is fetched from claude.ai in the background, and applied at the tick after it lands.
    /// </summary>
    public void Tick(OrchestratorDb db, IEnumerable<(string ConfigPath, string CredentialsPath)> configs, DateTime nowUtc)
    {
        foreach (var (configPath, credentialsPath) in configs)
        {
            if (!logins.TryGetValue(configPath, out var login)) logins[configPath] = login = new Login();
            // A fetch that landed since the last tick.
            if (login.Pending.IsCompleted && login.Fetched is { } fetched)
            {
                login.Fetched = null;
                Apply(db, fetched, nowUtc);
            }
            try
            {
                var stamp = File.Exists(configPath) ? File.GetLastWriteTimeUtc(configPath) : default;
                if (!login.ConfigRead || stamp != login.ConfigStamp)
                {
                    (login.ConfigStamp, login.ConfigRead) = (stamp, true);
                    if (Read(configPath) is { } profile) Apply(db, profile, nowUtc);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
            if (Ask && login.Pending.IsCompleted && nowUtc >= login.NextFetchUtc)
            {
                login.NextFetchUtc = nowUtc + Backoff;
                var l = login;
                login.Pending = Task.Run(async () =>
                {
                    if (await FetchProfileAsync(credentialsPath).ConfigureAwait(false) is { } p)
                    {
                        l.Fetched = p;
                        l.NextFetchUtc = nowUtc + FetchEvery;
                    }
                });
            }
        }
    }

    /// <summary>Writes <paramref name="profile"/> over its member's row; whether anything changed. A login that isn't a member
    /// is left alone.</summary>
    public bool Apply(OrchestratorDb db, ClaudeProfile profile, DateTime nowUtc)
    {
        try
        {
            var changed = MemberRoster.Sync(db, profile, nowUtc);
            if (changed) LastResult = $"synced {profile.Email} from claude.ai";
            return changed;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Data.Common.DbException)
        {
            LastResult = $"couldn't sync {profile.Email}: {ex.Message}";
            return false;
        }
    }

    /// <summary>The profile Claude Code keeps for the login signed in at <paramref name="configPath"/>; null when signed out or on an API key.</summary>
    public static ClaudeProfile? Read(string configPath)
    {
        if (!File.Exists(configPath)) return null;
        using var stream = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var doc = JsonDocument.Parse(stream);
        if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("oauthAccount", out var o) ||
            o.ValueKind != JsonValueKind.Object || AccountDirs.Normalize(Text(o, "emailAddress")) is not { } email)
            return null;
        return new ClaudeProfile(email, Text(o, "displayName") ?? Text(o, "fullName"), RoleOf(Text(o, "organizationRole")));
    }

    /// <summary>The profile from Claude's profile endpoint (<paramref name="profileBody"/>) and its roles endpoint
    /// (<paramref name="rolesBody"/>, may be null); null when the profile has no email.</summary>
    internal static ClaudeProfile? Parse(string profileBody, string? rolesBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(profileBody);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("account", out var a) ||
                a.ValueKind != JsonValueKind.Object || AccountDirs.Normalize(Text(a, "email") ?? Text(a, "email_address")) is not { } email)
                return null;
            string? role = null;
            if (rolesBody is not null)
            {
                try
                {
                    using var roles = JsonDocument.Parse(rolesBody);
                    if (roles.RootElement.ValueKind == JsonValueKind.Object) role = RoleOf(Text(roles.RootElement, "organization_role"));
                }
                catch (JsonException) { }
            }
            return new ClaudeProfile(email, Text(a, "display_name") ?? Text(a, "full_name"), role);
        }
        catch (JsonException) { return null; }
    }

    /// <summary>claude.ai's organization role as one of <see cref="MemberRoster.Roles"/> (<c>primary_owner</c> is
    /// <c>primary-owner</c>, <c>membership_admin</c> is <c>admin</c>, <c>user</c> is <c>member</c>); null when it's none of them.</summary>
    public static string? RoleOf(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return null;
        if (MemberRoster.Canonical(role, MemberRoster.Roles) is { } r) return r;
        var v = role.Trim().ToLowerInvariant();
        if (v.Contains("admin", StringComparison.Ordinal)) return "admin";
        return v is "user" or "developer" or "billing" ? "member" : null;
    }

    private async Task<ClaudeProfile?> FetchProfileAsync(string credentialsPath)
    {
        try
        {
            if (ClaudeMeterProbe.Token(credentialsPath, DateTime.UtcNow) is not { } token) return null;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            if (await Fetch(ProfileEndpoint, token, cts.Token).ConfigureAwait(false) is not { } profile) return null;
            string? roles = null;
            try { roles = await Fetch(RolesEndpoint, token, cts.Token).ConfigureAwait(false); }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException) { }
            return Parse(profile, roles);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static async Task<string?> FetchAsync(string endpoint, string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("anthropic-beta", "oauth-2025-04-20");
        request.Headers.UserAgent.ParseAdd("MindAttic.Orchestrator");
        using var response = await Http.SendAsync(request, ct).ConfigureAwait(false);
        return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false) : null;
    }

    private static string? Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s ? s.Trim() : null;
}
