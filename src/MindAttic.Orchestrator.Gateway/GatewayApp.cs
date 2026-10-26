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
                if (g.Throttle.Blocked(from)) return TypedResults.StatusCode(StatusCodes.Status429TooManyRequests);
                if (request is null) return TypedResults.BadRequest();
                if (!GatewayKeys.Matches(g.OwnerKey, request.OwnerKey))
                {
                    g.Throttle.Failed(from);
                    g.Log.Write($"token refused: wrong owner key from {from}");
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
                if (Authenticate(http, g) is not { } who) return Results.Unauthorized();
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
        if (Authenticate(http, g) is not { } who) { http.Response.StatusCode = StatusCodes.Status401Unauthorized; return; }
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

        // Every interval: the input typed since (counts and sizes only), and the access checked again, so a revoked or
        // expired grant closes the stream rather than lasting as long as the connection does.
        var input = new GatewayInputMeter();
        using var revoke = new CancellationTokenSource();
        using var stopWatch = new CancellationTokenSource();
        var revoked = false;
        var watch = Task.Run(async () =>
        {
            try
            {
                while (!stopWatch.IsCancellationRequested)
                {
                    await Task.Delay(g.AuditInterval, stopWatch.Token).ConfigureAwait(false);
                    WriteInput(g, input, who.Subject, session.Id, mode, from);
                    var now = OwnerOnlyGrants.Effective(g.Grants.AccessTo(who.Subject, session), who.Scope, requestedMode);
                    if (now < access)
                    {
                        revoked = true;
                        g.Log.Write($"access to {session.Id} by {who.Subject} dropped to {now}; closing the stream");
                        await revoke.CancelAsync().ConfigureAwait(false);
                        return;
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { g.Log.Error(ex, "watching a stream's access failed; contained"); }
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

    /// <summary>The bearer token's holder, or null. Only the Authorization header is read: a token in a URL would land in logs.</summary>
    internal static GatewayPrincipal? Authenticate(HttpContext http, GatewayServices g)
    {
        var header = http.Request.Headers.Authorization.ToString();
        const string scheme = "Bearer ";
        return header.StartsWith(scheme, StringComparison.OrdinalIgnoreCase) ? g.Tokens.Validate(header[scheme.Length..].Trim()) : null;
    }
}

/// <summary>Wrong owner keys per address: <see cref="MaxFailures"/> within <see cref="Window"/> blocks that address until they age out.</summary>
internal sealed class LoginThrottle(Func<DateTime>? clock = null)
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(5);
    private readonly Func<DateTime> now = clock ?? (() => DateTime.UtcNow);
    private readonly ConcurrentDictionary<IPAddress, Queue<DateTime>> failures = new();

    public bool Blocked(IPAddress from)
    {
        if (!failures.TryGetValue(from, out var q)) return false;
        lock (q)
        {
            Prune(q);
            return q.Count >= MaxFailures;
        }
    }

    public void Failed(IPAddress from)
    {
        var q = failures.GetOrAdd(from, _ => new Queue<DateTime>());
        lock (q)
        {
            Prune(q);
            q.Enqueue(now());
        }
        // Bounded: an address with no recent failures is forgotten.
        if (failures.Count > 1024)
            foreach (var (address, queue) in failures)
                lock (queue) { Prune(queue); if (queue.Count == 0) failures.TryRemove(address, out _); }
    }

    public void Succeeded(IPAddress from) => failures.TryRemove(from, out _);

    private void Prune(Queue<DateTime> q)
    {
        var cutoff = now() - Window;
        while (q.Count > 0 && q.Peek() < cutoff) q.Dequeue();
    }
}
