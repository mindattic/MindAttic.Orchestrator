using System.IO;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Audit;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

public enum View { Agents, Repos, Queue, Audit, Accounts }

/// <summary>Budget (phase 2), the cross-repo queue (phase 3) and the audit (phase 4).</summary>
public sealed partial class MainViewModel
{
    private Dispatcher dispatcher = null!;
    private Autopilot autopilot = null!;
    private AuditRunner auditRunner = null!;
    private SchedulePlan? plan;
    private BudgetLevel? lastBudgetLevel;
    private PaceMode? lastPaceMode;
    private readonly HashSet<string> knownStuck = new(StringComparer.Ordinal);
    private AuditReport? shownAudit;
    private DateTime auditFileStamp;
    private bool briefed;
    private DateTime seenSavedUtc;
    private bool watched = true;

    /// <summary>Whether the window is on screen. Minimized it keeps refreshing, but nobody is looking: you're not
    /// "seen", and showing it again is a new open that may start with a "While you were away" summary.</summary>
    public void SetWatched(bool shown)
    {
        if (shown && !watched) briefed = false;
        watched = shown;
    }

    private sealed class Seen { public DateTime LastSeenUtc { get; init; } }
    /// <summary>The newest rebalance move toasted; moves made before the window opened aren't.</summary>
    private DateTime rebalanceSeenUtc = DateTime.UtcNow;
    private View view;
    private string viewChoice = "Agents";
    private bool isSettingsOpen;
    private bool isAuditing;
    private string auditInfo = "Not run yet.";
    private string queueMessage = "";

    private void InitPhases()
    {
        // One of each for every tab (M.ORCH-T613): a login's tab shares the global login's.
        dispatcher = root?.dispatcher ?? new Dispatcher(control, store);
        // The same lease the status refresh uses (M.ORCH-T112): one FileStream per path per process, so a second
        // one here would just fail to acquire what the first already holds. Held until the app exits; while
        // the logon daemon holds it, the daemon feeds the /allocate CLIs and keeps rl-status.json fresh, and
        // this window only shows.
        autopilot = root?.autopilot ?? new Autopilot(store, new MindAttic.Orchestrator.Sessions.ContextCycler(), lease) { WaitingPath = Autopilot.DefaultWaitingPath };
        auditRunner = root?.auditRunner ?? new AuditRunner(aggregator.Options, store);
        Settings = root?.Settings ?? new SettingsViewModel(store, () =>
        {
            foreach (var t in LoginTabs) t.IsSettingsOpen = false;
            _ = RefreshAsync();
        });

        ToggleSettingsCommand = new RelayCommand(() => { Settings.Load(); IsSettingsOpen = !IsSettingsOpen; });
        RunAuditCommand = new RelayCommand(async () => await Root.RunAuditAsync(), () => !IsAuditing);
        ShowViewCommand = new RelayCommand(p => { if (p is string s) ViewChoice = s; });
        ShowTicketCommand = new RelayCommand(p => { if (p is AgentRowViewModel a) ShowTicketDetails(a); });

        if (root is null && auditRunner.Latest() is { } last) ShowAudit(last);
    }

    // ── View switching ──────────────────────────────────────────────────────

    /// <summary>Agents first: the live picture (each CLI with its subagents inside) is what the window opens on.</summary>
    /// <summary>Accounts (M.ORCH-T603): which Claude login each live CLI runs on.</summary>
    public IReadOnlyList<string> ViewChoices { get; } = ViewNames;

    internal static readonly IReadOnlyList<string> ViewNames = ["Agents", "Repos", "Queue", "Audit", "Accounts"];

    /// <summary>A view of the dashboard: choosing one (a pill, a tile, a toast that opens a repo) shows the Dashboard tab.</summary>
    public string ViewChoice
    {
        get => viewChoice;
        set
        {
            // Ctrl+click on the selected pill deselects it: the window jumped to Agents with no pill lit (M.ORCH-T432).
            if (value is null) return;
            Page = DashboardPage;
            if (!Set(ref viewChoice, value)) return;
            view = Enum.TryParse<View>(value, out var v) ? v : View.Agents;
            RaiseViews();
            if (IsAccountsView) _ = RefreshLoginCardsAsync();
        }
    }

    private void RaiseViews()
    {
        Raise(nameof(IsReposView)); Raise(nameof(IsQueueView)); Raise(nameof(IsAuditView)); Raise(nameof(IsAgentsView)); Raise(nameof(ShowDetail));
        Raise(nameof(IsAccountsView));
        Raise(nameof(IsViewBusy));
        Raise(nameof(GroupsInView)); Raise(nameof(AgentsInView)); Raise(nameof(LoginCardsInView));
    }

    // A person's own lists are bound only while their view shows (M.ORCH-T654): bound while hidden, a tab switch tore down
    // and rebuilt the cards of every view opened since, not just the one on screen. The workspace's lists (the queue, the
    // audit) are one set for every tab, so they stay bound.
    public IReadOnlyList<TileGroupViewModel>? GroupsInView => IsReposView ? Groups : null;
    public ObservableCollection<AgentRowViewModel>? AgentsInView => IsAgentsView ? Agents : null;
    public ObservableCollection<LoginCardViewModel>? LoginCardsInView => IsAccountsView ? LoginCards : null;

    /// <summary>A refresh really running behind the shown view (M.ORCH-T634): the Accounts cards being read, or the audit running.
    /// Every view stays loaded in memory and switching never waits on this; it only drives the small spinner at the view's bottom.</summary>
    public bool IsViewBusy => IsDashboardPage && (view == View.Accounts ? loadingLoginCards : view == View.Audit && IsAuditing);

    public bool ShowDetail => IsReposView && HasSelection;

    // The dashboard's views show only on its Dashboard tab (M.ORCH-T622): on Terminals the panes have the whole body.
    public bool IsReposView => view == View.Repos && IsDashboardPage;
    public bool IsQueueView => view == View.Queue && IsDashboardPage;
    public bool IsAuditView => view == View.Audit && IsDashboardPage;
    public bool IsAgentsView => view == View.Agents && IsDashboardPage;
    public bool IsAccountsView => view == View.Accounts && IsDashboardPage;

    // ── Dashboard and Terminals, each person's two tabs (M.ORCH-T622) ─────────

    public const string DashboardPage = "Dashboard", TerminalsPage = "Terminals";
    private string page = DashboardPage;

    /// <summary>The two tabs under the person's tab: their dashboard, and their terminals.</summary>
    public IReadOnlyList<string> Pages => PageNames;
    private static readonly IReadOnlyList<string> PageNames = [DashboardPage, TerminalsPage];

    /// <summary>The person's tab showing: <see cref="DashboardPage"/> (tiles, chart, view pills and the chosen view) or
    /// <see cref="TerminalsPage"/> (only their terminals, filling the window below the person tabs).</summary>
    public string Page
    {
        get => page;
        set
        {
            // Ctrl+click on the selected tab deselects it, writing null: it stays on the one it was.
            if (value is not (DashboardPage or TerminalsPage) || !Set(ref page, value)) return;
            Raise(nameof(IsDashboardPage)); Raise(nameof(ShowsDashboard)); Raise(nameof(IsTerminalsView)); Raise(nameof(TerminalsVisibility));
            RaiseViews();
            if (IsAccountsView) _ = RefreshLoginCardsAsync();
        }
    }

    public bool IsDashboardPage => page == DashboardPage;

    /// <summary>The Terminals tab is showing: the header tiles, the chart and the view pills fold away for the panes.</summary>
    public bool IsTerminalsView => page == TerminalsPage;

    /// <summary>The header tiles, the chart and the view pills: on a signed-in login's Dashboard tab only.</summary>
    public bool ShowsDashboard => IsDashboardPage && IsTabSignedIn;

    /// <summary>The Terminals tab's (M.ORCH-T618): its terminals are native windows WPF can't draw over, so they're hidden while
    /// Settings or the loader covers the window, or on a tab that only signs its login in (M.ORCH-T615), not only on the
    /// Dashboard tab; hidden, never collapsed, so each keeps its size.</summary>
    public System.Windows.Visibility TerminalsVisibility =>
        IsTerminalsView && !IsSettingsOpen && !IsLoading && IsTabSignedIn ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden;
    public ICommand ShowViewCommand { get; private set; } = null!;

    // ── Settings ────────────────────────────────────────────────────────────

    public SettingsViewModel Settings { get; private set; } = null!;
    public bool IsSettingsOpen { get => isSettingsOpen; set { if (Set(ref isSettingsOpen, value)) Raise(nameof(TerminalsVisibility)); } }
    public ICommand ToggleSettingsCommand { get; private set; } = null!;

    // ── Budget ──────────────────────────────────────────────────────────────

    public BudgetStatus? Budget => snapshot?.Budget;
    public bool BudgetIsPeak => Budget?.BlockLimitIsPeak ?? true;

    /// <summary>The tab's login's 5-hour window, % used (M.ORCH-T628): from its own pace, else (a login the daemon hasn't paced
    /// yet) the roster's last reading of it.</summary>
    public string BudgetHeadline => Budget is null && OwnRow?.FiveHourUsedAt(DateTime.UtcNow) is { } read ? Formatting.FiveHourPercent(read)
        : Budget?.CurrentBlock is null ? "Idle" : Formatting.FiveHourPercent(Budget.BlockPercent);

    public string BudgetCaption => Budget switch
    {
        null => OwnRow?.FiveHourUsedAt(DateTime.UtcNow) is null ? "" : "of your 5-hour limit (Claude's count)",
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
                return Budget is null ? OwnRow?.FiveHourResetAt(DateTime.UtcNow) is { } fr ? $"resets {Formatting.ResetTime(fr, DateTime.UtcNow)}" : "" : Budget.WeeklyPercent is { } wp ? $"week {Left(wp)}% left" : $"{Formatting.Tokens(Budget.Today.Fresh)} uncached today";
            var parts = new List<string> { $"resets {Formatting.ResetTime(block.EndUtc, DateTime.UtcNow)}", $"{Formatting.Tokens((long)b.FreshPerHour)}/h" };
            if (b.ProjectedLimitUtc is { } at) parts.Add($"limit ~{Formatting.ResetTime(at, DateTime.UtcNow)}");
            if (b.WeeklyPercent is { } weekPercent) parts.Add($"week {Left(weekPercent)}% left");
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
            if (snapshot is not null && Burndown.Compute(b, snapshot.History, snapshot.TakenUtc, snapshot.Account) is { } week)
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
    // Weekly-aware pacing says first when the week caps this window (M.ORCH-T666): "Capped by the week: 31% of this window".
    public string PaceText => Pace is { Weekly: { Capped: true } week } capped && capped.IsPaced
        // With its plan (M.ORCH-T669): "Capped by the week: 31% of this window (planned: this window to 31%, the week to 98% at its reset)".
        ? week.Short + (week.Plan is { Length: > 0 } plan ? $" ({plan})" : "") + (PaceCore is { Length: > 0 } core ? " · " + core : "")
        : PaceCore;

    private string PaceCore => Pace switch
    {
        null or { IsPaced: false } or { Mode: PaceMode.Open } => "",
        // The reset with its weekday when it's another day: the week can govern (M.ORCH-T408).
        { Mode: PaceMode.Throttled } p => $"Paced to {p.SessionCap} of {store.LoadSettings().MaxConcurrentSessions} · resets {(p.ResetUtc is { } r ? Formatting.ResetTime(r, DateTime.UtcNow) : "")}",
        { Mode: PaceMode.Coast } p => $"Coasting · resets {(p.ResetUtc is { } r ? Formatting.ResetTime(r, DateTime.UtcNow) : "")}",
        // Which limit, and its weekday when it's days off (a weekly wall), as the pace note agents see says it.
        { } p => $"At the {p.Governing ?? "5-hour"} limit until {(p.ResetUtc is { } r ? Formatting.ResetTime(r, DateTime.UtcNow) : "the reset")}",
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

    /// <summary>This 5-hour window against its pace, for the utilization graph; with nothing running on the tab's login, its
    /// window as a flat line at its current % (M.ORCH-T629), so every person's tab always shows its chart.</summary>
    public Utilization? Utilization => snapshot is null ? null
        : IsLoginIdle(snapshot) ? IdleUtilization(snapshot, OwnRow, paceTargetPercent, DateTime.UtcNow) : snapshot.Utilization;

    /// <summary>The graph in words (it's also the card's accessible name): where the window is against the pace line,
    /// how long it ran over it, and where the recent rate ends up.</summary>
    public string UtilizationText => UtilizationCaption(Utilization, snapshot is not null, LoginName) + NoWorkText;

    /// <summary>The tab's person, for the idle caption: its title without "(fallback)".</summary>
    private string LoginName => tab.Title.EndsWith(" (fallback)", StringComparison.Ordinal) ? tab.Title[..^" (fallback)".Length]
        : tab.IsGlobal ? "the global login" : tab.Title;

    /// <summary>Nothing running on the login (M.ORCH-T629): none of its repos working and none of its agents working or with a
    /// subagent running.</summary>
    internal static bool IsLoginIdle(Snapshot s) =>
        s.Working == 0 && !s.Agents.Any(a => a.State == MindAttic.Orchestrator.Activity.AgentState.Working || a.Subagents.Count > 0);

    /// <summary>The login's window while it's idle (M.ORCH-T629): the running window's % and reset (its own pace's, else the
    /// roster's last reading of it), or the next 5 hours from now at 0% once it has reset or was never read.</summary>
    internal static Utilization IdleUtilization(Snapshot s, AccountRow? row, double targetPercent, DateTime nowUtc)
    {
        var target = s.Utilization?.TargetPercent ?? targetPercent;
        if (s.Utilization is { Ended: false, Idle: false } u && u.EndUtc > nowUtc)
            return UtilizationSeries.IdleWindow(u.EndUtc, u.NowPercent, target, nowUtc);
        if (s.Budget is { CurrentBlock: { } block } b && block.EndUtc > nowUtc)
            return UtilizationSeries.IdleWindow(block.EndUtc,
                b.BlockLimitIsPeak || b.BlockLimit <= 0 ? row?.FiveHourUsedAt(nowUtc) ?? 0 : b.BlockPercent, target, nowUtc);
        return UtilizationSeries.IdleWindow(row?.FiveHourResetAt(nowUtc), row?.FiveHourUsedAt(nowUtc) ?? 0, target, nowUtc);
    }

    /// <summary>On course to end under target with nothing left for any /allocate CLI to start: said under the graph, with the
    /// proposals waiting for your approval (M.ORCH-T529), as the daemon's one alert a window says it.</summary>
    private string NoWorkText => snapshot is null || Plan is not { } plan ? ""
        : UnderTarget.NoWorkLine(snapshot, plan,new MindAttic.Orchestrator.Sessions.ContextCycler().AutopilotClis()) is { } line ? " " + line : "";

    /// <summary>The caption for <see cref="UtilizationText"/>. Empty only before the first snapshot, and the card is collapsed
    /// only on an empty caption, so it stays up between windows with the last one as it ended (M.ORCH-T502).</summary>
    internal static string UtilizationCaption(Utilization? utilization, bool loaded, string login = "this login") => utilization switch
    {
        { Idle: true, Unstarted: true } =>
            $"Idle: 0% of this 5-hour window used; nothing running on {login}. No window has started, so this is the next 5 hours from now.",
        { Idle: true } u => $"Idle: {Formatting.FiveHourPercent(u.NowPercent)} of this 5-hour window used; nothing running on {login}. " +
                            $"It resets at {Formatting.ResetTime(u.EndUtc, u.NowUtc)}; the target is {u.TargetPercent:0}%.",
        null => loaded ? "No 5-hour limit known yet, so there's no pace to draw this window against." : "",
        { HasLastWindow: true } u => $"No 5-hour window running: the last ended at {Formatting.ResetTime(u.EndUtc, DateTime.UtcNow)} at {Formatting.FiveHourPercent(u.NowPercent)}; the next starts with your next prompt.",
        { Ended: true } => "No 5-hour window running: the next starts with your next prompt.",
        { } u => Running(u),
    };

    private static string Running(Utilization u) =>
        $"{Formatting.FiveHourPercent(u.NowPercent)} used, {(u.OverNow ? "over" : "under")} the {(u.Curved ? "front-loaded pace curve" : "pace line")} ({u.PaceAt(u.NowUtc):0}% by now)" +
        (u.OverPace >= TimeSpan.FromMinutes(1) ? $"; over it for {(int)u.OverPace.TotalMinutes} min so far" : "") + ". " +
        (u.HitsLimitUtc is { } hit ? $"At the recent rate it would reach 100% at {Formatting.ResetTime(hit, u.NowUtc)}, before the {Formatting.ResetTime(u.EndUtc, u.NowUtc)} reset."
            : $"At the recent rate it ends near {u.ProjectedEndPercent:0}% at {Formatting.ResetTime(u.EndUtc, u.NowUtc)}; the target is {u.TargetPercent:0}%.");

    /// <summary>5-hour reset, for the agent board's header.</summary>
    public string BlockResetText => Budget?.CurrentBlock is { } b ? $"resets {Formatting.ResetTime(b.EndUtc, DateTime.UtcNow)}" : "";

    private static string Left(double usedPercent) => Math.Max(0, 100 - usedPercent).ToString("0");

    /// <summary>The tab's login's week used (M.ORCH-T628): its own pace's, else the roster's last reading of it.</summary>
    private double? WeekUsed => Budget?.WeeklyPercent ?? OwnRow?.WeekUsedAt(DateTime.UtcNow);

    /// <summary>When the tab's login's week resets: its own pace's, else the roster's; null on a rolling 7 days.</summary>
    private DateTime? WeekReset => Budget?.WeekResetUtc ?? OwnRow?.WeekResetAt(DateTime.UtcNow);

    /// <summary>How much of the tab's login's week is left (M.ORCH-T628): "23% left".</summary>
    public string WeeklyHeadline => WeeklyHeadlineOf(WeekUsed);

    internal static string WeeklyHeadlineOf(double? usedPercent) => usedPercent is { } u ? $"{Left(u)}% left" : "—";

    /// <summary>"resets Mon 10/27 5:00 PM · stops at 95%": the week's reset, then its weekly stop line (M.ORCH-T609).</summary>
    public string WeeklyCaption => WeeklyCaptionOf(WeekUsed, WeekReset, weeklyStopPercent, DateTime.UtcNow);

    internal static string WeeklyCaptionOf(double? usedPercent, DateTime? resetUtc, int stopPercent, DateTime nowUtc)
    {
        if (usedPercent is not { } used) return "no weekly limit known yet";
        var reset = resetUtc is { } r ? $"resets {Formatting.ResetOnDay(r)}" : "rolling 7 days";
        return $"{reset} · {(used >= stopPercent ? "stopped" : "stops")} at {stopPercent}%";
    }

    /// <summary>What the account menu also showed for the login (M.ORCH-T628): its plan and when its usage was last checked,
    /// "Team · Max 5x · checked 3m ago"; empty when the roster doesn't know it.</summary>
    public string WeeklyAccountLine => AccountLineOf(OwnRow, DateTime.UtcNow);

    internal static string AccountLineOf(AccountRow? row, DateTime nowUtc)
    {
        if (row is null) return "";
        var parts = new List<string>();
        if (row.Plan is { Length: > 0 } plan) parts.Add(plan);
        parts.Add(row.ReadUtc is { } r ? $"checked {Formatting.Ago(r, nowUtc)}" : "not checked yet");
        return string.Join(" · ", parts);
    }

    /// <summary>Raises every tile figure that reads the tab's login's roster row.</summary>
    private void RaiseOwnNumbers()
    {
        foreach (var name in new[] { nameof(BudgetHeadline), nameof(BudgetCaption), nameof(BudgetDetail),
                                     nameof(WeeklyHeadline), nameof(WeeklyCaption), nameof(WeeklyAccountLine),
                                     nameof(Utilization), nameof(UtilizationText) })
            Raise(name);
    }

    // ── Agent board ─────────────────────────────────────────────────────────

    public ObservableCollection<AgentRowViewModel> Agents { get; } = [];
    public bool HasAgents => Agents.Count > 0;

    /// <summary>Subagents running across every agent on the board.</summary>
    public int SubagentCount => Agents.Sum(a => a.Subagents.Count);

    public string AgentsSummary => Agents.Count == 0 ? "" :
        $"{Agents.Count} agent{(Agents.Count == 1 ? "" : "s")} · {Agents.Count(a => a.State == MindAttic.Orchestrator.Activity.AgentState.Waiting)} need you · " +
        $"{SubagentCount} subagent{(SubagentCount == 1 ? "" : "s")} running";
    public ICommand ShowTicketCommand { get; private set; } = null!;

    /// <summary>The ticket's row (when it's open) and history: its sessions and the commits that mention it.</summary>
    public async void ShowTicketDetails(AgentRowViewModel a)
    {
        try
        {
            if (a.TicketId is not { Length: > 0 } ticketId) return;
            var repo = snapshot?.Repos.FirstOrDefault(r => r.Name == a.Repo);
            var repoPath = repo?.Path ?? "";
            var ticket = repo?.OpenTickets.FirstOrDefault(t => t.Id == ticketId);
            ShowText($"{ticketId} in {a.Repo}", await Task.Run(() => MindAttic.Orchestrator.Tickets.TicketReplay.PlainReport(ticketId, repoPath, aggregator.Options.ClaudeProjectsDir, ticket,
                store.LoadSettings().WorktreeRoot)));
        }
        // async void: an escaped exception (a corrupt assignments.json, a locked file) would close the dashboard.
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            QueueMessage = $"Failed: {ex.Message}";
            OrchestratorLog.For("Dashboard").Error(ex, "ShowTicketDetails failed");
        }
    }

    private void ApplyAgents(Snapshot next)
    {
        // Ones waiting on you first: that's what a glance at the board is for. Then the most recently active.
        Agents.SyncBy([.. next.Agents.OrderBy(r => r.State != MindAttic.Orchestrator.Activity.AgentState.Waiting)], r => r.SessionId, a => a.SessionId,
            r => { var a = new AgentRowViewModel(r.SessionId); a.Update(r, TitleOf(r), next.TakenUtc); return a; },
            (a, r) => a.Update(r, TitleOf(r), next.TakenUtc));
        Raise(nameof(HasAgents)); Raise(nameof(SubagentCount)); Raise(nameof(AgentsSummary));
        ReconcileMoves(next);

        string? TitleOf(AgentBoardRow r) => r.TicketId is not { Length: > 0 } id ? null
            : next.Repos.FirstOrDefault(repo => repo.Name == r.Repo)?.OpenTickets.FirstOrDefault(t => t.Id == id)?.Summary;
    }

    // ── Queue ───────────────────────────────────────────────────────────────

    // The queue is the whole workspace's (M.ORCH-T613), so one plan and one set of rows, on the global login's tab, are what
    // every tab shows (M.ORCH-T654): a tab switch hands the window the same collections, and WPF keeps their rows.
    private readonly ObservableCollection<QueueRowViewModel> queue = [], proposals = [];
    private readonly ObservableCollection<FinishedViewModel> finished = [];
    public ObservableCollection<QueueRowViewModel> Queue => Root.queue;
    public ObservableCollection<QueueRowViewModel> Proposals => Root.proposals;
    public ObservableCollection<FinishedViewModel> Finished => Root.finished;
    private SchedulePlan? Plan => Root.plan;
    public string Hold => Plan?.Hold ?? "";
    public bool HasHold => !string.IsNullOrEmpty(Hold);
    public string SlotsText => Plan is not { } plan ? ""
        : plan.Cap < plan.Slots ? $"{plan.Occupied} sessions open · pacing allows {plan.Cap} of {plan.Slots}"
        : $"{plan.Occupied} sessions open · fills up to {plan.Slots}";
    // /allocate is on per CLI (its marks, M.ORCH-T164), as `orchestrator settings` reports it: the dashboard's own Autopilot
    // has no order to read, so it always read as off.
    public string AutopilotText => AutopilotLine(new MindAttic.Orchestrator.Sessions.ContextCycler().AutopilotClis(), store.LoadSettings().Autopilot) +
        (autopilot.Working.Count == 0 ? "" : $" Working in the background: {string.Join(", ", autopilot.Working.Select(a => $"{a.TicketId} in {a.Repo}"))}.");

    internal static string AutopilotLine(IReadOnlyDictionary<int, IReadOnlyList<string>> clis, bool on = true) => clis.Count == 0
        ? "Proposals wait for you. Type /allocate in a CLI to have its repos' tickets worked by its own subagents."
        // Paused from Settings or the CLI: the CLIs' marks stay, but orchestrator next hands them nothing.
        : !on ? $"Autopilot is paused: the {clis.Count} CLI{(clis.Count == 1 ? "" : "s")} that typed /allocate get no tickets until you resume it from Settings."
        : $"Autopilot is on in {clis.Count} CLI{(clis.Count == 1 ? "" : "s")} for " +
          string.Join(", ", clis.Values.SelectMany(r => r).Select(r => r == Autopilot.AllRepos ? "every repo" : r).Distinct(StringComparer.OrdinalIgnoreCase)) +
          ", each through its own subagents, paced to the 5-hour window.";
    public string QueueMessage { get => queueMessage; private set => Set(ref queueMessage, value); }
    public int QueueCount => Queue.Count(q => !q.IsAssigned);

    /// <summary>What an empty queue says: tickets waiting for approval are left out of it, so they're named, not "no open
    /// tickets" (M.ORCH-T350).</summary>
    public string EmptyQueueText => EmptyQueue(Plan?.AwaitingApproval.Count ?? 0);

    internal static string EmptyQueue(int awaitingApproval) => awaitingApproval == 0
        ? "No open tickets anywhere. Add one with: tickets ticket new"
        : $"Nothing workable: {awaitingApproval} open ticket{(awaitingApproval == 1 ? "" : "s")} wait{(awaitingApproval == 1 ? "s" : "")} for your approval (orchestrator approve <ID>); fixes need none.";

    /// <summary>Tickets being started now: the row shows them unassigned until the refresh after the start, and a second
    /// click in that time would open a second agent on the same ticket.</summary>
    private readonly HashSet<string> starting = new(StringComparer.OrdinalIgnoreCase);

    public bool IsStarting(string ticketId) => Root.starting.Contains(ticketId);

    public async void Dispatch(QueueItem item, bool worktree)
    {
        if (Locked(item.Id)) return;
        var starting = Root.starting;
        if (!starting.Add(item.Id)) return;
        try
        {
            QueueMessage = $"Starting {item.Id}…";
            var providerKey = store.LoadSettings().DefaultProvider;
            // On this tab's login (M.ORCH-T613): the global login's tab opens it on the global login, a login's own tab on
            // its own (signed in first if it's been signed out since).
            Dispatcher.TabLogin login = new(null, full?.GlobalEmail);
            if (providerKey == "Claude" && item.Assignee is { } who)
            {
                // A ticket assigned to a user opens only on their login (M.ORCH-T668), whichever tab Start was pressed on; one
                // whose user isn't signed in here waits for them, not for a sign-in of someone else's account from here.
                if (TicketPins.Same(who, full?.GlobalEmail)) login = new(null, who);
                else if (!await Task.Run(() => accountDirs.For(who).SignedIn))
                {
                    QueueMessage = TicketPins.WaitingText(NameOf(who), "not signed in on this machine") + ". Reassign it, or Redelegate now, to start it elsewhere.";
                    return;
                }
                else if (await OwnLoginDirAsync(who, m => QueueMessage = m) is not { } pinned) return;
                else login = new(pinned, who);
            }
            else if (providerKey == "Claude" && ScopeEmail is { } own)
            {
                if (await OwnLoginDirAsync(own, m => QueueMessage = m) is not { } dir) return;
                login = new(dir, own);
            }
            var r = await Task.Run(() => dispatcher.Dispatch(item, providerKey, worktree, login: login));
            QueueMessage = r.Message;
            Record(r, "Dispatch");
            await RefreshAsync();
            // A refresh already under way returns at once above: the queue is replanned from the assignments now, so the row
            // reads as assigned before the guard below lets go.
            if (full is not null) Root.ApplyPlan(full);
        }
        // async void: an escaped exception (a corrupt assignments.json, a locked file) would close the dashboard.
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            QueueMessage = $"Failed: {ex.Message}";
            OrchestratorLog.For("Dashboard").Error(ex, "Dispatch failed");
        }
        finally
        {
            starting.Remove(item.Id);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private static readonly Serilog.ILogger queueLog = OrchestratorLog.For("Queue");

    private static void Record(Dispatcher.Result r, string action)
    {
        if (r.Ok) queueLog.Information("{Action:l}: {Message:l}", action, r.Message);
        else queueLog.Error("{Action:l} failed: {Message:l}", action, r.Message);
    }

    public void Bump(string ticketId, int by) => Guarded("Bump", () =>
    {
        // Priority lives in the ticket database's queue, where every tool reads and writes it (M.ORCH-T384); a ticket it doesn't
        // hold (a repo still on its own USER_STORIES.md) keeps it in settings.json, which the plan reads too (M.ORCH-T441).
        using var db = MindAttic.Tickets.Data.TicketsDb.OpenIfMigrated(aggregator.Options.WorkspaceRoot);
        var inDb = db?.Ticket(ticketId) is not null;
        var next = MindAttic.Orchestrator.Tickets.TicketQueue.Bump(store, db, ticketId, by);
        NoteLocalChange();
        // Shown at once, not at the next refresh: the plan is made again over the snapshot with the new priority.
        if (!inDb) { if (full is not null) Root.ApplyPlan(full); }
        else if (full is not null)
        {
            var p = new Dictionary<string, long>(full.Priorities, StringComparer.OrdinalIgnoreCase);
            if (next == 0) p.Remove(ticketId); else p[ticketId] = next;
            full = full with { Priorities = p };
            Root.ApplyPlan(full);
        }
    });

    public void Release(string ticketId) => Guarded("Release", () =>
    {
        if (Locked(ticketId)) return;
        dispatcher.Release(ticketId);
        QueueMessage = $"{ticketId} is back in the queue.";
        queueLog.Information("Released {Ticket:l}", ticketId);
        if (full is not null) Root.ApplyPlan(full);
    });

    /// <summary>Runs a click's change to the shared files: one that can't be saved (locked past the store's retries, or
    /// unreadable) is said in the queue line, not left to close the dashboard (and the status lease it may hold).</summary>
    private void Guarded(string action, Action change)
    {
        try { change(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException
                                       or System.Data.Common.DbException or KeyNotFoundException)
        {
            QueueMessage = $"{action} failed: {ex.Message}";
            OrchestratorLog.For("Dashboard").Error(ex, "{Action:l} failed", action);
        }
    }

    // ── Selecting a repo ────────────────────────────────────────────────────

    public void SelectRepo(string name)
    {
        ViewChoice = "Repos";
        var (tile, clearSearch) = FindRepo(name, Waiting.Tiles.Concat(Working.Tiles).Concat(Tickets.Tiles), tiles);
        if (clearSearch) Search = ""; // a toast asked for a repo the search box was hiding
        if (tile is not null) Selected = tile;
    }

    /// <summary>The tile to select for <paramref name="name"/>: one already shown as is, else a known one the search
    /// (or Quiet being hidden) keeps out of view, which needs the search cleared; null for a repo nobody knows.</summary>
    internal static (RepoTileViewModel? Tile, bool ClearSearch) FindRepo(
        string name, IEnumerable<RepoTileViewModel> shown, IReadOnlyDictionary<string, RepoTileViewModel> known) =>
        shown.FirstOrDefault(t => t.Name == name) is { } tile ? (tile, false)
        : known.GetValueOrDefault(name) is { } hidden ? (hidden, true)
        : (null, false);

    /// <summary>The ticket's sessions and commits in a window: what happened, without digging through Launcher history.</summary>
    public async void ShowDetails(Assignment a)
    {
        try
        {
            var repoPath = snapshot?.Repos.FirstOrDefault(r => r.Name == a.Repo)?.Path ?? a.Directory;
            ShowText($"{a.TicketId} in {a.Repo}", await Task.Run(() => MindAttic.Orchestrator.Tickets.TicketReplay.PlainReport(a.TicketId, repoPath, aggregator.Options.ClaudeProjectsDir,
                worktreeRoot: store.LoadSettings().WorktreeRoot)));
        }
        // async void: an escaped exception (a corrupt assignments.json, a locked file) would close the dashboard.
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            QueueMessage = $"Failed: {ex.Message}";
            OrchestratorLog.For("Dashboard").Error(ex, "ShowDetails failed");
        }
    }

    /// <summary>A read-only window that shows <paramref name="text"/> in a monospaced, scrollable box.</summary>
    private static void ShowText(string title, string text) =>
        new System.Windows.Window
        {
            Title = title, Width = 900, Height = 650, Owner = System.Windows.Application.Current.MainWindow,
            Content = new System.Windows.Controls.TextBox
            {
                Text = text, IsReadOnly = true, FontFamily = new FontFamily("Consolas"), FontSize = 12,
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
            },
        }.Show();

    public async void Finish(Assignment a)
    {
        if (Locked(a.TicketId)) return;
        try
        {
            var repoPath = snapshot?.Repos.FirstOrDefault(r => r.Name == a.Repo)?.Path ?? "";
            var r = await Task.Run(() => dispatcher.RemoveWorktree(a, repoPath));
            QueueMessage = r.Message;
            Record(r, "Remove worktree");
            if (full is not null) Root.ApplyPlan(full);
        }
        // async void: an escaped exception (a corrupt assignments.json, a locked file) would close the dashboard.
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            QueueMessage = $"Failed: {ex.Message}";
            OrchestratorLog.For("Dashboard").Error(ex, "Finish failed");
        }
    }

    // ── Audit ───────────────────────────────────────────────────────────────

    // The audit is the workspace's too: one report and one set of rows, on the global login's tab, in every tab (M.ORCH-T654).
    private readonly ObservableCollection<DuplicateGroupViewModel> duplicates = [];
    private readonly ObservableCollection<DriftViewModel> drift = [];
    private readonly ObservableCollection<MissingViewModel> missing = [];
    public ObservableCollection<DuplicateGroupViewModel> Duplicates => Root.duplicates;
    public ObservableCollection<DriftViewModel> Drift => Root.drift;
    public ObservableCollection<MissingViewModel> Missing => Root.missing;

    public bool IsAuditing
    {
        get => Root.isAuditing;
        private set
        {
            if (Root.isAuditing == value) return;
            Root.isAuditing = value;
            foreach (var t in LoginTabs) { t.Raise(nameof(IsAuditing)); t.Raise(nameof(IsViewBusy)); }
        }
    }

    public string AuditInfo
    {
        get => Root.auditInfo;
        private set
        {
            Root.auditInfo = value;
            foreach (var t in LoginTabs) t.Raise(nameof(AuditInfo));
        }
    }
    public string DuplicatesHeader => $"DUPLICATED ACROSS REPOS · {Duplicates.Count}";
    public string DriftHeader => $"SHARED AGENT FILES THAT DIFFER · {Drift.Count}";
    public string MissingHeader => $"SHARED AGENT FILES SOME REPOS LACK · {Missing.Count}";
    public ICommand RunAuditCommand { get; private set; } = null!;

    private async Task RunAuditAsync()
    {
        IsAuditing = true;
        AuditInfo = "Scanning every repo…";
        try
        {
            // A report that can't be saved (audit.json held, disk full) is still shown, with why, as the CLI does (M.ORCH-T467).
            var report = await Task.Run(() => auditRunner.Scan());
            ShowAudit(report);
            if (!auditRunner.TrySave(report, out var saveError)) AuditInfo += $" · not saved: {saveError}";
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { AuditInfo = $"Audit failed: {ex.Message}"; }
        finally
        {
            IsAuditing = false;
            CommandManager.InvalidateRequerySuggested(); // the button's enabled state follows IsAuditing only when asked
        }
    }

    private void ShowAudit(AuditReport report)
    {
        shownAudit = report;
        var root = aggregator.Options.WorkspaceRoot;
        Duplicates.ReorderTo([.. report.Duplicates.Select(g => new DuplicateGroupViewModel(g, root))]);
        Drift.ReorderTo([.. report.Drift.Where(d => !d.IsCustom).Concat(report.Drift.Where(d => d.IsCustom)).Select(d => new DriftViewModel(d))]);
        Missing.ReorderTo([.. report.Missing.GroupBy(m => m.File).OrderByDescending(g => g.First().HaveIt).Select(g => new MissingViewModel(g.Key, [.. g]))]);
        AuditInfo = $"{report.ReposScanned} repos, {report.FilesScanned} source files, {report.Took.TotalSeconds:0.0}s · {report.RanUtc.ToLocalTime():g}";
        foreach (var t in LoginTabs) { t.Raise(nameof(DuplicatesHeader)); t.Raise(nameof(DriftHeader)); t.Raise(nameof(MissingHeader)); }
    }

    // ── Per-refresh ─────────────────────────────────────────────────────────

    private void ApplyPhases(Snapshot next)
    {
        foreach (var name in new[] { nameof(Budget), nameof(BudgetIsPeak), nameof(BudgetHeadline), nameof(BudgetCaption),
                                     nameof(BudgetDetail), nameof(BudgetTooltip), nameof(BudgetFill), nameof(BudgetBrush),
                                     nameof(BlockResetText), nameof(WeeklyHeadline), nameof(WeeklyCaption), nameof(WeeklyAccountLine),
                                     nameof(Pace), nameof(PaceText), nameof(PaceBrush), nameof(Utilization), nameof(UtilizationText) })
            Raise(name);

        ApplyAgents(next);

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

        // The daemon audits on a schedule; pick up a newer report and say what it found. Once, on the global login's tab: the
        // audit is the workspace's, not a login's.
        if (IsGlobalTab && !IsAuditing && NewerAudit(auditRunner, ref auditFileStamp, shownAudit) is { } latest)
        {
            var news = AuditNews.Between(shownAudit, latest);
            ShowAudit(latest);
            if (news.Any) Toasts.Insert(0, new ToastViewModel("Audit found something new", news.ToString(), Palette.Waiting, null));
        }

        // The daemon moved a CLI off a login that ran low (M.ORCH-T608): each move since the last one shown, from the window's
        // open; said once, on the global login's tab, as moves go between logins.
        if (IsGlobalTab)
            foreach (var move in Sessions.AccountRebalancer.Since(store, rebalanceSeenUtc))
            {
                Toasts.Insert(0, new ToastViewModel("Moved a CLI", move.Text, Palette.Working, null));
                rebalanceSeenUtc = move.AtUtc;
            }

        // The queue and the "while you were away" summary are the whole workspace's, the same in every tab (M.ORCH-T613): planned
        // once, by the global login's tab (applied first), whose rows every tab shows (M.ORCH-T654).
        if (IsGlobalTab && full is { } all)
        {
            ApplyPlan(all);
            Brief(all);
        }
    }

    /// <summary>
    /// The saved report when it changed since <paramref name="seenStamp"/> and ran after <paramref name="shown"/>; null
    /// otherwise. Its save time is taken only once it's read: a read that failed (the daemon replacing it) left the new audit
    /// unshown, and its toast unraised, until the next audit a day later (M.ORCH-T460).
    /// </summary>
    internal static AuditReport? NewerAudit(AuditRunner runner, ref DateTime seenStamp, AuditReport? shown)
    {
        var stamp = runner.SavedUtc ?? default;
        if (stamp == seenStamp) return null;
        if (stamp == default) { seenStamp = default; return null; }
        if (!runner.TryLatest(out var latest)) return null; // tried again next refresh
        seenStamp = stamp;
        return latest is not null && latest.RanUtc > (shown?.RanUtc ?? DateTime.MinValue) ? latest : null;
    }

    /// <summary>
    /// Autopilot's tick, off the UI thread: holding the lease (no daemon running), this window reaps and runs the hourly
    /// sweep, a git fetch and more per finished worktree that can take minutes, which froze the dashboard at its first
    /// refresh and every hour after (M.ORCH-T382). The refresh stays in progress until it's done, so ticks never overlap.
    /// </summary>
    private async Task TickAutopilotAsync(Snapshot next)
    {
        if (plan is { } current)
        {
            var settings = store.LoadSettings();
            foreach (var line in await Task.Run(() => autopilot.Tick(current, next, settings, DateTime.UtcNow)))
            {
                QueueMessage = line;
                queueLog.Information("{Line:l}", line);
                if (Autopilot.NeedsYou(line) is { } needsYou)
                    Toasts.Insert(0, new ToastViewModel("Needs you", needsYou, Palette.Waiting, null));
            }
            PruneToasts(Toasts, DateTime.UtcNow);
        }
        foreach (var t in LoginTabs) t.Raise(nameof(AutopilotText));
    }

    /// <summary>Once per open: if you've been away, say what happened. Then remember you're here.</summary>
    private void Brief(Snapshot next)
    {
        if (!watched) return;
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
        if (!IsGlobalTab) { Root.ApplyPlan(next); return; }
        plan = Scheduler.Plan(next, store.LoadSettings(), store.LoadAssignments());
        var proposals = plan.Proposals.ToDictionary(p => p.Item.Id, StringComparer.Ordinal);
        var rank = 0;
        // Which tickets weekly-aware pacing sent to another login, and why (M.ORCH-T666).
        var delegations = Redelegation.Load(store);
        Queue.SyncBy(plan.Queue, q => q.Id, r => r.Id,
            q => { var row = new QueueRowViewModel(q, this); row.Update(q, ++rank, proposals.GetValueOrDefault(q.Id), delegations.GetValueOrDefault(q.Id)); return row; },
            (row, q) => row.Update(q, ++rank, proposals.GetValueOrDefault(q.Id), delegations.GetValueOrDefault(q.Id)));
        Proposals.ReorderTo([.. Queue.Where(q => q.IsNext)]);
        Finished.SyncBy(plan.Finished, a => a.TicketId, f => f.TicketId, a => new FinishedViewModel(a, this), (row, a) => row.Update(a));
        // Every person's Tickets view (M.ORCH-T668): theirs and the unassigned queue, from the same plan.
        ApplyUserTickets();

        foreach (var t in LoginTabs)
            foreach (var name in new[] { nameof(Hold), nameof(HasHold), nameof(SlotsText), nameof(AutopilotText), nameof(QueueCount), nameof(EmptyQueueText), nameof(UtilizationText) })
                t.Raise(name);
        // A row's Start and Release are enabled by whether it's assigned, which the refresh just changed: WPF rechecks a
        // command only on input, so the button shown would stay disabled until some unrelated click (M.ORCH-T351).
        CommandManager.InvalidateRequerySuggested();
    }
}
