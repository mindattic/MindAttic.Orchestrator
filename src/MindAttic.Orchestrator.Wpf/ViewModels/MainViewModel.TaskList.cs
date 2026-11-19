using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Windows.Media;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Status;
using MindAttic.Tickets.Data;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>One label and its value on the Task List's detail modal (M.ORCH-T1593): "Status" / "open".</summary>
public sealed record TaskDetailLine(string Label, string Value);

/// <summary>One process spending tokens right now, on the sidebar's Task List (M.ORCH-T1593): a CLI whose turn is running,
/// or one of its subagents. Named with its owner (the person whose login it bills) and the ticket it works, its accent
/// that person's tab colour, so who's spending on what reads at a glance across every login, not only the shown one.</summary>
public sealed class TaskRowViewModel : ObservableObject
{
    private AgentBoardRow cli;
    private SubagentActivity? subagent;
    private string owner = "";
    private string? ownerEmail;
    private Brush ownerBrush = Brushes.Gray;
    private string? caption;

    /// <summary>Creates the task row view model.</summary>
    public TaskRowViewModel(string key, AgentBoardRow cli, SubagentActivity? subagent)
    {
        Key = key;
        this.cli = cli;
        this.subagent = subagent;
    }

    /// <summary>The sync key: the CLI's session, or "session/agent" for one of its subagents.</summary>
    public string Key { get; }

    /// <summary>The cli.</summary>
    public AgentBoardRow Cli => cli;
    /// <summary>The subagent.</summary>
    public SubagentActivity? Subagent => subagent;
    /// <summary>The CLI's session (a subagent's own parent): the Play button's target (M.ORCH-T1770), always known for a
    /// live row.</summary>
    public string SessionId => cli.SessionId;
    /// <summary>The Play button's accessible name.</summary>
    public string PlayName => $"Jump to {Owner}'s {Kind}";
    /// <summary>Whether subagent.</summary>
    public bool IsSubagent => subagent is not null;
    /// <summary>A subagent row sits indented under its owner (the border margin the card binds).</summary>
    public System.Windows.Thickness Indent => IsSubagent ? new System.Windows.Thickness(12, 0, 0, 0) : new System.Windows.Thickness(0);

    /// <summary>The repo.</summary>
    public string Repo => cli.Repo;

    /// <summary>Who it bills: their tab's name ("Ryan").</summary>
    public string Owner => owner;
    /// <summary>The owner email.</summary>
    public string? OwnerEmail => ownerEmail;

    /// <summary>Their tab's colour (M.ORCH-T616): the row's accent stripe and owner name.</summary>
    public Brush OwnerBrush => ownerBrush;

    /// <summary>The ticket it works: the CLI's attributed ticket, else its first claim; a subagent's the ticket id its prompt
    /// names, else its CLI's. Null with none known.</summary>
    public string? TicketId => subagent is { } s ? MainViewModel.TicketIdIn(s.Task) ?? CliTicket : CliTicket;

    private string? CliTicket => cli.TicketId ?? cli.Claims.FirstOrDefault();

    /// <summary>"ID · friendly name" for its ticket, or "No ticket" while none is known.</summary>
    public string TicketText => TicketId is null ? "No ticket" : caption ?? TicketId;

    /// <summary>Whether ticket.</summary>
    public bool HasTicket => TicketId is not null;

    /// <summary>"CLI" or "Subagent (Explore)": which kind of process it is.</summary>
    public string Kind => subagent is { } s ? $"Subagent ({s.Type})" : cli.IsAllocate ? "Allocate agent" : "CLI";

    /// <summary>What it's doing now (its latest tool call), else what it was asked, else its state's own words.</summary>
    public string Doing => subagent is { } s ? s.Doing ?? s.Task ?? "Working" : cli.Doing is { Length: > 0 } d ? d : cli.StateDetail;

    /// <summary>The tokens.</summary>
    public string Tokens => Formatting.Tokens((subagent?.Usage ?? cli.Tokens).Total);

    /// <summary>The row's accessible name and tooltip: who, what kind, which repo and ticket, what it's doing.</summary>
    public string OpenName => $"{Owner}'s {Kind} on {Repo}, {TicketText}: {Doing}. Open details";

    internal void Update(AgentBoardRow nextCli, SubagentActivity? nextSubagent, string nextOwner, string? nextEmail, Brush nextBrush, string? nextCaption)
    {
        cli = nextCli;
        subagent = nextSubagent;
        owner = nextOwner;
        ownerEmail = nextEmail;
        ownerBrush = nextBrush;
        caption = nextCaption;
        Raise(string.Empty);
    }
}

/// <summary>
/// The sidebar's Task List (M.ORCH-T1593): every process spending tokens right now, across every login, each named with its
/// owner and its ticket and colour-coded by its owner's tab colour; clicking one opens a modal with the full ticket (or, with
/// no ticket, the CLI's own description). Kept on the global login's tab, which reads the whole workspace's snapshot, and
/// shown in every tab (MainWindow.xaml.cs binds it to the global tab like the login strip).
/// </summary>
public sealed partial class MainViewModel
{
    private ICommand? openTaskDetailCommand, closeTaskDetailCommand;
    private bool isTaskDetailOpen;
    /// <summary>The has task list.</summary>
    private string taskDetailTitle = "";
    private string? taskDetailTicketId;
    private string taskDetailDescription = "";

    /// <summary>Every process spending tokens now: each working CLI, then its running subagents under it.</summary>
    public ObservableCollection<TaskRowViewModel> TaskList => Root.taskList;
    private readonly ObservableCollection<TaskRowViewModel> taskList = [];

    /// <summary>Whether task list.</summary>
    public bool HasTaskList => Root.taskList.Count > 0;

    /// <summary>"Task List  3", its section label's count.</summary>
    public string TaskListCount => Root.taskList.Count.ToString(CultureInfo.InvariantCulture);

    private ICommand? jumpToCliCommand;

    // The sidebar's unified Active / Pending / top-priority sections (M.ORCH-T1770): a live task (a CLI or subagent, or a
    // live session with no ticket yet) and an open ticket sit in the same "Active" group instead of two separately labelled
    // ones - unified by binding one ItemsControl to a mixed collection and letting WPF pick each row's DataTemplate by its
    // own runtime type (MainWindow.xaml's SidebarTaskTemplate/SidebarActivityTemplate/SidebarTicketTemplate, each now
    // implicit - no x:Key - instead of only reachable by an explicit ItemTemplate). Every row has two independent click
    // targets: its Play button jumps to the CLI and the person working it (JumpToCliCommand, reusing the same lookup
    // Tickets Desktop's own "Show who is working X" uses), its Ticket button opens it in Tickets (OpenInTicketsCommand).

    /// <summary>The unified "Active" section: every live task first (the Task List, then a live session with no ticket
    /// attributed yet), then this login's own open tickets being worked that aren't already shown as one of those (so a
    /// ticket a live CLI already carries isn't listed twice).</summary>
    public IReadOnlyList<object> ActiveItems
    {
        get
        {
            var shown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var items = new List<object>(TaskList.Count + LiveActivity.Count + ActiveTab.Tickets.Count);
            foreach (var row in TaskList) { items.Add(row); if (row.TicketId is { } id) shown.Add(id); }
            foreach (var row in LiveActivity) items.Add(row);
            foreach (var row in ActiveTab.Tickets) if (shown.Add(row.Id)) items.Add(row);
            return items;
        }
    }

    /// <summary>Whether active items.</summary>
    public bool HasActiveItems => ActiveItems.Count > 0;
    /// <summary>"Active  5", the section label's count.</summary>
    public string ActiveCount => ActiveItems.Count.ToString(CultureInfo.InvariantCulture);

    /// <summary>The ticket id <paramref name="row"/> names, for the top-priority section's dedupe; null for a task row with
    /// none attributed yet.</summary>
    private static string? IdOf(object row) => row switch
    {
        TaskRowViewModel t => t.TicketId,
        UserTicketRowViewModel u => u.Id,
        _ => null,
    };

    /// <summary>The first 100 highest-priority open tickets (M.ORCH-T1770), in the scheduler's own priority order (explicit
    /// priority, then milestone, then graph weight, then recency - the same order <c>plan.Queue</c> is already built in):
    /// the workspace-wide backlog, after whatever's already shown above for this login in Active or Pending.</summary>
    public IReadOnlyList<UserTicketRowViewModel> Top100Items
    {
        get
        {
            var shown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in ActiveItems) if (IdOf(row) is { } id) shown.Add(id);
            foreach (var row in PendingTab.Tickets) shown.Add(row.Id);
            return [.. (Root.plan?.Queue ?? []).Where(q => !shown.Contains(q.Id)).Take(100).Select(q => new UserTicketRowViewModel(q, this))];
        }
    }

    /// <summary>Whether top100 items.</summary>
    public bool HasTop100Items => Top100Items.Count > 0;
    /// <summary>"Top priority  100", the section label's count.</summary>
    public string Top100Count => Top100Items.Count.ToString(CultureInfo.InvariantCulture);

    /// <summary>Every tab reads the unified sections again: after the Task List, the live activity or the ticket queues
    /// change.</summary>
    private void RaiseActiveSections()
    {
        Raise(nameof(ActiveItems)); Raise(nameof(HasActiveItems)); Raise(nameof(ActiveCount));
        Raise(nameof(Top100Items)); Raise(nameof(HasTop100Items)); Raise(nameof(Top100Count));
    }

    /// <summary>The Play button (M.ORCH-T1770): jumps to the CLI and the person working <paramref name="row"/> - a Task
    /// List row, a live session with no ticket yet, or a ticket card - reusing the same lookup Tickets Desktop's own "Show
    /// who is working X" uses (<see cref="ShowWorker"/>). A no-op for anything else, or a row with neither a session nor a
    /// ticket to look one up by.</summary>
    internal void JumpToCli(object? row)
    {
        var (session, login, ticket) = row switch
        {
            TaskRowViewModel t => (t.SessionId, t.Cli.OwnLogin, t.TicketId),
            LiveActivityRowViewModel a => ((string?)a.SessionId, (string?)null, (string?)null),
            UserTicketRowViewModel u => (u.SessionId, u.Assignee.Email, (string?)u.Id),
            _ => ((string?)null, (string?)null, (string?)null),
        };
        if (session is null && ticket is null) return;
        Root.ShowWorker(new ShowWorkerRequest { Id = ticket ?? "", Session = session, Login = login });
    }

    /// <summary>The Play button's command, bound to the clicked row itself (its type says which of Task List row, live
    /// activity row or ticket row it is).</summary>
    public ICommand JumpToCliCommand => Root.jumpToCliCommand ??= new RelayCommand(p => Root.JumpToCli(p));

    /// <summary>Whether task detail open.</summary>
    public bool IsTaskDetailOpen { get => Root.isTaskDetailOpen; set { Root.isTaskDetailOpen = value; RaiseOnEveryTab(nameof(IsTaskDetailOpen)); } }

    /// <summary>Every tab proxies the Task List to the global login's (the window shows whichever tab is chosen), so a change
    /// is said on each of them.</summary>
    private void RaiseOnEveryTab(string name)
    {
        foreach (var t in Root.loginTabs) t.Raise(name);
        if (!Root.loginTabs.Contains(Root)) Root.Raise(name);
    }
    /// <summary>The task detail title.</summary>
    public string TaskDetailTitle => Root.taskDetailTitle;

    /// <summary>The ticket's summary, or the CLI's own description ("Working on ...") with no ticket.</summary>
    public string TaskDetailDescription => Root.taskDetailDescription;

    /// <summary>The task detail lines.</summary>
    public ObservableCollection<TaskDetailLine> TaskDetailLines => Root.taskDetailLines;
    private readonly ObservableCollection<TaskDetailLine> taskDetailLines = [];

    /// <summary>The ticket the modal shows, for its "Open in Tickets"; null for a CLI with none.</summary>
    public string? TaskDetailTicketId => Root.taskDetailTicketId;
    /// <summary>Whether task detail ticket.</summary>
    public bool HasTaskDetailTicket => Root.taskDetailTicketId is not null;

    /// <summary>The paragraph/review button every card in the unified sidebar list shares (M.ORCH-T1825): a Task List row
    /// opens its own CLI/subagent detail (<see cref="OpenTaskDetail"/>), a ticket card opens its own ticket straight
    /// (<see cref="OpenTicketDetail"/>, the "bubbles up the ticket" case - it already is one), and a live, ticket-less
    /// activity card opens its own CLI's detail with no ticket to bubble up to (<see cref="OpenActivityDetail"/>).</summary>
    public ICommand OpenTaskDetailCommand => Root.openTaskDetailCommand ??= new RelayCommand(p =>
    {
        switch (p)
        {
            case TaskRowViewModel row: Root.OpenTaskDetail(row); break;
            case UserTicketRowViewModel row: Root.OpenTicketDetail(row); break;
            case LiveActivityRowViewModel row: Root.OpenActivityDetail(row); break;
        }
    });
    /// <summary>The close task detail command.</summary>
    public ICommand CloseTaskDetailCommand => Root.closeTaskDetailCommand ??= new RelayCommand(() => Root.IsTaskDetailOpen = false);

    private static readonly Regex TicketIdPattern = new(@"\b[A-Z][A-Z0-9]*(?:\.[A-Z0-9]+)*-T\d+\b", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>The first ticket id <paramref name="text"/> names ("Work on ticket M.ORCH-T1593 ..."), else null.</summary>
    internal static string? TicketIdIn(string? text) => text is null ? null : TicketIdPattern.Match(text) is { Success: true } m ? m.Value : null;

    /// <summary>Spending tokens now: its turn running (not merely held by the pace gate or waiting for you), or a subagent running.</summary>
    internal static bool IsSpending(AgentBoardRow a) => a.State == AgentState.Working || a.Subagents.Count > 0;

    /// <summary>The login tab whose login <paramref name="ownLogin"/> is (null: the global login's).</summary>
    private MainViewModel OwnerTabOf(string? ownLogin)
    {
        var email = AccountDirs.Normalize(ownLogin);
        if (email is null) return Root;
        return Root.loginTabs.FirstOrDefault(t => string.Equals(AccountDirs.Normalize(t.ScopeEmail), email, StringComparison.OrdinalIgnoreCase)) ?? Root;
    }

    /// <summary>Brings the Task List up to date with <paramref name="all"/>'s agent board, on the global login's tab.</summary>
    private void SyncTaskList(Snapshot all)
    {
        if (!IsGlobalTab) return;
        var rows = new List<(string Key, AgentBoardRow Cli, SubagentActivity? Sub)>();
        foreach (var a in all.Agents.Where(IsSpending).OrderBy(a => a.StartedUtc))
        {
            if (a.State == AgentState.Working) rows.Add((a.SessionId, a, null));
            rows.AddRange(a.Subagents.OrderBy(s => s.StartedUtc).Select(s => ($"{a.SessionId}/{s.AgentId}", a, (SubagentActivity?)s)));
        }
        taskList.SyncBy(rows, r => r.Key, v => v.Key, r => new TaskRowViewModel(r.Key, r.Cli, r.Sub), (v, r) => { });
        foreach (var (key, cli, sub) in rows)
        {
            var view = taskList.First(v => v.Key == key);
            var ownerTab = OwnerTabOf(cli.OwnLogin);
            var email = AccountDirs.Normalize(cli.OwnLogin) ?? ownerTab.PersonEmail;
            var ticket = sub is { } s ? TicketIdIn(s.Task) ?? cli.TicketId ?? cli.Claims.FirstOrDefault() : cli.TicketId ?? cli.Claims.FirstOrDefault();
            var caption = ticket is null ? null : TicketsDb.Caption(ticket, FriendlyNameOf(ticket), SummaryOf(ticket) ?? "");
            view.Update(cli, sub, ownerTab.TabTitle, email, CliAccentOr(cli.SessionId, ownerTab.TabBrush), caption);
        }
        RaiseOnEveryTab(nameof(HasTaskList));
        RaiseOnEveryTab(nameof(TaskListCount));
        // The unified Active section includes the Task List (M.ORCH-T1770): every tab's own reads it again.
        foreach (var t in Root.loginTabs) t.RaiseActiveSections();
        if (!Root.loginTabs.Contains(Root)) Root.RaiseActiveSections();
    }

    /// <summary>The open ticket's queue row, or null when it isn't in the open queue.</summary>
    private QueueItem? QueueItemOf(string id) => Root.plan?.Queue.FirstOrDefault(q => string.Equals(q.Id, id, StringComparison.OrdinalIgnoreCase));

    private string? SummaryOf(string id) =>
        QueueItemOf(id)?.Ticket.Summary ?? Root.closedTickets.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase))?.Ticket.Summary;

    /// <summary>A ticket's own detail lines (M.ORCH-T1825, split out of the old single <c>OpenTaskDetail</c> so a ticket
    /// card's own paragraph button can show exactly this, with no CLI/subagent wrapped around it): title, summary and the
    /// queue's (or the recently closed list's) own facts about <paramref name="id"/>; null when neither knows it.</summary>
    private (string Title, string Description, List<TaskDetailLine> Lines)? TicketDetailLinesOf(string id, string fallbackRepo, DateTime now)
    {
        var item = QueueItemOf(id);
        var closed = item is not null ? null : Root.closedTickets.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
        if ((item?.Ticket ?? closed?.Ticket) is not { } ticket) return null;
        var lines = new List<TaskDetailLine>
        {
            new("Ticket", ticket.Id),
            new("Repo", item?.Repo ?? closed?.Repo ?? fallbackRepo),
            new("Type", ticket.Type),
            new("Status", ticket.Status),
        };
        if (item is not null)
        {
            if (item.Priority != 0) lines.Add(new("Priority", item.Priority.ToString("+#;-#", CultureInfo.InvariantCulture)));
            lines.Add(new("Approved", item.MayWork ? "Yes" : "Waits for approval"));
            if (item.Assignee is { } who) lines.Add(new("Assigned to", NameOf(who)));
            if (item.Assignment is { } a)
                lines.Add(new("Being worked", $"since {Formatting.Ago(a.StartedUtc, now)}" + (a.Account is { } on ? $" on {on}" : "") + (a.Model is { } model ? $" ({model})" : "")));
            if (item.BlockedBy.Count > 0) lines.Add(new("Waits for", string.Join(", ", item.BlockedBy)));
            if (item.RequiredBy.Count > 0) lines.Add(new("Required by", string.Join(", ", item.RequiredBy)));
            if (item.Estimate is { } e) lines.Add(new("Estimate", $"~{Formatting.Tokens(e.Fresh)}"));
        }
        else if (closed?.ClosedUtc is { } at) lines.Add(new("Closed", Formatting.Ago(at, now)));
        if (ticket.Date.Length > 0) lines.Add(new("Opened", ticket.Date));
        if (ticket.Ref.Length > 0) lines.Add(new("Ref", ticket.Ref));
        return (TicketsDb.Caption(ticket.Id, FriendlyNameOf(ticket.Id), ticket.Summary), ticket.Summary, lines);
    }

    /// <summary>Fills and opens the detail modal for <paramref name="row"/>: its full ticket when it has one the queue (or the
    /// recently closed list) knows, else the CLI's own description, with who it bills and what it's doing either way.</summary>
    private void OpenTaskDetail(TaskRowViewModel row)
    {
        var now = DateTime.UtcNow;
        var cli = row.Cli;
        var id = row.TicketId;
        var found = id is null ? null : TicketDetailLinesOf(id, cli.Repo, now);
        string title, description;
        List<TaskDetailLine> lines;
        if (found is { } f)
        {
            (title, description, lines) = f;
        }
        else
        {
            title = $"{row.Kind} on {cli.Repo}";
            description = row.Subagent?.Task is { Length: > 0 } task ? task
                : cli.Doing is { Length: > 0 } doing ? doing : cli.StateDetail;
            lines = [];
            if (id is not null) lines.Add(new("Ticket", $"{id} (not in the open queue)"));
            lines.Add(new("Repo", cli.Repo));
        }
        lines.Add(new("Owner", row.OwnerEmail is { } email ? $"{row.Owner} ({email})" : row.Owner));
        lines.Add(new("Process", row.Kind));
        if (row.Subagent is { } sub)
        {
            lines.Add(new("Doing", sub.Doing ?? "—"));
            lines.Add(new("Started", Formatting.Ago(sub.StartedUtc, now)));
            lines.Add(new("Tokens", Formatting.Tokens(sub.Usage.Total)));
            lines.Add(new("Parent CLI", cli.SessionId));
        }
        else
        {
            lines.Add(new("State", cli.StateDetail));
            if (cli.Doing is { Length: > 0 } d) lines.Add(new("Doing", d));
            lines.Add(new("Started", Formatting.Ago(cli.StartedUtc, now)));
            lines.Add(new("Tokens", Formatting.Tokens(cli.Tokens.Total)));
            if (cli.Subagents.Count > 0) lines.Add(new("Subagents", cli.Subagents.Count.ToString(CultureInfo.InvariantCulture)));
            if (cli.Claims.Count > 0) lines.Add(new("Claims", string.Join(", ", cli.Claims)));
            if (cli.BackgroundJobs > 0) lines.Add(new("Background jobs", cli.BackgroundJobs.ToString(CultureInfo.InvariantCulture)));
            if (cli.Billing is { Length: > 0 } b) lines.Add(new("Billing", b));
            lines.Add(new("Session", cli.SessionId));
            if (cli.CliPid is { } pid) lines.Add(new("PID", pid.ToString(CultureInfo.InvariantCulture)));
        }
        ShowTaskDetail(title, description, found is not null ? id : null, lines);
    }

    /// <summary>Fills and opens the detail modal for <paramref name="row"/>'s own ticket (M.ORCH-T1825): the paragraph
    /// button's "bubbles up the ticket" case for a ticket card - it already is one, so this is <see cref="TicketDetailLinesOf"/>
    /// alone, plus who it's assigned to.</summary>
    private void OpenTicketDetail(UserTicketRowViewModel row)
    {
        var now = DateTime.UtcNow;
        var found = TicketDetailLinesOf(row.Id, row.Repo, now);
        var (title, description, lines) = found ?? (row.Caption, row.Summary, [new("Ticket", row.Id), new("Repo", row.Repo)]);
        lines.Add(new("Assigned to", row.AssigneeText));
        ShowTaskDetail(title, description, found is not null ? row.Id : null, lines);
    }

    /// <summary>Fills and opens the detail modal for <paramref name="row"/>'s own live, ticket-less CLI (M.ORCH-T1825): the
    /// paragraph button's case with no ticket to bubble up to, so this is just its repo and what it's doing, the same shape
    /// <see cref="OpenTaskDetail"/> falls back to for a CLI with none.</summary>
    private void OpenActivityDetail(LiveActivityRowViewModel row) =>
        ShowTaskDetail($"CLI on {row.Repo}", row.Summary, null, [new("Repo", row.Repo), new("Session", row.SessionId)]);

    /// <summary>The detail modal's own final step, every opener above ending with it: fills its title, description, ticket
    /// (for its "Open in Tickets", null to hide it) and lines, then shows it.</summary>
    private void ShowTaskDetail(string title, string description, string? ticketId, List<TaskDetailLine> lines)
    {
        Root.taskDetailTitle = title;
        Root.taskDetailDescription = description;
        Root.taskDetailTicketId = ticketId;
        Root.taskDetailLines.Clear();
        foreach (var line in lines) Root.taskDetailLines.Add(line);
        foreach (var name in new[] { nameof(TaskDetailTitle), nameof(TaskDetailDescription), nameof(TaskDetailTicketId), nameof(HasTaskDetailTicket) })
            RaiseOnEveryTab(name);
        IsTaskDetailOpen = true;
    }
}
