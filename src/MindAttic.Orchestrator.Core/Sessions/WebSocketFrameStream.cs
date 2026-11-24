using System.Net.WebSockets;
using System.Threading.Channels;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// A connected WebSocket to a team gateway's <c>/sessions/{id}/stream</c> as a <see cref="Stream"/> of TermHost frames
/// (M.ORCH-T936), so a RelayConnection reads and writes it exactly as it does a TermHost's pipe. The gateway carries one frame
/// per binary message both ways (docs/spikes/cli-host-viewer.md, "The protocol over the WebSocket"):
/// <list type="bullet">
/// <item>reads hand out the bytes of each received message in turn, so <see cref="TermProtocol.ReadFrameAsync"/> reads one
/// frame per message; a close from the gateway reads as the end of the stream;</item>
/// <item>each write is one whole frame (RelayConnection writes a frame in one call) and goes as one binary message. It is queued
/// and sent by a loop of its own, so a slow link never blocks the caller (the UI thread typing); a queue that backs up past
/// <see cref="MaxQueued"/> frames, or a send that fails, makes every later write throw an <see cref="IOException"/>; reads go on
/// until the socket closes or the stream is disposed.</item>
/// </list>
/// Disposing it aborts the socket. Nothing it does logs a byte of the stream.
/// </summary>
public sealed class WebSocketFrameStream : Stream
{
    /// <summary>The most frames waiting to be sent before writes fail: far past anything a person types.</summary>
    public const int MaxQueued = 4096;

    /// <summary>The largest message accepted: one frame of <see cref="TermProtocol.MaxPayload"/> and its header.</summary>
    public const int MaxMessage = TermProtocol.MaxPayload + 5;

    private readonly WebSocket socket;
    private readonly Channel<byte[]> outbox = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(MaxQueued)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.Wait,
    });
    private readonly CancellationTokenSource stop = new();
    private byte[] message = new byte[64 * 1024];
    private int position, length;
    private volatile bool broken, disposed;

    /// <summary>How often a stream looks for a fresher token to send the gateway (M.ORCH-T1041).</summary>
    public static readonly TimeSpan RefreshCheck = TimeSpan.FromSeconds(20);

    private volatile bool wrote;

    /// <param name="freshToken">The member's token for this gateway as it is now (the settings, read again), or null when there
    /// is none: whenever it is a newer one than the stream last sent (it lasts longer), it goes to the gateway in a RefreshToken
    /// frame (M.ORCH-T1041), so the stream outlives the token it was opened with. Without it nothing is refreshed.</param>
    /// <param name="sentToken">The token the stream was opened with.</param>
    public WebSocketFrameStream(WebSocket socket, Func<string?>? freshToken = null, string? sentToken = null)
    {
        this.socket = socket;
        _ = Task.Run(SendLoopAsync);
        if (freshToken is not null) _ = Task.Run(() => RefreshLoopAsync(freshToken, sentToken));
    }

    /// <summary>Every <see cref="RefreshCheck"/>, a newer token goes to the gateway, after the viewer's own first frame (its Hello)
    /// so it never comes ahead of it. Never throws; ends with the stream.</summary>
    private async Task RefreshLoopAsync(Func<string?> freshToken, string? sent)
    {
        try
        {
            while (!disposed && !broken)
            {
                await Task.Delay(RefreshCheck, stop.Token).ConfigureAwait(false);
                if (!wrote) continue;
                string? next;
                try { next = freshToken(); }
                catch (Exception ex)
                {
                    OrchestratorLog.For("TeamWall").Error(ex, "Reading a gateway's fresh token failed; contained");
                    continue;
                }
                if (!IsNewer(next, sent, DateTime.UtcNow)) continue;
                if (!outbox.Writer.TryWrite(TermProtocol.Frame(TermFrameKind.RefreshToken, System.Text.Encoding.UTF8.GetBytes(next!)))) return;
                sent = next;
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
    }

    /// <summary>Whether <paramref name="candidate"/> is worth sending in place of <paramref name="sent"/>: another token, not yet
    /// expired, that says it lasts longer.</summary>
    internal static bool IsNewer(string? candidate, string? sent, DateTime nowUtc) =>
        !string.IsNullOrWhiteSpace(candidate) && candidate != sent
        && GatewayRemotes.ExpiresUtc(candidate) is { } expires && expires > nowUtc
        && (GatewayRemotes.ExpiresUtc(sent) is not { } old || expires > old);

    /// <summary>Readable until disposed.</summary>
    public override bool CanRead => !disposed;
    /// <summary>Writable until disposed or the link breaks.</summary>
    public override bool CanWrite => !disposed && !broken;
    /// <summary>Never seekable.</summary>
    public override bool CanSeek => false;
    /// <summary>Not supported: throws.</summary>
    public override long Length => throw new NotSupportedException();
    /// <summary>Not supported: throws.</summary>
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    private async Task SendLoopAsync()
    {
        try
        {
            await foreach (var frame in outbox.Reader.ReadAllAsync(stop.Token).ConfigureAwait(false))
                await socket.SendAsync(frame, WebSocketMessageType.Binary, endOfMessage: true, stop.Token).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Cancelled by Dispose, or the link failed: either way nothing more can be sent; reads end only with the socket.
            broken = true;
        }
    }

    /// <summary>Reads the next bytes of the current message, receiving the next message when it's used up; 0 once the
    /// gateway has closed.</summary>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (buffer.Length == 0) return 0;
        if (position >= length && !await ReceiveMessageAsync(cancellationToken).ConfigureAwait(false)) return 0;
        var n = Math.Min(buffer.Length, length - position);
        message.AsMemory(position, n).CopyTo(buffer);
        position += n;
        return n;
    }

    /// <summary>Reads into <paramref name="buffer"/> at <paramref name="offset"/>, as the memory overload does.</summary>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    /// <summary>Reads synchronously, blocking on the async read.</summary>
    public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    /// <summary>Receives the next whole, non-empty message into the buffer; false when the gateway closed the socket.</summary>
    private async Task<bool> ReceiveMessageAsync(CancellationToken ct)
    {
        position = 0;
        length = 0;
        while (true)
        {
            if (length == message.Length)
            {
                if (message.Length >= MaxMessage) throw new InvalidDataException($"A gateway message is over the {MaxMessage}-byte limit.");
                Array.Resize(ref message, (int)Math.Min((long)message.Length * 2, MaxMessage));
            }
            var result = await socket.ReceiveAsync(message.AsMemory(length), ct).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                broken = true;
                return false;
            }
            length += result.Count;
            if (!result.EndOfMessage) continue;
            if (length > 0) return true;
        }
    }

    /// <summary>Queues <paramref name="count"/> bytes of <paramref name="buffer"/> as one frame, as the span overload does.</summary>
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    /// <summary>Queues <paramref name="buffer"/> (one whole frame) to go as one binary message; throws an
    /// <see cref="IOException"/> once the link is broken or the queue is full.</summary>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (broken) throw new IOException("The gateway's stream is closed.");
        if (buffer.Length == 0) return;
        if (!outbox.Writer.TryWrite(buffer.ToArray())) throw new IOException("The gateway's stream is backed up; the link is taken as lost.");
        wrote = true;
    }

    /// <summary>Queues the frame as <see cref="Write(ReadOnlySpan{byte})"/> does; it never waits for the send.</summary>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Write(buffer.AsSpan(offset, count));
        return Task.CompletedTask;
    }

    /// <summary>Queues the frame as <see cref="Write(ReadOnlySpan{byte})"/> does; it never waits for the send.</summary>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    /// <summary>Does nothing: each write is already queued whole.</summary>
    public override void Flush() { }
    /// <summary>Not supported: throws.</summary>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    /// <summary>Not supported: throws.</summary>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <summary>Stops the send loop and aborts the socket, so a pending read ends at once.</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            broken = true;
            outbox.Writer.TryComplete();
            try { stop.Cancel(); } catch (ObjectDisposedException) { }
            stop.Dispose();
            // Abort, not a close handshake: a pending receive ends at once, and the gateway sees the viewer go.
            try { socket.Abort(); } catch (Exception) { }
            try { socket.Dispose(); } catch (Exception) { }
        }
        base.Dispose(disposing);
    }
}
