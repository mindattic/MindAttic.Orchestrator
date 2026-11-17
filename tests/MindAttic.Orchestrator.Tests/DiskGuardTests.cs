using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;
using Serilog.Events;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The disk floor (M.ORCH-T1679): under it, after a self-cleanup, new work is held; running work never is; it lifts
/// by itself once clear of the floor by the margin.</summary>
public sealed class DiskGuardTests
{
    private static readonly DateTime T0 = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
    private static readonly OrchestratorSettings S = new();

    private static IReadOnlyList<DriveFree> C(double gb) => [new DriveFree(@"C:\", gb)];

    [Test]
    public void The_floor_and_its_margin_are_settings_with_defaults() =>
        Assert.That((S.DiskHoldGb, S.DiskHoldResumeMarginGb), Is.EqualTo((5d, 2d)));

    [TestCase(50.0, false, false)]
    [TestCase(5.0, false, false)]   // at the floor isn't under it
    [TestCase(4.9, false, true)]
    [TestCase(0.001, false, true)]
    [TestCase(6.9, true, true)]     // once held, the margin keeps it held
    [TestCase(7.0, true, false)]    // clear of the floor by the margin
    [TestCase(6.9, false, false)]   // never held: the margin doesn't apply
    public void Held_under_the_floor_and_until_clear_of_it_by_the_margin(double free, bool wasHeld, bool held) =>
        Assert.That(DiskGuard.Held(free, 5, 2, wasHeld), Is.EqualTo(held));

    [Test]
    public void A_floor_of_zero_never_holds()
    {
        Assert.That(DiskGuard.Held(0, 0, 2, wasHeld: true), Is.False);
        var reclaims = 0;
        var g = new DiskGuard { Read = _ => C(0.1), Reclaim = (_, _) => { reclaims++; return "x"; } };
        Assert.That(g.Observe(S with { DiskHoldGb = 0 }, T0), Is.Null);
        Assert.That(reclaims, Is.Zero, "off, it deletes nothing either");
    }

    [Test]
    public void With_room_nothing_is_held_and_nothing_is_reclaimed()
    {
        var reclaims = 0;
        var g = new DiskGuard { Read = _ => C(40), Reclaim = (_, _) => { reclaims++; return "x"; } };
        Assert.That(g.Observe(S, T0), Is.Null);
        Assert.That(g.Last, Is.Null);
        Assert.That(reclaims, Is.Zero);
    }

    [Test]
    public void Under_the_floor_the_reclaim_runs_first_and_a_drive_it_got_back_is_not_held()
    {
        var free = 3.0;
        var g = new DiskGuard { Read = _ => C(free), Reclaim = (_, _) => { free = 12; return "Freed 9.0 GB"; } };
        Assert.That(g.Observe(S, T0), Is.Null, "the reclaim got it over the floor: no hold");
        Assert.That(g.LastReclaim, Is.EqualTo("Freed 9.0 GB"));
    }

    [Test]
    public void Still_under_the_floor_after_the_reclaim_new_work_is_held_and_told_why()
    {
        var free = 3.0;
        var reclaims = 0;
        var g = new DiskGuard { Read = _ => C(free), Reclaim = (_, _) => { reclaims++; free = 3.4; return "Freed 400 MB"; } };
        var hold = g.Observe(S, T0);
        Assert.That(hold, Is.Not.Null);
        Assert.That(reclaims, Is.EqualTo(1));
        Assert.That(hold!.Drive.FreeGb, Is.EqualTo(3.4), "judged on the reading after the reclaim");
        Assert.That((hold.FloorGb, hold.ResumeGb), Is.EqualTo((5d, 7d)));
        Assert.That(hold.NeededGb, Is.EqualTo(3.6).Within(1e-9));
        Assert.That(hold.Why, Does.Contain(@"C:\").And.Contain("3.4 GB free").And.Contain("5 GB floor").And.Contain("until 7 GB is free")
            .And.Contain("free 3.6 GB more").And.Contain("Running work carries on"));
        Assert.That(g.Last, Is.SameAs(hold));
    }

    [Test]
    public void The_hold_lifts_by_itself_only_once_clear_of_the_floor_by_the_margin()
    {
        var free = 2.0;
        var g = new DiskGuard { Read = _ => C(free) };
        Assert.That(g.Observe(S, T0), Is.Not.Null);
        free = 5.5;
        Assert.That(g.Observe(S, T0.AddMinutes(1)), Is.Not.Null, "over the floor but inside the margin: still held, so it doesn't flap");
        free = 7.1;
        Assert.That(g.Observe(S, T0.AddMinutes(2)), Is.Null, "clear by the margin: lifted");
        free = 5.5;
        Assert.That(g.Observe(S, T0.AddMinutes(3)), Is.Null, "and not held again until it's under the floor itself");
        Assert.That(g.Observe(S with { DiskHoldResumeMarginGb = 0 }, T0.AddMinutes(4)), Is.Null);
    }

    [Test]
    public void The_reclaim_runs_at_most_every_cleanup_interval_and_not_inside_the_margin()
    {
        var free = 2.0;
        var reclaims = 0;
        var g = new DiskGuard { Read = _ => C(free), Reclaim = (_, _) => { reclaims++; return null; } };
        g.Observe(S, T0);
        g.Observe(S, T0.AddMinutes(1));
        Assert.That(reclaims, Is.EqualTo(1), "not again inside DiskCleanupEveryMinutes");
        g.Observe(S, T0.AddMinutes(S.DiskCleanupEveryMinutes));
        Assert.That(reclaims, Is.EqualTo(2), "again once it has passed");
        free = 6;
        g.Observe(S, T0.AddMinutes(60));
        Assert.That(reclaims, Is.EqualTo(2), "over the floor, waiting out the margin: nothing to reclaim for");
    }

    [Test]
    public void A_reclaim_that_fails_holds_rather_than_throws()
    {
        var g = new DiskGuard { Read = _ => C(1), Reclaim = (_, _) => throw new IOException("in use") };
        Assert.That(g.Observe(S, T0), Is.Not.Null);
        Assert.That(g.LastReclaim, Does.Contain("in use"));
    }

    [Test]
    public void The_fullest_watched_drive_counts_and_unreadable_drives_change_nothing()
    {
        IReadOnlyList<DriveFree> drives = [new DriveFree(@"C:\", 300), new DriveFree(@"D:\", 2)];
        var g = new DiskGuard { Read = _ => drives };
        Assert.That(g.Observe(S, T0)!.Drive.Root, Is.EqualTo(@"D:\"));
        drives = [];
        Assert.That(g.Observe(S, T0.AddMinutes(1)), Is.Not.Null, "nothing read: the last judgment stands");
        Assert.That(new DiskGuard { Read = _ => [] }.Observe(S, T0), Is.Null, "and a drive never read holds nothing");
    }

    [Test]
    public void A_process_told_the_daemon_already_holds_applies_the_margin_too()
    {
        var g = new DiskGuard { Read = _ => C(6) };
        Assert.That(g.Observe(S, T0), Is.Null, "on its own a fresh process reads 6 GB as over the floor");
        Assert.That(new DiskGuard { Read = _ => C(6) }.Observe(S, T0, heldElsewhere: true), Is.Not.Null, "the daemon holds: inside the margin stays held");
        Assert.That(new DiskGuard { Read = _ => C(40) }.Observe(S, T0, heldElsewhere: true), Is.Null, "a stale hold can't keep a drive with room held");
    }

    // ── The pace carries the reason ─────────────────────────────────────────

    [Test]
    public void The_pace_carries_the_hold_and_its_reason_and_changes_no_allowance()
    {
        var pace = new Pace(PaceMode.Open, 4, 1_000, 0, null, "5-hour", null, "On pace.") { SubagentAllowance = 8, Subagents = 3 };
        Assert.That(Pacer.HoldForDisk(pace, null), Is.SameAs(pace));
        var held = Pacer.HoldForDisk(pace, new DiskHold(new DriveFree(@"C:\", 3), 5, 7));
        Assert.That(held.Disk, Is.Not.Null);
        Assert.That(held.Note, Does.StartWith("On pace.").And.Contain("under the 5 GB floor").And.Contain("free 4 GB more"));
        Assert.That(held.SubagentAllowance, Is.EqualTo(8), "a floor, not a throttle: running work and its allowance are left alone");
    }

    // ── The daemon says so once ─────────────────────────────────────────────

    [Test]
    public void The_daemon_alerts_once_when_the_hold_starts_and_logs_when_it_lifts()
    {
        var dir = Path.Combine(Path.GetTempPath(), "orch-diskhold-" + Guid.NewGuid().ToString("N"));
        try
        {
            var free = 50.0;
            var said = new List<(LogEventLevel Level, string Line)>();
            var alerts = new List<(string Title, string Message, bool Urgent)>();
            var daemon = new Daemon(new JsonStore(Path.Combine(dir, "store")), (l, line) => said.Add((l, line)))
            {
                AlertSink = (t, m, u) => alerts.Add((t, m, u)),
            };
            var g = new DiskGuard { Read = _ => C(free), Reclaim = (_, _) => "Freed 0 MB" };

            daemon.WatchDiskHold(S, g);
            Assert.That(alerts, Is.Empty);

            free = 3;
            daemon.WatchDiskHold(S, g);
            daemon.WatchDiskHold(S, g);
            Assert.That(alerts, Has.Count.EqualTo(1));
            Assert.That(alerts[0].Title, Is.EqualTo("Disk full: holding new work"));
            Assert.That(alerts[0].Message, Does.Contain("free 4 GB more"));
            Assert.That(said.Select(x => x.Line), Has.Some.Contains("the self-cleanup ran first: Freed 0 MB"));

            free = 9;
            daemon.WatchDiskHold(S, g);
            Assert.That(alerts, Has.Count.EqualTo(1));
            Assert.That(said.Select(x => x.Line), Has.Some.Contains("space is back"));
        }
        finally
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}
