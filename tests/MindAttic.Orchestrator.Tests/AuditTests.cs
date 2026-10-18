using System.Security.AccessControl;
using System.Security.Principal;
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
    public void A_repo_name_is_stripped_only_as_a_whole_word()
    {
        // Found writing the AuditRunner test: a repo named "A" turned "save" into "s{REPO}ve" and reported drift.
        Assert.That(ConventionAudit.Normalize("save the Prose notes; prosebook stays", "Prose", @"C:\W\Prose"), Is.EqualTo("save the {REPO} notes; prosebook stays"));
        Assert.That(ConventionAudit.Normalize("using MindAttic.Orchestrator.Core; MindAttic.OrchestratorX", "MindAttic.Orchestrator", @"C:\W\MindAttic.Orchestrator"),
            Is.EqualTo("using {REPO}.Core; MindAttic.OrchestratorX"), "a namespace under it counts; a longer name doesn't");
        Assert.That(ConventionAudit.Normalize("cd C:\\W\\A && echo a-b A", "A", @"C:\W\A"), Is.EqualTo("cd {PATH} && echo a-b {REPO}"));

        var repos = new[] { "A", "B", "C" }.Select(n => Repo(n, new() { [".claude/commands/quicksave.md"] = "save the handoff" })).ToArray();
        Assert.That(ConventionAudit.Run(repos).Drift, Is.Empty, "one-letter repo names no longer make identical files differ");
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

    [Test]
    public void Repos_without_agent_files_dont_count_toward_how_many_have_a_file()
    {
        // "(5/2 have it)": every repo with an AGENTS.md was counted, but only the .claude ones were eligible.
        var plain = new[] { "P1", "P2", "P3", "P4" }.Select(n =>
        {
            var path = Path.Combine(root, n);
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "AGENTS.md"), "read the standard");
            return (n, path);
        });
        var repos = plain.Concat(new[]
        {
            Repo("A", new() { ["AGENTS.md"] = "read the standard" }),
            Repo("B", new() { [".claude/commands/x.md"] = "x" }),
            Repo("C", new() { [".claude/commands/y.md"] = "y" }),
        }).ToArray();

        var (_, missing) = ConventionAudit.Run(repos);

        Assert.That(missing.Where(m => m.File == "AGENTS.md"), Is.Empty, "1 of 3 agent repos has it: under the bar");
        Assert.That(missing.All(m => m.HaveIt <= m.Eligible), Is.True);
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
    public void An_agent_file_that_cant_be_read_is_skipped_instead_of_failing_the_run()
    {
        var repos = new[] { "Alpha", "Beta", "Gamma", "Delta" }.Select(n => Repo(n, new()
        {
            [".claude/hooks/load.ps1"] = string.Format(Hook, n) + (n == "Delta" ? "Remove-Item -Recurse x\n" : ""),
            [".claude/commands/quicksave.md"] = n == "Delta" ? "save it somewhere else" : "save",
        })).ToArray();
        var file = new FileInfo(Path.Combine(repos[3].Path, ".claude", "hooks", "load.ps1"));
        var deny = new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.ReadData, AccessControlType.Deny);
        var acl = file.GetAccessControl();
        acl.AddAccessRule(deny);
        file.SetAccessControl(acl);
        try
        {
            Assert.That(() => File.ReadAllText(file.FullName), Throws.InstanceOf<UnauthorizedAccessException>(), "the setup really denies reading");

            var (drift, _) = ConventionAudit.Run(repos);

            var d = drift.Single();
            Assert.That((d.Repo, d.File), Is.EqualTo(("Delta", ".claude/commands/quicksave.md")),
                "the unreadable hook is left out; the same repo's readable files are still audited");
        }
        finally
        {
            acl = file.GetAccessControl();
            acl.RemoveAccessRule(deny);
            file.SetAccessControl(acl);
        }
    }

    [TestCase(@"C:\W\A\.claude\commands\quicksave.md", "mindattic-agent-standard")]
    [TestCase(@"C:\W\A\.claude\hooks\load.ps1", "mindattic-agent-standard")]
    [TestCase("C:/W/A/.prose/commands/x.md", "mindattic-agent-standard")]
    [TestCase(@"C:\W\A\Views\Main.xaml", "MindAttic.Components (WPF)")]
    [TestCase(@"C:\W\A\Pages\Index.razor", "MindAttic.Components (web/Blazor)")]
    [TestCase(@"C:\W\A\wwwroot\site.CSS", "MindAttic.Components (web/Blazor)")]
    [TestCase(@"C:\W\A\app\main.tsx", "MindAttic.Components (web/Blazor)")]
    [TestCase(@"C:\W\A\deploy.ps1", "MindAttic.Deploy or mindattic-agent-standard")]
    [TestCase(@"C:\W\A\tools\Build.psm1", "MindAttic.Deploy or mindattic-agent-standard")]
    [TestCase(@"C:\W\A\scripts\run.py", "a shared Python package")]
    [TestCase(@"C:\W\A\src\Util.cs", "MindAttic.Helpers (or a new shared package)")]
    public void Duplication_suggests_the_home_for_its_kind_of_file(string path, string home) =>
        Assert.That(DuplicationAudit.HomeFor(path), Is.EqualTo(home));

    [Test]
    public void Run_saves_audit_json_and_latest_is_null_when_it_is_corrupt()
    {
        foreach (var name in new[] { "Alpha", "Beta" })
            Directory.CreateDirectory(Path.Combine(Repo(name, new() { ["src/Util.cs"] = Code("Util") }).Path, ".git"));
        var dir = Path.Combine(root, "store");
        var runner = new AuditRunner(new OrchestratorOptions { WorkspaceRoot = root }, new MindAttic.Orchestrator.Settings.JsonStore(dir));

        runner.Run();

        var json = Path.Combine(dir, "audit.json");
        Assert.That(File.Exists(json), Is.True, "the report is kept as audit.json");
        Assert.That(runner.Latest(), Is.Not.Null);

        File.WriteAllText(json, "{ \"RanUtc\": \"2026-10-17T00:00:00Z\", \"Drift\": [ trunc");
        Assert.That(runner.Latest(), Is.Null, "half-written JSON reads as no audit, not an exception");

        File.WriteAllText(json, "{}");
        Assert.That(runner.Latest(), Is.Null, "a report with no run time isn't a report");
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
    public void A_drifted_copy_of_a_file_two_repos_hold_identically_is_reported()
    {
        // M.ORCH-T352: the identical pair was left out of the near-copy search, so the third copy matched nothing.
        var repos = new[]
        {
            Repo("One", new() { ["src/Util.cs"] = Code("Util") }),
            Repo("Two", new() { ["lib2/Util.cs"] = Code("Util") }),
            Repo("Three", new() { ["Helpers/Util.cs"] = Code("Util", extra: 6) }),
        };

        var (groups, _) = DuplicationAudit.Run(repos);

        Assert.That(groups.Single(g => g.Identical).Files.Select(f => f.Repo), Is.EquivalentTo(new[] { "One", "Two" }));
        Assert.That(groups.Where(g => !g.Identical).SelectMany(g => g.Files).Select(f => f.Repo), Does.Contain("Three"));
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
