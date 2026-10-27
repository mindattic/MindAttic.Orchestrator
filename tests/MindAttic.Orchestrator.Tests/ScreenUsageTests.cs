using MindAttic.Orchestrator.Budget;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>Claude's usage figures read off a hosted CLI's screen (M.ORCH-T1118).</summary>
public sealed class ScreenUsageTests
{
    // 10:00 in Chicago (CDT, UTC-5).
    private static readonly DateTime Now = new(2026, 10, 27, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FiveReset = new(2026, 10, 27, 19, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime WeekReset = new(2026, 10, 29, 21, 0, 0, DateTimeKind.Utc);

    private const string Sample = """
        Current session
        ████░░░  0% used
        Resets 2:30pm (America/Chicago)

        Current week (all models)
        █████░  87% used
        Resets Oct 29, 4pm (America/Chicago)

        Current week (Fable)
        ██░░░░  12% used
        Resets Oct 30, 9am (America/Chicago)
        """;

    [Test]
    public void The_usage_screen_gives_the_session_and_the_week_with_their_resets_in_UTC()
    {
        var r = ScreenUsage.Parse(Sample, Now);
        Assert.That(r, Is.Not.Null);
        Assert.That(r!.FiveHour, Is.EqualTo(new ReportedWindow(0, FiveReset)));
        Assert.That(r.Week, Is.EqualTo(new ReportedWindow(87, WeekReset)), "the all-models week, not Fable's");
        Assert.That(r.AtUtc, Is.EqualTo(Now));
    }

    [Test]
    public void Escapes_and_cursor_moves_are_read_through()
    {
        const string e = "\u001b";
        var text = $"{e}[2K{e}[1A{e}[G{e}[1mCurrent{e}[1Csession{e}[22m\r\n{e}[38;2;215;119;87m████{e}[39m{e}[2m░░░{e}[22m  0{e}[2m%{e}[22m used\r\n" +
                   $"{e}]0;claude{e}\\{e}[2mResets 2:30pm (America/Chicago){e}[22m\r\n\r\n" +
                   $"{e}[1mCurrent week (all models){e}[22m\r\n█████░  87% used\r\n{e}[2mResets Oct 29, 4pm (America/Chicago){e}[22m\r\n";
        var r = ScreenUsage.Parse(text, Now);
        Assert.That(r?.FiveHour, Is.EqualTo(new ReportedWindow(0, FiveReset)));
        Assert.That(r?.Week, Is.EqualTo(new ReportedWindow(87, WeekReset)));
    }

    [Test]
    public void The_session_block_alone_gives_the_session_only()
    {
        var r = ScreenUsage.Parse("Current session\n██░ 41% used\nResets 2:30pm (America/Chicago)\n", Now);
        Assert.That(r?.FiveHour, Is.EqualTo(new ReportedWindow(41, FiveReset)));
        Assert.That(r?.Week, Is.Null);
    }

    [Test]
    public void A_block_whose_figures_are_not_drawn_yet_gives_nothing()
    {
        Assert.That(ScreenUsage.Parse("Current session\n████", Now), Is.Null);
        // Its percent mustn't be borrowed from the next block.
        var r = ScreenUsage.Parse("Current session\nCurrent week (all models)\n█ 87% used\nResets Oct 29, 4pm (America/Chicago)", Now);
        Assert.That(r?.FiveHour, Is.Null);
        Assert.That(r?.Week?.Percent, Is.EqualTo(87));
    }

    [Test]
    public void A_stale_screen_whose_resets_are_past_or_too_far_ahead_gives_nothing()
    {
        // A week whose reset is already past.
        Assert.That(ScreenUsage.Parse("Current week (all models)\n 87% used\nResets Oct 26, 4pm (America/Chicago)", Now), Is.Null);
        // A session reset at 9am, an hour ago: its next one is tomorrow, further than a 5-hour window.
        Assert.That(ScreenUsage.Parse("Current session\n 3% used\nResets 9am (America/Chicago)", Now), Is.Null);
    }

    [Test]
    public void Resets_read_with_or_without_minutes_and_a_date()
    {
        Assert.That(ScreenUsage.ResetUtc("2:30pm", "America/Chicago", Now), Is.EqualTo(FiveReset));
        Assert.That(ScreenUsage.ResetUtc("Oct 29, 4pm", "America/Chicago", Now), Is.EqualTo(WeekReset));
        Assert.That(ScreenUsage.ResetUtc("Oct 29 at 4pm", "America/Chicago", Now), Is.EqualTo(WeekReset));
        Assert.That(ScreenUsage.ResetUtc("12am", "UTC", Now), Is.EqualTo(new DateTime(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc)));
        Assert.That(ScreenUsage.ResetUtc("Oct 29, 4pm", "Nowhere/Atlantis", Now), Is.Null);
    }

    [Test]
    public void A_chunk_split_mid_block_is_read_once_the_rest_arrives()
    {
        var found = new List<ReportedUsage>();
        using var scanner = new ScreenUsageScanner(found.Add, () => Now);
        var at = Sample.IndexOf("87%", StringComparison.Ordinal) + 1;
        scanner.Feed(Sample[..at]);
        scanner.Scan();
        scanner.Feed(Sample[at..]);
        scanner.Scan();
        Assert.That(found, Has.Count.EqualTo(1));
        Assert.That(found[0].Week, Is.EqualTo(new ReportedWindow(87, WeekReset)));
        Assert.That(found[0].FiveHour, Is.EqualTo(new ReportedWindow(0, FiveReset)));

        // The same screen drawn again is not a new reading.
        scanner.Feed(Sample);
        scanner.Scan();
        Assert.That(found, Has.Count.EqualTo(1));
    }

    [Test]
    public void A_scraped_reading_never_replaces_a_newer_one()
    {
        const string me = "acct/org";
        var was = new ReportedUsage(new ReportedWindow(12.4, FiveReset), new ReportedWindow(86, WeekReset), Now.AddMinutes(-3)) { Account = me };
        var screen = new ReportedUsage(new ReportedWindow(12, FiveReset.AddSeconds(20)), new ReportedWindow(87, WeekReset), Now);

        var merged = ScreenUsage.Merge(was, screen, me, Now);
        Assert.That(merged, Is.Not.Null);
        Assert.That(merged!.FiveHour, Is.EqualTo(was.FiveHour), "a lower percent of the same window keeps the meter's");
        Assert.That(merged.Week, Is.EqualTo(new ReportedWindow(87, WeekReset)));
        Assert.That(merged.Account, Is.EqualTo(me));

        // Nothing higher: nothing to write.
        Assert.That(ScreenUsage.Merge(was with { Week = new ReportedWindow(88, WeekReset) }, screen, me, Now), Is.Null);
        // A different window from one still running keeps the running one.
        Assert.That(ScreenUsage.Merge(was, screen with { FiveHour = null, Week = new ReportedWindow(90, WeekReset.AddDays(-7)) }, me, Now), Is.Null);
        // Another account's reading is no reading of this one's.
        Assert.That(ScreenUsage.Merge(was with { Account = "other" }, screen, me, Now)?.FiveHour?.Percent, Is.EqualTo(12));
    }

    [Test]
    public void A_screen_reading_is_written_in_the_probes_format_with_its_own_source()
    {
        var root = Path.Combine(Path.GetTempPath(), "screen-usage-" + Guid.NewGuid().ToString("N"));
        try
        {
            var dirs = new MindAttic.Orchestrator.Sessions.AccountDirs(root);
            var dir = dirs.For("amy@example.com");
            Directory.CreateDirectory(dir.Directory);
            File.WriteAllText(dir.ConfigPath, """{"oauthAccount":{"accountUuid":"a1","organizationUuid":"o1"}}""");
            var now = DateTime.UtcNow;
            var screen = new ReportedUsage(null, new ReportedWindow(87, now.AddDays(2)), now);

            var written = ScreenUsage.Record("amy@example.com", dir.Directory, screen, now, dirs, tell: false);
            Assert.That(written?.Week?.Percent, Is.EqualTo(87));
            var json = File.ReadAllText(dir.UsagePath);
            Assert.That(json, Does.Contain("\"source\":\"screen\"").And.Contain("\"account\":\"a1/o1\""));
            Assert.That(ReportedUsage.Load(now, dir.UsagePath)?.Week?.Percent, Is.EqualTo(87));

            // An unknown login is never written.
            Assert.That(ScreenUsage.Record("nobody@example.com", null, screen, now, dirs, tell: false), Is.Null);
            Assert.That(ScreenUsage.Record(null, @"C:\somewhere\else", screen, now, dirs, tell: false), Is.Null);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch (IOException) { }
        }
    }
}
