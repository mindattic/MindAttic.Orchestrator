using System.Diagnostics;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The daemon's early refresh (<see cref="Daemon"/>'s wait between ticks), driven with short times.</summary>
public sealed class RefreshWaiterTests
{
    private static readonly TimeSpan MinGap = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);

    private string dir = null!;
    private string signal = null!;
    private string usage = null!;
    private DateTime stamp = DateTime.UtcNow.AddMinutes(-10);

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "orch-refresh-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        signal = Path.Combine(dir, "refresh.now");
        usage = Path.Combine(dir, "claude-usage.json");
        File.WriteAllText(signal, "");
        File.WriteAllText(usage, "{}");
        File.SetLastWriteTimeUtc(signal, stamp);
        File.SetLastWriteTimeUtc(usage, stamp);
    }

    [TearDown]
    public void TearDown() { try { Directory.Delete(dir, recursive: true); } catch (IOException) { } }

    private RefreshWaiter Waiter() => new([signal, usage], MinGap, Poll);

    /// <summary>Writes the file with a strictly newer time than any before it, so timestamp resolution can't hide it.</summary>
    private void Touch(string path)
    {
        stamp = stamp.AddSeconds(1);
        File.WriteAllText(path, stamp.Ticks.ToString());
        File.SetLastWriteTimeUtc(path, stamp);
    }

    private static (RefreshWake Wake, TimeSpan Took) Timed(Func<RefreshWake> wait)
    {
        var sw = Stopwatch.StartNew();
        var wake = wait();
        return (wake, sw.Elapsed);
    }

    [Test]
    public void Touching_refresh_now_wakes_it_once_MinGap_has_passed()
    {
        var waiter = Waiter();
        Touch(signal);

        var (wake, took) = Timed(() => waiter.Wait(Interval, CancellationToken.None));

        Assert.That(wake, Is.EqualTo(RefreshWake.Signal));
        Assert.That(took, Is.GreaterThanOrEqualTo(MinGap - TimeSpan.FromMilliseconds(15)), "never earlier than MinGap");
        Assert.That(took, Is.LessThan(MinGap + TimeSpan.FromSeconds(1)), "well before the interval");
    }

    [Test]
    public void A_signal_during_the_wait_wakes_it_soon_after()
    {
        var waiter = Waiter();
        using var _ = new Timer(__ => Touch(signal), null, TimeSpan.FromMilliseconds(800), Timeout.InfiniteTimeSpan);

        var (wake, took) = Timed(() => waiter.Wait(Interval, CancellationToken.None));

        Assert.That(wake, Is.EqualTo(RefreshWake.Signal));
        Assert.That(took, Is.InRange(TimeSpan.FromMilliseconds(780), TimeSpan.FromMilliseconds(2000)));
    }

    [Test]
    public void Rewriting_claude_usage_json_wakes_it()
    {
        var waiter = Waiter();
        Touch(usage);

        var (wake, took) = Timed(() => waiter.Wait(Interval, CancellationToken.None));

        Assert.That(wake, Is.EqualTo(RefreshWake.Signal));
        Assert.That(took, Is.LessThan(MinGap + TimeSpan.FromSeconds(1)));
    }

    [Test]
    public void With_no_signal_it_waits_the_full_interval()
    {
        var waiter = Waiter();
        var interval = TimeSpan.FromMilliseconds(1200);

        var (wake, took) = Timed(() => waiter.Wait(interval, CancellationToken.None));

        Assert.That(wake, Is.EqualTo(RefreshWake.Interval));
        Assert.That(took, Is.GreaterThanOrEqualTo(interval - TimeSpan.FromMilliseconds(15)));
        Assert.That(took, Is.LessThan(interval + TimeSpan.FromMilliseconds(800)));
    }

    [Test]
    public void Missing_files_are_no_signal()
    {
        File.Delete(signal);
        File.Delete(usage);
        var waiter = Waiter();

        Assert.That(waiter.Wait(TimeSpan.FromMilliseconds(600), CancellationToken.None), Is.EqualTo(RefreshWake.Interval));
    }

    [Test]
    public void Cancelling_returns_at_once()
    {
        var waiter = Waiter();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));

        var (wake, took) = Timed(() => waiter.Wait(TimeSpan.FromSeconds(30), cts.Token));

        Assert.That(wake, Is.EqualTo(RefreshWake.Cancelled));
        Assert.That(took, Is.LessThan(TimeSpan.FromMilliseconds(900)));

        using var already = new CancellationTokenSource();
        already.Cancel();
        var (wake2, took2) = Timed(() => waiter.Wait(TimeSpan.FromSeconds(30), already.Token));
        Assert.That(wake2, Is.EqualTo(RefreshWake.Cancelled));
        Assert.That(took2, Is.LessThan(TimeSpan.FromMilliseconds(200)));
    }

    [Test]
    public void A_burst_of_signals_never_brings_two_refreshes_closer_than_MinGap()
    {
        var waiter = Waiter();
        using var cts = new CancellationTokenSource();
        var burst = Task.Run(async () =>
        {
            var which = 0;
            while (!cts.IsCancellationRequested)
            {
                Touch(which++ % 2 == 0 ? signal : usage);
                await Task.Delay(5);
            }
        });

        var wakes = new List<TimeSpan>();
        var clock = Stopwatch.StartNew();
        try
        {
            for (var i = 0; i < 6; i++)
            {
                Assert.That(waiter.Wait(Interval, CancellationToken.None), Is.EqualTo(RefreshWake.Signal), "a burst wakes every wait early");
                wakes.Add(clock.Elapsed);
            }
        }
        finally
        {
            cts.Cancel();
            burst.Wait();
        }

        var gaps = wakes.Zip(wakes.Skip(1), (a, b) => b - a).Prepend(wakes[0]).ToList();
        Assert.That(gaps, Has.All.GreaterThanOrEqualTo(MinGap - TimeSpan.FromMilliseconds(15)), string.Join(", ", gaps.Select(g => g.TotalMilliseconds)));
    }
}
