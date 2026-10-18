using System.Diagnostics;
using System.Text.Json;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>What the hooks rely on in <c>rl-status.json</c> (M.ORCH-T126).</summary>
public sealed class RateLimitStatusWriterTests
{
    private static readonly DateTime Day = new(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T0 = new(2026, 10, 13, 10, 30, 0, DateTimeKind.Utc);

    private string path = "";

    [SetUp]
    public void SetUp() => path = Path.Combine(Path.GetTempPath(), "rl-" + Guid.NewGuid().ToString("N") + ".json");

    [TearDown]
    public void TearDown()
    {
        File.Delete(path);
        File.Delete(path + ".tmp");
    }

    private static BudgetStatus Budgeted(long spent = 400) =>
        BudgetCalculator.Compute([new UsageEvent(Day.AddHours(10), new TokenUsage(0, 0, 1_000_000, spent))],
            new OrchestratorSettings { BlockBudget = 1000 }, Day.AddHours(11), Day);

    private static BudgetStatus PeakOnly() =>
        BudgetCalculator.Compute([new UsageEvent(Day.AddHours(10), new TokenUsage(0, 0, 1_000_000, 1))],
            new OrchestratorSettings(), Day.AddHours(11), Day);

    private static Pace Paced(PaceMode mode = PaceMode.Throttled) =>
        new(mode, 2, 2000, 3000, 1000, "5-hour", Day.AddHours(15), "Throttled.", "Conserve.");

    private static Pace Unpaced() => new(PaceMode.Unpaced, 4, 0, 0, null, null, null, "off");

    private JsonElement Read()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.Clone();
    }

    private static string Python(string code, string arg)
    {
        var psi = new ProcessStartInfo("python") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var a in new[] { "-I", "-c", code, arg }) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var output = p.StandardOutput.ReadToEnd();
        var error = p.StandardError.ReadToEnd();
        p.WaitForExit();
        Assert.That(p.ExitCode, Is.Zero, error);
        return output.Trim();
    }

    [Test]
    public void The_timestamp_parses_both_ways_the_hooks_read_it()
    {
        var now = new DateTime(2026, 10, 16, 14, 5, 9, DateTimeKind.Utc);
        new RateLimitStatusWriter(path) { UtcNow = () => now }.Write(Budgeted());
        var stamp = Read().GetProperty("timestamp").GetString()!;
        Assert.That(stamp, Is.EqualTo("2026-10-16T14:05:09Z"));

        // context-gate.py / pace-gate.py: an aware UTC datetime.
        Assert.That(Python("import sys; from datetime import datetime; print(datetime.fromisoformat(sys.argv[1].replace('Z', '+00:00')).timestamp())", stamp),
            Is.EqualTo(new DateTimeOffset(now).ToUnixTimeSeconds().ToString() + ".0"));
        // rl-check.py / pace-gate.py: a naive datetime compared with utcnow().
        Assert.That(Python("import sys; from datetime import datetime; print(datetime.fromisoformat(sys.argv[1].replace('Z', '')).isoformat())", stamp),
            Is.EqualTo("2026-10-16T14:05:09"), "naive, and the same wall time as utcnow() would give");
    }

    /// <summary>2026-10-17 04:47-04:52Z (M.ORCH-T205): with dozens of held calls each re-reading the file, the daemon's
    /// one-shot replace kept landing while a gate had it open (Python opens without delete sharing), failed in silence,
    /// and the file went stale for minutes while the daemon kept ticking.</summary>
    [Test]
    public void A_write_while_a_gate_has_the_file_open_waits_for_it_and_a_write_that_never_lands_is_reported()
    {
        var now = T0;
        var writer = new RateLimitStatusWriter(path) { UtcNow = () => now, ReplaceRetry = TimeSpan.FromSeconds(1) };
        writer.Write(Budgeted(), Paced() with { AgentDelaySeconds = 40 });

        // A gate reading the file (read and write sharing, no delete) for a moment: the write waits it out and lands.
        var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var closing = Task.Run(async () => { await Task.Delay(300); reader.Dispose(); });
        now = T0.AddSeconds(20);
        writer.Write(Budgeted(500), Paced() with { AgentDelaySeconds = 60 });
        closing.Wait();
        Assert.That(Read().GetProperty("timestamp").GetString(), Is.EqualTo("2026-10-13T10:30:20Z"), "written once the reader let go");
        Assert.That(writer.FailingSinceUtc, Is.Null);

        // Held open past every retry: not written, and said so rather than swallowed.
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            now = T0.AddSeconds(40);
            writer.Write(Budgeted(600), Paced() with { AgentDelaySeconds = 80 });
            Assert.That(writer.FailingSinceUtc, Is.EqualTo(now));
            Assert.That(writer.LastError, Is.Not.Null.And.Not.Empty);
            now = T0.AddSeconds(60);
            writer.Write(Budgeted(600), Paced() with { AgentDelaySeconds = 80 });
            Assert.That(writer.FailingSinceUtc, Is.EqualTo(T0.AddSeconds(40)), "failing since the first that failed");
        }
        now = T0.AddSeconds(80);
        writer.Write(Budgeted(600), Paced() with { AgentDelaySeconds = 80 });
        Assert.That((writer.FailingSinceUtc, writer.LastError), Is.EqualTo(((DateTime?)null, (string?)null)), "the next write that lands clears it");
        Assert.That(Read().GetProperty("pace_delay_seconds").GetDouble(), Is.EqualTo(80));
    }

    [Test]
    public void The_file_says_when_its_window_resets_so_the_gates_hold_at_its_pace_while_it_is_stale()
    {
        new RateLimitStatusWriter(path) { UtcNow = () => T0 }.Write(Budgeted(), Paced() with { AgentDelaySeconds = 40 });
        Assert.That(Read().GetProperty("window_reset").GetString(), Is.EqualTo("2026-10-13T15:00:00Z"), "the paced window's reset");

        // The daemon reading its own file back after a stall: the stale minutes were held at that pace, not unheld.
        var window = BudgetCalculator.ControlWindow.TotalMinutes;
        Assert.That(new RateLimitStatusWriter(path).AgentDelay(T0.AddMinutes(10)), Is.EqualTo(40 * (1 - Math.Exp(-10 / window))).Within(1e-9),
            "ten stale minutes inside the window count as held at 40s");
    }

    [Test]
    public void The_file_is_rewritten_at_least_once_a_minute_when_nothing_changed()
    {
        var now = T0;
        var writer = new RateLimitStatusWriter(path) { UtcNow = () => now };
        writer.Write(Budgeted());
        Assert.That(Read().GetProperty("timestamp").GetString(), Is.EqualTo("2026-10-13T10:30:00Z"));

        now = T0.AddSeconds(30);
        writer.Write(Budgeted());
        Assert.That(Read().GetProperty("timestamp").GetString(), Is.EqualTo("2026-10-13T10:30:00Z"), "unchanged within the minute: not rewritten");

        now = T0.AddSeconds(45);
        writer.Write(Budgeted(500));
        Assert.That(Read().GetProperty("timestamp").GetString(), Is.EqualTo("2026-10-13T10:30:45Z"), "a change is written at once");

        now = T0.AddSeconds(45 + 60);
        writer.Write(Budgeted(500));
        Assert.That(Read().GetProperty("timestamp").GetString(), Is.EqualTo("2026-10-13T10:31:45Z"), "a minute on, refreshed though nothing changed");
    }

    [Test]
    public void Subagent_fields_are_omitted_when_unpaced()
    {
        var writer = new RateLimitStatusWriter(path) { UtcNow = () => T0 };
        writer.Write(Budgeted(), Unpaced() with { SubagentAllowance = 3, Subagents = 2 });
        var root = Read();
        foreach (var key in new[] { "subagent_allowance", "subagents_running", "subagent_note", "pace", "pace_note", "wind_down_note" })
            Assert.That(root.TryGetProperty(key, out _), Is.False, key);

        writer.Write(Budgeted(), Paced() with { SubagentAllowance = null });
        Assert.That(Read().TryGetProperty("subagent_allowance", out _), Is.False, "paced with no allowance: nothing held");

        writer.Write(Budgeted(), Paced() with { SubagentAllowance = 3, Subagents = 2 });
        root = Read();
        Assert.That((root.GetProperty("subagent_allowance").GetInt32(), root.GetProperty("subagents_running").GetInt32()), Is.EqualTo((3, 2)));
        Assert.That(root.GetProperty("subagent_note").GetString(), Is.Not.Empty);
    }

    /// <summary>M.ORCH-T320: the gates added marks newer than the whole-second timestamp, which stays up to a minute old
    /// while the count doesn't change, so a mark the daemon had counted could be counted again.</summary>
    [Test]
    public void The_newest_mark_the_count_saw_is_written_to_the_file_times_precision_and_a_new_one_is_written_at_once()
    {
        var mark = path + ".mark";
        try
        {
            File.WriteAllText(mark, "");
            File.SetLastWriteTimeUtc(mark, T0.AddTicks(1_234_567)); // a fraction of a second past T0
            var seen = File.GetLastWriteTimeUtc(mark);
            var now = T0.AddSeconds(1);
            var writer = new RateLimitStatusWriter(path) { UtcNow = () => now };
            writer.Write(Budgeted(), Paced() with { SubagentAllowance = 3, Subagents = 1, SubagentsSeenThroughUtc = seen });
            var ns = Read().GetProperty("subagents_seen_through_ns").GetInt64();
            Assert.That(Python("import os,sys; print(os.stat(sys.argv[1]).st_mtime_ns)", mark), Is.EqualTo(ns.ToString()),
                "the gates compare st_mtime_ns: the mark it saw is exactly as new, so not counted again");

            // One subagent stopped and another started: the same count, but the new mark is seen, so it's written now.
            now = T0.AddSeconds(10);
            writer.Write(Budgeted(), Paced() with { SubagentAllowance = 3, Subagents = 1, SubagentsSeenThroughUtc = seen.AddSeconds(5) });
            Assert.That(Read().GetProperty("timestamp").GetString(), Is.EqualTo("2026-10-13T10:30:10Z"), "not left a minute old");
            Assert.That(Read().GetProperty("subagents_seen_through_ns").GetInt64(), Is.EqualTo(ns + 5_000_000_000L));

            writer.Write(Budgeted(), Paced() with { SubagentAllowance = 3, Subagents = 0 });
            Assert.That(Read().GetProperty("subagents_seen_through_ns").GetInt64(), Is.Zero, "no marks seen: every mark there is new");
        }
        finally
        {
            File.Delete(mark);
        }
    }

    [Test]
    public void Pace_delay_seconds_is_omitted_at_zero()
    {
        var writer = new RateLimitStatusWriter(path) { UtcNow = () => T0 };
        writer.Write(Budgeted());
        Assert.That(Read().TryGetProperty("pace_delay_seconds", out _), Is.False, "no pace at all");

        writer.Write(Budgeted(), Paced() with { AgentDelaySeconds = 0 });
        var root = Read();
        Assert.That((root.TryGetProperty("pace_delay_seconds", out _), root.TryGetProperty("pace_delay_avg", out _)), Is.EqualTo((false, false)));

        writer.Write(Budgeted(), Paced() with { AgentDelaySeconds = 20 });
        Assert.That(Read().GetProperty("pace_delay_seconds").GetDouble(), Is.EqualTo(20));
    }

    [Test]
    public void Stop_and_wind_down_note_are_never_written_together()
    {
        var writer = new RateLimitStatusWriter(path) { UtcNow = () => T0 };
        foreach (var pace in new[] { Paced(PaceMode.Over), Paced(PaceMode.Throttled), Unpaced() })
        {
            writer.Write(Budgeted(), pace with { Stop = true, WindDown = true });
            var root = Read();
            Assert.That(root.GetProperty("stop").GetBoolean(), Is.True, pace.Mode.ToString());
            Assert.That(root.TryGetProperty("wind_down_note", out _), Is.False, pace.Mode + ": stopped, so there's nothing to wind down");
        }

        writer.Write(Budgeted(), Paced() with { WindDown = true });
        var wound = Read();
        Assert.That((wound.TryGetProperty("stop", out _), wound.GetProperty("wind_down_note").GetString()), Is.EqualTo((false, (Paced() with { WindDown = true }).WindDownNote)));
    }

    [Test]
    public void A_weekly_stop_tells_a_refused_prompt_its_own_window_and_reset_not_the_5_hour_blocks()
    {
        var writer = new RateLimitStatusWriter(path) { UtcNow = () => T0 };
        var weeklyReset = Day.AddDays(3).AddHours(9);
        writer.Write(Budgeted(), Paced(PaceMode.Over) with { Stop = true, Governing = "weekly", ResetUtc = weeklyReset });
        var root = Read();
        Assert.That((root.GetProperty("stop_window").GetString(), root.GetProperty("reset_time").GetString()),
            Is.EqualTo(("weekly", weeklyReset.ToLocalTime().ToString("ddd h:mm tt", System.Globalization.CultureInfo.InvariantCulture))));
    }

    [Test]
    public void A_stop_with_no_known_5_hour_limit_still_reaches_the_pace_gate()
    {
        // A weekly wall while no 5-hour limit is known: the pace says stop, and the gate only hears it from this file.
        var writer = new RateLimitStatusWriter(path) { UtcNow = () => T0 };
        writer.Write(PeakOnly(), Paced(PaceMode.Over) with { Stop = true, Governing = "weekly" });
        var root = Read();
        Assert.That((root.GetProperty("stop").GetBoolean(), root.GetProperty("source").GetString()), Is.EqualTo((true, "MindAttic.Orchestrator")));
        Assert.That(root.GetProperty("stop_note").GetString(), Does.Contain("weekly"));
        Assert.That(root.TryGetProperty("tokens_remaining", out _), Is.False, "no limit known: no count against one");
        Assert.That(root.GetProperty("reset_time").GetString(), Is.EqualTo(Day.AddHours(15).ToLocalTime().ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture)),
            "the stop's own reset");

        writer.Write(PeakOnly(), Paced());
        Assert.That(File.Exists(path), Is.False, "the stop lifted: withdrawn again");
    }

    [Test]
    public void A_peak_only_limit_removes_its_own_file_and_leaves_anyone_elses()
    {
        var writer = new RateLimitStatusWriter(path) { UtcNow = () => T0 };
        writer.Write(Budgeted());
        Assert.That(File.Exists(path), Is.True);

        writer.Write(PeakOnly());
        Assert.That(File.Exists(path), Is.False, "ours: withdrawn, so no stale numbers are left behind");

        writer.Write(Budgeted());
        Assert.That(File.Exists(path), Is.True, "the same status as before is written again at once once a limit is back");

        File.WriteAllText(path, """{"percent_used": 5, "source": "someone else"}""");
        writer.Write(PeakOnly());
        Assert.That(File.ReadAllText(path), Does.Contain("someone else"), "not ours: left alone");
    }

    [Test]
    public void The_agent_delay_average_decays_toward_the_current_hold()
    {
        var now = T0;
        var writer = new RateLimitStatusWriter(path) { UtcNow = () => now };
        var window = BudgetCalculator.ControlWindow.TotalMinutes;

        writer.Write(Budgeted(), Paced() with { AgentDelaySeconds = 40 });
        Assert.That(writer.AgentDelay(now), Is.Zero.Within(1e-9), "just set: nothing has been held yet");
        var last = 0.0;
        for (var step = 1; step <= 6; step++)
        {
            var before = writer.AgentDelay(now.AddMinutes(2));
            Assert.That(before, Is.EqualTo(last + (40 - last) * (1 - Math.Exp(-2 / window))).Within(1e-9),
                $"step {step}: two minutes of a 40s hold move the average that share of the way to it");
            Assert.That(before, Is.GreaterThan(last).And.LessThan(40), $"step {step}: approached, never passed");
            now = now.AddMinutes(2);
            writer.Write(Budgeted(), Paced() with { AgentDelaySeconds = 40 });
            last = Read().GetProperty("pace_delay_avg").GetDouble();
            Assert.That(last, Is.EqualTo(Math.Round(before, 1)));
        }
        var held = writer.AgentDelay(now);
        Assert.That(held, Is.GreaterThan(20).And.LessThan(40));

        // The hold comes off: the average falls toward 0, and is still written while it's worth correcting by.
        writer.Write(Budgeted(), Paced() with { AgentDelaySeconds = 0 });
        var falling = writer.AgentDelay(now.AddMinutes(2));
        Assert.That(falling, Is.EqualTo(last * Math.Exp(-2 / window)).Within(1e-9));
        Assert.That(falling, Is.LessThan(held));
        now = now.AddMinutes(2);
        writer.Write(Budgeted(), Paced() with { AgentDelaySeconds = 0 });
        var root = Read();
        Assert.That((root.TryGetProperty("pace_delay_seconds", out _), root.GetProperty("pace_delay_avg").GetDouble()), Is.EqualTo((false, Math.Round(falling, 1))));

        // Nobody refreshes the file: past the gate's stale point the agents weren't held, so it decays to nothing.
        Assert.That(writer.AgentDelay(now.AddHours(3)), Is.LessThan(0.01));
    }
}
