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

    private static string UserLine(string session, string ts, string text) =>
        System.Text.Json.JsonSerializer.Serialize(new { type = "user", timestamp = ts, sessionId = session, cwd = @"D:\W\Repo",
            message = new { role = "user", content = text } });

    private static string Response(string session, string id, string ts, int output) =>
        System.Text.Json.JsonSerializer.Serialize(new { type = "assistant", timestamp = ts, sessionId = session, cwd = @"D:\W\Repo",
            message = new { id, usage = new { input_tokens = 0, cache_creation_input_tokens = 0, cache_read_input_tokens = 0, output_tokens = output } } });

    [Test]
    public void A_subagents_ticket_is_charged_only_its_own_tokens_not_its_parent_clis_session()
    {
        // M.ORCH-T252: an /autopilot subagent's transcript shares its CLI's session id. The ticket its prompt named was stamped
        // on the whole session, charging that ticket for the CLI's and every other subagent's tokens.
        var dir = Path.Combine(Path.GetTempPath(), "orch-attr-" + Guid.NewGuid().ToString("N"));
        var subs = Path.Combine(dir, "p", "s", "subagents");
        Directory.CreateDirectory(subs);
        try
        {
            File.WriteAllLines(Path.Combine(dir, "p", "s.jsonl"), [
                UserLine("s", "2026-10-14T10:00:00Z", "/autopilot"),
                Response("s", "m1", "2026-10-14T10:01:00Z", 1000),
            ]);
            File.WriteAllLines(Path.Combine(subs, "agent-a1.jsonl"), [
                UserLine("s", "2026-10-14T10:02:00Z", "Work on ticket RP-T7 (fix): one. You have your own git worktree…"),
                Response("s", "a1", "2026-10-14T10:03:00Z", 70),
            ]);
            File.WriteAllLines(Path.Combine(subs, "agent-b2.jsonl"), [
                UserLine("s", "2026-10-14T10:02:00Z", "Work on ticket RP-T8 (feature): two. You have your own git worktree…"),
                Response("s", "b1", "2026-10-14T10:04:00Z", 80),
            ]);
            File.WriteAllLines(Path.Combine(subs, "agent-c3.jsonl"), [
                UserLine("s", "2026-10-14T10:02:00Z", "Find where the pacer reads its settings"),
                Response("s", "c1", "2026-10-14T10:05:00Z", 5),
            ]);

            var s = new TranscriptReader(dir).Poll(Now.AddDays(-1)).Single();

            Assert.That(s.TicketId, Is.Null, "the CLI wasn't dispatched on a ticket; only its subagents were");
            Assert.That(s.UsageSince(Now.AddDays(-1)).Output, Is.EqualTo(1155), "the CLI's limits still count every subagent");
            var costs = TicketCosts.Observe([s], _ => "Repo").OrderBy(c => c.TicketId).ToList();
            Assert.That(costs.Select(c => (c.TicketId, c.Type, c.Fresh)),
                Is.EqualTo(new[] { ("RP-T7", (string?)"fix", 70L), ("RP-T8", (string?)"feature", 80L) }));
            Assert.That(costs.Select(c => c.SessionId), Is.EqualTo(new[] { "s/agent-a1", "s/agent-b2" }), "each subagent is its own cost entry");

            // A cost stored by the old reader, charging RP-T7 for the whole session, is dropped once the session is seen again.
            var stored = new SessionCost("s", "RP-T7", "fix", "Repo", 1155, Now.AddHours(-1));
            var merged = TicketCosts.Merge([stored], costs, Now, TicketCosts.Unticketed([s]));
            Assert.That(new TicketCosts(merged).Tickets.Single(t => t.TicketId == "RP-T7").Fresh, Is.EqualTo(70));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public void A_dispatched_session_is_charged_for_itself_but_not_for_a_subagent_on_another_ticket()
    {
        var dir = Path.Combine(Path.GetTempPath(), "orch-attr-" + Guid.NewGuid().ToString("N"));
        var subs = Path.Combine(dir, "p", "s", "subagents");
        Directory.CreateDirectory(subs);
        try
        {
            File.WriteAllLines(Path.Combine(dir, "p", "s.jsonl"), [
                UserLine("s", "2026-10-14T10:00:00Z", "Work on ticket RP-T1 (fix): the parent's own. When it's done…"),
                Response("s", "m1", "2026-10-14T10:01:00Z", 300),
            ]);
            File.WriteAllLines(Path.Combine(subs, "agent-a1.jsonl"), [
                UserLine("s", "2026-10-14T10:02:00Z", "Work on ticket RP-T7 (fix): one. You have your own git worktree…"),
                Response("s", "a1", "2026-10-14T10:03:00Z", 70),
            ]);
            File.WriteAllLines(Path.Combine(subs, "agent-c3.jsonl"), [
                UserLine("s", "2026-10-14T10:02:00Z", "Find where the pacer reads its settings"),
                Response("s", "c1", "2026-10-14T10:05:00Z", 5),
            ]);

            var s = new TranscriptReader(dir).Poll(Now.AddDays(-1)).Single();

            Assert.That((s.TicketId, s.TicketType), Is.EqualTo(("RP-T1", "fix")));
            var costs = TicketCosts.Observe([s], _ => "Repo").ToDictionary(c => c.TicketId, c => c.Fresh);
            Assert.That(costs, Is.EqualTo(new Dictionary<string, long> { ["RP-T1"] = 305, ["RP-T7"] = 70 }),
                "a subagent without a ticket of its own is part of the parent's work");

            // M.ORCH-T344: the old reader stored RP-T1's whole session (RP-T7's subagent included); its own share replaces it.
            var observed = TicketCosts.Observe([s], _ => "Repo").ToList();
            var merged = new TicketCosts(TicketCosts.Merge([new SessionCost("s", "RP-T1", "fix", "Repo", 375, Now.AddHours(-1))], observed, Now));
            Assert.That(merged.Tickets.ToDictionary(t => t.TicketId, t => t.Fresh), Is.EqualTo(new Dictionary<string, long> { ["RP-T1"] = 305, ["RP-T7"] = 70 }));
            var agedOut = TicketCosts.Merge([new SessionCost("s", "RP-T1", "fix", "Repo", 900, Now.AddHours(-1))], observed, Now);
            Assert.That(agedOut.Single(c => c.SessionId == "s").Fresh, Is.EqualTo(900), "more than both now show: older turns aged out, kept");
            var own = TicketCosts.Merge([new SessionCost("s", "RP-T1", "fix", "Repo", 340, Now.AddHours(-1), OwnShare: true)], observed, Now);
            Assert.That(own.Single(c => c.SessionId == "s").Fresh, Is.EqualTo(340), "a stored own share is never taken for the old whole-session one");
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

        var plan = Scheduler.Plan(snap with { Approved = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "A-T9" } }, new OrchestratorSettings(), []);

        Assert.That(plan.TooBig.Single().Id, Is.EqualTo("A-T9"));
        Assert.That(plan.Proposals.Single().Item.Id, Is.EqualTo("B-T9"));
        Assert.That(plan.Proposals.Single().Reason, Does.Contain("~500"));
    }
}
