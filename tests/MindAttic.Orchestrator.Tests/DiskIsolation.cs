using MindAttic.Orchestrator.Activity;
using NUnit.Framework;

/// <summary>
/// The disk guard reads no drives for the whole test assembly, so what /allocate hands out never depends on how much free
/// space the machine running the tests has (M.ORCH-T1708). A test of the guard builds its own <see cref="DiskGuard"/> with a reading.
/// </summary>
[SetUpFixture]
public sealed class DiskIsolation
{
    private DiskGuard? before;

    [OneTimeSetUp]
    public void ReadNoDrives()
    {
        before = DiskGuard.Shared;
        DiskGuard.Shared = new DiskGuard { Read = _ => [] };
    }

    [OneTimeTearDown]
    public void PutTheGuardBack()
    {
        if (before is not null) DiskGuard.Shared = before;
    }
}
