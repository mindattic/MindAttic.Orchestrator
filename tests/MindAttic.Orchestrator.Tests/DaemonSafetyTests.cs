using System.Diagnostics;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Tickets;
using NUnit.Framework;
using Serilog.Events;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The daemon loop's safety: hook installs that fail are retried, only real "needs you" lines alert, and a hung
/// <c>workflow db sync</c> or <c>gh</c> is killed and logged instead of stalling the loop.</summary>
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
    public void Only_autopilots_needs_you_lines_raise_the_Needs_you_alert()
    {
        var daemon = Make();
        daemon.ReportAutopilot([
            "autopilot: started X-T1 in X in the background: the dashboard needs you to confirm (log x.log)",
            "autopilot: X-T2 landed and closed",
            "needs you: not autopilot's prefix",
            "autopilot: needs you: X-T3's agent in X ended without finishing it",
        ]);

        Assert.That(alerts, Has.Count.EqualTo(1));
        Assert.That(alerts[0].Title, Is.EqualTo("Needs you"));
        Assert.That(alerts[0].Message, Is.EqualTo("X-T3's agent in X ended without finishing it"));
        Assert.That(said.Count(x => x.Level == LogEventLevel.Warning), Is.EqualTo(1));
        Assert.That(said.Single(x => x.Level == LogEventLevel.Warning).Line, Does.StartWith("autopilot: needs you: X-T3"));
        Assert.That(said, Has.Count.EqualTo(4), "every line is still logged");
    }

    // ---- workflow db sync ----

    private string Db()
    {
        var path = Path.Combine(dir, "workflow.db");
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

        daemon.SyncWorkflowDbIfDue(db, now);
        Assert.That(runner.Calls, Has.Count.EqualTo(1));
        Assert.That(runner.Calls[0].File, Is.EqualTo("workflow"));
        Assert.That(runner.Calls[0].Args, Is.EqualTo(new[] { "db", "sync" }));
        Assert.That(runner.Calls[0].Timeout, Is.EqualTo(TimeSpan.FromSeconds(7)));
        Assert.That(said, Has.Some.Matches<(LogEventLevel Level, string Line)>(x =>
            x.Level == LogEventLevel.Warning && x.Line.Contains("workflow db sync took over 7s; stopped it")));

        daemon.SyncWorkflowDbIfDue(db, now.AddMinutes(5));
        Assert.That(runner.Calls, Has.Count.EqualTo(1), "tries stay 10 minutes apart");

        timedOut = false;
        daemon.SyncWorkflowDbIfDue(db, now.AddMinutes(11));
        Assert.That(runner.Calls, Has.Count.EqualTo(2), "the killed sync didn't count the database as synced");
        Assert.That(said.Last(), Is.EqualTo((LogEventLevel.Information, "workflow db sync: pushed")));

        daemon.SyncWorkflowDbIfDue(db, now.AddMinutes(30));
        Assert.That(runner.Calls, Has.Count.EqualTo(2), "an unchanged database isn't synced again");
    }

    [Test]
    public void A_missing_workflow_cli_is_logged_not_thrown()
    {
        var daemon = Make(runner: new FakeRunner(_ => throw new System.ComponentModel.Win32Exception("not found")));
        Assert.DoesNotThrow(() => daemon.SyncWorkflowDbIfDue(Db(), DateTime.UtcNow));
        Assert.That(said, Has.Some.Matches<(LogEventLevel Level, string Line)>(x => x.Line.StartsWith("couldn't run 'workflow db sync'")));
    }

    /// <summary>Stands in for <c>workflow</c>: runs a real child that would write <paramref name="marker"/> after a couple of seconds.</summary>
    private static IProcessRunner Hung(string marker) => new FakeRunner(c =>
        ProcessRunner.Default.Run("cmd.exe", ["/c", $"ping -n 3 127.0.0.1 >nul & echo late> {marker}"], c.Timeout));

    [Test]
    [Platform("Win")]
    public void A_hung_db_sync_is_killed_with_its_children_without_blocking_the_loop()
    {
        var marker = Path.Combine(dir, "late.txt");
        var daemon = Make(runner: Hung(marker), syncTimeout: TimeSpan.FromMilliseconds(300));

        var sw = Stopwatch.StartNew();
        daemon.SyncWorkflowDbIfDue(Db(), DateTime.UtcNow);
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
        Assert.That(GitHubIntake.Find("o/r", a => GitHubIntake.Gh(runner, TimeSpan.FromSeconds(60), a), DateTime.UtcNow), Is.Empty,
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
        // cmd exits at once, but the ping it started in the background inherited its stdout and holds it for ~6s.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var r = ProcessRunner.Default.Run("cmd.exe", ["/c", "start /b ping -n 7 127.0.0.1"], TimeSpan.FromSeconds(2));
        Assert.That(sw.Elapsed, Is.LessThan(TimeSpan.FromSeconds(4.5)));
        Assert.That(r.ExitCode, Is.EqualTo(0));
    }
}
