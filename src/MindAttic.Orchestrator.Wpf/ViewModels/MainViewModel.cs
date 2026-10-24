using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Setup;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

public enum Focus { All, Waiting, Working, Tickets }

public sealed class TileGroupViewModel(string title, string hint, Brush accent) : ObservableObject
{
    private bool isVisible;

    public string Title { get; } = title;
    public string Hint { get; } = hint;
    public Brush Accent { get; } = accent;
    public ObservableCollection<RepoTileViewModel> Tiles { get; } = [];
    public int Count => Tiles.Count;
    public bool IsVisible { get => isVisible; set => Set(ref isVisible, value); }
    public void Touch() => Raise(nameof(Count));
}

public sealed class ToastViewModel(string title, string body, Brush accent, RepoTileViewModel? target)
{
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;

    /// <summary>How long it stays up; null uses the default two minutes.</summary>
    public TimeSpan? Lifetime { get; init; }

    /// <summary>The dashboard tab it was raised on (M.ORCH-T613), the login's name; set as it's shown, and only while there's
    /// more than one tab.</summary>
    public string? Tab { get; set; }

    /// <summary>Its title, tagged with its <see cref="Tab"/> ("Jordan · Repo needs you") when it has one.</summary>
    public string Title => Tab is null ? title : $"{Tab} · {title}";
    public string Body { get; } = body;
    public Brush Accent { get; } = accent;
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
    private string refreshInfo = "Loading…";

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
        Clock = root?.Clock ?? new();
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
        DismissToastCommand = new RelayCommand(p => { if (p is ToastViewModel t) Toasts.Remove(t); });
        OpenToastCommand = new RelayCommand(async p =>
        {
            if (p is not ToastViewModel t) return;
            Toasts.Remove(t);
            // To the CLI's own tab (M.ORCH-T619); failing that, the repo's detail, from whichever view: not Select, a
            // toggle that only shows in the Repos view.
            await OpenToastAsync(t);
        });
        ClearSearchCommand = new RelayCommand(() => Search = "");
        // One command for every tab (M.ORCH-T654): every ticket link in the window binds it, so a tab of its own re-bound
        // each of them, the whole queue's, on every tab switch. It acts on the tab on screen.
        OpenInTicketsCommand = root?.OpenInTicketsCommand ?? new RelayCommand(p => Shown.OpenInTickets(p as string));
        InitPhases();
        PendingMoves.Changed += OnMovesChanged;
        if (store.Load<ReposView>(ReposViewName).Sort is { } sort && RepoSorts.Contains(sort)) repoSort = sort;
    }

    public TileGroupViewModel Waiting { get; }
    public TileGroupViewModel Working { get; }
    public TileGroupViewModel Tickets { get; }
    public TileGroupViewModel Quiet { get; }
    public IReadOnlyList<TileGroupViewModel> Groups { get; }
    public ObservableCollection<ToastViewModel> Toasts { get; } = [];

    /// <summary>Drops this tab's subscription to the static <see cref="PendingMoves.Changed"/>, so a login tab removed on
    /// sign-out isn't kept alive handling every move (M.ORCH-T734).</summary>
    public void Detach() => PendingMoves.Changed -= OnMovesChanged;

    /// <summary>The header's live clock, ticking every second on its own timer, not with the refresh (M.ORCH-T193).</summary>
    public ClockViewModel Clock { get; }

    /// <summary>The window's zoom, 80-200% (WCAG 1.4.4): the library's ZoomLevel, which MainWindow attaches to its content
    /// with Ctrl+Plus / Ctrl+Minus / Ctrl+0; the tiles and cards wrap and the views scroll, so nothing is cut off as it grows.</summary>
    public ZoomLevel Zoom { get; }
    public IReadOnlyList<string> Providers => SessionControl.Providers;

    public ICommand SelectCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand StartCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand UseTicketCommand { get; }

    /// <summary>A ticket id anywhere in the window opens that ticket in Tickets Desktop, to view, approve, prioritize or
    /// close it there (M.ORCH-T500); the parameter is the id.</summary>
    public ICommand OpenInTicketsCommand { get; }

    private void OpenInTickets(string? ticketId)
    {
        if (MindAttic.Orchestrator.Tickets.TicketsDesktop.Open(ticketId, aggregator.Options) is { } error)
            Toasts.Insert(0, new ToastViewModel("Couldn't open it in Tickets Desktop", error, Palette.Over, null));
    }
    public ICommand DismissToastCommand { get; }
    public ICommand OpenToastCommand { get; }
    public ICommand ClearSearchCommand { get; }

    public string Search { get => search; set { if (Set(ref search, value)) Regroup(); } }
    public bool ShowQuiet { get => showQuiet; set { if (Set(ref showQuiet, value)) Regroup(); } }
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
    public bool IsWorkingFocus => Focus == Focus.Working;
    public bool IsTicketsFocus => Focus == Focus.Tickets;

    public const string SortByName = "Name", SortByTickets = "Open tickets";
    public IReadOnlyList<string> RepoSorts => RepoSortNames;
    private static readonly IReadOnlyList<string> RepoSortNames = [SortByName, SortByTickets];

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

    public bool HasSelection => Selected is not null;
    // Ctrl+click on the selected pill deselects it, writing null: Start would crash the dashboard on it.
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
    public string Login { get => login; set { if (value is not null) Set(ref login, value); } }

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
    public string Prompt { get => prompt; set => Set(ref prompt, value); }
    public string StatusMessage { get => statusMessage; private set => Set(ref statusMessage, value); }
    public string RefreshInfo { get => refreshInfo; private set => Set(ref refreshInfo, value); }

    /// <summary>Whether this window is the one process building snapshots (no daemon got the lease first), so it has to
    /// keep refreshing even while hidden: rl-status.json and allocate depend on it.</summary>
    public bool HoldsLease => lease.IsHeld;

    public int WorkingCount => snapshot?.Working ?? 0;
    public int WaitingCount => snapshot?.Waiting ?? 0;
    public int HostCount => snapshot?.LiveHosts ?? 0;
    public int TicketCount => snapshot?.OpenTickets ?? 0;
    public int RepoCount => snapshot?.Repos.Count(r => !r.Name.StartsWith('(')) ?? 0;
    public string WorkspaceRoot => aggregator.Options.WorkspaceRoot;
    public string TokensToday => Formatting.Tokens(snapshot?.UsageToday.Total ?? 0);
    public string TokensDetail
    {
        get
        {
            var u = snapshot?.UsageToday ?? default;
            return $"{Formatting.Tokens(u.Fresh)} uncached · {Formatting.Tokens(u.Output)} out";
        }
    }
    public IReadOnlyList<string> Warnings => snapshot?.Warnings ?? [];
    public bool HasWarnings => Warnings.Count > 0;

    /// <summary>The status bar's count of them: not only ticket files go wrong (billing, the approvals' move into the ticket
    /// database), and calling every one a ticket file problem sent you looking in the wrong place (M.ORCH-T465).</summary>
    public string WarningsText => WarningLine(Warnings.Count);

    internal static string WarningLine(int count) =>
        count == 1 ? "1 problem reading the workspace, hover for details" : $"{count} problems reading the workspace, hover for details";

    private bool isLoading;

    /// <summary>True until the first refresh finishes, either way (a failure shows in RefreshInfo): the full-screen
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
        if (refreshing) return;
        refreshing = true;
        try
        {
            var clock = Stopwatch.StartNew();
            var dirs = OwnLoginDirs();
            var (next, tabs, nextOrgs) = await BuiltAfterChanges(() => Task.Run(() =>
            {
                var built = aggregator.Refresh();
                // Each login's name and color, read with the snapshot, off the UI thread; and the organization's members, so
                // every one has its tab, one not signed in here yet a tab to sign in from (M.ORCH-T615, M.ORCH-T670); and the orgs,
                // which person tabs each one's switcher shows (M.ORCH-T655).
                IReadOnlyList<Member> members = [];
                try { members = MemberRoster.Load(aggregator.Db); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Data.Common.DbException) { }
                var orgDirectory = aggregator.Db.TryLoadOrgs();
                return (built, LoginScope.Tabs(built, dirs, members), orgDirectory);
            }), () => localChanges);
            ApplyTabs(next, tabs, nextOrgs);
            await TickAllocateAsync(next);
            var info = aggregator.LastWasShared
                ? $"Updated {next.TakenUtc.ToLocalTime():T} · shared"
                : $"Updated {DateTime.Now:T} · {clock.ElapsedMilliseconds} ms";
            foreach (var tab in LoginTabs) tab.RefreshInfo = info;
        }
        // Any failure is shown and retried next tick: this runs from an async void timer, so an escaped exception would
        // close the dashboard, and with it allocate and rl-status.json if this window holds the lease.
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            foreach (var tab in LoginTabs) tab.RefreshInfo = $"Refresh failed: {ex.Message}";
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
    }

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
        Regroup();
        ApplyPhases(next);
        ApplyBilling(next);
        ApplyReminders(DateTime.UtcNow);
        ApplyAccounts(all, DateTime.UtcNow);
        // Who's out of room is read with the roster, just above (M.ORCH-T668).
        if (IsGlobalTab) ApplyUserTickets();
        // After the phases' own toasts (pace, budget, stuck, audit, Needs you), or they'd go past the cap (M.ORCH-T340).
        PruneToasts(Toasts, DateTime.UtcNow);
        Raise(nameof(WorkingCount)); Raise(nameof(WaitingCount)); Raise(nameof(HostCount));
        Raise(nameof(TicketCount)); Raise(nameof(RepoCount)); Raise(nameof(TokensToday));
        Raise(nameof(TokensDetail)); Raise(nameof(Warnings)); Raise(nameof(HasWarnings)); Raise(nameof(WarningsText));
        Raise(nameof(TabBadge)); Raise(nameof(TabTip));
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

    public bool IsEmpty => Groups.All(g => !g.IsVisible);

    private void Select(RepoTileViewModel? tile) => Selected = Selected == tile ? null : tile;

    private async Task StartSessionAsync()
    {
        if (Selected is null) return;
        // What Start was pressed with: a sign-in first can take minutes, and the selection may change meanwhile.
        await StartSessionAsync(Selected, Provider, Login, string.IsNullOrWhiteSpace(Prompt) ? null : Prompt);
    }

    /// <summary>
    /// <c>MindAttic.Orchestrator.exe --start &lt;repo&gt;</c> (M.ORCH-T767, the Tickets desktop's Repos tab Start link): on the global
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
        string? configDir = null;
        var on = login;
        if (kind == "Claude" && ScopeEmail is { } own)
        {
            // This tab's login (M.ORCH-T613): the CLI opens on it, and shows on this tab.
            if (await OwnLoginDirAsync(own, m => StatusMessage = m) is not { } dir) return;
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
                return;
            }
            on = auto?.Email ?? GlobalLogin;
        }
        var error = control.Start(tile.Name, tile.Path, kind, text, configDir);
        StatusMessage = error ?? $"Opened a {kind} tab for {tile.Name}" + (configDir is null ? "" : $" on {on}'s own login") + ". It'll appear here within a few seconds.";
        if (error is null && Selected == tile) Prompt = "";
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
