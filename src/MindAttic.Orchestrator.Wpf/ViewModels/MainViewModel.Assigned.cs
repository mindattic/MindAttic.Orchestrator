using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;
using MindAttic.Components.Wpf.Controls;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Tickets;
using MindAttic.Tickets.Log;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>One choice in a ticket's "Assigned to" picker (M.ORCH-T668): a user by their login's email, with a status light
/// (room, out of room, not signed in here) and their week left; or "Unassigned" (<see cref="Email"/> null).</summary>
public sealed class PersonOptionViewModel(string? email) : ObservableObject
{
    private string name = "Unassigned";
    private string trailing = "";
    private MaStatus? status;
    private string? statusLabel;
    private string? outOfRoom;

    /// <summary>The user's login, null for "Unassigned".</summary>
    public string? Email { get; } = email;
    public string Name { get => name; private set => Set(ref name, value); }
    public string Trailing { get => trailing; private set => Set(ref trailing, value); }
    public MaStatus? Status { get => status; private set => Set(ref status, value); }
    public string? StatusLabel { get => statusLabel; private set => Set(ref statusLabel, value); }

    /// <summary>Why they can't take their tickets now ("week at 97%"); null while they have room.</summary>
    public string? OutOfRoom { get => outOfRoom; private set => Set(ref outOfRoom, value); }

    public void Update(string nextName, string nextTrailing, string? why, bool signedIn)
    {
        Name = nextName;
        Trailing = nextTrailing;
        OutOfRoom = why;
        Status = !signedIn ? MaStatus.Bad : why is null ? MaStatus.Good : MaStatus.Warning;
        StatusLabel = !signedIn ? "Not signed in here" : why is null ? "Has room" : "Out of room";
    }
}

/// <summary>A ticket on a user's Tickets view (M.ORCH-T668): one assigned to them, or one of the unassigned queue, with who
/// it's assigned to and, while its user is out of room, why it waits. The assignment is the ticket database's, read through the
/// engine: assigning, reassigning and "Redelegate now" are done in Tickets Desktop, which the row's links open (M.ORCH-T762).</summary>
public sealed class UserTicketRowViewModel : ObservableObject
{
    private readonly MainViewModel owner;
    private QueueItem item;
    private PersonOptionViewModel assignee;
    private string? waiting;
    private DateTime? closedUtc;

    public UserTicketRowViewModel(QueueItem item, MainViewModel owner)
    {
        this.item = item;
        this.owner = owner;
        assignee = owner.OptionFor(item.Assignee);
    }

    /// <summary>A closed or won't-do ticket on the Queue's Closed or Won't do tab (M.ORCH-T849): when it closed, if known.</summary>
    public UserTicketRowViewModel(ClosedTicket closed, MainViewModel owner) : this(closed.AsItem(), owner) => closedUtc = closed.ClosedUtc;

    public string Id => item.Id;
    public string Repo => item.Repo;
    public string Type => item.Ticket.Type;
    public string Summary => item.Ticket.Summary;
    public string Priority => item.Priority == 0 ? "" : item.Priority.ToString("+#;-#");

    /// <summary>Who it's assigned to ("Unassigned" for nobody), with their room as a status light.</summary>
    public PersonOptionViewModel Assignee => assignee;

    /// <summary>Assigned to someone: their status light shows.</summary>
    public bool IsPinned => assignee.Email is not null;

    /// <summary>"Assigned to Ryan", or "Unassigned".</summary>
    public string AssigneeText => assignee.Email is null ? "Unassigned" : $"Assigned to {assignee.Name}";

    /// <summary>"Waiting for Ryan: week at 97%" while its user is out of room and it isn't being worked; null otherwise.</summary>
    public string? Waiting => waiting;
    public bool IsWaiting => waiting is not null;

    /// <summary>What's happening to it: being worked (and on which login), waiting for its user, or what the queue says.</summary>
    public string Status => waiting ?? item switch
    {
        { Ticket.Status: TicketRules.WontDo } => "Won't do" + (closedUtc is { } w ? $", closed {Formatting.Ago(w, DateTime.UtcNow)}" : ""),
        { Ticket.Status: TicketRules.Closed } => "Closed" + (closedUtc is { } c ? $" {Formatting.Ago(c, DateTime.UtcNow)}" : ""),
        { Assignment: { } a, IsAssigned: true } => $"Being worked since {Formatting.Ago(a.StartedUtc, DateTime.UtcNow)}" + (a.Account is { } on ? $" on {on}" : ""),
        { BlockedBy.Count: > 0 } => $"Waits for {string.Join(", ", item.BlockedBy)}",
        { MayWork: false } => "Waits for approval",
        { TooBig: true } => $"Too big for what's left ({item.Estimate})",
        { Assignee: { } who } => $"Dispatched only on {owner.NameOf(who)}'s login",
        _ => item.Estimate is { } e ? $"~{Formatting.Tokens(e.Fresh)}" : "",
    };

    /// <summary>A closed row's refresh (M.ORCH-T849): its ticket and when it closed.</summary>
    public void Update(ClosedTicket next)
    {
        closedUtc = next.ClosedUtc;
        Update(next.AsItem());
    }

    public void Update(QueueItem next)
    {
        item = next;
        assignee = owner.OptionFor(next.Assignee);
        waiting = next is { Assignee: { } who, IsAssigned: false } && owner.OptionFor(who).OutOfRoom is { } why
            ? TicketPins.WaitingText(owner.NameOf(who), why) : null;
        Raise(string.Empty);
    }
}

/// <summary>Which of a person's tickets a Queue tab lists (M.ORCH-T849).</summary>
public enum QueueStatus
{
    /// <summary>Open and claimed or being worked.</summary>
    Active,
    /// <summary>Open and waiting: unapproved, waiting on a prerequisite, or queued.</summary>
    Pending,
    /// <summary>Closed within <see cref="ClosedTicketSource.Window"/>.</summary>
    Closed,
    /// <summary>Closed as won't do within <see cref="ClosedTicketSource.Window"/>.</summary>
    WontDo,
}

/// <summary>One tab of a person's Queue (M.ORCH-T849): its tickets, and their count in its header.</summary>
public sealed class QueueTabViewModel(QueueStatus status, string name) : ObservableObject
{
    public QueueStatus Status { get; } = status;
    public string Name { get; } = name;
    public ObservableCollection<UserTicketRowViewModel> Tickets { get; } = [];

    /// <summary>The count the tab's header shows after its name.</summary>
    public string CountText => Tickets.Count.ToString(CultureInfo.InvariantCulture);

    /// <summary>"Active: 3 tickets", the tab's accessible name.</summary>
    public string AccessibleName => $"{Name}: {Tickets.Count} ticket{(Tickets.Count == 1 ? "" : "s")}";

    public void RaiseCount()
    {
        Raise(nameof(CountText));
        Raise(nameof(AccessibleName));
    }
}

/// <summary>
/// Tickets per user (M.ORCH-T668): under each person's open CLIs, their Tickets view lists the tickets assigned to them (the
/// global login's tab also lists the unassigned queue, M.ORCH-T842), each saying who has it. An assignment is a pin (<see cref="TicketPins"/>): it's dispatched only on its user's
/// login, and while they're out of room it waits for them, its card saying why. The assignment itself is made, changed or dropped
/// in Tickets Desktop (M.ORCH-T762), which every ticket here links to.
/// </summary>
public sealed partial class MainViewModel
{
    private readonly ObservableCollection<PersonOptionViewModel> people = [];
    private readonly ObservableCollection<PersonOptionViewModel> orgPeople = [];
    private readonly PersonOptionViewModel nobody = new(null);

    /// <summary>"Unassigned", then every person of this tab's org (M.ORCH-T754) with a tab, by their tab's name, and anyone of the
    /// org a ticket is pinned to with no tab here. With one org, everyone. Every option is also in the global login's tab's list of
    /// all of them, which names an assignee wherever they are.</summary>
    public ObservableCollection<PersonOptionViewModel> People => orgPeople;

    /// <summary>This tab's person's tickets: the ones assigned to them, in queue order.</summary>
    public ObservableCollection<UserTicketRowViewModel> AssignedTickets { get; } = [];

    /// <summary>The unassigned queue, in queue order, on the global login's tab only (M.ORCH-T842): a person's page shows only
    /// their own queue, and this is the dashboard's one place to see an unassigned ticket and reach "Assign in Tickets".</summary>
    public ObservableCollection<UserTicketRowViewModel> UnassignedTickets { get; } = [];

    /// <summary>This tab's person's login: its own email, or on the global login's tab the email signed in to it.</summary>
    public string? PersonEmail => tab.IsApiKey ? null : AccountDirs.Normalize(tab.Email ?? Root.full?.GlobalEmail);

    /// <summary>The Tickets view shows on a person's signed-in tab whose login is known.</summary>
    public bool ShowsUserTickets => IsTabSignedIn && PersonEmail is not null;

    /// <summary>The unassigned queue shows on the global login's tab only (M.ORCH-T842).</summary>
    public bool ShowsUnassignedTickets => ShowsUserTickets && IsGlobalTab;

    /// <summary>Nothing is assigned to this tab's person: the view says how to assign them something.</summary>
    public bool HasNoAssigned => AssignedTickets.Count == 0;

    public string AssignedHeader => $"ASSIGNED TO {LoginName.ToUpperInvariant()} · {AssignedTickets.Count}";
    public string UnassignedHeader => $"UNASSIGNED · {UnassignedTickets.Count}";
    public string NoAssignedText => $"Nothing is assigned to {LoginName}. Assign a ticket to them in Tickets Desktop (Assign in Tickets below): it's then dispatched only on their login.";

    /// <summary>The option for <paramref name="email"/>: its person's, or one made for an assignee with no tab here (named by their
    /// email); "Unassigned" for null.</summary>
    internal PersonOptionViewModel OptionFor(string? email)
    {
        if (AccountDirs.Normalize(email) is not { } e) return Root.nobody;
        if (Root.people.FirstOrDefault(p => p.Email == e) is { } known) return known;
        var made = new PersonOptionViewModel(e);
        made.Update(e, "", "not signed in on this machine", signedIn: false);
        Root.people.Add(made);
        return made;
    }

    /// <summary>A user's name as their tab is titled ("Ryan"), else their email.</summary>
    internal string NameOf(string email) => Root.people.FirstOrDefault(p => TicketPins.Same(p.Email, email))?.Name ?? email;

    // The Queue's tabs (M.ORCH-T849).

    /// <summary>How often a refresh re-reads the recently closed tickets; a push (<see cref="ReloadTicketQueues"/>) reads at once.</summary>
    private static readonly TimeSpan ClosedReadEvery = TimeSpan.FromMinutes(1);

    private readonly ClosedTicketSource closedSource = new();
    private IReadOnlyList<ClosedTicket> closedTickets = [];
    private DateTime closedReadUtc = DateTime.MinValue;
    private bool closedReading;
    private bool closedReadAgain;
    private QueueTabViewModel? selectedQueueTab;

    /// <summary>The person's Queue by status: Active, Pending, Closed (the last 7 days) and Won't do, each with its count.</summary>
    public IReadOnlyList<QueueTabViewModel> QueueTabs { get; } =
    [
        new(QueueStatus.Active, "Active"),
        new(QueueStatus.Pending, "Pending"),
        new(QueueStatus.Closed, "Closed"),
        new(QueueStatus.WontDo, "Won't do"),
    ];

    /// <summary>The Queue tab shown; Active until another is picked.</summary>
    public QueueTabViewModel SelectedQueueTab
    {
        get => selectedQueueTab ?? QueueTabs[0];
        set
        {
            // The strip leaves the window's tree as the body scrolls, which can hand back null: keep the pick.
            if (value is null || ReferenceEquals(value, SelectedQueueTab)) return;
            selectedQueueTab = value;
            foreach (var name in new[] { nameof(SelectedQueueTab), nameof(QueueTickets), nameof(HasNoQueueTickets), nameof(NoQueueTicketsText) })
                Raise(name);
        }
    }

    /// <summary>The tickets of the Queue tab shown.</summary>
    public ObservableCollection<UserTicketRowViewModel> QueueTickets => SelectedQueueTab.Tickets;

    public bool HasNoQueueTickets => QueueTickets.Count == 0;

    public string NoQueueTicketsText => SelectedQueueTab.Status switch
    {
        QueueStatus.Active => $"Nothing assigned to {LoginName} is being worked right now.",
        QueueStatus.Pending => $"Nothing assigned to {LoginName} is waiting. Assign a ticket to them in Tickets Desktop (Assign in Tickets): it's then dispatched only on their login.",
        QueueStatus.Closed => $"Nothing assigned to {LoginName} closed in the last {ClosedTicketSource.Window.TotalDays:0} days.",
        _ => $"Nothing assigned to {LoginName} was closed as won't do in the last {ClosedTicketSource.Window.TotalDays:0} days.",
    };

    /// <summary>
    /// The one place the Queue's data loads (M.ORCH-T849): re-reads the recently closed tickets from the ticket database, then
    /// brings every tab's Queue (and the rest of the Tickets view) up to date with the plan. A refresh reaches it through
    /// <see cref="ApplyUserTickets"/>, at most once a minute for the closed read; a pushed ticket event (M.ORCH-T879) calls this
    /// to read at once. Never throws: a failed read keeps the last good one.
    /// </summary>
    public void ReloadTicketQueues()
    {
        try
        {
            Root.LoadClosedTickets(force: true);
            Root.ApplyUserTickets();
        }
        catch (Exception ex) { Trace.TraceWarning($"Queue reload failed: {ex.Message}"); }
    }

    /// <summary>Reads the recently closed tickets off the UI thread, then shows them; on the global login's tab, with a UI to
    /// return to. Without <paramref name="force"/>, only once <see cref="ClosedReadEvery"/> has passed since the last read.</summary>
    private void LoadClosedTickets(bool force)
    {
        if (closedReading) { closedReadAgain |= force; return; }
        var now = DateTime.UtcNow;
        if (!force && now - closedReadUtc < ClosedReadEvery) return;
        // No UI to come back to (a test, or before the window's up): nothing is read.
        if (SynchronizationContext.Current is null) return;
        var ui = TaskScheduler.FromCurrentSynchronizationContext();
        var workspaceRoot = WorkspaceRoot;
        closedReading = true;
        closedReadUtc = now;
        Task.Run(() =>
        {
            try { return closedSource.Read(workspaceRoot, now); }
            catch (Exception ex) { Trace.TraceWarning($"Closed tickets read failed: {ex.Message}"); return null; }
        }).ContinueWith(read =>
        {
            try
            {
                closedReading = false;
                if (read.Status == TaskStatus.RanToCompletion && read.Result is { } closed) ApplyClosedTickets(closed);
                if (closedReadAgain) { closedReadAgain = false; LoadClosedTickets(force: true); }
            }
            catch (Exception ex) { Trace.TraceWarning($"Closed tickets apply failed: {ex.Message}"); }
        }, CancellationToken.None, TaskContinuationOptions.None, ui);
    }

    /// <summary>Shows <paramref name="closed"/> on every tab's Closed and Won't do tabs.</summary>
    internal void ApplyClosedTickets(IReadOnlyList<ClosedTicket> closed)
    {
        Root.closedTickets = closed;
        Root.ApplyUserTickets();
    }

    /// <summary>This tab's Queue tabs from the plan's queue and the recently closed tickets.</summary>
    private void SyncQueueTabs(IReadOnlyList<QueueItem> queue, IReadOnlyList<ClosedTicket> closed)
    {
        var email = PersonEmail;
        List<QueueItem> mine = email is null ? [] : [.. queue.Where(q => TicketPins.Same(q.Assignee, email))];
        List<ClosedTicket> done = email is null ? [] : [.. closed.Where(c => TicketPins.Same(c.Assignee, email))];
        SyncOpen(QueueTabs[0].Tickets, mine.Where(q => q.IsAssigned));
        SyncOpen(QueueTabs[1].Tickets, mine.Where(q => !q.IsAssigned));
        SyncClosed(QueueTabs[2].Tickets, done.Where(c => c.Ticket.Status == TicketRules.Closed));
        SyncClosed(QueueTabs[3].Tickets, done.Where(c => c.Ticket.Status == TicketRules.WontDo));
        foreach (var tab in QueueTabs) tab.RaiseCount();
        Raise(nameof(HasNoQueueTickets));
        Raise(nameof(NoQueueTicketsText));
    }

    private void SyncOpen(ObservableCollection<UserTicketRowViewModel> rows, IEnumerable<QueueItem> items) =>
        rows.SyncBy([.. items], q => q.Id, r => r.Id, q => { var r = new UserTicketRowViewModel(q, this); r.Update(q); return r; }, (r, q) => r.Update(q));

    private void SyncClosed(ObservableCollection<UserTicketRowViewModel> rows, IEnumerable<ClosedTicket> items) =>
        rows.SyncBy([.. items], c => c.Id, r => r.Id, c => { var r = new UserTicketRowViewModel(c, this); r.Update(c); return r; }, (r, c) => r.Update(c));

    /// <summary>Brings the people and every tab's ticket lists up to date with the plan and the roster's last reading; on the
    /// global login's tab, after each plan and each roster read.</summary>
    private void ApplyUserTickets()
    {
        if (!IsGlobalTab) { Root.ApplyUserTickets(); return; }
        // The recently closed tickets, for each Queue's Closed and Won't do tabs (M.ORCH-T849): read off the UI thread at most
        // once a minute, shown when the read is back.
        try { LoadClosedTickets(force: false); }
        catch (Exception ex) { Trace.TraceWarning($"Closed tickets read failed: {ex.Message}"); }
        var now = DateTime.UtcNow;
        var settings = (FiveHour: (double)paceTargetPercent, Week: (double)weeklyStopPercent);
        var tabs = loginTabs.Where(t => !t.tab.IsApiKey && t.PersonEmail is not null).ToList();
        var wanted = new List<PersonOptionViewModel> { nobody };
        var orgOf = new Dictionary<PersonOptionViewModel, string>();
        foreach (var t in tabs)
        {
            var email = t.PersonEmail!;
            var option = people.FirstOrDefault(p => p.Email == email) ?? new PersonOptionViewModel(email);
            var row = accountRows.FirstOrDefault(r => TicketPins.Same(r.Email, email));
            var week = row?.WeekUsedAt(now);
            option.Update(t.LoginName, week is { } w ? $"week {Math.Max(0, 100 - w):0}% left" : "",
                TicketPins.OutOfRoom(t.tab.SignedIn, row?.FiveHourUsedAt(now), week, settings.FiveHour, settings.Week), t.tab.SignedIn);
            wanted.Add(option);
            orgOf[option] = t.OrgId;
        }
        // An assignee with no tab here keeps an option, so their tickets still show who has them.
        var pinned = plan?.Queue.Select(q => q.Assignee).OfType<string>().ToHashSet(StringComparer.Ordinal) ?? [];
        wanted.AddRange(people.Where(p => p.Email is { } e && pinned.Contains(e) && !wanted.Contains(p)));
        people.ReorderTo(wanted);

        var queue = plan?.Queue ?? [];
        foreach (var t in loginTabs)
        {
            var email = t.PersonEmail;
            var org = t.OrgId;
            t.orgPeople.ReorderTo([nobody, .. people.Where(p => p.Email is { } e && (orgOf.TryGetValue(p, out var o) ? o : orgs.OrgOfClaude(e)) == org)]);
            // Each tab's rows are its own, so their pickers offer the tab's org's people (M.ORCH-T754).
            t.AssignedTickets.SyncBy([.. queue.Where(q => email is not null && TicketPins.Same(q.Assignee, email))], q => q.Id, r => r.Id,
                q => { var r = new UserTicketRowViewModel(q, t); r.Update(q); return r; }, (r, q) => r.Update(q));
            // Only the global login's tab lists the unassigned queue (M.ORCH-T842); a person's page shows just their own.
            t.UnassignedTickets.SyncBy([.. queue.Where(q => t.IsGlobalTab && q.Assignee is null && !q.IsAssigned)], q => q.Id, r => r.Id,
                q => { var r = new UserTicketRowViewModel(q, t); r.Update(q); return r; }, (r, q) => r.Update(q));
            try { t.SyncQueueTabs(queue, closedTickets); }
            catch (Exception ex) { Trace.TraceWarning($"Queue tabs failed: {ex.Message}"); }
            foreach (var name in new[] { nameof(AssignedHeader), nameof(UnassignedHeader), nameof(NoAssignedText), nameof(HasNoAssigned), nameof(PersonEmail), nameof(ShowsUserTickets), nameof(ShowsUnassignedTickets) })
                t.Raise(name);
        }
    }
}
