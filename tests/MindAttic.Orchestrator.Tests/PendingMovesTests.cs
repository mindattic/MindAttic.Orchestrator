using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Wpf.ViewModels;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>A move in flight locks its CLI's controls until it lands on the new login (M.ORCH-T638).</summary>
[NonParallelizable]
public sealed class PendingMovesTests
{
    private static readonly DateTime T0 = new(2026, 10, 22, 18, 0, 0, DateTimeKind.Utc);

    [SetUp, TearDown]
    public void Reset() => PendingMoves.Clear();

    private static AgentBoardRow Cli(string session, string repo, string? ownLogin, DateTime started) =>
        new(session, repo, null, null, started, started, AgentState.Working, "", false, new TokenUsage(0, 0, 0, 0), []) { OwnLogin = ownLogin };

    [Test]
    public void A_move_locks_its_cli_until_it_shows_up_on_the_new_login()
    {
        var changes = 0;
        void Count() => changes++;
        PendingMoves.Changed += Count;
        try
        {
            PendingMoves.Begin(new PendingMoves.Move("s1", "Repo", "ryan@example.com", "Ryan", T0));
            Assert.That(PendingMoves.For("s1")?.TargetName, Is.EqualTo("Ryan"));
            Assert.That(PendingMoves.For("s2"), Is.Null);

            Assert.That(PendingMoves.Reconcile([Cli("s1", "Repo", null, T0.AddHours(-1))], T0.AddMinutes(1)), Is.Empty);
            Assert.That(PendingMoves.For("s1"), Is.Not.Null, "still on the old login: still moving");

            Assert.That(PendingMoves.Reconcile([Cli("s1", "Repo", "RYAN@example.com", T0.AddHours(-1))], T0.AddMinutes(2)), Is.Empty);
            Assert.That(PendingMoves.For("s1"), Is.Null, "its own session on the new login: landed");
            Assert.That(changes, Is.EqualTo(2), "raised when it began and when it landed");
        }
        finally { PendingMoves.Changed -= Count; }
    }

    [Test]
    public void A_resumed_cli_under_a_new_session_in_the_same_repo_counts_as_landed_but_an_older_one_does_not()
    {
        PendingMoves.Begin(new PendingMoves.Move("s1", "Repo", null, "Global login", T0));
        PendingMoves.Reconcile([Cli("old", "Repo", null, T0.AddMinutes(-5))], T0.AddMinutes(1));
        Assert.That(PendingMoves.For("s1"), Is.Not.Null, "a CLI already on the target before the move isn't this one");

        PendingMoves.Reconcile([Cli("new", "Repo", null, T0.AddMinutes(3))], T0.AddMinutes(4));
        Assert.That(PendingMoves.For("s1"), Is.Null);
    }

    [Test]
    public void A_move_that_never_lands_times_out_once_and_unlocks()
    {
        PendingMoves.Begin(new PendingMoves.Move("s1", "Repo", "ryan@example.com", "Ryan", T0));
        var late = T0 + PendingMoves.Timeout + TimeSpan.FromSeconds(1);
        Assert.That(PendingMoves.Reconcile([], late).Select(m => m.SessionId), Is.EqualTo(new[] { "s1" }));
        Assert.That(PendingMoves.For("s1"), Is.Null);
        Assert.That(PendingMoves.Reconcile([], late), Is.Empty, "said once");
    }

    [Test]
    public void A_refused_move_unlocks_at_once()
    {
        PendingMoves.Begin(new PendingMoves.Move("s1", "Repo", "ryan@example.com", "Ryan", T0));
        PendingMoves.End("s1");
        Assert.That(PendingMoves.For("s1"), Is.Null);
    }
}
