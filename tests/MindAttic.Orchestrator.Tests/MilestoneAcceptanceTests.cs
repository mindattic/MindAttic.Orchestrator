using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using MindAttic.Tickets.Data;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T1684: a milestone's build and exists checks run on their own, harness and sign-off checks only on accept,
/// and a milestone is reached only when every check has passed.</summary>
public sealed class MilestoneAcceptanceTests
{
    private const string Head = "abcdef1234567890";

    private readonly TempDirs temps = new();
    private string dir = "";
    private string feed = "";
    private JsonStore store = null!;
    private readonly List<string> built = [];
    private int catFile;
    private string? held;

    [SetUp]
    public void SetUp()
    {
        dir = temps.New();
        feed = Path.Combine(dir, "feed");
        Directory.CreateDirectory(feed);
        Directory.CreateDirectory(Path.Combine(dir, "A"));
        store = new JsonStore(Path.Combine(dir, "store"));
        built.Clear();
        catFile = 0;
        held = null;
    }

    [TearDown]
    public void TearDown()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        temps.DeleteAll();
    }

    private MilestoneAcceptance Make(Func<string, BranchBuilds.BuildResult>? build = null) => new(store)
    {
        Git = (path, args) => args[0] switch
        {
            "rev-parse" => (0, Head + "\n"),
            "ls-tree" => (0, "A.slnx\nsrc\n"),
            "cat-file" => (catFile, ""),
            _ => (0, ""),
        },
        MainlineOf = _ => "main",
        RunBuild = s => { built.Add(s); return build?.Invoke(s) ?? new BranchBuilds.BuildResult(true); },
        RepoPath = name => name == "A" ? Path.Combine(dir, "A") : null,
        WorkspaceRoot = dir,
        WorktreeRoot = Path.Combine(dir, "wt"),
        Feed = feed,
        Held = () => held,
    };

    private static MilestoneCheck Check(string kind, string? target = null, params string[] repos) =>
        new("MS-1", 1, kind, "a check", repos, target, MilestoneCheckResults.Unknown, null, null);

    private TicketsDb Tickets()
    {
        var repo = Path.Combine(dir, "A");
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        Directory.CreateDirectory(Path.Combine(repo, "docs"));
        File.WriteAllText(Path.Combine(repo, "docs", "USER_STORIES.md"),
            "---\ncode: A\n---\n\n# A\n\n## Tickets\n\n| ID | Date | Type | Status | Summary | Ref |\n|---|---|---|---|---|---|\n" +
            "| A-T1 | 2026-10-01 | fix | open | one | — |\n");
        var db = TicketsDb.Open(TicketsDb.DefaultPath(dir));
        db.Import([new MindAttic.Tickets.Repo("A", repo)]);
        return db;
    }

    [Test]
    public void A_target_of_two_words_ending_in_a_version_is_a_package_and_anything_else_is_a_path()
    {
        Assert.That(MilestoneAcceptance.ParseTarget("MindAttic.Tickets 6.0.0"), Is.EqualTo(new MilestoneAcceptance.Target("MindAttic.Tickets", "6.0.0", null)));
        Assert.That(MilestoneAcceptance.ParseTarget("  \"MindAttic.Vault 1.0.0-beta\" "), Is.EqualTo(new MilestoneAcceptance.Target("MindAttic.Vault", "1.0.0-beta", null)));
        Assert.That(MilestoneAcceptance.ParseTarget(@"docs\README.md"), Is.EqualTo(new MilestoneAcceptance.Target(null, null, @"docs\README.md")));
        Assert.That(MilestoneAcceptance.ParseTarget("docs/My File.md").IsPackage, Is.False, "the second word isn't a version");
        Assert.That(MilestoneAcceptance.ParseTarget("docs/release 2.md").IsPackage, Is.False, "the first word is a path");
        Assert.That(MilestoneAcceptance.ParseTarget(@"C:\LocalNuGet\X.1.0.0.nupkg").Path, Is.EqualTo(@"C:\LocalNuGet\X.1.0.0.nupkg"));
    }

    [Test]
    public void A_package_exists_when_its_nupkg_is_in_the_feed()
    {
        var checks = Make();
        var missing = checks.Evaluate(Check(MilestoneCheckKinds.Exists, "MindAttic.Thing 1.2.3"));
        Assert.That(missing.Result, Is.EqualTo(MilestoneCheckResults.Fail));
        Assert.That(missing.Evidence, Does.Contain(Path.Combine(feed, "MindAttic.Thing.1.2.3.nupkg")), "the evidence says where it was looked for");

        File.WriteAllText(Path.Combine(feed, "MindAttic.Thing.1.2.3.nupkg"), "");
        Assert.That(checks.Evaluate(Check(MilestoneCheckKinds.Exists, "MindAttic.Thing 1.2.3")).Result, Is.EqualTo(MilestoneCheckResults.Pass));
    }

    [Test]
    public void An_absolute_path_and_a_path_with_no_repo_are_looked_for_on_disk()
    {
        var checks = Make();
        var file = Path.Combine(dir, "out", "app.exe");
        Assert.That(checks.Evaluate(Check(MilestoneCheckKinds.Exists, file)).Result, Is.EqualTo(MilestoneCheckResults.Fail));
        Assert.That(checks.Evaluate(Check(MilestoneCheckKinds.Exists, @"out\app.exe")).Result, Is.EqualTo(MilestoneCheckResults.Fail), "relative to the workspace root");

        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "");
        Assert.That(checks.Evaluate(Check(MilestoneCheckKinds.Exists, file)).Result, Is.EqualTo(MilestoneCheckResults.Pass));
        Assert.That(checks.Evaluate(Check(MilestoneCheckKinds.Exists, @"out\app.exe")).Result, Is.EqualTo(MilestoneCheckResults.Pass));
        Assert.That(checks.Evaluate(Check(MilestoneCheckKinds.Exists, "out")).Result, Is.EqualTo(MilestoneCheckResults.Pass), "a folder counts");
    }

    [Test]
    public void A_path_in_a_repo_is_looked_for_on_its_main_line()
    {
        var checks = Make();
        var there = checks.Evaluate(Check(MilestoneCheckKinds.Exists, @"docs\GUIDE.md", "A"));
        Assert.That(there.Result, Is.EqualTo(MilestoneCheckResults.Pass));
        Assert.That(there.Evidence, Is.EqualTo("docs/GUIDE.md is in A on main at abcdef1"));

        catFile = 128;
        Assert.That(checks.Evaluate(Check(MilestoneCheckKinds.Exists, "docs/GUIDE.md", "A")).Result, Is.EqualTo(MilestoneCheckResults.Fail));

        catFile = -1;
        Assert.That(checks.Evaluate(Check(MilestoneCheckKinds.Exists, "docs/GUIDE.md", "A")).Result, Is.Null, "git not answering isn't a missing file");
        Assert.That(checks.Evaluate(Check(MilestoneCheckKinds.Exists, "docs/GUIDE.md", "B")).Result, Is.Null, "no checkout of B here");
        Assert.That(checks.Evaluate(Check(MilestoneCheckKinds.Exists, @"..\secret.txt", "A")).Result, Is.Null, "not a path inside the repo");
    }

    [Test]
    public void A_build_check_builds_the_main_line_in_its_own_worktree_and_names_the_commit()
    {
        var checks = Make();
        var passed = checks.Evaluate(Check(MilestoneCheckKinds.Build, null, "A"));
        Assert.That(passed.Result, Is.EqualTo(MilestoneCheckResults.Pass));
        Assert.That(passed.Evidence, Is.EqualTo("A on main at abcdef1 builds"));
        Assert.That(built, Is.EqualTo(new[] { Path.Combine(dir, "wt", "A", MilestoneAcceptance.WorktreeName, "A.slnx") }));

        Assert.That(Make().Evaluate(Check(MilestoneCheckKinds.Build, null, "A")).Result, Is.EqualTo(MilestoneCheckResults.Pass));
        Assert.That(built, Has.Count.EqualTo(1), "the build made at that commit is the evidence; it isn't built again");
    }

    [Test]
    public void A_main_line_suite_that_passed_at_the_commit_is_the_evidence()
    {
        store.Save(MainlineTests.StateName, new Dictionary<string, MainlineTests.RepoState> { ["A"] = new() { Tested = Head, Green = Head } });
        var passed = Make().Evaluate(Check(MilestoneCheckKinds.Build, null, "A"));
        Assert.That(passed.Result, Is.EqualTo(MilestoneCheckResults.Pass));
        Assert.That(passed.Evidence, Does.Contain("its main-line suite passed there"));
        Assert.That(built, Is.Empty);
    }

    [Test]
    public void A_compiler_error_fails_the_check()
    {
        var failed = Make(_ => new BranchBuilds.BuildResult(false, "Foo.cs(1,1): error CS0103: no such name")).Evaluate(Check(MilestoneCheckKinds.Build, null, "A"));
        Assert.That(failed.Result, Is.EqualTo(MilestoneCheckResults.Fail));
        Assert.That(failed.Evidence, Does.StartWith("A on main at abcdef1 fails to build: ").And.Contain("CS0103"));
    }

    [Test]
    public void A_build_that_could_not_run_never_fails_the_check()
    {
        held = "RAM is low";
        var heldBack = Make().Evaluate(Check(MilestoneCheckKinds.Build, null, "A"));
        Assert.That(heldBack.Result, Is.Null);
        Assert.That(heldBack.Evidence, Does.Contain("RAM is low"));
        Assert.That(built, Is.Empty, "nothing is built while the machine is held");

        held = null;
        Assert.That(Make(_ => new BranchBuilds.BuildResult(false, "it ran past 30 minutes and was stopped")).Evaluate(Check(MilestoneCheckKinds.Build, null, "A")).Result, Is.Null);
        Assert.That(Make(_ => new BranchBuilds.BuildResult(false, "couldn't run dotnet: not found")).Evaluate(Check(MilestoneCheckKinds.Build, null, "A")).Result, Is.Null);
        Assert.That(Make(_ => new BranchBuilds.BuildResult(false, "x.csproj : error MSB3027: Could not copy the file")).Evaluate(Check(MilestoneCheckKinds.Build, null, "A")).Result, Is.Null,
            "a file another process held is the machine, not main");
        Assert.That(Make().Evaluate(Check(MilestoneCheckKinds.Build, null, "B")).Result, Is.Null, "no checkout of B here");
        Assert.That(built, Has.Count.EqualTo(3), "a build that didn't complete is not kept as that commit's result");
    }

    [Test]
    public void Only_an_error_about_the_code_completes_a_failed_build()
    {
        Assert.That(MilestoneAcceptance.Completed(new BranchBuilds.BuildResult(true)), Is.True);
        Assert.That(MilestoneAcceptance.Completed(new BranchBuilds.BuildResult(false, "a.cs(2,3): error CS1002: ; expected")), Is.True);
        Assert.That(MilestoneAcceptance.Completed(new BranchBuilds.BuildResult(false, null)), Is.False);
        Assert.That(MilestoneAcceptance.Completed(new BranchBuilds.BuildResult(false, "it ran past 30 minutes and was stopped")), Is.False);
    }

    [Test]
    public void Harness_and_signoff_checks_never_run_on_their_own()
    {
        var checks = Make();
        Assert.That(checks.Evaluate(Check(MilestoneCheckKinds.Harness, "main menu")).Result, Is.Null);
        Assert.That(checks.Evaluate(Check(MilestoneCheckKinds.Signoff)).Result, Is.Null);
        Assert.That(checks.Evaluate(Check(MilestoneCheckKinds.Signoff), accept: true).Result, Is.Null, "a sign-off needs a name");

        var harness = checks.Evaluate(Check(MilestoneCheckKinds.Harness, "main menu"), accept: true, by: "Ryan");
        Assert.That((harness.Result, harness.Evidence), Is.EqualTo((MilestoneCheckResults.Unknown, MilestoneAcceptance.NoHarnessRunner)));
        var signoff = checks.Evaluate(Check(MilestoneCheckKinds.Signoff), accept: true, by: " Ryan ");
        Assert.That((signoff.Result, signoff.Evidence), Is.EqualTo((MilestoneCheckResults.Pass, "signed off by Ryan")));
    }

    [Test]
    public void A_run_records_a_result_once_and_leaves_what_it_does_not_run()
    {
        using var db = Tickets();
        var m = db.ActivateMilestone(db.AddMilestone("First playable", "It runs", ["A"]).Id);
        db.AddMilestoneCheck(m.Id, MilestoneCheckKinds.Exists, "the build output is there", null, @"out\app.exe");
        db.AddMilestoneCheck(m.Id, MilestoneCheckKinds.Signoff, "the owner signs off");
        var checks = Make();

        var first = checks.Run(db, m);
        Assert.That(first.Select(c => (c.Check.Result, c.Recorded)), Is.EqualTo(new[] { (MilestoneCheckResults.Fail, true), (MilestoneCheckResults.Unknown, false) }));
        Assert.That(checks.Run(db, m).Any(c => c.Recorded), Is.False, "the same result and evidence isn't written again");

        var lines = checks.RunDue(() => TicketsDb.Open(TicketsDb.DefaultPath(dir)));
        Assert.That(lines, Is.Empty, "nothing changed, so the daemon says nothing");
        Assert.That(checks.RunDue(() => null), Is.Empty, "no ticket database");
        Assert.That(checks.RunDue(() => throw new IOException("locked")), Is.Empty, "a database that can't be read");
    }

    [Test]
    public void Accept_reaches_the_milestone_only_when_every_check_passes()
    {
        using var db = Tickets();
        var m = db.ActivateMilestone(db.AddMilestone("First playable", "It runs", ["A"]).Id);
        db.AddMilestoneCheck(m.Id, MilestoneCheckKinds.Build, "A builds on main", ["A"]);
        db.AddMilestoneCheck(m.Id, MilestoneCheckKinds.Exists, "the build output is there", null, @"out\app.exe");
        db.AddMilestoneCheck(m.Id, MilestoneCheckKinds.Signoff, "the owner signs off");
        var checks = Make();

        var held = checks.Accept(db, m.Id, "Ryan");
        Assert.That(held.Reached, Is.False);
        Assert.That(held.Unmet.Select(c => c.Check.Number), Is.EqualTo(new long[] { 2 }));
        Assert.That(db.Milestone(m.Id)!.State, Is.EqualTo(MilestoneStates.Accepting), "it stays in acceptance");
        Assert.That(db.MilestoneChecks(m.Id).Single(c => c.Kind == MilestoneCheckKinds.Signoff).Evidence, Is.EqualTo("signed off by Ryan"));

        Directory.CreateDirectory(Path.Combine(dir, "out"));
        File.WriteAllText(Path.Combine(dir, "out", "app.exe"), "");
        var reached = checks.Accept(db, m.Id, "Ryan");
        Assert.That(reached.Reached, Is.True);
        Assert.That(db.Milestone(m.Id)!.State, Is.EqualTo(MilestoneStates.Reached));
        Assert.That(db.Milestone(m.Id)!.ReachedBy, Is.EqualTo("Ryan"));
        Assert.Throws<InvalidOperationException>(() => checks.Accept(db, m.Id, "Ryan"), "a reached milestone isn't accepted again");
    }

    [Test]
    public void A_harness_check_keeps_a_milestone_in_acceptance_while_nothing_can_run_one()
    {
        using var db = Tickets();
        var m = db.ActivateMilestone(db.AddMilestone("Soaked", "It survives a soak", ["A"]).Id);
        db.AddMilestoneCheck(m.Id, MilestoneCheckKinds.Harness, "a run reaches the main menu", ["A"], "main menu");

        var result = Make().Accept(db, m.Id, "Ryan");
        Assert.That(result.Reached, Is.False);
        var harness = db.MilestoneChecks(m.Id).Single();
        Assert.That((harness.Result, harness.Evidence), Is.EqualTo((MilestoneCheckResults.Unknown, MilestoneAcceptance.NoHarnessRunner)));
        Assert.Throws<ArgumentException>(() => Make().Accept(db, m.Id, " "), "accepting needs a name");
    }
}
