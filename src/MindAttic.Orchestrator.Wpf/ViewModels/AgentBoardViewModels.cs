using System.Collections.ObjectModel;
using System.Windows.Media;
using MindAttic.Orchestrator.Activity;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>One live Claude CLI or autopilot background agent, with its running subagents nested under it.</summary>
public sealed class AgentRowViewModel(string sessionId) : ObservableObject
{
    private AgentBoardRow row = null!;
    private DateTime nowUtc;
    private string? ticketTitle;

    public string SessionId { get; } = sessionId;
    public string Repo => row.Repo;
    public string? TicketId => row.TicketId;
    public bool HasTicket => TicketId is { Length: > 0 };

    /// <summary>"M.ORCH-T84: Agent board …" when the ticket's title is known, else just the id, else the repo's name.</summary>
    public string Headline => TicketId is not { Length: > 0 } ? $"{Repo} (no ticket)"
        : ticketTitle is { Length: > 0 } ? $"{TicketId}: {ticketTitle}" : TicketId;

    public string Elapsed => Formatting.Span(nowUtc - row.StartedUtc);
    public string Tokens => row.Tokens.Total == 0 ? "" : Formatting.Tokens(row.Tokens.Total);
    public bool IsAutopilot => row.IsAutopilot;
    public string Kind => IsAutopilot ? "background" : "CLI";

    public string StateLabel => row.State switch
    {
        AgentState.Working => "Working",
        AgentState.HeldByPace => "Held by pace",
        _ => "Waiting",
    };

    public Brush StateBrush => row.State switch
    {
        AgentState.Working => Palette.Working,
        AgentState.HeldByPace => Palette.Waiting,
        _ => Palette.Waiting,
    };

    public string StateDetail => row.StateDetail;

    public ObservableCollection<SubagentRowViewModel> Subagents { get; } = [];
    public bool HasSubagents => Subagents.Count > 0;

    public void Update(AgentBoardRow next, string? title, DateTime snapshotUtc)
    {
        row = next;
        ticketTitle = title;
        nowUtc = snapshotUtc;
        Subagents.SyncBy(next.Subagents, s => s.AgentId, r => r.AgentId,
            s => new SubagentRowViewModel(s, snapshotUtc), (r, s) => r.Update(s, snapshotUtc));
        Raise(string.Empty);
        Raise(nameof(HasSubagents));
    }
}

/// <summary>One subagent running under an <see cref="AgentRowViewModel"/>: what kind, how long, what it's spent.</summary>
public sealed class SubagentRowViewModel(SubagentActivity subagent, DateTime nowUtc) : ObservableObject
{
    private SubagentActivity subagent = subagent;
    private DateTime nowUtc = nowUtc;

    public string AgentId => subagent.AgentId;
    public string Type => subagent.Type;
    public string Elapsed => Formatting.Span(nowUtc - subagent.StartedUtc);
    public string Tokens => subagent.Usage.Total == 0 ? "" : Formatting.Tokens(subagent.Usage.Total);

    public void Update(SubagentActivity next, DateTime snapshotUtc)
    {
        subagent = next;
        nowUtc = snapshotUtc;
        Raise(string.Empty);
    }
}
