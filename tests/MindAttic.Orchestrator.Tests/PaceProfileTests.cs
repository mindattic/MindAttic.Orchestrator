using System.Text.Json;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T234: the selectable pace profile, linear (the default, unchanged) or front-loaded (target × elapsed^k).</summary>
public sealed class PaceProfileTests
{
    private static readonly DateTime Day = new(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc);

    private static readonly OrchestratorSettings Linear = new() { BlockBudget = 10_000 };
    private static readonly OrchestratorSettings Front = Linear with { PaceProfile = OrchestratorSettings.FrontLoadedProfile };

    private static UsageEvent E(double hour, long output) => new(Day.AddHours(hour), new TokenUsage(0, 0, 0, output));

    private static Pace Pace(double nowHour, OrchestratorSettings s, int subagents, params UsageEvent[] events) =>
        Pacer.Compute(BudgetCalculator.Compute(events, s, Day.AddHours(nowHour), Day, TimeZoneInfo.Utc), 1, null, s, Day.AddHours(nowHour),
            subagents: subagents);

    [Test]
    public void Linear_is_the_default_and_paces_as_before()
    {
        var s = new OrchestratorSettings();
        Assert.That((s.PaceProfile, s.FrontLoaded, s.PaceExponent, s.PaceCurveK), Is.EqualTo(("linear", false, 1.0, 0.55)));

        // 500 used of 9,800 (98% of 10k) with 4.5h left: what's left spread evenly, exactly as before the profiles.
        var p = Pace(10.5, Linear, 0, E(10, 500));
        Assert.That(p.SustainablePerHour, Is.EqualTo((9_800 - 500) / 4.5).Within(0.5));
        Assert.That((p.Profile, p.WindDownAt), Is.EqualTo(("linear", Pacer.WindDownShare)));
    }

    [Test]
    public void Front_loaded_spends_faster_early_in_the_window()
    {
        var linear = Pace(10.5, Linear, 0, E(10, 500));
        var front = Pace(10.5, Front, 0, E(10, 500));

        // The curve 30 minutes on (an hour in): 9,800 × 0.2^0.55, less the 500 spent, over half an hour.
        Assert.That(front.SustainablePerHour, Is.EqualTo((9_800 * Math.Pow(0.2, 0.55) - 500) / 0.5).Within(1));
        Assert.That(front.SustainablePerHour, Is.GreaterThan(linear.SustainablePerHour * 2));
        Assert.That(front.Profile, Is.EqualTo("front-loaded"));
    }

    [Test]
    public void Above_the_curve_it_holds_until_the_curve_catches_up_without_stopping()
    {
        // 6,000 spent half an hour in: well above the curve an hour in (~4,045), far under the target.
        var p = Pace(10.5, Front, 2, E(10, 3000), E(10.4, 3000));

        Assert.That(p.SustainablePerHour, Is.Zero);
        Assert.That(p.Stop, Is.False, "the stop is the target's alone; above the curve only holds");
        Assert.That(p.Mode, Is.EqualTo(PaceMode.Coast));
        Assert.That(p.Note, Does.Contain("front-loaded curve"));
        Assert.That(p.SubagentAllowance, Is.Zero);
        Assert.That(p.AgentDelaySeconds, Is.EqualTo(Front.MaxAgentDelaySeconds), "running agents are held while above the curve");

        // An hour and a half later the curve has passed 6,000: it lets go again.
        Assert.That(Pace(12, Front, 2, E(10, 3000), E(10.4, 3000)).SustainablePerHour, Is.GreaterThan(0));
    }

    [Test]
    public void Front_loaded_starts_no_new_subagent_past_85_percent()
    {
        // 86% spent with 30 minutes left: the rate still pays for more, but the front-loaded wind-down starts none.
        var front = Pace(14.5, Front, 2, E(10, 8_600));
        Assert.That(front.SustainablePerHour, Is.GreaterThan(0));
        Assert.That((front.WindDown, front.WindDownAt, front.SubagentAllowance, front.Stop), Is.EqualTo((true, 0.85, (int?)0, false)));
        Assert.That(front.SubagentNote, Does.Contain("wind-down line"));
        // Written the same on any Windows language (M.ORCH-T405): a culture with "85 %" percents doesn't change it.
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            var spaced = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.GetCultureInfo("en-US").Clone();
            spaced.NumberFormat.PercentPositivePattern = 0; // "85 %"
            System.Globalization.CultureInfo.CurrentCulture = spaced;
            Assert.That(front.SubagentNote, Does.Contain("past its 85% wind-down line"));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = culture; }
        Assert.That(front.WindDownHold, Does.Contain("85"));

        // Linear is untouched: its wind-down stays at 90%.
        var linear = Pace(14.5, Linear, 2, E(10, 8_600));
        Assert.That(linear.WindDown, Is.False);
        Assert.That(linear.SubagentAllowance, Is.GreaterThan(0));
    }

    [Test]
    public void The_curve_never_aims_past_the_target_and_the_target_still_stops()
    {
        var start = Day.AddHours(10);
        var reset = start.AddHours(5);
        // Over the last stretch the curve's rate is the linear one: both land on the target at the reset, never above it.
        for (var left = 0.05; left <= 0.5; left += 0.05)
        {
            var now = reset.AddHours(-left);
            Assert.That(Pacer.SustainableRate(10_000, 9_000, 0.98, start, reset, now, 0.55),
                Is.EqualTo(Pacer.SustainableRate(10_000, 9_000, 0.98, start, reset, now, 1)).Within(1e-6));
        }
        // Everywhere in the window the curve aims at no more than the target.
        for (var h = 0.0; h < 5; h += 0.25)
            Assert.That(Pacer.SustainableRate(10_000, 0, 0.98, start, reset, start.AddHours(h), 0.55) * Math.Min(0.5, 5 - h),
                Is.LessThanOrEqualTo(9_800 + 1e-6));

        var stopped = Pace(14, Front, 0, E(10, 9_800));
        Assert.That((stopped.Stop, stopped.SubagentAllowance), Is.EqualTo((true, (int?)0)));
    }

    [Test]
    public void Switching_mid_window_follows_the_new_curve_at_the_next_check()
    {
        // Same window, same spend: the next check paces on whichever profile settings.json holds now.
        var events = new[] { E(10, 3000), E(10.4, 3000) };
        Assert.That(Pace(10.5, Front, 0, events).SustainablePerHour, Is.Zero);
        Assert.That(Pace(10.5, Linear, 0, events).SustainablePerHour, Is.EqualTo((9_800 - 6_000) / 4.5).Within(0.5),
            "back on linear, what's left is spread evenly to the reset");
    }

    [Test]
    public void The_chart_draws_the_active_profiles_curve()
    {
        var start = Day.AddHours(10);
        var straight = new Utilization(start, start.AddHours(5), start.AddHours(1), 98, [], 10, 5, 50, TimeSpan.Zero);
        var curved = straight with { Exponent = 0.55 };

        Assert.That(straight.Curved, Is.False);
        Assert.That(curved.Curved, Is.True);
        Assert.That(straight.PaceAt(start.AddHours(2.5)), Is.EqualTo(49).Within(1e-9));
        Assert.That(curved.PaceAt(start.AddHours(2.5)), Is.EqualTo(98 * Math.Pow(0.5, 0.55)).Within(1e-9));
        Assert.That(curved.PaceAt(start.AddHours(5)), Is.EqualTo(98).Within(1e-9));
    }

    [TestCase("linear", "linear")]
    [TestCase("Front-Loaded", "front-loaded")]
    [TestCase("frontloaded", "front-loaded")]
    [TestCase("front", "front-loaded")]
    [TestCase("steep", null)]
    public void Typed_profiles_are_read(string typed, string? saved) =>
        Assert.That(OrchestratorSettings.ParsePaceProfile(typed), Is.EqualTo(saved));

    [Test]
    public void The_profile_is_saved_and_a_typo_paces_linearly()
    {
        var json = JsonSerializer.Serialize(Front with { PaceCurveK = 0.4 });
        Assert.That(json, Does.Contain("\"PaceProfile\":\"front-loaded\"").And.Contain("\"PaceCurveK\":0.4"));
        Assert.That(json, Does.Not.Contain("FrontLoaded\"").And.Not.Contain("PaceExponent"));

        var typo = new OrchestratorSettings { PaceProfile = "frnt", PaceCurveK = 0.3 };
        Assert.That((typo.FrontLoaded, typo.PaceExponent), Is.EqualTo((false, 1.0)));
        Assert.That(new OrchestratorSettings { PaceProfile = "front-loaded", PaceCurveK = 0.01 }.PaceExponent, Is.EqualTo(0.1));
    }
}
