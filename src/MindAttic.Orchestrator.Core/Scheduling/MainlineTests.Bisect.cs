using MindAttic.Orchestrator.Settings;
using MindAttic.Tickets.Data;
using MindAttic.Tickets.Log;

namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// Bisect to the breaker (M.ORCH-T1687, roadmap Phase 9). When a main-line suite goes red and files new "Fails on main" tickets,
/// the commits from the last green run to the red head are bisected, in a worktree of their own (<see cref="BisectWorktreeName"/>,
/// never the shared checkout), to the one that broke them. First the red head is run again: a failure that doesn't reproduce
/// there (a full disk, MSB3883 like M.ORCH-T1666, a flaky test) blames no one, and neither does one that also fails at the last
/// green commit. A step whose build breaks for some other reason is skipped, and a step that hits a build-environment error
/// stops the bisect. It takes the same single heavy slot the suites do (<see cref="MainlineTests.MaxRunning"/>), waits while the
/// memory guard holds them (<see cref="MainlineTests.Held"/>) and stops if the guard holds mid-way, and is bounded by
/// <see cref="BisectMaxSteps"/> and <see cref="BisectTimeout"/> (Vault settings).
/// <para>The tickets library has no reopen (tickets are append-only: a closed ticket can't be reopened), so the culprit's ticket is
/// "reopened" the append-only way: each "Fails on main" ticket the red run filed, while nobody has started on it, is replaced by
/// one whose brief names the culprit commit and its ticket (still a "Fails on main" ticket for the same test, so it dedupes and
/// closes itself on a pass as before), linked as a follow-up of the culprit's ticket and superseding the one it replaces, which
/// is closed won't-do.</para>
/// </summary>
public sealed partial class MainlineTests
{
    /// <summary>The worktree under <c>WorktreeRoot\&lt;repo&gt;</c> a bisect runs in; not a ticket's name, so the sweep leaves it.</summary>
    public const string BisectWorktreeName = "main-bisect";

    /// <summary>What a bisected ticket's brief says before the culprit commit.</summary>
    internal const string BisectedMark = "bisected to ";

    /// <summary>The most suite runs one bisect makes after its two checks (the MainBisectMaxSteps Vault setting; 0 turns it off).</summary>
    internal int BisectMaxSteps { get; set; } = (int)OrchestratorSettings.DefaultMainBisectMaxSteps;

    /// <summary>How long one bisect may take in all (the MainBisectTimeoutMinutes Vault setting).</summary>
    internal TimeSpan BisectTimeout { get; set; } = TimeSpan.FromMinutes(OrchestratorSettings.DefaultMainBisectTimeoutMinutes);

    /// <summary>The clock a bisect's time limit is read against; a test's stands in.</summary>
    internal Func<DateTime> Clock { get; init; } = () => DateTime.UtcNow;

    /// <summary>Where a bisect goes: its own long-running task; a test's runs it inline.</summary>
    internal Func<Func<BisectResult>, Task<BisectResult>> SpawnBisect { get; init; } =
        work => Task.Factory.StartNew(work, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    /// <summary>The bisect step limit in <paramref name="s"/>, kept within 0 to 64.</summary>
    public static int BisectMaxStepsOf(OrchestratorSettings s) =>
        double.IsFinite(s.MainBisectMaxSteps) ? (int)Math.Clamp(s.MainBisectMaxSteps, 0, 64) : (int)OrchestratorSettings.DefaultMainBisectMaxSteps;

    /// <summary>The bisect time limit in <paramref name="s"/>; the default when it isn't a usable number.</summary>
    public static TimeSpan BisectTimeoutOf(OrchestratorSettings s) =>
        OrchestratorSettings.MinutesSpan(s.MainBisectTimeoutMinutes, TimeSpan.FromMinutes(OrchestratorSettings.DefaultMainBisectTimeoutMinutes));

    /// <summary>A bisect's outcome: the first bad commit and its subject, or null with <see cref="Note"/> saying why no one was blamed.</summary>
    public sealed record BisectResult(string? Culprit, string? Subject, int Steps, string Note);

    /// <summary>What one bisect step's suite run says about its commit.</summary>
    internal enum Verdict { Good, Bad, Skip, Environment }

    private sealed record PendingBisect(string Repo, string Path, string Mainline, string Green, string Head, IReadOnlyList<(string Id, Failure Failure)> Filed);

    private sealed record BisectRun(PendingBisect Of, Task<BisectResult> Task);

    private readonly Dictionary<string, PendingBisect> pendingBisects = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, BisectRun> bisects = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The repos being bisected now.</summary>
    public IReadOnlyCollection<string> Bisecting => bisects.Keys;

    /// <summary>Queues a bisect of a red run that filed new tickets, from <paramref name="green"/> (the last green commit) to its head;
    /// it starts at a check when the heavy slot is free. Nothing is queued with the bisect off, no green to start from, or a failure
    /// that reads as the build environment rather than code.</summary>
    private IEnumerable<Line> QueueBisect(Run run, string? green, SuiteResult result, IReadOnlyList<(string Id, Failure Failure)> filed)
    {
        if (filed.Count == 0 || BisectMaxSteps <= 0) yield break;
        if (green is null || string.Equals(green, run.Head, StringComparison.OrdinalIgnoreCase))
        {
            yield return new Line(false, $"main tests: {run.Repo} has no green run before {Short(run.Head)} to bisect from; the culprit isn't looked for");
            yield break;
        }
        if (result.Failed.FirstOrDefault(f => IsEnvironmentFailure(f.Message)) is { } env)
        {
            yield return new Line(true, $"main tests: {run.Repo}'s red run at {Short(run.Head)} reads as the build environment, not a commit ({Clip(OneLine(env.Message), 160)}); not bisected, no ticket blamed");
            yield break;
        }
        IReadOnlyList<(string Id, Failure Failure)> merged = pendingBisects.TryGetValue(run.Repo, out var before) ? [.. before.Filed, .. filed] : filed;
        pendingBisects[run.Repo] = new PendingBisect(run.Repo, run.Path, run.Mainline, before?.Green ?? green, run.Head, merged);
        yield return new Line(false, $"main tests: {run.Repo}'s red run at {Short(run.Head)} will be bisected from the last green {Short(green)} to the commit that broke it");
    }

    /// <summary>Starts the queued bisects the heavy slot and the memory guard allow, before any new suite.</summary>
    private void StartBisects(List<Line> lines, string worktreeRoot)
    {
        foreach (var p in pendingBisects.Values.ToList())
        {
            if (running.Count + bisects.Count >= MaxRunning) return;
            if (running.ContainsKey(p.Repo) || bisects.ContainsKey(p.Repo)) continue;
            if (Held() is { } why)
            {
                if (heldSaid is null) lines.Add(new Line(false, $"main tests: holding {p.Repo}'s bisect (and any suites due): {why}"));
                heldSaid = why;
                return;
            }
            pendingBisects.Remove(p.Repo);
            var tree = Path.Combine(worktreeRoot, p.Repo, BisectWorktreeName);
            var targets = p.Filed.Select(f => OneLine(f.Failure.Test)).ToHashSet(StringComparer.Ordinal);
            var (max, timeout) = (BisectMaxSteps, BisectTimeout);
            bisects[p.Repo] = new BisectRun(p, SpawnBisect(() => RunBisect(p.Path, tree, p.Green, p.Head, targets, max, timeout)));
            lines.Add(new Line(false, $"main tests: bisecting {p.Repo} from {Short(p.Green)} to {Short(p.Head)} (at most {max} steps, {timeout.TotalMinutes:0} minutes) for {string.Join(", ", targets.Take(3))}{(targets.Count > 3 ? ", ..." : "")}"));
        }
    }

    /// <summary>Reports the bisects that finished and files what they found.</summary>
    private IEnumerable<Line> FinishBisects(Func<TicketsDb?> openTickets)
    {
        foreach (var done in bisects.Values.Where(b => b.Task.IsCompleted).ToList())
        {
            bisects.Remove(done.Of.Repo);
            var p = done.Of;
            var found = done.Task.IsCompletedSuccessfully ? done.Task.Result
                : new BisectResult(null, null, 0, done.Task.Exception?.GetBaseException().Message ?? "it was cancelled");
            if (found.Culprit is null)
            {
                yield return new Line(false, $"main tests: {p.Repo}'s bisect from {Short(p.Green)} to {Short(p.Head)} blamed no one: {found.Note}");
                continue;
            }
            yield return new Line(true, $"main tests: {p.Repo}'s bisect found {Short(found.Culprit)} \"{Clip(found.Subject ?? "", 120)}\" broke {p.Mainline} ({found.Steps} steps)");
            List<Line> said;
            try
            {
                using var db = openTickets();
                said = db is null ? [new Line(true, $"main tests: no ticket database, so {p.Repo}'s culprit wasn't filed")]
                    : [.. Blame(db, p.Repo, p.Mainline, p.Green, p.Head, found, p.Filed).Select(l => new Line(l.StartsWith("filed", StringComparison.Ordinal), "main tests: " + l))];
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or KeyNotFoundException or System.Data.Common.DbException)
            {
                said = [new Line(true, $"main tests: couldn't file {p.Repo}'s culprit: {ex.Message}")];
            }
            foreach (var l in said) yield return l;
        }
    }

    /// <summary>
    /// The ticket work of a bisect that found its culprit: each ticket in <paramref name="filed"/> that's still open and that nobody
    /// has started on is replaced by a "Fails on main" ticket for the same test whose brief names the culprit commit and its ticket,
    /// linked as a follow-up of that ticket (when it's in the database) and superseding the one it replaces, which is closed
    /// won't-do; the new ones go to the top of the queue. What was done, said.
    /// </summary>
    internal static IReadOnlyList<string> Blame(TicketsDb db, string repo, string mainline, string green, string head, BisectResult found,
        IReadOnlyList<(string Id, Failure Failure)> filed)
    {
        var said = new List<string>();
        if (found.Culprit is not { } culprit) return said;
        var culpritTicket = CulpritTicket(found.Subject);
        var known = culpritTicket is not null && db.Ticket(culpritTicket) is not null;
        var replaced = new List<string>();
        foreach (var (id, f) in filed.DistinctBy(x => x.Id, StringComparer.OrdinalIgnoreCase))
        {
            if (db.Ticket(id) is not { } row || row.Ticket.Status != TicketRules.Open)
            {
                said.Add($"{id} isn't open any more; {Short(culprit)} broke {f.Test} but nothing was refiled");
                continue;
            }
            var work = db.Work(row.Ticket.Id);
            if (work.Session is not null || work.Login is not null)
            {
                said.Add($"{id} is already being worked; {f.Test} was bisected to {Short(culprit)}{(culpritTicket is null ? "" : $" ({culpritTicket})")}");
                continue;
            }
            var t = db.Add(repo, "fix", TicketRules.Open, BlameSummary(f, mainline, found, culpritTicket, green), $"{mainline}@{Short(head)}");
            try { db.AddLink(t.Id, row.Ticket.Id, "supersedes", $"bisected to {Short(culprit)}"); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException) { }
            if (known)
            {
                try { db.AddLink(t.Id, culpritTicket!, "follow-up-of", $"broke {mainline} in {Short(culprit)}"); }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException) { }
            }
            db.Close(row.Ticket.Id, TicketRules.WontDo, $"superseded by {t.Id}: bisected to {Short(culprit)}");
            replaced.Add(t.Id);
            said.Add($"filed {t.Id} in {repo}: {f.Test} broken by {Short(culprit)}{(culpritTicket is null ? "" : $" ({culpritTicket})")}, superseding {row.Ticket.Id}");
        }
        if (replaced.Count > 0) ToTheTop(db, replaced);
        return said;
    }

    /// <summary>The ticket a commit subject lands: the last ticket ID it names (the convention puts it at the end); null when none.</summary>
    internal static string? CulpritTicket(string? subject) =>
        subject is null ? null : TicketId().Matches(subject).Select(m => m.Value).LastOrDefault();

    /// <summary>A bisected ticket's one line: the test and how it failed, then the culprit commit, its ticket (whose work this reopens)
    /// and what to do, so the fix starts from the change that broke it.</summary>
    internal static string BlameSummary(Failure f, string mainline, BisectResult found, string? culpritTicket, string green)
    {
        var message = Clip(OneLine(f.Message), 300);
        if (message.Length == 0) message = "no message";
        var who = culpritTicket is null ? "no ticket named" : $"reopening {culpritTicket}'s work";
        return OneLine($"{SummaryPrefix}{f.Test}{Separator}{message} ({BisectedMark}{Short(found.Culprit ?? "")} \"{Clip(OneLine(found.Subject ?? ""), 160)}\" on {mainline}, " +
            $"{who}: fix what that commit changed so this passes again; bisected from the last green {Short(green)} in {found.Steps} steps)");
    }

    /// <summary>
    /// Bisects <paramref name="green"/>..<paramref name="head"/> in <paramref name="tree"/> for the commit that first fails any of
    /// <paramref name="targets"/>. Runs the red head first and the green commit second: a failure that doesn't reproduce at the head,
    /// or that fails at the green commit too, isn't a commit's doing and blames no one. Then <c>git bisect</c>, one suite per step, up
    /// to <paramref name="maxSteps"/> steps within <paramref name="timeout"/>, stopping when the memory guard holds or a step hits a
    /// build-environment error. The worktree is left out of bisect mode however it ends. Never throws for git or the suite.
    /// </summary>
    internal BisectResult RunBisect(string repo, string tree, string green, string head, IReadOnlySet<string> targets, int maxSteps, TimeSpan timeout)
    {
        var deadline = Clock() + timeout;
        if (System.IO.File.Exists(Path.Combine(tree, ".git"))) Git(tree, ["bisect", "reset"]); // a bisect a stopped daemon left
        if (Prepare(repo, tree, head) is { } error) return new BisectResult(null, null, 0, error);
        var steps = 0;
        try
        {
            var (atHead, why) = Judge(SuiteHere(tree, deadline), targets);
            if (atHead == Verdict.Environment) return new BisectResult(null, null, 0, $"the build environment failed at {Short(head)} ({why}), not a commit");
            if (atHead != Verdict.Bad)
                return new BisectResult(null, null, 0, $"the failure didn't reproduce at {Short(head)} ({(atHead == Verdict.Good ? "it passes there now" : why)}): a build-environment problem or a flaky test, not a commit");
            if (Git(tree, ["checkout", "--detach", "--force", "-q", green]).Code != 0) return new BisectResult(null, null, 0, $"couldn't check out the last green {Short(green)}");
            var (atGreen, whyGreen) = Judge(SuiteHere(tree, deadline), targets);
            if (atGreen == Verdict.Bad) return new BisectResult(null, null, 0, $"it fails at the last green {Short(green)} too: not a commit since (the machine, or a test that depends on the date or the network)");
            if (atGreen != Verdict.Good) return new BisectResult(null, null, 0, $"the last green {Short(green)} couldn't be judged ({whyGreen})");

            var (code, output) = Git(tree, ["bisect", "start", head, green]);
            if (code != 0) return new BisectResult(null, null, 0, $"git bisect start failed: {Clip(OneLine(output), 200)}");
            while (true)
            {
                if (FirstBad(output) is { } culprit) return new BisectResult(culprit, SubjectOf(tree, culprit), steps, "found");
                if (output.Contains("only 'skip'ped commits left", StringComparison.Ordinal))
                    return new BisectResult(null, null, steps, "only commits whose build broke for other reasons were left, so the culprit is among them");
                if (steps >= maxSteps) return new BisectResult(null, null, steps, $"it reached its {maxSteps}-step limit");
                if (Clock() >= deadline) return new BisectResult(null, null, steps, $"it ran past its {timeout.TotalMinutes:0}-minute limit");
                if (Held() is { } held) return new BisectResult(null, null, steps, $"stopped while the heavy work is held: {held}");
                var (verdict, whyStep) = Judge(SuiteHere(tree, deadline), targets);
                if (verdict == Verdict.Environment) return new BisectResult(null, null, steps, $"a step hit the build environment ({whyStep}), so nothing is blamed");
                steps++;
                (code, output) = Git(tree, ["bisect", verdict switch { Verdict.Good => "good", Verdict.Bad => "bad", _ => "skip" }]);
                if (code != 0 && FirstBad(output) is null) return new BisectResult(null, null, steps, $"git bisect failed: {Clip(OneLine(output), 200)}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new BisectResult(null, null, steps, ex.Message);
        }
        finally
        {
            Git(tree, ["bisect", "reset"]);
        }
    }

    /// <summary>The whole suite at the commit checked out in <paramref name="tree"/>, within what's left of the bisect's time.</summary>
    private SuiteResult SuiteHere(string tree, DateTime deadline)
    {
        var left = deadline - Clock();
        if (left <= TimeSpan.Zero) return new SuiteResult(false, [], new HashSet<string>(), "out of time");
        var (listed, tracked) = Git(tree, ["ls-tree", "--name-only", "HEAD"]);
        if (listed != 0 || SolutionIn(tracked.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) is not { } solution)
            return new SuiteResult(false, [], new HashSet<string>(), "no solution at this commit");
        Git(tree, ["clean", "-fdq"]);
        InitSubmodules(tree);
        var path = Path.Combine(tree, solution);
        return RunSuite is { } run ? run(path)
            : Dotnet(ProcessRunner.Default, path, left < Timeout ? left : Timeout, LockRetryDelay, LockRetries, EndLeftoverProcesses);
    }

    /// <summary>What a step's run says: bad when any target fails; good when none does and the build held; skip when it couldn't
    /// tell (the build broke for another reason, or the run didn't complete); environment when the machine, not the code, failed.</summary>
    internal static (Verdict Verdict, string? Why) Judge(SuiteResult r, IReadOnlySet<string> targets)
    {
        if (!r.Completed) return (IsEnvironmentFailure(r.Note) ? Verdict.Environment : Verdict.Skip, r.Note);
        if (r.Failed.FirstOrDefault(f => IsEnvironmentFailure(f.Message)) is { } env) return (Verdict.Environment, Clip(OneLine(env.Message), 160));
        var failed = r.Failed.Select(f => OneLine(f.Test)).ToHashSet(StringComparer.Ordinal);
        if (targets.Any(failed.Contains)) return (Verdict.Bad, null);
        if (failed.Contains(BuildName)) return (Verdict.Skip, "the build broke for another reason");
        return (Verdict.Good, null);
    }

    /// <summary>Whether an error is the machine (a full disk, MSB3883 like M.ORCH-T1666, a build node that died, no memory) rather than
    /// the code: such a red run is never bisected and never blames a ticket.</summary>
    internal static bool IsEnvironmentFailure(string? text) =>
        text is { Length: > 0 } && EnvironmentMarkers.Any(m => text.Contains(m, StringComparison.OrdinalIgnoreCase));

    private static readonly string[] EnvironmentMarkers =
    [
        "MSB3883", "MSB4166", "not enough space on the disk", "There is not enough space", "disk is full", "disk full",
        "No space left on device", "0x80070070", "Insufficient memory to continue", "Not enough memory resources",
    ];

    /// <summary>The commit <c>git bisect</c> named first bad ("&lt;sha&gt; is the first bad commit"); null when it hasn't yet.</summary>
    internal static string? FirstBad(string output)
    {
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (line.EndsWith("is the first bad commit", StringComparison.Ordinal) && line.Split(' ', 2)[0] is { Length: >= 7 } sha && CommitHash().IsMatch(sha))
                return sha;
        return null;
    }

    private string? SubjectOf(string tree, string commit)
    {
        var (code, output) = Git(tree, ["log", "-1", "--format=%s", commit]);
        return code == 0 ? output.Trim() : null;
    }

    private static string Clip(string text, int max) => text.Length > max ? text[..(max - 3)] + "..." : text;
}
