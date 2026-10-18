using System.Windows.Media;
using MindAttic.Orchestrator.Budget;
using MindAttic.Orchestrator.Scheduling;
using MindAttic.Orchestrator.Settings;
using MindAttic.Orchestrator.Wpf;
using MindAttic.Orchestrator.Wpf.ViewModels;
using MindAttic.Workflow.Tickets;
using NUnit.Framework;

namespace MindAttic.Orchestrator.Tests;

/// <summary>The dashboard's view-model rules (M.ORCH-T149): which groups show, which toasts stay, where the tray's
/// "show repo" lands, the tray's one line, a queue row's Where and Status, a tile's search match, and the refresh pace.</summary>
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

    // ── Autopilot line ──────────────────────────────────────────────────────

    [Test]
    public void The_queue_says_autopilot_is_on_from_the_clis_that_typed_it()
    {
        Assert.That(MainViewModel.AutopilotLine(new Dictionary<int, IReadOnlyList<string>>()), Does.StartWith("Proposals wait for you"));
        var on = MainViewModel.AutopilotLine(new Dictionary<int, IReadOnlyList<string>> { [1] = ["Prose"], [2] = ["prose", "GTFO.*"], [3] = ["*"] });
        Assert.That(on, Is.EqualTo("Autopilot is on in 3 CLIs for Prose, GTFO.*, every repo, each through its own subagents, paced to the 5-hour window."));
        Assert.That(MainViewModel.AutopilotLine(new Dictionary<int, IReadOnlyList<string>> { [1] = ["Prose"] }, on: false), Does.StartWith("Autopilot is paused"),
            "paused from the tray or Settings, the CLIs that typed it get nothing");
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

    // ── SelectRepo ──────────────────────────────────────────────────────────

    [Test]
    public void Selecting_a_shown_repo_keeps_the_search()
    {
        var a = Tile("A", RepoState.Waiting);
        var known = new Dictionary<string, RepoTileViewModel>(StringComparer.OrdinalIgnoreCase) { ["A"] = a };

        Assert.That(MainViewModel.FindRepo("A", [a], known), Is.EqualTo(((RepoTileViewModel?)a, false)));
    }

    [Test]
    public void Selecting_a_repo_the_search_hides_clears_the_search()
    {
        var a = Tile("A", RepoState.Waiting);
        var b = Tile("Bravo", RepoState.Working);
        var known = new Dictionary<string, RepoTileViewModel>(StringComparer.OrdinalIgnoreCase) { ["A"] = a, ["Bravo"] = b };
        var shown = known.Values.Where(t => t.Matches("brav")).ToList();
        Assert.That(shown, Is.EqualTo(new[] { b }), "the search hides A");

        Assert.That(MainViewModel.FindRepo("A", shown, known), Is.EqualTo(((RepoTileViewModel?)a, true)));
    }

    [Test]
    public void Selecting_an_unknown_repo_finds_nothing_and_keeps_the_search() =>
        Assert.That(MainViewModel.FindRepo("Nope", [Tile("A")], new Dictionary<string, RepoTileViewModel>()),
            Is.EqualTo(((RepoTileViewModel?)null, false)));

    // ── TraySummary ─────────────────────────────────────────────────────────

    [Test]
    public void Tray_text_names_the_budget_counts_and_the_pace_only_when_paced()
    {
        Assert.That(MainViewModel.TrayText("Idle", null, 0, 0), Is.EqualTo("Budget Idle · 0 need you · 0 working"));
        Assert.That(MainViewModel.TrayText("42%", new Pace(PaceMode.Unpaced, 4, 0, 0, null, null, null, ""), 2, 3),
            Is.EqualTo("Budget 42% · 2 need you · 3 working"));
        Assert.That(MainViewModel.TrayText("88%", new Pace(PaceMode.Throttled, 1, 100, 100, null, "5-hour", Now, "paced"), 1, 2),
            Is.EqualTo("Budget 88% · throttled · 1 need you · 2 working"));
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
}
