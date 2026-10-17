using Microsoft.Data.Sqlite;
using MindAttic.Orchestrator.Tickets;
using MindAttic.Workflow;
using MindAttic.Workflow.Data;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class OpenTicketSourceTests
{
    private string root = "";

    [SetUp]
    public void SetUp()
    {
        root = Path.Combine(Path.GetTempPath(), "orch-open-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
    }

    [TearDown]
    public void TearDown()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(root, recursive: true);
    }

    private Repo Repo(string name, string code, string rows)
    {
        var dir = Path.Combine(root, name);
        Directory.CreateDirectory(Path.Combine(dir, ".git"));
        Directory.CreateDirectory(Path.Combine(dir, "docs"));
        File.WriteAllText(Path.Combine(dir, "docs", "USER_STORIES.md"),
            $"---\ncode: {code}\n---\n\n# {name}\n\n## Tickets\n\n| ID | Date | Type | Status | Summary | Ref |\n|---|---|---|---|---|---|\n" + rows);
        return new Repo(name, dir);
    }

    [Test]
    public void Without_a_database_it_reads_open_tickets_from_the_markdown_and_rereads_only_on_change()
    {
        var a = Repo("A", "A", "| A-T1 | 2026-10-01 | fix | open | one | — |\n| A-T2 | 2026-10-01 | fix | closed | two | x |\n");
        var source = new OpenTicketSource();
        var warnings = new List<string>();

        var first = source.OpenFor(a, warnings);
        Assert.That(first.Select(t => t.Id), Is.EqualTo(new[] { "A-T1" }), "only open ones");
        Assert.That(source.OpenFor(a, warnings), Is.SameAs(first), "unchanged file: the cached list");

        File.AppendAllText(a.StoriesPath, "| A-T3 | 2026-10-02 | fix | open | three | — |\n");
        File.SetLastWriteTimeUtc(a.StoriesPath, DateTime.UtcNow.AddSeconds(5));
        Assert.That(source.OpenFor(a, warnings).Select(t => t.Id), Is.EqualTo(new[] { "A-T1", "A-T3" }));
        Assert.That(source.OpenFor(new Repo("None", Path.Combine(root, "None")), warnings), Is.Empty, "no file: nothing");
        Assert.That(warnings, Is.Empty);
    }

    [Test]
    public void Once_migrated_it_reads_the_database_with_its_prerequisites_and_falls_back_for_repos_not_in_it()
    {
        var a = Repo("A", "A", "| A-T1 | 2026-10-01 | fix | open | one, blocked by A-T2 | — |\n| A-T2 | 2026-10-01 | fix | open | two | — |\n");
        var b = Repo("B", "B", "| B-T1 | 2026-10-01 | fix | open | only in markdown | — |\n");
        using (var db = WorkflowDb.Open(WorkflowDb.DefaultPath(root)))
        {
            db.Import([a]);
            db.Close("A-T2", reference: "done");
        }
        File.WriteAllText(a.StoriesPath, "edited by hand: ignored, the database is the store\n");

        var source = new OpenTicketSource();
        var warnings = new List<string>();
        Assert.That(source.OpenFor(a, warnings).Select(t => t.Id), Is.EqualTo(new[] { "A-T1" }), "A-T2 was closed in the database");
        Assert.That(source.Requires["A-T1"], Is.EqualTo(new[] { "A-T2" }), "'blocked by' became a requires link");
        Assert.That(source.OpenFor(b, warnings).Select(t => t.Id), Is.EqualTo(new[] { "B-T1" }), "B isn't migrated: its markdown");
        Assert.That(warnings, Is.Empty);
    }

    [Test]
    public void A_database_change_on_disk_is_picked_up_and_without_one_there_are_no_prerequisites()
    {
        var a = Repo("A", "A", "| A-T1 | 2026-10-01 | fix | open | one, blocked by A-T2 | — |\n| A-T2 | 2026-10-01 | fix | open | two | — |\n");
        var source = new OpenTicketSource();
        var warnings = new List<string>();
        source.OpenFor(a, warnings);
        Assert.That(source.Requires, Is.Empty, "the markdown carries no requires links");

        var path = WorkflowDb.DefaultPath(root);
        using (var db = WorkflowDb.Open(path)) db.Import([a]);
        Assert.That(source.OpenFor(a, warnings).Select(t => t.Id), Is.EqualTo(new[] { "A-T1", "A-T2" }));
        Assert.That(source.Requires["a-t1"], Is.EqualTo(new[] { "A-T2" }), "looked up without regard to case");

        using (var db = WorkflowDb.Open(path)) db.Close("A-T1", reference: "done");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(5));
        Assert.That(source.OpenFor(a, warnings).Select(t => t.Id), Is.EqualTo(new[] { "A-T2" }), "re-read once the database changed");
        Assert.That(warnings, Is.Empty);
    }

    [Test]
    public void A_database_read_that_fails_keeps_the_last_good_read_not_the_markdown()
    {
        // The markdown lags the database: it still lists A-T1 open, and lacks A-T3, which autopilot is working.
        var a = Repo("A", "A", "| A-T1 | 2026-10-01 | fix | open | one | — |\n");
        var path = WorkflowDb.DefaultPath(root);
        using (var db = WorkflowDb.Open(path))
        {
            db.Import([a]);
            db.Close("A-T1", reference: "done");
            db.Add("A", "fix", MindAttic.Workflow.Tickets.TicketRules.Open, "three", "—");
        }
        var source = new OpenTicketSource();
        var warnings = new List<string>();
        var good = source.OpenFor(a, warnings).Select(t => t.Id).ToList();
        Assert.That(good, Has.Count.EqualTo(1).And.None.EqualTo("A-T1"));

        SqliteConnection.ClearAllPools();
        var saved = File.ReadAllBytes(path);
        File.WriteAllText(path, "not a database: caught mid-write");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(5));
        Assert.That(source.OpenFor(a, warnings).Select(t => t.Id), Is.EqualTo(good), "the last good read, not the stale markdown");
        Assert.That(warnings, Has.Count.EqualTo(1).And.Some.StartsWith("workflow database:"));

        // WorkflowDb's constructor leaves its connection open when the schema read throws (a MindAttic.Workflow bug, filed
        // there); only the finalizer lets the file go.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        File.WriteAllBytes(path, saved);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(10));
        Assert.That(source.OpenFor(a, warnings).Select(t => t.Id), Is.EqualTo(good), "read again once it can be");
    }
}
