using System.Collections.ObjectModel;
using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>One row of a login's Agents panel (M.ORCH-T1747, roadmap Phase 1): an agent's role, state, the unit it's on and
/// when it last said it's alive, as the <c>agents</c> table (<see cref="AgentBook"/>) held it at the last refresh.</summary>
public sealed class AgentRowViewModel(Agent agent, DateTime nowUtc)
{
    /// <summary>Its stable <c>agt-</c> id.</summary>
    public string Id { get; } = agent.Id;
    /// <summary>The role it plays.</summary>
    public string Role { get; } = agent.Role;
    /// <summary>Its state: running, idle, paused (a retired agent isn't listed).</summary>
    public string State { get; } = agent.State;
    /// <summary>The unit (ticket) it's on now; empty when none.</summary>
    public string Unit { get; } = agent.Unit ?? "";
    /// <summary>Its last heartbeat, "2m ago"; "no heartbeat" before its first.</summary>
    public string Heartbeat { get; } = agent.HeartbeatUtc is { } h ? Formatting.Ago(h, nowUtc) : "no heartbeat";
    /// <summary>Whether it's running now.</summary>
    public bool IsRunning { get; } = agent.State == AgentBook.Running;
    /// <summary>What the row shows after its role: "running · M.ORCH-T12 · 2m ago".</summary>
    public string Line { get; } = string.Join(" · ", new[] { agent.State, agent.Unit, agent.HeartbeatUtc is { } beat ? Formatting.Ago(beat, nowUtc) : "no heartbeat" }
        .Where(p => !string.IsNullOrEmpty(p)));
    /// <summary>Its hover tip and accessible name: everything above plus its id and runtime.</summary>
    public string Tip { get; } = string.Join(". ", new[]
    {
        $"Agent {agent.Id}", $"Role: {agent.Role}", $"State: {agent.State}", $"Runtime: {agent.Runtime}",
        agent.Unit is { Length: > 0 } unit ? $"On {unit}" : "On no unit",
        agent.HeartbeatUtc is { } last ? $"Last heartbeat {Formatting.Ago(last, nowUtc)}" : "No heartbeat yet",
    });
    /// <summary>What names it across refreshes: everything it shows.</summary>
    internal string Key { get; } = $"{agent.Id}|{agent.State}|{agent.Unit}|{agent.Role}|{agent.HeartbeatUtc:O}";
}

/// <summary>
/// Each login's Agents panel in the login tree (M.ORCH-T1747): the agents the <c>agents</c> table (<see cref="AgentBook"/>,
/// M.ORCH-T1744) has on that login, retired ones left out. Read with the rest of a refresh (or a pushed snapshot's figures),
/// off the UI thread, then spread over the tabs by owner when the tabs are applied: no poll of its own.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>The agents the last read found, on the global login's tab; null when none was read (the last list stays).</summary>
    private volatile IReadOnlyList<Agent>? agentsRead;
    private readonly ObservableCollection<AgentRowViewModel> agentRows = [];

    /// <summary>Every agent not retired, off the UI thread; null when the table can't be read (not made yet, the database down).
    /// Never throws.</summary>
    internal IReadOnlyList<Agent>? AgentsNow()
    {
        try { return [.. new AgentBook(aggregator.Db).All().Where(a => !a.Terminated)]; }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return null; }
    }

    /// <summary>Gives each login's tab the agents read on it, by owner; a tab whose rows are unchanged is left alone.</summary>
    private void ApplyAgents()
    {
        if (!IsGlobalTab) { Root.ApplyAgents(); return; }
        if (agentsRead is not { } agents) return;
        var now = DateTime.UtcNow;
        var byOwner = agents.ToLookup(a => AccountDirs.Normalize(a.Owner) ?? "", StringComparer.OrdinalIgnoreCase);
        foreach (var t in loginTabs.ToList())
            t.ShowAgents([.. byOwner[t.PersonEmail ?? "\0"].OrderBy(a => StateOrder(a.State)).ThenByDescending(a => a.CreatedUtc)
                .Select(a => new AgentRowViewModel(a, now))]);
    }

    /// <summary>Where <paramref name="state"/> sorts: <see cref="AgentBook.States"/>' order, running first; an unknown one last.</summary>
    private static int StateOrder(string state)
    {
        for (var i = 0; i < AgentBook.States.Count; i++) if (AgentBook.States[i] == state) return i;
        return AgentBook.States.Count;
    }

    private void ShowAgents(IReadOnlyList<AgentRowViewModel> rows)
    {
        if (agentRows.Select(r => r.Key).SequenceEqual(rows.Select(r => r.Key), StringComparer.Ordinal)) return;
        var had = agentRows.Count;
        agentRows.Clear();
        foreach (var row in rows) agentRows.Add(row);
        if (had != agentRows.Count) { Raise(nameof(HasAgents)); Raise(nameof(AgentsTitle)); }
    }

    /// <summary>This login's agents, running first, each group newest first.</summary>
    public ObservableCollection<AgentRowViewModel> Agents => agentRows;

    /// <summary>Whether this login has any agent not retired: the panel shows only then.</summary>
    public bool HasAgents => agentRows.Count > 0;

    /// <summary>The panel's heading, with its count.</summary>
    public string AgentsTitle => $"Agents ({agentRows.Count})";
}
