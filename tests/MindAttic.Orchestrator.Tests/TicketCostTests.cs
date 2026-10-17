using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using MindAttic.Workflow.Tickets;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class TicketCostTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void A_session_is_tied_to_its_ticket_by_the_dispatch_prompt_or_its_worktree()
    {
        var item = new QueueItem("Repo", @"D:\W\Repo", new Ticket("RP-T7", "2026-10-01", "fix", "open", "x", ""), 0, null, false);

        Assert.That(TicketAttribution.FromPrompt(Scheduler.PromptFor(item)), Is.EqualTo(("RP-T7", "fix")), "the prompt the scheduler writes is the one it reads back");
        Assert.That(TicketAttribution.FromWorktree(@"D:\Projects\MindAttic.worktrees\Repo\RP-T7\src"), Is.EqualTo("RP-T7"));
        Assert.That(TicketAttribution.FromWorktree(@"D:\Projects\MindAttic\Repo"), Is.Null);
    }

    [TestCase(@"d:\projects\mindattic.worktrees\repo\rp-t7\src", "RP-T7")]
    [TestCase(@"d:/projects/mindattic.worktrees/mindattic.orchestrator/m.orch-t150", "M.ORCH-T150")]
    [TestCase(@"D:\Projects\MindAttic.worktrees\Repo\Rp-T7", "RP-T7")]
    [TestCase(null, null)]
    [TestCase(@"d:\projects\mindattic.worktrees\repo\rp-t7x", null)]
    public void A_lower_case_worktree_path_still_names_the_ticket_in_upper_case(string? cwd, string? expected) =>
        Assert.That(TicketAttribution.FromWorktree(cwd), Is.EqualTo(expected));

    [Test]
    public void A_family_code_with_a_dot_is_attributed_like_any_other()
    {
        var item = new QueueItem("MindAttic.Orchestrator", @"D:\W\MindAttic.Orchestrator", new Ticket("M.ORCH-T7", "2026-10-01", "fix", "open", "x", ""), 0, null, false);

        Assert.That(TicketAttribution.FromPrompt(Scheduler.PromptFor(item)), Is.EqualTo(("M.ORCH-T7", "fix")));
        Assert.That(TicketAttribution.FromWorktree(@"D:\Projects\MindAttic.worktrees\MindAttic.Orchestrator\M.ORCH-T7\src"), Is.EqualTo("M.ORCH-T7"));
    }

    [Test]
    public void A_transcript_that_was_dispatched_on_a_ticket_says_so()
    {
        var dir = Path.Combine(Path.GetTempPath(), "orch-attr-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "p"));
        try
        {
            File.WriteAllLines(Path.Combine(dir, "p", "s.jsonl"), [
                System.Text.Json.JsonSerializer.Serialize(new { type = "user", timestamp = "2026-10-14T10:00:00Z", sessionId = "s", cwd = @"D:\W\Repo",
                    message = new { role = "user", content = "Work on ticket RP-T7 (fix): the thing. When it's done…" } }),
                System.Text.Json.JsonSerializer.Serialize(new { type = "assistant", timestamp = "2026-10-14T10:01:00Z", sessionId = "s", cwd = @"D:\W\Repo",
                    message = new { id = "m1", usage = new { input_tokens = 5, cache_creation_input_tokens = 0, cache_read_input_tokens = 0, output_tokens = 95 } } }),
            ]);

            var s = new TranscriptReader(dir).Poll(Now.AddDays(-1)).Single();

            Assert.That((s.TicketId, s.TicketType), Is.EqualTo(("RP-T7", "fix")));
            var cost = TicketCosts.Observe([s], _ => "Repo").Single();
            Assert.That((cost.TicketId, cost.Fresh), Is.EqualTo(("RP-T7", 100L)));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    private static SessionCost S(string ticket, string type, string repo, long fresh, int day = 1) =>
        new(Guid.NewGuid().ToString(), ticket, type, repo, fresh, Now.AddDays(-day));

    [Test]
    public void Estimates_prefer_the_same_repo_and_type_then_fall_back()
    {
        var costs = new TicketCosts([
            S("A-T1", "fix", "A", 100), S("A-T2", "fix", "A", 200), S("A-T3", "fix", "A", 300),
            S("B-T1", "fix", "B", 1000), S("B-T2", "feature", "B", 5000),
        ]);

        Assert.That(costs.Estimate("A", "fix")!.Fresh, Is.EqualTo(200));
        Assert.That(costs.Estimate("B", "fix")!.Of, Is.EqualTo("fix tickets"), "only one fix in B, so all fixes");
        Assert.That(costs.Estimate("C", "chore")!.Of, Is.EqualTo("tickets"));
        Assert.That(new TicketCosts([]).Estimate("A", "fix"), Is.Null);
    }

    [Test]
    public void A_ticket_is_one_cost_across_its_sessions_and_history_outlives_the_transcripts()
    {
        var first = S("A-T1", "fix", "A", 100);
        var stored = TicketCosts.Merge([], [first, S("A-T1", "fix", "A", 50)], Now);
        var later = TicketCosts.Merge(stored, [first with { Fresh = 80 }], Now); // the transcript partly aged out

        Assert.That(new TicketCosts(later).Tickets.Single().Fresh, Is.EqualTo(150), "the largest sighting of each session is kept");
    }

    [Test]
    public void A_ticket_too_big_for_what_is_left_waits_instead_of_running_out_halfway()
    {
        Ticket T(string id, string type) => new(id, "2026-10-01", type, "open", id, "");
        var snap = new Snapshot(Now, [
            new RepoStatus("A", @"D:\W\A", RepoState.Dormant, [], [], [T("A-T9", "feature")], null, default),
            new RepoStatus("B", @"D:\W\B", RepoState.Dormant, [], [], [T("B-T9", "fix")], null, default),
        ], [])
        {
            Pace = new Pace(PaceMode.Open, 4, 1000, 100, 100, "5-hour", Now.AddHours(2), "On pace.") { RoomTokens = 2_000 },
            Costs = new TicketCosts([.. Enumerable.Range(1, 3).SelectMany(i => new[] { S($"X-T{i}", "feature", "X", 9_000), S($"Y-T{i}", "fix", "Y", 500) })]),
        };

        var plan = Scheduler.Plan(snap, new OrchestratorSettings { Approved = ["A-T9"] }, []);

        Assert.That(plan.TooBig.Single().Id, Is.EqualTo("A-T9"));
        Assert.That(plan.Proposals.Single().Item.Id, Is.EqualTo("B-T9"));
        Assert.That(plan.Proposals.Single().Reason, Does.Contain("~500"));
    }
}
