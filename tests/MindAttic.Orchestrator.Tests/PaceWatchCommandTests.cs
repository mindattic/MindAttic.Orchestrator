using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>Live gate activity: the pace gate's calculated delay right now, and its own CLI (<c>orchestrator pace-watch</c>)
/// that makes it externally visible.</summary>
public sealed class PaceWatchCommandTests
{
    private readonly TempDirs temp = new();

    [TearDown]
    public void TearDown() => temp.DeleteAll();

    [TestCase(0, "0ms")]
    [TestCase(1, "1ms")]
    [TestCase(999, "999ms")]
    [TestCase(1000, "1.0s")]
    [TestCase(2500, "2.5s")]
    public void A_delay_reads_in_milliseconds_under_a_second_and_seconds_past_it(double ms, string text) =>
        Assert.That(PaceWatchCommand.Format(ms), Is.EqualTo(text));

    [Test]
    public void No_number_at_all_says_so_rather_than_a_bare_zero() =>
        Assert.That(PaceWatchCommand.Format(null), Is.EqualTo("unknown (no daemon yet)"));

    [Test]
    public void The_current_delay_is_rl_status_json_s_pace_delay_seconds_in_milliseconds()
    {
        var path = Path.Combine(temp.New(), "rl-status.json");
        File.WriteAllText(path, """{"timestamp":"2026-10-25T00:00:00Z","pace_delay_seconds":1.25}""");
        Assert.That(PaceWatchCommand.CurrentDelayMs(path), Is.EqualTo(1250));
    }

    [Test]
    public void No_pace_delay_field_reads_as_zero_not_unknown()
    {
        var path = Path.Combine(temp.New(), "rl-status.json");
        File.WriteAllText(path, """{"timestamp":"2026-10-25T00:00:00Z"}""");
        Assert.That(PaceWatchCommand.CurrentDelayMs(path), Is.EqualTo(0));
    }

    [Test]
    public void A_file_that_isn_t_there_yet_reads_as_unknown_not_a_crash() =>
        Assert.That(PaceWatchCommand.CurrentDelayMs(Path.Combine(temp.New(), "gone.json")), Is.Null);
}
