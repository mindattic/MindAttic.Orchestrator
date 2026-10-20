namespace MindAttic.Orchestrator.Scheduling;

/// <summary>
/// How the global subagent allowance is shared between CLIs (M.ORCH-T495): one repo's /autopilot claimed all 8 places at
/// once while every other CLI got none. Each active CLI (the caller, every CLI with subagents running, and every CLI that
/// wants one) has a fair share, <c>allowance / active</c> (at least one). A CLI may go past its share only into places no
/// other CLI is waiting for: another CLI's share is held for it while it wants more (up to what it has running, or its
/// share if that's more), and a CLI that wants nothing more lends what it isn't using. Nothing running is ever stopped:
/// a CLI over its share only starts nothing new until it's back under, so the next free place goes to the one under.
/// The pace gate (<c>pace-gate.py</c>) applies the same rule live, a waiting Agent call being what "wants" means there.
/// </summary>
public static class SubagentShare
{
    /// <summary>How many subagents CLI <paramref name="me"/> may have running at once: its fair share, or more when the
    /// other CLIs' running subagents and what is held for those that want more leave room.</summary>
    /// <param name="running">Subagents running now (or about to), by CLI process id.</param>
    /// <param name="wanting">The other CLIs that want more subagents now.</param>
    public static int Limit(int allowance, int me, IReadOnlyDictionary<int, int> running, IEnumerable<int> wanting)
    {
        var wants = wanting.Where(p => p != me).ToHashSet();
        var active = running.Where(r => r.Value > 0).Select(r => r.Key).Concat(wants).Append(me).Distinct().Count();
        var share = Share(allowance, active);
        var held = running.Where(r => r.Key != me && !wants.Contains(r.Key)).Sum(r => Math.Max(0, r.Value)) +
            wants.Sum(p => Math.Max(running.GetValueOrDefault(p), share));
        return Math.Max(share, allowance - held);
    }

    /// <summary>One CLI's fair share of <paramref name="allowance"/> among <paramref name="active"/> CLIs: never under one,
    /// so with more CLIs than places each still gets the next one that frees in turn.</summary>
    public static int Share(int allowance, int active) => Math.Max(1, allowance / Math.Max(1, active));

    /// <summary>How many more CLI <paramref name="me"/> may start now: under its <see cref="Limit"/> and under the allowance
    /// across every CLI (<paramref name="total"/> running everywhere).</summary>
    public static int Room(int allowance, int total, int me, IReadOnlyDictionary<int, int> running, IEnumerable<int> wanting) =>
        Math.Max(0, Math.Min(allowance - total, Limit(allowance, me, running, wanting) - running.GetValueOrDefault(me)));
}
