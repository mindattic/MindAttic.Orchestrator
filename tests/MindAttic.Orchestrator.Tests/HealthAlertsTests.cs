using MindAttic.Orchestrator.Health;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T2039: a health check alerts once per transition (unhealthy after N in a row, recovered after M), never on
/// every run, degraded never; the alerted set survives a restart.</summary>
public sealed class HealthAlertsTests
{
    private readonly TempDirs temps = new();
    private static readonly DateTimeOffset Start = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
    private int run;

    [TearDown]
    public void TearDown() => temps.DeleteAll();

    private HealthReport Report(HealthGrade grade, double value = 1.5, string? disk = "C:")
    {
        var at = Start.AddSeconds(30 * ++run);
        return new HealthReport("machine", "PC", null, at, 10, 30,
            [new HealthResult("disk:free", disk, "system", value, "GB", grade, at, 10, 2, "C: is nearly full")]);
    }

    [Test]
    public void Unhealthy_alerts_once_after_the_threshold_and_never_repeats_while_it_stays()
    {
        var alerts = new HealthAlerts(new JsonStore(temps.New()));
        Assert.That(alerts.Take(Report(HealthGrade.Unhealthy), 2, 1), Is.Empty, "one bad reading doesn't alert");
        var fired = alerts.Take(Report(HealthGrade.Unhealthy), 2, 1);
        Assert.That(fired, Has.Count.EqualTo(1));
        Assert.That(fired[0].Unhealthy, Is.True);
        Assert.That(fired[0].Text, Does.Contain("disk:free").And.Contain("1.5 GB").And.Contain("unhealthy line 2 GB").And.Contain("C: is nearly full"));
        for (var i = 0; i < 20; i++)
            Assert.That(alerts.Take(Report(HealthGrade.Unhealthy), 2, 1), Is.Empty, "no repeat while it stays unhealthy");
    }

    [Test]
    public void A_report_seen_twice_counts_once()
    {
        var alerts = new HealthAlerts(null);
        var report = Report(HealthGrade.Unhealthy);
        Assert.That(alerts.Take(report, 2, 1), Is.Empty);
        Assert.That(alerts.Take(report, 2, 1), Is.Empty);
    }

    [Test]
    public void Recovery_alerts_once_after_its_threshold_and_a_flap_below_it_does_not()
    {
        var alerts = new HealthAlerts(null);
        alerts.Take(Report(HealthGrade.Unhealthy), 1, 2);
        Assert.That(alerts.Take(Report(HealthGrade.Healthy, 20), 1, 2), Is.Empty, "one good reading isn't a recovery yet");
        Assert.That(alerts.Take(Report(HealthGrade.Unhealthy), 1, 2), Is.Empty, "still alerted: no second unhealthy alert");
        Assert.That(alerts.Take(Report(HealthGrade.Degraded, 8), 1, 2), Is.Empty);
        var back = alerts.Take(Report(HealthGrade.Healthy, 20), 1, 2);
        Assert.That(back, Has.Count.EqualTo(1));
        Assert.That(back[0].Unhealthy, Is.False);
        Assert.That(back[0].Text, Does.Contain("recovered"));
        Assert.That(alerts.Take(Report(HealthGrade.Healthy, 20), 1, 2), Is.Empty, "the recovery is said once");
    }

    [Test]
    public void Degraded_never_alerts_and_breaks_an_unhealthy_run()
    {
        var alerts = new HealthAlerts(null);
        for (var i = 0; i < 10; i++)
        {
            Assert.That(alerts.Take(Report(HealthGrade.Unhealthy), 2, 1), Is.Empty);
            Assert.That(alerts.Take(Report(HealthGrade.Degraded, 8), 2, 1), Is.Empty);
        }
    }

    [Test]
    public void Instances_are_counted_apart()
    {
        var alerts = new HealthAlerts(null);
        alerts.Take(Report(HealthGrade.Unhealthy, disk: "C:"), 2, 1);
        Assert.That(alerts.Take(Report(HealthGrade.Unhealthy, disk: "D:"), 2, 1), Is.Empty, "D:'s first bad reading, not C:'s second");
    }

    [Test]
    public void A_restart_neither_repeats_the_alert_nor_loses_the_recovery()
    {
        var dir = temps.New();
        var first = new HealthAlerts(new JsonStore(dir));
        first.Take(Report(HealthGrade.Unhealthy), 2, 1);
        Assert.That(first.Take(Report(HealthGrade.Unhealthy), 2, 1), Has.Count.EqualTo(1));

        var again = new HealthAlerts(new JsonStore(dir));
        Assert.That(again.Take(Report(HealthGrade.Unhealthy), 2, 1), Is.Empty);
        Assert.That(again.Take(Report(HealthGrade.Unhealthy), 2, 1), Is.Empty);
        var back = again.Take(Report(HealthGrade.Healthy, 20), 2, 1);
        Assert.That(back, Has.Count.EqualTo(1));
        Assert.That(back[0].Unhealthy, Is.False);
    }
}
