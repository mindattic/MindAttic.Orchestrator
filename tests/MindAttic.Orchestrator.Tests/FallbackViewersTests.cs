using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Setup;
using MindAttic.Orchestrator.Term;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The fallback viewers (M.ORCH-T960): "the dashboard is down" and which hosted CLIs need a window of their own.</summary>
public sealed class FallbackViewersTests
{
    private static readonly DateTime T0 = new(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);
    private static readonly IReadOnlyDictionary<string, DateTime> NoneLaunched = new Dictionary<string, DateTime>();
    private static readonly IReadOnlySet<string> NoneDismissed = new HashSet<string>();

    private static TermSessionRecord Session(string id, string state = TermSessionStates.Running) => new()
    {
        Id = id, HostPid = 4242, HostStartUtc = T0, Pipe = TermProtocol.PipeName(id, "tester"), Title = "RD: Repo [Claude]",
        State = state, StartedUtc = T0, Protocol = TermProtocol.Version,
    };

    // ── Dashboard down ──────────────────────────────────────────────────────

    [Test]
    public void A_dashboard_that_is_up_is_never_down()
    {
        var watch = new DashboardDownWatch();
        for (var s = 0; s < 30; s++) Assert.That(watch.Observe(up: true, T0.AddSeconds(s)), Is.False);
        Assert.That(watch.DownSinceUtc, Is.Null);
    }

    [Test]
    public void Down_only_once_gone_for_the_whole_threshold()
    {
        var watch = new DashboardDownWatch(TimeSpan.FromSeconds(5));
        Assert.That(watch.Observe(up: false, T0), Is.False, "just gone");
        Assert.That(watch.Observe(up: false, T0.AddSeconds(4.9)), Is.False, "a restart's moment");
        Assert.That(watch.Observe(up: false, T0.AddSeconds(5)), Is.True);
        Assert.That(watch.IsDown, Is.True);
        Assert.That(watch.DownSinceUtc, Is.EqualTo(T0));
    }

    [Test]
    public void Coming_back_starts_the_count_again()
    {
        var watch = new DashboardDownWatch(TimeSpan.FromSeconds(5));
        watch.Observe(up: false, T0);
        watch.Observe(up: false, T0.AddSeconds(6));
        Assert.That(watch.IsDown, Is.True);
        Assert.That(watch.Observe(up: true, T0.AddSeconds(7)), Is.False);
        Assert.That(watch.Observe(up: false, T0.AddSeconds(8)), Is.False, "gone again, but only just");
        Assert.That(watch.Observe(up: false, T0.AddSeconds(12)), Is.False);
        Assert.That(watch.Observe(up: false, T0.AddSeconds(13)), Is.True);
    }

    [Test]
    public void A_clock_stepped_back_restarts_the_count()
    {
        var watch = new DashboardDownWatch(TimeSpan.FromSeconds(5));
        watch.Observe(up: false, T0);
        Assert.That(watch.Observe(up: false, T0.AddMinutes(-1)), Is.False);
        Assert.That(watch.Observe(up: false, T0.AddMinutes(-1).AddSeconds(5)), Is.True);
    }

    [Test]
    public void The_default_threshold_is_five_seconds() =>
        Assert.That(new DashboardDownWatch().Threshold, Is.EqualTo(TimeSpan.FromSeconds(5)));

    [Test]
    public void The_dashboard_is_up_only_while_both_its_mutex_and_show_event_exist()
    {
        var name = @"Local\MindAttic.Orchestrator.Tests.Dashboard." + Guid.NewGuid().ToString("N");
        Assert.That(DashboardPresence.IsUp(name), Is.False, "nothing there");
        using (new Mutex(false, SingleInstance.MutexName(name)))
        {
            Assert.That(DashboardPresence.IsUp(name), Is.False, "a launch handing over opens the mutex too, but has no show event");
            using (new EventWaitHandle(false, EventResetMode.AutoReset, SingleInstance.ShowEvent(name)))
                Assert.That(DashboardPresence.IsUp(name), Is.True);
        }
        Assert.That(DashboardPresence.IsUp(name), Is.False, "the process that held them is gone");
    }

    [Test]
    public void The_dashboard_is_present_while_either_its_mutex_or_its_show_event_exists()
    {
        // M.ORCH-T1050: a fallback viewer whose link is taken folds at once on either, rather than reconnecting.
        var name = @"Local\MindAttic.Orchestrator.Tests.Dashboard." + Guid.NewGuid().ToString("N");
        Assert.That(DashboardPresence.IsPresent(name), Is.False, "nothing there");
        using (new Mutex(false, SingleInstance.MutexName(name)))
            Assert.That(DashboardPresence.IsPresent(name), Is.True, "the mutex alone");
        using (new EventWaitHandle(false, EventResetMode.AutoReset, SingleInstance.ShowEvent(name)))
            Assert.That(DashboardPresence.IsPresent(name), Is.True, "the show event alone");
        Assert.That(DashboardPresence.IsPresent(name), Is.False, "the process that held them is gone");
    }

    [Test]
    public void A_viewer_slot_is_held_while_its_mutex_exists()
    {
        var id = "t960-" + Guid.NewGuid().ToString("N")[..8];
        Assert.That(TermViewerSlot.IsHeld(id), Is.False);
        using (new Mutex(true, TermViewerSlot.MutexName(id)))
            Assert.That(TermViewerSlot.IsHeld(id), Is.True);
        Assert.That(TermViewerSlot.IsHeld(id), Is.False);
    }

    // ── Which sessions need a window ────────────────────────────────────────

    [Test]
    public void Every_running_session_without_a_viewer_needs_one()
    {
        var need = FallbackViewers.NeedingWindow([Session("a"), Session("b")], _ => false, NoneLaunched, NoneDismissed, T0, TimeSpan.FromSeconds(20));
        Assert.That(need.Select(s => s.Id), Is.EqualTo(new[] { "a", "b" }));
    }

    [Test]
    public void A_session_with_a_viewer_or_an_exited_cli_needs_none()
    {
        var need = FallbackViewers.NeedingWindow([Session("viewed"), Session("done", TermSessionStates.Exited), Session("bare")],
            id => id == "viewed", NoneLaunched, NoneDismissed, T0, TimeSpan.FromSeconds(20));
        Assert.That(need.Select(s => s.Id), Is.EqualTo(new[] { "bare" }));
    }

    [Test]
    public void A_viewer_just_started_has_its_grace_before_another_is()
    {
        var launched = new Dictionary<string, DateTime> { ["a"] = T0 };
        var grace = TimeSpan.FromSeconds(20);
        Assert.That(FallbackViewers.NeedingWindow([Session("a")], _ => false, launched, NoneDismissed, T0.AddSeconds(19), grace), Is.Empty);
        Assert.That(FallbackViewers.NeedingWindow([Session("a")], _ => false, launched, NoneDismissed, T0.AddSeconds(20), grace).Select(s => s.Id),
            Is.EqualTo(new[] { "a" }), "it never claimed its slot: started again");
    }

    [Test]
    public void A_window_the_person_closed_stays_closed()
    {
        var dismissed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "a" };
        var need = FallbackViewers.NeedingWindow([Session("a"), Session("b")], _ => false, NoneLaunched, dismissed, T0, TimeSpan.FromSeconds(20));
        Assert.That(need.Select(s => s.Id), Is.EqualTo(new[] { "b" }));
    }

    [Test]
    public void A_slot_that_cannot_be_read_counts_as_held()
    {
        var need = FallbackViewers.NeedingWindow([Session("a")], _ => throw new IOException("no"), NoneLaunched, NoneDismissed, T0, TimeSpan.FromSeconds(20));
        Assert.That(need, Is.Empty);
    }

    [Test]
    public void A_record_without_a_pipe_or_listed_twice_gets_one_window_at_most()
    {
        var need = FallbackViewers.NeedingWindow([Session("a"), Session("a"), Session("nopipe") with { Pipe = "" }], _ => false,
            NoneLaunched, NoneDismissed, T0, TimeSpan.FromSeconds(20));
        Assert.That(need.Select(s => s.Id), Is.EqualTo(new[] { "a" }));
    }

    // ── The daemon's pass ───────────────────────────────────────────────────

    private sealed class Rig
    {
        public bool Up = true;
        public List<TermSessionRecord> Live = [];
        public List<string> Started = [];
        public bool FailLaunch;

        public FallbackViewers Make() => new(TimeSpan.FromSeconds(5))
        {
            DashboardUp = () => Up,
            Sessions = () => Live,
            HasViewer = _ => false,
            ResolveExe = () => @"C:\Apps\MindAttic.Orchestrator\TermViewer\MindAttic.Orchestrator.TermViewer.exe",
            Launch = (_, s) => { if (FailLaunch) throw new InvalidOperationException("no"); Started.Add(s.Id); return null; },
            Say = (_, _) => { },
        };
    }

    [Test]
    public void Nothing_opens_while_the_dashboard_is_up()
    {
        var rig = new Rig { Live = [Session("a")] };
        var viewers = rig.Make();
        for (var s = 0; s < 30; s++) viewers.Tick(T0.AddSeconds(s));
        Assert.That(rig.Started, Is.Empty);
    }

    [Test]
    public void Each_session_gets_one_window_once_the_dashboard_has_been_down_five_seconds()
    {
        var rig = new Rig { Up = false, Live = [Session("a"), Session("b")] };
        var viewers = rig.Make();
        Assert.That(viewers.Tick(T0), Is.Zero);
        Assert.That(viewers.Tick(T0.AddSeconds(4)), Is.Zero);
        Assert.That(viewers.Tick(T0.AddSeconds(5)), Is.EqualTo(2));
        Assert.That(viewers.Tick(T0.AddSeconds(6)), Is.Zero, "not again within the grace");
        Assert.That(rig.Started, Is.EqualTo(new[] { "a", "b" }));
    }

    [Test]
    public void A_session_started_while_the_dashboard_is_down_gets_a_window_too()
    {
        var rig = new Rig { Up = false, Live = [Session("a")] };
        var viewers = rig.Make();
        viewers.Tick(T0);
        viewers.Tick(T0.AddSeconds(5));
        rig.Live.Add(Session("launcher"));
        viewers.Tick(T0.AddSeconds(6));
        Assert.That(rig.Started, Is.EqualTo(new[] { "a", "launcher" }));
    }

    [Test]
    public void The_dashboard_coming_back_stops_new_windows()
    {
        var rig = new Rig { Up = false, Live = [Session("a")] };
        var viewers = rig.Make();
        viewers.Tick(T0);
        viewers.Tick(T0.AddSeconds(5));
        rig.Up = true;
        rig.Live.Add(Session("b"));
        viewers.Tick(T0.AddSeconds(6));
        Assert.That(rig.Started, Is.EqualTo(new[] { "a" }));
    }

    [Test]
    public void A_viewer_that_fails_to_start_is_not_retried_every_second_and_nothing_throws()
    {
        var rig = new Rig { Up = false, Live = [Session("a")], FailLaunch = true };
        var viewers = rig.Make();
        viewers.Tick(T0);
        Assert.DoesNotThrow(() => viewers.Tick(T0.AddSeconds(5)));
        rig.FailLaunch = false;
        viewers.Tick(T0.AddSeconds(6));
        Assert.That(rig.Started, Is.Empty, "within the grace");
        viewers.Tick(T0.AddSeconds(26));
        Assert.That(rig.Started, Is.EqualTo(new[] { "a" }));
    }

    [Test]
    public void A_probe_that_throws_counts_as_up_so_no_window_opens()
    {
        var rig = new Rig { Live = [Session("a")] };
        var viewers = new FallbackViewers(TimeSpan.FromSeconds(5))
        {
            DashboardUp = () => throw new InvalidOperationException("no"),
            Sessions = () => rig.Live,
            HasViewer = _ => false,
            ResolveExe = () => "x.exe",
            Launch = (_, s) => { rig.Started.Add(s.Id); return null; },
            Say = (_, _) => { },
        };
        for (var s = 0; s < 10; s++) viewers.Tick(T0.AddSeconds(s));
        Assert.That(rig.Started, Is.Empty);
    }

    [Test]
    public void A_crashed_viewer_waits_longer_each_time_up_to_a_cap()
    {
        Assert.That(FallbackViewers.CrashDelay(1), Is.EqualTo(TimeSpan.FromSeconds(2)));
        Assert.That(FallbackViewers.CrashDelay(2), Is.EqualTo(TimeSpan.FromSeconds(4)));
        Assert.That(FallbackViewers.CrashDelay(3), Is.EqualTo(TimeSpan.FromSeconds(8)));
        Assert.That(FallbackViewers.CrashDelay(50), Is.EqualTo(FallbackViewers.MaxCrashDelay));
        Assert.That(FallbackViewers.CrashDelay(0), Is.EqualTo(TimeSpan.FromSeconds(2)));
    }

    [Test]
    public void A_viewer_that_keeps_crashing_is_reopened_with_a_backoff_then_given_up_on_and_said_once()
    {
        var rig = new Rig { Up = false, Live = [Session("a")] };
        var said = new List<string>();
        var processes = new List<System.Diagnostics.Process>();
        var viewers = new FallbackViewers(TimeSpan.FromSeconds(5))
        {
            DashboardUp = () => rig.Up,
            Sessions = () => rig.Live,
            HasViewer = _ => false,
            ResolveExe = () => "x.exe",
            Launch = (_, s) =>
            {
                rig.Started.Add(s.Id);
                var crash = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c exit 3") { CreateNoWindow = true, UseShellExecute = false })!;
                processes.Add(crash);
                crash.WaitForExit();
                return crash;
            },
            Say = (_, line) => said.Add(line),
        };
        try
        {
            var now = T0;
            viewers.Tick(now);
            now = now.AddSeconds(5);
            viewers.Tick(now);
            Assert.That(rig.Started, Has.Count.EqualTo(1));
            for (var crash = 1; crash < FallbackViewers.MaxCrashes; crash++)
            {
                now = now.AddSeconds(1);
                viewers.Tick(now); // reaps the crash
                Assert.That(rig.Started, Has.Count.EqualTo(crash), "not at once");
                now += FallbackViewers.CrashDelay(crash);
                viewers.Tick(now);
                Assert.That(rig.Started, Has.Count.EqualTo(crash + 1), "after the wait");
            }
            for (var s = 0; s < 300; s++) viewers.Tick(now.AddSeconds(s + 1));
            Assert.That(rig.Started, Has.Count.EqualTo(FallbackViewers.MaxCrashes), "given up on");
            Assert.That(said.Count(l => l.Contains("crashed", StringComparison.Ordinal)), Is.EqualTo(1), "said once");
        }
        finally { foreach (var p in processes) p.Dispose(); }
    }

    [Test]
    public void No_viewer_installed_opens_nothing()
    {
        var rig = new Rig { Up = false, Live = [Session("a")] };
        var viewers = new FallbackViewers(TimeSpan.FromSeconds(5))
        {
            DashboardUp = () => false,
            Sessions = () => rig.Live,
            HasViewer = _ => false,
            ResolveExe = () => null,
            Launch = (_, s) => { rig.Started.Add(s.Id); return null; },
            Say = (_, _) => { },
        };
        viewers.Tick(T0);
        Assert.That(viewers.Tick(T0.AddSeconds(5)), Is.Zero);
        Assert.That(rig.Started, Is.Empty);
    }
}
