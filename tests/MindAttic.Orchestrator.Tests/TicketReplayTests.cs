using System.Diagnostics;
using System.Text.Json;
using MindAttic.Orchestrator.Tickets;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class TicketReplayTests
{
    [Test]
    public void Commits_shows_a_stat_or_a_patch_and_the_report_says_when_there_is_nothing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "orch-replay-stat-" + Guid.NewGuid().ToString("N"));
        var projects = Path.Combine(dir, "projects");
        Directory.CreateDirectory(projects);
        try
        {
            Git(dir, "init", "-q", "-b", "main");
            Git(dir, "config", "user.email", "test@example.com");
            Git(dir, "config", "user.name", "Test");
            File.WriteAllText(Path.Combine(dir, "a.txt"), "one\n");
            Git(dir, "add", "a.txt");
            Git(dir, "commit", "-q", "-m", "Fix X-T7: the thing");

            Assert.That(TicketReplay.Commits("X-T7", dir, diff: false), Does.Contain("a.txt").And.Contain("1 file changed").And.Not.Contain("+one"));
            Assert.That(TicketReplay.Commits("X-T7", dir, diff: true), Does.Contain("+one"), "the patch itself");
            Assert.That(TicketReplay.Commits("X-T7", Path.Combine(dir, "missing"), diff: false), Is.Empty, "no repo there: empty, not an error");

            var report = TicketReplay.PlainReport("X-T8", dir, projects);
            Assert.That(report, Does.Contain("No dispatched sessions found").And.Contain("No commits mention it."));
            Assert.That(TicketReplay.PlainReport("X-T7", dir, projects), Does.Contain("Fix X-T7: the thing").And.Not.Contain("No commits mention it."));
        }
        finally
        {
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(dir, recursive: true);
        }
    }

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
            Git(dir, "commit", "-q", "-m", "Work on M.ORCH-T1: first one");
            File.WriteAllText(Path.Combine(dir, "a.txt"), "2");
            Git(dir, "add", "-A");
            Git(dir, "commit", "-q", "-m", "Work on M.ORCH-T10: an unrelated ticket");
            File.WriteAllText(Path.Combine(dir, "a.txt"), "3");
            Git(dir, "add", "-A");
            Git(dir, "commit", "-q", "-m", "Work on M.ORCH-T19: another unrelated ticket");

            var commits = TicketReplay.Commits("M.ORCH-T1", dir, diff: false);

            Assert.That(commits, Does.Contain("M.ORCH-T1:"));
            Assert.That(commits, Does.Not.Contain("M.ORCH-T10"));
            Assert.That(commits, Does.Not.Contain("M.ORCH-T19"));
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
            // An /autopilot CLI that handed RP-T7 out: the prompt is in its tool output and its Agent call, not asked of it.
            File.WriteAllLines(Path.Combine(dir, "D--W-Repo", "pilot.jsonl"), [
                User("/autopilot", "2026-10-14T09:30:00Z"),
                JsonSerializer.Serialize(new { type = "user", timestamp = "2026-10-14T09:31:00Z", message = new { role = "user",
                    content = new[] { new { type = "tool_result", content = "Work on ticket RP-T7 (fix): crash on save." } } } }),
                JsonSerializer.Serialize(new { type = "assistant", timestamp = "2026-10-14T09:32:00Z", message = new { id = "m9",
                    content = new[] { new { type = "tool_use", input = new { prompt = "Work on ticket RP-T7 (fix): crash on save." } } } } }),
            ]);

            var sessions = TicketReplay.Sessions("RP-T7", dir);

            Assert.That(sessions, Has.Count.EqualTo(2), "by prompt and by worktree folder; RP-T70 isn't RP-T7");
            Assert.That((sessions[0].Fresh, sessions[0].LastReply), Is.EqualTo((150L, "Fixed and committed.")));
            Assert.That(sessions[0].Prompts.Single(), Does.StartWith("Work on ticket RP-T7"));
            Assert.That(sessions[1].Prompts.Single(), Is.EqualTo("carry on"));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public void A_dotted_ticket_matches_its_own_worktree_folder_but_not_a_longer_id()
    {
        // Claude names the folder for D:\Projects\MindAttic.worktrees\MindAttic.Orchestrator\M.ORCH-T1 with dots as dashes.
        var dir = Path.Combine(Path.GetTempPath(), "orch-replay-folder-" + Guid.NewGuid().ToString("N"));
        string Session(string folder, string at)
        {
            Directory.CreateDirectory(Path.Combine(dir, folder));
            var file = Path.Combine(dir, folder, "s.jsonl");
            File.WriteAllLines(file, [JsonSerializer.Serialize(new { type = "user", timestamp = at, message = new { role = "user", content = "carry on" } })]);
            return file;
        }
        try
        {
            var mine = Session("D--Projects-MindAttic-worktrees-MindAttic-Orchestrator-M-ORCH-T1", "2026-10-14T10:00:00Z");
            var dotted = Session("D--Projects-MindAttic.worktrees-MindAttic.Orchestrator-M.ORCH-T1", "2026-10-14T11:00:00Z");
            Session("D--Projects-MindAttic-worktrees-MindAttic-Orchestrator-M-ORCH-T11", "2026-10-14T12:00:00Z");
            Session("D--Projects-MindAttic-MindAttic-Orchestrator-M-ORCH-T1", "2026-10-14T13:00:00Z");

            var sessions = TicketReplay.Sessions("M.ORCH-T1", dir);

            Assert.That(sessions.Select(s => s.File), Is.EqualTo(new[] { mine, dotted }),
                "its worktree folder either way it's spelled; not -M-ORCH-T11, and not a folder outside the worktrees");
            Assert.That(TicketReplay.Sessions("M.ORCH-T11", dir), Has.Count.EqualTo(1));
            Assert.That(TicketReplay.Sessions("M.ORCH-T1", Path.Combine(dir, "missing")), Is.Empty, "no projects folder: nothing");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
