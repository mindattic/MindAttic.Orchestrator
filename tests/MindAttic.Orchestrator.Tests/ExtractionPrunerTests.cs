using MindAttic.Orchestrator.Setup;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class ExtractionPrunerTests
{
    private string root = "";

    [SetUp]
    public void SetUp() => root = Directory.CreateTempSubdirectory("orch-extract-").FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(root, recursive: true);

    private string Build(string app, string hash, int bytes)
    {
        var dir = Path.Combine(root, app, hash);
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "wpfgfx_cor3.dll"), new byte[bytes]);
        return dir;
    }

    [Test]
    public void Old_builds_are_deleted_and_one_a_running_copy_holds_open_is_left()
    {
        var old1 = Build("MindAttic.Orchestrator", "BJZR4SuR2wKJ", 100);
        var old2 = Build("MindAttic.Orchestrator", "hdW3ULKXB9yl", 50);
        var running = Build("MindAttic.Orchestrator", "ZnoYCztVtBgV", 10);
        var other = Build("MindAttic.Launcher", "S4BnOP1IGpFx", 10);

        (int, long) pruned;
        using (File.Open(Path.Combine(running, "wpfgfx_cor3.dll"), FileMode.Open, FileAccess.Read, FileShare.Read))
            pruned = ExtractionPruner.Prune(root, "MindAttic.Orchestrator");

        Assert.That(pruned, Is.EqualTo((2, 150L)));
        Assert.That(Directory.Exists(old1) || Directory.Exists(old2), Is.False);
        Assert.That(File.Exists(Path.Combine(running, "wpfgfx_cor3.dll")), Is.True, "a live build keeps every file");
        Assert.That(Directory.Exists(other), Is.True, "another app's builds aren't this one's to prune");
        Assert.That(Directory.GetDirectories(Path.Combine(root, "MindAttic.Orchestrator")), Is.EqualTo(new[] { running }));
    }

    [Test]
    public void A_folder_left_half_deleted_by_an_earlier_prune_is_finished_off()
    {
        Build("MindAttic.Orchestrator", "8b4IqB4KyI-b.prune", 20);
        Assert.That(ExtractionPruner.Prune(root, "MindAttic.Orchestrator"), Is.EqualTo((1, 20L)));
        Assert.That(Directory.GetDirectories(Path.Combine(root, "MindAttic.Orchestrator")), Is.Empty);
    }

    [Test]
    public void No_extraction_folder_yet_prunes_nothing()
    {
        Assert.That(ExtractionPruner.Prune(root, "MindAttic.Orchestrator"), Is.EqualTo((0, 0L)));
    }
}
