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
        // The caller's plan may be seconds old: an /autopilot CLI's `next --claim` (or another window) may have taken the
        // ticket since. Only the assignment the plan saw (a stale one) may be replaced.
        bool ClaimedElsewhere(IEnumerable<Assignment> all) => all.Any(a => a.TicketId == item.Id && a != item.Assignment);
        if (ClaimedElsewhere(store.LoadAssignments())) return new Result(false, $"{item.Id} was just claimed elsewhere; it isn't started twice.", null);
        var directory = item.RepoPath;
        if (useWorktree)
        {
            var (ok, path, error) = EnsureWorktree(item.RepoPath, Path.Combine(settings.WorktreeRoot, item.Repo, item.Id), BranchFor(item.Id));
            if (!ok) return new Result(false, error, null);
            directory = path;
        }

        var title = useWorktree ? $"{item.Repo} {item.Id}" : item.Repo;
        var (startError, cameUp) = (OpenTab ?? Open)(title, directory, provider, Scheduler.PromptFor(item), submit);
        if (startError is not null) return new Result(false, startError, null);

        var assignment = new Assignment(item.Id, item.Repo, directory, provider, DateTime.UtcNow, useWorktree);
        var raced = false;
        store.UpdateAssignments(all =>
        {
            if (ClaimedElsewhere(all)) { raced = true; return; }
            all.RemoveAll(a => a.TicketId == item.Id);
            all.Add(assignment);
        });
        if (raced) return new Result(false, $"{item.Id} was claimed elsewhere while its tab opened; close the tab just opened, the other claim stands.", null);

        var where = useWorktree ? $"worktree {directory} (branch {BranchFor(item.Id)})" : item.Repo;
        return new Result(true, $"Opened {provider} on {item.Id} in {where}. " + (!submit
            ? "Its prompt is pre-filled; press Enter in the tab to begin."
            : "Its prompt submits by itself once the agent is up."), assignment, cameUp);
    }

    /// <summary>Seam: opens the agent's tab (title, directory, provider, prompt, submit) and returns the error, or the
    /// task that says whether it came up. Tests swap it so no Windows Terminal tab opens.</summary>
    internal Func<string, string, string, string, bool, (string? Error, Task<bool>? CameUp)>? OpenTab { get; init; }

    private (string? Error, Task<bool>? CameUp) Open(string title, string directory, string provider, string prompt, bool submit)
    {
        var before = sessions.HostIds();
        var focus = Foreground.Current();
        var startError = sessions.Start(title, directory, provider, prompt);
        if (startError is not null) return (startError, null);
        _ = Foreground.RestoreAsync(focus, TimeSpan.FromSeconds(2));
        // Every tab is checked: one whose agent never starts would otherwise look like a free slot.
        return (null, Task.Run(async () => submit
            ? await sessions.SubmitWhenReadyAsync(directory, before, SubmitSettle, SubmitTimeout)
            : await sessions.WaitForHostAsync(directory, before, SubmitTimeout) is not null));
    }

    /// <summary>Drops an assignment (the ticket returns to the queue). Never touches the worktree.</summary>
    public void Release(string ticketId)
    {
        store.UpdateAssignments(all => all.RemoveAll(a => a.TicketId == ticketId));
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
        {
            // Reused as it is, unless it's clean and everything in it is already on the main line (the ticket landed, then
            // was reopened or its claim went stale): then it moves up to the main line, not handed out days behind (M.ORCH-T367).
            // As a commit: with no origin the start point is the repo's own HEAD, which inside the worktree would mean its own.
            var current = Git(repoPath, "rev-parse", StartPoint(repoPath)).Output.Trim();
            if (current.Length > 0 && Git(path, "status", "--porcelain").Output.Trim().Length == 0 &&
                Git(path, "merge-base", "--is-ancestor", "HEAD", current).Code == 0)
                Git(path, "merge", "--ff-only", "-q", current);
            return (true, path, "");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // A worktree folder deleted by hand is still registered with git, its branch "checked out" there, so adding it again
        // would fail every time: let git forget worktrees whose folders are gone first.
        Git(repoPath, "worktree", "prune");
        var (exists, _) = Git(repoPath, "rev-parse", "--verify", "--quiet", "refs/heads/" + branch);
        var start = StartPoint(repoPath);
        // A branch with work of its own is checked out as it is; one whose every commit is already on the main line (the
        // ticket landed, was reopened and claimed again) starts over from it, not from where it landed weeks ago (M.ORCH-T360).
        var landed = exists == 0 && Git(repoPath, "merge-base", "--is-ancestor", "refs/heads/" + branch, start).Code == 0;
        var (code, output) = exists == 0 && !landed
            ? Git(repoPath, "worktree", "add", path, branch)
            : Git(repoPath, "worktree", "add", landed ? "-B" : "-b", branch, path, start);
        return code == 0 ? (true, path, "") : (false, path, $"git worktree add failed: {output.Trim()}");
    }

    /// <summary>Where a new ticket branch starts: the repo's main line as origin has it, not whatever your checkout has
    /// checked out (a feature branch of yours would otherwise ride along onto main with the agent's work); HEAD only
    /// when there's no origin to start from.</summary>
    internal static string StartPoint(string repoPath)
    {
        Git(repoPath, "fetch", "-q", "origin");
        var line = "origin/" + Mainline(repoPath);
        return Git(repoPath, "rev-parse", "--verify", "--quiet", line).Code == 0 ? line : "HEAD";
    }

    internal static readonly TimeSpan GitTimeout = TimeSpan.FromMinutes(2);

    /// <summary>The branch <paramref name="repoPath"/>'s work lands on: what origin/HEAD points at (main or master), else
    /// whichever of main and master origin has, else main.</summary>
    public static string Mainline(string repoPath)
    {
        var (code, head) = Git(repoPath, "symbolic-ref", "--short", "refs/remotes/origin/HEAD");
        if (code == 0 && head.Trim() is var h && h.StartsWith("origin/", StringComparison.Ordinal) && h.Length > "origin/".Length)
            return h["origin/".Length..];
        foreach (var name in new[] { "main", "master" })
            if (Git(repoPath, "rev-parse", "--verify", "--quiet", "refs/remotes/origin/" + name).Code == 0) return name;
        return "main";
    }

    internal static (int Code, string Output) Git(string repoPath, params string[] args) => Git(GitTimeout, repoPath, args);

    /// <param name="timeout">How long git gets before it's killed with its whole process tree (seam: tests pass a short one).</param>
    internal static (int Code, string Output) Git(TimeSpan timeout, string repoPath, params string[] args)
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
        // Never wait on a credential prompt: this runs inside the daemon's tick, and nobody is there to answer it.
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        try
        {
            using var p = Process.Start(psi)!;
            var stdout = p.StandardOutput.ReadToEndAsync();
            var stderr = p.StandardError.ReadToEndAsync();
            var started = Stopwatch.StartNew();
            var tookTooLong = (-1, $"git {string.Join(' ', args)} took over {timeout.TotalSeconds:0}s; stopped it");
            if (!p.WaitForExit(timeout))
            {
                try { p.Kill(entireProcessTree: true); } catch (Exception ex) when (ex is InvalidOperationException or AggregateException) { } // exited meanwhile, or part of the tree couldn't be ended
                return tookTooLong;
            }
            // The output ends at the pipes' EOF, not git's exit: a helper it started that outlives it (a credential
            // helper during fetch) would hold the daemon's tick forever, so that wait counts against the timeout too.
            var left = timeout - started.Elapsed;
            if (!Task.WaitAll([stdout, stderr], left > TimeSpan.Zero ? left : TimeSpan.Zero)) return tookTooLong;
            return (p.ExitCode, stdout.Result + stderr.Result);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return (-1, ex.Message);
        }
    }
}
