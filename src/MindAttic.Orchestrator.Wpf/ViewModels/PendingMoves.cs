using MindAttic.Orchestrator.Activity;

namespace MindAttic.Orchestrator.Wpf.ViewModels;

/// <summary>
/// Moves sent but not landed yet (M.ORCH-T638), shared by every person's tab: a moving CLI's controls stay locked under a
/// "Moving to …" overlay until it shows up on the new login (its own session, or a CLI in its repo started since the move,
/// since a resumed CLI may come back under a new session), or until <see cref="Timeout"/> passes.
/// </summary>
public static class PendingMoves
{
    public sealed record Move(string SessionId, string Repo, string? TargetEmail, string TargetName, DateTime StartedUtc);

    /// <summary>A move restarts the CLI at its next idle point, which can wait out a long turn.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(30);

    private static readonly Dictionary<string, Move> moves = new(StringComparer.Ordinal);

    /// <summary>Raised on the thread that began, ended or reconciled a move (the dashboard's).</summary>
    public static event Action? Changed;

    public static Move? For(string? sessionId)
    {
        lock (moves) return sessionId is not null && moves.TryGetValue(sessionId, out var m) ? m : null;
    }

    public static void Begin(Move move)
    {
        lock (moves) moves[move.SessionId] = move;
        Changed?.Invoke();
    }

    public static void End(string sessionId)
    {
        bool removed;
        lock (moves) removed = moves.Remove(sessionId);
        if (removed) Changed?.Invoke();
    }

    /// <summary>Ends every move <paramref name="seen"/> shows landed, and every one past <see cref="Timeout"/>; returns the
    /// timed-out ones, once each, for the caller to say so.</summary>
    public static IReadOnlyList<Move> Reconcile(IEnumerable<AgentBoardRow> seen, DateTime nowUtc)
    {
        var rows = seen.ToList();
        var timedOut = new List<Move>();
        var changed = false;
        lock (moves)
        {
            foreach (var m in moves.Values.ToList())
            {
                var landed = rows.Any(r => SameLogin(r.OwnLogin, m.TargetEmail) &&
                    (r.SessionId == m.SessionId || (r.Repo == m.Repo && r.StartedUtc >= m.StartedUtc)));
                if (landed) { moves.Remove(m.SessionId); changed = true; }
                else if (nowUtc - m.StartedUtc > Timeout) { moves.Remove(m.SessionId); timedOut.Add(m); changed = true; }
            }
        }
        if (changed) Changed?.Invoke();
        return timedOut;
    }

    internal static void Clear()
    {
        lock (moves) moves.Clear();
    }

    private static bool SameLogin(string? a, string? b) => string.Equals(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase);
}
