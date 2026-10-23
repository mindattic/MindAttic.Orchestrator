using System.Collections.ObjectModel;
using System.Windows.Input;
using MindAttic.Components.Wpf.Controls;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;

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
/// it's assigned to (a picker that assigns or reassigns it) and, while its user is out of room, why it waits and its
/// "Redelegate now".</summary>
public sealed class UserTicketRowViewModel : ObservableObject
{
    private readonly MainViewModel owner;
    private QueueItem item;
    private PersonOptionViewModel assignee;
    private string? waiting;

    public UserTicketRowViewModel(QueueItem item, MainViewModel owner)
    {
        this.item = item;
        this.owner = owner;
        assignee = owner.OptionFor(item.Assignee);
        RedelegateCommand = new RelayCommand(() => owner.Shown.RedelegateTicket(this.item.Id), () => IsWaiting && !IsMoving);
    }

    public string Id => item.Id;
    public string Repo => item.Repo;
    public string Type => item.Ticket.Type;
    public string Summary => item.Ticket.Summary;
    public string Priority => item.Priority == 0 ? "" : item.Priority.ToString("+#;-#");
    public bool IsMoving => item.Moving is not null || PendingMoves.ForTicket(item.Id) is not null;

    /// <summary>Off while a CLI moving to another login holds it (M.ORCH-T637).</summary>
    public bool CanAssign => !IsMoving;

    /// <summary>Everyone a ticket can be assigned to, "Unassigned" first: the same options on every row.</summary>
    public ObservableCollection<PersonOptionViewModel> People => owner.People;

    /// <summary>Who it's assigned to. Choosing someone assigns it to them (or reassigns it); "Unassigned" takes it off them. A
    /// choice cleared by the list changing under it (null) changes nothing.</summary>
    public PersonOptionViewModel Assignee
    {
        get => assignee;
        set
        {
            if (value is null || ReferenceEquals(value, assignee) || IsMoving) return;
            assignee = value;
            Raise();
            owner.Shown.AssignTicket(item.Id, value.Email);
        }
    }

    /// <summary>"Waiting for Ryan: week at 97%" while its user is out of room and it isn't being worked; null otherwise.</summary>
    public string? Waiting => waiting;
    public bool IsWaiting => waiting is not null;

    /// <summary>What's happening to it: being worked (and on which login), waiting for its user, or what the queue says.</summary>
    public string Status => waiting ?? item switch
    {
        { Assignment: { } a, IsAssigned: true } => $"Being worked since {Formatting.Ago(a.StartedUtc, DateTime.UtcNow)}" + (a.Account is { } on ? $" on {on}" : ""),
        { BlockedBy.Count: > 0 } => $"Waits for {string.Join(", ", item.BlockedBy)}",
        { TooBig: true } => $"Too big for what's left ({item.Estimate})",
        { Assignee: { } who } => $"Dispatched only on {owner.NameOf(who)}'s login",
        _ => item.Estimate is { } e ? $"~{Formatting.Tokens(e.Fresh)}" : "",
    };

    public ICommand RedelegateCommand { get; }

    public void Update(QueueItem next)
    {
        item = next;
        assignee = owner.OptionFor(next.Assignee);
        waiting = next is { Assignee: { } who, IsAssigned: false } && owner.OptionFor(who).OutOfRoom is { } why
            ? TicketPins.WaitingText(owner.NameOf(who), why) : null;
        Raise(string.Empty);
    }
}

/// <summary>
/// Tickets per user (M.ORCH-T668): under each person's open CLIs, their Tickets view lists the tickets assigned to them and the
/// unassigned queue, each with a picker (the library's MaPicker, M.COMP-T52) to assign it or reassign it to another user. An
/// assignment is a pin (<see cref="TicketPins"/>): it's dispatched only on its user's login, and while they're out of room it
/// waits for them, its card saying why, until its "Redelegate now" hands it on like an unassigned ticket.
/// </summary>
public sealed partial class MainViewModel
{
    private readonly ObservableCollection<PersonOptionViewModel> people = [];
    private readonly PersonOptionViewModel nobody = new(null);

    /// <summary>"Unassigned", then every person with a tab, by their tab's name; the same list in every tab.</summary>
    public ObservableCollection<PersonOptionViewModel> People => Root.people;

    /// <summary>This tab's person's tickets: the ones assigned to them, in queue order.</summary>
    public ObservableCollection<UserTicketRowViewModel> AssignedTickets { get; } = [];

    /// <summary>The unassigned queue, in queue order, as this tab shows it (the same tickets on every tab).</summary>
    public ObservableCollection<UserTicketRowViewModel> UnassignedTickets { get; } = [];

    /// <summary>This tab's person's login: its own email, or on the global login's tab the email signed in to it.</summary>
    public string? PersonEmail => tab.IsApiKey ? null : AccountDirs.Normalize(tab.Email ?? Root.full?.GlobalEmail);

    /// <summary>The Tickets view shows on a person's signed-in tab whose login is known.</summary>
    public bool ShowsUserTickets => IsTabSignedIn && PersonEmail is not null;

    /// <summary>Nothing is assigned to this tab's person: the view says how to assign them something.</summary>
    public bool HasNoAssigned => AssignedTickets.Count == 0;

    public string AssignedHeader => $"ASSIGNED TO {LoginName.ToUpperInvariant()} · {AssignedTickets.Count}";
    public string UnassignedHeader => $"UNASSIGNED · {UnassignedTickets.Count}";
    public string NoAssignedText => $"Nothing is assigned to {LoginName}. Pick them in a ticket's Assigned to below: it's then dispatched only on their login.";

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

    /// <summary>Brings the people and every tab's ticket lists up to date with the plan and the roster's last reading; on the
    /// global login's tab, after each plan and each roster read.</summary>
    private void ApplyUserTickets()
    {
        if (!IsGlobalTab) { Root.ApplyUserTickets(); return; }
        var now = DateTime.UtcNow;
        var settings = (FiveHour: (double)paceTargetPercent, Week: (double)weeklyStopPercent);
        var tabs = loginTabs.Where(t => !t.tab.IsApiKey && t.PersonEmail is not null).ToList();
        var wanted = new List<PersonOptionViewModel> { nobody };
        foreach (var t in tabs)
        {
            var email = t.PersonEmail!;
            var option = people.FirstOrDefault(p => p.Email == email) ?? new PersonOptionViewModel(email);
            var row = accountRows.FirstOrDefault(r => TicketPins.Same(r.Email, email));
            var week = row?.WeekUsedAt(now);
            option.Update(t.LoginName, week is { } w ? $"week {Math.Max(0, 100 - w):0}% left" : "",
                TicketPins.OutOfRoom(t.tab.SignedIn, row?.FiveHourUsedAt(now), week, settings.FiveHour, settings.Week), t.tab.SignedIn);
            wanted.Add(option);
        }
        // An assignee with no tab here keeps an option, so their tickets still show who has them.
        var pinned = plan?.Queue.Select(q => q.Assignee).OfType<string>().ToHashSet(StringComparer.Ordinal) ?? [];
        wanted.AddRange(people.Where(p => p.Email is { } e && pinned.Contains(e) && !wanted.Contains(p)));
        people.ReorderTo(wanted);

        var queue = plan?.Queue ?? [];
        foreach (var t in loginTabs)
        {
            var email = t.PersonEmail;
            t.AssignedTickets.SyncBy([.. queue.Where(q => email is not null && TicketPins.Same(q.Assignee, email))], q => q.Id, r => r.Id,
                q => { var r = new UserTicketRowViewModel(q, this); r.Update(q); return r; }, (r, q) => r.Update(q));
            t.UnassignedTickets.SyncBy([.. queue.Where(q => q.Assignee is null && !q.IsAssigned)], q => q.Id, r => r.Id,
                q => { var r = new UserTicketRowViewModel(q, this); r.Update(q); return r; }, (r, q) => r.Update(q));
            foreach (var name in new[] { nameof(AssignedHeader), nameof(UnassignedHeader), nameof(NoAssignedText), nameof(HasNoAssigned), nameof(PersonEmail), nameof(ShowsUserTickets) })
                t.Raise(name);
        }
    }

    /// <summary>Assigns ticket <paramref name="ticketId"/> to the user signed in as <paramref name="email"/>, or reassigns it;
    /// null takes it off them. Its login is recorded, and it's dispatched only there from now on.</summary>
    public void AssignTicket(string ticketId, string? email) => Guarded("Assign", () =>
    {
        if (Locked(ticketId)) return;
        var change = email is null ? TicketPins.Unassign(store, ticketId) : TicketPins.Assign(store, ticketId, email);
        QueueMessage = change.Text;
        queueLog.Information("{Line:l}", change.Text);
        NoteLocalChange();
        if (full is not null) Root.ApplyPlan(full);
    });

    /// <summary>"Redelegate now" on a pinned ticket waiting for its user: the pin goes, and it's handed on like an unassigned ticket
    /// (another user with room, else the API key).</summary>
    public void RedelegateTicket(string ticketId) => Guarded("Redelegate", () =>
    {
        if (Locked(ticketId)) return;
        var change = TicketPins.Redelegate(store, ticketId);
        QueueMessage = change.From is { } from
            ? $"{change.TicketId} no longer waits for {NameOf(from)}: it's handed on like an unassigned ticket, to another user with room, else the API key."
            : change.Text;
        queueLog.Information("Redelegated {Ticket:l} from {From:l}", change.TicketId, change.From ?? "nobody");
        NoteLocalChange();
        if (full is not null) Root.ApplyPlan(full);
    });
}
