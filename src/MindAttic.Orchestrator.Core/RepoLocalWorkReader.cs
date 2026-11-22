using System.Collections.Concurrent;

namespace MindAttic.Orchestrator;

/// <summary>
/// Each repo's work that lives only on this machine (M.ORCH-T2036): uncommitted files, unpushed commits, stashes and
/// local-only ignored paths, read by MindAttic.Tickets' <see cref="MindAttic.Tickets.Repos.RepoStatus.Read"/>. Those are
/// several git calls per repo, far too slow for every refresh, so <see cref="Get"/> answers from a cache at once and, when a
/// repo's reading is older than <see cref="Interval"/>, reads it again on the thread pool, never the caller's (the UI)
/// thread. A repo not read yet answers null until its first read lands. Thread-safe.
/// </summary>
public sealed class RepoLocalWorkReader
{
    /// <summary>How long a repo's reading stands before <see cref="Get"/> reads it again.</summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>The read itself, by repo path; git through MindAttic.Tickets by default. Replaceable for tests.</summary>
    internal Func<string, string, RepoLocalWork> ReadRepo { get; init; } = ReadNow;

    private readonly ConcurrentDictionary<string, RepoLocalWork> cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Task> reading = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The latest reading of the repo at <paramref name="path"/>, null before its first; starts a background read
    /// when there's none or it's older than <see cref="Interval"/> and no read of it is already running.</summary>
    public RepoLocalWork? Get(string name, string path, DateTime nowUtc)
    {
        if (string.IsNullOrEmpty(path)) return null;
        cache.TryGetValue(path, out var known);
        if ((known is null || nowUtc - known.ReadUtc >= Interval) && !reading.ContainsKey(path))
        {
            var task = new Task(() =>
            {
                try { cache[path] = ReadRepo(name, path); }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    cache[path] = new RepoLocalWork(0, 0, 0, 0, DateTime.UtcNow, ex.Message);
                }
                finally { reading.TryRemove(path, out _); }
            });
            if (reading.TryAdd(path, task)) task.Start(TaskScheduler.Default);
        }
        return known;
    }

    /// <summary>Waits for every read in flight; for tests and a one-off CLI run that wants the counts in its one snapshot.</summary>
    public void WaitForReads(TimeSpan timeout)
    {
        var tasks = reading.Values.ToArray();
        if (tasks.Length > 0) Task.WaitAll(tasks, timeout);
    }

    /// <summary>Reads the repo at <paramref name="path"/> now, on the calling thread.</summary>
    public static RepoLocalWork ReadNow(string name, string path)
    {
        var s = MindAttic.Tickets.Repos.RepoStatus.Read(new MindAttic.Tickets.Repo(name, path));
        return new RepoLocalWork(s.Uncommitted, s.Unpushed, s.Stashes, s.LocalOnlyIgnored.Count, DateTime.UtcNow, s.Error);
    }
}
