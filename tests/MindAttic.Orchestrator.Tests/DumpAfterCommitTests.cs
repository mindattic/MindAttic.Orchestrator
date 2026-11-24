using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>orchestrator.sql is rewritten only from what a write committed (M.ORCH-T2145): a commit that fails leaves the dump
/// as it was, and a dump that fails after the commit doesn't fail the save (M.ORCH-T2165).</summary>
[NonParallelizable]
public sealed class DumpAfterCommitTests
{
    private const string Name = "dump-after-commit-test";
    private readonly TempDirs temps = new();

    [TearDown]
    public void TearDown() => temps.DeleteAll();

    private string NewPath() => Path.Combine(temps.New(), "data", "orchestrator.db");

    private static OrchestratorDb FailingCommit(string path) =>
        new(path) { CommitChange = _ => throw new InvalidOperationException("commit failed") };

    private static OrchestratorDb FailingDump(string path) =>
        new(path) { BeforeCommittedDump = () => throw new IOException("dump failed") };

    [Test]
    public void A_failed_commit_leaves_the_dump_and_the_data_as_they_were()
    {
        var path = NewPath();
        var db = new OrchestratorDb(path);
        db.SaveState(Name, "\"before\"");
        var dump = File.ReadAllText(db.DumpPath);
        Assert.That(dump, Does.Contain("before"));

        Assert.That(() => FailingCommit(path).SaveState(Name, "\"after\""), Throws.InvalidOperationException);

        Assert.That(File.ReadAllText(db.DumpPath), Is.EqualTo(dump));
        Assert.That(db.LoadState(Name)?.Value, Is.EqualTo("\"before\""));
    }

    [Test]
    public void A_failed_commit_through_Write_leaves_the_dump_as_it_was()
    {
        var path = NewPath();
        var db = new OrchestratorDb(path);
        db.SaveState(Name, "\"before\"");
        var dump = File.ReadAllText(db.DumpPath);

        Assert.That(() => FailingCommit(path).Write((connection, tx) =>
            OrchestratorDb.Execute(connection, $"UPDATE state SET value = '\"after\"' WHERE name = '{Name}';", tx)),
            Throws.InvalidOperationException);

        Assert.That(File.ReadAllText(db.DumpPath), Is.EqualTo(dump));
        Assert.That(db.LoadState(Name)?.Value, Is.EqualTo("\"before\""));
    }

    [Test]
    public void A_failed_commit_on_a_new_database_writes_no_dump()
    {
        var db = FailingCommit(NewPath());

        Assert.That(() => db.SaveState(Name, "\"after\""), Throws.InvalidOperationException);

        Assert.That(File.Exists(db.DumpPath), Is.False);
    }

    [Test]
    public void A_dump_that_fails_after_the_commit_does_not_fail_the_save()
    {
        var path = NewPath();
        var db = new OrchestratorDb(path);
        db.SaveState(Name, "\"before\"");
        var dump = File.ReadAllText(db.DumpPath);

        Assert.That(() => FailingDump(path).SaveState(Name, "\"after\""), Throws.Nothing);

        Assert.That(db.LoadState(Name)?.Value, Is.EqualTo("\"after\""));
        Assert.That(File.ReadAllText(db.DumpPath), Is.EqualTo(dump), "the failed dump is left for the next change");

        db.SaveState("dump-after-commit-other", "1");
        Assert.That(File.ReadAllText(db.DumpPath), Does.Contain("after").And.Contain("dump-after-commit-other"));
    }
}
