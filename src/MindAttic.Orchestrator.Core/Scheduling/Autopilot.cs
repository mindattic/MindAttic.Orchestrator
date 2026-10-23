using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// The daemon's side of autopilot. <c>/allocate</c> runs inside the CLI it's typed into, as that CLI's subagents
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
    // The daemon no longer starts headless background agents (M.ORCH-T164): /allocate runs inside the CLI it's typed into, as
    // that CLI's subagents (see Next). Tick still reaps agents started before, lands their work and sweeps finished worktrees.
    internal Func<IReadOnlyList<string>?> Order { get; init; } = () => null;
    internal Func<int, bool> Alive { get; init; } = ContextCycler.IsClaude;
    internal string LogDirectory { get; init; } = BackgroundAgent.LogDirectory;
    internal Func<string, string, string, (bool Ok, string Path, string Error)> MakeWorktree { get; init; } = Dispatcher.EnsureWorktree;
    internal Func<string, string> MainlineOf { get; init; } = Dispatcher.Mainline;

    /// <summary>Where the shared statusline reads which repos wait on the pace (<see cref="DefaultWaitingPath"/>);
    /// null writes nothing.</summary>
    public string? WaitingPath { get; init; }

    /// <summary><c>~/.claude/allocate-waiting.json</c>: the repos autopilot is on for that have no agent working because
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

    /// <summary>Whether <c>/allocate</c> is on (until <c>/allocate off</c>).</summary>
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
        // Reaping and the hourly sweep (git fetch, merge, worktree remove) are the lease holder's whatever /allocate's state:
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
        // "/allocate all" (order "*"): every repo in the workspace with an open story, CLI open or not.
        var everyRepo = order?.Any(IsAll) == true;
        var repos = order is null ? []
            : everyRepo ? snapshot.Repos.Where(r => r.Path.Length > 0 && !r.Name.StartsWith('(') && r.OpenTickets.Count > 0).ToList()
            // Only the repos /allocate was turned on for (each CLI adds its own), never every repo that has a CLI open.
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
        // Weekly-aware (M.ORCH-T666): a ticket must fit the week's room too.
        var room = settings.WeeklyAwarePacing ? plan.Pace?.FitRoomTokens : plan.Pace?.RoomTokens;
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
            // A ticket assigned to a user (M.ORCH-T668) goes only to a CLI on their login, never to a background agent.
            if (next.IsAssigned || next.IsPinned || next.BlockedBy.Count > 0 || parked.Contains(next.Id) || !next.MayWork ||
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
    /// session is tied to the ticket), lands the branch on main itself, then closes the ticket in the ticket
    /// database (the same database from any folder, worktrees included).
    /// </summary>
    internal static string PromptFor(QueueItem item, string worktree, string branch, string mainline = "main", ModelChoice.Choice? model = null) =>
        $"Work on ticket {item.Id} ({item.Ticket.Type}): {item.Ticket.Summary} " +
        $"You have your own git worktree of {item.Repo} ({worktree}, branch {branch}) so other agents can work {item.Repo} at the same time: " +
        "run every command from that folder (cd there first) and edit only files under it; work, build and test only there. " +
        // Run headless (-p): the run ends with your last turn, so a subagent left running in the background is cut off with it.
        "Run any subagents in the foreground, never in the background. " +
        // M.ORCH-T561: the full suite (often 20+ minutes that spend no tokens) is the daemon's, run on main after the landing
        // (MainlineTests), so the subagent's slot goes to the next ticket as soon as its own work is done.
        "When it's done: build, and run only the tests your change touches (the tests of the code you changed), not the whole suite: " +
        $"once you land, the orchestrator runs {item.Repo}'s full suite on {mainline} itself and files a fix ticket for any test that fails, so don't wait for a full run. " +
        "Then run /commit, but don't push (the next step does). " +
        // M.ORCH-T563: a Sonnet ticket's big or risky diff gets one Opus review of the diff alone before it lands.
        (model is { IsSonnet: true } ? ReviewStep(item.Id) : "") +
        $"Land it on {item.Repo}'s main line, {mainline}: git fetch origin, git rebase origin/{mainline} (resolve any conflicts, then rebuild and rerun those tests), git push origin HEAD:{mainline}. " +
        // Found live (M.ORCH-T178): chained after the push, a rejected push (main moved) still closed the ticket, unlanded, on a pre-rebase hash.
        $"If the push is rejected because {mainline} moved, fetch, rebase and push again. Only once the push has succeeded, as its own step, " +
        $"mark it finished: tickets ticket close {item.Id} --ref <the test, or the commit hash as it landed on {mainline}>. " +
        "If you can't finish or land it, leave the ticket open and say why.";

    /// <summary>What a Sonnet ticket's prompt says between its commit and its landing (M.ORCH-T563).</summary>
    internal static string ReviewStep(string id) =>
        $"Then, before landing, run orchestrator review {id} from the worktree: it decides whether this Sonnet ticket's diff needs its one Opus " +
        "review (a big diff, or one touching the gates, pacing or git landing) and, if so, runs it on the diff alone. Fix what the review " +
        "finds that's really wrong, rerun the tests it touches and commit that; don't run the review again. ";

    /// <summary>
    /// Agents that exited. A worktree agent is done when its ticket is closed (in the ticket database, or on origin/main
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

    /// <summary>A ticket id as a worktree folder is named after it: M.ORCH-T123, M.COMP-T4, USS-T5.</summary>
    private static readonly System.Text.RegularExpressions.Regex TicketName =
        new(@"^(?:[A-Z]\.)?[A-Z][A-Z0-9]*-T\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
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
                // Only a folder named as a ticket is (PREFIX-T<n>) is a ticket's worktree: a bughunt or hand-made one isn't a closed
                // ticket because the database doesn't know its name, and a subagent may be working in it unseen (M.ORCH-T547).
                if (!TicketName.IsMatch(id)) continue;
                // A junction to a sibling repo (M.ORCH-T652) is never a worktree to tidy, whatever it's named.
                if (new DirectoryInfo(tree).Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
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
        using (var db = MindAttic.Tickets.Data.TicketsDb.OpenIfMigrated(Path.GetDirectoryName(repoPath)))
        {
            if (db?.Ticket(a.TicketId) is { } known) closed = known.Ticket.Status is "closed" or "wont-do";
            else if (db is not null && db.Repos().Any(r => Same(r.Name, a.Repo)))
                // A ticket the database doesn't have (an old prefix, USS- before U.SPC-) isn't open work either: its worktree
                // is tidied if it's landed and clean, not reported unlanded after every start (M.ORCH-T368).
                closed = true;
            else
            {
                // No database, or one that doesn't hold this repo yet: its tickets are in its USER_STORIES.md, as next --claim
                // reads them; read as closed, an open one's worktree was removed and the ticket handed out again (M.ORCH-T551).
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

    /// <summary>A ticket handed to a CLI's subagent: where it works, what it's told, and the model it runs on (M.ORCH-T563).</summary>
    public sealed record Handout(QueueItem Item, string? Worktree, string? Prompt, ModelChoice.Choice? Model = null)
    {
        /// <summary>Not this CLI's: sent to another login, which opens a tab on it (M.ORCH-T666); null for this CLI's own.</summary>
        public LoginRoom? DelegatedTo { get; init; }

        /// <summary>Why it went there (<see cref="Redelegation.Text"/>); null for this CLI's own.</summary>
        public string? Delegated { get; init; }
    }

    /// <summary>What <c>orchestrator next --claim</c> ends with once it claimed some (M.ORCH-T529: a CLI held two claims unstarted
    /// to avoid merge conflicts while the window ran under its target): start every one now, at once, each on its model.</summary>
    public static string StartAllLine(int claimed) => claimed == 1
        ? "Start a subagent on this ticket now, with the prompt above, on the model it names (the Agent tool's model: sonnet or opus). " +
          "Claimed, it's this CLI's to work: nothing else will."
        : $"Start all {claimed} now: one subagent per ticket above, all in one message, each with its own prompt and on the model it names " +
          "(the Agent tool's model: sonnet or opus). Don't hold one back " +
          "because its files overlap another's: each works in its own worktree and rebases onto main as it lands. Claimed, they're " +
          "this CLI's to work: nothing else will.";

    /// <summary>
    /// The next tickets a CLI running <c>/allocate</c> hands to its own subagents (M.ORCH-T164): workable ones (fixes and
    /// approved, already the only ones in the queue) of <paramref name="repos"/> ("*": all) that nobody has, in queue order,
    /// as many as the pace's subagent allowance has room for (or <paramref name="count"/>). With <paramref name="claim"/>
    /// each gets its own worktree from the repo's main line and an assignment, so it isn't handed out twice; when nothing
    /// is handed out, <c>Why</c> says why. For an <paramref name="unthrottled"/> CLI (/pacing off, M.ORCH-T195/T483) the pace's
    /// subagent allowance and room don't apply, as the pace gate skips its holds for it: it gets <paramref name="count"/>
    /// or <see cref="OrchestratorSettings.MaxConcurrentSessions"/> less the tickets already being worked or claimed (as unpaced), and the daemon's next measurement holds everyone else to make up
    /// for it. Only the window's hard stop still applies. A ticket another agent is working isn't handed out (M.ORCH-T255): one in
    /// <see cref="SchedulePlan.Worked"/> (filed this turn by an agent fixing it, or named in a live session's prompt or worktree),
    /// except what the calling CLI <paramref name="self"/> itself filed, and one whose assignment went stale while the
    /// background agent on it is still running.
    /// Paced, the calling CLI (<paramref name="cli"/>) gets its fair share of the allowance, not all of it (M.ORCH-T495: one
    /// repo's /allocate claimed all 8 places at once): the other CLIs' running subagents, and a share held for every other
    /// /allocate CLI (<paramref name="pilots"/>) that has workable tickets of its own, are left to them (<see cref="SubagentShare"/>).
    /// It also warms up (M.ORCH-T558): one subagent the minute its /allocate turned on (<paramref name="onSince"/>, by CLI), one
    /// more each minute after, up to its share, so a CLI opened just after it gets to claim its own before every place is taken.
    /// A ticket assigned to a user (M.ORCH-T668) is handed only to a CLI on that user's login (<paramref name="login"/>, the calling
    /// CLI's account email; null when not known, which takes no pinned ticket), and its claim records that login.
    /// </summary>
    public static (IReadOnlyList<Handout> Handed, string? Why) Next(SchedulePlan plan, OrchestratorSettings settings, JsonStore store,
        IReadOnlyList<string> repos, int? count, bool claim, DateTime nowUtc,
        Func<string, string, string, (bool Ok, string Path, string Error)>? makeWorktree = null, Func<string, string>? mainlineOf = null, bool unthrottled = false,
        string? self = null, Func<Assignment, bool>? pilotRunning = null, Func<string, string, bool>? stillOpen = null,
        int? cli = null, IReadOnlyDictionary<int, IReadOnlyList<string>>? pilots = null, Func<int, bool>? cliRunning = null,
        IReadOnlyDictionary<int, DateTime>? onSince = null, Func<int, string?>? loginOf = null,
        IReadOnlyList<LoginRoom>? logins = null, Func<QueueItem, LoginRoom, string?>? redelegate = null, string? login = null,
        bool canFallBack = false)
    {
        var (handed, why) = NextHere(plan, settings, store, repos, count, claim, nowUtc, makeWorktree, mainlineOf, unthrottled, self,
            pilotRunning, stillOpen, cli, pilots, cliRunning, onSince, loginOf, login);
        // Weekly-aware pacing (M.ORCH-T666): what this CLI's login can't fit goes to a login that can, else the fallback login, so
        // no ticket waits on a full account. Only for a claim (a dry run hands nothing out), and never for a /pacing off CLI,
        // whose own room isn't held to.
        if (!claim || unthrottled || redelegate is null || !settings.WeeklyAwarePacing) return (handed, why);
        var delegated = Redelegate(plan, settings, store, repos, handed, nowUtc, logins ?? [], redelegate, login, canFallBack, self,
            pilotRunning ?? (a => a.Pilot is { } p && BackgroundAgent.IsRunning(p, a.PilotStartedUtc)), stillOpen ?? StillOpen, cli);
        return delegated.Count == 0 ? (handed, why) : ([.. handed, .. delegated], why);
    }

    /// <summary>
    /// The tickets of <paramref name="repos"/> this CLI could work but its login can't fit (M.ORCH-T666): estimated past what's
    /// left of its 5-hour room or its week's (<see cref="Pace.FitRoomTokens"/>), or every one while its login is stopped. A ticket assigned to a user (M.ORCH-T668) is never redelegated (<see cref="TicketPins.Redelegable"/>): it
    /// waits for its user until "Redelegate now" drops its pin. Each goes to the roomiest other signed-in login it fits (<paramref name="logins"/>), one per login per
    /// call, else, while this CLI runs on a login of its own (<paramref name="canFallBack"/>), to the fallback login, once per
    /// <see cref="Redelegation.FallbackHold"/>;
    /// <paramref name="redelegate"/> opens it there (an error, or null once it's open), and its reason is kept for the dashboard.
    /// </summary>
    private static List<Handout> Redelegate(SchedulePlan plan, OrchestratorSettings settings, JsonStore store, IReadOnlyList<string> repos,
        IReadOnlyList<Handout> handed, DateTime nowUtc, IReadOnlyList<LoginRoom> logins, Func<QueueItem, LoginRoom, string?> redelegate,
        string? login, bool canFallBack, string? self, Func<Assignment, bool> pilotRunning, Func<string, string, bool> stillOpen, int? cli)
    {
        var delegated = new List<Handout>();
        if (!settings.Autopilot || QuietHours.Active(settings.QuietHours, nowUtc.ToLocalTime()) is not null) return delegated;
        if (cli is { } moving && new Sessions.MoveLocks(store).All().Any(m => m.Pid == moving || (self is not null && m.SessionId == self))) return delegated;
        var pace = plan.Pace;
        var closedHere = plan.Stopped;
        var room = pace?.FitRoomTokens;
        bool FitsHere(QueueItem q) => !closedHere && (room is not { } left || q.Estimate is not { } e || e.Fresh <= left);
        var mine = handed.Select(h => h.Item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var elsewhere = plan.Worked.Where(w => !(w.Filed && w.AgentId is null && self is not null && w.SessionId == self))
            .Select(w => w.TicketId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidates = plan.Queue.Where(q => Named(repos, q.Repo) && !mine.Contains(q.Id) &&
            !settings.AutopilotSkip.Contains(q.Repo, StringComparer.OrdinalIgnoreCase) && q.RepoPath.Length > 0 &&
            !q.IsAssigned && !elsewhere.Contains(q.Id) && !(q.Assignment is { } a && pilotRunning(a)) &&
            q.BlockedBy.Count == 0 && q.MayWork && TicketPins.Redelegable(q) && !plan.OverRepoCap.Contains(q.Repo) && !FitsHere(q)).ToList();
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // One to the fallback login at a time: the next waits out its hold rather than piling tabs on it.
        var fallback = !canFallBack || Redelegation.FallbackHeld(store, nowUtc) ? null : LoginRoom.Fallback;
        foreach (var item in candidates)
        {
            if (Redelegation.Choose(item, logins, fallback, taken) is not { } to) continue;
            if (!stillOpen(item.RepoPath, item.Id)) continue;
            var text = Redelegation.Text(item, login, pace, to);
            if (redelegate(item, to) is not null) { taken.Add(to.Email ?? Redelegation.FallbackKey); continue; }
            taken.Add(to.Email ?? Redelegation.FallbackKey);
            Redelegation.Record(store, item.Id, text, to.IsFallback ? nowUtc : null);
            delegated.Add(new Handout(item, null, null, ModelChoice.For(item.Ticket, item.Repo, settings)) { DelegatedTo = to, Delegated = text });
        }
        return delegated;
    }

    /// <summary>What `next` says when the named repos' queue has run dry (M.ORCH-T700): nothing open, waiting, busy or held.
    /// It offers a bug hunt and leaves it to the user: "Ask but don't do unless explicitly given permission."</summary>
    internal static string Dry(IReadOnlyList<string> repos, IReadOnlySet<string>? unknownRepos = null)
    {
        // A repo whose Tickets.db couldn't be read isn't dry, its tickets are unknown (M.ORCH-T740): no bug hunt on that.
        var every = repos.Count == 0 || repos.Any(IsAll);
        var unread = unknownRepos is { Count: > 0 } ? unknownRepos.Where(r => every || Named(repos, r)).OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList() : [];
        if (unread.Count > 0)
            return $"couldn't read the tickets of {string.Join(", ", unread)} (its Tickets.db failed to open), so its queue isn't known to be empty; " +
                "nothing else is ready. Try again shortly, and don't offer a bug hunt for it";
        var named = every ? "these repos" : string.Join(", ", repos);
        return $"no open fixes or approved tickets left there: {named} ran dry. Ask the user whether to run /bughunt on {named}, " +
            "and hunt only after an explicit yes (anything else means no); each bug it finds becomes a fix ticket that /allocate then works";
    }

    private static (IReadOnlyList<Handout> Handed, string? Why) NextHere(SchedulePlan plan, OrchestratorSettings settings, JsonStore store,
        IReadOnlyList<string> repos, int? count, bool claim, DateTime nowUtc,
        Func<string, string, string, (bool Ok, string Path, string Error)>? makeWorktree, Func<string, string>? mainlineOf, bool unthrottled,
        string? self, Func<Assignment, bool>? pilotRunning, Func<string, string, bool>? stillOpen,
        int? cli, IReadOnlyDictionary<int, IReadOnlyList<string>>? pilots, Func<int, bool>? cliRunning,
        IReadOnlyDictionary<int, DateTime>? onSince, Func<int, string?>? loginOf, string? login)
    {
        cliRunning ??= ContextCycler.IsClaude;
        stillOpen ??= StillOpen;
        pilotRunning ??= a => a.Pilot is { } p && BackgroundAgent.IsRunning(p, a.PilotStartedUtc);
        makeWorktree ??= Dispatcher.EnsureWorktree;
        mainlineOf ??= Dispatcher.Mainline;
        // A CLI on its way to another login (M.ORCH-T637) claims nothing until it lands: its own tickets are handed back then.
        if (cli is { } moving && new Sessions.MoveLocks(store).All().FirstOrDefault(m => m.Pid == moving || (self is not null && m.SessionId == self)) is { } move)
            return ([], $"this CLI is moving to {move.ToName}: it claims nothing until the move lands, " +
                "then its tickets are handed back to it to start again");
        if (plan.Stopped)
            return ([], $"the {plan.Pace?.Governing ?? "5-hour"} window is at its target: nothing starts before it resets" +
                (plan.Pace?.ResetUtc is { } reset ? $" ({Formatting.ResetTime(reset, nowUtc)})" : ""));
        // The switches the settings and the Settings panel offer apply to the CLIs that run /allocate: Tick, which
        // read them, starts nothing since M.ORCH-T164.
        if (!settings.Autopilot) return ([], "autopilot is paused (orchestrator settings --autopilot true, or the dashboard's Settings, resumes it)");
        if (QuietHours.Active(settings.QuietHours, nowUtc.ToLocalTime()) is { } quiet) return ([], $"quiet hours ({quiet}): nothing starts until they end");
        bool Named(string repo) => Autopilot.Named(repos, repo);
        // Weekly-aware (M.ORCH-T666): a ticket must fit the week's room as well as the 5-hour window's; off, there's none.
        var weekRoom = unthrottled || !settings.WeeklyAwarePacing ? null : plan.Pace?.WeeklyRoomTokens;
        static double? Least(double? a, double? b) => a is { } x ? b is { } y ? Math.Min(x, y) : x : b;
        var room = unthrottled ? null : Least(plan.Pace?.RoomTokens, weekRoom);
        // As the queue judges too big: a reserved ticket against the whole room, the rest against what isn't reserved.
        double? RoomFor(QueueItem q) => q.Reserved > 0 ? room : unthrottled ? null : Least(plan.RoomForRest, weekRoom) ?? room;
        bool TooBigNow(QueueItem q) => RoomFor(q) is { } left && q.Estimate is { } e && e.Fresh > left;
        // Only what the calling CLI filed itself is free to it: the ticket it was started on is its own work (M.ORCH-T328).
        var elsewhere = plan.Worked.Where(w => !(w.Filed && w.AgentId is null && self is not null && w.SessionId == self))
            .Select(w => w.TicketId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool Busy(QueueItem q) => q.IsAssigned || elsewhere.Contains(q.Id) || (q.Assignment is { } a && pilotRunning(a));
        bool Free(QueueItem q) => !settings.AutopilotSkip.Contains(q.Repo, StringComparer.OrdinalIgnoreCase) &&
            q.RepoPath.Length > 0 && !Busy(q) && q.BlockedBy.Count == 0 &&
            q.MayWork && !TooBigNow(q); // a stale unapproved one waits for your yes (M.ORCH-T383)
        // A ticket that went dark (M.ORCH-T748): one that kept dying, or whose work can't be carried forward, is yours, never
        // handed out again by itself; one handed back is claimed with its predecessor's work carried forward.
        var orphans = OrphanWatch.Load(store).ToDictionary(r => r.TicketId, StringComparer.OrdinalIgnoreCase);
        bool GivenUp(QueueItem q) => orphans.TryGetValue(q.Id, out var r) && r.State == OrphanRecord.NeedsYou;
        // A pinned ticket (M.ORCH-T668) is only its user's: on another login it waits for them, never handed on from here.
        bool Workable(QueueItem q) => Free(q) && TicketPins.MayTake(q, login) && !GivenUp(q);
        var open = plan.Queue.Where(q => Named(q.Repo) && Workable(q)).ToList();
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
            var pinned = free.Where(q => q.BlockedBy.Count == 0 && !TooBigNow(q) && !TicketPins.MayTake(q, login)).ToList();
            if (pinned.Count > 0 && busy == 0 && blocked == 0 && tooBig == 0)
                return ([], $"nothing else is ready: {pinned.Count} assigned to other users wait for them on their own logins (" +
                    string.Join(", ", pinned.Select(q => q.Assignee).Distinct(StringComparer.OrdinalIgnoreCase)) + ")");
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
                : Dry(repos, plan.UnknownRepos));
        }
        // Without the pace's allowance the session setting caps it, less what's already being worked or claimed, as Tick
        // subtracts its running agents: with nothing subtracted, each call handed out the whole setting again (M.ORCH-T549).
        // With no CLI resolved the fair-share block below is skipped, so unstarted claims count here too (M.ORCH-T732): else
        // each next --claim handed out the full allowance again.
        var unstarted = cli is null && !unthrottled && plan.Pace?.SubagentAllowance is not null ? UnstartedClaims(plan, cliRunning, login) : 0;
        var more = count ?? (!unthrottled && plan.Pace?.SubagentAllowance is { } allowed ? allowed - plan.Pace.Subagents - unstarted
            : settings.MaxConcurrentSessions - plan.Queue.Count(Busy));
        if (more <= 0) return ([], "the pace has no room for another subagent right now; run it again in a few minutes");
        // Its fair share, not every free place (M.ORCH-T495): another /allocate CLI with workable tickets of its own keeps its share.
        if (count is null && !unthrottled && cli is { } me && plan.Pace?.SubagentAllowance is { } cap)
        {
            // A CLI's claims no subagent has shown up on yet are its subagents about to run (M.ORCH-T532): they count against its
            // own share, not against whichever CLI covers their repo. A claim whose CLI isn't known counts for nobody.
            // A live /allocate CLI (pilots holds only running ones) or this one is running; any other claimer is asked of Windows.
            // Only CLIs billing the same login share its allowance (M.ORCH-T674): another login's CLIs have their own limits.
            var myLogin = loginOf?.Invoke(me);
            bool SameLogin(int c) => loginOf is null || string.Equals(loginOf(c), myLogin, StringComparison.OrdinalIgnoreCase);
            var pending = PendingClaims(plan, c => (c == me || pilots?.ContainsKey(c) == true || cliRunning(c)) && SameLogin(c));
            var running = plan.Pace.SubagentsByCli.Keys.Union(pending.Keys)
                .ToDictionary(c => c, c => plan.Pace.SubagentsByCli.GetValueOrDefault(c) + pending.GetValueOrDefault(c));
            var wanting = (pilots ?? new Dictionary<int, IReadOnlyList<string>>())
                .Where(p => p.Key != me && SameLogin(p.Key) && plan.Queue.Any(q => Autopilot.Named(p.Value, q.Repo) && Free(q)))
                .Select(p => p.Key).ToList();
            var total = plan.Pace.Subagents + pending.Values.Sum();
            var fair = SubagentShare.Room(cap, total, me, running, wanting);
            // Warming up (M.ORCH-T558): at most one more each minute since this CLI's /allocate turned on.
            var since = onSince is not null && onSince.TryGetValue(me, out var on) ? on : (DateTime?)null;
            var warm = SubagentShare.WarmUp(since, nowUtc);
            if (fair > 0 && SubagentShare.Room(cap, total, me, running, wanting, warm) <= 0)
                return ([], $"this CLI's /allocate is warming up (on {Math.Max(0, (int)(nowUtc - since!.Value).TotalMinutes)} min: at most {warm} " +
                    $"subagents yet, {running.GetValueOrDefault(me)} running or claimed; one more each minute, so CLIs opened just after it " +
                    "get theirs too). Run it again in a minute");
            fair = SubagentShare.Room(cap, total, me, running, wanting, warm);
            // Under its share but every place taken, other CLIs' unstarted claims included: say so, not "its fair share" while it
            // runs nothing (M.ORCH-T601).
            if (fair <= 0 && cap - total <= 0 && SubagentShare.Limit(cap, me, running, wanting) > running.GetValueOrDefault(me))
            {
                var othersClaimed = pending.Where(p => p.Key != me).Sum(p => p.Value);
                return ([], $"the subagent allowance is full ({total} of {cap}: {total - pending.Values.Sum()} running" +
                    (othersClaimed > 0 ? $", {othersClaimed} claimed by other CLIs with no subagent on yet" : "") +
                    $"); this CLI, with {running.GetValueOrDefault(me)}, is under its share and gets the next place that frees. Run it again in a few minutes");
            }
            if (fair <= 0)
                return ([], $"this CLI has its fair share of the subagent allowance ({running.GetValueOrDefault(me)} " +
                    (pending.GetValueOrDefault(me) is > 0 and var mine ? $"running or claimed ({mine} claimed with no subagent on yet: start them)" : "running") +
                    $" of {cap} across {wanting.Count + 1} CLIs that want them); the rest are kept for the others. Run it again when one of yours finishes");
            more = Math.Min(more, fair);
        }

        var handed = new List<Handout>();
        // Past one it can't have (another CLI claimed it first, or its worktree failed) to the next, until `more` are handed out.
        var claimedElsewhere = 0;
        var closedSince = 0;
        // A repo you capped (settings --repo-sessions) gets no more than that at once, counting what's already being worked
        // there: worktrees keep agents apart, so only a cap you set applies (M.ORCH-T358).
        var inRepo = plan.Queue.Where(Busy).GroupBy(q => q.Repo, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        var capped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // When room is scarce (M.ORCH-T669: the 5-hour window's or the week's), it goes to the higher-priority tickets first: each
        // one handed out takes its estimate out of what's left, in queue order (priority, orchestrator bump, first; a user's own
        // assigned tickets in the same order), so a lower-priority ticket waits behind them rather than taking the room they need.
        double committed = 0;
        foreach (var item in open)
        {
            if (handed.Count >= more) break;
            if (committed > 0 && RoomFor(item) is { } roomLeft && item.Estimate is { } cost && committed + cost.Fresh > roomLeft) continue;
            if (plan.OverRepoCap.Contains(item.Repo) ||
                inRepo.GetValueOrDefault(item.Repo) >= OrchestratorSettings.ForRepo(settings.RepoMaxSessions, item.Repo, int.MaxValue))
            {
                capped.Add(item.Repo);
                continue;
            }
            var model = ModelChoice.For(item.Ticket, item.Repo, settings);
            if (!claim)
            {
                handed.Add(new Handout(item, null, null, model));
                inRepo[item.Repo] = inRepo.GetValueOrDefault(item.Repo) + 1;
                committed += item.Estimate?.Fresh ?? 0;
                continue;
            }
            // The plan's ticket list may be older than a `tickets ticket close` made a second ago: one closed since isn't
            // handed out (or given a worktree) to be worked twice (M.ORCH-T489).
            if (!stillOpen(item.RepoPath, item.Id)) { closedSince++; continue; }
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
                all.Add(new Assignment(item.Id, item.Repo, tree, Providers.Claude, nowUtc, true) { Cli = cli, CliSession = self, Model = model.Model, Account = AccountDirs.Normalize(login) });
            });
            if (taken) { claimedElsewhere++; continue; }
            var carried = orphans.TryGetValue(item.Id, out var orphan) && orphan.State == OrphanRecord.HandedBack ? OrphanWatch.CarryForward(orphan) : "";
            handed.Add(new Handout(item, tree, carried + PromptFor(item, tree, branch, mainlineOf(item.RepoPath), model), model));
            inRepo[item.Repo] = inRepo.GetValueOrDefault(item.Repo) + 1;
            committed += item.Estimate?.Fresh ?? 0;
        }
        return (handed, handed.Count > 0 ? null
            : capped.Count > 0 ? $"{string.Join(", ", capped)} {(capped.Count == 1 ? "is" : "are")} at the cap you set (settings --repo-sessions or --repo-cap); nothing else is ready"
            : claimedElsewhere > 0 ? "another CLI claimed them first; nothing else is ready"
            : closedSince > 0 ? "they were closed since the queue was read; nothing else is ready"
            : "couldn't make a worktree for any of them (see orchestrator queue)");
    }

    /// <summary>Every claim no subagent has shown up on yet, whether or not its CLI is known (a claim made where the CLI
    /// couldn't be resolved has none); one whose CLI is known counts only while that CLI runs (M.ORCH-T732).</summary>
    internal static int UnstartedClaims(SchedulePlan plan, Func<int, bool>? cliRunning, string? login)
    {
        var worked = plan.Worked.Select(w => w.TicketId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return plan.Queue.Count(q => q.IsAssigned && q.Assignment is { IsClaim: true } a && !worked.Contains(q.Id) &&
            (a.Cli is not { } c || cliRunning?.Invoke(c) != false) &&
            (login is null || a.Account is null || string.Equals(a.Account, login, StringComparison.OrdinalIgnoreCase))); // another login's claims use its own allowance
    }

    /// <summary>Each CLI's claims (by the CLI that made them, M.ORCH-T532) that no subagent is seen working yet
    /// (<see cref="SchedulePlan.Worked"/>), not gone stale: the subagents it's about to start. A claim whose CLI is no longer
    /// running (<paramref name="cliRunning"/>) starts nothing: it isn't pending (M.ORCH-T550: a closed CLI's unstarted claims held
    /// their places until StaleAfterHours), and <see cref="AutopilotWaker"/> offers it to the /allocate CLI on its repo.</summary>
    internal static IReadOnlyDictionary<int, int> PendingClaims(SchedulePlan plan, Func<int, bool>? cliRunning = null)
    {
        var worked = plan.Worked.Select(w => w.TicketId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return plan.Queue.Where(q => q.IsAssigned && q.Assignment is { IsClaim: true, Cli: { } c } && !worked.Contains(q.Id) && cliRunning?.Invoke(c) != false)
            .GroupBy(q => q.Assignment!.Cli!.Value).ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>Whether ticket <paramref name="id"/> of the repo at <paramref name="repoPath"/> is still open right now: in the
    /// ticket database once the workspace is migrated, else in the repo's <c>docs/USER_STORIES.md</c>. True when that can't
    /// be read (the plan's own read stands).</summary>
    internal static bool StillOpen(string repoPath, string id)
    {
        try
        {
            using (var db = MindAttic.Tickets.Data.TicketsDb.OpenIfMigrated(Path.GetDirectoryName(repoPath)))
                if (db?.Ticket(id) is { } known) return known.Ticket.Status == MindAttic.Tickets.Log.TicketRules.Open;
            var stories = Path.Combine(repoPath, "docs", "USER_STORIES.md");
            return !File.Exists(stories) || !ClosedIn(File.ReadAllText(stories), id);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Data.Common.DbException)
        {
            return true;
        }
    }

    private const string NeedsYouPrefix = "autopilot: needs you: ";

    /// <summary>The order <c>/allocate all</c> writes: every repo with open stories.</summary>
    public const string AllRepos = "*";

    /// <summary>Whether <paramref name="name"/> means every repo: <see cref="AllRepos"/>, or the word "all" as typed
    /// (<c>orchestrator next --repos all</c>, M.ORCH-T728), which a literal match would take for a repo called "all".</summary>
    public static bool IsAll(string name) => name.Trim() is var n && (n == AllRepos || n.Equals("all", StringComparison.OrdinalIgnoreCase));

    /// <summary>The names in <paramref name="names"/> that match no repo in <paramref name="snapshot"/> (M.ORCH-T728), so a
    /// typo is said as one rather than read as a repo that ran dry; "all" and "*" always match.</summary>
    public static IReadOnlyList<string> Unmatched(IEnumerable<string> names, Snapshot snapshot) =>
        [.. names.Where(n => !IsAll(n) && !snapshot.Repos.Any(r => Named([n], r.Name)))];

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
    /// such as <c>GTFO.*</c> for every repo whose name starts <c>GTFO.</c> (M.ORCH-T165); <c>*</c> or "all" alone is every repo.</summary>
    public static bool Named(IEnumerable<string> names, string repo) =>
        names.Any(n => IsAll(n) || System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(n.Trim(), repo, ignoreCase: true));
}
