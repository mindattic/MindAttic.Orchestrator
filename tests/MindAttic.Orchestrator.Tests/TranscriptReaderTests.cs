using MindAttic.Orchestrator.Activity;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class TranscriptReaderTests
{
    private string dir = "";

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "orch-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "D--Projects-X"));
    }

    [TearDown]
    public void TearDown() => Directory.Delete(dir, recursive: true);

    private static readonly DateTime Since = new(2026, 10, 13, 5, 0, 0, DateTimeKind.Utc);

    private static string Line(string id, string ts, int input, int output, string cwd = @"D:\Projects\X") =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "assistant", timestamp = ts, sessionId = "s1", cwd,
            message = new
            {
                id, model = "claude-sonnet-5",
                usage = new { input_tokens = input, cache_creation_input_tokens = 10, cache_read_input_tokens = 100, output_tokens = output },
            },
        });

    private string File(string name = "s1.jsonl") => Path.Combine(dir, "D--Projects-X", name);

    // The shape Claude Code writes when it refuses a turn at the limit (trimmed from a real transcript).
    private static string LimitLine(string ts, string text) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "assistant", timestamp = ts, sessionId = "s1", cwd = @"D:\Projects\X", error = "rate_limit", isApiErrorMessage = true,
            message = new
            {
                id = Guid.NewGuid().ToString(), model = "<synthetic>", role = "assistant",
                usage = new { input_tokens = 0, cache_creation_input_tokens = 0, cache_read_input_tokens = 0, output_tokens = 0 },
                content = new[] { new { type = "text", text } },
            },
        });

    private string Cache => Path.Combine(dir, "cache", "transcripts.json");

    // Overwrites the bytes already read with spaces: only a reader that starts over would notice.
    private void BlankReadBytes() =>
        System.IO.File.WriteAllText(File(), new string(' ', (int)new FileInfo(File()).Length - 1) + "\n");

    private static string ToolLine(string id, string ts) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "assistant", timestamp = ts, sessionId = "s1", cwd = @"D:\Projects\X",
            message = new
            {
                id, model = "claude-sonnet-5",
                usage = new { input_tokens = 1, cache_creation_input_tokens = 0, cache_read_input_tokens = 0, output_tokens = 1 },
                content = new[] { new { type = "tool_use", name = "Bash", input = new { command = "ls" } } },
            },
        });

    [Test]
    public void A_transcript_read_again_from_the_start_doesnt_count_its_tool_calls_twice()
    {
        // Three identical calls read twice became six, past the loop threshold: a false "looks stuck".
        string[] calls = [ToolLine("m1", "2026-10-13T06:00:00Z"), ToolLine("m2", "2026-10-13T06:01:00Z"), ToolLine("m3", "2026-10-13T06:02:00Z")];
        System.IO.File.WriteAllLines(File(), [.. calls, Line("pad", "2026-10-13T06:03:00Z", 1, 1)]);
        var reader = new TranscriptReader(dir);
        Assert.That(reader.Poll(Since).Single().Loop, Is.Null);

        System.IO.File.WriteAllLines(File(), calls); // rewritten shorter: read again from 0
        Assert.That(reader.Poll(Since).Single().Loop, Is.Null);
    }

    [Test]
    public void A_restarted_reader_picks_up_where_the_last_one_stopped()
    {
        System.IO.File.WriteAllLines(File(), [Line("m1", "2026-10-13T12:00:00Z", 1, 10), Line("m2", "2026-10-13T12:01:00Z", 1, 20)]);
        System.IO.File.SetLastWriteTimeUtc(File(), new DateTime(2026, 10, 13, 12, 1, 0, DateTimeKind.Utc));
        new TranscriptReader(dir, Cache).Poll(Since);
        BlankReadBytes();
        System.IO.File.AppendAllLines(File(), [Line("m3", "2026-10-13T12:02:00Z", 1, 40)]);
        var mtime = new DateTime(2026, 10, 13, 12, 2, 0, DateTimeKind.Utc);
        System.IO.File.SetLastWriteTimeUtc(File(), mtime);

        var s = new TranscriptReader(dir, Cache).Poll(Since).Single();

        Assert.That(s.UsageSince(Since).Output, Is.EqualTo(70), "m1 and m2 from the cache, m3 read fresh");
        Assert.That(s.Cwd, Is.EqualTo(@"D:\Projects\X"));
        Assert.That(s.LastWriteUtc, Is.EqualTo(mtime), "growth while nobody watched isn't activity right now");
    }

    [Test]
    public void A_later_window_trims_the_cache_and_an_earlier_one_reads_from_scratch()
    {
        System.IO.File.WriteAllLines(File(), [Line("m1", "2026-10-13T12:00:00Z", 1, 10), Line("m2", "2026-10-14T12:00:00Z", 1, 20)]);
        new TranscriptReader(dir, Cache).Poll(Since);
        BlankReadBytes();

        var later = new TranscriptReader(dir, Cache).Poll(Since.AddDays(1)).Single();
        var earlier = new TranscriptReader(dir, Cache).Poll(Since.AddDays(-1));

        Assert.That(later.UsageSince(DateTime.MinValue).Output, Is.EqualTo(20), "m1 fell out of the window; m2 kept without rereading");
        Assert.That(earlier, Is.Empty, "a window the cache doesn't cover is read from the file, which is blank now");
    }

    [Test]
    public void A_limit_message_is_recorded_as_a_hit_not_a_response()
    {
        System.IO.File.WriteAllLines(File(), [
            Line("m1", "2026-10-13T20:00:00Z", 5, 5),
            LimitLine("2026-10-13T21:22:25Z", "You've hit your session limit · resets 6:40pm (America/Chicago)"),
        ]);

        var s = new TranscriptReader(dir).Poll(Since).Single();

        Assert.That(s.Events, Has.Count.EqualTo(1), "the zero-usage synthetic message isn't a response");
        Assert.That(s.Hits.Single(), Is.EqualTo(new LimitHit(
            new DateTime(2026, 10, 13, 21, 22, 25, DateTimeKind.Utc), LimitKind.Session, new DateTime(2026, 10, 13, 23, 40, 0, DateTimeKind.Utc))));
    }

    [Test]
    public void Counts_each_message_once_even_when_split_across_lines()
    {
        System.IO.File.WriteAllLines(File(), [
            Line("m1", "2026-10-13T12:00:00Z", 2, 30),
            Line("m1", "2026-10-13T12:00:00Z", 2, 30),
            Line("m2", "2026-10-13T12:01:00Z", 3, 40),
        ]);

        var s = new TranscriptReader(dir).Poll(Since).Single();

        Assert.That(s.UsageSince(Since), Is.EqualTo(new TokenUsage(5, 20, 200, 70)));
        Assert.That(s.Cwd, Is.EqualTo(@"D:\Projects\X"));
        Assert.That(s.Model, Is.EqualTo("claude-sonnet-5"));
    }

    [Test]
    public void A_response_whose_later_lines_grow_its_output_is_counted_at_its_last_lines_count_once()
    {
        // A subagent's transcript writes one line per content block, and its output_tokens grow from line to line (M.ORCH-T322).
        System.IO.File.WriteAllLines(File(), [
            Line("m1", "2026-10-13T12:00:00Z", 4, 42),
            Line("m1", "2026-10-13T12:00:01Z", 4, 238),
            Line("m1", "2026-10-13T12:00:01Z", 4, 238),
        ]);
        var reader = new TranscriptReader(dir, Cache);
        Assert.That(reader.Poll(Since).Single().UsageSince(Since), Is.EqualTo(new TokenUsage(4, 10, 100, 238)));

        System.IO.File.AppendAllLines(File(), [Line("m1", "2026-10-13T12:00:02Z", 4, 300)]);
        Assert.That(reader.Poll(Since).Single().UsageSince(Since), Is.EqualTo(new TokenUsage(4, 10, 100, 300)), "a line read later still grows it");
        Assert.That(new TranscriptReader(dir, Cache).Poll(Since).Single().UsageSince(Since), Is.EqualTo(new TokenUsage(4, 10, 100, 300)),
            "a restarted reader's cache keeps what each response was counted at, so the new line only adds its growth");
    }

    [Test]
    public void Ignores_messages_before_the_boundary()
    {
        System.IO.File.WriteAllLines(File(), [
            Line("m1", "2026-10-12T23:00:00Z", 1000, 1000),
            Line("m2", "2026-10-13T12:00:00Z", 1, 1),
        ]);

        var s = new TranscriptReader(dir).Poll(Since).Single();

        Assert.That(s.UsageSince(Since).Output, Is.EqualTo(1));
    }

    [Test]
    public void Reads_only_appended_lines_and_waits_for_a_partial_line_to_finish()
    {
        var reader = new TranscriptReader(dir);
        System.IO.File.WriteAllText(File(), Line("m1", "2026-10-13T12:00:00Z", 1, 1) + "\n");
        Assert.That(reader.Poll(Since).Single().UsageSince(Since).Output, Is.EqualTo(1));

        var next = Line("m2", "2026-10-13T12:05:00Z", 1, 5);
        System.IO.File.AppendAllText(File(), next[..20]);
        TouchLater(File());
        Assert.That(reader.Poll(Since).Single().UsageSince(Since).Output, Is.EqualTo(1), "half a line isn't counted yet");

        System.IO.File.AppendAllText(File(), next[20..] + "\n");
        TouchLater(File());
        Assert.That(reader.Poll(Since).Single().UsageSince(Since).Output, Is.EqualTo(6));
    }

    [Test]
    public void Merges_subagent_transcripts_into_their_session()
    {
        System.IO.File.WriteAllLines(File(), [Line("m1", "2026-10-13T12:00:00Z", 1, 1)]);
        var sub = Path.Combine(dir, "D--Projects-X", "s1", "subagents");
        Directory.CreateDirectory(sub);
        System.IO.File.WriteAllLines(Path.Combine(sub, "agent-a.jsonl"), [Line("m9", "2026-10-13T12:00:00Z", 1, 9)]);

        var s = new TranscriptReader(dir).Poll(Since).Single();

        Assert.That(s.UsageSince(Since).Output, Is.EqualTo(10));
    }

    // A line as Claude Code writes it with both ids: sessionId names the transcript, session_id the session the line was written in.
    internal static string SessionLine(string id, string ts, int output, string sessionId, string sessionIdField, string? kind = null) =>
        System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["type"] = "assistant", ["timestamp"] = ts, ["sessionId"] = sessionId, ["session_id"] = sessionIdField, ["cwd"] = @"D:\Projects\X",
            ["sessionKind"] = kind,
            ["message"] = new
            {
                id, model = "claude-sonnet-5",
                usage = new { input_tokens = 1, cache_creation_input_tokens = 0, cache_read_input_tokens = 0, output_tokens = output },
                content = new[] { new { type = "tool_use", name = "Bash", input = new { command = "copied" } } },
            },
        }.Where(kv => kv.Value is not null).ToDictionary());

    [Test]
    public void A_background_sessions_copy_of_the_session_it_continues_isnt_counted_again()
    {
        // M.ORCH-T375 (trimmed from a real pair): a CLI session sent to the background goes on in a new transcript that opens
        // with the old one's lines, rewritten with the new sessionId and "sessionKind":"bg" but still carrying the old session_id.
        System.IO.File.WriteAllLines(File(), [
            SessionLine("m1", "2026-10-13T12:00:00Z", 10, "s1", "s1"),
            SessionLine("m2", "2026-10-13T12:01:00Z", 20, "s1", "s1"),
        ]);
        System.IO.File.WriteAllLines(File("bg1.jsonl"), [
            SessionLine("m1", "2026-10-13T12:00:00Z", 10, "bg1", "s1", "bg"),
            SessionLine("m2", "2026-10-13T12:01:00Z", 20, "bg1", "s1", "bg"),
            SessionLine("m3", "2026-10-13T12:05:00Z", 300, "bg1", "bg1", "bg"),
        ]);
        // After a /clear the new transcript's own lines carry the earlier session's session_id: they're real, and counted.
        System.IO.File.WriteAllLines(File("s2.jsonl"), [SessionLine("m4", "2026-10-13T12:06:00Z", 4000, "s2", "s1")]);

        var sessions = new TranscriptReader(dir).Poll(Since).ToDictionary(s => s.SessionId);

        Assert.That(sessions["s1"].UsageSince(Since).Output, Is.EqualTo(30));
        Assert.That(sessions["bg1"].UsageSince(Since).Output, Is.EqualTo(300), "only the background session's own response");
        Assert.That(sessions["bg1"].Events, Has.Count.EqualTo(1));
        Assert.That(sessions["s2"].UsageSince(Since).Output, Is.EqualTo(4000), "a session_id differing from sessionId alone isn't a copy");
    }

    [Test]
    public void A_forks_copy_of_the_parents_line_that_started_it_isnt_counted_again()
    {
        // A fork's transcript opens with a fork-context-ref line, then the parent's Agent call line copied as is (M.ORCH-T323).
        System.IO.File.WriteAllLines(File(), [Line("m1", "2026-10-13T12:00:00Z", 1, 1)]);
        var sub = Path.Combine(dir, "D--Projects-X", "s1", "subagents");
        Directory.CreateDirectory(sub);
        var copy = System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "assistant", timestamp = "2026-10-13T12:00:00Z", agentId = "a",
            message = new
            {
                id = "m1", model = "claude-sonnet-5",
                usage = new { input_tokens = 1, cache_creation_input_tokens = 10, cache_read_input_tokens = 100, output_tokens = 1 },
                content = new[] { new { type = "tool_use", id = "t1", name = "Agent", input = new { prompt = "go" } } },
            },
        });
        System.IO.File.WriteAllLines(Path.Combine(sub, "agent-a.jsonl"), [
            """{"type":"fork-context-ref","agentId":"a","parentSessionId":"s1"}""",
            copy,
            """{"type":"user","timestamp":"2026-10-13T12:00:01Z","sessionId":"s1","cwd":"D:\\Projects\\X","message":{"role":"user","content":"go"}}""",
            Line("m9", "2026-10-13T12:00:02Z", 1, 9),
        ]);

        var s = new TranscriptReader(dir).Poll(Since).Single();

        Assert.That(s.UsageSince(Since), Is.EqualTo(new TokenUsage(2, 20, 200, 10)), "the parent's response once, and the fork's own");
        Assert.That(s.Doing, Is.Null, "the parent's Agent call isn't the fork's");
    }

    [Test]
    public void A_fork_a_subagent_started_doesnt_count_the_subagents_transcript_it_copied()
    {
        // M.ORCH-T348: no fork-context-ref line; the parent subagent's whole transcript is copied, up to the prompt answering
        // the fork's Agent call, which its .meta.json names.
        System.IO.File.WriteAllLines(File(), [Line("m1", "2026-10-13T12:00:00Z", 1, 1)]);
        var sub = Path.Combine(dir, "D--Projects-X", "s1", "subagents");
        Directory.CreateDirectory(sub);
        string Prompt(string text, string? answering = null) => System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "user", timestamp = "2026-10-13T12:00:00Z", sessionId = "s1", cwd = @"D:\Projects\X",
            message = new
            {
                role = "user",
                content = answering is null
                    ? new object[] { new { type = "text", text } }
                    : [new { type = "tool_result", tool_use_id = answering, content = "Fork started" }, new { type = "text", text }],
            },
        });
        var parent = new[] { Prompt("Work on ticket X-T1 (fix): the parent's. When it's done…"), Line("p1", "2026-10-13T12:01:00Z", 1, 50) };
        System.IO.File.WriteAllLines(Path.Combine(sub, "agent-a.jsonl"), parent);
        System.IO.File.WriteAllText(Path.Combine(sub, "agent-f.meta.json"), """{"agentType":"fork","isFork":true,"toolUseId":"toolu_f","parentAgentId":"a"}""");
        System.IO.File.WriteAllLines(Path.Combine(sub, "agent-f.jsonl"), [.. parent, Prompt("<fork-boilerplate>\n</fork-boilerplate>\n\nYour directive: look", "toolu_f"),
            Line("f1", "2026-10-13T12:03:00Z", 1, 7)]);

        var s = new TranscriptReader(dir).Poll(Since).Single();

        Assert.That(s.UsageSince(Since).Output, Is.EqualTo(1 + 50 + 7), "the parent subagent's response once, and the fork's own");
        Assert.That(s.TicketedSubagents.Select(t => t.AgentId), Is.EqualTo(new[] { "agent-a" }), "the copied prompt's ticket isn't the fork's");
    }

    [Test]
    public void A_forks_meta_written_after_its_transcript_was_read_has_the_copy_taken_back_out()
    {
        // M.ORCH-T378: Claude Code writes a fork's .meta.json up to half an hour after its transcript; read before then, the
        // subagent's copied transcript looked like the fork's own.
        System.IO.File.WriteAllLines(File(), [Line("m1", "2026-10-13T12:00:00Z", 1, 1)]);
        var sub = Path.Combine(dir, "D--Projects-X", "s1", "subagents");
        Directory.CreateDirectory(sub);
        string Prompt(string text, string? answering = null) => System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "user", timestamp = "2026-10-13T12:00:00Z", sessionId = "s1", cwd = @"D:\Projects\X",
            message = new
            {
                role = "user",
                content = answering is null
                    ? new object[] { new { type = "text", text } }
                    : [new { type = "tool_result", tool_use_id = answering, content = "Fork started" }, new { type = "text", text }],
            },
        });
        var parent = new[] { Prompt("Work on ticket X-T1 (fix): the parent's. When it's done…"), Line("p1", "2026-10-13T12:01:00Z", 1, 50) };
        System.IO.File.WriteAllLines(Path.Combine(sub, "agent-a.jsonl"), parent);
        System.IO.File.WriteAllText(Path.Combine(sub, "agent-a.meta.json"), """{"agentType":"general-purpose"}""");
        System.IO.File.WriteAllLines(Path.Combine(sub, "agent-f.jsonl"), [.. parent, Prompt("Your directive: look", "toolu_f"),
            Line("f1", "2026-10-13T12:03:00Z", 1, 7)]);
        var written = DateTime.UtcNow.AddMinutes(-5);
        foreach (var f in Directory.GetFiles(dir, "*.jsonl", SearchOption.AllDirectories)) System.IO.File.SetLastWriteTimeUtc(f, written);
        var reader = new TranscriptReader(dir);
        Assert.That(reader.Poll(Since).Single().UsageSince(Since).Output, Is.EqualTo(1 + 50 + 50 + 7), "no meta yet: the copy looks like its own");
        System.IO.File.WriteAllText(Path.Combine(sub, "agent-f.meta.json"), """{"agentType":"fork","isF""");
        Assert.That(reader.Poll(Since).Single().UsageSince(Since).Output, Is.EqualTo(1 + 50 + 50 + 7), "caught half written: read again next time");

        System.IO.File.WriteAllText(Path.Combine(sub, "agent-f.meta.json"), """{"agentType":"fork","isFork":true,"toolUseId":"toolu_f","parentAgentId":"a"}""");
        var s = reader.Poll(Since).Single();

        Assert.That(s.UsageSince(Since).Output, Is.EqualTo(1 + 50 + 7), "once it's there, the parent subagent's response is counted once");
        Assert.That(s.TicketedSubagents.Select(t => t.AgentId), Is.EqualTo(new[] { "agent-a" }), "and the copied prompt's ticket isn't the fork's");
        Assert.That(s.LastWriteUtc, Is.EqualTo(written), "reading it again from the start isn't writing just now: the session stays idle");
    }

    [Test]
    public void A_subagents_ticket_is_its_own_not_its_parent_sessions()
    {
        // /autopilot hands each subagent "Work on ticket X": the parent CLI works many, so it isn't stamped with the first.
        System.IO.File.WriteAllLines(File(), [Line("m1", "2026-10-13T12:00:00Z", 1, 1)]);
        var sub = Path.Combine(dir, "D--Projects-X", "s1", "subagents");
        Directory.CreateDirectory(sub);
        System.IO.File.WriteAllLines(Path.Combine(sub, "agent-a.jsonl"), [
            System.Text.Json.JsonSerializer.Serialize(new { type = "user", timestamp = "2026-10-13T12:00:00Z", sessionId = "s1", cwd = @"D:\Projects\X",
                message = new { role = "user", content = "Work on ticket X-T7 (fix): the thing." } }),
            Line("m9", "2026-10-13T12:00:01Z", 1, 9)]);

        var s = new TranscriptReader(dir).Poll(Since).Single();

        Assert.That((s.UsageSince(Since).Output, s.TicketId), Is.EqualTo((10, (string?)null)), "its usage is the session's, its ticket isn't");
        Assert.That(TranscriptReader.SubagentId(Path.Combine(sub, "agent-a.jsonl")), Is.EqualTo("agent-a"));
        Assert.That(TranscriptReader.SubagentId(File()), Is.Null);
    }

    private static string Call(string id, string toolId, string ts, string command) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "assistant", timestamp = ts, sessionId = "s1", cwd = @"D:\Projects\X",
            message = new
            {
                id, model = "claude-sonnet-5", stop_reason = "tool_use",
                usage = new { input_tokens = 1, cache_creation_input_tokens = 0, cache_read_input_tokens = 0, output_tokens = 1 },
                content = new[] { new { type = "tool_use", id = toolId, name = "PowerShell", input = new { command } } },
            },
        });

    private static string Result(string toolId, string ts, string output) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "user", timestamp = ts, sessionId = "s1", cwd = @"D:\Projects\X",
            message = new { role = "user", content = new[] { new { tool_use_id = toolId, type = "tool_result", content = output, is_error = false } } },
        });

    private static string EndTurn(string id, string ts) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "assistant", timestamp = ts, sessionId = "s1", cwd = @"D:\Projects\X",
            message = new
            {
                id, model = "claude-sonnet-5", stop_reason = "end_turn",
                usage = new { input_tokens = 1, cache_creation_input_tokens = 0, cache_read_input_tokens = 0, output_tokens = 1 },
                content = new[] { new { type = "text", text = "Done." } },
            },
        });

    [Test]
    public void A_ticket_a_transcript_files_is_being_worked_by_it_until_its_turn_ends()
    {
        // M.ORCH-T255: a subagent that hit bugs filed M.ORCH-T250/T251/T253 and fixed them; `next --claim` handed each out again.
        System.IO.File.WriteAllLines(File(), [
            Call("m1", "tu1", "2026-10-13T12:00:00Z", "workflow ticket new --repo X --type fix --summary \"the parent files one too\""),
            Result("tu1", "2026-10-13T12:00:01Z", "X-T8\nX: tickets OK (8)"),
        ]);
        var sub = Path.Combine(dir, "D--Projects-X", "s1", "subagents");
        Directory.CreateDirectory(sub);
        var agent = Path.Combine(sub, "agent-a.jsonl");
        System.IO.File.WriteAllLines(agent, [
            Call("m2", "tu2", "2026-10-13T12:00:00Z", "workflow ticket new --help"),
            Result("tu2", "2026-10-13T12:00:01Z", "USAGE: workflow ticket new [OPTIONS]"),
            Call("m3", "tu3", "2026-10-13T12:01:00Z", @"cd D:\Projects\X; workflow ticket new --repo X --type fix --summary ""a bug"""),
            Result("tu3", "2026-10-13T12:01:01Z", "X-T9"),
            Call("m4", "tu4", "2026-10-13T12:02:00Z", "git status"),
            Result("tu4", "2026-10-13T12:02:01Z", "X-T10"),
        ]);
        var reader = new TranscriptReader(dir, Cache);

        var filing = reader.Poll(Since).Single().Filing;
        Assert.That(filing.Select(f => (f.TicketId, f.AgentId)), Is.EquivalentTo(new[] { ("X-T8", (string?)null), ("X-T9", "agent-a") }),
            "the id each `workflow ticket new` printed, by whoever ran it; not --help's output nor another call's");

        Assert.That(new TranscriptReader(dir, Cache).Poll(Since).Single().Filing, Has.Count.EqualTo(2), "a restarted reader still knows (from its cache)");

        System.IO.File.AppendAllLines(agent, [EndTurn("m5", "2026-10-13T12:03:00Z")]);
        TouchLater(agent);
        Assert.That(reader.Poll(Since).Single().Filing.Select(f => f.TicketId), Is.EqualTo(new[] { "X-T8" }),
            "once the subagent's turn is over, what it filed and left open is the queue's again");
    }

    [Test]
    public void Old_transcripts_still_report_whose_they_are()
    {
        System.IO.File.WriteAllLines(File(), [Line("m1", "2026-09-01T12:00:00Z", 1, 1)]);
        System.IO.File.SetLastWriteTimeUtc(File(), new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc));

        var s = new TranscriptReader(dir).Poll(Since).Single();

        Assert.That(s.Cwd, Is.EqualTo(@"D:\Projects\X"));
        Assert.That(s.UsageSince(Since).Total, Is.Zero);
    }

    [Test]
    public void First_read_keeps_the_files_own_timestamp_but_later_growth_counts_as_now()
    {
        var written = new DateTime(2026, 10, 13, 6, 0, 0, DateTimeKind.Utc);
        System.IO.File.WriteAllLines(File(), [Line("m1", "2026-10-13T06:00:00Z", 1, 1)]);
        System.IO.File.SetLastWriteTimeUtc(File(), written);
        var reader = new TranscriptReader(dir);

        Assert.That(reader.Poll(Since).Single().LastWriteUtc, Is.EqualTo(written), "startup must not make every session look live");

        System.IO.File.AppendAllLines(File(), [Line("m2", "2026-10-13T06:01:00Z", 1, 1)]);
        System.IO.File.SetLastWriteTimeUtc(File(), written); // a stale directory timestamp, as with an open handle
        Assert.That(reader.Poll(Since).Single().LastWriteUtc, Is.GreaterThan(DateTime.UtcNow.AddMinutes(-1)));
    }

    // Filesystem timestamps can be coarse; make sure the reader sees a change.
    private static void TouchLater(string path) =>
        System.IO.File.SetLastWriteTimeUtc(path, System.IO.File.GetLastWriteTimeUtc(path).AddSeconds(1));

    [Test]
    public void A_quiet_transcript_whose_directory_entry_hasnt_moved_isnt_opened_again()
    {
        // Every poll opened all ~900 transcripts of the week to check their length (M.ORCH-T116).
        var hour = DateTime.UtcNow.AddHours(-1);
        System.IO.File.WriteAllLines(File("quiet.jsonl"), [Line("q1", "2026-10-13T12:00:00Z", 1, 10)]);
        System.IO.File.SetLastWriteTimeUtc(File("quiet.jsonl"), hour);
        System.IO.File.WriteAllLines(File("hot.jsonl"), [Line("h1", "2026-10-13T12:00:00Z", 1, 5)]);
        var reader = new TranscriptReader(dir);

        var first = reader.Poll(Since);
        Assert.That(reader.LastPollOpened, Is.EqualTo(2));

        var again = reader.Poll(Since);
        Assert.That(reader.LastPollOpened, Is.EqualTo(1), "only the one written in the last few minutes");
        Assert.That(again, Is.SameAs(first), "nothing new: the same sessions, not rebuilt");
        var version = reader.Version;

        System.IO.File.AppendAllLines(File("quiet.jsonl"), [Line("q2", "2026-10-13T12:05:00Z", 1, 30)]);
        System.IO.File.SetLastWriteTimeUtc(File("quiet.jsonl"), hour); // even with its timestamp put back, its length moved
        var grown = reader.Poll(Since);
        Assert.That(reader.LastPollOpened, Is.EqualTo(2));
        Assert.That(reader.Version, Is.GreaterThan(version));
        Assert.That(grown.Single().UsageSince(Since).Output, Is.EqualTo(45));
    }
}
