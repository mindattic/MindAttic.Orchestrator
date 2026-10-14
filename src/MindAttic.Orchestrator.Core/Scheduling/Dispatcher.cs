using System.Diagnostics;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// Starts a queued ticket: in the repo itself when it's free, otherwise in a git worktree on its own
/// branch so two agents never edit the same checkout. Records the assignment so the ticket leaves the
/// queue until it's closed.
/// </summary>
public sealed class Dispatcher(SessionControl sessions, JsonStore store)
{
    public sealed record Result(bool Ok, string Message, Assignment? Assignment);

    public Result Dispatch(QueueItem item, string provider, bool useWorktree)
    {
        var settings = store.LoadSettings();
        var directory = item.RepoPath;
        if (useWorktree)
        {
            var (ok, path, error) = EnsureWorktree(item.RepoPath, Path.Combine(settings.WorktreeRoot, item.Repo, item.Id), BranchFor(item.Id));
            if (!ok) return new Result(false, error, null);
            directory = path;
        }

        var title = useWorktree ? $"{item.Repo} {item.Id}" : item.Repo;
        var startError = sessions.Start(title, directory, provider, Scheduler.PromptFor(item));
        if (startError is not null) return new Result(false, startError, null);

        var assignment = new Assignment(item.Id, item.Repo, directory, provider, DateTime.UtcNow, useWorktree);
        var all = store.LoadAssignments();
        all.RemoveAll(a => a.TicketId == item.Id);
        all.Add(assignment);
        store.SaveAssignments(all);

        var where = useWorktree ? $"worktree {directory} (branch {BranchFor(item.Id)})" : item.Repo;
        return new Result(true, $"Opened {provider} on {item.Id} in {where}. Its prompt is pre-filled; press Enter in the tab to begin.", assignment);
    }

    /// <summary>Drops an assignment (the ticket returns to the queue). Never touches the worktree.</summary>
    public void Release(string ticketId)
    {
        var all = store.LoadAssignments();
        if (all.RemoveAll(a => a.TicketId == ticketId) > 0) store.SaveAssignments(all);
    }

    /// <summary>Removes a finished assignment's worktree. git refuses if it has uncommitted changes,
    /// and the branch is kept, so nothing unmerged is lost.</summary>
    public Result RemoveWorktree(Assignment a, string repoPath)
    {
        if (!a.Worktree) { Release(a.TicketId); return new Result(true, $"{a.TicketId} released.", null); }
        var (code, output) = Git(repoPath, "worktree", "remove", a.Directory);
        if (code != 0) return new Result(false, $"git kept the worktree: {output.Trim()}", a);
        Release(a.TicketId);
        return new Result(true, $"Removed {a.Directory}; branch {BranchFor(a.TicketId)} is kept.", null);
    }

    public static string BranchFor(string ticketId) => "orch/" + ticketId.ToLowerInvariant();

    internal static (bool Ok, string Path, string Error) EnsureWorktree(string repoPath, string path, string branch)
    {
        if (Directory.Exists(path) && (File.Exists(Path.Combine(path, ".git")) || Directory.Exists(Path.Combine(path, ".git"))))
            return (true, path, "");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var (exists, _) = Git(repoPath, "rev-parse", "--verify", "--quiet", "refs/heads/" + branch);
        var (code, output) = exists == 0
            ? Git(repoPath, "worktree", "add", path, branch)
            : Git(repoPath, "worktree", "add", "-b", branch, path, "HEAD");
        return code == 0 ? (true, path, "") : (false, path, $"git worktree add failed: {output.Trim()}");
    }

    private static (int Code, string Output) Git(string repoPath, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-C");
        psi.ArgumentList.Add(repoPath);
        foreach (var a in args) psi.ArgumentList.Add(a);
        try
        {
            using var p = Process.Start(psi)!;
            var stdout = p.StandardOutput.ReadToEndAsync();
            var stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();
            return (p.ExitCode, stdout.Result + stderr);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return (-1, ex.Message);
        }
    }
}
