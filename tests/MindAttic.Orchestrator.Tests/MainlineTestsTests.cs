using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using MindAttic.Tickets.Data;
using MindAttic.Tickets.Log;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T561: the full suite runs on main in the daemon, with no LLM, after a landing; subagents don't wait on it.</summary>
public sealed class MainlineTestsTests
{
    private readonly TempDirs temps = new();
    private string dir = "";
    private string repo = "";
    private JsonStore store = null!;
    private string head = "aaaaaaa1";
    private string diff = "src/A.cs";
    private string root = "A.slnx";
    private readonly List<string[]> git = [];
    private readonly List<TaskCompletionSource<MainlineTests.SuiteResult>> runs = [];
    private static readonly DateTime Now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void SolutionIn_picks_a_tracked_root_solution_slnx_first()
    {
        Assert.That(MainlineTests.SolutionIn(["B.sln", "src", "Z.slnx", "A.slnx"]), Is.EqualTo("A.slnx"));
        Assert.That(MainlineTests.SolutionIn(["B.sln", "src"]), Is.EqualTo("B.sln"));
        Assert.That(MainlineTests.SolutionIn(["README.md", "src"]), Is.Null);
    }

    [Test]
    public void The_solution_is_chosen_from_the_built_commit_not_the_repo_folder()
    {
        // The repo folder holds A.slnx, but the commit being built tracks only B.slnx.
        string? ran = null;
        var tests = Make();
        var inner = tests.Git;
        tests = new MainlineTests(store)
        {
            Git = (p, a) => a[0] == "ls-tree" ? (0, "B.slnx\n") : inner(p, a),
            MainlineOf = _ => "main",
            RunSuite = s => { ran = s; return Result([], []); },
            Spawn = w => Task.FromResult(w()),
        };
        Tick(tests, Now);
        head = "bbbbbbb2";
        Tick(tests, Now.AddMinutes(5));
        Assert.That(ran, Is.EqualTo(Path.Combine(dir, "wt", "A", MainlineTests.WorktreeName, "B.slnx")));
    }

    [SetUp]
    public void SetUp()
    {
        dir = temps.New();
        repo = Path.Combine(dir, "A");
        Directory.CreateDirectory(Path.Combine(repo, "docs"));
        File.WriteAllText(Path.Combine(repo, "A.slnx"), "<Solution />");
        File.WriteAllText(Path.Combine(repo, "docs", "USER_STORIES.md"),
            "---\ncode: A\n---\n\n# A\n\n## Tickets\n\n| ID | Date | Type | Status | Summary | Ref |\n|---|---|---|---|---|---|\n" +
            "| A-T1 | 2026-10-01 | fix | open | unrelated | — |\n");
        using (var db = TicketsDb.Open(TicketsDb.DefaultPath(dir)))
            db.Import([new MindAttic.Tickets.Repo("A", repo)]);
        store = new JsonStore(Path.Combine(dir, "store"));
        head = "aaaaaaa1";
        diff = "src/A.cs";
        git.Clear();
        runs.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        temps.DeleteAll();
    }

    private MainlineTests Make() => new(store)
    {
        Git = (path, args) =>
        {
            git.Add(args);
            return args[0] switch
            {
                "rev-parse" => (0, head + "\n"),
                "diff" => (0, diff + "\n"),
                "ls-tree" => (0, root),
                "log" => (0, "bbbbbbb Fix the parser (A-T7)\ncccccc2 Add the thing (A-T8)\n"),
                _ => (0, ""),
            };
        },
        MainlineOf = _ => "main",
        RunSuite = _ => throw new InvalidOperationException("Spawn stands in for the run"),
        Spawn = _ =>
        {
            var run = new TaskCompletionSource<MainlineTests.SuiteResult>();
            runs.Add(run);
            return run.Task;
        },
    };

    private IReadOnlyList<MainlineTests.Line> Tick(MainlineTests tests, DateTime at) =>
        tests.Tick([("A", repo)], Path.Combine(dir, "wt"), () => TicketsDb.Open(TicketsDb.DefaultPath(dir)), at);

    private static MainlineTests.SuiteResult Result(string[] failed, string[] passed) =>
        new(true, [.. failed.Select(f => new MainlineTests.Failure(f, $"{f} expected 1 but was 2"))], new HashSet<string>([.. passed, MainlineTests.BuildName]));

    private List<Ticket> Filed()
    {
        using var db = TicketsDb.Open(TicketsDb.DefaultPath(dir));
        return [.. db.Tickets("A").Select(t => t.Ticket).Where(t => MainlineTests.TestOf(t.Summary) is not null)];
    }

    [Test]
    public void The_claim_prompt_runs_only_the_touched_tests_and_leaves_the_full_suite_to_the_orchestrator()
    {
        var item = new QueueItem("A", @"D:\W\A", new Ticket("A-T1", "2026-10-01", "fix", "open", "x", "—"), 0, null, false);
        var prompt = Autopilot.PromptFor(item, @"D:\WT\A\A-T1", "orch/a-t1", "main");

        Assert.That(prompt, Does.Contain("run only the tests your change touches"));
        Assert.That(prompt, Does.Contain("the orchestrator runs A's full suite on main itself and files a fix ticket for any test that fails, so don't wait for a full run"));
        Assert.That(prompt.IndexOf("run only the tests", StringComparison.Ordinal), Is.LessThan(prompt.IndexOf("/commit", StringComparison.Ordinal)), "the touched tests come before the commit");
    }

    [Test]
    public void A_repo_seen_first_is_recorded_not_run_and_a_landing_starts_one_run_that_later_landings_coalesce_into()
    {
        var tests = Make();
        Assert.That(Tick(tests, Now), Is.Empty, "the daemon starting isn't a landing");
        Assert.That(runs, Is.Empty);

        head = "aaaaaaa2";
        Assert.That(Tick(tests, Now.AddMinutes(1)).Single().Text, Does.Contain("running A's full suite on main at aaaaaaa"));
        Assert.That(runs, Has.Count.EqualTo(1));

        head = "aaaaaaa3"; // landed while the run goes
        Tick(tests, Now.AddMinutes(2));
        Assert.That(runs, Has.Count.EqualTo(1), "none starts while one runs for that repo");

        runs[0].SetResult(Result([], ["A.Tests.One"]));
        var lines = Tick(tests, Now.AddMinutes(3));
        Assert.That(lines.Select(l => l.Text), Has.Some.Contains("passes on main at aaaaaaa"));
        Assert.That(runs, Has.Count.EqualTo(2), "what landed meanwhile gets one run of its own");
        Assert.That(store.Load<Dictionary<string, MainlineTests.RepoState>>(MainlineTests.StateName)["A"].Green, Is.EqualTo("aaaaaaa2"));
        Assert.That(Filed(), Is.Empty, "nothing is filed when every test passes");
    }

    [Test]
    public void Failing_tests_are_filed_once_each_as_fixes_and_closed_when_they_pass_again()
    {
        var tests = Make();
        Tick(tests, Now);
        head = "aaaaaaa2";
        Tick(tests, Now.AddMinutes(1));
        runs[0].SetResult(Result(["A.Tests.One", "A.Tests.Two"], ["A.Tests.Three"]));
        var lines = Tick(tests, Now.AddMinutes(2));

        var filed = Filed();
        Assert.That(filed.Select(t => (MainlineTests.TestOf(t.Summary), t.Type, t.Status)),
            Is.EquivalentTo(new[] { ("A.Tests.One", "fix", "open"), ("A.Tests.Two", "fix", "open") }));
        Assert.That(filed[0].Summary, Does.Contain("A.Tests.One expected 1 but was 2").And.Contains("bbbbbbb Fix the parser (A-T7); cccccc2 Add the thing (A-T8)"),
            "the failure message and the commits landed since the last green run");
        Assert.That(lines.Where(l => l.Warn).Select(l => l.Text), Has.Some.Contains("filed A-T2"));

        // Still failing on the next run: not filed twice. Two passes again: its ticket closes.
        head = "aaaaaaa3";
        Tick(tests, Now.AddMinutes(3));
        runs[1].SetResult(Result(["A.Tests.One"], ["A.Tests.Two", "A.Tests.Three"]));
        lines = Tick(tests, Now.AddMinutes(4));
        filed = Filed();
        Assert.That(filed, Has.Count.EqualTo(2), "A.Tests.One isn't filed again");
        Assert.That(filed.Single(t => MainlineTests.TestOf(t.Summary) == "A.Tests.One").Status, Is.EqualTo(TicketRules.Open));
        Assert.That(filed.Single(t => MainlineTests.TestOf(t.Summary) == "A.Tests.Two").Status, Is.EqualTo(TicketRules.Closed));
        Assert.That(lines.Select(l => l.Text), Has.Some.Contains("still fails").And.Some.Contains("passes on main again"));
        Assert.That(store.Load<Dictionary<string, MainlineTests.RepoState>>(MainlineTests.StateName)["A"].Green, Is.Null, "not green while one fails");
    }

    [Test]
    public void Failing_test_tickets_go_to_the_top_of_the_queue_in_the_order_they_failed_naming_the_landed_tickets()
    {
        // M.ORCH-T562: above every other open ticket, the first failure first; later failures queue behind earlier ones.
        using (var db = TicketsDb.Open(TicketsDb.DefaultPath(dir))) db.SetPriority("A-T1", 5);
        var tests = Make();
        Tick(tests, Now);
        head = "aaaaaaa2";
        Tick(tests, Now.AddMinutes(1));
        Assert.That(git.Single(a => a[0] == "log"), Does.Contain(":(exclude)data").And.Contain(":(exclude)*.md"), "only commits that changed code");
        runs[0].SetResult(Result(["A.Tests.One", "A.Tests.Two"], []));
        Tick(tests, Now.AddMinutes(2));

        List<string> Order()
        {
            using var db = TicketsDb.Open(TicketsDb.DefaultPath(dir));
            return [.. db.Queue().Select(q => q.Ticket.Id)];
        }
        Assert.That(Order(), Is.EqualTo(new[] { "A-T2", "A-T3", "A-T1" }));
        Assert.That(Filed()[0].Summary, Does.Contain("since the last green run: tickets A-T7, A-T8 in bbbbbbb Fix the parser (A-T7)"));

        // One still fails, Two passes (closed), Three fails for the first time: it goes behind One, still ahead of A-T1.
        head = "aaaaaaa3";
        Tick(tests, Now.AddMinutes(3));
        runs[1].SetResult(Result(["A.Tests.One", "A.Tests.Three"], ["A.Tests.Two"]));
        Tick(tests, Now.AddMinutes(4));
        Assert.That(Order(), Is.EqualTo(new[] { "A-T2", "A-T4", "A-T1" }));
    }

    [Test]
    public void A_run_that_did_not_complete_files_and_closes_nothing()
    {
        var tests = Make();
        Tick(tests, Now);
        head = "aaaaaaa2";
        Tick(tests, Now.AddMinutes(1));
        runs[0].SetResult(new MainlineTests.SuiteResult(false, [], new HashSet<string>(), "it ran past 90 minutes and was stopped"));
        var lines = Tick(tests, Now.AddMinutes(2));
        Assert.That(lines.Single().Text, Does.Contain("didn't complete: it ran past 90 minutes"));
        Assert.That(Filed(), Is.Empty);
    }

    [Test]
    public void A_landing_of_only_data_docs_or_markdown_is_not_run_and_keeps_green()
    {
        var tests = Make();
        Tick(tests, Now);
        head = "aaaaaaa2";
        Tick(tests, Now.AddMinutes(1));
        runs[0].SetResult(Result([], ["A.Tests.One"]));
        Tick(tests, Now.AddMinutes(2));

        head = "aaaaaaa3";
        diff = "data/orchestrator.db\ndata/orchestrator.sql\ndocs/USER_STORIES.md";
        Assert.That(Tick(tests, Now.AddMinutes(3)), Is.Empty);
        Assert.That(runs, Has.Count.EqualTo(1));
        var state = store.Load<Dictionary<string, MainlineTests.RepoState>>(MainlineTests.StateName)["A"];
        Assert.That((state.Tested, state.Green), Is.EqualTo(("aaaaaaa3", "aaaaaaa3")));
    }

    [Test]
    public void Main_is_read_at_most_once_a_minute_and_a_repo_with_no_solution_is_left_alone()
    {
        var tests = Make();
        Tick(tests, Now);
        var reads = git.Count(a => a[0] == "rev-parse");
        Tick(tests, Now.AddSeconds(15));
        Assert.That(git.Count(a => a[0] == "rev-parse"), Is.EqualTo(reads));

        root = "README.md";
        head = "aaaaaaa2";
        Tick(tests, Now.AddMinutes(2));
        Assert.That(runs, Is.Empty);
    }

    [Test]
    public void Trx_results_give_failures_by_class_and_name_and_a_test_failing_in_any_file_fails()
    {
        const string ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        static string Trx(string outcome) => $"""
            <TestRun xmlns="{ns}">
              <Results>
                <UnitTestResult testId="1" testName="Adds" outcome="{outcome}"><Output><ErrorInfo><Message>  Expected: 2
              But was: 3 </Message></ErrorInfo></Output></UnitTestResult>
                <UnitTestResult testId="2" testName="Subtracts" outcome="Passed" />
              </Results>
              <TestDefinitions>
                <UnitTest id="1" name="Adds"><TestMethod className="A.Tests.MathTests" name="Adds" /></UnitTest>
                <UnitTest id="2" name="Subtracts"><TestMethod className="A.Tests.MathTests" name="Subtracts" /></UnitTest>
              </TestDefinitions>
            </TestRun>
            """;
        var (failed, passed) = MainlineTests.ReadTrx([Trx("Failed"), Trx("Passed"), "not xml"]);
        Assert.That(failed.Select(f => f.Test), Is.EqualTo(new[] { "A.Tests.MathTests.Adds" }));
        Assert.That(failed[0].Message, Does.StartWith("Expected: 2"));
        Assert.That(passed, Is.EquivalentTo(new[] { "A.Tests.MathTests.Subtracts" }));
    }

    [Test]
    public void A_ticket_summary_is_one_line_and_names_its_test()
    {
        var summary = MainlineTests.Summary(new MainlineTests.Failure("A.Tests.X(1|2)", "line one\r\nline | two"), "main", "abc Fix (A-T3)");
        Assert.That(summary, Does.Not.Contain("\n").And.Not.Contain("|"));
        Assert.That(summary, Does.StartWith("Fails on main: A.Tests.X(1/2) — line one line / two"));
        Assert.That(MainlineTests.TestOf(summary), Is.EqualTo("A.Tests.X(1/2)"));
        Assert.That(MainlineTests.TestOf("Fix the parser"), Is.Null);
        Assert.That(MainlineTests.Summary(new MainlineTests.Failure("T", "m"), "main", "abc Sync (M.ORCH-T524); def Tidy"), Does.EndWith("tickets M.ORCH-T524 in abc Sync (M.ORCH-T524); def Tidy)"));
        Assert.That(MainlineTests.Summary(new MainlineTests.Failure("T", "m"), "main", ""), Does.EndWith("green run: not known)"));
        Assert.That(MainlineTests.BuildErrors("ok\n  src/A.cs(3,1): error CS1002: ; expected\nmore"), Is.EqualTo("src/A.cs(3,1): error CS1002: ; expected"));
    }
}

public sealed class MainlineTestsFileLockTests
{
    private sealed class Runner(Queue<ProcessResult> results) : IProcessRunner
    {
        public int Calls;
        public ProcessResult Run(string file, IReadOnlyList<string> args, TimeSpan timeout) { Calls++; return results.Dequeue(); }
    }

    private const string Lock = "Microsoft.Common.CurrentVersion.targets(5096,5): error MSB3027: Could not copy \"a.dll\" to \"b\". Exceeded retry count of 10.";

    [Test]
    public void A_file_lock_that_clears_on_retry_is_not_a_failure()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var runner = new Runner(new([new(1, Lock, "", false), new(0, "", "", false)]));
            var r = MainlineTests.Dotnet(runner, Path.Combine(dir, "x.sln"), TimeSpan.FromMinutes(1), TimeSpan.Zero);
            Assert.That(r.Completed);
            Assert.That(r.Failed, Is.Empty);
            Assert.That(runner.Calls, Is.EqualTo(2));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Test]
    public void A_file_lock_that_persists_is_not_completed_so_nothing_is_filed()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var runner = new Runner(new([new(1, Lock, "", false), new(1, Lock, "", false), new(1, Lock, "", false)]));
            var r = MainlineTests.Dotnet(runner, Path.Combine(dir, "x.sln"), TimeSpan.FromMinutes(1), TimeSpan.Zero);
            Assert.That(r.Completed, Is.False);
            Assert.That(runner.Calls, Is.EqualTo(3));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Test]
    public void A_real_compile_error_is_not_retried()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var runner = new Runner(new([new(1, "A.cs(1,1): error CS1002: ; expected", "", false)]));
            var r = MainlineTests.Dotnet(runner, Path.Combine(dir, "x.sln"), TimeSpan.FromMinutes(1), TimeSpan.Zero);
            Assert.That(r.Completed);
            Assert.That(r.Failed, Has.Count.EqualTo(1));
            Assert.That(runner.Calls, Is.EqualTo(1));
        }
        finally { Directory.Delete(dir, true); }
    }
}
