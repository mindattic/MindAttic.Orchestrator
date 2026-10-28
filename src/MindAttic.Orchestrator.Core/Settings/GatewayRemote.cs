using System.Text;
using System.Text.Json;
using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Settings;

/// <summary>
/// A teammate's gateway the Team Wall watches (M.ORCH-T936), in <see cref="GatewaySettings.Remotes"/>: a name, whose CLIs they
/// are, the gateway's URL, its certificate pin and the token the teammate issued for this member
/// (<c>orchestrator gateway token &lt;who&gt;</c> on their machine, M.ORCH-T934). The token is kept sealed with DPAPI for the
/// current Windows user: the settings never hold it in the clear, and another user or machine can't open it.
/// </summary>
public sealed record GatewayRemote
{
    /// <summary>The short name it is added, removed and popped out by (unique, case-insensitive).</summary>
    public string Name { get; init; } = "";

    /// <summary>Whose CLIs they are, as the wall labels them; the name when empty.</summary>
    public string? Person { get; init; }

    /// <summary>The gateway's base URL, <c>https://host:port</c> (<see cref="GatewayRemotes.NormalizeUrl"/>).</summary>
    public string Url { get; init; } = "";

    /// <summary>The SHA-256 of the gateway's certificate (64 hex characters), as <c>orchestrator gateway</c> prints it on the
    /// teammate's machine. Null: the certificate must be one Windows trusts.</summary>
    public string? CertSha256 { get; init; }

    /// <summary>The bearer token, DPAPI-sealed for this Windows user and base64 (<see cref="GatewayRemotes.Seal"/>).</summary>
    public string? SealedToken { get; init; }

    /// <summary>Who the wall says these CLIs belong to.</summary>
    public string Who => string.IsNullOrWhiteSpace(Person) ? Name : Person.Trim();
}

/// <summary>Helpers for <see cref="GatewayRemote"/>: URLs, pins and the sealed token. Nothing here logs a token.</summary>
public static class GatewayRemotes
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("MindAttic.Orchestrator.GatewayRemote.v1");

    /// <summary>A gateway URL as <c>https://host:port</c>: <c>host</c>, <c>host:port</c>, <c>https://…</c> and <c>wss://…</c> are
    /// all taken (no port means <see cref="Term.TermGateway.DefaultPort"/>); null when it isn't one, or is plain http.</summary>
    public static string? NormalizeUrl(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim();
        var hadScheme = t.Contains("://", StringComparison.Ordinal);
        if (!hadScheme) t = "https://" + t;
        if (!Uri.TryCreate(t, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme is not ("https" or "wss")) return null;
        if (string.IsNullOrEmpty(uri.Host)) return null;
        var port = uri.IsDefaultPort && !HasExplicitPort(t) ? Term.TermGateway.DefaultPort : uri.Port;
        var host = uri.HostNameType == UriHostNameType.IPv6 ? "[" + uri.IdnHost.Trim('[', ']') + "]" : uri.IdnHost;
        return $"https://{host}:{port}";
    }

    private static bool HasExplicitPort(string url)
    {
        var rest = url[(url.IndexOf("://", StringComparison.Ordinal) + 3)..];
        var authority = rest.Split('/', 2)[0];
        var close = authority.LastIndexOf(']');
        return authority.LastIndexOf(':') > close;
    }

    /// <summary>A certificate pin as 64 upper-case hex characters (colons and spaces dropped); null when it isn't one.</summary>
    public static string? NormalizePin(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var pin = text.Replace(":", "").Replace(" ", "").Replace("-", "").Trim().ToUpperInvariant();
        return pin.Length == 64 && pin.All(char.IsAsciiHexDigit) ? pin : null;
    }

    /// <summary>Seals <paramref name="token"/> for this Windows user (DPAPI), as base64.</summary>
    public static string Seal(string token) =>
        Convert.ToBase64String(LoginTokens.Dpapi.Protect(Encoding.UTF8.GetBytes(token.Trim()), Entropy));

    /// <summary>The remote's token in the clear, or null when it has none or it can't be unsealed here (another user or machine).</summary>
    public static string? Token(GatewayRemote remote)
    {
        if (string.IsNullOrWhiteSpace(remote.SealedToken)) return null;
        try
        {
            return LoginTokens.Dpapi.Unprotect(Convert.FromBase64String(remote.SealedToken), Entropy) is { } plain
                ? Encoding.UTF8.GetString(plain) : null;
        }
        catch (FormatException) { return null; }
    }

    /// <summary>When a <c>v1.&lt;payload&gt;.&lt;signature&gt;</c> gateway token says it expires (read, not verified: only the
    /// gateway can check it); null when it isn't one.</summary>
    public static DateTime? ExpiresUtc(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var parts = token.Trim().Split('.');
        if (parts.Length != 3 || parts[0] != "v1") return null;
        try
        {
            var padded = parts[1].Replace('-', '+').Replace('_', '/');
            padded += new string('=', (4 - padded.Length % 4) % 4);
            using var doc = JsonDocument.Parse(Convert.FromBase64String(padded));
            return doc.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentOutOfRangeException) { return null; }
    }

    /// <summary>The remote named <paramref name="name"/> (case-insensitive), or null.</summary>
    public static GatewayRemote? Find(GatewaySettings settings, string name) =>
        settings.Remotes.FirstOrDefault(r => string.Equals(r.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>A remote's name is letters, digits, dots, dashes and underscores, 1 to 40 of them.</summary>
    public static bool IsValidName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 40 && name.Trim().All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');
}
