using System.Windows.Input;
using MindAttic.Components.Wpf.Mvvm;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>One ticket in the cross-repo queue. Its id and title open it in Tickets Desktop, where it's approved, prioritized,
/// assigned or blocked (M.ORCH-T762): the row only runs work (Start, Release).</summary>
public sealed class QueueRowViewModel : ObservableObject
{
    private QueueItem item;
    private int rank;
    private Proposal? proposal;
    private string? delegation;

    /// <summary>A row of the one queue every tab shows (M.ORCH-T654): its buttons act on the tab on screen, so Start opens
    /// the CLI on that tab's login.</summary>
    public QueueRowViewModel(QueueItem item, MainViewModel owner)
    {
        this.item = item;
        StartCommand = new RelayCommand(() => owner.Shown.Dispatch(this.item), () => !this.item.IsAssigned && this.item.MayWork && !owner.IsStarting(this.item.Id) && !IsMoving);
        ReleaseCommand = new RelayCommand(() => owner.Shown.Release(this.item.Id), () => this.item.IsAssigned && !IsMoving);
    }

    /// <summary>Held by a CLI moving to another login (M.ORCH-T637): its controls are off until the move lands.</summary>
    public bool IsMoving => item.Moving is not null || PendingMoves.ForTicket(item.Id) is not null;
    /// <summary>What the move holding it says (M.ORCH-T637), empty when it isn't moving.</summary>
    public string MovingText => (PendingMoves.ForTicket(item.Id) ?? item.Moving)?.Text ?? "";
    /// <summary>Re-reads <see cref="IsMoving"/>, <see cref="MovingText"/> and <see cref="Status"/> after a move starts or
    /// lands.</summary>
    public void RaiseMoving() { Raise(nameof(IsMoving)); Raise(nameof(MovingText)); Raise(nameof(Status)); }

    /// <summary>The ticket's id.</summary>
    public string Id => item.Id;
    /// <summary>The queue item the row shows.</summary>
    public QueueItem Item => item;
    /// <summary>Its place in the queue, from the latest plan.</summary>
    public int Rank => rank;
    /// <summary>The repo the ticket belongs to.</summary>
    public string Repo => item.Repo;
    /// <summary>The ticket's type, as the tickets database names it.</summary>
    public string Type => item.Ticket.Type;
    /// <summary>The ticket's one-line title.</summary>
    public string Summary => item.Ticket.Summary;
    /// <summary>The ticket's date, as the tickets database gives it.</summary>
    public string Date => item.Ticket.Date;
    /// <summary>The queue priority with its sign ("+2", "-1"), empty at the default 0.</summary>
    public string Priority => item.Priority == 0 ? "" : item.Priority.ToString("+#;-#");
    /// <summary>A CLI has claimed it.</summary>
    public bool IsAssigned => item.IsAssigned;

    /// <summary>An assigned row is dimmed (it's someone's already): the row card's opacity, so the view needs no style of its own.</summary>
    public double RowOpacity => IsAssigned ? 0.6 : 1;
    /// <summary>The scheduler proposes it next.</summary>
    public bool IsNext => proposal is not null;
    /// <summary>Why the scheduler proposes it, empty when it doesn't.</summary>
    public string Reason => proposal?.Reason ?? "";

    /// <summary>The row's status line: the move holding it, what blocks it, who's working it, or why it's next or
    /// waiting.</summary>
    public string Status => IsMoving ? MovingText : item switch
    {
        { BlockedBy.Count: > 0 } => $"Waits for {string.Join(", ", item.BlockedBy)}",
        { IsStale: true } => $"Stale: quiet {Formatting.Ago(item.StaleSinceUtc, DateTime.UtcNow)}",
        { TooBig: true } => $"Too big for what's left ({item.Estimate}): split it or wait for the reset",
        { Assignment: { } a } => $"{a.Provider} since {Formatting.Ago(a.StartedUtc, DateTime.UtcNow)}{(OldWorktree(a) ? " · old worktree" : "")}" +
            // Weekly-aware pacing sent it to another login (M.ORCH-T666): redelegated, or fell back to the fallback login.
            (delegation is { Length: > 0 } d ? $" · {d}" : ""),
        _ => (IsNext ? "Next up" : item.RepoBusy ? "Repo busy" : item.Old ? "Old: groom it?" : "") +
             (item.Reserved > 0 ? $" · {Formatting.Tokens(item.Reserved)} reserved" : "") +
             (item.Estimate is { } e ? $" · ~{Formatting.Tokens(e.Fresh)}" : "") +
             // Assigned to a user (M.ORCH-T668): dispatched only on their login.
             (item.Assignee is { } who ? $" · for {who}" : ""),
    };

    /// <summary>Opens a CLI on the shown tab's login to work it; off once it's claimed, starting, moving or not
    /// workable.</summary>
    public ICommand StartCommand { get; }
    /// <summary>Releases its claim; only while it's claimed and not moving.</summary>
    public ICommand ReleaseCommand { get; }

    /// <summary>Whether <paramref name="a"/> has a worktree of its own: only one made before M.ORCH-T1122, in a folder named after its
    /// ticket (&lt;root&gt;\&lt;repo&gt;\&lt;ticket&gt;); every ticket since is worked in its repo's own checkout on main.</summary>
    internal static bool OldWorktree(Assignment a) =>
        a.Worktree && System.IO.Path.GetFileName(System.IO.Path.TrimEndingDirectorySeparator(a.Directory)).Equals(a.TicketId, StringComparison.OrdinalIgnoreCase);

    /// <summary>Takes the ticket's latest queue item, rank, proposal and delegation note, and raises every
    /// property.</summary>
    public void Update(QueueItem next, int nextRank, Proposal? nextProposal, string? nextDelegation = null)
    {
        item = next;
        rank = nextRank;
        proposal = nextProposal;
        delegation = nextDelegation;
        Raise(string.Empty);
    }
}

