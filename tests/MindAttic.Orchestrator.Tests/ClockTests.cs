using System.Windows.Threading;
using MindAttic.Orchestrator.Wpf.ViewModels;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The header's live clock shows seconds and ticks on its own timer, not with the data refresh (M.ORCH-T193).</summary>
[Apartment(ApartmentState.STA)]
public sealed class ClockTests
{
    [Test]
    [SetCulture("en-US")]
    public void Clock_shows_hours_minutes_seconds_and_am_pm() =>
        Assert.That(ClockViewModel.Format(new DateTime(2026, 10, 16, 19, 31, 39)), Is.EqualTo("7:31:39 PM"));

    [Test]
    [SetCulture("en-US")]
    public void Clock_ticks_on_its_own_timer_without_any_refresh()
    {
        var time = new DateTime(2026, 10, 16, 19, 31, 39);
        var clock = new ClockViewModel(() => time, TimeSpan.FromMilliseconds(20));
        var changes = new List<string?>();
        clock.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        clock.Start();
        Assert.That(clock.Text, Is.EqualTo("7:31:39 PM"));

        time = time.AddSeconds(1);
        Pump(TimeSpan.FromMilliseconds(500), () => clock.Text == "7:31:40 PM");
        Assert.That(clock.Text, Is.EqualTo("7:31:40 PM"));
        time = time.AddSeconds(1);
        Pump(TimeSpan.FromMilliseconds(500), () => clock.Text == "7:31:41 PM");
        Assert.That(clock.Text, Is.EqualTo("7:31:41 PM"));
        Assert.That(changes, Has.All.EqualTo(nameof(ClockViewModel.Text)));

        clock.Stop();
        Assert.That(clock.IsRunning, Is.False);
        time = time.AddSeconds(1);
        Pump(TimeSpan.FromMilliseconds(100), () => false);
        Assert.That(clock.Text, Is.EqualTo("7:31:41 PM"));
    }

    [Test]
    [SetCulture("en-US")]
    public void Clock_shows_the_current_time_before_its_first_tick() =>
        Assert.That(new ClockViewModel().Text, Does.Match(@"^\d{1,2}:\d{2}:\d{2}\s(AM|PM)$"));

    private static void Pump(TimeSpan limit, Func<bool> done)
    {
        var until = DateTime.UtcNow + limit;
        while (!done() && DateTime.UtcNow < until)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
            Thread.Sleep(5);
        }
    }
}
