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
}
