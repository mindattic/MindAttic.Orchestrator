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
        // flapping the pace gate's hold and the "RATE LIMIT" banner between whichever wrote last (MAO-T112).
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
}
