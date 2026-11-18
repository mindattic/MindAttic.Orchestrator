using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Input;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Status;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>One row of the owner inbox (M.ORCH-T1689): what waits on the owner, since when, and what to do about it. A ticket's
/// row links its id into Tickets Desktop, like every ticket id in the window. It can also act right there (M.ORCH-T1732):
/// Approve, Clear, Release and Delegate run against the ticket database directly, the Copy button stays for whoever would
/// rather paste the command into a terminal, and Sign in / Consent (not tickets) keep Copy as their only way. An addition
/// waiting to join a milestone (M.ORCH-T1685) can be approved or declined; a milestone's growth is only told.</summary>
public sealed class InboxRowViewModel(OwnerInboxItem item, DateTime nowUtc, IReadOnlyList<PersonOptionViewModel>? people = null)
{
    /// <summary>What names it across refreshes.</summary>
    public string Key { get; } = item.Key;
    /// <summary>The raw kind (<see cref="OwnerInbox"/>'s constants), for what buttons apply; <see cref="Kind"/> is its label.</summary>
    internal string RawKind { get; } = item.Kind;
    /// <summary>Its kind, as a word: Approve, Waiting on you, Stale, Orphaned, Sign in, Consent.</summary>
    public string Kind { get; } = OwnerInbox.KindLabel(item.Kind);
    /// <summary>Which one: a ticket id, a login's email, a consent request's id.</summary>
    public string Id { get; } = item.Id;
    /// <summary>What it is.</summary>
    public string Title { get; } = item.Title;
    /// <summary>The ticket it opens in Tickets Desktop; null when it isn't a ticket.</summary>
    public string? TicketId { get; } = item.TicketId;
    /// <summary>Whether it is a ticket (its id is a link).</summary>
    public bool IsTicket => TicketId is not null;
    /// <summary>Its repo and how long it has waited, "MindAttic.Orchestrator · 2d ago"; either part alone, or empty.</summary>
    public string Where { get; } = string.Join(" · ", new[] { item.Repo, item.SinceUtc is { } since ? Formatting.Ago(since, nowUtc) : null }.Where(p => !string.IsNullOrEmpty(p)));
    /// <summary>The command that answers it.</summary>
    public string Action { get; } = item.Action;
    /// <summary>What a screen reader says for the row.</summary>
    public string AccessibleName { get; } = $"{OwnerInbox.KindLabel(item.Kind)}: {item.Id}. {item.Title}. {item.Action}";

    /// <summary>Whether "Approve" applies: an open ticket waiting for approval.</summary>
    public bool CanApprove => RawKind == OwnerInbox.Approval;
    /// <summary>Whether "Clear" applies: a ticket parked with the <c>waiting-on-owner</c> label.</summary>
    public bool CanClearWaiting => RawKind == OwnerInbox.WaitingOnOwner;
    /// <summary>Whether "Release" applies: a stale or orphaned claim, the same release the Queue's own button runs.</summary>
    public bool CanRelease => RawKind is OwnerInbox.Orphaned or OwnerInbox.Stale;
    /// <summary>Whether "Work it now" applies: approving or clearing it is also enough to try dispatching it at once.</summary>
    public bool CanWorkNow => RawKind is OwnerInbox.Approval or OwnerInbox.WaitingOnOwner;
    /// <summary>Whether it can be delegated to someone else, or back to nobody: any ticket item.</summary>
    public bool CanDelegate => IsTicket;

    /// <summary>The milestone a waiting addition would join (M.ORCH-T1685); null for any other row.</summary>
    public string? MilestoneId { get; } = item.MilestoneId;
    /// <summary>The ticket or epic waiting to join <see cref="MilestoneId"/>; null for any other row.</summary>
    public string? MilestoneItem { get; } = item.MilestoneItem;
    /// <summary>Whether "Approve" and "Decline" for a milestone addition apply: a ticket or epic waiting to join a milestone.</summary>
    public bool CanAnswerAddition => RawKind == OwnerInbox.MilestoneAddition && MilestoneId is not null && MilestoneItem is not null;

    /// <summary>The Delegate picker's choice: starts on whoever already has it (<see cref="OwnerInboxItem.Assignee"/>), else
    /// "Unassigned". The ComboBox sets it as the row is browsed; only the Delegate button commits it, so browsing the list
    /// doesn't change anything by itself.</summary>
    public PersonOptionViewModel? Assignee { get; set; } =
        (people ?? []).FirstOrDefault(p => item.Assignee is { } email && string.Equals(p.Email, email, StringComparison.OrdinalIgnoreCase))
        ?? (people ?? []).FirstOrDefault(p => p.Email is null);
}

/// <summary>
/// The owner inbox in the dashboard (M.ORCH-T1689): the menu bar's Inbox button with its count, and the list it opens. Built
/// with the rest of the refresh, off the UI thread, from the snapshot and plan that refresh already made, plus the consent
/// requests it already polls. Kept on the global login's tab and shown from every tab, like the Task List.
/// </summary>
public sealed partial class MainViewModel
{
    private volatile IReadOnlyList<OwnerInboxItem>? inboxRead;
    private readonly ObservableCollection<InboxRowViewModel> inboxRows = [];
    private bool isInboxOpen;
    private ICommand? toggleInboxCommand, closeInboxCommand, copyInboxActionCommand;
    private ICommand? approveInboxCommand, clearWaitingInboxCommand, releaseInboxCommand, delegateInboxCommand, workInboxNowCommand;
    private ICommand? approveAdditionInboxCommand, declineAdditionInboxCommand;

    /// <summary>The inbox for <paramref name="built"/> and its <paramref name="plan"/>; null (the last one stays) when the refresh
    /// made no plan or a repo's tickets couldn't be read, when the list would be partial. Never throws.</summary>
    internal IReadOnlyList<OwnerInboxItem>? InboxNow(Snapshot built, SchedulePlan? plan, OrchestratorSettings? settings)
    {
        if (plan is null || built.UnknownRepos.Count > 0) return null;
        try { return OwnerInbox.Build(OwnerInbox.Read(store, built, plan, settings ?? new OrchestratorSettings(), DateTime.UtcNow, consentsRead)); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return null; }
    }

    /// <summary>Shows the inbox the refresh read: rows kept in its order, the count and its texts said on every tab.</summary>
    private void ApplyInbox()
    {
        if (!IsGlobalTab) { Root.ApplyInbox(); return; }
        if (inboxRead is not { } items) return;
        var now = DateTime.UtcNow;
        var same = inboxRows.Count == items.Count && inboxRows.Select(r => r.Key).SequenceEqual(items.Select(i => i.Key), StringComparer.OrdinalIgnoreCase);
        // Unchanged and on screen: left alone, so the open list keeps its scroll and focus. Otherwise rebuilt (the list is
        // short), which also moves each row's "2h ago" on.
        if (same && isInboxOpen) return;
        inboxRows.Clear();
        foreach (var item in items) inboxRows.Add(new InboxRowViewModel(item, now, people));
        if (same) return;
        foreach (var name in new[] { nameof(InboxCount), nameof(InboxBadge), nameof(HasInbox), nameof(InboxToolTip), nameof(InboxTitle) })
            RaiseOnEveryTab(name);
    }

    /// <summary>Everything waiting on the owner, most pressing kind first.</summary>
    public ObservableCollection<InboxRowViewModel> InboxItems => Root.inboxRows;

    /// <summary>Who a ticket row can be delegated to (M.ORCH-T1732): every known person, "Unassigned" first. Not scoped to any
    /// one tab's org, since a ticket here can go to anyone with a login on this machine.</summary>
    public ObservableCollection<PersonOptionViewModel> InboxAssignees => Root.people;

    /// <summary>How many items wait.</summary>
    public int InboxCount => Root.inboxRows.Count;

    /// <summary>Whether anything waits.</summary>
    public bool HasInbox => InboxCount > 0;

    /// <summary>The count on the Inbox button: empty with nothing waiting, "99+" past 99.</summary>
    public string InboxBadge => BadgeOf(InboxCount);

    internal static string BadgeOf(int count) => count <= 0 ? "" : count > 99 ? "99+" : count.ToString(CultureInfo.InvariantCulture);

    /// <summary>The Inbox button's tooltip and accessible name.</summary>
    public string InboxToolTip => InboxLine(InboxCount);

    internal static string InboxLine(int count) => count switch
    {
        <= 0 => "Inbox: nothing is waiting on you",
        1 => "Inbox: 1 item is waiting on you",
        _ => $"Inbox: {count} items are waiting on you",
    };

    /// <summary>The list's title, with its count.</summary>
    public string InboxTitle => InboxCount > 0 ? $"Inbox ({InboxCount})" : "Inbox";

    /// <summary>Whether the inbox list is open; the same on every tab.</summary>
    public bool IsInboxOpen { get => Root.isInboxOpen; set { Root.isInboxOpen = value; RaiseOnEveryTab(nameof(IsInboxOpen)); } }

    /// <summary>The menu bar's Inbox button: opens the list, or closes it.</summary>
    public ICommand ToggleInboxCommand => Root.toggleInboxCommand ??= new RelayCommand(() => Root.IsInboxOpen = !Root.IsInboxOpen);

    /// <summary>Closes the list (its close button, Esc, the backdrop).</summary>
    public ICommand CloseInboxCommand => Root.closeInboxCommand ??= new RelayCommand(() => Root.IsInboxOpen = false);

    /// <summary>Copies a row's command to the clipboard, to paste into a terminal.</summary>
    public ICommand CopyInboxActionCommand => Root.copyInboxActionCommand ??= new RelayCommand(p =>
    {
        if (p is not InboxRowViewModel row) return;
        try { System.Windows.Clipboard.SetText(row.Action); }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.ExternalException or InvalidOperationException) { /* the clipboard is held: nothing copied */ }
    });

    // ── Acting on a row directly (M.ORCH-T1732): each runs on whichever tab is on screen, like the Queue's own Start and
    // Release, so a ticket assigned to a user still opens on that user's login. ──

    /// <summary>Approves the row's ticket.</summary>
    public ICommand ApproveInboxCommand => Root.approveInboxCommand ??= new RelayCommand(p => { if (p is InboxRowViewModel row) Root.Shown.ApproveInbox(row); });
    /// <summary>Clears the row's <c>waiting-on-owner</c> label.</summary>
    public ICommand ClearWaitingInboxCommand => Root.clearWaitingInboxCommand ??= new RelayCommand(p => { if (p is InboxRowViewModel row) Root.Shown.ClearWaitingInbox(row); });
    /// <summary>Releases the row's stale or orphaned claim.</summary>
    public ICommand ReleaseInboxCommand => Root.releaseInboxCommand ??= new RelayCommand(p => { if (p is InboxRowViewModel row) Root.Shown.ReleaseInbox(row); });
    /// <summary>Delegates the row's ticket to its picker's choice (or back to nobody).</summary>
    public ICommand DelegateInboxCommand => Root.delegateInboxCommand ??= new RelayCommand(p => { if (p is InboxRowViewModel row) Root.Shown.DelegateInbox(row); });
    /// <summary>Approves or clears the row, then starts it at once if it's workable.</summary>
    public ICommand WorkInboxNowCommand => Root.workInboxNowCommand ??= new RelayCommand(p => { if (p is InboxRowViewModel row) Root.Shown.WorkInboxNow(row); });

    /// <summary>Approves the row's waiting milestone addition.</summary>
    public ICommand ApproveAdditionInboxCommand => Root.approveAdditionInboxCommand ??= new RelayCommand(p => { if (p is InboxRowViewModel row) Root.Shown.AnswerAdditionInbox(row, approve: true); });
    /// <summary>Declines the row's waiting milestone addition.</summary>
    public ICommand DeclineAdditionInboxCommand => Root.declineAdditionInboxCommand ??= new RelayCommand(p => { if (p is InboxRowViewModel row) Root.Shown.AnswerAdditionInbox(row, approve: false); });

    /// <summary>Approves or declines <paramref name="row"/>'s waiting milestone addition (M.ORCH-T1685,
    /// <see cref="OwnerInboxActions.ApproveAddition"/>, <see cref="OwnerInboxActions.DeclineAddition"/>) as this tab's person
    /// (<see cref="PersonEmail"/>), then refreshes: the row leaves the Inbox once nothing waits. A decline here gives no
    /// reason; <c>orchestrator milestone decline --reason</c> records one. With no person known it says so and changes nothing.</summary>
    public async void AnswerAdditionInbox(InboxRowViewModel row, bool approve)
    {
        if (!row.CanAnswerAddition) return;
        var what = approve ? "Approve" : "Decline";
        if (PersonEmail is not { } by)
        {
            QueueMessage = $"{what} failed: nobody is signed in on this tab, so there is no one to record. Run the command below with --by instead.";
            return;
        }
        try
        {
            if (approve) OwnerInboxActions.ApproveAddition(WorkspaceRoot, row.MilestoneId!, row.MilestoneItem!, by);
            else OwnerInboxActions.DeclineAddition(WorkspaceRoot, row.MilestoneId!, row.MilestoneItem!, by);
            QueueMessage = approve ? $"{row.MilestoneItem} is on {row.MilestoneId} now." : $"{row.MilestoneItem} is declined for {row.MilestoneId}.";
            await RefreshAsync();
            if (full is not null) Root.ApplyPlan(full);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or InvalidDataException or System.Data.Common.DbException or KeyNotFoundException or InvalidOperationException)
        {
            QueueMessage = $"{what} failed: {ex.Message}";
            OrchestratorLog.For("Dashboard").Error(ex, "Answering a milestone addition from the Inbox failed");
        }
    }

    /// <summary>Approves <paramref name="row"/>'s ticket (<see cref="OwnerInboxActions.Approve"/>), then refreshes: the row
    /// leaves the Inbox once the plan no longer counts it as awaiting approval.</summary>
    public async void ApproveInbox(InboxRowViewModel row)
    {
        if (!row.CanApprove || Locked(row.Id)) return;
        try
        {
            OwnerInboxActions.Approve(WorkspaceRoot, row.Id);
            QueueMessage = $"{row.Id} is approved.";
            await RefreshAsync();
            if (full is not null) Root.ApplyPlan(full);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Data.Common.DbException or KeyNotFoundException or InvalidOperationException)
        {
            QueueMessage = $"Approve failed: {ex.Message}";
            OrchestratorLog.For("Dashboard").Error(ex, "Approve from the Inbox failed");
        }
    }

    /// <summary>Clears <paramref name="row"/>'s <c>waiting-on-owner</c> label (<see cref="OwnerInboxActions.ClearWaiting"/>),
    /// then refreshes.</summary>
    public async void ClearWaitingInbox(InboxRowViewModel row)
    {
        if (!row.CanClearWaiting || Locked(row.Id)) return;
        try
        {
            OwnerInboxActions.ClearWaiting(WorkspaceRoot, row.Id);
            QueueMessage = $"{row.Id} is cleared: it's handed out again like any other queued ticket.";
            await RefreshAsync();
            if (full is not null) Root.ApplyPlan(full);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Data.Common.DbException or KeyNotFoundException or InvalidOperationException)
        {
            QueueMessage = $"Clear failed: {ex.Message}";
            OrchestratorLog.For("Dashboard").Error(ex, "Clear from the Inbox failed");
        }
    }

    /// <summary>Releases <paramref name="row"/>'s stale or orphaned claim: the same <see cref="Release"/> the Queue's own
    /// button runs.</summary>
    public void ReleaseInbox(InboxRowViewModel row)
    {
        if (row.CanRelease) Release(row.Id);
    }

    /// <summary>Delegates <paramref name="row"/>'s ticket to its picker's current choice, or back to nobody when it's
    /// "Unassigned" (<see cref="OwnerInboxActions.Delegate"/>), then refreshes.</summary>
    public async void DelegateInbox(InboxRowViewModel row)
    {
        if (!row.CanDelegate || Locked(row.Id)) return;
        try
        {
            var change = OwnerInboxActions.Delegate(WorkspaceRoot, row.Id, row.Assignee?.Email);
            QueueMessage = change.Text;
            await RefreshAsync();
            if (full is not null) Root.ApplyPlan(full);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or InvalidDataException or System.Data.Common.DbException or KeyNotFoundException or InvalidOperationException)
        {
            QueueMessage = $"Delegate failed: {ex.Message}";
            OrchestratorLog.For("Dashboard").Error(ex, "Delegate from the Inbox failed");
        }
    }

    /// <summary>Approves (or clears) <paramref name="row"/>, refreshes, then starts it at once through the same
    /// <see cref="Dispatch"/> the Queue's own Start button runs, if the refreshed plan says it's workable and nobody has it
    /// yet; otherwise says why not instead of silently doing nothing.</summary>
    public async void WorkInboxNow(InboxRowViewModel row)
    {
        if (!row.CanWorkNow || Locked(row.Id)) return;
        try
        {
            QueueMessage = $"Starting {row.Id}…";
            if (row.RawKind == OwnerInbox.Approval) OwnerInboxActions.Approve(WorkspaceRoot, row.Id);
            else OwnerInboxActions.ClearWaiting(WorkspaceRoot, row.Id);
            await RefreshAsync();
            if (full is not null) Root.ApplyPlan(full);
            var item = Plan?.Queue.FirstOrDefault(q => string.Equals(q.Id, row.Id, StringComparison.OrdinalIgnoreCase));
            if (item is null) { QueueMessage = $"{row.Id} is ready, but isn't in the queue yet (it may still be blocked)."; return; }
            if (item.IsAssigned) { QueueMessage = $"{row.Id} is already being worked."; return; }
            if (!item.MayWork) { QueueMessage = $"{row.Id} still isn't workable."; return; }
            Dispatch(item);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Data.Common.DbException or KeyNotFoundException or InvalidOperationException)
        {
            QueueMessage = $"Failed: {ex.Message}";
            OrchestratorLog.For("Dashboard").Error(ex, "Work-it-now from the Inbox failed");
        }
    }
}
