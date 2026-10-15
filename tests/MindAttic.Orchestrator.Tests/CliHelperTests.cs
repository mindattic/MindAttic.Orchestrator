using MindAttic.Orchestrator.Budget;
using MindAttic.Workflow.Tickets;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The CLI's pure helpers: what settings typed at the command line parse through, and the budget lines it prints.</summary>
public sealed class CliHelperTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 14, 0, 0, DateTimeKind.Utc);

    [TestCase("20000000", 20_000_000L)]
    [TestCase("20M", 20_000_000L)]
    [TestCase("20m", 20_000_000L)]
    [TestCase("1.5B", 1_500_000_000L)]
    [TestCase("500k", 500_000L)]
    [TestCase(" 2,500,000 ", 2_500_000L)]
    [TestCase("13_000_000", 13_000_000L)]
    [TestCase("0", 0L)]
    public void Token_counts_read_plain_scaled_and_grouped(string text, long expected)
    {
        Assert.That(TokenText.TryParse(text, out var value), Is.True, text);
        Assert.That(value, Is.EqualTo(expected));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("-5")]
    [TestCase("-2M")]
    [TestCase("M")]
    [TestCase("lots")]
    [TestCase("5X")]
    [TestCase("NaN")]
    [TestCase("Infinity")]
    public void Token_counts_refuse_empty_negative_and_junk(string text)
    {
        Assert.That(TokenText.TryParse(text, out var value), Is.False, text);
        Assert.That(value, Is.Zero);
    }

    [Test]
    public void Ticket_ids_typed_in_any_case_are_stored_as_the_scheduler_compares_them()
    {
        // `bump mao-t5` said "priority is now +1" and changed nothing: the scheduler compares ids ordinally, upper case.
        Assert.That(Ctx.Id(" mao-t5 "), Is.EqualTo("MAO-T5"));
    }

    [Test]
    public void Status_lists_working_repos_first_then_the_most_recent_then_the_most_tickets_then_by_name()
    {
        RepoStatus R(string name, RepoState state, int minutesAgo = -1, int tickets = 0) => new(name, "", state, [], [],
            [.. Enumerable.Range(1, tickets).Select(i => new Ticket($"{name}-T{i}", "2026-10-01", "fix", "open", "x", ""))],
            minutesAgo < 0 ? null : Now.AddMinutes(-minutesAgo), default);

        var ordered = StatusCommand.Order([
            R("dormant-many", RepoState.Dormant, tickets: 5), R("waiting-old", RepoState.Waiting, 20), R("working", RepoState.Working, 1),
            R("waiting-new", RepoState.Waiting, 5), R("dormant-b", RepoState.Dormant, tickets: 1), R("Dormant-a", RepoState.Dormant, tickets: 1),
        ]);

        Assert.That(ordered.Select(r => r.Name), Is.EqualTo(new[] { "working", "waiting-new", "waiting-old", "dormant-many", "Dormant-a", "dormant-b" }));
    }

    [Test]
    public void Report_weeks_start_on_monday()
    {
        var monday = new DateOnly(2026, 10, 12);
        foreach (var offset in Enumerable.Range(0, 7))
            Assert.That(ReportCommand.WeekOf(monday.AddDays(offset)), Is.EqualTo(monday), monday.AddDays(offset).DayOfWeek.ToString());
        Assert.That(ReportCommand.WeekOf(new DateOnly(2026, 10, 11)), Is.EqualTo(new DateOnly(2026, 10, 5)), "Sunday ends the week before");
    }

    private static BudgetStatus B(double percent, long limit = 20_000_000, bool peak = false, LimitSource source = LimitSource.Manual) =>
        new(new UsageBlock(Now.AddHours(-2), Now.AddHours(3), new TokenUsage((long)(limit * percent / 100), 0, 0, 0), Now), limit, peak, percent,
            default, new TokenUsage(4_000_000, 0, 0, 0), null, null, 0, null, 80) { BlockLimitSource = source, WeekStartUtc = Now.AddDays(-3) };

    private static string T(DateTime utc) => $"{utc.ToLocalTime():t}";

    [Test]
    public void The_budget_line_for_no_block_peak_manual_learned_reported_and_at_the_limit()
    {
        var reset = T(Now.AddHours(3));
        Assert.That(BudgetCommand.Line(B(10) with { CurrentBlock = null }), Is.EqualTo("[grey]budget: no active 5-hour block[/]"));
        Assert.That(BudgetCommand.Line(B(40, peak: true, source: LimitSource.Peak)),
            Is.EqualTo($"[aqua]this block is at 40% of your heaviest block in 7 days[/] (8M uncached this block, resets {reset}) [grey]· no budget set: `orchestrator budget --block <tokens>`[/]"));
        Assert.That(BudgetCommand.Line(B(50)), Is.EqualTo($"[green]budget: 50% of your 5h budget[/] (10M of 20M; 10M uncached this block, resets {reset})"));
        Assert.That(BudgetCommand.Line(B(85, source: LimitSource.Learned)), Does.StartWith("[yellow]budget: 85% of your learned 5h limit[/]"), "past the warn line");
        Assert.That(BudgetCommand.Line(B(30, source: LimitSource.Reported)), Does.StartWith("[green]budget: 30% of your 5h limit (Claude's count)[/]"));

        var until = Now.AddDays(2);
        Assert.That(BudgetCommand.Line(B(30) with { AtLimitUntilUtc = until, AtLimitKind = LimitKind.Weekly }),
            Does.StartWith("[red]budget: 30%").And.EndWith($" [red]· at the weekly limit until {T(until)}[/]"), "behind a wall: red whatever the percent");
        Assert.That(BudgetCommand.Line(B(30) with { AtLimitUntilUtc = until, AtLimitKind = LimitKind.Session }), Does.Contain("at the 5-hour limit"));
    }

    [Test]
    public void The_week_line_says_since_the_reset_or_that_it_rolls()
    {
        var start = Now.AddDays(-3);
        var next = Now.AddDays(4);
        Assert.That(BudgetCommand.WeekLine(B(10) with { WeekResetUtc = next, WeeklyPercent = 42 }),
            Is.EqualTo($"this week (since {start.ToLocalTime():ddd h:mm tt}) 4M uncached (42% of weekly) · resets {next.ToLocalTime():ddd h:mm tt}"));
        Assert.That(BudgetCommand.WeekLine(B(10)),
            Is.EqualTo("last 7 days 4M uncached [grey]· rolling: set the real reset with --weekly-reset \"Mon 9:00\"[/]"));
    }

    [Test]
    public void The_limits_line_names_where_each_limit_came_from()
    {
        Assert.That(BudgetCommand.SourceLine(B(10, peak: true, source: LimitSource.Peak)), Is.EqualTo("5h none · weekly none"), "peak-relative isn't a limit");
        Assert.That(BudgetCommand.SourceLine(B(10) with { WeeklyLimit = 90_000_000, WeeklyLimitSource = LimitSource.Reported }),
            Is.EqualTo("5h 20M (set) · weekly 90M (from Claude's own usage percent)"));

        var hits = new LimitCalibration([
            new LimitObservation(LimitKind.Session, Now.AddDays(-2), Now.AddDays(-2).AddHours(5), 13_000_000),
            new LimitObservation(LimitKind.Weekly, Now.AddDays(-9), Now.AddDays(-2), 80_000_000),
            new LimitObservation(LimitKind.Weekly, Now.AddDays(-16), Now.AddDays(-9), 84_000_000),
        ]);
        Assert.That(BudgetCommand.SourceLine(B(10, limit: 13_000_000, source: LimitSource.Learned) with
            { Calibration = hits, WeeklyLimit = 82_000_000, WeeklyLimitSource = LimitSource.Learned }),
            Is.EqualTo("5h 13M (learned from 1 limit hits) · weekly 82M (learned from 2 limit hits)"));
        Assert.That(BudgetCommand.SourceLine(B(10, peak: true, source: LimitSource.Peak) with { Calibration = hits, BlockLimitOutdated = true }),
            Is.EqualTo("5h relearning (already past the 13M learned earlier, with no wall) · weekly none"));
    }
}
