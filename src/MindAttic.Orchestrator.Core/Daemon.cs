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
        var statusFile = new RateLimitStatusWriter();
        void Build() => aggregator = new StatusAggregator(options, store, statusFile, lease);
        Build();

        Say(LogEventLevel.Information, $"daemon started (pid {Environment.ProcessId}, every {interval.TotalSeconds:0}s, workspace {options.WorkspaceRoot})");
        var cycler = new ContextCycler();
        var autopilot = new Autopilot(store, cycler, lease) { WaitingPath = Autopilot.DefaultWaitingPath };
        cycler.IsAgent = autopilot.IsAgent;
        string? lastPaused = null;
        cycler.Prune(DateTime.UtcNow);
        string? lastHold = null;
        (PaceMode, int)? lastPace = null;
        bool? lastHeldElsewhere = null;
        var ledger = new PaceLedgerWatch(store, options.WorkspaceRoot, (level, line) => Say(level, line), (title, message, urgent) => Alert(title, message, urgent));
        var knownStale = new HashSet<string>(StringComparer.Ordinal);
        var knownStuck = new HashSet<string>(StringComparer.Ordinal);
        var lastStates = new Dictionary<string, RepoState>(StringComparer.OrdinalIgnoreCase);
        var tick = new TickTimer();
        while (!cancel.IsCancellationRequested)
        {
            tick.Start();
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
                tick.Lap("refresh");
                ReportStatusFile(statusFile);
                var s = store.LoadSettings();
                var plan = Scheduler.Plan(snapshot, s, store.LoadAssignments());
                notifyUrl = s.NotifyUrl;

                EnsureHooks(s);
                WatchHealth(autopilot);
                tick.Lap("hooks and machine health");
                try { ledger.Tick(DateTime.UtcNow); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Say(LogEventLevel.Warning, $"pace ledger: couldn't update it (retrying next minute): {ex.Message}"); }
                tick.Lap("pace ledger");
                foreach (var r in ReapIfDue())
                    Say(LogEventLevel.Warning, $"reaped pid {r.Process.Pid} ({r.Process.Name}): {r.Reason}" +
                        (string.IsNullOrWhiteSpace(r.Process.CommandLine) ? "" : $" — was running: {r.Process.CommandLine}"));
                tick.Lap("process reaper");

                cycler.PruneMarks();
                foreach (var line in cycler.KeepGoingSince())
                {
                    Say(LogEventLevel.Information, line);
                    if (line.StartsWith("keep-going: let the ", StringComparison.Ordinal)) Alert("Needs you", line["keep-going: ".Length..]);
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
                // The context gate's keep-going reads which claims went stale: `orchestrator next` hands those out again.
                var staleIds = plan.Stale.Select(q => q.Id).Order(StringComparer.Ordinal).ToList();
                // Rewritten each minute even unchanged: the gate trusts it only while it's fresh, i.e. while this daemon runs
                // (rl-status.json can't vouch for that: the dashboard keeps it fresh too).
                if (publishedStale is null || !staleIds.SequenceEqual(publishedStale) || DateTime.UtcNow - publishedStaleUtc >= TimeSpan.FromMinutes(1))
                    try
                    {
                        store.Save(StaleTicketsName, staleIds);
                        // And what else `orchestrator next` holds back that keep-going can't see from the files it reads:
                        // quiet hours, tickets another live session or subagent is working (M.ORCH-T255), and stale claims
                        // whose background agent still runs.
                        store.Save(AutopilotHoldsName, new AutopilotHolds(
                            QuietHours.Active(s.QuietHours, DateTime.Now) is not null,
                            [.. plan.Worked],
                            [.. plan.Stale.Where(q => q.Assignment is { Pilot: { } p } a && Sessions.BackgroundAgent.IsRunning(p, a.PilotStartedUtc)).Select(q => q.Id)]));
                        publishedStale = staleIds;
                        publishedStaleUtc = DateTime.UtcNow;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }

                var stuck = Activity.Watchdog.Stuck(snapshot);
                foreach (var x in stuck.Where(x => knownStuck.Add(x.SessionId + x.Loop.Tool + x.Loop.Input)))
                {
                    Say(LogEventLevel.Warning, $"a session in {x.Repo} looks stuck, repeating {x.Loop.Describe()} since {x.Loop.SinceUtc.ToLocalTime():t}");
                    Alert("Looks stuck", $"A session in {x.Repo} keeps repeating {x.Loop.Describe()}.", urgent: true);
                }
                knownStuck.IntersectWith(stuck.Select(x => x.SessionId + x.Loop.Tool + x.Loop.Input));

                // An hour and half an hour before the reset: a window on course to end under its target with nothing for any
                // CLI to work is said, never acted on (M.ORCH-T176).
                ReportUnderTarget(snapshot, plan, cycler.AutopilotClis());
                tick.Lap("keep-going and alerts");

                RunAuditIfDue(options, s);
                tick.Lap("audit");
                SyncWorkflowDbIfDue(options);
                tick.Lap("workflow db sync");
                if (s.GitHubIntake && DateTime.UtcNow - intakeUtc >= TimeSpan.FromHours(1))
                {
                    intakeUtc = DateTime.UtcNow;
                    foreach (var (repo, ticket) in Intake(options.WorkspaceRoot, gh: args => Tickets.GitHubIntake.Gh(Runner, Tickets.GitHubIntake.GhTimeout, args)))
                        Say(LogEventLevel.Information, $"filed {ticket.Id} in {repo} from GitHub: {ticket.Summary}");
                }
                tick.Lap("GitHub intake");

                ReportAutopilot(autopilot.Tick(plan, snapshot, s, DateTime.UtcNow));
                tick.Lap("autopilot");
                // Keyed without its numbers: a hold whose percent ticks 93 → 94 is the same hold, not a new line (found live: 46 lines in 2h).
                var pausedKey = autopilot.Paused is null ? null : Digits().Replace(autopilot.Paused, "#");
                if (autopilot.On && pausedKey != lastPaused)
                {
                    Say(LogEventLevel.Information, autopilot.Paused is { } why ? $"autopilot is holding: {why}" : "autopilot is starting stories again");
                    lastPaused = pausedKey;
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
            if (tick.Slow() is { } slow) Say(LogEventLevel.Warning, slow);
            WaitForNextRefresh(interval, cancel);
        }
        Say(LogEventLevel.Information, "daemon stopped");
    }

    private readonly UnderTarget underTarget = new();

    /// <summary>Logs and raises the Needs-you alert when <see cref="UnderTarget"/> says one is due.</summary>
    internal void ReportUnderTarget(Snapshot snapshot, SchedulePlan plan, IReadOnlyDictionary<int, IReadOnlyList<string>> autopilotClis)
    {
        if (underTarget.Check(snapshot, plan, autopilotClis, snapshot.TakenUtc) is not { } line) return;
        Say(LogEventLevel.Warning, $"under target: {line}");
        Alert("Needs you", line);
    }

    private DateTime? reportedFailing;

    /// <summary>Says once when <c>rl-status.json</c> stops being written (the gates then hold at its last pace) and once
    /// when writes land again, rather than letting it go stale in silence (M.ORCH-T205).</summary>
    internal void ReportStatusFile(RateLimitStatusWriter file)
    {
        if (file.FailingSinceUtc is { } since && reportedFailing != since && DateTime.UtcNow - since >= TickTimer.SlowTick / 2)
        {
            reportedFailing = since;
            Say(LogEventLevel.Warning, $"couldn't write rl-status.json since {since.ToLocalTime():T} ({file.LastError}); " +
                "the pace gates hold calls at the last pace it gave until it's written again");
        }
        else if (file.FailingSinceUtc is null && reportedFailing is not null)
        {
            reportedFailing = null;
            Say(LogEventLevel.Information, "rl-status.json is being written again");
        }
    }

    /// <summary>
    /// Times each part of a tick. <c>rl-status.json</c> is written once a tick, so a tick that runs long leaves the pace
    /// gates on a stale file; one past <see cref="SlowTick"/> is logged with where its time went, so a stall can be traced
    /// to its step (M.ORCH-T205: no sample for 5 minutes and nothing said why).
    /// </summary>
    internal sealed class TickTimer(Func<DateTime>? clock = null)
    {
        /// <summary>A tick this long is a third of the way to the gates' stale line.</summary>
        public static readonly TimeSpan SlowTick = RateLimitStatusWriter.GateStale / 3;

        private readonly Func<DateTime> now = clock ?? (() => DateTime.UtcNow);
        private readonly List<(string Name, TimeSpan Took)> laps = [];
        private DateTime started, last;

        public void Start()
        {
            laps.Clear();
            started = last = now();
        }

        public void Lap(string name)
        {
            var at = now();
            laps.Add((name, at - last));
            last = at;
        }

        /// <summary>The line to log when the tick since <see cref="Start"/> ran past <see cref="SlowTick"/>; null otherwise.</summary>
        public string? Slow()
        {
            var total = now() - started;
            if (total < SlowTick) return null;
            var parts = laps.Where(l => l.Took >= TimeSpan.FromSeconds(1)).OrderByDescending(l => l.Took)
                .Select(l => $"{l.Name} {l.Took.TotalSeconds:0}s");
            return $"a tick took {total.TotalSeconds:0}s ({string.Join(", ", parts.DefaultIfEmpty("no one step over a second"))}); " +
                "rl-status.json isn't refreshed while a tick runs";
        }
    }

    /// <summary>Installs the agent hooks; the default writes Claude's and the other providers'. A test's stands in for it.</summary>
    internal Func<int, bool, IEnumerable<string?>> InstallHooks { get; init; } = (percent, keepGoing) =>
        [Setup.AgentGate.Ensure(contextPercent: percent, keepGoing: keepGoing), Setup.AgentGate.EnsureOtherProviders()];

    /// <summary>Runs <c>workflow db sync</c> and <c>gh</c>; a test's stands in for the real one.</summary>
    internal IProcessRunner Runner { get; init; } = ProcessRunner.Default;

    /// <summary>How long <c>workflow db sync</c> may run before it's killed, so a hung push can't stall the loop.</summary>
    internal TimeSpan SyncTimeout { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>Where alerts go; null sends them through the <see cref="Notifier"/>. A test's records them.</summary>
    internal Action<string, string, bool>? AlertSink { get; init; }

    private (int, bool)? gatePercent; // what the hooks were last written with

    /// <summary>
    /// The hooks carry the cycle point, so they're rewritten when it (or keep-going) changes. A write that fails on the
    /// file system (a hook file locked by a running CLI) is logged and left undone, so the next tick tries again.
    /// </summary>
    internal void EnsureHooks(OrchestratorSettings s)
    {
        if (gatePercent == (s.ContextCyclePercent, s.KeepGoing)) return;
        try
        {
            foreach (var did in InstallHooks(s.ContextCyclePercent, s.KeepGoing).ToList())
                if (did is not null) Say(LogEventLevel.Information, did);
            gatePercent = (s.ContextCyclePercent, s.KeepGoing);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Say(LogEventLevel.Warning, $"couldn't install the agent hooks (retrying next tick): {ex.Message}"); }
    }

    /// <summary>Logs autopilot's lines; only its "needs you" lines (<see cref="Autopilot.NeedsYou"/>) are warnings and raise the
    /// Needs you alert, not a started line whose ticket summary happens to say "needs you".</summary>
    internal void ReportAutopilot(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            var needsYou = Autopilot.NeedsYou(line);
            Say(needsYou is null ? LogEventLevel.Information : LogEventLevel.Warning, line);
            if (needsYou is not null) Alert("Needs you", needsYou);
        }
    }

    /// <summary>Touched by the context gate whenever a turn or a subagent ends (its reply's tokens are then in its transcript).</summary>
    public static string RefreshSignal { get; } = Path.Combine(Setup.AgentGate.ClaudeDir, "session-context", "refresh.now");

    /// <summary>The soonest a signal brings the next refresh forward after the last one.</summary>
    // Each refresh runs the whole tick (a WMI process query for the Launcher's tabs among it), and with a dozen agents a
    // turn ends every second or two: 10s keeps typed turns counted well inside the 15s tick without refreshing nonstop.
    internal static readonly TimeSpan MinGap = TimeSpan.FromSeconds(10);

    private readonly RefreshWaiter waiter = new([RefreshSignal, ReportedUsage.DefaultPath], MinGap);

    /// <summary>
    /// Waits out <paramref name="interval"/>, but refreshes early when a turn ends anywhere (the context gate's
    /// <see cref="RefreshSignal"/>) or Claude's own usage report is rewritten, so what you type and every reply,
    /// tool calls or not, is in the pace within a couple of seconds rather than at the next tick.
    /// </summary>
    private void WaitForNextRefresh(TimeSpan interval, CancellationToken cancel) => waiter.Wait(interval, cancel);

    /// <summary>The scheduled audit: when the last one is older than <see cref="OrchestratorSettings.AuditEveryHours"/>, run it and report what's new.</summary>
    private DateTime auditTriedUtc;

    /// <summary>The stale claims' ticket IDs, kept in the store for the context gate (see <see cref="Setup.AgentGate"/>'s workable()).</summary>
    public const string StaleTicketsName = "stale-tickets";

    /// <summary>What else orchestrator next holds back, for the context gate's keep-going (rewritten with the stale list).</summary>
    public const string AutopilotHoldsName = "autopilot-holds";

    public sealed record AutopilotHolds(bool Quiet, IReadOnlyList<Scheduling.WorkedTicket> Worked, IReadOnlyList<string> LivePilot);
    private IReadOnlyList<string>? publishedStale;
    private DateTime publishedStaleUtc;

    private void RunAuditIfDue(OrchestratorOptions options, OrchestratorSettings s)
    {
        if (s.AuditEveryHours <= 0) return;
        var every = TimeSpan.FromHours(s.AuditEveryHours);
        var runner = new Audit.AuditRunner(options, store);
        var before = runner.Latest();
        if (before is not null && DateTime.UtcNow - before.RanUtc < every) return;
        // A run whose save failed isn't retried every tick (it scans the whole workspace), and the failure doesn't end
        // the tick before autopilot's turn: it's said, and tried again when the next one is due.
        if (DateTime.UtcNow - auditTriedUtc < every) return;
        auditTriedUtc = DateTime.UtcNow;
        Audit.AuditReport after;
        try { after = runner.Run(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Say(LogEventLevel.Warning, $"audit: couldn't save it (trying again in {s.AuditEveryHours}h): {ex.Message}");
            return;
        }
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
        // Judged against the last level, so it only comes down once clearly clear of the line, a step at a time.
        var (level, why) = Activity.MachineHealth.Judge(sample, healthLevel);
        autopilot.HealthHold = level == Activity.HealthLevel.Ok ? null : why;
        if (level == healthLevel) return;
        var offenders = sample.Offenders(why);
        if (level == Activity.HealthLevel.Ok)
            Say(LogEventLevel.Information, "machine health is back to normal; autopilot can start agents again");
        else
        {
            Say(level == Activity.HealthLevel.Danger ? LogEventLevel.Error : LogEventLevel.Warning,
                $"machine health {level.ToString().ToLowerInvariant()}: {why} ({offenders}); autopilot starts no agents until it recovers");
            // One alert per episode and per escalation: warn, then danger; not again for every step back down and up.
            if (level > alertedHealth)
                Alert(level == Activity.HealthLevel.Danger ? "Machine in danger" : "Machine running low",
                    $"{why}. {char.ToUpperInvariant(offenders[0])}{offenders[1..]}. Autopilot is holding.", urgent: level == Activity.HealthLevel.Danger);
        }
        alertedHealth = level == Activity.HealthLevel.Ok ? level : (Activity.HealthLevel)Math.Max((int)alertedHealth, (int)level);
        healthLevel = level;
    }

    private Activity.HealthLevel alertedHealth;

    private DateTime syncedDbWriteUtc;
    private DateTime syncTriedUtc;

    /// <summary>
    /// Every 10 minutes, when the Workflow database has changed since the last sync, commits it (and its readable dump)
    /// in the MindAttic.Workflow repo and pushes, so tickets are backed up and reach other machines.
    /// </summary>
    private void SyncWorkflowDbIfDue(OrchestratorOptions options) =>
        SyncWorkflowDbIfDue(MindAttic.Workflow.Data.WorkflowDb.DefaultPath(options.WorkspaceRoot), DateTime.UtcNow);

    /// <summary>The sync for the database at <paramref name="path"/>. One that runs past <see cref="SyncTimeout"/> is killed and
    /// logged, and the database isn't counted as synced, so the next try (10 minutes on) runs it again.</summary>
    internal void SyncWorkflowDbIfDue(string path, DateTime nowUtc)
    {
        if (!File.Exists(path) || nowUtc - syncTriedUtc < TimeSpan.FromMinutes(10)) return;
        var written = File.GetLastWriteTimeUtc(path);
        if (written == syncedDbWriteUtc) return;
        syncTriedUtc = nowUtc;
        try
        {
            var r = Runner.Run("workflow", ["db", "sync"], SyncTimeout);
            if (r.TimedOut) { Say(LogEventLevel.Warning, $"workflow db sync took over {SyncTimeout.TotalSeconds:0}s; stopped it (retrying in 10 minutes)"); return; }
            if (r.ExitCode == 0) syncedDbWriteUtc = written;
            Say(r.ExitCode == 0 ? LogEventLevel.Information : LogEventLevel.Warning, $"workflow db sync: {(r.Output + r.Error).Trim()}");
        }
        catch (System.ComponentModel.Win32Exception ex) { Say(LogEventLevel.Warning, $"couldn't run 'workflow db sync': {ex.Message}"); }
    }

    /// <summary>Files new GitHub issues and CI failures as tickets in every repo that has a GitHub origin.</summary>
    public static IEnumerable<(string Repo, MindAttic.Workflow.Tickets.Ticket Ticket)> Intake(string workspaceRoot, bool dryRun = false,
        Func<string[], (int Code, string Output)>? gh = null)
    {
        gh ??= Tickets.GitHubIntake.Gh;
        var remotes = Setup.WorkspaceBootstrap.Export(workspaceRoot).Repos.ToDictionary(m => m.Name, m => m.Remote, StringComparer.OrdinalIgnoreCase);
        foreach (var repo in new MindAttic.Workflow.Workspace(workspaceRoot).Repos().Where(r => File.Exists(r.StoriesPath)))
        {
            var remote = remotes.GetValueOrDefault(repo.Name);
            if (remote is null || Tickets.GitHubIntake.Slug(remote) is not { } slug) continue;
            var items = Tickets.GitHubIntake.Find(slug, gh, DateTime.UtcNow);
            if (items.Count == 0) continue;
            using (var db = MindAttic.Workflow.Data.WorkflowDb.OpenIfMigrated(workspaceRoot))
                if (db is not null && db.Repos().Any(x => x.Name.Equals(repo.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    // A dry run files nothing, but still says what it would file.
                    foreach (var t in Tickets.GitHubIntake.FileNew(db, repo.Name, items, dryRun)) yield return (repo.Name, t);
                    continue;
                }
            var stories = MindAttic.Workflow.Tickets.StoryFile.Load(repo.StoriesPath);
            var filed = Tickets.GitHubIntake.FileNew(stories, items);
            if (filed.Count > 0 && !dryRun) stories.Save();
            foreach (var t in filed) yield return (repo.Name, t);
        }
    }

    private void Alert(string title, string message, bool urgent = false)
    {
        if (AlertSink is { } sink) sink(title, message, urgent);
        else _ = notifier.SendAsync(notifyUrl, title, message, urgent);
    }

    private void Say(LogEventLevel level, string line, Exception? ex = null)
    {
        log.Write(level, ex, "{Line:l}", line);
        echo?.Invoke(level, line);
    }

    [GeneratedRegex(@"[\d.,]+")]
    private static partial Regex Digits();
}
