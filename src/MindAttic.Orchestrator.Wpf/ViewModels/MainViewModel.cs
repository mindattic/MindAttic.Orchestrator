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
    public DateTime CreatedUtc { get; } = DateTime.UtcNow;

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
            if (t.Target is not null) Select(t.Target);
        });
        ClearSearchCommand = new RelayCommand(() => Search = "");
        InitPhases();
    }

    public TileGroupViewModel Waiting { get; }
    public TileGroupViewModel Working { get; }
    public TileGroupViewModel Tickets { get; }
    public TileGroupViewModel Quiet { get; }
    public IReadOnlyList<TileGroupViewModel> Groups { get; }
    public ObservableCollection<ToastViewModel> Toasts { get; } = [];
    public IReadOnlyList<string> Providers => SessionControl.Providers;

    public ICommand SelectCommand { get; }
    public ICommand FocusCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand StartCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand UseTicketCommand { get; }
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
    public string Provider { get => provider; set => Set(ref provider, value); }
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

    public async Task RefreshAsync()
    {
        if (refreshing) return;
        refreshing = true;
        try
        {
            var clock = Stopwatch.StartNew();
            var next = await Task.Run(() => aggregator.Refresh());
            Apply(next);
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
        finally { refreshing = false; }
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
        foreach (var stale in Toasts.Where(t => DateTime.UtcNow - t.CreatedUtc > (t.Lifetime ?? ToastLifetime) ||
                                                t.Target is { State: not RepoState.Waiting }).ToList())
            Toasts.Remove(stale);
        while (Toasts.Count > 4) Toasts.RemoveAt(Toasts.Count - 1);

        snapshot = next;
        Regroup();
        ApplyPhases(next);
        Raise(nameof(WorkingCount)); Raise(nameof(WaitingCount)); Raise(nameof(HostCount));
        Raise(nameof(TicketCount)); Raise(nameof(RepoCount)); Raise(nameof(TokensToday));
        Raise(nameof(TokensDetail)); Raise(nameof(Warnings)); Raise(nameof(HasWarnings));
    }

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

        var searching = !string.IsNullOrWhiteSpace(Search);
        Waiting.IsVisible = Waiting.Count > 0 && Focus is Focus.All or Focus.Waiting;
        Working.IsVisible = Working.Count > 0 && Focus is Focus.All or Focus.Working;
        Tickets.IsVisible = Tickets.Count > 0 && Focus is Focus.All or Focus.Tickets;
        Quiet.IsVisible = Quiet.Count > 0 && Focus is Focus.All && (ShowQuiet || searching);
        foreach (var g in Groups) g.Touch();
        Raise(nameof(IsEmpty));
    }

    public bool IsEmpty => Groups.All(g => !g.IsVisible);

    private void FocusOn(Focus next)
    {
        Focus = Focus == next ? Focus.All : next;
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
