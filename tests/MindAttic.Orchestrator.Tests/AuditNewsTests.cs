using MindAttic.Orchestrator.Audit;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class AuditNewsTests
{
    private static DriftFinding Drift(string repo, string file, double similarity = 0.9) =>
        new(file, repo, $"{repo}/{file}", "canon", 10, 12, 2, similarity);

    private static DuplicateGroup Dup(params string[] repos) =>
        new([.. repos.Select(r => new DuplicateFile(r, "Util.cs", 40))], true, 1, 40, "MindAttic.Helpers");

    private static AuditReport Report(IReadOnlyList<DriftFinding> drift, IReadOnlyList<DuplicateGroup> dups) =>
        new(DateTime.UtcNow, TimeSpan.Zero, 2, 10, drift, [], dups);

    [Test]
    public void Only_what_the_last_audit_did_not_have_is_news()
    {
        var before = Report([Drift("A", "hook.ps1")], [Dup("A", "B")]);
        var after = Report([Drift("A", "hook.ps1"), Drift("B", "hook.ps1"), Drift("C", "own.md", 0.1)], [Dup("A", "B"), Dup("B", "C")]);

        var news = AuditNews.Between(before, after);

        Assert.That(news.Drift.Select(d => d.Repo), Is.EqualTo(new[] { "B" }), "a custom file (under 30% alike) isn't drift");
        Assert.That(news.Duplicates, Has.Count.EqualTo(1));
        Assert.That(news.ToString(), Does.Contain("B/hook.ps1").And.Contain("B+C"));
        Assert.That(AuditNews.Between(null, after).Any, Is.False, "the first audit has nothing to compare with");
    }

    [Test]
    public void A_file_once_only_custom_is_news_once_it_becomes_genuine_drift()
    {
        // Was under 30% alike (a different file, not a stale copy) in the old report, so it carried no weight there;
        // now it's a genuine stale copy of the canonical file. A past "custom" sighting of the same (Repo, File) must
        // never permanently poison it against ever being reported as new drift.
        var before = Report([Drift("A", "weird.md", 0.1)], []);
        var after = Report([Drift("A", "weird.md", 0.5)], []);

        var news = AuditNews.Between(before, after);

        Assert.That(news.Drift.Select(d => d.Repo), Is.EqualTo(new[] { "A" }));
    }

    [Test]
    public void With_no_previous_report_nothing_is_news_however_much_the_audit_found()
    {
        var after = Report([Drift("A", "hook.ps1"), Drift("B", "hook.ps1")], [Dup("A", "B")]);

        var news = AuditNews.Between(null, after);

        Assert.That(news.Drift, Is.Empty);
        Assert.That(news.Duplicates, Is.Empty);
        Assert.That((news.Any, news.ToString()), Is.EqualTo((false, "")));
        Assert.That(AuditNews.Between(null, Report([], [])).Any, Is.False);
    }

    [Test]
    public void The_news_line_says_one_or_many_and_names_at_most_three()
    {
        Assert.That(new AuditNews([Drift("A", "x.ps1")], [Dup("A", "B")]).ToString(),
            Is.EqualTo("1 newly drifted agent file (A/x.ps1); 1 new duplicated-code group (A+B)"));
        Assert.That(new AuditNews([.. "ABCD".Select(r => Drift(r.ToString(), "x.ps1"))], []).ToString(),
            Is.EqualTo("4 newly drifted agent files (A/x.ps1, B/x.ps1, C/x.ps1, …)"));
        Assert.That(new AuditNews([], [Dup("A", "B"), Dup("B", "C"), Dup("C", "D"), Dup("D", "A")]).ToString(),
            Is.EqualTo("4 new duplicated-code groups (A+B, B+C, C+D, …)"));
        Assert.That(new AuditNews([], [Dup("A", "B"), Dup("B", "C")]).ToString(), Is.EqualTo("2 new duplicated-code groups (A+B, B+C)"), "no ellipsis at three or fewer");
        Assert.That((new AuditNews([], []).ToString(), new AuditNews([], []).Any), Is.EqualTo(("", false)));
    }
}
