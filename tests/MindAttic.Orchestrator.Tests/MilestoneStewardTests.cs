using MindAttic.Orchestrator.Scheduling;
using MindAttic.Tickets.Data;
using MindAttic.Tickets.Log;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T1686: the milestone steward proposes one ticket for each failed build or exists check nothing open covers,
/// reopens only a closed milestone ticket a later open ticket says came back, reports the rest, and does nothing twice.</summary>
public sealed class MilestoneStewardTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);

    private readonly TempDirs temps = new();

    [TearDown]
    public void TearDown()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        temps.DeleteAll();
    }

    private static Milestone Ms(string id = "MS-1", string state = MilestoneStates.Active, params string[] repos) =>
        new(id, $"name of {id}", "a goal", repos.Length == 0 ? ["A"] : repos, 1, state, "2026-11-01T00:00:00Z", "2026-11-01T00:00:00Z", "2026-11-02T00:00:00Z", null, null);

    private static MilestoneCheck Check(long number, string kind, string result, string? evidence = null, string? target = null, params string[] repos) =>
        new("MS-1", number, kind, $"check {number}", repos, target, result, evidence is null ? null : "2026-11-10T00:00:00Z", evidence);

    private static MilestoneCheck BuildFails(long number = 1, params string[] repos) =>
        Check(number, MilestoneCheckKinds.Build, MilestoneCheckResults.Fail, "A on main at abc1234 fails to build: X.cs(1,1): error CS0103", null, repos.Length == 0 ? ["A"] : repos);

    private static Dictionary<string, string> Statuses(params (string Id, string Status)[] tickets) =>
        tickets.ToDictionary(t => t.Id, t => t.Status, StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, IReadOnlyList<string>> Labels(params (string Id, string Label)[] labels) =>
        labels.GroupBy(l => l.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)[.. g.Select(l => l.Label)], StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<StewardAction> Plan(Milestone m, MilestoneCheck[] checks, Dictionary<string, string>? statuses = null,
        Dictionary<string, IReadOnlyList<string>>? labels = null, string[]? members = null, params HygieneFinding[] findings) =>
        MilestoneSteward.Plan([new MilestoneSteward.Scope(m, checks, members ?? [])], statuses ?? Statuses(), labels ?? Labels(), findings);

    // ── Checks ───────────────────────────────────────────────────────────

    [Test]
    public void AFailedCheckWithNoTicket_ProposesOne_InTheChecksFirstRepo_StampedWithItsMarker()
    {
        var action = Plan(Ms(), [BuildFails(2, "B", "A")]).Single();

        Assert.Multiple(() =>
        {
            Assert.That(action.Kind, Is.EqualTo(StewardActionKind.Propose));
            Assert.That(action.MilestoneId, Is.EqualTo("MS-1"));
            Assert.That(action.Check, Is.EqualTo(2));
            Assert.That(action.Repo, Is.EqualTo("B"));
            Assert.That(action.Marker, Is.EqualTo("milestone-check:ms-1/2"));
            Assert.That(TicketsDb.NormalizeLabel(action.Marker), Is.EqualTo(action.Marker), "the marker is a label the store takes as it is");
            Assert.That(action.Summary, Does.StartWith("MS-1 check #2 (build) fails: check 2"));
            Assert.That(action.Description, Does.Contain("error CS0103"));
            Assert.That(action.Description, Does.Contain("MS-1 (name of MS-1)"));
            Assert.That(action.Description, Does.Contain("also names A"));
        });
    }

    [Test]
    public void AFailedCheckWithAnOpenStampedTicket_ProposesNothing()
    {
        var plan = Plan(Ms(), [BuildFails()], Statuses(("A-T9", TicketRules.Open)),
            Labels(("A-T9", MilestoneSteward.Label), ("A-T9", MilestoneSteward.Marker("MS-1", 1))));

        Assert.That(plan, Is.Empty);
    }

    [Test]
    public void AnOpenTicketStampedForAnotherCheck_DoesNotCoverThisOne()
    {
        var plan = Plan(Ms(), [BuildFails(1)], Statuses(("A-T9", TicketRules.Open)), Labels(("A-T9", MilestoneSteward.Marker("MS-1", 11)), ("A-T9", MilestoneSteward.Marker("MS-2", 1))));

        Assert.That(plan.Single().Kind, Is.EqualTo(StewardActionKind.Propose));
    }

    [Test]
    public void ACheckNotYetEvaluated_OrPassed_ProposesNothing()
    {
        var plan = Plan(Ms(),
        [
            Check(1, MilestoneCheckKinds.Build, MilestoneCheckResults.Unknown, null, null, "A"),
            Check(2, MilestoneCheckKinds.Exists, MilestoneCheckResults.Unknown, null, "docs/README.md", "A"),
            Check(3, MilestoneCheckKinds.Exists, MilestoneCheckResults.Pass, "docs/README.md is in A on main at abc1234", "docs/README.md", "A"),
        ]);

        Assert.That(plan, Is.Empty);
    }

    [Test]
    public void AnUnknownCheckWithEvidenceRecorded_IsProposedAsAChore()
    {
        var action = Plan(Ms(), [Check(1, MilestoneCheckKinds.Build, MilestoneCheckResults.Unknown, "there's no checkout of A here", null, "A")]).Single();

        Assert.Multiple(() =>
        {
            Assert.That(action.Kind, Is.EqualTo(StewardActionKind.Propose));
            Assert.That(action.Type, Is.EqualTo("chore"));
            Assert.That(action.Summary, Does.Contain("has no result"));
        });
    }

    [Test]
    public void HarnessAndSignoffChecks_NeverProposeAnything()
    {
        var plan = Plan(Ms(),
        [
            Check(1, MilestoneCheckKinds.Harness, MilestoneCheckResults.Fail, "the run stopped at wave 3", "wave 10"),
            Check(2, MilestoneCheckKinds.Harness, MilestoneCheckResults.Unknown, MilestoneAcceptance.NoHarnessRunner, "wave 10"),
            Check(3, MilestoneCheckKinds.Signoff, MilestoneCheckResults.Fail, "the owner said no"),
            Check(4, MilestoneCheckKinds.Signoff, MilestoneCheckResults.Unknown),
        ]);

        Assert.That(plan, Is.Empty);
    }

    [Test]
    public void ABuildFailureIsAFix_AndAMissingArtifactIsAFeatureOrChoreLeftForTheOwner()
    {
        var plan = Plan(Ms(),
        [
            BuildFails(1),
            Check(2, MilestoneCheckKinds.Exists, MilestoneCheckResults.Fail, "docs/GUIDE.md isn't in A on main at abc1234", "docs/GUIDE.md", "A"),
            Check(3, MilestoneCheckKinds.Exists, MilestoneCheckResults.Fail, "package MindAttic.Thing 2.0.0 isn't in the local feed", "MindAttic.Thing 2.0.0"),
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Select(a => a.Kind), Is.All.EqualTo(StewardActionKind.Propose));
            Assert.That(plan.Select(a => a.Type), Is.EqualTo(new[] { "fix", "feature", "chore" }));
            Assert.That(plan[2].Repo, Is.EqualTo("A"), "a check that names no repo is filed in the milestone's first");
            Assert.That(plan[0].Description, Does.Contain("is a bug"));
            Assert.That(plan[1].Description, Does.Contain("not worked until the owner approves it"));
            Assert.That(plan[2].Description, Does.Contain("not worked until the owner approves it"));
        });
    }

    [Test]
    public void ACheckWhoseOwnTicketWasClosedOrDeclined_IsReported_NotProposedAgain()
    {
        var plan = Plan(Ms(), [BuildFails(1), BuildFails(2)],
            Statuses(("A-T8", TicketRules.Closed), ("A-T9", TicketRules.WontDo)),
            Labels(("A-T8", MilestoneSteward.Marker("MS-1", 1)), ("A-T9", MilestoneSteward.Marker("MS-1", 2))));

        Assert.Multiple(() =>
        {
            Assert.That(plan.Select(a => a.Kind), Is.All.EqualTo(StewardActionKind.Report));
            Assert.That(plan.Select(a => a.TicketId), Is.EqualTo(new[] { "A-T8", "A-T9" }));
            Assert.That(plan[0].Text, Does.Contain("is closed, but the check still fails"));
            Assert.That(plan[1].Text, Does.Contain("is declined, but the check still fails"));
        });
    }

    [Test]
    public void AFailedCheckWithNoRepoAnywhere_IsReported()
    {
        var m = Ms() with { Repos = [] };
        var action = Plan(m, [Check(1, MilestoneCheckKinds.Exists, MilestoneCheckResults.Fail, @"C:\out\thing.zip doesn't exist", @"C:\out\thing.zip")]).Single();

        Assert.That(action.Kind, Is.EqualTo(StewardActionKind.Report));
        Assert.That(action.Text, Does.Contain("names a repo"));
    }

    [Test]
    public void AMilestoneThatIsNotActiveOrAccepting_GetsNothing()
    {
        var finding = new HygieneFinding("A", "A-T1", "one", "closed with no Ref: no test or commit cited");
        foreach (var state in new[] { MilestoneStates.Planned, MilestoneStates.Reached, MilestoneStates.Dropped })
            Assert.That(Plan(Ms(state: state), [BuildFails()], Statuses(("A-T1", TicketRules.Closed)), null, ["A-T1"], finding), Is.Empty, state);
        Assert.That(Plan(Ms(state: MilestoneStates.Accepting), [BuildFails()]), Has.Count.EqualTo(1));
    }

    // ── Hygiene ──────────────────────────────────────────────────────────

    [Test]
    public void AClosedMilestoneTicketALaterOpenTicketSaysCameBack_IsReopened_AndOneWithNoRefIsOnlyReported()
    {
        var plan = Plan(Ms(), [],
            Statuses(("A-T1", TicketRules.Closed), ("A-T2", TicketRules.Closed), ("A-T7", TicketRules.Open)), null, ["A-T1", "A-T2", "A-T7"],
            new HygieneFinding("A", "A-T1", "the list scrolls", "A-T7 says it came back: \"A-T1 is broken again\""),
            new HygieneFinding("A", "A-T2", "the list sorts", "closed with no Ref: no test or commit cited"));

        Assert.Multiple(() =>
        {
            Assert.That(plan.Select(a => a.Kind), Is.EqualTo(new[] { StewardActionKind.Reopen, StewardActionKind.Report }));
            Assert.That(plan[0].TicketId, Is.EqualTo("A-T1"));
            Assert.That(plan[0].Because, Is.EqualTo("A-T7"));
            Assert.That(plan[1].TicketId, Is.EqualTo("A-T2"));
            Assert.That(plan[1].Text, Does.Contain("closed without evidence"));
        });
    }

    [Test]
    public void ATicketThatCameBack_ButWhoseLaterTicketIsClosedToo_IsOnlyReported()
    {
        var action = Plan(Ms(), [], Statuses(("A-T1", TicketRules.Closed), ("A-T7", TicketRules.Closed)), null, ["A-T1"],
            new HygieneFinding("A", "A-T1", "the list scrolls", "A-T7 says it came back: \"A-T1 is broken again\"")).Single();

        Assert.That(action.Kind, Is.EqualTo(StewardActionKind.Report));
        Assert.That(action.Because, Is.EqualTo("A-T7"));
    }

    [Test]
    public void HygieneFindings_ForTicketsOffTheMilestone_OrNotClosed_AreIgnored()
    {
        var plan = Plan(Ms(), [], Statuses(("A-T1", TicketRules.Closed), ("A-T2", TicketRules.Open), ("A-T7", TicketRules.Open)), null, ["A-T2"],
            new HygieneFinding("A", "A-T1", "off the milestone", "A-T7 says it came back: \"A-T1 again\""),
            new HygieneFinding("A", "A-T2", "an open feature", "closed with no Ref: no test or commit cited"));

        Assert.That(plan, Is.Empty);
    }

    [Test]
    public void ATicketOnTwoMilestones_IsReopenedOnce()
    {
        var statuses = Statuses(("A-T1", TicketRules.Closed), ("A-T7", TicketRules.Open));
        HygieneFinding[] findings = [new("A", "A-T1", "the list scrolls", "A-T7 says it came back: \"A-T1 again\"")];
        var plan = MilestoneSteward.Plan(
            [new MilestoneSteward.Scope(Ms("MS-1"), [], ["A-T1"]), new MilestoneSteward.Scope(Ms("MS-2"), [], ["A-T1"])], statuses, Labels(), findings);

        Assert.That(plan.Count(a => a.Kind == StewardActionKind.Reopen), Is.EqualTo(1));
    }

    // ── Twice ────────────────────────────────────────────────────────────

    [Test]
    public void ASecondPlan_AfterTheFirstWasFiled_ProposesNothing()
    {
        MilestoneCheck[] checks = [BuildFails(1), Check(2, MilestoneCheckKinds.Exists, MilestoneCheckResults.Fail, "docs/GUIDE.md isn't in A", "docs/GUIDE.md", "A")];
        var first = Plan(Ms(), checks);
        var statuses = Statuses();
        var labels = new List<(string, string)>();
        foreach (var (action, i) in first.Select((a, i) => (a, i)))
        {
            statuses[$"A-T{i + 20}"] = TicketRules.Open;
            labels.Add(($"A-T{i + 20}", MilestoneSteward.Label));
            labels.Add(($"A-T{i + 20}", action.Marker!));
        }

        Assert.That(first, Has.Count.EqualTo(2));
        Assert.That(Plan(Ms(), checks, statuses, Labels([.. labels])), Is.Empty);
    }

    [Test]
    public void Applied_AProposalIsAnOpenStampedTicketWaitingToJoinTheMilestone_AndASecondRunFilesNothing()
    {
        var dir = temps.New();
        var repo = Path.Combine(dir, "A");
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        Directory.CreateDirectory(Path.Combine(repo, "docs"));
        File.WriteAllText(Path.Combine(repo, "docs", "USER_STORIES.md"),
            "---\ncode: A\n---\n\n# A\n\n## Tickets\n\n| ID | Date | Type | Status | Summary | Ref |\n|---|---|---|---|---|---|\n" +
            "| A-T1 | 2026-10-01 | fix | open | one | — |\n");
        using var db = TicketsDb.Open(TicketsDb.DefaultPath(dir));
        db.Import([new MindAttic.Tickets.Repo("A", repo)]);
        var m = db.AddMilestone("First", "a goal", ["A"]);
        db.AddToMilestone(m.Id, ["A-T1"]);
        db.AddMilestoneCheck(m.Id, MilestoneCheckKinds.Build, "A builds", ["A"]);
        db.AddMilestoneCheck(m.Id, MilestoneCheckKinds.Exists, "the guide is written", ["A"], "docs/GUIDE.md");
        db.AddMilestoneCheck(m.Id, MilestoneCheckKinds.Signoff, "the owner says so");
        db.ActivateMilestone(m.Id);
        db.SetMilestoneCheckResult(m.Id, 1, MilestoneCheckResults.Fail, "A on main at abc1234 fails to build: X.cs(1,1): error CS0103");
        db.SetMilestoneCheckResult(m.Id, 2, MilestoneCheckResults.Fail, "docs/GUIDE.md isn't in A on main at abc1234");

        var first = MilestoneStewards.Apply(db, MilestoneStewards.Read(db, MilestoneStewards.Locked(db)));
        var filed = first.Select(o => o.Filed!).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(first.Select(o => o.Applied), Is.EqualTo(new[] { true, true }));
            Assert.That(filed.Select(id => db.Ticket(id)!.Value.Ticket.Type), Is.EqualTo(new[] { "fix", "feature" }));
            Assert.That(filed.Select(id => db.Ticket(id)!.Value.Ticket.Status), Is.All.EqualTo(TicketRules.Open));
            Assert.That(filed.Select(id => db.QueueState(id).Approval), Is.All.EqualTo(TicketsDb.Proposed), "the steward approves nothing");
            Assert.That(db.Labels(filed[0]), Is.EquivalentTo(new[] { MilestoneSteward.Label, MilestoneSteward.Marker(m.Id, 1) }));
            Assert.That(db.MilestonePending(m.Id).Select(p => p.Item), Is.EquivalentTo(filed), "the scope lock keeps each waiting for the owner");
            Assert.That(db.MilestonePending(m.Id).Select(p => p.By), Is.All.EqualTo(MilestoneSteward.By));
            Assert.That(db.MilestoneMembers(m.Id).Select(x => x.Id), Is.EqualTo(new[] { "A-T1" }));
        });
        Assert.That(MilestoneStewards.Read(db, MilestoneStewards.Locked(db)), Is.Empty, "run again, it has nothing to do");
    }

    // ── The daemon's cadence ─────────────────────────────────────────────

    [Test]
    public void AMilestoneIsDue_WhenNeverStewarded_WhenItsStateChanged_OrADayOn()
    {
        var active = Ms("MS-1");
        var accepting = Ms("MS-2", MilestoneStates.Accepting);
        var state = new Dictionary<string, MilestoneStewards.Ran>
        {
            ["MS-1"] = new() { State = MilestoneStates.Active, AtUtc = Now.AddHours(-23) },
            ["MS-2"] = new() { State = MilestoneStates.Active, AtUtc = Now.AddHours(-1) },
        };

        Assert.Multiple(() =>
        {
            Assert.That(MilestoneStewards.Due([active, accepting], new Dictionary<string, MilestoneStewards.Ran>(), Now).Select(m => m.Id), Is.EqualTo(new[] { "MS-1", "MS-2" }));
            Assert.That(MilestoneStewards.Due([active, accepting], state, Now).Select(m => m.Id), Is.EqualTo(new[] { "MS-2" }), "its state changed");
            Assert.That(MilestoneStewards.Due([active], state, Now.AddHours(1)).Select(m => m.Id), Is.EqualTo(new[] { "MS-1" }), "a day on");
        });
    }
}
