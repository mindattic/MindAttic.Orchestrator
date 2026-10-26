using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MindAttic.Orchestrator.Gateway;

/// <summary>What a token lets its holder do: <see cref="Watch"/> sees a session, <see cref="Interact"/> also types into it.</summary>
internal static class GatewayScopes
{
    public const string Watch = "watch";
    public const string Interact = "interact";

    public static bool IsValid(string? scope) => scope?.ToLowerInvariant() is Watch or Interact;

    public static GatewayAccess ToAccess(string scope) => scope == Interact ? GatewayAccess.Interact : GatewayAccess.Watch;
}

/// <summary>A validated token's holder.</summary>
internal sealed record GatewayPrincipal(string Subject, string Scope, DateTime ExpiresUtc);

/// <summary>
/// Short-lived bearer tokens (M.ORCH-T933): <c>v1.&lt;payload&gt;.&lt;signature&gt;</c>, both base64url, the payload a small JSON
/// object (subject, scope, issued, expires, a random id) and the signature its HMAC-SHA256 under the gateway's signing key
/// (<see cref="GatewayKeys"/>). Stateless: a token is good until it expires, at most <see cref="GatewayOptions.MaxTokenMinutes"/>
/// after it was issued; replacing the signing key revokes every token at once. A token is never logged.
/// </summary>
internal sealed class GatewayTokens(byte[] signingKey, Func<DateTime>? clock = null)
{
    private const string Prefix = "v1.";
    private static readonly TimeSpan Skew = TimeSpan.FromSeconds(30);
    private readonly Func<DateTime> now = clock ?? (() => DateTime.UtcNow);

    private sealed record Payload(
        [property: JsonPropertyName("sub")] string Sub,
        [property: JsonPropertyName("scope")] string Scope,
        [property: JsonPropertyName("iat")] long Iat,
        [property: JsonPropertyName("exp")] long Exp,
        [property: JsonPropertyName("jti")] string Jti);

    public (string Token, DateTime ExpiresUtc) Issue(string subject, string scope, TimeSpan lifetime)
    {
        if (!GatewayScopes.IsValid(scope)) throw new ArgumentException($"Unknown scope {scope}.", nameof(scope));
        var max = TimeSpan.FromMinutes(GatewayOptions.MaxTokenMinutes);
        if (lifetime <= TimeSpan.Zero || lifetime > max) lifetime = max;
        var issued = now();
        var expires = issued + lifetime;
        var payload = new Payload(subject, scope.ToLowerInvariant(), ToUnix(issued), ToUnix(expires), Convert.ToHexString(RandomNumberGenerator.GetBytes(8)));
        var body = Prefix + Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload));
        return (body + "." + Base64Url(Sign(body)), DateTimeOffset.FromUnixTimeSeconds(payload.Exp).UtcDateTime);
    }

    /// <summary>The token's holder; null when it is malformed, forged, expired, not yet valid or lived too long.</summary>
    public GatewayPrincipal? Validate(string? token)
    {
        try
        {
            if (string.IsNullOrEmpty(token) || token.Length > 2048 || !token.StartsWith(Prefix, StringComparison.Ordinal)) return null;
            var dot = token.LastIndexOf('.');
            if (dot <= Prefix.Length) return null;
            var body = token[..dot];
            var signature = FromBase64Url(token[(dot + 1)..]);
            if (signature is null || !CryptographicOperations.FixedTimeEquals(signature, Sign(body))) return null;
            if (FromBase64Url(body[Prefix.Length..]) is not { } json) return null;
            var p = JsonSerializer.Deserialize<Payload>(json);
            if (p is null || string.IsNullOrWhiteSpace(p.Sub) || !GatewayScopes.IsValid(p.Scope)) return null;
            var at = now();
            var issued = DateTimeOffset.FromUnixTimeSeconds(p.Iat).UtcDateTime;
            var expires = DateTimeOffset.FromUnixTimeSeconds(p.Exp).UtcDateTime;
            if (issued > at + Skew || expires <= at - Skew || expires - issued > TimeSpan.FromMinutes(GatewayOptions.MaxTokenMinutes)) return null;
            return new GatewayPrincipal(p.Sub, p.Scope, expires);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or ArgumentException) { return null; }
    }

    private byte[] Sign(string body) => HMACSHA256.HashData(signingKey, Encoding.ASCII.GetBytes(body));

    private static long ToUnix(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds();

    internal static string Base64Url(ReadOnlySpan<byte> bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    internal static byte[]? FromBase64Url(string text)
    {
        if (text.Length == 0 || text.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))) return null;
        var padded = text.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        try { return Convert.FromBase64String(padded); }
        catch (FormatException) { return null; }
    }
}

/// <summary>
/// The gateway's two secrets, each 32 random bytes in its own file in the data folder (under the user's own
/// <c>%LOCALAPPDATA%</c>), made on first use: <c>signing.key</c> signs tokens, and <c>owner.key</c> is what the machine owner
/// trades for a token (<c>POST /auth/token</c>) from another machine. Reading either file proves you are the owner, so a token can
/// also be minted locally (<c>MindAttic.Orchestrator.Gateway token</c>). Delete <c>signing.key</c> and restart to revoke every
/// token; delete <c>owner.key</c> to change the owner key.
/// </summary>
internal static class GatewayKeys
{
    public const string SigningKeyFile = "signing.key", OwnerKeyFile = "owner.key";

    public static byte[] LoadOrCreate(string directory, string file)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, file);
        if (File.Exists(path) && TryRead(path) is { Length: >= 32 } existing) return existing;
        var key = RandomNumberGenerator.GetBytes(32);
        var temp = path + ".tmp";
        File.WriteAllText(temp, GatewayTokens.Base64Url(key));
        File.Move(temp, path, overwrite: true);
        return key;
    }

    /// <summary>The owner key as the owner types or pastes it.</summary>
    public static string Text(byte[] key) => GatewayTokens.Base64Url(key);

    /// <summary>Whether <paramref name="presented"/> is the owner key, in constant time.</summary>
    public static bool Matches(byte[] ownerKey, string? presented)
    {
        if (string.IsNullOrEmpty(presented) || presented.Length > 256) return false;
        var given = SHA256.HashData(Encoding.UTF8.GetBytes(presented.Trim()));
        var expected = SHA256.HashData(Encoding.UTF8.GetBytes(Text(ownerKey)));
        return CryptographicOperations.FixedTimeEquals(given, expected);
    }

    private static byte[]? TryRead(string path)
    {
        try { return GatewayTokens.FromBase64Url(File.ReadAllText(path).Trim()); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }
}
