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
    private bool loaded, disposed;

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
        Terminal.Loaded += (_, _) =>
        {
            try
            {
                Terminal.SetTheme(Theme, FontFamilyName, FontSizePoints);
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

    /// <summary>The pipe went away without this tab closing it, with why.</summary>
    public event Action<string>? Detached;

    /// <summary>Attaches to the host serving <paramref name="pipe"/>: at once when the renderer's window exists, else once it
    /// loads (output written before it exists would be lost).</summary>
    public void Attach(string pipe, int hostProtocol)
    {
        pipeName = pipe;
        protocol = TermProtocol.Supported.Contains((ushort)hostProtocol) ? (ushort)hostProtocol : TermProtocol.Version;
        Connect();
    }

    private void Connect()
    {
        if (disposed || !loaded || pipeName is null || Connection is not null) return;
        var connection = new RelayConnection(pipeName, protocol);
        connection.Exited += code => Exited?.Invoke(code);
        connection.Detached += why => Detached?.Invoke(why);
        Connection = connection;
        // Resets the renderer (ESC c), then Start()s the connection; the host's replay follows.
        Terminal.Connection = connection;
        // Phase 0 lesson 2: the renderer reported its grid before this connection existed, so tell the host the real one now.
        if (Terminal.Rows > 0 && Terminal.Columns > 0) connection.Resize((uint)Terminal.Rows, (uint)Terminal.Columns);
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
