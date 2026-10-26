using System.Buffers.Binary;
using System.Net.WebSockets;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Gateway;

/// <summary>How a bridge ended, for the log: counts only, never content.</summary>
internal sealed record BridgeResult(string EndedBy, long FramesUp, long FramesDown, long BytesDown, long Dropped);

/// <summary>
/// Bridges one remote viewer's WebSocket to one session's local pipe (M.ORCH-T933), frame for frame: each binary WebSocket
/// message carries exactly one TermHost frame (<c>u8 kind | u32 length | payload</c>, <see cref="TermProtocol"/>), in both
/// directions, so a remote viewer runs the same RelayConnection logic over a WebSocket stream as a local one does over the pipe.
/// <para>Viewer to host, only the viewer's kinds go through, and the first must be Hello. A read-only (<see cref="GatewayAccess.Watch"/>)
/// viewer's Hello is rewritten to size 0x0, which the host ignores, so it never takes the session's size, and from protocol 2
/// (M.ORCH-T930) to a read-only Hello, so the host drops its input itself; its Input, Resize and TakeSize frames are dropped.
/// Host to viewer, every frame goes through unchanged (Hello, Replay, Live, Output, Modes, Size with its owner byte, Title,
/// Typing, Exit).</para>
/// <para>Nothing here throws: either side ending, or a malformed frame, closes both.</para>
/// </summary>
internal static class TermBridge
{
    /// <summary>The largest message a remote viewer may send: a 64 KB paste chunk with room to spare, far under the host's limit.</summary>
    public const int MaxInbound = 1024 * 1024 + 5;

    private static readonly TimeSpan CloseWait = TimeSpan.FromSeconds(2);

    /// <param name="input">Counts each Input frame that goes on to the host, for the audit log (M.ORCH-T934): counts only.</param>
    /// <param name="revoke">Fired when the viewer's access was revoked (M.ORCH-T934): the pipe is let go, and the viewer is told
    /// so with a policy-violation close, as when the host closes it (cancelling a receive would abort the socket unannounced).</param>
    public static async Task<BridgeResult> RunAsync(WebSocket socket, Stream pipe, GatewayAccess access, GatewayLog log, CancellationToken ct,
        GatewayInputMeter? input = null, CancellationToken revoke = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var counts = new Counts();
        var revoked = 0;
        using var onRevoke = revoke.Register(() =>
        {
            Interlocked.Exchange(ref revoked, 1);
            TryDispose(pipe);
        });
        var up = Task.Run(() => UpstreamAsync(socket, pipe, access, counts, log, input, cts.Token));
        var down = Task.Run(() => DownstreamAsync(socket, pipe, counts, log, cts.Token));
        string endedBy;
        try
        {
            var first = await Task.WhenAny(up, down).ConfigureAwait(false);
            if (first == down)
            {
                var byRevoke = Volatile.Read(ref revoked) == 1;
                endedBy = byRevoke ? "access revoked" : "host";
                // The host closed the pipe (the session ended, or another viewer replaced this one), or access was revoked: say
                // so, and give the viewer a moment to answer the close.
                await TryCloseOutputAsync(socket, byRevoke ? WebSocketCloseStatus.PolicyViolation : WebSocketCloseStatus.NormalClosure,
                    byRevoke ? "access revoked" : "the session's host closed the stream").ConfigureAwait(false);
                await Task.WhenAny(up, Task.Delay(CloseWait, CancellationToken.None)).ConfigureAwait(false);
                cts.Cancel();
            }
            else
            {
                var close = await up.ConfigureAwait(false);
                if (Volatile.Read(ref revoked) == 1) close = new(WebSocketCloseStatus.PolicyViolation, "access revoked");
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
            Interlocked.Read(ref counts.BytesDown), Interlocked.Read(ref counts.Dropped));
    }

    private sealed class Counts
    {
        public long FramesUp, FramesDown, BytesDown, Dropped;
    }

    private readonly record struct Close(WebSocketCloseStatus Status, string Reason);

    /// <summary>Viewer to host. Returns how the socket should be closed.</summary>
    private static async Task<Close> UpstreamAsync(WebSocket socket, Stream pipe, GatewayAccess access, Counts counts, GatewayLog log,
        GatewayInputMeter? input, CancellationToken ct)
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
                    // From protocol 2 it is also marked read-only (M.ORCH-T930), keeping the viewer's id.
                    if (access < GatewayAccess.Interact)
                        frame = TermProtocol.Frame(TermFrameKind.Hello, TermProtocol.Hello(hello.Protocol, 0, 0, interactive: false, hello.ViewerId));
                }
                else if (!Forwards(kind, access))
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

    /// <summary>Host to viewer: each frame as one binary message, unchanged.</summary>
    private static async Task DownstreamAsync(WebSocket socket, Stream pipe, Counts counts, GatewayLog log, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var (kind, payload) = await TermProtocol.ReadFrameAsync(pipe, ct).ConfigureAwait(false);
                var frame = TermProtocol.Frame(kind, payload);
                await socket.SendAsync(frame, WebSocketMessageType.Binary, endOfMessage: true, ct).ConfigureAwait(false);
                Interlocked.Increment(ref counts.FramesDown);
                Interlocked.Add(ref counts.BytesDown, frame.Length);
            }
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException or ObjectDisposedException or OperationCanceledException
                                       or WebSocketException or InvalidDataException) { }
        catch (Exception ex) { log.Error(ex, "writing to the viewer failed; contained"); }
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
