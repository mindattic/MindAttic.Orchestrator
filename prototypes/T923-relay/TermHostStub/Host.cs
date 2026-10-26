using System.Buffers.Binary;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using static TermHostStub.Native;

namespace TermHostStub;

/// <summary>
/// One CLI on a ConPTY, plus the named pipe a viewer attaches to (M.ORCH-T923). One viewer at a time; a new connection replaces
/// the old. On attach the ring is copied and the viewer subscribed under one lock, so no output byte is lost or doubled.
/// </summary>
internal sealed class Host
{
    private readonly string id, commandLine, pipeName;
    private readonly string? cwd;
    private readonly TimeSpan linger;
    private readonly object gate = new();
    private readonly Ring ring = new(4 * 1024 * 1024);
    private readonly ModeScanner modes = new();
    private readonly ManualResetEventSlim done = new();
    private readonly StreamWriter log;

    private IntPtr hPC, hProcess;
    private Stream? ptyIn;
    private ushort cols = 120, rows = 30;
    private Viewer? viewer;
    private bool exited;
    private int exitCode;
    private uint lastFlags = Protocol.CursorVisible;

    public Host(string id, string commandLine, string? cwd, TimeSpan linger)
    {
        this.id = id; this.commandLine = commandLine; this.cwd = cwd; this.linger = linger;
        pipeName = Protocol.PipeName(id);
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MindAttic", "termproto");
        Directory.CreateDirectory(dir);
        log = new StreamWriter(new FileStream(Path.Combine(dir, Protocol.Sanitize(id) + ".log"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
        { AutoFlush = true };
    }

    public void Log(string message)
    {
        lock (log) log.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {message}");
    }

    /// <summary>Runs until the CLI has exited and the linger is over.</summary>
    public int Run()
    {
        Log($"host start id={id} pid={Environment.ProcessId} pipe={pipeName} cmd={commandLine}");
        StartCli();
        _ = Task.Run(AcceptLoopAsync);
        new Thread(PumpOutput) { IsBackground = true, Name = "pty-out" }.Start();
        new Thread(WatchExit) { IsBackground = true, Name = "pty-exit" }.Start();
        done.Wait();
        Log("host exit");
        return 0;
    }

    private void StartCli()
    {
        if (!CreatePipe(out var inRead, out var inWrite, IntPtr.Zero, 0) || !CreatePipe(out var outRead, out var outWrite, IntPtr.Zero, 0))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        var hr = CreatePseudoConsole(new COORD { X = (short)cols, Y = (short)rows }, inRead, outWrite, 0, out hPC);
        if (hr != 0) throw new InvalidOperationException($"CreatePseudoConsole 0x{hr:x}");
        ptyIn = new FileStream(new SafeFileHandle(inWrite, true), FileAccess.Write, 1, false);
        ptyOut = new FileStream(new SafeFileHandle(outRead, true), FileAccess.Read, 1, false);

        var size = IntPtr.Zero;
        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
        var list = Marshal.AllocHGlobal(size);
        if (!InitializeProcThreadAttributeList(list, 1, 0, ref size)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        if (!UpdateProcThreadAttribute(list, 0, PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE, hPC, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

        var si = new STARTUPINFOEX { lpAttributeList = list };
        si.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();
        Environment.SetEnvironmentVariable("COLORTERM", "truecolor");
        if (!CreateProcessW(null, (commandLine + "\0").ToCharArray(), IntPtr.Zero, IntPtr.Zero, false,
                EXTENDED_STARTUPINFO_PRESENT | CREATE_UNICODE_ENVIRONMENT, IntPtr.Zero, cwd, ref si, out var pi))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "CreateProcess: " + commandLine);
        hProcess = pi.hProcess;
        CloseHandle(pi.hThread);
        DeleteProcThreadAttributeList(list);
        Marshal.FreeHGlobal(list);
        // The pseudoconsole owns its ends now.
        CloseHandle(inRead);
        CloseHandle(outWrite);
        Log($"cli started pid={pi.dwProcessId}");
    }

    private Stream? ptyOut;
    private Thread? pump;

    private void PumpOutput()
    {
        pump = Thread.CurrentThread;
        var buf = new byte[16384];
        try
        {
            int n;
            while ((n = ptyOut!.Read(buf, 0, buf.Length)) > 0) OnOutput(buf.AsSpan(0, n));
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
        Log("pty output ended");
    }

    private void OnOutput(ReadOnlySpan<byte> chunk)
    {
        lock (gate)
        {
            if (ring.Append(chunk)) modes.Reset();
            modes.Feed(chunk);
            if (viewer is { } v && !v.Send(Protocol.Output, chunk)) DropViewer(v);
            var flags = modes.Flags();
            if (flags != lastFlags)
            {
                lastFlags = flags;
                if (viewer is { } v2 && !v2.Send(Protocol.Modes, BitConverter.GetBytes(flags))) DropViewer(v2);
            }
        }
    }

    private void WatchExit()
    {
        WaitForSingleObject(hProcess, 0xFFFFFFFF);
        GetExitCodeProcess(hProcess, out var code);
        Log($"cli exited code={(int)code}");
        // Closing the pseudoconsole flushes its last output; the pump keeps reading meanwhile and ends at EOF.
        ClosePseudoConsole(hPC);
        for (var i = 0; i < 40 && pump is { IsAlive: true }; i++) Thread.Sleep(50);
        lock (gate)
        {
            exited = true;
            exitCode = (int)code;
            if (viewer is { } v && !v.Send(Protocol.Exit, BitConverter.GetBytes(exitCode))) DropViewer(v);
        }
        Log($"lingering {linger.TotalSeconds:0}s");
        Thread.Sleep(linger);
        done.Set();
    }

    private async Task AcceptLoopAsync()
    {
        while (true)
        {
            var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            try { await server.WaitForConnectionAsync().ConfigureAwait(false); }
            catch (Exception ex) { Log("accept failed: " + ex.Message); server.Dispose(); await Task.Delay(500); continue; }
            _ = Task.Run(() => ServeAsync(server));
        }
    }

    private async Task ServeAsync(NamedPipeServerStream pipe)
    {
        Viewer? mine = null;
        try
        {
            var (kind, hello) = await Protocol.ReadFrameAsync(pipe).ConfigureAwait(false);
            if (kind != Protocol.Hello || hello.Length < 4) return;
            ApplySize(BinaryPrimitives.ReadUInt16LittleEndian(hello), BinaryPrimitives.ReadUInt16LittleEndian(hello.AsSpan(2)));
            mine = new Viewer(pipe);
            lock (gate)
            {
                // Copy the ring and subscribe under this one lock: nothing is lost or doubled between them.
                var old = viewer;
                viewer = mine;
                old?.Dispose();
                var data = StripQueries(ring.Copy());
                var tail = Encoding.UTF8.GetBytes(modes.Reassert());
                var payload = new byte[12 + data.Length + tail.Length];
                BinaryPrimitives.WriteUInt16LittleEndian(payload, cols);
                BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2), rows);
                BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), modes.Flags());
                BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(8), (uint)data.Length);
                data.CopyTo(payload, 12);
                tail.CopyTo(payload, 12 + data.Length);
                if (!mine.Send(Protocol.Replay, payload) || !mine.Send(Protocol.Live, [])) { DropViewer(mine); return; }
                if (exited) mine.Send(Protocol.Exit, BitConverter.GetBytes(exitCode));
                Log($"viewer attached cols={cols} rows={rows} replay={data.Length}B");
            }
            // The replay may have garbled the live region (it was drawn at older sizes): a one-column wiggle makes the CLI repaint.
            if (!exited) _ = Task.Run(() => Nudge(mine));

            while (true)
            {
                var (k, p) = await Protocol.ReadFrameAsync(pipe).ConfigureAwait(false);
                if (k == Protocol.Input) { if (!exited) WriteInput(p); }
                else if (k == Protocol.Resize && p.Length >= 4)
                    ApplySize(BinaryPrimitives.ReadUInt16LittleEndian(p), BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(2)));
            }
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or ObjectDisposedException or InvalidDataException) { }
        finally
        {
            lock (gate) { if (mine is not null && viewer == mine) { viewer = null; Log("viewer detached"); } }
            pipe.Dispose();
        }
    }

    private void Nudge(Viewer v)
    {
        Thread.Sleep(150);
        ushort c, r;
        lock (gate) { if (viewer != v || exited) return; c = cols; r = rows; }
        if (c > 1) { SetPtySize((ushort)(c - 1), r); Thread.Sleep(150); }
        lock (gate) { if (exited) return; }
        SetPtySize(c, r);
    }

    private void ApplySize(ushort c, ushort r)
    {
        if (c == 0 || r == 0) return;
        lock (gate) { if (exited) return; cols = c; rows = r; }
        SetPtySize(c, r);
    }

    private void SetPtySize(ushort c, ushort r)
    {
        var hr = ResizePseudoConsole(hPC, new COORD { X = (short)c, Y = (short)r });
        Log($"resize {c}x{r} hr=0x{hr:x}");
    }

    private void WriteInput(byte[] bytes)
    {
        try { ptyIn!.Write(bytes); ptyIn.Flush(); }
        catch (IOException) { }
    }

    private void DropViewer(Viewer v)
    {
        if (viewer == v) viewer = null;
        v.Dispose();
    }

    /// <summary>The terminal queries conhost and TUIs ask at start (cursor position, device attributes): replayed, the viewer's
    /// renderer would answer them again and the answers would land in the CLI's input as typed text.</summary>
    private static byte[] StripQueries(byte[] data)
    {
        string[] queries = ["\u001b[6n", "\u001b[c", "\u001b[0c", "\u001b[>c", "\u001b[>0c", "\u001b[?6n", "\u001b[5n"];
        var text = Encoding.Latin1.GetString(data);
        var changed = false;
        foreach (var q in queries)
            if (text.Contains(q, StringComparison.Ordinal)) { text = text.Replace(q, ""); changed = true; }
        return changed ? Encoding.Latin1.GetBytes(text) : data;
    }

    private sealed class Viewer(Stream pipe) : IDisposable
    {
        public bool Send(byte kind, ReadOnlySpan<byte> payload)
        {
            try { pipe.Write(Protocol.Frame(kind, payload)); return true; }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException) { return false; }
        }

        public void Dispose() { try { pipe.Dispose(); } catch { } }
    }
}
