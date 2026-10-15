using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Activity;

public enum AgentState
{
    /// <summary>Wrote to its transcript within the working window.</summary>
    Working,
    /// <summary>Stopped and waiting for you.</summary>
    Waiting,
    /// <summary>Working, but the pace gate is holding its tool calls back.</summary>
    HeldByPace,
}

/// <summary>One Claude CLI or autopilot background agent at a glance: what it's on, how long, how it's
/// doing, what it's spent, and the subagents it has running right now.</summary>
public sealed record AgentBoardRow(
    string SessionId,
    string Repo,
    string? TicketId,
    string? TicketType,
    DateTime StartedUtc,
    DateTime LastActivityUtc,
    AgentState State,
    string StateDetail,
    bool IsAutopilot,
    TokenUsage Tokens,
    IReadOnlyList<SubagentActivity> Subagents);

/// <summary>
/// Builds the agent board: every live Claude session (a Launcher tab or a headless autopilot agent),
/// each with its running subagents nested under it. Pure projection over what <see cref="StatusAggregator"/>
/// already gathers; nothing here reads a file itself.
/// </summary>
public static class AgentBoard
{
    public static IReadOnlyList<AgentBoardRow> Build(
        IReadOnlyList<RepoStatus> repos,
        Pace? pace,
        IReadOnlyList<Assignment> assignments,
        IReadOnlyList<SubagentActivity> subagents,
        TimeSpan workingWindow,
        DateTime nowUtc)
    {
        var byParent = subagents.ToLookup(s => s.ParentSessionId, StringComparer.Ordinal);
        var byTicket = assignments.ToDictionary(a => a.TicketId, StringComparer.OrdinalIgnoreCase);
        var paceHolding = pace is { IsPaced: true, Mode: not PaceMode.Open };

        var rows = new List<AgentBoardRow>();
        foreach (var repo in repos)
        {
            foreach (var session in repo.Sessions)
            {
                if (session.Provider != Providers.Claude || session.SessionId.Length == 0) continue;

                var age = nowUtc - session.LastWriteUtc;
                var working = age <= workingWindow;
                var state = !working ? AgentState.Waiting : paceHolding ? AgentState.HeldByPace : AgentState.Working;
                var detail = state switch
                {
                    AgentState.Waiting => "Waiting for you",
                    AgentState.HeldByPace => pace!.Note,
                    _ => "Working",
                };

                var assignment = session.TicketId is { } tid ? byTicket.GetValueOrDefault(tid) : null;
                var started = assignment?.StartedUtc ?? (session.Events.Count > 0 ? session.Events[0].AtUtc : session.LastWriteUtc);

                rows.Add(new AgentBoardRow(
                    session.SessionId, repo.Name, session.TicketId, session.TicketType,
                    started, session.LastWriteUtc, state, detail,
                    assignment?.Pilot is not null, session.UsageToday,
                    [.. byParent[session.SessionId]]));
            }
        }
        return [.. rows.OrderByDescending(r => r.LastActivityUtc)];
    }
}
