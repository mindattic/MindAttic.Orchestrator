using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Status;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>One row of the owner inbox (M.ORCH-T1689): what waits on the owner, since when, and what to do about it. A ticket's
/// row links its id into Tickets Desktop, like every ticket id in the window.</summary>
public sealed class InboxRowViewModel(OwnerInboxItem item, DateTime nowUtc)
{
    /// <summary>What names it across refreshes.</summary>
    public string Key { get; } = item.Key;
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
        foreach (var item in items) inboxRows.Add(new InboxRowViewModel(item, now));
        if (same) return;
        foreach (var name in new[] { nameof(InboxCount), nameof(InboxBadge), nameof(HasInbox), nameof(InboxToolTip), nameof(InboxTitle) })
            RaiseOnEveryTab(name);
    }

    /// <summary>Everything waiting on the owner, most pressing kind first.</summary>
    public ObservableCollection<InboxRowViewModel> InboxItems => Root.inboxRows;

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
}
