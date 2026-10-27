using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using MindAttic.Tickets.Log;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T786: the last-resort cool-off. With the new-start cool-off already on and the trend still past the pace line,
/// the orchestrator aborts just enough ticket subagents (not landing first, fastest first) to bend it back under, releases their
/// claims and puts their tickets first in the queue; a manual session's own subagents are never touched.</summary>
public sealed class AgentAbortTests
{
    // Now as the clock has it: a subagent's mark is only alive within hours of its file's own time.
    private static readonly DateTime Now = DateTime.UtcNow;
    private static readonly DateTime End = Now.AddHours(3);

    /// <summary>Three hours left, on course for <paramref name="projected"/>% against the 98% line.</summary>
    private static Utilization Trend(double projected, DateTime? now = null) =>
        new(End.AddHours(-5), End, now ?? Now, 98, [], 50, 10, projected, TimeSpan.Zero);

    private static AbortCandidate C(string id, double rate, bool landing = false) => new(id, "A-T" + id[1..], rate, landing);

    [Test]
    public void It_aborts_just_enough_fastest_first_to_bend_the_trend_back_under()
    {
        // 18 points over with 3 hours left: 6%/h to shed.
        var running = new[] { C("a1", 2), C("a2", 5), C("a3", 3), C("a4", 0.5) };
        Assert.That(AgentAbort.Choose(Trend(116), running, [], Now).Select(c => c.AgentId), Is.EqualTo(new[] { "a2", "a3" }), "5 + 3 covers 6, fastest first");
        Assert.That(AgentAbort.Choose(Trend(104), running, [], Now).Select(c => c.AgentId), Is.EqualTo(new[] { "a2" }), "2%/h over: the fastest alone");
        Assert.That(AgentAbort.Choose(Trend(200), running, [], Now), Has.Count.EqualTo(4), "even all of them can't cover it: all go");
        Assert.That(AgentAbort.Choose(Trend(98), running, [], Now), Is.Empty, "on the line is not past it");
        Assert.That(AgentAbort.Choose(Trend(90), running, [], Now), Is.Empty, "under the line");
    }

    [Test]
    public void A_transcripts_agent_id_is_keyed_as_its_mark_is_named()
    {
        // The transcript names it agent-<id> (its file name); the gate's mark, bare <id> (bughunt 2026-10-27).
        Assert.That(AgentAbort.MarkName(AgentAbort.BareId("agent-a1b2c3")), Is.EqualTo("a1b2c3"));
        Assert.That(AgentAbort.BareId("a1b2c3"), Is.EqualTo("a1b2c3"), "already bare: unchanged");
    }

    [Test]
    public void One_landing_its_work_is_aborted_last()
    {
        var running = new[] { C("a1", 9, landing: true), C("a2", 4), C("a3", 3) };
        Assert.That(AgentAbort.Choose(Trend(116), running, [], Now).Select(c => c.AgentId), Is.EqualTo(new[] { "a2", "a3" }),
            "the fastest is committing: the ones at a safe stop go first");
        Assert.That(AgentAbort.Landing("Commit and push the change"), Is.True);
        Assert.That(AgentAbort.Landing("Running git rebase origin/main"), Is.True);
        Assert.That(AgentAbort.Landing("Editing Daemon.cs"), Is.False);
        Assert.That(AgentAbort.Landing(null), Is.False);
    }

    [Test]
    public void Recent_aborts_still_in_the_trend_are_counted_as_shed_while_they_roll_out()
    {
        var running = new[] { C("a2", 5), C("a3", 3) };
        // 6%/h over, but one aborted a minute ago burned 7%/h: most of it is still in the 10-minute rate, nothing more goes.
        Assert.That(AgentAbort.Choose(Trend(116), running, [new AbortMark("a1", Now.AddMinutes(-1), 7)], Now), Is.Empty);
        // Rolled out (past the settle time): the trend is what it is, and it's still over.
        Assert.That(AgentAbort.Choose(Trend(116), running, [new AbortMark("a1", Now.AddMinutes(-11), 7)], Now).Select(c => c.AgentId),
            Is.EqualTo(new[] { "a2", "a3" }));
    }

    [Test]
    public void It_is_due_only_once_the_cool_off_has_had_its_chance()
    {
        var cooling = new Pace(PaceMode.Open, 6, 10_000, 0, 1_000, "5-hour", End, "On pace.") { SubagentAllowance = 4, Subagents = 6, CoolOffPercent = 118 };
        var after = TimeSpan.FromMinutes(3);
        Assert.That(AgentAbort.Due(cooling, Trend(118), Now.AddMinutes(-3), after, Now), Is.True);
        Assert.That(AgentAbort.Due(cooling, Trend(118), Now.AddMinutes(-1), after, Now), Is.False, "the throttle gets its minutes first");
        Assert.That(AgentAbort.Due(cooling with { CoolOffPercent = null }, Trend(118), Now.AddHours(-1), after, Now), Is.False, "no cool-off");
        Assert.That(AgentAbort.Due(cooling with { Stop = true }, Trend(118), Now.AddHours(-1), after, Now), Is.False, "the hard cutoff has it");
        Assert.That(AgentAbort.Due(cooling, Trend(118) with { Ended = true }, Now.AddHours(-1), after, Now), Is.False, "no window running");
        Assert.That(AgentAbort.Due(cooling, null, Now.AddHours(-1), after, Now), Is.False);
    }

    [Test]
    public void A_subagents_burn_is_its_recent_fresh_tokens_against_the_window()
    {
        var events = new[] { Event(Now.AddMinutes(-2), 15_000), Event(Now.AddMinutes(-8), 5_000), Event(Now.AddMinutes(-30), 99_000) };
        // 20k in the last 10 minutes is 120k an hour: 12% of a 1M window an hour.
        Assert.That(AgentAbort.PercentPerHour(events, null, 1_000_000, Now), Is.EqualTo(12).Within(1e-9));
        var activity = new SubagentActivity("a1", "s1", "general-purpose", Now.AddHours(-1), Now, new TokenUsage(50_000, 0, 1_000_000, 0));
        Assert.That(AgentAbort.PercentPerHour(null, activity, 1_000_000, Now), Is.EqualTo(5).Within(1e-9), "no responses known: its run's average, cache reads left out");
        Assert.That(AgentAbort.PercentPerHour(events, null, 0, Now), Is.Zero, "no limit");
    }

    [Test]
    public void The_daemon_aborts_releases_the_claim_and_puts_the_ticket_first_never_a_manual_sessions_subagent()
    {
        var dir = Path.Combine(Path.GetTempPath(), "abort-" + Guid.NewGuid().ToString("N"));
        try
        {
            var marks = Path.Combine(dir, "session-context");
            var subagents = Path.Combine(marks, "subagents");
            Directory.CreateDirectory(subagents);
            foreach (var a in new[] { "a1", "a2", "manual" }) File.WriteAllText(Path.Combine(subagents, a), ""); // running, no CLI recorded
            var store = new JsonStore(Path.Combine(dir, "store"));
            store.SaveAssignments([
                new Assignment("A-T1", "A", @"D:\W\A-T1", "Claude", Now, true) { Cli = 7 },
                new Assignment("A-T2", "A", @"D:\W\A-T2", "Claude", Now, true) { Cli = 7 },
            ]);
            var session = new TranscriptSession("s1", @"D:\W\A", Now, default, null)
            {
                TicketedSubagents =
                [
                    new TicketedSubagent("a1", "A-T1", "fix", @"D:\W\A-T1", [Event(Now.AddMinutes(-1), 20_000)]), // 12%/h
                    new TicketedSubagent("a2", "A-T2", "fix", @"D:\W\A-T2", [Event(Now.AddMinutes(-1), 2_000)]),  // 1.2%/h
                ],
            };
            Snapshot At(DateTime now, double projected) => new(now, [new RepoStatus("A", @"D:\W\A", RepoState.Working, [], [session], [T("A-T1"), T("A-T2"), T("A-T3")], null, default)], [])
            {
                Budget = new BudgetStatus(new UsageBlock(End.AddHours(-5), End, default, now), 1_000_000, false, 50, default, default, null, null, 0, null, 90),
                Pace = new Pace(PaceMode.Open, 6, 10_000, 0, 1_000, "5-hour", End, "On pace.") { SubagentAllowance = 2, Subagents = 3, CoolOffPercent = projected },
                Utilization = Trend(projected, now),
            };
            var aborter = new AgentAborter(store, marks, Path.Combine(dir, "projects"));
            var settings = new OrchestratorSettings();

            Assert.That(aborter.Tick(At(Now, 118), settings, Now), Is.Empty, "the cool-off just came on: it gets its minutes first");
            var later = Now.AddMinutes(3);
            var aborted = aborter.Tick(At(later, 118), settings, later);
            Assert.That(aborted.Select(a => (a.AgentId, a.TicketId)), Is.EqualTo(new[] { ("a1", "A-T1") }), "12%/h covers the 6.7%/h over; the manual one is never picked");
            Assert.That(File.Exists(Path.Combine(marks, AgentAbort.DirName, "a1")), Is.True, "the pace gate's abort mark");
            Assert.That(Directory.GetFiles(Path.Combine(marks, AgentAbort.DirName)), Has.Length.EqualTo(1));
            Assert.That(store.LoadAssignments().Select(a => a.TicketId), Is.EqualTo(new[] { "A-T2" }), "its claim released, the other kept");
            Assert.That(AgentAbort.Requeued(store, later), Is.EquivalentTo(new[] { "A-T1" }));

            var plan = Scheduler.Plan(At(later, 118) with { Requeued = AgentAbort.Requeued(store, later) }, settings, store.LoadAssignments());
            Assert.That(plan.Queue.First().Id, Is.EqualTo("A-T1"), "first in the queue for the next round");

            var next = later.AddMinutes(1);
            Assert.That(aborter.Tick(At(next, 118), settings, next), Is.Empty, "the abort is still rolling out of the trend: nothing more yet");
            Assert.That(aborter.Tick(At(next, 118), settings with { AgentAbort = false }, next), Is.Empty, "off");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Test]
    public void Abort_marks_go_once_their_subagent_stopped_and_they_rolled_out()
    {
        var dir = Path.Combine(Path.GetTempPath(), "abort-" + Guid.NewGuid().ToString("N"));
        try
        {
            AgentAbort.WriteMark(dir, C("a1", 4), "b@example.com", 118, 98, Now);
            AgentAbort.WriteMark(dir, C("a2", 4), null, 118, 98, Now);
            var mark = AgentAbort.Marks(dir, Now).Single(m => m.AgentId == "a1");
            Assert.That((mark.AtUtc, mark.PercentPerHour), Is.EqualTo((Now, 4.0)));
            var alive = new HashSet<string>(["a2"]);
            AgentAbort.Prune(dir, alive, Now.AddMinutes(5));
            Assert.That(AgentAbort.Marks(dir, Now).Select(m => m.AgentId), Is.EquivalentTo(new[] { "a1", "a2" }), "still settling");
            AgentAbort.Prune(dir, alive, Now.AddMinutes(11));
            Assert.That(AgentAbort.Marks(dir, Now).Select(m => m.AgentId), Is.EqualTo(new[] { "a2" }), "a1 stopped and rolled out; a2 still runs");
            AgentAbort.Prune(dir, alive, Now.AddHours(7));
            Assert.That(AgentAbort.Marks(dir, Now), Is.Empty, "past its life");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
    }

    private static UsageEvent Event(DateTime at, long fresh) => new(at, new TokenUsage(fresh, 0, 0, 0));

    private static Ticket T(string id) => new(id, "2026-10-01", "fix", "open", $"summary of {id}", "");
}
