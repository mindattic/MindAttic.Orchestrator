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
}
