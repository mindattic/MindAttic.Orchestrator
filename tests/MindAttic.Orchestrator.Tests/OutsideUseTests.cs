using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>A member's own use outside the orchestrator (M.ORCH-T682): the margin it's judged by, and what each login learns
/// from it, in both directions and always within the settings' bounds.</summary>
public sealed class OutsideUseTests
{
    private static readonly DateTime T0 = new(2026, 10, 23, 16, 0, 0, DateTimeKind.Utc);
    private static readonly OrchestratorSettings Defaults = new();
    private const string Key = "a@x.com";

    private static OutsideStretch Stretch(DateTime to, double five, bool outside = true, double minutes = 10) =>
        new(to.AddMinutes(-minutes), to, five, 0, five, 0) { IsOutside = outside };

    /// <summary>An episode: a stretch of <paramref name="five"/> outside points at <see cref="T0"/>, then quiet until it ends.</summary>
    private static List<string> Episode(OutsideUseWatch watch, double five, OrchestratorSettings? settings = null, DateTime? at = null)
    {
        var log = new List<string>();
        var start = at ?? T0;
        var s = settings ?? Defaults;
        Assert.That(watch.Observe(Key, "Honey", [], Stretch(start, five), (_, _) => null, s, start, log), Is.EqualTo(OutsideChange.Started));
        Assert.That(watch.Observe(Key, "Honey", [], null, (_, _) => null, s, start.AddMinutes(s.OutsideUseQuietMinutes), log), Is.EqualTo(OutsideChange.Ended));
        return log;
    }

    [Test]
    public void The_margin_is_the_rounding_of_two_readings_plus_a_share_of_what_the_clis_explain()
    {
        UsageSample At(int minute, double percent, long fresh) => new(T0.AddMinutes(minute), percent, T0.AddHours(1), fresh);
        // Idle CLIs: 2 points is rounding, 3 is someone.
        Assert.That(OutsideUse.Judge([At(-20, 10, 0), At(0, 12, 0)], null, null, T0)!.IsOutside, Is.False);
        Assert.That(OutsideUse.Judge([At(-20, 10, 0), At(0, 13, 0)], null, null, T0)!.IsOutside, Is.True);
        // CLIs that spent 20 points: 7 more is within 2 + 25% of 20; 8 isn't.
        Assert.That(OutsideUse.Judge([At(-20, 10, 0), At(0, 37, 2_000_000)], 100_000, null, T0)!.IsOutside, Is.False);
        Assert.That(OutsideUse.Judge([At(-20, 10, 0), At(0, 38, 2_000_000)], 100_000, null, T0)!.IsOutside, Is.True);
        // CLI spend with no tokens per percent learned can't be told apart: no judgement at all.
        Assert.That(OutsideUse.Judge([At(-20, 10, 0), At(0, 30, 2_000_000)], null, null, T0), Is.Null);
    }

    [Test]
    public void A_member_who_used_most_of_the_room_gets_a_lower_ceiling_next_time_and_one_who_barely_used_it_a_higher_one()
    {
        var watch = new OutsideUseWatch();
        // Held to 90: 10 points of room, 9 of them used.
        var log = Episode(watch, 9);
        Assert.That(watch.Find(Key)!.LearnedCeiling, Is.EqualTo(88));
        Assert.That(log, Has.Some.Contains("Honey's outside-use ceiling lowered from 90 to 88: Honey used 9 of the 10 points of room it left"));

        // Next time 12 points of room, 3 used: back up.
        log = Episode(watch, 3, at: T0.AddHours(2));
        Assert.That(watch.Find(Key)!.LearnedCeiling, Is.EqualTo(89));
        Assert.That(log, Has.Some.Contains("raised from 88 to 89"));
    }

    [Test]
    public void The_learned_ceiling_never_leaves_its_bounds()
    {
        var watch = new OutsideUseWatch();
        var tight = Defaults with { OutsideUseCeilingMinPercent = 89, OutsideUseCeilingMaxPercent = 90 };
        Episode(watch, 9, tight);
        Assert.That(watch.Find(Key)!.LearnedCeiling, Is.EqualTo(89));
        Episode(watch, 10, tight, T0.AddHours(2));
        Assert.That(watch.Find(Key)!.LearnedCeiling, Is.EqualTo(89), "not below the minimum");
        Episode(watch, 0.1, tight, T0.AddHours(4));
        Episode(watch, 0.1, tight, T0.AddHours(6));
        Assert.That(watch.Find(Key)!.LearnedCeiling, Is.EqualTo(90), "not above the maximum");
        // A bound tightened after it was learned still holds it.
        Assert.That(OutsideTuning.For(Defaults with { OutsideUseCeilingMaxPercent = 85 }, watch.Find(Key)).Ceiling, Is.EqualTo(85));
    }

    [Test]
    public void The_quiet_period_follows_the_gaps_between_their_bursts_both_ways()
    {
        var watch = new OutsideUseWatch();
        watch.Observe(Key, "Honey", [], null, (_, _) => null, Defaults, T0);
        watch.Find(Key)!.Gaps.AddRange([30, 32, 35]);
        var log = Episode(watch, 9);
        Assert.That(watch.Find(Key)!.LearnedQuietMinutes, Is.EqualTo(40), "four gaps in five covered, plus five minutes");
        Assert.That(log, Has.Some.Contains("Honey's quiet period raised from 20 min to 40 min: Honey's gaps between bursts run to 35 minutes"));

        watch.Find(Key)!.Gaps.Clear();
        watch.Find(Key)!.Gaps.AddRange([6, 7, 7]);
        log = Episode(watch, 9, Defaults with { OutsideUseQuietMinutes = 40 }, T0.AddHours(3));
        Assert.That(watch.Find(Key)!.LearnedQuietMinutes, Is.EqualTo(12));
        Assert.That(log, Has.Some.Contains("lowered from 40 min to 12 min"));
    }

    [Test]
    public void The_noise_margin_grows_after_a_lone_blip_and_shrinks_when_idle_stretches_sit_well_under_it()
    {
        var watch = new OutsideUseWatch();
        // 2.5 points once, never again: likely noise.
        var log = Episode(watch, 2.5);
        Assert.That(watch.Find(Key)!.LearnedMargin, Is.EqualTo(2.5));
        Assert.That(log, Has.Some.Contains("noise margin raised from 2 to 2.5: a lone rise of 2.5 points"));

        // Three hours of readings with nobody on it, none rising more than half a point unexplained: narrower.
        log = [];
        for (var i = 1; i <= OutsideUseWatch.NoiseNeeded; i++)
            watch.Observe(Key, "Honey", [], Stretch(T0.AddHours(1).AddMinutes(5 * i), 0.5, outside: false), (_, _) => null, Defaults, T0.AddHours(1).AddMinutes(5 * i), log);
        Assert.That(watch.Find(Key)!.LearnedMargin, Is.EqualTo(2.25));
        Assert.That(log, Has.Some.Contains("noise margin lowered from 2.5 to 2.25"));
    }

    [Test]
    public void The_headroom_grows_when_their_use_outran_the_projection_and_shrinks_when_it_fell_well_short()
    {
        var reset = T0.AddHours(5);
        List<UsageSample> Readings(params int[] minutes) => [.. minutes.Select(m => new UsageSample(T0.AddMinutes(m), 20 + m / 5.0, reset, 0))];

        // 3 points in its first 10 minutes (0.3 a minute), then 10 more in the next 10: 13 where 6 were foretold.
        var watch = new OutsideUseWatch();
        var log = new List<string>();
        watch.Observe(Key, "Honey", Readings(-10, 0), Stretch(T0, 3), (_, _) => null, Defaults, T0, log);
        watch.Observe(Key, "Honey", Readings(-10, 0, 10), Stretch(T0.AddMinutes(10), 13, minutes: 20), (_, to) => Stretch(to, 10), Defaults, T0.AddMinutes(10), log);
        watch.Observe(Key, "Honey", Readings(-10, 0, 10), null, (_, _) => null, Defaults, T0.AddMinutes(30), log);
        Assert.That(watch.Find(Key)!.LearnedHeadroom, Is.EqualTo(3));
        Assert.That(log, Has.Some.Contains("Honey's outside-use headroom raised from 2 to 3"));

        // 6 points in its first 10 minutes (0.6 a minute), then one more over the next 30: 7 where 24 were foretold.
        watch = new OutsideUseWatch();
        log = [];
        watch.Observe(Key, "Honey", Readings(-10, 0), Stretch(T0, 6), (_, _) => null, Defaults, T0, log);
        watch.Observe(Key, "Honey", Readings(-10, 0, 30), Stretch(T0.AddMinutes(30), 7, minutes: 40), (_, to) => Stretch(to, 1), Defaults, T0.AddMinutes(30), log);
        watch.Observe(Key, "Honey", Readings(-10, 0, 30), null, (_, _) => null, Defaults, T0.AddMinutes(50), log);
        Assert.That(watch.Find(Key)!.LearnedHeadroom, Is.EqualTo(1.5));
        Assert.That(log, Has.Some.Contains("headroom lowered from 2 to 1.5"));
    }

    [Test]
    public void The_settings_and_the_cli_read_bounds_as_min_dash_max()
    {
        Assert.That(OutsideTuning.ParseBounds("70-95", 1, 100), Is.EqualTo((70, 95)));
        Assert.That(OutsideTuning.ParseBounds(" 10 - 60 ", 1, 240), Is.EqualTo((10, 60)));
        Assert.That(OutsideTuning.ParseBounds("95-70", 1, 100), Is.Null);
        Assert.That(OutsideTuning.ParseBounds("0-95", 1, 100), Is.Null);
        Assert.That(OutsideTuning.ParseBounds("90", 1, 100), Is.Null);
    }
}
