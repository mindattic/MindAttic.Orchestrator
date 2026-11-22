using System.Text.Json.Nodes;
using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Health;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T2020: a probe run's report, graded and written as health+json.</summary>
public sealed class HealthReportTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 22, 20, 40, 0, TimeSpan.Zero);

    private static HealthReport Sample()
    {
        var t = HealthThresholds.Default;
        return new HealthReport("machine", "RYAN-PC", "RAM, commit/pagefile and every disk", At, 182, 30,
        [
            HealthResult.Graded(HealthCheck.RamFreeGb, t, 5.8, At),
            HealthResult.Graded(HealthCheck.CommitPercent, t, 99, At, output: "lghub_agent 3.1 GB"),
            HealthResult.Graded(HealthCheck.DiskFreeGb, t, 41.2, At, "C:"),
            HealthResult.Graded(HealthCheck.DiskFreeGb, t, double.NaN, At, "R:", "drive not ready"),
            new HealthResult("sqldb:state", "srv/db", "datastore", null, "state", HealthGrade.Healthy, At, ObservedState: "Paused"),
        ]);
    }

    [Test]
    public void A_report_round_trips_through_its_json()
    {
        var report = Sample();
        var back = HealthReport.FromJson(report.ToJson());
        Assert.That(back, Is.EqualTo(report));
        Assert.That(back!.Status, Is.EqualTo(HealthGrade.Unhealthy));
    }

    [Test]
    public void The_json_is_health_json_shaped()
    {
        var root = JsonNode.Parse(Sample().ToJson())!.AsObject();
        Assert.That((string?)root["status"], Is.EqualTo("fail"));
        var disks = root["checks"]!["disk:free"]!.AsArray();
        Assert.That(disks.Count, Is.EqualTo(2));
        Assert.That((string?)disks[0]!["componentId"], Is.EqualTo("C:"));
        Assert.That(disks[1]!["observedValue"], Is.Null);
        Assert.That((string?)disks[1]!["status"], Is.EqualTo("warn"));
        Assert.That((string?)root["checks"]!["sqldb:state"]![0]!["observedValue"], Is.EqualTo("Paused"));
        Assert.That((string?)root["time"], Is.EqualTo("2026-11-22T20:40:00Z"));
    }

    [Test]
    public void Graded_stamps_the_line_it_used()
    {
        var t = HealthThresholds.Default;
        var r = HealthResult.Graded(HealthCheck.DtuPercent, t, 50, At, "srv/db");
        Assert.That(r.CheckId, Is.EqualTo("sqldb:dtu"));
        Assert.That(r.DegradedAt, Is.EqualTo(t.DtuPercent.Degraded));
        Assert.That(r.UnhealthyAt, Is.EqualTo(t.DtuPercent.Unhealthy));
        Assert.That(r.Grade, Is.EqualTo(t.Grade(HealthCheck.DtuPercent, 50)));
    }

    [Test]
    public void Reading_accepts_aliases_and_any_case()
    {
        var json = """{"Probe":"links","CHECKS":{"link:latency":[{"Status":"down","observedValue":null},{"status":"ok","observedValue":3}]}}""";
        var r = HealthReport.FromJson(json)!;
        Assert.That(r.Results.Select(x => x.Grade), Is.EqualTo(new[] { HealthGrade.Unhealthy, HealthGrade.Healthy }));
        Assert.That(r.Results[1].ObservedValue, Is.EqualTo(3));
    }

    [Test]
    public void MachineHealth_levels_map_onto_grades()
    {
        Assert.That(HealthGrades.From(HealthLevel.Ok), Is.EqualTo(HealthGrade.Healthy));
        Assert.That(HealthGrades.From(HealthLevel.Warn), Is.EqualTo(HealthGrade.Degraded));
        Assert.That(HealthGrades.From(HealthLevel.Danger), Is.EqualTo(HealthGrade.Unhealthy));
        Assert.That(HealthGrades.NagiosExit(HealthGrade.Degraded), Is.EqualTo(1));
    }
}
