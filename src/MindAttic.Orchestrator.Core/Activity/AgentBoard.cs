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
    IReadOnlyList<SubagentActivity> Subagents)
{
    /// <summary>Its latest tool call in plain words, and when.</summary>
    public string? Doing { get; init; }
    public DateTime? DoingAtUtc { get; init; }

    /// <summary>What its CLI bills, in words, and whether it's being restarted for the billing mode (M.ORCH-T436); null when unknown.</summary>
    public string? Billing { get; init; }

    /// <summary>The Claude login its CLI bills (M.ORCH-T606): its per-CLI login's email, or the global login's marked
    /// "(global)"; null when not known.</summary>
    public string? Login { get; init; }

    /// <summary>The per-CLI login its CLI runs on (its email, M.ORCH-T607), null on the global login: what its "Move to" menu leaves out.</summary>
    public string? OwnLogin { get; init; }

    /// <summary>Its claude.exe process id, as the context gate recorded it; null when not known.</summary>
    public int? CliPid { get; init; }

    /// <summary>The tickets its CLI claimed for its subagents (<c>orchestrator next --claim</c>, M.ORCH-T532), in claim order:
    /// only its own, never another CLI's on the same repo.</summary>
    public IReadOnlyList<string> Claims { get; init; } = [];

    /// <summary>The Launcher tab its CLI runs in, for the card's "Go to tab" (M.ORCH-T619); null for a headless agent or when not known.</summary>
    public HostSession? Host { get; init; }
}

/// <summary>
/// Builds the agent board: every live Claude session (a Launcher tab or a headless autopilot agent),
/// each with its running subagents nested under it. Pure projection over what <see cref="StatusAggregator"/>
/// already gathers; nothing here reads a file itself. Each CLI's row lists the tickets that CLI claimed (by the CLI process
/// <c>clis</c> maps its session to, or the session the claim recorded), so two CLIs on one repo each show only their own.
/// </summary>
public static class AgentBoard
{
    public static IReadOnlyList<AgentBoardRow> Build(
        IReadOnlyList<RepoStatus> repos,
        Pace? pace,
        IReadOnlyList<Assignment> assignments,
        IReadOnlyList<SubagentActivity> subagents,
        TimeSpan workingWindow,
        DateTime nowUtc,
        IReadOnlyDictionary<string, bool>? live = null,
        IReadOnlyDictionary<string, int>? clis = null)
    {
        var byParent = subagents.ToLookup(s => s.ParentSessionId, StringComparer.Ordinal);
        var byTicket = assignments.DistinctBy(a => a.TicketId, StringComparer.OrdinalIgnoreCase).ToDictionary(a => a.TicketId, StringComparer.OrdinalIgnoreCase);
        // Held only when the gate really holds its tool calls (a per-call delay, or the stop): Throttled or Coast alone
        // only cap new sessions, and every working CLI read Held by pace while running free (M.ORCH-T440). The stop holds
        // with pacing off too: it's written either way and the gate refuses every call (M.ORCH-T466).
        var paceHolding = pace is { Stop: true } || pace is { IsPaced: true, AgentDelaySeconds: > 0 };

        var rows = new List<AgentBoardRow>();
        foreach (var repo in repos)
        {
            foreach (var session in repo.Sessions)
            {
                if (session.Provider != Providers.Claude || session.SessionId.Length == 0) continue;
                // Given which CLIs are running, only theirs are on the board, and their own busy/idle says working or
                // waiting; without it (tests, a one-off read), a transcript written within the working window counts.
                bool working;
                if (live is null) working = nowUtc - session.LastWriteUtc <= workingWindow;
                else if (live.TryGetValue(session.SessionId, out var busy)) working = busy;
                else continue;
                var state = !working ? AgentState.Waiting : paceHolding ? AgentState.HeldByPace : AgentState.Working;
                var detail = state switch
                {
                    AgentState.Waiting => "Waiting for you",
                    AgentState.HeldByPace => pace!.Note,
                    _ => "Working",
                };

                var assignment = session.TicketId is { } tid ? byTicket.GetValueOrDefault(tid) : null;
                var started = assignment?.StartedUtc ?? (session.Events.Count > 0 ? session.Events[0].AtUtc : session.LastWriteUtc);

                // Its CLI's claims: made by its CLI process (any of its sessions, so a /clear keeps them), or in this session.
                int? cli = clis is not null && clis.TryGetValue(session.SessionId, out var p) ? p : null;
                var claims = assignments.Where(a => a.IsClaim && ((cli is not null && a.Cli == cli) ||
                        (a.CliSession is not null && a.CliSession == session.SessionId)))
                    .Select(a => a.TicketId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                rows.Add(new AgentBoardRow(
                    session.SessionId, repo.Name, session.TicketId, session.TicketType,
                    started, session.LastWriteUtc, state, detail,
                    assignment?.Pilot is not null, session.UsageToday,
                    [.. byParent[session.SessionId]]) { Doing = session.Doing, DoingAtUtc = session.DoingAtUtc, Claims = claims, CliPid = cli });
            }
        }
        return [.. rows.OrderByDescending(r => r.LastActivityUtc)];
    }
}
