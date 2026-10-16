using MindAttic.Orchestrator.Activity;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>TranscriptReader's skips (FullCheckEvery, HotFor, restored files), its cache saves and its Version (M.ORCH-T142).</summary>
public sealed class TranscriptReaderSkipsTests
{
    private static readonly DateTime Since = new(2026, 10, 13, 5, 0, 0, DateTimeKind.Utc);

    private string dir = "";
    private DateTime clock;

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "orch-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "D--Projects-X"));
        clock = DateTime.UtcNow;
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    private string Transcript(string name = "s1.jsonl") => Path.Combine(dir, "D--Projects-X", name);

    private string Cache => Path.Combine(dir, "cache", "transcripts.json");

    private TranscriptReader Reader(string? cache = null, string? projects = null) =>
        new(projects ?? dir, cache) { UtcNow = () => clock };

    private static string Line(string id, string ts, int output, bool tool = false) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "assistant", timestamp = ts, sessionId = "s1", cwd = @"D:\Projects\X",
            message = new
            {
                id, model = "claude-sonnet-5",
                usage = new { input_tokens = 1, cache_creation_input_tokens = 0, cache_read_input_tokens = 0, output_tokens = output },
                content = tool ? new object[] { new { type = "tool_use", name = "Bash", input = new { command = "ls" } } } : [],
            },
        });

    [Test]
    public void FullCheckEvery_reopens_growth_hidden_behind_an_unchanged_directory_entry()
    {
        // The second line is complete but for its newline; the byte that finishes it replaces a space, so the
        // file's length and (put back) timestamp don't move: only opening it finds the new line.
        var quiet = clock.AddHours(-1);
        System.IO.File.WriteAllText(Transcript(), Line("m1", "2026-10-13T12:00:00Z", 10) + "\n" + Line("m2", "2026-10-13T12:01:00Z", 20) + " ");
        System.IO.File.SetLastWriteTimeUtc(Transcript(), quiet);
        var reader = Reader();
        Assert.That(reader.Poll(Since).Single().UsageSince(Since).Output, Is.EqualTo(10), "the unfinished line waits");

        var bytes = System.IO.File.ReadAllBytes(Transcript());
        bytes[^1] = (byte)'\n';
        System.IO.File.WriteAllBytes(Transcript(), bytes);
        System.IO.File.SetLastWriteTimeUtc(Transcript(), quiet);
        var length = new FileInfo(Transcript()).Length;

        clock = clock.AddMinutes(1);
        var skipped = reader.Poll(Since);
        Assert.That(reader.LastPollOpened, Is.Zero, "quiet, and its entry hasn't moved");
        Assert.That(skipped.Single().UsageSince(Since).Output, Is.EqualTo(10));

        clock = clock.AddMinutes(3).AddSeconds(59);
        reader.Poll(Since);
        Assert.That(reader.LastPollOpened, Is.Zero, "just short of FullCheckEvery since the last full check");

        clock = clock.AddSeconds(1);
        var version = reader.Version;
        var checkedAll = reader.Poll(Since);
        Assert.That(new FileInfo(Transcript()).Length, Is.EqualTo(length), "the entry really is unchanged");
        Assert.That(reader.LastPollOpened, Is.EqualTo(1), "FullCheckEvery opens it anyway");
        Assert.That(checkedAll.Single().UsageSince(Since).Output, Is.EqualTo(30));
        Assert.That(reader.Version, Is.EqualTo(version + 1));

        clock = clock.AddMinutes(1);
        reader.Poll(Since);
        Assert.That(reader.LastPollOpened, Is.EqualTo(1), "growth it found counts as written just now, so it's hot again");
    }

    [Test]
    public void A_file_restored_from_the_cache_is_opened_on_the_first_poll()
    {
        var quiet = clock.AddHours(-1);
        System.IO.File.WriteAllLines(Transcript(), [Line("m1", "2026-10-13T12:00:00Z", 10)]);
        System.IO.File.SetLastWriteTimeUtc(Transcript(), quiet);
        Reader(Cache).Poll(Since);
        Assert.That(System.IO.File.Exists(Cache), Is.True);

        // Written while nobody watched, with its timestamp put back.
        System.IO.File.AppendAllLines(Transcript(), [Line("m2", "2026-10-13T12:01:00Z", 20)]);
        System.IO.File.SetLastWriteTimeUtc(Transcript(), quiet);

        var reader = Reader(Cache);
        var s = reader.Poll(Since).Single();
        Assert.That(reader.LastPollOpened, Is.EqualTo(1), "restored, so opened whatever its entry says");
        Assert.That(s.UsageSince(Since).Output, Is.EqualTo(30), "m1 from the cache, m2 read now, each once");
        Assert.That(s.Events, Has.Count.EqualTo(2));
        Assert.That(s.LastWriteUtc, Is.EqualTo(quiet), "growth found on restore isn't activity right now");

        clock = clock.AddMinutes(1);
        reader.Poll(Since);
        Assert.That(reader.LastPollOpened, Is.Zero, "once read it's no longer restored: quiet and unchanged is skipped");
    }

    [Test]
    public void An_out_of_window_file_read_again_from_0_when_resumed_counts_each_message_once()
    {
        System.IO.File.WriteAllLines(Transcript(), [
            Line("m1", "2026-10-13T12:00:00Z", 1, tool: true),
            Line("m2", "2026-10-14T12:00:00Z", 10, tool: true),
        ]);
        System.IO.File.SetLastWriteTimeUtc(Transcript(), new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc));
        var reader = Reader();
        Assert.That(reader.Poll(Since).Single().UsageSince(Since).Output, Is.EqualTo(11));

        // The window moves past the file's timestamp: it falls out, and is to be read from 0 when it's resumed.
        System.IO.File.SetLastWriteTimeUtc(Transcript(), new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc));
        var later = new DateTime(2026, 10, 14, 6, 0, 0, DateTimeKind.Utc);
        Assert.That(reader.Poll(later).Single().UsageSince(later).Output, Is.EqualTo(10), "m1 trimmed, m2 kept");

        System.IO.File.AppendAllLines(Transcript(), [Line("m3", "2026-10-14T13:00:00Z", 100, tool: true)]);
        var s = reader.Poll(later).Single();

        Assert.That(s.UsageSince(later).Output, Is.EqualTo(110), "m2 isn't counted again on the reread, m3 is new");
        Assert.That(s.Events.Select(e => e.Usage.Output), Is.EqualTo(new long[] { 10, 100 }));
        Assert.That(s.Loop, Is.Null, "the reread's tool calls replace the old ones rather than doubling them");
    }

    [Test]
    public void The_cache_is_saved_at_most_once_a_minute()
    {
        System.IO.File.WriteAllLines(Transcript(), [Line("m1", "2026-10-13T12:00:00Z", 10)]);
        var reader = Reader(Cache);
        reader.Poll(Since);
        Assert.That(System.IO.File.Exists(Cache), Is.True, "the first poll that reads anything saves");
        System.IO.File.Delete(Cache);

        clock = clock.AddSeconds(30);
        System.IO.File.AppendAllLines(Transcript(), [Line("m2", "2026-10-13T12:01:00Z", 20)]);
        Assert.That(reader.Poll(Since).Single().UsageSince(Since).Output, Is.EqualTo(30));
        Assert.That(System.IO.File.Exists(Cache), Is.False, "new, but saved under a minute ago");

        clock = clock.AddSeconds(29);
        reader.Poll(Since);
        Assert.That(System.IO.File.Exists(Cache), Is.False, "59 seconds since the save");

        clock = clock.AddSeconds(1);
        reader.Poll(Since);
        Assert.That(System.IO.File.Exists(Cache), Is.True, "a minute on, what was held back is saved without anything new");
        System.IO.File.Delete(Cache);

        clock = clock.AddMinutes(2);
        reader.Poll(Since);
        Assert.That(System.IO.File.Exists(Cache), Is.False, "nothing new since the save: nothing to write");

        var restored = Reader(Cache);
        System.IO.File.WriteAllText(Cache, "");
        Assert.That(restored.Poll(Since).Single().UsageSince(Since).Output, Is.EqualTo(30), "an unreadable cache reads from scratch");
    }

    [Test]
    public void Version_moves_only_when_something_changed()
    {
        System.IO.File.WriteAllLines(Transcript(), [Line("m1", "2026-10-13T12:00:00Z", 10)]);
        var reader = Reader();
        var first = reader.Poll(Since);
        var v = reader.Version;
        Assert.That(v, Is.EqualTo(1));

        clock = clock.AddSeconds(10);
        Assert.That(reader.Poll(Since), Is.SameAs(first), "opened (it's hot) but unchanged");
        Assert.That(reader.LastPollOpened, Is.EqualTo(1));
        Assert.That(reader.Version, Is.EqualTo(v));

        clock = clock.AddMinutes(6);
        reader.Poll(Since);
        Assert.That(reader.Version, Is.EqualTo(v), "a full check that finds nothing new");

        System.IO.File.AppendAllLines(Transcript(), [Line("m2", "2026-10-13T12:01:00Z", 20)]);
        reader.Poll(Since);
        Assert.That(reader.Version, Is.EqualTo(++v), "growth");

        reader.Poll(Since.AddHours(1));
        Assert.That(reader.Version, Is.EqualTo(++v), "the window moved");
        reader.Poll(Since.AddHours(1));
        Assert.That(reader.Version, Is.EqualTo(v));

        System.IO.File.WriteAllLines(Transcript("s2.jsonl"), [Line("n1", "2026-10-13T12:02:00Z", 5)]);
        reader.Poll(Since.AddHours(1));
        Assert.That(reader.Version, Is.EqualTo(++v), "a new transcript");

        System.IO.File.Delete(Transcript("s2.jsonl"));
        reader.Poll(Since.AddHours(1));
        Assert.That(reader.Version, Is.EqualTo(++v), "a transcript gone");
        reader.Poll(Since.AddHours(1));
        Assert.That(reader.Version, Is.EqualTo(v));
    }

    [Test]
    public void A_missing_projects_folder_moves_Version_once()
    {
        var reader = Reader(projects: Path.Combine(dir, "nowhere"));
        Assert.That(reader.Poll(Since), Is.Empty);
        Assert.That(reader.Version, Is.EqualTo(1));
        Assert.That(reader.Poll(Since), Is.Empty);
        Assert.That(reader.Version, Is.EqualTo(1), "still empty: nothing changed");
    }
}
