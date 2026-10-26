using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Text;
using MindAttic.Orchestrator.Gateway;
using MindAttic.Orchestrator.Term;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// Broadcast secret hygiene (M.ORCH-T935): the masker on remote streams (token shapes, Vault literals, tokens split across
/// chunks, inside SGR colour runs, PEM blocks, escape sequences left intact, prose and code left alone), the owner's
/// broadcast settings, and the bridge's pause, resume, fence and unshare behaviour.
/// <para>Every token here is a fake built at run time from a fixed seed, so no secret-shaped literal sits in the source.</para>
/// </summary>
public sealed class BroadcastMaskingTests
{
    private const string Esc = "\u001b";

    /// <summary>A fake token: <paramref name="prefix"/> and <paramref name="length"/> mixed-case letters and digits.</summary>
    private static string Fake(string prefix, int length, int seed = 7, string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789")
    {
        var random = new Random(seed);
        var body = new char[length];
        for (var i = 0; i < length; i++) body[i] = alphabet[random.Next(alphabet.Length)];
        // Make sure of a digit, an upper and a lower case letter, whatever the seed (those the alphabet has).
        var at = 0;
        foreach (var c in "7Qk")
            if (at < length && alphabet.Contains(c)) body[at++] = c;
        return prefix + new string(body);
    }

    private static string Bullets(int n) => new('•', n);

    private static string Mask(string text, SecretMasker? secrets = null) => secrets is null
        ? Encoding.UTF8.GetString(new StreamMasker(SecretMasker.ShapesOnly).Process(Encoding.UTF8.GetBytes(text), final: true))
        : Encoding.UTF8.GetString(new StreamMasker(secrets).Process(Encoding.UTF8.GetBytes(text), final: true));

    /// <summary>Feeds <paramref name="chunks"/> one at a time, then flushes, as the bridge does.</summary>
    private static string Stream(IEnumerable<string> chunks, SecretMasker? secrets = null)
    {
        var masker = new StreamMasker(secrets ?? SecretMasker.ShapesOnly);
        var output = new List<byte>();
        foreach (var chunk in chunks) output.AddRange(masker.Process(Encoding.UTF8.GetBytes(chunk)));
        output.AddRange(masker.Flush());
        return Encoding.UTF8.GetString(output.ToArray());
    }

    public static IEnumerable<string> Tokens()
    {
        yield return Fake("sk-ant-api03-", 40);
        yield return Fake("sk-ant-oat01-", 60);
        yield return Fake("sk-proj-", 48);
        yield return Fake("sk-", 48, seed: 3);
        yield return Fake("ghp_", 36);
        yield return Fake("gho_", 36, seed: 11);
        yield return Fake("github_pat_", 82, alphabet: "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789_");
        yield return Fake("AKIA", 16, alphabet: "ABCDEFGHJKLMNPQRSTUVWXYZ234567");
        yield return Fake("eyJ", 20) + "." + Fake("eyJ", 30, seed: 5) + "." + Fake("", 43, seed: 9);
        yield return Fake("xoxb-", 40, alphabet: "abcdefghijkmnopqrstuvwxyz0123456789-");
    }

    // ── shapes ──────────────────────────────────────────────────────────────────

    [TestCaseSource(nameof(Tokens))]
    public void Each_token_shape_becomes_one_bullet_per_character_and_nothing_else_changes(string token)
    {
        var masked = Mask($"export X={token}\r\nnext line\r\n");
        Assert.That(masked, Is.EqualTo($"export X={Bullets(token.Length)}\r\nnext line\r\n"));
    }

    [Test]
    public void A_long_value_after_a_secret_sounding_name_is_masked_but_the_name_is_kept()
    {
        var value = Fake("", 40);
        Assert.That(Mask($"ANTHROPIC_API_KEY={value}"), Is.EqualTo($"ANTHROPIC_API_KEY={Bullets(40)}"));
        Assert.That(Mask($"\"client_secret\": \"{value}\","), Is.EqualTo($"\"client_secret\": \"{Bullets(40)}\","));
        Assert.That(Mask($"password = '{value}'"), Is.EqualTo($"password = '{Bullets(40)}'"));
        var hex = Fake("", 40, alphabet: "0123456789abcdef");
        Assert.That(Mask($"token={hex}"), Is.EqualTo($"token={Bullets(40)}"));
        Assert.That(Mask($"Authorization: Bearer {value}"), Is.EqualTo($"Authorization: Bearer {Bullets(40)}"));
    }

    [Test]
    public void A_vault_value_is_masked_as_a_literal_wherever_it_shows_up()
    {
        var vaultValue = "Fixture&Pass-" + Fake("", 10, seed: 21);
        var secrets = new SecretMasker([vaultValue, "short"]);
        Assert.That(secrets.LiteralCount, Is.EqualTo(1), "a value under 8 characters is ignored: it would hit ordinary words");
        Assert.That(Mask($"login with {vaultValue} now", secrets), Is.EqualTo($"login with {Bullets(vaultValue.Length)} now"));
        Assert.That(Mask("a short word", secrets), Is.EqualTo("a short word"));
    }

    [Test]
    public void A_pem_private_key_body_is_masked_through_its_end_line_and_its_begin_line_kept()
    {
        var body = Fake("", 64, seed: 1, alphabet: "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/");
        var pem = $"-----BEGIN RSA PRIVATE KEY-----\r\n{body}\r\n{body}\r\n-----END RSA PRIVATE KEY-----\r\nafter";
        var end = "-----END RSA PRIVATE KEY-----";
        var expected = $"-----BEGIN RSA PRIVATE KEY-----\r\n{Bullets(64)}\r\n{Bullets(64)}\r\n{Bullets(end.Length)}\r\nafter";
        Assert.That(Mask(pem), Is.EqualTo(expected));
        // Line by line, as a CLI prints it.
        Assert.That(Stream(pem.Split("\r\n").Select((line, i) => i == 0 ? line : "\r\n" + line)), Is.EqualTo(expected));
    }

    // ── chunk boundaries ────────────────────────────────────────────────────────

    [TestCaseSource(nameof(Tokens))]
    public void A_token_split_across_two_chunks_at_any_point_is_still_masked(string token)
    {
        var text = $"echo {token}\r\n$ ";
        var expected = $"echo {Bullets(token.Length)}\r\n$ ";
        var bytes = Encoding.UTF8.GetBytes(text);
        for (var cut = 1; cut < bytes.Length; cut++)
        {
            var masker = new StreamMasker(SecretMasker.ShapesOnly);
            var output = new List<byte>();
            output.AddRange(masker.Process(bytes.AsSpan(0, cut)));
            output.AddRange(masker.Process(bytes.AsSpan(cut)));
            output.AddRange(masker.Flush());
            Assert.That(Encoding.UTF8.GetString(output.ToArray()), Is.EqualTo(expected), $"split at byte {cut}");
        }
    }

    [Test]
    public void A_token_arriving_a_character_at_a_time_is_masked()
    {
        var token = Fake("sk-ant-api03-", 40);
        Assert.That(Stream(token.Select(c => c.ToString()).Append("\r\n")), Is.EqualTo(Bullets(token.Length) + "\r\n"));
    }

    [Test]
    public void A_prefix_sent_in_an_earlier_chunk_still_counts()
    {
        var value = Fake("", 40);
        Assert.That(Stream(["API_KEY=", "\"", value, "\"\r\n"]), Is.EqualTo($"API_KEY=\"{Bullets(40)}\"\r\n"));
    }

    [Test]
    public void A_trailing_run_is_held_until_the_next_chunk_or_a_flush_and_a_line_end_goes_at_once()
    {
        var masker = new StreamMasker(SecretMasker.ShapesOnly);
        Assert.That(masker.Process("hello wor"u8), Is.EqualTo("hello "u8.ToArray()));
        Assert.That(masker.HasPending, Is.True);
        Assert.That(masker.Process("ld\r\n"u8), Is.EqualTo("world\r\n"u8.ToArray()));
        Assert.That(masker.HasPending, Is.False);
        Assert.That(masker.Process("abc"u8), Is.Empty);
        Assert.That(masker.Flush(), Is.EqualTo("abc"u8.ToArray()));
    }

    [Test]
    public void An_unfinished_escape_or_utf8_character_is_held_whole()
    {
        var masker = new StreamMasker(SecretMasker.ShapesOnly);
        Assert.That(masker.Process(Encoding.UTF8.GetBytes($"ok {Esc}[3")), Is.EqualTo("ok "u8.ToArray()));
        Assert.That(Encoding.UTF8.GetString(masker.Process(Encoding.UTF8.GetBytes("1m!"))), Is.EqualTo($"{Esc}[31m!"));
        var euro = Encoding.UTF8.GetBytes("€!");
        Assert.That(masker.Process(euro.AsSpan(0, 1)), Is.Empty);
        Assert.That(masker.Process(euro.AsSpan(1)), Is.EqualTo(euro));
    }

    // ── escape sequences ────────────────────────────────────────────────────────

    [Test]
    public void A_token_inside_an_sgr_colour_run_is_masked_and_every_escape_is_kept_byte_for_byte()
    {
        var token = Fake("ghp_", 36);
        var text = $"{Esc}[1;32m{token[..10]}{Esc}[38;2;255;0;0m{token[10..]}{Esc}[0m done";
        var expected = $"{Esc}[1;32m{Bullets(10)}{Esc}[38;2;255;0;0m{Bullets(token.Length - 10)}{Esc}[0m done";
        Assert.That(Mask(text), Is.EqualTo(expected));
        // Split inside the escape sequence in the middle of the token, too.
        var cut = text.IndexOf("38;2", StringComparison.Ordinal);
        Assert.That(Stream([text[..cut], text[cut..]]), Is.EqualTo(expected));
    }

    [Test]
    public void Escape_sequences_and_controls_pass_through_untouched()
    {
        string[] samples =
        [
            $"{Esc}[?1049h{Esc}[H{Esc}[2J{Esc}[?25l",
            $"{Esc}]0;a window title{'\u0007'}",
            $"{Esc}]8;;https://example.com/docs{Esc}\\link{Esc}]8;;{Esc}\\",
            $"{Esc}(B{Esc}7{Esc}8{Esc}M",
            "tab\there\bback\r\n",
            $"{Esc}P1$r0m{Esc}\\",
        ];
        foreach (var sample in samples) Assert.That(Mask(sample), Is.EqualTo(sample), sample.Replace(Esc, "ESC"));
    }

    [Test]
    public void A_token_inside_an_escape_sequence_is_never_rewritten()
    {
        // An OSC title is not screen text: changing its bytes would corrupt the sequence, so it goes as it is (titles reach
        // remote viewers masked through the Title frame instead).
        var token = Fake("sk-ant-api03-", 40);
        var osc = $"{Esc}]0;{token}\u0007";
        Assert.That(Mask(osc), Is.EqualTo(osc));
        Assert.That(SecretMasker.ShapesOnly.MaskText($"title {token}"), Is.EqualTo($"title {Bullets(token.Length)}"));
    }

    // ── false positives ─────────────────────────────────────────────────────────

    [TestCase("The quick brown fox jumps over the lazy dog, then asks for the task-list and a desk-lamp.")]
    [TestCase("Run pip install scikit-learn and sk-learn-contrib-imbalanced before the notebook.")]
    [TestCase("commit 1c95035f2a3b4c5d6e7f8091a2b3c4d5e6f70812 (HEAD -> main)")]
    [TestCase("session 0f8fad5b-d9cb-469f-a165-70867728950e started")]
    [TestCase("var token = await GetTokenAsync(cancellationToken);")]
    [TestCase("private const string ApiKeyName = \"ANTHROPIC_API_KEY\";")]
    [TestCase("cacheKey = \"user-profile-settings-v2-cache\";")]
    [TestCase("password: hunter2")]
    [TestCase("key=ThisIsJustAVeryLongIdentifierName")]
    [TestCase("See https://github.com/mindattic/MindAttic.Orchestrator/blob/main/docs/app-bus.md#term-watchers for details.")]
    [TestCase("eyes on the prize; xoxo from the team; skip-ahead; AKIAS is not an id")]
    [TestCase("  at MindAttic.Orchestrator.Gateway.TermBridge.RunAsync(WebSocket socket, Stream pipe) in TermBridge.cs:line 42")]
    public void Prose_code_hashes_and_ids_go_through_unchanged(string text)
    {
        Assert.That(Mask(text + "\r\n"), Is.EqualTo(text + "\r\n"));
        Assert.That(Stream(text.Chunk(7).Select(c => new string(c)).Append("\r\n")), Is.EqualTo(text + "\r\n"));
    }

    [Test]
    public void Non_ascii_text_goes_through_unchanged()
    {
        const string text = "Café — 日本語 \U0001F600 • done\r\n";
        Assert.That(Mask(text), Is.EqualTo(text));
    }

    [Test]
    public void Looks_random_tells_generated_values_from_written_ones()
    {
        Assert.That(SecretMasker.LooksRandom(Fake("", 32)), Is.True);
        Assert.That(SecretMasker.LooksRandom(Fake("", 40, alphabet: "0123456789abcdef")), Is.True);
        Assert.That(SecretMasker.LooksRandom("correcthorsebatterystaple"), Is.False);
        Assert.That(SecretMasker.LooksRandom("user-profile-settings-v2-cache"), Is.False);
        Assert.That(SecretMasker.LooksRandom("short1A"), Is.False);
    }

    // ── the owner's settings ────────────────────────────────────────────────────

    [Test]
    public void Nothing_is_shared_by_default_and_masking_is_on()
    {
        var s = new TermBroadcastSettings();
        Assert.That(s.IsShared("abc"), Is.False);
        Assert.That(s.IsMasked("abc"), Is.True);
        Assert.That(s.IsPaused("abc"), Is.False);
        Assert.That((s with { ShareAll = true }).IsShared("abc"), Is.True);
        Assert.That((s with { ShareAll = true }).Share("abc", false).IsShared("abc"), Is.False, "a session's own choice wins over share-all");
        Assert.That(s.Mask("ABC", false).IsMasked("abc"), Is.False, "ids match in any case");
    }

    [Test]
    public void Pausing_fences_history_and_resuming_all_resumes_each_session()
    {
        var started = new DateTime(2026, 10, 26, 10, 0, 0, DateTimeKind.Utc);
        var s = new TermBroadcastSettings().Share("a", true);
        Assert.That(s.HistoryFenced("a", started), Is.False);
        s = s.Pause("a", true, started.AddMinutes(5));
        Assert.That(s.IsPaused("a"), Is.True);
        s = s.Pause("a", false, started.AddMinutes(6));
        Assert.That(s.IsPaused("a"), Is.False);
        Assert.That(s.HistoryFenced("a", started), Is.True, "once paused, its history may hold what the pause hid");

        var t = new TermBroadcastSettings().Pause("b", true, started).PauseEvery(true, started.AddMinutes(1));
        Assert.That(t.IsPaused("c"), Is.True, "pause all covers every session");
        t = t.PauseEvery(false, started.AddMinutes(2));
        Assert.That(t.IsPaused("b") || t.IsPaused("c"), Is.False);
        Assert.That(t.HistoryFenced("c", started.AddMinutes(1).AddSeconds(30)), Is.True, "started during the pause");
        Assert.That(t.HistoryFenced("c", started.AddMinutes(3)), Is.False, "started after it");
    }

    [Test]
    public void The_settings_file_round_trips_and_a_bad_one_reads_as_the_defaults()
    {
        var dir = Directory.CreateTempSubdirectory("broadcast-tests-").FullName;
        try
        {
            Assert.That(TermBroadcast.Read(dir).ShareAll, Is.False);
            var written = TermBroadcast.Update(s => s.Share("Abc", true).Mask("abc", false), dir);
            Assert.That(written, Is.Not.Null);
            var read = TermBroadcast.Read(dir);
            Assert.That(read.IsShared("abc") && !read.IsMasked("ABC"), Is.True);
            Assert.That(TermBroadcast.Update(s => s.Prune(["other"]), dir)!.Sessions, Is.Empty);
            File.WriteAllText(TermBroadcast.PathIn(dir), "{ not json");
            Assert.That(TermBroadcast.Read(dir).IsShared("abc"), Is.False);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Test]
    public void A_term_watchers_envelope_parses_back()
    {
        var message = new TermWatchersEvent
        {
            Event = TermWatchersEvents.Attached,
            Watcher = new TermWatcher { Connection = "1-1", SessionId = "s1", Viewer = "owner@example.com", Mode = "watch", From = "100.64.0.2" },
            Watchers = [new TermWatcher { Connection = "1-1", SessionId = "s1", Viewer = "owner@example.com", Mode = "watch" }],
            GatewayPid = 42,
        };
        var parsed = TermWatchersBus.Parse(TermWatchersBus.Envelope(message, DateTime.UtcNow, 42));
        Assert.That(parsed, Is.Not.Null);
        Assert.That(parsed!.Event, Is.EqualTo(TermWatchersEvents.Attached));
        Assert.That(parsed.Watchers.Single().Viewer, Is.EqualTo("owner@example.com"));
        Assert.That(TermBus.Parse(TermWatchersBus.Envelope(message, DateTime.UtcNow, 42)), Is.Null, "not a term-session line");
    }

    // ── the bridge: pause, resume, fence, unshare ───────────────────────────────

    [Test]
    public async Task Pause_sends_a_paused_frame_withholds_output_and_input_and_resume_clears_and_goes_on()
    {
        var control = new FixedBroadcast(new TermBroadcastSettings { ShareAll = true });
        await using var bridge = await Bridge.StartAsync(control, GatewayAccess.Interact);
        await bridge.HandshakeAsync();

        await bridge.HostSendAsync(TermFrameKind.Output, "before\r\n");
        Assert.That(await bridge.ReceiveOutputUntilAsync("before"), Does.Contain("before"));

        control.Current = control.Current.Pause(Bridge.SessionId, true, DateTime.UtcNow);
        var paused = await bridge.ReceiveAsync(f => f.Kind == TermFrameKind.Paused);
        Assert.That(paused.Payload, Is.EqualTo(new byte[] { 1 }));

        await bridge.HostSendAsync(TermFrameKind.Output, "shown while paused\r\n");
        await bridge.HostSendAsync(TermFrameKind.Title, "paused title");
        await bridge.ViewerSendAsync(TermFrameKind.Input, "typed while paused");
        await Task.Delay(300);

        control.Current = control.Current.Pause(Bridge.SessionId, false, DateTime.UtcNow);
        var seen = new List<(TermFrameKind Kind, byte[] Payload)>();
        var resumed = await bridge.ReceiveAsync(f => f.Kind == TermFrameKind.Paused, seen);
        Assert.That(resumed.Payload, Is.EqualTo(new byte[] { 0 }));
        var clear = await bridge.ReceiveAsync(f => true);
        Assert.That(clear.Kind, Is.EqualTo(TermFrameKind.Output));
        Assert.That(clear.Payload, Is.EqualTo(TermBridge.ResumeClear));

        await bridge.ViewerSendAsync(TermFrameKind.Input, "typed after");
        var input = await TermProtocol.ReadFrameAsync(bridge.Host, Timeout());
        Assert.That(Encoding.UTF8.GetString(input.Payload), Is.EqualTo("typed after"), "the input typed while paused never reached the host");

        await bridge.HostSendAsync(TermFrameKind.Output, "after\r\n");
        var after = await bridge.ReceiveOutputUntilAsync("after", seen);
        Assert.That(after, Does.Contain("after"));
        Assert.That(seen.Any(f => Encoding.UTF8.GetString(f.Payload).Contains("paused", StringComparison.Ordinal) && f.Kind != TermFrameKind.Title), Is.False,
            "nothing the host wrote during the pause reached the viewer");
    }

    [Test]
    public async Task A_session_paused_at_attach_gets_a_paused_frame_and_an_empty_replay()
    {
        var control = new FixedBroadcast(new TermBroadcastSettings { ShareAll = true }.Pause(Bridge.SessionId, true, DateTime.UtcNow));
        await using var bridge = await Bridge.StartAsync(control, GatewayAccess.Watch);
        await bridge.ViewerSendAsync(TermFrameKind.Hello, TermProtocol.Hello(TermProtocol.Version, 100, 25));
        await TermProtocol.ReadFrameAsync(bridge.Host, Timeout());
        await bridge.HostSendFrameAsync(TermProtocol.Frame(TermFrameKind.Hello, TermProtocol.Hello(TermProtocol.Version, 120, 30)));
        await bridge.HostSendFrameAsync(TermProtocol.Frame(TermFrameKind.Replay, TermProtocol.Replay(120, 30, TermModes.None, "old screen"u8, "\e[?25h"u8)));

        Assert.That((await bridge.ReceiveAsync(_ => true)).Kind, Is.EqualTo(TermFrameKind.Hello));
        Assert.That((await bridge.ReceiveAsync(_ => true)).Kind, Is.EqualTo(TermFrameKind.Paused));
        var replay = await bridge.ReceiveAsync(_ => true);
        Assert.That(replay.Kind, Is.EqualTo(TermFrameKind.Replay));
        var r = TermProtocol.ReadReplay(replay.Payload)!;
        Assert.That(r.Data, Is.Empty);
        Assert.That(r.Reassert, Is.Empty);
        Assert.That((r.Cols, r.Rows), Is.EqualTo(((ushort)120, (ushort)30)), "the size still goes");
    }

    [Test]
    public async Task A_session_paused_before_gets_no_history_on_attach_and_its_replay_is_otherwise_masked()
    {
        var token = Fake("sk-ant-api03-", 40);
        var control = new FixedBroadcast(new TermBroadcastSettings { ShareAll = true });
        await using (var bridge = await Bridge.StartAsync(control, GatewayAccess.Watch))
        {
            await bridge.HandshakeAsync($"key {token}\r\n");
            var replay = TermProtocol.ReadReplay(bridge.Replay!)!;
            Assert.That(Encoding.UTF8.GetString(replay.Data), Is.EqualTo($"key {Bullets(token.Length)}\r\n"));
        }

        control.Current = control.Current.Pause(Bridge.SessionId, true, DateTime.UtcNow).Pause(Bridge.SessionId, false, DateTime.UtcNow);
        await using (var bridge = await Bridge.StartAsync(control, GatewayAccess.Watch))
        {
            await bridge.HandshakeAsync("what the pause hid\r\n");
            var replay = TermProtocol.ReadReplay(bridge.Replay!)!;
            Assert.That(replay.Data, Is.EqualTo(TermBridge.FencedNotice));
        }
    }

    [Test]
    public async Task Output_is_masked_on_the_way_out_unless_the_owner_turned_masking_off()
    {
        var token = Fake("ghp_", 36);
        var control = new FixedBroadcast(new TermBroadcastSettings { ShareAll = true });
        await using var bridge = await Bridge.StartAsync(control, GatewayAccess.Watch);
        await bridge.HandshakeAsync();

        await bridge.HostSendAsync(TermFrameKind.Output, "token ");
        // Two frames in one write, as a CLI's quick output reaches the pipe: the token is split between them.
        await bridge.HostSendFrameAsync([.. TermProtocol.Frame(TermFrameKind.Output, Encoding.UTF8.GetBytes(token[..12])),
            .. TermProtocol.Frame(TermFrameKind.Output, Encoding.UTF8.GetBytes(token[12..] + "\r\n"))]);
        var masked = await bridge.ReceiveOutputUntilAsync("\r\n");
        Assert.That(masked, Is.EqualTo($"token {Bullets(token.Length)}\r\n"));

        control.Current = control.Current.Mask(Bridge.SessionId, false);
        await bridge.HostSendAsync(TermFrameKind.Output, $"again {token}\r\n");
        Assert.That(await bridge.ReceiveOutputUntilAsync("\r\n"), Is.EqualTo($"again {token}\r\n"));
    }

    [Test]
    public async Task Held_output_goes_out_after_a_short_quiet_spell()
    {
        var control = new FixedBroadcast(new TermBroadcastSettings { ShareAll = true });
        await using var bridge = await Bridge.StartAsync(control, GatewayAccess.Watch);
        await bridge.HandshakeAsync();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        await bridge.HostSendAsync(TermFrameKind.Output, "prompt> abc");
        Assert.That(await bridge.ReceiveOutputUntilAsync("abc"), Is.EqualTo("prompt> abc"));
        Assert.That(clock.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)));
    }

    [Test]
    public async Task Unsharing_a_session_closes_its_remote_stream()
    {
        var control = new FixedBroadcast(new TermBroadcastSettings { ShareAll = true });
        await using var bridge = await Bridge.StartAsync(control, GatewayAccess.Watch);
        await bridge.HandshakeAsync();
        control.Current = control.Current.Share(Bridge.SessionId, false);
        var buffer = new byte[1024];
        WebSocketReceiveResult result;
        do result = await bridge.Viewer.ReceiveAsync(buffer, Timeout());
        while (result.MessageType != WebSocketMessageType.Close);
        Assert.That(bridge.Viewer.CloseStatusDescription, Does.Contain("stopped sharing"));
        var ended = await bridge.Result;
        Assert.That(ended.EndedBy, Is.EqualTo("owner"));
    }

    private static CancellationToken Timeout() => new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token;

    /// <summary>One <see cref="TermBridge"/> between a viewer WebSocket and an in-memory host pipe, both ends in the test's hands.</summary>
    private sealed class Bridge : IAsyncDisposable
    {
        public const string SessionId = "s1";
        private readonly CancellationTokenSource stop = new();
        public required WebSocket Viewer { get; init; }
        public required Stream Host { get; init; }
        public required Task<BridgeResult> Result { get; init; }
        public byte[]? Replay { get; private set; }

        public static Task<Bridge> StartAsync(IBroadcastControl control, GatewayAccess access)
        {
            var (gatewayPipe, hostPipe) = Duplex.Pair();
            var (serverStream, clientStream) = Duplex.Pair();
            var server = WebSocket.CreateFromStream(serverStream, new WebSocketCreationOptions { IsServer = true, KeepAliveInterval = TimeSpan.Zero });
            var client = WebSocket.CreateFromStream(clientStream, new WebSocketCreationOptions { IsServer = false, KeepAliveInterval = TimeSpan.Zero });
            var stream = new BroadcastStream(SessionId, DateTime.UtcNow.AddMinutes(-1), control, SecretMasker.ShapesOnly);
            var result = TermBridge.RunAsync(server, gatewayPipe, access, new GatewayLog(null), CancellationToken.None, broadcast: stream);
            return Task.FromResult(new Bridge { Viewer = client, Host = hostPipe, Result = result });
        }

        /// <summary>Hello both ways, a Replay of <paramref name="screen"/> and Live; the replay as the viewer got it is kept.</summary>
        public async Task HandshakeAsync(string screen = "")
        {
            await ViewerSendAsync(TermFrameKind.Hello, TermProtocol.Hello(TermProtocol.Version, 100, 25));
            await TermProtocol.ReadFrameAsync(Host, Timeout());
            await HostSendFrameAsync(TermProtocol.Frame(TermFrameKind.Hello, TermProtocol.Hello(TermProtocol.Version, 120, 30)));
            await HostSendFrameAsync(TermProtocol.Frame(TermFrameKind.Replay, TermProtocol.Replay(120, 30, TermModes.None, Encoding.UTF8.GetBytes(screen), [])));
            await HostSendFrameAsync(TermProtocol.Frame(TermFrameKind.Live, []));
            Assert.That((await ReceiveAsync(_ => true)).Kind, Is.EqualTo(TermFrameKind.Hello));
            var replay = await ReceiveAsync(_ => true);
            Assert.That(replay.Kind, Is.EqualTo(TermFrameKind.Replay));
            Replay = replay.Payload;
            Assert.That((await ReceiveAsync(_ => true)).Kind, Is.EqualTo(TermFrameKind.Live));
        }

        public Task HostSendAsync(TermFrameKind kind, string text) => HostSendFrameAsync(TermProtocol.Frame(kind, Encoding.UTF8.GetBytes(text)));

        public async Task HostSendFrameAsync(byte[] frame)
        {
            await Host.WriteAsync(frame);
            await Host.FlushAsync();
        }

        public Task ViewerSendAsync(TermFrameKind kind, string text) => ViewerSendAsync(kind, Encoding.UTF8.GetBytes(text));

        public Task ViewerSendAsync(TermFrameKind kind, byte[] payload) =>
            Viewer.SendAsync(TermProtocol.Frame(kind, payload), WebSocketMessageType.Binary, true, Timeout());

        /// <summary>Frames until one matches; every frame read (the match too) goes into <paramref name="seen"/>.</summary>
        public async Task<(TermFrameKind Kind, byte[] Payload)> ReceiveAsync(Func<(TermFrameKind Kind, byte[] Payload), bool> match,
            List<(TermFrameKind Kind, byte[] Payload)>? seen = null)
        {
            var buffer = new byte[64 * 1024];
            while (true)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await Viewer.ReceiveAsync(buffer, Timeout());
                    if (result.MessageType == WebSocketMessageType.Close) throw new EndOfStreamException("closed: " + Viewer.CloseStatusDescription);
                    message.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);
                var frame = message.ToArray();
                var item = ((TermFrameKind)frame[0], frame[5..]);
                seen?.Add(item);
                if (match(item)) return item;
            }
        }

        /// <summary>The text of the Output frames read until it contains <paramref name="marker"/>.</summary>
        public async Task<string> ReceiveOutputUntilAsync(string marker, List<(TermFrameKind Kind, byte[] Payload)>? seen = null)
        {
            var text = new StringBuilder();
            while (!text.ToString().Contains(marker, StringComparison.Ordinal))
            {
                var frame = await ReceiveAsync(f => f.Kind == TermFrameKind.Output, seen);
                text.Append(Encoding.UTF8.GetString(frame.Payload));
            }
            return text.ToString();
        }

        public async ValueTask DisposeAsync()
        {
            Viewer.Abort();
            await Host.DisposeAsync();
            try { await Result.WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception) { }
            stop.Dispose();
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
