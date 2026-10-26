using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Gateway;

/// <summary>What a token lets its holder do: <see cref="Watch"/> sees a session, <see cref="Interact"/> also types into it.</summary>
internal static class GatewayScopes
{
    public const string Watch = TermGatewayTokens.Watch;
    public const string Interact = TermGatewayTokens.Interact;

    public static bool IsValid(string? scope) => TermGatewayTokens.IsScope(scope);

    public static GatewayAccess ToAccess(string scope) => scope == Interact ? GatewayAccess.Interact : GatewayAccess.Watch;
}

/// <summary>A validated token's holder.</summary>
internal sealed record GatewayPrincipal(string Subject, string Scope, DateTime ExpiresUtc);

/// <summary>
/// Short-lived bearer tokens (M.ORCH-T933): <c>v1.&lt;payload&gt;.&lt;signature&gt;</c>, both base64url, the payload a small JSON
/// object (subject, scope, issued, expires, a random id) and the signature its HMAC-SHA256 under the gateway's signing key
/// (<see cref="GatewayKeys"/>). The format itself is <see cref="TermGatewayTokens"/>, shared with <c>orchestrator gateway token</c>
/// (M.ORCH-T934), which mints a member's token on the owner's machine. Stateless: a token is good until it expires, at most
/// <see cref="GatewayOptions.MaxTokenMinutes"/> after it was issued; replacing the signing key revokes every token at once. A
/// token is never logged.
/// </summary>
internal sealed class GatewayTokens(Func<byte[]> signingKeyNow, Func<DateTime>? clock = null)
{
    private static readonly TimeSpan Skew = TimeSpan.FromSeconds(30);
    private readonly Func<DateTime> now = clock ?? (() => DateTime.UtcNow);

    /// <param name="signingKey">A fixed key (a test, or a one-off command).</param>
    public GatewayTokens(byte[] signingKey, Func<DateTime>? clock = null) : this(() => signingKey, clock) { }

    public (string Token, DateTime ExpiresUtc) Issue(string subject, string scope, TimeSpan lifetime) =>
        TermGatewayTokens.Issue(signingKeyNow(), subject, scope, lifetime, now());

    /// <summary>The token's holder; null when it is malformed, forged, expired, not yet valid or lived too long.</summary>
    public GatewayPrincipal? Validate(string? token)
    {
        try
        {
            if (string.IsNullOrEmpty(token) || token.Length > 2048 || !token.StartsWith(TermGatewayTokens.Prefix, StringComparison.Ordinal)) return null;
            var dot = token.LastIndexOf('.');
            if (dot <= TermGatewayTokens.Prefix.Length) return null;
            var body = token[..dot];
            var signature = FromBase64Url(token[(dot + 1)..]);
            if (signature is null || !CryptographicOperations.FixedTimeEquals(signature, TermGatewayTokens.Sign(signingKeyNow(), body))) return null;
            if (FromBase64Url(body[TermGatewayTokens.Prefix.Length..]) is not { } json) return null;
            var p = JsonSerializer.Deserialize<TermGatewayTokenPayload>(json);
            if (p is null || string.IsNullOrWhiteSpace(p.Sub) || !GatewayScopes.IsValid(p.Scope)) return null;
            var at = now();
            var issued = DateTimeOffset.FromUnixTimeSeconds(p.Iat).UtcDateTime;
            var expires = DateTimeOffset.FromUnixTimeSeconds(p.Exp).UtcDateTime;
            if (issued > at + Skew || expires <= at - Skew || expires - issued > TimeSpan.FromMinutes(GatewayOptions.MaxTokenMinutes)) return null;
            return new GatewayPrincipal(p.Sub, p.Scope, expires);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or ArgumentException) { return null; }
    }

    /// <summary>Whether a token that validated as <paramref name="principal"/> has since expired (M.ORCH-T1041), by the same clock
    /// and skew as <see cref="Validate"/>: when it no longer validates and this is false, the signing key changed.</summary>
    public bool Expired(GatewayPrincipal principal) => principal.ExpiresUtc <= now() - Skew;

    internal static string Base64Url(ReadOnlySpan<byte> bytes) => TermGatewayTokens.Base64Url(bytes);

    internal static byte[]? FromBase64Url(string text) => TermGatewayTokens.FromBase64Url(text);
}

/// <summary>
/// The serving gateway's signing key as <c>signing.key</c> holds it now (M.ORCH-T983): the file's time is looked at, at most once
/// every <see cref="Recheck"/>, and the key read again when it changes, so replacing or deleting the file revokes every token,
/// open streams included (each re-validates its token), without a restart. A gone or unusable file gets a new key (as at
/// first use); a file that can't be read just now keeps the key it had, since making a new one then would revoke every token
/// over a sharing violation. The key is never logged.
/// </summary>
internal sealed class SigningKeyFile
{
    public static readonly TimeSpan Recheck = TimeSpan.FromSeconds(1);
    private readonly string directory;
    private readonly string path;
    private readonly System.Threading.Lock gate = new();
    private byte[] key;
    private DateTime stamp, checkedUtc;

    public SigningKeyFile(string directory)
    {
        this.directory = directory;
        path = Path.Combine(directory, GatewayKeys.SigningKeyFile);
        key = GatewayKeys.LoadOrCreate(directory, GatewayKeys.SigningKeyFile);
        stamp = WrittenUtc();
        checkedUtc = DateTime.UtcNow;
    }

    /// <summary>The key now.</summary>
    public byte[] Current
    {
        get
        {
            lock (gate)
            {
                var at = DateTime.UtcNow;
                if (at - checkedUtc < Recheck) return key;
                checkedUtc = at;
                try
                {
                    if (!File.Exists(path))
                    {
                        key = GatewayKeys.LoadOrCreate(directory, GatewayKeys.SigningKeyFile);
                        stamp = WrittenUtc();
                        return key;
                    }
                    var written = File.GetLastWriteTimeUtc(path);
                    if (written == stamp) return key;
                    var read = TermGatewayTokens.FromBase64Url(File.ReadAllText(path).Trim());
                    key = read is { Length: >= 32 } ? read : GatewayKeys.LoadOrCreate(directory, GatewayKeys.SigningKeyFile);
                    stamp = read is { Length: >= 32 } ? written : WrittenUtc();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                return key;
            }
        }
    }

    private DateTime WrittenUtc()
    {
        try { return File.GetLastWriteTimeUtc(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return DateTime.MinValue; }
    }
}

/// <summary>
/// The gateway's two secrets, each 32 random bytes in its own file in the data folder (under the user's own
/// <c>%LOCALAPPDATA%</c>), made on first use: <c>signing.key</c> signs tokens, and <c>owner.key</c> is what the machine owner
/// trades for a token (<c>POST /auth/token</c>) from another machine. Reading either file proves you are the owner, so a token can
/// also be minted locally (<c>MindAttic.Orchestrator.Gateway token</c>, or <c>orchestrator gateway token &lt;who&gt;</c> for a
/// member). Delete or replace <c>signing.key</c> to revoke every token (the serving gateway notices within a second and
/// closes open streams within a watch interval, <see cref="SigningKeyFile"/>); delete <c>owner.key</c> to change the owner key.
/// </summary>
internal static class GatewayKeys
{
    public const string SigningKeyFile = TermGatewayTokens.SigningKeyFile, OwnerKeyFile = TermGatewayTokens.OwnerKeyFile;

    public static byte[] LoadOrCreate(string directory, string file) => TermGatewayTokens.LoadOrCreateKey(directory, file);

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
}
