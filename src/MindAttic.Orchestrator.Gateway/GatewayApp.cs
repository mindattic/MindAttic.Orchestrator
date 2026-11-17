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

    /// <summary>Each session's title as <c>GET /sessions</c> last listed it while not paused, by lower-case id: what it lists
    /// while the session is paused (M.ORCH-T981). Only live sessions are kept.</summary>
    internal ConcurrentDictionary<string, string?> ListedTitles { get; } = new(StringComparer.Ordinal);

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

    /// <summary>The org members' own keys (hub phase 1, M.ORCH-T1640): <c>members.json</c>, reread as it changes. The default has
    /// none, so only the owner key mints a token.</summary>
    public IMemberKeys MemberKeys { get; init; } = NoMemberKeys.Instance;

    /// <summary>The session owners' consents to other members' interact grants (hub phase 2, M.ORCH-T1641), in memory.</summary>
    public GatewayConsents Consents { get; init; } = new();

    /// <summary>The Windows account each hosted session runs under (hub phase 2): this gateway's own user for every one, in this build.</summary>
    public ISessionAccounts SessionAccounts { get; init; } = HostUserAccounts.Instance;

    /// <summary>Starts and stops CLIs for members' clients (hub phase 2); none by default.</summary>
    public ISessionLauncher Launcher { get; init; } = NoLauncher.Instance;
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
    [property: JsonPropertyName("paused")] bool Paused,
    [property: JsonPropertyName("isMenu")] bool IsMenu,
    // Hub phase 2 (M.ORCH-T1641), only while it's on (null otherwise, and left out): the member who owns the session, and where an
    // interact grant that waits on their consent stands (ask, pending or denied; gone once they allow it).
    [property: JsonPropertyName("owner")] string? Owner = null,
    [property: JsonPropertyName("consent")] string? Consent = null);

internal sealed record TokenRequest(
    [property: JsonPropertyName("ownerKey")] string? OwnerKey,
    [property: JsonPropertyName("scope")] string? Scope,
    [property: JsonPropertyName("minutes")] int? Minutes,
    [property: JsonPropertyName("memberKey")] string? MemberKey = null);

/// <summary><c>POST /tickets</c>'s body: the tickets CLI's arguments, and the folder (relative to the workspace root) it was
/// typed in, which picks the repo a command without <c>--repo</c> means.</summary>
internal sealed record TicketsRequest(
    [property: JsonPropertyName("args")] List<string>? Args,
    [property: JsonPropertyName("directory")] string? Directory);

internal sealed record TicketsResponse(
    [property: JsonPropertyName("exitCode")] int ExitCode,
    [property: JsonPropertyName("output")] string Output);

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
/// address in five minutes and it answers 429 until they age out. <c>{ memberKey, ... }</c> instead (hub phase 1, M.ORCH-T1640):
/// an active org member's own key, from <c>orchestrator machine invite</c>, to a token whose subject is their email.</item>
/// <item><c>POST /tickets</c>: <c>{ args, directory? }</c> runs the tickets CLI here for the owner's own client (M.ORCH-T1479):
/// the owner's interact token, <c>{ exitCode, output }</c> back; or an active org member's interact token for
/// <see cref="MemberTickets"/>' allowlist, run as them and audited per member (hub phase 1, M.ORCH-T1640).</item>
/// <item><c>POST /branches</c>: <c>{ args }</c> runs <c>orchestrator branches</c> here (its listing or <c>report</c>, nothing else)
/// for the owner's own client's local test runs of work branches (M.ORCH-T1635); same token rule and answer as <c>/tickets</c>.</item>
/// <item><c>GET /sessions</c>: this machine's hosted sessions the caller may see.</item>
/// <item><c>GET /sessions/{id}/stream</c>, a WebSocket (permessage-deflate offered): the session's frames, both ways
/// (<see cref="TermBridge"/>); <c>?mode=watch</c> asks for read-only. It lasts as long as its token, or the latest fresh one
/// the viewer sends in a RefreshToken frame (M.ORCH-T1041); one that lapses ends it as "token expired", a replaced signing key
/// as "access revoked".</item>
/// </list>
/// Everything but <c>/health</c> and <c>/auth/token</c> needs <c>Authorization: Bearer &lt;token&gt;</c>. Every handler contains
/// its exceptions; nothing logs a token, a key or a header.
/// </summary>
internal static class GatewayApp
{
    public static string Version { get; } = typeof(GatewayApp).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>How many access checks in a row may fail before a stream is closed (M.ORCH-T984).</summary>
    public const int MaxWatchFailures = 2;

    /// <summary>How many refused token refreshes one stream logs and audits (M.ORCH-T1041); later ones are dropped unsaid.</summary>
    public const int MaxRefusedRefreshes = 5;

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
                string subject;
                if (GatewayKeys.Matches(g.OwnerKey, request.OwnerKey)) subject = g.Options.Owner;
                else if (request.OwnerKey is null && !string.IsNullOrWhiteSpace(request.MemberKey))
                {
                    // An org member's own key (hub phase 1, M.ORCH-T1640): its token is theirs, never the owner's, and only while they
                    // are an active member; the ACL decides what it sees, the tickets allowlist what it runs.
                    var member = g.MemberKeys.Match(request.MemberKey);
                    if (member is null)
                    {
                        g.Log.Write($"token refused: unknown or revoked member key from {from}");
                        AuditRefusal(g, "", from.ToString(), "unknown or revoked member key");
                        return TypedResults.Unauthorized();
                    }
                    if (!g.Grants.IsActiveMember(member) || string.Equals(member, g.Options.Owner.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        g.Log.Write($"token refused: {member} isn't an active member, from {from}");
                        g.Audit.Write(Record(g, TermGatewayAudit.Refused, member, "", "none", from.ToString(), detail: "member key: not an active member"));
                        return TypedResults.Unauthorized();
                    }
                    subject = member;
                }
                else
                {
                    g.Log.Write($"token refused: wrong owner key from {from}");
                    AuditRefusal(g, "", from.ToString(), "wrong owner key");
                    return TypedResults.Unauthorized();
                }
                g.Throttle.Succeeded(from);
                var scope = request.Scope is null ? GatewayScopes.Interact : request.Scope.ToLowerInvariant();
                if (!GatewayScopes.IsValid(scope)) return TypedResults.BadRequest();
                var minutes = Math.Clamp(request.Minutes ?? g.Options.TokenMinutes, 1, GatewayOptions.MaxTokenMinutes);
                var (token, expires) = g.Tokens.Issue(subject, scope, TimeSpan.FromMinutes(minutes));
                g.Log.Write($"token issued to {subject} ({scope}, {minutes} min) for {from}");
                if (!ReferenceEquals(subject, g.Options.Owner)) g.MemberKeys.Seen(subject);
                return TypedResults.Ok(new TokenResponse(token, subject, scope, expires));
            }
            catch (Exception ex)
            {
                g.Log.Error(ex, "issuing a token failed; contained");
                return TypedResults.StatusCode(StatusCodes.Status500InternalServerError);
            }
        });

        app.MapPost("/tickets", async (TicketsRequest? request, HttpContext http) =>
        {
            try
            {
                // The owner's own client (M.ORCH-T1479): its tickets commands run here, on the host, which owns the ticket database.
                // Only the owner's interact token: a member's token never reaches it.
                if (Authenticate(http, g) is not { } who)
                {
                    AuditRefusal(g, "", RemoteOf(http), TokenRefusal(http));
                    return Results.Unauthorized();
                }
                var isOwner = string.Equals(who.Subject.Trim(), g.Options.Owner.Trim(), StringComparison.OrdinalIgnoreCase);
                // An org member's interact token (hub phase 1, M.ORCH-T1640) may run the allowlisted verbs as themselves.
                if (who.Scope != GatewayScopes.Interact || (!isOwner && !g.Grants.IsActiveMember(who.Subject)))
                {
                    AuditRefusal(g, who.Subject, RemoteOf(http), "tickets: not the owner's interact token");
                    return Results.StatusCode(StatusCodes.Status403Forbidden);
                }
                if (request?.Args is not { Count: > 0 and <= 64 } args || args.Any(a => a is null || a.Length > 32_000)) return Results.BadRequest();
                if (!isOwner) return await MemberTicketsAsync(http, g, who.Subject, args, request.Directory).ConfigureAwait(false);
                g.Log.Write($"tickets {args[0]} {(args.Count > 1 ? args[1] : "")} for {who.Subject} from {RemoteOf(http)}");
                var result = await TicketsRunner.RunAsync(args, request.Directory, http.RequestAborted);
                return Results.Json(new TicketsResponse(result.ExitCode, result.Output));
            }
            catch (Exception ex)
            {
                g.Log.Error(ex, "running a tickets command failed; contained");
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }
        });

        app.MapPost("/branches", async (TicketsRequest? request, HttpContext http) =>
        {
            try
            {
                // The owner's own client (M.ORCH-T1635): the host's work-branch queue, and its local test runs' reports, both
                // through orchestrator branches here, which owns the branch builds' record. The owner's interact token only.
                if (Authenticate(http, g) is not { } who)
                {
                    AuditRefusal(g, "", RemoteOf(http), TokenRefusal(http));
                    return Results.Unauthorized();
                }
                if (!string.Equals(who.Subject.Trim(), g.Options.Owner.Trim(), StringComparison.OrdinalIgnoreCase) || who.Scope != GatewayScopes.Interact)
                {
                    AuditRefusal(g, who.Subject, RemoteOf(http), "branches: not the owner's interact token");
                    return Results.StatusCode(StatusCodes.Status403Forbidden);
                }
                if (request?.Args is not { Count: > 0 and <= 64 } args || args.Any(a => a is null || a.Length > 32_000) || !TicketsRunner.BranchesAllowed(args))
                    return Results.BadRequest();
                g.Log.Write($"orchestrator {args[0]} {(args.Count > 1 ? args[1] : "")} for {who.Subject} from {RemoteOf(http)}");
                var result = await TicketsRunner.RunBranchesAsync(args, http.RequestAborted);
                return Results.Json(new TicketsResponse(result.ExitCode, result.Output));
            }
            catch (Exception ex)
            {
                g.Log.Error(ex, "running an orchestrator branches command failed; contained");
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
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
                if (!string.Equals(who.Subject.Trim(), g.Options.Owner.Trim(), StringComparison.OrdinalIgnoreCase)) g.MemberKeys.Seen(who.Subject);
                var list = new List<GatewaySession>();
                var broadcast = g.Broadcast.Current;
                var sessions = g.Sessions.Enumerate();
                foreach (var s in sessions)
                {
                    // Only what the owner shares (M.ORCH-T935): remote watching is opt-in, per session or for all of them.
                    if (!broadcast.IsShared(s.Id)) continue;
                    var (access, decision, consent) = Access(g, who.Subject, who.Scope, s, null);
                    if (access == GatewayAccess.None) continue;
                    list.Add(new GatewaySession(s.Id, ListedTitle(g, s, broadcast), s.Login, s.Repo, s.Cols, s.Rows, s.State, s.ExitCode, s.StartedUtc, s.Protocol,
                        access == GatewayAccess.Interact ? GatewayScopes.Interact : GatewayScopes.Watch, broadcast.IsPaused(s.Id), s.IsMenu,
                        decision.SessionOwner, ConsentText(decision, consent)));
                }
                // Titles kept for sessions that ended are let go.
                if (g.ListedTitles.Count > sessions.Count)
                {
                    var live = sessions.Select(s => TitleKey(s.Id)).ToHashSet(StringComparer.Ordinal);
                    foreach (var key in g.ListedTitles.Keys) if (!live.Contains(key)) g.ListedTitles.TryRemove(key, out _);
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

        MapHub(app, g);
    }

    /// <summary>
    /// Hub phase 2's endpoints (M.ORCH-T1641), each answering 404 unless the ACL snapshot carries a hub policy (the Vault's
    /// HubMemberSessions, on the host only), so a gateway without it looks exactly as before:
    /// <list type="bullet">
    /// <item><c>POST /sessions</c> <c>{ repo, login? }</c>: starts a CLI in a repo on one of the caller's own logins (the machine
    /// owner: any login), as <see cref="TermHubPolicy.PlanLaunch"/> allows; an interact token.</item>
    /// <item><c>DELETE /sessions/{id}</c>: stops a CLI its owner (or the machine owner) no longer wants; anyone else gets 404.</item>
    /// <item><c>GET /consents</c>: the consent requests waiting on the caller, as a session owner.</item>
    /// <item><c>POST /consents/{id}</c> <c>{ allow }</c>: their answer; an interact token.</item>
    /// </list>
    /// Each start, stop and answer is audited by who and what, never content.
    /// </summary>
    private static void MapHub(WebApplication app, GatewayServices g)
    {
        app.MapPost("/sessions", async (StartRequest? request, HttpContext http) =>
        {
            try
            {
                if (Authenticate(http, g) is not { } who)
                {
                    AuditRefusal(g, "", RemoteOf(http), TokenRefusal(http));
                    return Results.Unauthorized();
                }
                if (g.Grants.Hub is not { } hub) return Results.NotFound();
                var from = RemoteOf(http);
                var isOwner = IsOwner(g, who.Subject);
                IResult Refuse(string why, int status = StatusCodes.Status403Forbidden)
                {
                    g.Log.Write($"start for {who.Subject} from {from} refused: {why}");
                    g.Audit.Write(Record(g, TermGatewayAudit.Refused, who.Subject, "", "none", from, detail: "start: " + why));
                    return Results.Json(new TicketsResponse(1, $"The host refused it: {why}."), statusCode: status);
                }
                if (who.Scope != GatewayScopes.Interact) return Refuse("starting a CLI needs an interact token");
                if (!isOwner && !g.Grants.IsActiveMember(who.Subject)) return Refuse("only active org members start CLIs here");
                var repo = request?.Repo?.Trim();
                if (!HubNames.IsRepo(repo)) return Results.BadRequest();
                var login = TermMemberKeys.Normalize(request!.Login is { Length: > 0 } l ? l : who.Subject);
                if (login.Length is 0 or > 256 || !login.Contains('@', StringComparison.Ordinal)) return Results.BadRequest();
                if (!isOwner && !g.Grants.LoginsOf(who.Subject).Contains(login)) return Refuse("members start CLIs only on their own logins");
                var plan = isOwner ? TermLaunchPlan.Host : hub.PlanLaunch(who.Subject);
                if (plan.Kind == TermLaunchKind.Refused) return Refuse(plan.Why ?? "not allowed");
                g.Log.Write($"start {repo} on {login} for {who.Subject} from {from} ({plan.Kind})");
                var result = await g.Launcher.StartAsync(repo!, login, plan, http.RequestAborted).ConfigureAwait(false);
                g.Audit.Write(Record(g, TermGatewayAudit.Start, who.Subject, "", GatewayScopes.Interact, from,
                    detail: $"{repo} on {login}: {(result.Ok ? "started" : result.NotSupported ? "not supported" : "failed")}"));
                return Results.Json(new TicketsResponse(result.Ok ? 0 : 1, result.Output),
                    statusCode: result.Ok ? StatusCodes.Status200OK : result.NotSupported ? StatusCodes.Status501NotImplemented : StatusCodes.Status502BadGateway);
            }
            catch (Exception ex)
            {
                g.Log.Error(ex, "starting a CLI failed; contained");
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }
        });

        app.MapDelete("/sessions/{id}", (string id, HttpContext http) =>
        {
            try
            {
                if (Authenticate(http, g) is not { } who)
                {
                    AuditRefusal(g, TermProtocol.Sanitize(id), RemoteOf(http), TokenRefusal(http));
                    return Results.Unauthorized();
                }
                if (g.Grants.Hub is null) return Results.NotFound();
                var from = RemoteOf(http);
                var session = g.Sessions.Get(id);
                // Only the machine owner, or the member who owns the CLI with an interact token; anyone else can't tell it's there.
                var mine = session is not null && (IsOwner(g, who.Subject)
                    || (who.Scope == GatewayScopes.Interact && g.Grants.IsActiveMember(who.Subject)
                        && g.Grants.Decide(who.Subject, session, g.SessionAccounts.AccountOf(session)).SessionOwner == TermMemberKeys.Normalize(who.Subject)));
                if (!mine)
                {
                    g.Audit.Write(Record(g, TermGatewayAudit.Refused, who.Subject, TermProtocol.Sanitize(id), "none", from,
                        detail: session is null ? "stop: no such session" : "stop: not their CLI"));
                    return Results.NotFound();
                }
                var result = g.Launcher.Stop(session!);
                g.Log.Write($"stop {session!.Id} for {who.Subject} from {from}: {(result.Ok ? "stopped" : result.Output)}");
                g.Audit.Write(Record(g, TermGatewayAudit.Stop, who.Subject, session.Id, GatewayScopes.Interact, from,
                    detail: result.Ok ? "stopped" : result.NotSupported ? "not supported" : "failed"));
                return Results.Json(new TicketsResponse(result.Ok ? 0 : 1, result.Output),
                    statusCode: result.Ok ? StatusCodes.Status200OK : result.NotSupported ? StatusCodes.Status501NotImplemented : StatusCodes.Status409Conflict);
            }
            catch (Exception ex)
            {
                g.Log.Error(ex, "stopping a CLI failed; contained");
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }
        });

        app.MapGet("/consents", (HttpContext http) =>
        {
            try
            {
                if (Authenticate(http, g) is not { } who)
                {
                    AuditRefusal(g, "", RemoteOf(http), TokenRefusal(http));
                    return Results.Unauthorized();
                }
                if (g.Grants.Hub is null) return Results.NotFound();
                return Results.Json(g.Consents.PendingFor(who.Subject));
            }
            catch (Exception ex)
            {
                g.Log.Error(ex, "listing consents failed; contained");
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }
        });

        app.MapPost("/consents/{id}", (string id, ConsentAnswer? answer, HttpContext http) =>
        {
            try
            {
                if (Authenticate(http, g) is not { } who)
                {
                    AuditRefusal(g, "", RemoteOf(http), TokenRefusal(http));
                    return Results.Unauthorized();
                }
                if (g.Grants.Hub is null) return Results.NotFound();
                if (who.Scope != GatewayScopes.Interact) return Results.StatusCode(StatusCodes.Status403Forbidden);
                if (answer?.Allow is not { } allow || id.Length is 0 or > 64) return Results.BadRequest();
                // Only the session's owner answers, and only while the request waits: an unknown, lapsed or someone else's is 404.
                if (g.Consents.Answer(id, who.Subject, allow) is not { } answered) return Results.NotFound();
                var from = RemoteOf(http);
                g.Log.Write($"consent {answered.Id}: {who.Subject} {(allow ? "allowed" : "refused")} {answered.Viewer} typing into {answered.Session}");
                g.Audit.Write(Record(g, TermGatewayAudit.Consent, who.Subject, answered.Session, allow ? GatewayScopes.Interact : GatewayScopes.Watch, from,
                    detail: $"{(allow ? "allowed" : "refused")} {answered.Viewer}"));
                return Results.Json(answered);
            }
            catch (Exception ex)
            {
                g.Log.Error(ex, "answering a consent failed; contained");
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }
        });
    }

    /// <summary>Whether <paramref name="subject"/> is the machine owner.</summary>
    private static bool IsOwner(GatewayServices g, string subject) =>
        string.Equals(subject.Trim(), g.Options.Owner.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// What <paramref name="subject"/> holding a <paramref name="scope"/> token may do with <paramref name="s"/> as asked
    /// (<paramref name="requestedMode"/>): the grants' decision (<see cref="IGatewayGrants.Decide"/>), held at watch while an interact
    /// grant waits on its session owner's consent (hub phase 2, M.ORCH-T1641), then the least of that, the token's scope and the
    /// request. Without a hub policy this is exactly the grant, scope and request as before.
    /// </summary>
    internal static (GatewayAccess Access, GatewayDecision Decision, ConsentState Consent) Access(GatewayServices g, string subject, string scope,
        TermSessionRecord s, string? requestedMode)
    {
        var decision = g.Grants.Decide(subject, s, g.SessionAccounts.AccountOf(s));
        var granted = decision.Access;
        var consent = ConsentState.None;
        if (granted == GatewayAccess.Interact && decision.ConsentKey is { } key)
        {
            consent = g.Consents.State(key);
            if (consent != ConsentState.Allowed) granted = GatewayAccess.Watch;
        }
        return (OwnerOnlyGrants.Effective(granted, scope, requestedMode), decision, consent);
    }

    /// <summary>How <c>GET /sessions</c> lists a consent that holds the caller at watch: ask (not asked yet, or it lapsed), pending
    /// or denied; null when no consent is involved or it was given.</summary>
    private static string? ConsentText(GatewayDecision decision, ConsentState consent) =>
        decision.ConsentKey is null ? null : consent switch
        {
            ConsentState.Allowed => null,
            ConsentState.Pending => "pending",
            ConsentState.Denied => "denied",
            _ => "ask",
        };

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
        var (access, decision, consent) = session is null || !shared ? (GatewayAccess.None, new GatewayDecision(GatewayAccess.None), ConsentState.None)
            : Access(g, who.Subject, who.Scope, session, requestedMode);
        var from = http.Connection.RemoteIpAddress?.ToString() ?? "?";
        // Hub phase 2 (M.ORCH-T1641): another member's interact grant on a member's CLI, first used: its owner is asked, and the
        // viewer watches meanwhile; once they allow it, the viewer's next attach types.
        if (session is not null && access == GatewayAccess.Watch && decision is { ConsentKey: { } key, SessionOwner: { } sessionOwner }
            && consent == ConsentState.None && who.Scope == GatewayScopes.Interact
            && !string.Equals(requestedMode, GatewayScopes.Watch, StringComparison.OrdinalIgnoreCase)
            && g.Consents.Ask(key, sessionOwner, TermMemberKeys.Normalize(who.Subject), session.Id, g.Grants.Hub?.ConsentWindow ?? TimeSpan.FromSeconds(TermHubPolicy.DefaultConsentSeconds)) is { } asked)
        {
            g.Log.Write($"consent {asked.Id}: asked {sessionOwner} whether {who.Subject} may type into {session.Id}; watching until they answer");
            g.Audit.Write(Record(g, TermGatewayAudit.Consent, who.Subject, session.Id, GatewayScopes.Watch, from, detail: $"asked {sessionOwner}"));
        }
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

        WebSocket accepted;
        try
        {
            accepted = await http.WebSockets.AcceptWebSocketAsync(new WebSocketAcceptContext
            {
                // permessage-deflate: VT compresses 5 to 10x (spike section 5). Offered; used when the viewer asks for it. Not on
                // an interactive stream while hub member sessions are on (M.ORCH-T1641): typed input beside secrets on one
                // compressed stream is the CRIME/BREACH shape (T934/T935).
                DangerousEnableCompression = !(access == GatewayAccess.Interact && g.Grants.Hub is not null),
                DisableServerContextTakeover = false,
                ServerMaxWindowBits = 15,
            }).ConfigureAwait(false);
        }
        catch
        {
            // The upgrade failed (M.ORCH-T1016): TermBridge, which owns the pipe from here on, never runs, so the pipe is closed
            // here; otherwise each failed upgrade leaks a handle and leaves a half-open viewer on the host.
            await DisposeQuietly(pipe, g).ConfigureAwait(false);
            throw;
        }
        using var socket = accepted;
        var mode = access == GatewayAccess.Interact ? GatewayScopes.Interact : GatewayScopes.Watch;
        var masked = g.Broadcast.Current.IsMasked(session.Id);
        g.Log.Write($"attach {session.Id} by {who.Subject} from {from} ({mode}, {(masked ? "masked" : "unmasked")})");
        g.Audit.Write(Record(g, TermGatewayAudit.Attach, who.Subject, session.Id, mode, from));
        // The owner's dashboard shows a "being watched" banner while this lasts (M.ORCH-T935).
        var watcher = g.Watchers.Attach(session.Id, who.Subject, access, from);

        // Every interval: the input typed since (counts and sizes only), and the token and access checked again, so an expired
        // token, a replaced signing key, or a revoked or expired grant closes the stream rather than lasting as long as the
        // connection does. The viewer may send a fresh token in band before its own expires (M.ORCH-T1041), which then counts.
        var input = new GatewayInputMeter();
        using var revoke = new CancellationTokenSource();
        using var stopWatch = new CancellationTokenSource();
        var revoked = false;
        var credential = new StreamCredential(token!, who);
        var endedWhy = "access revoked";
        var refusedRefreshes = 0;
        void Refresh(string fresh)
        {
            var p = g.Tokens.Validate(fresh.Trim());
            var refused = p is null ? "bad or expired token"
                : !string.Equals(p.Subject.Trim(), who.Subject.Trim(), StringComparison.OrdinalIgnoreCase) ? "another member's token"
                : IsRevokedMember(g, p.Subject) ? "their member key was revoked"
                : Access(g, p.Subject, p.Scope, session, requestedMode).Access < access ? "less access than the stream has"
                : null;
            if (refused is not null)
            {
                // A viewer that keeps sending bad tokens isn't let flood the log and the audit.
                if (Interlocked.Increment(ref refusedRefreshes) > MaxRefusedRefreshes) return;
                g.Log.Write($"token refresh for {session.Id} by {who.Subject} refused: {refused}");
                g.Audit.Write(Record(g, TermGatewayAudit.Refused, who.Subject, session.Id, mode, from, detail: "token refresh refused: " + refused));
                return;
            }
            if (credential.Extend(fresh.Trim(), p!))
                g.Log.Write($"token for {session.Id} by {who.Subject} refreshed: good until {p!.ExpiresUtc:HH:mm:ss}Z");
        }
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
                    // The token as well as the grant (M.ORCH-T983): the latest one the viewer sent expired, or signing.key was
                    // replaced, so it no longer validates, and the stream ends with it. Expiry is told apart from a replaced
                    // key (M.ORCH-T1041): the one is the viewer not refreshing in time, the other the owner revoking everyone.
                    var (current, holder) = credential.Now;
                    if (g.Tokens.Validate(current) is null)
                    {
                        var expired = g.Tokens.Expired(holder);
                        endedWhy = expired ? "token expired" : "access revoked";
                        why = expired ? "lapsed: its token expired and no fresh one came" : "revoked: the signing key changed";
                    }
                    else if (IsRevokedMember(g, who.Subject))
                    {
                        // machine revoke (hub phase 1, M.ORCH-T1640): closed with 1008 like any revoked access.
                        endedWhy = "access revoked";
                        why = "revoked: their member key was revoked";
                    }
                    else
                    {
                        var now = Access(g, who.Subject, holder.Scope, session, requestedMode).Access;
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
                new BroadcastStream(session.Id, session.StartedUtc, g.Broadcast, g.Secrets), Refresh, () => endedWhy).ConfigureAwait(false);
        }
        finally { g.Watchers.Detach(watcher); }
        await stopWatch.CancelAsync().ConfigureAwait(false);
        try { await watch.ConfigureAwait(false); } catch (Exception) { }
        WriteInput(g, input, who.Subject, session.Id, mode, from);
        var ended = revoked ? endedWhy : result.EndedBy;
        g.Log.Write($"detach {session.Id} by {who.Subject}: ended by {ended}, {result.FramesUp} frames up, " +
                    $"{result.FramesDown} down ({result.BytesDown} bytes), {result.Dropped} dropped, {result.Withheld} withheld while paused");
        g.Audit.Write(Record(g, TermGatewayAudit.Detach, who.Subject, session.Id, mode, from, result.FramesUp, result.BytesDown,
            $"ended by {ended}; {result.FramesDown} frames down, {result.Dropped} dropped"));
    }

    private static async Task DisposeQuietly(Stream pipe, GatewayServices g)
    {
        try { await pipe.DisposeAsync().ConfigureAwait(false); }
        catch (Exception ex) { g.Log.Error(ex, "closing a session pipe failed; contained"); }
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

    /// <summary>The title <c>GET /sessions</c> lists for <paramref name="s"/> (M.ORCH-T981): masked as its stream is, unless the
    /// owner turned masking off for it; while it is paused, the one listed before the pause (none if it never was), so a title
    /// set during the pause never reaches a viewer, as on the stream.</summary>
    private static string? ListedTitle(GatewayServices g, TermSessionRecord s, TermBroadcastSettings broadcast)
    {
        var key = TitleKey(s.Id);
        string? raw;
        if (broadcast.IsPaused(s.Id)) raw = g.ListedTitles.GetValueOrDefault(key);
        else g.ListedTitles[key] = raw = s.Title;
        return raw is null || !broadcast.IsMasked(s.Id) ? raw : g.Secrets.MaskText(raw);
    }

    private static string TitleKey(string id) => id.Trim().ToLowerInvariant();

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

    /// <summary>The bearer token's holder, or null. Only the Authorization header is read: a token in a URL would land in logs. A
    /// member whose key was revoked (hub phase 1, M.ORCH-T1640) is refused, whatever token they hold.</summary>
    internal static GatewayPrincipal? Authenticate(HttpContext http, GatewayServices g) =>
        BearerToken(http) is { } token && g.Tokens.Validate(token) is { } p && !IsRevokedMember(g, p.Subject) ? p : null;

    /// <summary>Whether <paramref name="subject"/> is a member <c>orchestrator machine revoke</c> cut off; never the owner.</summary>
    private static bool IsRevokedMember(GatewayServices g, string subject) =>
        !string.Equals(subject.Trim(), g.Options.Owner.Trim(), StringComparison.OrdinalIgnoreCase) && g.MemberKeys.IsRevoked(subject);

    /// <summary>
    /// A tickets command from an org member's token (hub phase 1, M.ORCH-T1640): only <see cref="MemberTickets.Verbs"/>, never an
    /// option that reaches a host file or names another author, and <c>close</c> only on a ticket one of their logins works or is
    /// assigned. It runs as them (a note's author is their email) and is audited per member: the verb, never the text. A refusal
    /// answers 403 with why in the usual <c>{ exitCode, output }</c>.
    /// </summary>
    private static async Task<IResult> MemberTicketsAsync(HttpContext http, GatewayServices g, string member, List<string> args, string? directory)
    {
        var from = RemoteOf(http);
        var verb = MemberTickets.Verb(args);
        IResult Refuse(string why)
        {
            g.Log.Write($"tickets {verb} for {member} from {from} refused: {why}");
            g.Audit.Write(Record(g, TermGatewayAudit.Refused, member, "", "none", from, detail: $"tickets {verb}: {why}"));
            return Results.Json(new TicketsResponse(1, $"The host refused it: {why}."), statusCode: StatusCodes.Status403Forbidden);
        }
        if (MemberTickets.Refusal(args) is { } refused) return Refuse(refused);
        if (args[1] == "close")
        {
            if (MemberTickets.TicketId(args) is not { } id) return Refuse("close names one ticket id");
            var show = await TicketsRunner.RunAsync(["ticket", "show", id, "--json"], directory, http.RequestAborted).ConfigureAwait(false);
            if (show.ExitCode != 0) return Refuse($"no ticket {id} to close");
            if (!MemberTickets.WorkedBy(show.Output, g.Grants.LoginsOf(member)))
                return Refuse($"members close only tickets their own logins work or are assigned, and {id} isn't one");
        }
        // A note is theirs: its author is the member, never the host's user (--author from them was refused above).
        List<string> run = args[1] == "note" ? [.. args, "--author", member] : args;
        g.MemberKeys.Seen(member);
        g.Log.Write($"tickets {verb} for {member} from {from}");
        var result = await TicketsRunner.RunAsync(run, directory, http.RequestAborted).ConfigureAwait(false);
        g.Audit.Write(Record(g, TermGatewayAudit.Tickets, member, "", GatewayScopes.Interact, from, detail: $"{verb}: exit {result.ExitCode}"));
        return Results.Json(new TicketsResponse(result.ExitCode, result.Output));
    }

    /// <summary>The bearer token in the Authorization header, unchecked; held only in memory, never logged.</summary>
    private static string? BearerToken(HttpContext http)
    {
        var header = http.Request.Headers.Authorization.ToString();
        const string scheme = "Bearer ";
        return header.StartsWith(scheme, StringComparison.OrdinalIgnoreCase) ? header[scheme.Length..].Trim() : null;
    }
}

/// <summary>
/// The token a stream lasts on (M.ORCH-T1041): the one it was opened with, then the latest fresh one the viewer sent in band
/// that validated for the same member. Held in memory only, never logged.
/// </summary>
internal sealed class StreamCredential(string token, GatewayPrincipal holder)
{
    private readonly System.Threading.Lock gate = new();
    private string token = token;
    private GatewayPrincipal holder = holder;

    /// <summary>The token now, and who it validated as.</summary>
    public (string Token, GatewayPrincipal Holder) Now
    {
        get { lock (gate) return (token, holder); }
    }

    /// <summary>Takes <paramref name="fresh"/> (validated as <paramref name="by"/>) when it lasts longer than the token held; false
    /// when it doesn't, so a replayed older token never shortens the stream.</summary>
    public bool Extend(string fresh, GatewayPrincipal by)
    {
        lock (gate)
        {
            if (by.ExpiresUtc <= holder.ExpiresUtc) return false;
            (token, holder) = (fresh, by);
            return true;
        }
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
