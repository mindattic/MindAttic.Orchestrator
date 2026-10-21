using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using MindAttic.Tickets.Log;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T563: next --claim picks each ticket's model, a Sonnet ticket's big or risky diff gets one Opus review before it
/// lands, and a ticket's cost is kept by model so orchestrator report compares cost per completed ticket.</summary>
public sealed class ModelChoiceTests
{
    private static readonly DateTime Now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
    private readonly TempDirs temps = new();
    private JsonStore store = null!;

    [SetUp]
    public void SetUp() => store = new JsonStore(Path.Combine(temps.New(), "store"));

    [TearDown]
    public void TearDown() => temps.DeleteAll();

    private static Ticket Tk(string id, string type, string summary) => new(id, "2026-10-01", type, "open", summary, "");

    [Test]
    public void Fixes_failed_tests_and_tight_changes_go_to_sonnet_features_design_and_wide_changes_to_opus()
    {
        var s = new OrchestratorSettings();
        string M(string type, string summary) => ModelChoice.For(Tk("A-T1", type, summary), "A", s).Model;

        Assert.That(M("fix", "next --claim crashes on an empty queue"), Is.EqualTo(ModelChoice.Sonnet));
        Assert.That(M("chore", "rename Foo to Bar in Baz.cs"), Is.EqualTo(ModelChoice.Sonnet));
        Assert.That(M("docs", "README: document --review-lines"), Is.EqualTo(ModelChoice.Sonnet));
        Assert.That(M("feature", "show the model on the agent board"), Is.EqualTo(ModelChoice.Opus));
        Assert.That(M("feature", "AutopilotTests.Foo is a failing test since T500"), Is.EqualTo(ModelChoice.Sonnet), "a failed test is Sonnet's whatever the type");
        Assert.That(M("chore", "redesign how claims are stored"), Is.EqualTo(ModelChoice.Opus), "design work");
        Assert.That(M("fix", "rename the setting across many files"), Is.EqualTo(ModelChoice.Opus), "a change across many files");
        Assert.That(M("fix", "the db migration loses rows"), Is.EqualTo(ModelChoice.Opus), "must be right the first time");
        Assert.That(M("epic", "anything"), Is.EqualTo(ModelChoice.Opus), "a type it doesn't know isn't taken for a tight change");
        Assert.That(ModelChoice.For(Tk("A-T1", "fix", "x"), "A", s).Why, Is.EqualTo("a fix"));
    }

    [Test]
    public void An_override_for_the_ticket_wins_over_its_repos_and_both_win_over_the_pick()
    {
        var s = new OrchestratorSettings { ModelOverrides = new() { ["Prose"] = "opus", ["GTFO.*"] = "Sonnet 5.5", ["P-T7"] = "claude-sonnet-5-5", ["Q-T1"] = "auto" } };
        Assert.That(ModelChoice.For(Tk("P-T1", "fix", "x"), "prose", s), Is.EqualTo(new ModelChoice.Choice(ModelChoice.Opus, "your override for Prose")));
        Assert.That(ModelChoice.For(Tk("P-T7", "fix", "x"), "Prose", s).Model, Is.EqualTo(ModelChoice.Sonnet), "the ticket's own override wins over Prose=opus");
        Assert.That(ModelChoice.For(Tk("G-T1", "feature", "x"), "GTFO.Api", s).Model, Is.EqualTo(ModelChoice.Sonnet), "a wildcard names the repo");
        Assert.That(ModelChoice.For(Tk("Q-T1", "feature", "x"), "Q", s).Model, Is.EqualTo(ModelChoice.Opus), "auto is no override");
        Assert.That((ModelChoice.Parse("Opus 5.5"), ModelChoice.Parse("auto"), ModelChoice.Parse("gpt"), ModelChoice.Parse(null)),
            Is.EqualTo((ModelChoice.Opus, ModelChoice.Auto, (string?)null, (string?)null)));
        Assert.That((ModelChoice.Family("claude-opus-5-5"), ModelChoice.Family("claude-sonnet-5-5[1m]"), ModelChoice.Family("<synthetic>")),
            Is.EqualTo((ModelChoice.Opus, ModelChoice.Sonnet, (string?)null)));
    }

    [Test]
    public void Next_claim_prints_each_tickets_model_records_it_and_tells_only_a_sonnet_ticket_to_get_its_review()
    {
        var snap = new Snapshot(Now, [new RepoStatus("A", @"D:\W\A", RepoState.Dormant, [], [],
            [Tk("A-T1", "fix", "off by one in the pacer"), Tk("A-T2", "feature", "a new dashboard")], null, default)], [])
        {
            Approved = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "A-T2" },
        };
        var plan = Scheduler.Plan(snap, new(), store.LoadAssignments()) with
        {
            Pace = new Pace(PaceMode.Open, 6, 10_000, 0, 1_000, "5-hour", Now.AddHours(3), "On pace.") { SpareSessions = 4, SubagentAllowance = 4 },
        };
        var settings = new OrchestratorSettings { WorktreeRoot = @"D:\WT" };

        var (handed, why) = Autopilot.Next(plan, settings, store, ["A"], null, claim: true, Now, (_, path, _) => (true, path, ""), _ => "main");

        Assert.That(why, Is.Null);
        var byId = handed.ToDictionary(h => h.Item.Id);
        Assert.That((byId["A-T1"].Model!.Model, byId["A-T2"].Model!.Model), Is.EqualTo((ModelChoice.Sonnet, ModelChoice.Opus)));
        Assert.That(ModelChoice.Line(byId["A-T1"].Model!), Is.EqualTo("model: sonnet (claude-sonnet-5-5): a fix"));
        Assert.That(byId["A-T1"].Prompt, Does.Contain("orchestrator review A-T1").And.Contain("Work on ticket A-T1 (fix)"));
        Assert.That(byId["A-T1"].Prompt!.IndexOf("orchestrator review", StringComparison.Ordinal),
            Is.GreaterThan(byId["A-T1"].Prompt!.IndexOf("/commit", StringComparison.Ordinal)).And.LessThan(byId["A-T1"].Prompt!.IndexOf("git fetch origin", StringComparison.Ordinal)),
            "reviewed after its commit, before it lands");
        Assert.That(byId["A-T2"].Prompt, Does.Not.Contain("orchestrator review"), "an Opus ticket isn't reviewed");
        Assert.That(store.LoadAssignments().ToDictionary(a => a.TicketId, a => a.Model), Is.EqualTo(new Dictionary<string, string?> { ["A-T1"] = "sonnet", ["A-T2"] = "opus" }),
            "kept on the claim, through the database");
        Assert.That(Autopilot.StartAllLine(2), Does.Contain("on the model it names"));
        Assert.That(MindAttic.Orchestrator.Setup.AgentGate.AllocateCommand, Does.Contain("on the model its `model:` line names"),
            "/allocate starts each subagent on its ticket's model");
    }

    [Test]
    public void A_sonnet_diff_is_reviewed_when_big_or_risky_and_never_an_opus_one()
    {
        var s = new OrchestratorSettings { ReviewDiffLines = 100 };
        var small = DiffReview.ParseNumstat("10\t5\tsrc/A/Foo.cs\n-\t-\tassets/icon.png\n");
        Assert.That(small, Is.EqualTo(new[] { new DiffReview.FileChange("src/A/Foo.cs", 10, 5), new DiffReview.FileChange("assets/icon.png", 0, 0) }));
        Assert.That(DiffReview.Judge(small, s, ModelChoice.Sonnet).Review, Is.False);

        var big = DiffReview.ParseNumstat("90\t20\tsrc/A/Foo.cs\n");
        var verdict = DiffReview.Judge(big, s, ModelChoice.Sonnet);
        Assert.That((verdict.Review, verdict.Lines, verdict.Why), Is.EqualTo((true, 110, "it changes 110 lines, over the 100-line limit")));
        Assert.That(DiffReview.Judge(big, s, ModelChoice.Opus).Review, Is.False, "only Sonnet tickets are reviewed");
        Assert.That(DiffReview.Judge(big, s with { ReviewDiffLines = -1 }, ModelChoice.Sonnet).Review, Is.False, "-1: none for size");

        foreach (var risky in new[] { "src/MindAttic.Orchestrator.Core/Setup/AgentGate.cs", "src/MindAttic.Orchestrator.Core/Budget/Pacer.cs",
                     "src/MindAttic.Orchestrator.Core/Scheduling/Dispatcher.cs", "shared/hooks/commit-msg", "deploy.ps1",
                     "src/MindAttic.Orchestrator.Core/{Old.cs => Scheduling/Autopilot.cs}" })
            Assert.That(DiffReview.Judge([new(risky, 1, 0)], s, ModelChoice.Sonnet).Review, Is.True, risky);
        Assert.That(DiffReview.Judge([new("src/A/Formatting.cs", 1, 0)], s, ModelChoice.Sonnet).Review, Is.False);
        Assert.That(DiffReview.Judge([new("src/A/Pacer.cs", 1, 0)], s with { RiskyFiles = [] }, ModelChoice.Sonnet).Review, Is.False, "none set");
    }

    [Test]
    public void Orchestrator_review_runs_one_opus_review_of_the_diff_alone_and_records_it()
    {
        var tree = temps.New();
        store.UpdateAssignments(all => all.Add(new Assignment("A-T1", "A", tree, Providers.Claude, Now, true) { Model = "sonnet" }));
        var ticket = Tk("A-T1", "fix", "off by one in the pacer");
        var settings = new OrchestratorSettings { ReviewDiffLines = 100 };
        var gitCalls = new List<string>();
        (int, string) Git(string dir, string[] args)
        {
            gitCalls.Add(string.Join(' ', args));
            return args[0] switch
            {
                "rev-parse" => (0, tree + "\n"),
                "diff" when args[1] == "--numstat" => (0, "3\t1\tsrc/MindAttic.Orchestrator.Core/Budget/Pacer.cs\n"),
                "diff" => (0, "diff --git a/x b/x\n+the change\n"),
                _ => (1, "unexpected"),
            };
        }
        var prompts = new List<string>();
        (bool, string) Claude(string prompt) { prompts.Add(prompt); return (true, "Pacer.cs line 3: the guard is inverted; flip it."); }

        var dry = DiffReview.Review(store, settings, tree, "A-T1", ticket, "A", force: false, dryRun: true, Now, Git, Claude, _ => "main");
        Assert.That((dry.Reviewed, prompts.Count), Is.EqualTo((false, 0)));
        Assert.That(dry.Text, Does.Contain("gets its one Opus review").And.Contain("risky files"));

        var first = DiffReview.Review(store, settings, tree, "A-T1", ticket, "A", force: false, dryRun: false, Now, Git, Claude, _ => "main");
        Assert.That(first.Reviewed, Is.True);
        Assert.That(first.Text, Does.Contain("the guard is inverted").And.Contain("Don't run the review again"));
        Assert.That(prompts.Single(), Does.StartWith("Work on ticket A-T1 (fix):").And.Contain("+the change").And.Contain("no tools"),
            "the diff alone, under the ticket so its cost is the ticket's");
        Assert.That(gitCalls, Does.Contain("diff --numstat origin/main...HEAD").And.Contain("diff origin/main...HEAD"));
        Assert.That(store.LoadAssignments().Single().ReviewedUtc, Is.EqualTo(Now));

        var again = DiffReview.Review(store, settings, tree, "A-T1", ticket, "A", force: false, dryRun: false, Now, Git, Claude, _ => "main");
        Assert.That((again.Reviewed, prompts.Count), Is.EqualTo((false, 1)), "one review, not two");
        Assert.That(again.Text, Does.Contain("already had its one Opus review"));

        store.UpdateAssignments(all => all[0] = all[0] with { Model = "opus", ReviewedUtc = null });
        var opus = DiffReview.Review(store, settings, tree, "A-T1", ticket, "A", force: false, dryRun: false, Now, Git, Claude, _ => "main");
        Assert.That((opus.Reviewed, prompts.Count), Is.EqualTo((false, 1)));
        Assert.That(opus.Text, Does.Contain("No Opus review needed").And.Contain("only Sonnet tickets"));

        var failed = DiffReview.Review(store, settings, tree, "A-T1", ticket, "A", force: true, dryRun: false, Now, Git, _ => (false, "claude exited 1"), _ => "main");
        Assert.That(failed.Reviewed, Is.False);
        Assert.That(failed.Text, Does.Contain("couldn't run").And.Contain("Land it without one"));
        Assert.That(store.LoadAssignments().Single().ReviewedUtc, Is.Null, "a review that didn't run isn't recorded");
        Assert.That(DiffReview.CommandLine(@"C:\t\p.txt"), Does.Contain("--model claude-opus-5-5").And.Contain("--tools \"\""));
    }

    [Test]
    public void A_tickets_cost_is_kept_by_the_model_that_spent_most_on_it_and_the_report_compares_completed_ones()
    {
        var sub = new TicketedSubagent("agent1", "A-T1", "fix", @"D:\WT\A\A-T1", [new(Now, new TokenUsage(1000, 0, 0, 0))], "claude-sonnet-5-5");
        var session = new TranscriptSession("s1", @"D:\W\A", Now, default, "claude-opus-5-5")
        {
            Events = [.. sub.Events, new UsageEvent(Now.AddMinutes(1), new TokenUsage(50, 0, 0, 0))],
            TicketId = "A-T1",
            TicketType = "fix",
            TicketedSubagents = [sub],
        };
        var observed = TicketCosts.Observe([session], _ => "A").ToList();
        Assert.That(observed.Select(c => (c.SessionId, c.Model)), Is.EquivalentTo(new[] { ("s1/agent1", (string?)"sonnet"), ("s1", "opus") }));

        var costs = new TicketCosts([.. observed,
            new SessionCost("s2", "A-T2", "feature", "A", 4000, Now, Model: "opus"),
            new SessionCost("s3", "A-T3", "feature", "A", 2000, Now, Model: "opus"),
            new SessionCost("s4", "A-T4", "fix", "A", 700, Now, Model: null),
            new SessionCost("s5", "A-T5", "fix", "A", 9000, Now, Model: "sonnet")]);
        Assert.That(costs.Tickets.Single(t => t.TicketId == "A-T1").Model, Is.EqualTo("sonnet"), "the Opus review (50) leaves it a Sonnet ticket (1000)");
        Assert.That(ReportCommand.ModelLine(costs.Tickets, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "A-T5" }),
            Is.EqualTo($"By model (completed tickets): opus {Formatting.Tokens(3000)} per ticket over 2 tickets ({Formatting.Tokens(6000)} in all) · " +
                $"sonnet {Formatting.Tokens(1050)} per ticket over 1 ticket ({Formatting.Tokens(1050)} in all) · 1 more with no model known"),
            "A-T5 is still open, so not counted");

        // A session stored before costs kept their model learns it from its transcript, cost unchanged.
        var merged = TicketCosts.Merge([new SessionCost("s1", "A-T1", "fix", "A", 50, Now, OwnShare: true)],
            [new SessionCost("s1", "A-T1", "fix", "A", 50, Now, OwnShare: true, Model: "opus")], Now);
        Assert.That(merged.Single().Model, Is.EqualTo("opus"));
    }

    [Test]
    public void Costs_and_claims_keep_their_model_in_the_database()
    {
        var db = new OrchestratorDb(Path.Combine(temps.New(), "data", "orchestrator.db"));
        List<SessionCost> costs = [new("s1", "A-T1", "fix", "A", 500, Now, Model: "sonnet"), new("s2", "A-T2", null, "A", 60, Now, OwnShare: true)];
        db.SaveTicketCosts(costs);
        Assert.That(db.LoadTicketCosts(), Is.EqualTo(costs));
        Assert.That(File.ReadAllText(db.DumpPath), Does.Contain("'sonnet'"));

        store.UpdateAssignments(all => all.Add(new Assignment("A-T1", "A", @"D:\WT\A\A-T1", Providers.Claude, Now, true) { Model = "opus", ReviewedUtc = Now }));
        var kept = store.LoadAssignments().Single();
        Assert.That((kept.Model, kept.ReviewedUtc), Is.EqualTo(("opus", (DateTime?)Now)));
    }
}
