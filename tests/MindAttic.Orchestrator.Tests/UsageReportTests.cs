using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Sessions;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The usage report (M.ORCH-T685): one row per member from the freshest reading there is, stale past 15 minutes
/// (raised to the transcripts' estimate when there's a real limit), the spend of the CLIs on each login and the API key's,
/// the lines under the table, and every format it prints or writes.</summary>
public sealed class UsageReportTests
{
    private static readonly DateTime Now = new(2026, 10, 23, 18, 0, 0, DateTimeKind.Utc);
    private static DateOnly Today => DateOnly.FromDateTime(Now.ToLocalTime());

    private static Member M(string email, string? name) => new() { Email = email, Name = name };

    private static LoginReading Reading(string email, string account, DateTime at, double five, DateTime fiveReset, double week, DateTime weekReset) =>
        new(email, account, null, new ReportedUsage(new ReportedWindow(five, fiveReset), new ReportedWindow(week, weekReset), at));

    private static UsageReport Build(IReadOnlyList<Member> members, IReadOnlyList<AccountRow>? roster = null, IReadOnlyList<LoginReading>? logins = null,
        IReadOnlyList<DayUsage>? history = null, IReadOnlyDictionary<string, BudgetStatus>? estimates = null, DateOnly? since = null,
        string? global = null, Func<string, string?>? staleReason = null) =>
        UsageReport.Build(members, roster ?? [], logins ?? [], history ?? [], estimates ?? new Dictionary<string, BudgetStatus>(), 90, Now, since, global,
            staleReason: staleReason);

    /// <summary>M.ORCH-T695: with no 5-hour window running none has started, so nothing is running or wasted: "—".</summary>
    [Test]
    public void A_fresh_reading_with_no_5_hour_window_running_shows_a_dash()
    {
        var report = Build([M("erin@x.com", "Erin")],
            logins: [new LoginReading("erin@x.com", "erin-key", null, new ReportedUsage(null, new ReportedWindow(91, Now.AddDays(4)), Now.AddMinutes(-1)))]);
        var cells = report.Cells()[0];
        Assert.That((cells[1], cells[2]), Is.EqualTo(("—", "—")));
        Assert.That(report.Rows[0].NoFiveHourWindow, Is.True);
    }

    /// <summary>M.ORCH-T688: the reset dates alone didn't say how long is left.</summary>
    [TestCase(26 * 60 + 14, "1d 2h 14m")]
    [TestCase(4 * 24 * 60 + 19 * 60 + 22, "4d 19h 22m")]
    [TestCase(19 * 60 + 22, "19h 22m")]
    [TestCase(14, "14m")]
    [TestCase(24 * 60, "1d 0h 0m")]
    [TestCase(0, "now")]
    public void Week_resets_in_counts_down_in_days_hours_and_minutes(int minutes, string expected)
    {
        var now = new DateTime(2026, 10, 23, 18, 0, 0, DateTimeKind.Utc);
        Assert.That(UsageReport.Countdown(now.AddMinutes(minutes), now), Is.EqualTo(expected));
        Assert.That(UsageReport.Countdown(null, now), Is.EqualTo("—"));
    }

    [Test]
    public void Each_member_gets_the_freshest_reading_with_what_is_left_and_whether_it_is_still_accurate()
    {
        var report = Build([M("amy@x.com", "Amy"), M("erin@x.com", "Erin")],
            roster: [new AccountRow { Account = "erin-key", Email = "erin@x.com", FiveHourPercent = 40, FiveHourResetUtc = Now.AddHours(-1),
                WeekPercent = 94, WeekResetUtc = Now.AddDays(3), ReadUtc = Now.AddHours(-3) }],
            logins: [Reading("amy@x.com", "amy-key", Now.AddMinutes(-3), 48, Now.AddHours(1), 70, Now.AddDays(5))],
            global: "erin-key");

        var amy = report.Rows.Single(r => r.Email == "amy@x.com");
        Assert.That((amy.FiveHourUsed, amy.WeekUsed, amy.WeekLeft, amy.LeftToTarget, amy.Stale, amy.Source),
            Is.EqualTo(((double?)48, (double?)70, (double?)30, (double?)20, false, UsageReport.Meter)));
        var erin = report.Rows.Single(r => r.Email == "erin@x.com");
        Assert.That(erin.Stale, Is.True, "three hours old is past 15 minutes");
        Assert.That(erin.FiveHourUnknown, Is.True, "the 5-hour window read has reset since: unknown, not 0%");
        Assert.That(erin.IsGlobalLogin, Is.True);

        var cells = report.Cells();
        Assert.That(report.Headers, Is.EqualTo(new[] { "Member", "5-hour used", "5h resets", "Week left", "Week resets in", "Last updated" }));
        Assert.That(cells[0][0], Is.EqualTo("Amy"));
        Assert.That(cells[0][5], Is.EqualTo("3m ago"));
        Assert.That(cells[0], Has.Count.EqualTo(report.Headers.Count));
        Assert.That(report.Rows[0].LeftToTarget, Is.EqualTo(20).Within(0.5), "the points left stay in the data and the notes, not a column");
        Assert.That(cells[1][0], Is.EqualTo("Erin"), "the fallback login is named like any member");
        Assert.That(cells[1][1], Is.EqualTo("unknown"));
        Assert.That(report.Headers, Has.None.StartsWith("Left to"));
        Assert.That(report.Rows[1].Stale, Is.True, "still stale, said in the note under the table");
    }

    [Test]
    public void A_stale_reading_is_raised_to_the_estimate_from_the_transcripts_when_there_is_a_real_limit()
    {
        var block = new UsageBlock(Now.AddHours(-2), Now.AddHours(3), default, Now);
        var budget = new BudgetStatus(block, 1_000_000, false, 61, default, default, 10_000_000, 33, 0, null, 90)
        {
            BlockLimitSource = LimitSource.Reported,
            WeeklyLimitSource = LimitSource.Reported,
        };
        var report = Build([M("amy@x.com", "Amy")],
            logins: [Reading("amy@x.com", "amy-key", Now.AddMinutes(-40), 50, Now.AddHours(3), 30, Now.AddDays(4))],
            estimates: new Dictionary<string, BudgetStatus> { ["amy-key"] = budget });

        var amy = report.Rows.Single();
        Assert.That((amy.FiveHourUsed, amy.WeekUsed, amy.Source, amy.Stale), Is.EqualTo(((double?)61, (double?)33, UsageReport.Estimated, true)));

        var fresh = Build([M("amy@x.com", "Amy")],
            logins: [Reading("amy@x.com", "amy-key", Now.AddMinutes(-5), 50, Now.AddHours(3), 30, Now.AddDays(4))],
            estimates: new Dictionary<string, BudgetStatus> { ["amy-key"] = budget }).Rows.Single();
        Assert.That((fresh.FiveHourUsed, fresh.Source), Is.EqualTo(((double?)50, UsageReport.Meter)), "a fresh meter reading is taken as it is");
    }

    [Test]
    public void The_spend_is_each_logins_own_today_this_week_and_since_a_day_and_the_api_keys_apart()
    {
        DayUsage Day(int daysAgo, long fresh, string? account) => new(Today.AddDays(-daysAgo), "Repo", Providers.Claude, fresh, account);
        var report = Build([M("amy@x.com", "Amy")],
            logins: [Reading("amy@x.com", "amy-key", Now, 10, Now.AddHours(4), 10, Now.AddDays(4))], // the week started 3 days ago
            history: [Day(0, 100, "amy-key"), Day(2, 50, "amy-key"), Day(5, 7, "amy-key"), Day(0, 9, "other-key"),
                Day(0, 30, UsageHistory.BilledElsewhere + "api-key"), Day(3, 20, UsageHistory.BilledElsewhere + "api-key"),
                new DayUsage(Today, "Repo", Providers.Codex, 1000, "amy-key")],
            since: Today.AddDays(-6));

        var amy = report.Rows.Single();
        Assert.That((amy.CliToday, amy.CliWeek, amy.CliSince), Is.EqualTo((100L, 150L, (long?)157)));
        Assert.That((report.ApiKeyToday, report.ApiKeyWeek, report.ApiKeySince), Is.EqualTo((30L, 50L, (long?)50)));
        Assert.That(amy.OutsideUse, Is.Null);
        Assert.That(report.DetailCells()[0][^1], Is.EqualTo(UsageReport.NotMeasured), "no outside use measured for this login");
        Assert.That(report.DetailHeaders, Does.Contain($"CLIs since {Today.AddDays(-6):yyyy-MM-dd}"));
    }

    [Test]
    public void Outside_use_is_what_the_daemon_measured_for_the_login()
    {
        Assert.That(UsageReport.OutsideText(new OutsideUseStats("a@x.com", "Amy", false, 0, 0, 0, null, 5, null)), Is.Null, "nothing measured yet");
        Assert.That(UsageReport.OutsideText(new OutsideUseStats("a@x.com", "Amy", false, 12, 3, 2, 6.5, 5, 8)),
            Is.EqualTo("3% a window, 6.5% a week on average (12 windows)"));
        Assert.That(UsageReport.OutsideText(new OutsideUseStats("", "Erin", true, 1, 12, 0, null, 5, null, HeldTo: 80)),
            Is.EqualTo("using Claude now; 12% a window on average (1 window)"));
    }

    [Test]
    public void A_login_here_that_is_not_a_member_still_gets_a_row_after_the_members()
    {
        var report = Build([M("amy@x.com", "Amy")], logins: [new LoginReading("zed@x.com", "zed-key", "Zeddy", null)]);
        Assert.That(report.Rows.Select(r => (r.Name, r.IsMember)), Is.EqualTo(new[] { ("Amy", true), ("Zeddy", false) }));
        Assert.That(report.Cells()[1][0], Is.EqualTo("Zeddy (not a member)"));
        Assert.That(report.Cells()[1][5], Is.EqualTo("never"));
    }

    [Test]
    public void The_lines_under_the_table_say_who_is_at_the_target_who_pacing_limits_who_has_room_and_why_a_reading_is_stale()
    {
        var report = Build([M("a@x.com", "Ann"), M("b@x.com", "Bob"), M("c@x.com", "Cat")],
            logins:
            [
                Reading("a@x.com", "a", Now, 10, Now.AddHours(1), 92, Now.AddDays(2)),
                Reading("b@x.com", "b", Now, 10, Now.AddHours(1), 80, Now.AddDays(2)),
                Reading("c@x.com", "c", Now.AddHours(-1), 10, Now.AddHours(1), 20, Now.AddDays(2)),
            ],
            staleReason: e => e == "c@x.com" ? "its login here isn't signed in" : null);

        var notes = report.Notes();
        Assert.That(notes, Has.Count.EqualTo(4));
        Assert.That(notes[0], Does.StartWith("At or past their weekly cutoff").And.Contain("Ann (90%; " + UsageReport.When(Now.AddDays(2), Now) + ", in 2d 0h 0m)"));
        Assert.That(notes[1], Does.StartWith("Cutoffs").And.Contain("Bob 90% / 98%"));
        Assert.That(notes, Has.None.Contain("75%"), "no take-in line");
        Assert.That(notes[2], Does.StartWith("Plenty of room").And.Contain("Cat (70 pts to the target)"));
        Assert.That(notes[3], Does.Contain("Cat (read 1hr ago): its login here isn't signed in"));
    }

    [Test]
    public void Resets_points_and_ages_read_the_way_people_say_them()
    {
        var localNoon = new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Local).ToUniversalTime();
        Assert.That(UsageReport.When(localNoon.AddHours(2).AddMinutes(20), localNoon), Is.EqualTo("2:20 PM today"));
        Assert.That(UsageReport.When(new DateTime(2026, 10, 30, 9, 0, 0, DateTimeKind.Local).ToUniversalTime(), localNoon), Is.EqualTo("Fri 10/30, 9:00 AM"));
        Assert.That(UsageReport.ToTarget(82), Is.EqualTo("82 pts"));
        Assert.That(UsageReport.ToTarget(1), Is.EqualTo("1 pt"));
        Assert.That(UsageReport.ToTarget(0.2), Is.EqualTo("at target"));
        Assert.That(UsageReport.ToTarget(-4), Is.EqualTo("over by 4"));
        Assert.That(UsageReport.Age(Now.AddMinutes(-3), Now), Is.EqualTo("3m ago"));
        Assert.That(UsageReport.Age(Now.AddMinutes(-125), Now), Is.EqualTo("2hr 5m ago"));
        Assert.That(UsageReport.Age(null, Now), Is.EqualTo("never"));
    }

    [Test]
    public void Every_format_carries_the_same_rows()
    {
        var report = Build([M("amy@x.com", "Amy <A|B>")],
            logins: [Reading("amy@x.com", "amy-key", Now.AddMinutes(-3), 48, Now.AddHours(1), 70, Now.AddDays(5))]);

        var text = report.PlainText();
        Assert.That(text, Does.StartWith("Member").And.Contain("Amy <A|B>").And.Contain("20 pts"));
        Assert.That(report.PlainText(detail: true), Does.Contain("Outside use").And.Contain("API key"));
        Assert.That(report.Markdown(), Does.Contain("| **Amy <A\\|B>** |").And.Contain("## Spend"));
        Assert.That(report.Html(), Does.Contain("Amy &lt;A|B&gt;").And.Not.Contain("<script"));
        var csv = report.Csv().Split('\n');
        Assert.That(csv[0], Does.StartWith("member,email,"));
        Assert.That(csv[1], Does.StartWith("Amy <A|B>,amy@x.com,true,false,48,"));
        Assert.That(csv[2], Does.StartWith("API key,"));
        using var json = System.Text.Json.JsonDocument.Parse(report.Json());
        var row = json.RootElement.GetProperty("members")[0];
        Assert.That(row.GetProperty("weekLeft").GetDouble(), Is.EqualTo(30));
        Assert.That(row.GetProperty("outsideUse").ValueKind, Is.EqualTo(System.Text.Json.JsonValueKind.Null));
        Assert.That(json.RootElement.GetProperty("notes").GetArrayLength(), Is.GreaterThan(0));
    }

    [Test]
    public void The_report_file_is_html_or_markdown_by_its_extension_and_the_dashboard_writes_both()
    {
        var dir = Path.Combine(Path.GetTempPath(), "usage-report-" + Guid.NewGuid().ToString("N"));
        try
        {
            var report = Build([M("amy@x.com", "Amy")]);
            var html = Wpf.ViewModels.MainViewModel.WriteUsageReport(report, Path.Combine(dir, "reports"));
            Assert.That(File.ReadAllText(html), Does.StartWith("<!doctype html>"));
            Assert.That(File.ReadAllText(Path.ChangeExtension(html, ".md")), Does.StartWith("# Usage report"));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
    }

    [Test, NonParallelizable] // UsageReportCommand.Override and Console.Out are process-wide
    public void Orchestrator_report_prints_the_table_csv_or_json_and_writes_the_markdown_to_share()
    {
        var dir = Path.Combine(Path.GetTempPath(), "usage-report-cli-" + Guid.NewGuid().ToString("N"));
        var previousOut = Console.Out;
        DateOnly? asked = null;
        UsageReportCommand.Override = since =>
        {
            asked = since;
            return Build([M("amy@x.com", "Amy")], logins: [Reading("amy@x.com", "amy-key", Now.AddMinutes(-3), 48, Now.AddHours(1), 70, Now.AddDays(5))], since: since);
        };
        try
        {
            string Run(params string[] args)
            {
                var output = new StringWriter();
                Console.SetOut(output);
                Assert.That(CliApp.Create().Run(args), Is.Zero, output.ToString());
                return output.ToString();
            }
            Assert.That(Run("report"), Does.StartWith("Member").And.Contain("Amy").And.Not.Contain("Outside use"));
            Assert.That(Run("report", "--detail", "--since", "2026-10-01"), Does.Contain("Outside use").And.Contain("CLIs since 2026-10-01"));
            Assert.That(asked, Is.EqualTo(new DateOnly(2026, 10, 1)));
            Assert.That(Run("report", "--csv"), Does.StartWith("member,email,"));
            Assert.That(Run("report", "--json"), Does.StartWith("{"));
            var md = Path.Combine(dir, "usage.md");
            Run("report", "--md", md);
            Assert.That(File.ReadAllText(md), Does.Contain("**Amy**"));
            Console.SetOut(new StringWriter());
            // Refused as a typing mistake, which the entry point says in one line.
            Assert.Throws<Spectre.Console.Cli.CommandRuntimeException>(() => CliApp.Create().Run(["report", "--since", "yesterday-ish"]));
        }
        finally
        {
            Console.SetOut(previousOut);
            UsageReportCommand.Override = null;
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public void The_meter_probes_trouble_is_why_a_reading_is_stale()
    {
        var dir = Path.Combine(Path.GetTempPath(), "usage-report-db-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var db = new Settings.OrchestratorDb(Path.Combine(dir, "state.db"));
            db.SaveState(AccountPacer.ProbeStateName, """{"a@x.com":{"skipped":"no credentials"},"b@x.com":{"failuresInRow":2,"lastResult":"401"},"c@x.com":{"failuresInRow":0}}""");
            var notes = UsageReport.ProbeNotes(db);
            Assert.That(notes["a@x.com"], Is.EqualTo("the meter probe isn't asking: no credentials"));
            Assert.That(notes["b@x.com"], Is.EqualTo("the meter probe failed 2 times in a row: 401"));
            Assert.That(notes.ContainsKey("c@x.com"), Is.False);
            Assert.That(UsageReport.ProbeNotes(null), Is.Empty);
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
    }
}
