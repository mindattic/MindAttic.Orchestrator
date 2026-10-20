using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Tickets;
using MindAttic.Workflow;

namespace MindAttic.Orchestrator;

/// <summary>
/// Builds a <see cref="Snapshot"/> of the whole workspace from sources that already exist: the
/// repo folders (MindAttic.Workflow's <see cref="Workspace"/>), each repo's ticket log, the
/// Launcher's host pipes, and Claude Code's transcripts. Holds incremental state between calls,
/// so keep one instance and call <see cref="Refresh"/> on a timer. Not thread-safe.
/// </summary>
/// <remarks>With a <paramref name="statusLease"/>, only the process holding it builds snapshots; it publishes each one
/// (<see cref="SnapshotFile"/>) and every other dashboard window or daemon shows that instead of building its own (M.ORCH-T115).</remarks>
/// <param name="statusLease">With <paramref name="rateLimitFile"/>, write it only while held (or free to
/// take): every open dashboard window and the headless daemon would otherwise poll Claude's own report and
/// the session transcripts at their own moment and overwrite the one shared file with a different number
/// every few seconds, flapping the pace gate's hold and the "RATE LIMIT" banner between whichever process
/// wrote last (found live: two dashboard windows open at once, M.ORCH-T112). Null always writes, as a one-off
/// CLI run does.</param>
public sealed class StatusAggregator(OrchestratorOptions options, JsonStore? store = null, RateLimitStatusWriter? rateLimitFile = null, DispatchLease? statusLease = null)
{
    /// <summary>Activity in the workspace root itself (cross-repo sessions).</summary>
    public const string WorkspaceRootName = "(workspace root)";

    /// <summary>Claude sessions running outside the workspace still spend the same budget.</summary>
    public const string OutsideName = "(outside workspace)";

    /// <summary>How far back usage is kept: long enough for a rolling week and a peak block.</summary>
    public const int HistoryDays = 7;

    private readonly Workspace workspace = new(options.WorkspaceRoot);
    private readonly OpenTicketSource tickets = new();
    private readonly TranscriptReader transcripts = new(options.ClaudeProjectsDir, options.TranscriptCache);
    private Sessions.ContextCycler? cycler;
    private Sessions.ContextCycler contextCycler => cycler ??= new(SessionContextDir) { ProjectsDir = options.ClaudeProjectsDir };

    /// <summary>Where the context gate keeps each session's state; null reads the real one. Replaceable for tests.</summary>
    internal string? SessionContextDir { get; init; }

    private Billing.BillingSwitch? billingSwitch;

    /// <summary>The billing switch (M.ORCH-T436) whose mode and CLIs each snapshot shows; the dashboard's toggle switches it.</summary>
    public Billing.BillingSwitch BillingSwitch { get => billingSwitch ??= new(SessionContextDir); init => billingSwitch = value; }
    private readonly SubagentActivityReader subagents = new(options.ClaudeProjectsDir);

    /// <summary>The Launcher tabs open now (a live pipe scan); replaceable for tests.</summary>
    internal Func<IReadOnlyList<HostSession>> ScanHosts { get; init; } = new HostSessionScanner(options).Scan;

    /// <summary>Codex's and Gemini's own logs under your profile; replaceable for tests.</summary>
    internal ProviderLogReader OtherProviders { get; init; } = ProviderLogReader.Default();

    /// <summary>Claude's own usage report (claude-usage.json); null reads the real one. Replaceable for tests.</summary>
    internal string? ReportedUsagePath { get; init; }

    /// <summary>Reads Claude's meter when no statusline has for a while and tokens are being spent. By default only a
    /// process that writes the real rl-status.json under the status lease (the daemon, a dashboard) asks, and only for the
    /// real claude-usage.json; tests set their own.</summary>
    internal ClaudeMeterProbe? MeterProbe { get; init; }
    private ClaudeMeterProbe? defaultProbe;
    private ClaudeMeterProbe? Probe => MeterProbe ??
        (ReportedUsagePath is null && statusLease is not null && rateLimitFile?.Path == new RateLimitStatusWriter().Path
            ? defaultProbe ??= new ClaudeMeterProbe { AccountKey = () => ClaudeAccount.Current(new ClaudeAccount().ConfigPath) } : null);
    private DateTime auditStamp;

    /// <summary>Which Claude account is signed in, so a switch to another leaves the old one's usage behind. Like the
    /// probe, only the process writing the real rl-status.json watches the real sign-in by default; tests set their own.</summary>
    internal ClaudeAccount? Account { get; init; }
    private ClaudeAccount? defaultAccount;
    private ClaudeAccount? SignIn => Account ??
        (ReportedUsagePath is null && statusLease is not null && rateLimitFile?.Path == new RateLimitStatusWriter().Path
            ? defaultAccount ??= new ClaudeAccount() : null);

    // What the transcripts add up to only changes when they do (M.ORCH-T116): kept until the reader's version, the day or the
    // sessions billed elsewhere move.
    private (long Version, DateTime Today, string OffSubscription) claudeKey = (-1, default, "");
    private List<TranscriptSession> claudeSessions = [];
    private List<TranscriptSession> subscriptionSessions = [];
    private List<UsageEvent> claudeEvents = [];
    private string? lastAccount;
    /// <summary>The stored limit hits as last read: limits.json held past the store's retries for a refresh isn't no hits (M.ORCH-T346).</summary>
    private List<LimitObservation>? lastLimits;
    private double? typicalPerSession;

    // Claude's meter readings (orchestrator.db's usage_samples, M.ORCH-T494), reread only when another process has added some
    // since; those recorded here that couldn't be written yet go out with the next.
    private List<UsageSample>? samples;
    private (long Count, long Last) samplesStamp;
    private readonly List<UsageSample> unsavedSamples = [];
    private long? samplesEstimate;
    // The last report with a usable 5-hour window: carried while that window runs, so one tick without one never drops the limit.
    private ReportedUsage? lastReported;
    // Claude's week at the stop line, held until its reset as the hooks latch it (M.ORCH-T438).
    private ReportedWindow? weekStop;
    private DateTime? probedReset;
    private DateTime historySavedUtc;
    private IReadOnlyDictionary<string, IReadOnlySet<string>> links = new Dictionary<string, IReadOnlySet<string>>();

    public OrchestratorOptions Options => options;
    public JsonStore Store { get; } = store ?? new JsonStore();

    private OrchestratorDb? db;

    /// <summary>The repo's data/orchestrator.db in the workspace's main checkout (M.ORCH-T490): the account roster.</summary>
    public OrchestratorDb Db
    {
        get => db ??= new OrchestratorDb(OrchestratorDb.DefaultPath(options.WorkspaceRoot));
        init => db = value;
    }

    /// <summary>Where the lease holder publishes its snapshots; defaults to beside the transcript cache.</summary>
    internal string? SnapshotPath { get; init; }

    private SnapshotFile? published;
    private SnapshotFile Published => published ??= new SnapshotFile(SnapshotPath ??
        Path.Combine(Path.GetDirectoryName(options.TranscriptCache) is { Length: > 0 } d ? d : Store.Directory, "status-snapshot.json"));

    /// <summary>Whether the last <see cref="Refresh"/> showed the lease holder's published snapshot rather than building one.</summary>
    public bool LastWasShared { get; private set; }

    /// <summary>
    /// The workspace now. Holding the status lease (or taking it while it's free), this builds the snapshot and publishes
    /// it; otherwise it shows the holder's latest, building its own only when there's none younger than
    /// <see cref="SnapshotFile.MaxAge"/>. A refresh for a given time always builds its own.
    /// </summary>
    public Snapshot Refresh(DateTime? nowUtc = null)
    {
        LastWasShared = false;
        if (nowUtc is null && statusLease is not null && !statusLease.IsHeld && !statusLease.TryAcquire() &&
            Published.Read(DateTime.UtcNow) is { } shared && !ResetSince(shared, DateTime.UtcNow))
        {
            LastWasShared = true;
            return WithLiveBilling(WithLiveQueue(shared), DateTime.UtcNow);
        }
        var snapshot = Build(nowUtc ?? DateTime.UtcNow);
        if (nowUtc is null && statusLease is { IsHeld: true }) Published.Write(snapshot);
        return snapshot;
    }

    // The Workflow database's queue as last read, by its write time (M.ORCH-T396).
    private (string Path, DateTime WriteUtc, IReadOnlySet<string> Approved, IReadOnlyDictionary<string, long> Priorities)? liveQueue;

    /// <summary>
    /// <paramref name="shared"/> with approvals and priorities as the Workflow database's queue holds them now (M.ORCH-T396): the
    /// holder's snapshot can be up to <see cref="SnapshotFile.MaxAge"/> old, and a ticket just approved (orchestrator approve, then
    /// dispatch) or bumped would read as it was, where settings.json was read live before. Reread only when the database changes.
    /// </summary>
    internal Snapshot WithLiveQueue(Snapshot shared)
    {
        var path = MindAttic.Workflow.Data.WorkflowDb.DefaultPath(workspace.Root);
        try
        {
            if (!File.Exists(path)) return shared;
            var written = File.GetLastWriteTimeUtc(path);
            if (liveQueue is not { } last || last.Path != path || last.WriteUtc != written)
            {
                using var db = MindAttic.Workflow.Data.WorkflowDb.OpenIfMigrated(workspace.Root);
                if (db is null) return shared;
                var queue = db.QueueStates();
                liveQueue = (path, written,
                    queue.Where(q => q.Value.Approval == MindAttic.Workflow.Data.WorkflowDb.Approved).Select(q => q.Key).ToHashSet(StringComparer.OrdinalIgnoreCase),
                    queue.Where(q => q.Value.Priority != 0).ToDictionary(q => q.Key, q => q.Value.Priority, StringComparer.OrdinalIgnoreCase));
            }
            return shared with { Approved = liveQueue.Value.Approved, Priorities = liveQueue.Value.Priorities };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
        {
            return shared; // the holder's, as read then: tried again next refresh
        }
    }

    /// <summary>
    /// <paramref name="shared"/> with billing as it is now once the mode was written after the holder built it (M.ORCH-T452): a
    /// switch from this process (the dashboard's toggle, <c>orchestrator billing</c>) read back as the old mode for up to
    /// <see cref="SnapshotFile.MaxAge"/>, snapping the toggle back and naming the old billing in the header.
    /// </summary>
    internal Snapshot WithLiveBilling(Snapshot shared, DateTime now)
    {
        try
        {
            var mode = BillingSwitch.ModeFile.Path;
            if (!File.Exists(mode) || File.GetLastWriteTimeUtc(mode) < shared.TakenUtc) return shared;
            var billing = BillingSwitch.Status([.. shared.Repos.SelectMany(r => r.Hosts)], now);
            var bySession = billing.Clis.ToDictionary(c => c.SessionId, Billing.BillingText.Of, StringComparer.Ordinal);
            return shared with
            {
                Billing = billing,
                Agents = [.. shared.Agents.Select(a => bySession.TryGetValue(a.SessionId, out var b) ? a with { Billing = b } : a)],
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException)
        {
            return shared; // the holder's, as published: tried again next refresh
        }
    }

    private Snapshot Build(DateTime now)
    {
        var warnings = new List<string>();
        // The history JSON files (meter readings, pace ledger, usage by day, ticket costs) move into orchestrator.db once (M.ORCH-T494).
        try { Db.ImportJson(Store.Directory); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        {
            warnings.Add($"history import: {ex.Message}");
        }
        // Approvals and priorities settings.json still holds move into the database's queue first, once (M.ORCH-T384).
        try { QueueImport.Run(Store, workspace.Root); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException
                                       or Microsoft.Data.Sqlite.SqliteException)
        {
            warnings.Add($"moving settings.json's approvals into the workflow database: {ex.Message} (tried again next refresh)");
        }
        // So do those of tickets the database didn't hold when they were approved, once it holds them (M.ORCH-T441).
        try { TicketQueue.MoveIntoDatabase(Store, workspace.Root); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException
                                       or KeyNotFoundException or Microsoft.Data.Sqlite.SqliteException)
        {
            warnings.Add($"moving settings.json's story-file approvals into the workflow database: {ex.Message} (tried again next refresh)");
        }
        var settings = Store.LoadSettings();
        var repos = workspace.Repos();

        // Worktrees the scheduler creates live outside the workspace; credit them to their repo.
        var matcher = new RepoMatcher(repos
            .SelectMany(r => new[] { (r.Name, r.Path), (r.Name, Path.Combine(settings.WorktreeRoot, r.Name)) })
            .Append((WorkspaceRootName, workspace.Root)));
        string Bucket(string? dir) => matcher.Match(dir) ?? OutsideName;

        var startOfToday = now.ToLocalTime().Date; // the day of the refresh asked for, not of the wall clock
        var startOfTodayUtc = startOfToday.ToUniversalTime();
        var historyStartUtc = startOfToday.AddDays(-HistoryDays).ToUniversalTime();

        // Claude's sessions drive the budget and pacing (its limits are Claude's); every provider's
        // count toward repo activity and per-repo totals.
        transcripts.WorktreeRoot = settings.WorktreeRoot;
        var polled = transcripts.Poll(historyStartUtc);
        // A CLI on an API key, a gateway or a cloud provider spends none of the subscription's windows (the hooks record each
        // session's auth): its tokens, limit hits and burn stay out of the budget and the pace, though its work still shows.
        var offSubscription = contextCycler.OffSubscription();
        var offKey = string.Join(",", offSubscription.Keys.Order(StringComparer.Ordinal));
        if (claudeKey != (transcripts.Version, startOfTodayUtc, offKey))
        {
            claudeKey = (transcripts.Version, startOfTodayUtc, offKey);
            claudeSessions = [.. polled.Select(s => s with { UsageToday = s.UsageSince(startOfTodayUtc) })];
            subscriptionSessions = [.. claudeSessions.Where(s => s.SessionId is not { } id || !offSubscription.ContainsKey(id))];
            claudeEvents = [.. subscriptionSessions.SelectMany(s => s.Events).OrderBy(e => e.AtUtc)];
            typicalPerSession = Pacer.TypicalPerSession(subscriptionSessions.Select(s => s.Events), historyStartUtc);
        }
        var allSessions = claudeSessions;
        var providerSessions = OtherProviders.Poll(historyStartUtc)
            .Select(s => s with { UsageToday = s.UsageSince(startOfTodayUtc) })
            .ToList();
        var sessions = allSessions.Concat(providerSessions).ToLookup(s => Bucket(s.Cwd));
        var hostSessions = ScanHosts().ToLookup(h => Bucket(h.WorkingDirectory));

        var result = new List<RepoStatus>(repos.Count + 2);
        foreach (var repo in repos)
            result.Add(Repo(repo.Name, repo.Path, tickets.OpenFor(repo, warnings)));

        foreach (var pseudo in new[] { (WorkspaceRootName, workspace.Root), (OutsideName, "") })
        {
            if (sessions[pseudo.Item1].Any() || hostSessions[pseudo.Item1].Any())
                result.Add(Repo(pseudo.Item1, pseudo.Item2, []));
        }

        var events = claudeEvents;
        // Read before the sign-in, so a reading written just after a /login is judged against the new account, never
        // confirmed as the old one's.
        var reported = ReportedUsage.Load(now, ReportedUsagePath);
        // Only the current account's spans count: what another account spent, reported or hit isn't this one's (its weekly
        // stop kept every prompt refused though the new account had room), and going back to an account picks its own up
        // again. Hits stay for learning the limits; their walls go.
        var signIn = SignIn?.Read(now);
        // The probe's last reading, where a statusline wrote over it before this refresh read it (M.ORCH-T479); only the
        // signed-in account's.
        if (Probe?.LastReading is { } probed && (signIn is null || signIn.Owns(probed)))
            reported = ClaudeMeterProbe.Fresher(reported, probed, now);
        var calibration = Calibrate(subscriptionSessions, events, historyStartUtc, now, signIn?.History);
        var switchedNow = false;
        var weekMoved = false;
        if (signIn is not null)
        {
            if (signIn.Account != lastAccount)
            {
                switchedNow = lastAccount is not null;
                lastAccount = signIn.Account;
                lastReported = null;
                weekStop = null;
            }
            events = [.. events.Where(e => signIn.Contains(e.AtUtc))];
            if (reported is not null && (!signIn.Owns(reported) || reported.Account is null && signIn.Switched && !signIn.Contains(reported.AtUtc))) reported = null;
            if (reported is not null) SignIn!.Confirm(signIn.Account, reported, now);
            calibration = new LimitCalibration([.. calibration.Observations.Where(o => signIn.Contains(o.HitUtc) || o.ResetUtc <= now)])
                { Mine = t => !signIn.Others(t) };
            // Every account's latest 5h and weekly percent and resets (M.ORCH-T490): a sign-in refreshes its row, and each
            // point the week moves refreshes it again and reads the meter.
            try { weekMoved = AccountRoster.Observe(Db, signIn, SignIn!.Profile, reported, now); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException) { }
        }
        // No statusline reporting while tokens are spent (a CLI waiting on subagents): read Claude's meter directly (M.ORCH-T203).
        // An account just signed in to has no window of its own yet (every other CLI still shows the old one's): read it now.
        if (Probe is { } probe && (statusLease is null || statusLease.IsHeld) && signIn?.IsApiKey != true)
        {
            // A window that reset since the last reading, with tokens spent in the new one: its percent is unknown until read,
            // so read it now rather than after StaleAfter (or the last ask's wait) runs out.
            var lapsed = ResetSinceReading(reported, now) is { } r && events.Count > 0 && events[^1].AtUtc >= r && events[^1].AtUtc <= now ? r : (DateTime?)null;
            if (switchedNow || lapsed is { } l && l != probedReset)
            {
                probe.Reset();
                probedReset = lapsed ?? probedReset;
            }
            // Seen when the running window was last read, not when a statusline last restamped the file (M.ORCH-T479).
            var seen = ClaudeMeterProbe.SeenAt(reported, signIn is null ? samples ?? [] : (samples ?? []).Where(s => signIn.Contains(s.AtUtc)), now);
            // A sign-in that just moved is read at once, even back to an account whose window is confirmed: claude-usage.json
            // holds the old account's last reading until a CLI replies, minutes on, and the budget, dashboard and statusline
            // showed nothing of the new one's meter until then (M.ORCH-T486).
            // The week moved a point since the roster's reading: read the meter again for it (M.ORCH-T490).
            probe.Tick(seen, events, now, force: switchedNow || weekMoved || lapsed is not null || signIn is { Switched: true } && !signIn.HasWindow(now));
        }
        var reportedLimit = ReportedLimitFrom(reported, events, now, signIn);
        List<UsageSample> mine = signIn is null ? samples ?? [] : [.. (samples ?? []).Where(s => signIn.Contains(s.AtUtc))];
        reported = ReportedLimit.Highest(mine, reported, now);
        reported = KeepWindow(reported, now);
        var budget = BudgetCalculator.Compute(events, settings, now, startOfTodayUtc, learned: calibration,
            reported: reported, reportedLimit: reportedLimit, ledgerFromUtc: signIn?.LedgerFrom);
        budget = HoldWeeklyStop(budget, now);

        var recentFrom = now - BudgetCalculator.RecentWindow;
        var active = subscriptionSessions.Count(s => s.Events.Count > 0 && s.Events[^1].AtUtc >= recentFrom);
        DateTime? marksSeenThrough = null;
        var pace = Pacer.Compute(budget, active, typicalPerSession, settings, now,
            rateLimitFile?.AgentDelay(now) ?? 0, rateLimitFile is null ? 0 : contextCycler.Subagents(out marksSeenThrough));
        pace = pace with
        {
            SubagentsSeenThroughUtc = marksSeenThrough,
            SubagentsByCli = rateLimitFile is null ? new Dictionary<int, int>() : contextCycler.AliveSubagents(now)
                .Where(x => x.Cli is not null).GroupBy(x => x.Cli!.Value).ToDictionary(g => g.Key, g => g.Count()),
        };
        var utilization = UtilizationSeries.Build(mine, budget, settings.PaceTargetPercent, now, settings.PaceExponent);
        // On course to end under its target: the allowance shown to the CLIs (and held to by the pace gate) says to start more (M.ORCH-T529).
        pace = Pacer.UnderTarget(pace, utilization, settings.MaxSubagents);
        // An API key has no 5-hour or weekly window: nothing it spends is held against the subscription's learned limits.
        if (signIn is { IsApiKey: true })
        {
            budget = BudgetCalculator.Compute(events, settings with { BlockBudget = null, WeeklyBudget = null }, now, startOfTodayUtc);
            pace = new Pace(PaceMode.Unpaced, settings.MaxConcurrentSessions, 0, budget.RecentFreshPerHour, null, null, null, Pacer.ApiKeyNote);
        }
        if (rateLimitFile is not null && (statusLease is null || statusLease.IsHeld || statusLease.TryAcquire()))
            rateLimitFile.Write(budget, pace);

        var runningSubagents = subagents.Poll(contextCycler, now);
        var agentBoard = AgentBoard.Build(result, pace, Store.LoadAssignments(), runningSubagents, options.WorkingWindow, now, contextCycler.LiveSessions(now));
        // Who's billed, and each CLI's own billing on its card (M.ORCH-T436); never the refresh's failure.
        Billing.BillingStatus? billing = null;
        try
        {
            billing = BillingSwitch.Status([.. result.SelectMany(r => r.Hosts)], now);
            var bySession = billing.Clis.ToDictionary(c => c.SessionId, c => Billing.BillingText.Of(c), StringComparer.Ordinal);
            agentBoard = [.. agentBoard.Select(a => bySession.TryGetValue(a.SessionId, out var b) ? a with { Billing = b } : a)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException)
        {
            warnings.Add($"billing: {ex.Message}");
        }

        var weekFrom = now.AddDays(-7);
        var providerUsage = new[] { (Providers.Claude, (IEnumerable<TranscriptSession>)allSessions) }
            .Concat(providerSessions.GroupBy(s => s.Provider).Select(g => (g.Key, (IEnumerable<TranscriptSession>)g)))
            .Select(p => new ProviderUsage(p.Item1,
                p.Item2.Aggregate(default(TokenUsage), (sum, s) => sum + s.UsageToday),
                p.Item2.Aggregate(default(TokenUsage), (sum, s) => sum + s.UsageSince(weekFrom)),
                [.. OtherProviders.Limits.Where(l => l.Provider == p.Item1 && l.ResetsUtc > now)]))
            .ToList();

        // A database held past its busy timeout reads as empty: merged and saved, a year of history would be cut to the week.
        var historyRead = Db.TryLoadUsageHistory(out var storedHistory);
        // Each Claude day is kept per account, and a session billed elsewhere apart, and the reset day's tokens before the reset apart
        // from those after it, so the weekly bars count what the budget does.
        string? AccountOf(TranscriptSession s, UsageEvent e) =>
            s.Provider != Providers.Claude ? null
            : s.SessionId is { } id && offSubscription.TryGetValue(id, out var auth) ? UsageHistory.BilledElsewhere + auth
            : signIn?.AccountAt(e.AtUtc);
        var history = UsageHistory.Merge(storedHistory, UsageHistory.Observe(allSessions.Concat(providerSessions), Bucket, historyStartUtc, AccountOf, budget.WeekStartUtc),
            DateOnly.FromDateTime(startOfToday.AddDays(-HistoryDays)), DateOnly.FromDateTime(startOfToday));
        // Today's numbers grow every refresh; a few minutes' lag in the saved copy costs nothing.
        if (historyRead && now - historySavedUtc >= TimeSpan.FromMinutes(5) && !history.SequenceEqual(storedHistory))
        {
            historySavedUtc = now;
            try { Db.SaveUsageHistory(history); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException) { }
        }

        var costsRead = Db.TryLoadTicketCosts(out var storedCosts);
        var costs = TicketCosts.Merge(storedCosts, TicketCosts.Observe(allSessions.Concat(providerSessions), Bucket), now,
            TicketCosts.Unticketed(allSessions.Concat(providerSessions)));
        if (costsRead && !costs.SequenceEqual(storedCosts))
        {
            try { Db.SaveTicketCosts(costs); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException) { }
        }

        return new Snapshot(now, result, warnings) { Budget = budget, Pace = pace, Utilization = utilization, ProviderUsage = providerUsage, Costs = new TicketCosts(costs), LinkedRepos = LinkedRepos(), History = history, Account = signIn?.Account, Requires = tickets.Requires, Approved = tickets.Approved, Priorities = tickets.Priorities, Agents = agentBoard, Billing = billing };

        RepoStatus Repo(string name, string path, IReadOnlyList<MindAttic.Workflow.Tickets.Ticket> open)
        {
            var repoSessions = sessions[name].OrderByDescending(s => s.LastWriteUtc).ToList();
            var repoHosts = hostSessions[name].ToList();
            DateTime? last = repoSessions.Count > 0 ? repoSessions[0].LastWriteUtc : null;
            var usage = repoSessions.Aggregate(default(TokenUsage), (sum, s) => sum + s.UsageToday);
            return new RepoStatus(name, path, Classify(last, repoHosts.Count, now), repoHosts, repoSessions, open, last, usage);
        }
    }

    /// <summary>How far back the first run looks for limit hits: about as long as Claude Code keeps transcripts.</summary>
    public const int BackfillDays = 30;

    /// <summary>
    /// Folds limit hits in the transcripts into <c>limits.json</c>. The first run reads the last
    /// month once, so limits are learned from hits you already had rather than the next one.
    /// </summary>
    /// <summary>Keeps a week of Claude's 5-hour reports (with the local tokens behind each) in
    /// orchestrator.db's <c>usage_samples</c> and sizes the limit from how they moved; null until one window has moved enough.</summary>
    /// <remarks>Every account's samples are kept; only <paramref name="signIn"/>'s size its limit.</remarks>
    private long? ReportedLimitFrom(ReportedUsage? reported, IReadOnlyList<UsageEvent> events, DateTime now, SignInState? signIn)
    {
        var stamped = Db.TryUsageSamplesStamp(out var stamp);
        if (samples is null || stamped && stamp != samplesStamp)
        {
            // A database another process holds past the busy timeout isn't an empty week: keep what was read before and try
            // again next tick. Taken as empty, the running window's samples were gone for that tick (no 5-hour limit, so no
            // pace, M.ORCH-T245).
            if (!stamped || !Db.TryLoadUsageSamples(out var loaded)) return samplesEstimate;
            samples = [.. loaded.Concat(unsavedSamples.Where(u => loaded.All(s => s.AtUtc != u.AtUtc))).OrderBy(s => s.AtUtc)];
            samplesStamp = stamp;
        }
        var recorded = ReportedLimit.Record(samples, reported, events, now, signIn is null ? null : signIn.Contains);
        samplesEstimate = ReportedLimit.Estimate(signIn is null ? samples : [.. samples.Where(s => signIn.Contains(s.AtUtc))]);
        if (recorded)
        {
            unsavedSamples.Add(samples.Last(s => s.AtUtc == reported!.AtUtc));
            // Like every other save here, a held database mustn't fail the whole refresh (no rl-status.json that tick); the
            // sample stays in memory and goes out with the next one recorded.
            try
            {
                Db.AddUsageSamples(unsavedSamples, now - OrchestratorHistory.KeepSamples);
                unsavedSamples.Clear();
                if (Db.TryUsageSamplesStamp(out var saved)) samplesStamp = saved;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException) { }
        }
        return samplesEstimate;
    }

    /// <summary>The latest reset of <paramref name="reading"/>'s windows that came after it was read and has passed by now; null when none has.</summary>
    internal static DateTime? ResetSinceReading(ReportedUsage? reading, DateTime now) =>
        new[] { reading?.FiveHour, reading?.Week }.Where(w => w is not null && w.ResetUtc > reading!.AtUtc && w.ResetUtc <= now)
            .Select(w => (DateTime?)w!.ResetUtc).Max();

    /// <summary>Whether a window <paramref name="snapshot"/> was worked out for has reset since it was taken: its pace, stop and
    /// room are the old window's.</summary>
    internal static bool ResetSince(Snapshot snapshot, DateTime now) =>
        new[] { snapshot.Pace?.ResetUtc, snapshot.Budget?.CurrentBlock?.EndUtc, snapshot.Budget?.WeekResetUtc, snapshot.Budget?.AtLimitUntilUtc }
            .Any(r => r is { } at && at > snapshot.TakenUtc && at <= now);

    /// <summary>
    /// <paramref name="reported"/>, or, when it has no 5-hour window still running (an idle tab restamping its last reply
    /// from a window that already reset, a report with no 5-hour part), the last one that had, while that window runs.
    /// Without a 5-hour window the budget had no limit to count against, so the pace went unpaced for that tick and
    /// rl-status.json was withdrawn: every call held at the pace went through at once (M.ORCH-T245, Oct 17 04:37-04:58Z,
    /// once a minute as an idle tab's statusline redrew). The samples' running window covers this too; this keeps the
    /// limit when they can't (not recorded yet, or unreadable).
    /// </summary>
    /// <summary>
    /// <paramref name="budget"/> with Claude's week at the stop line held until its reset (M.ORCH-T438). The hooks latch that
    /// stop the moment they see it; every open session's statusline restamps its own last, lower weekly percent each minute, and
    /// a report older than <see cref="ReportedUsage.MaxAge"/> drops out, so without the hold the daemon would lift the stop
    /// (and hand out tickets) while every tool call is still refused. Usage in a week never goes down.
    /// </summary>
    internal BudgetStatus HoldWeeklyStop(BudgetStatus budget, DateTime now)
    {
        if (weekStop is { } held && held.ResetUtc <= now) weekStop = null;
        if (budget.WeeklyStopped(now) && budget.WeeklyStop is { } seen &&
            (weekStop is null || seen.ResetUtc != weekStop.ResetUtc || seen.Percent >= weekStop.Percent))
            weekStop = seen;
        return weekStop is null ? budget : budget with { WeeklyStop = weekStop };
    }

    private ReportedUsage? KeepWindow(ReportedUsage? reported, DateTime now)
    {
        if (ReportedUsage.Usable(reported?.FiveHour, now) is not null)
        {
            lastReported = reported;
            return reported;
        }
        if (lastReported?.FiveHour is not { } kept || kept.ResetUtc <= now) return reported;
        return reported is null ? lastReported
            : new ReportedUsage(kept, reported.Week, lastReported.AtUtc) { WeekAtUtc = reported.WeekAt };
    }

    private LimitCalibration Calibrate(IReadOnlyList<TranscriptSession> sessions, IReadOnlyList<UsageEvent> events, DateTime historyStartUtc, DateTime now,
        IReadOnlyList<AccountSpan>? signIns = null)
    {
        // A wall is learned from the tokens of the account that hit it, not every account's in its window: one signed in to
        // for an hour of it would make the limit look bigger than it is (M.ORCH-T347).
        string? AccountAt(DateTime utc) => signIns?.LastOrDefault(s => utc >= s.From && (s.To is not { } to || utc < to))?.Account;
        IReadOnlyList<LimitObservation> Observe(IEnumerable<LimitHit> hits, IReadOnlyList<UsageEvent> evs, DateTime since) =>
            signIns is not { Count: > 1 } ? LimitCalibration.Observe(hits, evs, since)
            : [.. hits.GroupBy(h => AccountAt(h.AtUtc)).SelectMany(g => LimitCalibration.Observe(g,
                g.Key is null ? evs : [.. evs.Where(e => AccountAt(e.AtUtc) == g.Key)], since)).OrderBy(o => o.HitUtc)];
        var read = Store.TryLoadLimits(out var stored);
        // Unreadable right now (held, or caught mid-write): the hits last read stand in, or the learned limit would go for this
        // refresh, the pace with it, and rl-status.json would be withdrawn as M.ORCH-T245's was (M.ORCH-T346).
        if (read) lastLimits = stored;
        else stored = lastLimits ?? [];
        var observed = Observe(sessions.SelectMany(s => s.Hits), events, historyStartUtc);
        if (!Store.HasLimits)
        {
            var since = now.AddDays(-BackfillDays);
            var old = new TranscriptReader(options.ClaudeProjectsDir).Poll(since);
            List<UsageEvent> oldEvents = [.. old.SelectMany(s => s.Events).OrderBy(e => e.AtUtc)];
            observed = [.. observed, .. Observe(old.SelectMany(s => s.Hits), oldEvents, since)];
        }

        var merged = LimitCalibration.Merge(stored, observed, now);
        if (read && (!Store.HasLimits || !merged.SequenceEqual(stored)))
        {
            try { Store.SaveLimits(merged); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return new LimitCalibration(merged);
    }

    /// <summary>Repos the last audit found sharing duplicated code; reread only when the audit file changes.</summary>
    private IReadOnlyDictionary<string, IReadOnlySet<string>> LinkedRepos()
    {
        var stamp = Store.WrittenUtc("audit") ?? default;
        if (stamp == auditStamp) return links;
        // Taken only once read: a failed read kept the map empty until the next audit, up to a day later (M.ORCH-T433).
        if (!new Audit.AuditRunner(options, Store).TryLatest(out var latest)) return links;
        auditStamp = stamp;
        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in latest?.Duplicates ?? [])
        {
            var repos = group.Files.Select(f => f.Repo).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var a in repos)
                foreach (var b in repos.Where(b => !b.Equals(a, StringComparison.OrdinalIgnoreCase)))
                    (map.TryGetValue(a, out var set) ? set : map[a] = new(StringComparer.OrdinalIgnoreCase)).Add(b);
        }
        return links = map.ToDictionary(kv => kv.Key, kv => (IReadOnlySet<string>)kv.Value, StringComparer.OrdinalIgnoreCase);
    }

    internal RepoState Classify(DateTime? lastActivityUtc, int hostCount, DateTime nowUtc)
    {
        var age = lastActivityUtc is null ? TimeSpan.MaxValue : nowUtc - lastActivityUtc.Value;
        if (age <= options.WorkingWindow) return RepoState.Working;
        if (hostCount > 0 || age <= options.WaitingWindow) return RepoState.Waiting;
        return RepoState.Dormant;
    }
}
