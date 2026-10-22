using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Wpf.ViewModels;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>A move in flight locks its CLI's controls, and those of every ticket it holds, until it lands on the new login
/// (M.ORCH-T638, M.ORCH-T637): begun here at once, then as the database's moves say.</summary>
[NonParallelizable]
public sealed class PendingMovesTests
{
    private static readonly DateTime T0 = new(2026, 10, 22, 18, 0, 0, DateTimeKind.Utc);

    [SetUp, TearDown]
    public void Reset() => PendingMoves.Clear();

    private static MoveLock Move(string session = "s1", params string[] tickets) =>
        new() { SessionId = session, Pid = 1, HostPid = 77, Repo = "Repo", ToEmail = "ryan@example.com", ToName = "Ryan", StartedUtc = T0, Tickets = [.. tickets] };

    [Test]
    public void A_move_begun_here_locks_its_cli_its_tab_and_its_tickets_at_once()
    {
        var changes = 0;
        void Count() => changes++;
        PendingMoves.Changed += Count;
        try
        {
            PendingMoves.Begin(Move("s1", "A-T1"));
            Assert.That(PendingMoves.For("s1")?.Text, Is.EqualTo("Moving to Ryan…"));
            Assert.That(PendingMoves.ForTicket("a-t1")?.SessionId, Is.EqualTo("s1"));
            Assert.That(PendingMoves.ForHost(77), Is.Not.Null);
            Assert.That(PendingMoves.For("s2"), Is.Null);
            Assert.That(PendingMoves.ForTicket("A-T2"), Is.Null);

            // The refresh before the database shows it keeps it locked.
            PendingMoves.Sync([], T0.AddSeconds(15));
            Assert.That(PendingMoves.For("s1"), Is.Not.Null);
            Assert.That(changes, Is.EqualTo(1));
        }
        finally { PendingMoves.Changed -= Count; }
    }

    [Test]
    public void The_databases_moves_lock_and_unlock_and_an_ended_one_is_said_once()
    {
        // A move made elsewhere (orchestrator move, the rebalancer) locks here too.
        Assert.That(PendingMoves.Sync([Move("s1", "A-T1")], T0), Is.Empty);
        Assert.That(PendingMoves.ForTicket("A-T1"), Is.Not.Null);

        var ended = PendingMoves.Sync([], T0.AddMinutes(1));
        Assert.That(ended.Select(m => m.SessionId), Is.EqualTo(new[] { "s1" }));
        Assert.That(PendingMoves.For("s1"), Is.Null);
        Assert.That(PendingMoves.ForTicket("A-T1"), Is.Null);
        Assert.That(PendingMoves.Sync([], T0.AddMinutes(2)), Is.Empty, "said once");
    }

    [Test]
    public void A_refused_move_unlocks_at_once_and_one_the_database_never_shows_after_its_grace()
    {
        PendingMoves.Begin(Move("s1"));
        PendingMoves.End("s1");
        Assert.That(PendingMoves.For("s1"), Is.Null);

        PendingMoves.Begin(Move("s2"));
        PendingMoves.Sync([], T0 + PendingMoves.LocalGrace + TimeSpan.FromSeconds(1));
        Assert.That(PendingMoves.For("s2"), Is.Null);
    }
}
