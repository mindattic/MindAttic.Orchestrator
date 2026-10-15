using System.Text.Json;
using MindAttic.Orchestrator.Tickets;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class TicketReplayTests
{
    [Test]
    public void Finds_the_sessions_dispatched_on_a_ticket_and_sums_them_up()
    {
        var dir = Path.Combine(Path.GetTempPath(), "orch-replay-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "D--W-Repo"));
        Directory.CreateDirectory(Path.Combine(dir, "D--Projects-MindAttic-worktrees-Repo-RP-T7"));
        string User(string text, string at) => JsonSerializer.Serialize(new { type = "user", timestamp = at, message = new { role = "user", content = text } });
        string Reply(string id, string text, string at, int output) => JsonSerializer.Serialize(new
        {
            type = "assistant", timestamp = at,
            message = new { id, content = new[] { new { type = "text", text } }, usage = new { input_tokens = 0, cache_creation_input_tokens = 0, cache_read_input_tokens = 0, output_tokens = output } },
        });
        try
        {
            File.WriteAllLines(Path.Combine(dir, "D--W-Repo", "a.jsonl"), [
                User("Work on ticket RP-T7 (fix): crash on save.", "2026-10-14T10:00:00Z"),
                Reply("m1", "Looking.", "2026-10-14T10:01:00Z", 100),
                Reply("m2", "Fixed and committed.", "2026-10-14T10:30:00Z", 50),
            ]);
            File.WriteAllLines(Path.Combine(dir, "D--W-Repo", "other.jsonl"), [User("Work on ticket RP-T70 (fix): something else.", "2026-10-14T09:00:00Z")]);
            File.WriteAllLines(Path.Combine(dir, "D--Projects-MindAttic-worktrees-Repo-RP-T7", "b.jsonl"), [User("carry on", "2026-10-14T11:00:00Z")]);

            var sessions = TicketReplay.Sessions("RP-T7", dir);

            Assert.That(sessions, Has.Count.EqualTo(2), "by prompt and by worktree folder; RP-T70 isn't RP-T7");
            Assert.That((sessions[0].Fresh, sessions[0].LastReply), Is.EqualTo((150L, "Fixed and committed.")));
            Assert.That(sessions[0].Prompts.Single(), Does.StartWith("Work on ticket RP-T7"));
            Assert.That(sessions[1].Prompts.Single(), Is.EqualTo("carry on"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
