using MindAttic.Orchestrator.Status;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The status inbox (M.ORCH-T872): the hooks' lines reach their handlers, other kinds are relayed to the stream, and a
/// writer with no daemon listening goes on at once.</summary>
public sealed class StatusInboxTests
{
    [Test]
    public void The_inbox_hands_its_kinds_to_their_handlers_and_relays_the_rest()
    {
        var poked = 0;
        var relayed = new List<string>();
        using var inbox = new StatusInbox { PipeName = "test-" + Guid.NewGuid(), Relay = e => relayed.Add(e.Kind) }
            .On(StatusInbox.Poke, _ => poked++);
        inbox.Take("""{"kind":"poke","v":1,"payload":{}}""");
        inbox.Take("""{"kind":"ticket-event","v":1,"payload":{}}""");
        inbox.Take("not json");
        inbox.Take("");
        Assert.That(poked, Is.EqualTo(1));
        Assert.That(relayed, Is.EqualTo(new[] { "ticket-event" }));
    }

    [Test]
    public void Sending_with_no_inbox_listening_fails_at_once_without_throwing()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        Assert.That(StatusInbox.TrySend(StatusEnvelope.Of(StatusInbox.Poke, new { }), "no-such-inbox-" + Guid.NewGuid()), Is.False);
        Assert.That(clock.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)));
    }

    [Test]
    public async Task A_line_sent_to_the_inbox_reaches_its_handler()
    {
        var pipe = "mindattic-orchestrator-status-in-test-" + Guid.NewGuid().ToString("N");
        var poked = new TaskCompletionSource();
        using var inbox = new StatusInbox { PipeName = pipe, Listeners = 1 }.On(StatusInbox.Poke, _ => poked.TrySetResult());
        inbox.Start();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (!StatusInbox.TrySend(StatusEnvelope.Of(StatusInbox.Poke, new { Source = "test" }), pipe, 500))
            await Task.Delay(50, stop.Token);
        await poked.Task.WaitAsync(stop.Token);
    }
}
