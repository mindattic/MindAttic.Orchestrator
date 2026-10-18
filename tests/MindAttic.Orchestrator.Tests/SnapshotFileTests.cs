using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// The published status snapshot every other orchestrator process reads (M.ORCH-T130): a bad file reads as
/// "nothing published" rather than throwing, an unchanged file is served from memory, a changed one is reread,
/// and two processes publishing at once never leave a reader a torn file.
/// </summary>
public sealed class SnapshotFileTests
{
    private string dir = "";
    private string path = "";

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "orch-snapshot-" + Guid.NewGuid().ToString("N"));
        path = Path.Combine(dir, "snapshot.json");
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
    }

    /// <summary>A snapshot whose shape is fixed: repo i has i+1 sessions, session j has j+1 events.</summary>
    internal static Snapshot Sample(DateTime takenUtc, int repos = 2, string marker = "w")
    {
        var list = new List<RepoStatus>();
        for (var i = 0; i < repos; i++)
        {
            var sessions = new List<TranscriptSession>();
            for (var j = 0; j <= i; j++)
            {
                sessions.Add(new TranscriptSession($"s{i}-{j}", $@"D:\W\R{i}", takenUtc, new TokenUsage(1, 2, 3, 4), "opus")
                {
                    Events = [.. Enumerable.Range(0, j + 1).Select(k => new UsageEvent(takenUtc.AddMinutes(-k), new TokenUsage(k, i, j, 7)))],
                });
            }
            list.Add(new RepoStatus($"R{i}", $@"D:\W\R{i}", RepoState.Working, [], sessions, [], takenUtc, new TokenUsage(1, 2, 3, 4)));
        }
        return new Snapshot(takenUtc, list, [marker]);
    }

    /// <summary>True when <paramref name="s"/> has exactly the shape <see cref="Sample"/> gives it.</summary>
    private static bool Whole(Snapshot s) =>
        s.Repos.Select((r, i) => r.Sessions.Count == i + 1
            && r.Sessions.Select((x, j) => x.Events.Count == j + 1
                && x.Events.Select((e, k) => e.Usage == new TokenUsage(k, i, j, 7)).All(ok => ok)).All(ok => ok)).All(ok => ok);

    // Whole seconds, so a round trip through the file can't shift it.
    private static DateTime Now => new(DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, DateTimeKind.Utc);

    private void Rewrite(Action<JsonObject> edit)
    {
        var json = JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
        edit(json);
        File.WriteAllText(path, json.ToJsonString());
    }

    private Snapshot? ReadWithoutThrowing(DateTime now)
    {
        Snapshot? read = null;
        Assert.DoesNotThrow(() => read = new SnapshotFile(path).Read(now));
        return read;
    }

    [Test]
    public void A_written_snapshot_reads_back_with_its_events()
    {
        var now = Now;
        new SnapshotFile(path).Write(Sample(now, repos: 3));

        var read = new SnapshotFile(path).Read(now);

        Assert.That(read, Is.Not.Null);
        Assert.That(read!.Repos, Has.Count.EqualTo(3));
        Assert.That(Whole(read), Is.True);
        Assert.That(read.Repos[2].Sessions[2].Events[2], Is.EqualTo(new UsageEvent(now.AddMinutes(-2), new TokenUsage(2, 2, 2, 7))));
        Assert.That(Directory.GetFiles(dir, "*.tmp"), Is.Empty, "the private temp file is swapped in, not left behind");
    }

    [Test]
    public void A_sessions_ticketed_subagents_read_back_so_readers_see_what_they_are_working()
    {
        var now = Now;
        var sample = Sample(now);
        var sub = new TicketedSubagent("agent1", "R-T7", "fix", @"D:\W\R1-T7", [new UsageEvent(now.AddMinutes(-1), new TokenUsage(1, 2, 3, 4))]);
        var repos = sample.Repos.Select((r, i) => i == 1 ? r with { Sessions = [r.Sessions[0] with { TicketedSubagents = [sub] }, r.Sessions[1]] } : r).ToList();
        new SnapshotFile(path).Write(sample with { Repos = repos });

        var read = new SnapshotFile(path).Read(now)!;

        var back = read.Repos[1].Sessions[0].TicketedSubagents.Single();
        Assert.That((back.AgentId, back.TicketId, back.TicketType, back.Cwd, back.Events.Single()), Is.EqualTo((sub.AgentId, sub.TicketId, sub.TicketType, sub.Cwd, sub.Events[0])));
        Assert.That(read.Repos[1].Sessions[1].TicketedSubagents, Is.Empty);
    }

    [Test]
    public void A_missing_file_reads_as_null() => Assert.That(new SnapshotFile(path).Read(Now), Is.Null);

    [Test]
    public void A_snapshot_older_than_MaxAge_reads_as_null()
    {
        var now = Now;
        new SnapshotFile(path).Write(Sample(now));
        Assert.That(new SnapshotFile(path).Read(now + SnapshotFile.MaxAge + TimeSpan.FromSeconds(1)), Is.Null);
    }

    [TestCase("")]
    [TestCase("{ not json")]
    [TestCase("null")]
    [TestCase("[1, 2, 3]")]
    [TestCase("{\"Version\": 1, \"Pid\": 1, \"Snapshot\": {\"TakenUtc\": \"yesterday\"}}")]
    public void A_corrupt_file_reads_as_null_instead_of_throwing(string content)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(path, content);

        Assert.That(ReadWithoutThrowing(Now), Is.Null);
    }

    [Test]
    public void A_file_that_turns_corrupt_drops_the_snapshot_it_had()
    {
        var now = Now;
        var file = new SnapshotFile(path);
        file.Write(Sample(now));
        Assert.That(file.Read(now), Is.Not.Null);

        File.WriteAllText(path, "{ torn");

        Assert.That(file.Read(now), Is.Null);
    }

    [Test]
    public void A_FormatVersion_mismatch_reads_as_null()
    {
        var now = Now;
        new SnapshotFile(path).Write(Sample(now));
        Rewrite(json => json["Version"] = SnapshotFile.FormatVersion + 1);

        Assert.That(ReadWithoutThrowing(now), Is.Null);
    }

    [Test]
    public void The_current_FormatVersion_still_reads_after_a_rewrite()
    {
        // Control for the mismatch tests: rewriting the file through JsonNode doesn't by itself break it.
        var now = Now;
        new SnapshotFile(path).Write(Sample(now));
        Rewrite(json => json["Version"] = SnapshotFile.FormatVersion);

        Assert.That(ReadWithoutThrowing(now), Is.Not.Null);
    }

    [Test]
    public void Fewer_repos_in_Events_than_in_the_snapshot_reads_as_null()
    {
        var now = Now;
        new SnapshotFile(path).Write(Sample(now, repos: 2));
        Rewrite(json => json["Events"]!.AsArray().RemoveAt(1));

        Assert.That(ReadWithoutThrowing(now), Is.Null);
    }

    [Test]
    public void Fewer_sessions_in_Events_than_in_a_repo_reads_as_null()
    {
        var now = Now;
        new SnapshotFile(path).Write(Sample(now, repos: 2));
        Rewrite(json => json["Events"]![1]!.AsArray().RemoveAt(1));

        Assert.That(ReadWithoutThrowing(now), Is.Null);
    }

    [Test]
    public void An_event_short_of_its_five_numbers_reads_as_null()
    {
        var now = Now;
        new SnapshotFile(path).Write(Sample(now, repos: 1));
        Rewrite(json => json["Events"]![0]![0]![0]!.AsArray().RemoveAt(4));

        Assert.That(ReadWithoutThrowing(now), Is.Null);
    }

    [Test]
    public void An_unchanged_file_is_served_from_memory()
    {
        var now = Now;
        new SnapshotFile(path).Write(Sample(now));
        var reader = new SnapshotFile(path);
        var first = reader.Read(now);
        Assert.That(first, Is.Not.Null);

        // Same length, same timestamp, different bytes: a reader that reread it would get nothing back.
        var stamp = File.GetLastWriteTimeUtc(path);
        File.WriteAllBytes(path, new byte[new FileInfo(path).Length]);
        File.SetLastWriteTimeUtc(path, stamp);

        Assert.That(reader.Read(now), Is.SameAs(first));
    }

    [Test]
    public void A_changed_timestamp_is_reread()
    {
        var now = Now;
        new SnapshotFile(path).Write(Sample(now, marker: "a"));
        var reader = new SnapshotFile(path);
        var first = reader.Read(now);
        Assert.That(first, Is.Not.Null);

        // Same length (a marker of the same length), new timestamp.
        var length = new FileInfo(path).Length;
        var stamp = File.GetLastWriteTimeUtc(path);
        new SnapshotFile(path).Write(Sample(now, marker: "b"));
        Assert.That(new FileInfo(path).Length, Is.EqualTo(length));
        File.SetLastWriteTimeUtc(path, stamp.AddSeconds(5));

        var second = reader.Read(now);
        Assert.That(second, Is.Not.SameAs(first));
        Assert.That(second!.Warnings, Is.EqualTo(new[] { "b" }));
    }

    [Test]
    public void A_changed_length_is_reread_even_under_the_same_timestamp()
    {
        var now = Now;
        new SnapshotFile(path).Write(Sample(now, marker: "a"));
        var reader = new SnapshotFile(path);
        var first = reader.Read(now);
        Assert.That(first, Is.Not.Null);

        var stamp = File.GetLastWriteTimeUtc(path);
        var length = new FileInfo(path).Length;
        new SnapshotFile(path).Write(Sample(now, marker: "a longer marker"));
        File.SetLastWriteTimeUtc(path, stamp);
        Assert.That(new FileInfo(path).Length, Is.Not.EqualTo(length));

        var second = reader.Read(now);
        Assert.That(second, Is.Not.SameAs(first));
        Assert.That(second!.Warnings, Is.EqualTo(new[] { "a longer marker" }));
    }

    [Test]
    public void A_file_that_goes_away_reads_as_null()
    {
        var now = Now;
        var file = new SnapshotFile(path);
        file.Write(Sample(now));
        Assert.That(file.Read(now), Is.Not.Null);

        File.Delete(path);

        Assert.That(file.Read(now), Is.Null);
    }

    [Test]
    public void Two_processes_writing_at_once_never_leave_a_torn_file()
    {
        const int writes = 150;
        // The test assembly itself, run through the dotnet host (dotnet test names the one it used).
        var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } h && File.Exists(h) ? h : "dotnet";
        var assembly = typeof(SnapshotFileTests).Assembly.Location;

        var writers = new[] { "one", "two" }.Select(name =>
        {
            var psi = new ProcessStartInfo(host) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            foreach (var arg in new[] { "exec", assembly, SnapshotWriterProgram.Verb, path, name, writes.ToString() }) psi.ArgumentList.Add(arg);
            return Process.Start(psi)!;
        }).ToList();

        // Read the way the other orchestrator processes do, the whole time both are writing.
        var reader = new SnapshotFile(path);
        int seen = 0, torn = 0;
        var markers = new HashSet<string>();
        var deadline = DateTime.UtcNow.AddMinutes(2);
        while (writers.Any(w => !w.HasExited) && DateTime.UtcNow < deadline)
        {
            if (File.Exists(path))
            {
                try
                {
                    // Every whole file parses; a torn one wouldn't.
                    using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
                }
                catch (JsonException) { torn++; }
                catch (IOException) { } // caught mid-swap or already swapped out: a reader just tries again
                catch (UnauthorizedAccessException) { }
            }
            var s = reader.Read(DateTime.UtcNow);
            if (s is not null)
            {
                seen++;
                markers.Add(s.Warnings.Single());
                Assert.That(Whole(s), Is.True, "a snapshot came back with its events out of step with its sessions");
            }
        }

        foreach (var w in writers)
        {
            Assert.That(w.WaitForExit(TimeSpan.FromMinutes(1)), Is.True, "a writer process hung");
            Assert.That(w.ExitCode, Is.EqualTo(0), w.StandardError.ReadToEnd());
            w.Dispose();
        }

        Assert.That(torn, Is.EqualTo(0), "a reader saw a torn file");
        Assert.That(seen, Is.GreaterThan(0), "the reader never saw a snapshot while the writers ran");
        Assert.That(markers, Has.All.Match("^(one|two)-\\d+$"));
        var last = new SnapshotFile(path).Read(DateTime.UtcNow);
        Assert.That(last, Is.Not.Null, "the file left behind doesn't read");
        Assert.That(Whole(last!), Is.True);
        // Not necessarily either writer's last: a write that loses the swap race is dropped, and the next one publishes.
        Assert.That(last!.Warnings.Single(), Does.Match("^(one|two)-\\d+$"));
        Assert.That(Directory.GetFiles(dir, "*.tmp"), Is.Empty, "a writer left its temp file behind");
    }
}
