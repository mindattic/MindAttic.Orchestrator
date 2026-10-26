using System.Text.RegularExpressions;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using Serilog.Events;

namespace MindAttic.Orchestrator;

/// <summary>
/// The always-on loop Scheduler's Orchestrator.Daemon job runs at logon: keeps <c>rl-status.json</c> and the pace current, notices
/// stale assignments, cycles context, and feeds the CLIs where /allocate is on (holding <see cref="DispatchLease"/>). Every
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
        using var lease = new DispatchLease(store.Directory, db: store.Db);
        StatusAggregator aggregator = null!;
        var statusFile = new RateLimitStatusWriter { Db = store.Db };
        void Build() => aggregator = new StatusAggregator(options, store, statusFile, lease);
        Build();

        Say(LogEventLevel.Information, $"daemon started (pid {Environment.ProcessId}, every {interval.TotalSeconds:0}s, workspace {options.WorkspaceRoot})");
        // Every meter ask and what came of it is on the record (M.ORCH-T659), to trace a window that went unseen.
        ClaudeMeterProbe.Say = (level, line) => Say(level, line);
        // The status stream (M.ORCH-T854): every reading and snapshot pushed to the open dashboards the moment it lands.
        using var status = ServeStatus ? new Status.StatusStreamServer { Files = WatchedUsage, ReadingsChanged = waiter.Poke } : null;
        status?.Start();
        // Its inbox (M.ORCH-T872): a hook that ended a turn, or a writer of a usage file, says so the moment it does, rather than
        // the daemon noticing the file; any other kind written there is relayed to every subscriber.
        // The app bus (M.ORCH-T879): ticket changes, Orchestrator.Desktop's and Tickets.Desktop's presence and their requests of
        // each other go through the hub, every ticket change read from the database's change feed too, so a ticket filed,
        // approved or closed anywhere reaches every app (and this daemon's next plan) within seconds.
        var hub = status is null ? null : new Status.AppBusHub(status, new Tickets.TicketActivityFeed())
        {
            TicketChanged = waiter.Poke,
            // The roster, orgs, settings, pause and reminders (M.ORCH-T891): this store's are kept for connecting dashboards, and a
            // change to one brings the next plan forward.
            OwnStore = Status.StateBus.StoreOf(store.Db), StateChanged = waiter.Poke,
        };
        using var inbox = status is null ? null : new Status.StatusInbox { Relay = e => hub!.Take(e) }
            .On(Status.StatusInbox.Poke, _ => waiter.Poke())
            .On(Status.StatusInbox.UsageWritten, _ => status.UsageWritten());
        inbox?.Start();
        if (hub is not null)
        {
            Status.AppBusClient.Hub = hub;
            _ = Task.Run(() => hub.FollowFeedAsync(() => options.WorkspaceRoot, TimeSpan.FromSeconds(2), cancel));
            // Every change this process writes to them is pushed from now on (through the hub, in this process), and each as it
            // stands now is kept for the dashboards that connect, whatever changed while no daemon ran (M.ORCH-T891).
            Status.StateBus.Enable(background: false);
            Status.StateBus.PublishAll(store);
        }
        // The fallback viewers (M.ORCH-T960): while the dashboard is down, every hosted CLI gets its own TermViewer window. On its own
        // thread, once a second, contained: nothing it does can end this loop.
        if (ShowFallbackViewers) StartFallbackViewers(cancel);
        void OnReading(ClaudeMeterProbe probe, ReportedUsage reading) =>
            status?.PublishReadings([Status.MeterReading.Of(probe.Label, reading, signedIn: null, "probe")]);
        ClaudeMeterProbe.ReadingLanded += OnReading;
        var cycler = new ContextCycler();
        var allocate = new Allocate(store, cycler, lease) { WaitingPath = Allocate.DefaultWaitingPath };
        var waker = new LimitWaker(cycler, store);
        var pilotWaker = new AllocateWaker(cycler, store);
        // One daemon wakes the CLIs: an `orchestrator daemon` run beside the logon one would type every resume twice.
        using var wakeLease = new DispatchLease(store.Directory, LimitWaker.LeaseName, store.Db);
        using var ledgerLease = new DispatchLease(store.Directory, PaceLedgerWatch.LeaseName, store.Db);
        using var remindLease = new DispatchLease(store.Directory, Reminders.ReminderSender.LeaseName, store.Db);
        // Each repo's full suite on its main line after a landing, with no LLM (M.ORCH-T561); one daemon runs them.
        using var testsLease = new DispatchLease(store.Directory, MainlineTests.LeaseName, store.Db);
        // The git-touching chores (audit, db syncs, mirror, GitHub intake) run in one daemon: two in the same checkout collide on
        // index.lock and undo each other's reset and merge --abort (M.ORCH-T722).
        using var gitChoresLease = new DispatchLease(store.Directory, GitChoresLeaseName, store.Db);
        var mainTests = new MainlineTests(store);
        var reminders = new Reminders.ReminderSender(store, new Reminders.PsstClient(Runner), (level, line) => Say(level, line), (title, message, urgent) => Alert(title, message, urgent));
        string? lastPaused = null;
        cycler.Prune(DateTime.UtcNow);
        var prunedUtc = DateTime.UtcNow;
        string? lastHold = null;
        (PaceMode, int)? lastPace = null;
        bool? lastHeldElsewhere = null;
        var ledger = new PaceLedgerWatch(store, options.WorkspaceRoot, (level, line) => Say(level, line), (title, message, urgent) => Alert(title, message, urgent))
            { Account = new ClaudeAccount(), AccountDirs = new AccountDirs() };
        var knownStale = new HashSet<string>(StringComparer.Ordinal);
        var knownStuck = new HashSet<string>(StringComparer.Ordinal);
        var lastStates = new Dictionary<string, RepoState>(StringComparer.OrdinalIgnoreCase);
        var tick = new TickTimer();
        var termHostsCleanedUtc = DateTime.MinValue;
        var gateway = new GatewaySupervisor((level, line) => Say(level, line));
        while (!cancel.IsCancellationRequested)
        {
            tick.Start();
            try
            {
                // The dashboard's first run may pick the workspace after Scheduler already started this.
                // Only from a settings.json that could be read: an unreadable one's default root would switch the whole tick (and
                // allocate's reap) to another workspace and back, dropping the aggregator's latches each way (M.ORCH-T513).
                if (OrchestratorOptions.TryLoad(store, out var latest) && latest.WorkspaceRoot != options.WorkspaceRoot)
                {
                    options = latest;
                    Build();
                    ledger.WorkspaceRoot = options.WorkspaceRoot;
                    Say(LogEventLevel.Information, $"workspace is now {options.WorkspaceRoot}");
                }
                var snapshot = aggregator.Refresh();
                // Built here (this daemon holds the status lease): its figures go to every dashboard now (M.ORCH-T872), which reads
                // the published file only when the rest of it moved.
                if (!aggregator.LastWasShared) status?.PublishFigures(snapshot);
                tick.Lap("refresh");
                ReportStatusFile(statusFile);
                // Only from settings that could be read: defaults would run allocate, keep-going and the subagent cap on
                // nothing anyone set and rewrite the hooks with them, every tick a bad row stays (M.ORCH-T720).
                if (!store.TryLoadSettings(out var s)) throw new SettingsUnreadableException(store.Describe(JsonStore.SettingsName));
                if (settingsUnreadable)
                {
                    settingsUnreadable = false;
                    Say(LogEventLevel.Information, "settings read again; the tick's work runs again");
                }
                var plan = Scheduler.Plan(snapshot, s, store.LoadAssignments());
                notifyUrl = s.NotifyUrl;

                EnsureHooks(s);
                WatchHealth(allocate);
                WatchDisk(s);
                tick.Lap("hooks, machine health and disk space");
                try { if (ledgerLease.TryAcquire()) ledger.Tick(DateTime.UtcNow); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Say(LogEventLevel.Warning, $"pace ledger: couldn't update it (retrying next minute): {ex.Message}"); }
                tick.Lap("pace ledger");
                foreach (var r in ReapIfDue())
                    Say(LogEventLevel.Warning, $"reaped pid {r.Process.Pid} ({r.Process.Name}): {r.Reason}" +
                        (string.IsNullOrWhiteSpace(r.Process.CommandLine) ? "" : $" — was running: {r.Process.CommandLine}"));
                tick.Lap("process reaper");
                PruneSessionTempIfDue(cycler);
                tick.Lap("ended sessions' temp folders");
                // Old side-by-side TermHost versions, once the CLIs still running on them have ended (M.ORCH-T932).
                if (DateTime.UtcNow - termHostsCleanedUtc >= TimeSpan.FromMinutes(30))
                {
                    termHostsCleanedUtc = DateTime.UtcNow;
                    foreach (var name in Term.TermHostInstalls.Cleanup(Term.TermHostInstalls.Root(new Setup.Installer().InstallDir)))
                        Say(LogEventLevel.Information, $"removed the unused TermHost {name}");
                    foreach (var name in Term.GatewayInstalls.Cleanup(Term.GatewayInstalls.Root(new Setup.Installer().InstallDir)))
                        Say(LogEventLevel.Information, $"removed the unused Gateway {name}");
                    foreach (var name in Term.TermViewerInstalls.Cleanup(Term.TermViewerInstalls.Root(new Setup.Installer().InstallDir)))
                        Say(LogEventLevel.Information, $"removed the unused TermViewer {name}");
                }
                // The team gateway (M.ORCH-T933), off unless turned on: started, restarted or stopped to match the settings. Its
                // trouble is contained here: it only ever costs remote viewers, never a tick or a TermHost.
                try { gateway.Tick(s.Gateway, DateTime.UtcNow); }
                catch (Exception ex) { Say(LogEventLevel.Warning, $"team gateway: {ex.Message}"); }
                tick.Lap("team gateway");

                cycler.PruneMarks();
                var roster = CliRoster.Build(cycler, aggregator.BillingSwitch, [.. snapshot.Repos.SelectMany(r => r.Hosts)], DateTime.UtcNow);
                try { CliRoster.Write(store.Db, roster, DateTime.UtcNow, cycler.Directory); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Say(LogEventLevel.Warning, $"CLI roster: couldn't write it: {ex.Message}"); }
                // Not only at start: the logon daemon runs for weeks (M.ORCH-T330).
                if (DateTime.UtcNow - prunedUtc >= TimeSpan.FromDays(1))
                {
                    cycler.Prune(DateTime.UtcNow);
                    prunedUtc = DateTime.UtcNow;
                }
                foreach (var line in cycler.KeepGoingSince())
                {
                    Say(LogEventLevel.Information, line);
                    if (line.StartsWith("keep-going: let the ", StringComparison.Ordinal)) Alert("Needs you", line["keep-going: ".Length..]);
                }
                // A CLI that stopped for a window is told to carry on (or you're told it's waiting) once that window resets (M.ORCH-T256).
                if (wakeLease.TryAcquire())
                {
                    // A moved CLI that landed on its new login gets its tickets back to start again in their worktrees; one that
                    // didn't land in time is unlocked with its old owner keeping them (M.ORCH-T637).
                    ReportMoves(snapshot.Agents, DateTime.UtcNow);
                    // Not while the global pause is on (M.ORCH-T808): nothing is told to carry on until it's lifted.
                    // Paused, a nudge left before it would tell a CLI to carry on: they're all dropped (M.ORCH-T864).
                    if (Scheduling.GlobalPause.Active(store)) Sessions.CliNudges.Clear(new Sessions.ContextCycler().Directory);
                    if (!Scheduling.GlobalPause.Active(store))
                        ReportWakes(waker.Tick(snapshot.Pace, s.ResumeAfterReset, DateTime.UtcNow));
                    // An idle /allocate CLI let stop when its next handed out nothing is sent back once next has work for it
                    // (M.ORCH-T523): keep-going acts only as a turn ends, so nothing asked it again when room opened.
                    ReportAllocateWakes(pilotWaker.Tick(plan, s, DateTime.UtcNow, snapshot));
                    // On course to end under target with room for more: a CLI holding claims no subagent is on is told to start
                    // them now (M.ORCH-T529: two sat unstarted at a projected 65% of 98% until the user asked).
                    ReportAllocateWakes(pilotWaker.TickClaims(snapshot, plan, s, DateTime.UtcNow), alert: false);
                    // A dispatch whose CLI or tab exited mid-task (closed, crashed, slept) is orphaned: said at once, and handed back to
                    // the repo's /allocate CLI or started again in a new tab in its worktree (M.ORCH-T748), as a window's stop is resumed.
                    try
                    {
                        orphans ??= new OrphanWatch(store)
                        {
                            OpenTab = (item, prompt) => new Dispatcher(new SessionControl(options), store).Dispatch(item, Providers.Claude, useWorktree: true, submit: true, prompt: prompt),
                        };
                        ReportOrphans(orphans.Tick(plan, snapshot, s, cycler.AllocateClis(), DateTime.UtcNow));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
                    {
                        Say(LogEventLevel.Warning, $"orphaned: {ex.Message}");
                    }
                    // The billing switch (M.ORCH-T436): a /login to another claude.ai account is noted, not switched back (M.ORCH-T676), and each
                    // open CLI billing the other mode is asked to restart (one daemon asks, as one wakes); one only you can
                    // restart is said to you.
                    try
                    {
                        foreach (var line in aggregator.BillingSwitch.Tick([.. snapshot.Repos.SelectMany(r => r.Hosts)], DateTime.UtcNow))
                        {
                            var yours = Billing.BillingSwitch.NeedsYou(line);
                            Say(yours ? LogEventLevel.Warning : LogEventLevel.Information, $"billing: {line}");
                            if (yours) Alert("Needs you", line);
                        }
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException)
                    {
                        Say(LogEventLevel.Warning, $"billing: {ex.Message}");
                    }
                    // Rebalance when an account runs low (M.ORCH-T608): a CLI on a login past its 5-hour pace target or weekly stop
                    // line moves to a free login with more headroom at its next idle point.
                    try
                    {
                        // With no login to move to, it fails over to the API key until its login resets (M.ORCH-T664).
                        rebalancer ??= new AccountRebalancer(AccountMove.Default(billing: aggregator.BillingSwitch, store: store)) { Store = store };
                        ReportRebalance(rebalancer.Tick(snapshot.Pace, snapshot.Accounts, [.. snapshot.Repos.SelectMany(r => r.Hosts)], s, DateTime.UtcNow,
                            [.. snapshot.Repos.SelectMany(r => r.Sessions)]));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException)
                    {
                        Say(LogEventLevel.Warning, $"rebalance: {ex.Message}");
                    }
                    // The last-resort cool-off (M.ORCH-T786): a login whose trend stays past its pace line with new subagents already
                    // slowed has just enough of its ticket subagents aborted to bend it back under, their tickets first in the queue.
                    try
                    {
                        aborter ??= new AgentAborter(store, cycler.Directory, options.ClaudeProjectsDir);
                        foreach (var a in aborter.Tick(snapshot, s, DateTime.UtcNow))
                            Say(LogEventLevel.Warning, $"agent-abort [{a.Login ?? "global login"}]: aborted subagent {a.AgentId} on {a.TicketId} " +
                                $"(burning ~{a.PercentPerHour:0.#}% of the 5-hour window an hour): the window is on course for {a.ProjectedPercent:0}%, past its " +
                                $"{a.TargetPercent:0}% pace line even with new subagents slowed. It stops at its next tool call, its worktree kept as it is; " +
                                $"{a.TicketId}'s claim is released and it's first in the queue for the next round.");
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidDataException)
                    {
                        Say(LogEventLevel.Warning, $"agent-abort: {ex.Message}");
                    }
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
                        store.Save(AllocateHoldsName, new AllocateHolds(
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
                    Say(LogEventLevel.Warning, $"a session in {x.Repo} looks stuck, repeating {x.Loop.Describe()} since {Formatting.ResetTime(x.Loop.SinceUtc, DateTime.UtcNow)}");
                    Alert("Looks stuck", $"A session in {x.Repo} keeps repeating {x.Loop.Describe()}.", urgent: true);
                }
                knownStuck.IntersectWith(stuck.Select(x => x.SessionId + x.Loop.Tool + x.Loop.Input));

                // Beside the watchdog: every reminder due, missed ones too (the machine slept), is texted through Psst (M.ORCH-T477).
                try { if (remindLease.TryAcquire()) reminders.Tick(DateTime.UtcNow); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Say(LogEventLevel.Warning, $"reminders: {ex.Message}"); }

                // An hour and half an hour before the reset: a window on course to end under its target with nothing for any
                // CLI to work is said, never acted on (M.ORCH-T176).
                ReportUnderTarget(snapshot, plan, cycler.AllocateClis());
                tick.Lap("keep-going, alerts and reminders");

                var gitChores = gitChoresLease.TryAcquire();
                if (gitChores) RunAuditIfDue(options, s);
                tick.Lap("audit");
                if (gitChores)
                {
                    SyncTicketsDbIfDue(options);
                    // The database everything writes (M.ORCH-T535), committed in the checkout that holds it.
                    if (Settings.OrchestratorDb.LivePath is var live && Settings.OrchestratorDb.RepoOf(live) is { } dbRepo)
                        SyncOrchestratorDbIfDue(dbRepo, live, DateTime.UtcNow);
                    MirrorIfDue(DateTime.UtcNow);
                }
                tick.Lap("tickets db sync");
                if (gitChores && s.GitHubIntake && DateTime.UtcNow - intakeUtc >= TimeSpan.FromHours(1))
                {
                    intakeUtc = DateTime.UtcNow;
                    // One repo's malformed story file or a locked database ends the intake, not the tick: allocate's reap
                    // and sweep below still run.
                    try
                    {
                        // The engine's intake (M.TIX-T134); the daemon is its one scheduled runner (M.ORCH-T762).
                        foreach (var (repo, ticket) in Tickets.GitHubIntake.Run(options.WorkspaceRoot, gh: args => Tickets.GitHubIntake.Gh(Runner, Tickets.GitHubIntake.GhTimeout, args)))
                            Say(LogEventLevel.Information, $"filed {ticket.Id} in {repo} from GitHub: {ticket.Summary}");
                    }
                    catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or System.Data.Common.DbException)
                    {
                        Say(LogEventLevel.Warning, $"GitHub intake stopped: {ex.Message}", ex);
                    }
                }
                tick.Lap("GitHub intake");

                ReportAllocate(allocate.Tick(plan, snapshot, s, DateTime.UtcNow));
                tick.Lap("allocate");
                // Who works each ticket, in the ticket database for Tickets Desktop's link back (M.ORCH-T766): kept in step with the
                // assignments as they stand after this tick's moves, redispatches and reaping (a claim gone stale is cleared). One
                // daemon writes it, the one that wakes the CLIs.
                if (wakeLease.TryAcquire()) SyncWorking(options.WorkspaceRoot, plan);
                tick.Lap("who works the tickets");
                RunMainlineTests(mainTests, testsLease, snapshot, s, options.WorkspaceRoot);
                tick.Lap("main-line tests");
                // Keyed without its numbers: a hold whose percent ticks 93 → 94 is the same hold, not a new line (found live: 46 lines in 2h).
                var pausedKey = allocate.Paused is null ? null : Digits().Replace(allocate.Paused, "#");
                if (allocate.On && pausedKey != lastPaused)
                {
                    Say(LogEventLevel.Information, allocate.Paused is { } why ? $"allocate is holding: {why}" : "allocate is starting stories again");
                    lastPaused = pausedKey;
                }
                if (allocate.On && allocate.HeldElsewhere != lastHeldElsewhere)
                {
                    Say(LogEventLevel.Information, allocate.HeldElsewhere
                        ? "another orchestrator (an open dashboard) is running allocate and keeping the budget file fresh; this does neither until it's free"
                        : "running allocate from this process");
                    lastHeldElsewhere = allocate.HeldElsewhere;
                }
            }
            catch (SettingsUnreadableException ex)
            {
                // Said once, not every minute it stays unreadable.
                if (!settingsUnreadable) Say(LogEventLevel.Warning, ex.Message);
                settingsUnreadable = true;
            }
            catch (Exception ex)
            {
                Say(LogEventLevel.Error, $"refresh failed: {ex.Message}", ex);
            }
            if (tick.Slow() is { } slow) Say(LogEventLevel.Warning, slow);
            WaitForNextRefresh(interval, cancel);
        }
        ClaudeMeterProbe.ReadingLanded -= OnReading;
        Say(LogEventLevel.Information, "daemon stopped");
    }

    /// <summary>Whether <see cref="Run"/> serves the status stream (M.ORCH-T854); a test's daemon doesn't.</summary>
    internal bool ServeStatus { get; init; } = true;

    /// <summary>Opens a standalone TermViewer for each hosted CLI while the dashboard is down (M.ORCH-T960); off in tests.</summary>
    internal bool ShowFallbackViewers { get; init; } = true;

    /// <summary>Held by the one daemon that runs the fallback viewers: a second daemon (<c>orchestrator daemon</c> beside the logon
    /// one) waits for it, and takes over if it ends.</summary>
    internal const string FallbackViewersMutex = @"Local\MindAttic.Orchestrator.FallbackViewers";

    private void StartFallbackViewers(CancellationToken cancel)
    {
        try
        {
            new Thread(() =>
            {
                try
                {
                    using var role = new Mutex(false, FallbackViewersMutex);
                    var owned = false;
                    while (!owned && !cancel.IsCancellationRequested)
                    {
                        try { owned = role.WaitOne(TimeSpan.FromSeconds(5)); }
                        catch (AbandonedMutexException) { owned = true; } // the daemon that had it died: ours now
                    }
                    if (!owned) return;
                    try { new FallbackViewers { Say = (level, line) => Say(level, line) }.Run(cancel); }
                    finally { try { role.ReleaseMutex(); } catch (ApplicationException) { } }
                }
                catch (Exception ex) { log.Error(ex, "The fallback viewers stopped; contained, the daemon carries on"); }
            })
            {
                IsBackground = true, Name = "FallbackViewers",
            }.Start();
        }
        catch (Exception ex) { log.Error(ex, "Starting the fallback viewers failed; contained"); }
    }

    private IReadOnlyList<AccountDir>? watchedDirs;

    /// <summary>The usage files the status stream watches: the global login's (the statusline's file; its reading is the later of
    /// that and the probe's row) and each per-CLI login's, with whether it's signed in.</summary>
    private IReadOnlyList<Status.StatusStreamServer.WatchedUsage> WatchedUsage()
    {
        try { watchedDirs = new AccountDirs().All(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { } // the last list read
        return [new(null, ReportedUsage.DefaultPath, null), .. (watchedDirs ?? []).Select(d => new Status.StatusStreamServer.WatchedUsage(d.Email, d.UsagePath, d.SignedIn))];
    }

    /// <summary>
    /// Starts each repo's full suite on its main line once something landed there, and reports the runs that finished, failing
    /// tests filed as fix tickets (<see cref="MainlineTests"/>). Runs go on their own threads: the tick only reads refs. Finished
    /// runs are reported whoever holds the lease; only its holder starts new ones.
    /// </summary>
    private void RunMainlineTests(MainlineTests tests, DispatchLease lease, Snapshot snapshot, OrchestratorSettings s, string workspaceRoot)
    {
        try
        {
            var repos = snapshot.Repos.Where(r => r.Path.Length > 0 && !r.Name.StartsWith('('))
                .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase).Select(g => (g.Key, g.First().Path));
            foreach (var line in tests.Tick(repos, s.WorktreeRoot, () => MindAttic.Tickets.Data.TicketsDb.OpenIfMigrated(workspaceRoot), DateTime.UtcNow, lease.TryAcquire()))
                Say(line.Warn ? LogEventLevel.Warning : LogEventLevel.Information, line.Text);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Data.Common.DbException)
        {
            Say(LogEventLevel.Warning, $"main tests: {ex.Message}");
        }
    }

    /// <summary>Logs each CLI nudged after its window reset and raises the Needs-you alert: nothing is typed into a CLI's box, so an
    /// idle one only takes its nudge at its next prompt (M.ORCH-T864).</summary>
    internal void ReportWakes(IEnumerable<LimitWaker.Wake> wakes)
    {
        foreach (var w in wakes)
        {
            Say(LogEventLevel.Warning, w.Line);
            Alert("Needs you", w.Line);
        }
    }

    private AccountRebalancer? rebalancer;

    /// <summary>The last-resort cool-off's state (M.ORCH-T786): when each login's cool-off came on.</summary>
    private AgentAborter? aborter;

    private string? lastWorkingFailure;

    /// <summary>Records who works each ticket in the ticket database (<see cref="Tickets.TicketWorking.Sync(string?, SchedulePlan, IReadOnlyList{Assignment}?)"/>,
    /// M.ORCH-T766). A write that fails is logged (once while it keeps failing the same way) and the tick carries on.</summary>
    private void SyncWorking(string workspaceRoot, SchedulePlan plan)
    {
        IReadOnlyList<Assignment> now;
        try { now = store.LoadAssignments(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return; }
        var (changed, failed) = Tickets.TicketWorking.Sync(workspaceRoot, plan, now);
        if (changed > 0) Say(LogEventLevel.Debug, $"who works the tickets: {changed} recorded or cleared in the ticket database");
        var failure = failed.Count == 0 ? null : string.Join("; ", failed);
        if (failure is not null && failure != lastWorkingFailure) Say(LogEventLevel.Warning, $"who works the tickets: {failure}");
        lastWorkingFailure = failure;
    }

    private OrphanWatch? orphans;

    /// <summary>Logs what <see cref="OrphanWatch"/> found and did, and raises each alert it carries (M.ORCH-T748).</summary>
    internal void ReportOrphans(IEnumerable<OrphanWatch.Event> events)
    {
        foreach (var e in events)
        {
            Say(e.Warn ? LogEventLevel.Warning : LogEventLevel.Information, e.Line);
            if (e.Alert is { } a) Alert(a.Title, a.Text, a.Urgent);
        }
    }

    /// <summary>Ends the moves in flight that landed, failed or timed out, and hands each one's tickets back (M.ORCH-T637):
    /// logged, and toasted when it didn't land or its CLI has tickets to start again (told at its next prompt, never typed).</summary>
    internal void ReportMoves(IReadOnlyList<Activity.AgentBoardRow> agents, DateTime nowUtc)
    {
        try
        {
            var handback = new MoveHandback(store);
            foreach (var end in new MoveLocks(store).Reconcile(agents, nowUtc))
            {
                var result = handback.Deliver(end);
                var warn = !end.Landed || result.Tickets.Count > 0;
                Say(warn ? LogEventLevel.Warning : LogEventLevel.Information, result.Line);
                if (!end.Landed) Alert("Move didn't land", result.Line["move: ".Length..]);
                else if (warn) Alert("Needs you", result.Line["move: ".Length..]);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Say(LogEventLevel.Warning, $"move: {ex.Message}");
        }
    }

    /// <summary>Logs each CLI the rebalancer moved and toasts it; one only asked of its tab (not yet seen to have moved) is only
    /// logged; one it couldn't move is logged as a warning, and toasted as Needs you when only you can fix it (M.ORCH-T681).</summary>
    internal void ReportRebalance(IEnumerable<Rebalanced> moves)
    {
        foreach (var m in moves)
        {
            Say(m.Moved || m.Asked ? LogEventLevel.Information : LogEventLevel.Warning, m.Line);
            var text = m.Line.StartsWith("rebalance: ", StringComparison.Ordinal) ? m.Line["rebalance: ".Length..] : m.Line;
            if (m.NeedsYou) Alert("Needs you", text);
            if (m.Title is { } title)
            {
                Alert(title, text);
                AccountRebalancer.Record(store, text, DateTime.UtcNow);
            }
            // A login at its own weekly or 5-hour cutoff: cut off, and the owner asked what to do (M.ORCH-T742).
            if (m.Cutoff is { } cutoff) ReportCutoff(cutoff);
            if (!m.Moved) continue;
            Alert("Moved a CLI", text);
            AccountRebalancer.Record(store, text, DateTime.UtcNow);
        }
    }

    private Reminders.PsstAlerts? psstAlerts;

    // Whether the last tick skipped its settings-driven work because the settings couldn't be read (M.ORCH-T720).
    private bool settingsUnreadable;

    /// <summary>The settings couldn't be read this tick: its settings-driven work is skipped rather than run on defaults.</summary>
    private sealed class SettingsUnreadableException(string where)
        : Exception($"settings in {where} couldn't be read; skipping this tick's settings-driven work (allocate, keep-going, hooks) rather than running it on defaults");

    /// <summary>A login reached its cutoff (M.ORCH-T742): an urgent alert, and a Psst text to the owner on its own thread.</summary>
    internal Task? ReportCutoff(CutoffCrossing c)
    {
        Alert("Cutoff reached", c.Text, urgent: true);
        if (!store.TryLoadSettings(out var s) || s.PsstAlertTo is not { Length: > 0 } to || string.IsNullOrWhiteSpace(to)) return null;
        psstAlerts ??= new Reminders.PsstAlerts(new Reminders.PsstClient(Runner), (level, line) => Say(level, line));
        return psstAlerts.Send($"cutoff|{c.Email}|{c.Weekly}", to, $"Orchestrator: {c.Text}", $"{c.Name} reached its {(c.Weekly ? "weekly" : "5-hour")} cutoff");
    }

    private readonly UnderTarget underTarget = new();

    /// <summary>Logs each /allocate CLI nudged (through its hook, never typed into its box, M.ORCH-T864) and raises the Needs-you alert, since an idle one only takes it at its next prompt.</summary>
    internal void ReportAllocateWakes(IEnumerable<AllocateWaker.Wake> wakes, bool alert = true)
    {
        // An idle CLI only takes its nudge when you next talk to it, so that's said to you; a busy one with claims to start takes it
        // at its next tool call or stop, and needs nothing from you.
        foreach (var w in wakes)
        {
            Say(alert ? LogEventLevel.Warning : LogEventLevel.Information, w.Line);
            if (alert) Alert("Needs you", w.Line);
        }
    }

    /// <summary>Logs and raises the Needs-you alert when <see cref="UnderTarget"/> says one is due.</summary>
    internal void ReportUnderTarget(Snapshot snapshot, SchedulePlan plan, IReadOnlyDictionary<int, IReadOnlyList<string>> allocateClis)
    {
        // Every tick, once a window: on course to end under target with nothing left for any /allocate CLI to start (M.ORCH-T529).
        if (underTarget.CheckNoWork(snapshot, plan, allocateClis) is { } none)
        {
            Say(LogEventLevel.Warning, $"under target: {none}");
            Alert("Needs you", none);
        }
        if (underTarget.Check(snapshot, plan, allocateClis, snapshot.TakenUtc) is not { } line) return;
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

    /// <summary>Runs <c>tickets db sync</c> and <c>gh</c>; a test's stands in for the real one.</summary>
    internal IProcessRunner Runner { get; init; } = ProcessRunner.Default;

    /// <summary>How long <c>tickets db sync</c> may run before it's killed, so a hung push can't stall the loop.</summary>
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

    /// <summary>Logs allocate's lines; only its "needs you" lines (<see cref="Allocate.NeedsYou"/>) are warnings and raise the
    /// Needs you alert, not a started line whose ticket summary happens to say "needs you".</summary>
    internal void ReportAllocate(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            var needsYou = Allocate.NeedsYou(line);
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

    private readonly RefreshWaiter waiter = new([RefreshSignal, ReportedUsage.DefaultPath, new ClaudeAccount().ConfigPath, new ClaudeAccount().StatePath], MinGap);

    /// <summary>
    /// Waits out <paramref name="interval"/>, but refreshes early when a turn ends anywhere (the context gate's
    /// <see cref="RefreshSignal"/>) or Claude's own usage report is rewritten, so what you type and every reply,
    /// tool calls or not, is in the pace within a couple of seconds rather than at the next tick; and when Claude Code's config
    /// or the sign-in record changes, so a /login is seen (and the new account's meter read) at once (M.ORCH-T486).
    /// </summary>
    private void WaitForNextRefresh(TimeSpan interval, CancellationToken cancel) => waiter.Wait(interval, cancel);

    /// <summary>The scheduled audit: when the last one is older than <see cref="OrchestratorSettings.AuditEveryHours"/>, run it and report what's new.</summary>
    private DateTime auditTriedUtc;

    /// <summary>The stale claims' ticket IDs, kept in the store for the context gate (see <see cref="Setup.AgentGate"/>'s workable()).</summary>
    public const string StaleTicketsName = "stale-tickets";

    /// <summary>What else orchestrator next holds back, for the context gate's keep-going (rewritten with the stale list).</summary>
    public const string AllocateHoldsName = "allocate-holds";

    public sealed record AllocateHolds(bool Quiet, IReadOnlyList<Scheduling.WorkedTicket> Worked, IReadOnlyList<string> LivePilot);
    private IReadOnlyList<string>? publishedStale;
    private DateTime publishedStaleUtc;

    /// <summary>The lease of the git chores: only the daemon holding it audits, syncs the databases, mirrors and files GitHub intake.</summary>
    public const string GitChoresLeaseName = "git-chores.lock";

    private void RunAuditIfDue(OrchestratorOptions options, OrchestratorSettings s)
    {
        if (s.AuditEveryHours <= 0) return;
        // Capped at a century: a huge value typed to mean "never" overflowed and ended every tick here, before allocate's turn.
        var every = TimeSpan.FromHours(Math.Min(s.AuditEveryHours, 36_525 * 24));
        var runner = new Audit.AuditRunner(options, store);
        // A failed read isn't "no audit yet": running now would diff against nothing and hide what's new, so wait for a readable one.
        if (!runner.TryLatest(out var before)) return;
        if (before is not null && DateTime.UtcNow - before.RanUtc < every) return;
        // A run whose save failed isn't retried every tick (it scans the whole workspace), and the failure doesn't end
        // the tick before allocate's turn: it's said, and tried again when the next one is due.
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

    private DateTime? sessionTempPrunedUtc;

    /// <summary>On start and every <see cref="SessionTempPruner.Every"/> after, deletes the %TEMP%\claude folders of sessions
    /// that have ended (M.ORCH-T473): Claude Code leaves each one's scratchpad and background-command logs there for good.</summary>
    private void PruneSessionTempIfDue(ContextCycler cycler)
    {
        if (sessionTempPrunedUtc is { } at && DateTime.UtcNow - at < SessionTempPruner.Every) return;
        sessionTempPrunedUtc = DateTime.UtcNow;
        var dir = Path.Combine(TempDir, "claude");
        var (folders, bytes) = SessionTempPruner.Prune(dir, cycler.SessionRunning, DateTime.UtcNow);
        if (folders > 0) Say(LogEventLevel.Information, $"deleted {folders} ended sessions' folders under {dir} ({bytes / 1048576.0:0.#} MB)");
    }

    private Activity.HealthLevel healthLevel;
    private DateTime healthSampledUtc;

    /// <summary>
    /// Every 30 seconds, samples handles, commit charge and free RAM. A change of level is logged and alerted with the
    /// worst offenders. Nothing is held for it (see <see cref="Activity.MachineHealth"/>); it only names the hold in
    /// <see cref="Allocate.Paused"/>, for the daemon's own starting that nothing in production uses.
    /// </summary>
    private void WatchHealth(Allocate allocate)
    {
        if (DateTime.UtcNow - healthSampledUtc < TimeSpan.FromSeconds(30)) return;
        healthSampledUtc = DateTime.UtcNow;
        var sample = Activity.MachineHealth.Sample();
        // Judged against the last level, so it only comes down once clearly clear of the line, a step at a time.
        var (level, why) = Activity.MachineHealth.Judge(sample, healthLevel);
        allocate.HealthHold = level == Activity.HealthLevel.Ok ? null : why;
        if (level == healthLevel) return;
        var offenders = sample.Offenders(why);
        if (level == Activity.HealthLevel.Ok)
            Say(LogEventLevel.Information, "machine health is back to normal");
        else
        {
            Say(level == Activity.HealthLevel.Danger ? LogEventLevel.Error : LogEventLevel.Warning,
                // Said, not held: /allocate CLIs hand out tickets through orchestrator next, which a busy machine doesn't
                // stop (heavy load is the point), so the alert mustn't claim allocate is holding (M.ORCH-T357).
                $"machine health {level.ToString().ToLowerInvariant()}: {why} ({offenders})");
            // One alert per episode and per escalation: warn, then danger; not again for every step back down and up.
            if (level > alertedHealth)
                Alert(level == Activity.HealthLevel.Danger ? "Machine in danger" : "Machine running low",
                    $"{why}. {char.ToUpperInvariant(offenders[0])}{offenders[1..]}.", urgent: level == Activity.HealthLevel.Danger);
        }
        alertedHealth = level == Activity.HealthLevel.Ok ? level : (Activity.HealthLevel)Math.Max((int)alertedHealth, (int)level);
        healthLevel = level;
    }

    private Activity.HealthLevel alertedHealth;

    private readonly Activity.DiskWatch disk = new();

    /// <summary>Reads free space on the drives to watch; a test's stands in for the real drives.</summary>
    internal Func<OrchestratorSettings, IReadOnlyList<Activity.DriveFree>> ReadDrives { get; init; } =
        s => Activity.DiskWatch.Read(Activity.DiskWatch.Roots(s.WorktreeRoot));

    /// <summary>The %TEMP% whose biggest folders, and whose claude folder's biggest files, a disk alert names.</summary>
    internal string TempDir { get; init; } = Path.GetTempPath();

    /// <summary>
    /// Every tick, reads free space on the system drive and the worktree root's (a drive read is cheap). Below
    /// <see cref="OrchestratorSettings.DiskWarnGb"/> it alerts, naming what fills %TEMP%, so a runaway writer is caught while
    /// it's still writing (M.ORCH-T471); %TEMP% is only walked when there's an alert to send.
    /// </summary>
    internal void WatchDisk(OrchestratorSettings s)
    {
        if (s.DiskWarnGb <= 0) return;
        if (disk.Observe(ReadDrives(s), s.DiskWarnGb, Math.Min(s.DiskCriticalGb, s.DiskWarnGb)) is not { } e) return;
        if (e.Alert)
        {
            var text = Activity.DiskWatch.Describe(e, s.DiskWarnGb, s.DiskCriticalGb,
                Activity.DiskWatch.LargestFolders(TempDir), Activity.DiskWatch.LargestFiles(Path.Combine(TempDir, "claude")));
            Say(e.Level == Activity.HealthLevel.Danger ? LogEventLevel.Error : LogEventLevel.Warning, $"disk space low: {text}");
            Alert(e.Level == Activity.HealthLevel.Danger ? "Disk almost full" : "Disk space low", text, urgent: e.Level == Activity.HealthLevel.Danger);
        }
        else if (e.Changed)
            Say(LogEventLevel.Information, e.Level == Activity.HealthLevel.Ok
                ? $"disk space is back to normal ({e.Drive.Root} has {e.Drive.FreeGb:0.0} GB free)"
                : $"disk space is still low but recovering ({e.Drive.Root} has {e.Drive.FreeGb:0.0} GB free)");
    }

    /// <summary>The CLI that syncs the ticket database, and its name before the rename (M.ORCH-T564), tried when it isn't installed.</summary>
    internal const string TicketsCli = "tickets", LegacyTicketsCli = "workflow";

    private DateTime syncedDbWriteUtc;
    private DateTime syncTriedUtc;

    /// <summary>
    /// Every 10 minutes, when the ticket database has changed since the last sync, commits it (and its readable dump)
    /// in the MindAttic.Tickets repo and pushes, so tickets are backed up and reach other machines.
    /// </summary>
    private DateTime mirroredUtc;

    /// <summary>With SQL Server the store of record (M.ORCH-T658), copies the settings, assignments and saved state into
    /// data/orchestrator.db once a minute, where the hooks read them: every write copies what it changed, and this makes good
    /// a copy that couldn't be made then (the file held past its timeout).</summary>
    private void MirrorIfDue(DateTime nowUtc)
    {
        if (nowUtc - mirroredUtc < TimeSpan.FromMinutes(1)) return;
        mirroredUtc = nowUtc;
        try { new Settings.OrchestratorDb(Settings.OrchestratorDb.LivePath).SyncMirror(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Data.Common.DbException)
        {
            Say(LogEventLevel.Warning, $"mirroring SQL Server into orchestrator.db: {ex.Message}");
        }
    }

    private void SyncTicketsDbIfDue(OrchestratorOptions options) =>
        SyncTicketsDbIfDue(MindAttic.Tickets.Data.TicketsDb.DefaultPath(options.WorkspaceRoot), DateTime.UtcNow, options.WorkspaceRoot);

    /// <summary>The sync for the database at <paramref name="path"/>. One that runs past <see cref="SyncTimeout"/> is killed and
    /// logged, and the database isn't counted as synced, so the next try (10 minutes on) runs it again. The sync is told
    /// <paramref name="root"/>, the workspace the database was checked in: the tickets CLI's own default is D:\Projects\MindAttic (M.ORCH-T402).</summary>
    internal void SyncTicketsDbIfDue(string path, DateTime nowUtc, string? root = null)
    {
        if (!File.Exists(path) || nowUtc - syncTriedUtc < TimeSpan.FromMinutes(10)) return;
        var written = File.GetLastWriteTimeUtc(path);
        if (written == syncedDbWriteUtc) return;
        syncTriedUtc = nowUtc;
        string[] args = root is { Length: > 0 } ? ["db", "sync", "--root", root] : ["db", "sync"];
        try
        {
            ProcessResult r;
            // The tickets CLI; the legacy workflow shim where only that is installed yet (M.ORCH-T564).
            try { r = Runner.Run(TicketsCli, args, SyncTimeout); }
            catch (System.ComponentModel.Win32Exception) { r = Runner.Run(LegacyTicketsCli, args, SyncTimeout); }
            if (r.TimedOut) { Say(LogEventLevel.Warning, $"tickets db sync took over {SyncTimeout.TotalSeconds:0}s; stopped it (retrying in 10 minutes)"); return; }
            if (r.ExitCode == 0) syncedDbWriteUtc = written;
            Say(r.ExitCode == 0 ? LogEventLevel.Information : LogEventLevel.Warning, $"tickets db sync: {(r.Output + r.Error).Trim()}");
        }
        catch (System.ComponentModel.Win32Exception ex) { Say(LogEventLevel.Warning, $"couldn't run 'tickets db sync': {ex.Message}"); }
    }

    private string? orchestratorDbSynced;
    private bool orchestratorDbUnpushed;
    private bool orchestratorDbIntegrateWarned;
    private DateTime orchestratorDbTriedUtc;

    /// <summary>The commit subject of <see cref="SyncOrchestratorDbIfDue"/>: the ticket that put the database in the repo.</summary>
    internal const string OrchestratorDbSyncSubject = "Sync data/orchestrator.db: the orchestrator's settings, state and history (M.ORCH-T524)";

    /// <summary>
    /// Every 10 minutes, when <c>data/orchestrator.sql</c> (rewritten with each change to the database) differs from the commit's,
    /// commits <c>data/orchestrator.db</c> and the dump in the main checkout at <paramref name="repo"/> and pushes, as
    /// <c>tickets db sync</c> does for Tickets.db (M.ORCH-T490). Only those two paths go in the commit (other staged work is
    /// left as it was), and a commit that doesn't land unstages them. A database <see cref="Settings.OrchestratorDb.EnvVar"/> (<paramref name="dbPath"/>)
    /// moves elsewhere isn't committed.
    /// </summary>
    internal void SyncOrchestratorDbIfDue(string repo, string dbPath, DateTime nowUtc)
    {
        var db = Path.Combine(repo, Settings.OrchestratorDb.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        var sql = Path.Combine(repo, Settings.OrchestratorDb.DumpRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (nowUtc - orchestratorDbTriedUtc < TimeSpan.FromMinutes(10) || !File.Exists(db) || !File.Exists(sql)) return;
        if (!string.Equals(Path.GetFullPath(dbPath), Path.GetFullPath(db), StringComparison.OrdinalIgnoreCase)) return;
        string dump;
        try { dump = File.ReadAllText(sql); }
        catch (IOException) { return; }
        if (dump == orchestratorDbSynced) return;
        orchestratorDbTriedUtc = nowUtc;
        string[] files = [Settings.OrchestratorDb.RelativePath, Settings.OrchestratorDb.DumpRelativePath];
        ProcessResult Git(params string[] args) => Runner.Run("git", ["-C", repo, .. args], SyncTimeout);
        try
        {
            var head = Git("show", "HEAD:" + Settings.OrchestratorDb.DumpRelativePath);
            if (head.ExitCode == 0 && Same(head.Output, dump))
            {
                // Committed already; a commit whose push failed is pushed again until one lands (M.ORCH-T538).
                if (orchestratorDbUnpushed) Push(retry: true);
                else orchestratorDbSynced = dump;
                return;
            }
            try
            {
                // Staged and committed under the database's write lock, so the commit never takes a write's half-applied pages
                // (M.ORCH-T536): the commit reads the working tree, which a write between the add and the commit would tear (M.ORCH-T719).
                ProcessResult add, commit = default!;
                try
                {
                    (add, commit) = new Settings.OrchestratorDb(dbPath).HoldingWrites(() =>
                    {
                        var added = Git(["add", "--", .. files]);
                        if (added.ExitCode != 0 || added.TimedOut) return (added, default!);
                        return (added, Git(["commit", "-q", "-m", OrchestratorDbSyncSubject, "--", .. files]));
                    });
                }
                catch (System.Data.Common.DbException ex) { throw new InvalidOperationException("couldn't lock the database: " + ex.Message); }
                if (add.ExitCode != 0 || add.TimedOut) throw new InvalidOperationException("git add failed: " + (add.Output + add.Error).Trim());
                if (commit.ExitCode != 0 || commit.TimedOut) throw new InvalidOperationException("git commit failed: " + (commit.Output + commit.Error).Trim());
            }
            catch (InvalidOperationException ex)
            {
                Git(["reset", "-q", "--", .. files]);
                Say(LogEventLevel.Warning, $"orchestrator db sync: {ex.Message}");
                return;
            }
            Push(retry: false);
        }
        catch (System.ComponentModel.Win32Exception ex) { Say(LogEventLevel.Warning, $"couldn't run git for the orchestrator db sync: {ex.Message}"); }
        // Only a push that lands counts the dump as synced (M.ORCH-T538): a failed one is tried again on the next try.
        void Push(bool retry)
        {
            // Agents land through their own worktrees, so main moves under this checkout: integrate origin/main first or every
            // push is rejected as non-fast-forward (M.ORCH-T620). A merge, not a rebase --autostash: the daemon keeps the
            // dirty data/ files open and changing so the autostash itself fails (M.ORCH-T624), and incoming commits never touch
            // them, so no stash is needed (merge commits pass the commit-msg hook).
            // Not while the user is resolving a merge, rebase, cherry-pick or revert here: the merge --abort below would throw
            // away their resolutions (M.ORCH-T721). Tried again on the next try.
            if (GitOperationInProgress(repo))
            {
                orchestratorDbUnpushed = true;
                return;
            }
            var fetch = Git("fetch", "-q", "origin");
            if (fetch.ExitCode == 0 && !fetch.TimedOut)
            {
                var rebase = Git("merge", "-q", "--no-edit", "origin/main");
                if (rebase.ExitCode != 0 || rebase.TimedOut)
                {
                    Git("merge", "--abort");
                    orchestratorDbUnpushed = true;
                    if (!orchestratorDbIntegrateWarned)
                        Say(LogEventLevel.Warning, $"orchestrator db sync: couldn't merge origin/main, so data/orchestrator.db isn't pushed (retrying every 10 minutes, not warning again): {(rebase.Output + rebase.Error).Trim()}");
                    orchestratorDbIntegrateWarned = true;
                    return;
                }
                orchestratorDbIntegrateWarned = false;
            }
            var push = Git("push", "-q");
            var ok = push.ExitCode == 0 && !push.TimedOut;
            orchestratorDbUnpushed = !ok;
            if (ok) orchestratorDbSynced = dump;
            Say(ok ? LogEventLevel.Information : LogEventLevel.Warning,
                ok ? (retry ? "orchestrator db sync: pushed data/orchestrator.db" : "orchestrator db sync: committed and pushed data/orchestrator.db")
                    : $"orchestrator db sync: committed data/orchestrator.db; push failed (retrying in 10 minutes): {(push.Output + push.Error).Trim()}");
        }
        // git may hand the committed text back with CRLF (core.autocrlf).
        static bool Same(string a, string b) => a.ReplaceLineEndings("\n") == b.ReplaceLineEndings("\n");
    }

    /// <summary>True while the checkout is mid-merge, rebase, cherry-pick or revert, which a daemon merge or abort must not touch.</summary>
    internal static bool GitOperationInProgress(string repo)
    {
        var gitDir = Path.Combine(repo, ".git");
        if (File.Exists(gitDir)) // a linked worktree: ".git" is a "gitdir: <path>" file
        {
            var line = File.ReadLines(gitDir).FirstOrDefault(l => l.StartsWith("gitdir:", StringComparison.Ordinal));
            if (line is null) return false;
            gitDir = Path.GetFullPath(line["gitdir:".Length..].Trim(), repo);
        }
        if (!Directory.Exists(gitDir)) return false;
        return new[] { "MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD" }.Any(f => File.Exists(Path.Combine(gitDir, f)))
            || new[] { "rebase-merge", "rebase-apply" }.Any(d => Directory.Exists(Path.Combine(gitDir, d)));
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
