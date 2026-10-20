using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
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
    private Focus focus;
    private RepoTileViewModel? selected;
    private string provider = "Claude";
    private string prompt = "";
    private string statusMessage = "";
    private string refreshInfo = "Loading…";

    public MainViewModel()
    {
        lease = new DispatchLease(store.Directory);
        aggregator = new StatusAggregator(OrchestratorOptions.Load(), store, new RateLimitStatusWriter(), lease);
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
        OpenToastCommand = new RelayCommand(p =>
        {
            if (p is not ToastViewModel t) return;
            Toasts.Remove(t);
            // To the repo's detail, from whichever view: not Select, a toggle that only shows in the Repos view.
            if (t.Target is not null) SelectRepo(t.Target.Name);
        });
        ClearSearchCommand = new RelayCommand(() => Search = "");
        OpenInWorkflowCommand = new RelayCommand(p => OpenInWorkflow(p as string));
        InitPhases();
    }

    public TileGroupViewModel Waiting { get; }
    public TileGroupViewModel Working { get; }
    public TileGroupViewModel Tickets { get; }
    public TileGroupViewModel Quiet { get; }
    public IReadOnlyList<TileGroupViewModel> Groups { get; }
    public ObservableCollection<ToastViewModel> Toasts { get; } = [];

    /// <summary>The header's live clock, ticking every second on its own timer, not with the refresh (M.ORCH-T193).</summary>
    public ClockViewModel Clock { get; } = new();
    public IReadOnlyList<string> Providers => SessionControl.Providers;

    public ICommand SelectCommand { get; }
    public ICommand FocusCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand StartCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand UseTicketCommand { get; }

    /// <summary>A ticket id anywhere in the window opens that ticket in Workflow Desktop, to view, approve, prioritize or
    /// close it there (M.ORCH-T500); the parameter is the id.</summary>
    public ICommand OpenInWorkflowCommand { get; }

    private void OpenInWorkflow(string? ticketId)
    {
        if (MindAttic.Orchestrator.Tickets.WorkflowDesktop.Open(ticketId, aggregator.Options) is { } error)
            Toasts.Insert(0, new ToastViewModel("Couldn't open it in Workflow", error, Palette.Over, null));
    }
    public ICommand DismissToastCommand { get; }
    public ICommand OpenToastCommand { get; }
    public ICommand ClearSearchCommand { get; }

    public string Search { get => search; set { if (Set(ref search, value)) Regroup(); } }
    public bool ShowQuiet { get => showQuiet; set { if (Set(ref showQuiet, value)) Regroup(); } }
    public Focus Focus { get => focus; private set => Set(ref focus, value); }

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
            Raise();
            Raise(nameof(HasSelection));
            Raise(nameof(ShowDetail));
        }
    }

    public bool HasSelection => Selected is not null;
    // Ctrl+click on the selected pill deselects it, writing null: Start would crash the dashboard on it.
    public string Provider { get => provider; set { if (value is not null) Set(ref provider, value); } }
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

    /// <summary>The status bar's count of them: not only ticket files go wrong (billing, the approvals' move into the Workflow
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
        ToastHandoffQuiet(MindAttic.Orchestrator.Sessions.Handoff.Read(new MindAttic.Orchestrator.Sessions.ContextCycler().Directory));
        // After the phases' own toasts (pace, budget, stuck, audit, Needs you), or they'd go past the cap (M.ORCH-T340).
        PruneToasts(Toasts, DateTime.UtcNow);
        Raise(nameof(WorkingCount)); Raise(nameof(WaitingCount)); Raise(nameof(HostCount));
        Raise(nameof(TicketCount)); Raise(nameof(RepoCount)); Raise(nameof(TokensToday));
        Raise(nameof(TokensDetail)); Raise(nameof(Warnings)); Raise(nameof(HasWarnings)); Raise(nameof(WarningsText));
    }

    private string? toastedQuiet;

    /// <summary>/handoff (M.ORCH-T481): once every CLI is quiet, one toast says it's safe to switch accounts (the daemon texts it too).</summary>
    internal void ToastHandoffQuiet(MindAttic.Orchestrator.Sessions.HandoffMark? mark)
    {
        if (mark is not { State: MindAttic.Orchestrator.Sessions.Handoff.Quiet, QuietAt: { } at } || at == toastedQuiet) return;
        toastedQuiet = at;
        Toasts.Insert(0, new ToastViewModel("All quiet", MindAttic.Orchestrator.Sessions.Handoff.QuietLine + ": nothing is working in any CLI (orchestrator handoff status lists who saved).", Palette.Quiet, null));
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

    private void Regroup()
    {
        var visible = tiles.Values.Where(t => t.Matches(Search)).ToList();
        Waiting.Tiles.ReorderTo([.. visible.Where(t => t.State == RepoState.Waiting)
            .OrderBy(t => t.Status?.LastActivityUtc ?? DateTime.MaxValue)]);
        Working.Tiles.ReorderTo([.. visible.Where(t => t.State == RepoState.Working)
            .OrderByDescending(t => t.TokensToday)]);
        Tickets.Tiles.ReorderTo([.. visible.Where(t => t.State == RepoState.Dormant && t.OpenTicketCount > 0)
            .OrderByDescending(t => t.OpenTicketCount).ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)]);
        Quiet.Tiles.ReorderTo([.. visible.Where(t => t.State == RepoState.Dormant && t.OpenTicketCount == 0)
            .OrderByDescending(t => t.Status?.LastActivityUtc ?? DateTime.MinValue).ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)]);

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
        var error = control.Start(Selected.Name, Selected.Path, Provider, string.IsNullOrWhiteSpace(Prompt) ? null : Prompt);
        StatusMessage = error ?? $"Opened a {Provider} tab for {Selected.Name}. It'll appear here within a few seconds.";
        if (error is null) Prompt = "";
    }
}
