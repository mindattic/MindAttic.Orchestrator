using MindAttic.Orchestrator.Scheduling;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T1688: a milestone's projected date and token cost come from its open tickets' estimates against measured
/// throughput, and it's at risk when the date slips past its own first forecast or it grows faster than it closes.</summary>
public sealed class MilestoneForecastTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Activated = Now.AddDays(-10);

    private static MilestoneForecast.OpenTicket[] Open(params long?[] estimates) =>
        [.. estimates.Select((e, i) => new MilestoneForecast.OpenTicket($"X-T{i + 1}", e))];

    private static MilestoneForecast.Throughput Measured(int closed, long? average = 100_000) => new(closed, 14, average);

    [Test]
    public void NothingClosedInTheWindow_TheDateIsUnknown()
    {
        var f = MilestoneForecast.Compute(Open(50_000, 70_000), Measured(0), Now);

        Assert.Multiple(() =>
        {
            Assert.That(f.ProjectedUtc, Is.Null);
            Assert.That(f.TicketsPerDay, Is.Null);
            Assert.That(f.Basis, Does.Contain("unknown"));
            Assert.That(f.RemainingTickets, Is.EqualTo(2));
            Assert.That(f.RemainingTokens, Is.EqualTo(120_000));
            Assert.That(f.AtRisk, Is.False);
        });
    }

    [Test]
    public void TheDate_IsTheRemainingTicketsOverTheTicketsClosedPerDay()
    {
        var f = MilestoneForecast.Compute(Open(1, 1, 1, 1), Measured(28), Now);

        Assert.Multiple(() =>
        {
            Assert.That(f.TicketsPerDay, Is.EqualTo(2));
            Assert.That(f.ProjectedUtc, Is.EqualTo(Now.AddDays(2)));
            Assert.That(f.Basis, Does.Contain("28 closed"));
        });
    }

    [Test]
    public void ATicketWithNoEstimate_CountsAtTheMeasuredAverage_AndIsCounted()
    {
        var f = MilestoneForecast.Compute(Open(50_000, null, 0, 30_000), Measured(7, average: 200_000), Now);

        Assert.Multiple(() =>
        {
            Assert.That(f.RemainingTokens, Is.EqualTo(480_000));
            Assert.That(f.FromAverage, Is.EqualTo(2));
            Assert.That(f.Unvalued, Is.Zero);
        });
    }

    [Test]
    public void WithNoMeasuredAverage_ATicketWithNoEstimateIsLeftOutOfTheSum_AndCounted()
    {
        var f = MilestoneForecast.Compute(Open(50_000, null), Measured(7, average: null), Now);

        Assert.Multiple(() =>
        {
            Assert.That(f.RemainingTokens, Is.EqualTo(50_000));
            Assert.That(f.FromAverage, Is.Zero);
            Assert.That(f.Unvalued, Is.EqualTo(1));
        });
    }

    [Test]
    public void NoOpenTickets_NothingIsLeftAndNoDateIsProjected()
    {
        var f = MilestoneForecast.Compute([], Measured(7), Now, Activated, new MilestoneForecast.Baseline(Now.AddDays(-30), Activated),
            new MilestoneForecast.Growth(5, 1));

        Assert.Multiple(() =>
        {
            Assert.That(f.Complete, Is.True);
            Assert.That(f.ProjectedUtc, Is.Null);
            Assert.That(f.AtRisk, Is.False);
        });
    }

    [Test]
    public void AProjectionLaterThanTheFirstByMoreThanTheMargin_IsAtRisk()
    {
        // The first forecast was 10 days after activation, so 2 days of slip are allowed; this one is 4 days later than it.
        var first = new MilestoneForecast.Baseline(Activated.AddDays(10), Activated);
        var f = MilestoneForecast.Compute(Open(1, 1, 1, 1), Measured(14), Now, Activated, first);

        Assert.Multiple(() =>
        {
            Assert.That(f.ProjectedUtc, Is.EqualTo(Now.AddDays(4)));
            Assert.That(f.AtRisk, Is.True);
            Assert.That(f.Reason, Does.Contain("4 days later than its first forecast"));
            Assert.That(f.Reason, Does.Contain("2 days allowed"));
        });
    }

    [Test]
    public void AProjectionWithinTheMarginOfTheFirst_IsNotAtRisk()
    {
        var first = new MilestoneForecast.Baseline(Activated.AddDays(10), Activated);
        var f = MilestoneForecast.Compute(Open(1, 1), Measured(14), Now, Activated, first);

        Assert.Multiple(() =>
        {
            Assert.That(f.ProjectedUtc, Is.EqualTo(Now.AddDays(2)));
            Assert.That(f.AtRisk, Is.False);
            Assert.That(f.Reasons, Is.Empty);
        });
    }

    [Test]
    public void AnEarlierProjectionThanTheFirst_IsNotAtRisk()
    {
        var first = new MilestoneForecast.Baseline(Now.AddDays(20), Activated);

        Assert.That(MilestoneForecast.Compute(Open(1), Measured(14), Now, Activated, first).AtRisk, Is.False);
    }

    [Test]
    public void AShortOriginalSpan_StillAllowsTheLeastSlip()
    {
        // Activated a day before its first projected date: a fifth of that is under MinSlip, so a day of slip is still allowed.
        var activated = Now.AddDays(-1);
        var first = new MilestoneForecast.Baseline(Now, activated);

        Assert.Multiple(() =>
        {
            Assert.That(MilestoneForecast.Compute(Open(1), Measured(14), Now, activated, first).AtRisk, Is.False);
            Assert.That(MilestoneForecast.Compute(Open(1, 1), Measured(14), Now, activated, first).AtRisk, Is.True);
        });
    }

    [Test]
    public void WithNoFirstForecastYet_ALateDateAloneIsNotAtRisk()
    {
        Assert.That(MilestoneForecast.Compute(Open(1, 1, 1, 1), Measured(1), Now, Activated).AtRisk, Is.False);
    }

    [Test]
    public void MoreTicketsJoiningThanClosing_IsAtRisk()
    {
        var f = MilestoneForecast.Compute(Open(1, 1), Measured(14), Now, Activated, growth: new MilestoneForecast.Growth(6, 4));

        Assert.Multiple(() =>
        {
            Assert.That(f.AtRisk, Is.True);
            Assert.That(f.Reason, Does.Contain("6 tickets have joined it since activation and 4 were closed"));
        });
    }

    [Test]
    public void AsManyTicketsClosingAsJoining_IsNotAtRisk()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MilestoneForecast.Compute(Open(1, 1), Measured(14), Now, Activated, growth: new MilestoneForecast.Growth(4, 4)).AtRisk, Is.False);
            Assert.That(MilestoneForecast.Compute(Open(1, 1), Measured(14), Now, Activated, growth: new MilestoneForecast.Growth(0, 0)).AtRisk, Is.False);
        });
    }

    [Test]
    public void BothASlipAndGrowth_AreBothGivenAsReasons()
    {
        var first = new MilestoneForecast.Baseline(Activated.AddDays(10), Activated);
        var f = MilestoneForecast.Compute(Open(1, 1, 1, 1), Measured(14), Now, Activated, first, new MilestoneForecast.Growth(3, 1));

        Assert.That(f.Reasons, Has.Count.EqualTo(2));
    }

    [Test]
    public void TheRecord_KeepsTheFirstProjectedDate_AndMovesTheLatest()
    {
        var record = new MilestoneForecasts.Recorded();
        record.Take(MilestoneForecast.Compute(Open(1, 1), Measured(0), Now), Now);
        Assert.That(record.Baseline, Is.Null);

        record.Take(MilestoneForecast.Compute(Open(1, 1), Measured(14), Now), Now);
        var later = Now.AddHours(1);
        record.Take(MilestoneForecast.Compute(Open(1, 1, 1, 1), Measured(14), later, Activated, record.Baseline, new MilestoneForecast.Growth(2, 0)), later);
        record.Take(MilestoneForecast.Compute(Open(1, 1, 1, 1), Measured(0), later.AddHours(1)), later.AddHours(1));

        Assert.Multiple(() =>
        {
            Assert.That(record.FirstProjectedUtc, Is.EqualTo(Now.AddDays(2)));
            Assert.That(record.FirstAtUtc, Is.EqualTo(Now));
            Assert.That(record.LatestProjectedUtc, Is.EqualTo(later.AddDays(4)));
            Assert.That(record.LatestAtUtc, Is.EqualTo(later));
            Assert.That(record.AtRisk, Is.False);
        });
    }
}
