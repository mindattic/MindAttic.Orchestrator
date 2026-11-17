namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// The user's on-demand local test run of the host's work branches (M.ORCH-T1635, a M.ORCH-T1501 follow-up;
/// <c>orchestrator branches test</c>). For each queued branch (<see cref="BranchBuilds.Ready"/>) it fetches the head the host built
/// clean, puts it in one detached worktree per repo (<see cref="WorktreeName"/>, under the worktree root, never the repo's own
/// checkout, which stays on main), runs the solution's full suite there exactly as the main-line suite runs
/// (<see cref="MainlineTests.Dotnet"/>), and says whether that head passed and, if not, what failed. A run that couldn't complete
/// (the head isn't on origin, no worktree, a timeout, a file another process held) tested nothing and is not a result: it isn't
/// reported. Recording the result is the caller's (<see cref="BranchBuilds.RecordTest"/> on the host).
/// </summary>
public sealed class BranchTestRun
{
    /// <summary>The worktree under <c>WorktreeRoot\&lt;repo&gt;</c> the local runs are made in: not <c>branch-*</c> (the host's
    /// per-branch builds) nor a ticket's name, so neither the builds nor the worktree sweep take it.</summary>
    public const string WorktreeName = "local-tests";

    /// <summary>How many failing tests a report names; the rest are counted.</summary>
    public const int MaxNamedFailures = 10;

    /// <summary>A run's outcome at <see cref="Head"/>. <see cref="Completed"/> false: nothing was tested (<see cref="Note"/> says
    /// why) and nothing is reported. Otherwise <see cref="Ok"/> whether the whole suite passed, and <see cref="Note"/> what failed.</summary>
    public sealed record Result(string Repo, string Branch, string Head, bool Completed, bool Ok, string? Note, int Passed = 0, int Failed = 0);

    internal Func<string, string[], (int Code, string Output)> Git { get; init; } = (repo, args) => Dispatcher.Git(repo, args);

    /// <summary>Runs the suite of the solution at the given path; a test's stands in for <c>dotnet test</c>.</summary>
    internal Func<string, MainlineTests.SuiteResult>? RunSuite { get; init; }

    /// <summary>How long one branch's suite may run.</summary>
    public TimeSpan Timeout { get; init; } = MainlineTests.SuiteTimeout;

    /// <summary>Retries after a build fails on a file another process held, and the wait before each (the main-line suite's
    /// Vault settings).</summary>
    public int LockRetries { get; init; } = (int)Settings.OrchestratorSettings.DefaultMainTestsLockRetries;

    /// <summary>The wait before each of <see cref="LockRetries"/>.</summary>
    public TimeSpan LockRetryDelay { get; init; } = TimeSpan.FromSeconds(Settings.OrchestratorSettings.DefaultMainTestsLockRetrySeconds);

    /// <summary>The worktree a repo's local runs are made in.</summary>
    public static string TreeOf(string worktreeRoot, string repo) => Path.Combine(worktreeRoot, repo, WorktreeName);

    /// <summary>
    /// Runs <paramref name="ready"/>'s suite at its head, with <paramref name="repoPath"/> (this machine's checkout of the repo)
    /// as the place it's fetched into; the checkout's own branch and files are never touched.
    /// </summary>
    public Result Run(BranchBuilds.Ready ready, string repoPath, string worktreeRoot)
    {
        Result Incomplete(string why) => new(ready.Repo, ready.Branch, ready.Head, false, false, why);
        if (!Directory.Exists(repoPath)) return Incomplete($"there's no checkout of {ready.Repo} here ({repoPath})");
        if (Fetch(repoPath, ready.Branch, ready.Head) is { } fetchError) return Incomplete(fetchError);

        var (listed, tracked) = Git(repoPath, ["ls-tree", "--name-only", ready.Head]);
        if (listed != 0) return Incomplete($"couldn't list {Short(ready.Head)}: {tracked.Trim()}");
        if (MainlineTests.SolutionIn(tracked.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) is not { } solution)
            return new Result(ready.Repo, ready.Branch, ready.Head, true, true, "no solution at its root; nothing to test");

        var tree = TreeOf(worktreeRoot, ready.Repo);
        if (Prepare(repoPath, tree, ready.Head) is { } prepareError) return Incomplete(prepareError);
        var suite = (RunSuite ?? (s => MainlineTests.Dotnet(ProcessRunner.Contained, s, Timeout, LockRetryDelay, LockRetries, MainlineTests.EndLeftoverProcesses)))(
            Path.Combine(tree, solution));
        return From(ready, suite);
    }

    /// <summary>A suite's outcome as a run's result: passed only when it completed with no failure.</summary>
    internal static Result From(BranchBuilds.Ready ready, MainlineTests.SuiteResult suite)
    {
        if (!suite.Completed) return new Result(ready.Repo, ready.Branch, ready.Head, false, false, suite.Note ?? "the suite didn't complete");
        var passed = suite.Passed.Count(p => p != MainlineTests.BuildName);
        if (suite.Failed.Count == 0) return new Result(ready.Repo, ready.Branch, ready.Head, true, true, null, passed);
        return new Result(ready.Repo, ready.Branch, ready.Head, true, false, Describe(suite.Failed), passed, suite.Failed.Count);
    }

    /// <summary>The failures, one per line ("Test: first message line"), at most <see cref="MaxNamedFailures"/>, then how many more.</summary>
    internal static string Describe(IReadOnlyList<MainlineTests.Failure> failed)
    {
        var lines = failed.Take(MaxNamedFailures).Select(f => string.IsNullOrWhiteSpace(f.Message) ? f.Test : $"{f.Test}: {f.Message.Trim()}").ToList();
        if (failed.Count > MaxNamedFailures) lines.Add($"and {failed.Count - MaxNamedFailures} more failing");
        return string.Join("\n", lines);
    }

    /// <summary>Brings <paramref name="head"/> into the checkout's objects: the branch from origin, then the commit itself when the
    /// branch has moved on since; the error, if it still isn't here. Only remote-tracking refs move, never the checkout.</summary>
    private string? Fetch(string repo, string branch, string head)
    {
        if (head.Length < 7) return $"{head} isn't a commit hash";
        var fetched = Git(repo, ["fetch", "-q", "origin", $"+refs/heads/{branch}:refs/remotes/origin/{branch}"]);
        if (Has(repo, head)) return null;
        Git(repo, ["fetch", "-q", "origin", head]);
        if (Has(repo, head)) return null;
        return fetched.Code != 0 ? $"couldn't fetch {branch} from origin: {fetched.Output.Trim()}"
            : $"{Short(head)} isn't on origin's {branch} any more (it moved on); the new head gets its own run once the host builds it";
    }

    private bool Has(string repo, string head) => Git(repo, ["cat-file", "-e", head + "^{commit}"]).Code == 0;

    /// <summary>Puts the worktree at <paramref name="head"/>, detached, making it the first time; the error, if it couldn't.</summary>
    private string? Prepare(string repo, string tree, string head)
    {
        if (File.Exists(Path.Combine(tree, ".git")) && Git(tree, ["checkout", "--detach", "--force", "-q", head]).Code == 0)
        {
            // Untracked files a test left behind go; ignored ones (bin, obj) stay, so the build is incremental.
            Git(tree, ["clean", "-fdq"]);
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

    private static string Short(string sha) => sha.Length > 7 ? sha[..7] : sha;
}
