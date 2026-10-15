using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using MindAttic.Workflow.Tickets;
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
}
