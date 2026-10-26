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
/// One CLI on a ConPTY and the named pipe its viewer attaches to (M.ORCH-T924, promoted from the T923 prototype). One viewer
/// at a time: a new connection replaces the old. When the CLI exits the viewer is sent Exit, and the host lingers
/// (<see cref="HostOptions.Linger"/>) until a viewer has seen the exit, then ends. Nothing here throws out of its thread:
/// every failure is logged and contained.
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

    private IntPtr hPC, hProcess;
    private Stream? ptyIn, ptyOut;
    private Thread? pump;
    private NamedPipeServerStream? firstInstance;
    private ushort cols = 120, rows = 30;
    private ViewerLink? viewer;
    private bool exited;
    private int exitCode;
    private string? title;

    // M.ORCH-T927: the late-attach replay. Fed and copied only under gate.
    private readonly Ring ring = new();
    private readonly ModeTracker modes = new();
    private TermModes lastModes = TermModes.CursorVisible;

    // M.ORCH-T925: the registry file and the term-session announcements.
    private Registration? registration;

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
        try { registration = new Registration(options, commandLine, cols, rows, log); registration.Start(); }
        catch (Exception ex) { log.Error(ex, "registering the session failed; running unregistered"); registration = null; }

        var started = TryStartCli(commandLine);
        _ = Task.Run(AcceptLoopAsync);
        if (started)
        {
            new Thread(PumpOutput) { IsBackground = true, Name = "pty-out" }.Start();
            new Thread(WatchExit) { IsBackground = true, Name = "pty-exit" }.Start();
        }
        else new Thread(() => Linger(exitCode)) { IsBackground = true, Name = "linger" }.Start();

        done.Wait();
        lock (gate) { viewer?.Dispose(); viewer = null; }
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
            if (hPC != IntPtr.Zero) { try { ClosePseudoConsole(hPC); } catch (Exception) { } hPC = IntPtr.Zero; }
            lock (gate)
            {
                exited = true;
                exitCode = code == 0 ? -1 : code;
                MarkExited(exitCode);
                // What a viewer that attaches is shown, so it can tell why nothing ran.
                ring.Append(Encoding.UTF8.GetBytes($"\u001b[31mTermHost: the CLI couldn't start: {why} (error {code})\u001b[0m\r\n"));
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
        pump = Thread.CurrentThread;
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
        lock (gate)
        {
            ring.Append(chunk);
            modes.Feed(chunk);
            if (viewer is { } v && !v.Post(TermFrameKind.Output, chunk)) Drop(v);

            var flags = modes.Flags;
            if (flags != lastModes)
            {
                lastModes = flags;
                if (viewer is { } v2 && !v2.Post(TermFrameKind.Modes, TermProtocol.Modes(flags))) Drop(v2);
            }
            if (modes.Title is { } cliTitle && cliTitle != title)
            {
                title = cliTitle;
                registration?.Update(s => s with { Title = cliTitle });
                if (viewer is { } v3 && !v3.Post(TermFrameKind.Title, TermProtocol.Title(cliTitle))) Drop(v3);
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
            ClosePseudoConsole(hPC);
            for (var i = 0; i < 40 && pump is { IsAlive: true }; i++) Thread.Sleep(50);
        }
        catch (Exception ex) { log.Error(ex, "watching the CLI failed; contained"); }
        lock (gate)
        {
            exited = true;
            exitCode = code;
            MarkExited(code);
            if (viewer is { } v && !v.Post(TermFrameKind.Exit, TermProtocol.Exit(code))) Drop(v);
        }
        Linger(code);
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
            if (hello.Kind != TermFrameKind.Hello || TermProtocol.ReadHello(hello.Payload) is not { } h)
            {
                log.Write($"connection dropped: it opened with {hello.Kind}, not a Hello");
                return;
            }
            mine = new ViewerLink(pipe, log);
            mine.ExitDelivered += exitSeen.Set;
            if (h.Protocol != TermProtocol.Version)
            {
                log.Write($"viewer speaks protocol {h.Protocol}, this host {TermProtocol.Version}: answered and closed");
                mine.Post(TermFrameKind.Hello, TermProtocol.Hello(TermProtocol.Version, cols, rows));
                mine.Complete();
                await mine.Writer.ConfigureAwait(false);
                return;
            }
            ApplySize(h.Cols, h.Rows, announce: false);
            Attach(mine);
            _ = Task.Run(() => Nudge(mine));
            await ReadViewerAsync(pipe, mine).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or ObjectDisposedException or InvalidDataException or OperationCanceledException) { }
        catch (Exception ex) { log.Error(ex, "serving a viewer failed; contained"); }
        finally
        {
            lock (gate)
            {
                if (mine is not null && viewer == mine) { viewer = null; log.Write("viewer detached"); }
            }
            mine?.Dispose();
            try { pipe.Dispose(); } catch (Exception) { }
        }
    }

    /// <summary>Makes <paramref name="link"/> the viewer: sends it the session so far and subscribes it to the live stream, under
    /// the one lock the output pump takes, so no byte is lost or doubled between them.</summary>
    private void Attach(ViewerLink link)
    {
        lock (gate)
        {
            var old = viewer;
            viewer = link;
            if (old is not null) { old.Dispose(); log.Write("previous viewer replaced"); }
            var (data, reassert) = ReplayScreen.Build(ring.ToArray(), modes);
            var ok = link.Post(TermFrameKind.Hello, TermProtocol.Hello(TermProtocol.Version, cols, rows))
                     && link.Post(TermFrameKind.Replay, TermProtocol.Replay(cols, rows, modes.Flags, data, reassert))
                     && link.Post(TermFrameKind.Live, [])
                     && (title is null || link.Post(TermFrameKind.Title, TermProtocol.Title(title)))
                     && (!exited || link.Post(TermFrameKind.Exit, TermProtocol.Exit(exitCode)));
            if (!ok) { Drop(link); return; }
            log.Write($"viewer attached cols={cols} rows={rows} replay={data.Length}B");
        }
    }

    private async Task ReadViewerAsync(Stream pipe, ViewerLink link)
    {
        while (!link.IsClosed)
        {
            var (kind, payload) = await TermProtocol.ReadFrameAsync(pipe).ConfigureAwait(false);
            switch (kind)
            {
                case TermFrameKind.Input:
                    WriteInput(payload);
                    break;
                case TermFrameKind.Resize when TermProtocol.ReadSize(payload) is { } size:
                    ApplySize(size.Cols, size.Rows, announce: true);
                    break;
                case TermFrameKind.Hello:
                    break;
                default:
                    log.Write($"ignored a {kind} frame from the viewer");
                    break;
            }
        }
    }

    /// <summary>
    /// The redraw nudge (M.ORCH-T927): a replay drawn at older sizes can garble the live region, so after it the ConPTY is
    /// resized to cols-1, then back to cols. Claude Code and other TUIs repaint on a resize, so the visible screen comes out
    /// right (the dtach trick). The session's size never changes, so no Size frame is sent.
    /// </summary>
    private void Nudge(ViewerLink link)
    {
        try
        {
            Thread.Sleep(150);
            ushort c, r;
            lock (gate) { if (viewer != link || exited) return; c = cols; r = rows; }
            if (c > 1) { SetPtySize((ushort)(c - 1), r); Thread.Sleep(150); }
            lock (gate)
            {
                if (exited) return;
                // A resize in the meantime already repainted at its own size.
                if (c != cols || r != rows) return;
            }
            SetPtySize(c, r);
        }
        catch (Exception ex) { log.Error(ex, "the redraw nudge failed; contained"); }
    }

    private void ApplySize(ushort c, ushort r, bool announce)
    {
        if (c == 0 || r == 0) return;
        lock (gate)
        {
            if (exited || (c == cols && r == rows)) return;
            cols = c;
            rows = r;
            registration?.Update(s => s with { Cols = c, Rows = r });
        }
        // Outside the lock, as the prototype did: the output pump must stay free to drain the repaint the resize causes.
        SetPtySize(c, r);
        lock (gate)
        {
            if (announce && viewer is { } v && !v.Post(TermFrameKind.Size, TermProtocol.Size(c, r))) Drop(v);
        }
    }

    private void SetPtySize(ushort c, ushort r)
    {
        try
        {
            var hr = ResizePseudoConsole(hPC, new COORD { X = (short)c, Y = (short)r });
            log.Write($"resize {c}x{r}" + (hr == 0 ? "" : $" failed hr=0x{hr:x}"));
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
        if (viewer == link) { viewer = null; log.Write("viewer dropped"); }
        link.Dispose();
    }
}
