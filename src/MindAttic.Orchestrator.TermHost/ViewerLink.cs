using System.Threading.Channels;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.TermHost;

/// <summary>
/// One attached viewer's pipe. Frames are queued in order by <see cref="Post"/> (called under the host's lock, so the replay
/// and the live stream can't interleave) and written by one writer task, so a viewer that stops reading never stalls the
/// CLI's output: once more than <see cref="MaxBacklog"/> bytes wait, the viewer is dropped instead.
/// </summary>
internal sealed class ViewerLink : IDisposable
{
    public const long MaxBacklog = 32L * 1024 * 1024;

    private readonly Stream pipe;
    private readonly HostLog log;
    private readonly Channel<byte[]> queue = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
    private long backlog;
    private int closed;

    public ViewerLink(Stream pipe, HostLog log)
    {
        this.pipe = pipe;
        this.log = log;
        Writer = Task.Run(WriteLoopAsync);
    }

    /// <summary>Raised once the viewer has been sent an Exit frame: it has seen the CLI exit.</summary>
    public event Action? ExitDelivered;

    public Task Writer { get; }

    public bool IsClosed => Volatile.Read(ref closed) != 0;

    /// <summary>Queues a frame; false when the viewer is gone or too far behind (it is closed then).</summary>
    public bool Post(TermFrameKind kind, ReadOnlySpan<byte> payload)
    {
        if (IsClosed) return false;
        var frame = TermProtocol.Frame(kind, payload);
        if (Interlocked.Add(ref backlog, frame.Length) > MaxBacklog)
        {
            log.Write($"viewer dropped: over {MaxBacklog / (1024 * 1024)} MB of output waiting");
            Dispose();
            return false;
        }
        return queue.Writer.TryWrite(frame);
    }

    /// <summary>Stops taking frames and closes the pipe once the queued ones are written.</summary>
    public void Complete() => queue.Writer.TryComplete();

    private async Task WriteLoopAsync()
    {
        try
        {
            await foreach (var frame in queue.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                await pipe.WriteAsync(frame).ConfigureAwait(false);
                await pipe.FlushAsync().ConfigureAwait(false);
                Interlocked.Add(ref backlog, -frame.Length);
                if (frame[0] == (byte)TermFrameKind.Exit) ExitDelivered?.Invoke();
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException or OperationCanceledException) { }
        catch (Exception ex) { log.Error(ex, "viewer writer failed; contained"); }
        finally { Dispose(); }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref closed, 1) != 0) return;
        queue.Writer.TryComplete();
        try { pipe.Dispose(); } catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
    }
}
