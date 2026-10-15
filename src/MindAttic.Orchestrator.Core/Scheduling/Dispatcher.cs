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
    /// <param name="CameUp">Completes true once the new tab is up and staying up (and, with <c>submit</c>,
    /// Enter was pressed in it); false if it never came up.</param>
    public sealed record Result(bool Ok, string Message, Assignment? Assignment, Task<bool>? CameUp = null);

    /// <summary>How long a new tab gets to load its agent before Enter is pressed, and to appear at all.</summary>
    public static readonly TimeSpan SubmitSettle = TimeSpan.FromSeconds(15), SubmitTimeout = TimeSpan.FromMinutes(2);

    /// <param name="submit">Press Enter on the pre-filled prompt once the agent is up (auto-submit repos only).</param>
    public Result Dispatch(QueueItem item, string provider, bool useWorktree, bool submit = false)
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
        var before = sessions.HostIds();
        var focus = Foreground.Current();
        var startError = sessions.Start(title, directory, provider, Scheduler.PromptFor(item));
        if (startError is not null) return new Result(false, startError, null);
        _ = Foreground.RestoreAsync(focus, TimeSpan.FromSeconds(2));
        // Every tab is checked: one whose agent never starts would otherwise look like a free slot.
        var cameUp = Task.Run(async () => submit
            ? await sessions.SubmitWhenReadyAsync(directory, before, SubmitSettle, SubmitTimeout)
            : await sessions.WaitForHostAsync(directory, before, SubmitTimeout) is not null);

        var assignment = new Assignment(item.Id, item.Repo, directory, provider, DateTime.UtcNow, useWorktree);
        var all = store.LoadAssignments();
        all.RemoveAll(a => a.TicketId == item.Id);
        all.Add(assignment);
        store.SaveAssignments(all);

        var where = useWorktree ? $"worktree {directory} (branch {BranchFor(item.Id)})" : item.Repo;
        return new Result(true, $"Opened {provider} on {item.Id} in {where}. " + (!submit
            ? "Its prompt is pre-filled; press Enter in the tab to begin."
            : "Its prompt submits by itself once the agent is up."), assignment, cameUp);
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
        if (code != 0 && IsWorktree(repoPath, a.Directory))
            return new Result(false, output.Contains("Permission denied", StringComparison.OrdinalIgnoreCase)
                ? $"Something still has {a.Directory} open, most likely the agent's tab: close it, then remove the worktree."
                : $"git kept the worktree: {output.Trim()}", a);

        // git unregisters the worktree and deletes its files even when Windows won't let it delete the
        // folder itself (a tab still open in it): that's removed, not failed. The empty folder goes
        // when the tab closes, or on the next removal.
        var leftover = code != 0;
        if (leftover)
            try { Directory.Delete(a.Directory); leftover = false; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        Release(a.TicketId);
        return new Result(true, $"Removed {a.Directory}; branch {BranchFor(a.TicketId)} is kept." +
            (leftover ? " Its empty folder stays until the tab open in it closes." : ""), null);
    }

    /// <summary>Whether git still lists <paramref name="directory"/> as one of the repo's worktrees.</summary>
    private static bool IsWorktree(string repoPath, string directory)
    {
        var (code, list) = Git(repoPath, "worktree", "list", "--porcelain");
        if (code != 0) return true; // can't tell: don't claim it's gone
        var target = Path.GetFullPath(directory).TrimEnd('\\', '/');
        return list.Split('\n').Any(l => l.StartsWith("worktree ", StringComparison.Ordinal) &&
            string.Equals(Path.GetFullPath(l[9..].Trim()).TrimEnd('\\', '/'), target, StringComparison.OrdinalIgnoreCase));
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

    internal static (int Code, string Output) Git(string repoPath, params string[] args)
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
