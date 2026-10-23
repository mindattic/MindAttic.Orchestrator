using System.Text.Json;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Setup;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>
/// M.ORCH-T438: the hooks refuse every tool call once Claude reports the week at its stop line (<see cref="OrchestratorSettings.WeeklyStopPercent"/>, 100 minus the weekly reserve: M.ORCH-T609); the
/// daemon has to stop at the same point (and lift it at the weekly reset), or autopilot hands out tickets and the dashboard
/// says On pace while every call is refused. Below that line the week is still never paced.
/// </summary>
public sealed class WeeklyStopTests
{
    private static readonly DateTime Now = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WeekReset = Now.AddDays(2);

    private static readonly OrchestratorSettings Settings = new() { BlockBudget = 1_000_000 };

    private static BudgetStatus Budget(double weekPercent, OrchestratorSettings? settings = null, DateTime? now = null)
    {
        var at = now ?? Now;
        var reported = new ReportedUsage(new ReportedWindow(20, at.AddHours(3)), new ReportedWindow(weekPercent, WeekReset), at.AddMinutes(-1));
        UsageEvent[] events = [new(at.AddHours(-1), new TokenUsage(0, 0, 0, 10_000))];
        return BudgetCalculator.Compute(events, settings ?? Settings, at, at.Date, TimeZoneInfo.Utc, reported: reported);
    }

    [Test]
    public void Claude_reporting_the_week_at_the_stop_line_stops_the_pace_until_the_weekly_reset()
    {
        var b = Budget(Settings.WeeklyStopPercent);
        var p = Pacer.Compute(b, 2, null, Settings, Now);

        Assert.That(b.WeeklyStop, Is.Not.Null, "the same line the hooks refuse at");
        Assert.That(p.Stop, Is.True);
        Assert.That((p.Mode, p.SessionCap, p.SubagentAllowance), Is.EqualTo((PaceMode.Over, 0, (int?)0)));
        Assert.That(p.Governing, Is.EqualTo("weekly"));
        Assert.That(p.ResetUtc, Is.EqualTo(WeekReset), "lifted at the weekly reset, not the 5-hour block's end");
        Assert.That(p.Note, Does.Contain("week at 90%").And.Not.Contain("On pace"));
    }

    [Test]
    public void Below_the_stop_line_the_week_is_never_paced()
    {
        // The user's rule: no slowing, holds, session caps or subagent limits for the week below the stop.
        var p = Pacer.Compute(Budget(Settings.WeeklyStopPercent - 1), 1, 1_000, Settings, Now);

        Assert.That(p.Stop, Is.False);
        Assert.That(p.Governing, Is.EqualTo("5-hour"));
        Assert.That((p.Mode, p.AgentDelaySeconds), Is.EqualTo((PaceMode.Open, 0.0)));
    }

    [Test]
    public void The_weekly_stop_holds_with_pacing_off()
    {
        var off = Settings with { Pacing = false };
        var p = Pacer.Compute(Budget(100, off), 1, null, off, Now);

        Assert.That((p.Stop, p.Governing), Is.EqualTo((true, "weekly")), "the stop never depends on pacing, as the hooks' doesn't");
    }

    [Test]
    public void Past_the_weekly_reset_the_stop_is_lifted()
    {
        var b = Budget(Settings.WeeklyStopPercent);
        var after = WeekReset.AddMinutes(1);

        Assert.That(b.WeeklyStopped(after), Is.False);
        Assert.That(Pacer.Compute(b, 1, null, Settings, after).Governing, Is.Not.EqualTo("weekly"));
    }

    [Test]
    public void The_stop_reaches_rl_status_with_the_weekly_window_and_reset()
    {
        var path = Path.Combine(Path.GetTempPath(), "orch-weekstop-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var b = Budget(Settings.WeeklyStopPercent);
            new RateLimitStatusWriter(path) { UtcNow = () => Now }.Write(b, Pacer.Compute(b, 1, null, Settings, Now));
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;

            Assert.That(root.GetProperty("stop").GetBoolean(), Is.True);
            Assert.That(root.GetProperty("stop_window").GetString(), Is.EqualTo("weekly"));
            Assert.That(root.GetProperty("stop_until").GetString(), Is.EqualTo(WeekReset.ToString("yyyy-MM-ddTHH:mm:ss") + "Z"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Next_claim_hands_nothing_out_at_the_weekly_stop()
    {
        var dir = Path.Combine(Path.GetTempPath(), "orch-weekstop-" + Guid.NewGuid().ToString("N"));
        try
        {
            var b = Budget(Settings.WeeklyStopPercent);
            var plan = Scheduler.Plan(new Snapshot(Now, [], []) { Budget = b, Pace = Pacer.Compute(b, 1, null, Settings, Now) }, Settings, []);
            var (handed, why) = Autopilot.Next(plan, Settings, new JsonStore(dir), ["*"], null, claim: true, Now);

            Assert.That(plan.Stopped, Is.True);
            Assert.That(handed, Is.Empty);
            Assert.That(why, Does.Contain("weekly"));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public void The_weekly_reserve_sets_the_stop_line_five_percent_by_default()
    {
        // M.ORCH-T609: every CLI on an account stops at 100 minus the reserve of its week instead of burning it all.
        Assert.That((new OrchestratorSettings().WeeklyReservePercent, new OrchestratorSettings().WeeklyStopPercent), Is.EqualTo((10, 90)));
        Assert.That(Budget(90).WeeklyStopped(Now), Is.True, "90% is the default line");
        Assert.That(Budget(89.5).WeeklyStopped(Now), Is.False);

        var two = Settings with { WeeklyReservePercent = 2 };
        Assert.That(Budget(96, two).WeeklyStopped(Now), Is.False, "a 2% reserve lets the week run to 98%");
        Assert.That(Budget(98, two).WeeklyStopped(Now), Is.True);
        Assert.That(Pacer.Compute(Budget(98, two), 1, null, two, Now).Note, Does.Contain("stops at 98%"));

        Assert.That((Settings with { WeeklyReservePercent = 0 }).WeeklyStopPercent, Is.EqualTo(100), "0 keeps nothing back");
        Assert.That((Settings with { WeeklyReservePercent = 80 }).WeeklyStopPercent, Is.EqualTo(50), "a hand edit past 50 is kept to 50");
        Assert.That((Settings with { WeeklyReservePercent = -3 }).WeeklyStopPercent, Is.EqualTo(100));
    }

    [Test]
    public void The_stop_line_reaches_rl_status_for_the_hooks_to_read_live()
    {
        var path = Path.Combine(Path.GetTempPath(), "orch-weekstop-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var three = Settings with { WeeklyReservePercent = 3 };
            var b = Budget(50, three);
            new RateLimitStatusWriter(path) { UtcNow = () => Now }.Write(b, Pacer.Compute(b, 1, null, three, Now));
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            Assert.That(doc.RootElement.GetProperty("weekly_stop_percent").GetInt32(), Is.EqualTo(97));
            Assert.That(Sessions.LoginPace.Read(path)?.WeeklyStopPercent, Is.EqualTo(97), "the Accounts view reads it back per login");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void A_held_stop_lifts_when_the_reserve_is_lowered_below_it()
    {
        var agg = new StatusAggregator(new OrchestratorOptions());
        Assert.That(agg.HoldWeeklyStop(Budget(96), Now).WeeklyStopped(Now), Is.True, "96% is past the default 95% line");
        var two = Settings with { WeeklyReservePercent = 2 };
        Assert.That(agg.HoldWeeklyStop(Budget(96, two), Now).WeeklyStopped(Now), Is.False, "at a 2% reserve the line is 98%: the week has room");
    }

    [Test]
    public void An_idle_tabs_lower_weekly_percent_doesnt_lift_the_stop_before_the_reset()
    {
        // Every open session restamps claude-usage.json with its own last weekly percent; the hooks latch the stop, so must the daemon.
        var agg = new StatusAggregator(new OrchestratorOptions());
        Assert.That(agg.HoldWeeklyStop(Budget(Settings.WeeklyStopPercent), Now).WeeklyStopped(Now), Is.True);

        var later = Now.AddMinutes(5);
        Assert.That(agg.HoldWeeklyStop(Budget(85, now: later), later).WeeklyStopped(later), Is.True, "a stale lower report");
        var noReport = BudgetCalculator.Compute([], Settings, later, later.Date, TimeZoneInfo.Utc);
        Assert.That(agg.HoldWeeklyStop(noReport, later).WeeklyStop?.ResetUtc, Is.EqualTo(WeekReset), "no report at all");

        var reset = WeekReset.AddMinutes(1);
        Assert.That(agg.HoldWeeklyStop(BudgetCalculator.Compute([], Settings, reset, reset.Date, TimeZoneInfo.Utc), reset).WeeklyStop, Is.Null,
            "lifted at the weekly reset");
    }
}
