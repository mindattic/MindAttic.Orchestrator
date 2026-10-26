using System.Buffers.Binary;
using System.IO;
using System.IO.Pipes;
using System.Text;
using Microsoft.Terminal.Wpf;
using TermHostStub;

namespace RelayViewer;

/// <summary>
/// An ITerminalConnection whose far end is a TermHostStub's pipe instead of a local ConPTY (M.ORCH-T923). Start() connects and
/// reads frames on a background task; the replay and the live output reach the renderer through TerminalOutput.
/// </summary>
internal sealed class RelayConnection(string sessionId) : ITerminalConnection
{
    private readonly object gate = new();
    private readonly Decoder utf8 = Encoding.UTF8.GetDecoder();
    private NamedPipeClientStream? pipe;
    private volatile bool closed;
    private uint rows = 30, cols = 120;

    public event EventHandler<TerminalOutputEventArgs>? TerminalOutput;

    /// <summary>Raised (any thread) with a human-readable state: connecting, attached, exited, detached, failure.</summary>
    public event Action<string>? StatusChanged;

    /// <summary>Raised with the host's mode bits (Protocol.Bracketed and the rest) on attach and each time they change.</summary>
    public event Action<uint>? ModesChanged;

    /// <summary>The host's grid, from the Replay frame.</summary>
    public event Action<int, int>? HostSize;

    public uint Modes { get; private set; }

    public void Start() => Task.Run(RunAsync);

    private async Task RunAsync()
    {
        try
        {
            StatusChanged?.Invoke("connecting");
            var p = new NamedPipeClientStream(".", Protocol.PipeName(sessionId), PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await p.ConnectAsync(timeout.Token).ConfigureAwait(false);
            uint r, c;
            lock (gate) { if (closed) { p.Dispose(); return; } pipe = p; r = rows; c = cols; }
            Send(Protocol.Hello, Protocol.Size((ushort)c, (ushort)r));

            while (true)
            {
                var (kind, payload) = await Protocol.ReadFrameAsync(p).ConfigureAwait(false);
                switch (kind)
                {
                    case Protocol.Replay: OnReplay(payload); break;
                    case Protocol.Live: StatusChanged?.Invoke("attached"); break;
                    case Protocol.Output: Emit(payload); break;
                    case Protocol.Modes:
                        Modes = BinaryPrimitives.ReadUInt32LittleEndian(payload);
                        ModesChanged?.Invoke(Modes);
                        break;
                    case Protocol.Exit:
                        StatusChanged?.Invoke($"CLI exited (code {BinaryPrimitives.ReadInt32LittleEndian(payload)})");
                        TerminalOutput?.Invoke(this, new TerminalOutputEventArgs(
                            $"\r\n\u001b[33m[CLI exited with code {BinaryPrimitives.ReadInt32LittleEndian(payload)}]\u001b[0m\r\n"));
                        break;
                }
            }
        }
        catch (Exception ex) when (closed || ex is IOException or ObjectDisposedException or EndOfStreamException or OperationCanceledException)
        {
            StatusChanged?.Invoke(closed ? "disconnected" : ex is OperationCanceledException ? "no host with that id (timed out)" : "host closed the pipe");
        }
        catch (Exception ex) { StatusChanged?.Invoke("failed: " + ex.Message); }
    }

    private void OnReplay(byte[] payload)
    {
        var hostCols = BinaryPrimitives.ReadUInt16LittleEndian(payload);
        var hostRows = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(2));
        Modes = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(4));
        var dataLen = (int)BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(8));
        HostSize?.Invoke(hostCols, hostRows);
        ModesChanged?.Invoke(Modes);
        // The renderer was just reset; win32-input-mode first (as EasyTerminalControl does), then the ring, then the modes.
        TerminalOutput?.Invoke(this, new TerminalOutputEventArgs("\u001b[?9001h"));
        Emit(payload.AsSpan(12, dataLen));
        var tail = Encoding.UTF8.GetString(payload, 12 + dataLen, payload.Length - 12 - dataLen);
        if (tail.Length > 0) TerminalOutput?.Invoke(this, new TerminalOutputEventArgs(tail));
    }

    private void Emit(ReadOnlySpan<byte> bytes)
    {
        var chars = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];
        var n = utf8.GetChars(bytes, chars, false);
        if (n > 0) TerminalOutput?.Invoke(this, new TerminalOutputEventArgs(new string(chars, 0, n)));
    }

    private void Send(byte kind, ReadOnlySpan<byte> payload)
    {
        var frame = Protocol.Frame(kind, payload);
        lock (gate)
        {
            if (pipe is not { IsConnected: true } p) return;
            try { p.Write(frame); }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
        }
    }

    /// <summary>Sends text to the CLI (the renderer's key sequences, or a paste payload).</summary>
    public void WriteInput(string data) => Send(Protocol.Input, Encoding.UTF8.GetBytes(data));

    public void Resize(uint rows, uint columns)
    {
        lock (gate) { this.rows = rows; cols = columns; }
        Send(Protocol.Resize, Protocol.Size((ushort)columns, (ushort)rows));
    }

    /// <summary>Drops the pipe; the host keeps running.</summary>
    public void Close()
    {
        lock (gate)
        {
            closed = true;
            pipe?.Dispose();
        }
    }
}
