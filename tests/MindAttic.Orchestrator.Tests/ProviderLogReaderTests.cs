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
