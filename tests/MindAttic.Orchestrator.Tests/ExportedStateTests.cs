using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>A mark kept both as a <see cref="OrchestratorDb"/> state row and exported as its old JSON file
/// (<see cref="ExportedState"/>), because something outside the orchestrator reads that file.</summary>
public sealed class ExportedStateTests
{
    private string dir = "";

    [SetUp]
    public void SetUp() => dir = Path.Combine(Path.GetTempPath(), "orch-exported-state-" + Guid.NewGuid().ToString("N"));

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    [Test]
    public void Write_round_trips_through_the_row_and_the_file()
    {
        var db = new OrchestratorDb(Path.Combine(dir, "orchestrator.db"));
        var path = Path.Combine(dir, "state", "mark.json");
        ExportedState.Write(db, "mark", path, """{ "On": true }""");
        Assert.That(File.Exists(path), Is.True);
        Assert.That(File.ReadAllText(path), Is.EqualTo("""{ "On": true }"""));
        Assert.That(ExportedState.Read(db, "mark", path), Is.EqualTo("""{ "On": true }"""));
        Assert.That(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"), Is.Empty, "no leftover temp file on the ordinary path");
    }

    [Test]
    public void Write_deletes_its_temp_file_even_when_the_move_into_place_fails()
    {
        // A directory already sitting where the exported file would go makes File.Move throw: the write never completes,
        // but the per-write temp file it staged first must not be left behind (matches OrchestratorDb.WriteDump's cleanup).
        var db = new OrchestratorDb(Path.Combine(dir, "orchestrator.db"));
        var path = Path.Combine(dir, "state", "mark.json");
        Directory.CreateDirectory(path);

        Assert.That(() => ExportedState.Write(db, "mark", path, """{ "On": true }"""), Throws.Exception);

        var leftover = Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp");
        Assert.That(leftover, Is.Empty, "the staged temp file is cleaned up rather than orphaned");
    }
}
