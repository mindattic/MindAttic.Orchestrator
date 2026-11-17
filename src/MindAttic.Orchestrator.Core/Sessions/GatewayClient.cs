using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

    /// <summary>The remote's Launcher menu pane, not a CLI it opened (M.ORCH-T1256); false for an older gateway that doesn't send it.</summary>
    [JsonPropertyName("isMenu")] public bool IsMenu { get; init; }

    /// <summary>Hub phase 2 (M.ORCH-T1641): the org member whose login the CLI runs on, on a host with member sessions on; else null.</summary>
    [JsonPropertyName("owner")] public string? Owner { get; init; }

    /// <summary>Hub phase 2: an interact grant on it that waits on its owner's consent (ask, pending or denied); null otherwise.</summary>
    [JsonPropertyName("consent")] public string? Consent { get; init; }
}

/// <summary>A consent request waiting on this member as a session owner (<c>GET /consents</c>, hub phase 2, M.ORCH-T1641).</summary>
public sealed record RemoteConsent
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("session")] public string Session { get; init; } = "";
    [JsonPropertyName("viewer")] public string Viewer { get; init; } = "";
    [JsonPropertyName("owner")] public string Owner { get; init; } = "";
    [JsonPropertyName("askedUtc")] public DateTime AskedUtc { get; init; }
    [JsonPropertyName("deadlineUtc")] public DateTime DeadlineUtc { get; init; }
}

/// <summary>What one look at a gateway found: its sessions, or why it couldn't be read (offline, refused, no token).</summary>
public sealed record GatewayListing(bool Online, IReadOnlyList<RemoteSessionInfo> Sessions, string? Problem)
{
    /// <summary>Offline.</summary>
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
    private volatile string? token;
    private readonly string? pin;
    private readonly string? ownerKey;
    private readonly string? memberKey;
    private readonly SemaphoreSlim minting = new(1, 1);
    private volatile string? mintProblem;

    private readonly Func<GatewayRemote?>? latest;

    /// <param name="latest">This remote as the settings hold it now (read again each time), so an open stream can send the
    /// gateway the fresh token the member was given since (M.ORCH-T1041); null: streams last only as long as this token.</param>
    public GatewayClient(GatewayRemote remote, Func<GatewayRemote?>? latest = null)
    {
        Remote = remote;
        this.latest = latest;
        token = GatewayRemotes.Token(remote);
        ownerKey = GatewayRemotes.OwnerKey(remote);
        // An org member's own key to the central host (hub phase 1, M.ORCH-T1640): minted with like the owner key, never both.
        memberKey = ownerKey is null ? GatewayRemotes.MemberKey(remote) : null;
        pin = GatewayRemotes.NormalizePin(remote.CertSha256);
        BaseUrl = GatewayRemotes.NormalizeUrl(remote.Url);
        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(5),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            SslOptions = new SslClientAuthenticationOptions { RemoteCertificateValidationCallback = (_, cert, _, errors) => Trusted(cert, errors) },
        };
        http = new HttpClient(handler) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
    }

    /// <summary>The remote.</summary>
    public GatewayRemote Remote { get; }

    /// <summary><c>https://host:port</c>, or null when the remote's URL isn't one.</summary>
    public string? BaseUrl { get; }

    /// <summary>Whether a token was found and could be unsealed for this Windows user.</summary>
    public bool HasToken => token is not null || ownerKey is not null || memberKey is not null;

    /// <summary>A token minted with the owner key lasts this long (the gateway's most).</summary>
    public const int MintMinutes = 60;

    /// <summary>
    /// With the owner key (the owner's own host, M.ORCH-T1473), a fresh token from <c>POST /auth/token</c> whenever there is none
    /// or it has under 10 minutes left, so the link never lapses. Without one, nothing: the token given is used until it expires.
    /// Never throws; a failed mint leaves the old token, and the listing says why it's offline.
    /// </summary>
    internal async Task MintIfDueAsync(CancellationToken ct)
    {
        if ((ownerKey is null && memberKey is null) || BaseUrl is null) return;
        if (token is { } t && GatewayRemotes.ExpiresUtc(t) is { } exp && exp - DateTime.UtcNow > TimeSpan.FromMinutes(10)) return;
        // Inside the never-throws contract too: a cancelled wait (another call minting) or a disposed client threw out of it.
        try { await minting.WaitAsync(ct).ConfigureAwait(false); }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException) { mintProblem ??= "no token: " + ex.GetType().Name; return; }
        try
        {
            if (token is { } again && GatewayRemotes.ExpiresUtc(again) is { } e2 && e2 - DateTime.UtcNow > TimeSpan.FromMinutes(10)) return;
            using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "/auth/token")
            {
                Content = ownerKey is not null
                    ? JsonContent.Create(new { ownerKey, scope = "interact", minutes = MintMinutes })
                    : JsonContent.Create(new { memberKey, scope = "interact", minutes = MintMinutes }),
            };
            using var bounded = CancellationTokenSource.CreateLinkedTokenSource(ct);
            bounded.CancelAfter(ListTimeout);
            using var response = await http.SendAsync(request, bounded.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                mintProblem = memberKey is not null ? response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => "it refused your member key (revoked, replaced by a newer invite, or you're no longer an active member: ask the host's owner for a new orchestrator machine invite)",
                    HttpStatusCode.TooManyRequests => "it's refusing keys from here for a while (too many wrong ones)",
                    var code => $"it answered {(int)code} to your member key",
                } : response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => "it refused this machine's owner key (the host's key changed: run the connect line it prints again)",
                    HttpStatusCode.TooManyRequests => "it's refusing owner keys from here for a while (too many wrong ones)",
                    var code => $"it answered {(int)code} to the owner key",
                };
                return;
            }
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            if (doc.RootElement.TryGetProperty("token", out var minted) && minted.GetString() is { Length: > 0 } fresh)
            {
                token = fresh;
                mintProblem = null;
            }
        }
        catch (HttpRequestException ex) { mintProblem = Describe(ex); }
        catch (Exception ex) when (ex is JsonException or OperationCanceledException or InvalidOperationException) { mintProblem = "no token: " + ex.GetType().Name; }
        finally { minting.Release(); }
    }

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
        await MintIfDueAsync(ct).ConfigureAwait(false);
        if (token is null && mintProblem is { } why) return GatewayListing.Offline(why);
        if (token is null) return GatewayListing.Offline("no token (or it can't be unsealed by this Windows user)");
        if (TokenExpiresUtc is { } expires && expires <= DateTime.UtcNow) return GatewayListing.Offline("its token has expired");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + "/sessions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var bounded = CancellationTokenSource.CreateLinkedTokenSource(ct);
            bounded.CancelAfter(ListTimeout);
            using var response = await http.SendAsync(request, bounded.Token).ConfigureAwait(false);
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

    /// <summary>How long a tickets command run on the host may take, end to end.</summary>
    public static readonly TimeSpan TicketsTimeout = TimeSpan.FromMinutes(4);

    /// <summary>
    /// Runs the tickets CLI on the host with <paramref name="args"/> (M.ORCH-T1479), in the workspace folder
    /// <paramref name="relativeDirectory"/> names there; needs the owner key (the owner's own host). Returns its exit code and
    /// output; a host that can't be reached or refuses comes back as exit 1 with why. Never throws but on cancellation.
    /// </summary>
    public Task<(int ExitCode, string Output)> RunTicketsAsync(IReadOnlyList<string> args, string? relativeDirectory, CancellationToken ct) =>
        RunOnHostAsync("/tickets", "tickets", args, relativeDirectory, ct);

    /// <summary>
    /// Runs <c>orchestrator branches</c> on the host with <paramref name="args"/> (M.ORCH-T1635): its work-branch listing (such as
    /// <c>branches --ready --json</c>) or a local test run's <c>branches report</c>; the gateway runs nothing else. Needs the owner
    /// key, like <see cref="RunTicketsAsync"/>, and comes back the same way.
    /// </summary>
    public Task<(int ExitCode, string Output)> RunBranchesAsync(IReadOnlyList<string> args, CancellationToken ct) =>
        RunOnHostAsync("/branches", "branches", args, null, ct);

    private async Task<(int ExitCode, string Output)> RunOnHostAsync(string route, string what, IReadOnlyList<string> args, string? relativeDirectory, CancellationToken ct)
    {
        if (BaseUrl is null) return (1, $"The host's gateway URL ({Remote.Url}) isn't an https address.");
        await MintIfDueAsync(ct).ConfigureAwait(false);
        if (token is null) return (1, "Couldn't reach the host for a token: " + (mintProblem ?? "no owner key here (orchestrator machine connect)") + ".");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + route)
            {
                Content = JsonContent.Create(new { args, directory = relativeDirectory }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var bounded = CancellationTokenSource.CreateLinkedTokenSource(ct);
            bounded.CancelAfter(TicketsTimeout);
            using var response = await http.SendAsync(request, bounded.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound) return (1, "The host's gateway is older than this PC's Orchestrator: deploy MindAttic.Orchestrator on the host (orchestrator machine pull, then run.ps1).");
            if (!response.IsSuccessStatusCode) return (1, $"The host refused the {what} command ({(int)response.StatusCode}).");
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            var root = doc.RootElement;
            return (root.TryGetProperty("exitCode", out var code) && code.TryGetInt32(out var c) ? c : 1,
                root.TryGetProperty("output", out var output) ? output.GetString() ?? "" : "");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return (1, $"The host didn't finish the {what} command in time."); }
        catch (HttpRequestException ex) { return (1, "The host can't be reached: " + Describe(ex) + "."); }
        catch (JsonException) { return (1, "The host's answer couldn't be read."); }
        // Bughunt, M.ORCH-T1538: a well-formed-JSON-but-not-an-object answer (null, an array, a scalar) makes
        // TryGetProperty above throw InvalidOperationException, which escaped uncaught, breaking this method's own "never
        // throws but on cancellation" contract - the same class of host-answer problem JsonException already covers.
        catch (InvalidOperationException) { return (1, "The host's answer couldn't be read."); }
    }

    /// <summary>Starts a CLI on the host in <paramref name="repo"/> on <paramref name="login"/> (the member's own email when null; hub
    /// phase 2, M.ORCH-T1641). Exit 0 and the host's output when it started; otherwise 1 and why. Never throws but on cancellation.</summary>
    public Task<(int ExitCode, string Output)> StartSessionAsync(string repo, string? login, CancellationToken ct) =>
        SendHubAsync(HttpMethod.Post, "/sessions", new { repo, login }, "start", ct);

    /// <summary>Stops one of the member's own CLIs on the host (hub phase 2). Comes back as <see cref="StartSessionAsync"/> does.</summary>
    public Task<(int ExitCode, string Output)> StopSessionAsync(string sessionId, CancellationToken ct) =>
        SendHubAsync(HttpMethod.Delete, "/sessions/" + Uri.EscapeDataString(sessionId.Trim()), null, "stop", ct);

    /// <summary>The consent requests waiting on this member as a session owner (hub phase 2); null when the host couldn't be asked
    /// (offline, refused, or member sessions are off there). Never throws but on cancellation.</summary>
    public async Task<IReadOnlyList<RemoteConsent>?> ConsentsAsync(CancellationToken ct)
    {
        var (code, body) = await SendRawAsync(HttpMethod.Get, "/consents", null, ct).ConfigureAwait(false);
        if (code != 200) return null;
        try { return JsonSerializer.Deserialize<List<RemoteConsent>>(body, Json) is { } list ? list.Where(c => !string.IsNullOrWhiteSpace(c.Id)).ToList() : []; }
        catch (JsonException) { return null; }
    }

    /// <summary>Answers consent request <paramref name="id"/> (hub phase 2). Exit 0 when the host took it; 1 and why otherwise.</summary>
    public async Task<(int ExitCode, string Output)> AnswerConsentAsync(string id, bool allow, CancellationToken ct)
    {
        var (code, body) = await SendRawAsync(HttpMethod.Post, "/consents/" + Uri.EscapeDataString(id.Trim()), new { allow }, ct).ConfigureAwait(false);
        return code switch
        {
            200 => (0, allow ? "Allowed: their next attach types into your CLI." : "Refused: they keep watching only, for as long as their grant lasts."),
            404 => (1, "No such request waiting on you: it lapsed (no answer in time leaves them watching), was answered, or isn't about your CLI."),
            < 0 => (1, body),
            _ => (1, $"The host answered {code}."),
        };
    }

    private async Task<(int ExitCode, string Output)> SendHubAsync(HttpMethod method, string route, object? body, string what, CancellationToken ct)
    {
        var (code, text) = await SendRawAsync(method, route, body, ct).ConfigureAwait(false);
        if (code < 0) return (1, text);
        if (code == 404 && method != HttpMethod.Delete) return (1, "The host doesn't let members start CLIs (its owner hasn't turned on HubMemberSessions, or its Orchestrator is older than this PC's).");
        if (code == 404) return (1, "No CLI of yours with that id on the host.");
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            var output = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("output", out var o) ? o.GetString() ?? "" : "";
            return (code == 200 ? 0 : 1, output.Length > 0 ? output : $"The host answered {code} to the {what}.");
        }
        catch (JsonException) { return (code == 200 ? 0 : 1, code == 200 ? "" : $"The host answered {code} to the {what}."); }
    }

    /// <summary>One authenticated request to the host: its status and body, or a negative status and why it couldn't be sent.</summary>
    private async Task<(int Status, string Body)> SendRawAsync(HttpMethod method, string route, object? body, CancellationToken ct)
    {
        if (BaseUrl is null) return (-1, $"The host's gateway URL ({Remote.Url}) isn't an https address.");
        await MintIfDueAsync(ct).ConfigureAwait(false);
        if (token is null) return (-1, "Couldn't reach the host for a token: " + (mintProblem ?? "no key here (orchestrator machine connect)") + ".");
        try
        {
            using var request = new HttpRequestMessage(method, BaseUrl + route);
            if (body is not null) request.Content = JsonContent.Create(body);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var bounded = CancellationTokenSource.CreateLinkedTokenSource(ct);
            bounded.CancelAfter(TicketsTimeout);
            using var response = await http.SendAsync(request, bounded.Token).ConfigureAwait(false);
            return ((int)response.StatusCode, await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return (-1, "The host didn't answer in time."); }
        catch (HttpRequestException ex) { return (-1, "The host can't be reached: " + Describe(ex) + "."); }
    }

    /// <summary>
    /// Opens session <paramref name="sessionId"/>'s stream: a WebSocket to <c>/sessions/{id}/stream</c> (<c>?mode=watch</c> when
    /// <paramref name="watch"/>) with permessage-deflate, as a frame stream. Throws when it can't be opened (offline, refused, the
    /// session gone).
    /// </summary>
    public async Task<Stream> ConnectAsync(string sessionId, bool watch, CancellationToken ct)
    {
        if (BaseUrl is null) throw new InvalidOperationException($"Gateway {Remote.Name}'s URL isn't an https address.");
        await MintIfDueAsync(ct).ConfigureAwait(false);
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
            return new WebSocketFrameStream(socket, latest is null && ownerKey is null && memberKey is null ? null : FreshToken, token);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>The member's token for this gateway as the settings hold it now: only while the remote still names the same
    /// gateway (URL and pin), so a token meant for another one never goes to this one. Null when there is none.</summary>
    internal string? FreshToken()
    {
        // The owner's own host: the token this client minted last (the wall's listing re-mints it before it runs low).
        if (ownerKey is not null || memberKey is not null) return token;
        if (latest?.Invoke() is not { } now) return null;
        if (GatewayRemotes.NormalizeUrl(now.Url) != BaseUrl || GatewayRemotes.NormalizePin(now.CertSha256) != pin) return null;
        return GatewayRemotes.Token(now);
    }

    /// <summary>Releases.</summary>
    public void Dispose()
    {
        http.Dispose();
        minting.Dispose();
    }
}
