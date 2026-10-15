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
}
