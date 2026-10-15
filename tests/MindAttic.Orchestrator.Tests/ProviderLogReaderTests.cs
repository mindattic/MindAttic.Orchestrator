using MindAttic.Orchestrator.Activity;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class ProviderLogReaderTests
{
    private string dir = "";

    [SetUp]
    public void SetUp() => dir = Path.Combine(Path.GetTempPath(), "orch-prov-" + Guid.NewGuid().ToString("N"));

    [TearDown]
    public void TearDown() => Directory.Delete(dir, recursive: true);

    private static string Ts(DateTime utc) => utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

    [Test]
    public void Codex_turns_count_once_with_cached_input_split_out_and_its_own_limits_kept()
    {
        var now = DateTime.UtcNow;
        var day = Path.Combine(dir, "codex", now.ToLocalTime().ToString("yyyy"), now.ToLocalTime().ToString("MM"), now.ToLocalTime().ToString("dd"));
        Directory.CreateDirectory(day);
        string Count(long total, long input, long cached, long output) =>
            "{\"timestamp\":\"" + Ts(now) + "\",\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"info\":{\"total_token_usage\":{\"total_tokens\":" + total +
            "},\"last_token_usage\":{\"input_tokens\":" + input + ",\"cached_input_tokens\":" + cached + ",\"cache_write_input_tokens\":0,\"output_tokens\":" + output +
            "}},\"rate_limits\":{\"primary\":{\"used_percent\":12.5,\"window_minutes\":300,\"resets_at\":" + new DateTimeOffset(now.AddHours(2)).ToUnixTimeSeconds() + "}}}}";
        File.WriteAllLines(Path.Combine(day, "rollout-a.jsonl"), [
            "{\"timestamp\":\"" + Ts(now) + "\",\"type\":\"session_meta\",\"payload\":{\"id\":\"s1\",\"cwd\":\"D:\\\\W\\\\Repo\"}}",
            Count(1000, 900, 600, 100),
            Count(1000, 900, 600, 100), // a repeat with no new turn
            Count(1500, 450, 400, 50),
        ]);

        var reader = new ProviderLogReader(Path.Combine(dir, "codex"), Path.Combine(dir, "gemini"));
        var s = reader.Poll(now.AddDays(-1)).Single();

        Assert.That((s.Provider, s.Cwd, s.SessionId), Is.EqualTo((Providers.Codex, @"D:\W\Repo", "s1")));
        Assert.That(s.Events, Has.Count.EqualTo(2));
        Assert.That(s.UsageSince(DateTime.MinValue), Is.EqualTo(new TokenUsage(350, 0, 1000, 150)), "uncached input = input - cached");
        Assert.That(reader.Limits.Single(), Has.Property("Window").EqualTo("5h").And.Property("UsedPercent").EqualTo(12.5));
    }

    private static string CodexCount(DateTime at, long total, long input) =>
        "{\"timestamp\":\"" + Ts(at) + "\",\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"info\":{\"total_token_usage\":{\"total_tokens\":" + total +
        "},\"last_token_usage\":{\"input_tokens\":" + input + ",\"cached_input_tokens\":0,\"cache_write_input_tokens\":0,\"output_tokens\":0}}}}";

    private static string CodexMeta(DateTime at) =>
        "{\"timestamp\":\"" + Ts(at) + "\",\"type\":\"session_meta\",\"payload\":{\"id\":\"s1\",\"cwd\":\"D:\\\\W\\\\Repo\"}}";

    [Test]
    public void A_second_poll_reads_only_what_was_appended_and_waits_for_an_unfinished_line()
    {
        var now = DateTime.UtcNow;
        var day = Path.Combine(dir, "codex", now.ToLocalTime().ToString("yyyy"), now.ToLocalTime().ToString("MM"), now.ToLocalTime().ToString("dd"));
        Directory.CreateDirectory(day);
        var file = Path.Combine(day, "rollout-a.jsonl");
        File.WriteAllText(file, CodexMeta(now) + "\n" + CodexCount(now, 100, 100) + "\n");
        var reader = new ProviderLogReader(Path.Combine(dir, "codex"), Path.Combine(dir, "gemini"));
        Assert.That(reader.Poll(now.AddDays(-1)).Single().Events, Has.Count.EqualTo(1));

        File.AppendAllText(file, CodexCount(now, 300, 200) + "\n" + CodexCount(now, 600, 300)); // the last line isn't finished
        Assert.That(reader.Poll(now.AddDays(-1)).Single().Events, Has.Count.EqualTo(2), "only the new complete line; nothing counted twice");
        File.AppendAllText(file, "\n");
        Assert.That(reader.Poll(now.AddDays(-1)).Single().UsageSince(DateTime.MinValue).Input, Is.EqualTo(600), "100 + 200 + 300, once each");

        File.WriteAllText(file, CodexMeta(now) + "\n" + CodexCount(now, 50, 50) + "\n"); // rewritten shorter
        Assert.That(reader.Poll(now.AddDays(-1)).Single().UsageSince(DateTime.MinValue).Input, Is.EqualTo(50), "a file that shrank is read from scratch");

        File.Delete(file);
        Assert.That(reader.Poll(now.AddDays(-1)), Is.Empty, "a deleted file drops out");
    }

    [Test]
    public void Codex_days_outside_the_window_and_gemini_chats_without_a_project_root_are_skipped()
    {
        var now = DateTime.UtcNow;
        var old = now.AddDays(-10);
        var oldDay = Path.Combine(dir, "codex", old.ToLocalTime().ToString("yyyy"), old.ToLocalTime().ToString("MM"), old.ToLocalTime().ToString("dd"));
        Directory.CreateDirectory(oldDay);
        File.WriteAllLines(Path.Combine(oldDay, "rollout-old.jsonl"), [CodexMeta(old), CodexCount(old, 100, 100)]);
        var chats = Path.Combine(dir, "gemini", "noroot", "chats");
        Directory.CreateDirectory(chats);
        File.WriteAllLines(Path.Combine(chats, "session-1.jsonl"),
            ["{\"id\":\"r1\",\"timestamp\":\"" + Ts(now) + "\",\"type\":\"gemini\",\"tokens\":{\"input\":800,\"output\":20,\"cached\":300,\"thoughts\":30}}"]);

        var reader = new ProviderLogReader(Path.Combine(dir, "codex"), Path.Combine(dir, "gemini"));
        Assert.That(reader.Poll(now.AddDays(-1)), Is.Empty, "a 10-day-old Codex folder and a Gemini chat with no .project_root");
        Assert.That(reader.Poll(now.AddDays(-30)).Select(s => s.Provider), Is.EqualTo(new[] { Providers.Codex }), "a wider window: a fresh read finds the old day");
    }

    [Test]
    public void Gemini_replies_count_with_their_project_root_and_thoughts_as_output()
    {
        var now = DateTime.UtcNow;
        var chats = Path.Combine(dir, "gemini", "proj", "chats");
        Directory.CreateDirectory(chats);
        File.WriteAllText(Path.Combine(dir, "gemini", "proj", ".project_root"), @"d:\w\repo" + "\n");
        var reply = "{\"id\":\"r1\",\"timestamp\":\"" + Ts(now) + "\",\"type\":\"gemini\",\"tokens\":{\"input\":800,\"output\":20,\"cached\":300,\"thoughts\":30}}";
        File.WriteAllLines(Path.Combine(chats, "session-1.jsonl"), ["{\"type\":\"user\",\"timestamp\":\"" + Ts(now) + "\"}", reply, reply]);

        var s = new ProviderLogReader(Path.Combine(dir, "codex"), Path.Combine(dir, "gemini")).Poll(now.AddDays(-1)).Single();

        Assert.That((s.Provider, s.Cwd), Is.EqualTo((Providers.Gemini, @"d:\w\repo")));
        Assert.That(s.UsageSince(DateTime.MinValue), Is.EqualTo(new TokenUsage(500, 0, 300, 50)), "the repeated reply counts once");
    }
}
