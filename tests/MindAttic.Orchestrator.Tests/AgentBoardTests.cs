using MindAttic.Orchestrator.Activity;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class AgentBoardTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan WorkingWindow = TimeSpan.FromMinutes(10);

    private static RepoStatus Repo(string name, params TranscriptSession[] sessions) =>
        new(name, @"D:\" + name, RepoState.Working, [], sessions, [], Now, default);

    private static TranscriptSession Session(string id, DateTime lastWriteUtc, string? ticketId = null, string? ticketType = null) =>
        new(id, @"D:\repo", lastWriteUtc, new TokenUsage(100, 0, 0, 50), "claude-sonnet-5") { TicketId = ticketId, TicketType = ticketType };

    [Test]
    public void A_session_that_wrote_within_the_working_window_is_working()
    {
        var repos = new[] { Repo("A", Session("s1", Now.AddMinutes(-1), "MAO-T1")) };

        var rows = AgentBoard.Build(repos, null, [], [], WorkingWindow, Now);

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0].State, Is.EqualTo(AgentState.Working));
        Assert.That(rows[0].StateDetail, Is.EqualTo("Working"));
    }

    [Test]
    public void A_session_idle_past_the_working_window_is_waiting_for_you()
    {
        var repos = new[] { Repo("A", Session("s1", Now.AddMinutes(-30), "MAO-T1")) };

        var rows = AgentBoard.Build(repos, null, [], [], WorkingWindow, Now);

        Assert.That(rows[0].State, Is.EqualTo(AgentState.Waiting));
        Assert.That(rows[0].StateDetail, Is.EqualTo("Waiting for you"));
    }

    [Test]
    public void A_recent_session_under_a_pace_hold_is_held_by_pace_not_plain_working()
    {
        var repos = new[] { Repo("A", Session("s1", Now.AddMinutes(-1), "MAO-T1")) };
        var pace = new Pace(PaceMode.Throttled, 1, 100, 100, null, "5-hour", Now.AddHours(1), "Paced to 1 session.");

        var rows = AgentBoard.Build(repos, pace, [], [], WorkingWindow, Now);

        Assert.That(rows[0].State, Is.EqualTo(AgentState.HeldByPace));
        Assert.That(rows[0].StateDetail, Is.EqualTo(pace.Note));
    }

    [Test]
    public void An_open_pace_leaves_a_recent_session_plain_working()
    {
        var repos = new[] { Repo("A", Session("s1", Now.AddMinutes(-1), "MAO-T1")) };
        var pace = new Pace(PaceMode.Open, 4, 100, 50, null, null, null, "Open: every slot is available.");

        var rows = AgentBoard.Build(repos, pace, [], [], WorkingWindow, Now);

        Assert.That(rows[0].State, Is.EqualTo(AgentState.Working));
    }

    [Test]
    public void An_autopilot_assignment_is_flagged_and_timed_from_when_it_started()
    {
        var repos = new[] { Repo("A", Session("s1", Now.AddMinutes(-1), "MAO-T1")) };
        var assignment = new Assignment("MAO-T1", "A", @"D:\A", Providers.Claude, Now.AddHours(-2), Worktree: true) { Pilot = 4242 };

        var rows = AgentBoard.Build(repos, null, [assignment], [], WorkingWindow, Now);

        Assert.That(rows[0].IsAutopilot, Is.True);
        Assert.That(rows[0].StartedUtc, Is.EqualTo(Now.AddHours(-2)), "the assignment's own start beats guessing from the transcript");
    }

    [Test]
    public void A_session_with_no_assignment_starts_from_its_first_recorded_event()
    {
        var session = Session("s1", Now.AddMinutes(-1), "MAO-T1") with { Events = [new UsageEvent(Now.AddMinutes(-20), default)] };
        var repos = new[] { Repo("A", session) };

        var rows = AgentBoard.Build(repos, null, [], [], WorkingWindow, Now);

        Assert.That(rows[0].StartedUtc, Is.EqualTo(Now.AddMinutes(-20)));
        Assert.That(rows[0].IsAutopilot, Is.False);
    }

    [Test]
    public void Subagents_are_nested_under_the_row_whose_session_id_is_their_parent()
    {
        var repos = new[] { Repo("A", Session("s1", Now.AddMinutes(-1), "MAO-T1"), Session("s2", Now.AddMinutes(-1), "MAO-T2")) };
        var subagent = new SubagentActivity("agent-1", "s1", "Explore", Now.AddMinutes(-5), Now, new TokenUsage(10, 0, 0, 5));

        var rows = AgentBoard.Build(repos, null, [], [subagent], WorkingWindow, Now);

        var s1 = rows.Single(r => r.SessionId == "s1");
        var s2 = rows.Single(r => r.SessionId == "s2");
        Assert.That(s1.Subagents, Is.EqualTo(new[] { subagent }));
        Assert.That(s2.Subagents, Is.Empty);
    }

    [Test]
    public void Rows_come_back_most_recently_active_first()
    {
        var repos = new[] { Repo("A", Session("old", Now.AddMinutes(-30), "MAO-T1"), Session("fresh", Now.AddMinutes(-1), "MAO-T2")) };

        var rows = AgentBoard.Build(repos, null, [], [], WorkingWindow, Now);

        Assert.That(rows.Select(r => r.SessionId), Is.EqualTo(new[] { "fresh", "old" }));
    }
}
