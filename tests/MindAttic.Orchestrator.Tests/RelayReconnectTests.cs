using MindAttic.Orchestrator.Term.Wpf;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>A Terminals tab whose relay drops reconnects while its host lives (M.ORCH-T966).</summary>
public sealed class RelayReconnectTests
{
    [Test]
    public void The_backoff_doubles_from_half_a_second_and_stops_at_five()
    {
        var waits = Enumerable.Range(0, 7).Select(i => RelayConnection.ReconnectDelay(i).TotalSeconds).ToArray();
        Assert.That(waits, Is.EqualTo(new[] { 0.5, 1, 2, 4, 5, 5, 5 }));
        Assert.That(RelayConnection.ReconnectDelay(int.MaxValue), Is.EqualTo(RelayConnection.MaxReconnectDelay));
        Assert.That(RelayConnection.ReconnectDelay(-1).TotalSeconds, Is.EqualTo(0.5));
    }

    /// <summary>bughunt 2026-10-28: WriteInput used to check for a link (HasLink) and queue the frame (Send) under two separate
    /// lock acquisitions, so a link dropping in between left a keystroke silently dropped with no InputDropped raised. Send now
    /// decides and queues atomically under one lock, so WriteInput (here with no link ever attached, the same end state as one
    /// dropping mid-check) must always raise InputDropped instead of swallowing the input.</summary>
    [Test]
    public void WriteInput_with_no_link_drops_the_keystroke_and_raises_InputDropped()
    {
        var dropped = 0;
        var connection = new RelayConnection("test-no-link", _ => Task.FromException<Stream>(new IOException("never connects")));
        connection.InputDropped += () => dropped++;

        connection.WriteInput("hello");

        Assert.That(dropped, Is.EqualTo(1));
    }
}
