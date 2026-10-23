using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using MindAttic.Tickets.Log;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>M.ORCH-T666: weekly-aware pacing, per login: from 75% of its week each 5-hour window is capped so the week lands at 98%
/// as it resets, and a ticket a login can't fit goes to another login with room, else the fallback login (the API key).</summary>
public sealed class WeeklyAwarePacingTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 6, 0, 0, DateTimeKind.Utc);
    private const long Limit = 10_000_000;

    private static readonly OrchestratorSettings On = new() { WeeklyAwarePacing = true, WorktreeRoot = @"D:\WT" };
    private static readonly OrchestratorSettings Off = new() { WorktreeRoot = @"D:\WT" };

    private string dir = "";
    private JsonStore store = null!;

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "orch-weekly-aware-" + Guid.NewGuid().ToString("N"));
        store = new JsonStore(Path.Combine(dir, "store"));
        opened.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>A login's budget by Claude's meter: <paramref name="fivePercent"/> of a 10M 5-hour window that began an hour ago,
    /// the week at <paramref name="weekPercent"/> resetting in <paramref name="weekResetHours"/>.</summary>
    private static BudgetStatus Budget(double fivePercent, double weekPercent, double weekResetHours)
    {
        var start = Now.AddHours(-1);
        var used = (long)(fivePercent / 100 * Limit);
        var block = new UsageBlock(start, start + BudgetCalculator.BlockLength, new TokenUsage(used, 0, 0, 0), Now);
        return new BudgetStatus(block, Limit, false, fivePercent, default, default, null, weekPercent, used, null, 80)
        {
            BlockLimitSource = LimitSource.Reported,
            WeekResetUtc = Now.AddHours(weekResetHours),
            WeekStartUtc = Now.AddHours(weekResetHours - 24 * 7),
        };
    }

    /// <summary>Readings in two 5-hour windows: the 5 hours rose 20 and 15 points while the week rose 2 and 1.</summary>
    private static List<UsageSample> Readings()
    {
        var week = Now.AddDays(2);
        UsageSample S(double hoursAgo, double five, DateTime reset, double weekPct) =>
            new(Now.AddHours(-hoursAgo), five, reset, 0) { WeekPercent = weekPct, WeekResetUtc = week };
        var first = Now.AddHours(-6);
        var second = Now.AddHours(4);
        return
        [
            S(10, 10, first, 70), S(8, 20, first, 71), S(7, 30, first, 72),
            S(1, 5, second, 72), S(0.5, 15, second, 73), S(0.2, 20, second, 73),
        ];
    }

    [Test]
    public void The_ratio_is_learned_from_readings_where_both_percents_rose_together()
    {
        Assert.That(WeeklyRatio.Learn(Readings()), Is.EqualTo(3.0 / 35).Within(1e-9), "3 week points over 20 + 15 of the 5 hours");
        var still = Readings().Select(r => r with { WeekPercent = 72 }).ToList();
        Assert.That(WeeklyRatio.Learn(still), Is.Null, "the 5 hours rose but the week never did: not read as a free week");
        Assert.That(WeeklyRatio.Learn([new UsageSample(Now, 10, Now.AddHours(2), 0), new UsageSample(Now.AddMinutes(5), 40, Now.AddHours(2), 0)]),
            Is.Null, "no week percent kept: not learned, so pacing stays 5-hour only");
    }

    [Test]
    public void A_week_under_75_percent_changes_nothing()
    {
        var b = Budget(fivePercent: 40, weekPercent: 60, weekResetHours: 30);
        var cap = WeeklyPacing.Cap(b, 0.1, On, Now);
        var on = Pacer.Compute(b, 1, 100_000, On, Now, week: cap);
        var off = Pacer.Compute(b, 1, 100_000, Off, Now);

        Assert.That(cap!.Capped, Is.False);
        Assert.That((on.Mode, on.SessionCap, on.SustainablePerHour, on.RoomTokens, on.Stop, on.WindDown, on.SubagentAllowance, on.FitRoomTokens),
            Is.EqualTo((off.Mode, off.SessionCap, off.SustainablePerHour, off.RoomTokens, off.Stop, off.WindDown, off.SubagentAllowance, off.RoomTokens)));
        Assert.That(on.Note, Does.StartWith(off.Note).And.Contain("Weekly-aware: the week is at 60%, under its 75% line"), "the note says it's on");
    }

    [Test]
    public void A_tight_week_past_75_percent_caps_the_window_so_the_week_lands_at_98_percent()
    {
        // Week at 90%, resetting in 20 hours: 5 windows from this one's start. The window has cost the week 1 point (10% × 0.1),
        // so 98 − 89 = 9 week points over 5 windows is 1.8 each: 18% of this window.
        var b = Budget(fivePercent: 10, weekPercent: 90, weekResetHours: 20);
        var cap = WeeklyPacing.Cap(b, 0.1, On, Now)!;
        var p = Pacer.Compute(b, 1, 100_000, On, Now, week: cap);

        Assert.That((cap.Capped, cap.WindowsLeft), Is.EqualTo((true, 5)));
        Assert.That(cap.CeilingPercent, Is.EqualTo(18).Within(1e-9));
        Assert.That(p.RoomTokens, Is.EqualTo(0.08 * Limit).Within(1), "room to the ceiling, not to 98%");
        Assert.That(p.WeeklyRoomTokens, Is.EqualTo(0.8 * Limit).Within(1), "the week's own 8 points are 80 points of the 5 hours");
        Assert.That(p.Stop, Is.False);
        Assert.That(p.Note, Does.Contain("capped by the week: 18% of this window"));
        Assert.That(cap.Short, Is.EqualTo("Capped by the week: 18% of this window"));

        var past = Budget(fivePercent: 18.5, weekPercent: 90.85, weekResetHours: 20);
        var spent = Pacer.Compute(past, 1, 100_000, On, Now, week: WeeklyPacing.Cap(past, 0.1, On, Now));
        Assert.That(spent.Stop, Is.True, "at its ceiling the window stops, as at its target");
        Assert.That(spent.Note, Does.Contain("at its 18% ceiling"));

        var loose = WeeklyPacing.Cap(Budget(fivePercent: 10, weekPercent: 80, weekResetHours: 100), 0.005, On, Now)!;
        Assert.That(loose.Capped, Is.False, "a week with room for every window's 98% doesn't hold this one");
        Assert.That(WeeklyPacing.Cap(b, null, On, Now)!.Capped, Is.False, "no ratio learned yet: 5-hour pacing only");
    }

    [Test]
    public void The_week_winds_down_at_95_and_stops_at_98_in_place_of_the_reserve()
    {
        Assert.That((On.WeeklyStopPercent, Off.WeeklyStopPercent), Is.EqualTo((98, 95)));
        var b = Budget(fivePercent: 5, weekPercent: 96, weekResetHours: 3);
        var p = Pacer.Compute(b, 1, 100_000, On, Now, week: WeeklyPacing.Cap(b, 0.1, On, Now));
        Assert.That((p.WindDown, p.Stop), Is.EqualTo((true, false)));
        Assert.That(p.WindDownHold, Does.StartWith("the weekly window is past 95% of its limit"));
    }

    private static Ticket T(string id) => new(id, "2026-10-01", "fix", "open", $"summary of {id}", "");

    /// <summary>The calling CLI's plan: its login's week at 96% (winding down), with A-T1 estimated big and A-T2 small.</summary>
    private SchedulePlan Plan96(OrchestratorSettings s)
    {
        var b = Budget(fivePercent: 10, weekPercent: 96, weekResetHours: 30);
        var pace = Pacer.Compute(b, 1, 100_000, s, Now, week: WeeklyPacing.Cap(b, 0.1, s, Now));
        var snap = new Snapshot(Now, [new RepoStatus("A", @"D:\W\A", RepoState.Dormant, [], [], [T("A-T1"), T("A-T2")], null, default)], []) { Pace = pace };
        var plan = Scheduler.Plan(snap, s, []);
        return plan with
        {
            Queue = [.. plan.Queue.Select(q => q with { Estimate = new CostEstimate(q.Id == "A-T1" ? 5_000_000 : 100_000, 3, "fixes") })],
        };
    }

    private readonly List<(string Ticket, string? To)> opened = [];

    private string? Open(QueueItem item, LoginRoom to)
    {
        opened.Add((item.Id, to.Email));
        store.UpdateAssignments(all => all.Add(new Assignment(item.Id, item.Repo, $@"D:\WT\A\{item.Id}", "Claude", Now, true)));
        return null;
    }

    private static (bool, string, string) Make(string repo, string path, string branch) => (true, path, "");

    private (IReadOnlyList<Autopilot.Handout> Handed, string? Why) Next(OrchestratorSettings s, IReadOnlyList<LoginRoom> logins) =>
        Autopilot.Next(Plan96(s), s, store, ["A"], null, claim: true, Now, Make, _ => "main", stillOpen: (_, _) => true, pilotRunning: _ => false,
            logins: logins, redelegate: Open, login: "alice@x.com", canFallBack: true);

    [Test]
    public void A_week_at_96_percent_redelegates_a_big_ticket_to_a_login_with_room()
    {
        var full = new LoginRoom("full@x.com", @"C:\acc\full", 1_000_000, 60, true);
        var roomy = new LoginRoom("roomy@x.com", @"C:\acc\roomy", 8_000_000, 40, true);
        var stopped = new LoginRoom("stopped@x.com", @"C:\acc\stopped", 9_000_000, 90, false);

        var (handed, _) = Next(On, [full, roomy, stopped]);

        var big = handed.Single(h => h.Item.Id == "A-T1");
        Assert.That(big.DelegatedTo?.Email, Is.EqualTo("roomy@x.com"), "the roomiest login it fits: full has more headroom but not 5M of room, stopped isn't open");
        Assert.That(big.Delegated, Does.Contain("redelegated from alice@x.com to roomy@x.com").And.Contain("week at 96%"));
        Assert.That(handed.Single(h => h.Item.Id == "A-T2").DelegatedTo?.Email, Is.EqualTo("full@x.com"),
            "nothing new starts on a login winding down: the small one goes where it fits too, one per login");
        Assert.That(opened, Is.EquivalentTo(new[] { ("A-T1", (string?)"roomy@x.com"), ("A-T2", (string?)"full@x.com") }));
        Assert.That(handed.All(h => h.Prompt is null && h.Worktree is null), "neither is this CLI's to start");
        Assert.That(Redelegation.Load(store)["A-T1"], Is.EqualTo(big.Delegated), "the dashboard's queue row says where it went and why");
    }

    [Test]
    public void With_no_login_with_room_it_falls_back_to_the_api_key()
    {
        var full = new LoginRoom("full@x.com", @"C:\acc\full", 1_000_000, 60, true);

        var (handed, _) = Next(On, [full]);

        var big = handed.Single(h => h.Item.Id == "A-T1");
        Assert.That(big.DelegatedTo!.IsFallback, Is.True);
        Assert.That(big.Delegated, Does.StartWith("fell back to the fallback login (API key)"));
        Assert.That(opened, Has.Member(("A-T1", (string?)null)), "a tab on the fallback login (~/.claude) works it");
        Assert.That(handed.Single(h => h.Item.Id == "A-T2").DelegatedTo?.Email, Is.EqualTo("full@x.com"));
        Assert.That((Redelegation.FallbackHeld(store, Now.AddMinutes(1)), Redelegation.FallbackHeld(store, Now.AddMinutes(4))), Is.EqualTo((true, false)),
            "the next one waits out the hold rather than piling tabs on the fallback login");
    }

    [Test]
    public void A_ticket_that_fits_its_own_login_isnt_redelegated()
    {
        // The week at 80%: the window is capped, but A-T2's 100k fits what's left; A-T1's 5M doesn't.
        var b = Budget(fivePercent: 10, weekPercent: 80, weekResetHours: 30);
        var pace = Pacer.Compute(b, 1, 100_000, On, Now, week: WeeklyPacing.Cap(b, 0.1, On, Now)) with { SubagentAllowance = 4, Subagents = 0 };
        var snap = new Snapshot(Now, [new RepoStatus("A", @"D:\W\A", RepoState.Dormant, [], [], [T("A-T1"), T("A-T2")], null, default)], []) { Pace = pace };
        var plan = Scheduler.Plan(snap, On, []);
        plan = plan with { Queue = [.. plan.Queue.Select(q => q with { Estimate = new CostEstimate(q.Id == "A-T1" ? 5_000_000 : 100_000, 3, "fixes") })] };
        Assert.That(pace.FitRoomTokens, Is.LessThan(5_000_000).And.GreaterThan(100_000));

        var (handed, _) = Autopilot.Next(plan, On, store, ["A"], null, claim: true, Now, Make, _ => "main", stillOpen: (_, _) => true,
            pilotRunning: _ => false, logins: [new LoginRoom("roomy@x.com", @"C:\acc\roomy", 8_000_000, 40, true)], redelegate: Open, login: "alice@x.com", canFallBack: true);

        Assert.That(handed.Single(h => h.Item.Id == "A-T2").DelegatedTo, Is.Null, "this CLI's own subagent works it");
        Assert.That(handed.Single(h => h.Item.Id == "A-T1").DelegatedTo?.Email, Is.EqualTo("roomy@x.com"));
    }

    [Test]
    public void A_ticket_assigned_to_a_user_isnt_redelegated()
    {
        // M.ORCH-T668: A-T1 is pinned to alice, whose week is at 96%: it waits for her rather than going to roomy or the API key.
        var plan = Plan96(On);
        plan = plan with { Queue = [.. plan.Queue.Select(q => q.Id == "A-T1" ? q with { Assignee = "alice@x.com" } : q)] };

        var (handed, _) = Autopilot.Next(plan, On, store, ["A"], null, claim: true, Now, Make, _ => "main", stillOpen: (_, _) => true,
            pilotRunning: _ => false, logins: [new LoginRoom("roomy@x.com", @"C:ccoomy", 8_000_000, 40, true)], redelegate: Open,
            login: "alice@x.com", canFallBack: true);

        Assert.That(handed.Select(h => h.Item.Id), Does.Not.Contain("A-T1"), "a pinned ticket waits for its user");
        Assert.That(opened.Select(o => o.Ticket), Is.EqualTo(new[] { "A-T2" }), "the unassigned one is still redelegated");

        // "Redelegate now" drops the pin: then it's handed on like any unassigned ticket.
        var unpinned = plan with { Queue = [.. plan.Queue.Where(q => q.Id == "A-T1").Select(q => q with { Assignee = null })] };
        var (after, _) = Autopilot.Next(unpinned, On, store, ["A"], null, claim: true, Now, Make, _ => "main", stillOpen: (_, _) => true,
            pilotRunning: _ => false, logins: [new LoginRoom("roomy@x.com", @"C:ccoomy", 8_000_000, 40, true)], redelegate: Open,
            login: "alice@x.com", canFallBack: true);
        Assert.That(after.Single().DelegatedTo?.Email, Is.EqualTo("roomy@x.com"));
    }

    [Test]
    public void The_toggle_off_changes_nothing()
    {
        var b = Budget(fivePercent: 10, weekPercent: 96, weekResetHours: 30);
        Assert.That(WeeklyPacing.Cap(b, 0.1, Off, Now), Is.Null);
        var p = Pacer.Compute(b, 1, 100_000, Off, Now, week: WeeklyPacing.Cap(b, 0.1, Off, Now));
        Assert.That((p.Weekly, p.WeeklyRoomTokens, p.WindDown, p.Stop), Is.EqualTo(((WeeklyCap?)null, (double?)null, false, false)));
        Assert.That(p.Note, Does.Not.Contain("Weekly-aware"));

        var (handed, why) = Next(Off, [new LoginRoom("roomy@x.com", @"C:\acc\roomy", 8_000_000, 40, true)]);
        Assert.That(opened, Is.Empty, "nothing is redelegated");
        Assert.That(handed.All(h => h.DelegatedTo is null));
        Assert.That(Redelegation.Load(store), Is.Empty);
    }
}
