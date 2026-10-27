using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// Tells an idle /allocate CLI there is work it can have (M.ORCH-T523), without ever typing into it (M.ORCH-T864). The context
/// gate's keep-going only acts as a turn ends: a CLI whose last <c>orchestrator next --claim</c> handed out nothing (no pace room,
/// its fair share in use, a ticket held while another session's turn that filed it ran) is let stop, and nothing asked it again
/// when the window reset, a subagent finished, or a ticket was filed or approved. Each tick, every live /allocate CLI whose newest
/// session is idle (and not waiting on a window, which <see cref="LimitWaker"/> handles, nor running a /goal) is asked
/// <see cref="Allocate.Next"/>, as its own <c>orchestrator next</c> would be (dry run: nothing is claimed); when it would hand out
/// a ticket, the nudge is left for the CLI's UserPromptSubmit hook (<see cref="CliNudges"/>), which adds it to the next turn's
/// context, and it's said to you in the dashboard and the log once per idle stretch: an idle CLI can't be woken without typing
/// into its box, which the Orchestrator never does. A CLI is nudged at most once per <see cref="Cooldown"/>.
/// </summary>
public sealed class AllocateWaker(ContextCycler cycler, JsonStore store)
{
    /// <summary>How long a CLI has been idle before it's woken: keep-going has already had its say at the stop, and a pause
    /// between your own prompts isn't interrupted.</summary>
    public static readonly TimeSpan Settle = TimeSpan.FromMinutes(2);

    /// <summary>The least time between two wakes of the same CLI.</summary>
    public static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(10);

    /// <summary>Whether the CLI process is still running (seam).</summary>
    internal Func<int, bool> IsAlive { get; init; } = ContextCycler.IsClaude;

    /// <summary>The live CLIs /allocate is on in, and their repos (seam; <see cref="ContextCycler.AllocateClis"/>).</summary>
    internal Func<IReadOnlyDictionary<int, IReadOnlyList<string>>>? Pilots { get; init; }

    /// <summary>When /allocate turned on in each of them (seam; <see cref="ContextCycler.AllocateSince"/>).</summary>
    internal Func<IReadOnlyDictionary<int, DateTime>>? PilotsSince { get; init; }

    /// <summary>The per-CLI logins, to find the account a CLI runs on (seam).</summary>
    internal AccountDirs Dirs { get; init; } = new();

    /// <summary>The session's <c>CLAUDE_CONFIG_DIR</c> when it runs on a login of its own (seam; <see cref="ContextCycler.OnOwnLogin"/>).</summary>
    internal Func<IReadOnlyDictionary<string, string>>? OwnLogins { get; init; }

    /// <summary>The orgs and the repos' orgs (seam; <see cref="OrgRouting.Load"/>).</summary>
    internal Func<OrgRouting>? Orgs { get; init; }

    /// <summary>One CLI nudged: <paramref name="Line"/> is what's said to you (log and dashboard alert); the nudge itself is left for
    /// its hook, never typed.</summary>
    public sealed record Wake(int Pid, string Where, IReadOnlyList<string> Tickets, string Line);

    private readonly Dictionary<int, DateTime> wokenUtc = [];
    private readonly Dictionary<int, DateTime> saidFor = [];

    /// <summary>CLIs nudged within <see cref="Cooldown"/>: skipped this pass, but their nudge still stands until their hook takes it.</summary>
    private IEnumerable<int> CoolingDown(DateTime nowUtc) => wokenUtc.Where(w => nowUtc - w.Value < Cooldown).Select(w => w.Key).ToList();

    /// <summary>Which idle CLIs have work now: the nudge is left for each one's hook, and said to you once per idle stretch.</summary>
    public IReadOnlyList<Wake> Tick(SchedulePlan plan, OrchestratorSettings settings, DateTime nowUtc, Snapshot? snapshot = null)
    {
        // /allocate off, or no CLI it's on in: no "work" nudge stands, so the last one isn't delivered on (bughunt 2026-10-27).
        if (!settings.KeepGoing || !settings.Allocate) { CliNudges.Retain(cycler.Directory, "work", []); return []; }
        var pilots = Pilots?.Invoke() ?? cycler.AllocateClis();
        foreach (var gone in wokenUtc.Keys.Concat(saidFor.Keys).Where(p => !pilots.ContainsKey(p)).ToList())
        {
            wokenUtc.Remove(gone);
            saidFor.Remove(gone);
        }
        if (pilots.Count == 0) { CliNudges.Retain(cycler.Directory, "work", []); return []; }
        var newest = cycler.Load().Where(c => c.Pid is not null).GroupBy(c => c.Pid!.Value)
            .ToDictionary(g => g.Key, g => g.MaxBy(c => (Written(c.SessionId), c.TurnUtc))!);
        var since = PilotsSince?.Invoke() ?? (Pilots is null ? cycler.AllocateSince() : new Dictionary<int, DateTime>()); // warming up (M.ORCH-T558): not woken for one it can't have yet
        var moving = Moving();
        var own = snapshot is null ? null : (OwnLogins?.Invoke() ?? cycler.OnOwnLogin());
        var plans = new Dictionary<string, SchedulePlan>(StringComparer.OrdinalIgnoreCase);
        // Woken only for its own org's tickets (M.ORCH-T753), as its next --claim would hand them out.
        var orgs = Orgs?.Invoke() ?? OrgRouting.Load(store, settings, snapshot?.GlobalEmail);
        // Each CLI's login, from its newest session, so only CLIs on the same login share its allowance (M.ORCH-T674).
        var sessionOf = newest.ToDictionary(n => n.Key, n => n.Value.SessionId);
        string? loginOf(int pid) => own is not null && sessionOf.TryGetValue(pid, out var s) && own.TryGetValue(s, out var d)
            ? Path.TrimEndingDirectorySeparator(Path.GetFullPath(d)) : null;

        var due = new List<(int Pid, SessionContext Session, IReadOnlyList<string> Repos, IReadOnlyList<string> Ids)>();
        foreach (var (pid, repos) in pilots)
        {
            // A CLI on its way to another login (M.ORCH-T637) is locked: it's handed its tickets back once it lands.
            if (moving.Contains(pid)) continue;
            if (!newest.TryGetValue(pid, out var c) || c is not { Idle: true, LimitStop: null, Goal: null }) continue;
            if (nowUtc - c.TurnUtc < Settle) continue;
            if (wokenUtc.TryGetValue(pid, out var last) && nowUtc - last < Cooldown) continue;
            if (!IsAlive(pid)) continue;
            // As its own `orchestrator next --claim` plans it (M.ORCH-T640): a CLI on a login of its own is held to that account's
            // pace and allowance, not the global login's, or the nudge fired every tick for a claim that was answered "no room".
            var cliPlan = plan;
            // Its login (M.ORCH-T668): a ticket assigned to a user is offered only to a CLI on theirs.
            var email = snapshot?.GlobalEmail;
            if (snapshot is not null && own is not null && own.TryGetValue(c.SessionId, out var dir) && Dirs.Of(dir) is { } login)
            {
                email = login.Email;
                if (!plans.TryGetValue(login.Email, out var forLogin))
                {
                    var account = snapshot.Accounts.FirstOrDefault(a => a.Email.Equals(login.Email, StringComparison.OrdinalIgnoreCase));
                    plans[login.Email] = forLogin = Scheduler.Plan(snapshot with { Budget = account?.Budget, Pace = account?.Pace, Utilization = account?.Utilization, Account = account?.Account },
                        settings, store.LoadAssignments());
                }
                cliPlan = forLogin;
            }
            var (handed, _) = Allocate.Next(cliPlan, settings, store, repos, null, claim: false, nowUtc,
                unthrottled: cycler.IsUnthrottled(pid), self: c.SessionId, cli: pid, pilots: pilots, onSince: since, loginOf: loginOf, login: email, orgs: orgs);
            if (handed.Count > 0) due.Add((pid, c, repos, [.. handed.Select(h => h.Item.Id)]));
        }
        CliNudges.Retain(cycler.Directory, "work", due.Select(d => d.Pid).Concat(CoolingDown(nowUtc))); // a CLI inside its cooldown keeps the nudge it was just left
        if (due.Count == 0) return [];

        var wakes = new List<Wake>();
        foreach (var d in due)
        {
            // Left for the hook on every pass (it replaces the last, so a nudge never piles up); said once per idle stretch.
            // Resume-after-reset off: only said to you, never nudged, as it was only said when typing was off.
            if (settings.ResumeAfterReset) CliNudges.Leave(cycler.Directory, d.Pid, "work", Prompt(d.Repos, d.Ids));
            wokenUtc[d.Pid] = nowUtc;
            if (saidFor.TryGetValue(d.Pid, out var turn) && turn == d.Session.TurnUtc) continue;
            saidFor[d.Pid] = d.Session.TurnUtc;
            var where = LimitWaker.Where(d.Session.Cwd);
            var tickets = string.Join(", ", d.Ids.Take(6)) + (d.Ids.Count > 6 ? "..." : "");
            wakes.Add(new Wake(d.Pid, where, d.Ids,
                $"The /allocate CLI in {where} is idle while orchestrator next has {tickets} for it. It's told at its next prompt; nothing is typed into its box, so it carries on when you next talk to it."));
        }
        return wakes;
    }

    /// <summary>How long a claimed ticket goes with no subagent on it before its CLI is told to start it (M.ORCH-T529).</summary>
    public static readonly TimeSpan ClaimIdle = TimeSpan.FromMinutes(5);

    private readonly Dictionary<int, string> saidClaims = [];

    /// <summary>
    /// Claimed tickets left unstarted while the window is on course to end under its target (M.ORCH-T529): on 2026-10-20 the
    /// pace projected ~65% of a 98% target while an /allocate CLI held M.COMP-T9 and M.ORCH-T499 claimed and unstarted to
    /// avoid merge conflicts, until the user asked. Each tick, when <see cref="UnderTarget.Projected"/> is under target and
    /// the subagent allowance has room, every ticket a CLI claimed (<c>orchestrator next --claim</c>: its worktree and
    /// assignment) with no subagent on it for <see cref="ClaimIdle"/> is put to the /allocate CLI that works its repo as a nudge
    /// its UserPromptSubmit hook adds to the next turn's context (<see cref="CliNudges"/>; never typed into its box, M.ORCH-T864):
    /// start them now, overlapping files are rebased, not a reason to wait. Busy or idle, but not one waiting on a window or
    /// running a /goal. A CLI is nudged at most once per <see cref="Cooldown"/> (shared with <see cref="Tick"/>), and each
    /// set of claims is said to you in the dashboard and the log once.
    /// </summary>
    public IReadOnlyList<Wake> TickClaims(Snapshot snapshot, SchedulePlan plan, OrchestratorSettings settings, DateTime nowUtc)
    {
        // Every early return drops the "claims" nudges that no longer stand, as the pass below does (bughunt 2026-10-27).
        if (!settings.KeepGoing || !settings.Allocate) { CliNudges.Retain(cycler.Directory, "claims", []); return []; }
        if (UnderTarget.Projected(snapshot, plan) is not { } projected || !UnderTarget.HasRoom(plan))
        { CliNudges.Retain(cycler.Directory, "claims", CoolingDown(nowUtc)); return []; }
        var pilots = Pilots?.Invoke() ?? cycler.AllocateClis();
        if (pilots.Count == 0) { CliNudges.Retain(cycler.Directory, "claims", []); return []; }
        var idle = IdleClaims(snapshot, plan, pilots, nowUtc);
        if (idle.Count == 0) { CliNudges.Retain(cycler.Directory, "claims", CoolingDown(nowUtc)); return []; }
        var newest = cycler.Load().Where(c => c.Pid is not null).GroupBy(c => c.Pid!.Value)
            .ToDictionary(g => g.Key, g => g.MaxBy(c => (Written(c.SessionId), c.TurnUtc))!);
        // No more tickets put to the CLIs than the allowance has room for.
        var room = plan.Pace!.SubagentAllowance!.Value - plan.Pace.Subagents;

        var due = new List<(int Pid, string? Cwd, IReadOnlyList<QueueItem> Items)>();
        var moving = Moving();
        foreach (var g in idle.GroupBy(c => c.Cli).OrderBy(g => g.Key))
        {
            if (room <= 0) break;
            var pid = g.Key;
            if (moving.Contains(pid)) continue; // locked while it moves (M.ORCH-T637)
            newest.TryGetValue(pid, out var c);
            if (c is not null && (c.LimitStop is not null || c.Goal is not null)) continue;
            if (wokenUtc.TryGetValue(pid, out var last) && nowUtc - last < Cooldown) continue;
            if (!IsAlive(pid)) continue;
            var items = g.Select(x => x.Item).Take(room).ToList();
            room -= items.Count;
            due.Add((pid, c?.Cwd, items));
        }
        CliNudges.Retain(cycler.Directory, "claims", due.Select(d => d.Pid).Concat(CoolingDown(nowUtc))); // a CLI inside its cooldown keeps the nudge it was just left
        if (due.Count == 0) return [];

        var wakes = new List<Wake>();
        foreach (var d in due)
        {
            var ids = d.Items.Select(i => i.Id).ToList();
            var tickets = string.Join(", ", ids);
            var them = ids.Count == 1 ? "it" : "them";
            if (settings.ResumeAfterReset) CliNudges.Leave(cycler.Directory, d.Pid, "claims", ClaimsPrompt(d.Items, projected, settings.PaceTargetPercent));
            wokenUtc[d.Pid] = nowUtc;
            if (saidClaims.TryGetValue(d.Pid, out var was) && was == tickets) continue;
            saidClaims[d.Pid] = tickets;
            var where = LimitWaker.Where(d.Cwd);
            wakes.Add(new Wake(d.Pid, where, ids,
                $"The window is on course for {projected:0}% of its {settings.PaceTargetPercent}% target while the /allocate CLI in {where} has {tickets} claimed with no subagent " +
                $"on {them} for {ClaimIdle.TotalMinutes:0}+ min. It's told to start {them} at its next prompt; nothing is typed into its box."));
        }
        return wakes;
    }

    /// <summary>
    /// The tickets claimed for a CLI's subagent (a worktree assignment no background agent holds, not gone stale) that nothing
    /// has worked for <see cref="ClaimIdle"/>, each with the /allocate CLI it's put to. A claim has a subagent on it when the
    /// plan sees a live session or subagent working it (<see cref="SchedulePlan.Worked"/>: a subagent's prompt or worktree
    /// names it), or a transcript (a session's, or a ticketed subagent's) or a Launcher tab has its working directory in the
    /// claim's worktree within <see cref="ClaimIdle"/>. The claim's CLI is the one that made it (M.ORCH-T532: with two
    /// /allocate CLIs on one repo, the repo's owner was prompted for the other's claims) while it's a live /allocate CLI;
    /// a claim whose CLI isn't known (made before claims recorded it) or has gone goes to the /allocate CLI whose repos name
    /// its repo: one naming it outright first, then one on a wildcard or every repo; a repo no /allocate CLI works is
    /// nobody's to be told.
    /// </summary>
    internal static IReadOnlyList<(int Cli, QueueItem Item)> IdleClaims(Snapshot snapshot, SchedulePlan plan,
        IReadOnlyDictionary<int, IReadOnlyList<string>> pilots, DateTime nowUtc)
    {
        var worked = plan.Worked.Select(w => w.TicketId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var idle = new List<(int, QueueItem)>();
        foreach (var q in plan.Queue)
        {
            if (q.Assignment is not { Worktree: true, Pilot: null } a || !q.IsAssigned || worked.Contains(q.Id)) continue;
            // Its CLI is moving to another login (M.ORCH-T637): handed back to it once it lands, never put to another CLI meanwhile.
            if (q.Moving is not null) continue;
            if (nowUtc - a.StartedUtc < ClaimIdle) continue;
            if (Active(a.Directory, snapshot, nowUtc)) continue;
            var claimer = a.Cli is { } made && pilots.ContainsKey(made) ? made : OwnerOf(q.Repo, pilots);
            if (claimer is { } cli) idle.Add((cli, q));
        }
        return idle;
    }

    private static bool Active(string directory, Snapshot snapshot, DateTime nowUtc)
    {
        bool Recent(DateTime at) => nowUtc - at < ClaimIdle;
        foreach (var repo in snapshot.Repos)
        {
            if (repo.Hosts.Any(h => IsUnder(h.WorkingDirectory, directory))) return true;
            foreach (var s in repo.Sessions)
            {
                if (Recent(s.LastWriteUtc) && IsUnder(s.Cwd, directory)) return true;
                if (s.TicketedSubagents.Any(t => t.Events.Count > 0 && Recent(t.Events[^1].AtUtc) && IsUnder(t.Cwd, directory))) return true;
            }
        }
        return false;
    }

    private static int? OwnerOf(string repo, IReadOnlyDictionary<int, IReadOnlyList<string>> pilots)
    {
        var named = pilots.Where(p => Allocate.Named(p.Value, repo)).OrderBy(p => p.Key).ToList();
        if (named.Count == 0) return null;
        var exact = named.Where(p => p.Value.Contains(repo, StringComparer.OrdinalIgnoreCase)).Select(p => (int?)p.Key).FirstOrDefault();
        return exact ?? named[0].Key;
    }

    private static bool IsUnder(string? path, string directory)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(directory)) return false;
        var p = Path.TrimEndingDirectorySeparator(path.Replace('/', '\\'));
        var d = Path.TrimEndingDirectorySeparator(directory.Replace('/', '\\'));
        return p.Equals(d, StringComparison.OrdinalIgnoreCase) || p.StartsWith(d + "\\", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>What a CLI holding idle claims is told, as hook context (<see cref="CliNudges"/>).</summary>
    public static string ClaimsPrompt(IReadOnlyList<QueueItem> items, double projected, int targetPercent) =>
        $"This 5-hour window is on course to end near {projected:0}%, under its {targetPercent}% target, and the pace has room for more subagents, " +
        $"but {(items.Count == 1 ? "a ticket" : $"{items.Count} tickets")} this CLI claimed {(items.Count == 1 ? "has" : "have")} had no subagent on {(items.Count == 1 ? "it" : "them")} " +
        $"for {ClaimIdle.TotalMinutes:0}+ min: " + string.Join("; ", items.Select(i =>
            $"{i.Id} (worktree {i.Assignment!.Directory}{(i.Assignment.Model is { } m ? $", model {m}" : "")})")) + ". " +
        "Start a subagent on each now, all in one message, each on the model named and with the prompt orchestrator next printed for it when it was claimed " +
        "(if that's gone: work the ticket only in that worktree, land it on main, then close it). Overlapping files are no reason to wait: " +
        "each rebases onto main as it lands. Then carry on with orchestrator next --claim.";

    /// <summary>What an idle /allocate CLI is told, as hook context (<see cref="CliNudges"/>).</summary>
    public static string Prompt(IReadOnlyList<string> repos, IReadOnlyList<string> ids) =>
        $"There's room to work again: orchestrator next has {ids.Count} workable ticket{(ids.Count == 1 ? "" : "s")} for this CLI's allocate " +
        $"({string.Join(", ", ids.Take(6))}{(ids.Count > 6 ? "..." : "")}). Run `orchestrator next --repos '{string.Join(",", repos)}' --claim` and hand each ticket it prints " +
        "to a subagent with the prompt and on the model it prints (several at once, in one message), then repeat. End your turn when it hands out nothing.";

    /// <summary>The CLIs on their way to another login (M.ORCH-T637), by process id.</summary>
    private HashSet<int> Moving() => [.. new MoveLocks(store).All().Select(m => m.Pid)];

    /// <summary>When the context gate last wrote the session's state, its start included (1601 when it's gone since).</summary>
    private DateTime Written(string sessionId) => File.GetLastWriteTimeUtc(Path.Combine(cycler.Directory, sessionId + ".state.json"));
}
