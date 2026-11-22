namespace MindAttic.Orchestrator.Term;

/// <summary>
/// An agent's stable id (M.ORCH-T1743, roadmap Phase 1): made once when the orchestrator starts a CLI and handed to it as
/// <see cref="EnvVar"/>, so it outlives what a process id doesn't: a <c>/clear</c> (the same process, the same environment), the
/// Launcher restarting its CLI (the child inherits the host's environment) and the dashboard restarting (the TermHost and its
/// registry record keep it). It names the agent's row in the <c>agents</c> table, and is stamped on the hosted session's
/// <see cref="TermSessionRecord.AgentId"/>, its <c>Assignment</c> and its <c>session_states</c> row's value (<c>"agent"</c>).
/// </summary>
public static class AgentIds
{
    /// <summary>The variable a CLI started by the orchestrator carries its agent id in.</summary>
    public const string EnvVar = "MINDATTIC_AGENT_ID";

    /// <summary>The prefix every id starts with, so one is told apart from a session id or a subagent's.</summary>
    public const string Prefix = "agt-";

    /// <summary>A new id: <see cref="Prefix"/> and 16 hex digits.</summary>
    public static string New() => Prefix + Guid.NewGuid().ToString("N")[..16];

    /// <summary>This process's agent id (<see cref="EnvVar"/>); null when it was started without one.</summary>
    public static string? Current() => Normalize(Environment.GetEnvironmentVariable(EnvVar));

    /// <summary><paramref name="id"/> trimmed, or null when it's blank or holds anything but letters, digits, '-' and '_' (it
    /// goes into a PowerShell string, a file name and a column, so nothing else is let through).</summary>
    public static string? Normalize(string? id)
    {
        var trimmed = id?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > 64) return null;
        foreach (var c in trimmed)
            if (!(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')) return null;
        return trimmed;
    }
}
