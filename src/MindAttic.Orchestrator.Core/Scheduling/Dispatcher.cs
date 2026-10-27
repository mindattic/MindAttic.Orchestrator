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

    /// <summary>The Claude login a dispatched tab opens on, chosen rather than picked (M.ORCH-T613: the dashboard tab it was
    /// started from): <see cref="ConfigDir"/> is the per-CLI login's folder, null for the global login; <see cref="Email"/> its
    /// account's email when known, which the assignment records (M.ORCH-T668).</summary>
    public sealed record TabLogin(string? ConfigDir, string? Email = null);

    /// <param name="submit">Press Enter on the pre-filled prompt once the agent is up (auto-submit repos only).</param>
    /// <param name="login">The login a Claude tab opens on; null picks one as a new tab's is (Auto, else the global login).</param>
    /// <param name="prompt">The tab's opening prompt; null is <see cref="Scheduler.PromptFor"/>'s (M.ORCH-T748: a redispatch carries its work forward).</param>
    public Result Dispatch(QueueItem item, string provider, bool useWorktree, bool submit = false, TabLogin? login = null, string? prompt = null)
    {
        prompt ??= Scheduler.PromptFor(item);
        var settings = store.LoadSettings();
        // The caller's plan may be seconds old: an /allocate CLI's `next --claim` (or another window) may have taken the
        // ticket since. Only the assignment the plan saw (a stale one) may be replaced.
        bool ClaimedElsewhere(IEnumerable<Assignment> all) => all.Any(a => a.TicketId == item.Id && a != item.Assignment);
        if (ClaimedElsewhere(store.LoadAssignments())) return new Result(false, $"{item.Id} was just claimed elsewhere; it isn't started twice.", null);
        // A ticket assigned to a user (M.ORCH-T668) starts only on their login: chosen for it when nothing was, refused on another.
        if (item.Assignee is { } who)
        {
            if (provider != Providers.Claude)
                return new Result(false, $"{item.Id} is assigned to {who}: it starts only on {who}'s Claude login, not in {provider}.", null);
            if (login is not null && !TicketPins.Same(login.Email, who))
                return new Result(false, $"{item.Id} is assigned to {who}: it starts only on {who}'s login. Reassign it, or Redelegate now, to start it elsewhere.", null);
            login ??= (PinnedLogin ?? LoginOf)(who);
            if (login is null)
                return new Result(false, $"{item.Id} is assigned to {who}, who isn't signed in on this machine: it waits for them (or Redelegate now).", null);
        }
        // An org's ticket (M.ORCH-T753) starts only on its own members' logins: a login chosen in another org is refused, and the
        // Auto pick below picks only among the org's. The global login (no email) and the API key aren't judged here.
        var orgs = OrgsNow();
        var orgId = orgs.OrgOf(item);
        if (orgs.Active && !item.IsPinned && provider == Providers.Claude && login?.Email is { } chosen && orgs.OrgOfLogin(chosen) != orgId)
            return new Result(false, $"{item.Id} is {orgs.NameOf(orgId)}'s: it starts only on its members' logins, not {chosen} ({orgs.NameOf(orgs.OrgOfLogin(chosen))}). " +
                "orchestrator orgs repo moves a repo to another org.", null);
        var directory = item.RepoPath;
        if (useWorktree)
        {
            var (ok, path, error) = EnsureWorktree(item.RepoPath, Path.Combine(settings.WorktreeRoot, item.Repo, item.Id), BranchFor(item.Id));
            if (!ok) return new Result(false, error, null);
            directory = path;
        }

        var title = useWorktree ? $"{item.Repo} {item.Id}" : item.Repo;
        Func<string, bool>? eligible = orgs.Active && !item.IsPinned ? email => orgs.OrgOfLogin(email) == orgId : null;
        string? pickedEmail = null;
        var (startError, cameUp) = OpenTab is { } seam ? seam(title, directory, provider, prompt, submit)
            : Open(title, directory, provider, prompt, submit, login, eligible, out pickedEmail);
        if (startError is not null) return new Result(false, startError, null);

        // With no login chosen, the one the picker chose works it (bughunt 2026-10-27).
        var assignment = new Assignment(item.Id, item.Repo, directory, provider, DateTime.UtcNow, useWorktree) { Account = AccountDirs.Normalize(login?.Email ?? pickedEmail) };
        var raced = false;
        store.UpdateAssignments(all =>
        {
            if (ClaimedElsewhere(all)) { raced = true; return; }
            all.RemoveAll(a => a.TicketId == item.Id);
            all.Add(assignment);
        });
        if (raced) return new Result(false, $"{item.Id} was claimed elsewhere while its tab opened; close the tab just opened, the other claim stands.", null);
        // Who works it, for Tickets Desktop's link back (M.ORCH-T766): its login now, its session once the daemon sees the tab's.
        Tickets.TicketWorking.Set(Path.GetDirectoryName(item.RepoPath), item.Id, assignment.Account, null);

        var where = useWorktree ? $"worktree {directory} (branch {BranchFor(item.Id)})" : item.Repo;
        return new Result(true, $"Opened {provider} on {item.Id} in {where}. " + (!submit
            ? "Its prompt is pre-filled; press Enter in the tab to begin."
            : "Its prompt submits by itself once the agent is up."), assignment, cameUp);
    }

    /// <summary>Seam: opens the agent's tab (title, directory, provider, prompt, submit) and returns the error, or the
    /// task that says whether it came up. Tests swap it so no Windows Terminal tab opens.</summary>
    internal Func<string, string, string, string, bool, (string? Error, Task<bool>? CameUp)>? OpenTab { get; init; }

    /// <summary>Seam: whether the owner has API billing on (M.ORCH-T706); null reads the billing mode in force.</summary>
    internal Func<bool>? ApiBilling { get; init; }

    private bool ApiBillingOn() => ApiBilling is { } seam ? seam() : ApiBillingInForce();

    /// <summary>Whether the owner has API billing on (M.ORCH-T706): the mode in force is the API key; a mode that can't be read counts as off.</summary>
    public static bool ApiBillingInForce()
    {
        try { return new Billing.BillingSwitch().Mode(out _) == Billing.BillingModes.Api; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { return false; }
    }

    /// <summary>Seam: the Auto login picker a Claude tab's login comes from (M.ORCH-T605); null uses the real per-CLI logins.</summary>
    internal LoginPicker? Logins { get; init; }

    /// <summary>Seam: the login a ticket assigned to a user (by email) opens on when nothing was chosen (M.ORCH-T668); null uses
    /// <see cref="LoginOf"/>.</summary>
    internal Func<string, TabLogin?>? PinnedLogin { get; init; }

    /// <summary>The login of the user signed in as <paramref name="email"/> on this machine: their per-CLI login, made ready for a
    /// CLI, else the global login when it's signed in as them; null when neither is.</summary>
    internal static TabLogin? LoginOf(string email)
    {
        var dirs = new AccountDirs();
        try
        {
            // The API key's person (M.ORCH-T906): its own login folder, made ready (the Vault key approved there) for a CLI that
            // bills API credits; null while the Vault holds no key.
            if (Billing.ApiKeyPerson.Is(email))
                return Billing.ApiKeyPerson.Prepare(dirs, new Billing.BillingSwitch()) is null
                    ? new TabLogin(Billing.ApiKeyPerson.Dir(dirs).Directory, Billing.ApiKeyPerson.Email) : null;
            if (AccountDirs.Normalize(email) is { } e && dirs.For(e) is { SignedIn: true } own)
            {
                dirs.Prepare(own);
                return new TabLogin(own.Directory, e);
            }
            return TicketPins.Same(Budget.AccountProfile.Read(dirs.HomeConfigPath)?.Email, email) ? new TabLogin(null, email) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>Seam: the orgs and the repos' orgs (M.ORCH-T753); null reads them from the store.</summary>
    internal Func<OrgRouting>? Orgs { get; init; }

    private OrgRouting OrgsNow()
    {
        if (Orgs is { } seam) return seam();
        var routing = OrgRouting.Load(store, store.LoadSettings());
        if (!routing.Active) return routing;
        // The global login's email only matters once there's more than one org.
        try { return routing with { GlobalEmail = Budget.AccountProfile.Read(new AccountDirs().HomeConfigPath)?.Email }; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return routing; }
    }

    private (string? Error, Task<bool>? CameUp) Open(string title, string directory, string provider, string prompt, bool submit, TabLogin? login, Func<string, bool>? eligible,
        out string? pickedEmail)
    {
        var before = sessions.HostIds();
        var focus = Foreground.Current();
        var noLogin = false;
        string? picked = null;
        var configDir = ConfigDirFor(provider, login, () =>
        {
            // One CLI per account (M.ORCH-T605): a Claude tab goes on a free per-CLI login. With none free it never lands on the
            // global login, a member's own seat (M.ORCH-T747): the API key when the owner has API billing on (M.ORCH-T706), else it waits.
            var loaded = store.LoadSettings();
            var chosen = (Logins ?? new LoginPicker { Allowances = () => Budget.WeeklyAllowances.Active(store, DateTime.UtcNow), Cutoffs = () => Budget.UsageCutoffs.Load(store) }).ForNewTab(provider, loaded.AutoLogin, DateTime.UtcNow, out _, loaded.WeeklyStopPercent, eligible);
            var dir = chosen?.Directory;
            picked = chosen?.Email;
            noLogin = dir is null && !ApiBillingOn();
            return dir;
        });
        pickedEmail = picked;
        if (noLogin) return ("no login of its own has room and API billing is off: it waits for a login with room, or for API billing to be switched on (the API key is the only fallback, never a member's seat)", null);
        var startError = sessions.Start(title, directory, provider, prompt, configDir);
        if (startError is not null) return (startError, null);
        _ = Foreground.RestoreAsync(focus, TimeSpan.FromSeconds(2));
        // Every tab is checked: one whose agent never starts would otherwise look like a free slot.
        return (null, Task.Run(async () => submit
            ? await sessions.SubmitWhenReadyAsync(directory, before, SubmitSettle, SubmitTimeout)
            : await sessions.WaitForHostAsync(directory, before, SubmitTimeout) is not null));
    }

    /// <summary>The config folder a new tab's CLI runs under: none for a provider with no Claude login; the chosen
    /// <paramref name="login"/>'s; else what <paramref name="picked"/> picks.</summary>
    internal static string? ConfigDirFor(string provider, TabLogin? login, Func<string?> picked) =>
        provider != "Claude" ? null : login is not null ? login.ConfigDir : picked();

    /// <summary>Drops an assignment (the ticket returns to the queue). Never touches the worktree. With
    /// <paramref name="workspaceRoot"/>, nobody is recorded working it in the ticket database any more (M.ORCH-T766); without, the
    /// daemon's sync clears it next tick.</summary>
    public void Release(string ticketId, string? workspaceRoot = null)
    {
        store.UpdateAssignments(all => all.RemoveAll(a => a.TicketId == ticketId));
        if (workspaceRoot is not null) Tickets.TicketWorking.Clear(workspaceRoot, ticketId);
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
            LinkSiblingPackages(repoPath, path);
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
        if (code == 0) LinkSiblingPackages(repoPath, path);
        return code == 0 ? (true, path, "") : (false, path, $"git worktree add failed: {output.Trim()}");
    }

    /// <summary>A Unity manifest's <c>file:../../Sibling/...</c> package paths resolve from the repo's Packages folder to its
    /// sibling repos; from a worktree (root\Repo\Ticket) they'd land in root\Repo. Junction each referenced sibling into
    /// that folder so the packages load unedited (M.ORCH-T652). Best effort: a failure leaves the worktree as it was.</summary>
    internal static void LinkSiblingPackages(string repoPath, string worktree)
    {
        try
        {
            var manifest = Path.Combine(worktree, "Packages", "manifest.json");
            if (!File.Exists(manifest)) return;
            var siblings = Path.GetDirectoryName(Path.GetFullPath(repoPath).TrimEnd('\\', '/'));
            var linkDir = Path.GetDirectoryName(Path.GetFullPath(worktree).TrimEnd('\\', '/'));
            if (siblings is null || linkDir is null) return;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(
                         File.ReadAllText(manifest), "\"file:\\.\\./\\.\\./([^/\\\\\"]+)"))
                names.Add(m.Groups[1].Value);
            foreach (var name in names)
            {
                var target = Path.Combine(siblings, name);
                var link = Path.Combine(linkDir, name);
                if (!Directory.Exists(target) || Directory.Exists(link) || File.Exists(link)) continue;
                if (OperatingSystem.IsWindows())
                {
                    if (!AccountDirs.TryMklinkArguments(link, target, out var cmdLine)) continue;
                    var psi = new ProcessStartInfo("cmd.exe") { Arguments = cmdLine, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
                    using var p = Process.Start(psi);
                    p?.StandardOutput.ReadToEnd();
                    p?.StandardError.ReadToEnd();
                    p?.WaitForExit(10_000);
                }
                else Directory.CreateSymbolicLink(link, target);
            }
        }
        catch (Exception) { } // the worktree exists by now: nothing here may fail it
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
