using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>New CLIs and main-line suites hold while RAM is short (M.ORCH-T1496).</summary>
public sealed class MainlineTestsMemoryTests
{
    private MemoryGuard? before;

    [SetUp]
    public void SetUp() => before = MemoryGuard.Shared;

    [TearDown]
    public void TearDown() => MemoryGuard.Shared = before!;

    [Test]
    public void A_new_CLI_is_held_only_while_RAM_is_short()
    {
        MemoryGuard.Shared = new MemoryGuard { Read = () => new MemoryReading(3, 50, true) };
        Assert.That(MemoryGuard.StartHeld(new OrchestratorSettings()), Does.StartWith("RAM is low (3.0 GB RAM free"));
        Assert.That(MemoryGuard.StartHeld(new OrchestratorSettings { MemoryGuard = false }), Is.Null, "the guard off");

        MemoryGuard.Shared = new MemoryGuard { Read = () => new MemoryReading(12, 50, true) };
        Assert.That(MemoryGuard.StartHeld(new OrchestratorSettings()), Is.Null);
    }
}
