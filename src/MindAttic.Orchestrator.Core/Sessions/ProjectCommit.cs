using System.Text;
using System.Text.RegularExpressions;
using MindAttic.Tickets;
using MindAttic.Tickets.Data;
using MindAttic.Tickets.Log;

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

/// <summary>The project commit result record: <paramref name="TicketId"/> is the ticket the commit names (one the message already
/// named, or one <see cref="ProjectCommit"/> created for it, then <paramref name="TicketCreated"/>).</summary>
public sealed record ProjectCommitResult(
    RosterProject Project, ProjectCommitOutcome Outcome, string Detail, string? TicketId = null, bool TicketCreated = false);

/// <summary>
/// The picker tree's "Commit and push" (M.ORCH-T1589): every project picked in the tree gets its whole working tree committed
/// under one message, then rebased onto its remote branch and pushed, one project after another. A project with nothing to
/// commit still pushes commits it hasn't pushed yet; one with neither is left alone.
/// <para>Every commit is aligned to a MindAttic.Tickets ticket (M.ORCH-T2044, ported from the Launcher's WorkspaceCommitter): a
/// message naming one of the repo's own tickets is kept as it is; otherwise a closed ticket is created from the message and its
/// ID appended to the subject, and the repo's generated USER_STORIES.md is rewritten before staging so the new row lands in the
/// same commit. So a message typed without an ID still passes the workspace's commit-msg hook. The MindAttic.Tickets repo, which
/// holds the database, is committed last so it carries every ticket the run created; when it isn't among the projects, its
/// database files alone are committed and pushed after them (the db sync).</para>
/// </summary>
public static partial class ProjectCommit
{
    /// <summary>The git timeout.</summary>
    public static readonly TimeSpan GitTimeout = TimeSpan.FromMinutes(2);

    /// <summary>The ticket database's repo.</summary>
    public const string TicketsRepoName = "MindAttic.Tickets";

    /// <summary>The db sync commit's subject, before the ticket it carries is appended.</summary>
    public const string SyncMessage = "Sync the ticket database";

    private static readonly HashSet<string> DocExtensions = new(StringComparer.OrdinalIgnoreCase) { ".md", ".htm", ".html", ".txt" };

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

    /// <summary>Commits everything in <paramref name="project"/>'s working tree as <paramref name="message"/> (aligned to a
    /// ticket), pulls its branch with --rebase (the tree is clean after the commit, so no stash) and pushes it; a ticket it
    /// created is then pushed in the ticket database's repo too, whose outcome is added to the detail. <paramref name="workspaceRoot"/>
    /// is the folder holding the repos and the ticket database (the project's parent folder when null). Never throws for a git
    /// failure: the result says what went wrong.</summary>
    public static ProjectCommitResult CommitAndPush(RosterProject project, string message, IProcessRunner? runner = null, string? workspaceRoot = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        var results = CommitAndPushAll([project], message, null, runner, workspaceRoot);
        var own = results[0];
        if (results.Count < 2) return own;
        var sync = results[1];
        return own with
        {
            Detail = sync.Outcome == ProjectCommitOutcome.Failed
                ? $"{own.Detail} Pushing its new ticket in {TicketsRepoName} failed: {sync.Detail}"
                : $"{own.Detail} {sync.Detail}",
        };
    }

    /// <summary>Commits and pushes every project in turn under <paramref name="message"/>, the MindAttic.Tickets repo last so it
    /// carries the tickets the run created; when it isn't among them and a ticket was created, its database is synced after
    /// them as an extra result. <paramref name="report"/> hears each result as it lands.</summary>
    public static IReadOnlyList<ProjectCommitResult> CommitAndPushAll(
        IEnumerable<RosterProject> projects, string message, Action<ProjectCommitResult>? report = null,
        IProcessRunner? runner = null, string? workspaceRoot = null)
    {
        ArgumentNullException.ThrowIfNull(projects);
        runner ??= ProcessRunner.Default;
        var results = new List<ProjectCommitResult>();
        void Add(ProjectCommitResult r) { results.Add(r); report?.Invoke(r); }

        var list = projects.ToList();
        var ordered = list.OrderBy(p => IsTicketsRepo(p.Path, RootOf(p, workspaceRoot)) ? 1 : 0).ToList();
        string? lastCreated = null, createdRoot = null;
        var ticketsCommitted = false;
        foreach (var p in ordered)
        {
            var root = RootOf(p, workspaceRoot);
            var r = CommitOne(p, message, runner, root, lastCreated);
            if (r.TicketCreated) { lastCreated = r.TicketId; createdRoot = root; }
            ticketsCommitted |= IsTicketsRepo(p.Path, root);
            Add(r);
        }

        // A commit outside the Tickets repo still created a ticket in the shared database; push it so other machines see it.
        if (lastCreated is not null && createdRoot is not null && !ticketsCommitted) Add(SyncTicketsDb(createdRoot, lastCreated, runner));
        return results;
    }

    private static string RootOf(RosterProject p, string? workspaceRoot) =>
        workspaceRoot ?? Path.GetDirectoryName(Path.GetFullPath(p.Path).TrimEnd('\\', '/')) ?? p.Path;

    private static ProjectCommitResult CommitOne(RosterProject project, string message, IProcessRunner runner, string root, string? carriedTicket)
    {
        if (string.IsNullOrWhiteSpace(message)) return new(project, ProjectCommitOutcome.Failed, "No commit message.");
        if (!Directory.Exists(project.Path)) return new(project, ProjectCommitOutcome.Failed, $"No folder at {project.Path}.");
        ProcessResult Git(params string[] args) => runner.Run("git", ["-C", project.Path, .. args], GitTimeout);
        ProjectCommitResult Fail(string step, ProcessResult r) =>
            new(project, ProjectCommitOutcome.Failed, r.TimedOut ? $"{step} timed out." : $"{step} failed: {FirstLine(r)}");
        string? ticketId = null;
        var created = false;
        try
        {
            var branch = Git("rev-parse", "--abbrev-ref", "HEAD");
            if (branch.ExitCode != 0 || branch.TimedOut) return Fail("Reading its branch", branch);
            var name = branch.Output.Trim();
            if (name is "" or "HEAD") return new(project, ProjectCommitOutcome.Failed, "Not on a branch (a detached HEAD); nothing was done.");

            var isTickets = IsTicketsRepo(project.Path, root);
            if (isTickets) WriteDump(root);

            var status = Git("status", "--porcelain");
            if (status.ExitCode != 0 || status.TimedOut) return Fail("git status", status);
            var committed = false;
            if (!string.IsNullOrWhiteSpace(status.Output))
            {
                var changes = ChangedPaths(status.Output);
                string text;
                if (isTickets && carriedTicket is not null && changes.All(IsDatabaseFile))
                {
                    text = SyncMessageFor(carriedTicket);
                    ticketId = carriedTicket;
                }
                else
                {
                    var aligned = Align(project, message.Trim(), changes, root, isTickets);
                    if (aligned.Error is not null) return new(project, ProjectCommitOutcome.Failed, aligned.Error);
                    (text, ticketId, created) = (aligned.Message, aligned.TicketId, aligned.Created);
                }

                var add = Git("add", "-A");
                if (add.ExitCode != 0 || add.TimedOut) return Unaligned(Fail("git add", add));
                var commit = Git("commit", "-q", "-m", text);
                if (commit.ExitCode != 0 || commit.TimedOut) return Unaligned(Fail("git commit", commit));
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
                var pull = Git("pull", "-q", "--rebase", "origin", name);
                if (pull.ExitCode != 0 || pull.TimedOut)
                {
                    Git("rebase", "--abort");
                    return Fail(committed ? "Committed, but pulling origin first" : "Pulling origin", pull) with { TicketId = ticketId, TicketCreated = created };
                }
            }
            var push = Git("push", "-q", "origin", $"HEAD:{name}");
            if (push.ExitCode != 0 || push.TimedOut)
                return Fail(committed ? "Committed, but the push" : "The push", push) with { TicketId = ticketId, TicketCreated = created };
            var head = Git("rev-parse", "--short", "HEAD").Output.Trim();
            var cited = ticketId is null ? "" : created ? $" (new ticket {ticketId})" : $" ({ticketId})";
            return new(project, ProjectCommitOutcome.Pushed,
                committed ? $"Committed and pushed {head} to {name}{cited}." : $"Pushed unpushed commits ({head}) to {name}.",
                ticketId, created);
        }
        catch (System.ComponentModel.Win32Exception) { return Unaligned(new(project, ProjectCommitOutcome.Failed, "git isn't installed or isn't on PATH.")); }

        // Nothing was committed, so a ticket created for it would claim work that never landed: mark it won't-do.
        ProjectCommitResult Unaligned(ProjectCommitResult failed)
        {
            if (!created || ticketId is null) return failed;
            try
            {
                using var db = OpenDb(root);
                if (db is null) return failed;
                db.Close(ticketId, TicketRules.WontDo, "commit failed");
                db.Render(new MindAttic.Tickets.Workspace(root), [RepoName(project.Path)]);
                if (IsTicketsRepo(project.Path, root)) WriteDump(root);
                return failed with { Detail = $"{failed.Detail} ({ticketId} marked wont-do)" };
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or System.Data.Common.DbException)
            {
                return failed with { Detail = $"{failed.Detail} (couldn't mark {ticketId} wont-do: {ex.Message})" };
            }
        }
    }

    private sealed record Alignment(string Message, string? TicketId, bool Created, string? Error);

    /// <summary>The message the commit uses: kept when it names one of the repo's tickets, else with a new closed ticket's ID
    /// appended. No migrated ticket database at all (a workspace without MindAttic.Tickets) commits the message as it is.</summary>
    private static Alignment Align(RosterProject project, string message, IReadOnlyList<string> changes, string root, bool isTickets)
    {
        var repoName = RepoName(project.Path);
        try
        {
            using var db = OpenDb(root);
            if (db is null) return new(message, TicketIds(message).FirstOrDefault(), false, null);
            if (!db.Repos().Any(r => r.Name.Equals(repoName, StringComparison.OrdinalIgnoreCase)))
                return new(message, null, false, $"{repoName} isn't in the ticket database, so the commit can't be aligned to a ticket; run 'tickets repos rollout tickets --apply'.");

            var existing = TicketIds(message).FirstOrDefault(id => db.Ticket(id) is { } t && t.Repo.Equals(repoName, StringComparison.OrdinalIgnoreCase));
            if (existing is not null) return new(message, existing, false, null);

            var ticket = db.Add(repoName, InferType(message, changes), TicketRules.Closed, Summary(message), "");
            db.Render(new MindAttic.Tickets.Workspace(root), [repoName]);
            if (isTickets) WriteDump(root, db); // the dump must carry this repo's own new ticket
            return new(WithTicket(message, ticket.Id), ticket.Id, true, null);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or System.Data.Common.DbException)
        {
            return new(message, null, false, $"Aligning the commit to a ticket failed: {ex.Message}");
        }
    }

    /// <summary>Commits only the database and its dump in the Tickets repo (other work there is left alone) and pushes it.</summary>
    private static ProjectCommitResult SyncTicketsDb(string root, string ticketId, IProcessRunner runner)
    {
        var path = TicketsRepoPath(root);
        var project = new RosterProject(TicketsRepoName, path, null);
        if (!Directory.Exists(path))
            return new(project, ProjectCommitOutcome.Failed, "The Tickets repo isn't there; run 'tickets db sync' to push the new ticket.", ticketId);
        ProcessResult Git(params string[] args) => runner.Run("git", ["-C", path, .. args], GitTimeout);
        ProjectCommitResult Fail(string step, ProcessResult r) =>
            new(project, ProjectCommitOutcome.Failed, r.TimedOut ? $"{step} timed out." : $"{step} failed: {FirstLine(r)}", ticketId);
        try
        {
            WriteDump(root);
            string[] files = [TicketsDb.RelativePath, TicketsDb.DumpRelativePath];
            var add = Git(["add", "--", .. files]);
            if (add.ExitCode != 0 || add.TimedOut) return Fail("Syncing the ticket database: git add", add);
            var diff = Git(["diff", "--cached", "--quiet", "--", .. files]);
            if (diff.ExitCode == 0 && !diff.TimedOut) return new(project, ProjectCommitOutcome.NothingToDo, "The ticket database was already synced.", ticketId);
            var commit = Git(["commit", "-q", "-m", SyncMessageFor(ticketId), "--", .. files]);
            if (commit.ExitCode != 0 || commit.TimedOut) return Fail("Syncing the ticket database: git commit", commit);

            var push = Git("push", "-q");
            if (push.ExitCode != 0 && !push.TimedOut && IsBehindRemote(push.Error))
            {
                // The remote moved ahead: rebase onto it, keeping any other work there aside, and push again.
                var pull = Git("pull", "-q", "--rebase", "--autostash");
                if (pull.ExitCode != 0 || pull.TimedOut)
                {
                    Git("rebase", "--abort");
                    return Fail("Synced the ticket database locally, but rebasing onto origin", pull);
                }
                push = Git("push", "-q");
            }
            if (push.ExitCode != 0 || push.TimedOut) return Fail("Synced the ticket database locally, but the push", push);
            return new(project, ProjectCommitOutcome.Pushed, $"Synced the ticket database ({ticketId}).", ticketId);
        }
        catch (System.ComponentModel.Win32Exception) { return new(project, ProjectCommitOutcome.Failed, "git isn't installed or isn't on PATH.", ticketId); }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or System.Data.Common.DbException)
        {
            return new(project, ProjectCommitOutcome.Failed, $"Syncing the ticket database failed: {ex.Message}", ticketId);
        }
    }

    private static bool IsBehindRemote(string pushError) =>
        pushError.Contains("non-fast-forward", StringComparison.OrdinalIgnoreCase) || pushError.Contains("fetch first", StringComparison.OrdinalIgnoreCase);

    /// <summary>The readable dump committed beside the database, the same file <c>tickets db sync</c> writes.</summary>
    private static void WriteDump(string root, TicketsDb? open = null)
    {
        var repo = TicketsRepoPath(root);
        if (!Directory.Exists(repo)) return;
        using var owned = open is null ? OpenDb(root) : null;
        var db = open ?? owned;
        if (db is null) return;
        var path = Path.Combine(repo, TicketsDb.DumpRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, db.Dump(), new UTF8Encoding(false));
    }

    /// <summary>The database once it holds any repo; null before migration.</summary>
    private static TicketsDb? OpenDb(string root)
    {
        var path = TicketsDb.DefaultPath(root);
        if (!File.Exists(path)) return null;
        var db = TicketsDb.Open(path);
        if (db.Repos().Count > 0) return db;
        db.Dispose();
        return null;
    }

    private static string TicketsRepoPath(string root) => Path.Combine(root, TicketsDb.HomeFolder(root));

    private static bool IsTicketsRepo(string path, string root) =>
        string.Equals(Path.GetFullPath(path).TrimEnd('\\', '/'), Path.GetFullPath(TicketsRepoPath(root)).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

    private static bool IsDatabaseFile(string file) =>
        Path.GetFileName(file).Equals(Path.GetFileName(TicketsDb.RelativePath), StringComparison.OrdinalIgnoreCase)
        || Path.GetFileName(file).Equals(Path.GetFileName(TicketsDb.DumpRelativePath), StringComparison.OrdinalIgnoreCase);

    private static string RepoName(string path) => Path.GetFileName(Path.GetFullPath(path).TrimEnd('\\', '/'));

    /// <summary>The paths git status --porcelain lists (a rename's new path), unquoted.</summary>
    internal static IReadOnlyList<string> ChangedPaths(string porcelain) =>
        [.. porcelain.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.Length > 3)
            .Select(l => l[3..])
            .Select(p => p.Contains(" -> ", StringComparison.Ordinal) ? p[(p.IndexOf(" -> ", StringComparison.Ordinal) + 4)..] : p)
            .Select(p => p.Trim().Trim('"'))];

    [GeneratedRegex(@"\b(?:[A-Z]\.)?[A-Z][A-Z0-9]*-T\d+\b")]
    private static partial Regex TicketIdPattern();

    /// <summary>Ticket IDs (<c>CODE-T&lt;n&gt;</c>) named in a commit message, in order.</summary>
    public static IReadOnlyList<string> TicketIds(string message) =>
        [.. TicketIdPattern().Matches(message).Select(m => m.Value).Distinct(StringComparer.Ordinal)];

    /// <summary>The ticket type for a commit: docs when only documentation changed, otherwise read from the message's first word.</summary>
    public static string InferType(string message, IReadOnlyList<string> changes)
    {
        if (changes.Count > 0 && changes.All(c => DocExtensions.Contains(Path.GetExtension(c)))) return "docs";
        var first = message.TrimStart().Split([' ', ':', '\t', '\r', '\n'], 2)[0].ToLowerInvariant();
        return first switch
        {
            "fix" or "fixes" or "fixed" or "repair" or "correct" or "bugfix" => "fix",
            "doc" or "docs" or "document" or "documentation" => "docs",
            "add" or "adds" or "implement" or "introduce" or "support" or "allow" or "let" or "enable" or "feat" or "feature" => "feature",
            _ => "chore",
        };
    }

    /// <summary>The message's first line, which becomes the ticket summary.</summary>
    public static string Summary(string message) => message.Trim().Split('\n')[0].Trim();

    /// <summary>The database sync's subject cites a ticket it carries, as the workspace commit-msg hook requires.</summary>
    public static string SyncMessageFor(string ticketId) => WithTicket(SyncMessage, ticketId);

    /// <summary>Appends <c>(ID)</c> to the message's first line, the convention commit subjects already use.</summary>
    public static string WithTicket(string message, string id)
    {
        var trimmed = message.Trim();
        var nl = trimmed.IndexOf('\n');
        return nl < 0 ? $"{trimmed} ({id})" : $"{trimmed[..nl].TrimEnd()} ({id}){trimmed[nl..]}";
    }

    private static string FirstLine(ProcessResult r)
    {
        var text = string.IsNullOrWhiteSpace(r.Error) ? r.Output : r.Error;
        var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        return line ?? $"exit code {r.ExitCode}";
    }
}
