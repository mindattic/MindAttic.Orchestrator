using MindAttic.Orchestrator.Status;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>What the Inbox's own buttons do to the ticket database (M.ORCH-T1732): each opens it for the one call. The happy
/// path is <see cref="MindAttic.Tickets.Data.TicketsDb"/>'s own (Approve, Assign, Unassign, RemoveLabels are tested there);
/// here, what a workspace with no migrated database yet does instead of silently doing nothing.</summary>
public sealed class OwnerInboxActionsTests
{
    private string root = "";

    [SetUp]
    public void SetUp() => root = Path.Combine(Path.GetTempPath(), "owner-inbox-actions-" + Guid.NewGuid().ToString("N"));

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    [Test]
    public void Approve_throws_instead_of_silently_doing_nothing_with_no_migrated_database()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => OwnerInboxActions.Approve(root, "A-T1"));
        Assert.That(ex!.Message, Does.Contain("migrated"));
    }

    [Test]
    public void ClearWaiting_throws_instead_of_silently_doing_nothing_with_no_migrated_database() =>
        Assert.Throws<InvalidOperationException>(() => OwnerInboxActions.ClearWaiting(root, "A-T1"));

    [Test]
    public void Delegate_throws_instead_of_silently_doing_nothing_with_no_migrated_database() =>
        Assert.Throws<InvalidOperationException>(() => OwnerInboxActions.Delegate(root, "A-T1", "amy@example.com"));
}
