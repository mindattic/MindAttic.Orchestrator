namespace MindAttic.Orchestrator.Activity;

/// <summary>A session that's still writing but repeating itself.</summary>
public sealed record StuckSession(string Repo, string SessionId, Loop Loop);

/// <summary>Picks out the sessions a snapshot shows looping (<see cref="LoopDetector"/>).</summary>
public static class Watchdog
{
    /// <summary>Only sessions written in the last few minutes count: an old transcript that once looped isn't news.</summary>
    public static readonly TimeSpan Recent = TimeSpan.FromMinutes(10);

    /// <summary>Every session in <paramref name="snapshot"/> with a loop found that was written within <see cref="Recent"/>.</summary>
    public static IReadOnlyList<StuckSession> Stuck(Snapshot snapshot) =>
        [.. snapshot.Repos.SelectMany(r => r.Sessions
            .Where(s => s.Loop is not null && snapshot.TakenUtc - s.LastWriteUtc <= Recent)
            .Select(s => new StuckSession(r.Name, s.SessionId, s.Loop!)))];
}
