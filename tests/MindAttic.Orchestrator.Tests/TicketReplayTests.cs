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
    public void A_sessions_cost_counts_a_response_at_its_largest_line_and_not_a_forks_copied_prologue()
    {
        // M.ORCH-T379: a subagent's lines of one response grow their output_tokens; a fork copies the subagent's transcript.
        var projects = Path.Combine(Path.GetTempPath(), "orch-replay-cost-" + Guid.NewGuid().ToString("N"));
        var sub = Path.Combine(projects, "D--Projects-X", "s1", "subagents");
        Directory.CreateDirectory(sub);
        try
        {
            static string User(string ts, object content) => JsonSerializer.Serialize(new { type = "user", timestamp = ts, message = new { role = "user", content } });
            static string Reply(string id, string ts, long output) => JsonSerializer.Serialize(new
            {
                type = "assistant", timestamp = ts,
                message = new { id, content = new[] { new { type = "text", text = "ok" } }, usage = new { input_tokens = 1, cache_creation_input_tokens = 0, output_tokens = output } },
            });
            var subagent = new[] { User("2026-10-13T12:00:00Z", "Work on ticket X-T7 (fix): the thing."), Reply("r1", "2026-10-13T12:00:01Z", 2), Reply("r1", "2026-10-13T12:00:02Z", 40) };
            File.WriteAllLines(Path.Combine(sub, "agent-a.jsonl"), subagent);
            File.WriteAllLines(Path.Combine(sub, "agent-f.jsonl"), [.. subagent,
                User("2026-10-13T12:01:00Z", new object[] { new { type = "tool_result", tool_use_id = "toolu_f", content = "Fork started" }, new { type = "text", text = "the fork's own" } }),
                Reply("f1", "2026-10-13T12:01:01Z", 5)]);
            File.WriteAllText(Path.Combine(sub, "agent-f.meta.json"), """{"isFork":true,"toolUseId":"toolu_f"}""");

            var sessions = TicketReplay.Sessions("X-T7", projects).ToDictionary(s => Path.GetFileName(s.File));

            Assert.That(sessions["agent-a.jsonl"].Fresh, Is.EqualTo(41), "the response once, at its last line's 40 output");
            Assert.That(sessions["agent-f.jsonl"].Fresh, Is.EqualTo(6), "the fork's own response only");
            Assert.That(sessions["agent-f.jsonl"].Prompts, Is.EqualTo(new[] { "the fork's own" }));
        }
        finally { Directory.Delete(projects, recursive: true); }
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
    public void A_background_sessions_copy_of_the_session_it_continues_isnt_counted_as_its_own()
    {
        // M.ORCH-T375: counted as the budget counts it, the copied history is the old session's, not the background one's.
        var dir = Path.Combine(Path.GetTempPath(), "orch-replay-bg-" + Guid.NewGuid().ToString("N"));
        var folder = Path.Combine(dir, "D--Projects-MindAttic-worktrees-Repo-RP-T7");
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllLines(Path.Combine(folder, "bg1.jsonl"), [
                TranscriptReaderTests.SessionLine("m1", "2026-10-14T10:00:00Z", 100, "bg1", "s1", "bg"),
                TranscriptReaderTests.SessionLine("m2", "2026-10-14T10:05:00Z", 7, "bg1", "bg1", "bg"),
            ]);

            var session = TicketReplay.Sessions("RP-T7", dir).Single();

            Assert.That(session.Fresh, Is.EqualTo(1 + 7), "its own response only");
            Assert.That(session.StartUtc, Is.EqualTo(new DateTime(2026, 10, 14, 10, 5, 0, DateTimeKind.Utc)), "it starts where its own lines do");
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
