using System.Text.Json;
using MindAttic.Orchestrator.Tickets;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class TicketReplayTests
{
    [Test]
    public void The_report_shows_the_work_run_and_points_to_Tickets_for_the_ticket_itself()
    {
        // M.ORCH-T762: the ticket's row and commits are MindAttic.Tickets' to show; the report keeps the sessions only.
        var projects = Path.Combine(Path.GetTempPath(), "orch-replay-report-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(projects);
        try
        {
            var report = TicketReplay.PlainReport("X-T8", projects);
            Assert.That(report, Does.Contain("No dispatched sessions found").And.Contain("open X-T8 in Tickets Desktop").And.Not.Contain("Commits"));
        }
        finally { Directory.Delete(projects, recursive: true); }
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
    public void A_sessions_own_subagents_count_for_its_ticket_but_not_one_handed_a_ticket_of_its_own()
    {
        // M.ORCH-T406: a subagent's transcript sits in <session>\subagents, not the worktree's folder, and its prompt doesn't name
        // the ticket; the budget charges it to the session's ticket all the same.
        var projects = Path.Combine(Path.GetTempPath(), "orch-replay-subs-" + Guid.NewGuid().ToString("N"));
        var folder = Path.Combine(projects, "D--Projects-MindAttic-worktrees-Repo-RP-T7");
        var sub = Path.Combine(folder, "s1", "subagents");
        Directory.CreateDirectory(sub);
        try
        {
            static string User(string ts, string content) => JsonSerializer.Serialize(new { type = "user", timestamp = ts, message = new { role = "user", content } });
            static string Reply(string id, string ts, long output) => JsonSerializer.Serialize(new
            {
                type = "assistant", timestamp = ts,
                message = new { id, content = new[] { new { type = "text", text = "ok" } }, usage = new { input_tokens = 0, cache_creation_input_tokens = 0, output_tokens = output } },
            });
            File.WriteAllLines(Path.Combine(folder, "s1.jsonl"), [User("2026-10-14T10:00:00Z", "carry on"), Reply("p1", "2026-10-14T10:00:01Z", 10)]);
            File.WriteAllLines(Path.Combine(sub, "agent-a.jsonl"), [User("2026-10-14T10:01:00Z", "Find the save code."), Reply("a1", "2026-10-14T10:01:01Z", 30)]);
            File.WriteAllLines(Path.Combine(sub, "agent-b.jsonl"), [User("2026-10-14T10:02:00Z", "Work on ticket RP-T9 (fix): another."), Reply("b1", "2026-10-14T10:02:01Z", 70)]);

            var sessions = TicketReplay.Sessions("RP-T7", projects);

            Assert.That(sessions.Select(s => Path.GetFileName(s.File)), Is.EqualTo(new[] { "s1.jsonl", "agent-a.jsonl" }));
            Assert.That(sessions.Sum(s => s.Fresh), Is.EqualTo(10 + 30));
            Assert.That(TicketReplay.Sessions("RP-T9", projects).Single().File, Does.EndWith("agent-b.jsonl"), "its own ticket's");
        }
        finally { Directory.Delete(projects, recursive: true); }
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
            // An /allocate CLI that handed RP-T7 out: the prompt is in its tool output and its Agent call, not asked of it.
            File.WriteAllLines(Path.Combine(dir, "D--W-Repo", "pilot.jsonl"), [
                User("/allocate", "2026-10-14T09:30:00Z"),
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
            // A typed prompt carries no session_id, copied or not (M.ORCH-T391): the copy's is the old session's all the same.
            string Prompt(string text, string at) => JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["type"] = "user", ["timestamp"] = at, ["sessionId"] = "bg1", ["sessionKind"] = "bg",
                ["message"] = new { role = "user", content = text },
            });
            File.WriteAllLines(Path.Combine(folder, "bg1.jsonl"), [
                Prompt("the old session's prompt", "2026-10-14T09:59:00Z"),
                TranscriptReaderTests.SessionLine("m1", "2026-10-14T10:00:00Z", 100, "bg1", "s1", "bg"),
                Prompt("carry on in the background", "2026-10-14T10:04:00Z"),
                TranscriptReaderTests.SessionLine("m2", "2026-10-14T10:05:00Z", 7, "bg1", "bg1", "bg"),
            ]);

            var session = TicketReplay.Sessions("RP-T7", dir).Single();

            Assert.That(session.Fresh, Is.EqualTo(1 + 7), "its own response only");
            Assert.That(session.StartUtc, Is.EqualTo(new DateTime(2026, 10, 14, 10, 4, 0, DateTimeKind.Utc)), "it starts where its own lines do");
            Assert.That(session.Prompts, Is.EqualTo(new[] { "carry on in the background" }), "the copy's prompt is the old session's");
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

    [Test]
    public void A_session_in_a_ticket_worktree_under_a_custom_WorktreeRoot_worked_the_ticket()
    {
        // M.ORCH-T446: only a folder named *worktrees counted, so with settings.json's WorktreeRoot = E:\WT nothing did.
        var dir = Path.Combine(Path.GetTempPath(), "orch-replay-root-" + Guid.NewGuid().ToString("N"));
        string Session(string folder)
        {
            Directory.CreateDirectory(Path.Combine(dir, folder));
            var file = Path.Combine(dir, folder, "s.jsonl");
            File.WriteAllLines(file, [JsonSerializer.Serialize(new { type = "user", timestamp = "2026-10-14T10:00:00Z", message = new { role = "user", content = "carry on" } })]);
            return file;
        }
        try
        {
            var mine = Session("E--WT-Repo-RP-T7");
            Session("E--Elsewhere-Repo-RP-T7");

            Assert.That(TicketReplay.Sessions("RP-T7", dir, @"E:\WT\").Select(s => s.File), Is.EqualTo(new[] { mine }),
                "the ticket's worktree under the configured root, not a folder elsewhere that ends the same");
            Assert.That(TicketReplay.Sessions("RP-T7", dir), Is.Empty, "without the setting, only a *worktrees folder counts");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
