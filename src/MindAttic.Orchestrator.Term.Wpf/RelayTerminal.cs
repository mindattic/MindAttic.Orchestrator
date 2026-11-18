using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Terminal.Wpf;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Term.Wpf;

/// <summary>
/// A Terminals tab's view of a CLI hosted by its own TermHost (M.ORCH-T926): a bare Microsoft.Terminal.Wpf
/// <see cref="TerminalControl"/> driven by a <see cref="RelayConnection"/> to the host's pipe, instead of EasyTerminalControl,
/// which always starts a local process. It keeps what the tab took from EasyTerminalControl: the Campbell theme with its font,
/// keyboard navigation contained (Tab and the arrows are the CLI's), and focus handed on to the renderer. Closing or disposing
/// it only drops the pipe: the CLI keeps running in its host, and a new RelayTerminal reattaches to it.
/// </summary>
public sealed class RelayTerminal : UserControl, IDisposable
{
    /// <summary>The font EasyTerminalControl set with its theme.</summary>
    public const string FontFamilyName = "Cascadia Code";
    /// <summary>The font size points constant.</summary>
    public const short FontSizePoints = 12;

    private string? pipeName;
    private ushort protocol = TermProtocol.Version;
    private Func<bool>? hostAlive;
    private bool interactive = true;
    private bool loaded, disposed;
    private Func<CancellationToken, Task<Stream>>? link;
    private string? viewerId;
    private short fontPoints = FontSizePoints;
    private Size? letterboxedAt;
    private int sizeGeneration;

    // The renderer the connection is set on (its TerminalOutput feeds it); null while the renderer is released (M.ORCH-T1737)
    // or before the connection exists. A renderer made on show differs from it until it loads and takes the connection.
    private TerminalControl? boundRenderer;
    private readonly Grid grid = new();
    private System.Windows.Threading.DispatcherTimer? releaseTimer;

    /// <summary>Creates the relay terminal.</summary>
    public RelayTerminal(TerminalTheme theme)
    {
        Theme = theme;
        Terminal = NewRenderer();
        grid.Children.Add(Terminal);
        Content = grid;
        Focusable = true;
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Contained);
        KeyboardNavigation.SetDirectionalNavigation(this, KeyboardNavigationMode.Contained);
        // Only focus that lands on this control itself is handed on: the renderer's own GotFocus bubbles through here too.
        GotFocus += (_, e) => { if (ReferenceEquals(e.OriginalSource, this)) Terminal.Focus(); };
    }

    /// <summary>A renderer wired to this viewer: the one made with it, or one made again when a released tab is shown
    /// (M.ORCH-T1737). Once loaded it is themed and takes the connection (a new one, or the paused one kept).</summary>
    private TerminalControl NewRenderer()
    {
        var renderer = new TerminalControl { Focusable = true, AutoResize = true };
        // Focus on the renderer is this viewer being used: it takes the session's size (M.ORCH-T930, spike section 3).
        renderer.GotFocus += (_, _) =>
        {
            try { Connection?.TakeSize(); }
            catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Taking the size failed; contained"); }
        };
        // A reattached pane (the host, and its session, outlive a closed tab or a redeployed dashboard, M.ORCH-T926) can come
        // back without ever owning the size: a viewer that joined while the old one hadn't been cleaned up yet letterboxes
        // (FollowSize below) and, without a click into it, GotFocus above never fires to reclaim it. Left that way, the row
        // growing (M.ORCH-T1208 floored it at half the window) only grows the dead margin around the still-letterboxed
        // renderer, not the renderer itself (M.ORCH-T1266). So the hosting row actually changing size is treated as this
        // viewer being used too: TakeSize() is a no-op once it already owns the size or isn't interactive, and this pane's own
        // visibility is checked since every pane in TerminalPanesHost's overlay is measured and arranged (SizeChanged fires)
        // even while another tab is the one shown.
        // M.ORCH-T1604: only a row that actually GROWS past what it was when this viewer lost the size reclaims it. Any layout
        // pass (including one landing just after the loss) used to reclaim at once, so two viewers could ping-pong the size and
        // a letterbox lasted only an instant.
        renderer.SizeChanged += (_, e) =>
        {
            if (!IsVisible || !ReferenceEquals(renderer, boundRenderer)) return;
            // M.ORCH-T1631: the loss (OwnsSize false) is set on the read thread, letterboxedAt only once FollowSizeAsync runs on the UI thread;
            // a layout pass in between used to reclaim at once, so the letterbox never showed (the flaky letterboxed-viewer test).
            if (Connection is { OwnsSize: false } && (letterboxedAt is not { } at
                || (e.NewSize.Width <= at.Width && e.NewSize.Height <= at.Height))) return;
            try { Connection?.TakeSize(); }
            catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Taking the size on resize failed; contained"); }
        };
        renderer.Loaded += (_, _) =>
        {
            try
            {
                if (disposed || !ReferenceEquals(renderer, Terminal)) return;
                renderer.SetTheme(Theme, FontFamilyName, fontPoints);
                loaded = true;
                if (Connection is { } kept)
                {
                    if (!ReferenceEquals(renderer, boundRenderer)) Rebind(renderer, kept);
                }
                else Connect();
            }
            catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Setting up a relay terminal failed; contained"); }
        };
        return renderer;
    }

    /// <summary>A renderer made again for a released tab (M.ORCH-T1737) takes the kept connection: the connection's own
    /// <see cref="RelayConnection.Start"/> sees it is running already and only re-asserts the input mode, and unpausing it now
    /// attaches again for the host's whole screen, since what it held was forgotten with the old renderer. UI thread.</summary>
    private void Rebind(TerminalControl renderer, RelayConnection connection)
    {
        boundRenderer = renderer;
        letterboxedAt = null;
        renderer.Connection = connection;
        if (renderer.Rows > 0 && renderer.Columns > 0) connection.Resize((uint)renderer.Rows, (uint)renderer.Columns);
        connection.Paused = paused;
        // Focus that landed on this control while the renderer was still being made is handed on now.
        if (IsKeyboardFocused) renderer.Focus();
        // Hidden again before it loaded: the release clock starts over.
        ArmRelease();
        OrchestratorLog.For("Terminals").Information("{Pipe:l}: made its renderer again on show; redrawing from the host's screen", pipeName);
    }

    /// <summary>How long this viewer may stay <see cref="Paused"/> before its renderer is released (M.ORCH-T1737): the native
    /// terminal window and its buffers (about 50 MB) are freed, the connection stays (paused, reading on, the taps working), and
    /// showing it again makes a new renderer that redraws the host's screen. Zero (the default) never releases it. Read when
    /// it is paused. UI thread.</summary>
    public TimeSpan ReleaseHiddenAfter { get; set; }

    /// <summary>Whether its renderer is released right now (M.ORCH-T1737).</summary>
    public bool IsRendererReleased => Connection is not null && boundRenderer is null;

    private void ArmRelease()
    {
        releaseTimer?.Stop();
        if (!paused || disposed || ReleaseHiddenAfter <= TimeSpan.Zero || boundRenderer is null) return;
        if (releaseTimer is null)
        {
            releaseTimer = new System.Windows.Threading.DispatcherTimer();
            releaseTimer.Tick += (_, _) =>
            {
                releaseTimer.Stop();
                ReleaseRenderer();
            };
        }
        releaseTimer.Interval = ReleaseHiddenAfter;
        releaseTimer.Start();
    }

    /// <summary>Releases the renderer of a viewer still paused (M.ORCH-T1737): its connection forgets what it held (the catch-up
    /// becomes the host's whole screen) and is unhooked from it, then the renderer leaves the tree and its native window is
    /// disposed. Not while the host can't be asked for its screen any more (an ended CLI keeps its last lines). UI thread;
    /// never throws.</summary>
    internal bool ReleaseRenderer()
    {
        try
        {
            if (disposed || !paused || Connection is not { } connection || boundRenderer is not { } renderer
                || !ReferenceEquals(renderer, Terminal) || !TerminalWindows.AreLive(renderer)) return false;
            if (!connection.ForgetScreen()) return false;
            boundRenderer = null;
            letterboxedAt = null;
            try { renderer.Connection = null!; }
            catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Unhooking a hidden renderer failed; contained"); }
            // Out of the window first: its native window is released only once it's no longer shown.
            grid.Children.Remove(renderer);
            TerminalWindows.DisposeAll(renderer);
            OrchestratorLog.For("Terminals").Information("{Pipe:l}: hidden {Minutes:0.#} min; released its renderer", pipeName, ReleaseHiddenAfter.TotalMinutes);
            return true;
        }
        catch (Exception ex)
        {
            OrchestratorLog.For("Terminals").Error(ex, "Releasing a hidden renderer failed; contained");
            return false;
        }
    }

    /// <summary>A released renderer is made again (M.ORCH-T1737); it takes the connection once it loads (<see cref="Rebind"/>).</summary>
    private void RestoreRenderer()
    {
        try
        {
            Terminal = NewRenderer();
            grid.Children.Add(Terminal);
        }
        catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Making a released renderer again failed; contained"); }
    }

    /// <summary>The renderer's selected text, or null while it has no live window (released, or not yet made): reading a
    /// renderer whose window is gone is a native access violation (M.ORCH-T1613).</summary>
    public string? ReadSelection() => TerminalWindows.AreLive(Terminal) ? Terminal.GetSelectedText() : null;

    /// <summary>The theme.</summary>
    public TerminalTheme Theme { get; }

    /// <summary>The renderer: its selection is what copy and cut take (T866/T867). A tab hidden long enough releases it and makes
    /// a new one when shown (M.ORCH-T1737, <see cref="ReleaseHiddenAfter"/>), so it is read fresh, never kept.</summary>
    public TerminalControl Terminal { get; private set; }

    /// <summary>The live connection; null before <see cref="Attach"/> and the renderer's window exist.</summary>
    public RelayConnection? Connection { get; private set; }

    /// <summary>The host's tracked modes (bracketed paste decides how a paste is sent).</summary>
    public TermModes Modes => Connection?.Modes ?? TermModes.None;

    /// <summary>The CLI exited, with its code.</summary>
    public event Action<int>? Exited;

    /// <summary>The pipe went away for good without this tab closing it, with why.</summary>
    public event Action<string>? Detached;

    /// <summary>The pipe dropped while the host lives and is being attached again (M.ORCH-T966): true then, false once the
    /// live stream is back.</summary>
    public event Action<bool>? ReconnectingChanged;

    /// <summary>Another viewer of the same CLI typed within two seconds of this one (M.ORCH-T930).</summary>
    public event Action? OtherTyping;

    /// <summary>Keys or a paste were dropped, not sent, because the link is down and being attached again (M.ORCH-T979). On the
    /// UI thread.</summary>
    public event Action? InputDropped;

    /// <summary>Attaches to the host serving <paramref name="pipe"/>: at once when the renderer's window exists, else once it
    /// loads (output written before it exists would be lost). While <paramref name="alive"/> says the host lives, a dropped
    /// link is attached again (M.ORCH-T966); it is called off the UI thread.</summary>
    public void Attach(string pipe, int hostProtocol, Func<bool>? alive = null, bool interactive = true)
    {
        this.interactive = interactive;
        pipeName = pipe;
        hostAlive = alive;
        protocol = TermProtocol.Supported.Contains((ushort)hostProtocol) ? (ushort)hostProtocol : TermProtocol.Version;
        Connect();
    }

    /// <summary>
    /// Attaches over any frame stream <paramref name="connect"/> opens (M.ORCH-T936): a team gateway's WebSocket, or a pipe for a
    /// viewer that names itself (<paramref name="viewer"/>, the id its Hello carries). <paramref name="name"/> is what logs call
    /// the link. Otherwise as <see cref="Attach"/>.
    /// </summary>
    public void AttachLink(string name, Func<CancellationToken, Task<Stream>> connect, int hostProtocol, Func<bool>? alive = null,
        bool interactive = true, string? viewer = null)
    {
        this.interactive = interactive;
        pipeName = name;
        link = connect;
        viewerId = viewer;
        hostAlive = alive;
        protocol = TermProtocol.Supported.Contains((ushort)hostProtocol) ? (ushort)hostProtocol : TermProtocol.Version;
        Connect();
    }

    /// <summary>The renderer's font size in points (12 by default). A Team Wall tile sets it small so a whole session's grid
    /// fits (M.ORCH-T936); a renderer that exists already is re-themed at once. Never throws.</summary>
    public short FontPoints
    {
        get => fontPoints;
        set
        {
            var points = Math.Clamp(value, (short)3, (short)72);
            if (points == fontPoints) return;
            fontPoints = points;
            if (!loaded || disposed || !TerminalWindows.AreLive(Terminal)) return;
            try { Terminal.SetTheme(Theme, FontFamilyName, fontPoints); }
            catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Setting a relay terminal's font failed; contained"); }
        }
    }

    /// <summary>Handed each chunk of the CLI's live output as it is decoded, off the UI thread (M.ORCH-T1118,
    /// <see cref="RelayConnection.OutputTap"/>); it must be cheap.</summary>
    public Action<string>? OutputTap { get; set; }

    /// <summary>Handed each piece of input this viewer sent (M.ORCH-T1413, <see cref="RelayConnection.InputTap"/>).</summary>
    public Action<string>? InputTap { get; set; }

    /// <summary>Whether this viewer is out of sight (M.ORCH-T1734, <see cref="RelayConnection.Paused"/>): it draws nothing and
    /// resizes nothing until it is shown again, then catches up. Kept for a connection made later. Paused past
    /// <see cref="ReleaseHiddenAfter"/>, its renderer is released, and made again when it is shown (M.ORCH-T1737). UI thread.</summary>
    public bool Paused
    {
        get => paused;
        set
        {
            paused = value;
            try
            {
                if (!value && IsRendererReleased && !disposed) RestoreRenderer();
                // A renderer made again takes the connection (and unpauses it) only once it loads: output drawn before that
                // would reach no renderer.
                if (Connection is { } c && boundRenderer is not null && ReferenceEquals(boundRenderer, Terminal)) c.Paused = value;
                ArmRelease();
            }
            catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Pausing a relay terminal failed; contained"); }
        }
    }

    private bool paused;

    /// <summary>The session's size (cols, rows) as the host last said it (protocol 2 hosts).</summary>
    public event Action<ushort, ushort>? SessionSizeChanged;

    private void Connect()
    {
        if (disposed || !loaded || pipeName is null || Connection is not null) return;
        var connection = link is { } open
            ? new RelayConnection(pipeName, open, protocol, hostAlive, interactive, viewerId)
            : new RelayConnection(pipeName, protocol, hostAlive, interactive);
        connection.Exited += code => Exited?.Invoke(code);
        connection.Detached += why => { ReconnectingChanged?.Invoke(false); Detached?.Invoke(why); };
        connection.Reconnecting += _ => ReconnectingChanged?.Invoke(true);
        connection.Attached += () => ReconnectingChanged?.Invoke(false);
        connection.SizeChanged += (c, r, owned) => _ = FollowSizeAsync(connection, c, r, owned);
        connection.OtherTyping += () => OtherTyping?.Invoke();
        connection.InputDropped += () => InputDropped?.Invoke();
        // Each live chunk to whoever reads the screen (M.ORCH-T1118), on the reader's thread.
        connection.OutputTap = text => OutputTap?.Invoke(text);
        connection.InputTap = data => InputTap?.Invoke(data);
        connection.Paused = paused;
        Connection = connection;
        boundRenderer = Terminal;
        // Resets the renderer (ESC c), then Start()s the connection; the host's replay follows.
        Terminal.Connection = connection;
        ArmRelease();
        // Phase 0 lesson 2: the renderer reported its grid before this connection existed, so tell the host the real one now.
        if (Terminal.Rows > 0 && Terminal.Columns > 0) connection.Resize((uint)Terminal.Rows, (uint)Terminal.Columns);
    }

    /// <summary>
    /// The host said the session's size and whether this viewer owns it (M.ORCH-T930). Not owning it, the renderer stops
    /// sizing itself to the control (<see cref="TerminalControl.AutoResize"/> off) and shows the session's grid, letterboxed.
    /// Owning it again, the renderer takes the size the host just applied for it (the last one it reported) and sizes itself to
    /// the control from then on. On the UI thread; never throws.
    /// </summary>
    private async Task FollowSizeAsync(RelayConnection connection, ushort cols, ushort rows, bool owned)
    {
        try
        {
            if (disposed || !ReferenceEquals(connection, Connection) || cols == 0 || rows == 0) return;
            SessionSizeChanged?.Invoke(cols, rows);
            if (!TerminalWindows.AreLive(Terminal)) return;
            // M.ORCH-T1646: calls overlap (each awaits the renderer's resize); an older one finishing last used to set AutoResize
            // from its stale 'owned', undoing a newer loss/gain. Only the newest call applies the final AutoResize.
            var generation = ++sizeGeneration;
            letterboxedAt = owned ? null : new Size(Terminal.ActualWidth, Terminal.ActualHeight);
            if (owned && Terminal.AutoResize) return;
            if (Terminal.Rows != rows || Terminal.Columns != cols || owned)
            {
                connection.SuppressResize = true;
                try
                {
                    Terminal.AutoResize = false;
                    // Awaited (bughunt 2026-10-28): resetting SuppressResize right after firing this off, instead of once it
                    // actually finished, made the suppression window effectively zero, letting this resize echo back to the host.
                    await Terminal.ResizeAsync(rows, cols, CancellationToken.None);
                }
                catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Following the session's size failed; contained"); }
                finally { connection.SuppressResize = false; }
            }
            if (generation == sizeGeneration) Terminal.AutoResize = owned;
        }
        catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Following the session's size failed; contained"); }
    }

    /// <summary>Sends <paramref name="data"/> to the CLI as typed input (keys or a paste payload).</summary>
    public void WriteInput(string data) => Connection?.WriteInput(data);

    /// <summary>Drops the pipe (the host and its CLI keep running) and releases the renderer's window. Never throws.</summary>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        releaseTimer?.Stop();
        try { Connection?.Close(); }
        catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Closing a relay connection failed; contained"); }
        try { if (loaded && boundRenderer is { } bound) bound.Connection = null!; }
        catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Detaching a relay terminal failed; contained"); }
        TerminalWindows.DisposeAll(this);
    }
}
