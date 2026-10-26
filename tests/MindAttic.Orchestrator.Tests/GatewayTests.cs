using System.IO.Pipelines;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using MindAttic.Orchestrator.Gateway;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Term;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The team gateway (M.ORCH-T933): tokens, grants, the WebSocket bridge over an in-memory pipe on a TestServer
/// (frames 1:1, a watcher's input and resize dropped, its Hello never sizing the session), and the daemon's supervisor.</summary>
public sealed partial class GatewayTests
{
    private const string Owner = "owner@example.com";
    private static readonly byte[] SigningKey = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
    private static readonly byte[] OwnerKey = Enumerable.Range(100, 32).Select(i => (byte)i).ToArray();

    // ── tokens ──────────────────────────────────────────────────────────────────

    [Test]
    public void A_token_it_issued_validates_with_its_subject_and_scope()
    {
        var tokens = new GatewayTokens(SigningKey);
        var (token, expires) = tokens.Issue(Owner, GatewayScopes.Watch, TimeSpan.FromMinutes(15));
        var who = tokens.Validate(token);
        Assert.That(who, Is.Not.Null);
        Assert.That(who!.Subject, Is.EqualTo(Owner));
        Assert.That(who.Scope, Is.EqualTo(GatewayScopes.Watch));
        Assert.That(who.ExpiresUtc, Is.EqualTo(expires));
    }

    [Test]
    public void A_tampered_forged_or_garbage_token_is_refused()
    {
        var tokens = new GatewayTokens(SigningKey);
        var (token, _) = tokens.Issue(Owner, GatewayScopes.Watch, TimeSpan.FromMinutes(15));
        var parts = token.Split('.');
        var widened = GatewayTokens.Base64Url(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(GatewayTokens.FromBase64Url(parts[1])!).Replace("watch", "interact")));
        Assert.That(tokens.Validate($"{parts[0]}.{widened}.{parts[2]}"), Is.Null, "payload changed under the old signature");
        Assert.That(new GatewayTokens(OwnerKey).Validate(token), Is.Null, "another gateway's key");
        Assert.That(tokens.Validate(null), Is.Null);
        Assert.That(tokens.Validate(""), Is.Null);
        Assert.That(tokens.Validate("v1.abc.def"), Is.Null);
        Assert.That(tokens.Validate("Bearer " + token), Is.Null);
    }

    [Test]
    public void A_token_is_refused_once_it_expires_and_never_lives_past_an_hour()
    {
        var now = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);
        var tokens = new GatewayTokens(SigningKey, () => now);
        var (token, expires) = tokens.Issue(Owner, GatewayScopes.Interact, TimeSpan.FromDays(3));
        Assert.That(expires - now, Is.EqualTo(TimeSpan.FromMinutes(GatewayOptions.MaxTokenMinutes)));
        Assert.That(tokens.Validate(token), Is.Not.Null);
        now = expires.AddMinutes(1);
        Assert.That(tokens.Validate(token), Is.Null);
    }

    [Test]
    public void The_owner_key_matches_only_itself()
    {
        Assert.That(GatewayKeys.Matches(OwnerKey, GatewayKeys.Text(OwnerKey)), Is.True);
        Assert.That(GatewayKeys.Matches(OwnerKey, GatewayKeys.Text(SigningKey)), Is.False);
        Assert.That(GatewayKeys.Matches(OwnerKey, null), Is.False);
        Assert.That(GatewayKeys.Matches(OwnerKey, ""), Is.False);
    }

    [Test]
    public void Five_wrong_owner_keys_from_an_address_block_it_until_they_age_out()
    {
        var now = DateTime.UtcNow;
        var throttle = new LoginThrottle(() => now);
        var from = IPAddress.Parse("10.0.0.7");
        for (var i = 0; i < LoginThrottle.MaxFailures; i++)
        {
            Assert.That(throttle.Blocked(from), Is.False);
            throttle.Failed(from);
        }
        Assert.That(throttle.Blocked(from), Is.True);
        Assert.That(throttle.Blocked(IPAddress.Loopback), Is.False, "another address");
        now += LoginThrottle.Window + TimeSpan.FromSeconds(1);
        Assert.That(throttle.Blocked(from), Is.False);
    }

    [Test]
    public void Concurrent_owner_key_attempts_cant_slip_past_the_limit()
    {
        var throttle = new LoginThrottle();
        var from = IPAddress.Parse("10.0.0.8");
        var admitted = 0;
        Parallel.For(0, 64, _ => { if (throttle.TryBegin(from)) Interlocked.Increment(ref admitted); });
        Assert.That(admitted, Is.EqualTo(LoginThrottle.MaxFailures));
        Assert.That(throttle.Blocked(from), Is.True);
        throttle.Succeeded(from);
        Assert.That(throttle.TryBegin(from), Is.True, "a right key clears the address");
    }

    [Test]
    public void The_throttle_keeps_a_bounded_number_of_addresses_forgetting_the_longest_quiet_first()
    {
        var throttle = new LoginThrottle(capacity: 3);
        var a = IPAddress.Parse("10.0.0.1");
        for (var i = 0; i < LoginThrottle.MaxFailures; i++) throttle.Failed(a);
        throttle.Failed(IPAddress.Parse("10.0.0.2"));
        throttle.Failed(IPAddress.Parse("10.0.0.3"));
        throttle.Failed(a);
        throttle.Failed(IPAddress.Parse("10.0.0.4"));
        Assert.That(throttle.Count, Is.EqualTo(3));
        Assert.That(throttle.Blocked(a), Is.True, "failed most recently: kept");
        Assert.That(throttle.Blocked(IPAddress.Parse("2001:db8::1")), Is.False);
        for (var i = 0; i < LoginThrottle.MaxFailures; i++) throttle.Failed(IPAddress.Parse("2001:db8::" + (i + 1)));
        Assert.That(throttle.Blocked(IPAddress.Parse("2001:db8::ffff")), Is.True, "IPv6 counts by its /64");
    }

    // ── grants ──────────────────────────────────────────────────────────────────

    [Test]
    public void Only_the_owner_is_granted_anything_for_now()
    {
        var grants = new OwnerOnlyGrants(Owner);
        var session = Session("s1");
        Assert.That(grants.AccessTo(Owner, session), Is.EqualTo(GatewayAccess.Interact));
        Assert.That(grants.AccessTo("OWNER@example.com", session), Is.EqualTo(GatewayAccess.Interact));
        Assert.That(grants.AccessTo("teammate@example.com", session), Is.EqualTo(GatewayAccess.None));
    }

    [Test]
    public void Access_is_the_least_of_the_grant_the_token_and_the_mode_asked_for()
    {
        Assert.That(OwnerOnlyGrants.Effective(GatewayAccess.Interact, GatewayScopes.Interact, null), Is.EqualTo(GatewayAccess.Interact));
        Assert.That(OwnerOnlyGrants.Effective(GatewayAccess.Interact, GatewayScopes.Watch, null), Is.EqualTo(GatewayAccess.Watch));
        Assert.That(OwnerOnlyGrants.Effective(GatewayAccess.Interact, GatewayScopes.Interact, "watch"), Is.EqualTo(GatewayAccess.Watch));
        Assert.That(OwnerOnlyGrants.Effective(GatewayAccess.Watch, GatewayScopes.Interact, "interact"), Is.EqualTo(GatewayAccess.Watch));
        Assert.That(OwnerOnlyGrants.Effective(GatewayAccess.None, GatewayScopes.Interact, null), Is.EqualTo(GatewayAccess.None));
    }

    [Test]
    public void Only_an_interactive_viewers_input_and_resize_go_on_to_the_host()
    {
        Assert.That(TermBridge.Forwards(TermFrameKind.Input, GatewayAccess.Interact), Is.True);
        Assert.That(TermBridge.Forwards(TermFrameKind.Resize, GatewayAccess.Interact), Is.True);
        Assert.That(TermBridge.Forwards(TermFrameKind.Input, GatewayAccess.Watch), Is.False);
        Assert.That(TermBridge.Forwards(TermFrameKind.Resize, GatewayAccess.Watch), Is.False);
        Assert.That(TermBridge.Forwards(TermFrameKind.TakeSize, GatewayAccess.Interact), Is.True);
        Assert.That(TermBridge.Forwards(TermFrameKind.TakeSize, GatewayAccess.Watch), Is.False);
        Assert.That(TermBridge.Forwards(TermFrameKind.Hello, GatewayAccess.Interact), Is.False, "a second Hello");
        Assert.That(TermBridge.Forwards(TermFrameKind.Output, GatewayAccess.Interact), Is.False, "a host's kind");
    }

    // ── the HTTP surface and the bridge, on a TestServer ────────────────────────

    [Test]
    public async Task Sessions_needs_a_token_and_lists_what_the_caller_may_see_without_command_lines()
    {
        await using var gateway = await TestGateway.StartAsync();
        using var http = gateway.Server.CreateClient();
        Assert.That((await http.GetAsync("/sessions")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        http.DefaultRequestHeaders.Authorization = new("Bearer", gateway.Token(GatewayScopes.Watch));
        var body = await http.GetStringAsync("/sessions");
        Assert.That(body, Does.Contain("\"id\":\"s1\""));
        Assert.That(body, Does.Contain("\"access\":\"watch\""));
        Assert.That(body, Does.Not.Contain("secret-command-line"));
        Assert.That(body, Does.Not.Contain(gateway.Session.Pipe));
    }

    [Test]
    public async Task A_token_is_issued_for_the_owner_key_and_refused_for_a_wrong_one()
    {
        await using var gateway = await TestGateway.StartAsync();
        using var http = gateway.Server.CreateClient();
        var wrong = await http.PostAsync("/auth/token", Json("{\"ownerKey\":\"nope\"}"));
        Assert.That(wrong.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        var right = await http.PostAsync("/auth/token", Json($"{{\"ownerKey\":\"{GatewayKeys.Text(OwnerKey)}\",\"scope\":\"watch\"}}"));
        Assert.That(right.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var token = System.Text.Json.JsonDocument.Parse(await right.Content.ReadAsStringAsync()).RootElement.GetProperty("token").GetString();
        Assert.That(gateway.Services.Tokens.Validate(token)?.Scope, Is.EqualTo(GatewayScopes.Watch));
    }

    [Test]
    public async Task Stream_refuses_no_token_and_an_unknown_session()
    {
        await using var gateway = await TestGateway.StartAsync();
        var anonymous = gateway.Server.CreateWebSocketClient();
        Assert.ThrowsAsync<InvalidOperationException>(() => anonymous.ConnectAsync(new Uri("ws://localhost/sessions/s1/stream"), Timeout()));
        var client = gateway.Client(GatewayScopes.Interact);
        Assert.ThrowsAsync<InvalidOperationException>(() => client.ConnectAsync(new Uri("ws://localhost/sessions/nope/stream"), Timeout()));
        Assert.That(gateway.Pipes.Connected, Is.Zero);
    }

    [Test]
    public async Task A_session_the_owner_hasnt_shared_is_neither_listed_nor_attachable()
    {
        await using var gateway = await TestGateway.StartAsync();
        gateway.Broadcast.Current = new TermBroadcastSettings();
        using var http = gateway.Server.CreateClient();
        http.DefaultRequestHeaders.Authorization = new("Bearer", gateway.Token(GatewayScopes.Interact));
        Assert.That(await http.GetStringAsync("/sessions"), Is.EqualTo("[]"));
        var client = gateway.Client(GatewayScopes.Interact);
        Assert.ThrowsAsync<InvalidOperationException>(() => client.ConnectAsync(new Uri("ws://localhost/sessions/s1/stream"), Timeout()));
        Assert.That(gateway.Pipes.Connected, Is.Zero);

        // "Share my sessions" shares it.
        gateway.Broadcast.Current = new TermBroadcastSettings { ShareAll = true };
        Assert.That(await http.GetStringAsync("/sessions"), Does.Contain("\"id\":\"s1\""));
    }

    [Test]
    public async Task Attaching_and_detaching_are_announced_with_who_and_how()
    {
        await using var gateway = await TestGateway.StartAsync();
        var events = new System.Collections.Concurrent.BlockingCollection<TermWatchersEvent>();
        gateway.Watchers.Changed += events.Add;
        using (var socket = await gateway.Client(GatewayScopes.Watch).ConnectAsync(new Uri("ws://localhost/sessions/s1/stream"), Timeout()))
        {
            Assert.That(events.TryTake(out var attached, TimeSpan.FromSeconds(10)), Is.True);
            Assert.That(attached!.Event, Is.EqualTo(TermWatchersEvents.Attached));
            Assert.That(attached.Watcher!.Viewer, Is.EqualTo(Owner));
            Assert.That(attached.Watcher.Mode, Is.EqualTo(GatewayScopes.Watch));
            Assert.That(attached.Watchers.Single().SessionId, Is.EqualTo("s1"));
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", Timeout());
        }
        Assert.That(events.TryTake(out var detached, TimeSpan.FromSeconds(10)), Is.True);
        Assert.That(detached!.Event, Is.EqualTo(TermWatchersEvents.Detached));
        Assert.That(detached.Watchers, Is.Empty);
    }

    [Test]
    public async Task A_watcher_gets_the_hosts_frames_one_for_one_but_its_input_and_resize_never_reach_the_host()
    {
        await using var gateway = await TestGateway.StartAsync();
        using var socket = await gateway.Client(GatewayScopes.Interact).ConnectAsync(new Uri("ws://localhost/sessions/s1/stream?mode=watch"), Timeout());
        await Send(socket, TermProtocol.Frame(TermFrameKind.Hello, TermProtocol.Hello(TermProtocol.Version, 100, 25)));

        // The host sees the watcher's Hello at 0x0, a size it ignores, so the session's size stays its own.
        var host = gateway.Pipes.Host;
        var hello = await TermProtocol.ReadFrameAsync(host, Timeout());
        Assert.That(hello.Kind, Is.EqualTo(TermFrameKind.Hello));
        Assert.That(TermProtocol.ReadHello(hello.Payload), Is.EqualTo(((ushort)TermProtocol.Version, (ushort)0, (ushort)0)));
        Assert.That(TermProtocol.ReadViewerHello(hello.Payload)!.Interactive, Is.False, "and read-only from protocol 2 (M.ORCH-T930)");

        byte[][] sent =
        [
            TermProtocol.Frame(TermFrameKind.Hello, TermProtocol.Hello(TermProtocol.Version, 120, 30)),
            TermProtocol.Frame(TermFrameKind.Replay, TermProtocol.Replay(120, 30, TermModes.CursorVisible, "screen so far"u8, "\e[?25h"u8)),
            TermProtocol.Frame(TermFrameKind.Live, []),
            TermProtocol.Frame(TermFrameKind.Output, "live output"u8),
        ];
        foreach (var frame in sent) await host.WriteAsync(frame);
        await host.FlushAsync();
        foreach (var frame in sent) Assert.That(await Receive(socket), Is.EqualTo(frame), "each host frame arrives as one message, unchanged");

        await Send(socket, TermProtocol.Frame(TermFrameKind.Input, "rm -rf /\r"u8));
        await Send(socket, TermProtocol.Frame(TermFrameKind.Resize, TermProtocol.Size(80, 20)));
        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", Timeout());

        // Nothing more reaches the host before the gateway closes the pipe.
        Assert.ThrowsAsync<EndOfStreamException>(() => TermProtocol.ReadFrameAsync(host, Timeout()));
    }

    [Test]
    public async Task An_interactive_viewers_hello_input_and_resize_reach_the_host_unchanged()
    {
        await using var gateway = await TestGateway.StartAsync();
        using var socket = await gateway.Client(GatewayScopes.Interact).ConnectAsync(new Uri("ws://localhost/sessions/s1/stream"), Timeout());
        var hello = TermProtocol.Frame(TermFrameKind.Hello, TermProtocol.Hello(TermProtocol.Version, 100, 25));
        var input = TermProtocol.Frame(TermFrameKind.Input, "dir\r"u8);
        var resize = TermProtocol.Frame(TermFrameKind.Resize, TermProtocol.Size(90, 20));
        foreach (var frame in new[] { hello, input, resize }) await Send(socket, frame);

        var host = gateway.Pipes.Host;
        foreach (var frame in new[] { hello, input, resize })
        {
            var (kind, payload) = await TermProtocol.ReadFrameAsync(host, Timeout());
            Assert.That(TermProtocol.Frame(kind, payload), Is.EqualTo(frame));
        }
        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", Timeout());
    }

    [Test]
    public async Task A_viewer_that_doesnt_open_with_hello_is_closed_and_the_host_sees_nothing()
    {
        await using var gateway = await TestGateway.StartAsync();
        using var socket = await gateway.Client(GatewayScopes.Interact).ConnectAsync(new Uri("ws://localhost/sessions/s1/stream"), Timeout());
        await Send(socket, TermProtocol.Frame(TermFrameKind.Input, "x"u8));
        var buffer = new byte[256];
        var result = await socket.ReceiveAsync(buffer, Timeout());
        Assert.That(result.MessageType, Is.EqualTo(WebSocketMessageType.Close));
        Assert.That(socket.CloseStatus, Is.EqualTo(WebSocketCloseStatus.ProtocolError));
        Assert.ThrowsAsync<EndOfStreamException>(() => TermProtocol.ReadFrameAsync(gateway.Pipes.Host, Timeout()));
    }

    [Test]
    public async Task When_the_host_closes_the_pipe_the_viewer_is_told_and_closed()
    {
        await using var gateway = await TestGateway.StartAsync();
        using var socket = await gateway.Client(GatewayScopes.Watch).ConnectAsync(new Uri("ws://localhost/sessions/s1/stream"), Timeout());
        await Send(socket, TermProtocol.Frame(TermFrameKind.Hello, TermProtocol.Hello(TermProtocol.Version, 100, 25)));
        await TermProtocol.ReadFrameAsync(gateway.Pipes.Host, Timeout());
        await gateway.Pipes.Host.DisposeAsync();
        var result = await socket.ReceiveAsync(new byte[256], Timeout());
        Assert.That(result.MessageType, Is.EqualTo(WebSocketMessageType.Close));
        Assert.That(socket.CloseStatus, Is.EqualTo(WebSocketCloseStatus.NormalClosure));
    }

    // ── the daemon's side ───────────────────────────────────────────────────────

    [Test]
    public void The_settings_arguments_parse_back_to_the_same_gateway_and_config()
    {
        var settings = new GatewaySettings
        {
            Enabled = true, Bind = "100.64.1.2", Port = 9443, CertThumbprint = "ABCDEF", DevCertNames = ["ryan-pc.tailnet.ts.net"],
            Owner = Owner, TokenMinutes = 10,
        };
        var args = settings.Arguments();
        var options = GatewayOptions.Parse(args, out var error);
        Assert.That(error, Is.Empty);
        Assert.That(options, Is.Not.Null);
        Assert.That(options!.Command, Is.EqualTo("serve"));
        Assert.That(options.Bind, Is.EqualTo("100.64.1.2"));
        Assert.That(options.Port, Is.EqualTo(9443));
        Assert.That(options.CertThumbprint, Is.EqualTo("ABCDEF"));
        Assert.That(options.DevCertNames, Is.EqualTo(new[] { "ryan-pc.tailnet.ts.net" }));
        Assert.That(options.Owner, Is.EqualTo(Owner));
        Assert.That(options.TokenMinutes, Is.EqualTo(10));
        Assert.That(options.Config, Is.EqualTo(string.Join(' ', args)), "what the status file records, so the daemon sees no change");
    }

    [Test]
    public void Gateway_options_refuse_a_bad_bind_or_port()
    {
        Assert.That(GatewayOptions.Parse(["--bind", "not-an-ip"], out _), Is.Null);
        Assert.That(GatewayOptions.Parse(["--port", "70000"], out _), Is.Null);
        Assert.That(GatewayOptions.Parse(["frobnicate"], out _), Is.Null);
        Assert.That(GatewayOptions.Parse([], out _)!.BindAddress, Is.EqualTo(IPAddress.Loopback), "loopback unless told otherwise");
    }

    [Test]
    public void The_supervisor_starts_the_gateway_only_when_it_is_turned_on()
    {
        using var dir = new TempDir();
        var started = new List<IReadOnlyList<string>>();
        var supervisor = Supervisor(dir.Path, started, []);
        var now = DateTime.UtcNow;
        supervisor.Tick(new GatewaySettings(), now);
        Assert.That(started, Is.Empty, "off by default");
        supervisor.Tick(new GatewaySettings { Enabled = true }, now);
        Assert.That(started, Has.Count.EqualTo(1));
        Assert.That(started[0][0], Is.EqualTo("serve"));
        supervisor.Tick(new GatewaySettings { Enabled = true }, now.AddSeconds(15));
        Assert.That(started, Has.Count.EqualTo(1), "not again before the retry is due");
        supervisor.Tick(new GatewaySettings { Enabled = true }, now + GatewaySupervisor.FirstRetry);
        Assert.That(started, Has.Count.EqualTo(2), "it isn't running, so it's started again");
    }

    [Test]
    public void The_supervisor_restarts_a_managed_gateway_on_a_settings_change_and_stops_it_when_turned_off()
    {
        using var dir = new TempDir();
        var started = new List<IReadOnlyList<string>>();
        var stopped = new List<int>();
        var supervisor = Supervisor(dir.Path, started, stopped);
        var on = new GatewaySettings { Enabled = true };
        RunningGateway(dir.Path, on, managed: true);

        supervisor.Tick(on, DateTime.UtcNow);
        Assert.That(started, Is.Empty, "running with these settings");
        Assert.That(stopped, Is.Empty);

        supervisor.Tick(on with { Port = 9443 }, DateTime.UtcNow);
        Assert.That(stopped, Is.EqualTo(new[] { Environment.ProcessId }));
        Assert.That(started, Has.Count.EqualTo(1));

        supervisor.Tick(on with { Enabled = false }, DateTime.UtcNow);
        Assert.That(stopped, Has.Count.EqualTo(2));
    }

    [Test]
    public void The_supervisor_never_stops_a_gateway_started_by_hand()
    {
        using var dir = new TempDir();
        var started = new List<IReadOnlyList<string>>();
        var stopped = new List<int>();
        var supervisor = Supervisor(dir.Path, started, stopped);
        RunningGateway(dir.Path, new GatewaySettings { Enabled = true }, managed: false);
        supervisor.Tick(new GatewaySettings { Enabled = false }, DateTime.UtcNow);
        supervisor.Tick(new GatewaySettings { Enabled = true, Port = 1234 }, DateTime.UtcNow);
        Assert.That(stopped, Is.Empty);
        Assert.That(started, Is.Empty);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────

    private static GatewaySupervisor Supervisor(string dir, List<IReadOnlyList<string>> started, List<int> stopped) =>
        new((_, _) => { })
        {
            StatusDirectory = dir,
            FindExe = () => @"C:\fake\" + TermGateway.ExeName,
            StartProcess = (_, args) => { started.Add(args); return 4242; },
            StopProcess = stopped.Add,
        };

    /// <summary>A status file naming this test process as the gateway, so it reads as running.</summary>
    private static void RunningGateway(string dir, GatewaySettings settings, bool managed)
    {
        using var self = System.Diagnostics.Process.GetCurrentProcess();
        TermGateway.WriteStatus(new TermGatewayStatus
        {
            Pid = Environment.ProcessId, ProcessStartUtc = self.StartTime.ToUniversalTime(), Bind = settings.Bind, Port = settings.Port,
            Config = string.Join(' ', settings.Arguments()), Managed = managed,
        }, dir);
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("gateway-tests-").FullName;
        public void Dispose() { try { Directory.Delete(Path, true); } catch (IOException) { } }
    }

    private static TermSessionRecord Session(string id) => new()
    {
        Id = id, HostPid = 1, Pipe = TermProtocol.PipeName(id), Title = "a session", Repo = "MindAttic.Orchestrator",
        CommandLine = "secret-command-line", Cols = 120, Rows = 30, StartedUtc = DateTime.UtcNow,
    };

    private static System.Net.Http.StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private static CancellationToken Timeout() => new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token;

    private static Task Send(WebSocket socket, byte[] frame) => socket.SendAsync(frame, WebSocketMessageType.Binary, true, Timeout());

    private static async Task<byte[]> Receive(WebSocket socket)
    {
        var buffer = new byte[64 * 1024];
        using var message = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, Timeout());
            message.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        return message.ToArray();
    }

    /// <summary>A gateway on a TestServer, with one session ("s1") whose pipe is an in-memory duplex stream.</summary>
    private sealed class TestGateway : IAsyncDisposable
    {
        private WebApplication app = null!;
        public GatewayServices Services { get; private set; } = null!;
        public MemoryPipes Pipes { get; } = new();
        public TermSessionRecord Session { get; private init; } = GatewayTests.Session("s1");
        public TestServer Server => app.GetTestServer();

        /// <summary>The owner's broadcast choices (M.ORCH-T935): by default "s1" is shared, unmasked, so the bridge's frames go
        /// through one for one, as T933's tests expect.</summary>
        public FixedBroadcast Broadcast { get; } = new(new TermBroadcastSettings().Share("s1", true).Mask("s1", false));

        /// <summary>Who is attached; it writes no file and sends nothing on the bus.</summary>
        public WatcherBoard Watchers { get; } = new(null, null);

        /// <param name="grants">The grants (M.ORCH-T934); the owner alone by default.</param>
        /// <param name="audit">Where audit records go; nowhere by default.</param>
        /// <param name="session">The session in place of the default "s1".</param>
        public static async Task<TestGateway> StartAsync(IGatewayGrants? grants = null, IGatewayAudit? audit = null, TimeSpan? auditInterval = null,
            TermSessionRecord? session = null, GatewayTokens? tokens = null)
        {
            var gateway = session is null ? new TestGateway() : new TestGateway { Session = session };
            gateway.Services = new GatewayServices
            {
                Options = new GatewayOptions { Owner = Owner },
                Tokens = tokens ?? new GatewayTokens(SigningKey),
                OwnerKey = OwnerKey,
                Grants = grants ?? new OwnerOnlyGrants(Owner),
                Sessions = new FixedSessions(gateway.Session),
                Pipes = gateway.Pipes,
                Log = new GatewayLog(null),
                Audit = audit ?? NoAudit.Instance,
                AuditInterval = auditInterval ?? TimeSpan.FromSeconds(10),
                Broadcast = gateway.Broadcast,
                Watchers = gateway.Watchers,
            };
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], EnvironmentName = "Testing" });
            builder.WebHost.UseTestServer();
            gateway.app = GatewayApp.Build(builder, gateway.Services);
            await gateway.app.StartAsync();
            return gateway;
        }

        public string Token(string scope, string subject = Owner) => Services.Tokens.Issue(subject, scope, TimeSpan.FromMinutes(5)).Token;

        public WebSocketClient Client(string scope, string subject = Owner)
        {
            var client = Server.CreateWebSocketClient();
            var token = Token(scope, subject);
            client.ConfigureRequest = request => request.Headers.Authorization = "Bearer " + token;
            return client;
        }

        public async ValueTask DisposeAsync()
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    private sealed class FixedSessions(params TermSessionRecord[] sessions) : ISessionSource
    {
        public IReadOnlyList<TermSessionRecord> Enumerate() => sessions;
        public TermSessionRecord? Get(string id) => sessions.FirstOrDefault(s => s.Id == id);
    }

    /// <summary>Hands the gateway one end of an in-memory duplex pipe per connect, and keeps the host's end for the test.</summary>
    private sealed class MemoryPipes : IPipeConnector
    {
        private readonly TaskCompletionSource<Stream> host = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Connected;

        public Stream Host => host.Task.Wait(TimeSpan.FromSeconds(10)) ? host.Task.Result : throw new TimeoutException("the gateway never opened the pipe");

        public Task<Stream> ConnectAsync(TermSessionRecord session, CancellationToken ct)
        {
            Interlocked.Increment(ref Connected);
            var (gatewayEnd, hostEnd) = Duplex.Pair();
            host.TrySetResult(hostEnd);
            return Task.FromResult(gatewayEnd);
        }
    }

    /// <summary>One end of an in-memory duplex stream: reads what the other end writes. Disposing it ends the other end's reads.</summary>
    private sealed class Duplex(PipeReader reader, PipeWriter writer) : Stream
    {
        private readonly Stream input = reader.AsStream(), output = writer.AsStream();

        public static (Stream, Stream) Pair()
        {
            var a = new Pipe();
            var b = new Pipe();
            return (new Duplex(a.Reader, b.Writer), new Duplex(b.Reader, a.Writer));
        }

        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => input.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => input.ReadAsync(buffer, ct);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => input.ReadAsync(buffer, offset, count, ct);
        public override void Write(byte[] buffer, int offset, int count) => output.Write(buffer, offset, count);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) => output.WriteAsync(buffer, ct);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct) => output.WriteAsync(buffer, offset, count, ct);
        public override void Flush() => output.Flush();
        public override Task FlushAsync(CancellationToken ct) => output.FlushAsync(ct);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                writer.Complete();
                reader.Complete();
            }
            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }
    }
}
