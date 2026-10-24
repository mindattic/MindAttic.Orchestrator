using MindAttic.Orchestrator.Scheduling;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T495: one repo's /allocate claimed all 8 subagent places at once while every other CLI got none.</summary>
public sealed class SubagentShareTests
{
    private static Dictionary<int, int> Running(params (int Cli, int Count)[] r) => r.ToDictionary(x => x.Cli, x => x.Count);

    [Test]
    public void Alone_a_cli_may_take_every_place_nobody_else_wants() =>
        Assert.That(SubagentShare.Room(8, 0, 1, Running(), []), Is.EqualTo(8));

    [Test]
    public void With_another_cli_waiting_each_gets_half()
    {
        Assert.That(SubagentShare.Room(8, 0, 1, Running(), [2]), Is.EqualTo(4), "the other CLI's share is kept for it");
        Assert.That(SubagentShare.Room(8, 4, 2, Running((1, 4)), [1]), Is.EqualTo(4), "and the other takes it");
    }

    [Test]
    public void A_cli_over_its_share_starts_nothing_while_another_waits_and_the_next_free_place_goes_to_the_one_under()
    {
        // A borrowed all 8 while B wanted nothing; now B waits and one of A's has finished.
        Assert.That(SubagentShare.Room(8, 7, 1, Running((1, 7)), [2]), Is.Zero);
        Assert.That(SubagentShare.Room(8, 7, 2, Running((1, 7)), [1]), Is.EqualTo(1));
    }

    [Test]
    public void A_cli_that_wants_nothing_more_lends_what_it_isnt_using()
    {
        // B runs one and wants no more: A may use the other 7, not just its half.
        Assert.That(SubagentShare.Room(8, 1, 1, Running((2, 1)), []), Is.EqualTo(7));
        // Three CLIs, one idle: the two that want them split the allowance.
        Assert.That(SubagentShare.Room(9, 0, 1, Running(), [2]), Is.EqualTo(5), "4 kept for the other, the rest is 9 - 4");
    }

    [Test]
    public void With_more_clis_than_places_each_still_gets_one_in_turn() =>
        Assert.That(SubagentShare.Room(2, 1, 3, Running((1, 1)), [1, 2]), Is.EqualTo(1), "a share is never under one; the allowance still caps the total");

    [Test]
    public void Never_past_the_allowance_across_every_cli() =>
        Assert.That(SubagentShare.Room(4, 4, 1, Running((2, 4)), []), Is.Zero);

    /// <summary>M.ORCH-T558: a CLI's /allocate warms up, one more subagent each minute since it turned on, up to its share.</summary>
    [Test]
    public void A_cli_warms_up_one_more_subagent_each_minute_since_its_allocate_turned_on()
    {
        var on = new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);
        Assert.That(new[] { 0, 59, 60, 150, 600 }.Select(s => SubagentShare.WarmUp(on, on.AddSeconds(s))), Is.EqualTo(new[] { 1, 1, 2, 3, 11 }));
        Assert.That(SubagentShare.WarmUp(null, on), Is.EqualTo(int.MaxValue), "not known to be on: no warm-up");
        Assert.That(SubagentShare.WarmUp(on.AddMinutes(1), on), Is.EqualTo(1), "a clock behind the mark still lets one start");
        Assert.That(SubagentShare.Room(12, 0, 1, Running(), [], warm: 1), Is.EqualTo(1), "alone at minute 0: one, not all 12");
        Assert.That(SubagentShare.Room(12, 1, 1, Running((1, 1)), [], warm: 3), Is.EqualTo(2), "minute 2: up to 3");
        Assert.That(SubagentShare.Room(12, 0, 1, Running(), [2], warm: 20), Is.EqualTo(6), "warmed past its share: the share still holds");
    }
}
