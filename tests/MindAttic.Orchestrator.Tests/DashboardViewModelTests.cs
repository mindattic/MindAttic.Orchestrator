using System.Windows.Media;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Setup;
using MindAttic.Orchestrator.Wpf;
using MindAttic.Orchestrator.Wpf.ViewModels;
using MindAttic.Tickets.Log;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The dashboard's view-model rules (M.ORCH-T149): which groups show, which toasts stay, where "show repo"
/// lands, a queue row's Where and Status, a tile's search match, and the refresh pace.</summary>
public sealed class DashboardViewModelTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    private static Ticket T(string id, string summary = "") => new(id, "2026-10-01", "fix", "open", summary == "" ? $"summary of {id}" : summary, "");

    private static RepoTileViewModel Tile(string name, RepoState state = RepoState.Dormant, params Ticket[] open)
    {
        var tile = new RepoTileViewModel(name);
        tile.Update(new RepoStatus(name, @"D:\" + name, state, [], [], open, Now, default), Now);
        return tile;
    }

    private static ToastViewModel Toast(string title, DateTime createdUtc, RepoTileViewModel? target = null, TimeSpan? lifetime = null) =>
        new(title, "", Brushes.Gray, target) { CreatedUtc = createdUtc, Lifetime = lifetime };

    // ── 5-hour utilization card ─────────────────────────────────────────────

    [Test]
    public void A_peak_only_5h_tile_reads_as_an_estimate_not_a_known_limit()
    {
        // M.ORCH-T636: against only the busiest block the chart beside the tile draws nothing, so the tile mustn't look like a
        // precise, known limit either: a rough "~100%", muted whatever its level, and a caption saying it's an estimate.
        _ = System.IO.Packaging.PackUriHelper.UriSchemePack; // registers pack:// outside an Application
        var day = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc);
        UsageEvent E(double hour, long output) => new(day.AddHours(hour), new TokenUsage(0, 0, 0, output));
        var peak = BudgetCalculator.Compute([E(1, 1000), E(10, 1000)], new OrchestratorSettings(), day.AddHours(11), day);
        var real = BudgetCalculator.Compute([E(1, 1000), E(10, 1000)], new OrchestratorSettings { BlockBudget = 1000 }, day.AddHours(11), day);
        Assert.That((peak.BlockLimitIsPeak, real.BlockLimitIsPeak), Is.EqualTo((true, false)));

        Assert.That(MainViewModel.BlockHeadline(peak), Is.EqualTo("~100%"));
        Assert.That(MainViewModel.BlockHeadline(real), Is.EqualTo("100.0%"), "a real limit keeps its precise figure");
        Assert.That(MainViewModel.BlockBrush(peak), Is.SameAs(Palette.Quiet), "muted, not the warn/over colour of a real limit");
        Assert.That(MainViewModel.BlockBrush(real), Is.Not.SameAs(Palette.Quiet));
        Assert.That(MainViewModel.PeakCaption, Does.StartWith("estimate"));
    }

    [Test]
    public void The_utilization_card_stays_visible_while_idle_between_windows()
    {
        // M.ORCH-T502: the card collapses only on an empty caption (MainWindow.xaml binds its Visibility to UtilizationText),
        // so idle between windows it must still have one: the last window as it ended, or just that the next is yet to start.
        var idle = new BudgetStatus(null, 0, true, 0, default, default, null, null, 0, null, 90);
        var end = Now.AddMinutes(-40);
        var last = UtilizationSeries.Build([new UsageSample(end.AddHours(-1), 64, end, 0)], idle, 99, Now);
        var caption = MainViewModel.UtilizationCaption(last, loaded: true);
        Assert.That(caption, Does.StartWith($"No 5-hour window running: the last ended at {Formatting.ResetTime(end, DateTime.UtcNow)} at 64%"));
        Assert.That(caption, Does.EndWith("the next starts with your next prompt."));
        Assert.That(Converters.EmptyToCollapsed.Convert(caption, typeof(System.Windows.Visibility), null, System.Globalization.CultureInfo.InvariantCulture),
            Is.EqualTo(System.Windows.Visibility.Visible));

        var none = MainViewModel.UtilizationCaption(UtilizationSeries.Build([], idle, 99, Now), loaded: true);
        Assert.That(none, Is.EqualTo("No 5-hour window running: the next starts with your next prompt."));
        Assert.That(MainViewModel.UtilizationCaption(null, loaded: true), Is.Not.Empty, "a window with no real limit keeps the card too");
        Assert.That(MainViewModel.UtilizationCaption(null, loaded: false), Is.Empty, "only before the first snapshot is it collapsed");
    }

    [Test]
    public void An_idle_login_draws_a_flat_line_at_its_current_percent_with_the_target_and_says_it_is_idle()
    {
        // M.ORCH-T629: nothing running, no pace of its own, the roster read 37% of a window resetting in 2 hours.
        var quiet = new Snapshot(Now, [], []);
        Assert.That(MainViewModel.IsLoginIdle(quiet), Is.True);
        var reset = Now.AddHours(2);
        var row = new AccountRow { Account = "a/o", Email = "ryan@x.com", FiveHourPercent = 37, FiveHourResetUtc = reset };
        var u = MainViewModel.IdleUtilization(quiet, row, 98, Now);
        Assert.That((u.Idle, u.Unstarted, u.StartUtc, u.EndUtc, u.NowPercent), Is.EqualTo((true, false, reset.AddHours(-5), reset, 37d)));
        Assert.That(u.Actual.Select(p => p.Percent), Is.EqualTo(new[] { 37d, 37d }), "flat across the window's span");
        Assert.That(MainViewModel.UtilizationCaption(u, loaded: true, "Ryan"),
            Does.StartWith("Idle: 37% of this 5-hour window used; nothing running on Ryan."));

        var chart = UtilizationChartData.From(u, new UtilizationChartData.ChartBrushes(Brushes.Blue, Brushes.Green, Brushes.Orange, Brushes.Red));
        Assert.That(chart.ReferenceLines, Has.Count.EqualTo(1), "the target line only");
        Assert.That(chart.Points, Has.Count.EqualTo(2));

        // Reset already, or never read: 0% over the next 5 hours from now.
        foreach (var r in new AccountRow?[] { row with { FiveHourResetUtc = Now.AddMinutes(-1) }, null })
        {
            var next = MainViewModel.IdleUtilization(quiet, r, 98, Now);
            Assert.That((next.Unstarted, next.StartUtc, next.EndUtc, next.NowPercent), Is.EqualTo((true, Now, Now.AddHours(5), 0d)));
            Assert.That(MainViewModel.UtilizationCaption(next, loaded: true, "Ryan"), Does.StartWith("Idle: 0% of this 5-hour window used; nothing running on Ryan."));
        }
    }

    [Test]
    public void A_per_cli_logins_idle_peak_block_draws_its_own_rough_percent_not_a_flat_zero()
    {
        // M.ORCH-T806: AccountRoster is only ever populated from the global ~/.claude sign-in, so a per-CLI login
        // (its own AccountDir, e.g. Waffle) always has row == null here. Idle against only a busiest-block peak (no
        // real limit known yet) must still draw the block's own rough BlockPercent, not flatline at 0%.
        _ = System.IO.Packaging.PackUriHelper.UriSchemePack; // registers pack:// outside an Application
        var day = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc);
        UsageEvent E(double hour, long output) => new(day.AddHours(hour), new TokenUsage(0, 0, 0, output));
        var peak = BudgetCalculator.Compute([E(1, 1000), E(10, 1000)], new OrchestratorSettings(), day.AddHours(11), day);
        Assert.That(peak.BlockLimitIsPeak, Is.True);
        Assert.That(peak.BlockPercent, Is.GreaterThan(0), "the block's own rough percent against the peak");

        var quiet = new Snapshot(day.AddHours(11), [], []) { Budget = peak };
        var u = MainViewModel.IdleUtilization(quiet, null, 90, day.AddHours(11));
        Assert.That(u.NowPercent, Is.EqualTo(peak.BlockPercent), "falls back to the block's rough percent, not 0, with no roster row");
    }

    [Test]
    public void A_momentary_idle_read_keeps_a_real_windows_climb_instead_of_flattening_it()
    {
        // M.ORCH-T787: seen once with a real window that climbed steadily to 96%, then a momentary idle read (nothing
        // working right that instant) flattened the whole window at 96% instead of keeping its actual climb.
        var start = Now.AddHours(-3);
        var end = Now.AddHours(2);
        var climb = new[] { new UtilizationPoint(start, 0), new UtilizationPoint(Now.AddMinutes(-10), 50), new UtilizationPoint(Now, 96) };
        var real = new Utilization(start, end, Now, 90, climb, 96, 10, 96, TimeSpan.Zero);
        var quiet = new Snapshot(Now, [], []) { Utilization = real };
        Assert.That(MainViewModel.IsLoginIdle(quiet), Is.True, "nothing is working this instant");

        var u = MainViewModel.IdleUtilization(quiet, null, 90, Now);
        Assert.That(u.Idle, Is.True);
        Assert.That(u.Actual, Is.EqualTo(climb), "the real climb, not a flat line at the current %");
        Assert.That((u.StartUtc, u.EndUtc, u.NowPercent), Is.EqualTo((start, end, 96d)));

        var chart = UtilizationChartData.From(u, new UtilizationChartData.ChartBrushes(Brushes.Blue, Brushes.Green, Brushes.Orange, Brushes.Red));
        Assert.That(chart.Points, Has.Count.EqualTo(3), "all three real readings kept, not collapsed to a two-point flat line");
    }

    // ── Repo sort (M.ORCH-T593) ─────────────────────────────────────────────

    [Test]
    public void Repo_cards_sort_by_name_ignoring_case_or_by_open_tickets_then_name()
    {
        RepoTileViewModel[] tiles = [Tile("zeta", open: T("Z-T1")), Tile("Beta", open: [T("B-T1"), T("B-T2")]),
                                     Tile("alpha", open: T("A-T1")), Tile("Gamma", open: [T("G-T1"), T("G-T2")])];

        Assert.That(MainViewModel.Sorted(tiles, MainViewModel.SortByName).Select(t => t.Name), Is.EqualTo(new[] { "alpha", "Beta", "Gamma", "zeta" }));
        Assert.That(MainViewModel.Sorted(tiles, MainViewModel.SortByTickets).Select(t => t.Name), Is.EqualTo(new[] { "Beta", "Gamma", "alpha", "zeta" }));    }

    // ── Regroup visibility ──────────────────────────────────────────────────

    [Test]
    public void Unfocused_every_non_empty_group_shows_but_quiet_only_with_show_quiet_or_a_search()
    {
        Assert.That(MainViewModel.GroupVisibility(1, 1, 1, 1, Focus.All, showQuiet: false, ""), Is.EqualTo((true, true, true, false)));
        Assert.That(MainViewModel.GroupVisibility(1, 1, 1, 1, Focus.All, showQuiet: false, "   "), Is.EqualTo((true, true, true, false)), "blank is no search");
        Assert.That(MainViewModel.GroupVisibility(1, 1, 1, 1, Focus.All, showQuiet: true, ""), Is.EqualTo((true, true, true, true)));
        Assert.That(MainViewModel.GroupVisibility(1, 1, 1, 1, Focus.All, showQuiet: false, "orch"), Is.EqualTo((true, true, true, true)));
    }

    [Test]
    public void An_empty_group_never_shows()
    {
        Assert.That(MainViewModel.GroupVisibility(0, 0, 0, 0, Focus.All, showQuiet: true, "x"), Is.EqualTo((false, false, false, false)));
        Assert.That(MainViewModel.GroupVisibility(0, 2, 0, 0, Focus.Waiting, showQuiet: true, ""), Is.EqualTo((false, false, false, false)));
    }

    [TestCase(Focus.Waiting, true, false, false)]
    [TestCase(Focus.Working, false, true, false)]
    [TestCase(Focus.Tickets, false, false, true)]
    public void A_focus_shows_only_its_own_group_and_never_quiet(Focus focus, bool waiting, bool working, bool tickets) =>
        Assert.That(MainViewModel.GroupVisibility(1, 1, 1, 1, focus, showQuiet: true, "x"), Is.EqualTo((waiting, working, tickets, false)));

    // ── Allocate line ──────────────────────────────────────────────────────

    [Test]
    public void The_queue_says_allocate_is_on_from_the_clis_that_typed_it()
    {
        Assert.That(MainViewModel.AllocateLine(new Dictionary<int, IReadOnlyList<string>>()), Does.StartWith("Proposals wait for you"));
        var on = MainViewModel.AllocateLine(new Dictionary<int, IReadOnlyList<string>> { [1] = ["Prose"], [2] = ["prose", "GTFO.*"], [3] = ["*"] });
        Assert.That(on, Is.EqualTo("Allocate is on in 3 CLIs for Prose, GTFO.*, every repo, each through its own subagents, paced to the 5-hour window."));
        Assert.That(MainViewModel.AllocateLine(new Dictionary<int, IReadOnlyList<string>> { [1] = ["Prose"] }, on: false), Does.StartWith("Allocate is paused"),
            "paused from Settings or the CLI, the CLIs that typed it get nothing");
    }

    // ── Toast pruning ───────────────────────────────────────────────────────

    [Test]
    public void A_toast_goes_after_its_lifetime_two_minutes_unless_set()
    {
        var fresh = Toast("fresh", Now.AddMinutes(-1));
        var old = Toast("old", Now.AddMinutes(-3));
        var longLived = Toast("brief", Now.AddMinutes(-10), lifetime: TimeSpan.FromMinutes(15));
        var expired = Toast("brief expired", Now.AddMinutes(-16), lifetime: TimeSpan.FromMinutes(15));
        var toasts = new List<ToastViewModel> { fresh, old, longLived, expired };

        MainViewModel.PruneToasts(toasts, Now);

        Assert.That(toasts, Is.EqualTo(new[] { fresh, longLived }));
    }

    [Test]
    public void A_toast_goes_once_its_repo_is_no_longer_waiting()
    {
        var waiting = Tile("A", RepoState.Waiting);
        var working = Tile("B", RepoState.Working);
        var quiet = Tile("C");
        var keep = Toast("A needs you", Now, waiting);
        var noTarget = Toast("Pacing", Now);
        var toasts = new List<ToastViewModel> { keep, Toast("B needs you", Now, working), noTarget, Toast("C needs you", Now, quiet) };

        MainViewModel.PruneToasts(toasts, Now);

        Assert.That(toasts, Is.EqualTo(new[] { keep, noTarget }));
    }

    [Test]
    public void At_most_four_toasts_stay_dropping_the_oldest_from_the_end()
    {
        var toasts = Enumerable.Range(1, 6).Select(i => Toast($"t{i}", Now)).ToList();
        var newest = toasts.Take(MainViewModel.MaxToasts).ToList();

        MainViewModel.PruneToasts(toasts, Now);

        Assert.That(MainViewModel.MaxToasts, Is.EqualTo(4));
        Assert.That(toasts, Is.EqualTo(newest));
    }

    // ── --show <email|session|ticket> (M.ORCH-T763) ─────────────────────────

    [Test]
    public void Show_finds_the_person_by_email_the_session_by_id_or_start_and_the_ticket_a_cli_is_on()
    {
        (string, Func<string, bool>)[] agents = [("0a1b2c3d4e5f-6789", id => id == "M.ORCH-T5"), ("ffff0000aaaa", _ => false)];
        Assert.That(MainViewModel.ShowMatches(ShowKind.Person, "Jo@X.com", "jo@x.com", agents), Is.True, "any case");
        Assert.That(MainViewModel.ShowMatches(ShowKind.Person, "jo@x.com", null, agents), Is.False);
        Assert.That(MainViewModel.ShowMatches(ShowKind.Session, "0a1b2c3d4e5f-6789", null, agents), Is.True);
        Assert.That(MainViewModel.ShowMatches(ShowKind.Session, "0A1B2C3D", null, agents), Is.True, "its first 8, as the dashboard shows it");
        Assert.That(MainViewModel.ShowMatches(ShowKind.Session, "0a1b", null, agents), Is.False, "too short to be sure");
        Assert.That(MainViewModel.ShowMatches(ShowKind.Ticket, "M.ORCH-T5", null, agents), Is.True);
        Assert.That(MainViewModel.ShowMatches(ShowKind.Ticket, "M.ORCH-T6", null, agents), Is.False);
    }

    [Test]
    public void Show_takes_only_a_well_formed_target_and_is_handed_over_beside_start()
    {
        Assert.That(DashboardRequest.ShowTarget(["--show", "jo@x.com"]), Is.EqualTo("jo@x.com"));
        Assert.That(DashboardRequest.ShowTarget(["--SHOW", " M.ORCH-T5 "]), Is.EqualTo("M.ORCH-T5"));
        Assert.That(DashboardRequest.ShowTarget(["--show"]), Is.Null);
        Assert.That(DashboardRequest.ShowTarget(["--show", "--start"]), Is.Null, "never an option");
        Assert.That(DashboardRequest.ShowTarget(["--show", "a b"]), Is.Null);
        Assert.That(DashboardRequest.ShowTarget(["--show", "x\" & calc"]), Is.Null);
        Assert.That(DashboardRequest.KindOf("jo@x.com"), Is.EqualTo(ShowKind.Person));
        Assert.That(DashboardRequest.KindOf("m.orch-t5"), Is.EqualTo(ShowKind.Ticket));
        Assert.That(DashboardRequest.KindOf("0a1b2c3d-4e5f"), Is.EqualTo(ShowKind.Session));
        Assert.That(DashboardRequest.HandOff(["--start", "Repo", "--show", "jo@x.com"]), Is.EqualTo(new[] { "--start", "Repo", "--show", "jo@x.com" }));
        Assert.That(DashboardRequest.HandOff(["--show", "jo@x.com"]), Is.EqualTo(new[] { "--show", "jo@x.com" }));
        Assert.That(DashboardRequest.HandOff(["--show", "a b", "--start", "..\\x"]), Is.Empty, "nothing malformed is passed on");
    }

    [Test]
    public void Start_from_the_command_line_takes_only_a_workspace_repo_by_its_own_name()
    {
        // M.ORCH-T767: --start <repo> starts a CLI only in a repo the workspace has; the folder and title are the workspace's.
        var a = Tile("MindAttic.Orchestrator");
        var known = new Dictionary<string, RepoTileViewModel>(StringComparer.OrdinalIgnoreCase) { [a.Name] = a };

        Assert.That(MainViewModel.StartTarget("mindattic.orchestrator", known), Is.SameAs(a));
        Assert.That(MainViewModel.StartTarget("Nope", known), Is.Null, "not in the workspace");
        Assert.That(MainViewModel.StartTarget(@"..\MindAttic.Orchestrator", known), Is.Null);
        Assert.That(MainViewModel.StartTarget("MindAttic.Orchestrator\" & calc", known), Is.Null);
        var noFolder = new RepoTileViewModel("Ghost");
        Assert.That(MainViewModel.StartTarget("Ghost", new Dictionary<string, RepoTileViewModel> { ["Ghost"] = noFolder }), Is.Null, "no folder to start in");
    }

    // ── QueueRowViewModel ───────────────────────────────────────────────────

    private static QueueItem Item(Assignment? assignment = null, bool repoBusy = false) =>
        new("A", @"D:\A", T("A-T1"), 0, assignment, repoBusy);

    private static QueueRowViewModel Row(QueueItem item, Proposal? proposal = null)
    {
        // The owner is only reached when a command runs; these tests never run one.
        var row = new QueueRowViewModel(item, null!);
        row.Update(item, 1, proposal);
        return row;
    }

    [Test]
    public void Where_follows_the_proposal_and_without_one_whether_the_repo_is_busy()
    {
        Assert.That(Row(Item()).Where, Is.EqualTo("in the repo"));
        Assert.That(Row(Item(repoBusy: true)).Where, Is.EqualTo("in a worktree"));
        var busy = Item(repoBusy: true);
        Assert.That(Row(busy, new Proposal(busy, UseWorktree: false, "")).Where, Is.EqualTo("in the repo"), "the proposal wins");
        var idle = Item();
        Assert.That(Row(idle, new Proposal(idle, UseWorktree: true, "")).Where, Is.EqualTo("in a worktree"));
    }

    [Test]
    public void Status_says_blocked_before_stale_before_too_big_before_assigned()
    {
        var assignment = new Assignment("A-T1", "A", @"D:\A", "Claude", Now.AddHours(-1), Worktree: true);
        var all = Item(assignment) with
        {
            BlockedBy = ["A-T0", "B-T2"], StaleSinceUtc = DateTime.UtcNow.AddHours(-3), TooBig = true,
            Estimate = new CostEstimate(2_000_000, 3, "fix"),
        };

        Assert.That(Row(all).Status, Is.EqualTo("Waits for A-T0, B-T2"));
        Assert.That(Row(all with { BlockedBy = [] }).Status, Does.StartWith("Stale: quiet 3h"));
        var notStale = all with { BlockedBy = [], StaleSinceUtc = null };
        Assert.That(Row(notStale).Status, Does.StartWith("Too big for what's left (").And.EndWith("): split it or wait for the reset"));
        Assert.That(Row(notStale with { TooBig = false }).Status, Does.StartWith("Claude since ").And.EndWith(" · worktree"));
        Assert.That(Row(notStale with { TooBig = false, Assignment = assignment with { Worktree = false } }).Status, Does.Not.Contain("worktree"));
    }

    [Test]
    public void An_unassigned_row_says_next_up_then_repo_busy_then_old_with_reserve_and_estimate()
    {
        var item = Item(repoBusy: true) with { Old = true, Reserved = 500_000, Estimate = new CostEstimate(1_200_000, 2, "fix") };

        Assert.That(Row(item, new Proposal(item, true, "top")).Status, Is.EqualTo("Next up · 500k reserved · ~1.2M"));
        Assert.That(Row(item).Status, Is.EqualTo("Repo busy · 500k reserved · ~1.2M"));
        Assert.That(Row(item with { RepoBusy = false }).Status, Is.EqualTo("Old: groom it? · 500k reserved · ~1.2M"));
        Assert.That(Row(Item()).Status, Is.Empty);
    }

    // ── RepoTileViewModel.Matches ───────────────────────────────────────────

    [Test]
    public void A_tile_matches_blank_its_name_or_an_open_tickets_id_or_summary_ignoring_case()
    {
        var tile = Tile("MindAttic.Orchestrator", RepoState.Dormant, T("M.ORCH-T149", "dashboard view models"));

        Assert.That(tile.Matches(""), Is.True);
        Assert.That(tile.Matches("  "), Is.True);
        Assert.That(tile.Matches("orchestrator"), Is.True, "name");
        Assert.That(tile.Matches("m.orch-t149"), Is.True, "ticket id");
        Assert.That(tile.Matches("VIEW MODELS"), Is.True, "ticket summary");
        Assert.That(tile.Matches("gtfo"), Is.False);
        Assert.That(new RepoTileViewModel("Bare").Matches("x"), Is.False, "no status yet: only the name can match");
    }

    // ── MainWindow refresh pace ─────────────────────────────────────────────

    [TestCase(false, false, 5)]
    [TestCase(false, true, 5)]
    [TestCase(true, true, 15)]
    [TestCase(true, false, 60)]
    public void Shown_refreshes_every_5s_hidden_every_15s_holding_the_lease_else_60s(bool hidden, bool holdsLease, int seconds) =>
        Assert.That(MainWindow.RefreshInterval(hidden, holdsLease), Is.EqualTo(TimeSpan.FromSeconds(seconds)));

    // ── The status bar's warnings ───────────────────────────────────────────

    [Test]
    public void The_status_bar_counts_every_warning_without_calling_it_a_ticket_file_problem()
    {
        // M.ORCH-T465: a billing or approvals warning read "1 ticket file problem(s)".
        Assert.That(MainViewModel.WarningLine(1), Is.EqualTo("1 problem reading the workspace, hover for details"));
        Assert.That(MainViewModel.WarningLine(3), Is.EqualTo("3 problems reading the workspace, hover for details"));

        var root = TestContext.CurrentContext.TestDirectory;
        while (!Path.Exists(Path.Combine(root, ".git"))) root = Path.GetDirectoryName(root) ?? throw new AssertionException("no repo root above the tests");
        var xaml = File.ReadAllText(Path.Combine(root, "src", "MindAttic.Orchestrator.Wpf", "MainWindow.xaml"));
        Assert.That(xaml, Does.Contain("{Binding WarningsText").And.Not.Contain("ticket file problem"));
    }

    // ── A change made while a refresh is built ──────────────────────────────

    [Test]
    public async Task A_snapshot_built_across_a_billing_switch_or_bump_is_built_again_not_applied()
    {
        var changes = 0;
        var builds = 0;
        // The billing switch lands while the first build is reading the old mode.
        var mode = await MainViewModel.BuiltAfterChanges(() =>
        {
            builds++;
            var read = changes == 0 ? "team" : "api";
            if (builds == 1) changes++;
            return Task.FromResult(read);
        }, () => changes);
        Assert.That((mode, builds), Is.EqualTo(("api", 2)));

        builds = 0;
        Assert.That(await MainViewModel.BuiltAfterChanges(() => Task.FromResult(++builds), () => changes), Is.EqualTo(1), "nothing changed: built once");

        builds = 0;
        Assert.That(await MainViewModel.BuiltAfterChanges(() => { changes++; return Task.FromResult(++builds); }, () => changes, tries: 3), Is.EqualTo(3),
            "changes that never stop still give a snapshot, after a few tries");
    }
}
