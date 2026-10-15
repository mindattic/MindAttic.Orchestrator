using System.Text.RegularExpressions;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using Serilog.Events;

namespace MindAttic.Orchestrator;

/// <summary>
/// The always-on loop the logon task runs: keeps <c>rl-status.json</c> and the pace current, notices
/// stale assignments, cycles context, and feeds the CLIs where /autopilot is on (holding <see cref="DispatchLease"/>). Every
/// event goes to MindAttic.Log; <c>echo</c> also gets it when someone is watching (the CLI's console).
/// Runs until cancelled; a failed refresh is logged and retried, since nobody sees a headless crash.
/// </summary>
public sealed partial class Daemon(JsonStore store, Action<LogEventLevel, string>? echo = null)
{
    private readonly Serilog.ILogger log = OrchestratorLog.For("Daemon");
    private readonly Notifier notifier = new();
    private string? notifyUrl;
    private DateTime intakeUtc;

    public void Run(TimeSpan interval, CancellationToken cancel)
    {
        var options = OrchestratorOptions.Load(store);
        using var lease = new DispatchLease(store.Directory);
        StatusAggregator aggregator = null!;
        void Build() => aggregator = new StatusAggregator(options, store, new RateLimitStatusWriter());
        Build();

        Say(LogEventLevel.Information, $"daemon started (pid {Environment.ProcessId}, every {interval.TotalSeconds:0}s, workspace {options.WorkspaceRoot})");
        var cycler = new ContextCycler();
        var autopilot = new Autopilot(store, cycler, lease);
        cycler.IsAgent = autopilot.IsAgent;
        string? lastPaused = null;
        cycler.Prune(DateTime.UtcNow);
        int? gatePercent = null;
        string? lastHold = null;
        (PaceMode, int)? lastPace = null;
        bool? lastHeldElsewhere = null;
        var knownStale = new HashSet<string>(StringComparer.Ordinal);
        var knownStuck = new HashSet<string>(StringComparer.Ordinal);
        var lastStates = new Dictionary<string, RepoState>(StringComparer.OrdinalIgnoreCase);
        while (!cancel.IsCancellationRequested)
        {
            try
            {
                // The dashboard's first run may pick the workspace after the logon task already started this.
                if (OrchestratorOptions.Load(store) is { } latest && latest.WorkspaceRoot != options.WorkspaceRoot)
                {
                    options = latest;
                    Build();
                    Say(LogEventLevel.Information, $"workspace is now {options.WorkspaceRoot}");
                }
                var snapshot = aggregator.Refresh();
                var s = store.LoadSettings();
                var plan = Scheduler.Plan(snapshot, s, store.LoadAssignments());
                notifyUrl = s.NotifyUrl;

                // The hooks carry the cycle point, so rewrite them when it changes.
                if (gatePercent != s.ContextCyclePercent)
                {
                    try
                    {
                        if (Setup.AgentGate.Ensure(contextPercent: s.ContextCyclePercent) is { } did) Say(LogEventLevel.Information, did);
                        if (Setup.AgentGate.EnsureOtherProviders() is { } didOthers) Say(LogEventLevel.Information, didOthers);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Say(LogEventLevel.Warning, $"couldn't install the agent hooks: {ex.Message}"); }
                    gatePercent = s.ContextCyclePercent;
                }
                foreach (var r in Sessions.ProcessReaper.ReapOnce(DateTime.UtcNow))
                    Say(LogEventLevel.Warning, $"reaped pid {r.Process.Pid} ({r.Process.Name}): {r.Reason}" +
                        (string.IsNullOrWhiteSpace(r.Process.CommandLine) ? "" : $" — was running: {r.Process.CommandLine}"));

                cycler.PruneMarks();
                foreach (var line in cycler.Tick([.. snapshot.Repos.SelectMany(r => r.Hosts)], s, DateTime.UtcNow))
                {
                    Say(LogEventLevel.Information, line);
                    if (line.EndsWith("yourself", StringComparison.Ordinal)) Alert("Context nearly full", line);
                }

                // An agent finished its turn and is waiting on you: the alert worth leaving the desk for.
                foreach (var repo in snapshot.Repos)
                {
                    if (lastStates.TryGetValue(repo.Name, out var was) && was == RepoState.Working && repo.State == RepoState.Waiting)
                        Alert("Needs you", $"{repo.Name}: the agent finished its turn and is waiting.");
                    lastStates[repo.Name] = repo.State;
                }

                if (snapshot.Pace is { } pace && (pace.Mode, pace.SessionCap) != lastPace)
                {
                    if (lastPace is { } prev && pace.Mode is PaceMode.Coast or PaceMode.Over && pace.Mode > prev.Item1)
                        Alert(pace.Mode == PaceMode.Over ? "At the limit" : "Coasting", pace.Note, urgent: pace.Mode == PaceMode.Over);
                    Say(pace.Mode is PaceMode.Open or PaceMode.Unpaced ? LogEventLevel.Information : LogEventLevel.Warning,
                        $"pace: {pace.Key}. {pace.Note}" + (pace.Advise ? " Running agents are asked to conserve." : ""));
                    lastPace = (pace.Mode, pace.SessionCap);
                }

                // Hold text carries live rates; log it when it says something new, not every time a number moves.
                var holdKey = plan.Hold is null ? null : Digits().Replace(plan.Hold, "#");
                if (holdKey != lastHold && plan.Hold is not null) Say(LogEventLevel.Information, plan.Hold);
                lastHold = holdKey;

                foreach (var q in plan.Stale.Where(q => knownStale.Add(q.Id)))
                {
                    Say(LogEventLevel.Warning, $"{q.Id} went stale: nothing in {q.Assignment!.Directory} since {Formatting.Ago(q.StaleSinceUtc, snapshot.TakenUtc)}; it's back in the queue");
                    Alert("Stale ticket", $"{q.Id} in {q.Repo}: its session went quiet without closing it; it's back in the queue.");
                }
                knownStale.IntersectWith(plan.Stale.Select(q => q.Id));

                var stuck = Activity.Watchdog.Stuck(snapshot);
                foreach (var x in stuck.Where(x => knownStuck.Add(x.SessionId + x.Loop.Tool + x.Loop.Input)))
                {
                    Say(LogEventLevel.Warning, $"a session in {x.Repo} looks stuck, repeating {x.Loop.Describe()} since {x.Loop.SinceUtc.ToLocalTime():t}");
                    Alert("Looks stuck", $"A session in {x.Repo} keeps repeating {x.Loop.Describe()}.", urgent: true);
                }
                knownStuck.IntersectWith(stuck.Select(x => x.SessionId + x.Loop.Tool + x.Loop.Input));

                RunAuditIfDue(options, s);
                SyncWorkflowDbIfDue(options);
                if (s.GitHubIntake && DateTime.UtcNow - intakeUtc >= TimeSpan.FromHours(1))
                {
                    intakeUtc = DateTime.UtcNow;
                    foreach (var (repo, ticket) in Intake(options.WorkspaceRoot))
                        Say(LogEventLevel.Information, $"filed {ticket.Id} in {repo} from GitHub: {ticket.Summary}");
                }

                foreach (var line in autopilot.Tick(plan, snapshot, s, DateTime.UtcNow))
                {
                    var needsYou = line.Contains("needs you", StringComparison.Ordinal);
                    Say(needsYou ? LogEventLevel.Warning : LogEventLevel.Information, line);
                    if (needsYou) Alert("Needs you", line["autopilot: needs you: ".Length..]);
                }
                if (autopilot.On && autopilot.Paused != lastPaused)
                {
                    Say(LogEventLevel.Information, autopilot.Paused is { } why ? $"autopilot is holding: {why}" : "autopilot is starting stories again");
                    lastPaused = autopilot.Paused;
                }
                if (autopilot.On && autopilot.HeldElsewhere != lastHeldElsewhere)
                {
                    Say(LogEventLevel.Information, autopilot.HeldElsewhere
                        ? "another orchestrator (an open dashboard) is running autopilot; this one only keeps the budget file fresh"
                        : "running autopilot from this process");
                    lastHeldElsewhere = autopilot.HeldElsewhere;
                }
            }
            catch (Exception ex)
            {
                Say(LogEventLevel.Error, $"refresh failed: {ex.Message}", ex);
            }
            cancel.WaitHandle.WaitOne(interval);
        }
        Say(LogEventLevel.Information, "daemon stopped");
    }

    /// <summary>The scheduled audit: when the last one is older than <see cref="OrchestratorSettings.AuditEveryHours"/>, run it and report what's new.</summary>
    private void RunAuditIfDue(OrchestratorOptions options, OrchestratorSettings s)
    {
        if (s.AuditEveryHours <= 0) return;
        var runner = new Audit.AuditRunner(options, store);
        var before = runner.Latest();
        if (before is not null && DateTime.UtcNow - before.RanUtc < TimeSpan.FromHours(s.AuditEveryHours)) return;
        var after = runner.Run();
        var news = Audit.AuditNews.Between(before, after);
        if (news.Any)
        {
            Say(LogEventLevel.Warning, $"audit: {news}");
            Alert("Audit found something new", news.ToString());
        }
        else Say(LogEventLevel.Information, $"audit: nothing new ({after.Drift.Count} drifted files, {after.Duplicates.Count} duplicate groups, {after.Took.TotalSeconds:0}s)");
    }

    private DateTime syncedDbWriteUtc;
    private DateTime syncTriedUtc;

    /// <summary>
    /// Every 10 minutes, when the Workflow database has changed since the last sync, commits it (and its readable dump)
    /// in the MindAttic.Workflow repo and pushes, so tickets are backed up and reach other machines.
    /// </summary>
    private void SyncWorkflowDbIfDue(OrchestratorOptions options)
    {
        var path = MindAttic.Workflow.Data.WorkflowDb.DefaultPath(options.WorkspaceRoot);
        if (!File.Exists(path) || DateTime.UtcNow - syncTriedUtc < TimeSpan.FromMinutes(10)) return;
        var written = File.GetLastWriteTimeUtc(path);
        if (written == syncedDbWriteUtc) return;
        syncTriedUtc = DateTime.UtcNow;
        try
        {
            using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("workflow", "db sync")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            })!;
            var output = p.StandardOutput.ReadToEndAsync();
            var error = p.StandardError.ReadToEnd();
            if (!p.WaitForExit(120_000)) { p.Kill(true); Say(LogEventLevel.Warning, "workflow db sync took over 2 minutes; stopped it"); return; }
            if (p.ExitCode == 0) syncedDbWriteUtc = written;
            Say(p.ExitCode == 0 ? LogEventLevel.Information : LogEventLevel.Warning, $"workflow db sync: {(output.Result + error).Trim()}");
        }
        catch (System.ComponentModel.Win32Exception ex) { Say(LogEventLevel.Warning, $"couldn't run 'workflow db sync': {ex.Message}"); }
    }

    /// <summary>Files new GitHub issues and CI failures as tickets in every repo that has a GitHub origin.</summary>
    public static IEnumerable<(string Repo, MindAttic.Workflow.Tickets.Ticket Ticket)> Intake(string workspaceRoot, bool dryRun = false)
    {
        var remotes = Setup.WorkspaceBootstrap.Export(workspaceRoot).Repos.ToDictionary(m => m.Name, m => m.Remote, StringComparer.OrdinalIgnoreCase);
        foreach (var repo in new MindAttic.Workflow.Workspace(workspaceRoot).Repos().Where(r => File.Exists(r.StoriesPath)))
        {
            var remote = remotes.GetValueOrDefault(repo.Name);
            if (remote is null || Tickets.GitHubIntake.Slug(remote) is not { } slug) continue;
            var items = Tickets.GitHubIntake.Find(slug, Tickets.GitHubIntake.Gh, DateTime.UtcNow);
            if (items.Count == 0) continue;
            using (var db = MindAttic.Workflow.Data.WorkflowDb.OpenIfMigrated(workspaceRoot))
                if (db is not null && db.Repos().Any(x => x.Name.Equals(repo.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    if (dryRun) continue; // a dry run files nothing
                    foreach (var t in Tickets.GitHubIntake.FileNew(db, repo.Name, items)) yield return (repo.Name, t);
                    continue;
                }
            var stories = MindAttic.Workflow.Tickets.StoryFile.Load(repo.StoriesPath);
            var filed = Tickets.GitHubIntake.FileNew(stories, items);
            if (filed.Count > 0 && !dryRun) stories.Save();
            foreach (var t in filed) yield return (repo.Name, t);
        }
    }

    private void Alert(string title, string message, bool urgent = false) =>
        _ = notifier.SendAsync(notifyUrl, title, message, urgent);

    private void Say(LogEventLevel level, string line, Exception? ex = null)
    {
        log.Write(level, ex, "{Line:l}", line);
        echo?.Invoke(level, line);
    }

    [GeneratedRegex(@"[\d.,]+")]
    private static partial Regex Digits();
}
