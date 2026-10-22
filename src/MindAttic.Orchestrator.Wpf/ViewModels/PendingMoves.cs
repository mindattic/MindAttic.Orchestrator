using MindAttic.Orchestrator.Sessions;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// Moves sent but not landed yet (M.ORCH-T638, M.ORCH-T637), shared by every person's tab: a moving CLI's controls, and the
/// controls of every ticket it or its subagents hold, stay locked under "Moving to …" until the move lands, fails or times out.
/// The moves themselves live in the database (<see cref="MoveLocks"/>, read into each refresh's <see cref="Snapshot.Moves"/>), so
/// one sent by <c>orchestrator move</c> or the rebalancer locks here too; one this dashboard just sent is locked at once
/// (<see cref="Begin"/>), before the next refresh shows it.
/// </summary>
public static class PendingMoves
{
    /// <summary>How long a move begun here stays locked without the database showing it (a refresh away, normally).</summary>
    public static readonly TimeSpan LocalGrace = TimeSpan.FromMinutes(2);

    private static readonly Dictionary<string, MoveLock> stored = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, MoveLock> local = new(StringComparer.Ordinal);

    /// <summary>Raised on the thread that began, ended or synced a move (the dashboard's).</summary>
    public static event Action? Changed;

    private static IEnumerable<MoveLock> All() => stored.Values.Concat(local.Values.Where(l => !stored.ContainsKey(l.SessionId)));

    /// <summary>The move of the CLI running <paramref name="sessionId"/>, if any.</summary>
    public static MoveLock? For(string? sessionId)
    {
        if (sessionId is null) return null;
        lock (stored) return stored.GetValueOrDefault(sessionId) ?? local.GetValueOrDefault(sessionId);
    }

    /// <summary>The move holding ticket <paramref name="ticketId"/>, if any.</summary>
    public static MoveLock? ForTicket(string? ticketId)
    {
        lock (stored) return MoveLocks.ForTicket([.. All()], ticketId);
    }

    /// <summary>The move of the CLI in the Launcher tab <paramref name="hostPid"/>, if any.</summary>
    public static MoveLock? ForHost(int? hostPid)
    {
        if (hostPid is null) return null;
        lock (stored) return All().FirstOrDefault(m => m.HostPid == hostPid);
    }

    /// <summary>Locks a move this dashboard is sending, at once.</summary>
    public static void Begin(MoveLock move)
    {
        lock (stored) local[move.SessionId] = move;
        Changed?.Invoke();
    }

    /// <summary>Unlocks a move this dashboard began that was refused.</summary>
    public static void End(string sessionId)
    {
        bool removed;
        lock (stored) removed = local.Remove(sessionId);
        if (removed) Changed?.Invoke();
    }

    /// <summary>Takes the moves in flight as the database has them; returns the ones that ended since the last sync. A move begun
    /// here is kept until the database shows it, or for <see cref="LocalGrace"/>.</summary>
    public static IReadOnlyList<MoveLock> Sync(IReadOnlyList<MoveLock> moves, DateTime nowUtc)
    {
        List<MoveLock> ended;
        var changed = false;
        lock (stored)
        {
            var next = moves.GroupBy(m => m.SessionId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            ended = [.. stored.Values.Where(m => !next.ContainsKey(m.SessionId))];
            foreach (var key in local.Keys.ToList())
                if (next.ContainsKey(key) || stored.ContainsKey(key) || nowUtc - local[key].StartedUtc > LocalGrace) { local.Remove(key); changed = true; }
            changed |= ended.Count > 0 || next.Count != stored.Count || next.Any(kv => !stored.TryGetValue(kv.Key, out var was) || !Same(was, kv.Value));
            stored.Clear();
            foreach (var (key, value) in next) stored[key] = value;
        }
        if (changed) Changed?.Invoke();
        return ended;
    }

    private static bool Same(MoveLock a, MoveLock b) =>
        a.ToName == b.ToName && a.Interrupted == b.Interrupted && a.HostPid == b.HostPid && a.Tickets.SequenceEqual(b.Tickets, StringComparer.OrdinalIgnoreCase);

    internal static void Clear()
    {
        lock (stored)
        {
            stored.Clear();
            local.Clear();
        }
    }
}
