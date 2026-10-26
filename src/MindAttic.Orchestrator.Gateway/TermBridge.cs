using System.Buffers.Binary;
using System.Net.WebSockets;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Gateway;

/// <summary>How a bridge ended, for the log: counts only, never content.</summary>
internal sealed record BridgeResult(string EndedBy, long FramesUp, long FramesDown, long BytesDown, long Dropped, long Withheld = 0);

/// <summary>
/// Bridges one remote viewer's WebSocket to one session's local pipe (M.ORCH-T933), frame for frame: each binary WebSocket
/// message carries exactly one TermHost frame (<c>u8 kind | u32 length | payload</c>, <see cref="TermProtocol"/>), in both
/// directions, so a remote viewer runs the same RelayConnection logic over a WebSocket stream as a local one does over the pipe.
/// <para>Viewer to host, only the viewer's kinds go through, and the first must be Hello. A read-only (<see cref="GatewayAccess.Watch"/>)
/// viewer's Hello is rewritten to size 0x0, which the host ignores, so it never takes the session's size, and from protocol 2
/// (M.ORCH-T930) to a read-only Hello, so the host drops its input itself; its Input, Resize and TakeSize frames are dropped.
/// Host to viewer, every frame goes through unchanged (Hello, Replay, Live, Output, Modes, Size with its owner byte, Title,
/// Typing, Exit).</para>
/// <para>With a <see cref="BroadcastStream"/> (M.ORCH-T935) it applies the owner's choices on the way out: Output, Replay data
/// and Title are masked (<see cref="StreamMasker"/>) unless the owner turned masking off for the session; while the session is
/// paused the viewer gets a Paused frame and no Output, Replay data or Title, and its Input is dropped, until it resumes (a
/// Paused 0 frame, then a cleared screen, since what the pause hid never follows); a session paused before gets no history on
/// attach; and a session the owner stops sharing closes the stream.</para>
/// <para>Nothing here throws: either side ending, or a malformed frame, closes both.</para>
/// </summary>
internal static class TermBridge
{
    /// <summary>The largest message a remote viewer may send: a 64 KB paste chunk with room to spare, far under the host's limit.</summary>
    public const int MaxInbound = 1024 * 1024 + 5;

    private static readonly TimeSpan CloseWait = TimeSpan.FromSeconds(2);

    /// <summary>Held output goes out after this much quiet, so masking never delays ordinary text longer (a candidate secret is
    /// held up to <see cref="StreamMasker.MaxCandidateHold"/>, M.ORCH-T982).</summary>
    public static readonly TimeSpan FlushAfter = StreamMasker.FlushAfter;

    /// <summary>How often a quiet stream looks at the owner's choices (pause, share).</summary>
    public static readonly TimeSpan PolicyTick = TimeSpan.FromMilliseconds(100);

    /// <summary>What a remote viewer's screen gets on resume: cleared, scrollback too, cursor home.</summary>
    internal static readonly byte[] ResumeClear = "\e[0m\e[H\e[2J\e[3J"u8.ToArray();

    /// <summary>The history of a session paused before, in place of its replay.</summary>
    internal static readonly byte[] FencedNotice = "\e[0m\e[2m[history hidden: this session's broadcast was paused before; output shows from here]\e[0m\r\n"u8.ToArray();

    /// <param name="input">Counts each Input frame that goes on to the host, for the audit log (M.ORCH-T934): counts only.</param>
    /// <param name="revoke">Fired when the viewer's access was revoked (M.ORCH-T934): the pipe is let go, and the viewer is told
    /// so with a policy-violation close, as when the host closes it (cancelling a receive would abort the socket unannounced).</param>
    /// <param name="broadcast">The owner's choices for this remote stream (M.ORCH-T935): masking, pause, sharing.</param>
    /// <param name="refresh">Handed each RefreshToken frame's token (M.ORCH-T1041); such a frame never reaches the host. Without
    /// it they are dropped.</param>
    /// <param name="revokeReason">Why <paramref name="revoke"/> fired, as the close and the result say it ("access revoked", or
    /// "token expired"); "access revoked" without it.</param>
    public static async Task<BridgeResult> RunAsync(WebSocket socket, Stream pipe, GatewayAccess access, GatewayLog log, CancellationToken ct,
        GatewayInputMeter? input = null, CancellationToken revoke = default, BroadcastStream? broadcast = null,
        Action<string>? refresh = null, Func<string>? revokeReason = null)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        // The downstream alone is also cancelled on a revoke (M.ORCH-T1060): letting go of the pipe doesn't end a read already
        // pending on every stream (BroadcastAsync keeps one across its ticks), so the bridge never noticed and the stream stayed
        // open. The upstream's receive isn't cancelled: that would abort the socket rather than close it with the reason.
        using var downCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
        var counts = new Counts();
        var revoked = 0;
        string RevokedWhy()
        {
            try { return revokeReason?.Invoke() is { Length: > 0 } why ? why : "access revoked"; }
            catch (Exception) { return "access revoked"; }
        }
        using var onRevoke = revoke.Register(() =>
        {
            // Marked first, so the downstream ending on the cancel below is taken for the revoke.
            Interlocked.Exchange(ref revoked, 1);
            TryDispose(pipe);
            try { downCts.Cancel(); } catch (Exception) { }
        });
        var up = Task.Run(() => UpstreamAsync(socket, pipe, access, counts, log, input, broadcast, refresh, cts.Token));
        var down = Task.Run(() => DownstreamAsync(socket, pipe, counts, log, broadcast, downCts.Token));
        string endedBy;
        try
        {
            var first = await Task.WhenAny(up, down).ConfigureAwait(false);
            if (first == down)
            {
                var byRevoke = Volatile.Read(ref revoked) == 1;
                // The owner stopped sharing the session (M.ORCH-T935): null when the host closed the pipe.
                var unshared = await down.ConfigureAwait(false);
                endedBy = byRevoke ? RevokedWhy() : unshared is not null ? "owner" : "host";
                // The host closed the pipe (the session ended, or another viewer replaced this one), access was revoked (or its
                // token expired), or the owner stopped sharing: say so, and give the viewer a moment to answer the close.
                await TryCloseOutputAsync(socket, byRevoke ? WebSocketCloseStatus.PolicyViolation : WebSocketCloseStatus.NormalClosure,
                    byRevoke ? endedBy : unshared ?? "the session's host closed the stream").ConfigureAwait(false);
                await Task.WhenAny(up, Task.Delay(CloseWait, CancellationToken.None)).ConfigureAwait(false);
                cts.Cancel();
            }
            else
            {
                var close = await up.ConfigureAwait(false);
                if (Volatile.Read(ref revoked) == 1) close = new(WebSocketCloseStatus.PolicyViolation, RevokedWhy());
                endedBy = close.Reason;
                cts.Cancel();
                TryDispose(pipe);
                await Quietly(down).ConfigureAwait(false);
                await TryCloseOutputAsync(socket, close.Status, close.Reason).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            endedBy = "error";
            log.Error(ex, "the bridge failed; contained");
            cts.Cancel();
        }
        TryDispose(pipe);
        await Quietly(up).ConfigureAwait(false);
        await Quietly(down).ConfigureAwait(false);
        if (socket.State is not (WebSocketState.Closed or WebSocketState.Aborted)) socket.Abort();
        return new BridgeResult(endedBy, Interlocked.Read(ref counts.FramesUp), Interlocked.Read(ref counts.FramesDown),
            Interlocked.Read(ref counts.BytesDown), Interlocked.Read(ref counts.Dropped), Interlocked.Read(ref counts.Withheld));
    }

    private sealed class Counts
    {
        public long FramesUp, FramesDown, BytesDown, Dropped, Withheld;
    }

    private readonly record struct Close(WebSocketCloseStatus Status, string Reason);

    /// <summary>Viewer to host. Returns how the socket should be closed.</summary>
    private static async Task<Close> UpstreamAsync(WebSocket socket, Stream pipe, GatewayAccess access, Counts counts, GatewayLog log,
        GatewayInputMeter? input, BroadcastStream? broadcast, Action<string>? refresh, CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        var helloSeen = false;
        try
        {
            while (true)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer, ct).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close) return new(WebSocketCloseStatus.NormalClosure, "viewer");
                    if (result.MessageType != WebSocketMessageType.Binary) return new(WebSocketCloseStatus.InvalidMessageType, "frames are binary");
                    if (message.Length + result.Count > MaxInbound) return new(WebSocketCloseStatus.MessageTooBig, "frame too large");
                    message.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                var frame = message.ToArray();
                if (frame.Length < 5 || BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(1)) != frame.Length - 5)
                    return new(WebSocketCloseStatus.ProtocolError, "one whole frame per message");
                var kind = (TermFrameKind)frame[0];
                if (!helloSeen)
                {
                    if (kind != TermFrameKind.Hello || TermProtocol.ReadViewerHello(frame.AsSpan(5)) is not { } hello)
                        return new(WebSocketCloseStatus.ProtocolError, "open with Hello");
                    helloSeen = true;
                    // Read-only: the host skips a 0x0 size, so a watcher's Hello never resizes the session.
                    // From protocol 2 it is also marked read-only (M.ORCH-T930), keeping the viewer's id, cleaned for the host's
                    // log (M.ORCH-T990).
                    if (access < GatewayAccess.Interact)
                        frame = TermProtocol.Frame(TermFrameKind.Hello, TermProtocol.Hello(hello.Protocol, 0, 0, interactive: false, TermProtocol.LogSafeId(hello.ViewerId)));
                    else if (frame.Length > 12 && TermProtocol.LogSafeId(hello.ViewerId) is var clean && clean != hello.ViewerId)
                        // The size and flags as sent (bytes 0 to 6 of the payload), the id cleaned.
                        frame = TermProtocol.Frame(TermFrameKind.Hello, [.. frame.AsSpan(5, 7), .. System.Text.Encoding.UTF8.GetBytes(clean)]);
                }
                else if (kind == TermFrameKind.RefreshToken && refresh is not null)
                {
                    // For the gateway, never the host (M.ORCH-T1041): a fresh token extends the stream.
                    try { refresh(System.Text.Encoding.UTF8.GetString(frame.AsSpan(5))); }
                    catch (Exception ex) { log.Error(ex, "refreshing a stream's token failed; contained"); }
                    continue;
                }
                else if (!Forwards(kind, access)
                         // Paused (M.ORCH-T935): the viewer can't see what it would be typing into.
                         || (kind == TermFrameKind.Input && broadcast is not null && broadcast.Control.Current.IsPaused(broadcast.SessionId)))
                {
                    Interlocked.Increment(ref counts.Dropped);
                    continue;
                }
                await pipe.WriteAsync(frame, ct).ConfigureAwait(false);
                await pipe.FlushAsync(ct).ConfigureAwait(false);
                Interlocked.Increment(ref counts.FramesUp);
                if (kind == TermFrameKind.Input) input?.Add(frame.Length - 5);
            }
        }
        catch (OperationCanceledException) { return new(WebSocketCloseStatus.NormalClosure, "cancelled"); }
        catch (Exception ex) when (ex is WebSocketException or IOException or ObjectDisposedException) { return new(WebSocketCloseStatus.NormalClosure, "viewer gone"); }
        catch (Exception ex)
        {
            log.Error(ex, "reading the viewer failed; contained");
            return new(WebSocketCloseStatus.InternalServerError, "gateway error");
        }
    }

    /// <summary>Whether a frame after Hello goes on to the host: input, resizing and taking the size (M.ORCH-T930) only from an
    /// interactive viewer, and nothing that isn't a viewer's kind (a second Hello included).</summary>
    internal static bool Forwards(TermFrameKind kind, GatewayAccess access) =>
        kind is TermFrameKind.Input or TermFrameKind.Resize or TermFrameKind.TakeSize && access >= GatewayAccess.Interact;

    /// <summary>Host to viewer: each frame as one binary message; unchanged without a <paramref name="broadcast"/>, else as the
    /// owner's choices say. Returns why the gateway ended the stream (the owner stopped sharing it), or null when the host did.</summary>
    private static async Task<string?> DownstreamAsync(WebSocket socket, Stream pipe, Counts counts, GatewayLog log, BroadcastStream? broadcast,
        CancellationToken ct)
    {
        try
        {
            if (broadcast is not null) return await BroadcastAsync(socket, pipe, counts, broadcast, ct).ConfigureAwait(false);
            while (!ct.IsCancellationRequested)
            {
                var (kind, payload) = await TermProtocol.ReadFrameAsync(pipe, ct).ConfigureAwait(false);
                await SendAsync(socket, TermProtocol.Frame(kind, payload), counts, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException or ObjectDisposedException or OperationCanceledException
                                       or WebSocketException or InvalidDataException) { }
        catch (Exception ex) { log.Error(ex, "writing to the viewer failed; contained"); }
        return null;
    }

    /// <summary>The downstream with the owner's choices applied (masking, pause, sharing); see the class summary.</summary>
    private static async Task<string?> BroadcastAsync(WebSocket socket, Stream pipe, Counts counts, BroadcastStream b, CancellationToken ct)
    {
        var id = b.SessionId;
        var masker = new StreamMasker(b.Secrets);
        var helloSent = false;
        var paused = false;
        byte[]? title = null;
        Task<(TermFrameKind Kind, byte[] Payload)>? read = null;

        // Everything held, now, as the frame order needs: a held strong candidate secret goes masked, never bare (M.ORCH-T982),
        // a weak one as it is (M.ORCH-T1038).
        async Task FlushAsync()
        {
            if (!masker.HasPending) return;
            var rest = masker.Flush(maskCandidate: true);
            if (rest.Length > 0) await SendAsync(socket, TermProtocol.Frame(TermFrameKind.Output, rest), counts, ct).ConfigureAwait(false);
        }

        // What's due after a quiet spell: a candidate secret stays held until it's told apart or its hold runs out.
        async Task FlushDueAsync()
        {
            if (!masker.HasPending) return;
            var rest = masker.FlushDue();
            if (rest.Length > 0) await SendAsync(socket, TermProtocol.Frame(TermFrameKind.Output, rest), counts, ct).ConfigureAwait(false);
        }

        while (!ct.IsCancellationRequested)
        {
            if (read is null)
            {
                read = TermProtocol.ReadFrameAsync(pipe, ct);
                // A read still pending when the stream ends fails as the pipe closes: observed here, so it's never "unobserved".
                _ = read.ContinueWith(t => _ = t.Exception, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
            if (!read.IsCompleted)
            {
                var wait = masker.DueIn is { } due && due < PolicyTick ? due : PolicyTick;
                if (wait > TimeSpan.Zero) await Task.WhenAny(read, Task.Delay(wait, ct)).ConfigureAwait(false);
            }

            var settings = b.Control.Current;
            if (!settings.IsShared(id)) return "the owner stopped sharing this session";
            if (helloSent && settings.IsPaused(id) != paused)
            {
                paused = !paused;
                if (paused)
                {
                    // Nothing held back from before the pause goes out after it.
                    masker.Reset();
                    await SendAsync(socket, TermProtocol.Frame(TermFrameKind.Paused, [1]), counts, ct).ConfigureAwait(false);
                }
                else
                {
                    await SendAsync(socket, TermProtocol.Frame(TermFrameKind.Paused, [0]), counts, ct).ConfigureAwait(false);
                    await SendAsync(socket, TermProtocol.Frame(TermFrameKind.Output, ResumeClear), counts, ct).ConfigureAwait(false);
                    if (title is not null)
                        await SendAsync(socket, TermProtocol.Frame(TermFrameKind.Title, MaskTitle(title, b, settings)), counts, ct).ConfigureAwait(false);
                }
            }

            if (!read.IsCompleted)
            {
                if (masker.DueIn <= TimeSpan.Zero) await FlushDueAsync().ConfigureAwait(false);
                continue;
            }

            var (kind, payload) = await read.ConfigureAwait(false);
            read = null;
            var masked = settings.IsMasked(id);
            switch (kind)
            {
                case TermFrameKind.Output when paused:
                    Interlocked.Increment(ref counts.Withheld);
                    break;
                case TermFrameKind.Output when masked:
                {
                    // The masker times what it holds itself (DueIn).
                    var now = masker.Process(payload);
                    if (now.Length > 0) await SendAsync(socket, TermProtocol.Frame(TermFrameKind.Output, now), counts, ct).ConfigureAwait(false);
                    break;
                }
                case TermFrameKind.Output:
                    // Masking was just turned off: what it held goes first.
                    await FlushAsync().ConfigureAwait(false);
                    await SendAsync(socket, TermProtocol.Frame(kind, payload), counts, ct).ConfigureAwait(false);
                    break;
                case TermFrameKind.Replay when TermProtocol.ReadReplay(payload) is { } replay:
                {
                    masker.Reset();
                    byte[] data, reassert;
                    if (paused)
                    {
                        (data, reassert) = ([], []);
                        Interlocked.Increment(ref counts.Withheld);
                    }
                    else if (settings.HistoryFenced(id, b.StartedUtc))
                        (data, reassert) = (FencedNotice, masked ? masker.Process(replay.Reassert, final: true) : replay.Reassert);
                    // A token half-printed at attach is masked, never sent bare up to the replay's end (M.ORCH-T1039).
                    else if (masked) (data, reassert) = (masker.ProcessReplay(replay.Data), masker.Process(replay.Reassert, final: true));
                    else (data, reassert) = (replay.Data, replay.Reassert);
                    var frame = TermProtocol.Frame(kind, TermProtocol.Replay(replay.Cols, replay.Rows, replay.Modes, data, reassert));
                    await SendAsync(socket, frame, counts, ct).ConfigureAwait(false);
                    break;
                }
                case TermFrameKind.Title:
                    title = payload;
                    if (paused) Interlocked.Increment(ref counts.Withheld);
                    else
                    {
                        // A title is apart from the screen: what's due goes first, but a held candidate secret waits on (a CLI
                        // that retitles itself often mustn't push a half-printed token out bare).
                        await FlushDueAsync().ConfigureAwait(false);
                        await SendAsync(socket, TermProtocol.Frame(kind, MaskTitle(payload, b, settings)), counts, ct).ConfigureAwait(false);
                    }
                    break;
                default:
                    // Hello, Live, Size, Modes, Exit and anything newer carry no screen content: they go as they are, after
                    // what's due. A held candidate secret waits on past them, as past a Title (M.ORCH-T1038): Modes and Size
                    // frames come often, and forcing a candidate out masked on each of them bulleted ordinary code. Only Exit
                    // ends the output, so everything held goes before it (a strong candidate masked).
                    if (!paused)
                    {
                        if (kind == TermFrameKind.Exit) await FlushAsync().ConfigureAwait(false);
                        else await FlushDueAsync().ConfigureAwait(false);
                    }
                    await SendAsync(socket, TermProtocol.Frame(kind, payload), counts, ct).ConfigureAwait(false);
                    if (kind == TermFrameKind.Hello) helloSent = true;
                    break;
            }
        }
        return null;
    }

    private static byte[] MaskTitle(byte[] title, BroadcastStream b, TermBroadcastSettings settings) =>
        settings.IsMasked(b.SessionId) ? TermProtocol.Title(b.Secrets.MaskText(TermProtocol.ReadTitle(title))) : title;

    private static async Task SendAsync(WebSocket socket, byte[] frame, Counts counts, CancellationToken ct)
    {
        await socket.SendAsync(frame, WebSocketMessageType.Binary, endOfMessage: true, ct).ConfigureAwait(false);
        Interlocked.Increment(ref counts.FramesDown);
        Interlocked.Add(ref counts.BytesDown, frame.Length);
    }

    private static async Task TryCloseOutputAsync(WebSocket socket, WebSocketCloseStatus status, string reason)
    {
        if (socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived)) return;
        try
        {
            using var timeout = new CancellationTokenSource(CloseWait);
            // A close reason is at most 123 bytes.
            await socket.CloseOutputAsync(status, reason.Length > 100 ? reason[..100] : reason, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception) { }
    }

    private static async Task Quietly(Task task)
    {
        try { await task.ConfigureAwait(false); } catch (Exception) { }
    }

    private static void TryDispose(Stream stream)
    {
        try { stream.Dispose(); } catch (Exception) { }
    }
}
