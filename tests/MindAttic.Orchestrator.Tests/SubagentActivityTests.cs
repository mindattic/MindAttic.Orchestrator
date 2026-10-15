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
        // A long subagent's transcript reaches tens of MB; it was re-parsed from byte 0 every refresh (MAO-T113).
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
        }
        finally
        {
            Directory.Delete(sessionDir, recursive: true);
            Directory.Delete(projectsDir, recursive: true);
        }
    }
}
