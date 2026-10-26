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
    public void The_inbox_name_is_built_from_its_parts_whatever_the_user_name_holds()
    {
        Assert.That(StatusStream.InboxPipeNameFor("ryan"), Is.EqualTo("mindattic-orchestrator-status-in-ryan"));
        // A user name that sanitizes to hold "-status-" is left as it is (M.ORCH-T969): a Replace rewrote it too.
        Assert.That(StatusStream.InboxPipeNameFor("a status b"), Is.EqualTo("mindattic-orchestrator-status-in-a-status-b"));
        Assert.That(StatusStream.InboxPipeName, Is.EqualTo(StatusStream.InboxPipeNameFor(Environment.UserName)));
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

    [Test]
    public async Task A_second_inbox_on_the_same_pipe_takes_none_of_its_lines()
    {
        // M.ORCH-T1000: a second daemon's inbox joined the first's pipe and took about half the lines.
        var pipe = "mindattic-orchestrator-status-in-test-" + Guid.NewGuid().ToString("N");
        var (first, second) = (0, 0);
        using var a = new StatusInbox { PipeName = pipe }.On(StatusInbox.Poke, _ => Interlocked.Increment(ref first));
        a.Start();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (!StatusInbox.TrySend(StatusEnvelope.Of("warm-up", new { }), pipe, 500)) await Task.Delay(50, stop.Token);
        using var b = new StatusInbox { PipeName = pipe }.On(StatusInbox.Poke, _ => Interlocked.Increment(ref second));
        b.Start();
        await Task.Delay(300, stop.Token);
        for (var sent = 0; sent < 20;)
            if (StatusInbox.TrySend(StatusEnvelope.Of(StatusInbox.Poke, new { }), pipe, 500)) sent++;
            else await Task.Delay(20, stop.Token);
        while (Volatile.Read(ref first) < 20) await Task.Delay(20, stop.Token);
        Assert.That(second, Is.Zero);
    }
}
