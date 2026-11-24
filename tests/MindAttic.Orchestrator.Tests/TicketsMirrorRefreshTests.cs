using MindAttic.Orchestrator.Setup;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T2164: refreshing the client's ticket mirror clears the old database's SQLite sidecars and keeps a locally
/// changed mirror, with its WAL, beside the fresh copy (M.ORCH-T2144). Runs in a temp folder, never the real mirror.</summary>
public sealed class TicketsMirrorRefreshTests
{
    private string temp = "";

    [SetUp]
    public void SetUp()
    {
        temp = Path.Combine(Path.GetTempPath(), "MindAtticOrchMirrorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(temp, "MindAttic.Tickets", "data"));
        Directory.CreateDirectory(Path.Combine(temp, "mirror"));
    }

    [TearDown]
    public void TearDown()
    {
        try { if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true); } catch { /* best-effort */ }
    }

    [Test]
    public void Refresh_removes_stale_sidecars_and_keeps_the_old_wal_beside_the_local_backup()
    {
        File.WriteAllText(Path.Combine(temp, "MindAttic.Tickets", "data", "Tickets.db"), "host");
        var mirror = Path.Combine(temp, "mirror", "Tickets.db");
        File.WriteAllText(mirror, "local");
        File.WriteAllText(mirror + "-wal", "local-wal");
        File.WriteAllText(mirror + "-shm", "local-shm");
        File.WriteAllText(mirror + "-journal", "local-journal");
        var said = new List<string>();

        new MachineSetup(said.Add, temp).RefreshTicketsMirror(mirror);

        Assert.That(File.ReadAllText(mirror), Is.EqualTo("host"));
        foreach (var sidecar in new[] { "-wal", "-shm", "-journal" })
            Assert.That(File.Exists(mirror + sidecar), Is.False, sidecar);
        var kept = Directory.GetFiles(Path.Combine(temp, "mirror"), "Tickets.local-*.db");
        Assert.That(kept, Has.Length.EqualTo(1));
        Assert.That(File.ReadAllText(kept[0]), Is.EqualTo("local"));
        Assert.That(File.ReadAllText(kept[0] + "-wal"), Is.EqualTo("local-wal"));
        Assert.That(File.Exists(mirror + ".copied"), Is.True);
        Assert.That(said, Has.Some.Contains(kept[0]));
    }
}
