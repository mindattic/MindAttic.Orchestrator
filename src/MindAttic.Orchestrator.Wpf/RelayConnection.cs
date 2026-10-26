using System.IO;
using System.IO.Pipes;
using System.Text;
using Microsoft.Terminal.Wpf;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Wpf;

/// <summary>
/// An <see cref="ITerminalConnection"/> whose far end is a TermHost's pipe instead of a local ConPTY (M.ORCH-T926, ported from
/// the Phase 0 prototype prototypes/T923-relay). Start() connects and reads frames on a background task; the replay and the
/// live output reach the renderer through <see cref="TerminalOutput"/>. Closing it drops the pipe only: the host and its CLI
/// keep running.
/// <para>Everything it raises (output, modes, exit, detach) is posted, in order, to the SynchronizationContext captured in
/// <see cref="Start"/>, the UI thread's: feeding the renderer from the pipe's thread while the UI thread resized it deadlocked
/// the viewer (Phase 0 lesson 1). Nothing here throws to its caller.</para>
/// </summary>
internal sealed class RelayConnection(string pipeName, ushort protocol = TermProtocol.Version) : ITerminalConnection
{
    /// <summary>Win32-input-mode: the renderer encodes every key as ESC[Vk;Sc;Uc;Kd;Cs;Rc_, which ConPTY decodes back to key events.</summary>
    internal const string Win32InputMode = "\u001b[?9001h";

    /// <summary>The most text one Input frame carries (well under 64 KB of UTF-8); a larger paste goes as several, in order.</summary>
    internal const int InputChunkChars = 16 * 1024;

    private readonly object gate = new();
    private readonly Decoder utf8 = Encoding.UTF8.GetDecoder();
    private NamedPipeClientStream? pipe;
    private volatile bool closed;
    private uint rows, cols;
    private SynchronizationContext? ui;

    public event EventHandler<TerminalOutputEventArgs>? TerminalOutput;

    /// <summary>The host's tracked modes changed (on attach, and each time the CLI changes one).</summary>
    public event Action<TermModes>? ModesChanged;

    /// <summary>The CLI exited with this code (also sent on attach once it has).</summary>
    public event Action<int>? Exited;

    /// <summary>The pipe is gone without this side closing it (the host ended, crashed, or another viewer took it), or it never
    /// connected; with why.</summary>
    public event Action<string>? Detached;

    /// <summary>The host's tracked modes: <see cref="TermModes.BracketedPaste"/> decides how a paste is sent (T868).</summary>
    public TermModes Modes { get; private set; }

    public bool IsClosed => closed;

    // Start() runs on the UI thread (the Connection setter calls it, right after resetting the renderer).
    public void Start()
    {
        ui = SynchronizationContext.Current;
        // Phase 0 lesson 4: win32-input-mode on the renderer, as EasyTerminalControl does, since the setter's reset cleared it.
        Raise(Win32InputMode);
        _ = Task.Run(RunAsync);
    }

    private void Post(Action action)
    {
        void Run()
        {
            try { action(); }
            catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "A relay terminal event failed; contained"); }
        }
        if (ui is { } context) context.Post(_ => Run(), null);
        else Run();
    }

    private void Raise(string text)
    {
        if (text.Length == 0) return;
        Post(() => { if (!closed) TerminalOutput?.Invoke(this, new TerminalOutputEventArgs(text)); });
    }

    private async Task RunAsync()
    {
        try
        {
            var p = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                await p.ConnectAsync(timeout.Token).ConfigureAwait(false);
            uint r, c;
            lock (gate)
            {
                if (closed) { p.Dispose(); return; }
                pipe = p;
                r = rows; c = cols;
            }
            Send(TermFrameKind.Hello, TermProtocol.Hello(protocol, (ushort)(c > 0 ? c : 120), (ushort)(r > 0 ? r : 30)));

            while (!closed)
            {
                var (kind, payload) = await TermProtocol.ReadFrameAsync(p).ConfigureAwait(false);
                switch (kind)
                {
                    case TermFrameKind.Hello:
                        if (TermProtocol.ReadHello(payload) is { } hello && TermProtocol.Check(hello.Protocol) is { Ok: false } bad)
                        {
                            Raise($"\r\n\u001b[33m{bad.Message}\u001b[0m\r\n");
                            Post(() => Detached?.Invoke(bad.Message));
                            Close();
                            return;
                        }
                        break;
                    case TermFrameKind.Replay:
                        if (TermProtocol.ReadReplay(payload) is { } replay) OnReplay(replay);
                        break;
                    case TermFrameKind.Output:
                        Emit(payload);
                        break;
                    case TermFrameKind.Modes:
                        SetModes(TermProtocol.ReadModes(payload));
                        break;
                    case TermFrameKind.Exit:
                        var code = TermProtocol.ReadExit(payload);
                        Post(() => Exited?.Invoke(code));
                        break;
                }
            }
        }
        catch (Exception ex) when (closed || ex is IOException or ObjectDisposedException or EndOfStreamException or OperationCanceledException or TimeoutException)
        {
            if (!closed) Post(() => Detached?.Invoke(ex is OperationCanceledException or TimeoutException ? "the host's pipe didn't answer" : "the host closed the pipe"));
        }
        catch (Exception ex)
        {
            OrchestratorLog.For("Terminals").Error(ex, "The relay to {Pipe:l} failed", pipeName);
            if (!closed) Post(() => Detached?.Invoke("failed: " + ex.Message));
        }
    }

    private void SetModes(TermModes modes)
    {
        Modes = modes;
        Post(() => ModesChanged?.Invoke(modes));
    }

    private void OnReplay(TermReplay replay)
    {
        SetModes(replay.Modes);
        Emit(replay.Data);
        if (replay.Reassert.Length > 0) Raise(Encoding.UTF8.GetString(replay.Reassert));
        // A replay that starts at a full reset (ESC c) cleared win32-input-mode again: re-asserted after it.
        Raise(Win32InputMode);
    }

    private void Emit(ReadOnlySpan<byte> bytes)
    {
        var chars = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];
        var n = utf8.GetChars(bytes, chars, false);
        if (n == 0) return;
        var text = new string(chars, 0, n);
        Raise(text);
        // The CLI's own full reset (ESC c) would turn win32-input-mode off in the renderer.
        if (text.Contains("\u001bc", StringComparison.Ordinal)) Raise(Win32InputMode);
    }

    private void Send(TermFrameKind kind, ReadOnlySpan<byte> payload)
    {
        var frame = TermProtocol.Frame(kind, payload);
        lock (gate)
        {
            if (closed || pipe is not { IsConnected: true } p) return;
            try { p.Write(frame); }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException) { }
        }
    }

    /// <summary>Sends text to the CLI: the renderer's key sequences, or a paste payload (chunked, in order).</summary>
    public void WriteInput(string data)
    {
        if (string.IsNullOrEmpty(data)) return;
        var at = 0;
        while (at < data.Length)
        {
            var length = Math.Min(InputChunkChars, data.Length - at);
            // Never split a surrogate pair across two frames.
            if (at + length < data.Length && char.IsHighSurrogate(data[at + length - 1])) length--;
            Send(TermFrameKind.Input, Encoding.UTF8.GetBytes(data, at, length));
            at += length;
        }
    }

    public void Resize(uint rows, uint columns)
    {
        if (rows == 0 || columns == 0) return;
        lock (gate)
        {
            if (this.rows == rows && cols == columns && pipe is not null) return;
            this.rows = rows;
            cols = columns;
        }
        Send(TermFrameKind.Resize, TermProtocol.Size((ushort)Math.Min(columns, ushort.MaxValue), (ushort)Math.Min(rows, ushort.MaxValue)));
    }

    /// <summary>Drops the pipe; the host keeps running.</summary>
    public void Close()
    {
        lock (gate)
        {
            closed = true;
            try { pipe?.Dispose(); }
            catch (Exception) { }
            pipe = null;
        }
    }
}
