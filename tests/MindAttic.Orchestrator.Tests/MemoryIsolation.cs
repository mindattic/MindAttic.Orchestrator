using MindAttic.Orchestrator.Activity;
using NUnit.Framework;

/// <summary>
/// The memory guard reads nothing for the whole test assembly, so no pace a test works out depends on how much RAM the
/// machine running it has free (M.ORCH-T1494). A test of the guard builds its own <see cref="MemoryGuard"/> with a reading.
/// </summary>
[SetUpFixture]
public sealed class MemoryIsolation
{
    private MemoryGuard? before;

    [OneTimeSetUp]
    public void ReadNoMemory()
    {
        before = MemoryGuard.Shared;
        MemoryGuard.Shared = new MemoryGuard { Read = () => null };
    }

    [OneTimeTearDown]
    public void PutTheGuardBack()
    {
        if (before is not null) MemoryGuard.Shared = before;
    }
}
