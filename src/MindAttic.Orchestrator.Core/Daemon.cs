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
        void Build() => aggregator = new StatusAggregator(options, store, new RateLimitStatusWriter(), lease);
        Build();

        Say(LogEventLevel.Information, $"daemon started (pid {Environment.ProcessId}, every {interval.TotalSeconds:0}s, workspace {options.WorkspaceRoot})");
        var cycler = new ContextCycler();
        var autopilot = new Autopilot(store, cycler, lease) { WaitingPath = Autopilot.DefaultWaitingPath };
        cycler.IsAgent = autopilot.IsAgent;
        string? lastPaused = null;
        cycler.Prune(DateTime.UtcNow);
        (int, bool)? gatePercent = null; // what the hooks were last written with
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
                if (gatePercent != (s.ContextCyclePercent, s.KeepGoing))
                {
                    try
                    {
                        if (Setup.AgentGate.Ensure(contextPercent: s.ContextCyclePercent, keepGoing: s.KeepGoing) is { } did) Say(LogEventLevel.Information, did);
                        if (Setup.AgentGate.EnsureOtherProviders() is { } didOthers) Say(LogEventLevel.Information, didOthers);
                        gatePercent = (s.ContextCyclePercent, s.KeepGoing);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Say(LogEventLevel.Warning, $"couldn't install the agent hooks (retrying next tick): {ex.Message}"); }
                }
                WatchHealth(autopilot);
                foreach (var r in ReapIfDue())
                    Say(LogEventLevel.Warning, $"reaped pid {r.Process.Pid} ({r.Process.Name}): {r.Reason}" +
                        (string.IsNullOrWhiteSpace(r.Process.CommandLine) ? "" : $" — was running: {r.Process.CommandLine}"));

                cycler.PruneMarks();
                foreach (var line in cycler.KeepGoingSince())
                {
                    Say(LogEventLevel.Information, line);
                    if (line.StartsWith("keep-going: let the ", StringComparison.Ordinal)) Alert("Needs you", line["keep-going: ".Length..]);
                }
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
                    var needsYou = Autopilot.NeedsYou(line);
                    Say(needsYou is null ? LogEventLevel.Information : LogEventLevel.Warning, line);
                    if (needsYou is not null) Alert("Needs you", needsYou);
                }
                if (autopilot.On && autopilot.Paused != lastPaused)
                {
                    Say(LogEventLevel.Information, autopilot.Paused is { } why ? $"autopilot is holding: {why}" : "autopilot is starting stories again");
                    lastPaused = autopilot.Paused;
                }
                if (autopilot.On && autopilot.HeldElsewhere != lastHeldElsewhere)
                {
                    Say(LogEventLevel.Information, autopilot.HeldElsewhere
                        ? "another orchestrator (an open dashboard) is running autopilot and keeping the budget file fresh; this does neither until it's free"
                        : "running autopilot from this process");
                    lastHeldElsewhere = autopilot.HeldElsewhere;
                }
            }
            catch (Exception ex)
            {
                Say(LogEventLevel.Error, $"refresh failed: {ex.Message}", ex);
            }
            WaitForNextRefresh(interval, cancel);
        }
        Say(LogEventLevel.Information, "daemon stopped");
    }

    /// <summary>Touched by the context gate whenever a turn or a subagent ends (its reply's tokens are then in its transcript).</summary>
    public static string RefreshSignal { get; } = Path.Combine(Setup.AgentGate.ClaudeDir, "session-context", "refresh.now");

    /// <summary>The soonest a signal brings the next refresh forward after the last one.</summary>
    internal static readonly TimeSpan MinGap = TimeSpan.FromSeconds(2);

    private DateTime lastSignal = SignalStamp();

    /// <summary>
    /// Waits out <paramref name="interval"/>, but refreshes early when a turn ends anywhere (the context gate's
    /// <see cref="RefreshSignal"/>) or Claude's own usage report is rewritten, so what you type and every reply,
    /// tool calls or not, is in the pace within a couple of seconds rather than at the next tick.
    /// </summary>
    private void WaitForNextRefresh(TimeSpan interval, CancellationToken cancel)
    {
        var until = DateTime.UtcNow + interval;
        var earliest = DateTime.UtcNow + MinGap;
        while (DateTime.UtcNow < until)
        {
            if (cancel.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(500))) return;
            if (DateTime.UtcNow < earliest) continue;
            var stamp = SignalStamp();
            if (stamp == lastSignal) continue;
            lastSignal = stamp;
            return;
        }
        lastSignal = SignalStamp();
    }

    private static DateTime SignalStamp()
    {
        static DateTime Of(string path) { try { return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : default; } catch (IOException) { return default; } }
        var a = Of(RefreshSignal);
        var b = Of(ReportedUsage.DefaultPath);
        return a > b ? a : b;
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

    private DateTime reapedUtc;

    /// <summary>Every <see cref="Sessions.ProcessReaper.Every"/>, kills the tool processes dead agents left behind.</summary>
    private IReadOnlyList<Sessions.ProcessReaper.Reaped> ReapIfDue()
    {
        if (DateTime.UtcNow - reapedUtc < Sessions.ProcessReaper.Every) return [];
        reapedUtc = DateTime.UtcNow;
        return Sessions.ProcessReaper.ReapOnce(reapedUtc);
    }

    private Activity.HealthLevel healthLevel;
    private DateTime healthSampledUtc;

    /// <summary>
    /// Every 30 seconds, samples handles, commit charge and free RAM. A change of level is logged and alerted with the
    /// worst offenders, and while it isn't OK autopilot starts no agents.
    /// </summary>
    private void WatchHealth(Autopilot autopilot)
    {
        if (DateTime.UtcNow - healthSampledUtc < TimeSpan.FromSeconds(30)) return;
        healthSampledUtc = DateTime.UtcNow;
        var sample = Activity.MachineHealth.Sample();
        var (level, why) = Activity.MachineHealth.Judge(sample);
        autopilot.HealthHold = level == Activity.HealthLevel.Ok ? null : why;
        if (level == healthLevel) return;
        var top = string.Join(", ", sample.Top.Select(h => $"{h.Name} {h.Handles:N0}"));
        if (level == Activity.HealthLevel.Ok)
            Say(LogEventLevel.Information, "machine health is back to normal; autopilot can start agents again");
        else
        {
            Say(level == Activity.HealthLevel.Danger ? LogEventLevel.Error : LogEventLevel.Warning,
                $"machine health {level.ToString().ToLowerInvariant()}: {why} (most handles: {top}); autopilot starts no agents until it recovers");
            Alert(level == Activity.HealthLevel.Danger ? "Machine in danger" : "Machine running low",
                $"{why}. Most handles: {top}. Autopilot is holding.", urgent: level == Activity.HealthLevel.Danger);
        }
        healthLevel = level;
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
            var error = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(120_000)) { p.Kill(true); Say(LogEventLevel.Warning, "workflow db sync took over 2 minutes; stopped it"); return; }
            if (p.ExitCode == 0) syncedDbWriteUtc = written;
            Say(p.ExitCode == 0 ? LogEventLevel.Information : LogEventLevel.Warning, $"workflow db sync: {(output.Result + error.Result).Trim()}");
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
