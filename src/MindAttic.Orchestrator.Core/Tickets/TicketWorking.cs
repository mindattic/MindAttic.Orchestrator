using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using MindAttic.Tickets.Data;

namespace MindAttic.Orchestrator.Tickets;

/// <summary>
/// Who is working each ticket, recorded in the ticket database through the MindAttic.Tickets engine (M.ORCH-T766, the
/// <c>ticket_work</c> table M.TIX-T134 added, as <c>tickets ticket working</c> writes it) so Tickets Desktop links every ticket
/// being worked back to the Orchestrator: the login it runs on (its account email) and the Claude session when known. It's set
/// as a ticket is claimed (<c>orchestrator next --claim</c>) or a tab opens on it, cleared as it's reaped (landed or given back)
/// or released, and the lease-holding daemon keeps it in step with the assignments every tick (<see cref="Sync"/>): a move to
/// another login, an orphan redispatch and a claim gone stale all show there within a minute. A write that fails (SQL Server down,
/// the database held) is logged and dropped: it never holds up a dispatch.
/// </summary>
public static class TicketWorking
{
    /// <summary>The login (account email, lower-cased) and Claude session working a ticket; either may be unknown, not both.</summary>
    public sealed record Who(string? Login, string? Session);

    private static readonly Serilog.ILogger Log = OrchestratorLog.For("TicketWorking");

    /// <summary>
    /// Who should be recorded on each open ticket now, from <paramref name="plan"/>: every ticket with a live assignment (a claim,
    /// a tab, a background agent; not one gone stale) on its <see cref="Assignment.Account"/> and its claiming CLI's session, else
    /// the session a live agent is seen working it in; and every ticket a live session works without an assignment
    /// (<see cref="SchedulePlan.Worked"/>), by its session. A ticket with neither login nor session known is left out.
    /// <paramref name="assignments"/>, read after the plan, stands for the plan's own: a claim handed back, reaped or moved since
    /// is taken as it is now (an assignment the plan didn't see isn't stale).
    /// </summary>
    public static IReadOnlyDictionary<string, Who> Wanted(SchedulePlan plan, IReadOnlyList<Assignment>? assignments = null)
    {
        var current = assignments?.GroupBy(a => a.TicketId, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);
        Assignment? Live(QueueItem q) => current is null ? q.IsStale ? null : q.Assignment
            : current.TryGetValue(q.Id, out var now) && !(q.IsStale && now == q.Assignment) ? now : null;
        // The lowest session id first, so the pick is the same every tick and the history isn't rewritten back and forth.
        var seen = plan.Worked.GroupBy(w => w.TicketId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(w => w.SessionId).Where(s => !string.IsNullOrWhiteSpace(s)).Order(StringComparer.Ordinal).FirstOrDefault(),
                StringComparer.OrdinalIgnoreCase);
        var open = plan.Queue.Concat(plan.AwaitingApproval).GroupBy(q => q.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var wanted = new Dictionary<string, Who>(StringComparer.OrdinalIgnoreCase);
        foreach (var q in open.Values)
        {
            Who? who = Live(q) is { } a ? Of(a.Account, a.CliSession ?? seen.GetValueOrDefault(q.Id))
                : seen.TryGetValue(q.Id, out var session) ? Of(null, session) : null;
            if (who is not null) wanted[q.Id] = who;
        }
        return wanted;
    }

    /// <summary>A <see cref="Who"/> as the engine keeps it (the email lower-cased, an address that isn't one dropped); null when
    /// nothing is known.</summary>
    public static Who? Of(string? login, string? session)
    {
        var l = TicketsDb.NormalizeEmail(login);
        var s = string.IsNullOrWhiteSpace(session) ? null : session.Trim();
        return l is null && s is null ? null : new Who(l, s);
    }

    /// <summary>
    /// Brings the database's working sessions in line with <paramref name="wanted"/>: each ticket there recorded as it says, and,
    /// with <paramref name="mayClear"/>, each one recorded as worked that isn't there cleared (landed, released, stale, given back).
    /// Each ticket is its own write; one that fails is said in the result and the rest go on.
    /// </summary>
    public static (int Changed, IReadOnlyList<string> Failed) Sync(TicketsDb db, IReadOnlyDictionary<string, Who> wanted, bool mayClear = true)
    {
        var failed = new List<string>();
        var changed = 0;
        IReadOnlyDictionary<string, TicketWork> now;
        try { now = db.WorkStates(); }
        catch (Exception ex) when (IsWriteFailure(ex)) { return (0, [$"couldn't read who works the tickets: {ex.Message}"]); }
        foreach (var (id, who) in wanted)
        {
            if (now.TryGetValue(id, out var w) && w.Login == who.Login && w.Session == who.Session) continue;
            try
            {
                db.SetWorking(id, who.Login, who.Session);
                changed++;
            }
            catch (Exception ex) when (IsWriteFailure(ex)) { failed.Add($"couldn't record who works {id}: {ex.Message}"); }
        }
        if (!mayClear) return (changed, failed);
        foreach (var (id, _) in now.Where(kv => kv.Value.IsWorking && !wanted.ContainsKey(kv.Key)))
        {
            try
            {
                db.ClearWorking(id);
                changed++;
            }
            catch (Exception ex) when (IsWriteFailure(ex)) { failed.Add($"couldn't clear who works {id}: {ex.Message}"); }
        }
        return (changed, failed);
    }

    /// <summary>
    /// <see cref="Sync"/> on the workspace's ticket database for <paramref name="plan"/>. Nothing is cleared while a repo's tickets
    /// couldn't be read (<see cref="SchedulePlan.UnknownRepos"/>): its worked tickets would look finished. No database (tickets
    /// still in the story files) is nothing to do.
    /// </summary>
    public static (int Changed, IReadOnlyList<string> Failed) Sync(string? workspaceRoot, SchedulePlan plan, IReadOnlyList<Assignment>? assignments = null)
    {
        try
        {
            using var db = TicketsDb.OpenIfMigrated(workspaceRoot);
            return db is null ? (0, []) : Sync(db, Wanted(plan, assignments), mayClear: plan.UnknownRepos.Count == 0);
        }
        catch (Exception ex) when (IsWriteFailure(ex)) { return (0, [$"couldn't open the ticket database: {ex.Message}"]); }
    }

    /// <summary>Records that <paramref name="login"/> and/or <paramref name="session"/> works ticket <paramref name="id"/> now;
    /// best effort (logged, never thrown). Nothing known is nothing written.</summary>
    public static void Set(string? workspaceRoot, string id, string? login, string? session)
    {
        if (Of(login, session) is not { } who) return;
        Write(workspaceRoot, id, "record who works", db => db.SetWorking(id, who.Login, who.Session));
    }

    /// <summary>Clears who works ticket <paramref name="id"/> (it landed, was released or given back); best effort.</summary>
    public static void Clear(string? workspaceRoot, string id) => Write(workspaceRoot, id, "clear who works", db => db.ClearWorking(id));

    private static void Write(string? workspaceRoot, string id, string what, Action<TicketsDb> write)
    {
        try
        {
            using var db = TicketsDb.OpenIfMigrated(workspaceRoot);
            if (db is null || db.Ticket(id) is null) return;
            write(db);
        }
        catch (Exception ex) when (IsWriteFailure(ex))
        {
            Log.Warning("couldn't {What} {Ticket:l} in the ticket database (the daemon tries again next tick): {Error:l}", what, id, ex.Message);
        }
    }

    /// <summary>What a write to the engine can fail with that must not end the caller's work: everything but a fatal error.</summary>
    private static bool IsWriteFailure(Exception ex) => ex is not (OutOfMemoryException or StackOverflowException or AccessViolationException);
}
