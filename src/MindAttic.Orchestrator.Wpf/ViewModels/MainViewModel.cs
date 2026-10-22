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
    public string Title { get; } = title;
    public string Body { get; } = body;
    public Brush Accent { get; } = accent;
    public RepoTileViewModel? Target { get; } = target;
}

public sealed partial class MainViewModel : ObservableObject
{
    private static readonly TimeSpan ToastLifetime = TimeSpan.FromMinutes(2);

    private readonly JsonStore store = new();
    // Shared with the headless daemon (same lock file): whichever process grabs it first is the one that
    // dispatches autopilot and writes rl-status.json, so every open window doesn't race to overwrite it
    // (M.ORCH-T112); released when this process exits, same as the daemon's own.
    private readonly DispatchLease lease;
    private readonly StatusAggregator aggregator;
    private readonly SessionControl control;
    private readonly Dictionary<string, RepoTileViewModel> tiles = new(StringComparer.OrdinalIgnoreCase);
    private Snapshot? snapshot;
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

    public MainViewModel()
    {
        lease = new DispatchLease(store.Directory, db: store.Db);
        aggregator = new StatusAggregator(OrchestratorOptions.Load(), store, new RateLimitStatusWriter { Db = store.Db }, lease);
        control = new SessionControl(aggregator.Options);
        Waiting = new("Needs you", "Sessions that stopped and are waiting for input, longest wait first", Palette.Waiting);
        Working = new("Working", "Agents mid-turn right now", Palette.Working);
        Tickets = new("Open tickets, no session", "Work that's queued but nobody's on", Palette.Tickets);
        Quiet = new("Quiet", "No session and no open tickets", Palette.Quiet);
        Groups = [Waiting, Working, Tickets, Quiet];

        SelectCommand = new RelayCommand(p => Select(p as RepoTileViewModel));
        FocusCommand = new RelayCommand(p => FocusOn(p is Focus f ? f : Enum.TryParse<Focus>(p as string, out var g) ? g : Focus.All));
        RefreshCommand = new RelayCommand(async () => await RefreshAsync());
        StartCommand = new RelayCommand(StartSession, () => Selected is { HasFolder: true });
        OpenFolderCommand = new RelayCommand(() => SessionControl.OpenFolder(Selected!.Path), () => Selected is { HasFolder: true });
        UseTicketCommand = new RelayCommand(p => { if (p is TicketRowViewModel t) Prompt = $"Work on ticket {t.Id}: {t.Summary}"; });
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
        OpenInTicketsCommand = new RelayCommand(p => OpenInTickets(p as string));
        InitPhases();
        if (store.Load<ReposView>(ReposViewName).Sort is { } sort && RepoSorts.Contains(sort)) repoSort = sort;
    }

    public TileGroupViewModel Waiting { get; }
    public TileGroupViewModel Working { get; }
    public TileGroupViewModel Tickets { get; }
    public TileGroupViewModel Quiet { get; }
    public IReadOnlyList<TileGroupViewModel> Groups { get; }
    public ObservableCollection<ToastViewModel> Toasts { get; } = [];

    /// <summary>The header's live clock, ticking every second on its own timer, not with the refresh (M.ORCH-T193).</summary>
    public ClockViewModel Clock { get; } = new();

    /// <summary>The window's zoom, 80-200% (WCAG 1.4.4): the library's ZoomLevel, which MainWindow attaches to its content
    /// with Ctrl+Plus / Ctrl+Minus / Ctrl+0; the tiles and cards wrap and the views scroll, so nothing is cut off as it grows.</summary>
    public ZoomLevel Zoom { get; } = new();
    public IReadOnlyList<string> Providers => SessionControl.Providers;

    public ICommand SelectCommand { get; }
    public ICommand FocusCommand { get; }
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
    public Focus Focus { get => focus; private set => Set(ref focus, value); }

    public const string SortByName = "Name", SortByTickets = "Open tickets";
    public IReadOnlyList<string> RepoSorts { get; } = [SortByName, SortByTickets];

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
            Raise(nameof(ShowDetail));
        }
    }

    public bool HasSelection => Selected is not null;
    // Ctrl+click on the selected pill deselects it, writing null: Start would crash the dashboard on it.
    public string Provider { get => provider; set { if (value is not null && Set(ref provider, value)) Raise(nameof(ShowLogins)); } }

    /// <summary>What <see cref="Logins"/> lists for the login every CLI uses unless started on one of its own.</summary>
    public const string GlobalLogin = "Global login";

    /// <summary>What <see cref="Logins"/> lists first, and starts on by default (M.ORCH-T605): one CLI per account, a per-CLI
    /// login no CLI runs on yet (most headroom first), else the global login (<see cref="LoginPicker"/>).</summary>
    public const string AutoLogin = "Auto";

    private readonly AccountDirs accountDirs = new();
    private string login = GlobalLogin;

    /// <summary>The logins a Claude tab can start on (M.ORCH-T559): Auto (unless turned off in Settings), the global one, then
    /// each per-CLI login signed in to (orchestrator login --account), as the CLI's <c>start --account</c> takes them.</summary>
    public IReadOnlyList<string> Logins { get; private set; } = [GlobalLogin];

    /// <summary>Shown only for Claude, and only once there's a per-CLI login to choose.</summary>
    public bool ShowLogins => Provider == "Claude" && Logins.Any(l => l != GlobalLogin && l != AutoLogin);

    // Ctrl+click on the selected pill deselects it, writing null: it stays on the one it was.
    public string Login { get => login; set { if (value is not null) Set(ref login, value); } }

    private void RefreshLogins()
    {
        var auto = true;
        try { auto = store.LoadSettings().AutoLogin; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { }
        IReadOnlyList<string> head = auto ? [AutoLogin, GlobalLogin] : [GlobalLogin];
        IReadOnlyList<string> found;
        try { found = [.. head, .. accountDirs.SignedIn().Select(a => a.Email)]; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { found = head; }
        if (found.SequenceEqual(Logins)) return;
        // Auto just offered (first time, or turned back on) is the default; a login gone falls back to the first choice.
        var autoNew = auto && !Logins.Contains(AutoLogin);
        Logins = found;
        if (autoNew || !found.Contains(Login)) Login = found[0];
        Raise(nameof(Logins));
        Raise(nameof(ShowLogins));
    }
    public string Prompt { get => prompt; set => Set(ref prompt, value); }
    public string StatusMessage { get => statusMessage; private set => Set(ref statusMessage, value); }
    public string RefreshInfo { get => refreshInfo; private set => Set(ref refreshInfo, value); }

    /// <summary>Whether this window is the one process building snapshots (no daemon got the lease first), so it has to
    /// keep refreshing even while hidden: rl-status.json and autopilot depend on it.</summary>
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

    private bool isLoading = true;

    /// <summary>True until the first refresh finishes, either way (a failure shows in RefreshInfo): the full-screen
    /// loader covers the dashboard until then, so it never shows empty or half-filled panels (M.ORCH-T184).</summary>
    public bool IsLoading { get => isLoading; private set => Set(ref isLoading, value); }

    public async Task RefreshAsync()
    {
        if (refreshing) return;
        refreshing = true;
        try
        {
            var clock = Stopwatch.StartNew();
            var next = await BuiltAfterChanges(() => Task.Run(() => aggregator.Refresh()), () => localChanges);
            Apply(next);
            await TickAutopilotAsync(next);
            RefreshInfo = aggregator.LastWasShared
                ? $"Updated {next.TakenUtc.ToLocalTime():T} · shared"
                : $"Updated {DateTime.Now:T} · {clock.ElapsedMilliseconds} ms";
        }
        // Any failure is shown and retried next tick: this runs from an async void timer, so an escaped exception would
        // close the dashboard, and with it autopilot and rl-status.json if this window holds the lease.
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            RefreshInfo = $"Refresh failed: {ex.Message}";
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
    private void NoteLocalChange() => localChanges++;

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

    private void Apply(Snapshot next)
    {
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
        ApplyAccounts(next, DateTime.UtcNow);
        // The Accounts view's cards, read off the UI thread, only while it's open (M.ORCH-T603).
        _ = RefreshLoginCardsAsync();
        // After the phases' own toasts (pace, budget, stuck, audit, Needs you), or they'd go past the cap (M.ORCH-T340).
        PruneToasts(Toasts, DateTime.UtcNow);
        Raise(nameof(WorkingCount)); Raise(nameof(WaitingCount)); Raise(nameof(HostCount));
        Raise(nameof(TicketCount)); Raise(nameof(RepoCount)); Raise(nameof(TokensToday));
        Raise(nameof(TokensDetail)); Raise(nameof(Warnings)); Raise(nameof(HasWarnings)); Raise(nameof(WarningsText));
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

    private void FocusOn(Focus next)
    {
        // A second click clears the filter only where it shows: from another view it opens Repos filtered again.
        Focus = Focus == next && IsReposView ? Focus.All : next;
        if (Focus != Focus.All) ViewChoice = "Repos"; // the cards filter the repo tiles, so show them
        Regroup();
    }

    private void Select(RepoTileViewModel? tile) => Selected = Selected == tile ? null : tile;

    private void StartSession()
    {
        if (Selected is null) return;
        string? configDir = null;
        var on = Login;
        if (Provider == "Claude" && Login == AutoLogin)
        {
            // Auto (M.ORCH-T605): a free per-CLI login, else the global one; held for this tab so the next Start goes elsewhere.
            var auto = new LoginPicker(accountDirs).ForNewTab(Provider, autoLogin: true, DateTime.UtcNow, out _, weeklyStopPercent);
            configDir = auto?.Directory;
            on = auto?.Email ?? GlobalLogin;
        }
        else if (ShowLogins && Login != GlobalLogin)
        {
            var dir = accountDirs.For(Login);
            if (!dir.SignedIn)
            {
                StatusMessage = $"{Login} has no login of its own any more: run orchestrator login --account {Login} once.";
                return;
            }
            try { accountDirs.Prepare(dir); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { StatusMessage = $"Couldn't prepare {Login}'s login: {ex.Message}"; return; }
            configDir = dir.Directory;
        }
        var error = control.Start(Selected.Name, Selected.Path, Provider, string.IsNullOrWhiteSpace(Prompt) ? null : Prompt, configDir);
        StatusMessage = error ?? $"Opened a {Provider} tab for {Selected.Name}" + (configDir is null ? "" : $" on {on}'s own login") + ". It'll appear here within a few seconds.";
        if (error is null) Prompt = "";
    }
}
