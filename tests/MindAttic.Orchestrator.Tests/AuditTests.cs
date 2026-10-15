using MindAttic.Orchestrator.Audit;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class AuditTests
{
    private string root = "";

    [SetUp]
    public void SetUp() => root = Directory.CreateTempSubdirectory("orch-audit-").FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(root, recursive: true);

    private (string Name, string Path) Repo(string name, Dictionary<string, string> files)
    {
        var path = Path.Combine(root, name);
        foreach (var (rel, text) in files)
        {
            var f = Path.Combine(path, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(f)!);
            File.WriteAllText(f, text);
        }
        Directory.CreateDirectory(Path.Combine(path, ".claude"));
        return (name, path);
    }

    private const string Hook = "# hook for {0}\nWrite-Host 'loading {0}'\nexit 0\n";

    [Test]
    public void The_majority_version_is_canonical_and_repo_names_do_not_count_as_drift()
    {
        var repos = new[]
        {
            Repo("Alpha", new() { [".claude/hooks/load.ps1"] = string.Format(Hook, "Alpha") }),
            Repo("Beta", new() { [".claude/hooks/load.ps1"] = string.Format(Hook, "Beta").Replace("\n", "\r\n") }),
            Repo("Gamma", new() { [".claude/hooks/load.ps1"] = string.Format(Hook, "Gamma") }),
            Repo("Delta", new() { [".claude/hooks/load.ps1"] = string.Format(Hook, "Delta") + "Remove-Item -Recurse x\n" }),
        };

        var (drift, _) = ConventionAudit.Run(repos);

        var d = drift.Single();
        Assert.That(d.Repo, Is.EqualTo("Delta"));
        Assert.That(d.CanonicalCount, Is.EqualTo(3));
        Assert.That(d.Similarity, Is.GreaterThan(0.7).And.LessThan(1));
    }

    [Test]
    public void Repos_missing_a_widely_shared_file_are_listed()
    {
        var repos = new[]
        {
            Repo("A", new() { [".claude/commands/quicksave.md"] = "save" }),
            Repo("B", new() { [".claude/commands/quicksave.md"] = "save" }),
            Repo("C", new() { [".claude/commands/quicksave.md"] = "save" }),
            Repo("D", new() { [".claude/commands/other.md"] = "x" }),
        };

        var (_, missing) = ConventionAudit.Run(repos);

        Assert.That(missing.Single(m => m.File == ".claude/commands/quicksave.md").Repo, Is.EqualTo("D"));
    }

    private static string Code(string name, int extra = 0) => string.Join('\n',
        new[] { "namespace X;", "public static class " + name + " {" }
        .Concat(Enumerable.Range(0, 30).Select(i => $"    public static int F{i}(int a) => a * {i} + {i % 7};"))
        .Concat(Enumerable.Range(0, extra).Select(i => $"    public static int Extra{i}() => {i};"))
        .Append("}"));

    [Test]
    public void A_run_over_the_workspace_is_saved_and_latest_reads_back_its_findings()
    {
        // The daemon's scheduled audit and its "what's new" alert both start from Latest().
        foreach (var name in new[] { "Alpha", "Beta", "Gamma", "Delta" })
        {
            var files = new Dictionary<string, string> { [".claude/hooks/load.ps1"] = string.Format(Hook, name) + (name == "Delta" ? "Remove-Item -Recurse x\n" : "") };
            if (name != "Delta") files[".claude/commands/quicksave.md"] = "save";
            if (name is "Alpha" or "Beta") files["src/Util.cs"] = Code("Util");
            Directory.CreateDirectory(Path.Combine(Repo(name, files).Path, ".git"));
        }
        var store = new MindAttic.Orchestrator.Settings.JsonStore(Path.Combine(root, "store"));
        var runner = new AuditRunner(new OrchestratorOptions { WorkspaceRoot = root }, store);
        Assert.That(runner.Latest(), Is.Null, "no audit yet");

        var report = runner.Run();
        var latest = new AuditRunner(new OrchestratorOptions { WorkspaceRoot = root }, store).Latest()!;

        Assert.That((report.ReposScanned, report.Drift.Single().Repo, report.Missing.Single(m => m.File.EndsWith("quicksave.md")).Repo),
            Is.EqualTo((4, "Delta", "Delta")));
        Assert.That(report.Duplicates.Single().Files.Select(f => f.Repo), Is.EquivalentTo(new[] { "Alpha", "Beta" }));
        Assert.That((latest.RanUtc, latest.Took, latest.ReposScanned, latest.FilesScanned), Is.EqualTo((report.RanUtc, report.Took, report.ReposScanned, report.FilesScanned)));
        Assert.That(latest.Drift, Is.EqualTo(report.Drift));
        Assert.That(latest.Missing, Is.EqualTo(report.Missing));
        Assert.That(latest.Duplicates.Select(g => (g.Identical, string.Join(",", g.Files.Select(f => $"{f.Repo}:{f.Path}")))),
            Is.EqualTo(report.Duplicates.Select(g => (g.Identical, string.Join(",", g.Files.Select(f => $"{f.Repo}:{f.Path}"))))));
    }

    [Test]
    public void Finds_identical_and_near_copies_across_repos_but_not_within_one()
    {
        var repos = new[]
        {
            Repo("One", new() { ["src/Util.cs"] = Code("Util"), ["src/Same.cs"] = Code("Same") }),
            Repo("Two", new() { ["lib2/Util.cs"] = Code("Util") }),
            Repo("Three", new() { ["Helpers/Util.cs"] = Code("Util", extra: 6), ["bin/Util.cs"] = Code("Util") }),
        };

        var (groups, scanned) = DuplicationAudit.Run(repos);

        Assert.That(scanned, Is.EqualTo(4), "bin/ is skipped");
        var identical = groups.Single(g => g.Identical);
        Assert.That(identical.Files.Select(f => f.Repo), Is.EquivalentTo(new[] { "One", "Two" }));
        Assert.That(identical.SuggestedHome, Does.Contain("MindAttic.Helpers"));
        var near = groups.Where(g => !g.Identical).ToList();
        Assert.That(near, Is.Not.Empty);
        Assert.That(near.SelectMany(g => g.Files).Any(f => f.Repo == "Three"));
    }

    [Test]
    public void Near_copies_in_many_repos_cluster_into_one_finding()
    {
        var repos = Enumerable.Range(0, 4)
            .Select(i => Repo($"R{i}", new() { ["src/Util.cs"] = Code("Util", extra: i) }))
            .ToArray();

        var (groups, _) = DuplicationAudit.Run(repos);

        Assert.That(groups, Has.Count.EqualTo(1));
        Assert.That(groups[0].RepoCount, Is.EqualTo(4));
    }

    [Test]
    public void Generated_migrations_are_not_duplication()
    {
        var repos = new[]
        {
            Repo("A", new() { ["Migrations/AppDbContextModelSnapshot.cs"] = Code("S") }),
            Repo("B", new() { ["Data/Migrations/X.cs"] = Code("S") }),
        };
        Assert.That(DuplicationAudit.Run(repos).Groups, Is.Empty);
    }

    [Test]
    public void Boilerplate_lines_are_ignored()
    {
        var file = Path.Combine(root, "x.cs");
        File.WriteAllText(file, "using System;\n// note\n{\n  int a  =   1;\n}\n/* block\n still */\nreturn a;");
        Assert.That(DuplicationAudit.SignificantLines(file), Is.EqualTo(new[] { "int a = 1;", "return a;" }));
    }
}
