using MindAttic.Orchestrator.Settings;

namespace MindAttic.Orchestrator.Sessions;

/// <summary>Dock (a standalone Claude tab into the dashboard's Terminals panel) or Undock (an embedded pane out to its own
/// Windows Terminal window) the other way.</summary>
public enum RelocateDirection { Dock, Undock }

/// <summary>
/// A relocation in flight: a Claude CLI ending here (its current host tab, idle-gated, never restarted in place) so it can
/// reopen resumed (<c>claude --resume</c>) on the other hosting surface. Kept in <c>data/orchestrator.db</c>'s
/// <see cref="RelocateLocks.StateName"/> state row, so <c>orchestrator dock</c> (a separate process from the dashboard) and
/// the dashboard's own reconcile tick see the same lock.
/// <para>Deliberately not <see cref="MoveLock"/>: a move's lock is shaped around a login changing (<c>ToEmail</c>/<c>ToName</c>,
/// ticket-claim handback via <see cref="MoveHandback"/>); a relocation never changes who the CLI runs as, only where its tab
/// lives, so "landed" here is simply "the host tab it used to run in is gone" rather than a login/repo/time heuristic.</para>
/// </summary>
public sealed record RelocateLock
{
    /// <summary>The CLI's session (what <c>--resume</c> reopens) and its claude.exe as it was when the relocation was sent.</summary>
    public string SessionId { get; init; } = "";
    public int Pid { get; init; }

    /// <summary>The Launcher tab it's ending in: ended once this pid is no longer live.</summary>
    public int HostPid { get; init; }

    public string Repo { get; init; } = "";
    public string? Cwd { get; init; }

    /// <summary>Unchanged by a relocation, carried through to reopen with: the per-CLI login (null: the global one), its email,
    /// its badge and its tab color.</summary>
    public string? ConfigDir { get; init; }
    public string? Email { get; init; }
    public string? Initials { get; init; }
    public string? TabColor { get; init; }

    public RelocateDirection Direction { get; init; }
    public DateTime StartedUtc { get; init; }
}

/// <summary>How a relocation ended: <see cref="Gone"/> once its host pid left the live scan (reopen it on the other surface),
/// or stuck past <see cref="RelocateLocks.Timeout"/> while still live (idle never arrived, or the kill didn't take: give up,
/// don't reopen anything).</summary>
public sealed record RelocateEnd(RelocateLock Lock, bool Gone);

/// <summary>
/// The relocations in flight, one write transaction each, so two processes never both begin one of a CLI or both end one. A
/// lock ends once its <see cref="RelocateLock.HostPid"/> is confirmed gone from a live scan (reopened on the other surface by
/// whoever calls <see cref="Reconcile"/>), or after <see cref="Timeout"/> (stuck: idle never arrived, or the kill somehow
/// didn't take).
/// </summary>
public sealed class RelocateLocks(JsonStore store)
{
    public const string StateName = "relocate-moves";

    /// <summary>A relocation ends its CLI at its next idle point, which can wait out a long turn (the same reasoning, and the
    /// same span, as <see cref="MoveLocks.Timeout"/>).</summary>
    public static readonly TimeSpan Timeout = MoveLocks.Timeout;

    public JsonStore Store { get; } = store;

    /// <summary>Every relocation in flight; none when they can't be read now.</summary>
    public IReadOnlyList<RelocateLock> All()
    {
        try { return Store.Load<List<RelocateLock>>(StateName); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { return []; }
    }

    /// <summary>Locks <paramref name="move"/>'s session; false (with the one already in flight) when it's already relocating.</summary>
    public bool TryBegin(RelocateLock move, out RelocateLock? already)
    {
        RelocateLock? found = null;
        Store.Update<List<RelocateLock>>(StateName, all =>
        {
            found = all.FirstOrDefault(m => m.SessionId == move.SessionId || m.HostPid == move.HostPid);
            if (found is null) all.Add(move);
        });
        already = found;
        return found is null;
    }

    /// <summary>Unlocks <paramref name="sessionId"/>'s relocation (a refused send); true when there was one.</summary>
    public bool End(string sessionId)
    {
        var removed = false;
        Store.Update<List<RelocateLock>>(StateName, all => removed = all.RemoveAll(m => m.SessionId == sessionId) > 0);
        return removed;
    }

    /// <summary>
    /// Ends every relocation whose <see cref="RelocateLock.HostPid"/> isn't in <paramref name="liveHostIds"/> (confirmed
    /// gone: a fresh <see cref="HostSessionScanner"/> scan, same tick this is called with, since its process exiting is what
    /// disposes the pipes the scanner keys off) and every one past <see cref="Timeout"/> still live (stuck), in one write.
    /// </summary>
    public IReadOnlyList<RelocateEnd> Reconcile(IReadOnlySet<int> liveHostIds, DateTime nowUtc)
    {
        if (All().Count == 0) return [];
        var ended = new List<RelocateEnd>();
        Store.Update<List<RelocateLock>>(StateName, all =>
        {
            foreach (var m in all.ToList())
            {
                var gone = !liveHostIds.Contains(m.HostPid);
                if (!gone && nowUtc - m.StartedUtc <= Timeout) continue;
                all.Remove(m);
                ended.Add(new RelocateEnd(m, gone));
            }
        });
        return ended;
    }
}
