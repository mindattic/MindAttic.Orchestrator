using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using EasyWindowsTerminalControl;
using EasyWindowsTerminalControl.Internals;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// One CLI running inside the dashboard (M.ORCH-T618): the Windows Terminal renderer (Microsoft.Terminal.Wpf, through
/// EasyWindowsTerminalControl) over ConPTY, running the pane's Launcher host command. Its tab carries the login's initials
/// badge and color, as a Windows Terminal tab would (M.ORCH-T616).
/// </summary>
public sealed class TerminalPaneViewModel : ObservableObject
{
    private bool isSelected, ended;
    private PaneRecord record;

    public TerminalPaneViewModel(PaneRequest request, PaneRecord record)
    {
        Request = request;
        this.record = record;
        Control = new EasyTerminalControl
        {
            StartupCommandLine = request.CommandLine,
            WorkingDirectory = request.Directory,
            Visibility = Visibility.Hidden,
        };
        System.Windows.Automation.AutomationProperties.SetName(Control, request.Title);
        TabBrush = request.TabColor is { } hex && new BrushConverter().ConvertFromString(hex) is Brush brush ? brush : Brushes.Gray;
        BadgeForeground = ReadableOn(TabBrush);
        Login = LoginOf(request.ConfigDir);
    }

    /// <summary>The per-CLI login a <c>CLAUDE_CONFIG_DIR</c> folder is (its folder name, the login's email); null for none, the global login.</summary>
    internal static string? LoginOf(string? configDir) =>
        string.IsNullOrEmpty(configDir) ? null : Path.GetFileName(Path.TrimEndingDirectorySeparator(configDir));

    /// <summary>The person whose Terminals tab it's on (M.ORCH-T622): its CLI's login's email, null for the global login.
    /// The login it was opened on until the dashboard sees its CLI on another (moved with "Move to").</summary>
    public string? Login { get; internal set; }

    /// <summary>claude.exe running in it, once the dashboard has seen it (for "Move to").</summary>
    public int? CliPid { get; internal set; }

    /// <summary>Its CLI's move in flight (M.ORCH-T638): the tab's controls stay locked under "Moving to …" until it lands.</summary>
    public bool IsMoving => PendingMoves.For(Record.SessionId) is not null;
    public bool IsUnlocked => !IsMoving;
    public string MovingText => PendingMoves.For(Record.SessionId) is { } m ? m.Text : "";
    public void RaiseMoving() { Raise(nameof(IsMoving)); Raise(nameof(IsUnlocked)); Raise(nameof(MovingText)); }

    public PaneRequest Request { get; }

    /// <summary>As kept in the store; its session is filled in once the dashboard sees its CLI's.</summary>
    public PaneRecord Record
    {
        get => record;
        internal set
        {
            if (!Set(ref record, value)) return;
            Raise(nameof(Tooltip));
            RaiseMoving();
        }
    }

    /// <summary>The terminal itself: kept for the pane's whole life, since a new control would start a new process.</summary>
    public EasyTerminalControl Control { get; }

    public string Title => Request.Title;
    public string Badge => Request.Initials is { } i ? $"{i}:" : "";
    public string Name => $"{SessionControl.ShortName(Request.Repo)} [{Request.Provider}]";
    public Brush TabBrush { get; }

    /// <summary>The badge's text color (M.ORCH-T621): black or white, whichever reads on the chip of <see cref="TabBrush"/>.</summary>
    public Brush BadgeForeground { get; }

    /// <summary>Black on a light <paramref name="background"/>, white on a dark one (by relative luminance).</summary>
    internal static Brush ReadableOn(Brush background)
    {
        if (background is not SolidColorBrush { Color: var c }) return Brushes.White;
        static double Lin(byte v) { var x = v / 255.0; return x <= 0.03928 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); }
        var lum = 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B);
        return lum > 0.179 ? Brushes.Black : Brushes.White;
    }
    public string Tooltip => $"{Request.Directory}\n{Record.Login}" + (Record.SessionId is { } s ? $"\nsession {s}" : "");

    /// <summary>The Launcher host process running in it, once found (for "Go to tab").</summary>
    public int? HostPid { get; internal set; }

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (!Set(ref isSelected, value)) return;
            // Hidden, not collapsed: a hidden terminal keeps its size, so its CLI's lines don't rewrap at every tab switch.
            Control.Visibility = value ? Visibility.Visible : Visibility.Hidden;
            if (value) Control.Dispatcher.BeginInvoke(() => Control.Focus());
        }
    }

    /// <summary>Its CLI has exited; the pane stays up, showing its last lines, until closed.</summary>
    public bool Ended { get => ended; internal set { if (Set(ref ended, value)) Raise(nameof(StateText)); } }

    public string StateText => Ended ? "ended" : "";

    /// <summary>The process ConPTY started (the Launcher host, or PowerShell setting the login first); null before it starts.</summary>
    internal int? RootPid => (Control.ConPTYTerm?.Process as ProcessFactory.WrappedProcess)?.Pid;

    internal bool HasExited
    {
        get
        {
            try { return Control.ConPTYTerm?.Process is { } p && p.HasExited; }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { return true; }
        }
    }

    /// <summary>Ends its CLI: the whole tree under the pane's process, the Launcher host and the CLI it runs.</summary>
    internal void Kill()
    {
        // A process that's already gone is a no-op, however it reports that (it can exit just before the kill).
        try { Control.ConPTYTerm?.Process?.Kill(true); }
        catch (Exception) { }
    }
}

/// <summary>
/// The Terminals view (M.ORCH-T618): the CLIs the Orchestrator opened inside itself while "Where CLIs open" is Orchestrator,
/// one pane tab each. Each pane is recorded (<see cref="PaneRecords"/>) with its repo, login and Claude session as soon as
/// they're known; a pane closed here, or whose CLI exits, is dropped from the record. Those still recorded when the dashboard
/// opens were open when it last closed (the panes died with it), and are offered again, resumed with <c>claude --resume</c>.
/// </summary>
public sealed class TerminalsViewModel : ObservableObject
{
    private readonly SessionControl control;
    private readonly JsonStore store;
    private TerminalPaneViewModel? selected;
    private IReadOnlyList<PaneRecord> previous;

    public TerminalsViewModel(SessionControl control, JsonStore store)
    {
        this.control = control;
        this.store = store;
        previous = PaneRecords.Load(store);
        CloseCommand = new RelayCommand(p => { if (p is TerminalPaneViewModel pane) Close(pane); });
        ReopenCommand = new RelayCommand(Reopen);
        DismissReopenCommand = new RelayCommand(() => Forget(previous));
    }

    public ObservableCollection<TerminalPaneViewModel> Panes { get; } = [];

    /// <summary>The one pane on screen, the shown person's chosen one (M.ORCH-T622); null shows none.</summary>
    public TerminalPaneViewModel? SelectedPane
    {
        get => selected;
        set
        {
            if (value is not null && !Panes.Contains(value)) return;
            if (selected == value) return;
            if (selected is not null) selected.IsSelected = false;
            selected = value;
            if (selected is not null) selected.IsSelected = true;
            Raise();
        }
    }

    public bool HasPanes => Panes.Count > 0;

    /// <summary>A pane was just opened: the window shows it, on its person's Terminals tab.</summary>
    public event Action<TerminalPaneViewModel>? Opened;

    /// <summary>A pane's CLI was seen on another login than the one it was on (moved): it goes to that person's Terminals tab.</summary>
    public event Action? LoginsChanged;

    public ICommand CloseCommand { get; }
    public ICommand ReopenCommand { get; }
    public ICommand DismissReopenCommand { get; }

    // ── Offered again from last time ─────────────────────────────────────────

    public bool HasReopen => previous.Count > 0;

    public string ReopenText => previous.Count switch
    {
        0 => "",
        1 => $"1 terminal was open when the Orchestrator last closed: {Describe(previous[0])}.",
        var n => $"{n} terminals were open when the Orchestrator last closed: {string.Join("; ", previous.Select(Describe))}.",
    };

    private static string Describe(PaneRecord r) => $"{r.Repo} ({r.Provider}, {r.Login}{(r.SessionId is null ? ", no session seen" : "")})";

    private void Reopen()
    {
        var old = previous;
        Forget(old);
        foreach (var record in old)
        {
            if (!Directory.Exists(record.Directory)) continue;
            Open(control.ResumeRequest(record));
        }
    }

    private void Forget(IReadOnlyList<PaneRecord> records)
    {
        previous = [];
        try { PaneRecords.Remove(store, [.. records.Select(r => r.Id)]); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { OrchestratorLog.For("Terminals").Warning(ex, "Couldn't forget the closed pane record(s); they may be offered for Reopen next start"); }
        Raise(nameof(HasReopen));
        Raise(nameof(ReopenText));
    }

    // ── Panes ───────────────────────────────────────────────────────────────

    /// <summary>Opens <paramref name="request"/> as a new pane and shows it; returns an error, or null.</summary>
    public string? Open(PaneRequest request) => Open(request, show: true);

    /// <summary>Opens <paramref name="request"/> as a new pane; <paramref name="show"/> brings it up, else it waits on its
    /// person's Terminals tab without taking the window from whatever is on screen. Returns an error, or null.</summary>
    private string? Open(PaneRequest request, bool show)
    {
        var record = new PaneRecord(Guid.NewGuid().ToString("N"), request.Repo, request.Directory, request.Provider, DateTime.UtcNow)
        {
            ConfigDir = request.ConfigDir,
            SessionId = request.ResumeSession,
        };
        TerminalPaneViewModel pane;
        try { pane = new TerminalPaneViewModel(request, record); }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            OrchestratorLog.For("Terminals").Error(ex, "Couldn't open a terminal pane for {Repo:l}", request.Repo);
            return $"Couldn't open a terminal pane: {ex.Message}";
        }
        // A login's Launcher tab isn't offered again from last time: every login gets its own anew (M.ORCH-T699).
        if (!request.IsLauncher) Save(record);
        Panes.Add(pane);
        if (show) SelectedPane = pane;
        Raise(nameof(HasPanes));
        if (show) Opened?.Invoke(pane);
        return null;
    }

    /// <summary>The live Launcher tab of <paramref name="login"/> (its email; null for the global login), if it has one open.</summary>
    public TerminalPaneViewModel? LauncherOf(string? login) =>
        Panes.FirstOrDefault(p => p.Request.IsLauncher && !p.Ended && !p.HasExited && string.Equals(p.Login ?? "", login ?? "", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// A login's own terminal tab (M.ORCH-T699): the Launcher signed in to it (<paramref name="request"/>, from
    /// <see cref="SessionControl.LauncherPaneRequest"/>). A login that already has one open keeps it: it's reused, never a
    /// second. <paramref name="show"/> brings it up, opened or reused; without it a new one waits quietly on its person's
    /// Terminals tab. Returns an error, or null.
    /// </summary>
    public string? OpenLauncher(PaneRequest request, bool show)
    {
        if (LauncherOf(TerminalPaneViewModel.LoginOf(request.ConfigDir)) is { } open)
        {
            if (show) { SelectedPane = open; Opened?.Invoke(open); }
            return null;
        }
        return Open(request with { IsLauncher = true }, show);
    }

    /// <summary>Closes <paramref name="pane"/>, ending its CLI.</summary>
    public void Close(TerminalPaneViewModel pane)
    {
        pane.Kill();
        Drop(pane.Record);
        var at = Panes.IndexOf(pane);
        Panes.Remove(pane);
        if (SelectedPane == pane)
        {
            selected = null;
            SelectedPane = Panes.Count == 0 ? null : Panes[Math.Min(at, Panes.Count - 1)];
            Raise(nameof(SelectedPane));
        }
        Raise(nameof(HasPanes));
    }

    /// <summary>The live pane running Launcher host <paramref name="hostPid"/>; null when none does.</summary>
    public TerminalPaneViewModel? Find(int hostPid) => Panes.FirstOrDefault(p => p.HostPid == hostPid && !p.Ended);

    /// <summary>At each refresh: a pane whose CLI has exited is marked ended and dropped from the record; a live one learns
    /// its Launcher host and the Claude session that host's CLI is on, and records the session.</summary>
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
                Drop(pane.Record);
                continue;
            }
            if (snapshot is null || pane.RootPid is not { } root) continue;
            pane.HostPid ??= hosts.FirstOrDefault(h => h.ProcessId == root || ProcessTree.IsAncestor(root, h.ProcessId))?.ProcessId;
            if (pane.HostPid is not { } host) continue;
            if (snapshot.Agents.FirstOrDefault(a => a.Host?.ProcessId == host) is not { } agent) continue;
            pane.CliPid = agent.CliPid ?? pane.CliPid;
            if (agent.SessionId is not { Length: > 0 } session) continue;
            if (session != pane.Record.SessionId)
            {
                pane.Record = pane.Record with { SessionId = session };
                Save(pane.Record);
            }
            // Its person is the login its CLI is on now: moved to another, it moves to that one's Terminals tab.
            var login = LoginScope.LoginOf(snapshot, session);
            if (!string.Equals(login, pane.Login, StringComparison.OrdinalIgnoreCase))
            {
                pane.Login = login;
                moved = true;
            }
        }
        if (moved) LoginsChanged?.Invoke();
    }

    private void Save(PaneRecord record)
    {
        try { PaneRecords.Put(store, record); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            OrchestratorLog.For("Terminals").Warning(ex, "Couldn't record the pane for {Repo:l}", record.Repo);
        }
    }

    private void Drop(PaneRecord record)
    {
        try { PaneRecords.Remove(store, [record.Id]); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { OrchestratorLog.For("Terminals").Warning(ex, "Couldn't drop the closed pane record; it may be offered for Reopen next start"); }
    }
}

/// <summary>
/// One person's Terminals tab (M.ORCH-T622): the panes of the shared <see cref="TerminalsViewModel"/> whose CLI runs on this
/// login's dashboard tab, one terminal tab each, and the one last chosen here. The terminals themselves stay in the window's
/// one host grid (a native terminal can't change parents without starting over); only the shown person's chosen pane shows.
/// </summary>
public sealed class PersonTerminalsViewModel : ObservableObject
{
    private readonly TerminalsViewModel all;
    private TerminalPaneViewModel? selected;

    public PersonTerminalsViewModel(TerminalsViewModel all)
    {
        this.all = all;
        all.PropertyChanged += OnAllChanged;
    }

    private void OnAllChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TerminalsViewModel.HasReopen) or nameof(TerminalsViewModel.ReopenText)) Raise(e.PropertyName);
    }

    /// <summary>Stops following the shared terminals, for a login tab removed on sign-out (M.ORCH-T734).</summary>
    public void Detach() => all.PropertyChanged -= OnAllChanged;

    /// <summary>This person's panes, in the order they opened.</summary>
    public ObservableCollection<TerminalPaneViewModel> Panes { get; } = [];

    /// <summary>The pane this person's Terminals tab shows.</summary>
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

    /// <summary>How many terminals this person has open, in words, for the Dashboard/Terminals strip's tooltip and screen readers.</summary>
    public string Summary => Panes.Count == 0 ? "No terminals open" : Panes.Count == 1 ? "1 terminal open" : $"{Panes.Count} terminals open";

    /// <summary>Raised when this person's chosen pane changes, so the window shows it.</summary>
    internal event Action? SelectionChanged;

    public ICommand CloseCommand => all.CloseCommand;
    public ICommand ReopenCommand => all.ReopenCommand;
    public ICommand DismissReopenCommand => all.DismissReopenCommand;
    public bool HasReopen => all.HasReopen;
    public string ReopenText => all.ReopenText;

    /// <summary>Makes <see cref="Panes"/> exactly <paramref name="mine"/>, in its order. The chosen pane stays chosen while it's
    /// still here; gone, its neighbor is chosen; and a pane arriving on an empty tab is chosen.</summary>
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
