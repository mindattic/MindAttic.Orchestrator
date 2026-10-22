using System.Windows.Input;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>One ticket in the cross-repo queue.</summary>
public sealed class QueueRowViewModel : ObservableObject
{
    private QueueItem item;
    private int rank;
    private Proposal? proposal;

    public QueueRowViewModel(QueueItem item, MainViewModel owner)
    {
        this.item = item;
        StartCommand = new RelayCommand(() => owner.Dispatch(this.item, this.proposal?.UseWorktree ?? this.item.NeedsWorktree), () => !this.item.IsAssigned && this.item.MayWork && !owner.IsStarting(this.item.Id) && !IsMoving);
        UpCommand = new RelayCommand(() => owner.Bump(this.item.Id, +1), () => !IsMoving);
        DownCommand = new RelayCommand(() => owner.Bump(this.item.Id, -1), () => !IsMoving);
        ReleaseCommand = new RelayCommand(() => owner.Release(this.item.Id), () => this.item.IsAssigned && !IsMoving);
    }

    /// <summary>Held by a CLI moving to another login (M.ORCH-T637): its controls are off until the move lands.</summary>
    public bool IsMoving => item.Moving is not null || PendingMoves.ForTicket(item.Id) is not null;
    public string MovingText => (PendingMoves.ForTicket(item.Id) ?? item.Moving)?.Text ?? "";
    public void RaiseMoving() { Raise(nameof(IsMoving)); Raise(nameof(MovingText)); Raise(nameof(Status)); }

    public string Id => item.Id;
    public QueueItem Item => item;
    public int Rank => rank;
    public string Repo => item.Repo;
    public string Type => item.Ticket.Type;
    public string Summary => item.Ticket.Summary;
    public string Date => item.Ticket.Date;
    public string Priority => item.Priority == 0 ? "" : item.Priority.ToString("+#;-#");
    public bool IsAssigned => item.IsAssigned;
    public bool IsNext => proposal is not null;
    public string Reason => proposal?.Reason ?? "";
    public string Where => proposal?.UseWorktree == true || (proposal is null && item.NeedsWorktree) ? "in a worktree" : "in the repo";

    public string Status => IsMoving ? MovingText : item switch
    {
        { BlockedBy.Count: > 0 } => $"Waits for {string.Join(", ", item.BlockedBy)}",
        { IsStale: true } => $"Stale: quiet {Formatting.Ago(item.StaleSinceUtc, DateTime.UtcNow)}",
        { TooBig: true } => $"Too big for what's left ({item.Estimate}): split it or wait for the reset",
        { Assignment: { } a } => $"{a.Provider} since {Formatting.Ago(a.StartedUtc, DateTime.UtcNow)}{(a.Worktree ? " · worktree" : "")}",
        _ => (IsNext ? "Next up" : item.RepoBusy ? "Repo busy" : item.Old ? "Old: groom it?" : "") +
             (item.Reserved > 0 ? $" · {Formatting.Tokens(item.Reserved)} reserved" : "") +
             (item.Estimate is { } e ? $" · ~{Formatting.Tokens(e.Fresh)}" : ""),
    };

    public ICommand StartCommand { get; }
    public ICommand UpCommand { get; }
    public ICommand DownCommand { get; }
    public ICommand ReleaseCommand { get; }

    public void Update(QueueItem next, int nextRank, Proposal? nextProposal)
    {
        item = next;
        rank = nextRank;
        proposal = nextProposal;
        Raise(string.Empty);
    }
}

/// <summary>A ticket whose work is closed but whose assignment (and maybe worktree) is still around.</summary>
public sealed class FinishedViewModel(Assignment assignment, MainViewModel owner) : ObservableObject
{
    public string TicketId => assignment.TicketId;
    public string Text => assignment.TicketId + TextAfterId;

    /// <summary><see cref="Text"/> after the id, which the card shows as a link into Tickets Desktop (M.ORCH-T500).</summary>
    public string TextAfterId => assignment.Worktree
        ? $" in {assignment.Repo} is closed. Its worktree is still at {assignment.Directory}."
        : $" in {assignment.Repo} is closed.";
    public string ActionLabel => assignment.Worktree ? "Remove worktree" : "Clear";
    /// <summary>Off while a CLI moving to another login holds the ticket (M.ORCH-T637): its worktree is handed back to it.</summary>
    public ICommand ActionCommand { get; } = new RelayCommand(() => owner.Finish(assignment), () => PendingMoves.ForTicket(assignment.TicketId) is null);
    public ICommand DetailsCommand { get; } = new RelayCommand(() => owner.ShowDetails(assignment));

    /// <summary>The ticket's assignment as the latest refresh has it (reopened, dispatched again elsewhere and closed): its
    /// text, its button and what they act on follow it (M.ORCH-T341).</summary>
    public void Update(Assignment latest)
    {
        if (latest == assignment) return;
        assignment = latest;
        Raise(nameof(Text));
        Raise(nameof(TextAfterId));
        Raise(nameof(ActionLabel));
    }
}
