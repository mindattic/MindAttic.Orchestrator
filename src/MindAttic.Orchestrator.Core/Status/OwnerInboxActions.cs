using MindAttic.Orchestrator.Tickets;
using MindAttic.Tickets.Data;

namespace MindAttic.Orchestrator.Status;

/// <summary>
/// What the dashboard's Inbox (M.ORCH-T1732) can do to a ticket right there, without leaving for Tickets Desktop: approve it,
/// clear its <c>waiting-on-owner</c> label, or delegate it to someone (or back to nobody); and to an addition waiting to join
/// a locked milestone (M.ORCH-T1685): approve or decline it. Each opens the workspace's ticket
/// database for the one call and closes it again; <see cref="InvalidOperationException"/> when it isn't migrated yet, the same
/// as a stale Inbox row pointing at a ticket that's since gone.
/// </summary>
public static class OwnerInboxActions
{
    /// <summary>Approves an open ticket (<see cref="TicketsDb.Approve"/>): what the Inbox's "Approve" button and "Work it now"
    /// both do first.</summary>
    public static void Approve(string workspaceRoot, string id)
    {
        using var db = Open(workspaceRoot);
        db.Approve(id);
    }

    /// <summary>Clears <see cref="OpenTicketSource.WaitingOnOwnerLabel"/>, so the ticket is handed out again like any other
    /// queued one.</summary>
    public static void ClearWaiting(string workspaceRoot, string id)
    {
        using var db = Open(workspaceRoot);
        db.RemoveLabels(id, [OpenTicketSource.WaitingOnOwnerLabel]);
    }

    /// <summary>Assigns <paramref name="id"/> to <paramref name="email"/> (null: unassigns it), the same change Tickets
    /// Desktop's "Assigned to" picker makes (<see cref="TicketsDb.Assign"/>, <see cref="TicketsDb.Unassign"/>).</summary>
    public static AssignChange Delegate(string workspaceRoot, string id, string? email)
    {
        using var db = Open(workspaceRoot);
        return email is null ? db.Unassign(id) : db.Assign(id, email);
    }

    /// <summary>The owner approves a pending milestone addition (M.ORCH-T1685, <see cref="TicketsDb.ApproveMilestoneAddition"/>):
    /// <paramref name="item"/> becomes a member of <paramref name="milestone"/>, approved by <paramref name="by"/>.</summary>
    public static void ApproveAddition(string workspaceRoot, string milestone, string item, string by)
    {
        using var db = Open(workspaceRoot);
        db.ApproveMilestoneAddition(milestone, item, by);
    }

    /// <summary>The owner declines a pending milestone addition (<see cref="TicketsDb.DeclineMilestoneAddition"/>): it stops
    /// waiting and never was a member, declined by <paramref name="by"/> with <paramref name="reason"/> when one is given.</summary>
    public static void DeclineAddition(string workspaceRoot, string milestone, string item, string by, string? reason = null)
    {
        using var db = Open(workspaceRoot);
        db.DeclineMilestoneAddition(milestone, item, by, reason);
    }

    private static TicketsDb Open(string workspaceRoot) =>
        TicketsDb.OpenIfMigrated(workspaceRoot) ?? throw new InvalidOperationException("No migrated ticket database yet.");
}
