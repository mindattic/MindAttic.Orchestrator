using MindAttic.Orchestrator.Settings;
using MindAttic.Tickets.Data;
using MindAttic.Tickets.Log;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// The feature test gate (M.ORCH-T2398): a branch the owner assigned (its container labelled <see cref="AssignedDispatch.GateLabel"/>)
/// is done only once its repo's whole suite passes. Every <see cref="OrchestratorSettings.FeatureGateCheckSeconds"/>, each open
/// gated container none of whose children is still open gets one run of its repo's full suite on <c>origin/&lt;main line&gt;</c>,
/// the way <see cref="MainlineTests"/> runs one (no LLM, a detached worktree of its own, <see cref="WorktreeName"/>), one at a
/// time. Passing closes the container (<see cref="TicketsDb.Close"/>, so its own parents roll up); failing files one
/// <see cref="FixLabel"/> fix task per failing test under it, approved and assigned to the container's assignee, so the branch
/// carries on (<see cref="AssignedDispatch"/> starts them) and the gate runs again once they close. Each outcome is a note on it.
/// </summary>
public sealed class FeatureGate(JsonStore store)
{
    /// <summary>The worktree under <c>WorktreeRoot\&lt;repo&gt;</c> the gate's suites run in; not a ticket's name, so the sweep leaves it.</summary>
    public const string WorktreeName = "feature-gate";

    /// <summary>The label on each fix task a failing gate files.</summary>
    public const string FixLabel = "gate-fix";

    /// <summary>How each fix task a failing gate files starts; what follows up to the separator is the test's name.</summary>
    public const string SummaryPrefix = "Fails the feature gate: ";

    private const string Separator = " — ";

    /// <summary>The author of the notes the gate leaves.</summary>
    public const string NoteAuthor = "orchestrator (feature gate)";

    /// <summary>How long a container waits before a run that didn't complete is tried again.</summary>
    internal static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(10);

    /// <summary>One finished run's outcome: the commit tested and what the suite said.</summary>
    internal sealed record Outcome(string Head, MainlineTests.SuiteResult Result);

    private sealed record Run(string Container, string Repo, string Path, string Mainline, Task<Outcome> Task);

    internal Func<string, string[], (int Code, string Output)> Git { get; init; } = (repo, args) => Dispatcher.Git(repo, args);
    internal Func<string, string> MainlineOf { get; init; } = Dispatcher.Mainline;

    /// <summary>Runs the suite of the solution at the given path; a test's stands in for <c>dotnet test</c>.</summary>
    internal Func<string, MainlineTests.SuiteResult>? RunSuite { get; init; }

    /// <summary>Where a run goes: its own long-running task; a test's runs it inline.</summary>
    internal Func<Func<Outcome>, Task<Outcome>> Spawn { get; init; } =
        work => Task.Factory.StartNew(work, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    /// <summary>Why no suite may start now (the machine held, a main-line suite running); null lets one.</summary>
    internal Func<string?> Held { get; init; } = () => null;

    /// <summary>How long one suite may run (FeatureGateTimeoutMinutes; the daemon sets it before each tick).</summary>
    internal TimeSpan Timeout { get; set; } = MainlineTests.SuiteTimeout;

    /// <summary>How often gated containers are looked at (FeatureGateCheckSeconds; the daemon sets it before each tick).</summary>
    internal TimeSpan CheckEvery { get; set; } = TimeSpan.FromMinutes(1);

    internal int LockRetries { get; set; } = (int)OrchestratorSettings.DefaultMainTestsLockRetries;
    internal TimeSpan LockRetryDelay { get; set; } = TimeSpan.FromSeconds(OrchestratorSettings.DefaultMainTestsLockRetrySeconds);

    private Run? running;
    private readonly Dictionary<string, DateTime> retryAfter = new(StringComparer.OrdinalIgnoreCase);
    private DateTime checkedUtc = DateTime.MinValue;
    private string? heldSaid;

    /// <summary>The container whose suite is running now; null when none is.</summary>
    public string? Running => running?.Container;

    /// <summary>
    /// Reports the run that finished (closing its container, or filing its failing tests under it) and, every
    /// <see cref="CheckEvery"/> when <paramref name="mayStart"/> (the lease holder) and nothing runs, starts the next due container's.
    /// </summary>
    public IReadOnlyList<MainlineTests.Line> Tick(IEnumerable<(string Name, string Path)> repos, string worktreeRoot, Func<TicketsDb?> openTickets, DateTime nowUtc, bool mayStart = true)
    {
        var lines = new List<MainlineTests.Line>();
        if (running is { Task.IsCompleted: true } done)
        {
            running = null;
            var outcome = done.Task.IsCompletedSuccessfully ? done.Task.Result
                : new Outcome("", new MainlineTests.SuiteResult(false, [], new HashSet<string>(), done.Task.Exception?.GetBaseException().Message ?? "it was cancelled"));
            try
            {
                using var db = openTickets();
                if (db is null) retryAfter[done.Container] = nowUtc + RetryAfter;
                else lines.AddRange(Report(db, done, outcome, nowUtc));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or System.Data.Common.DbException)
            {
                retryAfter[done.Container] = nowUtc + RetryAfter;
                lines.Add(new(true, $"feature gate: couldn't record {done.Container}'s suite: {ex.Message}"));
            }
        }
        if (!mayStart || running is not null || nowUtc - checkedUtc < CheckEvery) return lines;
        checkedUtc = nowUtc;

        var paths = repos.GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Path, StringComparer.OrdinalIgnoreCase);
        (string Container, string Repo)? due;
        try
        {
            using var db = openTickets();
            if (db is null) return lines;
            due = Due(db, nowUtc).Where(d => paths.ContainsKey(d.Repo)).Cast<(string, string)?>().FirstOrDefault();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Data.Common.DbException)
        {
            lines.Add(new(true, $"feature gate: couldn't read the gated tickets: {ex.Message}"));
            return lines;
        }
        if (due is not { } next) return lines;
        if (Held() is { } why)
        {
            if (heldSaid is null) lines.Add(new(false, $"feature gate: holding {next.Container}'s suite: {why}"));
            heldSaid = why;
            return lines;
        }
        heldSaid = null;
        var path = paths[next.Repo];
        var mainline = MainlineOf(path);
        var tree = Path.Combine(worktreeRoot, next.Repo, WorktreeName);
        running = new Run(next.Container, next.Repo, path, mainline, Spawn(() => RunOnMain(path, mainline, tree)));
        lines.Add(new(false, $"feature gate: every task of {next.Container} is done; running {next.Repo}'s full suite on {mainline}"));
        return lines;
    }

    /// <summary>The open containers labelled <see cref="AssignedDispatch.GateLabel"/> with children, none of them open, not waiting
    /// out a retry: (container, repo), oldest first.</summary>
    private IEnumerable<(string Container, string Repo)> Due(TicketsDb db, DateTime nowUtc)
    {
        var gated = db.Find(TicketQuery.Parse("status:open"))
            .Where(f => f.Labels?.Contains(AssignedDispatch.GateLabel, StringComparer.OrdinalIgnoreCase) == true).ToList();
        if (gated.Count == 0) yield break;
        var graph = db.Graph();
        foreach (var f in gated.OrderBy(f => f.Ticket.Number ?? int.MaxValue))
        {
            if (retryAfter.TryGetValue(f.Ticket.Id, out var again) && nowUtc < again) continue;
            var children = graph.Children(f.Ticket.Id).Where(c => c.IsTicket).ToList();
            if (children.Count > 0 && !children.Any(c => c.IsOpen)) yield return (f.Ticket.Id, f.Repo);
        }
    }

    /// <summary>Fetches, puts the gate's worktree at <c>origin/&lt;mainline&gt;</c> and runs the solution's whole suite there.</summary>
    private Outcome RunOnMain(string path, string mainline, string tree)
    {
        MainlineTests.SuiteResult Incomplete(string why) => new(false, [], new HashSet<string>(), why);
        Git(path, ["fetch", "--quiet", "origin"]);
        var (code, output) = Git(path, ["rev-parse", "--verify", "--quiet", "refs/remotes/origin/" + mainline]);
        if (code != 0 || output.Trim() is not { Length: > 0 } head) return new("", Incomplete($"origin/{mainline} couldn't be read"));
        var (listed, tracked) = Git(path, ["ls-tree", "--name-only", head]);
        if (listed != 0 || MainlineTests.SolutionIn(tracked.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) is not { } solution)
            return new(head, new MainlineTests.SuiteResult(true, [], new HashSet<string> { MainlineTests.BuildName }, "no solution at its root; nothing to test"));
        if (new MainlineTests(store) { Git = Git }.Prepare(path, tree, head) is { } error) return new(head, Incomplete(error));
        var suite = (RunSuite ?? (s => MainlineTests.Dotnet(ProcessRunner.Contained, s, Timeout, LockRetryDelay, LockRetries, MainlineTests.EndLeftoverProcesses)))(
            Path.Combine(tree, solution));
        return new(head, suite);
    }

    /// <summary>Records a finished run on its container: a note always; closed when it passed and nothing under it opened since;
    /// a fix task per failing test none is open for when it failed.</summary>
    private List<MainlineTests.Line> Report(TicketsDb db, Run run, Outcome outcome, DateTime nowUtc)
    {
        var lines = new List<MainlineTests.Line>();
        var result = outcome.Result;
        var at = outcome.Head.Length > 0 ? $"{run.Mainline}@{Short(outcome.Head)}" : run.Mainline;
        if (!result.Completed)
        {
            retryAfter[run.Container] = nowUtc + RetryAfter;
            lines.Add(new(true, $"feature gate: {run.Container}'s suite on {at} didn't complete: {result.Note}; trying again later"));
            return lines;
        }
        retryAfter.Remove(run.Container);
        if (db.Ticket(run.Container) is not { Ticket.Status: TicketRules.Open }) return lines;
        var children = db.Graph().Children(run.Container).Where(c => c.IsTicket).ToList();
        var passed = result.Passed.Count(p => p != MainlineTests.BuildName);
        if (result.Failed.Count == 0)
        {
            if (children.Any(c => c.IsOpen))
            {
                db.AddNote(run.Container, NoteAuthor, $"Feature gate: the full suite passed on {at} ({passed} tests), but a task under it opened meanwhile; it runs again once that closes.");
                lines.Add(new(false, $"feature gate: {run.Container}'s suite passes on {at}, but a task under it is open again; not closed"));
                return lines;
            }
            db.AddNote(run.Container, NoteAuthor, $"Feature gate: the full suite passed on {at} ({passed} tests{(result.Note is { } note ? $"; {note}" : "")}). Closed.");
            db.Close(run.Container, TicketRules.Closed, $"tests pass {at} ({passed} tests)");
            lines.Add(new(false, $"feature gate: {run.Container}'s suite passes on {at} ({passed} tests); closed it"));
            return lines;
        }

        var openTests = children.Where(c => c.IsOpen && TestOf(c.Summary) is not null).Select(c => TestOf(c.Summary)!).ToHashSet(StringComparer.Ordinal);
        var assignee = db.Assignees().GetValueOrDefault(run.Container);
        var filed = new List<string>();
        foreach (var f in result.Failed)
        {
            if (openTests.Contains(OneLine(f.Test))) continue;
            var t = db.Add(run.Repo, "fix", TicketRules.Open, Summary(f, run.Container), at);
            db.AddLink(t.Id, run.Container, TicketsDb.PartOf);
            db.SetLevel(t.Id, "task");
            db.AddLabels(t.Id, [FixLabel]);
            db.Approve(t.Id);
            if (assignee is not null) db.Assign(t.Id, assignee);
            openTests.Add(OneLine(f.Test));
            filed.Add(t.Id);
        }
        var named = string.Join(", ", result.Failed.Select(f => f.Test).Take(5)) + (result.Failed.Count > 5 ? ", ..." : "");
        db.AddNote(run.Container, NoteAuthor, $"Feature gate: {result.Failed.Count} failing on {at} ({named}). " +
            (filed.Count > 0 ? $"Filed {string.Join(", ", filed)} under it{(assignee is null ? "" : $" for {assignee}")}; it runs again once they close." : "Each already has an open task under it."));
        lines.Add(new(true, $"feature gate: {result.Failed.Count} failing in {run.Repo} on {at} for {run.Container}" +
            (filed.Count > 0 ? $"; filed {string.Join(", ", filed)} under it" : "")));
        return lines;
    }

    /// <summary>A fix task's one line: the test, how it failed, and the branch it holds back.</summary>
    internal static string Summary(MainlineTests.Failure f, string container)
    {
        var message = OneLine(f.Message);
        if (message.Length > 300) message = message[..297] + "...";
        if (message.Length == 0) message = "no message";
        return $"{SummaryPrefix}{OneLine(f.Test)}{Separator}{message} (holds back {container})";
    }

    /// <summary>The test a fix task this filed is for; null for any other ticket.</summary>
    internal static string? TestOf(string? summary)
    {
        if (summary is null || !summary.StartsWith(SummaryPrefix, StringComparison.Ordinal)) return null;
        var end = summary.IndexOf(Separator, SummaryPrefix.Length, StringComparison.Ordinal);
        return end < 0 ? null : summary[SummaryPrefix.Length..end];
    }

    private static string OneLine(string text) => string.Join(' ', text.Replace('|', '/').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string Short(string sha) => sha.Length > 7 ? sha[..7] : sha;
}
