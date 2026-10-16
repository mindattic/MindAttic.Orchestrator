using MindAttic.Orchestrator.Scheduling;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

public sealed class QuietHoursTests
{
    private static DateTime At(int hour, int minute) => new(2026, 10, 14, hour, minute, 0);

    [TestCase("22:00-07:00", 22, 0, 7, 0)]
    [TestCase("12:00-13:00", 12, 0, 13, 0)]
    [TestCase(" 09:30 - 10:45 ", 9, 30, 10, 45)]
    public void A_readable_window_parses_into_from_and_to(string text, int fh, int fm, int th, int tm)
    {
        Assert.That(QuietHours.TryParse(text, out var w), Is.True);
        Assert.That(w.From, Is.EqualTo(new TimeOnly(fh, fm)));
        Assert.That(w.To, Is.EqualTo(new TimeOnly(th, tm)));
    }

    [TestCase("07:00-07:00")]
    [TestCase("junk")]
    [TestCase("abc-def")]
    [TestCase("25:00-07:00")]
    [TestCase("22:00-25:00")]
    [TestCase("22:00")]
    [TestCase("22:00-")]
    [TestCase("-07:00")]
    [TestCase("")]
    public void An_unreadable_window_is_rejected(string text)
    {
        Assert.That(QuietHours.TryParse(text, out var w), Is.False);
        Assert.That(w, Is.EqualTo(default((TimeOnly, TimeOnly))));
    }

    [TestCase(21, 59, false)]
    [TestCase(22, 0, true)]
    [TestCase(23, 59, true)]
    [TestCase(0, 0, true)]
    [TestCase(6, 59, true)]
    [TestCase(7, 0, false)]
    [TestCase(12, 0, false)]
    public void A_window_across_midnight_wraps(int hour, int minute, bool quiet) =>
        Assert.That(QuietHours.Active(["22:00-07:00"], At(hour, minute)),
            quiet ? Is.EqualTo("22:00-07:00") : Is.Null);

    [TestCase(11, 59, false)]
    [TestCase(12, 0, true)]
    [TestCase(12, 59, true)]
    [TestCase(13, 0, false)]
    public void A_same_day_window_includes_its_start_and_excludes_its_end(int hour, int minute, bool quiet) =>
        Assert.That(QuietHours.Active(["12:00-13:00"], At(hour, minute)),
            quiet ? Is.EqualTo("12:00-13:00") : Is.Null);

    [TestCase(23, 0, "22:00-07:00")]
    [TestCase(12, 30, "12:00-13:00")]
    [TestCase(17, 15, "17:00-17:30")]
    [TestCase(15, 0, null)]
    public void Of_several_windows_the_one_now_falls_in_is_named(int hour, int minute, string? expected) =>
        Assert.That(QuietHours.Active(["22:00-07:00", "12:00-13:00", "17:00-17:30"], At(hour, minute)),
            Is.EqualTo(expected));

    [Test]
    public void Overlapping_windows_name_the_first_listed() =>
        Assert.That(QuietHours.Active(["12:00-14:00", "13:00-15:00"], At(13, 30)), Is.EqualTo("12:00-14:00"));

    [Test]
    public void Unreadable_windows_are_skipped_not_fatal()
    {
        Assert.That(QuietHours.Active(["junk", "07:00-07:00", "25:00-26:00", "12:00-13:00"], At(12, 15)),
            Is.EqualTo("12:00-13:00"));
        Assert.That(QuietHours.Active(["junk"], At(12, 15)), Is.Null);
    }

    [Test]
    public void No_windows_means_never_quiet() =>
        Assert.That(QuietHours.Active([], At(23, 0)), Is.Null);
}
