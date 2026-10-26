using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using MindAttic.Orchestrator.Term;
using MindAttic.Orchestrator.TermHost.Replay;
using static MindAttic.Orchestrator.TermHost.Native;

namespace MindAttic.Orchestrator.TermHost;

/// <summary>
/// One CLI on a ConPTY and the named pipe its viewers attach to (M.ORCH-T924, promoted from the T923 prototype). Any number
/// of viewers at once (M.ORCH-T930): output goes to every one, input from interactive ones is merged and from read-only ones
/// dropped, and the size is the owner's (<see cref="ViewerPolicy"/>); the rest are told it with Size frames and letterbox.
/// When the CLI exits every viewer is sent Exit, and the host lingers (<see cref="HostOptions.Linger"/>) until a viewer has
/// seen the exit, then ends. Nothing here throws out of its thread: every failure is logged and contained.
/// </summary>
internal sealed class TermSession
{
    private static readonly TimeSpan HelloTimeout = TimeSpan.FromSeconds(10);

    private readonly HostOptions options;
    private readonly HostLog log;
    private readonly object gate = new();
    private readonly object inputGate = new();
    private readonly ManualResetEventSlim done = new();
    private readonly ManualResetEventSlim exitSeen = new();

    private static readonly TimeSpan TypingThrottle = TimeSpan.FromSeconds(1);
    private const int DrainLimitMs = 100, DrainQuietMs = 20;

    // M.ORCH-T997: how long the exit waits for the output pump to reach EOF before Exit goes out.
    private const int PumpDrainMs = 5_000, PumpDrainMaxMs = 30_000, PumpQuietMs = 500;

    // M.ORCH-T994: the most a Replay frame's data may take: half a viewer's backlog, leaving the rest for the live output that
    // queues behind it while it is written.
    private const int ReplayBudget = (int)(ViewerLink.MaxBacklog / 2);

    private IntPtr hPC, hProcess;

    // M.ORCH-T991: the pseudoconsole's lifetime. Every ResizePseudoConsole and the close take ptyGate; the close sets ptyClosed
    // and zeroes hPC first, so no resize ever reaches a closed (freed) pseudoconsole. Never taken while holding gate.
    private readonly object ptyGate = new();
    private volatile bool ptyClosed;

    // When the pump last handed on output (Environment.TickCount64): a resize waits for a quiet gap after it (M.ORCH-T930).
    private long lastOutputTicks;
    private Stream? ptyIn, ptyOut;
    private Thread? pump;
    private NamedPipeServerStream? firstInstance;
    private ushort cols = 120, rows = 30;
    // M.ORCH-T930: every attached viewer, and who types and owns the size. Both under gate.
    private readonly List<ViewerLink> viewers = [];
    private readonly ViewerPolicy policy = new();
    private long nextViewerId;
    private bool exited;
    private int exitCode;
    private string? title;

    // M.ORCH-T927: the late-attach replay. Fed and copied only under gate.
    private readonly Ring ring = new();
    private readonly ModeTracker modes = new();
    private TermModes lastModes = TermModes.CursorVisible;

    // M.ORCH-T925: the registry file and the term-session announcements.
    private Registration? registration;

    // M.ORCH-T929: the headless emulator the exact replay is taken from; null when it couldn't start or has failed, and the
    // ring above serves alone. Fed, resized and snapshotted only under gate.
    private HeadlessScreen? screen;

    public TermSession(HostOptions options, HostLog log)
    {
        this.options = options;
        this.log = log;
        title = options.Title;
    }

    /// <summary>Runs until the CLI has exited and the linger is over. 0 when the CLI ran, 1 when it couldn't start, 3 when the
    /// session id is already taken by another host.</summary>
    public int Run()
    {
        var commandLine = options.CommandLine(HostOptions.FindOnPath);
        log.Write($"host start id={options.Id} pipe={options.PipeName} cwd={options.Cwd} cmd={commandLine}");

        // Claim the pipe name first: a second host with the same id must not serve the same pipe.
        try { firstInstance = NewServer(firstInstance: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Write($"session id {options.Id} is taken (pipe {options.PipeName} exists): {ex.Message}; exiting");
            return 3;
        }

        // Registered once the id is ours, before the CLI starts, so a launcher waiting for the file finds it at once.
        if (options.Unlisted) log.Write("unlisted: no registry file, no term-session announcement");
        else
        {
            try { registration = new Registration(options, commandLine, cols, rows, log); registration.Start(); }
            catch (Exception ex) { log.Error(ex, "registering the session failed; running unregistered"); registration = null; }
        }

        StartScreen();
        var started = TryStartCli(commandLine);
        _ = Task.Run(AcceptLoopAsync);
        if (started)
        {
            // Set before WatchExit starts, so its wait for the pump never misses a pump that hasn't run yet (M.ORCH-T997).
            pump = new Thread(PumpOutput) { IsBackground = true, Name = "pty-out" };
            pump.Start();
            new Thread(WatchExit) { IsBackground = true, Name = "pty-exit" }.Start();
        }
        else new Thread(() => Linger(exitCode)) { IsBackground = true, Name = "linger" }.Start();

        done.Wait();
        lock (gate)
        {
            foreach (var v in viewers) v.Dispose();
            viewers.Clear();
            screen?.Dispose();
            screen = null;
        }
        registration?.End();
        log.Write("host exit");
        return started ? 0 : 1;
    }

    private bool TryStartCli(string commandLine)
    {
        try
        {
            StartCli(commandLine);
            return true;
        }
        catch (Exception ex)
        {
            var code = ex is Win32Exception w ? w.NativeErrorCode : -1;
            var why = code == 2 || code == 3 ? $"command not found: {commandLine}" : ex.Message;
            log.Write($"the CLI couldn't start ({why}, error {code}): {ex}");
            try { ClosePty(); } catch (Exception) { }
            lock (gate)
            {
                exited = true;
                exitCode = code == 0 ? -1 : code;
                MarkExited(exitCode);
                // What a viewer that attaches is shown, so it can tell why nothing ran.
                var message = Encoding.UTF8.GetBytes($"\u001b[31mTermHost: the CLI couldn't start: {why} (error {code})\u001b[0m\r\n");
                ring.Append(message);
                FeedScreen(message);
            }
            return false;
        }
    }

    private void StartCli(string commandLine)
    {
        if (!CreatePipe(out var inRead, out var inWrite, IntPtr.Zero, 0) || !CreatePipe(out var outRead, out var outWrite, IntPtr.Zero, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreatePipe");
        var hr = CreatePseudoConsole(new COORD { X = (short)cols, Y = (short)rows }, inRead, outWrite, 0, out hPC);
        if (hr != 0) throw new Win32Exception(hr, $"CreatePseudoConsole 0x{hr:x}");
        ptyIn = new FileStream(new SafeFileHandle(inWrite, true), FileAccess.Write, 1, false);
        ptyOut = new FileStream(new SafeFileHandle(outRead, true), FileAccess.Read, 1, false);

        var size = IntPtr.Zero;
        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
        var list = Marshal.AllocHGlobal(size);
        try
        {
            if (!InitializeProcThreadAttributeList(list, 1, 0, ref size)) throw new Win32Exception(Marshal.GetLastWin32Error(), "InitializeProcThreadAttributeList");
            try
            {
                if (!UpdateProcThreadAttribute(list, 0, PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE, hPC, IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "UpdateProcThreadAttribute");
                var si = new STARTUPINFOEX { lpAttributeList = list };
                si.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();
                var cwd = string.IsNullOrWhiteSpace(options.Cwd) ? null : options.Cwd;
                if (!CreateProcessW(null, (commandLine + "\0").ToCharArray(), IntPtr.Zero, IntPtr.Zero, false,
                        EXTENDED_STARTUPINFO_PRESENT | CREATE_UNICODE_ENVIRONMENT, IntPtr.Zero, cwd, ref si, out var pi))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateProcess: " + commandLine);
                hProcess = pi.hProcess;
                CloseHandle(pi.hThread);
                log.Write($"cli started pid={pi.dwProcessId}");
            }
            finally { DeleteProcThreadAttributeList(list); }
        }
        finally
        {
            Marshal.FreeHGlobal(list);
            // The pseudoconsole holds its own ends now.
            CloseHandle(inRead);
            CloseHandle(outWrite);
        }
    }

    private void PumpOutput()
    {
        var buffer = new byte[16384];
        try
        {
            int n;
            while ((n = ptyOut!.Read(buffer, 0, buffer.Length)) > 0) OnOutput(buffer.AsSpan(0, n));
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
        catch (Exception ex) { log.Error(ex, "the output pump failed; contained"); }
        log.Write("pty output ended");
    }

    private void OnOutput(ReadOnlySpan<byte> chunk)
    {
        Volatile.Write(ref lastOutputTicks, Environment.TickCount64);
        lock (gate)
        {
            ring.Append(chunk);
            FeedScreen(chunk);
            modes.Feed(chunk);
            Broadcast(TermFrameKind.Output, chunk);

            var flags = modes.Flags;
            if (flags != lastModes)
            {
                lastModes = flags;
                Broadcast(TermFrameKind.Modes, TermProtocol.Modes(flags));
            }
            if (modes.Title is { } cliTitle && cliTitle != title)
            {
                title = cliTitle;
                registration?.Update(s => s with { Title = cliTitle });
                Broadcast(TermFrameKind.Title, TermProtocol.Title(cliTitle));
            }
        }
    }

    private void WatchExit()
    {
        var code = -1;
        try
        {
            WaitForSingleObject(hProcess, INFINITE);
            if (GetExitCodeProcess(hProcess, out var raw)) code = (int)raw;
            log.Write($"cli exited code={code}");
            // Closing the pseudoconsole flushes its last output; the pump keeps reading meanwhile and ends at EOF.
            ClosePty();
            WaitForPump();
        }
        catch (Exception ex) { log.Error(ex, "watching the CLI failed; contained"); }
        // Closed even when watching failed, so a resize after the exit never reaches the pseudoconsole.
        try { ClosePty(); } catch (Exception ex) { log.Error(ex, "closing the pseudoconsole failed; contained"); }
        lock (gate)
        {
            exited = true;
            exitCode = code;
            MarkExited(code);
            Broadcast(TermFrameKind.Exit, TermProtocol.Exit(code));
        }
        Linger(code);
    }

    /// <summary>Waits for the output pump to reach EOF before Exit is broadcast (M.ORCH-T997), so the CLI's last output reaches
    /// the viewers ahead of Exit and isn't lost when the host leaves. Bounded: <see cref="PumpDrainMs"/>, and on to
    /// <see cref="PumpDrainMaxMs"/> while output still flows (none for <see cref="PumpQuietMs"/> ends it).</summary>
    private void WaitForPump()
    {
        if (pump is not { } p) return;
        var start = Environment.TickCount64;
        while (!p.Join(50))
        {
            var now = Environment.TickCount64;
            if (now - start >= PumpDrainMaxMs) break;
            if (now - start >= PumpDrainMs && now - Volatile.Read(ref lastOutputTicks) >= PumpQuietMs) break;
        }
        if (p.IsAlive) log.Write($"the output pump hasn't ended {Environment.TickCount64 - start}ms after the exit; sending Exit anyway");
    }

    /// <summary>Closes the pseudoconsole once (M.ORCH-T991): under <see cref="ptyGate"/> it is marked closed and its handle zeroed
    /// before the close, so a resize racing the exit finds it closed instead of calling ResizePseudoConsole on freed memory. The
    /// close itself runs outside the lock: it can wait for the output pump to drain.</summary>
    private void ClosePty()
    {
        IntPtr closing;
        lock (ptyGate)
        {
            ptyClosed = true;
            closing = hPC;
            hPC = IntPtr.Zero;
        }
        if (closing != IntPtr.Zero) ClosePseudoConsole(closing);
    }

    private void MarkExited(int code) =>
        registration?.Update(s => s with { State = TermSessionStates.Exited, ExitCode = code });

    private void Linger(int code)
    {
        try
        {
            log.Write($"exit code={code}; lingering up to {options.Linger.TotalSeconds:0}s for a viewer to see it");
            if (exitSeen.Wait(options.Linger))
            {
                log.Write("a viewer has seen the exit");
                // Let its writer finish the last frames before the pipe goes.
                Thread.Sleep(500);
            }
        }
        catch (Exception ex) { log.Error(ex, "lingering failed; contained"); }
        done.Set();
    }

    private NamedPipeServerStream NewServer(bool firstInstance = false) =>
        new(options.PipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly | (firstInstance ? PipeOptions.FirstPipeInstance : 0));

    private async Task AcceptLoopAsync()
    {
        while (!done.IsSet)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = Interlocked.Exchange(ref firstInstance, null) ?? NewServer();
                await server.WaitForConnectionAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log.Write("accept failed: " + ex.Message);
                server?.Dispose();
                await Task.Delay(500).ConfigureAwait(false);
                continue;
            }
            var connection = server;
            _ = Task.Run(() => ServeAsync(connection));
        }
    }

    private async Task ServeAsync(NamedPipeServerStream pipe)
    {
        ViewerLink? mine = null;
        try
        {
            (TermFrameKind Kind, byte[] Payload) hello;
            using (var timeout = new CancellationTokenSource(HelloTimeout))
                hello = await TermProtocol.ReadFrameAsync(pipe, timeout.Token).ConfigureAwait(false);
            if (hello.Kind != TermFrameKind.Hello || TermProtocol.ReadViewerHello(hello.Payload) is not { } h)
            {
                log.Write($"connection dropped: it opened with {hello.Kind}, not a Hello");
                return;
            }
            var agreed = TermProtocol.Negotiate(h.Protocol);
            mine = new ViewerLink(pipe, log) { Id = Interlocked.Increment(ref nextViewerId), Protocol = agreed ?? TermProtocol.Version };
            mine.ExitDelivered += exitSeen.Set;
            if (agreed is null)
            {
                log.Write($"viewer speaks protocol {h.Protocol}, this host {TermProtocol.Version}: answered and closed");
                ushort c, r;
                lock (gate) { c = cols; r = rows; }
                mine.Post(TermFrameKind.Hello, TermProtocol.Hello(TermProtocol.Version, c, r));
                mine.Complete();
                await mine.Writer.ConfigureAwait(false);
                return;
            }
            bool owns;
            lock (gate) owns = policy.Join(mine.Id, h.Interactive, h.Cols, h.Rows);
            log.Write($"viewer {mine.Id} connected: protocol {agreed}, {(h.Interactive ? "interactive" : "read-only")}" +
                      (h.ViewerId.Length > 0 ? $", id {TermProtocol.LogSafeId(h.ViewerId)}" : "") + (owns ? ", owns the size" : ""));
            // The owner's size first, so the replay below is taken at it.
            if (owns) ApplySize(h.Cols, h.Rows, ownerChanged: true);
            if (Attach(mine)) _ = Task.Run(() => Nudge(mine));
            await ReadViewerAsync(pipe, mine).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or ObjectDisposedException or InvalidDataException or OperationCanceledException) { }
        catch (Exception ex) { log.Error(ex, "serving a viewer failed; contained"); }
        finally
        {
            var handOver = false;
            if (mine is not null)
            {
                long? heir;
                lock (gate)
                {
                    viewers.Remove(mine);
                    handOver = policy.Leave(mine.Id);
                    heir = policy.Owner;
                }
                log.Write($"viewer {mine.Id} detached" + (handOver ? $"; the size goes to {(heir is { } o ? "viewer " + o : "nobody")}" : ""));
            }
            mine?.Dispose();
            try { pipe.Dispose(); } catch (Exception) { }
            if (handOver)
            {
                try { ApplyOwnerSize(ownerChanged: true); }
                catch (Exception ex) { log.Error(ex, "handing the size over failed; contained"); }
            }
        }
    }

    /// <summary>Adds <paramref name="link"/> to the viewers: sends it the session so far and subscribes it to the live stream,
    /// under the one lock the output pump takes, so no byte is lost or doubled between them. A protocol 2 viewer is told the
    /// size and whether it owns it before the replay, so it letterboxes before drawing. True when the replay came from the
    /// ring and wants the redraw nudge; the emulator's snapshot is exact at the session's size (M.ORCH-T929).</summary>
    private bool Attach(ViewerLink link)
    {
        lock (gate)
        {
            if (link.IsClosed) return false;
            var (data, reassert, exact) = ReplayScreen.Build(screen is { } s ? s.Snapshot : null, ring.ToArray, modes,
                ex => StopScreen(ex, "the emulator's snapshot failed"));
            // M.ORCH-T994: a replay over the viewer's backlog would drop it on every attach, so its oldest scrollback goes.
            var fitted = ReplayScreen.Fit(data, ReplayBudget - reassert.Length);
            if (fitted.Length != data.Length)
            {
                log.Write($"viewer {link.Id}: the replay ({data.Length}B) is over {ReplayBudget}B; its oldest scrollback is left out");
                data = fitted;
            }
            var ok = link.Post(TermFrameKind.Hello, TermProtocol.Hello(link.Protocol, cols, rows))
                     && (link.Protocol < TermProtocol.MultiViewer || link.Post(TermFrameKind.Size, SizeFor(link)))
                     && link.Post(TermFrameKind.Replay, TermProtocol.Replay(cols, rows, modes.Flags, data, reassert))
                     && link.Post(TermFrameKind.Live, [])
                     && (title is null || link.Post(TermFrameKind.Title, TermProtocol.Title(title)))
                     && (!exited || link.Post(TermFrameKind.Exit, TermProtocol.Exit(exitCode)));
            if (!ok) { Drop(link); return false; }
            viewers.Add(link);
            log.Write($"viewer {link.Id} attached cols={cols} rows={rows} replay={data.Length}B from the {(exact ? "emulator" : "ring")}; {viewers.Count} viewer(s)");
            return !exact;
        }
    }

    /// <summary>Called under <see cref="gate"/>: queues a frame for every viewer, dropping any that can't take it.</summary>
    private void Broadcast(TermFrameKind kind, ReadOnlySpan<byte> payload)
    {
        for (var i = viewers.Count - 1; i >= 0; i--)
        {
            var v = viewers[i];
            if (!v.Post(kind, payload)) Drop(v);
        }
    }

    /// <summary>Called under <see cref="gate"/>: tells every viewer the session's size, and each protocol 2 one whether it owns it.</summary>
    private void BroadcastSize()
    {
        for (var i = viewers.Count - 1; i >= 0; i--)
        {
            var v = viewers[i];
            if (!v.Post(TermFrameKind.Size, SizeFor(v))) Drop(v);
        }
    }

    /// <summary>Called under <see cref="gate"/>.</summary>
    private byte[] SizeFor(ViewerLink v) =>
        v.Protocol >= TermProtocol.MultiViewer ? TermProtocol.Size(cols, rows, policy.Owner == v.Id) : TermProtocol.Size(cols, rows);

    /// <summary>Starts the headless emulator (M.ORCH-T929) at the session's size. When it can't start, the ring serves alone.</summary>
    private void StartScreen()
    {
        try
        {
            var started = new HeadlessScreen(cols, rows);
            lock (gate) screen = started;
            log.Write($"emulator started (libghostty-vt) {cols}x{rows}, scrollback {started.ScrollbackLines} lines");
        }
        catch (Exception ex) { log.Error(ex, "the emulator couldn't start; the replay uses the raw ring"); }
    }

    /// <summary>Called under <see cref="gate"/>.</summary>
    private void FeedScreen(ReadOnlySpan<byte> chunk)
    {
        if (screen is null) return;
        try { screen.Feed(chunk); }
        catch (Exception ex) { StopScreen(ex, "the emulator failed on output"); }
    }

    /// <summary>Called under <see cref="gate"/>. Drops the emulator for good: once it has missed output it can't be exact.</summary>
    private void StopScreen(Exception ex, string what)
    {
        log.Error(ex, what + "; the replay uses the raw ring from now on");
        var dead = screen;
        screen = null;
        try { dead?.Dispose(); } catch (Exception) { }
    }

    private async Task ReadViewerAsync(Stream pipe, ViewerLink link)
    {
        while (!link.IsClosed)
        {
            var (kind, payload) = await TermProtocol.ReadFrameAsync(pipe).ConfigureAwait(false);
            switch (kind)
            {
                case TermFrameKind.Input:
                    OnInput(link, payload);
                    break;
                case TermFrameKind.Resize when TermProtocol.ReadSize(payload) is { } size:
                    bool apply;
                    lock (gate) apply = policy.Resize(link.Id, size.Cols, size.Rows);
                    if (apply) ApplySize(size.Cols, size.Rows, ownerChanged: false);
                    break;
                case TermFrameKind.TakeSize:
                    bool took;
                    lock (gate) took = policy.TakeSize(link.Id);
                    if (took)
                    {
                        log.Write($"viewer {link.Id} took the size");
                        ApplyOwnerSize(ownerChanged: true);
                    }
                    break;
                case TermFrameKind.Hello:
                    break;
                default:
                    log.Write($"ignored a {kind} frame from viewer {link.Id}");
                    break;
            }
        }
    }

    /// <summary>One Input frame (M.ORCH-T930): dropped from a read-only viewer; from an interactive one it makes that viewer the
    /// size's owner (its size applied first) and is written to the CLI, merged with everyone else's. Viewers typing within
    /// <see cref="TermProtocol.TypingWindow"/> of each other are each sent Typing. The terminal's own replies and focus reports
    /// (<see cref="AutoReplies"/>, M.ORCH-T993) are not typing: they pass only from the size's owner (or from anyone while nobody
    /// owns it), so the CLI gets one answer per query and the owner's focus, and they never move the ownership.</summary>
    private void OnInput(ViewerLink link, byte[] payload)
    {
        var typed = AutoReplies.Strip(payload, out var hasReplies);
        var verdict = new ViewerPolicy.InputVerdict(false, false, []);
        byte[] write;
        lock (gate)
        {
            if (!policy.IsInteractive(link.Id))
            {
                if (!link.InputDropLogged) log.Write($"viewer {link.Id} is read-only: its input is dropped");
                link.InputDropLogged = true;
                return;
            }
            if (typed.Length > 0) verdict = policy.Input(link.Id, DateTime.UtcNow);
            // After any typing in this frame the sender owns the size, so its replies here pass with it.
            write = !hasReplies || policy.Owner is null || policy.Owner == link.Id ? payload : typed;
            if (verdict.Colliding.Count > 0)
            {
                var now = DateTime.UtcNow;
                foreach (var v in viewers.ToArray())
                {
                    if (v.Protocol < TermProtocol.MultiViewer || !verdict.Colliding.Contains(v.Id) || now - v.LastTyping < TypingThrottle) continue;
                    v.LastTyping = now;
                    if (!v.Post(TermFrameKind.Typing, [])) Drop(v);
                }
            }
        }
        if (verdict.OwnerChanged)
        {
            log.Write($"viewer {link.Id} typed: it owns the size");
            ApplyOwnerSize(ownerChanged: true);
        }
        if (write.Length > 0) WriteInput(write);
    }

    /// <summary>
    /// The redraw nudge (M.ORCH-T927), after a ring replay only: one drawn at older sizes can garble the live region, so after it the ConPTY is
    /// resized to cols-1, then back to cols. Claude Code and other TUIs repaint on a resize, so the visible screen comes out
    /// right (the dtach trick). The session's size never changes, so no Size frame is sent.
    /// </summary>
    private void Nudge(ViewerLink link)
    {
        try
        {
            Thread.Sleep(150);
            // The narrow step under ptyGate with the size it is taken from (M.ORCH-T992), so a resize can't slip in between.
            lock (ptyGate)
            {
                ushort c, r;
                lock (gate) { if (!viewers.Contains(link) || exited || ptyClosed) return; c = cols; r = rows; }
                if (c <= 1) return;
                SetPtySize((ushort)(c - 1), r);
            }
            Thread.Sleep(150);
            lock (gate) { if (exited || ptyClosed) return; }
            // Back to the session's size as it is now, whatever resizes came meanwhile.
            SyncPtySize();
        }
        catch (Exception ex) { log.Error(ex, "the redraw nudge failed; contained"); }
    }

    /// <summary>Applies the owner's wanted size (<see cref="ViewerPolicy.OwnerSize"/>). With <paramref name="ownerChanged"/>, every
    /// viewer is told the size even when it stays the same, since who owns it changed.</summary>
    private void ApplyOwnerSize(bool ownerChanged)
    {
        (ushort Cols, ushort Rows)? wanted;
        lock (gate) wanted = policy.OwnerSize;
        if (wanted is { } w) ApplySize(w.Cols, w.Rows, ownerChanged);
        else if (ownerChanged) lock (gate) BroadcastSize();
    }

    /// <summary>
    /// Resizes the session to <paramref name="c"/> x <paramref name="r"/> (M.ORCH-T930). Like VS Code, output the CLI already
    /// wrote is flushed to the viewers first (<see cref="DrainOutput"/>), then every viewer is sent Size before the ConPTY is
    /// resized, so a letterboxed viewer draws the old output at the old size and the repaint at the new one.
    /// </summary>
    private void ApplySize(ushort c, ushort r, bool ownerChanged)
    {
        bool resize;
        lock (gate) resize = c > 0 && r > 0 && !exited && !ptyClosed && (c != cols || r != rows);
        if (resize) DrainOutput();
        lock (gate)
        {
            resize = c > 0 && r > 0 && !exited && !ptyClosed && (c != cols || r != rows);
            if (resize)
            {
                cols = c;
                rows = r;
                registration?.Update(s => s with { Cols = c, Rows = r });
                // The emulator follows the session's size (reflowing as a terminal would), so a snapshot is taken at it.
                if (screen is not null)
                {
                    try { screen.Resize(c, r); }
                    catch (Exception ex) { StopScreen(ex, "the emulator failed to resize"); }
                }
            }
            if (resize || ownerChanged) BroadcastSize();
        }
        // Outside gate, as the prototype did: the output pump must stay free to drain the repaint the resize causes. Through the
        // sequencer (M.ORCH-T992), so two racing resizes can't reach the ConPTY in the wrong order.
        if (resize) SyncPtySize();
    }

    /// <summary>The one resize sequencer (M.ORCH-T992): under <see cref="ptyGate"/> it reads the session's size as it is now and
    /// applies it to the ConPTY. Callers change cols and rows under <see cref="gate"/> first and then call this, so whichever
    /// reaches the ConPTY last applies the latest size: the PTY always ends at the size viewers, registry and emulator have.
    /// Lock order: ptyGate, then gate.</summary>
    private void SyncPtySize()
    {
        lock (ptyGate)
        {
            ushort c, r;
            lock (gate) { c = cols; r = rows; }
            SetPtySize(c, r);
        }
    }

    /// <summary>Lets output already on its way reach the viewers before a Size frame: waits, up to <see cref="DrainLimitMs"/>, for
    /// the pump to go <see cref="DrainQuietMs"/> without output. (Peeking at the ConPTY's pipe isn't possible: the pump's blocking
    /// read holds it, and a peek waits behind that read.) Never throws.</summary>
    private void DrainOutput()
    {
        try
        {
            var until = Environment.TickCount64 + DrainLimitMs;
            while (Environment.TickCount64 < until && Environment.TickCount64 - Volatile.Read(ref lastOutputTicks) < DrainQuietMs) Thread.Sleep(5);
        }
        catch (Exception ex) { log.Error(ex, "flushing output before a resize failed; contained"); }
    }

    /// <summary>Resizes the ConPTY, never once it is closed (M.ORCH-T991): the check and the call are under <see cref="ptyGate"/>,
    /// which the close takes too. To the session's size only through <see cref="SyncPtySize"/>.</summary>
    private void SetPtySize(ushort c, ushort r)
    {
        try
        {
            lock (ptyGate)
            {
                if (ptyClosed || hPC == IntPtr.Zero) return;
                var hr = ResizePseudoConsole(hPC, new COORD { X = (short)c, Y = (short)r });
                log.Write($"resize {c}x{r}" + (hr == 0 ? "" : $" failed hr=0x{hr:x}"));
            }
        }
        catch (Exception ex) { log.Error(ex, "resize failed; contained"); }
    }

    private void WriteInput(byte[] bytes)
    {
        lock (gate) { if (exited) return; }
        lock (inputGate)
        {
            try { ptyIn!.Write(bytes); ptyIn.Flush(); }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
        }
    }

    /// <summary>Called under <see cref="gate"/>.</summary>
    private void Drop(ViewerLink link)
    {
        // Its reader ends on the closed pipe, and hands the size over if it owned it (ServeAsync).
        if (viewers.Remove(link)) log.Write($"viewer {link.Id} dropped");
        link.Dispose();
    }
}
