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
    private readonly HostSessionScanner hosts = new(options);
    private readonly TranscriptReader transcripts = new(options.ClaudeProjectsDir);

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

        var allSessions = transcripts.Poll(historyStartUtc)
            .Select(s => s with { UsageToday = s.UsageSince(startOfTodayUtc) })
            .ToList();
        var sessions = allSessions.ToLookup(s => Bucket(s.Cwd));
        var hostSessions = hosts.Scan().ToLookup(h => Bucket(h.WorkingDirectory));

        var result = new List<RepoStatus>(repos.Count + 2);
        foreach (var repo in repos)
            result.Add(Build(repo.Name, repo.Path, tickets.OpenFor(repo, warnings)));

        foreach (var pseudo in new[] { (WorkspaceRootName, workspace.Root), (OutsideName, "") })
        {
            if (sessions[pseudo.Item1].Any() || hostSessions[pseudo.Item1].Any())
                result.Add(Build(pseudo.Item1, pseudo.Item2, []));
        }

        var budget = BudgetCalculator.Compute(
            [.. allSessions.SelectMany(s => s.Events)], settings, now, startOfTodayUtc);
        rateLimitFile?.Write(budget);

        return new Snapshot(now, result, warnings) { Budget = budget };

        RepoStatus Build(string name, string path, IReadOnlyList<MindAttic.Workflow.Tickets.Ticket> open)
        {
            var repoSessions = sessions[name].OrderByDescending(s => s.LastWriteUtc).ToList();
            var repoHosts = hostSessions[name].ToList();
            DateTime? last = repoSessions.Count > 0 ? repoSessions[0].LastWriteUtc : null;
            var usage = repoSessions.Aggregate(default(TokenUsage), (sum, s) => sum + s.UsageToday);
            return new RepoStatus(name, path, Classify(last, repoHosts.Count, now), repoHosts, repoSessions, open, last, usage);
        }
    }

    internal RepoState Classify(DateTime? lastActivityUtc, int hostCount, DateTime nowUtc)
    {
        var age = lastActivityUtc is null ? TimeSpan.MaxValue : nowUtc - lastActivityUtc.Value;
        if (age <= options.WorkingWindow) return RepoState.Working;
        if (hostCount > 0 || age <= options.WaitingWindow) return RepoState.Waiting;
        return RepoState.Dormant;
    }
}
