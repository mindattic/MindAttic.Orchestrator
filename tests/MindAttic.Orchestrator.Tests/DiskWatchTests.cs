using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;
using Serilog.Events;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T471: the daemon alerts when the system or worktree drive runs low, naming what fills %TEMP%.</summary>
public sealed class DiskWatchTests
{
    private string dir = null!;

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "orch-disk-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
    }

    [TearDown]
    public void TearDown() { try { Directory.Delete(dir, recursive: true); } catch (IOException) { } }

    private static IReadOnlyList<DriveFree> C(double gb) => [new DriveFree(@"C:\", gb)];

    [TestCase(50, HealthLevel.Ok)]
    [TestCase(19.9, HealthLevel.Warn)]
    [TestCase(4.9, HealthLevel.Danger)]
    [TestCase(0.001, HealthLevel.Danger)]
    public void Free_space_under_the_lines_is_warn_then_danger(double free, HealthLevel level) =>
        Assert.That(DiskWatch.Judge(free, 20, 5), Is.EqualTo(level));

    [Test]
    public void A_warn_line_of_zero_turns_the_watch_off() => Assert.That(DiskWatch.Judge(0, 0, 0), Is.EqualTo(HealthLevel.Ok));

    [Test]
    public void Coming_back_up_it_needs_to_be_clear_of_the_line()
    {
        Assert.That(DiskWatch.Judge(21, 20, 5, HealthLevel.Warn), Is.EqualTo(HealthLevel.Warn));
        Assert.That(DiskWatch.Judge(23, 20, 5, HealthLevel.Warn), Is.EqualTo(HealthLevel.Ok));
        Assert.That(DiskWatch.Judge(6, 20, 5, HealthLevel.Danger), Is.EqualTo(HealthLevel.Danger));
        Assert.That(DiskWatch.Judge(10, 20, 5, HealthLevel.Danger), Is.EqualTo(HealthLevel.Warn));
    }

    [Test]
    public void It_alerts_per_step_down_and_each_time_free_space_halves_not_every_tick()
    {
        var watch = new DiskWatch();
        bool Alerts(double gb) => watch.Observe(C(gb), 20, 5)!.Alert;
        Assert.That(Alerts(40), Is.False);
        Assert.That(Alerts(18), Is.True, "past the warn line");
        Assert.That(Alerts(17), Is.False, "same episode, barely moved");
        Assert.That(Alerts(9), Is.True, "halved since the last alert: still filling");
        Assert.That(Alerts(8), Is.False);
        Assert.That(Alerts(4.9), Is.True, "past the critical line");
        Assert.That(Alerts(3), Is.False);
        Assert.That(Alerts(2.4), Is.True, "halved again");
        Assert.That(Alerts(0), Is.True, "the drive filled");
        Assert.That(Alerts(0), Is.False, "full is said once, not every tick");
        Assert.That(Alerts(40), Is.False);
        Assert.That(watch.Level, Is.EqualTo(HealthLevel.Ok));
        Assert.That(Alerts(18), Is.True, "a new episode alerts again");
    }

    [Test]
    public void The_fullest_drive_counts()
    {
        var e = new DiskWatch().Observe([new DriveFree(@"C:\", 300), new DriveFree(@"D:\", 2)], 20, 5)!;
        Assert.That(e.Drive.Root, Is.EqualTo(@"D:\"));
        Assert.That(e.Level, Is.EqualTo(HealthLevel.Danger));
    }

    [Test]
    public void The_watched_drives_are_the_system_drive_and_the_worktree_roots_once_each()
    {
        var system = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows))!;
        Assert.That(DiskWatch.Roots(Path.Combine(system, "worktrees")), Is.EqualTo(new[] { system }));
        Assert.That(DiskWatch.Roots(null), Is.EqualTo(new[] { system }));
        Assert.That(DiskWatch.Read(DiskWatch.Roots(null)), Has.Count.EqualTo(1));
    }

    private void Write(string relative, int bytes)
    {
        var path = Path.Combine(dir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
    }

    [Test]
    public void It_names_the_biggest_temp_folders_and_the_biggest_files_agents_left()
    {
        Write(@"claude\D--Projects-X\sess1\tasks\runaway.output", 5000);
        Write(@"claude\D--Projects-X\sess1\scratchpad\bin\app.dll", 300);
        Write(@"claude\D--Projects-Y\sess2\tasks\small.output", 10);
        Write(@".net\App\abc\native.dll", 2000);
        Write(@"empty-ish\a.txt", 1);
        Write("loose.txt", 99999);

        var folders = DiskWatch.LargestFolders(dir, 2);
        Assert.That(folders.Select(f => f.Path), Is.EqualTo(new[] { "claude", ".net" }));
        Assert.That(folders[0].Bytes, Is.EqualTo(5310));

        var files = DiskWatch.LargestFiles(Path.Combine(dir, "claude"), 2);
        Assert.That(files.Select(f => f.Path), Is.EqualTo(new[] { @"D--Projects-X\sess1\tasks\runaway.output", @"D--Projects-X\sess1\scratchpad\bin\app.dll" }));
        Assert.That(DiskWatch.LargestFiles(Path.Combine(dir, "missing")), Is.Empty);
    }

    [Test]
    public void The_daemon_pushes_one_alert_naming_the_culprit_and_logs_the_recovery()
    {
        Write(@"claude\D--Projects-X\sess1\tasks\runaway.output", 5000);
        var free = 50.0;
        var said = new List<(LogEventLevel Level, string Line)>();
        var alerts = new List<(string Title, string Message, bool Urgent)>();
        var daemon = new Daemon(new JsonStore(Path.Combine(dir, "store")), (l, line) => said.Add((l, line)))
        {
            AlertSink = (t, m, u) => alerts.Add((t, m, u)),
            ReadDrives = _ => C(free),
            TempDir = dir,
        };
        var s = new OrchestratorSettings();

        daemon.WatchDisk(s);
        Assert.That(alerts, Is.Empty);

        free = 3;
        daemon.WatchDisk(s);
        daemon.WatchDisk(s);
        Assert.That(alerts, Has.Count.EqualTo(1));
        Assert.That(alerts[0].Title, Is.EqualTo("Disk almost full"));
        Assert.That(alerts[0].Urgent, Is.True);
        Assert.That(alerts[0].Message, Does.Contain(@"C:\ has 3.0 GB free").And.Contain("claude 0 MB")
            .And.Contain(@"D--Projects-X\sess1\tasks\runaway.output"));

        free = 100;
        daemon.WatchDisk(s);
        Assert.That(said.Last().Line, Does.Contain("back to normal"));

        free = 1;
        daemon.WatchDisk(s with { DiskWarnGb = 0 });
        Assert.That(alerts, Has.Count.EqualTo(1), "0 turns the watch off");
    }
}
