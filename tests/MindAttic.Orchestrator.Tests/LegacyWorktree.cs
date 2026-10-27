using MindAttic.Orchestrator.Scheduling;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// A ticket worktree as the dispatcher made one before M.ORCH-T1122, on its own <c>orch/&lt;ticket&gt;</c> branch from the repo's
/// main line. Nothing in the orchestrator makes one any more (every ticket is worked in its repo's own checkout on main); the
/// tests of how an old one left behind is still landed, swept and tidied make one with this.
/// </summary>
internal static class LegacyWorktree
{
    public static (bool Ok, string Path, string Error) Add(string repoPath, string path, string branch)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Dispatcher.Git(repoPath, "fetch", "-q", "origin");
        var line = "origin/" + Dispatcher.Mainline(repoPath);
        var start = Dispatcher.Git(repoPath, "rev-parse", "--verify", "--quiet", line).Code == 0 ? line : "HEAD";
        var exists = Dispatcher.Git(repoPath, "rev-parse", "--verify", "--quiet", "refs/heads/" + branch).Code == 0;
        var (code, output) = exists
            ? Dispatcher.Git(repoPath, "worktree", "add", path, branch)
            : Dispatcher.Git(repoPath, "worktree", "add", "-b", branch, path, start);
        return (code == 0, path, code == 0 ? "" : output.Trim());
    }
}
