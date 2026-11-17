using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Setup;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>Which kind of work the dashboard narrows its list to.</summary>
public enum Focus
{
    /// <summary>Everything.</summary>
    All,
    /// <summary>Sessions waiting on the user.</summary>
    Waiting,
    /// <summary>Sessions actively working.</summary>
    Working,
    /// <summary>Open tickets.</summary>
    Tickets,
}

/// <summary>The tile group view model type.</summary>
public sealed class TileGroupViewModel(string title, string hint, Brush accent) : ObservableObject
{
    /// <summary>The title.</summary>
    private bool isVisible;

    /// <summary>The title.</summary>
    public string Title { get; } = title;
    /// <summary>The hint.</summary>
    public string Hint { get; } = hint;
    /// <summary>The accent.</summary>
    public Brush Accent { get; } = accent;
    /// <summary>The tiles.</summary>
    public ObservableCollection<RepoTileViewModel> Tiles { get; } = [];
    /// <summary>The count.</summary>
    public int Count => Tiles.Count;
    /// <summary>Whether visible.</summary>
    public bool IsVisible { get => isVisible; set => Set(ref isVisible, value); }
    /// <summary>Touches.</summary>
    public void Touch() => Raise(nameof(Count));
}

/// <summary>The toast view model type.</summary>
public sealed class ToastViewModel(string title, string body, Brush accent, RepoTileViewModel? target)
{
    /// <summary>The created utc.</summary>
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;

    /// <summary>How long it stays up; null uses the default two minutes.</summary>
    public TimeSpan? Lifetime { get; init; }

    /// <summary>The dashboard tab it was raised on (M.ORCH-T613), the login's name; set as it's shown, and only while there's
    /// more than one tab.</summary>
    public string? Tab { get; set; }

    /// <summary>Its title, tagged with its <see cref="Tab"/> ("Jordan · Repo needs you") when it has one.</summary>
    public string Title => Tab is null ? title : $"{Tab} · {title}";
    /// <summary>The body.</summary>
    public string Body { get; } = body;
    /// <summary>The accent.</summary>
    public Brush Accent { get; } = accent;
    /// <summary>The target.</summary>
    public RepoTileViewModel? Target { get; } = target;

    /// <summary>The one ticket an alert with no repo to open is about (M.ORCH-T762): clicking it opens that ticket in Tickets Desktop.
    /// Null when it names none, or more than one.</summary>
    public string? TicketId { get; } = target is null ? MindAttic.Orchestrator.Tickets.TicketsDesktop.OnlyTicketIn(title + " " + body) : null;
}

public sealed partial class MainViewModel : ObservableObject
{
    private static readonly TimeSpan ToastLifetime = TimeSpan.FromMinutes(2);

    /// <summary>The global login's tab, which owns the one shared refresh, the lease, allocate and every login's tab; null on
    /// the global login's tab itself (M.ORCH-T613).</summary>
    private readonly MainViewModel? root;
    private MainViewModel Root => root ?? this;
    private readonly JsonStore store;

    /// <summary>The settings store every login's tab shares (the window saves the theme switch to it, M.ORCH-T678).</summary>
    internal JsonStore Store => store;
    // Shared with the headless daemon (same lock file): whichever process grabs it first is the one that
    // dispatches allocate and writes rl-status.json, so every open window doesn't race to overwrite it
    // (M.ORCH-T112); released when this process exits, same as the daemon's own.
    private readonly DispatchLease lease;
    private readonly StatusAggregator aggregator;
    private readonly SessionControl control;
    private readonly Dictionary<string, RepoTileViewModel> tiles = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>The snapshot as this tab sees it: only its login's CLIs, tabs and sessions, and its login's meter (M.ORCH-T613).</summary>
    private Snapshot? snapshot;

    /// <summary>The snapshot of this tab's org (M.ORCH-T655; the whole workspace's with one org), for what reads the same in
    /// every tab of it (billing, the Accounts view).</summary>
    private Snapshot? full;

    /// <summary>The whole workspace's snapshot, every org's, on the global login's tab: the queue is planned from it.</summary>
    private Snapshot? workspace;
    private bool refreshing;
    private string search = "";
    private bool showQuiet;
    private string repoSort = SortByName;
    private Focus focus;
    private RepoTileViewModel? selected;
    private string provider = "Claude";
    private string prompt = "";
    private string statusMessage = "";

    /// <summary>Creates the main view model.</summary>
    public MainViewModel() : this(null, null) { }

    /// <summary>A login's tab under <paramref name="root"/> (the global login's), sharing its store, lease, refresh and
    /// allocate; or, with no root, the global login's tab, which makes them.</summary>
    private MainViewModel(MainViewModel? root, LoginTab? tab)
    {
        this.root = root;
        this.tab = tab ?? new LoginTab(null, GlobalLogin, "The global login (~/.claude), its member's own seat", "");
        isLoading = root is null;
        store = root?.store ?? new JsonStore();
        lease = root?.lease ?? new DispatchLease(store.Directory, db: store.Db);
        aggregator = root?.aggregator ?? new StatusAggregator(OrchestratorOptions.Load(), store, new RateLimitStatusWriter { Db = store.Db }, lease);
        control = root?.control ?? new SessionControl(aggregator.Options);
        Zoom = root?.Zoom ?? new();
        if (root is null) { loginTabs = [this]; orgTabs.Add(this); }
        Toasts.CollectionChanged += OnToastsChanged;
        Waiting = new("Needs you", "Sessions that stopped and are waiting for input, longest wait first", Palette.Waiting);
        Working = new("Working", "Agents mid-turn right now", Palette.Working);
        Tickets = new("Open tickets, no session", "Work that's queued but nobody's on", Palette.Tickets);
        Quiet = new("Quiet", "No session and no open tickets", Palette.Quiet);
        Groups = [Waiting, Working, Tickets, Quiet];

        SelectCommand = new RelayCommand(p => Select(p as RepoTileViewModel));
        RefreshCommand = new RelayCommand(async () => await RefreshAsync());
        StartCommand = new RelayCommand(async () => await StartSessionAsync(), () => Selected is { HasFolder: true } && !IsSigningIn);
        OpenFolderCommand = new RelayCommand(() => SessionControl.OpenFolder(Selected!.Path), () => Selected is { HasFolder: true });
        // Off for a ticket a CLI moving to another login holds (M.ORCH-T637): it's handed back to that CLI once it lands.
        UseTicketCommand = new RelayCommand(p => { if (p is TicketRowViewModel { IsMoving: false } t) Prompt = $"Work on ticket {t.Id}: {t.Summary}"; },
            p => p is not TicketRowViewModel { IsMoving: true });
        DismissToastCommand = new RelayCommand(p => { if (p is ToastViewModel t) RemoveToast(t); });
        OpenToastCommand = new RelayCommand(async p =>
        {
            if (p is not ToastViewModel t) return;
            // From every tab's view (M.ORCH-T1574): the dots show all tabs' toasts, so it may be another tab's.
            RemoveToast(t);
            // To the CLI's own tab (M.ORCH-T619); failing that, the repo's detail, from whichever view: not Select, a
            // toggle that only shows in the Repos view.
            await OpenToastAsync(t);
        });
        ClearSearchCommand = new RelayCommand(() => Search = "");
        // One command for every tab (M.ORCH-T654): every ticket link in the window binds it, so a tab of its own re-bound
        // each of them, the whole queue's, on every tab switch. It acts on the tab on screen.
        OpenInTicketsCommand = root?.OpenInTicketsCommand ?? new RelayCommand(p => Shown.OpenInTickets(p as string));
        OpenTicketsAppCommand = root?.OpenTicketsAppCommand ?? new RelayCommand(() => Shown.OpenTicketsApp());
        InitPhases();
        InitSaveAllClose();
        InitTerminals();
        PendingMoves.Changed += OnMovesChanged;
        if (store.Load<ReposView>(ReposViewName).Sort is { } sort && RepoSorts.Contains(sort)) repoSort = sort;
    }

    /// <summary>The waiting.</summary>
    public TileGroupViewModel Waiting { get; }
    /// <summary>The working.</summary>
    public TileGroupViewModel Working { get; }
    /// <summary>The tickets.</summary>
    public TileGroupViewModel Tickets { get; }
    /// <summary>The quiet.</summary>
    public TileGroupViewModel Quiet { get; }
    /// <summary>The groups.</summary>
    public IReadOnlyList<TileGroupViewModel> Groups { get; }
    /// <summary>This tab's own toasts: what it raised (a toast's tab tag and the window's notification come from here).</summary>
    public ObservableCollection<ToastViewModel> Toasts { get; } = [];

    /// <summary>Every tab's toasts, newest first, capped like each tab's (M.ORCH-T1574): what the toast dots show, so one raised
    /// on another tab (the menu bar's tools run on the global login's) is seen whichever tab is on screen. The same list on every tab.</summary>
    public ObservableCollection<ToastViewModel> AllToasts => Root.allToasts;

    private readonly ObservableCollection<ToastViewModel> allToasts = [];

    /// <summary>Drops this tab's subscription to the static <see cref="PendingMoves.Changed"/>, so a login tab removed on
    /// sign-out isn't kept alive handling every move (M.ORCH-T734).</summary>
    public void Detach() => PendingMoves.Changed -= OnMovesChanged;

    /// <summary>The window's zoom, 80-200% (WCAG 1.4.4): the library's ZoomLevel, which MainWindow attaches to its content
    /// with Ctrl+Plus / Ctrl+Minus / Ctrl+0; the tiles and cards wrap and the views scroll, so nothing is cut off as it grows.</summary>
    public ZoomLevel Zoom { get; }
    /// <summary>The providers.</summary>
    public IReadOnlyList<string> Providers => SessionControl.Providers;

    /// <summary>The select command.</summary>
    public ICommand SelectCommand { get; }
    /// <summary>The refresh command.</summary>
    public ICommand RefreshCommand { get; }
    /// <summary>The start command.</summary>
    public ICommand StartCommand { get; }
    /// <summary>The open folder command.</summary>
    public ICommand OpenFolderCommand { get; }
    /// <summary>The use ticket command.</summary>
    public ICommand UseTicketCommand { get; }

    /// <summary>A ticket id anywhere in the window opens that ticket in Tickets Desktop, to view, approve, prioritize or
    /// close it there (M.ORCH-T500); the parameter is the id.</summary>
    public ICommand OpenInTicketsCommand { get; }

    // OpenInTickets: MainViewModel.AppBus.cs (M.ORCH-T879: over the app bus when Tickets Desktop listens).

    /// <summary>The upper-right Tickets button (M.ORCH-T878): opens Tickets Desktop, or brings it forward if it's running.</summary>
    public ICommand OpenTicketsAppCommand { get; }

    /// <summary>The sort by tickets.</summary>
    private void OpenTicketsApp()
    {
        if (TicketsApp.OpenOrFocus(aggregator.Options) is { } error)
            Toasts.Insert(0, new ToastViewModel("Couldn't open Tickets Desktop", error, Palette.Over, null));
    }
    /// <summary>The dismiss toast command.</summary>
    public ICommand DismissToastCommand { get; }
    /// <summary>The open toast command.</summary>
    public ICommand OpenToastCommand { get; }
    /// <summary>The clear search command.</summary>
    public ICommand ClearSearchCommand { get; }

    /// <summary>The search.</summary>
    public string Search { get => search; set { if (Set(ref search, value)) Regroup(); } }
    /// <summary>The show quiet.</summary>
    public bool ShowQuiet { get => showQuiet; set { if (Set(ref showQuiet, value)) Regroup(); } }
    /// <summary>The focus.</summary>
    public Focus Focus
    {
        get => focus;
        private set
        {
            if (!Set(ref focus, value)) return;
            Raise(nameof(IsWaitingFocus)); Raise(nameof(IsWorkingFocus)); Raise(nameof(IsTicketsFocus));
        }
    }

    /// <summary>Which stat tile filters the repos right now: the library's MaTile.IsSelected outlines it (M.ORCH-T667).</summary>
    public bool IsWaitingFocus => Focus == Focus.Waiting;
    /// <summary>Whether working focus.</summary>
    public bool IsWorkingFocus => Focus == Focus.Working;
    /// <summary>Whether tickets focus.</summary>
    public bool IsTicketsFocus => Focus == Focus.Tickets;

    /// <summary>The constants sort by name, sort by tickets.</summary>
    public const string SortByName = "Name", SortByTickets = "Open tickets";
    /// <summary>The repo sorts.</summary>
    public IReadOnlyList<string> RepoSorts => RepoSortNames;
    /// <summary>The has selection.</summary>
    private static readonly IReadOnlyList<string> RepoSortNames = [SortByName, SortByTickets];

    /// <summary>The provider constant.</summary>
    private const string ReposViewName = "dashboard-repos";
    private sealed class ReposView { public string? Sort { get; init; } }

    /// <summary>How the repo cards are ordered within each section (M.ORCH-T593); kept for the next time the window opens.</summary>
    public string RepoSort
    {
        get => repoSort;
        set
        {
            // Ctrl+click on the selected pill deselects it, writing null: it stays on the one it was.
            if (value is null || !Set(ref repoSort, value)) return;
            try { store.Save(ReposViewName, new ReposView { Sort = value }); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            Regroup();
        }
    }

    /// <summary>The selected.</summary>
    public RepoTileViewModel? Selected
    {
        get => selected;
        private set
        {
            if (selected == value) return;
            if (selected is not null) selected.IsSelected = false;
            selected = value;
            if (selected is not null) selected.IsSelected = true;
            Prompt = "";
            StatusMessage = "";
            if (selected is not null) RefreshLogins();
            Raise();
            Raise(nameof(HasSelection));
        }
    }

    /// <summary>Whether selection.</summary>
    public bool HasSelection => Selected is not null;
    // Ctrl+click on the selected pill deselects it, writing null: Start would crash the dashboard on it.
    /// <summary>The provider.</summary>
    public string Provider { get => provider; set { if (value is not null && Set(ref provider, value)) Raise(nameof(ShowLogins)); } }

    /// <summary>What <see cref="Logins"/> lists for the global login (~/.claude), its member's own seat.</summary>
    public const string GlobalLogin = "Global login";

    /// <summary>The global login's tab's other choice (M.ORCH-T605): one CLI per account, a per-CLI login no CLI runs on yet
    /// (most headroom first), else the global login (<see cref="LoginPicker"/>).</summary>
    public const string AutoLogin = "Auto";

    private readonly AccountDirs accountDirs = new();
    private string login = GlobalLogin;

    /// <summary>The logins Start can open a Claude tab on from this dashboard tab (M.ORCH-T613): a login's own tab starts on
    /// its login and offers no choice; the global login's tab starts on the global login, or on Auto while it's on in
    /// Settings. Every other login has a tab of its own to start from.</summary>
    public IReadOnlyList<string> Logins { get; private set; } = [GlobalLogin];

    /// <summary>The <see cref="Logins"/> pills on the global login's tab: the global login, then Auto when <paramref name="auto"/>.</summary>
    internal static IReadOnlyList<string> GlobalTabLogins(bool auto) => auto ? [GlobalLogin, AutoLogin] : [GlobalLogin];

    /// <summary>Shown only for Claude, and only while there's a choice.</summary>
    public bool ShowLogins => Provider == "Claude" && Logins.Count > 1;

    // Ctrl+click on the selected pill deselects it, writing null: it stays on the one it was.
    /// <summary>The login.</summary>
    public string Login { get => login; set { if (value is not null) Set(ref login, value); } }

    /// <summary>Whether warnings.</summary>
    private void RefreshLogins()
    {
        IReadOnlyList<string> found = [];
        if (ScopeEmail is null)
        {
            var auto = true;
            try { auto = store.LoadSettings().AutoLogin; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { }
            found = GlobalTabLogins(auto);
        }
        if (found.SequenceEqual(Logins)) return;
        Logins = found;
        // A choice gone falls back to the tab's own login.
        if (!found.Contains(Login)) Login = found.Count > 0 ? found[0] : GlobalLogin;
        Raise(nameof(Logins));
        Raise(nameof(ShowLogins));
    }
    /// <summary>The prompt.</summary>
    public string Prompt { get => prompt; set => Set(ref prompt, value); }
    /// <summary>The status message.</summary>
    public string StatusMessage { get => statusMessage; private set => Set(ref statusMessage, value); }

    /// <summary>Whether this window is the one process building snapshots (no daemon got the lease first), so it has to
    /// keep refreshing even while hidden: rl-status.json and allocate depend on it.</summary>
    public bool HoldsLease => lease.IsHeld;

    /// <summary>The working count.</summary>
    public int WorkingCount => snapshot?.Working ?? 0;
    /// <summary>The waiting count.</summary>
    public int WaitingCount => snapshot?.Waiting ?? 0;
    /// <summary>The host count.</summary>
    public int HostCount => snapshot?.LiveHosts ?? 0;
    /// <summary>The ticket count.</summary>
    public int TicketCount => snapshot?.OpenTickets ?? 0;
    /// <summary>The repo count.</summary>
    public int RepoCount => snapshot?.Repos.Count(r => !r.Name.StartsWith('(')) ?? 0;
    /// <summary>The workspace root.</summary>
    public string WorkspaceRoot => aggregator.Options.WorkspaceRoot;
    /// <summary>The tokens today.</summary>
    public string TokensToday => Formatting.Tokens(snapshot?.UsageToday.Total ?? 0);
    /// <summary>The tokens detail.</summary>
    public string TokensDetail
    {
        get
        {
            var u = snapshot?.UsageToday ?? default;
            return $"{Formatting.Tokens(u.Fresh)} uncached · {Formatting.Tokens(u.Output)} out";
        }
    }
    /// <summary>The warnings.</summary>
    public IReadOnlyList<string> Warnings => snapshot?.Warnings ?? [];
    /// <summary>Whether warnings.</summary>
    public bool HasWarnings => Warnings.Count > 0;

    /// <summary>The status bar's count of them: not only ticket files go wrong (billing, the approvals' move into the ticket
    /// database), and calling every one a ticket file problem sent you looking in the wrong place (M.ORCH-T465).</summary>
    public string WarningsText => WarningLine(Warnings.Count);

    internal static string WarningLine(int count) =>
        count == 1 ? "1 problem reading the workspace, hover for details" : $"{count} problems reading the workspace, hover for details";

    private bool isLoading;

    /// <summary>True until the first refresh finishes, either way (a failure shows in the status message): the full-screen
    /// loader covers the dashboard until then, so it never shows empty or half-filled panels (M.ORCH-T184).</summary>
    public bool IsLoading { get => isLoading; private set => Set(ref isLoading, value); }

    /// <summary>The one refresh every tab shares (M.ORCH-T613): a login's tab asks the global login's, which builds the
    /// workspace's snapshot once, keeps a tab per login, and hands each its own view of it.</summary>
    public async Task RefreshAsync()
    {
        if (root is not null)
        {
            await root.RefreshAsync();
            return;
        }
        // One asked for mid-refresh (a snapshot the status stream just announced, M.ORCH-T854) runs once this one is done,
        // rather than being dropped until the next push.
        if (refreshing) { refreshQueued = true; return; }
        refreshing = true;
        // The digest of the rest of the snapshot the stream last said (M.ORCH-T872): the published file read now holds at least it.
        var rest = restWanted;
        try
        {
            var clock = Stopwatch.StartNew();
            var dirs = OwnLoginDirs();
            IReadOnlyList<Member> readMembers = [];
            (Snapshot, SchedulePlan, IReadOnlyDictionary<string, string>)? planned = null;
            var (next, tabs, nextOrgs, diskFree, ramPercent, allocatePausedNow) = await BuiltAfterChanges(() => Task.Run(() =>
            {
                var built = aggregator.Refresh();
                // Each login's name and color, read with the snapshot, off the UI thread; and the organization's members, so
                // every one has its tab, one not signed in here yet a tab to sign in from (M.ORCH-T615, M.ORCH-T670); and the orgs,
                // which person tabs each one's switcher shows (M.ORCH-T655).
                IReadOnlyList<Member> members = [];
                try { members = MemberRoster.Load(aggregator.Db); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Data.Common.DbException) { }
                readMembers = members;
                var orgDirectory = aggregator.Db.TryLoadOrgs();
                // The API key's person (M.ORCH-T906): its tab is "signed in" while the Vault holds a Claude key.
                var hasKey = false;
                try { hasKey = aggregator.BillingSwitch.HasKey; }
                catch (Exception ex) when (ex is not OutOfMemoryException) { }
                lastApiKey = hasKey;
                // The menu bar's own host readout (M.ORCH-T1499): this machine's C: free space and RAM load, not any login's,
                // reused from the same cheap stat calls the disk-space and memory alerts already read (M.ORCH-T471/M.ORCH-T1494).
                var diskFreeGb = DiskWatch.Read(["C:\\"]) is [{ } drive, ..] ? drive.FreeGb : 0;
                var ramUtilizationPercent = MachineHealth.RamUtilizationPercent();
                // The master switch for /allocate (M.ORCH-T1491): only a setting that was read and is off shows the banner.
                var paused = store.TryLoadSettings(out var settingsNow) && !settingsNow.Allocate;
                // Members whose until date has passed drop off the roster, so no CLI tab is started or respawned for them (M.ORCH-T1401).
                var lapsed = MemberUntil.Lapsed(settingsNow?.MemberUntil, DateTime.Now);
                // On the host, the org members connected to it now (hub phase 1, M.ORCH-T1640): the menu bar's chip.
                connectedMembersRead = ConnectedMembersNow(settingsNow);
                // On a member's PC, consent requests on their own CLIs on the host (hub phase 2, M.ORCH-T1641): toasts.
                consentsRead = ConsentsNow(settingsNow);
                planned = PlanOffUi(built);
                // The owner inbox (M.ORCH-T1689): everything waiting on the owner, from the snapshot and plan just made.
                if (InboxNow(built, planned?.Item2, settingsNow) is { } inbox) inboxRead = inbox;
                return (built, LoginScope.Tabs(built, dirs, members, hasKey, lapsed), orgDirectory, diskFreeGb, ramUtilizationPercent, paused);
            }), () => localChanges);
            precomputedPlan = planned;
            ApplyTabs(next, tabs, nextOrgs);
            // The memory guard's level (M.ORCH-T1498): the daemon's, stepped, as the snapshot's pace carries it; else this process's own.
            // And the disk floor's hold (M.ORCH-T1692): the daemon's, as the snapshot's pace carries it; else this process's own.
            SetHostStats(diskFree, ramPercent, next.Pace?.Memory ?? MemoryGuard.Shared.Last, allocatePausedNow, next.Pace?.Disk ?? DiskGuard.Shared.Last);
            ApplyInbox();
            // What a pushed snapshot's figures are laid over until the next read (M.ORCH-T872).
            (lastMembers, lastOrgs, restShown) = (readMembers, nextOrgs, rest);
            await TickAllocateAsync(next);
            foreach (var tab in LoginTabs)
                if (tab.StatusMessage.StartsWith("Refresh failed:", StringComparison.Ordinal)) tab.StatusMessage = "";
            // Found dead (bughunt, M.ORCH-T1538): clock was started and never read, so a slow refresh never left a trace.
            if (clock.Elapsed > SlowRefreshThreshold)
                OrchestratorLog.For("Dashboard").Warning("A refresh took {Ms}ms", clock.ElapsedMilliseconds);
        }
        // Any failure is shown and retried next tick: this runs from an async void timer, so an escaped exception would
        // close the dashboard, and with it allocate and rl-status.json if this window holds the lease.
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            foreach (var tab in LoginTabs) tab.StatusMessage = $"Refresh failed: {ex.Message}";
            OrchestratorLog.For("Dashboard").Error(ex, "refresh failed");
        }
        finally
        {
            // The first time, keep the loader up until the dashboard behind it has been laid out (layout runs at Render
            // priority, ahead of Loaded), so it lifts on a finished window rather than one still drawing (M.ORCH-T200).
            if (IsLoading && System.Windows.Application.Current?.Dispatcher is { } ui && ui.CheckAccess())
                await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Loaded);
            refreshing = false;
            IsLoading = false;
        }
        if (refreshQueued)
        {
            refreshQueued = false;
            await RefreshAsync();
        }
    }

    /// <summary>Whether the Vault held a Claude key at the last read (M.ORCH-T906): the API key's person tab's "signed in",
    /// reused when the stream's figures are laid over the last read.</summary>
    private bool lastApiKey;

    /// <summary>A refresh was asked for while one ran.</summary>
    private bool refreshQueued;

    /// <summary>How long a refresh may take before it's logged (M.ORCH-T1538): generous, so only a real stall is noisy.</summary>
    private static readonly TimeSpan SlowRefreshThreshold = TimeSpan.FromSeconds(5);

    /// <summary>Counts the changes made here that a snapshot reads back (a billing switch, a priority bump).</summary>
    private int localChanges;

    /// <summary>A change made here is in every snapshot built from now on.</summary>
    private void NoteLocalChange() => Root.localChanges++;

    /// <summary>
    /// A snapshot built after the last change made here: one whose build began before a billing switch or a bump landed read
    /// the old mode or priority, and applied after it snapped the toggle and the header back to the old billing, or the row
    /// back to its old place, until the next refresh (M.ORCH-T462). Built again then, a few times at most.
    /// </summary>
    internal static async Task<T> BuiltAfterChanges<T>(Func<Task<T>> build, Func<int> changes, int tries = 3)
    {
        while (true)
        {
            var before = changes();
            var built = await build();
            if (changes() == before || --tries <= 0) return built;
        }
    }

    /// <summary>This tab's view of the workspace's snapshot <paramref name="all"/>: <paramref name="next"/>, the part on its login.</summary>
    private void Apply(Snapshot all, Snapshot next)
    {
        full = all;
        var first = snapshot is null;
        foreach (var repo in next.Repos)
        {
            if (!tiles.TryGetValue(repo.Name, out var tile)) tiles[repo.Name] = tile = new RepoTileViewModel(repo.Name);
            var before = first ? (RepoState?)null : tile.State;
            tile.Update(repo, next.TakenUtc);
            if (before == RepoState.Working && repo.State == RepoState.Waiting)
                Toasts.Insert(0, new ToastViewModel($"{repo.Name} needs you", "The agent finished its turn and is waiting.", Palette.Waiting, tile));
        }
        foreach (var gone in tiles.Keys.Except(next.Repos.Select(r => r.Name), StringComparer.OrdinalIgnoreCase).ToList())
        {
            if (tiles[gone] == Selected) Selected = null;
            tiles.Remove(gone);
        }
        snapshot = next;
        SyncLiveActivity(next);
        Regroup();
        ApplyPhases(next);
        ApplyBilling(next);
        ApplyReminders(DateTime.UtcNow);
        ApplyAccounts(all, DateTime.UtcNow);
        // Who's out of room is read with the roster, just above (M.ORCH-T668).
        if (IsGlobalTab) ApplyUserTickets();
        // Each terminal learns its CLI and follows it to the login it runs on (M.ORCH-T810).
        if (IsGlobalTab) AllTerminals.Update(all);
        // The sidebar's Task List (M.ORCH-T1593): every process spending tokens across the whole workspace, not only this org's.
        if (IsGlobalTab) SyncTaskList(workspace ?? all);
        // After the phases' own toasts (pace, budget, stuck, audit, Needs you), or they'd go past the cap (M.ORCH-T340).
        PruneToasts(Toasts, DateTime.UtcNow);
        Raise(nameof(WorkingCount)); Raise(nameof(WaitingCount)); Raise(nameof(HostCount));
        Raise(nameof(TicketCount)); Raise(nameof(RepoCount)); Raise(nameof(TokensToday));
        Raise(nameof(TokensDetail)); Raise(nameof(Warnings)); Raise(nameof(HasWarnings)); Raise(nameof(WarningsText));
        Raise(nameof(ShowsWarningsSection));
        Raise(nameof(TabTrailing)); Raise(nameof(TabTip)); Raise(nameof(TabHoverTip));
    }

    /// <summary>The most toasts shown at once; the oldest (last) go first.</summary>
    internal const int MaxToasts = 4;

    /// <summary>Drops toasts past their lifetime (two minutes unless set), those whose repo is no longer waiting on you,
    /// and then the oldest past <see cref="MaxToasts"/>.</summary>
    internal static void PruneToasts(IList<ToastViewModel> toasts, DateTime nowUtc)
    {
        foreach (var stale in toasts.Where(t => nowUtc - t.CreatedUtc > (t.Lifetime ?? ToastLifetime) ||
                                                t.Target is { State: not RepoState.Waiting }).ToList())
            toasts.Remove(stale);
        while (toasts.Count > MaxToasts) toasts.RemoveAt(toasts.Count - 1);
    }

    /// <summary>Which groups show: a non-empty group in the focus (All shows every one), and Quiet only unfocused and
    /// only with Show quiet ticked or a search typed.</summary>
    internal static (bool Waiting, bool Working, bool Tickets, bool Quiet) GroupVisibility(
        int waiting, int working, int tickets, int quiet, Focus focus, bool showQuiet, string search) =>
        (waiting > 0 && focus is Focus.All or Focus.Waiting,
         working > 0 && focus is Focus.All or Focus.Working,
         tickets > 0 && focus is Focus.All or Focus.Tickets,
         quiet > 0 && focus is Focus.All && (showQuiet || !string.IsNullOrWhiteSpace(search)));

    /// <summary>A section's cards in <paramref name="sort"/>'s order: by name, or most open tickets first and then by name.</summary>
    internal static IEnumerable<RepoTileViewModel> Sorted(IEnumerable<RepoTileViewModel> tiles, string sort) =>
        (sort == SortByTickets ? tiles.OrderByDescending(t => t.OpenTicketCount) : tiles.OrderBy(_ => 0))
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase);

    private void Regroup()
    {
        var visible = tiles.Values.Where(t => t.Matches(Search)).ToList();
        // Needs you keeps longest wait first whatever the sort: the one waiting longest is the one to answer next.
        Waiting.Tiles.ReorderTo([.. visible.Where(t => t.State == RepoState.Waiting)
            .OrderBy(t => t.Status?.LastActivityUtc ?? DateTime.MaxValue)]);
        Working.Tiles.ReorderTo([.. Sorted(visible.Where(t => t.State == RepoState.Working), RepoSort)]);
        Tickets.Tiles.ReorderTo([.. Sorted(visible.Where(t => t.State == RepoState.Dormant && t.OpenTicketCount > 0), RepoSort)]);
        Quiet.Tiles.ReorderTo([.. Sorted(visible.Where(t => t.State == RepoState.Dormant && t.OpenTicketCount == 0), RepoSort)]);

        (Waiting.IsVisible, Working.IsVisible, Tickets.IsVisible, Quiet.IsVisible) =
            GroupVisibility(Waiting.Count, Working.Count, Tickets.Count, Quiet.Count, Focus, ShowQuiet, Search);
        foreach (var g in Groups) g.Touch();
        Raise(nameof(IsEmpty));
    }

    /// <summary>Whether empty.</summary>
    public bool IsEmpty => Groups.All(g => !g.IsVisible);

    private void Select(RepoTileViewModel? tile) => Selected = Selected == tile ? null : tile;

    private async Task StartSessionAsync()
    {
        if (Selected is null) return;
        // What Start was pressed with: a sign-in first can take minutes, and the selection may change meanwhile.
        await StartSessionAsync(Selected, Provider, Login, string.IsNullOrWhiteSpace(Prompt) ? null : Prompt);
    }

    /// <summary>
    /// <c>MindAttic.Orchestrator.Desktop.exe --start &lt;repo&gt;</c> (M.ORCH-T767, the Tickets desktop's Repos tab Start link): on the global
    /// login's tab, once the first snapshot is in, a Claude CLI in that repo the way Start opens one on Auto (the login picker
    /// and its cutoffs, never a member's own seat, the API key only while API billing is on). Only a repo the workspace has
    /// starts; anything else is refused and said in the status line.
    /// </summary>
    public async Task StartRepoAsync(string name)
    {
        if (root is not null) { await root.StartRepoAsync(name); return; }
        SelectedTab = this;
        for (var waited = 0; IsLoading && waited < 600; waited++) await Task.Delay(100);
        var log = OrchestratorLog.For("Dashboard");
        if (StartTarget(name, tiles) is not { } tile)
        {
            log.Warning("--start {Repo}: not a repo in the workspace; nothing started", name);
            StatusMessage = $"Not started: {name} isn't a repo in the workspace.";
            return;
        }
        log.Information("--start {Repo:l}: starting a CLI", tile.Name);
        await StartSessionAsync(tile, "Claude", AutoLogin, null);
    }

    /// <summary>The workspace repo <paramref name="name"/> names, by its own name: a plain repo name (<see cref="StartRequest.IsRepoName"/>)
    /// with a folder; null for anything else.</summary>
    internal static RepoTileViewModel? StartTarget(string name, IReadOnlyDictionary<string, RepoTileViewModel> known) =>
        StartRequest.IsRepoName(name) && known.GetValueOrDefault(name) is { HasFolder: true } tile ? tile : null;

    private async Task StartSessionAsync(RepoTileViewModel tile, string kind, string login, string? text)
    {
        if (await StartSessionAsync(tile.Name, tile.Path, kind, login, text) && Selected == tile) Prompt = "";
    }

    /// <summary>Opens a <paramref name="kind"/> CLI for the repo <paramref name="name"/> at <paramref name="path"/> on this tab's
    /// login (or a free one); true once it's asked to start.</summary>
    private async Task<bool> StartSessionAsync(string name, string path, string kind, string login, string? text)
    {
        string? configDir = null;
        var on = login;
        if (kind == "Claude" && ScopeEmail is { } own)
        {
            // This tab's login (M.ORCH-T613): the CLI opens on it, and shows on this tab.
            if (await OwnLoginDirAsync(own, m => StatusMessage = m) is not { } dir) return false;
            configDir = dir;
            on = TabTitle;
        }
        else if (kind == "Claude" && login == AutoLogin)
        {
            // Auto (M.ORCH-T605): a free per-CLI login (never the global one, a member's own seat); held for this tab so the next Start goes elsewhere.
            var auto = new LoginPicker(accountDirs).ForNewTab(kind, autoLogin: true, DateTime.UtcNow, out _, weeklyStopPercent);
            configDir = auto?.Directory;
            if (auto is null && full?.Billing?.Mode != MindAttic.Orchestrator.Billing.BillingModes.Api)
            {
                // Never the global login, a member's own seat (M.ORCH-T747): the API key when billing is on, else it waits.
                StatusMessage = "No login of its own has room and API billing is off: not started on the global login. Pick a login's tab, or switch API billing on.";
                return false;
            }
            on = auto?.Email ?? GlobalLogin;
        }
        // Short of RAM (M.ORCH-T1496) a CLI you start yourself asks first; automated starts are simply held.
        if (MindAttic.Orchestrator.Activity.MemoryGuard.StartHeld(store.LoadSettings()) is { } low
            && MindAttic.Components.Wpf.Controls.MaMessageBox.Show(System.Windows.Application.Current?.MainWindow,
                low + " Another CLI can push it into paging, which grows the pagefile until the disk is full.",
                "Start a CLI anyway?", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning,
                System.Windows.MessageBoxResult.No) != System.Windows.MessageBoxResult.Yes)
        {
            StatusMessage = $"Not started: {low}";
            return false;
        }
        // Under the disk floor (M.ORCH-T1692) it's refused outright, with why: a full drive has no "anyway". Read off the UI
        // thread, since under the floor the guard runs a self-cleanup first; the daemon's hold (the snapshot's pace) counts too.
        var settingsForStart = store.LoadSettings();
        var heldByDaemon = full?.Pace?.Disk is not null;
        if (await Task.Run(() => DiskGuard.Shared.Observe(settingsForStart, DateTime.UtcNow, heldByDaemon)?.Why) is { } diskFull)
        {
            StatusMessage = $"Not started: {diskFull}.";
            return false;
        }
        var error = control.Start(name, path, kind, text, configDir);
        StatusMessage = error ?? $"Opened a {kind} tab for {name}" + (configDir is null ? "" : $" on {on}'s own login") + ". It'll appear here within a few seconds.";
        return error is null;
    }

    /// <summary>
    /// <paramref name="email"/>'s own login folder, signed in and made ready for a CLI to start on; null, with why said through
    /// <paramref name="say"/>, when it can't be. A login signed out since its tab opened gets its own sign-in first (M.ORCH-T610),
    /// in a private window of its own browser profile, and the CLI opens once the folder is signed in as that account.
    /// </summary>
    private async Task<string?> OwnLoginDirAsync(string email, Action<string> say)
    {
        var dir = accountDirs.For(email);
        // A login's .claude.json can run to megabytes: read off the UI thread.
        if (!await Task.Run(() => dir.SignedIn))
        {
            say($"{dir.Email} isn't signed in yet: sign in as {dir.Email} in the browser window that opens, and the tab opens on it.");
            if (!await SignInOwnAsync(dir.Email) || !await Task.Run(() => dir.SignedIn))
            {
                say($"No tab opened: {dir.Email} isn't signed in as itself. {AccountsMessage}");
                return null;
            }
        }
        try { await Task.Run(() => accountDirs.Prepare(dir)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            say($"Couldn't prepare {email}'s login: {ex.Message}");
            return null;
        }
        return dir.Directory;
    }
}
