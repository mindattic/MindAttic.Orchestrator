using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The edges of Pacer's small helpers, each pinned on its own (M.ORCH-T127).</summary>
public sealed class PacerHelperTests
{
    private static readonly DateTime Day = new(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc);

    private static UsageEvent At(int hour, int minute, long output, long cacheRead = 0) =>
        new(Day.AddHours(hour).AddMinutes(minute), new TokenUsage(0, 0, cacheRead, output));

    // ── SubagentAllowance ───────────────────────────────────────────────────

    [TestCase(0)]
    [TestCase(-3)]
    public void No_subagent_ceiling_allows_none(int max) =>
        Assert.That(Pacer.SubagentAllowance(10_000, 0, 100, 2, max), Is.Zero);

    [TestCase(0.0)]
    [TestCase(-500.0)]
    public void No_sustainable_rate_allows_no_subagents(double sustainable) =>
        Assert.That(Pacer.SubagentAllowance(sustainable, 0, 100, 4, 16), Is.Zero);

    [Test]
    public void The_subagent_allowance_is_clamped_between_one_and_the_ceiling()
    {
        Assert.That(Pacer.SubagentAllowance(10_000, 0, 100, 0, 8), Is.EqualTo(8), "100 more fit, the ceiling caps it");
        Assert.That(Pacer.SubagentAllowance(10_000, 0, 100, int.MaxValue, 8), Is.EqualTo(8), "a huge running count doesn't overflow");
        Assert.That(Pacer.SubagentAllowance(2000, 100_000, 500, 0, 16), Is.EqualTo(1), "far over: still one, a held spawn waits");
        Assert.That(Pacer.SubagentAllowance(2000, 3000, null, 0, 16), Is.EqualTo(1), "no history and over: one less than none is still one");
    }

    [Test]
    public void Over_demand_drains_the_allowance_below_what_is_running()
    {
        Assert.That(Pacer.SubagentAllowance(2000, 4000, 500, 10, 16), Is.EqualTo(6), "2,000/h over at 500/h each: four fewer than the ten running");
        Assert.That(Pacer.SubagentAllowance(2000, 2100, 500, 10, 16), Is.EqualTo(9), "any overage rounds down to at least one fewer");
        Assert.That(Pacer.SubagentAllowance(2000, 3000, null, 5, 16), Is.EqualTo(4), "no history and over: one fewer");
        Assert.That(Pacer.SubagentAllowance(2000, 1000, 0, 3, 16), Is.EqualTo(5), "a zero per-agent cost counts as no history: two more");
    }

    // ── SpareSessions ───────────────────────────────────────────────────────

    [Test]
    public void With_no_session_history_one_spare_session_while_there_is_room_else_none()
    {
        Assert.That(Pacer.SpareSessions(2000, 1000, null), Is.EqualTo(1));
        Assert.That(Pacer.SpareSessions(2000, 1000, 0), Is.EqualTo(1), "a zero cost counts as no history");
        Assert.That(Pacer.SpareSessions(2000, 2000, null), Is.Zero, "exactly at the rate: no room");
        Assert.That(Pacer.SpareSessions(2000, 3000, null), Is.Zero, "over without history is zero, not negative");
    }

    [TestCase(0.0)]
    [TestCase(-100.0)]
    public void No_sustainable_rate_leaves_no_spare_sessions(double sustainable)
    {
        Assert.That(Pacer.SpareSessions(sustainable, 0, 500), Is.Zero);
        Assert.That(Pacer.SpareSessions(sustainable, 0, null), Is.Zero);
    }

    [Test]
    public void Spare_sessions_round_down_and_go_negative_when_over()
    {
        Assert.That(Pacer.SpareSessions(2000, 1000, 300), Is.EqualTo(3), "1,000/h spare over 300/h is 3.3: three");
        Assert.That(Pacer.SpareSessions(2000, 3000, 500), Is.EqualTo(-2), "1,000/h over: two sessions too many");
        Assert.That(Pacer.SpareSessions(2000, 2100, 500), Is.EqualTo(-1), "any overage is at least one too many");
    }

    [Test]
    public void Spare_sessions_are_clamped_to_plus_or_minus_a_hundred()
    {
        Assert.That(Pacer.SpareSessions(1e9, 0, 1), Is.EqualTo(100));
        Assert.That(Pacer.SpareSessions(1, 1e9, 1), Is.EqualTo(-100));
    }

    // ── Unheld ──────────────────────────────────────────────────────────────

    [TestCase(1000, 0, 1000, TestName = "Unheld_with_no_hold_is_the_measured_rate")]
    [TestCase(1000, 20, 2000, TestName = "Unheld_a_hold_equal_to_the_step_doubles_it")]
    [TestCase(1000, 60, 4000, TestName = "Unheld_scales_by_step_plus_hold_over_step")]
    [TestCase(1000, -30, 1000, TestName = "Unheld_a_negative_hold_counts_as_none")]
    [TestCase(0, 100, 0, TestName = "Unheld_nothing_measured_stays_nothing")]
    public void Unheld_scales_by_the_hold(double measured, double held, double expected) =>
        Assert.That(Pacer.Unheld(measured, held), Is.EqualTo(expected).Within(1e-9));

    // ── AgentDelay ──────────────────────────────────────────────────────────

    [Test]
    public void Agents_are_never_held_whatever_the_burn_since_throttling_is_locked_off()
    {
        // M.ORCH-T706: hard cutoffs only; the hold is gone, so no setting or burn can bring it back.
        Assert.That(new OrchestratorSettings().ThrottleAgents, Is.False);
        Assert.That(Pacer.AgentDelay(100_000, 2000, 0, new OrchestratorSettings { MaxAgentDelaySeconds = 300 }), Is.Zero);
        Assert.That(Pacer.AgentDelay(100, 0, 0, new OrchestratorSettings { MaxAgentDelaySeconds = 300 }), Is.Zero);
    }

    [Test]
    public void With_agent_throttling_off_nobody_is_held()
    {
        var off = new OrchestratorSettings();
        Assert.That(Pacer.AgentDelay(100_000, 2000, 0, off), Is.Zero);
        Assert.That(Pacer.AgentDelay(100, 0, 0, off), Is.Zero, "not even out of room");
        Assert.That(Pacer.AgentDelay(100_000, 2000, 0, new OrchestratorSettings { MaxAgentDelaySeconds = 0 }), Is.Zero, "a zero ceiling is off too");
        Assert.That(Pacer.AgentDelay(100_000, 2000, 0, new OrchestratorSettings { MaxAgentDelaySeconds = -5 }), Is.Zero, "and so is a negative one");
    }

    // ── TypicalPerSession ───────────────────────────────────────────────────

    [Test]
    public void Typical_session_cost_is_null_without_events_in_the_window()
    {
        Assert.That(Pacer.TypicalPerSession([], Day), Is.Null);
        Assert.That(Pacer.TypicalPerSession([[]], Day), Is.Null);
        Assert.That(Pacer.TypicalPerSession([[At(1, 0, 500)]], Day.AddHours(2)), Is.Null, "everything is before the window");
        Assert.That(Pacer.TypicalPerSession([[At(2, 0, 100)]], Day.AddHours(2)), Is.EqualTo(600), "an event right at the start counts");
    }

    [Test]
    public void A_ten_minute_bucket_sums_its_fresh_tokens_into_an_hourly_rate()
    {
        Assert.That(Pacer.TypicalPerSession([[At(10, 0, 100), At(10, 9, 100)]], Day), Is.EqualTo(1200), "both in 10:00-10:10: 200 per 10 minutes");
        Assert.That(Pacer.TypicalPerSession([[At(10, 0, 100, cacheRead: 1_000_000)]], Day), Is.EqualTo(600), "cache reads aren't fresh");
    }

    [Test]
    public void Buckets_are_aligned_to_the_clock_and_kept_apart_per_session()
    {
        // 10:08 and 10:12 straddle 10:10: two buckets (600/h and 1,800/h), with the other session's 1,200/h between them.
        Assert.That(Pacer.TypicalPerSession([[At(10, 8, 100), At(10, 12, 300)], [At(10, 5, 200)]], Day), Is.EqualTo(1200));
        // Two sessions in the same 10 minutes are two stretches of one session's cost, not one combined stretch.
        Assert.That(Pacer.TypicalPerSession([[At(10, 0, 100)], [At(10, 1, 100)]], Day), Is.EqualTo(600));
    }

    [Test]
    public void With_an_even_count_the_upper_middle_bucket_is_the_median()
    {
        IReadOnlyList<UsageEvent> s = [At(10, 0, 400), At(10, 10, 100), At(10, 20, 300), At(10, 30, 200)];
        Assert.That(Pacer.TypicalPerSession([s], Day), Is.EqualTo(1800), "600, 1,200, 1,800, 2,400 an hour: index two");
    }
}
