using MindAttic.Orchestrator.Sessions;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The marks the context gate keeps and ContextCycler reads: the /autopilot order, the keep-going log,
/// /crossrepo and /unthrottle marks, and subagent marks (M.ORCH-T138).</summary>
public sealed class ContextCyclerMarksTests
{
    private string dir = "";

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "ctxmarks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
    }

    /// <summary>A process id that is certainly not a running Claude CLI: this test process.</summary>
    private static int NotClaude => Environment.ProcessId;

    // ---- AutopilotOrder ----

    [Test]
    public void Autopilot_order_is_null_while_the_file_is_missing()
    {
        Assert.That(new ContextCycler(dir).AutopilotOrder(), Is.Null, "no file: /autopilot is off");
        Assert.That(new ContextCycler(Path.Combine(dir, "nowhere")).AutopilotOrder(), Is.Null, "no folder at all: off too");
    }

    [Test]
    public void An_empty_autopilot_order_means_every_cli()
    {
        File.WriteAllText(Path.Combine(dir, ContextCycler.AutopilotFile), "");
        Assert.That(new ContextCycler(dir).AutopilotOrder(), Is.Not.Null.And.Empty);

        File.WriteAllText(Path.Combine(dir, ContextCycler.AutopilotFile), " \r\n , \n");
        Assert.That(new ContextCycler(dir).AutopilotOrder(), Is.Not.Null.And.Empty, "only separators and blanks: still every CLI");
    }

    [Test]
    public void The_autopilot_order_splits_on_commas_and_newlines_and_keeps_wildcards()
    {
        File.WriteAllText(Path.Combine(dir, ContextCycler.AutopilotFile), "GTFO.* , MindAttic.Orchestrator\r\nSoundBoard\n*\n,,Other ");

        Assert.That(new ContextCycler(dir).AutopilotOrder(),
            Is.EqualTo(new[] { "GTFO.*", "MindAttic.Orchestrator", "SoundBoard", "*", "Other" }));
    }

    // ---- KeepGoingSince ----

    private static string Line(string action, string repo, string why, int open, string? provider = null) =>
        System.Text.Json.JsonSerializer.Serialize(provider is null
            ? (object)new { action, repo, why, open }
            : new { action, repo, why, open, provider }) + "\n";

    [Test]
    public void The_first_keep_going_read_only_remembers_where_the_log_ends()
    {
        var cycler = new ContextCycler(dir);
        File.WriteAllText(cycler.KeepGoingLog, Line("carry on", "A", "stopped early", 3));

        Assert.That(cycler.KeepGoingSince(), Is.Empty, "a restart doesn't repeat what was logged before it");
        Assert.That(cycler.KeepGoingSince(), Is.Empty);

        File.AppendAllText(cycler.KeepGoingLog, Line("carry on", "B", "stopped early", 2));
        Assert.That(cycler.KeepGoingSince(), Is.EqualTo(new[]
            { "keep-going: told the CLI in B to carry on (stopped early, 2 tickets still open)" }));
        Assert.That(cycler.KeepGoingSince(), Is.Empty, "each line is told once");
    }

    [Test]
    public void Each_keep_going_line_is_reported_once_in_its_own_form()
    {
        var cycler = new ContextCycler(dir);
        Assert.That(cycler.KeepGoingSince(), Is.Empty, "no log yet");

        // The log appears after the first read: everything in it is new.
        File.WriteAllText(cycler.KeepGoingLog,
            Line("carry on", "A", "stopped early", 3) +
            "not json\n" +
            Line("let stop", "B", "stopped twice", 1) +
            Line("carry on", "C", "stopped early", 4, provider: "Codex"));

        Assert.That(cycler.KeepGoingSince(), Is.EqualTo(new[]
        {
            "keep-going: told the CLI in A to carry on (stopped early, 3 tickets still open)",
            "keep-going: let the CLI in B stop (stopped twice, 1 open); it stopped again without doing anything, so it likely needs you",
            "keep-going: told the Codex CLI in C to carry on (stopped early, 4 tickets still open)",
        }), "a line that isn't JSON is skipped, and a non-Claude CLI is named");
        Assert.That(cycler.KeepGoingSince(), Is.Empty);
    }

    [Test]
    public void A_keep_going_line_still_being_written_waits_for_the_next_read()
    {
        var cycler = new ContextCycler(dir);
        cycler.KeepGoingSince();
        var whole = Line("let stop", "Ä-repo", "stopped twice", 0);
        File.WriteAllText(cycler.KeepGoingLog, whole[..10]);

        Assert.That(cycler.KeepGoingSince(), Is.Empty, "no newline yet");
        File.AppendAllText(cycler.KeepGoingLog, whole[10..]);
        Assert.That(cycler.KeepGoingSince(), Has.Count.EqualTo(1).And.Some.Contains("let the CLI in Ä-repo stop"));
        Assert.That(cycler.KeepGoingSince(), Is.Empty);
    }

    [Test]
    public void A_keep_going_log_that_shrank_starts_over_from_its_end()
    {
        var cycler = new ContextCycler(dir);
        File.WriteAllText(cycler.KeepGoingLog, Line("carry on", "A", "x", 1) + Line("carry on", "B", "x", 1));
        cycler.KeepGoingSince();

        File.WriteAllText(cycler.KeepGoingLog, Line("carry on", "C", "x", 1)); // rotated: shorter than where it read to
        Assert.That(cycler.KeepGoingSince(), Is.Empty, "it can't tell what's new, so it remembers the end again");
        File.AppendAllText(cycler.KeepGoingLog, Line("carry on", "D", "x", 1));
        Assert.That(cycler.KeepGoingSince(), Has.Count.EqualTo(1).And.Some.Contains(" D "));
    }

    // ---- PruneMarks ----

    [Test]
    public void Prune_marks_drops_crossrepo_unthrottled_and_autopilot_marks_of_clis_that_have_closed()
    {
        var keep = new[] { "abc.json", "abc.state.json", ContextCycler.AutopilotFile, "keep-going.jsonl" };
        var drop = new[]
        {
            $"crossrepo-{NotClaude}", $"unthrottled-{NotClaude}", $"autopilot-{NotClaude}",
            "crossrepo-0", "unthrottled-notapid", "autopilot-",
        };
        foreach (var f in keep.Concat(drop)) File.WriteAllText(Path.Combine(dir, f), "x");

        new ContextCycler(dir).PruneMarks();

        Assert.That(Directory.GetFiles(dir).Select(Path.GetFileName).Order(), Is.EqualTo(keep.Order()),
            "a reused process id never inherits a mark, and nothing else in the folder is touched");
    }

    [Test]
    public void Prune_marks_drops_subagent_marks_past_six_hours_or_whose_cli_has_closed()
    {
        var cycler = new ContextCycler(dir) { ProjectsDir = Path.Combine(dir, "projects") };
        Directory.CreateDirectory(cycler.SubagentDirectory);
        var now = DateTime.UtcNow;
        void Mark(string id, string body, TimeSpan age)
        {
            var f = Path.Combine(cycler.SubagentDirectory, id);
            File.WriteAllText(f, body);
            File.SetLastWriteTimeUtc(f, now - age);
        }
        Mark("young", "", TimeSpan.FromHours(5));
        Mark("old", "", ContextCycler.SubagentMarkLife + TimeSpan.FromMinutes(5));
        Mark("closed-cli", NotClaude.ToString(), TimeSpan.FromMinutes(1));

        cycler.PruneMarks();

        Assert.That(Directory.GetFiles(cycler.SubagentDirectory).Select(Path.GetFileName), Is.EqualTo(new[] { "young" }),
            "a mark with no process id is trusted until it's 6 hours old");
    }

    [Test]
    public void Week_old_prune_keeps_a_running_clis_own_marks_and_drops_stale_session_files()
    {
        var keep = new[] { "autopilot-123", "unthrottled-123", "crossrepo-123", ContextCycler.AutopilotFile };
        var drop = new[] { "old.json", "old.state.json" };
        var now = DateTime.UtcNow;
        foreach (var f in keep.Concat(drop))
        {
            File.WriteAllText(Path.Combine(dir, f), "x");
            File.SetLastWriteTimeUtc(Path.Combine(dir, f), now - TimeSpan.FromDays(8));
        }

        new ContextCycler(dir).Prune(now);

        Assert.That(Directory.GetFiles(dir).Select(Path.GetFileName).Order(), Is.EqualTo(keep.Order()),
            "a CLI that typed /autopilot a week ago and is still running keeps it; PruneMarks drops it when the CLI closes");
    }

    [Test]
    public void A_statusline_file_mid_write_does_not_drop_the_sessions_state()
    {
        File.WriteAllText(Path.Combine(dir, "abc.json"), """{"used_perc""");
        File.WriteAllText(Path.Combine(dir, "abc.state.json"), """{"state":"busy","pid":42,"cwd":"D:\\x"}""");

        var s = new ContextCycler(dir).Load().Single();

        Assert.That((s.SessionId, s.Pid, s.Idle, s.Cwd, s.Percent), Is.EqualTo(("abc", (int?)42, false, @"D:\x", 0d)));
    }

    [Test]
    public void A_statusline_file_held_open_for_writing_is_still_read()
    {
        var path = Path.Combine(dir, "abc.json");
        File.WriteAllText(path, """{"used_percentage":37,"cwd":"D:\\x"}""");
        using var writer = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

        Assert.That(new ContextCycler(dir).Load().Single().Percent, Is.EqualTo(37));
    }

    [Test]
    public void A_cli_that_ran_clear_is_in_its_newest_session()
    {
        // "zzz" was before the /clear, "aaa" is now: by file name the old one comes first.
        File.WriteAllText(Path.Combine(dir, "zzz.state.json"), """{"state":"idle","pid":42,"at":"2026-10-17T10:00:00Z"}""");
        File.WriteAllText(Path.Combine(dir, "aaa.state.json"), """{"state":"busy","pid":42,"at":"2026-10-17T12:00:00Z"}""");
        File.WriteAllText(Path.Combine(dir, "bbb.state.json"), """{"state":"busy","pid":7,"at":"2026-10-17T13:00:00Z"}""");
        var cycler = new ContextCycler(dir);
        Assert.That((cycler.LatestSessionOf(42), cycler.LatestSessionOf(99)), Is.EqualTo(("aaa", (string?)null)));
    }

    [Test]
    public void Prune_marks_without_a_folder_does_nothing()
    {
        Assert.DoesNotThrow(() => new ContextCycler(Path.Combine(dir, "nowhere")).PruneMarks());
    }

    // ---- SubagentAlive's 20-minute idle rule ----

    [Test]
    public void A_subagent_counts_until_its_transcript_has_been_quiet_for_twenty_minutes()
    {
        var projects = Path.Combine(dir, "projects");
        var cycler = new ContextCycler(dir) { ProjectsDir = projects };
        Directory.CreateDirectory(cycler.SubagentDirectory);
        var folder = Path.Combine(projects, "proj", "parent", "subagents");
        Directory.CreateDirectory(folder);
        var now = DateTime.UtcNow;
        void Agent(string id, TimeSpan quiet, TimeSpan markAge, string body = "")
        {
            var mark = Path.Combine(cycler.SubagentDirectory, id);
            File.WriteAllText(mark, body);
            File.SetLastWriteTimeUtc(mark, now - markAge);
            var transcript = Path.Combine(folder, $"agent-{id}.jsonl");
            File.WriteAllText(transcript, "{}\n");
            File.SetLastWriteTimeUtc(transcript, now - quiet);
        }
        Agent("nineteen", TimeSpan.FromMinutes(19), TimeSpan.FromHours(1));
        Agent("twentyone", TimeSpan.FromMinutes(21), TimeSpan.FromMinutes(21));
        Agent("fresh-but-old-mark", TimeSpan.Zero, ContextCycler.SubagentMarkLife + TimeSpan.FromMinutes(1));
        Agent("fresh-but-closed-cli", TimeSpan.Zero, TimeSpan.Zero, NotClaude.ToString());

        Assert.That(ContextCycler.SubagentIdleLife, Is.EqualTo(TimeSpan.FromMinutes(20)));
        Assert.That(cycler.AliveSubagentIds(now), Is.EqualTo(new[] { "nineteen" }),
            "quiet past 20 minutes has stopped; a fresh transcript doesn't save a 6-hour-old mark or one whose CLI closed");
        Assert.That(cycler.Subagents(), Is.EqualTo(1));

        cycler.PruneMarks();
        Assert.That(Directory.GetFiles(cycler.SubagentDirectory).Select(Path.GetFileName).Order(), Is.EqualTo(new[] { "nineteen", "twentyone" }),
            "the idle rule only stops it counting; pruning keeps the quiet one's mark");

        File.SetLastWriteTimeUtc(Path.Combine(folder, "agent-twentyone.jsonl"), now);
        Assert.That(cycler.AliveSubagentIds(now).Order(), Is.EqualTo(new[] { "nineteen", "twentyone" }), "it counts again the moment it writes");
    }
}
