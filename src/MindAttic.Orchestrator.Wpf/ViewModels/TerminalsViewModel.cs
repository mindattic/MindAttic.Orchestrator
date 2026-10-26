using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using EasyWindowsTerminalControl;
using EasyWindowsTerminalControl.Internals;
using Microsoft.Terminal.Wpf;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// One CLI running in a person's Terminals panel (M.ORCH-T810): a true terminal, the Windows Terminal renderer
/// (Microsoft.Terminal.Wpf, GPU drawn, full ANSI and 24-bit color) over ConPTY, through EasyWindowsTerminalControl, so an
/// interactive CLI like Claude Code renders as it does in Windows Terminal. Plain stream redirection can't: it needs a TTY.
/// Its tab carries the login's initials badge and color, as its Windows Terminal tab would (M.ORCH-T616).
/// </summary>
public sealed class TerminalPaneViewModel : ObservableObject
{
    private bool isSelected, ended;

    public TerminalPaneViewModel(PaneRequest request, string? pipeName = null)
    {
        Request = request;
        Control = new EasyTerminalControl
        {
            StartupCommandLine = request.ProcessCommandLine(pipeName),
            WorkingDirectory = request.Directory,
            // Windows Terminal's own Campbell scheme: without a theme the renderer's 16-color table isn't set, and the CLI's
            // colors were lost (what M.ORCH-T751 removed the old panes over).
            Theme = Campbell,
            Visibility = Visibility.Hidden,
        };
        System.Windows.Automation.AutomationProperties.SetName(Control, request.Title);
        TabBrush = request.TabColor is { } hex && TryBrush(hex) is { } brush ? brush : Brushes.Gray;
        BadgeForeground = ReadableOn(TabBrush);
        Login = request.Login;
    }

    /// <summary>Windows Terminal's default Campbell scheme, as the renderer's COLORREFs (0x00BBGGRR).</summary>
    internal static readonly TerminalTheme Campbell = new()
    {
        DefaultBackground = Ref(0x0C0C0C),
        DefaultForeground = Ref(0xCCCCCC),
        DefaultSelectionBackground = Ref(0xFFFFFF),
        CursorStyle = CursorStyle.BlinkingBar,
        ColorTable =
        [
            Ref(0x0C0C0C), Ref(0xC50F1F), Ref(0x13A10E), Ref(0xC19C00), Ref(0x0037DA), Ref(0x881798), Ref(0x3A96DD), Ref(0xCCCCCC),
            Ref(0x767676), Ref(0xE74856), Ref(0x16C60C), Ref(0xF9F1A5), Ref(0x3B78FF), Ref(0xB4009E), Ref(0x61D6D6), Ref(0xF2F2F2),
        ],
    };

    /// <summary>"#RRGGBB" as a Win32 COLORREF.</summary>
    internal static uint Ref(int rgb) => (uint)(((rgb & 0xFF) << 16) | (rgb & 0xFF00) | ((rgb >> 16) & 0xFF));

    private static Brush? TryBrush(string hex)
    {
        try { return new BrushConverter().ConvertFromInvariantString(hex) as Brush; }
        catch (Exception ex) when (ex is FormatException or NotSupportedException) { return null; }
    }

    public PaneRequest Request { get; }

    /// <summary>The person whose page it's on: its CLI's login's email, null for the global login. The login it was opened on
    /// until the dashboard sees its CLI on another (moved with "Move to").</summary>
    public string? Login { get; internal set; }

    /// <summary>The terminal itself: kept for the tab's whole life, since a new control would start a new process.</summary>
    public EasyTerminalControl Control { get; }

    public string Title => Request.Title;
    public string Badge => Request.Initials is { Length: > 0 } i ? $"{i}:" : "";
    public string Name => Request.IsMenu ? SessionControl.ShortName(Request.Name) : $"{SessionControl.ShortName(Request.Name)} [{Request.Provider}]";
    public string Tooltip => $"{Request.Title}\n{Request.Directory}\n{Login ?? "global login"}";
    public Brush TabBrush { get; }

    /// <summary>The badge's text color: black or white, whichever reads on the chip of <see cref="TabBrush"/>.</summary>
    public Brush BadgeForeground { get; }

    /// <summary>Black on a light <paramref name="background"/>, white on a dark one (by relative luminance).</summary>
    internal static Brush ReadableOn(Brush background)
    {
        if (background is not SolidColorBrush { Color: var c }) return Brushes.White;
        static double Lin(byte v) { var x = v / 255.0; return x <= 0.03928 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); }
        var lum = 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B);
        return lum > 0.179 ? Brushes.Black : Brushes.White;
    }

    /// <summary>The Launcher host process running in it, once found (for "Go to tab").</summary>
    public int? HostPid { get; internal set; }

    /// <summary>Its Claude session and claude.exe pid, once the snapshot matches this pane to one (for Undock, M.ORCH-Tnnn):
    /// <c>--resume</c> reopens exactly this conversation on the other hosting surface.</summary>
    public string? SessionId { get; internal set; }
    public int? CliPid { get; internal set; }

    /// <summary>Set once Undock has been requested for this pane, so its context menu can say so instead of offering it
    /// again (a second Undock before the first lands is refused by <see cref="RelocateLocks"/> anyway, but the menu
    /// should not invite it).</summary>
    internal bool Relocating { get; set; }

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (!Set(ref isSelected, value)) return;
            // Hidden, not collapsed: a hidden terminal keeps its size, so its CLI's lines don't rewrap at every tab switch.
            Control.Visibility = value ? Visibility.Visible : Visibility.Hidden;
        }
    }

    /// <summary>Its CLI has exited; the tab stays up, showing its last lines, until closed.</summary>
    public bool Ended { get => ended; internal set { if (Set(ref ended, value)) Raise(nameof(StateText)); } }

    public string StateText => Ended ? "ended" : "";

    /// <summary>The process ConPTY started (the PowerShell that runs the command); null before it starts.</summary>
    internal int? RootPid => (Control.ConPTYTerm?.Process as ProcessFactory.WrappedProcess)?.Pid;

    internal bool HasExited
    {
        get
        {
            try { return Control.ConPTYTerm?.Process is { } p && p.HasExited; }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { return true; }
        }
    }

    /// <summary>Ends its CLI: the whole tree under the tab's process.</summary>
    internal void Kill()
    {
        // A process that's already gone is a no-op, however it reports that (it can exit just before the kill).
        try { Control.ConPTYTerm?.Process?.Kill(true); }
        catch (Exception) { }
    }
}

/// <summary>
/// Every terminal open in the dashboard's Terminals panels (M.ORCH-T810), whoever's: one list for the window, on the global
/// login's tab. Each person's page shows its own (<see cref="PersonTerminalsViewModel"/>); the terminals themselves all live in
/// the panel's one host grid, since a native terminal can't be re-templated without starting over, and only the shown person's
/// chosen one is visible (<see cref="SelectedPane"/>).
/// </summary>
public sealed class TerminalsViewModel : ObservableObject
{
    private readonly string? pipeName;
    private TerminalPaneViewModel? selected;

    public TerminalsViewModel(string? pipeName = null)
    {
        this.pipeName = pipeName;
        CloseCommand = new RelayCommand(p => { if (p is TerminalPaneViewModel pane) Close(pane); });
    }

    public ObservableCollection<TerminalPaneViewModel> Panes { get; } = [];

    /// <summary>The one terminal on screen, the shown person's chosen one; null shows none.</summary>
    public TerminalPaneViewModel? SelectedPane
    {
        get => selected;
        set
        {
            if (value is not null && !Panes.Contains(value)) return;
            if (selected == value) return;
            if (selected is not null) selected.IsSelected = false;
            selected = value;
            if (selected is not null)
            {
                selected.IsSelected = true;
                // Switching tabs (or opening one) should hand the keyboard straight to the CLI now on screen, not
                // leave it wherever it last was (e.g. the tab strip itself): queued past the Visibility flip
                // above, since the terminal's native window isn't actually shown until that layout pass runs.
                var pane = selected;
                pane.Control.Dispatcher.InvokeAsync(() => { if (ReferenceEquals(selected, pane)) pane.Control.Focus(); }, DispatcherPriority.Input);
            }
            Raise();
        }
    }

    /// <summary>A terminal was just opened: the window brings it up, on its person's page.</summary>
    public event Action<TerminalPaneViewModel>? Opened;

    /// <summary>A terminal's CLI was seen on another login than the one it was on (moved): it goes to that person's page.</summary>
    public event Action? LoginsChanged;

    public ICommand CloseCommand { get; }

    /// <summary>Opens <paramref name="request"/> as a new terminal tab and brings it up; returns an error, or null.</summary>
    public string? Open(PaneRequest request)
    {
        if (request.Problem() is { } problem) return problem;
        TerminalPaneViewModel pane;
        try { pane = new TerminalPaneViewModel(request, pipeName); }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            OrchestratorLog.For("Terminals").Error(ex, "Couldn't open a terminal for {Repo:l}", request.Name);
            return $"Couldn't open a terminal: {ex.Message}";
        }
        Panes.Add(pane);
        Opened?.Invoke(pane);
        return null;
    }

    /// <summary>Closes <paramref name="pane"/>, ending its CLI.</summary>
    public void Close(TerminalPaneViewModel pane)
    {
        pane.Kill();
        if (SelectedPane == pane) SelectedPane = null;
        Panes.Remove(pane);
    }

    /// <summary>The live terminal running Launcher host <paramref name="hostPid"/>; null when none does.</summary>
    public TerminalPaneViewModel? Find(int hostPid) => Panes.FirstOrDefault(p => p.HostPid == hostPid && !p.Ended);

    /// <summary>At each refresh: a terminal whose CLI has exited is marked ended; a live one learns its Launcher host, and
    /// follows its CLI to the login it runs on now.</summary>
    public void Update(Snapshot? snapshot)
    {
        if (Panes.Count == 0) return;
        var hosts = snapshot?.Repos.SelectMany(r => r.Hosts).ToList() ?? [];
        var moved = false;
        foreach (var pane in Panes.Where(p => !p.Ended).ToList())
        {
            if (pane.HasExited)
            {
                pane.Ended = true;
                continue;
            }
            if (snapshot is null || pane.RootPid is not { } root) continue;
            pane.HostPid ??= hosts.FirstOrDefault(h => h.ProcessId == root || ProcessTree.IsAncestor(root, h.ProcessId))?.ProcessId;
            if (pane.HostPid is not { } host) continue;
            if (snapshot.Agents.FirstOrDefault(a => a.Host?.ProcessId == host) is not { SessionId: { Length: > 0 } session } row) continue;
            pane.SessionId ??= session;
            pane.CliPid ??= row.CliPid;
            // Its person is the login its CLI is on now: moved to another, it moves to that one's page.
            var login = LoginScope.LoginOf(snapshot, session);
            if (!string.Equals(login, pane.Login, StringComparison.OrdinalIgnoreCase))
            {
                pane.Login = login;
                moved = true;
            }
        }
        if (moved) LoginsChanged?.Invoke();
    }
}

/// <summary>
/// One person's Terminals panel (M.ORCH-T810), between their Current work and their Tickets: the terminals of the shared
/// <see cref="TerminalsViewModel"/> whose CLI runs on this login, one tab each, and the one last chosen here.
/// </summary>
public sealed class PersonTerminalsViewModel : ObservableObject
{
    private readonly TerminalsViewModel all;
    private TerminalPaneViewModel? selected;

    public PersonTerminalsViewModel(TerminalsViewModel all) => this.all = all;

    /// <summary>This person's terminals, in the order they opened.</summary>
    public ObservableCollection<TerminalPaneViewModel> Panes { get; } = [];

    /// <summary>The terminal this person's panel shows.</summary>
    public TerminalPaneViewModel? SelectedPane
    {
        get => selected;
        // Ctrl+click on the selected tab deselects it, writing null: it stays on the one it was.
        set
        {
            if (value is null && Panes.Count > 0) return;
            if (value is not null && !Panes.Contains(value)) return;
            if (!Set(ref selected, value)) return;
            SelectionChanged?.Invoke();
        }
    }

    public bool HasPanes => Panes.Count > 0;

    /// <summary>How many terminals this person has open, in words.</summary>
    public string Summary => Panes.Count switch { 0 => "", 1 => "1 open", var n => $"{n} open" };

    /// <summary>Raised when this person's chosen terminal changes, so the window shows it.</summary>
    internal event Action? SelectionChanged;

    public ICommand CloseCommand => all.CloseCommand;

    private ICommand? nextCommand, previousCommand;

    /// <summary>Ctrl+Left and Ctrl+Right (M.ORCH-T824): the next and the previous CLI tab of this person's, cycling, so
    /// switching works even while a terminal holds keyboard focus (plain Left/Right there move the CLI's own cursor).</summary>
    public ICommand NextCommand => nextCommand ??= new RelayCommand(() => Step(+1));
    public ICommand PreviousCommand => previousCommand ??= new RelayCommand(() => Step(-1));

    private void Step(int by)
    {
        if (Panes.Count < 2) return;
        var at = selected is null ? -1 : Panes.IndexOf(selected);
        SelectedPane = Panes[((at + by) % Panes.Count + Panes.Count) % Panes.Count];
    }

    /// <summary>Makes <see cref="Panes"/> exactly <paramref name="mine"/>, in its order. The chosen one stays chosen while it's
    /// still here; gone, its neighbor is chosen; and one arriving on an empty panel is chosen.</summary>
    internal void SyncTo(IReadOnlyList<TerminalPaneViewModel> mine)
    {
        var at = selected is null ? -1 : Panes.IndexOf(selected);
        for (var i = Panes.Count - 1; i >= 0; i--)
            if (!mine.Contains(Panes[i])) Panes.RemoveAt(i);
        for (var i = 0; i < mine.Count; i++)
        {
            var now = Panes.IndexOf(mine[i]);
            if (now < 0) Panes.Insert(i, mine[i]);
            else if (now != i) Panes.Move(now, i);
        }
        if (selected is not null && !Panes.Contains(selected))
        {
            selected = null;
            if (Panes.Count > 0) selected = Panes[Math.Clamp(at, 0, Panes.Count - 1)];
            Raise(nameof(SelectedPane));
            SelectionChanged?.Invoke();
        }
        else if (selected is null && Panes.Count > 0) SelectedPane = Panes[^1];
        Raise(nameof(HasPanes));
        Raise(nameof(Summary));
    }
}
