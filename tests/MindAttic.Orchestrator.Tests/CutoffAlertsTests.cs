using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T742: a login at its own cutoff is reported once per crossing, and survives a restart.</summary>
public sealed class CutoffAlertsTests
{
    private const string Erin = "ohleo199@mindattic.com";
    private readonly TempDirs temps = new();
    private static readonly DateTime Now = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);

    [TearDown]
    public void TearDown() => temps.DeleteAll();

    private static ReportedUsage Usage(double week, double five) =>
        new(new(five, Now.AddHours(2)), new(week, Now.AddDays(3)), Now);

    [Test]
    public void A_crossing_is_reported_once_and_names_org_allow_and_org_limit()
    {
        var alerts = new CutoffAlerts(new JsonStore(temps.New()));
        var first = alerts.Check(Erin, "Erin", Usage(91, 50), 90, 98, false, Now);
        Assert.That(first, Has.Count.EqualTo(1));
        Assert.That(first[0].Weekly, Is.True);
        Assert.That(first[0].Text, Does.Contain("org allow ohleo199").And.Contain("org limit ohleo199"));
        Assert.That(alerts.Check(Erin, "Erin", Usage(95, 50), 90, 98, false, Now.AddMinutes(1)), Is.Empty);
    }

    [Test]
    public void A_restart_does_not_repeat_it_and_a_new_window_does()
    {
        var dir = temps.New();
        new CutoffAlerts(new JsonStore(dir)).Check(Erin, "Erin", Usage(50, 99), 90, 98, false, Now);
        var again = new CutoffAlerts(new JsonStore(dir));
        Assert.That(again.Check(Erin, "Erin", Usage(50, 99), 90, 98, false, Now.AddMinutes(5)), Is.Empty);
        var later = new ReportedUsage(new(99, Now.AddHours(8)), new(10, Now.AddDays(3)), Now);
        var next = again.Check(Erin, "Erin", later, 90, 98, false, Now.AddHours(6));
        Assert.That(next, Has.Count.EqualTo(1));
        Assert.That(next[0].Weekly, Is.False);
    }

    [Test]
    public void A_custom_cutoff_and_an_allowance_are_respected()
    {
        var alerts = new CutoffAlerts(null);
        Assert.That(alerts.Check(Erin, "Erin", Usage(85, 10), 90, 98, false, Now), Is.Empty);
        Assert.That(alerts.Check(Erin, "Erin", Usage(85, 10), 80, 98, true, Now), Is.Empty);
        Assert.That(alerts.Check(Erin, "Erin", Usage(85, 10), 80, 98, false, Now), Has.Count.EqualTo(1));
    }
}
