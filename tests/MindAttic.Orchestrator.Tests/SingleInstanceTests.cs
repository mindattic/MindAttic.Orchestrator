using MindAttic.Orchestrator.Setup;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class SingleInstanceTests
{
    private static string UniqueName() => $@"Local\MindAttic.Orchestrator.Tests.SingleInstance.{Guid.NewGuid():N}";

    /// <summary>A launch, as a separate process would: on its own thread, since a mutex is re-entrant on the thread that owns it.</summary>
    private static SingleInstance? LaunchElsewhere(string name, TimeSpan wait, IReadOnlyList<string>? args = null)
    {
        SingleInstance? result = null;
        var t = new Thread(() => result = SingleInstance.Claim(name, wait, args));
        t.Start();
        t.Join();
        return result;
    }

    [Test]
    public void A_second_launch_brings_the_running_one_forward_and_exits()
    {
        // M.ORCH-T497: one launch made two dashboards, each with a tray icon; the second now asks the first to show itself.
        var name = UniqueName();
        using var first = SingleInstance.Claim(name, TimeSpan.Zero);
        Assert.That(first, Is.Not.Null);
        using var shown = new ManualResetEventSlim();
        first!.OnShowRequested(shown.Set);

        Assert.That(LaunchElsewhere(name, TimeSpan.FromSeconds(5)), Is.Null, "a second dashboard started");
        Assert.That(shown.Wait(TimeSpan.FromSeconds(5)), Is.True, "the running dashboard wasn't asked to show itself");
    }

    [Test]
    public void A_launch_with_start_hands_its_repo_to_the_running_one()
    {
        // M.ORCH-T767: the Tickets desktop's Start link runs MindAttic.Orchestrator.Desktop.exe --start <repo>; the running dashboard gets it.
        var name = UniqueName();
        using var first = SingleInstance.Claim(name, TimeSpan.Zero);
        IReadOnlyList<string>? got = null;
        using var handed = new ManualResetEventSlim();
        first!.OnShowRequested(args => { got = args; handed.Set(); });

        Assert.That(LaunchElsewhere(name, TimeSpan.FromSeconds(5), ["--start", "MindAttic.Tickets"]), Is.Null, "a second dashboard started");
        Assert.That(handed.Wait(TimeSpan.FromSeconds(10)), Is.True, "the running dashboard wasn't handed the arguments");
        Assert.That(got, Is.EqualTo(new[] { "--start", "MindAttic.Tickets" }));
    }

    [Test]
    public void A_hand_off_before_the_window_listens_waits_for_it()
    {
        var name = UniqueName();
        using var first = SingleInstance.Claim(name, TimeSpan.Zero);
        Assert.That(LaunchElsewhere(name, TimeSpan.FromSeconds(5), ["--start", "A"]), Is.Null);
        Thread.Sleep(300); // delivered to nobody yet

        IReadOnlyList<string>? got = null;
        using var handed = new ManualResetEventSlim();
        first!.OnShowRequested(args => { if (args.Count > 0) { got = args; handed.Set(); } });
        Assert.That(handed.Wait(TimeSpan.FromSeconds(10)), Is.True);
        Assert.That(got, Is.EqualTo(new[] { "--start", "A" }));
    }

    [Test]
    public void A_hand_off_that_is_not_a_list_of_strings_is_dropped()
    {
        Assert.That(SingleInstance.ParseHandOff("[\"--start\",\"A\"]"u8.ToArray()), Is.EqualTo(new[] { "--start", "A" }));
        Assert.That(SingleInstance.ParseHandOff("{\"x\":1}"u8.ToArray()), Is.Null);
        Assert.That(SingleInstance.ParseHandOff("[null]"u8.ToArray()), Is.Null);
        Assert.That(SingleInstance.ParseHandOff("not json"u8.ToArray()), Is.Null);
        var huge = new MemoryStream(new byte[SingleInstance.MaxHandOffBytes + 1]);
        Assert.That(SingleInstance.ReadHandOffAsync(huge, CancellationToken.None).Result, Is.Null);
    }

    [Test]
    public void Every_further_launch_still_brings_it_forward()
    {
        var name = UniqueName();
        using var first = SingleInstance.Claim(name, TimeSpan.Zero);
        var count = 0;
        using var twice = new CountdownEvent(2);
        first!.OnShowRequested(() => { Interlocked.Increment(ref count); twice.Signal(); });

        Assert.That(LaunchElsewhere(name, TimeSpan.Zero), Is.Null);
        Assert.That(SpinWait.SpinUntil(() => Volatile.Read(ref count) == 1, TimeSpan.FromSeconds(5)), Is.True);
        Assert.That(LaunchElsewhere(name, TimeSpan.Zero), Is.Null);
        Assert.That(twice.Wait(TimeSpan.FromSeconds(5)), Is.True);
        Thread.Sleep(200);
        Assert.That(Volatile.Read(ref count), Is.EqualTo(2), "each launch asks once");
    }

    [Test]
    public void Once_the_dashboard_exits_the_next_launch_runs()
    {
        var name = UniqueName();
        SingleInstance.Claim(name, TimeSpan.Zero)!.Dispose();

        using var next = LaunchElsewhere(name, TimeSpan.Zero);
        Assert.That(next, Is.Not.Null);
    }

    [Test]
    public void A_launch_while_the_old_one_is_exiting_waits_for_it_rather_than_leaving_none()
    {
        // An upgrade asks the old dashboard to exit and starts the new one: if the old still holds the mutex (its show event
        // already gone), the new one waits for it instead of exiting with no dashboard up.
        var name = UniqueName();
        using var held = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var old = new Thread(() =>
        {
            using var m = new Mutex(true, name + ".Instance");
            held.Set();
            release.Wait();
            m.ReleaseMutex();
        });
        old.Start();
        held.Wait();

        SingleInstance? next = null;
        var launch = new Thread(() => next = SingleInstance.Claim(name, TimeSpan.FromSeconds(10)));
        launch.Start();
        Thread.Sleep(200);
        release.Set();
        launch.Join();
        old.Join();

        Assert.That(next, Is.Not.Null);
        next!.Dispose();
    }

    [Test]
    public void A_dashboard_that_was_killed_does_not_block_the_next()
    {
        var name = UniqueName();
        var dead = new Thread(() => new Mutex(true, name + ".Instance")); // owner thread ends without releasing: abandoned
        dead.Start();
        dead.Join();

        using var next = LaunchElsewhere(name, TimeSpan.FromSeconds(5));
        Assert.That(next, Is.Not.Null);
    }
}
