using MindAttic.Orchestrator.Activity;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class TranscriptReaderTests
{
    private string dir = "";

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "orch-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "D--Projects-X"));
    }

    [TearDown]
    public void TearDown() => Directory.Delete(dir, recursive: true);

    private static readonly DateTime Since = new(2026, 10, 13, 5, 0, 0, DateTimeKind.Utc);

    private static string Line(string id, string ts, int input, int output, string cwd = @"D:\Projects\X") =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "assistant", timestamp = ts, sessionId = "s1", cwd,
            message = new
            {
                id, model = "claude-sonnet-5",
                usage = new { input_tokens = input, cache_creation_input_tokens = 10, cache_read_input_tokens = 100, output_tokens = output },
            },
        });

    private string File(string name = "s1.jsonl") => Path.Combine(dir, "D--Projects-X", name);

    // The shape Claude Code writes when it refuses a turn at the limit (trimmed from a real transcript).
    private static string LimitLine(string ts, string text) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "assistant", timestamp = ts, sessionId = "s1", cwd = @"D:\Projects\X", error = "rate_limit", isApiErrorMessage = true,
            message = new
            {
                id = Guid.NewGuid().ToString(), model = "<synthetic>", role = "assistant",
                usage = new { input_tokens = 0, cache_creation_input_tokens = 0, cache_read_input_tokens = 0, output_tokens = 0 },
                content = new[] { new { type = "text", text } },
            },
        });

    private string Cache => Path.Combine(dir, "cache", "transcripts.json");

    // Overwrites the bytes already read with spaces: only a reader that starts over would notice.
    private void BlankReadBytes() =>
        System.IO.File.WriteAllText(File(), new string(' ', (int)new FileInfo(File()).Length - 1) + "\n");

    private static string ToolLine(string id, string ts) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "assistant", timestamp = ts, sessionId = "s1", cwd = @"D:\Projects\X",
            message = new
            {
                id, model = "claude-sonnet-5",
                usage = new { input_tokens = 1, cache_creation_input_tokens = 0, cache_read_input_tokens = 0, output_tokens = 1 },
                content = new[] { new { type = "tool_use", name = "Bash", input = new { command = "ls" } } },
            },
        });

    [Test]
    public void A_transcript_read_again_from_the_start_doesnt_count_its_tool_calls_twice()
    {
        // Three identical calls read twice became six, past the loop threshold: a false "looks stuck".
        string[] calls = [ToolLine("m1", "2026-10-13T06:00:00Z"), ToolLine("m2", "2026-10-13T06:01:00Z"), ToolLine("m3", "2026-10-13T06:02:00Z")];
        System.IO.File.WriteAllLines(File(), [.. calls, Line("pad", "2026-10-13T06:03:00Z", 1, 1)]);
        var reader = new TranscriptReader(dir);
        Assert.That(reader.Poll(Since).Single().Loop, Is.Null);

        System.IO.File.WriteAllLines(File(), calls); // rewritten shorter: read again from 0
        Assert.That(reader.Poll(Since).Single().Loop, Is.Null);
    }

    [Test]
    public void A_restarted_reader_picks_up_where_the_last_one_stopped()
    {
        System.IO.File.WriteAllLines(File(), [Line("m1", "2026-10-13T12:00:00Z", 1, 10), Line("m2", "2026-10-13T12:01:00Z", 1, 20)]);
        System.IO.File.SetLastWriteTimeUtc(File(), new DateTime(2026, 10, 13, 12, 1, 0, DateTimeKind.Utc));
        new TranscriptReader(dir, Cache).Poll(Since);
        BlankReadBytes();
        System.IO.File.AppendAllLines(File(), [Line("m3", "2026-10-13T12:02:00Z", 1, 40)]);
        var mtime = new DateTime(2026, 10, 13, 12, 2, 0, DateTimeKind.Utc);
        System.IO.File.SetLastWriteTimeUtc(File(), mtime);

        var s = new TranscriptReader(dir, Cache).Poll(Since).Single();

        Assert.That(s.UsageSince(Since).Output, Is.EqualTo(70), "m1 and m2 from the cache, m3 read fresh");
        Assert.That(s.Cwd, Is.EqualTo(@"D:\Projects\X"));
        Assert.That(s.LastWriteUtc, Is.EqualTo(mtime), "growth while nobody watched isn't activity right now");
    }

    [Test]
    public void A_later_window_trims_the_cache_and_an_earlier_one_reads_from_scratch()
    {
        System.IO.File.WriteAllLines(File(), [Line("m1", "2026-10-13T12:00:00Z", 1, 10), Line("m2", "2026-10-14T12:00:00Z", 1, 20)]);
        new TranscriptReader(dir, Cache).Poll(Since);
        BlankReadBytes();

        var later = new TranscriptReader(dir, Cache).Poll(Since.AddDays(1)).Single();
        var earlier = new TranscriptReader(dir, Cache).Poll(Since.AddDays(-1));

        Assert.That(later.UsageSince(DateTime.MinValue).Output, Is.EqualTo(20), "m1 fell out of the window; m2 kept without rereading");
        Assert.That(earlier, Is.Empty, "a window the cache doesn't cover is read from the file, which is blank now");
    }

    [Test]
    public void A_limit_message_is_recorded_as_a_hit_not_a_response()
    {
        System.IO.File.WriteAllLines(File(), [
            Line("m1", "2026-10-13T20:00:00Z", 5, 5),
            LimitLine("2026-10-13T21:22:25Z", "You've hit your session limit · resets 6:40pm (America/Chicago)"),
        ]);

        var s = new TranscriptReader(dir).Poll(Since).Single();

        Assert.That(s.Events, Has.Count.EqualTo(1), "the zero-usage synthetic message isn't a response");
        Assert.That(s.Hits.Single(), Is.EqualTo(new LimitHit(
            new DateTime(2026, 10, 13, 21, 22, 25, DateTimeKind.Utc), LimitKind.Session, new DateTime(2026, 10, 13, 23, 40, 0, DateTimeKind.Utc))));
    }

    [Test]
    public void Counts_each_message_once_even_when_split_across_lines()
    {
        System.IO.File.WriteAllLines(File(), [
            Line("m1", "2026-10-13T12:00:00Z", 2, 30),
            Line("m1", "2026-10-13T12:00:00Z", 2, 30),
            Line("m2", "2026-10-13T12:01:00Z", 3, 40),
        ]);

        var s = new TranscriptReader(dir).Poll(Since).Single();

        Assert.That(s.UsageSince(Since), Is.EqualTo(new TokenUsage(5, 20, 200, 70)));
        Assert.That(s.Cwd, Is.EqualTo(@"D:\Projects\X"));
        Assert.That(s.Model, Is.EqualTo("claude-sonnet-5"));
    }

    [Test]
    public void Ignores_messages_before_the_boundary()
    {
        System.IO.File.WriteAllLines(File(), [
            Line("m1", "2026-10-12T23:00:00Z", 1000, 1000),
            Line("m2", "2026-10-13T12:00:00Z", 1, 1),
        ]);

        var s = new TranscriptReader(dir).Poll(Since).Single();

        Assert.That(s.UsageSince(Since).Output, Is.EqualTo(1));
    }

    [Test]
    public void Reads_only_appended_lines_and_waits_for_a_partial_line_to_finish()
    {
        var reader = new TranscriptReader(dir);
        System.IO.File.WriteAllText(File(), Line("m1", "2026-10-13T12:00:00Z", 1, 1) + "\n");
        Assert.That(reader.Poll(Since).Single().UsageSince(Since).Output, Is.EqualTo(1));

        var next = Line("m2", "2026-10-13T12:05:00Z", 1, 5);
        System.IO.File.AppendAllText(File(), next[..20]);
        TouchLater(File());
        Assert.That(reader.Poll(Since).Single().UsageSince(Since).Output, Is.EqualTo(1), "half a line isn't counted yet");

        System.IO.File.AppendAllText(File(), next[20..] + "\n");
        TouchLater(File());
        Assert.That(reader.Poll(Since).Single().UsageSince(Since).Output, Is.EqualTo(6));
    }

    [Test]
    public void Merges_subagent_transcripts_into_their_session()
    {
        System.IO.File.WriteAllLines(File(), [Line("m1", "2026-10-13T12:00:00Z", 1, 1)]);
        var sub = Path.Combine(dir, "D--Projects-X", "s1", "subagents");
        Directory.CreateDirectory(sub);
        System.IO.File.WriteAllLines(Path.Combine(sub, "agent-a.jsonl"), [Line("m9", "2026-10-13T12:00:00Z", 1, 9)]);

        var s = new TranscriptReader(dir).Poll(Since).Single();

        Assert.That(s.UsageSince(Since).Output, Is.EqualTo(10));
    }

    [Test]
    public void A_subagents_ticket_is_its_own_not_its_parent_sessions()
    {
        // /autopilot hands each subagent "Work on ticket X": the parent CLI works many, so it isn't stamped with the first.
        System.IO.File.WriteAllLines(File(), [Line("m1", "2026-10-13T12:00:00Z", 1, 1)]);
        var sub = Path.Combine(dir, "D--Projects-X", "s1", "subagents");
        Directory.CreateDirectory(sub);
        System.IO.File.WriteAllLines(Path.Combine(sub, "agent-a.jsonl"), [
            System.Text.Json.JsonSerializer.Serialize(new { type = "user", timestamp = "2026-10-13T12:00:00Z", sessionId = "s1", cwd = @"D:\Projects\X",
                message = new { role = "user", content = "Work on ticket X-T7 (fix): the thing." } }),
            Line("m9", "2026-10-13T12:00:01Z", 1, 9)]);

        var s = new TranscriptReader(dir).Poll(Since).Single();

        Assert.That((s.UsageSince(Since).Output, s.TicketId), Is.EqualTo((10, (string?)null)), "its usage is the session's, its ticket isn't");
        Assert.That(TranscriptReader.IsSubagentTranscript(Path.Combine(sub, "agent-a.jsonl")), Is.True);
        Assert.That(TranscriptReader.IsSubagentTranscript(File()), Is.False);
    }

    [Test]
    public void Old_transcripts_still_report_whose_they_are()
    {
        System.IO.File.WriteAllLines(File(), [Line("m1", "2026-09-01T12:00:00Z", 1, 1)]);
        System.IO.File.SetLastWriteTimeUtc(File(), new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc));

        var s = new TranscriptReader(dir).Poll(Since).Single();

        Assert.That(s.Cwd, Is.EqualTo(@"D:\Projects\X"));
        Assert.That(s.UsageSince(Since).Total, Is.Zero);
    }

    [Test]
    public void First_read_keeps_the_files_own_timestamp_but_later_growth_counts_as_now()
    {
        var written = new DateTime(2026, 10, 13, 6, 0, 0, DateTimeKind.Utc);
        System.IO.File.WriteAllLines(File(), [Line("m1", "2026-10-13T06:00:00Z", 1, 1)]);
        System.IO.File.SetLastWriteTimeUtc(File(), written);
        var reader = new TranscriptReader(dir);

        Assert.That(reader.Poll(Since).Single().LastWriteUtc, Is.EqualTo(written), "startup must not make every session look live");

        System.IO.File.AppendAllLines(File(), [Line("m2", "2026-10-13T06:01:00Z", 1, 1)]);
        System.IO.File.SetLastWriteTimeUtc(File(), written); // a stale directory timestamp, as with an open handle
        Assert.That(reader.Poll(Since).Single().LastWriteUtc, Is.GreaterThan(DateTime.UtcNow.AddMinutes(-1)));
    }

    // Filesystem timestamps can be coarse; make sure the reader sees a change.
    private static void TouchLater(string path) =>
        System.IO.File.SetLastWriteTimeUtc(path, System.IO.File.GetLastWriteTimeUtc(path).AddSeconds(1));

    [Test]
    public void A_quiet_transcript_whose_directory_entry_hasnt_moved_isnt_opened_again()
    {
        // Every poll opened all ~900 transcripts of the week to check their length (M.ORCH-T116).
        var hour = DateTime.UtcNow.AddHours(-1);
        System.IO.File.WriteAllLines(File("quiet.jsonl"), [Line("q1", "2026-10-13T12:00:00Z", 1, 10)]);
        System.IO.File.SetLastWriteTimeUtc(File("quiet.jsonl"), hour);
        System.IO.File.WriteAllLines(File("hot.jsonl"), [Line("h1", "2026-10-13T12:00:00Z", 1, 5)]);
        var reader = new TranscriptReader(dir);

        var first = reader.Poll(Since);
        Assert.That(reader.LastPollOpened, Is.EqualTo(2));

        var again = reader.Poll(Since);
        Assert.That(reader.LastPollOpened, Is.EqualTo(1), "only the one written in the last few minutes");
        Assert.That(again, Is.SameAs(first), "nothing new: the same sessions, not rebuilt");
        var version = reader.Version;

        System.IO.File.AppendAllLines(File("quiet.jsonl"), [Line("q2", "2026-10-13T12:05:00Z", 1, 30)]);
        System.IO.File.SetLastWriteTimeUtc(File("quiet.jsonl"), hour); // even with its timestamp put back, its length moved
        var grown = reader.Poll(Since);
        Assert.That(reader.LastPollOpened, Is.EqualTo(2));
        Assert.That(reader.Version, Is.GreaterThan(version));
        Assert.That(grown.Single().UsageSince(Since).Output, Is.EqualTo(45));
    }
}
