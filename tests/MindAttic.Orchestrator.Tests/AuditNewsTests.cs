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
}
