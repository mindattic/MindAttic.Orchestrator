using MindAttic.Orchestrator.Health;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T2035: the health probes grade their readings against lines read from the settings.</summary>
public sealed class HealthThresholdsTests
{
    [Test]
    public void Probes_grade_against_the_lines_the_settings_hold()
    {
        var t = HealthThresholds.Of(new OrchestratorSettings() with { HealthDtuDegradedPercent = 50, HealthDtuUnhealthyPercent = 70 });
        Assert.That(t.Grade(HealthCheck.DtuPercent, 49), Is.EqualTo(HealthGrade.Healthy));
        Assert.That(t.Grade(HealthCheck.DtuPercent, 50), Is.EqualTo(HealthGrade.Degraded));
        Assert.That(t.Grade(HealthCheck.DtuPercent, 100), Is.EqualTo(HealthGrade.Unhealthy));
    }

    [Test]
    public void Free_space_checks_grade_worse_as_the_reading_falls()
    {
        var t = HealthThresholds.Default;
        Assert.That(t.Grade(HealthCheck.RamFreeGb, 8), Is.EqualTo(HealthGrade.Healthy));
        Assert.That(t.Grade(HealthCheck.RamFreeGb, 3), Is.EqualTo(HealthGrade.Degraded));
        Assert.That(t.Grade(HealthCheck.RamFreeGb, 1), Is.EqualTo(HealthGrade.Unhealthy));
        Assert.That(t.Grade(HealthCheck.DiskFreeGb, 4), Is.EqualTo(HealthGrade.Unhealthy));
        Assert.That(t.Grade(HealthCheck.LinkLatencyMs, double.NaN), Is.EqualTo(HealthGrade.Healthy));
    }

    [Test]
    public void Misordered_lines_are_a_problem_the_cli_refuses()
    {
        Assert.That(HealthThresholds.Problem(new OrchestratorSettings()), Is.Null);
        Assert.That(HealthThresholds.Problem(new OrchestratorSettings() with { HealthStorageDegradedPercent = 99, HealthStorageUnhealthyPercent = 90 }), Is.Not.Null);
        Assert.That(HealthThresholds.Problem(new OrchestratorSettings() with { HealthRamUnhealthyGb = 8 }), Is.Not.Null);
    }
}
