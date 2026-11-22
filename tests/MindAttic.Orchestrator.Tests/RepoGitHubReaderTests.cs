using MindAttic.Tickets.Repos;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// RepoGitHubReader (M.ORCH-T2038): the archived/wip/untagged mapping (the Launcher's TicketsMenu.GitHubCell rule), and that
/// Get answers null until its background sweep lands, then from the cache, with a failed or tokenless sweep leaving it unknown.
/// </summary>
public sealed class RepoGitHubReaderTests
{
    private static readonly DateTime Now = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);

    private static Dictionary<string, GitHubRepoInfo> Index(params GitHubRepoInfo[] repos) =>
        repos.ToDictionary(r => r.Slug, StringComparer.OrdinalIgnoreCase);

    [Test]
    public void No_remote_wins_even_without_an_index() =>
        Assert.That(RepoGitHubReader.Classify(null, null), Is.EqualTo(GitHubRepoState.NoRemote));

    [Test]
    public void Unknown_when_index_missing() =>
        Assert.That(RepoGitHubReader.Classify("o/r", null), Is.Null);

    [Test]
    public void Not_found() =>
        Assert.That(RepoGitHubReader.Classify("o/r", Index()), Is.EqualTo(GitHubRepoState.NotFound));

    [Test]
    public void Archived_beats_wip() =>
        Assert.That(RepoGitHubReader.Classify("o/r", Index(new GitHubRepoInfo("o/r", true, ["wip"]))),
            Is.EqualTo(GitHubRepoState.Archived));

    [Test]
    public void Wip_vs_untagged()
    {
        Assert.That(RepoGitHubReader.Classify("o/r", Index(new GitHubRepoInfo("o/r", false, ["WIP", "game"]))),
            Is.EqualTo(GitHubRepoState.Wip));
        Assert.That(RepoGitHubReader.Classify("o/r", Index(new GitHubRepoInfo("o/r", false, ["game"]))),
            Is.EqualTo(GitHubRepoState.Untagged));
    }

    [Test]
    public void Get_is_null_until_the_sweep_lands_then_cached()
    {
        var sweeps = 0;
        var reader = new RepoGitHubReader
        {
            Token = () => "t",
            ReadSlug = _ => "o/r",
            FetchIndex = (_, slugs) =>
            {
                sweeps++;
                Assert.That(slugs, Is.EquivalentTo(new[] { "o/r" }));
                return Index(new GitHubRepoInfo("o/r", false, ["wip"]));
            },
        };

        Assert.That(reader.Get(@"C:\r", Now), Is.Null);
        reader.WaitForReads(TimeSpan.FromSeconds(10));
        Assert.That(reader.Get(@"C:\r", Now.AddMinutes(1)), Is.EqualTo(GitHubRepoState.Wip));
        reader.WaitForReads(TimeSpan.FromSeconds(10));
        Assert.That(sweeps, Is.EqualTo(1));
    }

    [Test]
    public void Failed_or_tokenless_sweep_leaves_it_unknown()
    {
        var failing = new RepoGitHubReader
        {
            Token = () => "t",
            ReadSlug = _ => "o/r",
            FetchIndex = (_, _) => throw new InvalidOperationException("GitHub API returned 401 listing repos."),
        };
        failing.Get(@"C:\r", Now);
        failing.WaitForReads(TimeSpan.FromSeconds(10));
        Assert.That(failing.Get(@"C:\r", Now), Is.Null);

        var tokenless = new RepoGitHubReader { Token = () => null, ReadSlug = _ => "o/r", FetchIndex = (_, _) => throw new AssertionException("no sweep without a token") };
        tokenless.Get(@"C:\r", Now);
        tokenless.WaitForReads(TimeSpan.FromSeconds(10));
        Assert.That(tokenless.Get(@"C:\r", Now), Is.Null);
    }
}
