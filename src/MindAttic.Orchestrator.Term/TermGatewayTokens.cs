using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MindAttic.Orchestrator.Term;

/// <summary>
/// The team gateway's bearer-token format and its key files (M.ORCH-T933), here in the dependency-free Term library so the
/// gateway that checks a token and <c>orchestrator gateway token &lt;who&gt;</c> (M.ORCH-T934), which mints one for a member on
/// the owner's machine, share a single definition. A token is <c>v1.&lt;payload&gt;.&lt;signature&gt;</c>: the payload base64url
/// JSON (<see cref="TermGatewayTokenPayload"/>), the signature its HMAC-SHA256 under <c>signing.key</c>. A token is never logged.
/// </summary>
public static class TermGatewayTokens
{
    public const string Prefix = "v1.";
    public const string Watch = "watch", Interact = "interact";
    public const string SigningKeyFile = "signing.key", OwnerKeyFile = "owner.key";

    /// <summary>The longest a session token may live.</summary>
    public const int MaxMinutes = 60;

    /// <summary>The longest, and default, life of a member token minted by <c>orchestrator gateway token</c> (M.ORCH-T1056): a
    /// teammate keeps watching without a new token every hour. Revoking their grants or replacing the signing key still cuts them
    /// off at once.</summary>
    public const int MemberDays = 365;

    /// <summary>The payload kind of a member token.</summary>
    public const string MemberKind = "member";

    public static bool IsScope(string? scope) => scope?.ToLowerInvariant() is Watch or Interact;

    /// <summary>The longest <paramref name="kind"/> of token may live.</summary>
    public static TimeSpan MaxLifetime(string? kind) => kind == MemberKind ? TimeSpan.FromDays(MemberDays) : TimeSpan.FromMinutes(MaxMinutes);

    /// <summary>A token for <paramref name="subject"/> with <paramref name="scope"/>, living <paramref name="lifetime"/> from
    /// <paramref name="nowUtc"/>: at most <see cref="MaxMinutes"/>, or <see cref="MemberDays"/> for a <paramref name="member"/> token.</summary>
    public static (string Token, DateTime ExpiresUtc) Issue(byte[] signingKey, string subject, string scope, TimeSpan lifetime, DateTime nowUtc,
        bool member = false)
    {
        if (string.IsNullOrWhiteSpace(subject)) throw new ArgumentException("A token needs a subject.", nameof(subject));
        if (!IsScope(scope)) throw new ArgumentException($"Unknown scope {scope}.", nameof(scope));
        var kind = member ? MemberKind : null;
        var max = MaxLifetime(kind);
        if (lifetime <= TimeSpan.Zero || lifetime > max) lifetime = max;
        var expires = nowUtc + lifetime;
        var payload = new TermGatewayTokenPayload(subject.Trim(), scope.ToLowerInvariant(), ToUnix(nowUtc), ToUnix(expires),
            Convert.ToHexString(RandomNumberGenerator.GetBytes(8)), kind);
        var body = Prefix + Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload));
        return (body + "." + Base64Url(Sign(signingKey, body)), DateTimeOffset.FromUnixTimeSeconds(payload.Exp).UtcDateTime);
    }

    public static byte[] Sign(byte[] signingKey, string body) => HMACSHA256.HashData(signingKey, Encoding.ASCII.GetBytes(body));

    /// <summary>The key in <paramref name="file"/> under <paramref name="directory"/>: 32 random bytes, made on first use.</summary>
    public static byte[] LoadOrCreateKey(string directory, string file)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, file);
        if (File.Exists(path) && TryRead(path) is { Length: >= 32 } existing) return existing;
        var key = RandomNumberGenerator.GetBytes(32);
        var temp = path + ".tmp";
        File.WriteAllText(temp, Base64Url(key));
        File.Move(temp, path, overwrite: true);
        return key;
    }

    public static string Base64Url(ReadOnlySpan<byte> bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[]? FromBase64Url(string text)
    {
        if (text.Length == 0 || text.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))) return null;
        var padded = text.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        try { return Convert.FromBase64String(padded); }
        catch (FormatException) { return null; }
    }

    public static long ToUnix(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds();

    private static byte[]? TryRead(string path)
    {
        try { return FromBase64Url(File.ReadAllText(path).Trim()); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }
}

/// <summary>A token's payload: subject, scope, issued and expires (Unix seconds), a random id, and its kind (null for a session
/// token, <see cref="TermGatewayTokens.MemberKind"/> for a member token).</summary>
public sealed record TermGatewayTokenPayload(
    [property: JsonPropertyName("sub")] string Sub,
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("iat")] long Iat,
    [property: JsonPropertyName("exp")] long Exp,
    [property: JsonPropertyName("jti")] string Jti,
    [property: JsonPropertyName("kind"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Kind = null);
