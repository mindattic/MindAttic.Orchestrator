using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using MindAttic.Tickets.Log;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class BriefingTests
{
    [Test]
    public void Says_what_happened_while_you_were_away_and_what_is_waiting()
    {
        var now = new DateTime(2026, 10, 14, 18, 0, 0, DateTimeKind.Utc);
        var session = new TranscriptSession("s", @"D:\W\Prose", now.AddMinutes(-20), default, null)
        {
            Events = [new UsageEvent(now.AddHours(-2), new TokenUsage(0, 0, 0, 3_000_000)), new UsageEvent(now.AddDays(-2), new TokenUsage(0, 0, 0, 9))],
        };
        var snap = new Snapshot(now, [
            new RepoStatus("Prose", @"D:\W\Prose", RepoState.Waiting, [], [session], [], now.AddMinutes(-20), default),
            new RepoStatus("Quiet", @"D:\W\Quiet", RepoState.Dormant, [], [], [new Ticket("Q-T1", "2026-10-13", "fix", "open", "x", "")], null, default),
        ], []);
        var plan = Scheduler.Plan(snap, new OrchestratorSettings(), [new Assignment("P-T3", "Prose", @"D:\W\Prose", "Claude", now.AddHours(-5), false)]);

        var text = Briefing.Compose(snap, plan, now.AddHours(-4));

        Assert.That(text, Does.Contain("4h away"));
        Assert.That(text, Does.Contain("3M uncached went by, mostly in Prose"));
        Assert.That(text, Does.Contain("P-T3 closed"), "its assignment finished while you were gone");
        Assert.That(text, Does.Contain("Waiting on you: Prose"));
        Assert.That(Briefing.Compose(snap, plan, now.AddMinutes(-30)), Is.Null, "half an hour isn't away");
    }

    [Test]
    public void Under_an_hour_away_is_no_briefing_and_an_hour_is()
    {
        var now = new DateTime(2026, 10, 14, 18, 0, 0, DateTimeKind.Utc);
        var session = new TranscriptSession("s", @"D:\W\A", now.AddMinutes(-5), default, null)
        {
            Events = [new UsageEvent(now.AddMinutes(-10), new TokenUsage(0, 0, 0, 5_000))],
        };
        var snap = new Snapshot(now, [new RepoStatus("A", @"D:\W\A", RepoState.Dormant, [], [session], [], now.AddMinutes(-5), default)], []);
        var plan = Scheduler.Plan(snap, new OrchestratorSettings(), []);

        Assert.That(Briefing.Compose(snap, plan, now - Briefing.MinAway + TimeSpan.FromSeconds(1)), Is.Null, "a second short of an hour");
        Assert.That(Briefing.Compose(snap, plan, now - Briefing.MinAway), Does.Contain("uncached went by, mostly in A"), "an hour exactly");
    }

    [Test]
    public void Nothing_ran_and_nothing_waiting_is_no_briefing()
    {
        var now = new DateTime(2026, 10, 14, 18, 0, 0, DateTimeKind.Utc);
        var old = new TranscriptSession("s", @"D:\W\A", now.AddDays(-2), default, null)
        {
            Events = [new UsageEvent(now.AddDays(-2), new TokenUsage(0, 0, 0, 9_000))],
        };
        RepoStatus Repo(RepoState state) => new("A", @"D:\W\A", state, [], [old], [], now.AddDays(-2), default);
        var quiet = new Snapshot(now, [Repo(RepoState.Dormant)], []);

        Assert.That(Briefing.Compose(quiet, Scheduler.Plan(quiet, new OrchestratorSettings(), []), now.AddHours(-6)), Is.Null,
            "usage from before you left doesn't count, and with nothing else to say there's no briefing");

        var waiting = new Snapshot(now, [Repo(RepoState.Waiting)], []);
        Assert.That(Briefing.Compose(waiting, Scheduler.Plan(waiting, new OrchestratorSettings(), []), now.AddHours(-6)),
            Does.Contain("Nothing ran.").And.Contain("Waiting on you: A"), "nothing ran, but something is waiting: still worth saying");
    }
}
