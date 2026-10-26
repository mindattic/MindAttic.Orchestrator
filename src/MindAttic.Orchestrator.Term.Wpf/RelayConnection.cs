using System.IO;
using System.IO.Pipes;
using System.Text;
using Microsoft.Terminal.Wpf;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Term.Wpf;

/// <summary>
/// An <see cref="ITerminalConnection"/> whose far end is a TermHost's pipe instead of a local ConPTY (M.ORCH-T926, ported from
/// the Phase 0 prototype prototypes/T923-relay). Start() connects and reads frames on a background task; the replay and the
/// live output reach the renderer through <see cref="TerminalOutput"/>. Closing it drops the pipe only: the host and its CLI
/// keep running.
/// <para>Everything it raises (output, modes, exit, detach) is posted, in order, to the SynchronizationContext captured in
/// <see cref="Start"/>, the UI thread's: feeding the renderer from the pipe's thread while the UI thread resized it deadlocked
/// the viewer (Phase 0 lesson 1). Nothing here throws to its caller.</para>
/// <para>A link that drops while the host lives is attached again with a backoff (M.ORCH-T966): see <see cref="RunAsync"/>.</para>
/// </summary>
public sealed class RelayConnection(string pipeName, ushort protocol = TermProtocol.Version, Func<bool>? hostAlive = null) : ITerminalConnection
{
    /// <summary>Win32-input-mode: the renderer encodes every key as ESC[Vk;Sc;Uc;Kd;Cs;Rc_, which ConPTY decodes back to key events.</summary>
    public const string Win32InputMode = "\u001b[?9001h";

    /// <summary>The most text one Input frame carries (well under 64 KB of UTF-8); a larger paste goes as several, in order.</summary>
    public const int InputChunkChars = 16 * 1024;

    private readonly object gate = new();
    private readonly Decoder utf8 = Encoding.UTF8.GetDecoder();
    private NamedPipeClientStream? pipe;
    private volatile bool closed, exited;
    private uint rows, cols;
    private SynchronizationContext? ui;

    public event EventHandler<TerminalOutputEventArgs>? TerminalOutput;

    /// <summary>The host's tracked modes changed (on attach, and each time the CLI changes one).</summary>
    public event Action<TermModes>? ModesChanged;

    /// <summary>The CLI exited with this code (also sent on attach once it has).</summary>
    public event Action<int>? Exited;

    /// <summary>The pipe is gone for good without this side closing it: the host ended or crashed, the CLI exited, or it never
    /// connected while the host lived; with why. While the host lives, a dropped link raises <see cref="Reconnecting"/> instead.</summary>
    public event Action<string>? Detached;

    /// <summary>The link dropped while the host lives (M.ORCH-T966), with why. It is being attached again.</summary>
    public event Action<string>? Reconnecting;

    /// <summary>An attach reached the live stream (the first one, or one after <see cref="Reconnecting"/>).</summary>
    public event Action? Attached;

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

    /// <summary>How long to wait before reconnect attempt <paramref name="attempt"/> (0 first): 0.5 s, 1 s, 2 s, 4 s, then 5 s
    /// (M.ORCH-T966).</summary>
    internal static TimeSpan ReconnectDelay(int attempt) =>
        TimeSpan.FromMilliseconds(Math.Min(500.0 * Math.Pow(2, Math.Clamp(attempt, 0, 16)), MaxReconnectDelay.TotalMilliseconds));

    /// <summary>The longest wait between reconnect attempts.</summary>
    internal static readonly TimeSpan MaxReconnectDelay = TimeSpan.FromSeconds(5);

    /// <summary>A link that stayed live this long starts the backoff over when it drops; a shorter one keeps climbing it, so two
    /// viewers of a one-viewer host (protocol 1, where a new connection replaces the old) can't take it from each other fast.</summary>
    internal static readonly TimeSpan StableLink = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Attaches, and while the host is alive (<c>hostAlive</c>) attaches again whenever the link drops, with a growing backoff
    /// (M.ORCH-T966): the host's pipe broke, another viewer took the session, or a transient error. Each new attach gets the
    /// host's replay, which restores the screen. It stops, raising <see cref="Detached"/>, once the host is gone, the CLI has
    /// exited, the host speaks a protocol this viewer can't, or no liveness probe was given.
    /// </summary>
    private async Task RunAsync()
    {
        var attempt = 0;
        var attaches = 0;
        while (!closed)
        {
            var started = DateTime.UtcNow;
            var drop = await AttachOnceAsync(attaches).ConfigureAwait(false);
            if (closed) return;
            if (drop.Live) attaches++;
            if (drop.Final || exited || !HostAlive())
            {
                Post(() => Detached?.Invoke(drop.Why));
                return;
            }
            if (drop.Live && DateTime.UtcNow - started >= StableLink) attempt = 0;
            var wait = ReconnectDelay(attempt++);
            var why = drop.Why;
            Post(() => Reconnecting?.Invoke(why));
            try { await Task.Delay(wait).ConfigureAwait(false); }
            catch (Exception) { }
            if (closed) return;
            if (!HostAlive())
            {
                Post(() => Detached?.Invoke(why + "; the host is gone"));
                return;
            }
        }
    }

    /// <summary>Whether the host is still alive, by the probe; false without one, or when it throws.</summary>
    private bool HostAlive()
    {
        if (hostAlive is null) return false;
        try { return hostAlive(); }
        catch (Exception ex)
        {
            OrchestratorLog.For("Terminals").Error(ex, "Probing the host of {Pipe:l} failed; taken as gone", pipeName);
            return false;
        }
    }

    /// <summary>Why one attach ended. <see cref="Final"/>: never try again. <see cref="Live"/>: it got as far as the live stream.</summary>
    private readonly record struct Drop(string Why, bool Final, bool Live);

    /// <summary>One attach: connect, Hello, then frames until the pipe goes. Never throws.</summary>
    private async Task<Drop> AttachOnceAsync(int previousAttaches)
    {
        var live = false;
        NamedPipeClientStream? p = null;
        try
        {
            p = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                await p.ConnectAsync(timeout.Token).ConfigureAwait(false);
            uint r, c;
            lock (gate)
            {
                if (closed) return new("closed", true, false);
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
                            Close();
                            return new(bad.Message, true, false);
                        }
                        break;
                    case TermFrameKind.Replay:
                        if (TermProtocol.ReadReplay(payload) is { } replay)
                        {
                            // A reattach: the renderer still shows the old screen, so it is cleared before the replay redraws it.
                            if (previousAttaches > 0) Raise("\u001bc");
                            OnReplay(replay);
                        }
                        break;
                    case TermFrameKind.Live:
                        live = true;
                        Post(() => Attached?.Invoke());
                        break;
                    case TermFrameKind.Output:
                        Emit(payload);
                        break;
                    case TermFrameKind.Modes:
                        SetModes(TermProtocol.ReadModes(payload));
                        break;
                    case TermFrameKind.Exit:
                        var code = TermProtocol.ReadExit(payload);
                        exited = true;
                        Post(() => Exited?.Invoke(code));
                        break;
                }
            }
            return new("closed", true, live);
        }
        catch (Exception ex) when (closed || ex is IOException or ObjectDisposedException or EndOfStreamException or OperationCanceledException or TimeoutException)
        {
            return new(ex is OperationCanceledException or TimeoutException ? "the host's pipe didn't answer" : "the host closed the pipe", closed, live);
        }
        catch (Exception ex)
        {
            OrchestratorLog.For("Terminals").Error(ex, "The relay to {Pipe:l} failed", pipeName);
            return new("failed: " + ex.Message, false, live);
        }
        finally
        {
            lock (gate)
            {
                if (ReferenceEquals(pipe, p)) pipe = null;
            }
            try { p?.Dispose(); }
            catch (Exception) { }
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
