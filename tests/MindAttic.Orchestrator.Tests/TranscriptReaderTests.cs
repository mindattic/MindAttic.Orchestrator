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
}
