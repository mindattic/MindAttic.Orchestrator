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
/// <para>From protocol 2 (M.ORCH-T930) the host serves several viewers: this one says in its Hello whether it is interactive,
/// learns from Size frames whether it owns the session's size (<see cref="SizeChanged"/>; one that doesn't letterboxes), and
/// takes the size by typing or with <see cref="TakeSize"/>.</para>
/// <para>A link that drops while the host lives is attached again with a backoff (M.ORCH-T966): see <see cref="RunAsync"/>.</para>
/// <para>The link is any <see cref="Stream"/> of frames (M.ORCH-T936): the host's pipe, or a team gateway's WebSocket
/// (<c>WebSocketFrameStream</c>, one frame per message), opened afresh by <c>connect</c> on every attach.</para>
/// </summary>
public sealed class RelayConnection(string name, Func<CancellationToken, Task<Stream>> connect, ushort protocol = TermProtocol.Version,
    Func<bool>? hostAlive = null, bool interactive = true, string? viewerId = null)
    : ITerminalConnection
{
    /// <summary>A viewer of the TermHost serving <paramref name="pipeName"/> on this machine.</summary>
    public RelayConnection(string pipeName, ushort protocol = TermProtocol.Version, Func<bool>? hostAlive = null, bool interactive = true)
        : this(pipeName, ct => ConnectPipeAsync(pipeName, ct), protocol, hostAlive, interactive)
    {
    }

    /// <summary>Connects to a TermHost's pipe (current user only), giving up after five seconds.</summary>
    public static async Task<Stream> ConnectPipeAsync(string pipeName, CancellationToken ct)
    {
        var p = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            await p.ConnectAsync(timeout.Token).ConfigureAwait(false);
            return p;
        }
        catch
        {
            p.Dispose();
            throw;
        }
    }

    /// <summary>Win32-input-mode: the renderer encodes every key as ESC[Vk;Sc;Uc;Kd;Cs;Rc_, which ConPTY decodes back to key events.</summary>
    public const string Win32InputMode = "\u001b[?9001h";

    /// <summary>The most text one Input frame carries (well under 64 KB of UTF-8); a larger paste goes as several, in order.</summary>
    public const int InputChunkChars = 16 * 1024;

    /// <summary>How this dashboard names itself in a protocol 2 Hello (the host logs it).</summary>
    internal static readonly string ViewerId = $"dashboard-{Environment.ProcessId}";

    private readonly object gate = new();
    private readonly Decoder utf8 = Encoding.UTF8.GetDecoder();
    // Frames to write, each with the link it was meant for (M.ORCH-T975): one writer task drains it, so no write ever blocks
    // the UI thread or holds 'gate', and Close() breaks a stalled write by cancelling 'sending'.
    private readonly System.Threading.Channels.Channel<(Stream Link, byte[] Frame)> outbox =
        System.Threading.Channels.Channel.CreateUnbounded<(Stream, byte[])>(new() { SingleReader = true });
    private readonly CancellationTokenSource sending = new();
    private Stream? link;
    private volatile bool closed, exited, ended;
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

    /// <summary>The session's size (cols, rows) and whether this viewer owns it (M.ORCH-T930, protocol 2 hosts only). One that
    /// doesn't own it should show the session at that size and letterbox.</summary>
    public event Action<ushort, ushort, bool>? SizeChanged;

    /// <summary>This viewer and another typed within two seconds of each other (M.ORCH-T930).</summary>
    public event Action? OtherTyping;

    /// <summary>Whether this viewer owns the session's size: true until a protocol 2 host says otherwise.</summary>
    public bool OwnsSize { get; private set; } = true;

    /// <summary>While set, the renderer's resizes aren't sent: they are this viewer following the session's size, not the size
    /// it would show itself.</summary>
    internal bool SuppressResize { get; set; }

    /// <summary>The host's tracked modes: <see cref="TermModes.BracketedPaste"/> decides how a paste is sent (T868).</summary>
    public TermModes Modes { get; private set; }

    public bool IsClosed => closed;

    // Start() runs on the UI thread (the Connection setter calls it, right after resetting the renderer).
    public void Start()
    {
        ui = SynchronizationContext.Current;
        // Phase 0 lesson 4: win32-input-mode on the renderer, as EasyTerminalControl does, since the setter's reset cleared it.
        Raise(Win32InputMode);
        _ = Task.Run(WriteLoopAsync);
        _ = Task.Run(RunAsync);
    }

    /// <summary>Writes the queued frames in order, each to the link it was queued for (a frame for a link that has since dropped
    /// is skipped, so nothing reaches a new link ahead of its Hello). Ends on <see cref="Close"/>. Never throws.</summary>
    private async Task WriteLoopAsync()
    {
        var token = sending.Token;
        try
        {
            await foreach (var (target, frame) in outbox.Reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                lock (gate)
                {
                    if (closed || !ReferenceEquals(link, target)) continue;
                }
                try { await target.WriteAsync(frame, token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
                // The link dropped under the write: the read loop sees it too and reattaches.
                catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException or NotSupportedException
                                               or OperationCanceledException or System.Net.WebSockets.WebSocketException) { }
                catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Writing to {Pipe:l} failed; contained", name); }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "The writer to {Pipe:l} failed; contained", name); }
    }

    /// <summary>Posts an event (not output) to the UI thread, after any output raised before it: the open batch is sealed, so
    /// output raised after this goes out after it too.</summary>
    private void Post(Action action)
    {
        lock (pendingGate) batch = null;
        PostRaw(action);
    }

    private void PostRaw(Action action)
    {
        void Run()
        {
            try { action(); }
            catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "A relay terminal event failed; contained"); }
        }
        if (ui is { } context) context.Post(_ => Run(), null);
        else Run();
    }

    /// <summary>The most output text (chars) waiting for the UI thread before the reader stops reading the link (M.ORCH-T1015):
    /// the host then holds the rest, under its own per-viewer cap.</summary>
    internal const int MaxPendingChars = 1 << 20;

    // Output waiting for the UI thread (M.ORCH-T1015): text raised while a batch is open joins it, so a flood is one posted
    // closure per UI turn rather than one per frame; any other event seals the batch, keeping everything in order (Phase 0
    // lesson 1). pendingChars counts every unrendered batch; past MaxPendingChars the reader waits on 'roomForOutput'.
    private readonly object pendingGate = new();
    private StringBuilder? batch;
    private int pendingChars;
    private TaskCompletionSource? roomForOutput;

    private void Raise(string text)
    {
        if (text.Length == 0) return;
        StringBuilder? opened = null;
        lock (pendingGate)
        {
            pendingChars += text.Length;
            if (batch is null) batch = opened = new StringBuilder();
            batch.Append(text);
        }
        if (opened is not null) PostRaw(() => Drain(opened));
    }

    /// <summary>On the UI thread: renders one batch, whole, and lets a waiting reader go on once there is room.</summary>
    private void Drain(StringBuilder done)
    {
        string text;
        TaskCompletionSource? room = null;
        lock (pendingGate)
        {
            if (ReferenceEquals(batch, done)) batch = null;
            text = done.ToString();
            pendingChars -= text.Length;
            if (pendingChars <= MaxPendingChars / 2) (room, roomForOutput) = (roomForOutput, null);
        }
        room?.TrySetResult();
        if (!closed) TerminalOutput?.Invoke(this, new TerminalOutputEventArgs(text));
    }

    /// <summary>Waits while too much output is waiting for the UI thread (M.ORCH-T1015); returns at once when there is room or
    /// the connection is closed.</summary>
    private Task RoomForOutputAsync()
    {
        lock (pendingGate)
        {
            if (closed || pendingChars <= MaxPendingChars) return Task.CompletedTask;
            roomForOutput ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return roomForOutput.Task;
        }
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
                ended = true;
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
                ended = true;
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
            OrchestratorLog.For("Terminals").Error(ex, "Probing the host of {Pipe:l} failed; taken as gone", name);
            return false;
        }
    }

    /// <summary>Why one attach ended. <see cref="Final"/>: never try again. <see cref="Live"/>: it got as far as the live stream.</summary>
    private readonly record struct Drop(string Why, bool Final, bool Live);

    /// <summary>One attach: connect, Hello, then frames until the pipe goes. Never throws.</summary>
    private async Task<Drop> AttachOnceAsync(int previousAttaches)
    {
        var live = false;
        Stream? p = null;
        try
        {
            p = await connect(CancellationToken.None).ConfigureAwait(false);
            lock (gate)
            {
                if (closed) return new("closed", true, false);
                link = p;
                // Queued under the gate that set the link, so the Hello is the first frame this link carries.
                outbox.Writer.TryWrite((p, TermProtocol.Frame(TermFrameKind.Hello,
                    TermProtocol.Hello(protocol, (ushort)(cols > 0 ? cols : 120), (ushort)(rows > 0 ? rows : 30), interactive, viewerId ?? ViewerId))));
            }

            while (!closed)
            {
                var (kind, payload) = await TermProtocol.ReadFrameAsync(p).ConfigureAwait(false);
                switch (kind)
                {
                    case TermFrameKind.Hello:
                        if (TermProtocol.ReadHello(payload) is { } hello && TermProtocol.Check(hello.Protocol) is { Ok: false } bad)
                        {
                            // Close() sets 'closed', which drops whatever Raise posts and makes RunAsync return without a
                            // Detached (M.ORCH-T973): the message and the end are posted here, past that check, and logged.
                            var message = bad.Message;
                            OrchestratorLog.For("Terminals").Warning("The host of {Pipe:l} speaks another protocol: {Message:l}", name, message);
                            var text = $"\r\n\u001b[33m{message}\u001b[0m\r\n";
                            Post(() => TerminalOutput?.Invoke(this, new TerminalOutputEventArgs(text)));
                            Post(() => Detached?.Invoke(message));
                            Close();
                            return new(message, true, false);
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
                        await RoomForOutputAsync().ConfigureAwait(false);
                        break;
                    case TermFrameKind.Modes:
                        SetModes(TermProtocol.ReadModes(payload));
                        break;
                    case TermFrameKind.Size when TermProtocol.ReadSizeOwner(payload) is { Owned: { } owned } size:
                        OwnsSize = owned;
                        Post(() => SizeChanged?.Invoke(size.Cols, size.Rows, owned));
                        break;
                    case TermFrameKind.Typing:
                        Post(() => OtherTyping?.Invoke());
                        break;
                    case TermFrameKind.Paused when payload.Length > 0 && payload[0] != 0:
                        // From the team gateway only (M.ORCH-T935): nothing follows until the owner resumes, which clears the screen.
                        Raise("\r\n\u001b[0m\u001b[7m broadcast paused by owner \u001b[0m\r\n");
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
        catch (Exception ex) when (ex is System.Net.WebSockets.WebSocketException or System.Net.Http.HttpRequestException
                                       or System.Security.Authentication.AuthenticationException or InvalidOperationException)
        {
            // A team gateway's link (M.ORCH-T936): unreachable, refused, or the session gone. Expected, so only said, not logged.
            return new("the gateway's stream failed: " + ex.Message, false, live);
        }
        catch (Exception ex)
        {
            OrchestratorLog.For("Terminals").Error(ex, "The relay to {Pipe:l} failed", name);
            return new("failed: " + ex.Message, false, live);
        }
        finally
        {
            lock (gate)
            {
                if (ReferenceEquals(link, p)) link = null;
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

    /// <summary>Queues a frame for the current link (M.ORCH-T975): the writer task writes it, never the caller (the UI thread).</summary>
    private void Send(TermFrameKind kind, ReadOnlySpan<byte> payload)
    {
        var frame = TermProtocol.Frame(kind, payload);
        lock (gate)
        {
            if (closed || link is not { } p || p is NamedPipeClientStream { IsConnected: false }) return;
            outbox.Writer.TryWrite((p, frame));
        }
    }

    /// <summary>Input was typed or pasted while there was no link to send it on (connecting or reconnecting, M.ORCH-T979): it
    /// was dropped, never queued, since stale keystrokes replayed into a CLI later could do anything.</summary>
    public event Action? InputDropped;

    private bool HasLink()
    {
        lock (gate) return !closed && link is { } p && p is not NamedPipeClientStream { IsConnected: false };
    }

    /// <summary>Sends text to the CLI: the renderer's key sequences, or a paste payload (chunked, in order). With no link it is
    /// dropped and <see cref="InputDropped"/> raised (not once the connection is closed or has ended).</summary>
    public void WriteInput(string data)
    {
        if (string.IsNullOrEmpty(data)) return;
        if (!HasLink())
        {
            if (!closed && !ended) Post(() => { if (!closed && !ended) InputDropped?.Invoke(); });
            return;
        }
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

    /// <summary>Takes the session's size for this viewer (M.ORCH-T930): a no-op when it owns it already, is read-only, or the
    /// host is protocol 1 (one viewer, which always owns it).</summary>
    public void TakeSize()
    {
        if (OwnsSize || !interactive || protocol < TermProtocol.MultiViewer) return;
        Send(TermFrameKind.TakeSize, []);
    }

    public void Resize(uint rows, uint columns)
    {
        if (rows == 0 || columns == 0 || SuppressResize) return;
        lock (gate)
        {
            if (this.rows == rows && cols == columns && link is not null) return;
            this.rows = rows;
            cols = columns;
        }
        Send(TermFrameKind.Resize, TermProtocol.Size((ushort)Math.Min(columns, ushort.MaxValue), (ushort)Math.Min(rows, ushort.MaxValue)));
    }

    /// <summary>Drops the link; the host keeps running.</summary>
    public void Close()
    {
        // The writer never holds 'gate' while it writes, so this lock can't wait on a stalled write; the cancel breaks one.
        closed = true;
        try { sending.Cancel(); }
        catch (ObjectDisposedException) { }
        outbox.Writer.TryComplete();
        TaskCompletionSource? room;
        lock (pendingGate) (room, roomForOutput) = (roomForOutput, null);
        room?.TrySetResult();
        lock (gate)
        {
            closed = true;
            try { link?.Dispose(); }
            catch (Exception) { }
            link = null;
        }
    }
}
