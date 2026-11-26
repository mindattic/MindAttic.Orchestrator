using MindAttic.Orchestrator.Sessions;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T2285: the dashboard's Launcher tabs run from a snapshot of the deploy folder, so a redeploy can't stop them.</summary>
public sealed class LauncherRunCopyTests
{
    private readonly TempDirs temp = new();

    [TearDown]
    public void TearDown() => temp.DeleteAll();

    private static string Deploy(string app, string content, DateTime written)
    {
        var release = Path.Combine(app, "Release");
        Directory.CreateDirectory(release);
        var exe = Path.Combine(release, "MindAttic.Launcher.exe");
        File.WriteAllText(exe, content);
        File.WriteAllText(Path.Combine(release, "repo.path"), @"D:\W\MindAttic.Launcher");
        File.SetLastWriteTimeUtc(exe, written);
        return exe;
    }

    [Test]
    public void A_build_is_snapshotted_once_beside_the_deploy_folder_with_its_files()
    {
        var app = temp.New();
        var exe = Deploy(app, "v1", new DateTime(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc));
        var copy = LauncherRunCopy.Resolve(exe);
        Assert.That(copy, Does.StartWith(Path.Combine(app, LauncherRunCopy.FolderName) + Path.DirectorySeparatorChar));
        Assert.That(File.ReadAllText(copy), Is.EqualTo("v1"));
        Assert.That(File.Exists(Path.Combine(Path.GetDirectoryName(copy)!, "repo.path")), Is.True);
        Assert.That(LauncherRunCopy.Resolve(exe), Is.EqualTo(copy));
    }

    [Test]
    public void A_new_build_is_snapshotted_in_the_background_and_the_unused_old_one_goes()
    {
        var grace = LauncherRunCopy.PickedGrace;
        LauncherRunCopy.PickedGrace = TimeSpan.Zero;
        try
        {
            var app = temp.New();
            var exe = Deploy(app, "v1", new DateTime(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc));
            var first = LauncherRunCopy.Resolve(exe);
            Deploy(app, "v2!", new DateTime(2026, 11, 26, 13, 0, 0, DateTimeKind.Utc));
            // Meanwhile the old snapshot runs; the new one is answered once its background copy is done.
            var second = first;
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (second == first && DateTime.UtcNow < deadline)
            {
                second = LauncherRunCopy.Resolve(exe);
                if (second == first) Thread.Sleep(50);
            }
            Assert.That(second, Is.Not.EqualTo(first));
            Assert.That(File.ReadAllText(second), Is.EqualTo("v2!"));
            Thread.Sleep(20);
            LauncherRunCopy.Resolve(exe);
            Assert.That(Directory.Exists(Path.GetDirectoryName(first)), Is.False);
        }
        finally { LauncherRunCopy.PickedGrace = grace; }
    }

    [Test]
    public void Mid_redeploy_with_no_deployed_exe_the_newest_snapshot_runs()
    {
        var app = temp.New();
        var exe = Deploy(app, "v1", new DateTime(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc));
        var copy = LauncherRunCopy.Resolve(exe);
        Directory.Delete(Path.GetDirectoryName(exe)!, recursive: true);
        Assert.That(LauncherRunCopy.Resolve(exe), Is.EqualTo(copy));
    }

    [Test]
    public void With_nothing_deployed_and_no_snapshot_the_exe_itself_is_answered()
    {
        var exe = Path.Combine(temp.New(), "Release", "MindAttic.Launcher.exe");
        Assert.That(LauncherRunCopy.Resolve(exe), Is.EqualTo(exe));
    }
}
