using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Settings;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T1401: a member's "until" date drops its tab from the roster once passed, and clearing it undoes that.</summary>
public sealed class MemberUntilTests
{
    private const string Setting = "honey@mindattic.com=2026-11-30;Waffle@MindAttic.com=2026-11-30T12:00";

    [Test]
    public void A_bare_day_lapses_at_the_end_of_that_day()
    {
        Assert.That(MemberUntil.IsLapsed(Setting, "honey@mindattic.com", new DateTime(2026, 11, 30, 23, 59, 59)), Is.False);
        Assert.That(MemberUntil.IsLapsed(Setting, "honey@mindattic.com", new DateTime(2026, 12, 1, 0, 0, 0)), Is.True);
    }

    [Test]
    public void A_given_time_lapses_at_that_moment()
    {
        Assert.That(MemberUntil.IsLapsed(Setting, "waffle@mindattic.com", new DateTime(2026, 11, 30, 12, 0, 0)), Is.False);
        Assert.That(MemberUntil.IsLapsed(Setting, "waffle@mindattic.com", new DateTime(2026, 11, 30, 12, 0, 1)), Is.True);
    }

    [Test]
    public void Emails_match_whatever_their_case()
    {
        Assert.That(MemberUntil.IsLapsed(Setting, "WAFFLE@mindattic.com", new DateTime(2027, 1, 1)), Is.True);
    }

    [Test]
    public void A_member_with_no_date_never_lapses()
    {
        Assert.That(MemberUntil.IsLapsed(Setting, "amy@mindattic.com", new DateTime(2030, 1, 1)), Is.False);
        Assert.That(MemberUntil.IsLapsed(Setting, null, new DateTime(2030, 1, 1)), Is.False);
    }

    [Test]
    public void Clearing_the_date_brings_the_member_back()
    {
        var now = new DateTime(2026, 12, 15);
        Assert.That(MemberUntil.Lapsed(Setting, now), Is.EquivalentTo(new[] { "honey@mindattic.com", "waffle@mindattic.com" }));
        Assert.That(MemberUntil.Lapsed("waffle@mindattic.com=2026-11-30", now), Is.EquivalentTo(new[] { "waffle@mindattic.com" }));
        Assert.That(MemberUntil.Lapsed("", now), Is.Empty);
        Assert.That(MemberUntil.Lapsed(null, now), Is.Empty);
    }

    [Test]
    public void Malformed_entries_are_skipped_and_hide_nobody()
    {
        var parsed = MemberUntil.Parse("nonsense; =2026-11-30; honey@mindattic.com=soon; waffle@mindattic.com=2026-13-40; amy@mindattic.com=2026-11-30");
        Assert.That(parsed.Keys, Is.EquivalentTo(new[] { "amy@mindattic.com" }));
    }

    [Test]
    public void Entries_may_be_split_by_semicolons_or_newlines_and_the_last_wins()
    {
        var parsed = MemberUntil.Parse("honey@mindattic.com=2026-11-01\nhoney@mindattic.com=2026-11-30");
        Assert.That(parsed["honey@mindattic.com"], Is.EqualTo(new DateTime(2026, 11, 30).AddDays(1).AddTicks(-1)));
    }

    [Test]
    public void The_default_drops_honey_and_waffle_now_and_nobody_else()
    {
        var lapsed = MemberUntil.Lapsed(new OrchestratorSettings().MemberUntil, new DateTime(2026, 11, 11, 15, 0, 0));
        Assert.That(lapsed, Is.EquivalentTo(new[] { "honey@mindattic.com", "waffle@mindattic.com" }));
    }
}
