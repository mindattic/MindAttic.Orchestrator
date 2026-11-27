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

    /// <summary><see cref="Delegate"/> for each of <paramref name="ids"/> on one open database (the Roadmap's whole-branch
    /// assign, M.ORCH-T2395); one that fails doesn't stop the rest. Returns the ids that failed, with why.</summary>
    public static IReadOnlyList<(string Id, string Error)> DelegateAll(string workspaceRoot, IEnumerable<string> ids, string? email)
    {
        using var db = Open(workspaceRoot);
        var failed = new List<(string Id, string Error)>();
        foreach (var id in ids)
        {
            try { _ = email is null ? db.Unassign(id) : db.Assign(id, email); }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or System.Data.Common.DbException)
            {
                failed.Add((id, ex.Message));
            }
        }
        return failed;
    }

    /// <summary>The Roadmap's whole-branch assign to a user (M.ORCH-T2397): <see cref="DelegateAll"/>, then each of
    /// <paramref name="ids"/> approved (the owner's yes, so the branch is work), and <paramref name="root"/> labelled
    /// <see cref="Scheduling.AssignedDispatch.GateLabel"/> when it's a container, so its tasks are dispatched to the user's login
    /// and its suite runs once they're all done (the feature test gate). Returns the ids that failed, with why.</summary>
    public static IReadOnlyList<(string Id, string Error)> AssignBranch(string workspaceRoot, string root, bool container, IReadOnlyList<string> ids, string email)
    {
        var failed = DelegateAll(workspaceRoot, ids, email).ToList();
        using var db = Open(workspaceRoot);
        foreach (var id in ids)
        {
            try { db.Approve(id); }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or System.Data.Common.DbException)
            {
                failed.Add((id, ex.Message));
            }
        }
        if (container)
        {
            try { db.AddLabels(root, [Scheduling.AssignedDispatch.GateLabel]); }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or System.Data.Common.DbException)
            {
                failed.Add((root, ex.Message));
            }
        }
        return failed;
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
