using System.Text.Json;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using MindAttic.Workflow.Tickets;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class PacerTests
{
    private static readonly DateTime Day = new(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc);

    private static UsageEvent E(double hour, long output) => new(Day.AddHours(hour), new TokenUsage(0, 0, 0, output));

    private static (BudgetStatus Budget, OrchestratorSettings Settings) Budget(double nowHour, OrchestratorSettings settings, params UsageEvent[] events) =>
        (BudgetCalculator.Compute(events, settings, Day.AddHours(nowHour), Day, TimeZoneInfo.Utc), settings);

    private static Pace Pace(double nowHour, int active, OrchestratorSettings settings, params UsageEvent[] events)
    {
        var (b, s) = Budget(nowHour, settings, events);
        return Pacer.Compute(b, active, null, s, Day.AddHours(nowHour));
    }

    [Test]
    public void Under_pace_every_slot_is_open()
    {
        // 500 used of 9,500 (95% of 10k) with 4.5h left: 2,000/h fits; 4 sessions burn 1,000/h together.
        var p = Pace(10.5, 4, new OrchestratorSettings { BlockBudget = 10_000 }, E(10, 500));

        Assert.That(p.Mode, Is.EqualTo(PaceMode.Open));
        Assert.That(p.SessionCap, Is.EqualTo(4));
        Assert.That(p.Advise, Is.False);
    }

    [Test]
    public void Burning_twice_the_sustainable_rate_halves_the_sessions()
    {
        // 2,000 used in the first 30 minutes (4,000/h); 9,000 left over 4.5h fits 2,000/h. Four sessions at 1,000/h each: two fit.
        var p = Pace(10.5, 4, new OrchestratorSettings { BlockBudget = 11_000, PaceTargetPercent = 100 }, E(10, 1000), E(10.4, 1000));

        Assert.That(p.SustainablePerHour, Is.EqualTo(2000).Within(1));
        Assert.That(p.RecentPerHour, Is.EqualTo(4000).Within(1));
        Assert.That((p.Mode, p.SessionCap), Is.EqualTo((PaceMode.Throttled, 2)));
        Assert.That(p.Advise, Is.True, "running agents are asked to conserve while they outrun the window");
        Assert.That(p.Governing, Is.EqualTo("5-hour"));
    }

    [Test]
    public void Near_the_reset_with_budget_left_it_opens_back_up()
    {
        // Half the block left with 30 minutes to go: spend it rather than leave it on the table.
        var p = Pace(14.5, 1, new OrchestratorSettings { BlockBudget = 10_000, PaceTargetPercent = 100 }, E(10, 4000), E(14 + 10.0 / 60, 1000));

        Assert.That(p.SustainablePerHour, Is.EqualTo(10_000).Within(1));
        Assert.That((p.Mode, p.SessionCap), Is.EqualTo((PaceMode.Open, 4)));
    }

    [Test]
    public void At_the_limit_nothing_starts()
    {
        var p = Pace(10.5, 1, new OrchestratorSettings { BlockBudget = 10_000 }, E(10, 10_000));

        Assert.That((p.Mode, p.SessionCap), Is.EqualTo((PaceMode.Over, 0)));
        Assert.That(p.AgentNote, Does.Contain("stop at a clean point"));
    }

    [Test]
    public void Past_the_target_but_under_the_limit_it_coasts()
    {
        var p = Pace(12, 1, new OrchestratorSettings { BlockBudget = 10_000 }, E(10, 9600));

        Assert.That((p.Mode, p.SessionCap), Is.EqualTo((PaceMode.Coast, 0)));
        Assert.That(p.Advise, Is.True);
    }

    [Test]
    public void The_tighter_weekly_window_governs()
    {
        // Week resets Wednesday 00:00 (13.5h away) with 9,000 of 100k left: 667/h, far below the block's room.
        var settings = new OrchestratorSettings { BlockBudget = 1_000_000, WeeklyBudget = 100_000, WeeklyReset = "Wed 00:00", PaceTargetPercent = 100 };
        var p = Pace(10.5, 1, settings, E(-24, 90_000), E(10, 1000));

        Assert.That(p.Governing, Is.EqualTo("weekly"));
        Assert.That(p.SustainablePerHour, Is.EqualTo(9000 / 13.5).Within(1));
        Assert.That((p.Mode, p.SessionCap), Is.EqualTo((PaceMode.Coast, 0)), "one session at 2,000/h doesn't fit 667/h");
    }

    [Test]
    public void Without_a_limit_or_with_pacing_off_it_stands_aside()
    {
        Assert.That(Pace(10.5, 1, new OrchestratorSettings(), E(10, 500)).Mode, Is.EqualTo(PaceMode.Unpaced), "no budget and nothing learned");
        Assert.That(Pace(10.5, 1, new OrchestratorSettings { BlockBudget = 10_000, Pacing = false }, E(10, 500)).Mode, Is.EqualTo(PaceMode.Unpaced));
    }

    [Test]
    public void With_nothing_running_the_usual_session_cost_decides()
    {
        var (b, s) = Budget(10.5, new OrchestratorSettings { BlockBudget = 10_000, PaceTargetPercent = 100 }, E(9, 500));

        Assert.That(Pacer.Compute(b, 0, 2_000, s, Day.AddHours(10.5)).SessionCap, Is.EqualTo(1), "9,500 over the 3.5h left fits one 2,000/h session");
        Assert.That(Pacer.Compute(b, 0, null, s, Day.AddHours(10.5)).SessionCap, Is.EqualTo(1), "no history: start one and measure");
    }

    [Test]
    public void Usual_session_cost_is_the_median_active_ten_minutes()
    {
        IReadOnlyList<UsageEvent> a = [E(10, 100), E(10.5, 300)];
        IReadOnlyList<UsageEvent> b = [E(11, 200), E(1, 999_999)];

        Assert.That(Pacer.TypicalPerSession([a, b], Day.AddHours(5)), Is.EqualTo(1200), "600, 1,200 and 1,800 an hour; the 1:00 burst is before the window");
    }

    // ── Scheduler and status file ───────────────────────────────────────────

    private static Ticket T(string id) => new(id, "2026-10-01", "feature", "open", $"summary of {id}", "");

    private static RepoStatus Repo(string name, RepoState state, params Ticket[] open) =>
        new(name, $@"D:\W\{name}", state, [], [], open, null, default);

    private static readonly RepoStatus[] FourRepos =
    [
        Repo("A", RepoState.Working), Repo("B", RepoState.Dormant, T("B-T1")), Repo("C", RepoState.Dormant, T("C-T1")), Repo("D", RepoState.Dormant, T("D-T1")),
    ];

    [Test]
    public void The_scheduler_fills_only_what_the_pace_allows()
    {
        var pace = new Pace(PaceMode.Throttled, 2, 2000, 4000, 1000, "5-hour", Day, "Pacing: capped at 2 of 4 sessions.");

        var plan = Scheduler.Plan(new Snapshot(Day, FourRepos, []) { Pace = pace }, new OrchestratorSettings(), []);

        Assert.That(plan.Cap, Is.EqualTo(2));
        Assert.That(plan.Proposals, Has.Count.EqualTo(1), "one session is working, so one more fits");
        Assert.That(plan.Hold, Is.Null);
    }

    [Test]
    public void Paced_the_warn_line_warns_but_doesnt_hold()
    {
        var (warn, s) = Budget(10.5, new OrchestratorSettings { BlockBudget = 10_000 }, E(10, 8500));
        Assume.That(warn.Level, Is.EqualTo(BudgetLevel.Warn));
        var open = new Pace(PaceMode.Open, 4, 2000, 100, 100, "5-hour", Day, "On pace.");

        var paced = Scheduler.Plan(new Snapshot(Day, FourRepos, []) { Budget = warn, Pace = open }, s, []);
        var unpaced = Scheduler.Plan(new Snapshot(Day, FourRepos, []) { Budget = warn }, s, []);

        Assert.That(paced.Proposals, Is.Not.Empty);
        Assert.That(unpaced.Proposals, Is.Empty, "with pacing off the warn line still holds");
    }

    [Test]
    public void Coasting_or_over_holds_and_says_why()
    {
        var coast = new Pace(PaceMode.Coast, 0, 0, 3000, 3000, "5-hour", Day, "Coasting: the 5-hour window is at its 95% target.");

        var plan = Scheduler.Plan(new Snapshot(Day, FourRepos, []) { Pace = coast }, new OrchestratorSettings(), []);

        Assert.That(plan.Proposals, Is.Empty);
        Assert.That(plan.Hold, Does.Contain("Coasting"));
    }

    [Test]
    public void The_status_file_tells_running_agents_to_conserve()
    {
        var path = Path.Combine(Path.GetTempPath(), "rl-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var (b, s) = Budget(10.5, new OrchestratorSettings { BlockBudget = 11_000, PaceTargetPercent = 100 }, E(10, 1000), E(10.4, 1000));
            var pace = Pacer.Compute(b, 4, null, s, Day.AddHours(10.5));
            new RateLimitStatusWriter(path).Write(b, pace);

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            Assert.That(doc.RootElement.GetProperty("pace").GetString(), Is.EqualTo("throttled"));
            Assert.That(doc.RootElement.GetProperty("pace_note").GetString(), Does.Contain("don't spawn subagents"));
        }
        finally { File.Delete(path); }
    }
}
