using System.Diagnostics;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Tickets;
using NUnit.Framework;
using Serilog.Events;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The daemon loop's safety: hook installs that fail are retried, only real "needs you" lines alert, and a hung
/// <c>tickets db sync</c> or <c>gh</c> is killed and logged instead of stalling the loop.</summary>
public sealed class DaemonSafetyTests
{
    private string dir = null!;
    private readonly List<(LogEventLevel Level, string Line)> said = [];
    private readonly List<(string Title, string Message, bool Urgent)> alerts = [];

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "orch-daemon-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        said.Clear();
        alerts.Clear();
    }

    [TearDown]
    public void TearDown() { try { Directory.Delete(dir, recursive: true); } catch (IOException) { } }

    private Daemon Make(Func<int, bool, IEnumerable<string?>>? hooks = null, IProcessRunner? runner = null, TimeSpan? syncTimeout = null) =>
        new(new JsonStore(Path.Combine(dir, "store")), (level, line) => said.Add((level, line)))
        {
            InstallHooks = hooks ?? ((_, _) => []),
            Runner = runner ?? new FakeRunner(_ => new ProcessResult(0, "", "", false)),
            SyncTimeout = syncTimeout ?? TimeSpan.FromMinutes(2),
            AlertSink = (title, message, urgent) => alerts.Add((title, message, urgent)),
        };

    private sealed class FakeRunner(Func<(string File, IReadOnlyList<string> Args, TimeSpan Timeout), ProcessResult> run) : IProcessRunner
    {
        public List<(string File, IReadOnlyList<string> Args, TimeSpan Timeout)> Calls { get; } = [];

        public ProcessResult Run(string file, IReadOnlyList<string> args, TimeSpan timeout)
        {
            Calls.Add((file, args, timeout));
            return run((file, args, timeout));
        }
    }

    // ---- AgentGate.Ensure ----

    [Test]
    public void A_hook_install_that_throws_IOException_is_retried_next_tick_not_marked_done()
    {
        var calls = 0;
        var daemon = Make(hooks: (_, _) => ++calls == 1 ? throw new IOException("settings.json is locked") : ["installed the hooks"]);
        var s = new OrchestratorSettings();

        daemon.EnsureHooks(s);
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(said, Has.Some.Matches<(LogEventLevel Level, string Line)>(x =>
            x.Level == LogEventLevel.Warning && x.Line.Contains("retrying next tick") && x.Line.Contains("settings.json is locked")));

        daemon.EnsureHooks(s);
        Assert.That(calls, Is.EqualTo(2), "the failed install wasn't remembered as done");
        Assert.That(said, Has.Some.Matches<(LogEventLevel Level, string Line)>(x => x.Level == LogEventLevel.Information && x.Line == "installed the hooks"));

        daemon.EnsureHooks(s);
        Assert.That(calls, Is.EqualTo(2), "once it succeeded, the same settings don't rewrite the hooks");
    }

    [Test]
    public void A_hook_install_denied_access_is_retried_too()
    {
        var calls = 0;
        var daemon = Make(hooks: (_, _) => ++calls == 1 ? throw new UnauthorizedAccessException("denied") : []);
        daemon.EnsureHooks(new OrchestratorSettings());
        daemon.EnsureHooks(new OrchestratorSettings());
        Assert.That(calls, Is.EqualTo(2));
    }

    [Test]
    public void A_failure_partway_through_the_providers_still_retries_the_whole_install()
    {
        var calls = 0;
        // Claude's hooks written, then the other providers' throw: the lazily-enumerated install must not be marked done.
        IEnumerable<string?> Install()
        {
            calls++;
            yield return "claude hooks";
            if (calls == 1) throw new IOException("codex config locked");
            yield return "other providers";
        }
        var daemon = Make(hooks: (_, _) => Install());
        daemon.EnsureHooks(new OrchestratorSettings());
        daemon.EnsureHooks(new OrchestratorSettings());
        Assert.That(calls, Is.EqualTo(2));
        Assert.That(said.Select(x => x.Line), Does.Contain("other providers"));
    }

    [Test]
    public void Any_other_failure_propagates_to_the_tick_and_is_still_retried()
    {
        var calls = 0;
        var daemon = Make(hooks: (_, _) => ++calls == 1 ? throw new InvalidOperationException("bug") : []);
        Assert.Throws<InvalidOperationException>(() => daemon.EnsureHooks(new OrchestratorSettings()));
        daemon.EnsureHooks(new OrchestratorSettings());
        Assert.That(calls, Is.EqualTo(2));
    }

    [Test]
    public void A_changed_cycle_point_or_keep_going_rewrites_the_hooks_with_the_new_values()
    {
        var seen = new List<(int, bool)>();
        var daemon = Make(hooks: (percent, keepGoing) => { seen.Add((percent, keepGoing)); return []; });
        daemon.EnsureHooks(new OrchestratorSettings { ContextCyclePercent = 80, KeepGoing = true });
        daemon.EnsureHooks(new OrchestratorSettings { ContextCyclePercent = 80, KeepGoing = true });
        daemon.EnsureHooks(new OrchestratorSettings { ContextCyclePercent = 70, KeepGoing = true });
        daemon.EnsureHooks(new OrchestratorSettings { ContextCyclePercent = 70, KeepGoing = false });
        Assert.That(seen, Is.EqualTo(new[] { (80, true), (70, true), (70, false) }));
    }

    // ---- Needs you ----

    [Test]
    public void Only_allocates_needs_you_lines_raise_the_Needs_you_alert()
    {
        var daemon = Make();
        daemon.ReportAllocate([
            "allocate: started X-T1 in X in the background: the dashboard needs you to confirm (log x.log)",
            "allocate: X-T2 landed and closed",
            "needs you: not allocate's prefix",
            "allocate: needs you: X-T3's agent in X ended without finishing it",
        ]);

        Assert.That(alerts, Has.Count.EqualTo(1));
        Assert.That(alerts[0].Title, Is.EqualTo("Needs you"));
        Assert.That(alerts[0].Message, Is.EqualTo("X-T3's agent in X ended without finishing it"));
        Assert.That(said.Count(x => x.Level == LogEventLevel.Warning), Is.EqualTo(1));
        Assert.That(said.Single(x => x.Level == LogEventLevel.Warning).Line, Does.StartWith("allocate: needs you: X-T3"));
        Assert.That(said, Has.Count.EqualTo(4), "every line is still logged");
    }

    // ---- tickets db sync ----

    private string Db()
    {
        var path = Path.Combine(dir, "Tickets.db");
        File.WriteAllText(path, "db");
        return path;
    }

    [Test]
    public void A_db_sync_past_its_timeout_is_logged_and_retried_on_the_next_try()
    {
        var timedOut = true;
        var runner = new FakeRunner(_ => timedOut ? ProcessResult.Killed : new ProcessResult(0, "pushed", "", false));
        var daemon = Make(runner: runner, syncTimeout: TimeSpan.FromSeconds(7));
        var db = Db();
        var now = DateTime.UtcNow;

        daemon.SyncTicketsDbIfDue(db, now, @"C:\src\Elsewhere");
        Assert.That(runner.Calls, Has.Count.EqualTo(1));
        Assert.That(runner.Calls[0].File, Is.EqualTo("tickets"));
        Assert.That(runner.Calls[0].Args, Is.EqualTo(new[] { "db", "sync", "--root", @"C:\src\Elsewhere" }), "the orchestrator's workspace, not the CLI's default (M.ORCH-T402)");
        Assert.That(runner.Calls[0].Timeout, Is.EqualTo(TimeSpan.FromSeconds(7)));
        Assert.That(said, Has.Some.Matches<(LogEventLevel Level, string Line)>(x =>
            x.Level == LogEventLevel.Warning && x.Line.Contains("tickets db sync took over 7s; stopped it")));

        daemon.SyncTicketsDbIfDue(db, now.AddMinutes(5));
        Assert.That(runner.Calls, Has.Count.EqualTo(1), "tries stay 10 minutes apart");

        timedOut = false;
        daemon.SyncTicketsDbIfDue(db, now.AddMinutes(11));
        Assert.That(runner.Calls, Has.Count.EqualTo(2), "the killed sync didn't count the database as synced");
        Assert.That(said.Last(), Is.EqualTo((LogEventLevel.Information, "tickets db sync: pushed")));

        daemon.SyncTicketsDbIfDue(db, now.AddMinutes(30));
        Assert.That(runner.Calls, Has.Count.EqualTo(2), "an unchanged database isn't synced again");
    }

    [Test]
    public void The_orchestrator_db_is_committed_with_its_dump_when_the_dump_changed()
    {
        var repo = Path.Combine(dir, "MindAttic.Orchestrator");
        Directory.CreateDirectory(Path.Combine(repo, "data"));
        var dbPath = Path.Combine(repo, "data", "orchestrator.db");
        new OrchestratorDb(dbPath).Dump(); // a real database: the sync locks it
        File.WriteAllText(Path.Combine(repo, "data", "orchestrator.sql"), "-- dump\nINSERT 1;\n");
        var committedDump = "-- dump\r\n";
        var runner = new FakeRunner(c => c.Args.Contains("show") ? new ProcessResult(0, committedDump, "", false) : new ProcessResult(0, "", "", false));
        var daemon = Make(runner: runner);
        var now = DateTime.UtcNow;

        daemon.SyncOrchestratorDbIfDue(repo, Path.Combine(dir, "elsewhere.db"), now);
        Assert.That(runner.Calls, Is.Empty, "a database the environment moves elsewhere isn't committed");

        daemon.SyncOrchestratorDbIfDue(repo, dbPath, now);
        string[] files = ["data/orchestrator.db", "data/orchestrator.sql"];
        Assert.That(runner.Calls.Select(c => string.Join(' ', c.Args)), Is.EqualTo(new[]
        {
            $"-C {repo} show HEAD:data/orchestrator.sql",
            $"-C {repo} add -- {string.Join(' ', files)}",
            $"-C {repo} commit -q -m {Daemon.OrchestratorDbSyncSubject} -- {string.Join(' ', files)}",
            $"-C {repo} fetch -q origin",
            $"-C {repo} merge -q --no-edit origin/main",
            $"-C {repo} push -q",
        }), "only the database and its dump go in the commit");
        Assert.That(runner.Calls.All(c => c.File == "git"));

        daemon.SyncOrchestratorDbIfDue(repo, dbPath, now.AddMinutes(30));
        Assert.That(runner.Calls, Has.Count.EqualTo(6), "an unchanged dump isn't committed again");

        // The same text as the commit's (git handing it back with CRLF): nothing to commit.
        File.WriteAllText(Path.Combine(repo, "data", "orchestrator.sql"), "-- dump\n");
        daemon.SyncOrchestratorDbIfDue(repo, dbPath, now.AddMinutes(45));
        Assert.That(runner.Calls, Has.Count.EqualTo(7));
        Assert.That(runner.Calls[^1].Args, Does.Contain("show"));
    }

    [Test]
    public void An_orchestrator_db_commit_that_fails_unstages_it()
    {
        var repo = Path.Combine(dir, "MindAttic.Orchestrator");
        Directory.CreateDirectory(Path.Combine(repo, "data"));
        var dbPath = Path.Combine(repo, "data", "orchestrator.db");
        new OrchestratorDb(dbPath).Dump(); // a real database: the sync locks it
        File.WriteAllText(Path.Combine(repo, "data", "orchestrator.sql"), "-- new\n");
        var runner = new FakeRunner(c => c.Args.Contains("commit") ? new ProcessResult(1, "", "hook refused", false) : new ProcessResult(0, "-- old\n", "", false));
        Make(runner: runner).SyncOrchestratorDbIfDue(repo, dbPath, DateTime.UtcNow);
        Assert.That(runner.Calls[^1].Args, Is.EqualTo(new[] { "-C", repo, "reset", "-q", "--", "data/orchestrator.db", "data/orchestrator.sql" }));
        Assert.That(said, Has.Some.Matches<(LogEventLevel Level, string Line)>(x => x.Line.Contains("git commit failed: hook refused")));
    }

    [Test]
    public void A_failed_push_of_the_orchestrator_db_is_tried_again()
    {
        // M.ORCH-T538: the dump counted as synced even when the push failed, so a quiet db stayed unpushed.
        var repo = Path.Combine(dir, "MindAttic.Orchestrator");
        Directory.CreateDirectory(Path.Combine(repo, "data"));
        var dbPath = Path.Combine(repo, "data", "orchestrator.db");
        new OrchestratorDb(dbPath).Dump();
        File.WriteAllText(Path.Combine(repo, "data", "orchestrator.sql"), "-- new\n");
        var committed = "-- old\n";
        var pushes = 0;
        var runner = new FakeRunner(c =>
        {
            if (c.Args.Contains("commit")) committed = "-- new\n";
            if (c.Args.Contains("push")) return ++pushes == 1 ? new ProcessResult(1, "", "network down", false) : new ProcessResult(0, "", "", false);
            return c.Args.Contains("show") ? new ProcessResult(0, committed, "", false) : new ProcessResult(0, "", "", false);
        });
        var daemon = Make(runner: runner);
        var now = DateTime.UtcNow;

        daemon.SyncOrchestratorDbIfDue(repo, dbPath, now);
        Assert.That(pushes, Is.EqualTo(1));
        Assert.That(said.Last().Line, Does.Contain("push failed"));

        daemon.SyncOrchestratorDbIfDue(repo, dbPath, now.AddMinutes(11));
        Assert.That(pushes, Is.EqualTo(2), "the committed but unpushed db is pushed on the next try");
        Assert.That(runner.Calls.Count(c => c.Args.Contains("commit")), Is.EqualTo(1), "not committed again");
        Assert.That(said.Last(), Is.EqualTo((LogEventLevel.Information, "orchestrator db sync: pushed data/orchestrator.db")));

        daemon.SyncOrchestratorDbIfDue(repo, dbPath, now.AddMinutes(22));
        Assert.That(pushes, Is.EqualTo(2), "once pushed, an unchanged dump isn't pushed again");
    }

    [Test]
    public void An_orchestrator_db_sync_that_cant_merge_main_aborts_and_warns_once()
    {
        // M.ORCH-T620: main moves under the checkout, so the push must follow a fetch and merge (M.ORCH-T624: not a rebase --autostash, which fails on the live dirty db).
        var repo = Path.Combine(dir, "MindAttic.Orchestrator");
        Directory.CreateDirectory(Path.Combine(repo, "data"));
        var dbPath = Path.Combine(repo, "data", "orchestrator.db");
        new OrchestratorDb(dbPath).Dump();
        File.WriteAllText(Path.Combine(repo, "data", "orchestrator.sql"), "-- new\n");
        var committed = "-- old\n";
        var runner = new FakeRunner(c =>
        {
            if (c.Args.Contains("commit")) committed = "-- new\n";
            if (c.Args.Contains("merge") && !c.Args.Contains("--abort")) return new ProcessResult(1, "", "conflict", false);
            return c.Args.Contains("show") ? new ProcessResult(0, committed, "", false) : new ProcessResult(0, "", "", false);
        });
        var daemon = Make(runner: runner);
        var now = DateTime.UtcNow;

        daemon.SyncOrchestratorDbIfDue(repo, dbPath, now);
        daemon.SyncOrchestratorDbIfDue(repo, dbPath, now.AddMinutes(11));
        Assert.That(runner.Calls.Any(c => c.Args.Contains("push")), Is.False, "a push that would be rejected isn't tried");
        Assert.That(runner.Calls.Count(c => c.Args.Contains("--abort")), Is.EqualTo(2));
        Assert.That(said.Count(x => x.Line.Contains("couldn't merge origin/main")), Is.EqualTo(1), "warned once");
    }

    [Test]
    public void An_orchestrator_db_sync_leaves_a_merge_in_progress_alone()
    {
        // M.ORCH-T721: the retry's merge --abort threw away a merge the user was resolving in the checkout.
        var repo = Path.Combine(dir, "MindAttic.Orchestrator");
        Directory.CreateDirectory(Path.Combine(repo, "data"));
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        File.WriteAllText(Path.Combine(repo, ".git", "MERGE_HEAD"), "abc\n");
        var dbPath = Path.Combine(repo, "data", "orchestrator.db");
        new OrchestratorDb(dbPath).Dump();
        File.WriteAllText(Path.Combine(repo, "data", "orchestrator.sql"), "-- new\n");
        var runner = new FakeRunner(c => c.Args.Contains("show") ? new ProcessResult(0, "-- new\n", "", false) : new ProcessResult(0, "", "", false));
        var daemon = Make(runner: runner);
        var now = DateTime.UtcNow;
        // Committed already but unpushed: the retry path.
        daemon.SyncOrchestratorDbIfDue(repo, dbPath, now);
        Assert.That(runner.Calls.Any(c => c.Args.Contains("merge") || c.Args.Contains("fetch") || c.Args.Contains("push")), Is.False);
        Directory.Delete(Path.Combine(repo, ".git"), true);
        Assert.That(Daemon.GitOperationInProgress(repo), Is.False);
        Directory.CreateDirectory(Path.Combine(repo, ".git", "rebase-merge"));
        Assert.That(Daemon.GitOperationInProgress(repo), Is.True);
    }

    [Test]
    public void The_orchestrator_db_is_staged_holding_its_write_lock()
    {
        // M.ORCH-T536: git add read the file while the daemon and hooks could be mid-write, so a commit could take half a write.
        var repo = Path.Combine(dir, "MindAttic.Orchestrator");
        Directory.CreateDirectory(Path.Combine(repo, "data"));
        var dbPath = Path.Combine(repo, "data", "orchestrator.db");
        new OrchestratorDb(dbPath).Dump();
        File.WriteAllText(Path.Combine(repo, "data", "orchestrator.sql"), "-- new\n");
        bool? writeBlocked = null;
        var runner = new FakeRunner(c =>
        {
            if (c.Args.Contains("add"))
            {
                using var other = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath};Pooling=False;Default Timeout=1");
                other.Open();
                OrchestratorDb.Execute(other, "PRAGMA busy_timeout = 0;");
                try { OrchestratorDb.Execute(other, "BEGIN IMMEDIATE; ROLLBACK;"); writeBlocked = false; }
                catch (Microsoft.Data.Sqlite.SqliteException) { writeBlocked = true; }
            }
            return c.Args.Contains("show") ? new ProcessResult(0, "-- old\n", "", false) : new ProcessResult(0, "", "", false);
        });
        Make(runner: runner).SyncOrchestratorDbIfDue(repo, dbPath, DateTime.UtcNow);
        Assert.That(writeBlocked, Is.True, "no other write can run while git stages the database");
        Assert.That(runner.Calls.Select(c => c.Args[2]), Is.EqualTo(new[] { "show", "add", "commit", "fetch", "merge", "push" }));
        Assert.DoesNotThrow(() => new OrchestratorDb(dbPath).SaveState("after", "{}"), "the lock is let go once it's staged");
    }

    [Test]
    public void A_missing_tickets_cli_is_logged_not_thrown()
    {
        var runner = new FakeRunner(_ => throw new System.ComponentModel.Win32Exception("not found"));
        var daemon = Make(runner: runner);
        Assert.DoesNotThrow(() => daemon.SyncTicketsDbIfDue(Db(), DateTime.UtcNow));
        Assert.That(runner.Calls.Select(c => c.File), Is.EqualTo(new[] { "tickets", "workflow" }), "the legacy shim is tried before giving up");
        Assert.That(said, Has.Some.Matches<(LogEventLevel Level, string Line)>(x => x.Line.StartsWith("couldn't run 'tickets db sync'")));
    }

    [Test]
    public void The_legacy_workflow_cli_syncs_where_tickets_isnt_installed_yet()
    {
        // M.ORCH-T564: MindAttic.Workflow became MindAttic.Tickets; a machine that still has only `workflow` keeps syncing.
        var runner = new FakeRunner(c => c.File == "tickets" ? throw new System.ComponentModel.Win32Exception("not found") : new ProcessResult(0, "pushed", "", false));
        Make(runner: runner).SyncTicketsDbIfDue(Db(), DateTime.UtcNow);
        Assert.That(runner.Calls.Select(c => c.File), Is.EqualTo(new[] { "tickets", "workflow" }));
        Assert.That(said.Last(), Is.EqualTo((LogEventLevel.Information, "tickets db sync: pushed")));
    }

    /// <summary>Stands in for <c>tickets</c>: runs a real child that would write <paramref name="marker"/> after a couple of seconds.</summary>
    private static IProcessRunner Hung(string marker) => new FakeRunner(c =>
        ProcessRunner.Default.Run("cmd.exe", ["/c", $"ping -n 3 127.0.0.1 >nul & echo late> {marker}"], c.Timeout));

    [Test]
    [Platform("Win")]
    public void A_hung_db_sync_is_killed_with_its_children_without_blocking_the_loop()
    {
        var marker = Path.Combine(dir, "late.txt");
        var daemon = Make(runner: Hung(marker), syncTimeout: TimeSpan.FromMilliseconds(300));

        var sw = Stopwatch.StartNew();
        daemon.SyncTicketsDbIfDue(Db(), DateTime.UtcNow);
        Assert.That(sw.Elapsed, Is.LessThan(TimeSpan.FromSeconds(1.5)), "the loop got control back at the timeout");
        Assert.That(said, Has.Some.Matches<(LogEventLevel Level, string Line)>(x => x.Line.Contains("stopped it")));

        Thread.Sleep(TimeSpan.FromSeconds(3.5));
        Assert.That(File.Exists(marker), Is.False, "the whole tree was killed, ping and all");
    }

    // ---- gh ----

    [Test]
    public void A_gh_call_past_its_timeout_reads_as_a_failure()
    {
        var runner = new FakeRunner(_ => ProcessResult.Killed);
        var (code, output) = GitHubIntake.Gh(runner, TimeSpan.FromSeconds(60), "issue", "list");
        Assert.That(code, Is.EqualTo(-1));
        Assert.That(output, Is.EqualTo("gh took over 60s; stopped it"));
        Assert.That(runner.Calls.Single().File, Is.EqualTo("gh"));
        Assert.That(runner.Calls.Single().Args, Is.EqualTo(new[] { "issue", "list" }));
        Assert.That(MindAttic.Tickets.Repos.GitHubIntake.Find("o/r", a => GitHubIntake.Gh(runner, TimeSpan.FromSeconds(60), a), DateTime.UtcNow), Is.Empty,
            "a killed call files nothing");
    }

    [Test]
    public void Gh_returns_stdout_on_success_and_stderr_on_failure_and_says_when_it_is_missing()
    {
        Assert.That(GitHubIntake.Gh(new FakeRunner(_ => new ProcessResult(0, "[]", "warn", false)), GitHubIntake.GhTimeout, "x"), Is.EqualTo((0, "[]")));
        Assert.That(GitHubIntake.Gh(new FakeRunner(_ => new ProcessResult(1, "", "auth", false)), GitHubIntake.GhTimeout, "x"), Is.EqualTo((1, "auth")));
        Assert.That(GitHubIntake.Gh(new FakeRunner(_ => throw new System.ComponentModel.Win32Exception()), GitHubIntake.GhTimeout, "x").Code, Is.EqualTo(-1));
    }

    [Test]
    [Platform("Win")]
    public void A_hung_gh_is_killed_at_its_timeout()
    {
        var marker = Path.Combine(dir, "late.txt");
        var sw = Stopwatch.StartNew();
        var (code, _) = GitHubIntake.Gh(Hung(marker), TimeSpan.FromMilliseconds(300), "run", "list");
        Assert.That(code, Is.EqualTo(-1));
        Assert.That(sw.Elapsed, Is.LessThan(TimeSpan.FromSeconds(1.5)));
        Thread.Sleep(TimeSpan.FromSeconds(3.5));
        Assert.That(File.Exists(marker), Is.False);
    }

    [Test]
    [Platform("Win")]
    public void The_real_runner_returns_output_and_exit_code_of_a_process_that_finishes()
    {
        var r = ProcessRunner.Default.Run("cmd.exe", ["/c", "echo hi& echo oops 1>&2& exit 3"], TimeSpan.FromSeconds(30));
        Assert.That(r.TimedOut, Is.False);
        Assert.That(r.ExitCode, Is.EqualTo(3));
        Assert.That(r.Output.Trim(), Is.EqualTo("hi"));
        Assert.That(r.Error.Trim(), Is.EqualTo("oops"));
    }

    [Test]
    [Platform("Win")]
    public void The_real_runner_does_not_wait_past_its_timeout_on_a_grandchild_holding_the_pipes()
    {
        // cmd exits at once, but the ping it started in the background inherited its stdout and holds it for ~20s.
        // The timeout leaves cmd room to start and exit on a loaded machine (the full suite): at 2s it sometimes
        // hadn't, so the runner took the kill path (-1) and this test never reached the grandchild case (M.ORCH-T744).
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var r = ProcessRunner.Default.Run("cmd.exe", ["/c", "start /b ping -n 21 127.0.0.1"], TimeSpan.FromSeconds(6));
        Assert.That(sw.Elapsed, Is.LessThan(TimeSpan.FromSeconds(12)), "it returned at its timeout, not when the grandchild let go of the pipes");
        Assert.That((r.ExitCode, r.TimedOut), Is.EqualTo((0, true)), "its output was cut short, so it isn't taken as a success with none");
    }

    // ---- rl-status.json kept fresh (M.ORCH-T205) ----

    [Test]
    public void A_tick_that_runs_long_is_logged_with_where_its_time_went()
    {
        var now = new DateTime(2026, 10, 17, 4, 47, 0, DateTimeKind.Utc);
        var tick = new Daemon.TickTimer(() => now);
        tick.Start();
        now = now.AddSeconds(2); tick.Lap("refresh");
        now = now.AddMilliseconds(200); tick.Lap("audit");
        Assert.That(tick.Slow(), Is.Null, "a normal tick says nothing");

        tick.Start();
        now = now.AddSeconds(5); tick.Lap("refresh");
        now = now.AddSeconds(130); tick.Lap("allocate");
        now = now.AddMilliseconds(300); tick.Lap("audit");
        Assert.That(tick.Slow(), Does.StartWith("a tick took 135s (allocate 130s, refresh 5s)").And.Contain("rl-status.json"));
    }

    [Test]
    public void A_tick_that_runs_long_has_the_refresh_run_beside_it_at_the_loops_cadence()
    {
        // M.ORCH-T1111: an 80s tick (72s of main-line tests) left the 5-hour meter unrefreshed for its length.
        var now = new DateTime(2026, 10, 27, 4, 52, 0, DateTimeKind.Utc);
        var refresher = new Daemon.OffTickRefresh(TimeSpan.FromSeconds(15), () => now);
        var beside = 0;
        refresher.Run(() => true); // the tick's own refresh
        now = now.AddSeconds(30);
        Assert.That(refresher.TryRefresh(() => beside++), Is.False, "between ticks the loop's own refresh is next");

        refresher.Run(() => true);
        refresher.Ticking = true;
        now = now.AddSeconds(10);
        Assert.That(refresher.TryRefresh(() => beside++), Is.False, "not before the cadence is up");
        now = now.AddSeconds(5);
        Assert.That(refresher.TryRefresh(() => beside++), Is.True);
        Assert.That(refresher.TryRefresh(() => beside++), Is.False, "timed from the one just run");
        now = now.AddSeconds(15);
        Assert.That(refresher.TryRefresh(() => beside++), Is.True);
        Assert.That(beside, Is.EqualTo(2));

        // A refresh that throws is timed too (bughunt 2026-10-27): not tried again every second for the rest of the tick.
        now = now.AddSeconds(15);
        Assert.That(() => refresher.TryRefresh(() => throw new IOException("held")), Throws.TypeOf<IOException>());
        now = now.AddSeconds(1);
        Assert.That(refresher.TryRefresh(() => beside++), Is.False, "the failed one's interval runs first");
    }

    [Test]
    public void A_status_file_that_cant_be_written_is_said_once_and_its_recovery_once()
    {
        var path = Path.Combine(dir, "rl-status.json");
        var budget = Budget.BudgetCalculator.Compute([new UsageEvent(DateTime.UtcNow, new TokenUsage(0, 0, 1_000_000, 400))],
            new OrchestratorSettings { BlockBudget = 1000 }, DateTime.UtcNow, DateTime.UtcNow.Date);
        var clock = DateTime.UtcNow.AddMinutes(-2);
        var file = new Budget.RateLimitStatusWriter(path) { UtcNow = () => clock, ReplaceRetry = TimeSpan.FromMilliseconds(100) };
        file.Write(budget);
        var daemon = Make();
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            clock = clock.AddSeconds(20);
            file.Write(budget with { WeeklyPercent = 5 });
            Assert.That(file.FailingSinceUtc, Is.Not.Null);
            daemon.ReportStatusFile(file);
            daemon.ReportStatusFile(file);
        }
        Assert.That(said.Count(x => x.Level == LogEventLevel.Warning && x.Line.StartsWith("couldn't write rl-status.json")), Is.EqualTo(1), "said once, not every tick");
        clock = clock.AddSeconds(20);
        file.Write(budget with { WeeklyPercent = 6 });
        daemon.ReportStatusFile(file);
        daemon.ReportStatusFile(file);
        Assert.That(said.Count(x => x.Line == "rl-status.json is being written again"), Is.EqualTo(1));
    }
}
