namespace MindAttic.Orchestrator.Sessions;

/// <summary>How committing and pushing one project went.</summary>
public enum ProjectCommitOutcome
{
    /// <summary>Its changes were committed (or it had unpushed commits) and the push landed.</summary>
    Pushed,
    /// <summary>A clean working tree with nothing unpushed: nothing was done.</summary>
    NothingToDo,
    /// <summary>Something failed; <see cref="ProjectCommitResult.Detail"/> says what. A commit already made stays local.</summary>
    Failed,
}

public sealed record ProjectCommitResult(RosterProject Project, ProjectCommitOutcome Outcome, string Detail);

/// <summary>
/// The picker tree's "Commit and push" (M.ORCH-T1589): every project picked in the tree gets its whole working tree committed
/// under one message, then rebased onto its remote branch and pushed, one project after another. A project with nothing to
/// commit still pushes commits it hasn't pushed yet; one with neither is left alone.
/// </summary>
public static class ProjectCommit
{
    public static readonly TimeSpan GitTimeout = TimeSpan.FromMinutes(2);

    /// <summary>How many files <paramref name="path"/>'s working tree has changed (git status --porcelain), or null when it
    /// isn't a git checkout git can read.</summary>
    public static int? ChangedFiles(string path, IProcessRunner? runner = null)
    {
        if (!Directory.Exists(path)) return null;
        try
        {
            var r = (runner ?? ProcessRunner.Default).Run("git", ["-C", path, "status", "--porcelain"], GitTimeout);
            if (r.ExitCode != 0 || r.TimedOut) return null;
            return r.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
        }
        catch (System.ComponentModel.Win32Exception) { return null; }
    }

    /// <summary>Commits everything in <paramref name="project"/>'s working tree as <paramref name="message"/>, pulls its branch
    /// with --rebase --autostash and pushes it. Never throws for a git failure: the result says what went wrong.</summary>
    public static ProjectCommitResult CommitAndPush(RosterProject project, string message, IProcessRunner? runner = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (string.IsNullOrWhiteSpace(message)) return new(project, ProjectCommitOutcome.Failed, "No commit message.");
        if (!Directory.Exists(project.Path)) return new(project, ProjectCommitOutcome.Failed, $"No folder at {project.Path}.");
        runner ??= ProcessRunner.Default;
        ProcessResult Git(params string[] args) => runner.Run("git", ["-C", project.Path, .. args], GitTimeout);
        ProjectCommitResult Fail(string step, ProcessResult r) =>
            new(project, ProjectCommitOutcome.Failed, r.TimedOut ? $"{step} timed out." : $"{step} failed: {FirstLine(r)}");
        try
        {
            var branch = Git("rev-parse", "--abbrev-ref", "HEAD");
            if (branch.ExitCode != 0 || branch.TimedOut) return Fail("Reading its branch", branch);
            var name = branch.Output.Trim();
            if (name is "" or "HEAD") return new(project, ProjectCommitOutcome.Failed, "Not on a branch (a detached HEAD); nothing was done.");

            var status = Git("status", "--porcelain");
            if (status.ExitCode != 0 || status.TimedOut) return Fail("git status", status);
            var committed = false;
            if (!string.IsNullOrWhiteSpace(status.Output))
            {
                var add = Git("add", "-A");
                if (add.ExitCode != 0 || add.TimedOut) return Fail("git add", add);
                var commit = Git("commit", "-q", "-m", message.Trim());
                if (commit.ExitCode != 0 || commit.TimedOut) return Fail("git commit", commit);
                committed = true;
            }
            else
            {
                // Nothing to commit: push only when it's ahead of its remote branch (or has no remote branch yet).
                var ahead = Git("rev-list", "--count", $"origin/{name}..HEAD");
                if (ahead.ExitCode == 0 && !ahead.TimedOut && ahead.Output.Trim() == "0")
                    return new(project, ProjectCommitOutcome.NothingToDo, "Nothing to commit or push.");
            }

            var remote = Git("ls-remote", "--exit-code", "--heads", "origin", name);
            if (remote.ExitCode == 0 && !remote.TimedOut)
            {
                var pull = Git("pull", "-q", "--rebase", "--autostash", "origin", name);
                if (pull.ExitCode != 0 || pull.TimedOut)
                {
                    Git("rebase", "--abort");
                    return Fail(committed ? "Committed, but pulling origin first" : "Pulling origin", pull);
                }
            }
            var push = Git("push", "-q", "origin", $"HEAD:{name}");
            if (push.ExitCode != 0 || push.TimedOut) return Fail(committed ? "Committed, but the push" : "The push", push);
            var head = Git("rev-parse", "--short", "HEAD").Output.Trim();
            return new(project, ProjectCommitOutcome.Pushed,
                committed ? $"Committed and pushed {head} to {name}." : $"Pushed unpushed commits ({head}) to {name}.");
        }
        catch (System.ComponentModel.Win32Exception) { return new(project, ProjectCommitOutcome.Failed, "git isn't installed or isn't on PATH."); }
    }

    private static string FirstLine(ProcessResult r)
    {
        var text = string.IsNullOrWhiteSpace(r.Error) ? r.Output : r.Error;
        var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        return line ?? $"exit code {r.ExitCode}";
    }
}
