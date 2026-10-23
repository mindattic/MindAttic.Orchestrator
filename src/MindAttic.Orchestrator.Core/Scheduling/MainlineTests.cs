using System.Text.RegularExpressions;
using System.Xml.Linq;
using MindAttic.Orchestrator.Settings;
using MindAttic.Tickets.Data;
using MindAttic.Tickets.Log;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// The full test suite of each repo's main line, run by the daemon with no LLM (M.ORCH-T561). A subagent builds and runs only
/// the tests its change touches, lands and closes its ticket, and its slot goes to the next ticket; it no longer waits on the
/// whole suite (often 20+ minutes that spend no tokens). Here, every <see cref="CheckEvery"/>, each repo with a solution at its
/// root whose <c>origin/&lt;main line&gt;</c> moved past the last commit tested gets one run of its whole suite, in a detached
/// worktree of its own (<see cref="WorktreeName"/>): one run per repo at a time, so landings made while one runs are covered,
/// coalesced, by the next. A run with failing tests files one <c>fix</c> ticket per test (its name, its failure message and the
/// commits landed since the last green run, and the tickets they landed); a test with an open ticket isn't filed again, and
/// one that passes closes it. A ticket filed goes to the top of the workspace's queue (M.ORCH-T562): above every other open
/// ticket, below the failing-test tickets filed before it, so the next free slot in any CLI takes the oldest failure first.
/// </summary>
public sealed partial class MainlineTests(JsonStore store)
{
    /// <summary>One daemon runs the suites: two would run each one twice.</summary>
    public const string LeaseName = "mainline-tests.lock";

    /// <summary>The store's record of each repo's last tested and last green commit.</summary>
    public const string StateName = "mainline-tests";

    /// <summary>The worktree under <c>WorktreeRoot\&lt;repo&gt;</c> the suite runs in; not a ticket's name, so the sweep leaves it.</summary>
    public const string WorktreeName = "main-tests";

    /// <summary>How every ticket this files starts; what follows up to <see cref="Separator"/> is the test's name.</summary>
    public const string SummaryPrefix = "Fails on main: ";

    internal const string Separator = " — ";

    /// <summary>The name a build that fails is filed under: it fails every test, so it's one ticket, not one per test.</summary>
    public const string BuildName = "the build";

    internal static readonly TimeSpan CheckEvery = TimeSpan.FromMinutes(1);
    /// <summary>How long a repo waits before a run that didn't complete is tried again (M.ORCH-T731).</summary>
    internal static readonly TimeSpan RetryIncompleteAfter = TimeSpan.FromMinutes(10);
    internal static readonly TimeSpan SuiteTimeout = TimeSpan.FromMinutes(90);

    /// <summary>What a repo's main line was last tested at, and last passed at.</summary>
    public sealed class RepoState
    {
        public string? Tested { get; set; }
        public string? Green { get; set; }
    }

    public sealed record Failure(string Test, string Message);

    /// <summary>A suite's outcome. <see cref="Completed"/> is false when it couldn't run or ran past its time (<see cref="Note"/>
    /// says why): then nothing is filed or closed. <see cref="Passed"/> holds <see cref="BuildName"/> once the build succeeded.</summary>
    public sealed record SuiteResult(bool Completed, IReadOnlyList<Failure> Failed, IReadOnlySet<string> Passed, string? Note = null);

    /// <summary>A line for the daemon's log; <see cref="Warn"/> marks a failing run.</summary>
    public sealed record Line(bool Warn, string Text);

    private sealed record Run(string Repo, string Path, string Mainline, string Head, string Since, Task<SuiteResult> Task);

    internal Func<string, string[], (int Code, string Output)> Git { get; init; } = (repo, args) => Dispatcher.Git(repo, args);
    internal Func<string, string> MainlineOf { get; init; } = Dispatcher.Mainline;

    /// <summary>Runs the suite of the solution at the given path; a test's stands in for <c>dotnet test</c>.</summary>
    internal Func<string, SuiteResult> RunSuite { get; init; } = solution => Dotnet(ProcessRunner.Default, solution, SuiteTimeout);

    /// <summary>Where a run goes: its own long-running task; a test's runs it inline.</summary>
    internal Func<Func<SuiteResult>, Task<SuiteResult>> Spawn { get; init; } =
        work => Task.Factory.StartNew(work, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    private readonly Dictionary<string, Run> running = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> retryAfter = new(StringComparer.OrdinalIgnoreCase);
    private DateTime checkedUtc = DateTime.MinValue;

    /// <summary>The repos whose suite is running now.</summary>
    public IReadOnlyCollection<string> Running => running.Keys;

    /// <summary>
    /// Reports the runs that finished (filing and closing tickets in <paramref name="openTickets"/>' database) and, every
    /// <see cref="CheckEvery"/> when <paramref name="mayStart"/> (the lease holder), starts a run for each repo whose main line
    /// moved. A repo seen for the first time is only recorded where it stands: the daemon starting isn't a landing.
    /// </summary>
    public IReadOnlyList<Line> Tick(IEnumerable<(string Name, string Path)> repos, string worktreeRoot, Func<TicketsDb?> openTickets, DateTime nowUtc, bool mayStart = true)
    {
        var lines = new List<Line>();
        var state = store.Load<Dictionary<string, RepoState>>(StateName);
        var changed = false;
        foreach (var done in running.Values.Where(r => r.Task.IsCompleted).ToList())
        {
            running.Remove(done.Repo);
            var st = state.TryGetValue(done.Repo, out var s) ? s : state[done.Repo] = new RepoState();
            var result = done.Task.IsCompletedSuccessfully ? done.Task.Result
                : new SuiteResult(false, [], new HashSet<string>(), done.Task.Exception?.GetBaseException().Message ?? "it was cancelled");
            lines.AddRange(Report(done, result, openTickets));
            // A run that didn't complete (timeout, file lock, dotnet missing, Prepare error) tested nothing: the head stays
            // untested so it's tried again after a pause, not skipped for good (M.ORCH-T731).
            if (!result.Completed) { retryAfter[done.Repo] = nowUtc + RetryIncompleteAfter; continue; }
            retryAfter.Remove(done.Repo);
            st.Tested = done.Head;
            if (result.Failed.Count == 0) st.Green = done.Head;
            changed = true;
        }

        if (mayStart && nowUtc - checkedUtc >= CheckEvery)
        {
            checkedUtc = nowUtc;
            foreach (var (name, path) in repos)
            {
                if (running.ContainsKey(name)) continue;
                if (retryAfter.TryGetValue(name, out var again) && nowUtc < again) continue;
                // Asked each check, not cached: a renamed default branch is followed (M.ORCH-T731).
                var mainline = MainlineOf(path);
                var (code, output) = Git(path, ["rev-parse", "--verify", "--quiet", "refs/remotes/origin/" + mainline]);
                if (code != 0 || output.Trim() is not { Length: > 0 } head) continue;
                if (!state.TryGetValue(name, out var st))
                {
                    state[name] = new RepoState { Tested = head };
                    changed = true;
                    continue;
                }
                if (string.Equals(st.Tested, head, StringComparison.OrdinalIgnoreCase)) continue;
                // Only data, docs or markdown landed (the orchestrator's own database sync every 10 minutes, a ticket view):
                // nothing a test reads changed, so it counts as tested where it was, green if that was.
                if (st.Tested is { } before && OnlyNonCode(path, before, head))
                {
                    if (string.Equals(st.Green, before, StringComparison.OrdinalIgnoreCase)) st.Green = head;
                    st.Tested = head;
                    changed = true;
                    continue;
                }
                // The solution comes from the commit being built, tracked files only: the repo's own folder may be behind
                // origin or hold an untracked or ignored generated solution the fresh worktree doesn't have (M.ORCH-T598).
                var (listed, tracked) = Git(path, ["ls-tree", "--name-only", head]);
                if (listed != 0 || SolutionIn(tracked.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) is not { } solution) continue;
                var since = Since(path, st.Green ?? st.Tested, head);
                var tree = Path.Combine(worktreeRoot, name, WorktreeName);
                running[name] = new Run(name, path, mainline, head, since, Spawn(() =>
                    Prepare(path, tree, head) is { } error ? new SuiteResult(false, [], new HashSet<string>(), error)
                        : RunSuite(Path.Combine(tree, solution))));
                lines.Add(new Line(false, $"main tests: running {name}'s full suite on {mainline} at {Short(head)}"));
            }
        }
        if (changed) store.Save(StateName, state);
        return lines;
    }

    /// <summary>Files a fix ticket for each failing test none is open for, and closes the open ones whose test passed.</summary>
    private IEnumerable<Line> Report(Run run, SuiteResult result, Func<TicketsDb?> openTickets)
    {
        if (!result.Completed)
        {
            yield return new Line(true, $"main tests: {run.Repo}'s full suite on {run.Mainline} at {Short(run.Head)} didn't complete: {result.Note}");
            yield break;
        }
        if (result.Failed.Count == 0)
            yield return new Line(false, $"main tests: {run.Repo}'s full suite passes on {run.Mainline} at {Short(run.Head)} ({result.Passed.Count(p => p != BuildName)} tests)");
        else
            yield return new Line(true, $"main tests: {result.Failed.Count} failing in {run.Repo} on {run.Mainline} at {Short(run.Head)}: {string.Join(", ", result.Failed.Select(f => f.Test).Take(5))}{(result.Failed.Count > 5 ? ", ..." : "")}");

        List<Line> filed;
        try
        {
            using var db = openTickets();
            if (db is null || !db.Repos().Any(r => r.Name.Equals(run.Repo, StringComparison.OrdinalIgnoreCase)))
            {
                filed = result.Failed.Count == 0 ? [] : [new Line(true, $"main tests: {run.Repo} isn't in the ticket database, so its failing tests weren't filed")];
            }
            else filed = [.. File(db, run.Repo, run.Mainline, run.Head, run.Since, result).Select(l => new Line(l.StartsWith("filed", StringComparison.Ordinal), "main tests: " + l))];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or System.Data.Common.DbException)
        {
            filed = [new Line(true, $"main tests: couldn't file or close {run.Repo}'s test tickets: {ex.Message}")];
        }
        foreach (var l in filed) yield return l;
    }

    /// <summary>The ticket work of a finished run in <paramref name="repo"/>: what was filed and closed, said.</summary>
    internal static IReadOnlyList<string> File(TicketsDb db, string repo, string mainline, string head, string since, SuiteResult result)
    {
        var said = new List<string>();
        var open = db.Tickets(repo).Select(t => t.Ticket)
            .Where(t => t.Status == TicketRules.Open && TestOf(t.Summary) is not null).ToList();
        // Compared as the summary holds them (one line, no '|'), so a parameterized test's name still matches its ticket.
        var passed = result.Passed.Select(OneLine).ToHashSet(StringComparer.Ordinal);
        var filed = new List<string>();
        foreach (var f in result.Failed)
        {
            if (open.FirstOrDefault(t => TestOf(t.Summary) == OneLine(f.Test)) is { } already)
            {
                said.Add($"{f.Test} still fails in {repo}; {already.Id} is open for it");
                continue;
            }
            var t = db.Add(repo, "fix", TicketRules.Open, Summary(f, mainline, since), $"{mainline}@{Short(head)}");
            open.Add(t);
            filed.Add(t.Id);
            said.Add($"filed {t.Id} in {repo}: {f.Test} fails on {mainline}");
        }
        foreach (var t in open.Where(t => TestOf(t.Summary) is { } test && passed.Contains(test)).ToList())
        {
            db.Close(t.Id, TicketRules.Closed, $"passes on {mainline}@{Short(head)}");
            said.Add($"closed {t.Id} in {repo}: {TestOf(t.Summary)} passes on {mainline} again");
        }
        if (filed.Count > 0) ToTheTop(db, filed);
        return said;
    }

    /// <summary>
    /// Puts the failing-test tickets at the top of the whole workspace's queue (M.ORCH-T562), in the order they failed: the ones
    /// already open (any repo) as the queue has them, then <paramref name="filed"/> as they failed, given descending priorities
    /// all above the highest any other open ticket holds. Only a priority that changes is written.
    /// </summary>
    internal static void ToTheTop(TicketsDb db, IReadOnlyList<string> filed)
    {
        var queue = db.Queue();
        var isNew = filed.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var failing = queue.Where(q => !isNew.Contains(q.Ticket.Id) && IsFailingTest(q.Ticket)).Select(q => q.Ticket.Id).Concat(filed).ToList();
        var band = failing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var top = Math.Max(0, queue.Where(q => !band.Contains(q.Ticket.Id)).Select(q => q.Priority).DefaultIfEmpty(0).Max());
        for (var i = 0; i < failing.Count; i++) db.SetPriority(failing[i], top + failing.Count - i);
    }

    private static bool IsFailingTest(Ticket t) =>
        t.Type.Equals("fix", StringComparison.OrdinalIgnoreCase) && TestOf(t.Summary) is not null;

    /// <summary>The ticket's one line: the test, how it failed, the tickets landed since the last green run and their commits,
    /// so the fix starts from what just changed (M.ORCH-T562).</summary>
    internal static string Summary(Failure f, string mainline, string since)
    {
        var message = OneLine(f.Message);
        if (message.Length > 300) message = message[..297] + "...";
        if (message.Length == 0) message = "no message";
        var tickets = LandedTickets(since);
        var landed = since.Length == 0 ? "not known"
            : $"{(tickets.Count > 0 ? "tickets " + string.Join(", ", tickets) : "no ticket named")} in {since}";
        return OneLine($"{SummaryPrefix}{f.Test}{Separator}{message} (landed on {mainline} since the last green run: {landed})");
    }

    /// <summary>The ticket IDs the commit subjects in <paramref name="since"/> name, newest first as they're listed, each once.</summary>
    internal static IReadOnlyList<string> LandedTickets(string since) =>
        [.. since.Split("; ").SelectMany(c => TicketId().Matches(c).Select(m => m.Value)).Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>The test a ticket this filed is for; null for any other ticket.</summary>
    internal static string? TestOf(string summary)
    {
        if (!summary.StartsWith(SummaryPrefix, StringComparison.Ordinal)) return null;
        var end = summary.IndexOf(Separator, SummaryPrefix.Length, StringComparison.Ordinal);
        return end < 0 ? null : summary[SummaryPrefix.Length..end];
    }

    private static string OneLine(string text) => Whitespace().Replace(text.Replace('|', '/'), " ").Trim();

    /// <summary>The solution among a commit's root entries (.slnx before .sln, then by name); null when it has none, which isn't run.</summary>
    internal static string? SolutionIn(IEnumerable<string> rootEntries)
    {
        var names = rootEntries.Where(n => !n.Contains('/') && !n.Contains('\\')).ToList();
        return names.Where(n => n.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.OrdinalIgnoreCase)
            .Concat(names.Where(n => n.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.OrdinalIgnoreCase)).FirstOrDefault();
    }

    /// <summary>Whether every path changed between the two commits is data, docs or markdown.</summary>
    private bool OnlyNonCode(string repo, string from, string to)
    {
        var (code, output) = Git(repo, ["diff", "--name-only", from, to]);
        if (code != 0) return false;
        var paths = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return paths.Length > 0 && paths.All(NonCode);
    }

    internal static bool NonCode(string path) =>
        path.StartsWith("data/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("docs/", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".md", StringComparison.OrdinalIgnoreCase);

    /// <summary>The commits from <paramref name="from"/> (exclusive) to <paramref name="head"/>, newest first: "abc1234 Subject (ID); ...".
    /// Only those that changed code: a database sync or a ticket view (data, docs, markdown) isn't what broke a test.</summary>
    private string Since(string repo, string? from, string head)
    {
        if (from is null) return "";
        var (code, output) = Git(repo, ["log", "--no-merges", "-n", "20", "--format=%h %s", $"{from}..{head}", "--", ".",
            ":(exclude)data", ":(exclude)docs", ":(exclude)*.md"]);
        return code != 0 ? "" : string.Join("; ", output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    /// <summary>Puts the suite's worktree at <paramref name="head"/>, detached, making it the first time; the error, if it couldn't.</summary>
    private string? Prepare(string repo, string tree, string head)
    {
        if (System.IO.File.Exists(Path.Combine(tree, ".git")) && Git(tree, ["checkout", "--detach", "--force", "-q", head]).Code == 0)
        {
            // Untracked files a test left behind go; ignored ones (bin, obj) stay, so the build is incremental.
            Git(tree, ["clean", "-fdq"]);
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
        return code == 0 ? null : $"git worktree add failed: {output.Trim()}";
    }

    private static string Short(string sha) => sha.Length > 7 ? sha[..7] : sha;

    /// <summary>
    /// <c>dotnet test</c> on <paramref name="solution"/>, read from its TRX results. Build servers are off: one left running
    /// would hold the output pipes open (and the run "timed out"). No results and a failed exit is a build that failed.
    /// </summary>
    internal static SuiteResult Dotnet(IProcessRunner runner, string solution, TimeSpan timeout, TimeSpan? retryDelay = null)
    {
        // A copy that fails because another process holds the file (MSB3027/MSB3021: a leftover testhost, antivirus, an
        // overlapping build) is the machine, not main: retried, and if it persists the run isn't completed, so no
        // "Fails on main: the build" ticket is filed for it (M.ORCH-T600).
        SuiteResult result = null!;
        for (var attempt = 0; attempt <= FileLockRetries; attempt++)
        {
            if (attempt > 0) Thread.Sleep(retryDelay ?? TimeSpan.FromSeconds(30));
            result = DotnetOnce(runner, solution, timeout);
            if (!(result.Completed && result.Failed.Count == 1 && result.Failed[0].Test == BuildName && IsFileLock(result.Failed[0].Message))) return result;
        }
        return new SuiteResult(false, [], new HashSet<string>(), $"the build couldn't copy a file another process held, after {FileLockRetries + 1} tries: {result.Failed[0].Message}");
    }

    internal const int FileLockRetries = 2;

    /// <summary>Whether a build's errors are a file another process holds, not a fault in the code.</summary>
    internal static bool IsFileLock(string errors) =>
        errors.Split("; ").Where(e => e.Contains(": error ", StringComparison.Ordinal)).ToList() is { Count: > 0 } lines && lines.All(IsLockError);

    private static bool IsLockError(string line) =>
        line.Contains("MSB3027", StringComparison.Ordinal) || line.Contains("MSB3021", StringComparison.Ordinal);

    private static SuiteResult DotnetOnce(IProcessRunner runner, string solution, TimeSpan timeout)
    {
        // Beside the worktree (main-tests-results), not in it: the next checkout's clean would take it.
        var tree = Path.GetDirectoryName(Path.GetFullPath(solution))!;
        var results = tree + "-results";
        try
        {
            if (Directory.Exists(results)) Directory.Delete(results, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return new SuiteResult(false, [], new HashSet<string>(), $"couldn't clear {results}: {ex.Message}"); }
        ProcessResult r;
        try { r = runner.Run("dotnet", ["test", solution, "--disable-build-servers", "--logger", "trx", "--results-directory", results], timeout); }
        catch (System.ComponentModel.Win32Exception ex) { return new SuiteResult(false, [], new HashSet<string>(), $"couldn't run dotnet: {ex.Message}"); }
        if (r.TimedOut) return new SuiteResult(false, [], new HashSet<string>(), $"it ran past {timeout.TotalMinutes:0} minutes and was stopped");
        var trx = Directory.Exists(results) ? Directory.GetFiles(results, "*.trx", SearchOption.AllDirectories) : [];
        if (trx.Length == 0)
            return r.ExitCode == 0 ? new SuiteResult(true, [], new HashSet<string> { BuildName })
                : new SuiteResult(true, [new Failure(BuildName, BuildErrors(r.Output + "\n" + r.Error))], new HashSet<string>());
        var (failed, passed) = ReadTrx(trx.Select(System.IO.File.ReadAllText));
        passed.Add(BuildName);
        return new SuiteResult(true, failed, passed);
    }

    /// <summary>The build's first few errors, else the end of its output.</summary>
    internal static string BuildErrors(string output)
    {
        var errors = output.Split('\n').Select(l => l.Trim()).Where(l => l.Contains(": error ", StringComparison.Ordinal)).Distinct()
            .OrderBy(IsLockError).Take(3).ToList(); // a real error is never crowded out by file-lock noise
        if (errors.Count > 0) return string.Join("; ", errors);
        var tail = output.Trim();
        return tail.Length > 300 ? tail[^300..] : tail;
    }

    /// <summary>The failed tests (by class and name, with their first message line) and the passed ones, across TRX files.</summary>
    internal static (List<Failure> Failed, HashSet<string> Passed) ReadTrx(IEnumerable<string> documents)
    {
        var failed = new List<Failure>();
        var passed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var text in documents)
        {
            XDocument doc;
            try { doc = XDocument.Parse(text); }
            catch (System.Xml.XmlException) { continue; }
            var classes = doc.Descendants().Where(e => e.Name.LocalName == "UnitTest")
                .Select(u => (Id: (string?)u.Attribute("id"), Class: (string?)u.Elements().FirstOrDefault(e => e.Name.LocalName == "TestMethod")?.Attribute("className")))
                .Where(u => u.Id is not null).GroupBy(u => u.Id!).ToDictionary(g => g.Key, g => g.First().Class);
            foreach (var result in doc.Descendants().Where(e => e.Name.LocalName == "UnitTestResult"))
            {
                var test = (string?)result.Attribute("testName") ?? "";
                if ((string?)result.Attribute("testId") is { } id && classes.GetValueOrDefault(id) is { Length: > 0 } cls) test = $"{cls}.{test}";
                switch ((string?)result.Attribute("outcome"))
                {
                    case "Passed":
                        passed.Add(test);
                        break;
                    case "Failed" or "Error" or "Timeout" or "Aborted":
                        var message = result.Descendants().FirstOrDefault(e => e.Name.LocalName == "Message")?.Value ?? "";
                        failed.Add(new Failure(test, message.Trim()));
                        break;
                }
            }
        }
        // A test that failed in one file and passed in another (two target frameworks) still fails.
        passed.ExceptWith(failed.Select(f => f.Test));
        return ([.. failed.DistinctBy(f => f.Test)], passed);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"\b(?:[A-Z]\.)?[A-Z][A-Z0-9]*-T\d+\b")]
    private static partial Regex TicketId();
}
