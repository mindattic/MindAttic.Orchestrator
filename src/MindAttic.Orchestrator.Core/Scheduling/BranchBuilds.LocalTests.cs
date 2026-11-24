using System.Text.RegularExpressions;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Tickets;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// The local test run's results on the host's work branches (M.ORCH-T1637, a M.ORCH-T1501 follow-up). The user's machine runs
/// a branch's suite at a head the host built clean and reports pass or fail for that head (<see cref="RecordTest"/>,
/// <c>orchestrator branches report</c>). A failing run is not abandoned or re-ticketed: the branch goes back to the work unit
/// that produced it (its ticket, named by the branch, <c>work/&lt;ticket&gt;</c>), which keeps iterating on that same branch
/// (<see cref="ReturnedToWork"/>, <see cref="Brief"/>, <see cref="BranchFor"/>). Nothing here holds any other branch: each
/// branch's result is its own, so the rest go on building and being tested meanwhile. A head that was tested drops out of
/// <see cref="ReadyToTest(IReadOnlyDictionary{string, Dictionary{string, BranchState}}, IReadOnlyDictionary{string, Dictionary{string, TestState}})"/>;
/// once the work unit pushes a new head, the host builds it and it's queued for a local run again. The results are kept apart
/// from the builds' own record (<see cref="TestsName"/>), so a report never races the daemon's build bookkeeping.
/// </summary>
public sealed partial class BranchBuilds
{
    /// <summary>The store's record of each work branch's last local test run.</summary>
    public const string TestsName = "branch-tests";

    /// <summary>Who a ticket note about a returned branch is from.</summary>
    public const string NoteAuthor = "orchestrator (branch tests)";

    /// <summary>A work branch's last local test run: the head it ran at, whether it passed, why not, and how many runs of the
    /// branch have failed so far (<see cref="Rounds"/>); <see cref="ReturnedTo"/> the ticket a failure went back to.</summary>
    public sealed class TestState
    {
        /// <summary>Gets or sets the head the run tested.</summary>
        public string Head { get; set; } = "";
        /// <summary>Gets or sets whether the run passed.</summary>
        public bool Ok { get; set; }
        /// <summary>Gets or sets what failed (its first failures), or null.</summary>
        public string? Note { get; set; }
        /// <summary>Gets or sets when the run was reported.</summary>
        public DateTime? TestedUtc { get; set; }
        /// <summary>Gets or sets how many of the branch's local runs have failed.</summary>
        public int Rounds { get; set; }
        /// <summary>Gets or sets the ticket (the work unit) a failing run went back to; null when the branch names none.</summary>
        public string? ReturnedTo { get; set; }
    }

    /// <summary>A branch whose local run failed at its head and is back with the work unit that produced it.</summary>
    public sealed record Returned(string Repo, string Branch, string Head, string? Ticket, string? Note, int Rounds, DateTime? TestedUtc);

    /// <summary>What became of a local test run the user reported for a work branch.</summary>
    public enum ReportOutcome
    {
        /// <summary>It passed at the branch's head: signed off, waiting for the merge.</summary>
        Passed,
        /// <summary>It failed at the branch's head: the branch is back with its work unit.</summary>
        SentBack,
        /// <summary>The branch moved since that head was built: the result is for a head nobody works on any more, dropped.</summary>
        Stale,
        /// <summary>The host knows no such work branch.</summary>
        Unknown,
        /// <summary>The store couldn't be read now; nothing was recorded.</summary>
        Unreadable,
    }

    /// <summary>A report's outcome, a line saying it, and the branch's ticket (when it names one).</summary>
    public sealed record Report(ReportOutcome Outcome, string Text, string? Ticket = null, string? Head = null);

    /// <summary>The store's record of the local runs; empty when there's none or it can't be read.</summary>
    public static Dictionary<string, Dictionary<string, TestState>> LoadTests(JsonStore store) =>
        store.TryLoad<Dictionary<string, Dictionary<string, TestState>>>(TestsName, out var tests) ? tests : [];

    /// <summary>The ticket a work branch is for (<c>work/M.ORCH-T1637</c>, or with a suffix after it, <c>work/M.ORCH-T1637-retry</c>);
    /// null when the branch names none.</summary>
    public static string? WorkUnitOf(string branch)
    {
        var m = TicketInBranch().Match(branch);
        return m.Success ? m.Groups["id"].Value : null;
    }

    /// <summary>The queue for the user's local test run: every branch built clean at its head whose head hasn't been tested yet.
    /// A failed head is with its work unit until it pushes a new one; a passed head waits for the merge.</summary>
    public static IReadOnlyList<Ready> ReadyToTest(IReadOnlyDictionary<string, Dictionary<string, BranchState>> state,
        IReadOnlyDictionary<string, Dictionary<string, TestState>> tests) =>
        [.. ReadyToTest(state).Where(r => TestOf(tests, r.Repo, r.Branch) is not { } t || !SameHead(t.Head, r.Head))];

    /// <summary>Every branch whose last local run failed at the head it still stands at, oldest failure first: the work units'
    /// own queue, each to keep iterating on its branch. A branch that moved since (a fix pushed) or is gone isn't here.</summary>
    public static IReadOnlyList<Returned> ReturnedToWork(IReadOnlyDictionary<string, Dictionary<string, BranchState>> state,
        IReadOnlyDictionary<string, Dictionary<string, TestState>> tests) =>
        [.. tests.SelectMany(r => r.Value.Where(b => !b.Value.Ok
                    && state.TryGetValue(r.Key, out var branches) && branches.TryGetValue(b.Key, out var st) && SameHead(st.Head, b.Value.Head))
                .Select(b => new Returned(r.Key, b.Key, b.Value.Head, b.Value.ReturnedTo ?? WorkUnitOf(b.Key), b.Value.Note, b.Value.Rounds, b.Value.TestedUtc)))
            .OrderBy(r => r.TestedUtc ?? DateTime.MinValue).ThenBy(r => r.Repo, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Branch, StringComparer.Ordinal)];

    /// <summary>Every branch whose local run passed at the head it still stands at: signed off, waiting for the host's merge.</summary>
    public static IReadOnlyList<Ready> SignedOff(IReadOnlyDictionary<string, Dictionary<string, BranchState>> state,
        IReadOnlyDictionary<string, Dictionary<string, TestState>> tests) =>
        [.. tests.SelectMany(r => r.Value.Where(b => b.Value.Ok
                    && state.TryGetValue(r.Key, out var branches) && branches.TryGetValue(b.Key, out var st) && SameHead(st.Head, b.Value.Head))
                .Select(b => new Ready(r.Key, b.Key, b.Value.Head, b.Value.TestedUtc)))
            .OrderBy(r => r.BuiltUtc ?? DateTime.MinValue).ThenBy(r => r.Repo, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Branch, StringComparer.Ordinal)];

    /// <summary>The work branch <paramref name="ticket"/> already has in <paramref name="repo"/>, so its next round goes on that same
    /// branch instead of a new one; null when it has none.</summary>
    public static string? BranchFor(IReadOnlyDictionary<string, Dictionary<string, BranchState>> state, string repo, string ticket) =>
        state.FirstOrDefault(r => string.Equals(r.Key, repo, StringComparison.OrdinalIgnoreCase)).Value?.Keys
            .Where(b => string.Equals(WorkUnitOf(b), ticket, StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal).FirstOrDefault();

    /// <summary>What the work unit is told when its branch comes back: what failed, and to keep going on that same branch.</summary>
    public static string Brief(Returned r) =>
        $"The local test run of {r.Repo}'s {r.Branch} failed at {Short(r.Head)}" + (r.Rounds > 1 ? $" (failed run {r.Rounds} of this branch)" : "") + ":" +
        (string.IsNullOrWhiteSpace(r.Note) ? " no failures were named." : "\n" + r.Note.Trim()) +
        $"\nKeep working {r.Ticket ?? "it"} on that same branch: fix it there and push a new head to origin/{r.Branch}, not to main and not on a new branch." +
        " The host builds the new head and queues it for a local test run again; other branches go on meanwhile.";

    /// <summary>
    /// Records a local test run of <paramref name="repo"/>'s <paramref name="branch"/> at <paramref name="head"/> (the full hash, or
    /// at least its first 7 characters). Taken only for the head the branch still stands at; a result for an older head is
    /// dropped (<see cref="ReportOutcome.Stale"/>: the work unit already moved on, its new head gets its own run). The same result
    /// reported twice for one head counts once.
    /// </summary>
    public static Report RecordTest(JsonStore store, string repo, string branch, string head, bool ok, string? note, DateTime nowUtc)
    {
        head = head.Trim();
        if (!store.TryLoad<Dictionary<string, Dictionary<string, BranchState>>>(StateName, out var state))
            return new Report(ReportOutcome.Unreadable, "the host's record of its work branches couldn't be read now; nothing was recorded, report it again");
        var repoKey = state.Keys.FirstOrDefault(k => string.Equals(k, repo, StringComparison.OrdinalIgnoreCase));
        if (repoKey is null || !state[repoKey].TryGetValue(branch, out var st))
            return new Report(ReportOutcome.Unknown, $"the host knows no work branch {branch} in {repo}");
        var ticket = WorkUnitOf(branch);
        if (head.Length < 7 || !SameHead(st.Head, head))
            return new Report(ReportOutcome.Stale, $"{repo}'s {branch} is at {Short(st.Head)} now, not {Short(head)}: that result is dropped, and its new head gets a local run of its own once it's built", ticket, st.Head);

        Dictionary<string, Dictionary<string, TestState>> tests;
        if (!store.TryLoad(TestsName, out tests))
            return new Report(ReportOutcome.Unreadable, "the host's record of local test runs couldn't be read now; nothing was recorded, report it again", ticket, st.Head);

        if (!tests.TryGetValue(repoKey, out var branches)) branches = tests[repoKey] = new Dictionary<string, TestState>(StringComparer.Ordinal);
        branches.TryGetValue(branch, out var was);
        var again = was is not null && SameHead(was.Head, st.Head) && was.Ok == ok;
        var now = new TestState
        {
            Head = st.Head,
            Ok = ok,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            TestedUtc = again ? was!.TestedUtc : nowUtc,
            Rounds = (was?.Rounds ?? 0) + (ok || again ? 0 : 1),
            ReturnedTo = ok ? null : ticket,
        };
        branches[branch] = now;
        // Branches the host has forgotten (merged and deleted, or dropped) take their results with them.
        foreach (var (r, bs) in tests.ToList())
        {
            var known = state.TryGetValue(r, out var s) ? s : null;
            foreach (var gone in bs.Keys.Where(b => known is null || !known.ContainsKey(b)).ToList()) bs.Remove(gone);
            if (bs.Count == 0) tests.Remove(r);
        }
        store.Save(TestsName, tests);

        return ok
            ? new Report(ReportOutcome.Passed, $"{repo}'s {branch} passed its local test run at {Short(st.Head)}: signed off, waiting for the merge to main", ticket, st.Head)
            : new Report(ReportOutcome.SentBack, $"{repo}'s {branch} failed its local test run at {Short(st.Head)}" + (now.Rounds > 1 ? $" (failed run {now.Rounds})" : "") +
                $": back to {(ticket is null ? "the work unit that produced it" : ticket)}, to keep iterating on that same branch; other branches aren't held", ticket, st.Head);
    }

    /// <summary>
    /// Hands a returned branch back to its work unit in the ticket database: a note on its ticket with what failed and to keep
    /// going on the same branch (<see cref="Brief"/>), and the ticket's working claim cleared so it's handed out again. Best
    /// effort; returns what couldn't be done (null when it all was, or there's no ticket database to write to). The ticket's
    /// awaiting mark is cleared in <paramref name="store"/> (M.ORCH-T1661): it stayed open while its branch waited, and now
    /// it's handed out again.
    /// </summary>
    public static string? SendBack(string? workspaceRoot, Returned r, JsonStore? store = null)
    {
        if (r.Ticket is null) return $"{r.Branch} names no ticket (work/<ticket>), so no ticket was told; its work unit picks it up from orchestrator branches";
        if (store is not null) ClearAwaiting(store, r.Ticket);
        try
        {
            using var db = MindAttic.Tickets.Data.TicketsDb.OpenIfMigrated(workspaceRoot);
            if (db is null) return null;
            if (db.Ticket(r.Ticket) is not { } t) return $"there's no ticket {r.Ticket} to send {r.Branch} back to";
            db.AddNote(t.Ticket.Id, NoteAuthor, Brief(r));
            if (t.Ticket.Status != MindAttic.Tickets.Log.TicketRules.Open)
                // Only a branch ticket closed before M.ORCH-T1661 kept them open until the merge.
                return $"{t.Ticket.Id} is {t.Ticket.Status}: the failure is noted on it, but it must be open again to be worked";
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException or AccessViolationException))
        {
            return $"couldn't note {r.Ticket} in the ticket database: {ex.Message}";
        }
        TicketWorking.Clear(workspaceRoot, r.Ticket);
        return null;
    }

    private static TestState? TestOf(IReadOnlyDictionary<string, Dictionary<string, TestState>> tests, string repo, string branch) =>
        tests.TryGetValue(repo, out var branches) && branches.TryGetValue(branch, out var t) ? t : null;

    /// <summary>Whether two hashes name the same commit: equal, or one a prefix (at least 7 characters) of the other.</summary>
    private static bool SameHead(string? a, string? b) =>
        !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && Math.Min(a.Length, b.Length) >= 7
        && (a.StartsWith(b, StringComparison.OrdinalIgnoreCase) || b.StartsWith(a, StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex(@"^work/(?<id>[A-Za-z][A-Za-z0-9.]*-T\d+)(?=$|[^0-9])")]
    private static partial Regex TicketInBranch();
}
