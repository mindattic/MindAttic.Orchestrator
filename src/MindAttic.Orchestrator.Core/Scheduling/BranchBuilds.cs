using System.Text.RegularExpressions;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// The remote host's branch builds (M.ORCH-T1501, first slice; off unless <see cref="OrchestratorSettings.RemoteBranchBuilds"/>).
/// On the host only, each unit of work may sit on its own <c>origin/work/&lt;name&gt;</c> branch, so a test run never blocks the
/// next change. Every <see cref="CheckEvery"/>, each repo's work branches whose head moved since it was last built are built,
/// one worktree per branch (<see cref="WorktreeName"/>), each in isolation from the others and from the repo's own checkout,
/// at most <see cref="MaxRunning"/> at once. A branch built clean at its head is ready for the user's local test run
/// (<see cref="ReadyToTest"/>, <c>orchestrator branches</c>); a branch deleted from origin is forgotten and its worktree removed.
/// Nothing here tests, merges or touches main: the local run, its sign-off and the merge come after (the follow-up tickets).
/// The user's own machine never sees a branch: it stays on main.
/// </summary>
public sealed partial class BranchBuilds(JsonStore store)
{
    /// <summary>One daemon builds the branches: two would build each one twice in the same worktree.</summary>
    public const string LeaseName = "branch-builds.lock";

    /// <summary>The store's record of every work branch: its head, and what it was last built at and how that went.</summary>
    public const string StateName = "branch-builds";

    /// <summary>Only branches under this are work branches; any other branch on origin is left alone.</summary>
    public const string Prefix = "work/";

    /// <summary>How a branch's worktree under <c>WorktreeRoot\&lt;repo&gt;</c> starts; the rest is the branch, made a folder name.</summary>
    public const string WorktreeName = "branch-";

    /// <summary>What one work branch stands at. <see cref="Built"/> is the head last built (null: never), <see cref="Ok"/> whether
    /// that build passed; <see cref="Building"/> the head a build is running at now.</summary>
    public sealed class BranchState
    {
        public string Head { get; set; } = "";
        public string? Built { get; set; }
        public bool Ok { get; set; }
        public string? Note { get; set; }
        public DateTime? BuiltUtc { get; set; }
        public string? Building { get; set; }
    }

    /// <summary>A build's outcome: <see cref="Ok"/>, else <see cref="Note"/> says why (its first errors, a timeout).</summary>
    public sealed record BuildResult(bool Ok, string? Note = null);

    /// <summary>A line for the daemon's log; <see cref="Warn"/> marks a build that failed.</summary>
    public sealed record Line(bool Warn, string Text);

    /// <summary>A branch built clean at its head, waiting for the user's local test run.</summary>
    public sealed record Ready(string Repo, string Branch, string Head, DateTime? BuiltUtc);

    private sealed record Run(string Repo, string Branch, string Head, Task<BuildResult> Task);

    internal Func<string, string[], (int Code, string Output)> Git { get; init; } = (repo, args) => Dispatcher.Git(repo, args);

    /// <summary>Builds the solution at the given path; a test's stands in for <c>dotnet build</c>.</summary>
    internal Func<string, BuildResult>? RunBuild { get; init; }

    /// <summary>Where a build goes: its own long-running task; a test's runs it inline.</summary>
    internal Func<Func<BuildResult>, Task<BuildResult>> Spawn { get; init; } =
        work => Task.Factory.StartNew(work, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    /// <summary>Why no build may start now (the memory guard past ok); null lets them. A held branch is built at a later check.</summary>
    internal Func<string?> Held { get; init; } = () => null;

    /// <summary>Builds at once across every repo (Vault: <see cref="OrchestratorSettings.BranchBuildsMaxRunning"/>).</summary>
    internal int MaxRunning { get; set; } = (int)OrchestratorSettings.DefaultBranchBuildsMaxRunning;

    /// <summary>How long one build may run before it's stopped (Vault: <see cref="OrchestratorSettings.BranchBuildsTimeoutMinutes"/>).</summary>
    internal TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(OrchestratorSettings.DefaultBranchBuildsTimeoutMinutes);

    /// <summary>How often the branches are looked at (Vault: <see cref="OrchestratorSettings.BranchBuildsCheckSeconds"/>).</summary>
    internal TimeSpan CheckEvery { get; set; } = TimeSpan.FromSeconds(OrchestratorSettings.DefaultBranchBuildsCheckSeconds);

    private readonly Dictionary<string, Run> running = new(StringComparer.OrdinalIgnoreCase);
    private DateTime checkedUtc = DateTime.MinValue;
    private string? heldSaid;

    /// <summary>The "repo|branch" keys being built now.</summary>
    public IReadOnlyCollection<string> Running => running.Keys;

    private static string Key(string repo, string branch) => repo + "|" + branch;

    /// <summary>
    /// Records the builds that finished and, every <see cref="CheckEvery"/> when <paramref name="mayStart"/> (the lease holder),
    /// starts a build for each work branch whose head moved since its last build.
    /// </summary>
    public IReadOnlyList<Line> Tick(IEnumerable<(string Name, string Path)> repos, string worktreeRoot, DateTime nowUtc, bool mayStart = true)
    {
        var lines = new List<Line>();
        // Unreadable now (the database busy): nothing this tick, so defaults never save over every branch's record.
        if (!store.TryLoad<Dictionary<string, Dictionary<string, BranchState>>>(StateName, out var state)) return lines;
        var changed = false;

        foreach (var done in running.Values.Where(r => r.Task.IsCompleted).ToList())
        {
            running.Remove(Key(done.Repo, done.Branch));
            var result = done.Task.IsCompletedSuccessfully ? done.Task.Result
                : new BuildResult(false, done.Task.Exception?.GetBaseException().Message ?? "it was cancelled");
            if (!state.TryGetValue(done.Repo, out var branches) || !branches.TryGetValue(done.Branch, out var st)) continue; // deleted meanwhile
            st.Building = null;
            st.Built = done.Head;
            st.Ok = result.Ok;
            st.Note = result.Note;
            st.BuiltUtc = nowUtc;
            changed = true;
            lines.Add(result.Ok
                ? new Line(false, $"branch builds: {done.Repo}'s {done.Branch} builds at {Short(done.Head)}; ready for a local test run")
                : new Line(true, $"branch builds: {done.Repo}'s {done.Branch} fails to build at {Short(done.Head)}: {result.Note}"));
        }

        // A build a daemon that ended left marked (no task here runs it) is built again.
        foreach (var (repo, branches) in state)
            foreach (var (branch, st) in branches)
                if (st.Building is not null && !running.ContainsKey(Key(repo, branch)))
                {
                    st.Building = null;
                    changed = true;
                }

        if (mayStart && nowUtc - checkedUtc >= CheckEvery)
        {
            checkedUtc = nowUtc;
            if (heldSaid is not null && Held() is null)
            {
                lines.Add(new Line(false, "branch builds: memory recovered; builds due start again"));
                heldSaid = null;
            }
            foreach (var (name, path) in repos)
            {
                var heads = Branches(path);
                if (heads is null) continue;
                var branches = state.TryGetValue(name, out var b) ? b : null;
                // Gone from origin (merged and deleted, or dropped): forgotten, its worktree removed.
                foreach (var gone in (branches?.Keys ?? Enumerable.Empty<string>()).Where(k => !heads.ContainsKey(k) && !running.ContainsKey(Key(name, k))).ToList())
                {
                    branches!.Remove(gone);
                    RemoveWorktree(path, TreeOf(worktreeRoot, name, gone));
                    lines.Add(new Line(false, $"branch builds: {name}'s {gone} is gone from origin; forgotten"));
                    changed = true;
                }
                foreach (var (branch, head) in heads)
                {
                    branches ??= state[name] = new Dictionary<string, BranchState>(StringComparer.Ordinal);
                    if (!branches.TryGetValue(branch, out var st)) st = branches[branch] = new BranchState();
                    if (!string.Equals(st.Head, head, StringComparison.OrdinalIgnoreCase))
                    {
                        st.Head = head;
                        changed = true;
                    }
                    if (running.ContainsKey(Key(name, branch)) || string.Equals(st.Built, head, StringComparison.OrdinalIgnoreCase)) continue;
                    if (running.Count >= MaxRunning) continue;
                    if (Held() is { } why)
                    {
                        if (heldSaid is null) lines.Add(new Line(false, $"branch builds: holding {name}'s {branch} (and any others due): {why}"));
                        heldSaid = why;
                        continue;
                    }
                    var (listed, tracked) = Git(path, ["ls-tree", "--name-only", head]);
                    if (listed != 0) continue;
                    if (MainlineTests.SolutionIn(tracked.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) is not { } solution)
                    {
                        // Nothing to build: counts as built clean, so it's still queued for its test run.
                        st.Built = head;
                        st.Ok = true;
                        st.Note = "no solution at its root; nothing to build";
                        st.BuiltUtc = nowUtc;
                        changed = true;
                        continue;
                    }
                    var tree = TreeOf(worktreeRoot, name, branch);
                    var timeout = Timeout;
                    st.Building = head;
                    changed = true;
                    running[Key(name, branch)] = new Run(name, branch, head, Spawn(() =>
                        Prepare(path, tree, head) is { } error ? new BuildResult(false, error)
                            : (RunBuild ?? (s => Dotnet(ProcessRunner.Default, s, timeout)))(Path.Combine(tree, solution))));
                    lines.Add(new Line(false, $"branch builds: building {name}'s {branch} at {Short(head)}"));
                }
            }
        }
        if (changed) store.Save(StateName, state);
        return lines;
    }

    /// <summary>Every branch built clean at its head, oldest build first: the queue the user's local test run takes from.</summary>
    public static IReadOnlyList<Ready> ReadyToTest(IReadOnlyDictionary<string, Dictionary<string, BranchState>> state) =>
        [.. state.SelectMany(r => r.Value.Where(b => b.Value.Ok && b.Value.Building is null && string.Equals(b.Value.Built, b.Value.Head, StringComparison.OrdinalIgnoreCase))
                .Select(b => new Ready(r.Key, b.Key, b.Value.Head, b.Value.BuiltUtc)))
            .OrderBy(r => r.BuiltUtc ?? DateTime.MinValue).ThenBy(r => r.Repo, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Branch, StringComparer.Ordinal)];

    /// <summary>The store's record, read; empty when there's none or it can't be read.</summary>
    public static Dictionary<string, Dictionary<string, BranchState>> Load(JsonStore store) =>
        store.TryLoad<Dictionary<string, Dictionary<string, BranchState>>>(StateName, out var state) ? state : [];

    /// <summary>The repo's work branches on origin and their heads (as this checkout last fetched or pushed them); null when git
    /// couldn't be asked.</summary>
    internal Dictionary<string, string>? Branches(string repo)
    {
        var (code, output) = Git(repo, ["for-each-ref", "--format=%(refname:strip=3) %(objectname)", "refs/remotes/origin/" + Prefix]);
        if (code != 0) return null;
        var heads = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var space = line.LastIndexOf(' ');
            if (space <= 0) continue;
            var branch = line[..space];
            if (branch.StartsWith(Prefix, StringComparison.Ordinal) && branch.Length > Prefix.Length) heads[branch] = line[(space + 1)..];
        }
        return heads;
    }

    /// <summary>The branch's own worktree: <c>WorktreeRoot\&lt;repo&gt;\branch-&lt;name&gt;</c>, the branch made one folder name.</summary>
    internal static string TreeOf(string worktreeRoot, string repo, string branch) =>
        Path.Combine(worktreeRoot, repo, WorktreeName + Unsafe().Replace(branch[(branch.StartsWith(Prefix, StringComparison.Ordinal) ? Prefix.Length : 0)..], "-"));

    /// <summary>Puts the branch's worktree at <paramref name="head"/>, detached, making it the first time; the error, if it couldn't.</summary>
    private string? Prepare(string repo, string tree, string head)
    {
        if (File.Exists(Path.Combine(tree, ".git")) && Git(tree, ["checkout", "--detach", "--force", "-q", head]).Code == 0)
        {
            Git(tree, ["clean", "-fdq"]); // ignored files (bin, obj) stay: the build is incremental
            InitSubmodules(tree);
            return null;
        }
        try
        {
            if (Directory.Exists(tree)) Directory.Delete(tree, recursive: true);
            Directory.CreateDirectory(Path.GetDirectoryName(tree)!);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return $"couldn't make {tree}: {ex.Message}"; }
        Git(repo, ["worktree", "prune"]);
        var (code, output) = Git(repo, ["worktree", "add", "--detach", tree, head]);
        if (code != 0) return $"git worktree add failed: {output.Trim()}";
        InitSubmodules(tree);
        return null;
    }

    private void InitSubmodules(string tree)
    {
        if (File.Exists(Path.Combine(tree, ".gitmodules"))) Git(tree, ["submodule", "update", "--init", "--recursive"]);
    }

    /// <summary>Best effort: a worktree that won't go now is pruned by a later <c>git worktree prune</c>.</summary>
    private void RemoveWorktree(string repo, string tree)
    {
        if (!Directory.Exists(tree)) return;
        if (Git(repo, ["worktree", "remove", "--force", tree]).Code == 0) return;
        try { Directory.Delete(tree, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary><c>dotnet build</c> on <paramref name="solution"/>, build servers off (one left running would hold the pipes open).</summary>
    internal static BuildResult Dotnet(IProcessRunner runner, string solution, TimeSpan timeout)
    {
        ProcessResult r;
        try { r = runner.Run("dotnet", ["build", solution, "--disable-build-servers", "-nologo"], timeout); }
        catch (System.ComponentModel.Win32Exception ex) { return new BuildResult(false, $"couldn't run dotnet: {ex.Message}"); }
        if (r.TimedOut && !r.Exited) return new BuildResult(false, $"it ran past {timeout.TotalMinutes:0} minutes and was stopped");
        return r.ExitCode == 0 ? new BuildResult(true) : new BuildResult(false, MainlineTests.BuildErrors(r.Output + "\n" + r.Error));
    }

    private static string Short(string sha) => sha.Length > 7 ? sha[..7] : sha;

    [GeneratedRegex(@"[^A-Za-z0-9._-]+")]
    private static partial Regex Unsafe();
}
