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
        var end = DateTime.UtcNow - ago;
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

    private StatusAggregator Aggregator() => new(new OrchestratorOptions { WorkspaceRoot = ws, ClaudeProjectsDir = projects, TranscriptCache = null }, store)
    {
        ScanHosts = () => hosts,
        OtherProviders = new ProviderLogReader(Path.Combine(dir, "codex"), Path.Combine(dir, "gemini")),
    };

    [Test]
    public void Sessions_in_a_repo_and_in_its_worktrees_are_credited_to_it_with_its_open_tickets_and_todays_usage()
    {
        var last = Transcript("s1", Path.Combine(ws, "A", "src"), TimeSpan.FromSeconds(30), 10, 20);
        Transcript("s2", Path.Combine(worktrees, "A", "A-T1"), TimeSpan.FromMinutes(10), 5);
        hosts.Add(new HostSession("Claude", 42, "mindattic-host-claude-42", Path.Combine(ws, "B")));

        var snap = Aggregator().Refresh();

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

        Assert.That(store.LoadUsageHistory().Where(d => d.Repo == "A").Sum(d => d.Fresh), Is.GreaterThan(0), "today's usage is saved for the history");
        Assert.That(store.LoadTicketCosts().Single().TicketId, Is.EqualTo("A-T1"), "the worktree's session is charged to its ticket");
    }

    [Test]
    public void Today_is_the_day_of_the_refresh_asked_for_not_the_wall_clocks()
    {
        Transcript("s1", Path.Combine(ws, "A"), TimeSpan.FromMinutes(1), 10);

        Assert.That(Aggregator().Refresh().Repos.Single(r => r.Name == "A").UsageToday.Output, Is.EqualTo(10));
        var twoDaysOn = Aggregator().Refresh(DateTime.UtcNow.AddDays(2)).Repos.Single(r => r.Name == "A");
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
        Assert.That((read.Pace!.SessionCap, read.Budget!.BlockPercent, read.Budget.Level), Is.EqualTo((built.Pace!.SessionCap, built.Budget!.BlockPercent, built.Budget.Level)));

        Assert.That(new SnapshotFile(file.Path).Read(built.TakenUtc + SnapshotFile.MaxAge + TimeSpan.FromSeconds(1)), Is.Null,
            "its writer stopped: build one instead");
    }

    // M.ORCH-T131: the lease's edges, and the 5-minute history save.

    private StatusAggregator Leased(MindAttic.Orchestrator.Scheduling.DispatchLease lease, string rlPath) =>
        new(new OrchestratorOptions { WorkspaceRoot = ws, ClaudeProjectsDir = projects, TranscriptCache = null }, store,
            new RateLimitStatusWriter(rlPath), lease)
        { ScanHosts = () => hosts, OtherProviders = new ProviderLogReader(Path.Combine(dir, "codex"), Path.Combine(dir, "gemini")) };

    private static DateTime Seconds(DateTime t) => new(t.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, DateTimeKind.Utc);

    [Test]
    public void A_store_less_aggregator_sizes_the_5_hour_limit_from_the_same_usage_samples_as_a_store_backed_one()
    {
        // It used to skip usage-samples.json without a store (if (store is null) return null), so a one-off CLI run
        // fell back to a learned or peak limit while the daemon sized it from Claude's own reports (MAO-T121).
        var options = new OrchestratorOptions { WorkspaceRoot = ws, ClaudeProjectsDir = projects, TranscriptCache = null };
        Assert.That(new StatusAggregator(options).Store.Directory, Is.EqualTo(new JsonStore().Directory),
            "no store given: it reads and keeps usage-samples.json where the daemon and dashboard do");

        var reset = Seconds(DateTime.UtcNow.AddHours(3));
        var at = Seconds(DateTime.UtcNow.AddMinutes(-1));
        List<UsageSample> samples = [new(at.AddHours(-1), 10, reset, 100_000), new(at, 30, reset, 300_000)];
        store.Save("usage-samples", samples);
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
        Assert.That(store.Load<List<UsageSample>>("usage-samples"), Has.Count.EqualTo(2), "a report already sampled isn't recorded twice");
    }

    [Test]
    public void A_locked_usage_samples_file_does_not_fail_the_refresh()
    {
        var options = new OrchestratorOptions { WorkspaceRoot = ws, ClaudeProjectsDir = projects, TranscriptCache = null };
        var reset = Seconds(DateTime.UtcNow.AddHours(3));
        var at = Seconds(DateTime.UtcNow.AddMinutes(-1));
        store.Save("usage-samples", new List<UsageSample> { new(at.AddHours(-1), 10, reset, 100_000) });
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

        // Another process holds it (an antivirus scan, the dashboard mid-save) past the store's retries.
        using (new FileStream(Path.Combine(store.Directory, "usage-samples.json"), FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.That(aggregator.Refresh().Budget, Is.Not.Null, "the new report is kept in memory, the refresh goes on");
        Assert.That(store.Load<List<UsageSample>>("usage-samples"), Has.Count.EqualTo(1), "not saved while locked");
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
        store.Save("usage-samples", new List<UsageSample> { new(at.AddHours(-1), 60, reset, 100_000), new(at.AddMinutes(-1), 80, reset, 200_000) });
        Report(reportPath, 82, reset, at);
        var before = aggregator.Refresh();
        Assert.That((before.Budget!.BlockLimitSource, before.Pace!.IsPaced), Is.EqualTo((LimitSource.Reported, true)));

        // Another process's save caught mid-write (or held past the store's retries), and the idle tab's minute comes round.
        var samplesPath = Path.Combine(store.Directory, "usage-samples.json");
        File.WriteAllText(samplesPath, "[ {");
        Report(reportPath, 96, Seconds(DateTime.UtcNow.AddHours(-4.5)), Seconds(DateTime.UtcNow));
        var tick = aggregator.Refresh();

        Assert.That((tick.Budget!.BlockLimitIsPeak, tick.Budget.BlockLimitSource, tick.Budget.BlockLimit),
            Is.EqualTo((false, LimitSource.Reported, before.Budget.BlockLimit)), "the running window's limit, not none");
        Assert.That(tick.Budget.CurrentBlock!.EndUtc, Is.EqualTo(reset), "counted against the window still running");
        Assert.That(tick.Pace!.IsPaced, Is.True, "not 'unpaced: nothing to pace against'");
        using (var rl = System.Text.Json.JsonDocument.Parse(File.ReadAllText(rlPath)))
            Assert.That(rl.RootElement.TryGetProperty("percent_used", out _) && rl.RootElement.TryGetProperty("pace", out _), Is.True,
                "rl-status.json still carries the percent and the pace for the gate");
        Assert.That(File.ReadAllText(samplesPath), Is.EqualTo("[ {"), "the unreadable file isn't saved over with what this tick saw");
    }

    [Test]
    public void A_report_with_no_running_5_hour_window_keeps_the_last_reports_window_while_it_runs()
    {
        var (aggregator, reportPath, _) = PacedOnReports();
        var reset = Seconds(DateTime.UtcNow.AddMinutes(33));
        Report(reportPath, 30, reset, Seconds(DateTime.UtcNow.AddMinutes(-1)));
        var before = aggregator.Refresh().Budget!;
        Assert.That(before.BlockLimitSource, Is.EqualTo(LimitSource.Reported));

        // No sample of the running window to fall back on (the week's file is gone), and the idle tab's stale report.
        File.Delete(Path.Combine(store.Directory, "usage-samples.json"));
        Report(reportPath, 96, Seconds(DateTime.UtcNow.AddHours(-4.5)), Seconds(DateTime.UtcNow));
        var tick = aggregator.Refresh();

        Assert.That((tick.Budget!.BlockLimitIsPeak, tick.Budget.BlockLimitSource, tick.Budget.CurrentBlock!.EndUtc),
            Is.EqualTo((false, LimitSource.Reported, reset)), "the last report's window is kept until it resets");
        Assert.That(tick.Pace!.IsPaced, Is.True);
    }

    [Test]
    public void A_usage_samples_file_unreadable_on_the_first_refresh_is_read_once_it_can_be_not_saved_over()
    {
        var (aggregator, reportPath, _) = PacedOnReports();
        var reset = Seconds(DateTime.UtcNow.AddMinutes(33));
        var at = Seconds(DateTime.UtcNow.AddMinutes(-1));
        store.Save("usage-samples", new List<UsageSample> { new(at.AddHours(-1), 10, reset, 100_000) });
        Report(reportPath, 30, reset, at);
        var samplesPath = Path.Combine(store.Directory, "usage-samples.json");

        using (new FileStream(samplesPath, FileMode.Open, FileAccess.Read, FileShare.None))
            aggregator.Refresh();
        aggregator.Refresh();

        Assert.That(store.Load<List<UsageSample>>("usage-samples"), Has.Count.EqualTo(2),
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
        long Saved(string repo) => store.LoadUsageHistory().Where(d => d.Repo == repo).Sum(d => d.Fresh);
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
}
