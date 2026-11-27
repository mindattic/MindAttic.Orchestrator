using System.ComponentModel;
using System.Data.Common;
using System.Diagnostics;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Activity;

/// <summary>What the sweeper knows of a ticket that owns a ledger entry: whether the ticket database has it, whether it's
/// closed (or wont-do), whether someone still holds it (an assignment, or a login or session recorded as working it), and
/// whether a commit naming it is on origin's main line.</summary>
public sealed record TicketFacts(bool Exists, bool Closed, bool Claimed, bool Landed);

/// <summary>Whether a process runs (null: can't be told) and, when it does and can be read, when it started.</summary>
public sealed record ProcessFact(bool? Running, DateTime? StartedUtc);

/// <summary>One thing the ledger sweep judged: where it came from (<see cref="LedgerSweeper.FromLedger"/>,
/// <see cref="LedgerSweeper.Leftover"/> or <see cref="LedgerSweeper.BuildOutput"/>), its size, whether it goes, and why.</summary>
public sealed record SweepItem(string Source, string Kind, string Path, string Owner, long Bytes, bool Delete, string Why);

/// <summary>A ledger sweep: every item's verdict, the bytes that went (in a dry run, would), and what couldn't be done.</summary>
public sealed record LedgerSweepResult(IReadOnlyList<SweepItem> Items, long Bytes, IReadOnlyList<string> Errors, bool DryRun)
{
    /// <summary>The items that went (in a dry run, would).</summary>
    public IReadOnlyList<SweepItem> Going => [.. Items.Where(i => i.Delete)];
}

/// <summary>Where the sweeper learns whether a ledger entry's owner is alive: the ticket database and git for a ticket (null:
/// can't be read now, so the entry is kept), <see cref="SessionLiveness"/> for a session, the process table for a pid.</summary>
public sealed record LedgerOwners(Func<string, TicketFacts?> Ticket, SessionLiveness Liveness, Func<int, ProcessFact> Process)
{
    /// <summary>Owners as the machine has them now: tickets from the ticket database under <paramref name="workspaceRoot"/>
    /// (each read once per sweep), claims from <paramref name="store"/>'s assignments and the database's working sessions, and
    /// "landed" from the repo's own <c>origin/&lt;main line&gt;</c> as it was last fetched (never fetched here: a commit not seen
    /// yet only makes the entry wait).</summary>
    public static LedgerOwners Live(JsonStore store, string? workspaceRoot, SessionLiveness liveness)
    {
        HashSet<string>? assigned = null;
        var read = new Dictionary<string, TicketFacts?>(StringComparer.OrdinalIgnoreCase);
        return new LedgerOwners(id => read.TryGetValue(id, out var known) ? known : read[id] = Read(id), liveness, ProcessNow);

        TicketFacts? Read(string id)
        {
            try
            {
                assigned ??= store.LoadAssignments().Select(a => a.TicketId).ToHashSet(StringComparer.OrdinalIgnoreCase);
                using var db = MindAttic.Tickets.Data.TicketsDb.OpenIfMigrated(workspaceRoot);
                if (db is null) return null;
                if (db.Ticket(id) is not { } found) return new TicketFacts(false, false, assigned.Contains(id), false);
                var closed = found.Ticket.Status is "closed" or "wont-do";
                var claimed = assigned.Contains(id) || (db.WorkStates().TryGetValue(id, out var work) && work.IsWorking);
                var landed = closed && workspaceRoot is { Length: > 0 } && Landed(Path.Combine(workspaceRoot, found.Repo), id);
                return new TicketFacts(true, closed, claimed, landed);
            }
            catch (Exception ex) when (ex is DbException or IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
            {
                return null; // the database busy or down: unknown, so kept, and read again at the next sweep
            }
        }
    }

    /// <summary>Whether a commit whose message names <paramref name="ticketId"/> the way every commit does (<c>(ID)</c>) is on
    /// <paramref name="repoPath"/>'s <c>origin/&lt;main line&gt;</c>.</summary>
    internal static bool Landed(string repoPath, string ticketId)
    {
        if (!Directory.Exists(repoPath)) return false;
        var (code, output) = Dispatcher.Git(repoPath, "log", "-1", "--format=%H", "--fixed-strings", $"--grep=({ticketId})",
            $"origin/{Dispatcher.Mainline(repoPath)}");
        return code == 0 && output.Trim().Length > 0;
    }

    /// <summary>Process <paramref name="pid"/> as the machine has it now.</summary>
    internal static ProcessFact ProcessNow(int pid)
    {
        try
        {
            using var p = System.Diagnostics.Process.GetProcessById(pid);
            return p.HasExited ? new ProcessFact(false, null) : new ProcessFact(true, p.StartTime.ToUniversalTime());
        }
        catch (ArgumentException) { return new ProcessFact(false, null); } // no such process
        catch (InvalidOperationException) { return new ProcessFact(false, null); } // it exited while it was read
        catch (Win32Exception) { return new ProcessFact(true, null); } // it runs, but isn't ours to read
    }
}

/// <summary>What a ledger sweep runs with: the two roots it may delete under, whether it only says what it would do, and its
/// ages and windows (each an <see cref="OrchestratorSettings"/> setting, see <see cref="From"/>).</summary>
public sealed record LedgerSweepOptions
{
    /// <summary>The worktree root: ticket scratch dirs are under its <c>.scratch</c>, the daemon's worktrees under each repo's folder.</summary>
    public required string WorktreeRoot { get; init; }

    /// <summary>The temp dir temp files are recorded under and crash leftovers are looked for in (its top level only).</summary>
    public required string TempDir { get; init; }

    /// <summary>Delete nothing, release nothing: only judge (and, with <see cref="LogPath"/>, write what would go).</summary>
    public bool DryRun { get; init; } = true;

    /// <summary><see cref="OrchestratorSettings.LedgerSweepQuietMinutes"/>.</summary>
    public double QuietMinutes { get; init; } = 30;

    /// <summary><see cref="OrchestratorSettings.LedgerSweepLeftoverHours"/>; 0 skips the leftovers.</summary>
    public double LeftoverHours { get; init; } = 24;

    /// <summary><see cref="OrchestratorSettings.LedgerSweepBuildOutputHours"/>; 0 skips the worktrees' bin/obj.</summary>
    public double BuildOutputHours { get; init; } = 72;

    /// <summary><see cref="OrchestratorSettings.LedgerSweepHistoryDays"/>; 0 keeps released rows.</summary>
    public double HistoryDays { get; init; } = 30;

    /// <summary>Where each deletion (in a dry run, each thing that would go) is written; null writes nothing.</summary>
    public string? LogPath { get; init; }

    /// <summary>The paths a dry run already wrote to the log: one that's there isn't written again (the daemon keeps it, so the
    /// same plan isn't logged every ten minutes).</summary>
    public ISet<string>? Logged { get; init; }

    /// <summary>Whether a build or test is known to run in a daemon worktree (the caller's own knowledge; the sweep also looks
    /// for processes naming it and open binaries).</summary>
    public Func<string, bool>? Busy { get; init; }

    /// <summary>The options <paramref name="s"/> sets; <paramref name="dryRun"/> overrides its dry-run setting.</summary>
    public static LedgerSweepOptions From(OrchestratorSettings s, string? tempDir = null, bool? dryRun = null) => new()
    {
        WorktreeRoot = s.WorktreeRoot,
        TempDir = tempDir ?? Path.GetTempPath(),
        DryRun = dryRun ?? s.LedgerSweepDryRun,
        QuietMinutes = s.LedgerSweepQuietMinutes,
        LeftoverHours = s.LedgerSweepLeftoverHours,
        BuildOutputHours = s.LedgerSweepBuildOutputHours,
        HistoryDays = s.LedgerSweepHistoryDays,
    };
}

/// <summary>
/// The ledger sweeper (roadmap Phase 4, M.ORCH-T1677), on the resource ledger (<see cref="ResourceLedger"/>, M.ORCH-T1676). On
/// daemon start and at the session-temp prune's cadence it reclaims:
/// <list type="bullet">
/// <item>ledger entries (scratch dirs, temp files) whose owner is dead or finished (<see cref="Judge"/>): the files go, then the
/// row is marked released, and a released row whose folder is gone is dropped after
/// <see cref="OrchestratorSettings.LedgerSweepHistoryDays"/>. Delete only what is proven delivered: a ticket's scratch goes once
/// its ticket is closed and a commit naming it is on origin's main line, or once its claim was handed back; anything else waits
/// for its lease to run out. A ticket still claimed keeps its scratch whatever its lease says;</item>
/// <item>crash leftovers no ledger row covers, by name, in the temp dir's top level (<see cref="LeftoverPatterns"/>), once older
/// and quiet for <see cref="OrchestratorSettings.LedgerSweepLeftoverHours"/>;</item>
/// <item>the <c>bin</c>/<c>obj</c> folders (only ones beside a project file) of the daemon's own worktrees
/// (<see cref="IsDaemonTree"/>), once nothing in them was written for <see cref="OrchestratorSettings.LedgerSweepBuildOutputHours"/>
/// and no build or test runs there. The worktree itself is never deleted.</item>
/// </list>
/// Never deleted, whatever its owner's state (<see cref="Refuse"/> and the probes <see cref="SessionTempPruner"/> uses): a
/// worktree, anything outside the scratch root and the temp dir, anything at or behind a junction or symlink, anything a running
/// process names on its command line, anything with a file held open, and anything written within
/// <see cref="OrchestratorSettings.LedgerSweepQuietMinutes"/>. Links inside a folder are removed as links, never followed. It
/// ships dry: <see cref="OrchestratorSettings.LedgerSweepDryRun"/> is on by default, so it only writes what it would delete.
/// </summary>
public static class LedgerSweeper
{
    /// <summary>A <see cref="SweepItem.Source"/>: a row of the resource ledger.</summary>
    public const string FromLedger = "ledger";

    /// <summary>A <see cref="SweepItem.Source"/>: a crash leftover found by name in the temp dir.</summary>
    public const string Leftover = "leftover";

    /// <summary>A <see cref="SweepItem.Source"/>: the stale bin/obj of one daemon worktree.</summary>
    public const string BuildOutput = "build-output";

    /// <summary>The crash leftovers swept by name: made and deleted in a try/finally (DiffReview's prompt file, LoginTokens'
    /// scratch config folder), so a crash orphans them. Both register in the ledger too (<see cref="LedgerTemp"/>); these
    /// patterns catch what a crash left before that, or while the database was busy.</summary>
    public static readonly IReadOnlyList<(string Pattern, bool Folder)> LeftoverPatterns =
        [("orchestrator-review-*.txt", false), ("mindattic-token-check-*", true)];

    /// <summary>The daemon's bisect worktree (MainlineTests.BisectWorktreeName).</summary>
    internal const string BisectTreeName = "main-bisect";

    private static readonly TimeSpan PidSlack = TimeSpan.FromSeconds(2);

    /// <summary>Whether <paramref name="name"/> is one of the daemon's own worktrees under <c>WorktreeRoot\&lt;repo&gt;</c>:
    /// main-tests, main-bisect, local-tests, merge-main, milestone-build, feature-gate, or a branch build's (<c>branch-*</c>). A ticket's old
    /// worktree or a work-&lt;ticket&gt; one isn't: agents work there.</summary>
    public static bool IsDaemonTree(string name) =>
        name.Equals(MainlineTests.WorktreeName, StringComparison.OrdinalIgnoreCase)
        || name.Equals(MilestoneAcceptance.WorktreeName, StringComparison.OrdinalIgnoreCase)
        || name.Equals(BisectTreeName, StringComparison.OrdinalIgnoreCase)
        || name.Equals(BranchTestRun.WorktreeName, StringComparison.OrdinalIgnoreCase)
        || name.Equals(BranchMerges.MergeTreeName, StringComparison.OrdinalIgnoreCase)
        || name.Equals(FeatureGate.WorktreeName, StringComparison.OrdinalIgnoreCase)
        || name.StartsWith(BranchBuilds.WorktreeName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The selection rule, pure: whether <paramref name="r"/>'s owner is dead or finished, so the entry may go, and why.
    /// <list type="bullet">
    /// <item>A worktree is never the sweeper's to delete.</item>
    /// <item>A ticket's: kept while the ticket's state can't be read, and while it's claimed and open. It goes when the ticket is
    /// closed and its commit is on origin's main line (delivered); when its claim was let go with the ticket still open (handed
    /// back); else when its lease has run out; a lease-less one when released.</item>
    /// <item>Anyone else's goes once released. A session's goes when the session has ended, a process's when the process is gone
    /// (or its pid is another process now); alive, it's kept whatever its lease; when that can't be told, it waits for its lease.</item>
    /// <item>The daemon's, and one with an owner the sweeper doesn't know, waits for its lease.</item>
    /// </list>
    /// </summary>
    public static (bool Go, string Why) Judge(Resource r, DateTime nowUtc, LedgerOwners owners)
    {
        if (r.Kind == ResourceKind.Worktree) return (false, "kept: a worktree is a git checkout that can hold work; only its stale bin/obj go");
        var lease = r.ExpiresUtc is { } e
            ? r.IsExpired(nowUtc) ? $"its lease ran out {Span(nowUtc - e)} ago" : $"its lease runs {Span(e - nowUtc)} more"
            : "it has no lease";
        if (Owned(r.Owner, "ticket:") is { } ticket)
        {
            if (owners.Ticket(ticket) is not { } t) return (false, $"kept: {ticket}'s state can't be read from the ticket database now");
            if (t.Closed && t.Landed) return (true, $"delivered: {ticket} is closed and its commit is on origin's main line");
            if (t.Claimed) return (false, $"kept: {ticket} is still claimed");
            if (r.IsReleased && !t.Closed) return (true, $"handed back: {ticket}'s claim ended and nobody holds it");
            var state = t.Closed ? $"{ticket} is closed but no commit naming it is on origin's main line"
                : t.Exists ? $"{ticket} is no longer claimed" : $"{ticket} isn't in the ticket database";
            if (r.IsExpired(nowUtc)) return (true, $"{state}, and {lease}");
            if (r.ExpiresUtc is null && r.IsReleased) return (true, $"{state}, and it was released with no lease");
            return (false, $"kept: {state}; {lease}");
        }
        if (r.IsReleased) return (true, "released by its owner");
        if (Owned(r.Owner, "session:") is { } session)
        {
            var v = owners.Liveness.Running(session);
            if (v.Running is true) return (false, $"kept: its session runs ({v.Why})");
            if (v.Running is false) return (true, $"its session ended: {v.Why}");
            return r.IsExpired(nowUtc) ? (true, $"can't tell if its session ended ({v.Why}), and {lease}")
                : (false, $"kept: can't tell if its session ended ({v.Why}); {lease}");
        }
        if (ResourceLedger.ParseProcessOwner(r.Owner) is { } p)
        {
            var now = owners.Process(p.Pid);
            if (now.Running is false) return (true, $"its process (pid {p.Pid}) is gone");
            if (now.Running is true)
                return p.StartedUtc is { } recorded && now.StartedUtc is { } started && (started - recorded).Duration() > PidSlack
                    ? (true, $"its process is gone: pid {p.Pid} is another process now (started {started:u})")
                    : (false, $"kept: its process (pid {p.Pid}) runs");
            return r.IsExpired(nowUtc) ? (true, $"can't tell if pid {p.Pid} runs, and {lease}") : (false, $"kept: can't tell if pid {p.Pid} runs; {lease}");
        }
        var whose = r.Owner == ResourceLedger.DaemonOwner ? "the daemon's" : $"owner {r.Owner} isn't one the sweeper knows";
        return r.IsExpired(nowUtc) ? (true, $"{whose}, and {lease}") : (false, $"kept: {whose}; {lease}");
    }

    /// <summary>
    /// Why <paramref name="path"/> must never be deleted, or null when it may be: it has to sit strictly under one of
    /// <paramref name="roots"/> (the scratch root and the temp dir; a drive root is never one), and neither it nor any folder
    /// between it and that root may be a junction or symlink.
    /// </summary>
    public static string? Refuse(string path, IReadOnlyList<string> roots)
    {
        string full;
        try { full = ResourceLedger.Normalize(path); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException) { return "its path can't be read"; }
        var root = roots.Select(SafeRoot).FirstOrDefault(r => r is not null && IsUnder(full, r));
        if (root is null) return "it's outside the scratch root and the temp dir";
        for (var at = full; at is not null && at.Length > root.Length; at = Path.GetDirectoryName(at))
            if (IsLink(at)) return $"{at} is a junction or symlink";
        return null;
    }

    /// <summary>Whether <paramref name="full"/> is strictly below <paramref name="root"/> (both normalized).</summary>
    internal static bool IsUnder(string full, string root) =>
        full.Length > root.Length + 1 && full.StartsWith(root, StringComparison.OrdinalIgnoreCase)
        && full[root.Length] is var c && (c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar);

    private static string? SafeRoot(string root)
    {
        try
        {
            var full = ResourceLedger.Normalize(root);
            // A drive root (or a share's) is never a root to delete under.
            return full.Length > (Path.GetPathRoot(full)?.Length ?? 0) ? full : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException) { return null; }
    }

    private static bool IsLink(string path)
    {
        try { return File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint); }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { return false; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return true; } // can't tell: treated as one
    }

    /// <summary>The roots a sweep with <paramref name="o"/> may delete under: the ticket scratch root and the temp dir.</summary>
    public static IReadOnlyList<string> Roots(LedgerSweepOptions o) => [Path.Combine(o.WorktreeRoot, TicketScratch.FolderName), o.TempDir];

    /// <summary>
    /// One sweep: the ledger's rows (<paramref name="ledger"/> null skips them), then the crash leftovers, then the daemon
    /// worktrees' stale bin/obj. With <see cref="LedgerSweepOptions.DryRun"/> nothing is deleted, released or dropped.
    /// </summary>
    public static LedgerSweepResult Sweep(ResourceLedger? ledger, LedgerOwners owners, LedgerSweepOptions o, DateTime? nowUtc = null)
    {
        var run = new Run(owners.Liveness, o, nowUtc ?? DateTime.UtcNow);
        var recorded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (ledger is not null) SweepRows(ledger, owners, run, recorded);
        SweepLeftovers(run, recorded);
        SweepBuildOutputs(run);
        return new LedgerSweepResult(run.Items, run.Bytes, run.Errors, o.DryRun);
    }

    private sealed class Run(SessionLiveness liveness, LedgerSweepOptions o, DateTime now)
    {
        public SessionLiveness Liveness => liveness;
        public LedgerSweepOptions Options => o;
        public DateTime Now => now;
        public List<SweepItem> Items { get; } = [];
        public List<string> Errors { get; } = [];
        public long Bytes { get; set; }

        public void Keep(string source, string kind, string path, string owner, long bytes, string why) =>
            Items.Add(new SweepItem(source, kind, path, owner, bytes, false, why));

        /// <summary>Records that <paramref name="item"/> went (or would), and writes it to the log.</summary>
        public void Went(SweepItem item)
        {
            Items.Add(item);
            Bytes += item.Bytes;
            if (o.LogPath is null) return;
            if (o.DryRun && o.Logged is { } seen && !seen.Add(item.Path)) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(o.LogPath)!);
                File.AppendAllText(o.LogPath, $"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}\t{item.Bytes}\t" +
                    $"{(o.DryRun ? "would delete (ledger sweep, dry run)" : "ledger sweep")}: {item.Why}\t{item.Path}{Environment.NewLine}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    private static void SweepRows(ResourceLedger ledger, LedgerOwners owners, Run run, HashSet<string> recorded)
    {
        var o = run.Options;
        IReadOnlyList<Resource> rows;
        try { rows = ledger.All(); }
        catch (Exception ex) when (ex is DbException or IOException or UnauthorizedAccessException)
        {
            run.Errors.Add($"the resource ledger couldn't be read: {ex.Message}");
            return;
        }
        var roots = Roots(o);
        foreach (var r in rows)
        {
            recorded.Add(r.Path);
            try
            {
                var (go, why) = Judge(r, run.Now, owners);
                if (!Directory.Exists(r.Path) && !File.Exists(r.Path))
                {
                    // Nothing on disk (never made, or its owner cleaned up): an ended owner's row is released; a released one
                    // is history, dropped once it's old enough.
                    if (o.DryRun) continue;
                    if (go && !r.IsReleased) ledger.Release(r.Kind, r.Path, run.Now);
                    else if (r.ReleasedUtc is { } at && o.HistoryDays > 0 && run.Now - at > TimeSpan.FromDays(o.HistoryDays)) ledger.Forget(r.Kind, r.Path);
                    continue;
                }
                if (go && Refuse(r.Path, roots) is { } never) (go, why) = (false, $"kept, never deleted: {never} ({why})");
                if (!Reclaim(run, FromLedger, r.Kind, r.Path, r.Owner, go, why, TimeSpan.FromMinutes(Math.Max(0, o.QuietMinutes)))) continue;
                if (!o.DryRun && !r.IsReleased) ledger.Release(r.Kind, r.Path, run.Now);
            }
            catch (Exception ex) when (ex is DbException or IOException or UnauthorizedAccessException)
            {
                run.Errors.Add($"{r.Path}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Sizes <paramref name="path"/> (a folder or a file; never through a link) and, when <paramref name="go"/>, deletes it
    /// unless something still uses it: written within <paramref name="quiet"/>, named on a running process's command line, or
    /// a file in it open. True when it went (in a dry run: would).
    /// </summary>
    private static bool Reclaim(Run run, string source, string kind, string path, string owner, bool go, string why, TimeSpan quiet)
    {
        var folder = Directory.Exists(path);
        // A link is never sized or walked: its target isn't the entry's.
        var link = IsLink(path);
        var files = link ? [] : folder ? new DirectoryInfo(path).GetFiles("*", SessionTempPruner.Walk) : [new FileInfo(path)];
        var size = files.Sum(f => f.Length);
        if (!go || link)
        {
            run.Keep(source, kind, path, owner, size, link && go ? $"kept, never deleted: it's a junction or symlink ({why})" : why);
            return false;
        }
        var newest = SessionTempPruner.Newest(folder ? SessionTempPruner.Tree(path, files) : files);
        if (quiet > TimeSpan.Zero && run.Now - newest < quiet)
        {
            run.Keep(source, kind, path, owner, size, $"kept: {why}, but something in it was written {Span(run.Now - newest)} ago (it goes once quiet for {Span(quiet)})");
            return false;
        }
        if (run.Liveness.UsedBy(path) is { } users)
        {
            run.Keep(source, kind, path, owner, size, $"kept: {why}, but {users}");
            return false;
        }
        if (files.FirstOrDefault(SessionTempPruner.InUse) is { } open)
        {
            run.Keep(source, kind, path, owner, size, $"kept: {why}, but {(folder ? Path.GetRelativePath(path, open.FullName) : "it")} is open");
            return false;
        }
        if (!run.Options.DryRun)
        {
            foreach (var f in files) SessionTempPruner.ClearReadOnly(f);
            if (folder) SessionTempPruner.DeleteTree(path);
            else File.Delete(path);
        }
        run.Went(new SweepItem(source, kind, path, owner, size, true, why));
        return true;
    }

    private static void SweepLeftovers(Run run, HashSet<string> recorded)
    {
        var o = run.Options;
        if (!(o.LeftoverHours > 0) || !double.IsFinite(o.LeftoverHours)) return;
        foreach (var (pattern, wantFolder) in LeftoverPatterns)
        {
            FileSystemInfo[] found;
            try
            {
                var temp = new DirectoryInfo(o.TempDir);
                if (!temp.Exists) return;
                found = temp.GetFileSystemInfos(pattern, SessionTempPruner.Links);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                run.Errors.Add($"{o.TempDir}: {ex.Message}");
                return;
            }
            foreach (var entry in found)
            {
                try
                {
                    // The ledger's own rows are judged by their owner above, not by name and age.
                    if ((entry is DirectoryInfo) != wantFolder || recorded.Contains(entry.FullName)) continue;
                    if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                    var newest = entry is DirectoryInfo d
                        ? SessionTempPruner.Newest(SessionTempPruner.Tree(d.FullName, d.GetFiles("*", SessionTempPruner.Walk)))
                        : SessionTempPruner.Newest([entry]);
                    var stale = SessionTempPruner.Stale(entry.CreationTimeUtc, newest, run.Now, o.LeftoverHours);
                    var why = stale
                        ? $"crash leftover: created {Span(run.Now - entry.CreationTimeUtc)} ago, nothing written in it for {Span(run.Now - newest)} (older than {o.LeftoverHours:0.#}h)"
                        : $"kept: not {o.LeftoverHours:0.#}h old and quiet yet (created {Span(run.Now - entry.CreationTimeUtc)} ago)";
                    Reclaim(run, Leftover, ResourceKind.TempFile, entry.FullName, "nobody (found by name)", stale, why, TimeSpan.Zero);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    run.Errors.Add($"{entry.FullName}: {ex.Message}");
                }
            }
        }
    }

    private static void SweepBuildOutputs(Run run)
    {
        var o = run.Options;
        if (!(o.BuildOutputHours > 0) || !double.IsFinite(o.BuildOutputHours)) return;
        foreach (var repo in Folders(o.WorktreeRoot))
        {
            if (repo.Name.Equals(TicketScratch.FolderName, StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var tree in Folders(repo.FullName).Where(t => IsDaemonTree(t.Name)))
            {
                try { SweepTree(run, tree); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { run.Errors.Add($"{tree.FullName}: {ex.Message}"); }
            }
        }
    }

    /// <summary>One daemon worktree: its bin/obj folders go together, or not at all (stale as a whole, nothing running there).</summary>
    private static void SweepTree(Run run, DirectoryInfo tree)
    {
        var o = run.Options;
        var outputs = BuildOutputs(tree);
        if (outputs.Count == 0) return;
        var files = outputs.ToDictionary(d => d, d => d.GetFiles("*", SessionTempPruner.Walk));
        var size = files.Values.Sum(fs => fs.Sum(f => f.Length));
        var newest = files.Max(kv => SessionTempPruner.Newest(SessionTempPruner.Tree(kv.Key.FullName, kv.Value)));
        void Keep(string why) => run.Keep(BuildOutput, "bin/obj", tree.FullName, ResourceLedger.DaemonOwner, size, why);
        if (run.Now - newest <= TimeSpan.FromHours(o.BuildOutputHours))
        {
            Keep($"kept: its bin/obj were written {Span(run.Now - newest)} ago (stale after {o.BuildOutputHours:0.#}h)");
            return;
        }
        var stale = $"stale build output: {outputs.Count} bin/obj folder{(outputs.Count == 1 ? "" : "s")}, nothing written for {Span(run.Now - newest)} (older than {o.BuildOutputHours:0.#}h)";
        if (o.Busy?.Invoke(tree.FullName) == true) { Keep($"kept: {stale}, but a build or test runs there"); return; }
        if (run.Liveness.UsedBy(tree.FullName) is { } users) { Keep($"kept: {stale}, but {users}"); return; }
        // A running build or test holds its binaries open; every other file there is rebuilt anyway.
        if (files.Values.SelectMany(fs => fs).Where(IsBinary).FirstOrDefault(SessionTempPruner.InUse) is { } open)
        {
            Keep($"kept: {stale}, but {Path.GetRelativePath(tree.FullName, open.FullName)} is open");
            return;
        }
        if (!o.DryRun)
            foreach (var (dir, its) in files)
            {
                try
                {
                    foreach (var f in its) SessionTempPruner.ClearReadOnly(f);
                    SessionTempPruner.DeleteTree(dir.FullName);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    run.Errors.Add($"{dir.FullName}: {ex.Message}");
                    size -= its.Sum(f => f.Length);
                }
            }
        run.Went(new SweepItem(BuildOutput, "bin/obj", tree.FullName, ResourceLedger.DaemonOwner, Math.Max(0, size), true, stale));
    }

    private static bool IsBinary(FileInfo f) =>
        f.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) || f.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The build output folders in <paramref name="tree"/>: every <c>bin</c> or <c>obj</c> that sits beside a project file
    /// (<c>*.*proj</c>), so a repo's own tracked <c>bin</c> folder of scripts is never one. Not through links, <c>.git</c> or
    /// <c>node_modules</c>, and not below a bin/obj itself.
    /// </summary>
    internal static IReadOnlyList<DirectoryInfo> BuildOutputs(DirectoryInfo tree)
    {
        var found = new List<DirectoryInfo>();
        var pending = new Stack<DirectoryInfo>();
        pending.Push(tree);
        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            DirectoryInfo[] below;
            try { below = dir.GetDirectories("*", SessionTempPruner.Links); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
            bool? project = null;
            foreach (var sub in below)
            {
                if (sub.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                if (sub.Name.Equals(".git", StringComparison.OrdinalIgnoreCase) || sub.Name.Equals("node_modules", StringComparison.OrdinalIgnoreCase)) continue;
                if (sub.Name.Equals("bin", StringComparison.OrdinalIgnoreCase) || sub.Name.Equals("obj", StringComparison.OrdinalIgnoreCase))
                {
                    if (project ??= HasProject(dir)) found.Add(sub);
                    continue;
                }
                pending.Push(sub);
            }
        }
        return found;
    }

    private static bool HasProject(DirectoryInfo dir)
    {
        try { return dir.EnumerateFiles("*.*proj", SessionTempPruner.Links).Any(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    private static IReadOnlyList<DirectoryInfo> Folders(string dir)
    {
        try
        {
            var d = new DirectoryInfo(dir);
            return d.Exists ? [.. d.GetDirectories("*", SessionTempPruner.Links).Where(x => !x.Attributes.HasFlag(FileAttributes.ReparsePoint))] : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }
    }

    private static string? Owned(string owner, string prefix) =>
        owner.StartsWith(prefix, StringComparison.Ordinal) && owner.Length > prefix.Length ? owner[prefix.Length..] : null;

    private static string Span(TimeSpan t) =>
        t.TotalHours >= 48 ? $"{t.TotalDays:0.#}d" : t.TotalHours >= 1 ? $"{t.TotalHours:0.#}h" : $"{Math.Max(0, t.TotalMinutes):0}min";

    /// <summary>One line for the log or the console: what went (or would) and from where, and how much is kept.</summary>
    public static string Describe(LedgerSweepResult r)
    {
        var going = r.Going;
        int Of(string source) => going.Count(i => i.Source == source);
        var line = $"Ledger sweep{(r.DryRun ? " (dry run)" : "")}: {(r.DryRun ? "would free" : "freed")} {DiskWatch.Size(r.Bytes)} " +
            $"({Of(FromLedger)} ledger entr{(Of(FromLedger) == 1 ? "y" : "ies")}, {Of(Leftover)} crash leftover{(Of(Leftover) == 1 ? "" : "s")}, " +
            $"{Of(BuildOutput)} worktree{(Of(BuildOutput) == 1 ? "" : "s")}' bin/obj); {r.Items.Count - going.Count} kept.";
        if (going.Count > 0)
            line += " Biggest: " + string.Join(", ", going.OrderByDescending(i => i.Bytes).Take(3).Select(i => $"{i.Path} {DiskWatch.Size(i.Bytes)}")) + ".";
        if (r.Errors.Count > 0) line += $" {r.Errors.Count} couldn't be done.";
        return line;
    }
}

/// <summary>
/// A temp file or folder made and deleted in a try/finally, recorded in the resource ledger while it exists (M.ORCH-T1677), so a
/// crash between the two leaves a row the sweeper reclaims once the process that made it is gone: owned by this process, by pid
/// and start time (<see cref="ResourceLedger.ProcessOwner(int, DateTime)"/>). Best effort and bounded: the database busy or
/// down never fails or holds up the caller for more than <see cref="Wait"/> (the sweep's name patterns catch what was missed).
/// </summary>
public static class LedgerTemp
{
    /// <summary>The longest the caller waits on the ledger, each way.</summary>
    public static readonly TimeSpan Wait = TimeSpan.FromSeconds(2);

    /// <summary>The ledger temp files are recorded in (seam); null records nothing.</summary>
    internal static Func<ResourceLedger?> Ledger { get; set; } = () => new ResourceLedger(new OrchestratorDb(OrchestratorDb.LivePath));

    /// <summary>Records <paramref name="path"/> as this process's before it's made; dispose the result once it's deleted, which
    /// drops the row.</summary>
    public static IDisposable Track(string path)
    {
        var registered = Task.Run(() =>
        {
            try
            {
                var ledger = Ledger();
                if (ledger is null) return null;
                using var me = Process.GetCurrentProcess();
                ledger.Register(ResourceKind.TempFile, path, ResourceLedger.ProcessOwner(me.Id, me.StartTime.ToUniversalTime()), DateTime.UtcNow);
                return ledger;
            }
            catch (Exception ex) when (Expected(ex)) { return null; }
        });
        try { registered.Wait(Wait); } catch (AggregateException) { }
        return new Tracked(registered, path);
    }

    private static bool Expected(Exception ex) =>
        ex is DbException or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException
            or NotSupportedException or Win32Exception or TimeoutException;

    private sealed class Tracked(Task<ResourceLedger?> registered, string path) : IDisposable
    {
        public void Dispose()
        {
            // After the registration, however long that took: never a row left for a file that's already gone.
            var forgotten = registered.ContinueWith(t =>
            {
                try { if (t.Status == TaskStatus.RanToCompletion) t.Result?.Forget(ResourceKind.TempFile, path); }
                catch (Exception ex) when (Expected(ex)) { }
            }, TaskScheduler.Default);
            try { forgotten.Wait(Wait); } catch (AggregateException) { }
        }
    }
}
