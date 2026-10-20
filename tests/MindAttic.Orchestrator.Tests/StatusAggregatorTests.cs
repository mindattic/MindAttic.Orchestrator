using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class StatusAggregatorTests
{
    private string dir = "";
    private string ws = "";
    private string projects = "";
    private string worktrees = "";
    private JsonStore store = null!;
    private readonly List<HostSession> hosts = [];
    private DateTime now;

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "orch-agg-" + Guid.NewGuid().ToString("N"));
        ws = Path.Combine(dir, "ws");
        projects = Path.Combine(dir, "projects");
        worktrees = Path.Combine(dir, "MindAttic.worktrees");
        store = new JsonStore(Path.Combine(dir, "store"));
        store.SaveSettings(new OrchestratorSettings { WorktreeRoot = worktrees });
        hosts.Clear();
        now = DateTime.UtcNow;
        Repo("A", "| A-T1 | 2026-10-01 | fix | open | one | — |\n| A-T2 | 2026-10-01 | fix | closed | two | x |\n| A-T3 | 2026-10-02 | feature | open | three | — |\n");
        Repo("B", "");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(dir, recursive: true);

    private void Repo(string name, string rows)
    {
        var path = Path.Combine(ws, name);
        Directory.CreateDirectory(Path.Combine(path, ".git"));
        Directory.CreateDirectory(Path.Combine(path, "docs"));
        File.WriteAllText(Path.Combine(path, "docs", "USER_STORIES.md"),
            $"---\ncode: {name}\n---\n\n# {name}\n\n## Tickets\n\n| ID | Date | Type | Status | Summary | Ref |\n|---|---|---|---|---|---|\n" + rows);
    }

    /// <summary>A Claude transcript for session <paramref name="id"/> in <paramref name="cwd"/>: one turn per output count,
    /// a minute apart ending <paramref name="ago"/> before now, last written then.</summary>
    private DateTime Transcript(string id, string cwd, TimeSpan ago, params int[] outputs)
    {
        var folder = Path.Combine(projects, "p-" + id);
        Directory.CreateDirectory(folder);
        var end = now - ago;
        var file = Path.Combine(folder, id + ".jsonl");
        File.WriteAllLines(file, outputs.Select((output, i) => System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "assistant", timestamp = end.AddMinutes(i - outputs.Length + 1).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"), sessionId = id, cwd,
            message = new
            {
                id = $"{id}-m{i}", model = "claude-sonnet-5",
                usage = new { input_tokens = 1, cache_creation_input_tokens = 0, cache_read_input_tokens = 100, output_tokens = output },
            },
        })));
        var written = new DateTime(end.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(file, written);
        return written;
    }

    /// <summary>These tests write turns up to an hour before "now" and count them as today's, so "now" is a fixed local noon
    /// (yesterday's, so it's never ahead of the wall clock) rather than the wall clock: in the hour after local midnight the
    /// turns would be yesterday's (M.ORCH-T310 skipped them then, hiding whatever broke in that hour).</summary>
    private void TurnsAreToday() =>
        now = DateTime.Today.AddDays(-1).AddHours(12).ToUniversalTime();

    private StatusAggregator Aggregator() => new(new OrchestratorOptions { WorkspaceRoot = ws, ClaudeProjectsDir = projects, TranscriptCache = null }, store)
    {
        ScanHosts = () => hosts,
        OtherProviders = new ProviderLogReader(Path.Combine(dir, "codex"), Path.Combine(dir, "gemini")),
    };

    [Test]
    public void Sessions_in_a_repo_and_in_its_worktrees_are_credited_to_it_with_its_open_tickets_and_todays_usage()
    {
        TurnsAreToday();
        var last = Transcript("s1", Path.Combine(ws, "A", "src"), TimeSpan.FromSeconds(30), 10, 20);
        Transcript("s2", Path.Combine(worktrees, "A", "A-T1"), TimeSpan.FromMinutes(10), 5);
        hosts.Add(new HostSession("Claude", 42, "mindattic-host-claude-42", Path.Combine(ws, "B")));

        var snap = Aggregator().Refresh(now);

        Assert.That(snap.Repos.Select(r => r.Name), Is.EqualTo(new[] { "A", "B" }), "no pseudo-repos: nothing ran at the root or outside");
        var a = snap.Repos.Single(r => r.Name == "A");
        Assert.That(a.Sessions.Select(s => s.SessionId), Is.EqualTo(new[] { "s1", "s2" }), "its worktree's session too, newest first");
        Assert.That(a.UsageToday.Output, Is.EqualTo(35));
        Assert.That(a.LastActivityUtc, Is.EqualTo(last));
        Assert.That(a.State, Is.EqualTo(RepoState.Working), "written half a minute ago");
        Assert.That(a.OpenTickets.Select(t => t.Id), Is.EqualTo(new[] { "A-T1", "A-T3" }));

        var b = snap.Repos.Single(r => r.Name == "B");
        Assert.That((b.State, b.Hosts.Single().ProcessId, b.LastActivityUtc, b.OpenTickets.Count), Is.EqualTo((RepoState.Waiting, 42, (DateTime?)null, 0)),
            "a tab open with nothing written: waiting on you");

        Assert.That(History.LoadUsageHistory().Where(d => d.Repo == "A").Sum(d => d.Fresh), Is.GreaterThan(0), "today's usage is saved for the history");
        Assert.That(History.LoadTicketCosts().Single().TicketId, Is.EqualTo("A-T1"), "the worktree's session is charged to its ticket");
    }

    [Test]
    public void Today_is_the_day_of_the_refresh_asked_for_not_the_wall_clocks()
    {
        TurnsAreToday();
        Transcript("s1", Path.Combine(ws, "A"), TimeSpan.FromMinutes(1), 10);

        Assert.That(Aggregator().Refresh(now).Repos.Single(r => r.Name == "A").UsageToday.Output, Is.EqualTo(10));
        var twoDaysOn = Aggregator().Refresh(now.AddDays(2)).Repos.Single(r => r.Name == "A");
        Assert.That(twoDaysOn.UsageToday.Output, Is.Zero, "two days later that turn isn't today's usage any more");
        Assert.That(twoDaysOn.Sessions, Has.Count.EqualTo(1), "still inside the week of history");
    }

    [Test]
    public void The_root_and_outside_pseudo_repos_appear_only_once_they_have_a_session_or_a_tab()
    {
        var aggregator = Aggregator();
        Assert.That(aggregator.Refresh().Repos.Select(r => r.Name), Is.EqualTo(new[] { "A", "B" }));

        Transcript("root", ws, TimeSpan.FromHours(2), 7);
        hosts.Add(new HostSession("Codex", 7, "mindattic-host-codex-7", Path.Combine(dir, "elsewhere")));
        var snap = aggregator.Refresh();

        Assert.That(snap.Repos.Select(r => r.Name), Is.EqualTo(new[] { "A", "B", StatusAggregator.WorkspaceRootName, StatusAggregator.OutsideName }));
        var root = snap.Repos.Single(r => r.Name == StatusAggregator.WorkspaceRootName);
        Assert.That((root.Path, root.State, root.Sessions.Single().SessionId), Is.EqualTo((ws, RepoState.Dormant, "root")), "two hours quiet, no tab");
        Assert.That(snap.Repos.Single(r => r.Name == StatusAggregator.OutsideName).Hosts.Single().ProcessId, Is.EqualTo(7));
    }

    [Test]
    public void A_refresh_only_writes_rlstatus_while_it_holds_or_can_take_the_lease()
    {
        // Every open dashboard window and the headless daemon used to refresh and write it independently,
        // flapping the pace gate's hold and the "RATE LIMIT" banner between whichever wrote last (M.ORCH-T112).
        store.SaveSettings(new OrchestratorSettings { WorktreeRoot = worktrees, BlockBudget = 100_000 });
        var rlPath = Path.Combine(dir, "rl-status.json");

        using (var heldElsewhere = new MindAttic.Orchestrator.Scheduling.DispatchLease(dir))
        {
            Assert.That(heldElsewhere.TryAcquire(), Is.True);
            using var locked = new MindAttic.Orchestrator.Scheduling.DispatchLease(dir);
            var gated = new StatusAggregator(new OrchestratorOptions { WorkspaceRoot = ws, ClaudeProjectsDir = projects, TranscriptCache = null }, store,
                new RateLimitStatusWriter(rlPath), locked) { ScanHosts = () => hosts, OtherProviders = new ProviderLogReader(Path.Combine(dir, "codex"), Path.Combine(dir, "gemini")) };
            gated.Refresh();
            Assert.That(File.Exists(rlPath), Is.False, "another process holds the lease: this one doesn't write");
        }

        using var free = new MindAttic.Orchestrator.Scheduling.DispatchLease(dir);
        var writes = new StatusAggregator(new OrchestratorOptions { WorkspaceRoot = ws, ClaudeProjectsDir = projects, TranscriptCache = null }, store,
            new RateLimitStatusWriter(rlPath), free) { ScanHosts = () => hosts, OtherProviders = new ProviderLogReader(Path.Combine(dir, "codex"), Path.Combine(dir, "gemini")) };
        writes.Refresh();
        Assert.That(File.Exists(rlPath), Is.True, "nobody held it: free to take, and it writes");
    }

    [Test]
    public void Only_the_lease_holder_builds_a_snapshot_and_every_other_process_shows_the_one_it_published()
    {
        // Every dashboard window and the daemon used to poll every transcript, parse subagents and scan the
        // Launcher's pipes on its own timer (M.ORCH-T115).
        store.SaveSettings(new OrchestratorSettings { WorktreeRoot = worktrees, BlockBudget = 100_000 });
        Transcript("s1", Path.Combine(ws, "A"), TimeSpan.FromSeconds(30), 10, 20);
        StatusAggregator With(MindAttic.Orchestrator.Scheduling.DispatchLease lease) =>
            new(new OrchestratorOptions { WorkspaceRoot = ws, ClaudeProjectsDir = projects, TranscriptCache = null }, store,
                new RateLimitStatusWriter(Path.Combine(dir, "rl-status.json")), lease)
            { ScanHosts = () => hosts, OtherProviders = new ProviderLogReader(Path.Combine(dir, "codex"), Path.Combine(dir, "gemini")) };

        using var daemonLease = new MindAttic.Orchestrator.Scheduling.DispatchLease(Path.Combine(dir, "store"));
        using var windowLease = new MindAttic.Orchestrator.Scheduling.DispatchLease(Path.Combine(dir, "store"));
        var daemon = With(daemonLease);
        var built = daemon.Refresh();
        Assert.That((daemon.LastWasShared, daemonLease.IsHeld), Is.EqualTo((false, true)));

        Transcript("s2", Path.Combine(ws, "B"), TimeSpan.FromSeconds(10), 5);
        var window = With(windowLease);
        var shown = window.Refresh();
        Assert.That(window.LastWasShared, Is.True);
        Assert.That(shown.TakenUtc, Is.EqualTo(built.TakenUtc), "the holder's snapshot, not one of its own");
        Assert.That(shown.Repos.Single(r => r.Name == "B").Sessions, Is.Empty, "it didn't read the transcripts itself");

        daemonLease.Dispose(); // the holder exited
        var taken = window.Refresh();
        Assert.That((window.LastWasShared, windowLease.IsHeld), Is.EqualTo((false, true)), "the lease was free: it took over");
        Assert.That(taken.Repos.Single(r => r.Name == "B").Sessions.Single().SessionId, Is.EqualTo("s2"));
    }

    [Test]
    public void A_shown_snapshot_takes_approvals_and_priorities_from_the_database_as_they_are_now()
    {
        // M.ORCH-T396: approve then dispatch read the holder's snapshot from before the approval, up to a minute old.
        var db = MindAttic.Workflow.Data.WorkflowDb.DefaultPath(ws);
        using (var w = MindAttic.Workflow.Data.WorkflowDb.Open(db))
            w.Import([new MindAttic.Workflow.Repo("A", Path.Combine(ws, "A")), new MindAttic.Workflow.Repo("B", Path.Combine(ws, "B"))]);
        try
        {
            StatusAggregator With(MindAttic.Orchestrator.Scheduling.DispatchLease lease) =>
                new(new OrchestratorOptions { WorkspaceRoot = ws, ClaudeProjectsDir = projects, TranscriptCache = null }, store,
                    new RateLimitStatusWriter(Path.Combine(dir, "rl-status.json")), lease)
                { ScanHosts = () => hosts, OtherProviders = new ProviderLogReader(Path.Combine(dir, "codex"), Path.Combine(dir, "gemini")) };
            using var daemonLease = new MindAttic.Orchestrator.Scheduling.DispatchLease(Path.Combine(dir, "store"));
            using var windowLease = new MindAttic.Orchestrator.Scheduling.DispatchLease(Path.Combine(dir, "store"));
            Assert.That(With(daemonLease).Refresh().Approved, Is.Empty);

            using (var w = MindAttic.Workflow.Data.WorkflowDb.Open(db))
            {
                w.Approve("A-T3");
                w.SetPriority("A-T3", 3);
            }
            var window = With(windowLease);
            var shown = window.Refresh();

            Assert.That(window.LastWasShared, Is.True);
            Assert.That((shown.Approved.Contains("a-t3"), shown.Priorities.GetValueOrDefault("A-T3")), Is.EqualTo((true, 3L)));
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); }
    }

    [Test]
    public void A_shown_snapshot_takes_billing_as_it_is_once_a_switch_was_made_after_it()
    {
        // M.ORCH-T452: the dashboard's toggle switched billing, then showed the daemon's snapshot from before the switch: the
        // toggle snapped back to the old mode for up to a minute.
        var contexts = Path.Combine(dir, "session-context");
        Directory.CreateDirectory(contexts);
        var config = Path.Combine(dir, ".claude.json");
        File.WriteAllText(config, "{\"oauthAccount\": {\"accountUuid\": \"acct-1\", \"organizationUuid\": \"org-1\"}}");
        Billing.BillingSwitch Billing() => new(contexts) { ConfigPath = config, VaultKey = () => "sk-ant-api03-0123456789abcdefghijWXYZ", IsAlive = _ => false };
        StatusAggregator With(MindAttic.Orchestrator.Scheduling.DispatchLease lease) =>
            new(new OrchestratorOptions { WorkspaceRoot = ws, ClaudeProjectsDir = projects, TranscriptCache = null }, store,
                new RateLimitStatusWriter(Path.Combine(dir, "rl-status.json")), lease)
            { ScanHosts = () => hosts, OtherProviders = new ProviderLogReader(Path.Combine(dir, "codex"), Path.Combine(dir, "gemini")), BillingSwitch = Billing() };
        using var daemonLease = new MindAttic.Orchestrator.Scheduling.DispatchLease(Path.Combine(dir, "store"));
        using var windowLease = new MindAttic.Orchestrator.Scheduling.DispatchLease(Path.Combine(dir, "store"));
        Assert.That(With(daemonLease).Refresh().Billing!.Mode, Is.EqualTo(MindAttic.Orchestrator.Billing.BillingModes.Team));

        var window = With(windowLease);
        Assert.That(window.Refresh().Billing!.Mode, Is.EqualTo(MindAttic.Orchestrator.Billing.BillingModes.Team), "nothing switched: the holder's");
        Assert.That(window.BillingSwitch.Switch("api", "dashboard", [], DateTime.UtcNow).Error, Is.Null);
        var shown = window.Refresh();

        Assert.That(window.LastWasShared, Is.True);
        Assert.That((shown.Billing!.Mode, shown.Billing.Headline), Is.EqualTo((MindAttic.Orchestrator.Billing.BillingModes.Api, "API KEY: sk-ant-...WXYZ")));
    }

    [Test]
    public void A_published_snapshot_reads_back_with_the_same_numbers_and_ages_out()
    {
        store.SaveSettings(new OrchestratorSettings { WorktreeRoot = worktrees, BlockBudget = 100_000 });
        Transcript("s1", Path.Combine(ws, "A", "src"), TimeSpan.FromSeconds(30), 10, 20);
        Transcript("s2", Path.Combine(worktrees, "A", "A-T1"), TimeSpan.FromMinutes(10), 5);
        hosts.Add(new HostSession("Claude", 42, "mindattic-host-claude-42", Path.Combine(ws, "B")));
        var built = Aggregator().Refresh();
        var a = built.Repos.Single(r => r.Name == "A");
        built = built with
        {
            Repos = [.. built.Repos.Select(r => r.Name != "A" ? r : r with
            {
                Sessions = [.. r.Sessions.Select(x => x with { Loop = new Loop("Bash", "ls", 5, built.TakenUtc) })],
            })],
            LinkedRepos = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase) { ["A"] = new HashSet<string>(["B"], StringComparer.OrdinalIgnoreCase) },
            Requires = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase) { ["A-T3"] = ["A-T1"] },
            Approved = new HashSet<string>(["A-T2"], StringComparer.OrdinalIgnoreCase),
            Priorities = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { ["A-T2"] = 7 },
            Agents = [new AgentBoardRow("s1", "A", "A-T1", "fix", built.TakenUtc, built.TakenUtc, AgentState.Working, "", false, new TokenUsage(1, 2, 3, 4),
                [new SubagentActivity("x", "s1", "Explore", built.TakenUtc, built.TakenUtc, new TokenUsage(5, 6, 7, 8))])],
            Budget = built.Budget! with { Calibration = new LimitCalibration([new LimitObservation(LimitKind.Session, built.TakenUtc, built.TakenUtc.AddHours(1), 123)]) },
        };
        var file = new SnapshotFile(Path.Combine(dir, "snapshot.json"));
        file.Write(built);

        var read = new SnapshotFile(file.Path).Read(built.TakenUtc.AddSeconds(5))!;

        var json = new System.Text.Json.JsonSerializerOptions { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };
        Assert.That(System.Text.Json.JsonSerializer.Serialize(read, json), Is.EqualTo(System.Text.Json.JsonSerializer.Serialize(built, json)));
        Assert.That(read.Repos.SelectMany(r => r.Sessions).Select(s => s.Events), Is.EqualTo(built.Repos.SelectMany(r => r.Sessions).Select(s => s.Events)),
            "usage events travel too: a repo's spend this block is worked out from them");
        Assert.That(read.Repos.Single(r => r.Name == "A").Sessions.Sum(s => s.UsageSince(a.Sessions.Min(x => x.Events[0].AtUtc)).Output), Is.EqualTo(35));
        Assert.That((read.LinkedRepos["a"].Contains("b"), read.Requires["a-t3"].Single()), Is.EqualTo((true, "A-T1")), "lookups stay case-insensitive");
        Assert.That((read.Approved.Contains("a-t2"), read.Priorities["a-t2"]), Is.EqualTo((true, 7L)), "the queue's approvals and priorities too (M.ORCH-T384)");
        Assert.That((read.Pace!.SessionCap, read.Budget!.BlockPercent, read.Budget.Level), Is.EqualTo((built.Pace!.SessionCap, built.Budget!.BlockPercent, built.Budget.Level)));

        Assert.That(new SnapshotFile(file.Path).Read(built.TakenUtc + SnapshotFile.MaxAge + TimeSpan.FromSeconds(1)), Is.Null,
            "its writer stopped: build one instead");
    }

    // M.ORCH-T131: the lease's edges, and the 5-minute history save.

    private StatusAggregator Leased(MindAttic.Orchestrator.Scheduling.DispatchLease lease, string rlPath) =>
        new(new OrchestratorOptions { WorkspaceRoot = ws, ClaudeProjectsDir = projects, TranscriptCache = null }, store,
            new RateLimitStatusWriter(rlPath), lease)
        { ScanHosts = () => hosts, OtherProviders = new ProviderLogReader(Path.Combine(dir, "codex"), Path.Combine(dir, "gemini")) };

    /// <summary>The scratch orchestrator.db every aggregator here uses (OrchestratorDbPerTest points the default at it).</summary>
    private static OrchestratorDb History => new(OrchestratorDb.DefaultPath());

    /// <summary>Another process's write holding the database: every read and write here gives up after a short busy timeout.</summary>
    private static IDisposable HoldDb()
    {
        var before = OrchestratorDb.BusyTimeoutMs;
        OrchestratorDb.BusyTimeoutMs = 100;
        var connection = History.Open();
        OrchestratorDb.Execute(connection, "BEGIN EXCLUSIVE;");
        return new Held(() =>
        {
            OrchestratorDb.Execute(connection, "ROLLBACK;");
            connection.Dispose();
            OrchestratorDb.BusyTimeoutMs = before;
        });
    }

    private sealed class Held(Action release) : IDisposable
    {
        public void Dispose() => release();
    }

    private static DateTime Seconds(DateTime t) => new(t.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, DateTimeKind.Utc);

    [Test]
    public void A_store_less_aggregator_sizes_the_5_hour_limit_from_the_same_usage_samples_as_a_store_backed_one()
    {
        // It used to skip usage-samples.json without a store (if (store is null) return null), so a one-off CLI run
        // fell back to a learned or peak limit while the daemon sized it from Claude's own reports (MAO-T121).
        var options = new OrchestratorOptions { WorkspaceRoot = ws, ClaudeProjectsDir = projects, TranscriptCache = null };
        Assert.That((new StatusAggregator(options).Db.Path, new StatusAggregator(options, store).Db.Path), Is.EqualTo((History.Path, History.Path)),
            "store or no store: it reads and keeps the meter readings in the orchestrator.db the daemon and dashboard do");

        var reset = Seconds(DateTime.UtcNow.AddHours(3));
        var at = Seconds(DateTime.UtcNow.AddMinutes(-1));
        List<UsageSample> samples = [new(at.AddHours(-1), 10, reset, 100_000), new(at, 30, reset, 300_000)];
        History.AddUsageSamples(samples, DateTime.MinValue);
        var reportPath = Path.Combine(dir, "claude-usage.json");
        File.WriteAllText(reportPath, System.Text.Json.JsonSerializer.Serialize(new
        {
            rate_limits = new { five_hour = new { used_percentage = 30, resets_at = new DateTimeOffset(reset).ToUnixTimeSeconds() } },
            at = at.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
        }));
        StatusAggregator On(JsonStore s) => new(options, s)
        {
            ScanHosts = () => hosts, OtherProviders = new ProviderLogReader(Path.Combine(dir, "codex"), Path.Combine(dir, "gemini")), ReportedUsagePath = reportPath,
        };

        var budget = On(store).Refresh().Budget!;
        var again = On(new JsonStore(store.Directory)).Refresh().Budget!;

        Assert.That((budget.BlockLimitSource, budget.BlockLimit), Is.EqualTo((LimitSource.Reported, ReportedLimit.Estimate(samples)!.Value)),
            "sized from how Claude's percent moved between the saved samples");
        Assert.That((again.BlockLimitSource, again.BlockLimit), Is.EqualTo((budget.BlockLimitSource, budget.BlockLimit)),
            "any aggregator over that store directory sizes it the same");
        Assert.That(History.LoadUsageSamples(), Has.Count.EqualTo(2), "a report already sampled isn't recorded twice");
    }

    [Test]
    public void A_limits_file_unreadable_for_a_refresh_keeps_the_learned_limit()
    {
        // M.ORCH-T346: read as no hits, the learned limit went, the pace with it, and rl-status.json was withdrawn.
        Transcript("s1", Path.Combine(ws, "A"), TimeSpan.FromMinutes(2), 50_000);
        var old = Seconds(DateTime.UtcNow.AddDays(-2));
        store.SaveLimits([new LimitObservation(LimitKind.Session, old, old.AddHours(1), 9_000_000)]);
        var aggregator = new StatusAggregator(new OrchestratorOptions { WorkspaceRoot = ws, ClaudeProjectsDir = projects, TranscriptCache = null }, store)
        {
            ScanHosts = () => hosts, OtherProviders = new ProviderLogReader(Path.Combine(dir, "codex"), Path.Combine(dir, "gemini")),
            ReportedUsagePath = Path.Combine(dir, "no-report.json"),
        };
        Assert.That(aggregator.Refresh().Budget!.BlockLimitSource, Is.EqualTo(LimitSource.Learned));

        using (new FileStream(Path.Combine(store.Directory, "limits.json"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var held = aggregator.Refresh(DateTime.UtcNow.AddSeconds(5)).Budget!;
            Assert.That((held.BlockLimitSource, held.BlockLimit), Is.EqualTo((LimitSource.Learned, 9_000_000L)), "the hits last read stand in");
        }
        Assert.That(store.LoadLimits(), Has.Count.EqualTo(1), "and nothing was saved over it");
    }

    [Test]
    public void A_held_database_does_not_fail_the_refresh()
    {
        var options = new OrchestratorOptions { WorkspaceRoot = ws, ClaudeProjectsDir = projects, TranscriptCache = null };
        var reset = Seconds(DateTime.UtcNow.AddHours(3));
        var at = Seconds(DateTime.UtcNow.AddMinutes(-1));
        History.AddUsageSamples([new(at.AddHours(-1), 10, reset, 100_000)], DateTime.MinValue);
        var reportPath = Path.Combine(dir, "claude-usage.json");
        File.WriteAllText(reportPath, System.Text.Json.JsonSerializer.Serialize(new
        {
            rate_limits = new { five_hour = new { used_percentage = 30, resets_at = new DateTimeOffset(reset).ToUnixTimeSeconds() } },
            at = at.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
        }));
        var aggregator = new StatusAggregator(options, store)
        {
            ScanHosts = () => [], OtherProviders = new ProviderLogReader(Path.Combine(dir, "codex"), Path.Combine(dir, "gemini")), ReportedUsagePath = reportPath,
        };

        // Another process holds it (a long write, git committing the file) past the busy timeout.
        using (HoldDb())
            Assert.That(aggregator.Refresh().Budget, Is.Not.Null, "the refresh goes on");
        Assert.That(History.LoadUsageSamples(), Has.Count.EqualTo(1), "not saved while held");
    }

    // M.ORCH-T245: once a minute from 04:37 to 04:58Z on Oct 17 an idle tab's statusline stamped its last reply from the
    // window that reset at 00:10Z into claude-usage.json; that tick had no 5-hour window, so no limit: the pace went
    // unpaced and rl-status.json was withdrawn, and every call the gate was holding went through at once.

    private void Report(string path, double percent, DateTime resetUtc, DateTime atUtc) =>
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new
        {
            rate_limits = new { five_hour = new { used_percentage = percent, resets_at = new DateTimeOffset(resetUtc).ToUnixTimeSeconds() } },
            at = atUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
        }));

    private (StatusAggregator Aggregator, string Report, string RlStatus) PacedOnReports()
    {
        Transcript("s1", Path.Combine(ws, "A"), TimeSpan.FromMinutes(2), 50_000, 60_000, 70_000);
        var reportPath = Path.Combine(dir, "claude-usage.json");
        var rlPath = Path.Combine(dir, "rl-status.json");
        var aggregator = new StatusAggregator(new OrchestratorOptions { WorkspaceRoot = ws, ClaudeProjectsDir = projects, TranscriptCache = null },
            store, new RateLimitStatusWriter(rlPath))
        {
            ScanHosts = () => hosts, OtherProviders = new ProviderLogReader(Path.Combine(dir, "codex"), Path.Combine(dir, "gemini")), ReportedUsagePath = reportPath,
        };
        return (aggregator, reportPath, rlPath);
    }

    [Test]
    public void An_idle_tabs_report_from_a_reset_window_keeps_the_limit_while_usage_samples_is_unreadable()
    {
        var (aggregator, reportPath, rlPath) = PacedOnReports();
        var reset = Seconds(DateTime.UtcNow.AddMinutes(33));
        var at = Seconds(DateTime.UtcNow.AddMinutes(-1));
        History.AddUsageSamples([new(at.AddHours(-1), 60, reset, 100_000), new(at.AddMinutes(-1), 80, reset, 200_000)], DateTime.MinValue);
        Report(reportPath, 82, reset, at);
        var before = aggregator.Refresh();
        Assert.That((before.Budget!.BlockLimitSource, before.Pace!.IsPaced), Is.EqualTo((LimitSource.Reported, true)));

        // Another process holds the database past the busy timeout, and the idle tab's minute comes round.
        var kept = History.LoadUsageSamples();
        Report(reportPath, 96, Seconds(DateTime.UtcNow.AddHours(-4.5)), Seconds(DateTime.UtcNow));
        Snapshot tick;
        using (HoldDb()) tick = aggregator.Refresh();

        Assert.That((tick.Budget!.BlockLimitIsPeak, tick.Budget.BlockLimitSource, tick.Budget.BlockLimit),
            Is.EqualTo((false, LimitSource.Reported, before.Budget.BlockLimit)), "the running window's limit, not none");
        Assert.That(tick.Budget.CurrentBlock!.EndUtc, Is.EqualTo(reset), "counted against the window still running");
        Assert.That(tick.Pace!.IsPaced, Is.True, "not 'unpaced: nothing to pace against'");
        using (var rl = System.Text.Json.JsonDocument.Parse(File.ReadAllText(rlPath)))
            Assert.That(rl.RootElement.TryGetProperty("percent_used", out _) && rl.RootElement.TryGetProperty("pace", out _), Is.True,
                "rl-status.json still carries the percent and the pace for the gate");
        Assert.That(History.LoadUsageSamples(), Is.EqualTo(kept), "nothing this tick saw is saved over the readings");
    }

    [Test]
    public void A_report_with_no_running_5_hour_window_keeps_the_last_reports_window_while_it_runs()
    {
        var (aggregator, reportPath, _) = PacedOnReports();
        var reset = Seconds(DateTime.UtcNow.AddMinutes(33));
        Report(reportPath, 30, reset, Seconds(DateTime.UtcNow.AddMinutes(-1)));
        var before = aggregator.Refresh().Budget!;
        Assert.That(before.BlockLimitSource, Is.EqualTo(LimitSource.Reported));

        // No sample of the running window to fall back on (the week's readings are gone), and the idle tab's stale report.
        using (var c = History.Open()) OrchestratorDb.Execute(c, "DELETE FROM usage_samples;");
        Report(reportPath, 96, Seconds(DateTime.UtcNow.AddHours(-4.5)), Seconds(DateTime.UtcNow));
        var tick = aggregator.Refresh();

        Assert.That((tick.Budget!.BlockLimitIsPeak, tick.Budget.BlockLimitSource, tick.Budget.CurrentBlock!.EndUtc),
            Is.EqualTo((false, LimitSource.Reported, reset)), "the last report's window is kept until it resets");
        Assert.That(tick.Pace!.IsPaced, Is.True);
    }

    [Test]
    public void Usage_samples_unreadable_on_the_first_refresh_are_read_once_they_can_be_not_saved_over()
    {
        var (aggregator, reportPath, _) = PacedOnReports();
        var reset = Seconds(DateTime.UtcNow.AddMinutes(33));
        var at = Seconds(DateTime.UtcNow.AddMinutes(-1));
        History.AddUsageSamples([new(at.AddHours(-1), 10, reset, 100_000)], DateTime.MinValue);
        Report(reportPath, 30, reset, at);

        using (HoldDb())
            aggregator.Refresh();
        aggregator.Refresh();

        Assert.That(History.LoadUsageSamples(), Has.Count.EqualTo(2),
            "reread once free: the week plus this report, not this report alone over an empty week");
    }

    [Test]
    public void A_one_off_aggregator_whose_lease_is_held_elsewhere_shows_the_published_snapshot_and_never_writes_rlstatus()
    {
        store.SaveSettings(new OrchestratorSettings { WorktreeRoot = worktrees, BlockBudget = 100_000 });
        Transcript("s1", Path.Combine(ws, "A"), TimeSpan.FromSeconds(30), 10, 20);
        var oneOffRl = Path.Combine(dir, "one-off-rl-status.json");

        using var daemonLease = new MindAttic.Orchestrator.Scheduling.DispatchLease(store.Directory);
        using var oneOffLease = new MindAttic.Orchestrator.Scheduling.DispatchLease(store.Directory);
        Assert.That(daemonLease.TryAcquire(), Is.True);

        // Nothing published yet: it builds its own to show, but still leaves rl-status.json to the holder.
        var unpublished = Leased(oneOffLease, oneOffRl);
        unpublished.Refresh();
        Assert.That((unpublished.LastWasShared, oneOffLease.IsHeld, File.Exists(oneOffRl)), Is.EqualTo((false, false, false)));

        var built = Leased(daemonLease, Path.Combine(dir, "daemon-rl-status.json")).Refresh();
        Transcript("s2", Path.Combine(ws, "B"), TimeSpan.FromSeconds(10), 5);
        var oneOff = Leased(oneOffLease, oneOffRl);
        var shown = oneOff.Refresh();

        Assert.That(oneOff.LastWasShared, Is.True);
        Assert.That(shown.TakenUtc, Is.EqualTo(built.TakenUtc), "the holder's published snapshot");
        Assert.That(shown.Repos.Single(r => r.Name == "B").Sessions, Is.Empty, "not one it read the transcripts for");
        Assert.That((oneOffLease.IsHeld, File.Exists(oneOffRl)), Is.EqualTo((false, false)), "never writes rl-status.json under another's lease");
    }

    [Test]
    public void A_refresh_for_a_given_time_always_builds_its_own_and_never_publishes()
    {
        store.SaveSettings(new OrchestratorSettings { WorktreeRoot = worktrees, BlockBudget = 100_000 });
        Transcript("s1", Path.Combine(ws, "A"), TimeSpan.FromSeconds(30), 10, 20);
        var snapshotPath = Path.Combine(store.Directory, "status-snapshot.json");

        using var daemonLease = new MindAttic.Orchestrator.Scheduling.DispatchLease(store.Directory);
        using var windowLease = new MindAttic.Orchestrator.Scheduling.DispatchLease(store.Directory);
        var daemon = Leased(daemonLease, Path.Combine(dir, "rl-status.json"));
        var published = daemon.Refresh();
        Assert.That(new SnapshotFile(snapshotPath).Read(DateTime.UtcNow)!.TakenUtc, Is.EqualTo(published.TakenUtc));

        Transcript("s2", Path.Combine(ws, "B"), TimeSpan.FromSeconds(10), 5);
        var at = DateTime.UtcNow;
        var window = Leased(windowLease, Path.Combine(dir, "window-rl-status.json"));
        var own = window.Refresh(at);
        Assert.That((window.LastWasShared, own.TakenUtc), Is.EqualTo((false, at)), "a fresh published snapshot was there, but it built its own");
        Assert.That(own.Repos.Single(r => r.Name == "B").Sessions.Single().SessionId, Is.EqualTo("s2"));

        var held = daemon.Refresh(at.AddSeconds(1));
        Assert.That((daemon.LastWasShared, held.TakenUtc), Is.EqualTo((false, at.AddSeconds(1))));
        Assert.That(new SnapshotFile(snapshotPath).Read(DateTime.UtcNow)!.TakenUtc, Is.EqualTo(published.TakenUtc),
            "neither the other window nor the holder itself published a snapshot for a given time");
    }

    [Test]
    public void Usage_history_is_saved_at_most_every_5_minutes()
    {
        Transcript("s1", Path.Combine(ws, "A"), TimeSpan.FromMinutes(1), 10);
        long Saved(string repo) => History.LoadUsageHistory().Where(d => d.Repo == repo).Sum(d => d.Fresh);
        var aggregator = Aggregator();
        var t0 = DateTime.UtcNow;

        aggregator.Refresh(t0);
        Assert.That((Saved("A") > 0, Saved("B")), Is.EqualTo((true, 0L)), "the first refresh saves");

        Transcript("s2", Path.Combine(ws, "B"), TimeSpan.FromSeconds(10), 20);
        var shown = aggregator.Refresh(t0.AddMinutes(4)).History!.Where(d => d.Repo == "B").Sum(d => d.Fresh);
        Assert.That(shown, Is.GreaterThan(0L), "4 minutes on: it's shown");
        Assert.That(Saved("B"), Is.Zero, "but the saved copy waits");

        aggregator.Refresh(t0.AddMinutes(5));
        Assert.That(Saved("B"), Is.EqualTo(shown), "5 minutes on: saved");
    }

    // M.ORCH-T285: sign-in changes, as the daemon sees them.

    private string Config(string json)
    {
        var path = Path.Combine(dir, ".claude.json");
        File.WriteAllText(path, json);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(Interlocked.Increment(ref configTick)));
        return path;
    }

    private int configTick;

    /// <summary>The account record as the version before spans wrote it: signed in to <paramref name="account"/> <paramref name="ago"/>.</summary>
    private void SignedInSince(string account, TimeSpan ago)
    {
        var path = SignIn().StatePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $$"""{"account":"{{account}}","since":{{new DateTimeOffset(now - ago).ToUnixTimeSeconds()}}}""");
    }

    private ClaudeAccount SignIn() => new() { ConfigPath = Path.Combine(dir, ".claude.json"), StatePath = Path.Combine(dir, "session-context", ClaudeAccount.FileName) };

    private StatusAggregator Daemon(ClaudeAccount account, string? rlPath = null, ClaudeMeterProbe? probe = null) =>
        new(new OrchestratorOptions { WorkspaceRoot = ws, ClaudeProjectsDir = projects, TranscriptCache = null }, store,
            rlPath is null ? null : new RateLimitStatusWriter(rlPath))
        {
            ScanHosts = () => hosts, OtherProviders = new ProviderLogReader(Path.Combine(dir, "codex"), Path.Combine(dir, "gemini")),
            ReportedUsagePath = Path.Combine(dir, "claude-usage.json"), Account = account, MeterProbe = probe,
            SessionContextDir = Path.Combine(dir, "session-context"),
        };

    private void Usage(double fivePercent, DateTime fiveReset, double weekPercent, DateTime weekReset, string? account = null)
    {
        var body = new System.Text.Json.Nodes.JsonObject
        {
            ["rate_limits"] = new System.Text.Json.Nodes.JsonObject
            {
                ["five_hour"] = new System.Text.Json.Nodes.JsonObject { ["used_percentage"] = fivePercent, ["resets_at"] = new DateTimeOffset(fiveReset).ToUnixTimeSeconds() },
                ["seven_day"] = new System.Text.Json.Nodes.JsonObject { ["used_percentage"] = weekPercent, ["resets_at"] = new DateTimeOffset(weekReset).ToUnixTimeSeconds() },
            },
            ["at"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
        };
        if (account is not null) body["account"] = account;
        File.WriteAllText(Path.Combine(dir, "claude-usage.json"), body.ToJsonString());
    }

    [Test]
    public void On_an_api_key_the_daemon_writes_no_stop_and_paces_nothing()
    {
        store.SaveSettings(new OrchestratorSettings { WorktreeRoot = worktrees, BlockBudget = 1_000 });
        Transcript("s1", Path.Combine(ws, "A"), TimeSpan.FromMinutes(1), 50_000, 60_000);
        var rlPath = Path.Combine(dir, "rl-status.json");

        Config("""{"oauthAccount":{"accountUuid":"a","organizationUuid":"max"}}""");
        SignedInSince("a/max", TimeSpan.FromHours(1));
        var paced = Daemon(SignIn(), rlPath).Refresh();
        Assert.That((paced.Pace!.Stop, File.ReadAllText(rlPath).Contains("\"stop\": true")), Is.EqualTo((true, true)), "the login is past its budget: stopped");

        Config("""{"primaryApiKey":"sk-ant-x"}""");
        var keyed = Daemon(SignIn(), rlPath).Refresh();
        Assert.That((keyed.Pace!.IsPaced, keyed.Pace.Stop, keyed.Pace.Note, keyed.Pace.AgentDelaySeconds), Is.EqualTo((false, false, Pacer.ApiKeyNote, 0.0)));
        Assert.That(keyed.Budget!.Level, Is.EqualTo(BudgetLevel.Ok), "orchestrator next's warn line doesn't hold it either");
        Assert.That(!File.Exists(rlPath) || !File.ReadAllText(rlPath).Contains("\"stop\""), Is.True, "no stop for the hooks");
    }

    [Test]
    public void A_session_on_an_env_api_key_is_left_out_of_the_subscription_budget()
    {
        TurnsAreToday();
        Transcript("s1", Path.Combine(ws, "A"), TimeSpan.FromMinutes(1), 100);
        Transcript("s2", Path.Combine(ws, "B"), TimeSpan.FromMinutes(1), 5_000);
        Config("""{"oauthAccount":{"accountUuid":"a"}}""");
        var account = SignIn();
        Directory.CreateDirectory(Path.Combine(dir, "session-context"));
        File.WriteAllText(Path.Combine(dir, "session-context", "s1.state.json"), """{"state": "busy", "auth": "subscription"}""");
        File.WriteAllText(Path.Combine(dir, "session-context", "s2.state.json"), """{"state": "busy", "auth": "api-key"}""");
        SignedInSince("a", TimeSpan.FromHours(1));

        var snap = Daemon(account).Refresh(now);

        Assert.That(snap.Budget!.Today.Output, Is.EqualTo(100), "only the subscription's session counts");
        Assert.That(snap.Repos.Single(r => r.Name == "B").UsageToday.Output, Is.EqualTo(5_000), "the key's work still shows");
    }

    [Test]
    public void A_limit_hit_is_learned_from_the_tokens_of_the_account_that_hit_it()
    {
        // M.ORCH-T347: B's wall counted A's 4M spent in B's window during an hour signed in to A.
        Transcript("a-work", Path.Combine(ws, "A"), TimeSpan.FromMinutes(150), 4_000_000);
        Transcript("b-work", Path.Combine(ws, "B"), TimeSpan.FromMinutes(30), 1_000_000);
        var hitAt = DateTime.UtcNow.AddMinutes(-10);
        var reset = new DateTime(DateTime.UtcNow.AddHours(1).Ticks / TimeSpan.TicksPerMinute * TimeSpan.TicksPerMinute, DateTimeKind.Utc);
        var folder = Path.Combine(projects, "p-hit");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "hit.jsonl"), System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "assistant", timestamp = hitAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"), sessionId = "hit", cwd = Path.Combine(ws, "B"), error = "rate_limit",
            message = new
            {
                id = "hit-1", model = "<synthetic>",
                usage = new { input_tokens = 0, cache_creation_input_tokens = 0, cache_read_input_tokens = 0, output_tokens = 0 },
                content = new[] { new { type = "text", text = $"You've hit your session limit · resets {reset.ToString("h:mmtt", System.Globalization.CultureInfo.InvariantCulture).ToLowerInvariant()} (UTC)" } },
            },
        }) + "\n");
        Config("""{"oauthAccount":{"accountUuid":"b"}}""");
        var account = SignIn();
        Directory.CreateDirectory(Path.GetDirectoryName(account.StatePath)!);
        long Ago(int minutes) => DateTimeOffset.UtcNow.AddMinutes(-minutes).ToUnixTimeSeconds();
        File.WriteAllText(account.StatePath, $$"""
            {"account":"b","since":{{Ago(120)}},"spans":[{"account":"b","from":{{Ago(240)}},"to":{{Ago(180)}}},
            {"account":"a","from":{{Ago(180)}},"to":{{Ago(120)}}},{"account":"b","from":{{Ago(120)}},"to":null}]}
            """);

        Daemon(account).Refresh();

        var wall = store.LoadLimits().Single(o => o.Kind == LimitKind.Session);
        Assert.That(wall.FreshUsed, Is.EqualTo(1_000_001), "B's own tokens, not A's 4M too");
    }

    [Test]
    public void The_weekly_bars_count_only_the_signed_in_accounts_subscription_tokens_as_the_weeks_total_does()
    {
        // M.ORCH-T372: the bars added every account's and every API-key CLI's tokens; the week's total only b's.
        TurnsAreToday();
        Transcript("a-work", Path.Combine(ws, "A"), TimeSpan.FromMinutes(150), 4_000);
        Transcript("b-work", Path.Combine(ws, "B"), TimeSpan.FromMinutes(30), 1_000);
        Transcript("key-work", Path.Combine(ws, "B"), TimeSpan.FromMinutes(20), 50_000);
        Config("""{"oauthAccount":{"accountUuid":"b"}}""");
        var account = SignIn();
        Directory.CreateDirectory(Path.GetDirectoryName(account.StatePath)!);
        File.WriteAllText(Path.Combine(dir, "session-context", "key-work.state.json"), """{"state": "busy", "auth": "api-key"}""");
        long Ago(int minutes) => new DateTimeOffset(now.AddMinutes(-minutes)).ToUnixTimeSeconds();
        File.WriteAllText(account.StatePath, $$"""
            {"account":"b","since":{{Ago(120)}},"spans":[{"account":"a","from":{{Ago(240)}},"to":{{Ago(120)}}},
            {"account":"b","from":{{Ago(120)}},"to":null}]}
            """);

        var snap = Daemon(account).Refresh(now);
        var week = Burndown.Compute(snap.Budget!, snap.History, snap.TakenUtc, snap.Account);

        Assert.That(snap.Account, Is.EqualTo("b"));
        Assert.That((week.Days.Sum(d => d.Fresh), snap.Budget!.Week.Fresh), Is.EqualTo((1_001L, 1_001L)), "b's own tokens in both");
        Assert.That(snap.History.Where(d => d.Provider == Providers.Claude).Select(d => (d.Account, d.Fresh)),
            Is.EquivalentTo(new[] { ("a", 4_001L), ("b", 1_001L), (UsageHistory.BilledElsewhere + "api-key", 50_001L) }),
            "kept per account, so the other's and the key's still show in reports");
    }

    [Test]
    public void Going_back_to_an_account_counts_its_spend_from_before_the_other_one_again()
    {
        TurnsAreToday();
        Transcript("s1", Path.Combine(ws, "A"), TimeSpan.FromMinutes(50), 100, 200);
        Transcript("s2", Path.Combine(ws, "B"), TimeSpan.FromMinutes(20), 7_000);
        Config("""{"oauthAccount":{"accountUuid":"a"}}""");
        var account = SignIn();
        Directory.CreateDirectory(Path.GetDirectoryName(account.StatePath)!);
        long Ago(int minutes) => new DateTimeOffset(now.AddMinutes(-minutes)).ToUnixTimeSeconds();
        File.WriteAllText(account.StatePath, $$"""
            {"account":"a","since":{{Ago(10)}},"spans":[{"account":"a","from":{{Ago(120)}},"to":{{Ago(30)}}},
            {"account":"b","from":{{Ago(30)}},"to":{{Ago(10)}}},{"account":"a","from":{{Ago(10)}},"to":null}]}
            """);

        Assert.That(Daemon(account).Refresh(now).Budget!.Today.Output, Is.EqualTo(300), "A's turns before B count again; B's don't");
    }

    [Test]
    public void After_a_switch_the_probe_reads_the_new_account_at_once_and_another_clis_stale_reading_is_ignored()
    {
        var aFive = Seconds(DateTime.UtcNow.AddHours(1));
        var aWeek = Seconds(DateTime.UtcNow.AddDays(2));
        var bFive = new DateTime(DateTime.UtcNow.AddHours(3).Ticks / TimeSpan.TicksPerMinute * TimeSpan.TicksPerMinute, DateTimeKind.Utc);
        var bWeek = new DateTime(DateTime.UtcNow.AddDays(5).Ticks / TimeSpan.TicksPerMinute * TimeSpan.TicksPerMinute, DateTimeKind.Utc);
        var credentials = Path.Combine(dir, ".credentials.json");
        File.WriteAllText(credentials, $$$"""{"claudeAiOauth":{"accessToken":"tok","expiresAt":{{{DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds()}}}}}""");
        var asked = 0;
        var probe = new ClaudeMeterProbe
        {
            UsagePath = Path.Combine(dir, "claude-usage.json"), CredentialsPath = credentials,
            AccountKey = () => ClaudeAccount.Current(Path.Combine(dir, ".claude.json")),
            Fetch = (_, _) =>
            {
                asked++;
                return Task.FromResult<string?>($$$"""{"five_hour":{"utilization":12.0,"resets_at":"{{{bFive:O}}}"},"seven_day":{"utilization":20.0,"resets_at":"{{{bWeek:O}}}"}}""");
            },
        };
        Config("""{"oauthAccount":{"accountUuid":"a","organizationUuid":"max"}}""");
        var account = SignIn();
        var daemon = Daemon(account, probe: probe);
        Usage(30, aFive, 99, aWeek);
        Assert.That(daemon.Refresh().Budget!.WeeklyPercent, Is.EqualTo(99), "A's own reading");
        Assert.That(asked, Is.Zero, "nothing spent, nothing stale: no ask");

        Config("""{"oauthAccount":{"accountUuid":"b","organizationUuid":"max"}}""");
        Usage(30, aFive, 99, aWeek); // another CLI restamps A's last reply
        var switched = daemon.Refresh();
        Assert.That(switched.Budget!.WeeklyPercent, Is.Not.EqualTo(99), "A's reading isn't B's");
        probe.Pending.Wait();
        Assert.That(asked, Is.EqualTo(1), "the switch was read at once, though nothing was stale");
        Assert.That(File.ReadAllText(Path.Combine(dir, "claude-usage.json")), Does.Contain("\"account\":\"b/max\""), "tagged with the account it read");

        var read = daemon.Refresh().Budget!;
        Assert.That((read.WeeklyPercent, read.CurrentBlock!.EndUtc), Is.EqualTo(((double?)20, bFive)), "the probe's reading is B's");
        Usage(30, aFive, 99, aWeek);
        var stale = daemon.Refresh().Budget!;
        Assert.That((stale.WeeklyPercent, stale.CurrentBlock!.EndUtc), Is.Not.EqualTo(((double?)99, aFive)), "A's restamped reading still isn't");
        Assert.That(stale.CurrentBlock.EndUtc, Is.EqualTo(bFive), "B's window carries on");
        Thread.Sleep(1100); // a statusline's stamp is to the second: not the second B was signed in to
        Usage(15, bFive, 25, bWeek);
        Assert.That(daemon.Refresh().Budget!.WeeklyPercent, Is.EqualTo(25), "B's statusline, its windows confirmed by the probe");
    }

    [Test]
    public void A_login_to_another_account_moves_everything_to_it_at_once_without_a_restart()
    {
        // M.ORCH-T486 (and M.ORCH-T485's weekly limit): one daemon, never restarted, sees a /login from A (at its weekly stop,
        // with a 5-hour limit learned from its own wall) to B. Everything follows B at once, and nothing pairs one account's
        // meter with the other's tokens.
        var aFive = Seconds(DateTime.UtcNow.AddHours(2));
        var aWeek = Seconds(DateTime.UtcNow.AddDays(2));
        var bFive = new DateTime(DateTime.UtcNow.AddHours(4).Ticks / TimeSpan.TicksPerMinute * TimeSpan.TicksPerMinute, DateTimeKind.Utc);
        var bWeek = new DateTime(DateTime.UtcNow.AddDays(5).Ticks / TimeSpan.TicksPerMinute * TimeSpan.TicksPerMinute, DateTimeKind.Utc);
        // A's wall two days ago, with 3M of A's tokens in its window: A's learned 5-hour limit.
        Transcript("a-old", Path.Combine(ws, "A"), TimeSpan.FromDays(2) + TimeSpan.FromMinutes(5), 3_000_000);
        var hitAt = now.AddDays(-2);
        var hitReset = new DateTime(hitAt.AddHours(1).Ticks / TimeSpan.TicksPerMinute * TimeSpan.TicksPerMinute, DateTimeKind.Utc);
        var folder = Path.Combine(projects, "p-hit");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "hit.jsonl"), System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "assistant", timestamp = hitAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"), sessionId = "hit", cwd = Path.Combine(ws, "A"), error = "rate_limit",
            message = new
            {
                id = "hit-1", model = "<synthetic>",
                usage = new { input_tokens = 0, cache_creation_input_tokens = 0, cache_read_input_tokens = 0, output_tokens = 0 },
                content = new[] { new { type = "text", text = $"You've hit your session limit · resets {hitReset.ToString("h:mmtt", System.Globalization.CultureInfo.InvariantCulture).ToLowerInvariant()} (UTC)" } },
            },
        }) + "\n");
        Transcript("a-work", Path.Combine(ws, "A"), TimeSpan.FromMinutes(40), 500_000, 500_000);

        var credentials = Path.Combine(dir, ".credentials.json");
        File.WriteAllText(credentials, $$$"""{"claudeAiOauth":{"accessToken":"tok","expiresAt":{{{DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds()}}}}}""");
        var asked = 0;
        var probe = new ClaudeMeterProbe
        {
            UsagePath = Path.Combine(dir, "claude-usage.json"), CredentialsPath = credentials,
            AccountKey = () => ClaudeAccount.Current(Path.Combine(dir, ".claude.json")),
            Fetch = (_, _) =>
            {
                asked++;
                return Task.FromResult<string?>($$$"""{"five_hour":{"utilization":10.0,"resets_at":"{{{bFive:O}}}"},"seven_day":{"utilization":77.0,"resets_at":"{{{bWeek:O}}}"}}""");
            },
        };
        var rlPath = Path.Combine(dir, "rl-status.json");
        var config = Config("""{"oauthAccount":{"accountUuid":"a","organizationUuid":"max","emailAddress":"a@example.com"}}""");
        SignedInSince("a/max", TimeSpan.FromDays(3));
        var daemon = new StatusAggregator(new OrchestratorOptions { WorkspaceRoot = ws, ClaudeProjectsDir = projects, TranscriptCache = null }, store,
            new RateLimitStatusWriter(rlPath))
        {
            ScanHosts = () => hosts, OtherProviders = new ProviderLogReader(Path.Combine(dir, "codex"), Path.Combine(dir, "gemini")),
            ReportedUsagePath = Path.Combine(dir, "claude-usage.json"), Account = SignIn(), MeterProbe = probe,
            SessionContextDir = Path.Combine(dir, "session-context"),
            BillingSwitch = new Billing.BillingSwitch(Path.Combine(dir, "session-context")) { ConfigPath = config, VaultKey = () => null, IsAlive = _ => false },
        };

        // A: its own reading, its weekly stop, its learned limit.
        Usage(40, aFive, 99, aWeek);
        var a = daemon.Refresh();
        var aLearned = store.LoadLimits().Single(o => o.Kind == LimitKind.Session).FreshUsed;
        Assert.That(aLearned, Is.EqualTo(3_000_001L), "A's wall, sized by A's tokens");
        Assert.That((a.Account, a.Budget!.WeeklyPercent, a.Budget.WeekResetUtc, a.Budget.BlockLimit), Is.EqualTo(("a/max", (double?)99, (DateTime?)aWeek, aLearned)));
        Assert.That((a.Pace!.Stop, File.ReadAllText(rlPath).Contains("\"stop\": true")), Is.EqualTo((true, true)), "A is at its weekly stop");
        Assert.That(a.Billing!.Headline, Does.Contain("a@example.com"));
        Assert.That(asked, Is.Zero, "A's reading is fresh: no ask");

        // The /login. Another CLI's statusline restamps A's last reply as new.
        Config("""{"oauthAccount":{"accountUuid":"b","organizationUuid":"max","emailAddress":"b@example.com"}}""");
        Usage(40, aFive, 99, aWeek);
        var switched = daemon.Refresh();
        Assert.That(switched.Account, Is.EqualTo("b/max"));
        Assert.That(switched.Budget!.WeeklyPercent, Is.Not.EqualTo(99), "A's percent isn't B's");
        Assert.That(switched.Budget.Week.Fresh, Is.Zero, "B's ledger: none of A's tokens");
        Assert.That(switched.Budget.BlockLimit, Is.Not.EqualTo(aLearned), "A's learned limit doesn't size B's window");
        Assert.That(switched.Pace!.Stop, Is.False, "A's weekly stop doesn't hold B");
        Assert.That(!File.Exists(rlPath) || !File.ReadAllText(rlPath).Contains("\"stop\""), Is.True, "nor do the hooks get it");
        Assert.That(switched.Billing!.Headline, Does.Contain("b@example.com"), "the dashboard's billing names B");
        probe.Pending.Wait();
        Assert.That(asked, Is.EqualTo(1), "B's meter is read the moment the switch is seen, not when a CLI next replies");
        Assert.That(File.ReadAllText(Path.Combine(dir, "claude-usage.json")), Does.Contain("\"account\":\"b/max\""), "and written tagged with B");

        // B spends 43.5k after the switch: 77% of B's week is mostly what B spent before this machine's ledger began.
        Thread.Sleep(1100);
        now = DateTime.UtcNow;
        Transcript("b-work", Path.Combine(ws, "B"), TimeSpan.Zero, 43_499);
        var b = daemon.Refresh();
        var budget = b.Budget!;
        Assert.That((b.Account, budget.WeeklyPercent, budget.WeekResetUtc, budget.CurrentBlock!.EndUtc), Is.EqualTo(("b/max", (double?)77, (DateTime?)bWeek, bFive)),
            "B's own percents and resets");
        Assert.That(budget.Week.Fresh, Is.EqualTo(43_500L), "B's tokens alone");
        Assert.That((budget.WeeklyLimit, budget.WeeklyLedgerPartial), Is.EqualTo(((long?)null, true)),
            "no weekly limit from 43.5k against 77% (it read 'weekly 11.5k', M.ORCH-T485): the percent stands alone");
        Assert.That(budget.BlockLimit, Is.Not.EqualTo(aLearned), "B's 10% isn't counted against A's limit");
        Assert.That(b.Pace!.Stop, Is.False);
        Assert.That(Burndown.Compute(budget, b.History, b.TakenUtc, b.Account).Days.Sum(d => d.Fresh), Is.EqualTo(43_500L), "the dashboard's week is B's");
        Assert.That(asked, Is.EqualTo(1), "B's reading stands: no second ask");
    }

    [Test]
    public void An_idle_tab_restamping_the_reset_windows_reply_doesnt_keep_the_probe_from_reading_and_cant_write_over_its_reading()
    {
        // M.ORCH-T479: Oct 19 06:20-11:20Z went unseen 1h55m while 3.5M were spent. Idle tabs' statuslines restamped
        // claude-usage.json each minute with their last reply, from the window that had reset, so the report never got stale
        // enough to ask; and a reading the probe wrote could be written over before the next refresh read it.
        var at = Seconds(DateTime.UtcNow);
        var reset = new DateTime(at.AddHours(4).Ticks / TimeSpan.TicksPerMinute * TimeSpan.TicksPerMinute, DateTimeKind.Utc);
        var weekReset = new DateTime(at.AddDays(3).Ticks / TimeSpan.TicksPerMinute * TimeSpan.TicksPerMinute, DateTimeKind.Utc);
        var credentials = Path.Combine(dir, ".credentials.json");
        File.WriteAllText(credentials, $$$"""{"claudeAiOauth":{"accessToken":"tok","expiresAt":{{{DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds()}}}}}""");
        var asked = 0;
        var probe = new ClaudeMeterProbe
        {
            UsagePath = Path.Combine(dir, "claude-usage.json"), CredentialsPath = credentials,
            AccountKey = () => ClaudeAccount.Current(Path.Combine(dir, ".claude.json")),
            Fetch = (_, _) =>
            {
                asked++;
                return Task.FromResult<string?>($$$"""{"five_hour":{"utilization":6.0,"resets_at":"{{{reset:O}}}"},"seven_day":{"utilization":60.0,"resets_at":"{{{weekReset:O}}}"}}""");
            },
        };
        Config("""{"oauthAccount":{"accountUuid":"a"}}""");
        SignedInSince("a", TimeSpan.FromHours(6));
        var daemon = Daemon(SignIn(), probe: probe);
        History.AddUsageSamples([new(at.AddMinutes(-35), 1, reset, 27_843)], DateTime.MinValue);
        Transcript("s1", Path.Combine(ws, "A"), TimeSpan.Zero, 500_000);
        Usage(95, at.AddMinutes(-40), 50, weekReset); // stamped just now, from the window that reset 40 minutes ago

        daemon.Refresh();
        probe.Pending.Wait();
        Assert.That(asked, Is.EqualTo(1), "the running window was last read 35 minutes ago while tokens are spent: read it");

        Usage(95, at.AddMinutes(-40), 50, weekReset); // and an idle tab writes over the probe's reading
        var read = daemon.Refresh().Budget!;
        Assert.That((read.WeeklyPercent, read.CurrentBlock!.EndUtc), Is.EqualTo(((double?)60, reset)), "the probe's reading stands");
        Assert.That(History.LoadUsageSamples().Select(s => s.Percent), Is.EqualTo(new[] { 1.0, 6.0 }), "and is sampled");
    }

    [Test]
    public void Right_after_the_week_resets_the_probe_reads_the_new_week_at_once()
    {
        var reset = new DateTime(now.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, DateTimeKind.Utc).AddSeconds(-5);
        var credentials = Path.Combine(dir, ".credentials.json");
        File.WriteAllText(credentials, $$$"""{"claudeAiOauth":{"accessToken":"tok","expiresAt":{{{DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds()}}}}}""");
        var asked = 0;
        var probe = new ClaudeMeterProbe
        {
            UsagePath = Path.Combine(dir, "claude-usage.json"), CredentialsPath = credentials,
            AccountKey = () => ClaudeAccount.Current(Path.Combine(dir, ".claude.json")),
            Fetch = (_, _) =>
            {
                asked++;
                return Task.FromResult<string?>($$$"""{"five_hour":{"utilization":31.0,"resets_at":"{{{reset.AddHours(4):O}}}"},"seven_day":{"utilization":0.0,"resets_at":"{{{reset.AddDays(7):O}}}"}}""");
            },
        };
        Config("""{"oauthAccount":{"accountUuid":"a"}}""");
        var daemon = Daemon(SignIn(), probe: probe);
        // Read three minutes before the 7:00 reset (not stale yet), and a turn lands just after it.
        File.WriteAllText(Path.Combine(dir, "claude-usage.json"), $$$"""
            {"rate_limits":{"five_hour":{"used_percentage":30,"resets_at":{{{new DateTimeOffset(reset.AddHours(4)).ToUnixTimeSeconds()}}}},"seven_day":{"used_percentage":80,"resets_at":{{{new DateTimeOffset(reset).ToUnixTimeSeconds()}}}}},
             "at":"{{{reset.AddMinutes(-3):yyyy-MM-ddTHH:mm:ssZ}}}"}
            """);
        Transcript("s1", Path.Combine(ws, "A"), TimeSpan.Zero, 100);

        Assert.That(daemon.Refresh(reset.AddSeconds(-10)).Budget!.WeeklyPercent, Is.EqualTo(80));
        Assert.That(asked, Is.Zero, "a reading two minutes old isn't stale");

        var after = daemon.Refresh(reset.AddSeconds(10)).Budget!;
        Assert.That((after.WeekStartUtc, after.WeeklyPercent), Is.Not.EqualTo((reset.AddDays(-7), (double?)80)), "the old week's 80% holds nothing");
        probe.Pending.Wait();
        Assert.That(asked, Is.EqualTo(1), "the new week was read at once");
        Assert.That(daemon.Refresh(reset.AddSeconds(20)).Budget!.WeeklyPercent, Is.EqualTo(0), "the new week's own percent");
    }
}
