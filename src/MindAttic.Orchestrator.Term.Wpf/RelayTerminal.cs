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
    public const short FontSizePoints = 12;

    private string? pipeName;
    private ushort protocol = TermProtocol.Version;
    private Func<bool>? hostAlive;
    private bool interactive = true;
    private bool loaded, disposed;
    private Func<CancellationToken, Task<Stream>>? link;
    private string? viewerId;
    private short fontPoints = FontSizePoints;

    public RelayTerminal(TerminalTheme theme)
    {
        Theme = theme;
        Terminal = new TerminalControl { Focusable = true, AutoResize = true };
        var grid = new Grid();
        grid.Children.Add(Terminal);
        Content = grid;
        Focusable = true;
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Contained);
        KeyboardNavigation.SetDirectionalNavigation(this, KeyboardNavigationMode.Contained);
        // Only focus that lands on this control itself is handed on: the renderer's own GotFocus bubbles through here too.
        GotFocus += (_, e) => { if (ReferenceEquals(e.OriginalSource, this)) Terminal.Focus(); };
        // Focus on the renderer is this viewer being used: it takes the session's size (M.ORCH-T930, spike section 3).
        Terminal.GotFocus += (_, _) =>
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
        Terminal.SizeChanged += (_, _) =>
        {
            if (!IsVisible) return;
            try { Connection?.TakeSize(); }
            catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Taking the size on resize failed; contained"); }
        };
        Terminal.Loaded += (_, _) =>
        {
            try
            {
                Terminal.SetTheme(Theme, FontFamilyName, fontPoints);
                loaded = true;
                Connect();
            }
            catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Setting up a relay terminal failed; contained"); }
        };
    }

    public TerminalTheme Theme { get; }

    /// <summary>The renderer: its selection is what copy and cut take (T866/T867).</summary>
    public TerminalControl Terminal { get; }

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
            if (!loaded || disposed) return;
            try { Terminal.SetTheme(Theme, FontFamilyName, fontPoints); }
            catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Setting a relay terminal's font failed; contained"); }
        }
    }

    /// <summary>Handed each chunk of the CLI's live output as it is decoded, off the UI thread (M.ORCH-T1118,
    /// <see cref="RelayConnection.OutputTap"/>); it must be cheap.</summary>
    public Action<string>? OutputTap { get; set; }

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
        connection.SizeChanged += (c, r, owned) => FollowSize(connection, c, r, owned);
        connection.OtherTyping += () => OtherTyping?.Invoke();
        connection.InputDropped += () => InputDropped?.Invoke();
        // Each live chunk to whoever reads the screen (M.ORCH-T1118), on the reader's thread.
        connection.OutputTap = text => OutputTap?.Invoke(text);
        Connection = connection;
        // Resets the renderer (ESC c), then Start()s the connection; the host's replay follows.
        Terminal.Connection = connection;
        // Phase 0 lesson 2: the renderer reported its grid before this connection existed, so tell the host the real one now.
        if (Terminal.Rows > 0 && Terminal.Columns > 0) connection.Resize((uint)Terminal.Rows, (uint)Terminal.Columns);
    }

    /// <summary>
    /// The host said the session's size and whether this viewer owns it (M.ORCH-T930). Not owning it, the renderer stops
    /// sizing itself to the control (<see cref="TerminalControl.AutoResize"/> off) and shows the session's grid, letterboxed.
    /// Owning it again, the renderer takes the size the host just applied for it (the last one it reported) and sizes itself to
    /// the control from then on. On the UI thread; never throws.
    /// </summary>
    private void FollowSize(RelayConnection connection, ushort cols, ushort rows, bool owned)
    {
        try
        {
            if (disposed || !ReferenceEquals(connection, Connection) || cols == 0 || rows == 0) return;
            SessionSizeChanged?.Invoke(cols, rows);
            if (owned && Terminal.AutoResize) return;
            if (Terminal.Rows != rows || Terminal.Columns != cols || owned)
            {
                connection.SuppressResize = true;
                try
                {
                    Terminal.AutoResize = false;
                    _ = Terminal.ResizeAsync(rows, cols, CancellationToken.None).ContinueWith(
                        t => OrchestratorLog.For("Terminals").Error(t.Exception!, "Following the session's size failed; contained"),
                        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                }
                finally { connection.SuppressResize = false; }
            }
            Terminal.AutoResize = owned;
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
        try { Connection?.Close(); }
        catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Closing a relay connection failed; contained"); }
        try { if (loaded) Terminal.Connection = null!; }
        catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Detaching a relay terminal failed; contained"); }
        TerminalWindows.DisposeAll(this);
    }
}
