namespace MindAttic.Orchestrator.Status;

/// <summary>
/// The daemon's per-session coalescing of the <see cref="AppBus.Activity"/> kind (M.ORCH-T962; docs/spikes/activity-feed.md section 5):
/// a busy CLI's hooks can rewrite its record many times a second, and the bus carries at most one message per session per
/// <see cref="Every"/>. An offer inside that gap is held, the newest replacing an older held one, and goes out at the next
/// <see cref="Due"/> once the gap has passed, so the last picture is never lost, only delayed. It keeps the latest message sent
/// per session (<see cref="Snapshot"/>: what a subscriber connecting now is handed), drops a reordered one (a lower
/// <see cref="ActivityPayload.Seq"/> than the newest seen), and ends a session gone silent with its CLI gone (<see cref="Expire"/>).
/// Pure and thread-safe: the time is passed in, nothing is read or published here.
/// </summary>
public sealed class ActivityCoalescer
{
    private readonly object gate = new();
    private readonly Dictionary<string, Entry> sessions = new(StringComparer.Ordinal);

    private sealed class Entry
    {
        /// <summary>The latest message sent; null while only a held one exists (it never went out).</summary>
        public ActivityPayload? Sent;
        public DateTime SentAt;
        /// <summary>The newest offer not yet sent.</summary>
        public ActivityPayload? Held;
        /// <summary>The highest seq seen (sent or held).</summary>
        public long? Seq;
        /// <summary>When an offer for it last arrived (the session's last sign of life).</summary>
        public DateTime SeenAt;
        /// <summary>The CLI's process, when its record says; null when unknown.</summary>
        public int? CliPid;
    }

    /// <summary>The least time between two messages for one session (the ActivityCoalesceSeconds Vault setting); zero sends
    /// every offer at once.</summary>
    public TimeSpan Every { get; set; } = TimeSpan.FromSeconds(Settings.OrchestratorSettings.DefaultActivityCoalesceSeconds);

    /// <summary>
    /// Takes <paramref name="next"/>, the newest picture of its session, at <paramref name="now"/>; returns it when it is to be sent
    /// now, null when it was dropped (no session, older than one seen, the same as the one sent, or an end for a session never
    /// sent) or held for <see cref="Due"/>. <paramref name="cliPid"/> is the CLI's process, kept for <see cref="Expire"/>. A
    /// session's end goes out at once whatever the gap and forgets it.
    /// </summary>
    public ActivityPayload? Offer(ActivityPayload next, DateTime now, int? cliPid = null)
    {
        if (string.IsNullOrWhiteSpace(next.Session)) return null;
        lock (gate)
        {
            var known = sessions.TryGetValue(next.Session, out var e);
            if (known && e!.Seq is { } seen && next.Seq is { } seq && seq <= seen) return null;
            if (next.State == ActivityStates.Ended)
            {
                // An end for a session nobody was shown is news to no one (an old record read at the daemon's start).
                if (!known) return null;
                sessions.Remove(next.Session);
                return e!.Sent is null ? null : next;
            }
            if (!known) sessions[next.Session] = e = new Entry();
            e!.SeenAt = now;
            if (next.Seq is not null) e.Seq = next.Seq;
            if (cliPid is > 0) e.CliPid = cliPid;
            if (e.Sent is { } sent && SamePicture(sent, next))
            {
                e.Held = null;
                return null;
            }
            if (e.Sent is null || now - e.SentAt >= Every)
            {
                e.Sent = next;
                e.SentAt = now;
                e.Held = null;
                return next;
            }
            e.Held = next;
            return null;
        }
    }

    /// <summary>The held messages whose session's gap has passed by <paramref name="now"/>, taken as sent.</summary>
    public IReadOnlyList<ActivityPayload> Due(DateTime now)
    {
        List<ActivityPayload> due = [];
        lock (gate)
            foreach (var e in sessions.Values)
            {
                if (e.Held is not { } held || now - e.SentAt < Every) continue;
                e.Sent = held;
                e.SentAt = now;
                e.Held = null;
                due.Add(held);
            }
        return due;
    }

    /// <summary>How long until the next held message is due at <paramref name="now"/>; null when none is held.</summary>
    public TimeSpan? NextDue(DateTime now)
    {
        TimeSpan? next = null;
        lock (gate)
            foreach (var e in sessions.Values)
            {
                if (e.Held is null) continue;
                var wait = e.SentAt + Every - now;
                if (wait < TimeSpan.Zero) wait = TimeSpan.Zero;
                if (next is null || wait < next) next = wait;
            }
        return next;
    }

    /// <summary>
    /// The sessions with no offer for <paramref name="silentFor"/> by <paramref name="now"/> whose CLI is gone (<paramref name="alive"/>
    /// says no for its pid, or its record never named one): each is forgotten, and for each that a subscriber was shown its end is
    /// returned, to send once.
    /// </summary>
    public IReadOnlyList<ActivityPayload> Expire(DateTime now, TimeSpan silentFor, Func<int, bool> alive)
    {
        List<ActivityPayload> ended = [];
        lock (gate)
            foreach (var (session, e) in sessions.ToList())
            {
                if (now - e.SeenAt < silentFor) continue;
                if (e.CliPid is { } pid && alive(pid)) continue;
                sessions.Remove(session);
                if (e.Sent is not { } last) continue;
                ended.Add(last with
                {
                    State = ActivityStates.Ended, Tool = null, Target = null, Verb = null, Summary = null,
                    Seq = (e.Seq ?? last.Seq) is { } s ? s + 1 : null, AtUtc = now,
                });
            }
        return ended;
    }

    /// <summary>The latest message sent for each session, oldest session first: the activity snapshot a connecting subscriber gets.</summary>
    public IReadOnlyList<ActivityPayload> Snapshot()
    {
        lock (gate)
            return [.. sessions.Values.Where(e => e.Sent is not null).OrderBy(e => e.SentAt).Select(e => e.Sent!)];
    }

    /// <summary>Whether two pictures of a session say the same thing (only their seq and time differ): not worth a message.</summary>
    internal static bool SamePicture(ActivityPayload a, ActivityPayload b) =>
        a with { Seq = null, AtUtc = null } == b with { Seq = null, AtUtc = null };
}
