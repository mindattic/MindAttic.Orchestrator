using System.Collections.Concurrent;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Tickets.Repos;
using MindAttic.Vault.Credentials;

namespace MindAttic.Orchestrator;

/// <summary>
/// Each repo's GitHub standing (M.ORCH-T2038): archived, tagged <c>wip</c>, or untagged, from MindAttic.Tickets'
/// <see cref="GitHubRepoIndex"/> (one paginated <c>/user/repos</c> sweep, plus a direct lookup for a renamed repo's old
/// slug), with the token read from MindAttic.Vault's GitHub bucket, the one the Launcher's GitHubCredentials uses. The sweep is
/// slow and rate-limited, so it runs rarely (<see cref="Interval"/>) on the thread pool, never the caller's (UI) thread, and
/// <see cref="Get"/> answers from the cached index at once. No token, or a sweep that fails, leaves a repo unknown (null)
/// until a later sweep succeeds; a successful sweep stays until the next one replaces it. Each repo's origin remote is read
/// once, in the same background pass. Thread-safe.
/// </summary>
public sealed class RepoGitHubReader
{
    /// <summary>How long a successful sweep stands before <see cref="Get"/> starts another.</summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromHours(6);

    /// <summary>How long after a failed sweep (no token, GitHub unreachable or refusing) before trying again.</summary>
    public TimeSpan RetryInterval { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>The GitHub token; MindAttic.Vault's GitHub bucket by default. Replaceable for tests.</summary>
    internal Func<string?> Token { get; init; } = DefaultToken;

    /// <summary>The sweep: the token and the repos' slugs in, the index by lower-cased slug out (renamed repos under their old
    /// slug too). Throws on failure. Replaceable for tests.</summary>
    internal Func<string, IReadOnlyCollection<string>, IReadOnlyDictionary<string, GitHubRepoInfo>> FetchIndex { get; init; } = FetchNow;

    /// <summary>A repo folder's GitHub <c>owner/repo</c> from its origin remote, null when it has none. Replaceable for tests.</summary>
    internal Func<string, string?> ReadSlug { get; init; } = path => MindAttic.Tickets.Repos.RepoStatus.GitHubSlugFrom(ProjectDiscovery.RemoteUrl(path));

    private readonly ConcurrentDictionary<string, string?> slugs = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> paths = new(StringComparer.OrdinalIgnoreCase);
    private volatile IReadOnlyDictionary<string, GitHubRepoInfo>? index;
    private DateTime nextSweepUtc = DateTime.MinValue;
    private readonly object gate = new();
    private Task? running;

    /// <summary>The repo at <paramref name="path"/>'s GitHub standing from the cached index, null while unknown (its remote
    /// not read yet, no sweep has succeeded); starts a background pass when the sweep is due or a repo's remote hasn't been
    /// read, unless one is already running.</summary>
    public GitHubRepoState? Get(string path, DateTime nowUtc)
    {
        if (string.IsNullOrEmpty(path)) return null;
        paths.TryAdd(path, 0);
        var known = slugs.TryGetValue(path, out var slug);
        lock (gate)
        {
            if (running is null && (!known || nowUtc >= nextSweepUtc))
            {
                var sweep = nowUtc >= nextSweepUtc;
                if (sweep) nextSweepUtc = nowUtc + RetryInterval; // a sweep already in hand isn't started twice
                running = Task.Run(() => Pass(sweep, nowUtc));
            }
        }
        return known ? Classify(slug, index) : null;
    }

    /// <summary>Waits for the pass in flight; for tests and a one-off CLI run.</summary>
    public void WaitForReads(TimeSpan timeout)
    {
        Task? t;
        lock (gate) t = running;
        t?.Wait(timeout);
    }

    private void Pass(bool sweep, DateTime nowUtc)
    {
        try
        {
            foreach (var p in paths.Keys.Where(p => !slugs.ContainsKey(p)).ToList())
            {
                try { slugs[p] = ReadSlug(p); }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    // Left out of slugs (not cached as null) so a transient failure (e.g. .git/config
                    // momentarily locked by another process) is retried on the next pass instead of
                    // permanently marking the repo as having no GitHub remote.
                    Serilog.Log.Warning("GitHub repo reader: reading {Path:l}'s remote failed: {Error:l}", p, ex.Message);
                }
            }
            if (!sweep) return;
            var token = Token();
            if (string.IsNullOrWhiteSpace(token)) return;
            try
            {
                var wanted = slugs.Values.OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                index = FetchIndex(token, wanted);
                lock (gate) nextSweepUtc = nowUtc + Interval;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Serilog.Log.Warning("GitHub repo index sweep failed: {Error:l}", ex.Message);
            }
        }
        finally { lock (gate) running = null; }
    }

    /// <summary>A repo's standing from its <paramref name="slug"/> and the <paramref name="index"/>, the Launcher's
    /// TicketsMenu.GitHubCell rule: no GitHub remote, then unknown without an index, then not found, then archived (over
    /// <c>wip</c>), then <c>wip</c>-tagged, else untagged.</summary>
    public static GitHubRepoState? Classify(string? slug, IReadOnlyDictionary<string, GitHubRepoInfo>? index)
    {
        if (slug is null) return GitHubRepoState.NoRemote;
        if (index is null) return null;
        if (!index.TryGetValue(slug, out var info)) return GitHubRepoState.NotFound;
        if (info.Archived) return GitHubRepoState.Archived;
        return info.HasTopic(GitHubRepoIndex.WorkInProgressTopic) ? GitHubRepoState.Wip : GitHubRepoState.Untagged;
    }

    private static string? DefaultToken() =>
        TokenStore.ForBucket(GitHubRepoIndex.VaultBucket).Get(GitHubRepoIndex.VaultTokenName);

    private static IReadOnlyDictionary<string, GitHubRepoInfo> FetchNow(string token, IReadOnlyCollection<string> slugs)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var client = new GitHubRepoIndex();
        var all = client.FetchAsync(token, cts.Token).GetAwaiter().GetResult();
        return client.WithRenamesAsync(token, all, slugs, cts.Token).GetAwaiter().GetResult();
    }
}
