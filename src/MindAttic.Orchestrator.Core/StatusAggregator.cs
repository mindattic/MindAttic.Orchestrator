using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Budget;
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
public sealed class StatusAggregator(OrchestratorOptions options, JsonStore? store = null, RateLimitStatusWriter? rateLimitFile = null)
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

    /// <summary>The Launcher tabs open now (a live pipe scan); replaceable for tests.</summary>
    internal Func<IReadOnlyList<HostSession>> ScanHosts { get; init; } = new HostSessionScanner(options).Scan;

    /// <summary>Codex's and Gemini's own logs under your profile; replaceable for tests.</summary>
    internal ProviderLogReader OtherProviders { get; init; } = ProviderLogReader.Default();
    private DateTime auditStamp;
    private DateTime historySavedUtc;
    private IReadOnlyDictionary<string, IReadOnlySet<string>> links = new Dictionary<string, IReadOnlySet<string>>();

    public OrchestratorOptions Options => options;
    public JsonStore Store { get; } = store ?? new JsonStore();

    public Snapshot Refresh(DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var settings = Store.LoadSettings();
        var warnings = new List<string>();
        var repos = workspace.Repos();

        // Worktrees the scheduler creates live outside the workspace; credit them to their repo.
        var matcher = new RepoMatcher(repos
            .SelectMany(r => new[] { (r.Name, r.Path), (r.Name, Path.Combine(settings.WorktreeRoot, r.Name)) })
            .Append((WorkspaceRootName, workspace.Root)));
        string Bucket(string? dir) => matcher.Match(dir) ?? OutsideName;

        var startOfToday = DateTime.Now.Date;
        var startOfTodayUtc = startOfToday.ToUniversalTime();
        var historyStartUtc = startOfToday.AddDays(-HistoryDays).ToUniversalTime();

        // Claude's sessions drive the budget and pacing (its limits are Claude's); every provider's
        // count toward repo activity and per-repo totals.
        var allSessions = transcripts.Poll(historyStartUtc)
            .Select(s => s with { UsageToday = s.UsageSince(startOfTodayUtc) })
            .ToList();
        var providerSessions = OtherProviders.Poll(historyStartUtc)
            .Select(s => s with { UsageToday = s.UsageSince(startOfTodayUtc) })
            .ToList();
        var sessions = allSessions.Concat(providerSessions).ToLookup(s => Bucket(s.Cwd));
        var hostSessions = ScanHosts().ToLookup(h => Bucket(h.WorkingDirectory));

        var result = new List<RepoStatus>(repos.Count + 2);
        foreach (var repo in repos)
            result.Add(Build(repo.Name, repo.Path, tickets.OpenFor(repo, warnings)));

        foreach (var pseudo in new[] { (WorkspaceRootName, workspace.Root), (OutsideName, "") })
        {
            if (sessions[pseudo.Item1].Any() || hostSessions[pseudo.Item1].Any())
                result.Add(Build(pseudo.Item1, pseudo.Item2, []));
        }

        List<UsageEvent> events = [.. allSessions.SelectMany(s => s.Events).OrderBy(e => e.AtUtc)];
        var calibration = Calibrate(allSessions, events, historyStartUtc, now);
        var reported = ReportedUsage.Load(now);
        var budget = BudgetCalculator.Compute(events, settings, now, startOfTodayUtc, learned: calibration,
            reported: reported, reportedLimit: ReportedLimitFrom(reported, events, now));

        var recentFrom = now - BudgetCalculator.RecentWindow;
        var active = allSessions.Count(s => s.Events.Count > 0 && s.Events[^1].AtUtc >= recentFrom);
        var contextCycler = new Sessions.ContextCycler();
        var pace = Pacer.Compute(budget, active, Pacer.TypicalPerSession(allSessions.Select(s => s.Events), historyStartUtc), settings, now,
            rateLimitFile?.AgentDelay(now) ?? 0, rateLimitFile is null ? 0 : contextCycler.Subagents());
        rateLimitFile?.Write(budget, pace);

        var runningSubagents = SubagentActivityReader.Running(contextCycler, options.ClaudeProjectsDir, now);
        var agentBoard = AgentBoard.Build(result, pace, Store.LoadAssignments(), runningSubagents, options.WorkingWindow, now);

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
        var costs = TicketCosts.Merge(storedCosts, TicketCosts.Observe(allSessions.Concat(providerSessions), Bucket), now);
        if (!costs.SequenceEqual(storedCosts))
        {
            try { Store.SaveTicketCosts(costs); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }

        return new Snapshot(now, result, warnings) { Budget = budget, Pace = pace, ProviderUsage = providerUsage, Costs = new TicketCosts(costs), LinkedRepos = LinkedRepos(), History = history, Requires = tickets.Requires, Agents = agentBoard };

        RepoStatus Build(string name, string path, IReadOnlyList<MindAttic.Workflow.Tickets.Ticket> open)
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
    private long? ReportedLimitFrom(ReportedUsage? reported, IReadOnlyList<UsageEvent> events, DateTime now)
    {
        if (store is null) return null;
        var samples = store.Load<List<UsageSample>>("usage-samples");
        if (ReportedLimit.Record(samples, reported, events, now)) store.Save("usage-samples", samples);
        return ReportedLimit.Estimate(samples);
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
