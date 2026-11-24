using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Tickets;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// A host branch ticket waiting on its branch (M.ORCH-T1661, a M.ORCH-T1633 follow-up). On the remote host with branch builds on,
/// the agent pushes its ticket's work to <c>origin/work/&lt;ticket&gt;</c> and no longer closes the ticket: it marks it awaiting its
/// branch's test (<see cref="MarkAwaiting"/>, <c>orchestrator branches pushed</c>), and the ticket stays open until the host merges
/// the signed-off head into main, when <see cref="BranchMerges"/> closes it with the merged commit as its ref. Meanwhile it isn't
/// handed out again (<see cref="AwaitingTickets"/>, kept out of the scheduler's queue). A failed local run hands it back
/// (<see cref="SendBack"/> clears the mark), as does a build of its head that failed, or a merge into main that conflicted. So the
/// Tickets library needs no reopen: a branch ticket is never closed before its work is on main.
/// </summary>
public sealed partial class BranchBuilds
{
    /// <summary>The store's record of the tickets waiting on their pushed work branch.</summary>
    public const string AwaitingName = "branch-awaiting";

    /// <summary>A ticket's pushed work branch, waiting on its build, its local test run and the host's merge.</summary>
    public sealed class AwaitingState
    {
        /// <summary>Gets or sets the repo the branch is in.</summary>
        public string Repo { get; set; } = "";
        /// <summary>Gets or sets the work branch's name (under <see cref="Prefix"/>).</summary>
        public string Branch { get; set; } = "";
        /// <summary>Gets or sets the head the agent pushed.</summary>
        public string Head { get; set; } = "";
        /// <summary>Gets or sets when it was pushed.</summary>
        public DateTime SinceUtc { get; set; }
    }

    /// <summary>The store's record of the awaiting tickets (ticket → its branch); empty when there's none or it can't be read.</summary>
    public static Dictionary<string, AwaitingState> LoadAwaiting(JsonStore store) =>
        store.TryLoad<Dictionary<string, AwaitingState>>(AwaitingName, out var awaiting) ? awaiting : [];

    /// <summary>Marks <paramref name="ticket"/> as waiting on <paramref name="repo"/>'s <paramref name="branch"/>, pushed at
    /// <paramref name="head"/>: it stays open but isn't handed out again until the branch comes back or is merged.</summary>
    public static void MarkAwaiting(JsonStore store, string repo, string branch, string ticket, string head, DateTime nowUtc) =>
        store.Update<Dictionary<string, AwaitingState>>(AwaitingName, all =>
        {
            foreach (var key in all.Keys.Where(k => string.Equals(k, ticket, StringComparison.OrdinalIgnoreCase)).ToList()) all.Remove(key);
            all[ticket] = new AwaitingState { Repo = repo, Branch = branch, Head = head.Trim(), SinceUtc = nowUtc };
        });

    /// <summary>Clears <paramref name="ticket"/>'s mark, so it's handed out again; whether it had one.</summary>
    public static bool ClearAwaiting(JsonStore store, string? ticket)
    {
        if (string.IsNullOrEmpty(ticket) || !store.Has(AwaitingName)) return false;
        var cleared = false;
        store.Update<Dictionary<string, AwaitingState>>(AwaitingName, all =>
        {
            foreach (var key in all.Keys.Where(k => string.Equals(k, ticket, StringComparison.OrdinalIgnoreCase)).ToList())
                cleared |= all.Remove(key);
        });
        return cleared;
    }

    /// <summary>
    /// The tickets waiting on their pushed branch now, which the scheduler keeps out of its queue: every marked one but those whose
    /// branch came back since (its last local run failed at the head it stands at: <see cref="ReturnedToWork"/>), or whose head
    /// failed to build. A mark whose branch the host hasn't seen yet (just pushed) still holds.
    /// </summary>
    public static IReadOnlySet<string> AwaitingTickets(JsonStore store)
    {
        var awaiting = LoadAwaiting(store);
        if (awaiting.Count == 0) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return AwaitingTickets(awaiting, Load(store), LoadTests(store));
    }

    /// <summary>The awaiting tickets of <paramref name="awaiting"/>, read against the builds and the local runs.</summary>
    public static IReadOnlySet<string> AwaitingTickets(IReadOnlyDictionary<string, AwaitingState> awaiting,
        IReadOnlyDictionary<string, Dictionary<string, BranchState>> state, IReadOnlyDictionary<string, Dictionary<string, TestState>> tests)
    {
        var returned = ReturnedToWork(state, tests);
        return awaiting.Where(kv =>
            {
                var a = kv.Value;
                if (returned.Any(r => string.Equals(r.Repo, a.Repo, StringComparison.OrdinalIgnoreCase) && string.Equals(r.Branch, a.Branch, StringComparison.Ordinal)))
                    return false;
                return StateOf(state, a.Repo, a.Branch) is not { Building: null, Ok: false } st || !SameHead(st.Built, st.Head);
            })
            .Select(kv => kv.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>What the work unit is told when its branch's head failed to build on the host; null when it didn't.</summary>
    public static string? BuildBrief(IReadOnlyDictionary<string, Dictionary<string, BranchState>> state, string repo, string branch) =>
        StateOf(state, repo, branch) is { Building: null, Ok: false } st && SameHead(st.Built, st.Head)
            ? $"The host's build of {repo}'s {branch} failed at {Short(st.Head)}:" + (string.IsNullOrWhiteSpace(st.Note) ? " no errors were named." : "\n" + st.Note.Trim()) +
              $"\nKeep working it on that same branch: fix it there and push a new head to origin/{branch}, not to main and not on a new branch."
            : null;

    private static BranchState? StateOf(IReadOnlyDictionary<string, Dictionary<string, BranchState>> state, string repo, string branch) =>
        state.FirstOrDefault(r => string.Equals(r.Key, repo, StringComparison.OrdinalIgnoreCase)).Value is { } branches && branches.TryGetValue(branch, out var st) ? st : null;

    /// <summary>
    /// The host's merge of <paramref name="branch"/> settled (M.ORCH-T1661): merged into main as <paramref name="merged"/>, its
    /// ticket is closed with that commit as its ref; a merge that was refused for good at that head (<paramref name="rejected"/>, a
    /// conflict with main) or a branch gone from origin hands its ticket back with a note. Either way the ticket's awaiting mark is
    /// cleared. The ticket database is written only when <paramref name="workspaceRoot"/> names one; returns what couldn't be done.
    /// </summary>
    public static string? SettleMerge(JsonStore store, string? workspaceRoot, string repo, string branch, string? merged, string? note, bool rejected, bool gone)
    {
        if (merged is null && !rejected && !gone) return null; // to be tried again, or the branch moved: its new head is still awaited
        var ticket = WorkUnitOf(branch);
        if (ticket is null) return null;
        ClearAwaiting(store, ticket);
        if (workspaceRoot is null) return null;
        try
        {
            using var db = MindAttic.Tickets.Data.TicketsDb.OpenIfMigrated(workspaceRoot);
            if (db is null) return null;
            if (db.Ticket(ticket) is not { } t) return $"there's no ticket {ticket} for {repo}'s {branch}";
            if (merged is not null)
            {
                if (t.Ticket.Status == MindAttic.Tickets.Log.TicketRules.Open) db.Close(t.Ticket.Id, MindAttic.Tickets.Log.TicketRules.Closed, merged);
            }
            else
            {
                db.AddNote(t.Ticket.Id, NoteAuthor, gone
                    ? $"{repo}'s {branch} is gone from origin before the host could merge it, so this ticket is handed out again: carry on from main."
                    : $"The host couldn't merge {repo}'s {branch} into main: {note}\nKeep working it on that same branch: merge main into it, fix that, and push a new head to origin/{branch}.");
            }
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException or AccessViolationException))
        {
            return $"couldn't update {ticket} in the ticket database: {ex.Message}";
        }
        TicketWorking.Clear(workspaceRoot, ticket);
        return null;
    }
}
