using MindAttic.Orchestrator.Tickets;
using MindAttic.Workflow.Tickets;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class GitHubIntakeTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    private static (int, string) FakeGh(string[] args) => args[0] switch
    {
        "issue" => (0, """
            [{"number":12,"title":"Crash | on save","labels":[{"name":"bug"},{"name":"orchestrator"}],"url":"https://github.com/o/r/issues/12"},
             {"number":13,"title":"Dark mode","labels":[{"name":"enhancement"}],"url":"https://github.com/o/r/issues/13"}]
            """),
        "run" => (0, """
            [{"databaseId":987,"name":"build","headBranch":"main","createdAt":"2026-10-14T09:00:00Z","url":"https://github.com/o/r/actions/runs/987"},
             {"databaseId":988,"name":"build","headBranch":"feature-x","createdAt":"2026-10-14T09:00:00Z","url":"u"},
             {"databaseId":900,"name":"build","headBranch":"main","createdAt":"2026-10-10T09:00:00Z","url":"u"}]
            """),
        _ => (1, ""),
    };

    [TestCase("https://github.com/mindattic/Prose.git", "mindattic/Prose")]
    [TestCase("git@github.com:mindattic/Prose.git", "mindattic/Prose")]
    [TestCase("https://gitlab.com/x/y.git", null)]
    public void Reads_the_repo_slug_from_its_remote(string remote, string? slug) => Assert.That(GitHubIntake.Slug(remote), Is.EqualTo(slug));

    [Test]
    public void Files_labelled_issues_and_todays_main_failures_once()
    {
        var items = GitHubIntake.Find("o/r", FakeGh, Now);
        Assert.That(items.Select(i => (i.Marker, i.Type)), Is.EqualTo(new[] { ("GH#12", "fix"), ("GH#13", "feature"), ("CI#987", "fix") }),
            "branch runs and old failures are skipped");

        var stories = StoryFile.Parse(StoryFile.NewFileText("R", "RP", "2026-10-14"));
        var first = GitHubIntake.FileNew(stories, items);
        var again = GitHubIntake.FileNew(stories, items);

        Assert.That(first.Select(t => t.Summary), Is.EqualTo(new[] { "GH#12: Crash / on save", "GH#13: Dark mode", "CI#987: build failed on main" }),
            "a pipe would break the table row");
        Assert.That(first[0].Ref, Is.EqualTo("https://github.com/o/r/issues/12"));
        Assert.That(again, Is.Empty, "filed once");
    }

    [Test]
    public void Reads_an_issues_type_label_in_any_case()
    {
        // M.ORCH-T443: only "bug" was read in any case; "Feature" fell through to fix, which is worked without approval.
        (int, string) Gh(string[] args) => args[0] == "issue" ? (0, """
            [{"number":1,"title":"a","labels":[{"name":"Feature"}],"url":"u1"},
             {"number":2,"title":"b","labels":[{"name":"Enhancement"}],"url":"u2"},
             {"number":3,"title":"c","labels":[{"name":"Documentation"}],"url":"u3"},
             {"number":4,"title":"d","labels":[{"name":"DOCS"}],"url":"u4"},
             {"number":5,"title":"e","labels":[{"name":"Chore"}],"url":"u5"},
             {"number":6,"title":"f","labels":[{"name":"BUG"},{"name":"Feature"}],"url":"u6"}]
            """) : (1, "");

        Assert.That(GitHubIntake.Find("o/r", Gh, Now).Select(i => i.Type),
            Is.EqualTo(new[] { "feature", "feature", "docs", "docs", "chore", "fix" }));
    }

    [Test]
    public void Keeps_a_failure_on_master_within_a_day_and_skips_one_a_minute_past_it_or_off_the_main_line()
    {
        (int, string) Gh(string[] args) => args[0] switch
        {
            "issue" => (1, "gh: not logged in"),
            "run" => (0, """
                [{"databaseId":1,"name":"build","headBranch":"master","createdAt":"2026-10-13T12:00:00Z","url":"u1"},
                 {"databaseId":2,"name":"build","headBranch":"main","createdAt":"2026-10-13T11:59:00Z","url":"u2"},
                 {"databaseId":3,"name":"build","headBranch":"mainline","createdAt":"2026-10-14T11:00:00Z","url":"u3"},
                 {"databaseId":4,"name":"build","headBranch":"main","createdAt":"not a date","url":"u4"}]
                """),
            _ => (1, ""),
        };

        var items = GitHubIntake.Find("o/r", Gh, Now);

        Assert.That(items.Select(i => (i.Marker, i.Summary, i.Url)), Is.EqualTo(new[] { ("CI#1", "CI#1: build failed on master", "u1") }),
            "exactly a day old still counts; older, another branch or an unreadable time doesn't; a failed issue call is skipped");
        Assert.That(GitHubIntake.Find("o/r", _ => (0, "not json"), Now), Is.Empty, "bad output: nothing, not an error");
    }

    [Test]
    public void Files_each_marker_once_in_the_database_and_GH1_is_not_GH12()
    {
        var root = Path.Combine(Path.GetTempPath(), "orch-intake-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            MindAttic.Workflow.Repo Repo(string name, string code)
            {
                var dir = Path.Combine(root, name);
                Directory.CreateDirectory(Path.Combine(dir, ".git"));
                Directory.CreateDirectory(Path.Combine(dir, "docs"));
                File.WriteAllText(Path.Combine(dir, "docs", "USER_STORIES.md"), StoryFile.NewFileText(name, code, "2026-10-14"));
                return new MindAttic.Workflow.Repo(name, dir);
            }
            using (var db = MindAttic.Workflow.Data.WorkflowDb.Open(MindAttic.Workflow.Data.WorkflowDb.DefaultPath(root)))
            {
                db.Import([Repo("R", "RP"), Repo("S", "SP")]);
                var items = new[]
                {
                    new IntakeItem("GH#12", "fix", "GH#12: Crash", "u12"),
                    new IntakeItem("CI#987", "fix", "CI#987: build failed on main", "u987"),
                    new IntakeItem("GH#12", "fix", "GH#12: Crash", "u12"),
                };
                var preview = GitHubIntake.FileNew(db, "R", items, dryRun: true);
                Assert.That(preview.Select(t => t.Summary), Is.EqualTo(new[] { "GH#12: Crash", "CI#987: build failed on main" }), "a dry run says what it would file");
                Assert.That(db.Tickets("R"), Is.Empty, "and files nothing");
                var first = GitHubIntake.FileNew(db, "R", items);
                Assert.That(first.Select(t => t.Summary), Is.EqualTo(new[] { "GH#12: Crash", "CI#987: build failed on main" }), "a repeat in one batch is filed once");
                Assert.That(GitHubIntake.FileNew(db, "R", items), Is.Empty, "already filed");
                Assert.That(GitHubIntake.FileNew(db, "R", [new IntakeItem("GH#1", "fix", "GH#1: Other", "u1")]).Single().Summary,
                    Is.EqualTo("GH#1: Other"), "GH#12 doesn't count as GH#1");
                Assert.That(GitHubIntake.FileNew(db, "S", [items[0]]), Has.Count.EqualTo(1), "another repo has its own tickets");
            }

            var stories = StoryFile.Parse(StoryFile.NewFileText("R", "RP", "2026-10-14"));
            Assert.That(GitHubIntake.FileNew(stories, [new IntakeItem("GH#12", "fix", "GH#12: Crash", "u"), new IntakeItem("GH#12", "fix", "GH#12: Crash", "u")]),
                Has.Count.EqualTo(1), "markdown too: a repeat in one batch is filed once");
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }
}
