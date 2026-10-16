using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Sessions;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class SubagentActivityTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    private static string NewDir(string prefix)
    {
        var dir = Path.Combine(Path.GetTempPath(), prefix + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Line(string type, string timestamp, string? attributionAgent = null, (long In, long CacheWrite, long CacheRead, long Out)? usage = null)
    {
        var usageJson = usage is { } u
            ? $",\"message\":{{\"usage\":{{\"input_tokens\":{u.In},\"cache_creation_input_tokens\":{u.CacheWrite},\"cache_read_input_tokens\":{u.CacheRead},\"output_tokens\":{u.Out}}}}}"
            : "";
        var agentJson = attributionAgent is null ? "" : $",\"attributionAgent\":\"{attributionAgent}\"";
        return $"{{\"type\":\"{type}\",\"timestamp\":\"{timestamp}\"{agentJson}{usageJson}}}";
    }

    [Test]
    public void A_subagent_marked_alive_is_read_from_its_own_transcript_next_to_its_parent()
    {
        var sessionDir = NewDir("ctx");
        var projectsDir = NewDir("projects");
        try
        {
            Directory.CreateDirectory(Path.Combine(sessionDir, "subagents"));
            // The mark is named after the raw agent id (the context gate's convention); the transcript Claude
            // Code writes is named "agent-{id}.jsonl", one level further down under its parent session's folder.
            File.WriteAllText(Path.Combine(sessionDir, "subagents", "a1"), ""); // no pid: trust the mark's age alone

            var parentFolder = Path.Combine(projectsDir, "proj", "session-1", "subagents");
            Directory.CreateDirectory(parentFolder);
            File.WriteAllLines(Path.Combine(parentFolder, "agent-a1.jsonl"),
            [
                Line("user", "2026-10-15T11:55:00Z"),
                Line("assistant", "2026-10-15T11:56:00Z", attributionAgent: "Explore", usage: (100, 0, 0, 50)),
                Line("assistant", "2026-10-15T11:58:00Z", attributionAgent: "Explore", usage: (20, 0, 0, 10)),
            ]);

            var cycler = new ContextCycler(sessionDir);
            var running = SubagentActivityReader.Running(cycler, projectsDir, Now);

            Assert.That(running, Has.Count.EqualTo(1));
            var s = running[0];
            Assert.That(s.AgentId, Is.EqualTo("a1"));
            Assert.That(s.ParentSessionId, Is.EqualTo("session-1"));
            Assert.That(s.Type, Is.EqualTo("Explore"));
            Assert.That(s.StartedUtc, Is.EqualTo(new DateTime(2026, 10, 15, 11, 55, 0, DateTimeKind.Utc)));
            Assert.That(s.LastWriteUtc, Is.EqualTo(new DateTime(2026, 10, 15, 11, 58, 0, DateTimeKind.Utc)));
            Assert.That(s.Usage, Is.EqualTo(new TokenUsage(120, 0, 0, 60)));
        }
        finally
        {
            Directory.Delete(sessionDir, recursive: true);
            Directory.Delete(projectsDir, recursive: true);
        }
    }

    [Test]
    public void A_stopped_subagent_leaves_no_mark_and_so_isnt_reported()
    {
        var sessionDir = NewDir("ctx");
        var projectsDir = NewDir("projects");
        try
        {
            // No mark file at all: SubagentStop already removed it.
            var parentFolder = Path.Combine(projectsDir, "proj", "session-1", "subagents");
            Directory.CreateDirectory(parentFolder);
            File.WriteAllLines(Path.Combine(parentFolder, "agent-a1.jsonl"), [Line("user", "2026-10-15T11:55:00Z")]);

            var cycler = new ContextCycler(sessionDir);
            var running = SubagentActivityReader.Running(cycler, projectsDir, Now);

            Assert.That(running, Is.Empty);
        }
        finally
        {
            Directory.Delete(sessionDir, recursive: true);
            Directory.Delete(projectsDir, recursive: true);
        }
    }

    [Test]
    public void A_subagent_transcript_is_tailed_so_each_poll_reads_only_what_was_appended()
    {
        // A long subagent's transcript reaches tens of MB; it was re-parsed from byte 0 every refresh (M.ORCH-T113).
        var sessionDir = NewDir("ctx");
        var projectsDir = NewDir("projects");
        try
        {
            Directory.CreateDirectory(Path.Combine(sessionDir, "subagents"));
            File.WriteAllText(Path.Combine(sessionDir, "subagents", "a2"), "");
            var folder = Path.Combine(projectsDir, "proj", "session-2", "subagents");
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, "agent-a2.jsonl");
            File.WriteAllLines(file, [Line("assistant", "2026-10-15T11:50:00Z", attributionAgent: "Plan", usage: (10, 0, 0, 5))]);

            var cycler = new ContextCycler(sessionDir);
            var reader = new SubagentActivityReader(projectsDir);
            Assert.That(reader.Poll(cycler, Now).Single().Usage, Is.EqualTo(new TokenUsage(10, 0, 0, 5)));

            // Appended since: one whole line and one still being written (no newline yet).
            File.AppendAllText(file, Line("assistant", "2026-10-15T11:52:00Z", attributionAgent: "Plan", usage: (1, 0, 0, 1)) + "\n" +
                Line("assistant", "2026-10-15T11:53:00Z", usage: (1000, 0, 0, 0))[..20]);
            var s = reader.Poll(cycler, Now).Single();
            Assert.That((s.Usage, s.ParentSessionId, s.Type), Is.EqualTo((new TokenUsage(11, 0, 0, 6), "session-2", "Plan")), "the first line isn't counted twice");
            Assert.That((s.StartedUtc, s.LastWriteUtc), Is.EqualTo((new DateTime(2026, 10, 15, 11, 50, 0, DateTimeKind.Utc), new DateTime(2026, 10, 15, 11, 52, 0, DateTimeKind.Utc))),
                "the half-written line waits until it's whole");

            File.WriteAllLines(file, [Line("assistant", "2026-10-15T11:55:00Z", usage: (3, 0, 0, 3))]); // replaced with something shorter
            Assert.That(reader.Poll(cycler, Now).Single().Usage, Is.EqualTo(new TokenUsage(3, 0, 0, 3)), "a file that shrank is read again from the start");

            // A message id that isn't a string used to throw out of the whole snapshot build.
            File.AppendAllText(file, """{"type":"assistant","timestamp":"2026-10-15T11:56:00Z","message":{"id":123,"usage":{"input_tokens":4,"output_tokens":4}}}""" + "\n");
            Assert.That(reader.Poll(cycler, Now).Single().Usage, Is.EqualTo(new TokenUsage(7, 0, 0, 7)));
        }
        finally
        {
            Directory.Delete(sessionDir, recursive: true);
            Directory.Delete(projectsDir, recursive: true);
        }
    }

    // ---- SubagentTranscripts.Find (M.ORCH-T143) ----

    private static string WriteTranscript(string projectsDir, string session, string agentId, params string[] lines)
    {
        var folder = Path.Combine(projectsDir, "proj", session, "subagents");
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, $"agent-{agentId}.jsonl");
        File.WriteAllLines(file, lines);
        return file;
    }

    [Test]
    public void Find_remembers_a_miss_and_looks_again_only_after_the_retry_wait()
    {
        var projectsDir = NewDir("projects");
        try
        {
            var id = "miss-" + Guid.NewGuid().ToString("N");
            var t0 = DateTime.UtcNow;
            Assert.That(SubagentTranscripts.Find(projectsDir, id, t0), Is.Null, "not written yet");

            var file = WriteTranscript(projectsDir, "session-1", id, "{}");
            Assert.That(SubagentTranscripts.Find(projectsDir, id, t0 + SubagentTranscripts.RetryMissAfter - TimeSpan.FromSeconds(1)), Is.Null,
                "a miss isn't looked for again inside the retry wait, even though the file is there now");
            Assert.That(SubagentTranscripts.Find(projectsDir, id, t0 + SubagentTranscripts.RetryMissAfter), Is.EqualTo(file),
                "once the wait is up it's looked for again and found");
            Assert.That(SubagentTranscripts.Find(projectsDir, id, t0 + SubagentTranscripts.RetryMissAfter), Is.EqualTo(file), "and remembered");
        }
        finally { Directory.Delete(projectsDir, recursive: true); }
    }

    [Test]
    public void Find_forgets_a_remembered_path_whose_file_was_deleted()
    {
        var projectsDir = NewDir("projects");
        try
        {
            var id = "gone-" + Guid.NewGuid().ToString("N");
            var first = WriteTranscript(projectsDir, "session-1", id, "{}");
            var t0 = DateTime.UtcNow;
            Assert.That(SubagentTranscripts.Find(projectsDir, id, t0), Is.EqualTo(first));

            File.Delete(first);
            Assert.That(SubagentTranscripts.Find(projectsDir, id, t0), Is.Null, "the remembered path isn't handed out once its file is gone");

            var moved = WriteTranscript(projectsDir, "session-2", id, "{}");
            Assert.That(SubagentTranscripts.Find(projectsDir, id, t0 + SubagentTranscripts.RetryMissAfter), Is.EqualTo(moved),
                "it's looked for afresh, not stuck on the old path");
        }
        finally { Directory.Delete(projectsDir, recursive: true); }
    }

    // ---- SubagentActivityReader (M.ORCH-T143) ----

    [Test]
    public void The_tail_of_a_subagent_no_longer_alive_is_dropped()
    {
        var sessionDir = NewDir("ctx");
        var projectsDir = NewDir("projects");
        try
        {
            var cycler = new ContextCycler(sessionDir) { ProjectsDir = projectsDir };
            Directory.CreateDirectory(cycler.SubagentDirectory);
            var a = "drop-a-" + Guid.NewGuid().ToString("N");
            var b = "drop-b-" + Guid.NewGuid().ToString("N");
            var markA = Path.Combine(cycler.SubagentDirectory, a);
            File.WriteAllText(markA, "");
            File.WriteAllText(Path.Combine(cycler.SubagentDirectory, b), "");
            var fileA = WriteTranscript(projectsDir, "session-1", a, Line("assistant", "2026-10-15T11:50:00Z", usage: (10, 0, 0, 0)));
            WriteTranscript(projectsDir, "session-1", b, Line("assistant", "2026-10-15T11:50:00Z", usage: (1, 0, 0, 0)));
            var now = DateTime.UtcNow;

            var reader = new SubagentActivityReader(projectsDir);
            Assert.That(reader.Poll(cycler, now).Select(s => s.AgentId).Order(), Is.EqualTo(new[] { a, b }.Order()));

            File.Delete(markA); // a stopped; b runs on
            Assert.That(reader.Poll(cycler, now).Select(s => s.AgentId), Is.EqualTo(new[] { b }));

            // a's transcript is now something else, longer than before. Had its tail been kept, the next read would
            // pick up at the old offset (mid-line) and add to the old count; dropped, it's read whole from the start.
            File.WriteAllLines(fileA,
            [
                Line("assistant", "2026-10-15T11:40:00Z", attributionAgent: "Explore-with-a-long-name", usage: (7, 0, 0, 0)),
                Line("assistant", "2026-10-15T11:41:00Z", attributionAgent: "Explore-with-a-long-name", usage: (5, 0, 0, 0)),
            ]);
            File.WriteAllText(markA, "");
            var again = reader.Poll(cycler, now).Single(s => s.AgentId == a);
            Assert.That((again.Usage, again.StartedUtc), Is.EqualTo((new TokenUsage(12, 0, 0, 0), new DateTime(2026, 10, 15, 11, 40, 0, DateTimeKind.Utc))));
        }
        finally
        {
            Directory.Delete(sessionDir, recursive: true);
            Directory.Delete(projectsDir, recursive: true);
        }
    }

    [Test]
    public void A_subagent_idle_twenty_minutes_isnt_reported_but_its_mark_is_kept()
    {
        var sessionDir = NewDir("ctx");
        var projectsDir = NewDir("projects");
        try
        {
            var cycler = new ContextCycler(sessionDir) { ProjectsDir = projectsDir };
            Directory.CreateDirectory(cycler.SubagentDirectory);
            var id = "idle-" + Guid.NewGuid().ToString("N");
            var mark = Path.Combine(cycler.SubagentDirectory, id);
            File.WriteAllText(mark, ""); // no pid: trust the mark's age alone
            var file = WriteTranscript(projectsDir, "session-1", id, Line("assistant", "2026-10-15T11:00:00Z", usage: (1, 0, 0, 1)));
            var now = DateTime.UtcNow;
            File.SetLastWriteTimeUtc(file, now - ContextCycler.SubagentIdleLife - TimeSpan.FromMinutes(1));

            var reader = new SubagentActivityReader(projectsDir);
            Assert.That(reader.Poll(cycler, now), Is.Empty, "quiet past 20 minutes: not running");
            Assert.That(cycler.Subagents(), Is.Zero);

            cycler.PruneMarks();
            Assert.That(File.Exists(mark), "the idle rule only stops it counting; its mark stays");

            File.SetLastWriteTimeUtc(file, now);
            Assert.That(reader.Poll(cycler, now).Select(s => s.AgentId), Is.EqualTo(new[] { id }), "it's reported again as soon as it writes");
        }
        finally
        {
            Directory.Delete(sessionDir, recursive: true);
            Directory.Delete(projectsDir, recursive: true);
        }
    }
}
