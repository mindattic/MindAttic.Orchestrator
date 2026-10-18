using System.Globalization;
using MindAttic.Orchestrator.Activity;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class FormattingTests
{
    [TestCase(0, "0")]
    [TestCase(999, "999")]
    [TestCase(1_000, "1k")]
    [TestCase(1_500, "1.5k")]
    [TestCase(999_949, "999.9k")]
    [TestCase(999_999, "1M")]
    [TestCase(1_000_000, "1M")]
    [TestCase(1_550_000, "1.6M")]
    [TestCase(999_999_999, "1B")]
    [TestCase(1_500_000_000, "1.5B")]
    public void Tokens_rounds_to_the_unit_it_lands_in_with_invariant_digits(long n, string expected)
    {
        var was = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE"); // a decimal comma must not leak in
            Assert.That(Formatting.Tokens(n), Is.EqualTo(expected));
        }
        finally { CultureInfo.CurrentCulture = was; }
    }

    [TestCase(59, "now")]
    [TestCase(60, "1m")]
    [TestCase(59 * 60, "59m")]
    [TestCase(60 * 60, "1h")]
    [TestCase(23 * 3600, "23h")]
    [TestCase(24 * 3600, "1d")]
    [TestCase(3 * 86400 + 5, "3d")]
    public void Span_steps_up_at_each_boundary(int seconds, string expected) =>
        Assert.That(Formatting.Span(TimeSpan.FromSeconds(seconds)), Is.EqualTo(expected));

    [Test]
    public void Ago_is_empty_for_nothing_bare_for_now_and_suffixed_otherwise()
    {
        var now = new DateTime(2026, 10, 15, 4, 0, 0, DateTimeKind.Utc);
        Assert.That(Formatting.Ago(null, now), Is.Empty);
        Assert.That(Formatting.Ago(now.AddSeconds(-10), now), Is.EqualTo("now"));
        Assert.That(Formatting.Ago(now.AddMinutes(-5), now), Is.EqualTo("5m ago"));
        Assert.That(Formatting.Ago(now.AddDays(-2), now), Is.EqualTo("2d ago"));
    }

    [Test]
    public void A_reset_days_off_names_its_weekday_and_one_today_only_its_time()
    {
        // 3:00 PM local, whatever zone runs the tests.
        var now = new DateTime(2026, 10, 15, 15, 0, 0, DateTimeKind.Local).ToUniversalTime();
        string Local(DateTime utc, string format) => utc.ToLocalTime().ToString(format, System.Globalization.CultureInfo.InvariantCulture);
        Assert.That(Formatting.ResetTime(now.AddHours(3), now), Is.EqualTo(Local(now.AddHours(3), "h:mm tt")));
        Assert.That(Formatting.ResetTime(now.AddDays(3), now), Is.EqualTo(Local(now.AddDays(3), "ddd h:mm tt")), "a weekly wall's reset, not 'today'");
        Assert.That(Formatting.ResetTime(now.AddHours(23), now), Is.EqualTo(Local(now.AddHours(23), "ddd h:mm tt")),
            "tomorrow's 2:00 PM, under a day off, isn't today's 2:00 PM (M.ORCH-T403)");
    }
}
