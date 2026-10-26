using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Status;
using MindAttic.Orchestrator.Wpf;
using MindAttic.Orchestrator.Wpf.ViewModels;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The status stream (M.ORCH-T854): readings merge as usage only rises within a window, messages round-trip at full
/// precision, a dashboard gets the whole state on connect then each change, and pushed readings win over an older snapshot's.</summary>
public sealed class StatusStreamTests
{
    private static readonly DateTime Now = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Reset = Now.AddHours(2);

    private static MeterReading R(double five, DateTime at, DateTime? reset = null, double? week = null) => new()
    {
        Email = "a@x.com", FiveHourPercent = five, FiveHourResetUtc = reset ?? Reset, WeekPercent = week, WeekResetUtc = week is null ? null : Now.AddDays(3), ReadUtc = at,
    };

    [Test]
    public void A_lower_percent_for_the_same_window_keeps_the_higher()
    {
        var merged = MeterReading.Merge(R(35.25, Now), R(12, Now.AddMinutes(1)));
        Assert.That(merged.FiveHourPercent, Is.EqualTo(35.25));
        Assert.That(merged.ReadUtc, Is.EqualTo(Now.AddMinutes(1)));
    }

    [Test]
    public void A_new_window_takes_the_new_reading_however_low()
    {
        var merged = MeterReading.Merge(R(80, Now), R(3, Now.AddMinutes(1), Reset.AddHours(5)));
        Assert.That(merged.FiveHourPercent, Is.EqualTo(3));
    }

    [Test]
    public void An_older_reading_changes_nothing()
    {
        var was = R(20, Now);
        Assert.That(MeterReading.Merge(was, R(40, Now.AddMinutes(-1))), Is.EqualTo(was));
    }

    [Test]
    public void Merge_keeps_full_precision() =>
        Assert.That(MeterReading.Merge(R(10, Now), R(10.123456789012, Now.AddSeconds(1))).FiveHourPercent, Is.EqualTo(10.123456789012));

    [Test]
    public void A_message_round_trips_its_doubles_exactly()
    {
        var sent = new StatusMessage { Kind = StatusMessage.Usage, AtUtc = Now, Readings = [R(0.1 + 0.2, Now, week: 66.66666666666667)] };
        var back = StatusStream.DeserializeMessage(StatusStream.Serialize(sent))!;
        Assert.That(back.Kind, Is.EqualTo(StatusMessage.Usage));
        Assert.That(back.Readings[0].FiveHourPercent, Is.EqualTo(0.1 + 0.2));
        Assert.That(back.Readings[0].WeekPercent, Is.EqualTo(66.66666666666667));
        Assert.That(back.Readings[0].Email, Is.EqualTo("a@x.com"));
    }

    [Test]
    public void The_state_holds_each_logins_latest_and_the_newest_snapshot()
    {
        using var server = new StatusStreamServer { PipeName = "test-" + Guid.NewGuid() };
        server.PublishReadings([R(10, Now), R(5, Now.AddMinutes(1)) with { Email = "A@X.com" }, MeterReading.Of(null, new ReportedUsage(new(7, Reset), null, Now), null, "probe")]);
        server.PublishSnapshot(Now);
        server.PublishSnapshot(Now.AddSeconds(-5));
        var state = server.Current();
        Assert.That(state.Kind, Is.EqualTo(StatusMessage.State));
        Assert.That(state.SnapshotTakenUtc, Is.EqualTo(Now));
        Assert.That(state.Readings, Has.Count.EqualTo(2));
        Assert.That(state.Readings.Single(r => r.Email is not null).FiveHourPercent, Is.EqualTo(10));
    }

    [Test]
    public async Task A_dashboard_gets_the_state_on_connect_then_each_change()
    {
        var pipe = "mindattic-orchestrator-status-test-" + Guid.NewGuid().ToString("N");
        using var server = new StatusStreamServer { PipeName = pipe };
        server.PublishReadings([R(10, Now)]);
        server.Start();
        var got = new List<StatusMessage>();
        var gotTwo = new TaskCompletionSource();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var client = StatusStreamClient.RunAsync(m =>
        {
            lock (got)
            {
                got.Add(m);
                if (got.Count == 1) server.PublishReadings([R(11, Now.AddMinutes(1))]);
                if (got.Count >= 2) gotTwo.TrySetResult();
            }
        }, _ => { }, stop.Token, pipe);
        await gotTwo.Task.WaitAsync(stop.Token);
        stop.Cancel();
        await client;
        Assert.That(got[0].Kind, Is.EqualTo(StatusMessage.State));
        Assert.That(got[0].Readings.Single().FiveHourPercent, Is.EqualTo(10));
        Assert.That(got[1].Kind, Is.EqualTo(StatusMessage.Usage));
        Assert.That(got[1].Readings.Single().FiveHourPercent, Is.EqualTo(11));
    }

    [Test]
    public void A_pushed_window_is_the_reading_or_the_snapshots_when_higher_for_the_same_window()
    {
        Assert.That(MainViewModel.LiveWindow(40.5, Reset, 30, Reset, Now), Is.EqualTo(40.5));
        Assert.That(MainViewModel.LiveWindow(20, Reset, 30, Reset.AddSeconds(30), Now), Is.EqualTo(30));
        Assert.That(MainViewModel.LiveWindow(20, Reset, 30, Reset.AddHours(-5), Now), Is.EqualTo(20), "another window's figure is no floor");
        Assert.That(MainViewModel.LiveWindow(20, Now.AddMinutes(-1), 30, null, Now), Is.Null, "a window that reset says nothing");
        Assert.That(MainViewModel.LiveWindow(null, Reset, 30, Reset, Now), Is.Null);
    }

    [TestCase(false, false, true, null)]
    [TestCase(true, false, true, null)]
    [TestCase(false, true, true, 5)]
    [TestCase(true, true, true, 15)]
    [TestCase(false, false, false, 5)]
    [TestCase(true, false, false, 60)]
    public void With_the_stream_live_there_is_no_poll_unless_this_window_builds(bool hidden, bool holdsLease, bool live, int? seconds) =>
        Assert.That(MainWindow.RefreshInterval(hidden, holdsLease, live), Is.EqualTo(seconds is { } s ? TimeSpan.FromSeconds(s) : (TimeSpan?)null));
}
