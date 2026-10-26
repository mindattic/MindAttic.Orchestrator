using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Gateway;

/// <summary>Everything the endpoints use; the gateway's Program builds the real one, a test its own.</summary>
internal sealed class GatewayServices
{
    public required GatewayOptions Options { get; init; }
    public required GatewayTokens Tokens { get; init; }
    public required byte[] OwnerKey { get; init; }
    public required IGatewayGrants Grants { get; init; }
    public required ISessionSource Sessions { get; init; }
    public required IPipeConnector Pipes { get; init; }
    public required GatewayLog Log { get; init; }
    public LoginThrottle Throttle { get; } = new();

    /// <summary>Caps how many unauthenticated refusals reach the audit (M.ORCH-T988).</summary>
    public RefusalBudget Refusals { get; } = new();

    /// <summary>Every attach, detach, refusal and remote input count (M.ORCH-T934); the gateway's Program spools them.</summary>
    public IGatewayAudit Audit { get; init; } = NoAudit.Instance;

    /// <summary>How often a stream writes its input counts and checks its access again (a revoked grant closes it).</summary>
    public TimeSpan AuditInterval { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>The clock audit records are stamped with.</summary>
    public Func<DateTime> Clock { get; init; } = () => DateTime.UtcNow;

    /// <summary>The owner's choices of what is shared, masked and paused (M.ORCH-T935): <c>broadcast.json</c>, reread as it
    /// changes. The default shares nothing.</summary>
    public IBroadcastControl Broadcast { get; init; } = new FixedBroadcast(new TermBroadcastSettings());

    /// <summary>What the masker hides: the token shapes, and the Vault's values once loaded.</summary>
    public SecretMasker Secrets { get; init; } = SecretMasker.ShapesOnly;

    /// <summary>Who is attached remotely, announced on the status bus.</summary>
    public WatcherBoard Watchers { get; init; } = new(null, null);
}

/// <summary>A session as <c>GET /sessions</c> lists it: never its command line, folder, config dir, pipe or host pid.</summary>
internal sealed record GatewaySession(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("login")] string? Login,
    [property: JsonPropertyName("repo")] string? Repo,
    [property: JsonPropertyName("cols")] int Cols,
    [property: JsonPropertyName("rows")] int Rows,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("exitCode")] int? ExitCode,
    [property: JsonPropertyName("startedUtc")] DateTime StartedUtc,
    [property: JsonPropertyName("protocol")] int Protocol,
    [property: JsonPropertyName("access")] string Access,
    [property: JsonPropertyName("paused")] bool Paused);

internal sealed record TokenRequest(
    [property: JsonPropertyName("ownerKey")] string? OwnerKey,
    [property: JsonPropertyName("scope")] string? Scope,
    [property: JsonPropertyName("minutes")] int? Minutes);

internal sealed record TokenResponse(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("expiresUtc")] DateTime ExpiresUtc);

/// <summary>
/// The gateway's HTTP surface (M.ORCH-T933):
/// <list type="bullet">
/// <item><c>GET /health</c>: unauthenticated liveness, <c>{ ok, version }</c>.</item>
/// <item><c>POST /auth/token</c>: <c>{ ownerKey, scope?, minutes? }</c> to a short-lived bearer token; five wrong keys from one
/// address in five minutes and it answers 429 until they age out.</item>
/// <item><c>GET /sessions</c>: this machine's hosted sessions the caller may see.</item>
/// <item><c>GET /sessions/{id}/stream</c>, a WebSocket (permessage-deflate offered): the session's frames, both ways
/// (<see cref="TermBridge"/>); <c>?mode=watch</c> asks for read-only.</item>
/// </list>
/// Everything but <c>/health</c> and <c>/auth/token</c> needs <c>Authorization: Bearer &lt;token&gt;</c>. Every handler contains
/// its exceptions; nothing logs a token, a key or a header.
/// </summary>
internal static class GatewayApp
{
    public static string Version { get; } = typeof(GatewayApp).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>How many access checks in a row may fail before a stream is closed (M.ORCH-T984).</summary>
    public const int MaxWatchFailures = 2;

    /// <summary>Builds the app on <paramref name="builder"/> (Kestrel and TLS are the caller's).</summary>
    public static WebApplication Build(WebApplicationBuilder builder, GatewayServices services)
    {
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(services);
        builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);
        var app = builder.Build();
        // A handler that throws past its own catch: a bare 500, never a developer page with details.
        app.UseExceptionHandler(e => e.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Task.CompletedTask;
        }));
        app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });
        Map(app, services);
        return app;
    }

    private static void Map(WebApplication app, GatewayServices g)
    {
        app.MapGet("/health", () => Results.Json(new { ok = true, version = Version }));

        app.MapPost("/auth/token", Results<Ok<TokenResponse>, UnauthorizedHttpResult, BadRequest, StatusCodeHttpResult> (TokenRequest? request, HttpContext http) =>
        {
            try
            {
                var from = http.Connection.RemoteIpAddress ?? IPAddress.None;
                // Counted before the key is checked (M.ORCH-T987), so concurrent guesses can't slip past the limit.
                if (!g.Throttle.TryBegin(from)) return TypedResults.StatusCode(StatusCodes.Status429TooManyRequests);
                if (request is null) return TypedResults.BadRequest();
                if (!GatewayKeys.Matches(g.OwnerKey, request.OwnerKey))
                {
                    g.Log.Write($"token refused: wrong owner key from {from}");
                    AuditRefusal(g, "", from.ToString(), "wrong owner key");
                    return TypedResults.Unauthorized();
                }
                g.Throttle.Succeeded(from);
                var scope = request.Scope is null ? GatewayScopes.Interact : request.Scope.ToLowerInvariant();
                if (!GatewayScopes.IsValid(scope)) return TypedResults.BadRequest();
                var minutes = Math.Clamp(request.Minutes ?? g.Options.TokenMinutes, 1, GatewayOptions.MaxTokenMinutes);
                var (token, expires) = g.Tokens.Issue(g.Options.Owner, scope, TimeSpan.FromMinutes(minutes));
                g.Log.Write($"token issued to {g.Options.Owner} ({scope}, {minutes} min) for {from}");
                return TypedResults.Ok(new TokenResponse(token, g.Options.Owner, scope, expires));
            }
            catch (Exception ex)
            {
                g.Log.Error(ex, "issuing a token failed; contained");
                return TypedResults.StatusCode(StatusCodes.Status500InternalServerError);
            }
        });

        app.MapGet("/sessions", (HttpContext http) =>
        {
            try
            {
                if (Authenticate(http, g) is not { } who)
                {
                    AuditRefusal(g, "", RemoteOf(http), TokenRefusal(http));
                    return Results.Unauthorized();
                }
                var list = new List<GatewaySession>();
                var broadcast = g.Broadcast.Current;
                foreach (var s in g.Sessions.Enumerate())
                {
                    // Only what the owner shares (M.ORCH-T935): remote watching is opt-in, per session or for all of them.
                    if (!broadcast.IsShared(s.Id)) continue;
                    var access = OwnerOnlyGrants.Effective(g.Grants.AccessTo(who.Subject, s), who.Scope, null);
                    if (access == GatewayAccess.None) continue;
                    list.Add(new GatewaySession(s.Id, s.Title, s.Login, s.Repo, s.Cols, s.Rows, s.State, s.ExitCode, s.StartedUtc, s.Protocol,
                        access == GatewayAccess.Interact ? GatewayScopes.Interact : GatewayScopes.Watch, broadcast.IsPaused(s.Id)));
                }
                return Results.Json(list);
            }
            catch (Exception ex)
            {
                g.Log.Error(ex, "listing sessions failed; contained");
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }
        });

        app.Map("/sessions/{id}/stream", async (HttpContext http, string id) =>
        {
            try { await StreamAsync(http, id, g).ConfigureAwait(false); }
            catch (Exception ex)
            {
                g.Log.Error(ex, $"streaming session {TermProtocol.Sanitize(id)} failed; contained");
                if (!http.Response.HasStarted) http.Response.StatusCode = StatusCodes.Status500InternalServerError;
            }
        });
    }

    private static async Task StreamAsync(HttpContext http, string id, GatewayServices g)
    {
        var token = BearerToken(http);
        if (Authenticate(http, g) is not { } who)
        {
            AuditRefusal(g, TermProtocol.Sanitize(id), RemoteOf(http), TokenRefusal(http));
            http.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        if (!http.WebSockets.IsWebSocketRequest) { http.Response.StatusCode = StatusCodes.Status400BadRequest; return; }
        var session = g.Sessions.Get(id);
        string? requestedMode = http.Request.Query["mode"];
        var shared = session is not null && g.Broadcast.Current.IsShared(session.Id);
        var access = session is null || !shared ? GatewayAccess.None
            : OwnerOnlyGrants.Effective(g.Grants.AccessTo(who.Subject, session), who.Scope, requestedMode);
        var from = http.Connection.RemoteIpAddress?.ToString() ?? "?";
        // Not found, not shared (M.ORCH-T935) and not allowed look the same, so a token can't probe for session ids.
        if (session is null || access == GatewayAccess.None)
        {
            g.Audit.Write(Record(g, TermGatewayAudit.Refused, who.Subject, TermProtocol.Sanitize(id), "none", from,
                detail: session is null ? "no such session" : !shared ? "not shared" : "no grant"));
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        // A protocol 1 host serves one viewer at a time: attaching would take the session from its owner's own tab
        // (M.ORCH-T985). Only the owner may do that to themselves; the wall's client-side check is not the guard.
        if (session.Protocol < TermProtocol.MultiViewer && !string.Equals(who.Subject.Trim(), g.Options.Owner.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            g.Audit.Write(Record(g, TermGatewayAudit.Refused, who.Subject, session.Id, "none", from, detail: "single-viewer host (protocol 1)"));
            http.Response.StatusCode = StatusCodes.Status409Conflict;
            return;
        }

        Stream pipe;
        try { pipe = await g.Pipes.ConnectAsync(session, http.RequestAborted).ConfigureAwait(false); }
        catch (Exception ex)
        {
            g.Log.Error(ex, $"couldn't open session {session.Id}'s pipe");
            http.Response.StatusCode = StatusCodes.Status502BadGateway;
            return;
        }

        using var socket = await http.WebSockets.AcceptWebSocketAsync(new WebSocketAcceptContext
        {
            // permessage-deflate: VT compresses 5 to 10x (spike section 5). Offered; used when the viewer asks for it.
            DangerousEnableCompression = true,
            DisableServerContextTakeover = false,
            ServerMaxWindowBits = 15,
        }).ConfigureAwait(false);
        var mode = access == GatewayAccess.Interact ? GatewayScopes.Interact : GatewayScopes.Watch;
        var masked = g.Broadcast.Current.IsMasked(session.Id);
        g.Log.Write($"attach {session.Id} by {who.Subject} from {from} ({mode}, {(masked ? "masked" : "unmasked")})");
        g.Audit.Write(Record(g, TermGatewayAudit.Attach, who.Subject, session.Id, mode, from));
        // The owner's dashboard shows a "being watched" banner while this lasts (M.ORCH-T935).
        var watcher = g.Watchers.Attach(session.Id, who.Subject, access, from);

        // Every interval: the input typed since (counts and sizes only), and the token and access checked again, so an expired
        // token, a replaced signing key, or a revoked or expired grant closes the stream rather than lasting as long as the
        // connection does.
        var input = new GatewayInputMeter();
        using var revoke = new CancellationTokenSource();
        using var stopWatch = new CancellationTokenSource();
        var revoked = false;
        // A check that throws is contained inside the loop, so the checks go on (M.ORCH-T984); one that fails
        // MaxWatchFailures times running closes the stream: access that can't be checked isn't assumed (fails closed).
        var watch = Task.Run(async () =>
        {
            var failures = 0;
            while (!stopWatch.IsCancellationRequested)
            {
                try { await Task.Delay(g.AuditInterval, stopWatch.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
                string? why = null;
                try
                {
                    WriteInput(g, input, who.Subject, session.Id, mode, from);
                    // The token as well as the grant (M.ORCH-T983): it expired, or signing.key was replaced, so it no longer
                    // validates, and the stream it opened ends with it.
                    if (g.Tokens.Validate(token) is null) why = "lapsed: its token expired or the signing key changed";
                    else
                    {
                        var now = OwnerOnlyGrants.Effective(g.Grants.AccessTo(who.Subject, session), who.Scope, requestedMode);
                        if (now < access) why = $"dropped to {now}";
                    }
                    failures = 0;
                }
                catch (Exception ex)
                {
                    g.Log.Error(ex, "checking a stream's access failed; contained");
                    if (++failures >= MaxWatchFailures) why = $"couldn't be checked {failures} times running";
                }
                if (why is null) continue;
                revoked = true;
                g.Log.Write($"access to {session.Id} by {who.Subject} {why}; closing the stream");
                try { await revoke.CancelAsync().ConfigureAwait(false); }
                catch (Exception ex) { g.Log.Error(ex, "closing a revoked stream failed; contained"); }
                return;
            }
        });

        BridgeResult result;
        try
        {
            result = await TermBridge.RunAsync(socket, pipe, access, g.Log, http.RequestAborted, input, revoke.Token,
                new BroadcastStream(session.Id, session.StartedUtc, g.Broadcast, g.Secrets)).ConfigureAwait(false);
        }
        finally { g.Watchers.Detach(watcher); }
        await stopWatch.CancelAsync().ConfigureAwait(false);
        try { await watch.ConfigureAwait(false); } catch (Exception) { }
        WriteInput(g, input, who.Subject, session.Id, mode, from);
        var ended = revoked ? "access revoked" : result.EndedBy;
        g.Log.Write($"detach {session.Id} by {who.Subject}: ended by {ended}, {result.FramesUp} frames up, " +
                    $"{result.FramesDown} down ({result.BytesDown} bytes), {result.Dropped} dropped, {result.Withheld} withheld while paused");
        g.Audit.Write(Record(g, TermGatewayAudit.Detach, who.Subject, session.Id, mode, from, result.FramesUp, result.BytesDown,
            $"ended by {ended}; {result.FramesDown} frames down, {result.Dropped} dropped"));
    }

    /// <summary>An input record for what <paramref name="input"/> counted since it was last taken; none when nothing was typed.</summary>
    private static void WriteInput(GatewayServices g, GatewayInputMeter input, string viewer, string session, string level, string from)
    {
        var (frames, bytes) = input.Take();
        if (frames > 0) g.Audit.Write(Record(g, TermGatewayAudit.Input, viewer, session, level, from, frames, bytes));
    }

    private static TermGatewayAuditRecord Record(GatewayServices g, string what, string viewer, string session, string level, string remote,
        long frames = 0, long bytes = 0, string? detail = null) => new()
    {
        AtUtc = g.Clock(), Event = what, Viewer = viewer, Owner = g.Options.Owner, Session = session, Level = level, Remote = remote,
        Frames = frames, Bytes = bytes, Detail = detail,
    };

    /// <summary>A refusal of an unauthenticated request in the audit (M.ORCH-T988), within <see cref="GatewayServices.Refusals"/>:
    /// who it was can't be known, so the viewer is "?"; never the token, the key or a header.</summary>
    private static void AuditRefusal(GatewayServices g, string session, string remote, string why)
    {
        if (!g.Refusals.TryTake(out var skipped)) return;
        g.Audit.Write(Record(g, TermGatewayAudit.Refused, "?", session, "none", remote,
            detail: skipped > 0 ? $"{why} ({skipped} more refusals not recorded before this)" : why));
    }

    /// <summary>Why a request's token was refused, in words that never hold the token.</summary>
    private static string TokenRefusal(HttpContext http) =>
        string.IsNullOrWhiteSpace(http.Request.Headers.Authorization.ToString()) ? "no token" : "bad or expired token";

    private static string RemoteOf(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "?";

    /// <summary>The bearer token's holder, or null. Only the Authorization header is read: a token in a URL would land in logs.</summary>
    internal static GatewayPrincipal? Authenticate(HttpContext http, GatewayServices g) =>
        BearerToken(http) is { } token ? g.Tokens.Validate(token) : null;

    /// <summary>The bearer token in the Authorization header, unchecked; held only in memory, never logged.</summary>
    private static string? BearerToken(HttpContext http)
    {
        var header = http.Request.Headers.Authorization.ToString();
        const string scheme = "Bearer ";
        return header.StartsWith(scheme, StringComparison.OrdinalIgnoreCase) ? header[scheme.Length..].Trim() : null;
    }
}

/// <summary>
/// Wrong owner keys per address: <see cref="MaxFailures"/> within <see cref="Window"/> blocks that address until they age out
/// (M.ORCH-T987). Every operation is O(1) under one short lock: each address keeps only its last <see cref="MaxFailures"/>
/// failure times, and at most <see cref="Capacity"/> addresses are kept, the one that failed longest ago forgotten first, so a
/// flood of addresses can't grow memory or make a request scan the table. An IPv6 address counts by its /64, the block one
/// client is usually given. <see cref="TryBegin"/> counts an attempt before its key is checked and <see cref="Succeeded"/> takes
/// it back, so concurrent guesses can't slip past the limit.
/// </summary>
internal sealed class LoginThrottle(Func<DateTime>? clock = null, int capacity = LoginThrottle.Capacity)
{
    public const int MaxFailures = 5;
    public const int Capacity = 4096;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(5);
    private readonly Func<DateTime> now = clock ?? (() => DateTime.UtcNow);
    private readonly int capacity = Math.Max(1, capacity);
    private readonly System.Threading.Lock gate = new();
    private readonly Dictionary<IPAddress, Entry> entries = [];
    // Least recently failed first.
    private readonly LinkedList<IPAddress> order = new();

    private sealed class Entry(LinkedListNode<IPAddress> node)
    {
        public readonly DateTime[] Times = new DateTime[MaxFailures];
        public int Next;
        public readonly LinkedListNode<IPAddress> Node = node;
    }

    /// <summary>How many addresses are tracked (for tests).</summary>
    internal int Count
    {
        get { lock (gate) return entries.Count; }
    }

    public bool Blocked(IPAddress from)
    {
        lock (gate) return entries.TryGetValue(Key(from), out var e) && Recent(e) >= MaxFailures;
    }

    /// <summary>An owner key is about to be checked: false when the address is blocked; otherwise the attempt counts as a failure
    /// until <see cref="Succeeded"/> takes it back, atomically with the check.</summary>
    public bool TryBegin(IPAddress from)
    {
        lock (gate)
        {
            var key = Key(from);
            if (entries.TryGetValue(key, out var e) && Recent(e) >= MaxFailures) return false;
            Record(key, e);
            return true;
        }
    }

    public void Failed(IPAddress from)
    {
        lock (gate)
        {
            var key = Key(from);
            Record(key, entries.GetValueOrDefault(key));
        }
    }

    public void Succeeded(IPAddress from)
    {
        lock (gate)
        {
            if (entries.Remove(Key(from), out var e)) order.Remove(e.Node);
        }
    }

    private void Record(IPAddress key, Entry? e)
    {
        if (e is null)
        {
            if (entries.Count >= capacity && order.First is { } oldest)
            {
                entries.Remove(oldest.Value);
                order.RemoveFirst();
            }
            e = new Entry(order.AddLast(key));
            entries[key] = e;
        }
        else
        {
            order.Remove(e.Node);
            order.AddLast(e.Node);
        }
        e.Times[e.Next] = now();
        e.Next = (e.Next + 1) % MaxFailures;
    }

    private int Recent(Entry e)
    {
        var cutoff = now() - Window;
        var n = 0;
        foreach (var t in e.Times) if (t >= cutoff && t != default) n++;
        return n;
    }

    /// <summary>The address as counted: an IPv4-mapped one as IPv4, an IPv6 one by its /64.</summary>
    internal static IPAddress Key(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) return address.MapToIPv4();
        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6) return address;
        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes);
    }
}
