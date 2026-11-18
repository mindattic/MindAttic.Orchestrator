using MindAttic.Orchestrator.Settings;
using MindAttic.Tickets.Data;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// A milestone's acceptance checks, run and recorded (M.ORCH-T1684, roadmap Phase 9): evidence, not ticket counts. Each check
/// is evaluated and its result written to the ticket database with a short evidence note
/// (<see cref="TicketsDb.SetMilestoneCheckResult"/>), only when the result or the note changed.
/// <list type="bullet">
/// <item><c>exists</c> (on its own): its target is a package in the local feed, written <c>&lt;PackageId&gt; &lt;Version&gt;</c>
/// (<c>&lt;feed&gt;\&lt;PackageId&gt;.&lt;Version&gt;.nupkg</c>); else an absolute path (a file or folder on this machine); else a
/// path relative to each of the check's repos, which must be there on that repo's main line (<c>origin/&lt;main&gt;</c>, so a
/// tracked file or folder); with no repo named, a path relative to the workspace root, on disk.</item>
/// <item><c>build</c> (on its own): each named repo builds at the head of its main line. A main-line suite that passed at that
/// commit (<see cref="MainlineTests"/>) or a build already made there is the evidence; otherwise the solution is built in a
/// detached worktree of its own (<see cref="WorktreeName"/>) with the branch builds' <c>dotnet build</c>, one at a time
/// (<see cref="LeaseName"/>), and never while <see cref="Held"/>. A build that couldn't run or didn't complete leaves the
/// check as it was: only a compiler or MSBuild error is a fail.</item>
/// <item><c>harness</c>: only on accept, and there is no runner for one yet, so it stays unknown (<see cref="NoHarnessRunner"/>).</item>
/// <item><c>signoff</c>: only on accept, recorded as passed with who signed off.</item>
/// </list>
/// </summary>
public sealed class MilestoneAcceptance(JsonStore store)
{
    /// <summary>One build check builds at a time, across the daemon and the CLI: two would build in the same worktree.</summary>
    public const string LeaseName = "milestone-builds.lock";

    /// <summary>The store's record of each repo's last build made here: the commit, and how it went.</summary>
    public const string StateName = "milestone-builds";

    /// <summary>The worktree under <c>WorktreeRoot\&lt;repo&gt;</c> a build check builds in.</summary>
    public const string WorktreeName = "milestone-build";

    /// <summary>The local package feed an <c>exists</c> check's package is looked for in.</summary>
    public const string LocalFeed = @"C:\LocalNuGet";

    /// <summary>What a harness check's evidence says until something can run one.</summary>
    public const string NoHarnessRunner = "no harness runner yet";

    /// <summary>What a repo's main line was last built at by a build check, and how that went.</summary>
    public sealed class BuildState
    {
        /// <summary>Gets or sets the head.</summary>
        public string Head { get; set; } = "";
        /// <summary>Gets or sets the ok.</summary>
        public bool Ok { get; set; }
        /// <summary>Gets or sets the note.</summary>
        public string? Note { get; set; }
    }

    /// <summary>What an <c>exists</c> check's target names: a package of the local feed, or a path.</summary>
    public sealed record Target(string? PackageId, string? Version, string? Path)
    {
        /// <summary>Whether it names a package.</summary>
        public bool IsPackage => PackageId is not null;
    }

    /// <summary>What a check came to now. <see cref="Result"/> is null when it didn't run or couldn't (held, no checkout, a
    /// build that didn't complete): the check is left as it was, and <see cref="Evidence"/> says why.</summary>
    public sealed record Outcome(string? Result, string Evidence)
    {
        /// <summary>Whether it ran and failed.</summary>
        public bool Failed => Result == MilestoneCheckResults.Fail;
    }

    /// <summary>A check as it stands after its run, what the run came to, and whether that was written to the database.</summary>
    public sealed record Checked(MilestoneCheck Check, Outcome Outcome, bool Recorded);

    /// <summary>What <c>orchestrator milestone accept</c> came to: reached only when every check passed.</summary>
    public sealed record Acceptance(Milestone Milestone, IReadOnlyList<Checked> Checks, bool Reached)
    {
        /// <summary>The checks that haven't passed.</summary>
        public IReadOnlyList<Checked> Unmet => [.. Checks.Where(c => !c.Check.Passed)];
    }

    /// <summary>A line for the daemon's log; <see cref="Warn"/> marks a check that failed.</summary>
    public sealed record Line(bool Warn, string Text);

    internal Func<string, string[], (int Code, string Output)> Git { get; init; } = (repo, args) => Dispatcher.Git(repo, args);
    internal Func<string, string> MainlineOf { get; init; } = Dispatcher.Mainline;

    /// <summary>Builds the solution at the given path; a test's stands in for <c>dotnet build</c>.</summary>
    internal Func<string, BranchBuilds.BuildResult>? RunBuild { get; init; }

    /// <summary>Where a repo's checkout is; null when this machine has none.</summary>
    public Func<string, string?> RepoPath { get; set; } = _ => null;

    /// <summary>What a path with no repo is relative to.</summary>
    public string WorkspaceRoot { get; set; } = MindAttic.Tickets.Workspace.DefaultRoot;

    /// <summary>Where the build worktrees live (<see cref="OrchestratorSettings.WorktreeRoot"/>).</summary>
    public string WorktreeRoot { get; set; } = new OrchestratorSettings().WorktreeRoot;

    /// <summary>The feed a package is looked for in.</summary>
    public string Feed { get; init; } = LocalFeed;

    /// <summary>How long one build may run (the branch builds' limit, <see cref="OrchestratorSettings.BranchBuildsTimeoutMinutes"/>).</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(OrchestratorSettings.DefaultBranchBuildsTimeoutMinutes);

    /// <summary>Why no build may start now (memory, disk, a suite or a heavy process running); null lets one.</summary>
    public Func<string?> Held { get; init; } = () => null;

    /// <summary>Whether each repo's main line is fetched from origin before it's read (the owner's own run; the daemon reads
    /// the ref as the checkout last fetched it, as the main-line suites do).</summary>
    public bool Fetch { get; init; }

    private readonly Dictionary<string, (string Mainline, string Head)?> heads = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The build timeout in <paramref name="s"/>.</summary>
    public static TimeSpan TimeoutOf(OrchestratorSettings s) => OrchestratorSettings.SecondsSpan(s.BranchBuildsTimeoutMinutes * 60, 60);

    /// <summary>Why a build may not start for the heavy-process lease (M.ORCH-T1680), or null: every lease the limit allows is held.</summary>
    public static string? HeavyHeld(OrchestratorDb db, int limit, int graceSeconds, DateTime nowUtc)
    {
        if (limit <= 0) return null;
        var held = HeavyLease.Held(db, nowUtc, graceSeconds);
        return held.Count >= limit ? $"a heavy process holds the machine ({held[0].Holder.Name}: {held[0].Holder.Pattern})" : null;
    }

    /// <summary>
    /// What an <c>exists</c> target names. <c>&lt;PackageId&gt; &lt;Version&gt;</c> (two words, the first with no path separator,
    /// the second starting with a digit) is a package; anything else is a path. Quotes around it are dropped.
    /// </summary>
    public static Target ParseTarget(string target)
    {
        var text = target.Trim().Trim('"', '\'').Trim();
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 2 && words[0].IndexOfAny(['\\', '/', ':']) < 0 && char.IsAsciiDigit(words[1][0]) && words[1].IndexOfAny(['\\', '/', ':']) < 0)
            return new Target(words[0], words[1], null);
        return new Target(null, null, text);
    }

    /// <summary>Runs the active and accepting milestones' build and exists checks; a line for each result that changed. Nothing
    /// (after one read) when there's no such milestone, and nothing at all when the ticket database can't be read.</summary>
    public IReadOnlyList<Line> RunDue(Func<TicketsDb?> openTickets)
    {
        var lines = new List<Line>();
        try
        {
            using var db = openTickets();
            if (db is null) return lines;
            foreach (var m in db.Milestones().Where(m => m.State is MilestoneStates.Active or MilestoneStates.Accepting))
                foreach (var c in Run(db, m).Where(c => c.Recorded))
                    lines.Add(new Line(c.Outcome.Failed, $"milestone checks: {m.Id} #{c.Check.Number} ({c.Check.Kind}) is {c.Check.Result}: {c.Outcome.Evidence}"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException
            or KeyNotFoundException or System.Data.Common.DbException) { }
        return lines;
    }

    /// <summary>Runs <paramref name="milestone"/>'s checks, the build and exists ones always and the rest only with
    /// <paramref name="accept"/>, and records each result that changed.</summary>
    public IReadOnlyList<Checked> Run(TicketsDb db, Milestone milestone, bool accept = false, string? by = null)
    {
        heads.Clear();
        var done = new List<Checked>();
        foreach (var check in db.MilestoneChecks(milestone.Id))
        {
            var outcome = Evaluate(check, accept, by);
            var changed = outcome.Result is not null && (outcome.Result != check.Result || outcome.Evidence != (check.Evidence ?? ""));
            done.Add(new Checked(changed ? db.SetMilestoneCheckResult(milestone.Id, check.Number, outcome.Result!, outcome.Evidence) : check, outcome, changed));
        }
        return done;
    }

    /// <summary>
    /// The owner asking for everything: an active milestone moves into acceptance, every check runs (a sign-off is recorded as
    /// passed, by <paramref name="by"/>), and only when every check has passed is the milestone reached. Never forced.
    /// </summary>
    public Acceptance Accept(TicketsDb db, string idOrName, string by)
    {
        if (string.IsNullOrWhiteSpace(by)) throw new ArgumentException("Say who accepts the milestone (--by).", nameof(by));
        var m = db.Milestone(idOrName) ?? throw new KeyNotFoundException($"No milestone {idOrName}.");
        if (m.State == MilestoneStates.Active) m = db.AcceptMilestone(m.Id);
        else if (m.State != MilestoneStates.Accepting)
            throw new InvalidOperationException($"{m.Id} is {m.State}; only an active or accepting milestone can be accepted.");
        var checks = Run(db, m, accept: true, by);
        return checks.All(c => c.Check.Passed) ? new Acceptance(db.AcceptMilestone(m.Id, reached: true, by: by.Trim()), checks, true)
            : new Acceptance(m, checks, false);
    }

    /// <summary>What <paramref name="check"/> comes to now; nothing is recorded.</summary>
    public Outcome Evaluate(MilestoneCheck check, bool accept = false, string? by = null) => check.Kind switch
    {
        MilestoneCheckKinds.Exists => Exists(check),
        MilestoneCheckKinds.Build => Build(check),
        MilestoneCheckKinds.Harness when !accept => new Outcome(null, "A harness check runs only when the owner runs orchestrator milestone accept."),
        MilestoneCheckKinds.Harness => check.Result == MilestoneCheckResults.Unknown ? new Outcome(MilestoneCheckResults.Unknown, NoHarnessRunner)
            : new Outcome(null, $"{NoHarnessRunner}; the result recorded by hand ({check.Result}) stands"),
        MilestoneCheckKinds.Signoff when accept && !string.IsNullOrWhiteSpace(by) => new Outcome(MilestoneCheckResults.Pass, $"signed off by {by.Trim()}"),
        MilestoneCheckKinds.Signoff => new Outcome(null, "A sign-off is recorded only by orchestrator milestone accept --by <who>."),
        _ => new Outcome(null, $"Nothing here runs a check of kind '{check.Kind}'."),
    };

    private Outcome Exists(MilestoneCheck check)
    {
        if (string.IsNullOrWhiteSpace(check.Target)) return new Outcome(null, "The check names no target.");
        var target = ParseTarget(check.Target);
        if (target.IsPackage)
        {
            var file = Path.Combine(Feed, $"{target.PackageId}.{target.Version}.nupkg");
            return File.Exists(file) ? new Outcome(MilestoneCheckResults.Pass, $"package {target.PackageId} {target.Version} is in the local feed ({file})")
                : new Outcome(MilestoneCheckResults.Fail, $"package {target.PackageId} {target.Version} isn't in the local feed (looked for {file})");
        }
        var path = target.Path!;
        if (Path.IsPathFullyQualified(path)) return OnDisk(path);
        if (check.Repos.Count == 0) return OnDisk(Path.Combine(WorkspaceRoot, path));

        var relative = string.Join('/', path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).Where(p => p != "."));
        if (relative.Length == 0 || relative.Split('/').Contains("..")) return new Outcome(null, $"'{path}' isn't a path inside a repo.");
        List<string> there = [], missing = [], unknown = [];
        foreach (var repo in check.Repos)
        {
            if (RepoPath(repo) is not { } checkout) { unknown.Add($"there's no checkout of {repo} here"); continue; }
            if (HeadOf(checkout) is not { } at) { unknown.Add($"couldn't read {repo}'s main line"); continue; }
            var code = Git(checkout, ["cat-file", "-e", $"{at.Head}:{relative}"]).Code;
            if (code == 0) there.Add($"{repo} on {at.Mainline} at {Short(at.Head)}");
            else if (code < 0) unknown.Add($"git didn't answer in {repo}");
            else missing.Add($"{repo} on {at.Mainline} at {Short(at.Head)}");
        }
        if (missing.Count > 0) return new Outcome(MilestoneCheckResults.Fail, $"{relative} isn't in {string.Join(", ", missing)}");
        if (unknown.Count > 0) return new Outcome(null, $"Couldn't look for {relative}: {string.Join("; ", unknown)}.");
        return new Outcome(MilestoneCheckResults.Pass, $"{relative} is in {string.Join(", ", there)}");
    }

    private static Outcome OnDisk(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            return File.Exists(full) || Directory.Exists(full) ? new Outcome(MilestoneCheckResults.Pass, $"{full} exists")
                : new Outcome(MilestoneCheckResults.Fail, $"{full} doesn't exist");
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
            return new Outcome(null, $"'{path}' isn't a usable path: {ex.Message}");
        }
    }

    private Outcome Build(MilestoneCheck check)
    {
        if (check.Repos.Count == 0) return new Outcome(null, "The check names no repo to build.");
        List<string> built = [], failed = [], unknown = [];
        foreach (var repo in check.Repos)
        {
            var (ok, note) = BuildOf(repo);
            (ok switch { true => built, false => failed, _ => unknown }).Add(note);
        }
        if (failed.Count > 0) return new Outcome(MilestoneCheckResults.Fail, OneLine(string.Join("; ", failed)));
        if (unknown.Count > 0) return new Outcome(null, OneLine($"Not run, so its result stands: {string.Join("; ", unknown)}."));
        return new Outcome(MilestoneCheckResults.Pass, OneLine(string.Join("; ", built)));
    }

    /// <summary>Whether <paramref name="repo"/> builds at the head of its main line (null: couldn't be found out now), and the
    /// evidence: the main-line suite's record or this one's when either has that commit, else a build made now.</summary>
    private (bool? Ok, string Note) BuildOf(string repo)
    {
        if (RepoPath(repo) is not { } checkout) return (null, $"there's no checkout of {repo} here");
        if (HeadOf(checkout) is not { } at) return (null, $"couldn't read {repo}'s main line");
        var (mainline, head) = at;
        var where = $"{repo} on {mainline} at {Short(head)}";
        if (store.TryLoad<Dictionary<string, MainlineTests.RepoState>>(MainlineTests.StateName, out var suites)
            && suites.Any(s => Same(s.Key, repo) && Same(s.Value.Green, head)))
            return (true, $"{where} builds (its main-line suite passed there)");
        if (!store.TryLoad<Dictionary<string, BuildState>>(StateName, out var builds)) return (null, $"the orchestrator's store couldn't be read for {repo}");
        if (builds.FirstOrDefault(b => Same(b.Key, repo)).Value is { } last && Same(last.Head, head)) return (last.Ok, Said(where, last.Ok, last.Note));

        if (Held() is { } why) return (null, $"{where} wasn't built: {why}");
        var (listed, tracked) = Git(checkout, ["ls-tree", "--name-only", head]);
        if (listed != 0) return (null, $"couldn't list {where}");
        if (MainlineTests.SolutionIn(tracked.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) is not { } solution)
            return (true, $"{where} has no solution at its root; nothing to build");

        using var lease = new DispatchLease(store.Directory, LeaseName, store.Db);
        if (!lease.TryAcquire()) return (null, $"{where} wasn't built: another milestone build is running");
        var tree = Path.Combine(WorktreeRoot, repo, WorktreeName);
        if (new MainlineTests(store) { Git = Git }.Prepare(checkout, tree, head) is { } error) return (null, $"{where} wasn't built: {error}");
        var timeout = Timeout;
        var result = (RunBuild ?? (s => BranchBuilds.Dotnet(ProcessRunner.Contained, s, timeout)))(Path.Combine(tree, solution));
        if (!Completed(result)) return (null, $"{where}: the build didn't complete ({result.Note ?? "no reason given"})");

        var key = builds.Keys.FirstOrDefault(k => Same(k, repo)) ?? repo;
        builds[key] = new BuildState { Head = head, Ok = result.Ok, Note = result.Ok ? null : Clip(result.Note) };
        store.Save(StateName, builds);
        return (result.Ok, Said(where, result.Ok, builds[key].Note));
    }

    /// <summary>Whether a build's outcome is one about the code: it passed, or the compiler or MSBuild named an error that isn't
    /// a file another process held. A timeout, a missing dotnet or a file lock is the machine, never a failed check.</summary>
    internal static bool Completed(BranchBuilds.BuildResult result) =>
        result.Ok || (result.Note is { } note && note.Contains(": error ", StringComparison.Ordinal) && !MainlineTests.IsFileLock(note));

    private static string Said(string where, bool ok, string? note) => ok ? $"{where} builds" : $"{where} fails to build: {note}";

    private (string Mainline, string Head)? HeadOf(string checkout)
    {
        if (heads.TryGetValue(checkout, out var known)) return known;
        var mainline = MainlineOf(checkout);
        if (Fetch) Git(checkout, ["fetch", "--quiet", "origin", mainline]);
        var (code, output) = Git(checkout, ["rev-parse", "--verify", "--quiet", "refs/remotes/origin/" + mainline]);
        return heads[checkout] = code == 0 && output.Trim() is { Length: > 0 } head ? (mainline, head) : null;
    }

    private static bool Same(string? a, string? b) => a is not null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static string Short(string sha) => sha.Length > 7 ? sha[..7] : sha;

    private static string? Clip(string? text) => text is { Length: > 400 } ? text[..397] + "..." : text;

    private static string OneLine(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
