using MindAttic.Orchestrator.Scheduling;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// RepoLocalWorkReader (M.ORCH-T2036) against a real temp git repo with a bare origin: the uncommitted, unpushed, stash and
/// local-only-ignored counts it reads, that the first Get answers null and reads in the background, and that it doesn't read
/// again inside its interval.
/// </summary>
public sealed class RepoLocalWorkReaderTests
{
    private string dir = "";
    private string repo = "";

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "orch-localwork-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var origin = Path.Combine(dir, "origin.git");
        repo = Path.Combine(dir, "A");
        Git(dir, "init", "-q", "--bare", "-b", "main", origin);
        Git(dir, "clone", "-q", origin, repo);
        Git(repo, "config", "user.email", "t@t");
        Git(repo, "config", "user.name", "t");
        Git(repo, "symbolic-ref", "HEAD", "refs/heads/main");
        File.WriteAllText(Path.Combine(repo, ".gitignore"), "notes/\nbin/\n");
        File.WriteAllText(Path.Combine(repo, "a.txt"), "a");
        Git(repo, "add", "-A");
        Git(repo, "commit", "-qm", "init");
        Git(repo, "push", "-q", "-u", "origin", "main");
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(dir, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    [Test]
    public void A_clean_pushed_repo_has_nothing_local()
    {
        var work = RepoLocalWorkReader.ReadNow("A", repo);

        Assert.That(work.Error, Is.Null);
        Assert.That(work.HasLocalOnlyWork, Is.False);
    }

    [Test]
    public void Counts_uncommitted_unpushed_stashed_and_local_only_ignored_work()
    {
        // Two unpushed commits.
        File.WriteAllText(Path.Combine(repo, "b.txt"), "b");
        Git(repo, "add", "b.txt");
        Git(repo, "commit", "-qm", "b");
        File.WriteAllText(Path.Combine(repo, "c.txt"), "c");
        Git(repo, "add", "c.txt");
        Git(repo, "commit", "-qm", "c");
        // One stash.
        File.WriteAllText(Path.Combine(repo, "a.txt"), "stashed");
        Git(repo, "stash", "-q");
        // Two uncommitted: a modified file and an untracked one.
        File.WriteAllText(Path.Combine(repo, "a.txt"), "edited");
        File.WriteAllText(Path.Combine(repo, "new.txt"), "new");
        // One ignored folder worth keeping, and one a build regenerates.
        Directory.CreateDirectory(Path.Combine(repo, "notes"));
        File.WriteAllText(Path.Combine(repo, "notes", "draft.md"), "x");
        Directory.CreateDirectory(Path.Combine(repo, "bin"));
        File.WriteAllText(Path.Combine(repo, "bin", "out.dll"), "x");

        var work = RepoLocalWorkReader.ReadNow("A", repo);

        Assert.That(work.Error, Is.Null);
        Assert.That(work.Uncommitted, Is.EqualTo(2));
        Assert.That(work.Unpushed, Is.EqualTo(2));
        Assert.That(work.Stashes, Is.EqualTo(1));
        Assert.That(work.LocalOnlyIgnored, Is.EqualTo(1));
        Assert.That(work.HasLocalOnlyWork, Is.True);
    }

    [Test]
    public void Get_answers_null_first_reads_in_the_background_and_holds_its_reading_for_the_interval()
    {
        File.WriteAllText(Path.Combine(repo, "new.txt"), "new");
        var reads = 0;
        var reader = new RepoLocalWorkReader
        {
            Interval = TimeSpan.FromHours(1),
            ReadRepo = (name, path) => { Interlocked.Increment(ref reads); return RepoLocalWorkReader.ReadNow(name, path); },
        };
        var now = DateTime.UtcNow;

        Assert.That(reader.Get("A", repo, now), Is.Null);
        reader.WaitForReads(TimeSpan.FromSeconds(30));
        var first = reader.Get("A", repo, now);
        reader.WaitForReads(TimeSpan.FromSeconds(30));

        Assert.That(first, Is.Not.Null);
        Assert.That(first!.Uncommitted, Is.EqualTo(1));
        Assert.That(reads, Is.EqualTo(1), "a reading inside its interval isn't read again");

        reader.Get("A", repo, now.AddHours(2));
        reader.WaitForReads(TimeSpan.FromSeconds(30));
        Assert.That(reads, Is.EqualTo(2), "a stale reading is read again");
    }

    private static void Git(string where, params string[] args)
    {
        var (code, output) = Dispatcher.Git(where, args);
        Assert.That(code, Is.Zero, $"git {string.Join(" ", args)}: {output}");
    }
}
