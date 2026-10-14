using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using MindAttic.Orchestrator.Audit;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

public enum View { Repos, Queue, Audit }

/// <summary>Budget (phase 2), the cross-repo queue (phase 3) and the audit (phase 4).</summary>
public sealed partial class MainViewModel
{
    private Dispatcher dispatcher = null!;
    private AutoDispatch autoDispatch = null!;
    private AuditRunner auditRunner = null!;
    private SchedulePlan? plan;
    private BudgetLevel? lastBudgetLevel;
    private View view;
    private string viewChoice = "Repos";
    private bool isSettingsOpen;
    private bool isAuditing;
    private string auditInfo = "Not run yet.";
    private string queueMessage = "";

    private void InitPhases()
    {
        dispatcher = new Dispatcher(control, store);
        autoDispatch = new AutoDispatch(dispatcher);
        auditRunner = new AuditRunner(aggregator.Options, store);
        Settings = new SettingsViewModel(store, () => { IsSettingsOpen = false; _ = RefreshAsync(); });

        ToggleSettingsCommand = new RelayCommand(() => { Settings.Load(); IsSettingsOpen = !IsSettingsOpen; });
        RunAuditCommand = new RelayCommand(async () => await RunAuditAsync(), () => !IsAuditing);
        ShowViewCommand = new RelayCommand(p => { if (p is string s) ViewChoice = s; });

        if (auditRunner.Latest() is { } last) ShowAudit(last);
    }

    // ── View switching ──────────────────────────────────────────────────────

    public IReadOnlyList<string> ViewChoices { get; } = ["Repos", "Queue", "Audit"];

    public string ViewChoice
    {
        get => viewChoice;
        set
        {
            if (!Set(ref viewChoice, value)) return;
            view = Enum.TryParse<View>(value, out var v) ? v : View.Repos;
            Raise(nameof(IsReposView)); Raise(nameof(IsQueueView)); Raise(nameof(IsAuditView)); Raise(nameof(ShowDetail));
        }
    }

    public bool ShowDetail => IsReposView && HasSelection;

    public bool IsReposView => view == View.Repos;
    public bool IsQueueView => view == View.Queue;
    public bool IsAuditView => view == View.Audit;
    public ICommand ShowViewCommand { get; private set; } = null!;

    // ── Settings ────────────────────────────────────────────────────────────

    public SettingsViewModel Settings { get; private set; } = null!;
    public bool IsSettingsOpen { get => isSettingsOpen; set => Set(ref isSettingsOpen, value); }
    public ICommand ToggleSettingsCommand { get; private set; } = null!;

    // ── Budget ──────────────────────────────────────────────────────────────

    public BudgetStatus? Budget => snapshot?.Budget;
    public bool BudgetIsPeak => Budget?.BlockLimitIsPeak ?? true;

    public string BudgetHeadline => Budget?.CurrentBlock is null ? "Idle" : $"{Budget.BlockPercent:0}%";

    public string BudgetCaption => Budget switch
    {
        null => "",
        { CurrentBlock: null } => "No 5-hour block running",
        { BlockLimitIsPeak: true } => "of your busiest 5h block",
        _ => "of your 5-hour budget",
    };

    public string BudgetDetail
    {
        get
        {
            if (Budget is not { CurrentBlock: { } block } b)
                return Budget is null ? "" : Budget.WeeklyPercent is { } wp ? $"week {wp:0}%" : $"{Formatting.Tokens(Budget.Today.Fresh)} uncached today";
            var parts = new List<string> { $"resets {block.EndUtc.ToLocalTime():t}", $"{Formatting.Tokens((long)b.FreshPerHour)}/h" };
            if (b.ProjectedLimitUtc is { } at) parts.Add($"limit ~{at.ToLocalTime():t}");
            if (b.WeeklyPercent is { } weekPercent) parts.Add($"week {weekPercent:0}%");
            return string.Join(" · ", parts);
        }
    }

    public string BudgetTooltip
    {
        get
        {
            if (Budget is not { } b) return "";
            var lines = new List<string>
            {
                $"This block: {Formatting.Tokens(b.CurrentBlock?.Usage.Fresh ?? 0)} uncached of {Formatting.Tokens(b.BlockLimit)}" +
                    (b.BlockLimitIsPeak ? " (your busiest block in 7 days; set a budget in Settings to track a real limit)" : ""),
                $"Today: {Formatting.Tokens(b.Today.Fresh)} uncached, {Formatting.Tokens(b.Today.CacheRead)} cache reads, {Formatting.Tokens(b.Today.Output)} output",
                (b.WeekResetUtc is { } next
                    ? $"This week (since {b.WeekStartUtc.ToLocalTime():ddd h:mm tt}): "
                    : "Last 7 days (rolling; set the weekly reset in Settings): ") +
                    $"{Formatting.Tokens(b.Week.Fresh)} uncached" + (b.WeeklyPercent is { } w ? $" ({w:0}% of weekly budget)" : "") +
                    (b.WeekResetUtc is { } n ? $", resets {n.ToLocalTime():ddd h:mm tt}" : ""),
                "Uncached = input + cache writes + output. Cache reads are cheap and excluded from budgets.",
            };
            return string.Join("\n", lines);
        }
    }

    /// <summary>0..1 fill for the bar; peak mode can pass 100% but the bar stops full.</summary>
    public double BudgetFill => Math.Clamp((Budget?.CurrentBlock is null ? 0 : Budget.BlockPercent) / 100, 0, 1);

    public Brush BudgetBrush => Budget switch
    {
        null or { CurrentBlock: null } => Palette.Quiet,
        { BlockLimitIsPeak: true, Level: BudgetLevel.Ok } => Palette.Tickets,
        { Level: BudgetLevel.Over } => Palette.Over,
        { Level: BudgetLevel.Warn } => Palette.Waiting,
        _ => Palette.Working,
    };

    // ── Queue ───────────────────────────────────────────────────────────────

    public ObservableCollection<QueueRowViewModel> Queue { get; } = [];
    public ObservableCollection<QueueRowViewModel> Proposals { get; } = [];
    public ObservableCollection<FinishedViewModel> Finished { get; } = [];
    public string Hold => plan?.Hold ?? "";
    public bool HasHold => !string.IsNullOrEmpty(Hold);
    public string SlotsText => plan is null ? "" : $"{plan.Occupied} sessions open · fills up to {plan.Slots}";
    public string AutoDispatchText => store.LoadSettings().AutoDispatch
        ? "Auto-dispatch is on: proposals open by themselves, one a minute, prompt pre-filled and unsent."
        : "Proposals wait for you. Turn on auto-dispatch in Settings to let them open by themselves.";
    public string QueueMessage { get => queueMessage; private set => Set(ref queueMessage, value); }
    public int QueueCount => Queue.Count(q => !q.IsAssigned);

    public async void Dispatch(QueueItem item, bool worktree)
    {
        QueueMessage = $"Starting {item.Id}…";
        var providerKey = store.LoadSettings().DefaultProvider;
        var r = await Task.Run(() => dispatcher.Dispatch(item, providerKey, worktree));
        QueueMessage = r.Message;
        await RefreshAsync();
    }

    public void Bump(string ticketId, int by)
    {
        var s = store.LoadSettings();
        var p = new Dictionary<string, int>(s.Priorities, StringComparer.Ordinal);
        var next = p.GetValueOrDefault(ticketId) + by;
        if (next == 0) p.Remove(ticketId); else p[ticketId] = next;
        store.SaveSettings(s with { Priorities = p });
        if (snapshot is not null) ApplyPlan(snapshot);
    }

    public void Release(string ticketId)
    {
        dispatcher.Release(ticketId);
        QueueMessage = $"{ticketId} is back in the queue.";
        if (snapshot is not null) ApplyPlan(snapshot);
    }

    public async void Finish(Assignment a)
    {
        var repoPath = snapshot?.Repos.FirstOrDefault(r => r.Name == a.Repo)?.Path ?? "";
        var r = await Task.Run(() => dispatcher.RemoveWorktree(a, repoPath));
        QueueMessage = r.Message;
        if (snapshot is not null) ApplyPlan(snapshot);
    }

    // ── Audit ───────────────────────────────────────────────────────────────

    public ObservableCollection<DuplicateGroupViewModel> Duplicates { get; } = [];
    public ObservableCollection<DriftViewModel> Drift { get; } = [];
    public ObservableCollection<MissingViewModel> Missing { get; } = [];
    public bool IsAuditing { get => isAuditing; private set => Set(ref isAuditing, value); }
    public string AuditInfo { get => auditInfo; private set => Set(ref auditInfo, value); }
    public string DuplicatesHeader => $"DUPLICATED ACROSS REPOS · {Duplicates.Count}";
    public string DriftHeader => $"SHARED AGENT FILES THAT DIFFER · {Drift.Count}";
    public string MissingHeader => $"SHARED AGENT FILES SOME REPOS LACK · {Missing.Count}";
    public ICommand RunAuditCommand { get; private set; } = null!;

    private async Task RunAuditAsync()
    {
        IsAuditing = true;
        AuditInfo = "Scanning every repo…";
        try { ShowAudit(await Task.Run(() => auditRunner.Run())); }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { AuditInfo = $"Audit failed: {ex.Message}"; }
        finally { IsAuditing = false; }
    }

    private void ShowAudit(AuditReport report)
    {
        var root = aggregator.Options.WorkspaceRoot;
        Duplicates.ReorderTo([.. report.Duplicates.Select(g => new DuplicateGroupViewModel(g, root))]);
        Drift.ReorderTo([.. report.Drift.Where(d => !d.IsCustom).Concat(report.Drift.Where(d => d.IsCustom)).Select(d => new DriftViewModel(d))]);
        Missing.ReorderTo([.. report.Missing.GroupBy(m => m.File).OrderByDescending(g => g.First().HaveIt).Select(g => new MissingViewModel(g.Key, [.. g]))]);
        AuditInfo = $"{report.ReposScanned} repos, {report.FilesScanned} source files, {report.Took.TotalSeconds:0.0}s · {report.RanUtc.ToLocalTime():g}";
        Raise(nameof(DuplicatesHeader)); Raise(nameof(DriftHeader)); Raise(nameof(MissingHeader));
    }

    // ── Per-refresh ─────────────────────────────────────────────────────────

    private void ApplyPhases(Snapshot next)
    {
        foreach (var name in new[] { nameof(Budget), nameof(BudgetIsPeak), nameof(BudgetHeadline), nameof(BudgetCaption),
                                     nameof(BudgetDetail), nameof(BudgetTooltip), nameof(BudgetFill), nameof(BudgetBrush) })
            Raise(name);

        if (next.Budget is { } b)
        {
            if (lastBudgetLevel is { } was && b.Level > was)
                Toasts.Insert(0, new ToastViewModel(
                    b.Level == BudgetLevel.Over ? "Over budget" : "Budget warning",
                    $"{b.WorstPercent:0}% used. The scheduler is holding new work.",
                    b.Level == BudgetLevel.Over ? Palette.Over : Palette.Waiting, null));
            lastBudgetLevel = b.Level;
        }

        ApplyPlan(next);

        if (plan is not null && autoDispatch.Tick(plan, store.LoadSettings(), DateTime.UtcNow) is { } r)
        {
            QueueMessage = r.Message;
            Toasts.Insert(0, new ToastViewModel(r.Ok ? "Auto-dispatched" : "Auto-dispatch failed", r.Message,
                r.Ok ? Palette.Working : Palette.Over, null));
        }
    }

    private void ApplyPlan(Snapshot next)
    {
        plan = Scheduler.Plan(next, store.LoadSettings(), store.LoadAssignments());
        var proposals = plan.Proposals.ToDictionary(p => p.Item.Id, StringComparer.Ordinal);
        var rank = 0;
        Queue.SyncBy(plan.Queue, q => q.Id, r => r.Id,
            q => { var row = new QueueRowViewModel(q, this); row.Update(q, ++rank, proposals.GetValueOrDefault(q.Id)); return row; },
            (row, q) => row.Update(q, ++rank, proposals.GetValueOrDefault(q.Id)));
        Proposals.ReorderTo([.. Queue.Where(q => q.IsNext)]);
        Finished.SyncBy(plan.Finished, a => a.TicketId, f => f.TicketId, a => new FinishedViewModel(a, this), (_, _) => { });

        foreach (var name in new[] { nameof(Hold), nameof(HasHold), nameof(SlotsText), nameof(AutoDispatchText), nameof(QueueCount) })
            Raise(name);
    }
}
