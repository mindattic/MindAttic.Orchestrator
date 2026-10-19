using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// The daemon's side of autopilot. <c>/autopilot</c> runs inside the CLI it's typed into, as that CLI's subagents
/// (<c>orchestrator next --claim</c>); here the lease holder (<see cref="DispatchLease"/>) reaps headless background
/// agents started before that (M.ORCH-T164), giving back a ticket one left unclosed, sweeps the worktrees of landed
/// tickets, and works out why autopilot would wait (switched off, quiet hours, machine health, the pace). The
/// starting half of <see cref="Tick"/> runs only when <see cref="Order"/> names repos, which nothing in production does.
/// </summary>
public sealed class Autopilot(JsonStore store, ContextCycler sessions, DispatchLease? lease = null)
{
    internal Func<string, string, string, int?> Start { get; init; } = BackgroundAgent.Start;
    internal Func<Assignment, bool> IsRunning { get; init; } = a => a.Pilot is { } p && BackgroundAgent.IsRunning(p, a.PilotStartedUtc);
    internal Func<int, DateTime?> StartTimeOf { get; init; } = BackgroundAgent.StartTimeOf;
    // The daemon no longer starts headless background agents (M.ORCH-T164): /autopilot runs inside the CLI it's typed into, as
    // that CLI's subagents (see Next). Tick still reaps agents started before, lands their work and sweeps finished worktrees.
    internal Func<IReadOnlyList<string>?> Order { get; init; } = () => null;
    internal Func<int, bool> Alive { get; init; } = ContextCycler.IsClaude;
    internal string LogDirectory { get; init; } = BackgroundAgent.LogDirectory;
    internal Func<string, string, string, (bool Ok, string Path, string Error)> MakeWorktree { get; init; } = Dispatcher.EnsureWorktree;
    internal Func<string, string> MainlineOf { get; init; } = Dispatcher.Mainline;

    /// <summary>Where the shared statusline reads which repos wait on the pace (<see cref="DefaultWaitingPath"/>);
    /// null writes nothing.</summary>
    public string? WaitingPath { get; init; }

    /// <summary><c>~/.claude/autopilot-waiting.json</c>: the repos autopilot is on for that have no agent working because
    /// the pace can't afford one yet, so each repo's CLI can say so in its statusline (no turn, no tokens).</summary>
    public static string DefaultWaitingPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "autopilot-waiting.json");

    private readonly Dictionary<string, string> told = [];

    /// <summary>Stories whose agent ended without closing them: not retried (it would likely fail the same way) until
    /// you look, or the orchestrator restarts.</summary>
    private readonly HashSet<string> parked = new(StringComparer.Ordinal);

    /// <summary>Another orchestrator process (usually the logon daemon) is starting the agents.</summary>
    public bool HeldElsewhere { get; private set; }

    /// <summary>Why no new agent starts right now (switched off, quiet hours, the pace), if that's so.</summary>
    public string? Paused { get; private set; }

    /// <summary>Set by the daemon while the machine is short of handles or memory (<see cref="Activity.MachineHealth"/>). It holds only this
    /// class's own starting in <see cref="Tick"/>, which runs only when <see cref="Order"/> names repos; <c>orchestrator next</c>
    /// never reads it.</summary>
    public string? HealthHold { get; set; }

    /// <summary>Whether <c>/autopilot</c> is on (until <c>/autopilot off</c>).</summary>
    public bool On { get; private set; }

    /// <summary>The repos it's working.</summary>
    public IReadOnlyList<string> Repos { get; private set; } = [];

    /// <summary>Repos it would work but is leaving alone, and why (worked by hand, or set to be skipped).</summary>
    public IReadOnlyDictionary<string, string> LeftAlone { get; private set; } = new Dictionary<string, string>();

    /// <summary>The background agents working now.</summary>
    public IReadOnlyList<Assignment> Working { get; private set; } = [];

    /// <summary>Whether <paramref name="pid"/> (a Claude CLI) runs inside one of the background agents.</summary>
    public bool IsAgent(int pid) => Working.Any(a => a.Pilot is { } p && (p == pid || ProcessTree.IsAncestor(p, pid)));

    /// <summary>Reaps finished agents and starts new ones; returns what happened, and once each, why a repo waits.
    /// <paramref name="dryRun"/> only says what it would start.</summary>
    public IReadOnlyList<string> Tick(SchedulePlan plan, Snapshot snapshot, OrchestratorSettings settings, DateTime nowUtc, bool dryRun = false)
    {
        var said = new List<string>();
        void Tell(string key, string why) { if (told.GetValueOrDefault(key) != why) said.Add(why); told[key] = why; }

        var order = Order();
        On = order is not null;
        // Reaping and the hourly sweep (git fetch, merge, worktree remove) are the lease holder's whatever /autopilot's state:
        // with the order unset (CLIs run autopilot now) the dashboard used to run them too, on its UI thread, beside the daemon.
        HeldElsewhere = lease is not null && !lease.TryAcquire();
        if (!dryRun && !HeldElsewhere)
        {
            Reap(plan, snapshot, said);
            if (nowUtc - sweptUtc >= SweepEvery)
            {
                sweptUtc = nowUtc;
                Sweep(snapshot, settings, said, nowUtc);
            }
        }
        Working = [.. store.LoadAssignments().Where(a => a.Log is not null && a.Pilot is not null && IsRunning(a))];

        // Repos with an open Claude CLI (agents' own sessions aside), or the ones named.
        // "/autopilot all" (order "*"): every repo in the workspace with an open story, CLI open or not.
        var everyRepo = order?.Contains(AllRepos) == true;
        var repos = order is null ? []
            : everyRepo ? snapshot.Repos.Where(r => r.Path.Length > 0 && !r.Name.StartsWith('(') && r.OpenTickets.Count > 0).ToList()
            // Only the repos /autopilot was turned on for (each CLI adds its own), never every repo that has a CLI open.
            : snapshot.Repos.Where(r => r.Path.Length > 0 && Named(order, r.Name)).ToList();
        // Repos you've told it to skip, and ones whose own CLI you're working in by hand, get no new agents.
        var byHand = ByHand(nowUtc, snapshot, TimeSpan.FromMinutes(Math.Max(0, settings.AutopilotHandMinutes)));
        var alone = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in repos.ToList())
        {
            var why = settings.AutopilotSkip.Contains(r.Name, StringComparer.OrdinalIgnoreCase) ? "you set it to be skipped (settings --autopilot-skip)"
                : byHand.Contains(r.Name) ? "its CLI is being worked by hand" : null;
            if (why is null) continue;
            repos.Remove(r);
            alone[r.Name] = why;
            Tell(r.Name, $"autopilot: leaving {r.Name} alone: {why}");
        }
        LeftAlone = alone;
        Repos =[.. repos.Select(r => r.Name).Order(StringComparer.OrdinalIgnoreCase)];
        if (order is { Count: > 0 } && !everyRepo && order.Where(n => !snapshot.Repos.Any(r => Named([n], r.Name))).ToList() is { Count: > 0 } unknown)
            Tell("unknown", $"autopilot: no repo named {string.Join(", ", unknown)} in the workspace");

        Paused = !settings.Autopilot ? "autopilot is switched off"
            : HealthHold is { } sick ? $"machine health: {sick}"
            : QuietHours.Active(settings.QuietHours, nowUtc.ToLocalTime()) is { } quiet ? $"quiet hours ({quiet})"
            : PaceHold(plan, snapshot);
        if (repos.Count == 0 || Paused is not null || HeldElsewhere) return Done(Paused is not null && Paused == PaceHold(plan, snapshot));

        // Every agent gets its own worktree, so a repo runs as many stories at once as its session limit and the
        // pace allow, and nobody's checkout (yours included) is ever edited under them.
        var running = Working.Count;
        var room = plan.Pace?.RoomTokens;
        // How many more the pace pays for, worked out fresh every tick. An agent started in the last control
        // window isn't in the measured draw yet, so it's counted as drawing already; unpaced, the session setting decides.
        var fresh = Working.Count(a => nowUtc - a.StartedUtc < BudgetCalculator.ControlWindow);
        var more = plan.Pace is { } pace ? pace.SpareSessions - fresh : settings.MaxConcurrentSessions - running;
        var byName = repos.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);
        var inRepo = repos.ToDictionary(r => r.Name, r => Working.Count(a => Same(a.Repo, r.Name)), StringComparer.OrdinalIgnoreCase);
        var failed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var paceBound = false;
        // The whole queue in its order (manual priority, then smallest effort), not repo by repo: filling the first repo
        // alphabetically before looking at the next left a +70 story waiting behind five unprioritized ones (found live).
        foreach (var next in plan.Queue)
        {
            if (!byName.TryGetValue(next.Repo, out var repo) || failed.Contains(repo.Name)) continue;
            if (next.IsAssigned || next.BlockedBy.Count > 0 || parked.Contains(next.Id) || !next.MayWork ||
                (room is { } left && next.Estimate is { } e && e.Fresh > left)) continue;
            if (more <= 0)
            {
                Tell("cap", $"autopilot: {running} agent{(running == 1 ? "" : "s")} working; the pace has no room for another right now");
                paceBound = true;
                break;
            }
            // Worktrees keep agents apart, so a repo is only capped if you've capped it yourself.
            if (inRepo[repo.Name] >= OrchestratorSettings.ForRepo(settings.RepoMaxSessions, repo.Name, int.MaxValue))
            {
                Tell(repo.Name, $"autopilot: {repo.Name} has {inRepo[repo.Name]} agents working, the cap you set for it (settings --repo-sessions)");
                continue;
            }
            if (dryRun)
            {
                said.Add($"autopilot would start a background agent on {next.Id} in {repo.Name}: {next.Ticket.Summary}");
                running++;
                more--;
                inRepo[repo.Name]++;
                continue;
            }

            var branch = Dispatcher.BranchFor(next.Id);
            var (ok, tree, error) = MakeWorktree(repo.Path, Path.Combine(settings.WorktreeRoot, repo.Name, next.Id), branch);
            if (!ok)
            {
                Tell(repo.Name, $"autopilot: couldn't make a worktree for {next.Id}: {error}");
                failed.Add(repo.Name);
                continue;
            }
            var log = Path.Combine(LogDirectory, $"{next.Id}-{nowUtc.ToLocalTime():yyyyMMdd-HHmm}.log");
            if (Start(tree, PromptFor(next, tree, branch, MainlineOf(repo.Path)), log) is not { } pid)
            {
                Tell(repo.Name, $"autopilot: couldn't start an agent in {repo.Name}");
                failed.Add(repo.Name);
                continue;
            }
            running++;
            more--;
            inRepo[repo.Name]++;
            told.Remove(repo.Name);
            var started = new Assignment(next.Id, next.Repo, tree, Providers.Claude, nowUtc, true) { Pilot = pid, PilotStartedUtc = StartTimeOf(pid), Log = log };
            store.UpdateAssignments(all =>
            {
                all.RemoveAll(a => a.TicketId == next.Id);
                all.Add(started);
            });
            said.Add($"autopilot: started {next.Id} in {repo.Name} in the background: {next.Ticket.Summary} (log {log})");
        }
        if (!paceBound)
            foreach (var repo in repos.Where(r => inRepo[r.Name] == 0 && !failed.Contains(r.Name)))
                Tell(repo.Name, plan.Queue.Any(q => Same(q.Repo, repo.Name) && !q.IsAssigned && q.MayWork) // a stale unapproved one waits for approval (M.ORCH-T401)
                    ? $"autopilot: {repo.Name}'s open stories are blocked, parked or too big for what's left before the reset"
                    : plan.AwaitingApproval.Count(q => Same(q.Repo, repo.Name)) is > 0 and var waiting
                        ? $"autopilot: {repo.Name}'s {waiting} open stor{(waiting == 1 ? "y waits" : "ies wait")} for your approval (orchestrator approve)"
                        : $"autopilot: {repo.Name} has no open stories left");
        return Done(paceBound);

        IReadOnlyList<string> Done(bool paceBound)
        {
            Working = [.. store.LoadAssignments().Where(a => a.Log is not null && a.Pilot is not null && IsRunning(a))];
            if (!dryRun && !HeldElsewhere)
                SaveWaiting(paceBound ? [.. Repos.Where(r => !Working.Any(a => Same(a.Repo, r)))] : [], nowUtc);
            return said;
        }
    }

    /// <summary>Saves the repos waiting on the pace, and those left alone and why, for the statusline; best effort, it's only a hint.</summary>
    private void SaveWaiting(IReadOnlyList<string> repos, DateTime nowUtc)
    {
        if (WaitingPath is null) return;
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(new { repos, left_alone = LeftAlone, at = nowUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture) });
            var tmp = $"{WaitingPath}.{Environment.ProcessId}.tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, WaitingPath, overwrite: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// The story's prompt for an agent in its own worktree. It starts like <see cref="Scheduler.PromptFor"/> (so its
    /// session is tied to the ticket), lands the branch on main itself, then closes the ticket in the Workflow
    /// database (the same database from any folder, worktrees included).
    /// </summary>
    internal static string PromptFor(QueueItem item, string worktree, string branch, string mainline = "main") =>
        $"Work on ticket {item.Id} ({item.Ticket.Type}): {item.Ticket.Summary} " +
        $"You have your own git worktree of {item.Repo} ({worktree}, branch {branch}) so other agents can work {item.Repo} at the same time: " +
        "run every command from that folder (cd there first) and edit only files under it; work, build and test only there. " +
        // Run headless (-p): the run ends with your last turn, so a subagent left running in the background is cut off with it.
        "Run any subagents in the foreground, never in the background. " +
        $"When it's done: run /commit, but don't push (the next step does). Land it on {item.Repo}'s main line, {mainline}: git fetch origin, git rebase origin/{mainline} (resolve any conflicts and rerun the tests), git push origin HEAD:{mainline}. " +
        // Found live (M.ORCH-T178): chained after the push, a rejected push (main moved) still closed the ticket, unlanded, on a pre-rebase hash.
        $"If the push is rejected because {mainline} moved, fetch, rebase and push again. Only once the push has succeeded, as its own step, " +
        $"mark it finished: workflow ticket close {item.Id} --ref <the test, or the commit hash as it landed on {mainline}>. " +
        "If you can't finish or land it, leave the ticket open and say why.";

    /// <summary>
    /// Agents that exited. A worktree agent is done when its ticket is closed (in the Workflow database, or on origin/main
    /// before the migration) and its branch is on origin/main: then your checkout is fast-forwarded (git refuses if that
    /// would touch your own changes) and the worktree removed (git keeps it if anything there is uncommitted). Otherwise
    /// the ticket goes back in the queue, parked for you.
    /// </summary>
    private void Reap(SchedulePlan plan, Snapshot snapshot, List<string> said)
    {
        // A tab's assignment (no agent of ours, no worktree to tidy) whose ticket closed is simply done; kept, it was
        // listed as "done" by every queue forever.
        // A claim by a CLI's subagent (orchestrator next) is the same: its worktree is tidied by the sweep once landed.
        var finishedTabs = plan.Finished.Where(a => a.Log is null).Select(a => a.TicketId).ToHashSet(StringComparer.Ordinal);
        if (finishedTabs.Count > 0) store.UpdateAssignments(all => all.RemoveAll(a => a.Log is null && finishedTabs.Contains(a.TicketId)));

        var gone = store.LoadAssignments().Where(a => a.Log is not null && !(a.Pilot is not null && IsRunning(a))).ToList();
        if (gone.Count == 0) return;
        foreach (var a in gone)
        {
            // The repo's own checkout, from the snapshot: the queue has no item for a repo whose last open story this was.
            var repoPath = snapshot.Repos.FirstOrDefault(r => r.Path.Length > 0 && Same(r.Name, a.Repo))?.Path
                ?? plan.Queue.FirstOrDefault(q => Same(q.Repo, a.Repo))?.RepoPath ?? a.Directory;
            var (closed, note) = a.Worktree ? Land(a, repoPath) : (!plan.Queue.Any(q => q.Id == a.TicketId), "");
            // Halted by the window's stop, it didn't fail: its work stays in its worktree and it carries on after the reset.
            var halted = !closed && plan.Stopped;
            if (!closed && !halted) parked.Add(a.TicketId);
            said.Add(closed
                ? $"autopilot: finished {a.TicketId} in {a.Repo}{note}"
                : halted ? $"autopilot: {a.TicketId}'s agent in {a.Repo} stopped at the window's target; it starts again after the reset{note}"
                : $"{NeedsYouPrefix}{a.TicketId}'s agent in {a.Repo} ended without finishing it (closed and landed), so autopilot leaves it for you (it's back in the queue); see {a.Log}{note}");
        }
        // Landing runs git for a while; only what was reaped is removed, under the lock, so a Dispatch meanwhile survives.
        store.UpdateAssignments(all => all.RemoveAll(gone.Contains));
    }

    internal static readonly TimeSpan SweepEvery = TimeSpan.FromHours(1);
    private DateTime sweptUtc;

    /// <summary>
    /// Worktrees under <see cref="OrchestratorSettings.WorktreeRoot"/> that no agent is on (no assignment, no tab open there, no
    /// session written from there in <see cref="Scheduler.WorkingWithin"/>) and whose ticket is no longer
    /// open: landed, they're removed (as <see cref="Land"/> would have, had it not missed them, e.g. on a repo landing on
    /// master before that was understood); not landed, they're kept and named once, since the work is only there.
    /// </summary>
    private void Sweep(Snapshot snapshot, OrchestratorSettings settings, List<string> said, DateTime nowUtc)
    {
        var assigned = store.LoadAssignments().Select(a => a.TicketId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var open = snapshot.Repos.SelectMany(r => r.OpenTickets).Select(t => t.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var repo in snapshot.Repos.Where(r => r.Path.Length > 0 && !r.Name.StartsWith('(')))
        {
            var root = Path.Combine(settings.WorktreeRoot, repo.Name);
            string[] trees;
            try { trees = Directory.Exists(root) ? Directory.GetDirectories(root) : []; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
            foreach (var tree in trees)
            {
                var id = Path.GetFileName(tree);
                // Only a ticket's own worktree (a .git file, not a full clone that ended up here) whose ticket is finished.
                if (assigned.Contains(id) || open.Contains(id) || !File.Exists(Path.Combine(tree, ".git"))) continue;
                // Nor one a tab is open in or a session wrote from lately: a tab's or a claim's assignment is gone as soon as its
                // ticket closes, and the agent (or you, reviewing) may still be there (M.ORCH-T329).
                var here = new Assignment(id.ToUpperInvariant(), repo.Name, tree, Providers.Claude, DateTime.MinValue, true);
                if (snapshot.TakenUtc - Scheduler.LastActivityUtc(here, snapshot) < Scheduler.WorkingWithin) continue;
                var (landed, note) = Land(here with { StartedUtc = nowUtc }, repo.Path);
                if (landed) said.Add($"autopilot: tidied the finished worktree of {id} in {repo.Name}{note}");
                else
                {
                    var why = $"autopilot: {id}'s worktree in {repo.Name} isn't open work any more but isn't landed{note}";
                    if (told.GetValueOrDefault("sweep:" + tree) != why) said.Add(why); // once, not every hour
                    told["sweep:" + tree] = why;
                }
            }
        }
    }

    /// <summary>Whether a worktree agent's ticket landed closed on origin/main, after tidying up; and what to add about it.</summary>
    internal Func<Assignment, string, (bool Closed, string Note)> Land { get; init; } = LandOnMain;

    internal static (bool Closed, string Note) LandOnMain(Assignment a, string repoPath)
    {
        Dispatcher.Git(repoPath, "fetch", "-q", "origin");
        // Found live: Automata, Prose and MindAttic.Authentication land on master; checking origin/main parked their
        // finished stories, and one agent told to push HEAD:main created a stray main branch.
        var mainline = Dispatcher.Mainline(repoPath);
        bool closed;
        using (var db = MindAttic.Workflow.Data.WorkflowDb.OpenIfMigrated(Path.GetDirectoryName(repoPath)))
        {
            if (db is not null)
                // A ticket the database doesn't have (an old prefix, USS- before U.SPC-) isn't open work either: its worktree
                // is tidied if it's landed and clean, not reported unlanded after every start (M.ORCH-T368).
                closed = db.Ticket(a.TicketId) is not { } known || known.Ticket.Status is "closed" or "wont-do";
            else
            {
                var (code, stories) = Dispatcher.Git(repoPath, "show", $"origin/{mainline}:docs/USER_STORIES.md");
                closed = code == 0 && ClosedIn(stories, a.TicketId);
            }
        }
        var note = "";
        // Closed but never landed: its commits are only on its branch, so it isn't done. Judged by where the worktree
        // itself stands, not by an orch/<id> branch it may never have been on (found live: 35 worktrees whose work was all
        // on master were reported unlanded because that branch didn't exist).
        var landed = Directory.Exists(a.Directory)
            ? Dispatcher.Git(a.Directory, "merge-base", "--is-ancestor", "HEAD", $"origin/{mainline}").Code == 0
            : Dispatcher.Git(repoPath, "merge-base", "--is-ancestor", Dispatcher.BranchFor(a.TicketId), $"origin/{mainline}").Code == 0;
        if (closed && !landed)
            return (false, $"; it closed its ticket but its branch {Dispatcher.BranchFor(a.TicketId)} isn't on {mainline}; the work is in {a.Directory}");
        if (closed && Dispatcher.Git(repoPath, "rev-parse", "--abbrev-ref", "HEAD").Output.Trim() == mainline)
            note += Dispatcher.Git(repoPath, "merge", "--ff-only", "-q", $"origin/{mainline}").Code == 0
                ? "; your checkout is up to date with it" : $"; pull {mainline} to get it (your checkout has changes in the way)";
        if (closed)
            note += Dispatcher.Git(repoPath, "worktree", "remove", a.Directory).Code == 0
                ? "" : $"; its worktree {a.Directory} kept (something there is uncommitted)";
        else note += $"; its work is in {a.Directory} (branch {Dispatcher.BranchFor(a.TicketId)})";
        return (closed, note);
    }

    /// <summary>A ticket handed to a CLI's subagent: where it works and what it's told.</summary>
    public sealed record Handout(QueueItem Item, string? Worktree, string? Prompt);

    /// <summary>
    /// The next tickets a CLI running <c>/autopilot</c> hands to its own subagents (M.ORCH-T164): workable ones (fixes and
    /// approved, already the only ones in the queue) of <paramref name="repos"/> ("*": all) that nobody has, in queue order,
    /// as many as the pace's subagent allowance has room for (or <paramref name="count"/>). With <paramref name="claim"/>
    /// each gets its own worktree from the repo's main line and an assignment, so it isn't handed out twice; when nothing
    /// is handed out, <c>Why</c> says why. For an <paramref name="unthrottled"/> CLI (/pacing off, M.ORCH-T195/T483) the pace's
    /// subagent allowance, room and wind-down don't apply, as the pace gate skips its holds for it: it gets <paramref name="count"/>
    /// or <see cref="OrchestratorSettings.MaxConcurrentSessions"/>, and the daemon's next measurement holds everyone else to make up
    /// for it. Only the window's hard stop still applies. A ticket another agent is working isn't handed out (M.ORCH-T255): one in
    /// <see cref="SchedulePlan.Worked"/> (filed this turn by an agent fixing it, or named in a live session's prompt or worktree),
    /// except what the calling CLI <paramref name="self"/> itself filed, and one whose assignment went stale while the
    /// background agent on it is still running.
    /// </summary>
    public static (IReadOnlyList<Handout> Handed, string? Why) Next(SchedulePlan plan, OrchestratorSettings settings, JsonStore store,
        IReadOnlyList<string> repos, int? count, bool claim, DateTime nowUtc,
        Func<string, string, string, (bool Ok, string Path, string Error)>? makeWorktree = null, Func<string, string>? mainlineOf = null, bool unthrottled = false,
        string? self = null, Func<Assignment, bool>? pilotRunning = null)
    {
        pilotRunning ??= a => a.Pilot is { } p && BackgroundAgent.IsRunning(p, a.PilotStartedUtc);
        makeWorktree ??= Dispatcher.EnsureWorktree;
        mainlineOf ??= Dispatcher.Mainline;
        if (plan.Stopped)
            return ([], $"the {plan.Pace?.Governing ?? "5-hour"} window is at its target: nothing starts before it resets" +
                (plan.Pace?.ResetUtc is { } reset ? $" ({Formatting.ResetTime(reset, nowUtc)})" : ""));
        // Past the wind-down line nothing new starts until the reset, estimate or not (M.ORCH-T254: five tickets whose
        // estimates each fit what was left went out at 96.6% as the reset neared and the allowance opened back up).
        if (!unthrottled && plan.Pace is { WindDown: true } wound) return ([], wound.WindDownHold);
        // The switches the settings, the tray and the Settings panel offer apply to the CLIs that run /autopilot: Tick, which
        // read them, starts nothing since M.ORCH-T164.
        if (!settings.Autopilot) return ([], "autopilot is paused (orchestrator settings --autopilot true, or the tray, resumes it)");
        if (QuietHours.Active(settings.QuietHours, nowUtc.ToLocalTime()) is { } quiet) return ([], $"quiet hours ({quiet}): nothing starts until they end");
        bool Named(string repo) => Autopilot.Named(repos, repo);
        var room = unthrottled ? null : plan.Pace?.RoomTokens;
        // As the queue judges too big: a reserved ticket against the whole room, the rest against what isn't reserved.
        bool TooBigNow(QueueItem q) => (q.Reserved > 0 ? room : unthrottled ? null : plan.RoomForRest ?? room) is { } left && q.Estimate is { } e && e.Fresh > left;
        // Only what the calling CLI filed itself is free to it: the ticket it was started on is its own work (M.ORCH-T328).
        var elsewhere = plan.Worked.Where(w => !(w.Filed && w.AgentId is null && self is not null && w.SessionId == self))
            .Select(w => w.TicketId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool Busy(QueueItem q) => q.IsAssigned || elsewhere.Contains(q.Id) || (q.Assignment is { } a && pilotRunning(a));
        var open = plan.Queue.Where(q => Named(q.Repo) && !settings.AutopilotSkip.Contains(q.Repo, StringComparer.OrdinalIgnoreCase) &&
            q.RepoPath.Length > 0 && !Busy(q) && q.BlockedBy.Count == 0 &&
            q.MayWork && !TooBigNow(q)).ToList(); // a stale unapproved one waits for your yes (M.ORCH-T383)
        if (open.Count == 0)
        {
            bool Skipped(string repo) => settings.AutopilotSkip.Contains(repo, StringComparer.OrdinalIgnoreCase);
            var skipped = plan.Queue.Where(q => Named(q.Repo) && Skipped(q.Repo)).Select(q => q.Repo).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var waiting = plan.AwaitingApproval.Count(q => Named(q.Repo) && !Skipped(q.Repo));
            var busy = plan.Queue.Count(q => Named(q.Repo) && !Skipped(q.Repo) && Busy(q));
            // Open but held back for a reason of their own: not "nothing left".
            var free = plan.Queue.Where(q => Named(q.Repo) && !Skipped(q.Repo) && !Busy(q) && q.RepoPath.Length > 0 && q.MayWork).ToList(); // an unapproved one is counted as waiting (M.ORCH-T401)
            var blocked = free.Count(q => q.BlockedBy.Count > 0);
            var tooBig = free.Count(q => q.BlockedBy.Count == 0 && TooBigNow(q));
            return ([], waiting == 0 && busy == 0 && blocked == 0 && tooBig == 0 && skipped.Count > 0 ? $"{string.Join(", ", skipped)} set to be skipped (settings --autopilot-skip); nothing else is ready"
                : waiting > 0 && busy == 0 && blocked == 0 && tooBig == 0 ? $"none left that may be worked: {waiting} wait for your approval (orchestrator approve)"
                : busy > 0 && blocked == 0 && tooBig == 0 ? $"{busy} already being worked; nothing else is ready"
                : blocked > 0 || tooBig > 0 ? "nothing ready: " + string.Join(", ", new[]
                    {
                        busy > 0 ? $"{busy} already being worked" : null,
                        blocked > 0 ? $"{blocked} wait on other open tickets" : null,
                        tooBig > 0 ? $"{tooBig} estimated past what's left before the reset" : null,
                        waiting > 0 ? $"{waiting} wait for your approval" : null,
                    }.OfType<string>())
                : "no open fixes or approved tickets left there");
        }
        var more = count ?? (!unthrottled && plan.Pace?.SubagentAllowance is { } allowed ? allowed - plan.Pace.Subagents : settings.MaxConcurrentSessions);
        if (more <= 0) return ([], "the pace has no room for another subagent right now; run it again in a few minutes");

        var handed = new List<Handout>();
        // Past one it can't have (another CLI claimed it first, or its worktree failed) to the next, until `more` are handed out.
        var claimedElsewhere = 0;
        // A repo you capped (settings --repo-sessions) gets no more than that at once, counting what's already being worked
        // there: worktrees keep agents apart, so only a cap you set applies (M.ORCH-T358).
        var inRepo = plan.Queue.Where(Busy).GroupBy(q => q.Repo, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        var capped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in open)
        {
            if (handed.Count >= more) break;
            if (plan.OverRepoCap.Contains(item.Repo) ||
                inRepo.GetValueOrDefault(item.Repo) >= OrchestratorSettings.ForRepo(settings.RepoMaxSessions, item.Repo, int.MaxValue))
            {
                capped.Add(item.Repo);
                continue;
            }
            if (!claim)
            {
                handed.Add(new Handout(item, null, null));
                inRepo[item.Repo] = inRepo.GetValueOrDefault(item.Repo) + 1;
                continue;
            }
            var branch = Dispatcher.BranchFor(item.Id);
            var (ok, tree, _) = makeWorktree(item.RepoPath, Path.Combine(settings.WorktreeRoot, item.Repo, item.Id), branch);
            if (!ok) continue;
            // The plan was read before the claim: another CLI's `next --claim` may have taken it since. The check and the
            // claim are one step under the assignments lock; only the (stale) assignment the plan saw may be replaced.
            var taken = false;
            store.UpdateAssignments(all =>
            {
                if (all.Any(a => a.TicketId == item.Id && a != item.Assignment)) { taken = true; return; }
                all.RemoveAll(a => a.TicketId == item.Id);
                all.Add(new Assignment(item.Id, item.Repo, tree, Providers.Claude, nowUtc, true));
            });
            if (taken) { claimedElsewhere++; continue; }
            handed.Add(new Handout(item, tree, PromptFor(item, tree, branch, mainlineOf(item.RepoPath))));
            inRepo[item.Repo] = inRepo.GetValueOrDefault(item.Repo) + 1;
        }
        return (handed, handed.Count > 0 ? null
            : capped.Count > 0 ? $"{string.Join(", ", capped)} {(capped.Count == 1 ? "is" : "are")} at the cap you set (settings --repo-sessions or --repo-cap); nothing else is ready"
            : claimedElsewhere > 0 ? "another CLI claimed them first; nothing else is ready"
            : "couldn't make a worktree for any of them (see orchestrator queue)");
    }

    private const string NeedsYouPrefix = "autopilot: needs you: ";

    /// <summary>The order <c>/autopilot all</c> writes: every repo with open stories.</summary>
    public const string AllRepos = "*";

    /// <summary>The alert text when <paramref name="line"/> is one of <see cref="Tick"/>'s "needs you" lines; null for any other
    /// (a started line can carry "needs you" inside its ticket summary).</summary>
    public static string? NeedsYou(string line) =>
        line.StartsWith(NeedsYouPrefix, StringComparison.Ordinal) ? line[NeedsYouPrefix.Length..] : null;

    /// <summary>Whether the ticket table in <paramref name="stories"/> has <paramref name="id"/> closed (or wont-do).</summary>
    internal static bool ClosedIn(string stories, string id) =>
        stories.Split('\n').Any(l => l.TrimStart().StartsWith($"| {id} |", StringComparison.Ordinal) &&
            l.Split('|').Select(c => c.Trim()).ElementAtOrDefault(4) is "closed" or "wont-do");

    /// <summary>Why the pace can't afford another story right now; null when it can.</summary>
    internal static string? PaceHold(SchedulePlan plan, Snapshot snapshot) =>
        plan.Pace is { Mode: PaceMode.Over or PaceMode.Coast } p ? $"pace: {p.Note}"
        // Past the wind-down line no new story starts until the reset (M.ORCH-T254).
        : plan.Pace is { WindDown: true } w ? $"pace: {w.WindDownHold}"
        : snapshot.Budget is { Level: BudgetLevel.Over } b ? $"budget at {b.WorstPercent:0}%"
        : null;

    /// <summary>The repos whose own CLI (not one of the agents) is mid-turn, or ended a turn within <paramref name="window"/>
    /// (<see cref="OrchestratorSettings.AutopilotHandMinutes"/>), so a pause between your prompts doesn't hand the repo to autopilot.</summary>
    internal HashSet<string> ByHand(DateTime nowUtc, Snapshot snapshot, TimeSpan window) =>
        sessions.Load().Where(s => s.Pid is { } p && Alive(p) && !IsAgent(p) && (!s.Idle || nowUtc - s.TurnUtc < window))
            .Select(s => RepoFor(s.Cwd, snapshot)?.Name).OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>The repo a CLI runs in: its root or a folder under it (never a worktree, which lives elsewhere).</summary>
    public static RepoStatus? RepoFor(string? cwd, Snapshot snapshot)
    {
        if (string.IsNullOrEmpty(cwd)) return null;
        var dir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(cwd));
        return snapshot.Repos.Where(r => r.Path.Length > 0).FirstOrDefault(r =>
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(r.Path));
            return dir.Equals(root, StringComparison.OrdinalIgnoreCase) || dir.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase);
        });
    }

    private static bool Same(string a, string b) => a.Equals(b, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="repo"/> is one of <paramref name="names"/>: a name, or a wildcard (<c>*</c>, <c>?</c>)
    /// such as <c>GTFO.*</c> for every repo whose name starts <c>GTFO.</c> (M.ORCH-T165); <c>*</c> alone is every repo.</summary>
    public static bool Named(IEnumerable<string> names, string repo) =>
        names.Any(n => System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(n, repo, ignoreCase: true));
}
