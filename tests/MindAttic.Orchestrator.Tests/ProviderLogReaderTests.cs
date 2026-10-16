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

    private string CodexDay(DateTime utc)
    {
        var local = utc.ToLocalTime();
        var day = Path.Combine(dir, "codex", local.ToString("yyyy"), local.ToString("MM"), local.ToString("dd"));
        Directory.CreateDirectory(day);
        return day;
    }

    private string GeminiProject(string name, string? root)
    {
        var project = Path.Combine(dir, "gemini", name);
        Directory.CreateDirectory(Path.Combine(project, "chats"));
        if (root is not null) File.WriteAllText(Path.Combine(project, ".project_root"), root + "\n");
        return project;
    }

    private static string GeminiReply(DateTime at, string id, long input) =>
        "{\"id\":\"" + id + "\",\"timestamp\":\"" + Ts(at) + "\",\"type\":\"gemini\",\"tokens\":{\"input\":" + input + ",\"output\":0,\"cached\":0,\"thoughts\":0}}";

    private ProviderLogReader Reader() => new(Path.Combine(dir, "codex"), Path.Combine(dir, "gemini"));

    [Test]
    public void A_truncated_gemini_chat_starts_over_and_its_ids_count_again()
    {
        var now = DateTime.UtcNow;
        var file = Path.Combine(GeminiProject("proj", @"d:\w\repo"), "chats", "session-1.jsonl");
        File.WriteAllLines(file, [GeminiReply(now, "r1", 100), GeminiReply(now, "r2", 200)]);
        var reader = Reader();
        Assert.That(reader.Poll(now.AddDays(-1)).Single().UsageSince(DateTime.MinValue).Input, Is.EqualTo(300));

        File.WriteAllLines(file, [GeminiReply(now, "r1", 40)]); // rewritten shorter, reusing an id
        var s = reader.Poll(now.AddDays(-1)).Single();
        Assert.That(s.Events, Has.Count.EqualTo(1), "the old events are dropped");
        Assert.That(s.UsageSince(DateTime.MinValue).Input, Is.EqualTo(40), "r1 is counted again from the new file, not skipped as seen");
    }

    [Test]
    public void A_truncated_codex_log_starts_over_with_its_running_total_reset()
    {
        var now = DateTime.UtcNow;
        var file = Path.Combine(CodexDay(now), "rollout-a.jsonl");
        File.WriteAllLines(file, [CodexMeta(now), CodexCount(now, 5000, 100), CodexCount(now, 9000, 200)]);
        var reader = Reader();
        Assert.That(reader.Poll(now.AddDays(-1)).Single().UsageSince(DateTime.MinValue).Input, Is.EqualTo(300));

        File.WriteAllLines(file, [CodexMeta(now), CodexCount(now, 70, 70)]); // a total far below the old one
        Assert.That(reader.Poll(now.AddDays(-1)).Single().UsageSince(DateTime.MinValue).Input, Is.EqualTo(70),
            "the smaller running total counts, since the old one was forgotten with the file");
    }

    [Test]
    public void A_codex_token_count_repeated_with_the_same_total_in_a_later_poll_is_not_counted_twice()
    {
        var now = DateTime.UtcNow;
        var file = Path.Combine(CodexDay(now), "rollout-a.jsonl");
        File.WriteAllLines(file, [CodexMeta(now), CodexCount(now, 1000, 100)]);
        var reader = Reader();
        reader.Poll(now.AddDays(-1));

        File.AppendAllLines(file, [CodexCount(now, 1000, 100), CodexCount(now, 900, 100)]); // a repeat, and a lower total
        var s = reader.Poll(now.AddDays(-1)).Single();
        Assert.That(s.Events, Has.Count.EqualTo(1));
        Assert.That(s.UsageSince(DateTime.MinValue).Input, Is.EqualTo(100));

        File.AppendAllLines(file, [CodexCount(now, 1200, 25)]);
        Assert.That(reader.Poll(now.AddDays(-1)).Single().UsageSince(DateTime.MinValue).Input, Is.EqualTo(125), "a real new turn still counts");
    }

    [Test]
    public void A_gemini_message_id_repeated_in_a_later_poll_counts_once()
    {
        var now = DateTime.UtcNow;
        var file = Path.Combine(GeminiProject("proj", @"d:\w\repo"), "chats", "session-1.jsonl");
        File.WriteAllLines(file, [GeminiReply(now, "r1", 100)]);
        var reader = Reader();
        reader.Poll(now.AddDays(-1));

        File.AppendAllLines(file, [GeminiReply(now, "r1", 100), GeminiReply(now, "r2", 7), GeminiReply(now, "r2", 7)]);
        var s = reader.Poll(now.AddDays(-1)).Single();
        Assert.That(s.Events, Has.Count.EqualTo(2));
        Assert.That(s.UsageSince(DateTime.MinValue).Input, Is.EqualTo(107));
    }

    [Test]
    public void A_gemini_project_whose_chats_folder_is_gone_mid_poll_is_skipped_and_the_others_still_count()
    {
        var now = DateTime.UtcNow;
        File.WriteAllLines(Path.Combine(GeminiProject("good", @"d:\w\good"), "chats", "session-1.jsonl"), [GeminiReply(now, "r1", 100)]);
        // A chats folder that passes Directory.Exists but whose listing fails: a junction to a folder since removed.
        var gone = Path.Combine(dir, "gone-target");
        Directory.CreateDirectory(gone);
        var broken = Path.Combine(dir, "gemini", "broken");
        Directory.CreateDirectory(broken);
        File.WriteAllText(Path.Combine(broken, ".project_root"), @"d:\w\broken");
        var link = Path.Combine(broken, "chats");
        using (var mk = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{gone}\"")
               { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true })!)
            mk.WaitForExit();
        Directory.Delete(gone);
        try
        {
            Assume.That(Directory.Exists(link), "the dangling junction still looks like a folder");
            Assert.Throws<DirectoryNotFoundException>(() => Directory.GetFiles(link, "*.jsonl"), "listing it fails, as a folder removed mid-poll does");

            IReadOnlyList<TranscriptSession> sessions = [];
            Assert.DoesNotThrow(() => sessions = Reader().Poll(now.AddDays(-1)));
            Assert.That(sessions.Select(s => s.Cwd), Is.EqualTo(new[] { @"d:\w\good" }));
        }
        finally { if (Directory.Exists(link)) Directory.Delete(link); } // removes the junction itself

    }

    [Test]
    public void A_gemini_project_whose_project_root_is_locked_is_skipped_without_failing_the_poll()
    {
        var now = DateTime.UtcNow;
        File.WriteAllLines(Path.Combine(GeminiProject("good", @"d:\w\good"), "chats", "session-1.jsonl"), [GeminiReply(now, "r1", 100)]);
        var locked = GeminiProject("locked", @"d:\w\locked");
        File.WriteAllLines(Path.Combine(locked, "chats", "session-1.jsonl"), [GeminiReply(now, "r9", 900)]);

        IReadOnlyList<TranscriptSession> sessions = [];
        using (new FileStream(Path.Combine(locked, ".project_root"), FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.DoesNotThrow(() => sessions = Reader().Poll(now.AddDays(-1)));
        Assert.That(sessions.Select(s => s.Cwd), Is.EqualTo(new[] { @"d:\w\good" }), "with no readable cwd the locked project's chat isn't counted");
    }

    private static string CodexLimits(DateTime at, double fiveHour, double week, DateTime fiveHourReset, DateTime weekReset) =>
        "{\"timestamp\":\"" + Ts(at) + "\",\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"info\":null,\"rate_limits\":{" +
        "\"primary\":{\"used_percent\":" + fiveHour.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"window_minutes\":300,\"resets_at\":" + new DateTimeOffset(fiveHourReset).ToUnixTimeSeconds() + "}," +
        "\"secondary\":{\"used_percent\":" + week.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"window_minutes\":10080,\"resets_at\":" + new DateTimeOffset(weekReset).ToUnixTimeSeconds() + "}}}}";

    [Test]
    public void Codex_five_hour_and_weekly_percentages_become_limits_and_the_latest_report_wins()
    {
        var now = DateTime.UtcNow;
        var fiveReset = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, DateTimeKind.Utc).AddHours(3);
        var weekReset = fiveReset.AddDays(4);
        var day = CodexDay(now);
        File.WriteAllLines(Path.Combine(day, "rollout-a.jsonl"), [CodexMeta(now), CodexLimits(now.AddMinutes(-5), 30.5, 61, fiveReset, weekReset)]);
        // Another session's older report mustn't replace the newer one, whichever file is read first.
        File.WriteAllLines(Path.Combine(day, "rollout-b.jsonl"), [CodexMeta(now), CodexLimits(now.AddMinutes(-50), 10, 20, fiveReset, weekReset)]);

        var reader = Reader();
        reader.Poll(now.AddDays(-1));
        Assert.That(reader.Limits.OrderBy(l => l.Window).Select(l => (l.Provider, l.Window, l.UsedPercent, l.ResetsUtc)), Is.EqualTo(new[]
        {
            (Providers.Codex, "5h", 30.5, fiveReset),
            (Providers.Codex, "week", 61.0, weekReset),
        }));

        File.AppendAllLines(Path.Combine(day, "rollout-b.jsonl"), [CodexLimits(now, 44, 70, fiveReset, weekReset)]);
        reader.Poll(now.AddDays(-1));
        Assert.That(reader.Limits.OrderBy(l => l.Window).Select(l => l.UsedPercent), Is.EqualTo(new[] { 44.0, 70.0 }), "a newer report replaces them");
    }
}
