using MindAttic.Orchestrator.Health;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T2057: the health lines learn from the probes' history, between the configured line and its bound.</summary>
public sealed class HealthLearningTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    private static IEnumerable<double> Many(double value, int count = 100) => Enumerable.Repeat(value, count);

    [Test]
    public void A_line_the_normal_reading_sits_past_moves_just_past_it()
    {
        var s = new OrchestratorSettings();
        var learned = HealthLearning.Learn(s, HealthCheck.CommitPercent, Many(85));
        Assert.That(learned, Is.Not.Null);
        Assert.That(learned!.Value.Line.Degraded, Is.EqualTo(85 * 1.05).Within(1e-9));
        // The unhealthy line moves as far, but no further than the 97% bound.
        Assert.That(learned.Value.Line.Unhealthy, Is.EqualTo(Math.Min(97, 90 + (85 * 1.05 - 80))).Within(1e-9));
        Assert.That(learned.Value.Reason, Does.Contain("past the configured degraded line"));
    }

    [Test]
    public void A_learned_line_never_passes_its_bound()
    {
        var s = new OrchestratorSettings() with { HealthCommitLearnCeilingPercent = 92 };
        var line = HealthLearning.Learn(s, HealthCheck.CommitPercent, Many(95))!.Value.Line;
        Assert.That(line.Degraded, Is.EqualTo(92));
        Assert.That(line.Unhealthy, Is.EqualTo(92));
    }

    [Test]
    public void A_free_space_line_learns_downward_and_stops_at_its_floor()
    {
        var s = new OrchestratorSettings();
        var line = HealthLearning.Learn(s, HealthCheck.RamFreeGb, Many(3))!.Value.Line;
        Assert.That(line.Degraded, Is.EqualTo(3 * 0.95).Within(1e-9));
        Assert.That(line.Unhealthy, Is.LessThanOrEqualTo(line.Degraded));
        var floored = HealthLearning.Learn(s, HealthCheck.RamFreeGb, Many(0.2))!.Value.Line;
        Assert.That(floored.Degraded, Is.EqualTo(s.HealthRamLearnFloorGb));
    }

    [Test]
    public void Readings_inside_the_line_keep_it_configured_and_too_few_leave_it_alone()
    {
        var s = new OrchestratorSettings();
        Assert.That(HealthLearning.Learn(s, HealthCheck.DtuPercent, Many(40))!.Value.Line, Is.EqualTo(HealthThresholds.Of(s).DtuPercent));
        Assert.That(HealthLearning.Learn(s, HealthCheck.DtuPercent, Many(99, s.HealthLearnMinSamples - 1)), Is.Null);
    }

    [Test]
    public void A_pass_logs_each_move_and_the_probes_grade_on_it()
    {
        var s = new OrchestratorSettings();
        var book = new LearnedHealthLines();
        var moves = HealthLearning.Pass(book, s, c => c == HealthCheck.LinkLatencyMs ? Many(2000) : [], Now);
        Assert.That(moves, Has.Count.EqualTo(1));
        Assert.That(moves[0].Check, Is.EqualTo(nameof(HealthCheck.LinkLatencyMs)));
        Assert.That(book.Changes, Has.Count.EqualTo(1));
        var t = HealthThresholds.Of(s, book);
        Assert.That(t.Grade(HealthCheck.LinkLatencyMs, 2000), Is.EqualTo(HealthGrade.Healthy));

        // Back inside the configured line: it goes back, and that's a move too.
        var back = HealthLearning.Pass(book, s, c => c == HealthCheck.LinkLatencyMs ? Many(100) : [], Now.AddMinutes(1));
        Assert.That(back, Has.Count.EqualTo(1));
        Assert.That(book.Lines, Is.Empty);
        Assert.That(HealthThresholds.Of(s, book).Grade(HealthCheck.LinkLatencyMs, 2000), Is.EqualTo(HealthGrade.Degraded));
    }

    [Test]
    public void With_learning_off_the_configured_lines_grade()
    {
        var s = new OrchestratorSettings();
        var book = new LearnedHealthLines();
        HealthLearning.Pass(book, s, c => c == HealthCheck.LinkLatencyMs ? Many(2000) : [], Now);
        var off = s with { HealthLearn = false };
        Assert.That(HealthThresholds.Of(off, book), Is.EqualTo(HealthThresholds.Of(off)));
        HealthLearning.Pass(book, off, _ => [], Now);
        Assert.That(book.Lines, Is.Empty);
    }
}
