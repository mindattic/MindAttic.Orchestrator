using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// A dispatched ticket whose session went dark (M.ORCH-T748): the CLI that claimed it (or the Launcher tab it was started in)
/// exited for any reason but a window's reset (closed, crashed, the machine slept), so its subagent died with it, the ticket is
/// still open, and nothing has worked its worktree since. Kept in the store's <see cref="OrphanWatch.StoreName"/> row so
/// <c>orchestrator status</c> and <c>orchestrator queue</c> show it at once, apart from an ordinary open ticket.
/// </summary>
public sealed record OrphanRecord(string TicketId, string Repo, string Directory, DateTime FirstSeenUtc)
{
    /// <summary>Waiting to be redispatched (<see cref="Why"/> says on what).</summary>
    public const string Orphaned = "orphaned";

    /// <summary>Its dead claim was dropped so the /allocate CLI on its repo claims it again with <c>orchestrator next --claim</c>.</summary>
    public const string HandedBack = "handed back";

    /// <summary>Started again in a new Launcher tab in its worktree.</summary>
    public const string Reopened = "reopened";

    /// <summary>Not redispatched again: it died once per redispatch allowed, or its work can't be carried forward by itself.</summary>
    public const string NeedsYou = "needs you";

    public string State { get; init; } = Orphaned;

    /// <summary>How many of its dispatches went dark.</summary>
    public int Deaths { get; init; }

    /// <summary>How many times the daemon redispatched it.</summary>
    public int Redispatches { get; init; }

    /// <summary>The dead assignment last counted (its start), so the same death isn't counted every tick.</summary>
    public DateTime? DeadStartedUtc { get; init; }

    public DateTime? RedispatchedUtc { get; init; }

    /// <summary>What it waits on, or what happened to it last.</summary>
    public string? Why { get; init; }

    /// <summary>The CLI (process id) whose exit orphaned it; null for a tab's.</summary>
    public int? Cli { get; init; }

    /// <summary>Commits on its worktree's branch not on the main line yet, when it went dark.</summary>
    public int Ahead { get; init; }

    /// <summary>Uncommitted paths in its worktree when it went dark (at most <see cref="OrphanWatch.MaxPaths"/>).</summary>
    public List<string> Uncommitted { get; init; } = [];

    /// <summary>Still to be acted on: waiting, or given up on (not yet running again).</summary>
    public bool Open => State is Orphaned or NeedsYou;
}

/// <summary>
/// Finds dispatched tickets whose session went dark and redispatches them (M.ORCH-T748), as the daemon resumes a CLI after its
/// window resets (<see cref="LimitWaker"/>) but for a host process that disappeared mid-task. Only through the existing claim
/// and dispatch machinery, never by typing into a session: the dead claim is handed back to the live /allocate CLI on its repo
/// (its next <c>orchestrator next --claim</c> restarts it in the same worktree, the prompt carrying the work forward), else a
/// new Launcher tab is opened on it in its worktree (<see cref="Dispatcher"/>: a login with room under its cutoffs, never the
/// global login, the API key only when the owner has API billing on). It waits while the window is at its target, autopilot is
/// paused, quiet hours run or no login has room; it never hands out a worktree whose work it can't carry forward, and after
/// <see cref="OrchestratorSettings.OrphanRedispatches"/> redispatches it stops and says the ticket keeps dying.
/// </summary>
public sealed class OrphanWatch(JsonStore store)
{
    public const string StoreName = "orphaned-tickets";

    /// <summary>The most uncommitted paths a record keeps and a prompt names.</summary>
    internal const int MaxPaths = 12;

    /// <summary>Whether a CLI process still runs (seam).</summary>
    internal Func<int, bool> CliRunning { get; init; } = ContextCycler.IsClaude;

    /// <summary>A worktree's commits ahead of the main line and its uncommitted paths; null when it can't be read (seam).</summary>
    internal Func<string, string, (int Ahead, IReadOnlyList<string> Dirty)?> Inspect { get; init; } = InspectWorktree;

    /// <summary>Opens a new tab on a ticket in its worktree with the prompt given (seam); null opens none.</summary>
    internal Func<QueueItem, string, Dispatcher.Result>? OpenTab { get; init; }

    /// <summary>Whether a ticket is still open right now (seam; <see cref="Autopilot.StillOpen"/>).</summary>
    internal Func<string, string, bool> StillOpen { get; init; } = Autopilot.StillOpen;

    /// <summary>The main line a repo lands on (seam).</summary>
    internal Func<string, string> MainlineOf { get; init; } = Dispatcher.Mainline;

    /// <param name="Line">For the log.</param>
    /// <param name="Alert">The alert's title and text when it's worth one; null for the log alone.</param>
    public sealed record Event(string Line, bool Warn, (string Title, string Text, bool Urgent)? Alert = null);

    /// <summary>The records kept: tickets that went dark and what became of them.</summary>
    public static List<OrphanRecord> Load(JsonStore store) => store.Load<List<OrphanRecord>>(StoreName);

    /// <summary>Forgets <paramref name="ticketId"/>'s record (<c>orchestrator release</c>): it's yours now.</summary>
    public static bool Forget(JsonStore store, string ticketId)
    {
        var removed = false;
        store.Update<List<OrphanRecord>>(StoreName, all => removed = all.RemoveAll(r => Same(r.TicketId, ticketId)) > 0);
        return removed;
    }

    /// <summary>
    /// The assignments gone dark: a worktree claim or tab (no background agent) on an open ticket, dispatched at least
    /// <paramref name="quiet"/> ago and not moving between logins, whose owner is gone (the claiming CLI's process, or, with no
    /// CLI recorded, any Launcher tab in its worktree), with nothing written in its worktree for <paramref name="quiet"/> and no
    /// other live session working it.
    /// </summary>
    internal static IReadOnlyList<QueueItem> Dark(SchedulePlan plan, Snapshot snapshot, Func<int, bool> cliRunning, TimeSpan quiet)
    {
        var dark = new List<QueueItem>();
        foreach (var q in plan.Queue)
        {
            if (q.Assignment is not { Worktree: true, Pilot: null, Log: null } a || q.Moving is not null) continue;
            if (snapshot.TakenUtc - a.StartedUtc < quiet) continue;
            var ownerGone = a.Cli is { } cli ? !cliRunning(cli)
                : !snapshot.Repos.Any(r => r.Hosts.Any(h => IsUnder(h.WorkingDirectory, a.Directory)));
            if (!ownerGone) continue;
            // Another live session working it (not the dead CLI's own, whose subagents died with it).
            if (plan.Worked.Any(w => Same(w.TicketId, q.Id) && (a.CliSession is null || w.SessionId != a.CliSession))) continue;
            if (Active(a.Directory, snapshot, quiet)) continue;
            dark.Add(q);
        }
        return dark;
    }

    /// <summary>A tab open in the worktree, or a session or ticketed subagent that wrote from it within <paramref name="quiet"/>.</summary>
    private static bool Active(string directory, Snapshot snapshot, TimeSpan quiet)
    {
        bool Recent(DateTime at) => snapshot.TakenUtc - at < quiet;
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

    /// <summary>
    /// One pass: records each dispatch that went dark (once), alerts on it, and redispatches what may be; drops the records of
    /// tickets that closed. <paramref name="pilots"/> are the live /allocate CLIs and their repos.
    /// </summary>
    public IReadOnlyList<Event> Tick(SchedulePlan plan, Snapshot snapshot, OrchestratorSettings settings,
        IReadOnlyDictionary<int, IReadOnlyList<string>> pilots, DateTime nowUtc)
    {
        var events = new List<Event>();
        if (!store.TryLoad<List<OrphanRecord>>(StoreName, out var records)) return events;
        var before = records.ToList();
        var open = snapshot.Repos.SelectMany(r => r.OpenTickets).Select(t => t.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var gone in records.Where(r => !open.Contains(r.TicketId) && !snapshot.UnknownRepos.Contains(r.Repo)).ToList())
        {
            records.Remove(gone);
            events.Add(new Event($"orphaned: {gone.TicketId} is closed now" + (gone.Redispatches > 0 ? $" (redispatched {Times(gone.Redispatches)})" : ""), false));
        }

        var quiet = TimeSpan.FromMinutes(Math.Clamp(settings.OrphanQuietMinutes, 1, 24 * 60));
        foreach (var q in Dark(plan, snapshot, CliRunning, quiet))
        {
            var a = q.Assignment!;
            var i = records.FindIndex(r => Same(r.TicketId, q.Id));
            var rec = i >= 0 ? records[i] : new OrphanRecord(q.Id, q.Repo, a.Directory, nowUtc);
            if (rec.DeadStartedUtc != a.StartedUtc)
            {
                // A new death: its worktree read as it was left, so the next agent is told what's there.
                var state = Inspect(a.Directory, q.RepoPath);
                rec = rec with
                {
                    Directory = a.Directory, Deaths = rec.Deaths + 1, DeadStartedUtc = a.StartedUtc, Cli = a.Cli,
                    State = rec.State == OrphanRecord.NeedsYou ? OrphanRecord.NeedsYou : OrphanRecord.Orphaned, Why = null,
                    Ahead = state?.Ahead ?? 0, Uncommitted = [.. (state?.Dirty ?? []).Take(MaxPaths)],
                };
                var owner = a.Cli is { } c ? $"the CLI that claimed it (pid {c}) is gone" : "the tab it was started in is gone";
                var text = $"{q.Id} in {q.Repo}: {owner} and nothing has worked {a.Directory} for {quiet.TotalMinutes:0}+ min; the ticket is still open" +
                    (rec.Deaths > 1 ? $" (it went dark {Times(rec.Deaths)})" : "");
                events.Add(new Event($"orphaned: {text}", true, ("Orphaned ticket", text, false)));
            }
            if (rec.State == OrphanRecord.Orphaned) rec = Redispatch(q, rec, plan, settings, pilots, nowUtc, events);
            if (i >= 0) records[i] = rec; else records.Add(rec);
        }

        if (!records.SequenceEqual(before))
            try { store.Save(StoreName, records); }
            catch (IOException ex) { events.Add(new Event($"orphaned: couldn't save what went dark: {ex.Message}", true)); }
        return events;
    }

    /// <summary>Redispatches <paramref name="q"/> when it may go now, else says what it waits on (once per reason).</summary>
    private OrphanRecord Redispatch(QueueItem q, OrphanRecord rec, SchedulePlan plan, OrchestratorSettings settings,
        IReadOnlyDictionary<int, IReadOnlyList<string>> pilots, DateTime nowUtc, List<Event> events)
    {
        OrphanRecord Wait(string why)
        {
            if (rec.Why != why) events.Add(new Event($"orphaned: {q.Id} waits: {why}", false));
            return rec with { Why = why };
        }
        OrphanRecord GiveUp(string why)
        {
            var text = $"{q.Id} in {q.Repo}: {why}";
            events.Add(new Event($"orphaned: {text}", true, ("Needs you", text, true)));
            return rec with { State = OrphanRecord.NeedsYou, Why = why };
        }

        var max = Math.Max(0, settings.OrphanRedispatches);
        if (max == 0) return Wait("redispatching orphaned tickets is off (settings --orphan-redispatches); start it again yourself");
        if (rec.Redispatches >= max)
            return GiveUp($"it went dark {Times(rec.Deaths)} and was redispatched {Times(rec.Redispatches)}, so it isn't redispatched again; its work is in {rec.Directory} (orchestrator release {q.Id} hands it back to you)");
        // Its work is carried forward only where a new claim or tab opens: the worktree <root>\<repo>\<ticket>.
        var tree = Path.Combine(settings.WorktreeRoot, q.Repo, q.Id);
        if (!SamePath(rec.Directory, tree) && (rec.Ahead > 0 || rec.Uncommitted.Count > 0))
            return GiveUp($"its work is in {rec.Directory}, not {tree} where a redispatch would start, so it isn't redispatched (that would leave the work behind)");
        if (!settings.Autopilot) return Wait("autopilot is paused");
        if (QuietHours.Active(settings.QuietHours, nowUtc.ToLocalTime()) is { } quietHours) return Wait($"quiet hours ({quietHours})");
        if (plan.Stopped) return Wait($"the {plan.Pace?.Governing ?? "5-hour"} window is at its target; it's redispatched after the reset");
        if (!StillOpen(q.RepoPath, q.Id)) return rec;

        var cli = pilots.Where(p => Autopilot.Named(p.Value, q.Repo)).Select(p => (int?)p.Key).Order().FirstOrDefault();
        if (cli is { } pilot)
        {
            // Handed back: the dead claim goes, so the /allocate CLI's next `orchestrator next --claim` claims it again in the same
            // worktree (nothing is typed into it; keep-going and AutopilotWaker send it on as they do any workable ticket).
            var dead = q.Assignment!;
            store.UpdateAssignments(all => all.RemoveAll(x => Same(x.TicketId, q.Id) && x.StartedUtc == dead.StartedUtc && x.Cli == dead.Cli));
            events.Add(new Event($"orphaned: handed {q.Id} back to the /allocate CLI {pilot} on {q.Repo}: its next orchestrator next --claim restarts it in {tree}, carrying its work forward", false));
            return rec with { State = OrphanRecord.HandedBack, Redispatches = rec.Redispatches + 1, RedispatchedUtc = nowUtc, Why = $"handed back to the /allocate CLI {pilot}" };
        }
        if (OpenTab is not { } open) return Wait("no /allocate CLI works its repo, and this process can't open a tab");
        var prompt = CarryForward(rec) + Autopilot.PromptFor(q, tree, Dispatcher.BranchFor(q.Id), MainlineOf(q.RepoPath));
        var result = open(q, prompt);
        if (!result.Ok) return Wait(result.Message);
        events.Add(new Event($"orphaned: started {q.Id} again in a new tab: {result.Message}", false, ("Redispatched", $"{q.Id} in {q.Repo} went dark; started again in a new tab in its worktree.", false)));
        return rec with { State = OrphanRecord.Reopened, Redispatches = rec.Redispatches + 1, RedispatchedUtc = nowUtc, Why = "started again in a new tab" };
    }

    /// <summary>What the next agent on an orphaned ticket is told before its ticket prompt: one line, no double quote.</summary>
    public static string CarryForward(OrphanRecord rec)
    {
        var work = new List<string>();
        if (rec.Ahead > 0) work.Add($"{rec.Ahead} commit{(rec.Ahead == 1 ? "" : "s")} not on the main line yet");
        if (rec.Uncommitted.Count > 0) work.Add("uncommitted changes in " + string.Join(", ", rec.Uncommitted.Select(p => p.Replace("\"", ""))));
        // Counted once it's redispatched: before that (a tab's prompt is written first) this is the next one.
        var attempt = rec.State == OrphanRecord.Orphaned ? rec.Redispatches + 1 : Math.Max(1, rec.Redispatches);
        return $"A previous agent on {rec.TicketId} died mid-task (its host process exited without closing the ticket; this is redispatch {attempt}). " +
            $"Its work so far is in the worktree {rec.Directory}" + (work.Count > 0 ? ": " + string.Join("; ", work) : "") + ". " +
            "Start by reading git status, git log and git diff there and any notes it left (docs or other files in the worktree), and carry that work forward: " +
            "never discard, reset or stash it away. ";
    }

    /// <summary>The line <c>orchestrator status</c> and <c>orchestrator queue</c> show for a record.</summary>
    public static string Describe(OrphanRecord r, DateTime nowUtc) => r.State switch
    {
        OrphanRecord.Orphaned => $"{r.TicketId} in {r.Repo} went dark {Formatting.Ago(r.FirstSeenUtc, nowUtc)} ({r.Directory}); " + (r.Why is { } why ? $"waits: {why}" : "being redispatched"),
        OrphanRecord.NeedsYou => $"{r.TicketId} in {r.Repo} needs you: {r.Why}",
        _ => $"{r.TicketId} in {r.Repo} went dark {Times(r.Deaths)}; {r.Why ?? r.State} {Formatting.Ago(r.RedispatchedUtc, nowUtc)}",
    };

    private static (int Ahead, IReadOnlyList<string> Dirty)? InspectWorktree(string directory, string repoPath)
    {
        if (!System.IO.Directory.Exists(directory)) return null;
        var (code, status) = Dispatcher.Git(directory, "status", "--porcelain");
        if (code != 0) return null;
        var dirty = status.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(l => l.Length > 3 ? l[3..].Trim().Trim('"') : l).ToList();
        var mainline = Dispatcher.Mainline(repoPath);
        var (aheadCode, ahead) = Dispatcher.Git(directory, "rev-list", "--count", $"origin/{mainline}..HEAD");
        return (aheadCode == 0 && int.TryParse(ahead.Trim(), out var n) ? n : 0, dirty);
    }

    private static string Times(int n) => n == 1 ? "once" : n == 2 ? "twice" : $"{n} times";

    private static bool Same(string a, string b) => a.Equals(b, StringComparison.OrdinalIgnoreCase);

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)), StringComparison.OrdinalIgnoreCase);

    private static bool IsUnder(string? path, string directory)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(directory)) return false;
        var p = Path.TrimEndingDirectorySeparator(path.Replace('/', '\\'));
        var d = Path.TrimEndingDirectorySeparator(directory.Replace('/', '\\'));
        return p.Equals(d, StringComparison.OrdinalIgnoreCase) || p.StartsWith(d + "\\", StringComparison.OrdinalIgnoreCase);
    }
}
