using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// A move in flight (M.ORCH-T637): the CLI being moved to another login and every ticket it or its subagents hold, locked from
/// the moment the move is sent until it lands on the new login (or fails, or times out). Kept in <c>data/orchestrator.db</c>'s
/// <see cref="MoveLocks.StateName"/> state row, so the dashboard, the daemon, <c>orchestrator move</c> and <c>orchestrator next</c>
/// all see the same locks.
/// </summary>
public sealed record MoveLock
{
    /// <summary>The moving CLI's session, and its claude.exe as it was when the move was sent.</summary>
    public string SessionId { get; init; } = "";
    public int Pid { get; init; }

    /// <summary>The Launcher tab it runs in (its host process and input pipe): the same tab restarts it on the new login.</summary>
    public int? HostPid { get; init; }
    public string? HostPipe { get; init; }

    public string Repo { get; init; } = "";
    public string? Cwd { get; init; }

    /// <summary>The per-CLI login it was on (its email), null for the global login.</summary>
    public string? FromEmail { get; init; }

    /// <summary>The login it's moving to: its email (null for the global login) and its name as the dashboard shows it.</summary>
    public string? ToEmail { get; init; }
    public string ToName { get; init; } = "";

    public DateTime StartedUtc { get; init; }

    /// <summary>The tickets it and its subagents hold: its claims (<c>orchestrator next --claim</c>), kept claimed for it.</summary>
    public List<string> Tickets { get; init; } = [];

    /// <summary>Whether its running subagents were stopped (its turn interrupted) when the move was sent.</summary>
    public bool Interrupted { get; init; }

    /// <summary>"Moving to Ryan…", as every locked control says it.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string Text => $"Moving to {ToName}…";
}

/// <summary>How a move ended: <see cref="Landed"/> on the new login (as <see cref="Cli"/>), or not (<see cref="Why"/>).</summary>
public sealed record MoveEnd(MoveLock Lock, bool Landed, AgentBoardRow? Cli, string Why);

/// <summary>
/// The moves in flight (M.ORCH-T637), one write transaction each, so two processes never both begin a move of one CLI or both
/// end one. A lock ends when the CLI shows up on the new login (<see cref="Landed"/>), when its CLI ended and didn't come back
/// within <see cref="GoneAfter"/>, or after <see cref="Timeout"/>; <see cref="Reconcile"/> hands each end back once.
/// </summary>
public sealed class MoveLocks(JsonStore store)
{
    public const string StateName = "account-moves";

    /// <summary>A move restarts the CLI at its next idle point, which can wait out a long turn.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(30);

    /// <summary>How long a move whose CLI has ended may take to come back on the new login before it has failed.</summary>
    public static readonly TimeSpan GoneAfter = TimeSpan.FromMinutes(5);

    public JsonStore Store { get; } = store;

    /// <summary>Whether a claude.exe is still running (seam).</summary>
    internal Func<int, bool> IsAlive { get; init; } = ContextCycler.IsClaude;

    /// <summary>Every move in flight; none when they can't be read now.</summary>
    public IReadOnlyList<MoveLock> All()
    {
        try { return Store.Load<List<MoveLock>>(StateName); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { return []; }
    }

    /// <summary>Locks <paramref name="move"/>'s CLI and tickets; false (with the move already in flight) when its CLI is moving.</summary>
    public bool TryBegin(MoveLock move, out MoveLock? already)
    {
        MoveLock? found = null;
        Store.Update<List<MoveLock>>(StateName, all =>
        {
            found = all.FirstOrDefault(m => m.SessionId == move.SessionId || m.Pid == move.Pid);
            if (found is null) all.Add(move);
        });
        already = found;
        return found is null;
    }

    /// <summary>Replaces the lock of <paramref name="move"/>'s session (e.g. to record that its subagents were stopped).</summary>
    public void Put(MoveLock move) => Store.Update<List<MoveLock>>(StateName, all =>
    {
        var i = all.FindIndex(m => m.SessionId == move.SessionId);
        if (i >= 0) all[i] = move;
    });

    /// <summary>Unlocks <paramref name="sessionId"/>'s move (a refused send); true when there was one.</summary>
    public bool End(string sessionId)
    {
        var removed = false;
        Store.Update<List<MoveLock>>(StateName, all => removed = all.RemoveAll(m => m.SessionId == sessionId) > 0);
        return removed;
    }

    /// <summary>The tickets a CLI holds: its claims, by its process or its session (as the agent board lists them).</summary>
    public static List<string> ClaimsOf(IEnumerable<Assignment> assignments, int pid, string sessionId) =>
        [.. assignments.Where(a => a.IsClaim && (a.Cli == pid || (a.CliSession is not null && a.CliSession == sessionId)))
            .Select(a => a.TicketId).Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>The move holding ticket <paramref name="ticketId"/>, if any.</summary>
    public static MoveLock? ForTicket(IEnumerable<MoveLock> moves, string? ticketId) =>
        ticketId is null ? null : moves.FirstOrDefault(m => m.Tickets.Contains(ticketId, StringComparer.OrdinalIgnoreCase));

    /// <summary>Whether <paramref name="row"/> is <paramref name="move"/>'s CLI on the new login: its own session, the same
    /// Launcher tab, or a CLI of its repo started since the move (a resumed CLI may come back under a new session).</summary>
    public static bool Landed(MoveLock move, AgentBoardRow row) =>
        string.Equals(row.OwnLogin ?? "", move.ToEmail ?? "", StringComparison.OrdinalIgnoreCase) &&
        (row.SessionId == move.SessionId ||
         (move.HostPid is { } host && row.Host?.ProcessId == host) ||
         (string.Equals(row.Repo, move.Repo, StringComparison.OrdinalIgnoreCase) && row.StartedUtc >= move.StartedUtc));

    /// <summary>
    /// Ends every move <paramref name="seen"/> (the live CLIs) shows landed, every one whose CLI ended and hasn't come back
    /// within <see cref="GoneAfter"/>, and every one past <see cref="Timeout"/>, in one write: each end is returned once, to the
    /// one caller that ended it.
    /// </summary>
    public IReadOnlyList<MoveEnd> Reconcile(IReadOnlyList<AgentBoardRow> seen, DateTime nowUtc)
    {
        if (All().Count == 0) return [];
        var ends = new List<MoveEnd>();
        Store.Update<List<MoveLock>>(StateName, all =>
        {
            foreach (var m in all.ToList())
            {
                MoveEnd? end = null;
                if (seen.FirstOrDefault(r => Landed(m, r)) is { } row) end = new MoveEnd(m, true, row, "");
                else if (nowUtc - m.StartedUtc > Timeout)
                    end = new MoveEnd(m, false, null, $"its CLI didn't show up on {m.ToName} within {Timeout.TotalMinutes:0} minutes");
                else if (nowUtc - m.StartedUtc > GoneAfter && !IsAlive(m.Pid))
                    end = new MoveEnd(m, false, null, $"its CLI ended and didn't come back on {m.ToName}");
                if (end is null) continue;
                all.Remove(m);
                ends.Add(end);
            }
        });
        return ends;
    }
}

/// <summary>
/// Hands a moved CLI's tickets back (M.ORCH-T637). Landed: its claims are put on the CLI as it is now (its process and session on
/// the new login) and its CLI is left a nudge to start their subagents again in the same worktrees, under the new account (added to its next
/// turn's context by its UserPromptSubmit hook, <see cref="CliNudges"/>; never typed into its box, M.ORCH-T864).
/// Failed or timed out: the claims stay with the old owner, as they were, and its CLI (if it's still running) is told the same.
/// </summary>
public sealed class MoveHandback(JsonStore store)
{
    /// <summary>Leaves a nudge for a CLI's hook (seam): the CLI's process id and the text; true when it was written.</summary>
    internal Func<int, string, bool> Nudge { get; init; } = (pid, text) => CliNudges.Leave(new ContextCycler().Directory, pid, "handback", text);

    /// <summary>Whether a claude.exe is still running (seam).</summary>
    internal Func<int, bool> IsAlive { get; init; } = ContextCycler.IsClaude;

    /// <param name="Left">The nudge was left for the CLI's hook; false with tickets: it's said to you to do.</param>
    public sealed record Result(MoveEnd End, IReadOnlyList<string> Tickets, bool Left, string Line);

    public Result Deliver(MoveEnd end)
    {
        var m = end.Lock;
        var where = LimitWaker.Where(m.Cwd);
        var held = m.Tickets.ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<Assignment> claims = [];
        try
        {
            store.UpdateAssignments(all =>
            {
                for (var i = 0; i < all.Count; i++)
                {
                    var a = all[i];
                    if (!a.IsClaim || !held.Contains(a.TicketId)) continue;
                    // Landed: the claim is the CLI's as it is now, so its next and its share count it there; otherwise the old owner keeps it.
                    // On its new login too (M.ORCH-T766), which the daemon records in the ticket database as who works it.
                    if (end.Landed && end.Cli is { } cli)
                        all[i] = a = a with { Cli = cli.CliPid ?? a.Cli, CliSession = cli.SessionId, Account = AccountDirs.Normalize(cli.OwnLogin ?? m.ToEmail) };
                    claims.Add(a);
                }
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { }

        var ids = claims.Select(c => c.TicketId).ToList();
        // The CLI as it runs now (its process on the new login when it landed), or the old one while it's still running.
        int? pid = end.Landed ? end.Cli?.CliPid : IsAlive(m.Pid) ? m.Pid : null;
        var headline = end.Landed
            ? $"move: the CLI in {where} is on {m.ToName} now"
            : $"move: the CLI in {where} didn't move to {m.ToName} ({end.Why}); it stays with {m.FromEmail ?? AccountMove.GlobalLabel.ToLowerInvariant()}";
        if (ids.Count == 0) return new Result(end, ids, false, headline + ".");

        var prompt = Prompt(end, claims);
        var left = false;
        if (pid is { } target)
        {
            try { left = Nudge(target, prompt); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        var tickets = string.Join(", ", ids);
        return new Result(end, ids, left, left
            ? $"{headline}; it's told at its next prompt to start its subagents again on {tickets} in their worktrees."
            : $"{headline}. Tell it to start its subagents again on {tickets} in their worktrees ({(pid is null ? "its CLI isn't running" : "its nudge couldn't be written")}).");
    }

    /// <summary>What the CLI is told, as hook context (see <see cref="AllocateWaker.Prompt"/>).</summary>
    public static string Prompt(MoveEnd end, IReadOnlyList<Assignment> claims)
    {
        var m = end.Lock;
        var head = end.Landed
            ? $"This CLI has just moved to {m.ToName}'s login. "
            : $"Moving this CLI to {m.ToName}'s login didn't happen ({end.Why}), so it stays on its login. ";
        return head + (m.Interrupted ? "The subagents it had running were stopped for the move. " : "") +
            $"{(claims.Count == 1 ? "This ticket is" : "These tickets are")} still claimed for this CLI, with {(claims.Count == 1 ? "its worktree" : "their worktrees")} and uncommitted changes kept: " +
            string.Join("; ", claims.Select(c => $"{c.TicketId} (worktree {c.Directory}{(c.Model is { } model ? $", model {model}" : "")})")) + ". " +
            "Start a subagent on each again now, all in one message, each on the model named and with the prompt orchestrator next printed for it when it was claimed, " +
            "working in that same worktree: don't make a new one or reset it, carry on from its uncommitted changes; land it on main and close the ticket as before. " +
            "Then carry on as you were.";
    }
}
