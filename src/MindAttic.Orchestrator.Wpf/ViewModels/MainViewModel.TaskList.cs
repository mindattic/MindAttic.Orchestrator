using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Windows.Media;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Sessions;
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

    public TaskRowViewModel(string key, AgentBoardRow cli, SubagentActivity? subagent)
    {
        Key = key;
        this.cli = cli;
        this.subagent = subagent;
    }

    /// <summary>The sync key: the CLI's session, or "session/agent" for one of its subagents.</summary>
    public string Key { get; }

    public AgentBoardRow Cli => cli;
    public SubagentActivity? Subagent => subagent;
    public bool IsSubagent => subagent is not null;

    public string Repo => cli.Repo;

    /// <summary>Who it bills: their tab's name ("Ryan").</summary>
    public string Owner => owner;
    public string? OwnerEmail => ownerEmail;

    /// <summary>Their tab's colour (M.ORCH-T616): the row's accent stripe and owner name.</summary>
    public Brush OwnerBrush => ownerBrush;

    /// <summary>The ticket it works: the CLI's attributed ticket, else its first claim; a subagent's the ticket id its prompt
    /// names, else its CLI's. Null with none known.</summary>
    public string? TicketId => subagent is { } s ? MainViewModel.TicketIdIn(s.Task) ?? CliTicket : CliTicket;

    private string? CliTicket => cli.TicketId ?? cli.Claims.FirstOrDefault();

    /// <summary>"ID · friendly name" for its ticket, or "No ticket" while none is known.</summary>
    public string TicketText => TicketId is null ? "No ticket" : caption ?? TicketId;

    public bool HasTicket => TicketId is not null;

    /// <summary>"CLI" or "Subagent (Explore)": which kind of process it is.</summary>
    public string Kind => subagent is { } s ? $"Subagent ({s.Type})" : cli.IsAllocate ? "Allocate agent" : "CLI";

    /// <summary>What it's doing now (its latest tool call), else what it was asked, else its state's own words.</summary>
    public string Doing => subagent is { } s ? s.Doing ?? s.Task ?? "Working" : cli.Doing is { Length: > 0 } d ? d : cli.StateDetail;

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
    private string taskDetailTitle = "";
    private string? taskDetailTicketId;
    private string taskDetailDescription = "";

    /// <summary>Every process spending tokens now: each working CLI, then its running subagents under it.</summary>
    public ObservableCollection<TaskRowViewModel> TaskList => Root.taskList;
    private readonly ObservableCollection<TaskRowViewModel> taskList = [];

    public bool HasTaskList => Root.taskList.Count > 0;

    /// <summary>"Task List  3", its section label's count.</summary>
    public string TaskListCount => Root.taskList.Count.ToString(CultureInfo.InvariantCulture);

    public bool IsTaskDetailOpen { get => Root.isTaskDetailOpen; set { Root.isTaskDetailOpen = value; RaiseOnEveryTab(nameof(IsTaskDetailOpen)); } }

    /// <summary>Every tab proxies the Task List to the global login's (the window shows whichever tab is chosen), so a change
    /// is said on each of them.</summary>
    private void RaiseOnEveryTab(string name)
    {
        foreach (var t in Root.loginTabs) t.Raise(name);
        if (!Root.loginTabs.Contains(Root)) Root.Raise(name);
    }
    public string TaskDetailTitle => Root.taskDetailTitle;

    /// <summary>The ticket's summary, or the CLI's own description ("Working on ...") with no ticket.</summary>
    public string TaskDetailDescription => Root.taskDetailDescription;

    public ObservableCollection<TaskDetailLine> TaskDetailLines => Root.taskDetailLines;
    private readonly ObservableCollection<TaskDetailLine> taskDetailLines = [];

    /// <summary>The ticket the modal shows, for its "Open in Tickets"; null for a CLI with none.</summary>
    public string? TaskDetailTicketId => Root.taskDetailTicketId;
    public bool HasTaskDetailTicket => Root.taskDetailTicketId is not null;

    public ICommand OpenTaskDetailCommand => Root.openTaskDetailCommand ??= new RelayCommand(p => { if (p is TaskRowViewModel row) Root.OpenTaskDetail(row); });
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
            view.Update(cli, sub, ownerTab.TabTitle, email, ownerTab.TabBrush, caption);
        }
        RaiseOnEveryTab(nameof(HasTaskList));
        RaiseOnEveryTab(nameof(TaskListCount));
    }

    /// <summary>The open ticket's queue row, or null when it isn't in the open queue.</summary>
    private QueueItem? QueueItemOf(string id) => Root.plan?.Queue.FirstOrDefault(q => string.Equals(q.Id, id, StringComparison.OrdinalIgnoreCase));

    private string? SummaryOf(string id) =>
        QueueItemOf(id)?.Ticket.Summary ?? Root.closedTickets.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase))?.Ticket.Summary;

    /// <summary>Fills and opens the detail modal for <paramref name="row"/>: its full ticket when it has one the queue (or the
    /// recently closed list) knows, else the CLI's own description, with who it bills and what it's doing either way.</summary>
    private void OpenTaskDetail(TaskRowViewModel row)
    {
        var now = DateTime.UtcNow;
        var lines = new List<TaskDetailLine>();
        var cli = row.Cli;
        string title, description;
        var id = row.TicketId;
        var item = id is null ? null : QueueItemOf(id);
        var closed = id is null || item is not null ? null : Root.closedTickets.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
        if (id is not null && (item?.Ticket ?? closed?.Ticket) is { } ticket)
        {
            title = TicketsDb.Caption(ticket.Id, FriendlyNameOf(ticket.Id), ticket.Summary);
            description = ticket.Summary;
            lines.Add(new("Ticket", ticket.Id));
            lines.Add(new("Repo", item?.Repo ?? closed?.Repo ?? cli.Repo));
            lines.Add(new("Type", ticket.Type));
            lines.Add(new("Status", ticket.Status));
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
        }
        else
        {
            title = $"{row.Kind} on {cli.Repo}";
            description = row.Subagent?.Task is { Length: > 0 } task ? task
                : cli.Doing is { Length: > 0 } doing ? doing : cli.StateDetail;
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
        Root.taskDetailTitle = title;
        Root.taskDetailDescription = description;
        Root.taskDetailTicketId = id is not null && (item is not null || closed is not null) ? id : null;
        Root.taskDetailLines.Clear();
        foreach (var line in lines) Root.taskDetailLines.Add(line);
        foreach (var name in new[] { nameof(TaskDetailTitle), nameof(TaskDetailDescription), nameof(TaskDetailTicketId), nameof(HasTaskDetailTicket) })
            RaiseOnEveryTab(name);
        IsTaskDetailOpen = true;
    }
}
