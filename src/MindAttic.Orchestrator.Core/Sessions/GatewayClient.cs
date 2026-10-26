using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>A session as a teammate's gateway lists it (<c>GET /sessions</c>, M.ORCH-T933).</summary>
public sealed record RemoteSessionInfo
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("login")] public string? Login { get; init; }
    [JsonPropertyName("repo")] public string? Repo { get; init; }
    [JsonPropertyName("cols")] public int Cols { get; init; }
    [JsonPropertyName("rows")] public int Rows { get; init; }
    [JsonPropertyName("state")] public string? State { get; init; }
    [JsonPropertyName("exitCode")] public int? ExitCode { get; init; }
    [JsonPropertyName("startedUtc")] public DateTime StartedUtc { get; init; }
    [JsonPropertyName("protocol")] public int Protocol { get; init; }

    /// <summary><c>watch</c> or <c>interact</c>: the most this caller may do with it (the grant and the token's scope).</summary>
    [JsonPropertyName("access")] public string? Access { get; init; }
}

/// <summary>What one look at a gateway found: its sessions, or why it couldn't be read (offline, refused, no token).</summary>
public sealed record GatewayListing(bool Online, IReadOnlyList<RemoteSessionInfo> Sessions, string? Problem)
{
    public static GatewayListing Offline(string problem) => new(false, [], problem);
}

/// <summary>
/// A client of one teammate's gateway (M.ORCH-T936): lists its sessions and opens a session's stream as a frame
/// <see cref="Stream"/> for a RelayConnection. TLS is checked against the remote's pin (the certificate's SHA-256) when it has
/// one, else by Windows as usual. The token goes only in the <c>Authorization</c> header and is never logged.
/// <see cref="ListAsync"/> never throws; <see cref="ConnectAsync"/> throws when the stream can't be opened, which a
/// RelayConnection contains.
/// </summary>
public sealed class GatewayClient : IDisposable
{
    /// <summary>How long a look at <c>/sessions</c> may take before the gateway counts as offline.</summary>
    public static readonly TimeSpan ListTimeout = TimeSpan.FromSeconds(6);

    /// <summary>How long opening a stream may take.</summary>
    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient http;
    private readonly string? token;
    private readonly string? pin;

    public GatewayClient(GatewayRemote remote)
    {
        Remote = remote;
        token = GatewayRemotes.Token(remote);
        pin = GatewayRemotes.NormalizePin(remote.CertSha256);
        BaseUrl = GatewayRemotes.NormalizeUrl(remote.Url);
        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(5),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            SslOptions = new SslClientAuthenticationOptions { RemoteCertificateValidationCallback = (_, cert, _, errors) => Trusted(cert, errors) },
        };
        http = new HttpClient(handler) { Timeout = ListTimeout };
    }

    public GatewayRemote Remote { get; }

    /// <summary><c>https://host:port</c>, or null when the remote's URL isn't one.</summary>
    public string? BaseUrl { get; }

    /// <summary>Whether a token was found and could be unsealed for this Windows user.</summary>
    public bool HasToken => token is not null;

    /// <summary>When the token says it expires (unverified); null when it doesn't say.</summary>
    public DateTime? TokenExpiresUtc => GatewayRemotes.ExpiresUtc(token);

    private bool Trusted(X509Certificate? cert, SslPolicyErrors errors) => PinMatches(cert, pin, errors);

    /// <summary>With a pin, the certificate's SHA-256 must equal it (a self-signed dev certificate is fine); without one, the
    /// usual chain and name checks must have passed.</summary>
    public static bool PinMatches(X509Certificate? cert, string? pin, SslPolicyErrors errors)
    {
        if (cert is null) return false;
        if (pin is null) return errors == SslPolicyErrors.None;
        try { return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(pin), SHA256.HashData(cert.GetRawCertData())); }
        catch (Exception ex) when (ex is FormatException or CryptographicException) { return false; }
    }

    /// <summary>The gateway's sessions this member may see. Never throws: an unreachable or refusing gateway comes back
    /// <see cref="GatewayListing.Online"/> false with why.</summary>
    public async Task<GatewayListing> ListAsync(CancellationToken ct)
    {
        if (BaseUrl is null) return GatewayListing.Offline("its URL isn't an https address");
        if (token is null) return GatewayListing.Offline("no token (or it can't be unsealed by this Windows user)");
        if (TokenExpiresUtc is { } expires && expires <= DateTime.UtcNow) return GatewayListing.Offline("its token has expired");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + "/sessions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Unauthorized) return GatewayListing.Offline("it refused the token (expired or revoked)");
            if (!response.IsSuccessStatusCode) return GatewayListing.Offline($"it answered {(int)response.StatusCode}");
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var sessions = JsonSerializer.Deserialize<List<RemoteSessionInfo>>(body, Json) ?? [];
            return new GatewayListing(true, sessions.Where(s => !string.IsNullOrWhiteSpace(s.Id)).ToList(), null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return GatewayListing.Offline("it didn't answer in time"); }
        catch (OperationCanceledException) { return GatewayListing.Offline("cancelled"); }
        catch (HttpRequestException ex) { return GatewayListing.Offline(Describe(ex)); }
        catch (JsonException) { return GatewayListing.Offline("its session list couldn't be read"); }
        catch (Exception ex)
        {
            OrchestratorLog.For("TeamWall").Error(ex, "Listing the sessions of gateway {Name:l} failed; contained", Remote.Name);
            return GatewayListing.Offline("failed: " + ex.GetType().Name);
        }
    }

    private static string Describe(HttpRequestException ex) =>
        ex.InnerException is AuthenticationException ? "its certificate doesn't match the pin"
        : ex.HttpRequestError switch
        {
            HttpRequestError.NameResolutionError => "its name doesn't resolve",
            HttpRequestError.ConnectionError => "it can't be reached",
            HttpRequestError.SecureConnectionError => "its certificate doesn't match the pin",
            _ => "it can't be reached",
        };

    /// <summary>
    /// Opens session <paramref name="sessionId"/>'s stream: a WebSocket to <c>/sessions/{id}/stream</c> (<c>?mode=watch</c> when
    /// <paramref name="watch"/>) with permessage-deflate, as a frame stream. Throws when it can't be opened (offline, refused, the
    /// session gone).
    /// </summary>
    public async Task<Stream> ConnectAsync(string sessionId, bool watch, CancellationToken ct)
    {
        if (BaseUrl is null) throw new InvalidOperationException($"Gateway {Remote.Name}'s URL isn't an https address.");
        if (token is null) throw new InvalidOperationException($"Gateway {Remote.Name} has no token this Windows user can open.");
        var url = new Uri("wss" + BaseUrl["https".Length..] + "/sessions/" + Uri.EscapeDataString(sessionId) + "/stream" + (watch ? "?mode=watch" : ""));
        var socket = new ClientWebSocket();
        try
        {
            socket.Options.SetRequestHeader("Authorization", "Bearer " + token);
            socket.Options.RemoteCertificateValidationCallback = (_, cert, _, errors) => Trusted(cert, errors);
            socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
            // permessage-deflate (spike section 5): VT compresses 5 to 10x. The gateway offers it with a 15-bit window and
            // context takeover (M.ORCH-T933); asking for the same makes it negotiate.
            socket.Options.DangerousDeflateOptions = new WebSocketDeflateOptions
            {
                ClientMaxWindowBits = 15,
                ServerMaxWindowBits = 15,
                ClientContextTakeover = true,
                ServerContextTakeover = true,
            };
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(ConnectTimeout);
            await socket.ConnectAsync(url, timeout.Token).ConfigureAwait(false);
            return new WebSocketFrameStream(socket);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    public void Dispose() => http.Dispose();
}
