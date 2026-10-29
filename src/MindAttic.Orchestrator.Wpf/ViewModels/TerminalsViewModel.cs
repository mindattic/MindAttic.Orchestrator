using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using EasyWindowsTerminalControl;
using EasyWindowsTerminalControl.Internals;
using Microsoft.Terminal.Wpf;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Term;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// One CLI in a person's Terminals panel (M.ORCH-T810): a true terminal, the Windows Terminal renderer (Microsoft.Terminal.Wpf,
/// GPU drawn, full ANSI and 24-bit color), so an interactive CLI like Claude Code renders as it does in Windows Terminal.
/// <para>The CLI lives in its own TermHost process (M.ORCH-T926): the tab is a <see cref="RelayTerminal"/> attached to the host's
/// pipe, so a dashboard crash, restart or redeploy never kills it, and the next dashboard reattaches the tab. When no host is
/// installed or one fails to start, the tab falls back to an in-process EasyTerminalControl over ConPTY, as before T926.</para>
/// Its tab carries the login's initials badge and color, as its Windows Terminal tab would (M.ORCH-T616).
/// </summary>
public sealed class TerminalPaneViewModel : ObservableObject
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, TerminalPaneViewModel> ByControl = new();
    private bool isSelected, ended, hostEnded, killRequested, reconnecting, otherTyping, inputNotSent;
    private System.Windows.Threading.DispatcherTimer? typingTimer, notSentTimer;
    private string? pipeName;
    private TermModes localModes;

    /// <summary>A tab that runs <paramref name="request"/> in the dashboard's own process (the fallback, and what tests build);
    /// with <paramref name="hosted"/> its terminal waits for <see cref="Attach"/> to a TermHost instead.</summary>
    public TerminalPaneViewModel(PaneRequest request, string? pipeName = null, bool hosted = false)
    {
        Request = request;
        this.pipeName = pipeName;
        Control = new TerminalView { Visibility = Visibility.Hidden };
        ByControl.AddOrUpdate(Control, this);
        System.Windows.Automation.AutomationProperties.SetName(Control, request.Title);
        TabBrush = request.TabColor is { } hex && TryBrush(hex) is { } brush ? brush : Brushes.Gray;
        Login = request.Login;
        if (hosted) UseRelay();
        else UseLocal();
    }

    /// <summary>A tab reattached to a live hosted session (dashboard start, or one started elsewhere).</summary>
    internal TerminalPaneViewModel(PaneRequest request, TermSessionRecord session) : this(request, hosted: true)
    {
        Attach(session);
        if (session.Exited) Ended = true;
    }

    /// <summary>Windows Terminal's default Campbell scheme (shared with the standalone TermViewer, M.ORCH-T960).</summary>
    internal static TerminalTheme Campbell => TerminalThemes.Campbell;

    /// <summary>"#RRGGBB" as a Win32 COLORREF.</summary>
    internal static uint Ref(int rgb) => TerminalThemes.Ref(rgb);

    private static Brush? TryBrush(string hex)
    {
        try { return new BrushConverter().ConvertFromInvariantString(hex) as Brush; }
        catch (Exception ex) when (ex is FormatException or NotSupportedException) { return null; }
    }

    /// <summary>The tab whose <see cref="Control"/> is <paramref name="element"/>, or holds it; null for none.</summary>
    internal static TerminalPaneViewModel? Of(DependencyObject? element)
    {
        // A ContentElement (a ticket Hyperlink) makes VisualTreeHelper.GetParent throw, so walk it logically (bughunt 2026-10-27).
        for (var at = element; at is not null;
             at = at is Visual or System.Windows.Media.Media3D.Visual3D
                 ? VisualTreeHelper.GetParent(at) ?? LogicalTreeHelper.GetParent(at)
                 : LogicalTreeHelper.GetParent(at))
            if (at is FrameworkElement fe && ByControl.TryGetValue(fe, out var pane)) return pane;
        return null;
    }

    public PaneRequest Request { get; }

    /// <summary>The person whose page it's on: its CLI's login's email, null for the global login. The login it was opened on
    /// until the dashboard sees its CLI on another (moved with "Move to").</summary>
    public string? Login { get; internal set; }

    /// <summary>The tab's element in the panel's host grid, kept for the tab's whole life; it holds <see cref="Relay"/> or
    /// <see cref="Local"/>.</summary>
    public FrameworkElement Control { get; }

    /// <summary>The terminal attached to the CLI's TermHost; null for an in-process tab.</summary>
    internal RelayTerminal? Relay { get; private set; }

    /// <summary>The in-process fallback terminal; null for a hosted tab.</summary>
    internal EasyTerminalControl? Local { get; private set; }

    /// <summary>Whether its CLI runs in a TermHost (it outlives the dashboard).</summary>
    public bool IsHosted => Relay is not null;

    /// <summary>The hosted session's id, set before its host starts; null for an in-process tab.</summary>
    public string? TermId { get; internal set; }

    /// <summary>The hosted session once its host registered (or the one reattached to).</summary>
    public TermSessionRecord? Session { get; private set; }

    public string Title => Request.Title;

    public string Name => Request.IsMenu ? SessionControl.ShortName(Request.Name) : $"{SessionControl.ShortName(Request.Name)}{SessionControl.ProviderTag(Request.Provider)}";
    public string Tooltip => $"{Request.Title}\n{Request.Directory}\n{Request.Alias ?? Login ?? "global login"}";
    public Brush TabBrush { get; }

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

    /// <summary>The Claude session its CLI is in now, once seen (M.ORCH-T862): what the session-restore file resumes it on.
    /// A restored tab starts with the session it resumes.</summary>
    public string? SessionId { get; internal set; }

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

    /// <summary>Its CLI has exited; the tab stays up, showing its last lines, until closed (by hand, or pruned once stale,
    /// M.ORCH-T1177).</summary>
    public bool Ended
    {
        get => ended;
        internal set
        {
            if (!Set(ref ended, value)) return;
            EndedAtUtc = value ? DateTime.UtcNow : null;
            Raise(nameof(StateText));
        }
    }

    /// <summary>When this tab last became <see cref="Ended"/> (M.ORCH-T1177): what <see cref="TerminalsViewModel.PruneEnded"/>
    /// measures a long-idle, ended tab's age against; null while it's live, cleared again on <see cref="Revive"/>.</summary>
    public DateTime? EndedAtUtc { get; private set; }

    /// <summary>Its relay dropped while the host lives and is attaching again (M.ORCH-T966).</summary>
    public bool Reconnecting { get => reconnecting; internal set { if (Set(ref reconnecting, value)) Raise(nameof(StateText)); } }

    /// <summary>Another viewer of this CLI typed within two seconds of this one (M.ORCH-T930); clears two seconds after the last.</summary>
    public bool OtherTyping { get => otherTyping; private set { if (Set(ref otherTyping, value)) Raise(nameof(StateText)); } }

    /// <summary>Keys or a paste were just dropped because the relay is reconnecting (M.ORCH-T979); clears after a few seconds.</summary>
    public bool InputNotSent { get => inputNotSent; private set { if (Set(ref inputNotSent, value)) Raise(nameof(StateText)); } }

    public string StateText => Ended ? "ended" : InputNotSent ? "not sent, reconnecting" : Reconnecting ? "reconnecting" : OtherTyping ? "another viewer is typing" : "";

    private bool paneKnown, paneBusy, paneAsking;

    /// <summary>This one CLI's own status badge (M.ORCH-T1272), the CLI tab's own version of the login tab's aggregate one
    /// (M.ORCH-T1270): a spinner while its agent is working or has subagents/claims running, a plain "?" while it's genuinely
    /// blocked on the person, "zzz" once it's idle. None of the three while its agent isn't known yet (a Launcher menu pane,
    /// or before its session is learned, <see cref="TerminalsViewModel.Update"/>).</summary>
    public bool ShowsPaneBusyBadge => paneKnown && paneBusy && !paneAsking;

    public bool ShowsPaneAskingBadge => paneKnown && paneAsking;

    public bool ShowsPaneIdleBadge => paneKnown && !paneBusy && !paneAsking;

    /// <summary>The icon in words, so it's never only a picture.</summary>
    public string PaneActivityWord => !paneKnown ? "" : paneAsking ? "Blocked waiting on you" : paneBusy ? "Busy" : "Idle";

    /// <summary>Sets this pane's own status from its matched <see cref="AgentBoardRow"/> (by <see cref="SessionId"/>,
    /// <see cref="TerminalsViewModel.Update"/>); null while it isn't known yet.</summary>
    internal void ApplyAgentStatus(AgentBoardRow? row)
    {
        var known = row is not null;
        var busy = row is { State: AgentState.Working or AgentState.HeldByPace } || row is { Subagents.Count: > 0 } || row is { Claims.Count: > 0 };
        var asking = row?.Asking == true;
        if (known == paneKnown && busy == paneBusy && asking == paneAsking) return;
        paneKnown = known; paneBusy = busy; paneAsking = asking;
        Raise(nameof(ShowsPaneBusyBadge)); Raise(nameof(ShowsPaneAskingBadge)); Raise(nameof(ShowsPaneIdleBadge)); Raise(nameof(PaneActivityWord));
    }

    /// <summary>Shows "not sent, reconnecting" for a few seconds (M.ORCH-T979): the input is dropped, never replayed later.</summary>
    private void ShowInputNotSent()
    {
        InputNotSent = true;
        if (notSentTimer is null)
        {
            notSentTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            notSentTimer.Tick += (_, _) => { notSentTimer.Stop(); InputNotSent = false; };
        }
        notSentTimer.Stop();
        notSentTimer.Start();
    }

    /// <summary>Shows the "another viewer is typing" hint for <see cref="TermProtocol.TypingWindow"/>. On the UI thread.</summary>
    private void ShowOtherTyping()
    {
        OtherTyping = true;
        typingTimer ??= NewTypingTimer();
        typingTimer.Stop();
        typingTimer.Start();
    }

    private System.Windows.Threading.DispatcherTimer NewTypingTimer()
    {
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TermProtocol.TypingWindow };
        timer.Tick += (_, _) => { timer.Stop(); OtherTyping = false; };
        return timer;
    }

    // ── remote broadcast (M.ORCH-T935) ──────────────────────────────────────────

    private IReadOnlyList<TermWatcher> watchers = [];
    private bool shared, broadcastPaused, masked = true;

    /// <summary>The remote viewers attached to it through the team gateway now.</summary>
    public IReadOnlyList<TermWatcher> Watchers => watchers;

    public bool IsWatched => watchers.Count > 0;

    /// <summary>Shared with remote viewers (on its own, or by "share my sessions"). Off by default.</summary>
    public bool IsShared => shared;

    /// <summary>Its remote broadcast is paused: remote viewers see "paused by owner" and nothing else.</summary>
    public bool IsBroadcastPaused => broadcastPaused;

    /// <summary>Secrets are masked on its remote streams (the default).</summary>
    public bool IsMasked => masked;

    /// <summary>The "being watched" banner shows while someone is attached remotely or its broadcast is paused.</summary>
    public bool ShowBroadcastBanner => IsHosted && (IsWatched || broadcastPaused);

    /// <summary>The banner's text: who is watching, how, and whether it's paused or unmasked.</summary>
    public string BroadcastText
    {
        get
        {
            var who = string.Join(", ", watchers.Select(w => $"{w.Viewer} ({(w.Mode == "interact" ? "can type" : "watching")}{(w.From is { Length: > 0 } f ? $", from {f}" : "")})"));
            var text = watchers.Count == 0 ? "No one is watching remotely." : $"Being watched remotely by {who}.";
            if (broadcastPaused) text += " Broadcast paused: remote viewers see nothing until you resume.";
            else if (!masked) text += " Secrets are NOT masked for remote viewers.";
            return text;
        }
    }

    public string PauseButtonText => broadcastPaused ? "Resume broadcast" : "Pause broadcast";

    /// <summary>Applies the owner's broadcast choices and the gateway's attached viewers to this tab.</summary>
    internal void SetBroadcast(TermBroadcastSettings settings, IReadOnlyList<TermWatcher> all)
    {
        var id = TermId;
        List<TermWatcher> mine = id is null ? [] : all.Where(w => string.Equals(w.SessionId, id, StringComparison.OrdinalIgnoreCase)).ToList();
        var changed = mine.Count != watchers.Count || !mine.Select(w => w.Connection).SequenceEqual(watchers.Select(w => w.Connection));
        watchers = mine;
        changed |= Set(ref shared, id is not null && settings.IsShared(id), nameof(IsShared));
        changed |= Set(ref broadcastPaused, id is not null && settings.IsPaused(id), nameof(IsBroadcastPaused));
        changed |= Set(ref masked, id is null || settings.IsMasked(id), nameof(IsMasked));
        if (!changed) return;
        Raise(nameof(Watchers));
        Raise(nameof(IsWatched));
        Raise(nameof(ShowBroadcastBanner));
        Raise(nameof(BroadcastText));
        Raise(nameof(PauseButtonText));
    }

    /// <summary>The process its CLI runs under: the TermHost for a hosted tab (the Launcher host and the CLI are its
    /// descendants), else the process ConPTY started (the PowerShell that runs the command); null before it starts.</summary>
    internal int? RootPid => Relay is not null ? Session?.HostPid : (Local?.ConPTYTerm?.Process as ProcessFactory.WrappedProcess)?.Pid;

    internal bool HasExited
    {
        get
        {
            if (Relay is not null) return hostEnded;
            try { return Local?.ConPTYTerm?.Process is { } p && p.HasExited; }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { return true; }
        }
    }

    /// <summary>Whether its CLI asked for bracketed paste (T868): from the host's Modes frames when hosted, so a reattached tab
    /// knows at once; from the output when in-process.</summary>
    internal bool Bracketed => ((Relay?.Modes ?? localModes) & TermModes.BracketedPaste) != 0;

    /// <summary>The in-process tab's bracketed-paste state, as <see cref="TerminalPaste"/> reads it from the output.</summary>
    internal void SetLocalBracketed(bool on) => localModes = on ? localModes | TermModes.BracketedPaste : localModes & ~TermModes.BracketedPaste;

    /// <summary>Writes <paramref name="data"/> to the CLI as typed input; false when there's nowhere to write it yet.</summary>
    internal bool WriteInput(string data)
    {
        if (Relay is { Connection: { } connection })
        {
            connection.WriteInput(data);
            return true;
        }
        if (Local?.ConPTYTerm is { } term)
        {
            term.WriteToTerm(data.AsSpan());
            return true;
        }
        return false;
    }

    /// <summary>The renderer's selected text, clearing it (copy and cut, T866/T867).</summary>
    internal string? ReadSelection() => (Relay?.Terminal ?? Local?.Terminal)?.GetSelectedText();

    private void UseRelay()
    {
        var relay = new RelayTerminal(Campbell);
        relay.Exited += _ => { hostEnded = true; Ended = true; };
        relay.ReconnectingChanged += on =>
        {
            if (on && !Reconnecting && !killRequested) OrchestratorLog.For("Terminals").Information("{Title:l}: lost its TermHost's pipe; reconnecting", Title);
            Reconnecting = on && !killRequested;
        };
        relay.OtherTyping += ShowOtherTyping;
        relay.InputDropped += () => { if (!killRequested && !Ended) ShowInputNotSent(); };
        relay.Detached += why =>
        {
            if (killRequested) return;
            OrchestratorLog.For("Terminals").Information("{Title:l}: detached from its TermHost ({Why:l})", Title, why);
            hostEnded = true;
            Ended = true;
        };
        // Claude's usage figures read off its screen (M.ORCH-T1118): fed on the relay's reader thread, parsed on the pool.
        if (Request.Provider == "Claude") relay.OutputTap = (screen ??= new Budget.ScreenUsageScanner(OnScreenUsage)).Feed;
        Relay = relay;
        ((TerminalView)Control).Child = relay;
    }

    private Budget.ScreenUsageScanner? screen;

    /// <summary>Its CLI drew usage figures (M.ORCH-T1118): written as its login's fresh reading (the global login's only when it
    /// runs on no login folder at all). On the pool; never throws.</summary>
    private void OnScreenUsage(Budget.ReportedUsage reading) =>
        Budget.ScreenUsage.Record(Login, Request.ConfigDir ?? Session?.ConfigDir, reading, DateTime.UtcNow);

    private void UseLocal()
    {
        var local = new EasyTerminalControl
        {
            StartupCommandLine = Request.ProcessCommandLine(pipeName),
            WorkingDirectory = Request.Directory,
            // Windows Terminal's own Campbell scheme: without a theme the renderer's 16-color table isn't set, and the CLI's
            // colors were lost (what M.ORCH-T751 removed the old panes over).
            Theme = Campbell,
        };
        Local = local;
        ((TerminalView)Control).Child = local;
        TerminalPaste.Track(this, local);
    }

    /// <summary>Attaches the tab to its TermHost's session.</summary>
    internal void Attach(TermSessionRecord session)
    {
        Session = session;
        TermId = session.Id;
        var attachedUtc = DateTime.UtcNow;
        Relay?.Attach(session.Pipe, session.Protocol, () => !killRequested && HostAlive(session, attachedUtc, DateTime.UtcNow));
    }

    /// <summary>How long a host that hasn't registered yet (TermLauncher gave up waiting after 5 s and returned its launch's
    /// record) counts as alive while its process runs, so the tab keeps retrying (M.ORCH-T972).</summary>
    internal static readonly TimeSpan RegisterGrace = TimeSpan.FromSeconds(30);

    /// <summary>Whether <paramref name="session"/>'s host still runs, by the registry (the pid alive and not reused): what keeps
    /// a dropped relay reconnecting (M.ORCH-T966). Within <see cref="RegisterGrace"/> of attaching, an unregistered host whose
    /// process runs counts too (M.ORCH-T972). A registry file that couldn't be read just now is unknown, not gone (M.ORCH-T1035):
    /// then the host's own process decides, so a passing read failure never strands the tab as ended. Reads files, so it is
    /// called off the UI thread.</summary>
    internal static bool HostAlive(TermSessionRecord session, DateTime attachedUtc, DateTime nowUtc)
    {
        if (!TermSessions.TryGet(session.Id, out var live)) return TermSessions.IsHostRunning(session);
        return live is not null
            ? live.HostPid == session.HostPid
            : nowUtc - attachedUtc < RegisterGrace && TermSessions.IsHostRunning(session);
    }

    /// <summary>A tab marked ended whose session's host turned up after all (its term-session started, M.ORCH-T972): attached
    /// afresh to <paramref name="session"/>. On the UI thread; never throws.</summary>
    internal void Revive(TermSessionRecord session)
    {
        try
        {
            if (killRequested || Relay is not { } old || session.Exited) return;
            OrchestratorLog.For("Terminals").Information("{Title:l}: its TermHost {Id:l} (pid {Pid}) registered; reattaching the ended tab", Title, session.Id, session.HostPid);
            // Out of the window first: its native window is released only once it's no longer shown.
            ((TerminalView)Control).Child = null;
            old.Dispose();
            hostEnded = false;
            Reconnecting = false;
            Ended = false;
            UseRelay();
            Attach(session);
        }
        catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Reviving {Title:l} failed; contained", Title); }
    }

    /// <summary>Its host couldn't start: the CLI runs in this process after all, as before T926.</summary>
    internal void FallBackToLocal()
    {
        if (Local is not null) return;
        var old = Relay;
        Relay = null;
        TermId = null;
        Session = null;
        // Out of the window first: its native window is released only once it's no longer shown.
        ((TerminalView)Control).Child = null;
        old?.Dispose();
        UseLocal();
    }

    /// <summary>Releases the terminal once its tab is closed: a hosted tab's pipe (the host was asked to end first) and every
    /// native window. Never throws.</summary>
    internal void DisposeControl()
    {
        try
        {
            screen?.Dispose();
            if (Relay is { } relay) relay.Dispose();
            else if (Local is { } local)
            {
                local.DisconnectConPTYTerm();
                TerminalView.DisposeWindows(local);
            }
        }
        catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Disposing a closed terminal failed; contained"); }
    }

    /// <summary>Ends its CLI: the whole tree under the tab's process. A hosted tab's host is killed, which takes its ConPTY and
    /// CLI with it; one still starting is killed as soon as it registers. False when a hosted tab's host still runs and
    /// couldn't be killed (M.ORCH-T1034): the tab should stay.</summary>
    internal bool Kill()
    {
        if (Relay is not null || TermId is not null)
        {
            killRequested = true;
            if (Session is { } session && !KillHost(session))
            {
                killRequested = false;
                return false;
            }
            return true;
        }
        // A process that's already gone is a no-op, however it reports that (it can exit just before the kill).
        try { Local?.ConPTYTerm?.Process?.Kill(true); }
        catch (Exception) { }
        return true;
    }

    /// <summary>Whether <see cref="Kill"/> was asked before its host registered.</summary>
    internal bool KillRequested => killRequested;

    /// <summary>Kills <paramref name="session"/>'s host and everything under it, when its pid is still that host (not reused).</summary>
    internal static bool KillHost(TermSessionRecord session) => TermHostKill.Kill(session);
}

/// <summary>
/// Every terminal open in the dashboard's Terminals panels (M.ORCH-T810), whoever's: one list for the window, on the global
/// login's tab. Each person's page shows its own (<see cref="PersonTerminalsViewModel"/>); the terminals themselves all live in
/// the panel's one host grid, since a native terminal can't be re-templated without starting over, and only the shown person's
/// chosen one is visible (<see cref="SelectedPane"/>).
/// <para>Each CLI starts in its own TermHost (M.ORCH-T926) and the tab attaches to it; on start the dashboard reattaches every
/// live hosted session (<see cref="ReattachAsync"/>) and follows the <c>term-session</c> bus kind (<see cref="OnTermSession"/>).</para>
/// </summary>
public sealed class TerminalsViewModel : ObservableObject
{
    private readonly string? pipeName;
    private readonly HashSet<string> launching = new(StringComparer.OrdinalIgnoreCase);
    private TerminalPaneViewModel? selected;

    public TerminalsViewModel(string? pipeName = null)
    {
        this.pipeName = pipeName;
        CloseCommand = new RelayCommand(p => { if (p is TerminalPaneViewModel pane) Close(pane); });
    }

    /// <summary>Whether new CLIs start in a TermHost (when one is installed); off, every tab runs in this process.</summary>
    public bool UseTermHost { get; init; } = true;

    /// <summary>Starts a hosted CLI and returns its record; blocks (the default is <see cref="TermLauncher.Start(TermLaunchSpec, TimeSpan?)"/>).
    /// Tests replace it.</summary>
    internal Func<TermLaunchSpec, TermSessionRecord> Launch { get; init; } = spec => TermLauncher.Start(spec);

    /// <summary>Whether a TermHost can be started (the default looks for one). Tests replace it.</summary>
    internal Func<bool> HostAvailable { get; init; } = () => TermLauncher.FindHost() is not null;

    /// <summary>A live hosted session by id, or null (the default reads the registry). Tests replace it.</summary>
    internal Func<string, TermSessionRecord?> Lookup { get; init; } = id => TermSessions.Get(id);

    /// <summary>Ends an orphaned host (the default kills it and its tree). Tests replace it.</summary>
    internal Action<TermSessionRecord> KillOrphan { get; init; } = session => TerminalPaneViewModel.KillHost(session);

    /// <summary>After a failed launch, how long to look for its host having started anyway (to adopt it), and how long after
    /// falling back to keep watching for it (to end it) (M.ORCH-T971).</summary>
    internal TimeSpan OrphanWait { get; init; } = TimeSpan.FromSeconds(3);
    internal TimeSpan OrphanWatch { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Ids whose launch failed and whose tab runs its CLI in this process instead: a host that turns up under one is an
    /// orphan, ended, never given a second tab (M.ORCH-T971).</summary>
    private readonly HashSet<string> abandoned = new(StringComparer.OrdinalIgnoreCase);

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

    // ── remote broadcast (M.ORCH-T935) ──────────────────────────────────────────

    private TermBroadcastSettings broadcast = new();
    private IReadOnlyList<TermWatcher> remoteWatchers = [];
    private ICommand? toggleShareCommand, toggleMaskCommand, togglePauseCommand, pauseAllCommand, shareAllCommand;

    /// <summary>Where <c>broadcast.json</c> and <c>watchers.json</c> are (the gateway's folder); tests use their own.</summary>
    internal string? BroadcastDirectory { get; init; }

    /// <summary>"Share my sessions": every session is shared with remote viewers unless it says otherwise.</summary>
    public bool ShareAll => broadcast.ShareAll;

    /// <summary>Every session's remote broadcast is paused.</summary>
    public bool PauseAll => broadcast.PauseAll;

    /// <summary>Shares the tab (its parameter, else the shown one) remotely, or stops sharing it.</summary>
    public ICommand ToggleShareCommand => toggleShareCommand ??= new RelayCommand(p =>
    {
        if (BroadcastId(p) is { } id) ChangeBroadcast(s => s.Share(id, !s.IsShared(id)));
    });

    /// <summary>Turns masking of secrets on the tab's remote streams off, or back on.</summary>
    public ICommand ToggleMaskCommand => toggleMaskCommand ??= new RelayCommand(p =>
    {
        if (BroadcastId(p) is { } id) ChangeBroadcast(s => s.Mask(id, !s.IsMasked(id)));
    });

    /// <summary>Pauses the tab's remote broadcast, or resumes it (Ctrl+Shift+B on the shown one). Resuming a tab while every
    /// session is paused resumes them all.</summary>
    public ICommand TogglePauseCommand => togglePauseCommand ??= new RelayCommand(p =>
    {
        if (BroadcastId(p) is not { } id) return;
        ChangeBroadcast(s => !s.IsPaused(id) ? s.Pause(id, true, DateTime.UtcNow)
            : s.PauseAll ? s.PauseEvery(false, DateTime.UtcNow) : s.Pause(id, false, DateTime.UtcNow));
    });

    /// <summary>Pauses every session's remote broadcast, or resumes them all.</summary>
    public ICommand PauseAllCommand => pauseAllCommand ??= new RelayCommand(() => ChangeBroadcast(s => s.PauseEvery(!s.PauseAll, DateTime.UtcNow)));

    /// <summary>Turns "share my sessions" on or off.</summary>
    public ICommand ShareAllCommand => shareAllCommand ??= new RelayCommand(() => ChangeBroadcast(s => s with { ShareAll = !s.ShareAll }));

    private string? BroadcastId(object? parameter) => (parameter as TerminalPaneViewModel ?? SelectedPane)?.TermId;

    /// <summary>A <c>term-watchers</c> bus message (UI thread): who is attached remotely now. Never throws.</summary>
    public void OnWatchers(TermWatchersEvent message)
    {
        try
        {
            remoteWatchers = message.Watchers ?? [];
            ApplyBroadcast();
        }
        catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Handling a term-watchers message failed; contained"); }
    }

    /// <summary>Rereads the owner's broadcast choices and the gateway's attached viewers (each refresh, and on start).</summary>
    internal void ReadBroadcast()
    {
        try
        {
            broadcast = TermBroadcast.Read(BroadcastDirectory);
            remoteWatchers = TermWatchersBus.Current(BroadcastDirectory);
            ApplyBroadcast();
        }
        catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Reading the broadcast settings failed; contained"); }
    }

    private void ChangeBroadcast(Func<TermBroadcastSettings, TermBroadcastSettings> change)
    {
        try
        {
            if (TermBroadcast.Update(change, BroadcastDirectory) is { } now) broadcast = now;
            else OrchestratorLog.For("Terminals").Warning("Couldn't write the broadcast settings");
            ApplyBroadcast();
        }
        catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Changing the broadcast settings failed; contained"); }
    }

    private void ApplyBroadcast()
    {
        foreach (var pane in Panes) pane.SetBroadcast(broadcast, remoteWatchers);
        Raise(nameof(ShareAll));
        Raise(nameof(PauseAll));
        Raise(nameof(PauseAllText));
    }

    public string PauseAllText => broadcast.PauseAll ? "Resume all" : "Pause all";

    /// <summary>Ticks as of construction, then up by one each call: a tab opened later always sorts after one opened earlier
    /// (M.ORCH-T1170), including across a dashboard restart, since ticks keep rising while this process is down too.</summary>
    private static long nextOrder = DateTime.UtcNow.Ticks;

    /// <summary>Opens <paramref name="request"/> as a new terminal tab and brings it up; returns an error, or null. Its CLI
    /// starts in a TermHost off the UI thread and the tab attaches once it registers; without a host, or when it fails, the
    /// tab runs the CLI in this process instead (logged).</summary>
    public string? Open(PaneRequest request)
    {
        if (request.Problem() is { } problem) return problem;
        request = request with { Order = Interlocked.Increment(ref nextOrder) };
        var hosted = false;
        if (UseTermHost)
        {
            try { hosted = HostAvailable(); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { OrchestratorLog.For("Terminals").Error(ex, "Looking for TermHost failed"); }
            if (!hosted) OrchestratorLog.For("Terminals").Warning("TermHost isn't installed: {Repo:l} runs inside the dashboard", request.Name);
        }
        TerminalPaneViewModel pane;
        try { pane = new TerminalPaneViewModel(request, pipeName, hosted); }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            OrchestratorLog.For("Terminals").Error(ex, "Couldn't open a terminal for {Repo:l}", request.Name);
            return $"Couldn't open a terminal: {ex.Message}";
        }
        Panes.Add(pane);
        Opened?.Invoke(pane);
        if (hosted) _ = StartHostedAsync(pane);
        return null;
    }

    /// <summary>Starts <paramref name="pane"/>'s CLI under a TermHost off the UI thread, then attaches the tab, or falls back to
    /// an in-process terminal when the host can't start. Never throws.</summary>
    private async Task StartHostedAsync(TerminalPaneViewModel pane)
    {
        var dispatcher = pane.Control.Dispatcher;
        var id = TermProtocol.NewSessionId();
        pane.TermId = id;
        lock (launching) launching.Add(id);
        TermSessionRecord? record = null;
        Exception? failure = null;
        try
        {
            // The tab's look goes beside the host's record, so a restarted dashboard reattaches it as it was.
            TermPaneFile.Write(id, pane.Request);
            var spec = TermLaunchSpec.From(pane.Request, pipeName) with { Id = id };
            record = await Task.Run(() => Launch(spec)).ConfigureAwait(false);
        }
        catch (Exception ex) { failure = ex; }

        // A launch that timed out, failed or printed no record may have started its detached host all the same (M.ORCH-T971):
        // the launch named the session (--id), so a host registered under it is adopted into this tab instead of the CLI
        // running twice; one that turns up only after the fallback is ended (WatchForOrphan, Reattach).
        if (record is null)
        {
            record = await Task.Run(() => FindOrphan(id)).ConfigureAwait(false);
            if (record is not null)
                OrchestratorLog.For("Terminals").Warning(failure, "TermHost's launch of {Repo:l} failed but its host {Id:l} (pid {Pid}) runs; the tab adopts it", pane.Request.Name, id, record.HostPid);
        }

        await dispatcher.InvokeAsync(() =>
        {
            try
            {
                if (record is null)
                {
                    OrchestratorLog.For("Terminals").Error(failure, "TermHost couldn't start {Repo:l}; it runs inside the dashboard instead", pane.Request.Name);
                    TermPaneFile.Delete(id);
                    lock (launching) abandoned.Add(id);
                    _ = Task.Run(() => WatchForOrphan(id));
                    if (Panes.Contains(pane) && !pane.KillRequested) pane.FallBackToLocal();
                    return;
                }
                if (!Panes.Contains(pane) || pane.KillRequested)
                {
                    // Closed while its host was starting: the host is ended now that it's known.
                    TerminalPaneViewModel.KillHost(record);
                    return;
                }
                pane.Attach(record);
                Changed?.Invoke();
            }
            catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Attaching {Repo:l} to its TermHost failed; contained", pane.Request.Name); }
            finally { lock (launching) launching.Remove(id); }
        });
    }

    /// <summary>The host of a failed launch <paramref name="id"/> when it registered anyway within <see cref="OrphanWait"/>; else
    /// null. Off the UI thread; never throws.</summary>
    private TermSessionRecord? FindOrphan(string id)
    {
        var until = DateTime.UtcNow + OrphanWait;
        while (true)
        {
            try { if (Lookup(id) is { } found) return found; }
            catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Looking for TermHost session {Id:l} failed; contained", id); return null; }
            if (DateTime.UtcNow >= until) return null;
            Thread.Sleep(100);
        }
    }

    /// <summary>For <see cref="OrphanWatch"/> after a failed launch fell back to an in-process CLI: a host that registers under
    /// its id after all is ended, so the CLI never runs twice (M.ORCH-T971). Off the UI thread; never throws.</summary>
    private void WatchForOrphan(string id)
    {
        var until = DateTime.UtcNow + OrphanWatch;
        while (DateTime.UtcNow < until)
        {
            try
            {
                if (Lookup(id) is { } orphan)
                {
                    EndOrphan(orphan);
                    return;
                }
            }
            catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Watching for an orphaned TermHost {Id:l} failed; contained", id); return; }
            Thread.Sleep(500);
        }
    }

    private void EndOrphan(TermSessionRecord orphan)
    {
        OrchestratorLog.For("Terminals").Warning("TermHost {Id:l} (pid {Pid}) started after its launch failed and its tab ran the CLI in-process; ending it", orphan.Id, orphan.HostPid);
        try { KillOrphan(orphan); }
        catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Ending the orphaned TermHost {Id:l} failed; contained", orphan.Id); }
    }

    /// <summary>A tab's hosted session became known (the restore file records it, M.ORCH-T928).</summary>
    public event Action? Changed;

    /// <summary>On dashboard start: a tab for every live hosted session no tab shows yet, on its person's page (by its login or
    /// config dir), and tab files of sessions that are gone deleted. The registry is read off the UI thread. Never throws.</summary>
    public async Task ReattachAsync()
    {
        try
        {
            var sessions = await Task.Run(() =>
            {
                var live = TermSessions.Enumerate();
                TermPaneFile.Prune(live.Select(s => s.Id));
                return live.Select(s => (Session: s, Request: TermPaneFile.Read(s.Id))).ToList();
            });
            foreach (var (session, request) in sessions) Reattach(session, request);
            ReadBroadcast();
        }
        catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Reattaching the hosted CLIs failed; contained"); }
    }

    /// <summary>
    /// The safety net under the <c>term-session</c> kind (M.ORCH-T1001): the bus keeps no copy of it, so a host announced while
    /// the daemon restarted (or every inbox listener was busy) never reached this dashboard. Run every minute or so: a tab for
    /// every live hosted session no tab shows yet, as at start, but no tab file is pruned (a host this dashboard is starting may
    /// not have registered yet) and a session whose tab was closed here lately is left alone while its host goes. Never throws.
    /// </summary>
    public async Task ResyncAsync()
    {
        try
        {
            // The tabs read here, on the UI thread; the registry off it.
            var shown = Panes.Select(p => p.TermId).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
            var live = await Task.Run(() => TermSessions.Enumerate()
                .Where(s => !shown.Contains(s.Id))
                .Select(s => (Session: s, Request: TermPaneFile.Read(s.Id))).ToList());
            var now = DateTime.UtcNow;
            foreach (var (session, request) in live)
            {
                lock (closedLately)
                    if (closedLately.TryGetValue(session.Id, out var closed) && now - closed < ClosedHoldOff) continue;
                if (Reattach(session, request) is not null) Changed?.Invoke();
            }
            lock (closedLately)
                foreach (var id in closedLately.Where(c => now - c.Value >= ClosedHoldOff).Select(c => c.Key).ToList()) closedLately.Remove(id);
        }
        catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Re-reading the hosted CLIs failed; contained"); }
    }

    /// <summary>How long a hosted session whose tab was closed here isn't given a tab back by <see cref="ResyncAsync"/>.</summary>
    private static readonly TimeSpan ClosedHoldOff = TimeSpan.FromMinutes(2);

    /// <summary>How long an ended tab stays, for whoever closes it by hand to still see its last lines, before
    /// <see cref="PruneEnded"/> closes it unasked (M.ORCH-T1177): without this, a member who stops coming back to the
    /// dashboard (on leave, between projects) never closes theirs, and every CLI that ends on their page piles up a tab,
    /// forever, for no one to see.</summary>
    internal static readonly TimeSpan EndedLifetime = TimeSpan.FromHours(24);

    /// <summary>Closes every tab that's sat <see cref="Ended"/> longer than <see cref="EndedLifetime"/>, as <see cref="Close"/>
    /// would by hand (M.ORCH-T1177). Run each refresh, from <see cref="Update"/>; never throws.</summary>
    internal void PruneEnded(DateTime nowUtc)
    {
        foreach (var pane in Panes.Where(p => p.Ended && p.EndedAtUtc is { } at && nowUtc - at > EndedLifetime).ToList())
        {
            try { Close(pane); }
            catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Pruning {Title:l}'s long-ended tab failed; contained", pane.Title); }
        }
    }

    private readonly Dictionary<string, DateTime> closedLately = new(StringComparer.OrdinalIgnoreCase);

    private bool ClosedLately(string id)
    {
        lock (closedLately) return closedLately.TryGetValue(id, out var closed) && DateTime.UtcNow - closed < ClosedHoldOff;
    }

    /// <summary>A tab's close couldn't end its CLI (M.ORCH-T1034): the tab stays, with why.</summary>
    public event Action<TerminalPaneViewModel, string>? CloseFailed;

    /// <summary>A tab attached to <paramref name="session"/>, unless one shows it already or this dashboard is starting it;
    /// <paramref name="request"/> is its tab file's, else one built from the record. Returns the tab, or null.</summary>
    internal TerminalPaneViewModel? Reattach(TermSessionRecord session, PaneRequest? request = null)
    {
        try
        {
            bool orphan;
            lock (launching)
            {
                if (launching.Contains(session.Id)) return null;
                orphan = abandoned.Contains(session.Id);
            }
            if (orphan)
            {
                // Its tab runs the CLI in this process since its launch failed (M.ORCH-T971): this host would be a second copy.
                EndOrphan(session);
                return null;
            }
            if (Panes.Any(p => string.Equals(p.TermId, session.Id, StringComparison.OrdinalIgnoreCase))) return null;
            request ??= TermPaneFile.FromRecord(session);
            var pane = new TerminalPaneViewModel(request, session);
            Panes.Add(pane);
            OrchestratorLog.For("Terminals").Information("Reattached {Title:l} to its TermHost {Id:l} (pid {Pid})", request.Title, session.Id, session.HostPid);
            return pane;
        }
        catch (Exception ex)
        {
            OrchestratorLog.For("Terminals").Error(ex, "Reattaching TermHost session {Id:l} failed; contained", session.Id);
            return null;
        }
    }

    /// <summary>A <c>term-session</c> bus message (UI thread): a session started elsewhere (the Launcher, M.LNCH-T82) gets a tab;
    /// one whose CLI exited, or whose host ended, is marked ended. Never throws.</summary>
    public void OnTermSession(TermSessionEvent message)
    {
        try
        {
            var pane = Panes.FirstOrDefault(p => string.Equals(p.TermId, message.Id, StringComparison.OrdinalIgnoreCase));
            switch (message.Event)
            {
                case TermSessionEvents.Started when pane is null:
                    // A host whose tab was closed here lately is going (M.ORCH-T1034): no tab back for it, as in ResyncAsync.
                    if (ClosedLately(message.Id)) break;
                    var record = TermSessions.Get(message.Id) ?? message.ToRecord();
                    Reattach(record, TermPaneFile.Read(record.Id));
                    break;
                case TermSessionEvents.Started when pane is { Ended: true, IsHosted: true, KillRequested: false }:
                    // Its host registered after the tab gave up on it (M.ORCH-T972): the tab comes back to life.
                    pane.Revive(TermSessions.Get(message.Id) ?? message.ToRecord());
                    break;
                case TermSessionEvents.Changed when pane is not null && message.Exited:
                case TermSessionEvents.Ended when pane is not null:
                    pane.Ended = true;
                    break;
            }
        }
        catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Handling a term-session message failed; contained"); }
    }

    /// <summary>Closes <paramref name="pane"/>, ending its CLI (a hosted one's TermHost is killed).</summary>
    public void Close(TerminalPaneViewModel pane)
    {
        // Deselected first, then removed: a still-selected item removed from a TabControl-bound collection crashes WPF
        // (M.ORCH-T859). Its teardown is contained so one bad CLI can never take the dashboard (and every terminal) down.
        try
        {
            // Its host still runs and couldn't be killed (M.ORCH-T1034): the tab stays, so it isn't re-added later as a stranger.
            if (!pane.Kill())
            {
                CloseFailed?.Invoke(pane, $"{pane.Title}'s CLI couldn't be ended (its host may run elevated); its tab stays open.");
                return;
            }
        }
        catch (Exception ex) { OrchestratorLog.For("Terminals").Error(ex, "Closing a terminal failed; removing its tab anyway"); }
        if (pane.TermId is { Length: > 0 } termId) lock (closedLately) closedLately[termId] = DateTime.UtcNow;
        if (SelectedPane == pane) SelectedPane = null;
        Panes.Remove(pane);
        // The tab is out of the host grid now: release the terminal's HwndHost, its pipe or ConPTY and its screen buffer,
        // which otherwise stay alive for the rest of the dashboard's run (M.ORCH-T869).
        pane.DisposeControl();
    }

    /// <summary>The live terminal running Launcher host <paramref name="hostPid"/>; null when none does.</summary>
    public TerminalPaneViewModel? Find(int hostPid) => Panes.FirstOrDefault(p => p.HostPid == hostPid && !p.Ended);

    /// <summary>At each refresh: an ended tab sat past <see cref="EndedLifetime"/> is pruned (M.ORCH-T1177); a terminal whose
    /// CLI has exited is marked ended; a live one learns its Launcher host, and follows its CLI to the login it runs on
    /// now.</summary>
    public void Update(Snapshot? snapshot)
    {
        if (Panes.Count == 0) return;
        PruneEnded(DateTime.UtcNow);
        if (Panes.Count == 0) return;
        ReadBroadcast();
        var hosts =snapshot?.Repos.SelectMany(r => r.Hosts).ToList() ?? [];
        var moved = false;
        var learned = false;
        IReadOnlyDictionary<int, int>? parents = null;
        foreach (var pane in Panes.Where(p => !p.Ended).ToList())
        {
            if (pane.HasExited)
            {
                pane.Ended = true;
                continue;
            }
            if (snapshot is null || pane.RootPid is not { } root) continue;
            pane.HostPid ??= hosts.FirstOrDefault(h => h.ProcessId == root || ProcessTree.IsAncestor(root, h.ProcessId))?.ProcessId;
            var session = SessionOf(pane, root, snapshot, ref parents);
            if (session is { Length: > 0 } && !string.Equals(session, pane.SessionId, StringComparison.Ordinal))
            {
                // The session its CLI is in now (a /clear starts a new one): what a restore resumes it on (M.ORCH-T862).
                pane.SessionId = session;
                learned = true;
            }
            // This one CLI's own status badge (M.ORCH-T1272), from the same agent row its session matches.
            pane.ApplyAgentStatus(session is { Length: > 0 } ? snapshot.Agents.FirstOrDefault(a => a.SessionId == session) : null);
            if (pane.HostPid is not { } || session is not { Length: > 0 }) continue;
            // Its person is the login its CLI is on now: moved to another, it moves to that one's page.
            var login = LoginScope.LoginOf(snapshot, session);
            if (!string.Equals(login, pane.Login, StringComparison.OrdinalIgnoreCase))
            {
                pane.Login = login;
                moved = true;
            }
        }
        if (moved) LoginsChanged?.Invoke();
        if (learned) SessionsLearned?.Invoke();
    }

    /// <summary>A pane's CLI's session was learned or changed (M.ORCH-T862): the session-restore file is rewritten.</summary>
    public event Action? SessionsLearned;

    /// <summary>The Claude session <paramref name="pane"/>'s CLI is in: the agent its Launcher host runs, else the one whose
    /// claude.exe (the pid the context gate recorded in session-context) runs under the pane's own process. Null for a
    /// Launcher menu pane, and while none is seen. The process tree is read once per refresh, and only when a host doesn't answer.</summary>
    private static string? SessionOf(TerminalPaneViewModel pane, int root, Snapshot snapshot, ref IReadOnlyDictionary<int, int>? parents)
    {
        if (pane.Request.IsMenu) return null;
        if (pane.HostPid is { } host && snapshot.Agents.FirstOrDefault(a => a.Host?.ProcessId == host) is { SessionId: { Length: > 0 } byHost })
            return byHost;
        if (!snapshot.Agents.Any(a => a.CliPid is not null)) return null;
        try
        {
            var tree = parents ??= ProcessTree.ParentMap();
            return snapshot.Agents.FirstOrDefault(a => a.CliPid is { } cli && (cli == root || ProcessTree.IsAncestor(root, cli, tree)))?.SessionId;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return null; }
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
        // Ctrl+click on the selected tab deselects it, writing null: it stays on the one it was, and the strip is told so
        // (M.ORCH-T1024), after the binding's own write is done (a change raised during it is ignored), or it shows none chosen.
        set
        {
            if (value is null && Panes.Count > 0)
            {
                if (selected is not null && Panes.Contains(selected)) SynchronizationContext.Current?.Post(_ => Raise(nameof(SelectedPane)), null);
                return;
            }
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
        // The chosen one, if it's leaving, is cleared here first (MaTabControl's SelectedItem binding told of a plain
        // property change) before its Panes.RemoveAt below: removing the still-selected item straight out of the bound
        // collection instead crashes the whole process (TabControl.GetSelectedTabItem -> ItemContainerGenerator.
        // ContainerFromIndex throws IndexOutOfRangeException on the stale index, unhandled; M.ORCH-T859).
        var cleared = selected is not null && !mine.Contains(selected);
        if (cleared)
        {
            selected = null;
            Raise(nameof(SelectedPane));
        }
        for (var i = Panes.Count - 1; i >= 0; i--)
            if (!mine.Contains(Panes[i])) Panes.RemoveAt(i);
        for (var i = 0; i < mine.Count; i++)
        {
            var now = Panes.IndexOf(mine[i]);
            if (now < 0) Panes.Insert(i, mine[i]);
            else if (now != i) Panes.Move(now, i);
        }
        if (selected is null && Panes.Count > 0)
        {
            selected = Panes[cleared ? Math.Clamp(at, 0, Panes.Count - 1) : Panes.Count - 1];
            Raise(nameof(SelectedPane));
            SelectionChanged?.Invoke();
        }
        else if (cleared) SelectionChanged?.Invoke();
        Raise(nameof(HasPanes));
        Raise(nameof(Summary));
    }
}
