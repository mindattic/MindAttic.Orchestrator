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
}
