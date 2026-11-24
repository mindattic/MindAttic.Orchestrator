using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// The remote host's merge of a signed-off work branch (M.ORCH-T1636, after M.ORCH-T1501's <see cref="BranchBuilds"/>; off unless
/// <see cref="OrchestratorSettings.RemoteBranchBuilds"/>). A local test run that passes signs the head it tested off: its result
/// is recorded in the store's <see cref="BranchBuilds.TestsName"/> record (M.ORCH-T1637's <c>orchestrator branches report --pass</c>).
/// Every check, the host takes each work branch signed off at the head it still stands at and, by itself, in a worktree of its
/// own (<see cref="MergeTreeName"/>, never the repo's checkout, which agents share), merges that head into main, pushes main, and
/// deletes the branch on origin (only while it's still at that head). A branch that moved after its sign-off isn't merged: its
/// new head needs its own run. A merge that conflicts with main is recorded and not tried again until a new head is signed off;
/// one that couldn't be made now (the worktree, a push that lost the race with another) is tried again at the next check. The
/// user's PC never sees a branch: it stays on main and takes the merge with <see cref="FastForward"/> (<c>orchestrator branches
/// ff</c>).
/// </summary>
public sealed class BranchMerges(JsonStore store)
{
    /// <summary>The store's record of each merge tried: the head, and whether it landed (and as what) or why not.</summary>
    public const string StateName = "branch-merges";

    /// <summary>The host's one worktree per repo the merges are made in (<c>WorktreeRoot\&lt;repo&gt;\merge-main</c>, apart from the
    /// builds' <c>branch-*</c> ones).</summary>
    public const string MergeTreeName = "merge-main";

    /// <summary>One merge tried: <see cref="Head"/> the signed-off head (empty when it's to be tried again); <see cref="Merged"/>
    /// main's commit once it landed, else <see cref="Note"/> says why it didn't.</summary>
    public sealed class MergeState
    {
        /// <summary>The signed-off branch head this merge was tried at; empty when it's to be tried again.</summary>
        public string Head { get; set; } = "";

        /// <summary>The merge commit pushed to main; null when it didn't land.</summary>
        public string? Merged { get; set; }

        /// <summary>Why it didn't land, or what was left behind (a branch that moved past its sign-off is kept).</summary>
        public string? Note { get; set; }

        /// <summary>When this merge was last tried (UTC).</summary>
        public DateTime Utc { get; set; }
    }

    /// <summary>A branch signed off at the head it stands at, due a merge.</summary>
    public sealed record Due(string Repo, string Path, string Branch, string Head);

    /// <summary>A merge's outcome for one branch. <see cref="Retry"/>: it failed for a reason a later check may not meet (the
    /// worktree couldn't be made, main moved before the push), so the same sign-off is tried again; a conflict is final for
    /// that head. <see cref="Rejected"/>: the merge itself was refused for good at that head (a conflict with main), so its ticket is
    /// handed back; <see cref="Gone"/>: the branch is gone from origin, so its ticket is handed back too (M.ORCH-T1661).</summary>
    public sealed record Outcome(string Repo, string Branch, string Head, string? Merged, string? Note, bool Retry = false, bool Rejected = false, bool Gone = false);

    /// <summary>A line for the daemon's log; <see cref="Warn"/> marks a merge that didn't land.</summary>
    public sealed record Line(bool Warn, string Text);

    internal Func<string, string[], (int Code, string Output)> Git { get; init; } = (repo, args) => Dispatcher.Git(repo, args);

    /// <summary>The repo's main line (main or master); a test's names it without asking origin.</summary>
    internal Func<string, string> Mainline { get; init; } = Dispatcher.Mainline;

    /// <summary>Where a round of merges runs: its own task; a test's runs it inline.</summary>
    internal Func<Func<IReadOnlyList<Outcome>>, Task<IReadOnlyList<Outcome>>> Spawn { get; init; } =
        work => Task.Factory.StartNew(work, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    /// <summary>How often the sign-offs are looked at (Vault: <see cref="OrchestratorSettings.BranchBuildsCheckSeconds"/>).</summary>
    internal TimeSpan CheckEvery { get; set; } = TimeSpan.FromSeconds(OrchestratorSettings.DefaultBranchBuildsCheckSeconds);

    /// <summary>The workspace whose ticket database a merge's ticket is closed (or handed back) in (M.ORCH-T1661); null writes
    /// no ticket, only clears its awaiting mark. The daemon sets it.</summary>
    public string? WorkspaceRoot { get; set; }

    /// <summary>Settles a merge's ticket (seam; <see cref="BranchBuilds.SettleMerge"/>); returns what couldn't be done.</summary>
    internal Func<Outcome, string?>? Settle { get; init; }

    private string? SettleMerge(Outcome o) =>
        Settle is { } settle ? settle(o) : BranchBuilds.SettleMerge(store, WorkspaceRoot, o.Repo, o.Branch, o.Merged, o.Note, o.Rejected, o.Gone);

    private Task<IReadOnlyList<Outcome>>? running;
    private DateTime checkedUtc = DateTime.MinValue;

    /// <summary>Whether a round of merges is running now.</summary>
    public bool Running => running is not null;

    /// <summary>
    /// Records the round of merges that finished and, every <see cref="CheckEvery"/> when <paramref name="mayStart"/> (the lease
    /// holder), starts the next: each work branch signed off at the head it still stands at is merged into main, pushed, and
    /// deleted on origin.
    /// </summary>
    public IReadOnlyList<Line> Tick(IEnumerable<(string Name, string Path)> repos, string worktreeRoot, DateTime nowUtc, bool mayStart = true)
    {
        var lines = new List<Line>();
        if (running is { IsCompleted: true } done)
        {
            Dictionary<string, Dictionary<string, MergeState>> record = [];
            // The record unreadable now: the round's results are kept and recorded on a later tick, not dropped.
            if (done.IsCompletedSuccessfully && done.Result.Count > 0 && !store.TryLoad(StateName, out record)) return lines;
            running = null;
            if (!done.IsCompletedSuccessfully)
                lines.Add(new Line(true, $"branch merges: a round failed: {done.Exception?.GetBaseException().Message ?? "it was cancelled"}"));
            else if (done.Result.Count > 0)
            {
                foreach (var o in done.Result)
                {
                    if (!record.TryGetValue(o.Repo, out var branches)) branches = record[o.Repo] = new Dictionary<string, MergeState>(StringComparer.Ordinal);
                    // A retry is kept without its head, so the next check tries the same sign-off again.
                    branches[o.Branch] = new MergeState { Head = o.Retry ? "" : o.Head, Merged = o.Merged, Note = o.Note, Utc = nowUtc };
                    lines.Add(o.Merged is { } merged
                        ? new Line(false, $"branch merges: {o.Repo}'s {o.Branch} signed off at {Short(o.Head)}; merged into main as {Short(merged)}, pushed" +
                            (o.Note is { } n ? $" ({n})" : ", and the branch deleted"))
                        : new Line(true, $"branch merges: {o.Repo}'s {o.Branch} signed off at {Short(o.Head)} but not merged" +
                            (o.Retry ? " yet" : "") + $": {o.Note}"));
                    // M.ORCH-T1661: the branch's ticket stayed open until now; merged, it's closed with main's commit as its ref,
                    // and a merge refused for good (or a branch gone) hands it back.
                    if (SettleMerge(o) is { } unsettled) lines.Add(new Line(true, $"branch merges: {o.Repo}'s {o.Branch}: {unsettled}"));
                }
                store.Save(StateName, record);
            }
        }

        if (!mayStart || running is not null || nowUtc - checkedUtc < CheckEvery) return lines;
        checkedUtc = nowUtc;
        var tests = BranchBuilds.LoadTests(store);
        if (tests.Count == 0) return lines;
        var due = DueNow(repos, BranchBuilds.SignedOff(BranchBuilds.Load(store), tests), Load(store));
        if (due.Count > 0) running = Spawn(() => [.. due.Select(d => Merge(d, Path.Combine(worktreeRoot, d.Repo, MergeTreeName)))]);
        return lines;
    }

    /// <summary>The store's record of the merges tried; empty when there's none or it can't be read.</summary>
    public static Dictionary<string, Dictionary<string, MergeState>> Load(JsonStore store) =>
        store.TryLoad<Dictionary<string, Dictionary<string, MergeState>>>(StateName, out var state) ? state : [];

    /// <summary>Every signed-off branch (<see cref="BranchBuilds.SignedOff"/>: its local run passed at the head it still stands
    /// at) in a repo here whose merge wasn't already tried at that head, oldest sign-off first.</summary>
    internal static List<Due> DueNow(IEnumerable<(string Name, string Path)> repos, IReadOnlyList<BranchBuilds.Ready> signedOff,
        IReadOnlyDictionary<string, Dictionary<string, MergeState>> tried)
    {
        var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, path) in repos) paths.TryAdd(name, path);
        var due = new List<Due>();
        foreach (var s in signedOff)
        {
            if (!paths.TryGetValue(s.Repo, out var path)) continue;
            if (Find(tried, s.Repo) is { } triedHere && triedHere.TryGetValue(s.Branch, out var t) && SameHead(t.Head, s.Head)) continue;
            due.Add(new Due(s.Repo, path, s.Branch, s.Head));
        }
        return due;
    }

    /// <summary>Merges the signed-off head into main in <paramref name="tree"/>, pushes main, and deletes the branch on origin
    /// while it's still at that head.</summary>
    internal Outcome Merge(Due d, string tree)
    {
        var (repo, path, branch, head) = d;
        if (Git(path, ["fetch", "-q", "--prune", "origin"]).Code != 0) return new Outcome(repo, branch, head, null, "origin couldn't be fetched", Retry: true);
        var tip = RevParse(path, "refs/remotes/origin/" + branch);
        if (tip is null) return new Outcome(repo, branch, head, null, $"{branch} is gone from origin", Gone: true);
        if (!SameHead(tip, head)) return new Outcome(repo, branch, head, null, $"{branch} moved to {Short(tip)} after its sign-off; that head needs its own local test run");

        var mainline = Mainline(path);
        if (Prepare(path, tree, "refs/remotes/origin/" + mainline) is { } error) return new Outcome(repo, branch, head, null, error, Retry: true);
        var (code, output) = Git(tree, ["merge", "--no-ff", "--no-edit", "-m", $"Merge {branch} at {Short(head)}: its local test run passed", tip]);
        if (code != 0)
        {
            var conflicts = Git(tree, ["diff", "--name-only", "--diff-filter=U"]).Output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Git(tree, ["merge", "--abort"]);
            return new Outcome(repo, branch, head, null, conflicts.Length > 0
                ? $"it conflicts with {mainline} in {string.Join(", ", conflicts.Take(5))}{(conflicts.Length > 5 ? $" and {conflicts.Length - 5} more" : "")}"
                : $"git merge failed: {FirstLine(output)}", Rejected: true);
        }
        var (pushed, pushOutput) = Git(tree, ["push", "-q", "origin", "HEAD:refs/heads/" + mainline]);
        if (pushed != 0) return new Outcome(repo, branch, head, null, $"its push to {mainline} was refused ({FirstLine(pushOutput)}); merged again on the new {mainline} at the next check", Retry: true);
        var merged = RevParse(tree, "HEAD") ?? "";

        // The branch goes only while it's still at the signed head: one that moved meanwhile keeps its new commits.
        string? note = null;
        if (Git(path, ["push", "-q", $"--force-with-lease=refs/heads/{branch}:{tip}", "origin", ":refs/heads/" + branch]).Code != 0)
            note = $"{branch} moved after its sign-off, so it was kept on origin";
        return new Outcome(repo, branch, head, merged, note);
    }

    /// <summary>Puts the merge worktree at <paramref name="start"/>, detached, making it the first time; the error, if it couldn't.</summary>
    private string? Prepare(string repo, string tree, string start)
    {
        if (File.Exists(Path.Combine(tree, ".git")))
        {
            Git(tree, ["merge", "--abort"]); // a merge a daemon that ended left half done
            if (Git(tree, ["checkout", "--detach", "--force", "-q", start]).Code == 0)
            {
                Git(tree, ["clean", "-fdq"]);
                return null;
            }
        }
        try
        {
            if (Directory.Exists(tree)) Directory.Delete(tree, recursive: true);
            Directory.CreateDirectory(Path.GetDirectoryName(tree)!);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return $"couldn't make {tree}: {ex.Message}"; }
        Git(repo, ["worktree", "prune"]);
        var (code, output) = Git(repo, ["worktree", "add", "--detach", tree, start]);
        return code == 0 ? null : $"git worktree add failed: {FirstLine(output)}";
    }

    private string? RevParse(string repo, string rev)
    {
        var (code, output) = Git(repo, ["rev-parse", "--verify", "--quiet", rev + "^{commit}"]);
        return code == 0 && FirstLine(output) is { Length: > 0 } sha ? sha : null;
    }

    /// <summary>
    /// The PC's side (M.ORCH-T1636): fast-forwards the checkout's main to origin's once the host has merged. Only a checkout on
    /// main, and only a fast-forward, so local commits or changes in the way leave it as it is, and the reason is said.
    /// </summary>
    public static (bool Ok, string Message) FastForward(string repoPath) => new BranchMerges(null!).FastForwardAt(repoPath);

    internal (bool Ok, string Message) FastForwardAt(string repoPath)
    {
        var mainline = Mainline(repoPath);
        var current = FirstLine(Git(repoPath, ["rev-parse", "--abbrev-ref", "HEAD"]).Output);
        if (!string.Equals(current, mainline, StringComparison.Ordinal)) return (false, $"on {current}, not {mainline}; left as it is");
        if (Git(repoPath, ["fetch", "-q", "origin"]).Code != 0) return (false, "origin couldn't be reached");
        var before = RevParse(repoPath, "HEAD");
        var (code, output) = Git(repoPath, ["merge", "--ff-only", "-q", "refs/remotes/origin/" + mainline]);
        if (code != 0) return (false, $"not fast-forwarded: {FirstLine(output)}");
        var after = RevParse(repoPath, "HEAD");
        return (true, string.Equals(before, after, StringComparison.OrdinalIgnoreCase)
            ? $"{mainline} is up to date"
            : $"{mainline} fast-forwarded {Short(before ?? "")}..{Short(after ?? "")}");
    }

    private static Dictionary<string, T>? Find<T>(IReadOnlyDictionary<string, Dictionary<string, T>> byRepo, string repo) =>
        byRepo.FirstOrDefault(r => string.Equals(r.Key, repo, StringComparison.OrdinalIgnoreCase)).Value;

    /// <summary>Whether two hashes name the same commit: equal, or one a prefix (at least 7 characters) of the other.</summary>
    private static bool SameHead(string? a, string? b) =>
        !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && Math.Min(a.Length, b.Length) >= 7
        && (a.StartsWith(b, StringComparison.OrdinalIgnoreCase) || b.StartsWith(a, StringComparison.OrdinalIgnoreCase));

    private static string FirstLine(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "";

    private static string Short(string sha) => sha.Length > 7 ? sha[..7] : sha;
}
