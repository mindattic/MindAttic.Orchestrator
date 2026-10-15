using System.Diagnostics;
using System.Text.Json;
using MindAttic.Orchestrator.Tickets;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class TicketReplayTests
{
    [Test]
    public void Commits_matches_the_ticket_id_as_a_whole_word_not_a_substring()
    {
        var dir = Path.Combine(Path.GetTempPath(), "orch-replay-git-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Git(dir, "init", "-q", "-b", "main");
            Git(dir, "config", "user.email", "test@example.com");
            Git(dir, "config", "user.name", "Test");
            File.WriteAllText(Path.Combine(dir, "a.txt"), "1");
            Git(dir, "add", "-A");
            Git(dir, "commit", "-q", "-m", "Work on MAO-T1: first one");
            File.WriteAllText(Path.Combine(dir, "a.txt"), "2");
            Git(dir, "add", "-A");
            Git(dir, "commit", "-q", "-m", "Work on MAO-T10: an unrelated ticket");
            File.WriteAllText(Path.Combine(dir, "a.txt"), "3");
            Git(dir, "add", "-A");
            Git(dir, "commit", "-q", "-m", "Work on MAO-T19: another unrelated ticket");

            var commits = TicketReplay.Commits("MAO-T1", dir, diff: false);

            Assert.That(commits, Does.Contain("MAO-T1:"));
            Assert.That(commits, Does.Not.Contain("MAO-T10"));
            Assert.That(commits, Does.Not.Contain("MAO-T19"));
        }
        finally
        {
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(dir, recursive: true);
        }
    }

    private static void Git(string dir, params string[] args)
    {
        var psi = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        psi.ArgumentList.Add("-C"); psi.ArgumentList.Add(dir);
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        p.WaitForExit();
    }

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
