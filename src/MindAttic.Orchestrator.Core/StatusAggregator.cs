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
    private double? typicalPerSession;

    // usage-samples.json, reread only when another process has written it since.
    private List<UsageSample>? samples;
    private DateTime samplesStamp;
    private long? samplesEstimate;
    // The last report with a usable 5-hour window: carried while that window runs, so one tick without one never drops the limit.
    private ReportedUsage? lastReported;
    private DateTime historySavedUtc;
    private IReadOnlyDictionary<string, IReadOnlySet<string>> links = new Dictionary<string, IReadOnlySet<string>>();

    public OrchestratorOptions Options => options;
    public JsonStore Store { get; } = store ?? new JsonStore();

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
            Published.Read(DateTime.UtcNow) is { } shared)
        {
            LastWasShared = true;
            return shared;
        }
        var snapshot = Build(nowUtc ?? DateTime.UtcNow);
        if (nowUtc is null && statusLease is { IsHeld: true }) Published.Write(snapshot);
        return snapshot;
    }

    private Snapshot Build(DateTime now)
    {
        var settings = Store.LoadSettings();
        var warnings = new List<string>();
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
        var calibration = Calibrate(subscriptionSessions, events, historyStartUtc, now);
        // Read before the sign-in, so a reading written just after a /login is judged against the new account, never
        // confirmed as the old one's.
        var reported = ReportedUsage.Load(now, ReportedUsagePath);
        // Only the current account's spans count: what another account spent, reported or hit isn't this one's (its weekly
        // stop kept every prompt refused though the new account had room), and going back to an account picks its own up
        // again. Hits stay for learning the limits; their walls go.
        var signIn = SignIn?.Read(now);
        var switchedNow = false;
        if (signIn is not null)
        {
            if (signIn.Account != lastAccount)
            {
                switchedNow = lastAccount is not null;
                lastAccount = signIn.Account;
                lastReported = null;
            }
            events = [.. events.Where(e => signIn.Contains(e.AtUtc))];
            if (reported is not null && (!signIn.Owns(reported) || reported.Account is null && signIn.Switched && !signIn.Contains(reported.AtUtc))) reported = null;
            if (reported is not null) SignIn!.Confirm(signIn.Account, reported, now);
            calibration = new LimitCalibration([.. calibration.Observations.Where(o => signIn.Contains(o.HitUtc) || o.ResetUtc <= now)]);
        }
        // No statusline reporting while tokens are spent (a CLI waiting on subagents): read Claude's meter directly (M.ORCH-T203).
        // An account just signed in to has no window of its own yet (every other CLI still shows the old one's): read it now.
        if (Probe is { } probe && (statusLease is null || statusLease.IsHeld) && signIn?.IsApiKey != true)
        {
            if (switchedNow) probe.Reset();
            probe.Tick(reported?.AtUtc, events, now, force: signIn is { Switched: true } && !signIn.HasWindow(now));
        }
        var reportedLimit = ReportedLimitFrom(reported, events, now, signIn);
        List<UsageSample> mine = signIn is null ? samples ?? [] : [.. (samples ?? []).Where(s => signIn.Contains(s.AtUtc))];
        reported = ReportedLimit.Highest(mine, reported, now);
        reported = KeepWindow(reported, now);
        var budget = BudgetCalculator.Compute(events, settings, now, startOfTodayUtc, learned: calibration,
            reported: reported, reportedLimit: reportedLimit);

        var recentFrom = now - BudgetCalculator.RecentWindow;
        var active = subscriptionSessions.Count(s => s.Events.Count > 0 && s.Events[^1].AtUtc >= recentFrom);
        var pace = Pacer.Compute(budget, active, typicalPerSession, settings, now,
            rateLimitFile?.AgentDelay(now) ?? 0, rateLimitFile is null ? 0 : contextCycler.Subagents());
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

        var weekFrom = now.AddDays(-7);
        var providerUsage = new[] { (Providers.Claude, (IEnumerable<TranscriptSession>)allSessions) }
            .Concat(providerSessions.GroupBy(s => s.Provider).Select(g => (g.Key, (IEnumerable<TranscriptSession>)g)))
            .Select(p => new ProviderUsage(p.Item1,
                p.Item2.Aggregate(default(TokenUsage), (sum, s) => sum + s.UsageToday),
                p.Item2.Aggregate(default(TokenUsage), (sum, s) => sum + s.UsageSince(weekFrom)),
                [.. OtherProviders.Limits.Where(l => l.Provider == p.Item1 && l.ResetsUtc > now)]))
            .ToList();

        var storedHistory = Store.LoadUsageHistory();
        var history = UsageHistory.Merge(storedHistory, UsageHistory.Observe(allSessions.Concat(providerSessions), Bucket, historyStartUtc),
            DateOnly.FromDateTime(startOfToday.AddDays(-HistoryDays)), DateOnly.FromDateTime(startOfToday));
        // Today's numbers grow every refresh; a few minutes' lag in the saved copy costs nothing.
        if (now - historySavedUtc >= TimeSpan.FromMinutes(5) && !history.SequenceEqual(storedHistory))
        {
            historySavedUtc = now;
            try { Store.SaveUsageHistory(history); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }

        var storedCosts = Store.LoadTicketCosts();
        var costs = TicketCosts.Merge(storedCosts, TicketCosts.Observe(allSessions.Concat(providerSessions), Bucket), now,
            TicketCosts.Unticketed(allSessions.Concat(providerSessions)));
        if (!costs.SequenceEqual(storedCosts))
        {
            try { Store.SaveTicketCosts(costs); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }

        return new Snapshot(now, result, warnings) { Budget = budget, Pace = pace, Utilization = UtilizationSeries.Build(mine, budget, settings.PaceTargetPercent, now), ProviderUsage = providerUsage, Costs = new TicketCosts(costs), LinkedRepos = LinkedRepos(), History = history, Requires = tickets.Requires, Agents = agentBoard };

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
    /// <c>usage-samples.json</c> and sizes the limit from how they moved; null until one window has moved enough.</summary>
    /// <remarks>Every account's samples are kept; only <paramref name="signIn"/>'s size its limit.</remarks>
    private long? ReportedLimitFrom(ReportedUsage? reported, IReadOnlyList<UsageEvent> events, DateTime now, SignInState? signIn)
    {
        var path = Path.Combine(Store.Directory, "usage-samples.json");
        var stamp = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : default;
        if (samples is null || stamp != samplesStamp)
        {
            // A file another process holds past the store's retries (or catches mid-write) isn't an empty week: keep what was
            // read before and try again next tick. Taken as empty, the running window's samples were gone for that tick (no
            // 5-hour limit, so no pace, M.ORCH-T245), the stamp stopped it rereading, and the next sample saved over the week.
            if (!Store.TryLoad<List<UsageSample>>("usage-samples", out var loaded)) return samplesEstimate;
            samples = loaded;
            samplesStamp = stamp;
        }
        var recorded = ReportedLimit.Record(samples, reported, events, now);
        samplesEstimate = ReportedLimit.Estimate(signIn is null ? samples : [.. samples.Where(s => signIn.Contains(s.AtUtc))]);
        if (recorded)
        {
            // Like every other save here, a locked file mustn't fail the whole refresh (no rl-status.json that tick);
            // the sample stays in memory and goes out with the next one recorded.
            try
            {
                Store.Save("usage-samples", samples);
                samplesStamp = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : default;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return samplesEstimate;
    }

    /// <summary>
    /// <paramref name="reported"/>, or, when it has no 5-hour window still running (an idle tab restamping its last reply
    /// from a window that already reset, a report with no 5-hour part), the last one that had, while that window runs.
    /// Without a 5-hour window the budget had no limit to count against, so the pace went unpaced for that tick and
    /// rl-status.json was withdrawn: every call held at the pace went through at once (M.ORCH-T245, Oct 17 04:37-04:58Z,
    /// once a minute as an idle tab's statusline redrew). The samples' running window covers this too; this keeps the
    /// limit when they can't (not recorded yet, or unreadable).
    /// </summary>
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

    private LimitCalibration Calibrate(IReadOnlyList<TranscriptSession> sessions, IReadOnlyList<UsageEvent> events, DateTime historyStartUtc, DateTime now)
    {
        var stored = Store.LoadLimits();
        var observed = LimitCalibration.Observe(sessions.SelectMany(s => s.Hits), events, historyStartUtc);
        if (!Store.HasLimits)
        {
            var since = now.AddDays(-BackfillDays);
            var old = new TranscriptReader(options.ClaudeProjectsDir).Poll(since);
            List<UsageEvent> oldEvents = [.. old.SelectMany(s => s.Events).OrderBy(e => e.AtUtc)];
            observed = [.. observed, .. LimitCalibration.Observe(old.SelectMany(s => s.Hits), oldEvents, since)];
        }

        var merged = LimitCalibration.Merge(stored, observed, now);
        if (!Store.HasLimits || !merged.SequenceEqual(stored))
        {
            try { Store.SaveLimits(merged); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return new LimitCalibration(merged);
    }

    /// <summary>Repos the last audit found sharing duplicated code; reread only when the audit file changes.</summary>
    private IReadOnlyDictionary<string, IReadOnlySet<string>> LinkedRepos()
    {
        var file = Path.Combine(Store.Directory, "audit.json");
        var stamp = File.Exists(file) ? File.GetLastWriteTimeUtc(file) : default;
        if (stamp == auditStamp) return links;
        auditStamp = stamp;
        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in new Audit.AuditRunner(options, Store).Latest()?.Duplicates ?? [])
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
