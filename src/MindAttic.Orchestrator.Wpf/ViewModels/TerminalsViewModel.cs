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

    public TerminalPaneViewModel(PaneRequest request, PaneRecord record)
    {
        Request = request;
        Record = record;
        Control = new EasyTerminalControl
        {
            StartupCommandLine = request.CommandLine,
            WorkingDirectory = request.Directory,
            Visibility = Visibility.Hidden,
        };
        System.Windows.Automation.AutomationProperties.SetName(Control, request.Title);
        TabBrush = request.TabColor is { } hex && new BrushConverter().ConvertFromString(hex) is Brush brush ? brush : Brushes.Gray;
    }

    public PaneRequest Request { get; }

    /// <summary>As kept in the store; its session is filled in once the dashboard sees its CLI's.</summary>
    public PaneRecord Record { get; internal set; }

    /// <summary>The terminal itself: kept for the pane's whole life, since a new control would start a new process.</summary>
    public EasyTerminalControl Control { get; }

    public string Title => Request.Title;
    public string Badge => Request.Initials is { } i ? $"[{i}]" : "";
    public string Name => $"{Request.Repo} [{Request.Provider}]";
    public Brush TabBrush { get; }
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
        try { Control.ConPTYTerm?.Process?.Kill(true); }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or AggregateException or NotSupportedException) { }
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

    public TerminalPaneViewModel? SelectedPane
    {
        get => selected;
        // Ctrl+click on the selected tab deselects it, writing null: it stays on the one it was.
        set
        {
            if (value is null && Panes.Count > 0) return;
            if (selected == value) return;
            if (selected is not null) selected.IsSelected = false;
            selected = value;
            if (selected is not null) selected.IsSelected = true;
            Raise();
        }
    }

    public bool HasPanes => Panes.Count > 0;

    /// <summary>A pane was just opened: the window shows the Terminals view.</summary>
    public event Action? Opened;

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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { }
        Raise(nameof(HasReopen));
        Raise(nameof(ReopenText));
    }

    // ── Panes ───────────────────────────────────────────────────────────────

    /// <summary>Opens <paramref name="request"/> as a new pane and shows it; returns an error, or null.</summary>
    public string? Open(PaneRequest request)
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
        Save(record);
        Panes.Add(pane);
        SelectedPane = pane;
        Raise(nameof(HasPanes));
        Opened?.Invoke();
        return null;
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

    /// <summary>Selects the pane running Launcher host <paramref name="hostPid"/>; false when none does.</summary>
    public bool Select(int hostPid)
    {
        if (Panes.FirstOrDefault(p => p.HostPid == hostPid && !p.Ended) is not { } pane) return false;
        SelectedPane = pane;
        return true;
    }

    /// <summary>At each refresh: a pane whose CLI has exited is marked ended and dropped from the record; a live one learns
    /// its Launcher host and the Claude session that host's CLI is on, and records the session.</summary>
    public void Update(Snapshot? snapshot)
    {
        if (Panes.Count == 0) return;
        var hosts = snapshot?.Repos.SelectMany(r => r.Hosts).ToList() ?? [];
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
            if (snapshot.Agents.FirstOrDefault(a => a.Host?.ProcessId == host)?.SessionId is { Length: > 0 } session && session != pane.Record.SessionId)
            {
                pane.Record = pane.Record with { SessionId = session };
                Save(pane.Record);
            }
        }
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { }
    }
}
