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
    private PaceMode? lastPaceMode;
    private readonly HashSet<string> knownStuck = new(StringComparer.Ordinal);
    private AuditReport? shownAudit;
    private DateTime auditFileStamp;
    private bool briefed;
    private DateTime seenSavedUtc;

    private sealed class Seen { public DateTime LastSeenUtc { get; init; } }
    private View view;
    private string viewChoice = "Repos";
    private bool isSettingsOpen;
    private bool isAuditing;
    private string auditInfo = "Not run yet.";
    private string queueMessage = "";

    private void InitPhases()
    {
        dispatcher = new Dispatcher(control, store);
        // Held until the app exits; while the logon daemon holds it, the daemon dispatches and this window only shows.
        autoDispatch = new AutoDispatch(dispatcher, lease: new DispatchLease(store.Directory));
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
        { BlockLimitSource: LimitSource.Learned } => "of your learned 5-hour limit",
        { BlockLimitSource: LimitSource.Reported } => "of your 5-hour limit (Claude's count)",
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
                LimitsLine(b),
            };
            if (Pace is { } p) lines.Add(p.Note);
            if (snapshot is not null && Burndown.Compute(b, snapshot.History, snapshot.TakenUtc) is { } week)
                lines.Add(week switch
                {
                    { Limit: null } => $"Week: {Formatting.Tokens(week.Used)} at {Formatting.Tokens((long)(week.PerHour * 24))}/day (no weekly limit known yet).",
                    { RunsOut: true } => $"Week: at {Formatting.Tokens((long)(week.PerHour * 24))}/day the weekly limit runs out {week.ExhaustedUtc!.Value.ToLocalTime():ddd h:mm tt}, before the {week.ResetUtc!.Value.ToLocalTime():ddd h:mm tt} reset.",
                    _ => $"Week: at {Formatting.Tokens((long)(week.PerHour * 24))}/day it ends with room to spare.",
                });
            foreach (var u in snapshot?.ProviderUsage.Where(u => u.Provider != MindAttic.Orchestrator.Providers.Claude) ?? [])
                lines.Add($"{u.Provider}: {Formatting.Tokens(u.Today.Fresh)} uncached today, {Formatting.Tokens(u.Week.Fresh)} this week" +
                    string.Concat(u.Limits.OrderBy(l => l.Window).Select(l => $"; {l.Window} {l.UsedPercent:0}% by its own count")));
            return string.Join("\n", lines);
        }
    }

    private static string LimitsLine(BudgetStatus b)
    {
        string Of(LimitSource? source, long? limit, LimitKind kind, bool outdated) => source switch
        {
            LimitSource.Manual => $"{Formatting.Tokens(limit ?? 0)}, set by you",
            LimitSource.Reported => $"{Formatting.Tokens(limit ?? 0)}, from Claude's own usage percent",
            LimitSource.Learned => $"{Formatting.Tokens(limit ?? 0)}, learned from your last {b.Calibration?.Basis(kind)} limit hits",
            _ when outdated => "relearning: already spent past the learned limit without hitting it",
            _ => "none yet",
        };
        return $"Limits: 5-hour {Of(b.BlockLimitIsPeak ? null : b.BlockLimitSource, b.BlockLimit, LimitKind.Session, b.BlockLimitOutdated)}; " +
               $"weekly {Of(b.WeeklyLimitSource, b.WeeklyLimit, LimitKind.Weekly, b.WeeklyLimitOutdated)}.";
    }

    // ── Pace ────────────────────────────────────────────────────────────────

    public Pace? Pace => snapshot?.Pace;

    /// <summary>Replaces the budget card's detail line while pacing is holding work back; empty otherwise.</summary>
    public string PaceText => Pace switch
    {
        null or { IsPaced: false } or { Mode: PaceMode.Open } => "",
        { Mode: PaceMode.Throttled } p => $"Paced to {p.SessionCap} of {store.LoadSettings().MaxConcurrentSessions} · resets {p.ResetUtc?.ToLocalTime():t}",
        { Mode: PaceMode.Coast } p => $"Coasting · resets {p.ResetUtc?.ToLocalTime():t}",
        { } p => $"At the limit until {p.ResetUtc?.ToLocalTime():t}",
    };

    public Brush PaceBrush => Pace?.Mode switch
    {
        PaceMode.Open => Palette.Working,
        PaceMode.Throttled => Palette.Waiting,
        PaceMode.Coast or PaceMode.Over => Palette.Over,
        _ => Palette.Quiet,
    };

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
    public string SlotsText => plan is null ? ""
        : plan.Cap < plan.Slots ? $"{plan.Occupied} sessions open · pacing allows {plan.Cap} of {plan.Slots}"
        : $"{plan.Occupied} sessions open · fills up to {plan.Slots}";
    public string AutoDispatchText => !store.LoadSettings().AutoDispatch
        ? "Proposals wait for you. Turn on auto-dispatch in Settings to let them open by themselves."
        : autoDispatch.Quiet is { } quiet
            ? $"Auto-dispatch is on but paused for quiet hours ({quiet}); proposals wait until it ends."
        : autoDispatch.HeldElsewhere
            ? "Auto-dispatch is on and running in the background daemon: proposals open from there, one a minute, prompt pre-filled and unsent."
            : "Auto-dispatch is on: proposals open by themselves, one a minute, prompt pre-filled and unsent.";
    public string QueueMessage { get => queueMessage; private set => Set(ref queueMessage, value); }
    public int QueueCount => Queue.Count(q => !q.IsAssigned);

    public async void Dispatch(QueueItem item, bool worktree)
    {
        QueueMessage = $"Starting {item.Id}…";
        var providerKey = store.LoadSettings().DefaultProvider;
        var r = await Task.Run(() => dispatcher.Dispatch(item, providerKey, worktree));
        QueueMessage = r.Message;
        Record(r, "Dispatch");
        await RefreshAsync();
    }

    private static readonly Serilog.ILogger queueLog = OrchestratorLog.For("Queue");

    private static void Record(Dispatcher.Result r, string action)
    {
        if (r.Ok) queueLog.Information("{Action:l}: {Message:l}", action, r.Message);
        else queueLog.Error("{Action:l} failed: {Message:l}", action, r.Message);
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
        queueLog.Information("Released {Ticket:l}", ticketId);
        if (snapshot is not null) ApplyPlan(snapshot);
    }

    // ── Tray and HUD ────────────────────────────────────────────────────────

    /// <summary>The workspace in one line, for the tray tooltip and the HUD.</summary>
    public string TraySummary => $"Budget {BudgetHeadline}" +
        (Pace is { IsPaced: true } p ? $" · {p.Key}" : "") +
        $" · {WaitingCount} need you · {WorkingCount} working";

    public bool AutoDispatchOn => store.LoadSettings().AutoDispatch;

    public IReadOnlyList<string> WaitingRepos => snapshot?.Repos.Where(r => r.State == RepoState.Waiting).Select(r => r.Name).ToList() ?? [];

    public void ToggleAutoDispatch()
    {
        var s = store.LoadSettings();
        store.SaveSettings(s with { AutoDispatch = !s.AutoDispatch });
        queueLog.Information("Auto-dispatch turned {State:l} from the tray", s.AutoDispatch ? "off" : "on");
        Raise(nameof(AutoDispatchText));
    }

    /// <summary>Starts the top proposal, if there is one; returns what it said.</summary>
    public string DispatchNext()
    {
        if (plan?.Proposals.FirstOrDefault() is not { } p) return plan?.Hold ?? "Nothing is ready to start.";
        Dispatch(p.Item, p.UseWorktree);
        return $"Starting {p.Item.Id}…";
    }

    public void SelectRepo(string name)
    {
        ViewChoice = "Repos";
        if (Waiting.Tiles.Concat(Working.Tiles).Concat(Tickets.Tiles).FirstOrDefault(t => t.Name == name) is { } tile) Selected = tile;
    }

    /// <summary>The ticket's sessions and commits in a window: what happened, without digging through Launcher history.</summary>
    public async void ShowDetails(Assignment a)
    {
        var repoPath = snapshot?.Repos.FirstOrDefault(r => r.Name == a.Repo)?.Path ?? a.Directory;
        var text = await Task.Run(() => MindAttic.Orchestrator.Tickets.TicketReplay.PlainReport(a.TicketId, repoPath, aggregator.Options.ClaudeProjectsDir));
        var box = new System.Windows.Controls.TextBox
        {
            Text = text, IsReadOnly = true, FontFamily = new FontFamily("Consolas"), FontSize = 12,
            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
        };
        new System.Windows.Window { Title = $"{a.TicketId} in {a.Repo}", Width = 900, Height = 650, Content = box,
            Owner = System.Windows.Application.Current.MainWindow }.Show();
    }

    public async void Finish(Assignment a)
    {
        var repoPath = snapshot?.Repos.FirstOrDefault(r => r.Name == a.Repo)?.Path ?? "";
        var r = await Task.Run(() => dispatcher.RemoveWorktree(a, repoPath));
        QueueMessage = r.Message;
        Record(r, "Remove worktree");
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
        shownAudit = report;
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
                                     nameof(BudgetDetail), nameof(BudgetTooltip), nameof(BudgetFill), nameof(BudgetBrush),
                                     nameof(Pace), nameof(PaceText), nameof(PaceBrush) })
            Raise(name);

        // Paced, the pace is what changes what the scheduler does, so it's what gets a toast.
        if (next.Pace is { IsPaced: true } pace)
        {
            if (lastPaceMode is { } was && pace.Mode > was && pace.Mode != PaceMode.Open)
                Toasts.Insert(0, new ToastViewModel(
                    pace.Mode switch { PaceMode.Throttled => "Pacing", PaceMode.Coast => "Coasting", _ => "At the limit" },
                    pace.Note, pace.Mode == PaceMode.Throttled ? Palette.Waiting : Palette.Over, null));
            lastPaceMode = pace.Mode;
            lastBudgetLevel = next.Budget?.Level;
        }
        else if (next.Budget is { } b)
        {
            if (lastBudgetLevel is { } was && b.Level > was)
                Toasts.Insert(0, new ToastViewModel(
                    b.Level == BudgetLevel.Over ? "Over budget" : "Budget warning",
                    $"{b.WorstPercent:0}% used. The scheduler is holding new work.",
                    b.Level == BudgetLevel.Over ? Palette.Over : Palette.Waiting, null));
            lastBudgetLevel = b.Level;
            lastPaceMode = null;
        }

        var stuck = MindAttic.Orchestrator.Activity.Watchdog.Stuck(next);
        foreach (var x in stuck.Where(x => knownStuck.Add(x.SessionId + x.Loop.Tool + x.Loop.Input)))
            Toasts.Insert(0, new ToastViewModel("Looks stuck", $"A session in {x.Repo} keeps repeating {x.Loop.Describe()}.", Palette.Over, null));
        knownStuck.IntersectWith(stuck.Select(x => x.SessionId + x.Loop.Tool + x.Loop.Input));

        // The daemon audits on a schedule; pick up a newer report and say what it found.
        var auditFile = System.IO.Path.Combine(store.Directory, "audit.json");
        var stamp = System.IO.File.Exists(auditFile) ? System.IO.File.GetLastWriteTimeUtc(auditFile) : default;
        if (!IsAuditing && stamp != auditFileStamp && (auditFileStamp = stamp) != default &&
            auditRunner.Latest() is { } latest && latest.RanUtc > (shownAudit?.RanUtc ?? DateTime.MinValue))
        {
            var news = AuditNews.Between(shownAudit, latest);
            ShowAudit(latest);
            if (news.Any) Toasts.Insert(0, new ToastViewModel("Audit found something new", news.ToString(), Palette.Waiting, null));
        }

        ApplyPlan(next);
        Brief(next);

        var autoSettings = store.LoadSettings();
        if (plan is not null && autoDispatch.Tick(AutoDispatch.PlanFor(plan, next, autoSettings, store.LoadAssignments()), autoSettings, DateTime.UtcNow) is { } r)
        {
            QueueMessage = r.Message;
            Record(r, "Auto-dispatch");
            Toasts.Insert(0, new ToastViewModel(r.Ok ? "Auto-dispatched" : "Auto-dispatch failed", r.Message,
                r.Ok ? Palette.Working : Palette.Over, null));
            var ticket = r.Assignment?.TicketId;
            r.Submitted?.ContinueWith(t =>
            {
                if (!(t.IsCompletedSuccessfully && t.Result))
                    queueLog.Warning("{Ticket:l}'s tab never came up to submit its prompt; press Enter in it yourself", ticket);
            }, TaskScheduler.Default);
        }
        Raise(nameof(AutoDispatchText));
    }

    /// <summary>Once per open: if you've been away, say what happened. Then remember you're here.</summary>
    private void Brief(Snapshot next)
    {
        if (!briefed && plan is not null)
        {
            briefed = true;
            var last = store.Load<Seen>("dashboard-seen").LastSeenUtc;
            if (last != default && Briefing.Compose(next, plan, last) is { } text)
                Toasts.Insert(0, new ToastViewModel("While you were away", text, Palette.Tickets, null) { Lifetime = TimeSpan.FromMinutes(15) });
        }
        if (next.TakenUtc - seenSavedUtc < TimeSpan.FromMinutes(1)) return;
        seenSavedUtc = next.TakenUtc;
        try { store.Save("dashboard-seen", new Seen { LastSeenUtc = next.TakenUtc }); }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { }
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
