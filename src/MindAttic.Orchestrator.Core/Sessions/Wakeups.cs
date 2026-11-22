using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>
/// The wakeup queue (AGENTIC_ROADMAP Phase 3; first used by the agent mailbox, M.ORCH-T2067): one coalescing <c>wakeups</c> state
/// row in the orchestrator's store holding, per idle CLI, why it should carry on now. A wake is never typed into a CLI's box
/// (M.ORCH-T864): what it carries is already left for the CLI's UserPromptSubmit hook (<see cref="CliNudges"/>), so a wake is
/// said to you once, in the log and the dashboard's Needs-you alert. Wakes coalesce: one entry per CLI and idle stretch (the turn
/// it went idle on), one reason of each kind in it, so a reason enqueued every tick is said once, and a new stretch starts afresh.
/// Kept in the store, so a daemon restart doesn't say them again. <see cref="AllocateWaker"/>'s own wakes move onto it later.
/// </summary>
public sealed class Wakeups(JsonStore store)
{
    /// <summary>The state row's name in the orchestrator's store.</summary>
    public const string Name = "wakeups";

    /// <summary>One CLI's pending wake: the idle stretch it's for, when it was first queued, and each reason's line, with the ones said.</summary>
    public sealed class Entry
    {
        /// <summary>The turn the CLI went idle on: a later one is a new stretch.</summary>
        public DateTime TurnUtc { get; set; }

        /// <summary>When the first reason was queued.</summary>
        public DateTime QueuedUtc { get; set; }

        /// <summary>Each reason (a nudge kind: <c>mail</c>, ...) and what's said for it.</summary>
        public Dictionary<string, string> Reasons { get; set; } = new(StringComparer.Ordinal);

        /// <summary>The reasons already said.</summary>
        public HashSet<string> Said { get; set; } = new(StringComparer.Ordinal);
    }

    /// <summary>One reason a CLI is woken, to be said once: <paramref name="Line"/> goes to the log and the dashboard.</summary>
    public sealed record Wake(int Pid, string Reason, string Line);

    private Dictionary<string, Entry>? queue;

    private Dictionary<string, Entry> Queue()
    {
        if (queue is not null) return queue;
        queue = store.TryLoad<Dictionary<string, Entry>>(Name, out var loaded) ? loaded : [];
        return queue;
    }

    private void Persist()
    {
        try { store.Save(Name, queue); }
        catch (IOException) { } // kept in memory; the next change tries again
    }

    /// <summary>Queues <paramref name="reason"/> for CLI <paramref name="pid"/>, idle since its turn at <paramref name="turnUtc"/>.
    /// Returns false when it coalesced into a wake already queued for that stretch (its line is updated, not said again).</summary>
    public bool Enqueue(int pid, DateTime turnUtc, string reason, string line, DateTime nowUtc)
    {
        var all = Queue();
        var key = pid.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!all.TryGetValue(key, out var entry) || entry.TurnUtc != turnUtc)
            all[key] = entry = new Entry { TurnUtc = turnUtc, QueuedUtc = nowUtc };
        var fresh = !entry.Reasons.ContainsKey(reason);
        if (!fresh && entry.Reasons[reason] == line) return false;
        entry.Reasons[reason] = line;
        Persist();
        return fresh;
    }

    /// <summary>Drops <paramref name="reason"/> from every CLI not in <paramref name="pids"/> (its cause is gone, or it took a
    /// prompt), and the entries left with no reason.</summary>
    public void Retain(string reason, IEnumerable<int> pids)
    {
        var all = Queue();
        var keep = pids.Select(p => p.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToHashSet(StringComparer.Ordinal);
        var changed = false;
        foreach (var (key, entry) in all.ToList())
        {
            if (keep.Contains(key)) continue;
            changed |= entry.Reasons.Remove(reason) | entry.Said.Remove(reason);
            if (entry.Reasons.Count == 0) changed |= all.Remove(key);
        }
        if (changed) Persist();
    }

    /// <summary>The queued reasons not yet said, oldest CLI first, marked said.</summary>
    public IReadOnlyList<Wake> Take()
    {
        var wakes = new List<Wake>();
        foreach (var (key, entry) in Queue().OrderBy(e => e.Value.QueuedUtc))
            foreach (var (reason, line) in entry.Reasons.OrderBy(r => r.Key, StringComparer.Ordinal))
            {
                if (!entry.Said.Add(reason)) continue;
                wakes.Add(new Wake(int.Parse(key, System.Globalization.CultureInfo.InvariantCulture), reason, line));
            }
        if (wakes.Count > 0) Persist();
        return wakes;
    }
}
