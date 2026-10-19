using System.Runtime.InteropServices;
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
    public void Old_builds_are_deleted_and_every_file_of_one_a_running_copy_has_loaded_is_left()
    {
        var old1 = Build("MindAttic.Orchestrator", "BJZR4SuR2wKJ", 100);
        var old2 = Build("MindAttic.Orchestrator", "hdW3ULKXB9yl", 50);
        var running = Build("MindAttic.Orchestrator", "ZnoYCztVtBgV", 10);
        var other = Build("MindAttic.Launcher", "S4BnOP1IGpFx", 10);
        // A really loaded native library: it's mapped with delete sharing, so the folder could still be renamed and its
        // other files deleted under the running copy (what the first version of this did to the live daemon).
        var dll = Path.Combine(running, "e_sqlite3.dll");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", "e_sqlite3.dll"), dll);

        var handle = NativeLibrary.Load(dll);
        (int, long) pruned;
        try { pruned = ExtractionPruner.Prune(root, "MindAttic.Orchestrator"); }
        finally { NativeLibrary.Free(handle); }

        Assert.That(pruned, Is.EqualTo((2, 150L)));
        Assert.That(Directory.Exists(old1) || Directory.Exists(old2), Is.False);
        Assert.That(File.Exists(Path.Combine(running, "wpfgfx_cor3.dll")), Is.True, "a live build keeps the files it hasn't loaded yet too");
        Assert.That(Directory.Exists(other), Is.True, "another app's builds aren't this one's to prune");
        Assert.That(Directory.GetDirectories(Path.Combine(root, "MindAttic.Orchestrator")), Is.EqualTo(new[] { running }));
    }

    [Test]
    public void No_extraction_folder_yet_prunes_nothing()
    {
        Assert.That(ExtractionPruner.Prune(root, "MindAttic.Orchestrator"), Is.EqualTo((0, 0L)));
    }
}
